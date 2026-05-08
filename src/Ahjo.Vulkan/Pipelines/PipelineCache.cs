using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// A <c>VkPipelineCache</c>. Persists pipeline state across program runs
/// so subsequent pipeline builds can reuse driver-compiled binaries
/// instead of recompiling from SPIR-V — typically the largest single
/// engine startup win after asset preload.
/// </summary>
/// <remarks>
/// <para><b>Lifecycle.</b> Create once at startup
/// (<see cref="Device.LoadOrCreatePipelineCache"/>), wire into every
/// <see cref="GraphicsPipelineBuilder"/> /
/// <see cref="ComputePipelineBuilder"/> via the <c>WithCache</c> overload,
/// then <see cref="Save"/> on shutdown. The driver merges newly-compiled
/// pipelines into the cache as they're built; nothing else to do.</para>
/// <para><b>Header validation.</b> <see cref="Device.LoadOrCreatePipelineCache"/>
/// inspects the on-disk header (vendor / device ID / cache UUID) before
/// feeding the data to <c>vkCreatePipelineCache</c>; a mismatch (the
/// user copied a cache from another machine, or the driver was updated
/// and the UUID rotated) is logged to stderr and the cache starts empty.
/// The driver also rejects a mismatched cache internally; the wrapper
/// check exists for the diagnostic message.</para>
/// </remarks>
public readonly unsafe struct PipelineCache : IVulkanHandle<PipelineCache>, IDisposable
{
    public readonly VkPipelineCache_T*  Handle;
    internal readonly VkDevice_T*       DeviceHandle;

    internal PipelineCache(VkPipelineCache_T* handle, VkDevice_T* device)
    {
        Handle       = handle;
        DeviceHandle = device;
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_PIPELINE_CACHE;
    public static PipelineCache FromRaw(nint handle) => new((VkPipelineCache_T*)handle, null);
    public ulong RawHandle => (ulong)Handle;
    public bool IsNull => Handle == null;

    /// <summary>
    /// Reads the cache contents via <c>vkGetPipelineCacheData</c> and
    /// writes them to <paramref name="path"/> atomically (write-then-rename
    /// so a crash mid-write can't leave a torn cache file behind that
    /// the next run would treat as authoritative).
    /// </summary>
    /// <remarks>
    /// Two calls are made into the driver: the first sizes the buffer,
    /// the second fills it. Cache size grows with the number of
    /// pipelines built; <see cref="ArrayPool{Byte}"/> covers the common
    /// case (a few KB to a few MB) without churning the LOH.
    /// </remarks>
    public void Save(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (Handle == null)
            throw new InvalidOperationException("PipelineCache.Save called on a null handle.");

        nuint size = 0;
        Vk.vkGetPipelineCacheData(DeviceHandle, Handle, &size, null).ThrowIfFailed();
        if (size == 0)
        {
            // Nothing to persist (no pipelines built yet, or the driver
            // declined to populate). Write a zero-byte file so the next
            // run still finds something — the loader treats short reads
            // as "no usable cache" and starts fresh.
            WriteAtomic(path, ReadOnlySpan<byte>.Empty);
            return;
        }
        if (size > int.MaxValue)
            throw new InvalidOperationException(
                $"Pipeline cache exceeds 2 GiB ({size} bytes); refusing to allocate.");

        int len = (int)size;
        byte[] buf = ArrayPool<byte>.Shared.Rent(len);
        try
        {
            fixed (byte* p = buf)
                Vk.vkGetPipelineCacheData(DeviceHandle, Handle, &size, p).ThrowIfFailed();
            WriteAtomic(path, buf.AsSpan(0, (int)size));
        }
        finally { ArrayPool<byte>.Shared.Return(buf); }
    }

    /// <summary>
    /// Wraps <c>vkMergePipelineCaches</c>. Folds <paramref name="sources"/>
    /// into <c>this</c> cache. Useful for engines that maintain per-thread
    /// caches and merge them at frame boundaries; not needed for the
    /// single-cache flow.
    /// </summary>
    public void Merge(ReadOnlySpan<PipelineCache> sources)
    {
        if (Handle == null)
            throw new InvalidOperationException("PipelineCache.Merge called on a null destination handle.");
        if (sources.IsEmpty) return;

        Span<nint> raw = stackalloc nint[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i].IsNull)
                throw new ArgumentException(
                    $"PipelineCache.Merge: source[{i}] is null.", nameof(sources));
            raw[i] = (nint)sources[i].Handle;
        }

        fixed (nint* p = raw)
            Vk.vkMergePipelineCaches(DeviceHandle, Handle, (uint)sources.Length, (VkPipelineCache_T**)p)
                .ThrowIfFailed();
    }

    public void Dispose()
    {
        if (Handle == null) return;
        if (DeviceHandle == null) return; // FromRaw — borrowed handle.
        Vk.vkDestroyPipelineCache(DeviceHandle, Handle, null);
    }

    private static void WriteAtomic(string path, ReadOnlySpan<byte> bytes)
    {
        // Write to a sibling temp file, then rename onto the target.
        // File.Move(overwrite: true) is the closest .NET ships to a
        // POSIX rename on Windows — atomic per the underlying
        // MoveFileEx(MOVEFILE_REPLACE_EXISTING) call.
        string tmp = path + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            if (!bytes.IsEmpty) fs.Write(bytes);
        }
        File.Move(tmp, path, overwrite: true);
    }
}
