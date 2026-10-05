using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
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
        HandleRegistry.TrackCreate(this);
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_PIPELINE_CACHE;
    public static PipelineCache FromRaw(nint handle) => new((VkPipelineCache_T*)handle, null);
    public ulong RawHandle => (ulong)Handle;
    public bool IsNull => Handle == null;

    /// <inheritdoc/>
    public bool OwnsHandle => DeviceHandle != null;

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
    /// <para>Concurrent saves of one path resolve to last-writer-wins. On
    /// Windows, replacing the file is retried for up to 2 seconds while
    /// another handle holds it (another process saving, a reader, an
    /// antivirus scan); after that the original exception propagates and
    /// the previously saved file is left intact.</para>
    /// </remarks>
    public void Save(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (Handle == null)
            throw new InvalidOperationException("PipelineCache.Save called on a null handle.");

        // Two-call query (size → fill) wrapped in a retry loop. The cache can
        // grow between the two calls whenever another thread is still building
        // pipelines — per-thread caches + Merge is an advertised pattern (see
        // Merge's doc comment) — and the driver then writes a partial buffer
        // and returns VK_INCOMPLETE. Persisting that truncated blob would
        // poison the next run's cache load, so we re-query the now-larger size
        // and refill rather than writing a short buffer (issue #97). The fill
        // call is a multi-success command, so it uses ThrowIfErrored (throws
        // only on an actual error code) and we branch on VK_INCOMPLETE
        // ourselves (issue #117).
        while (true)
        {
            nuint size = 0;
            Vk.vkGetPipelineCacheData(DeviceHandle, Handle, &size, null).ThrowIfErrored();
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
                nuint written = size;
                VkResult result;
                fixed (byte* p = buf)
                    result = Vk.vkGetPipelineCacheData(DeviceHandle, Handle, &written, p);
                result.ThrowIfErrored();
                if (result == VkResult.VK_INCOMPLETE)
                    continue; // Cache grew between the two calls; re-query and refill.
                WriteAtomic(path, buf.AsSpan(0, (int)written));
                return;
            }
            finally { ArrayPool<byte>.Shared.Return(buf); }
        }
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
        if (!OwnsHandle) return; // FromRaw — borrowed handle.
        HandleRegistry.TrackDispose(this);
        Vk.vkDestroyPipelineCache(DeviceHandle, Handle, null);
    }

    private const int RenameBudgetMilliseconds = 2_000;
    private const int MaxRenameBackoffMilliseconds = 50;
    private const int ErrorSharingViolationHResult = unchecked((int)0x80070020);

    // Serializes in-process renames only, and is never held across a backoff
    // sleep (#237). A process-wide gate unrelated to any handle — not a
    // handle-keyed side table of the kind #118 removed.
    private static readonly Lock RenameGate = new();

    // Test seam (InternalsVisibleTo): the atomic write is the wrapper's half
    // of #230; a driverless concurrency test drives it directly since Save
    // needs a live VkPipelineCache handle. The three-argument overload exists
    // so tests can drive the rename retry budget without waiting out the
    // default.
    internal static void WriteAtomic(string path, ReadOnlySpan<byte> bytes)
        => WriteAtomic(path, bytes, TimeSpan.FromMilliseconds(RenameBudgetMilliseconds));

    internal static void WriteAtomic(string path, ReadOnlySpan<byte> bytes, TimeSpan renameBudget)
    {
        // Write to a per-writer temp sibling, then rename onto the target.
        // The temp name is unique per process + thread, so two concurrent
        // savers of one cache path no longer collide on a fixed sibling —
        // FileShare.None turned the second into an IOException (#230).
        // On failure the destination is untouched, so the previously
        // published cache survives.
        string tmp = $"{path}.{Environment.ProcessId}-{Environment.CurrentManagedThreadId}.tmp";
        try
        {
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                if (!bytes.IsEmpty) fs.Write(bytes);
            }
            PublishByRename(tmp, path, renameBudget);
        }
        catch
        {
            // A failed write or rename must not leave a stray temp sibling
            // behind. File.Delete no-ops on a missing file; a best-effort
            // cleanup failure is swallowed so the original error surfaces.
            try { File.Delete(tmp); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    private static void PublishByRename(string tmp, string path, TimeSpan budget)
    {
        // File.Move(overwrite: true) is the atomic publish step — a
        // MoveFileEx(MOVEFILE_REPLACE_EXISTING) on Windows, rename(2) on
        // POSIX. On Windows the replace fails while ANY handle is open on the
        // destination, whatever its share mode (UnauthorizedAccessException),
        // or while the temp is held (IOException, ERROR_SHARING_VIOLATION).
        // POSIX rename(2) has neither failure mode, so nothing is retried
        // there.
        //
        // In-process renames are serialized by RenameGate; that removed every
        // retry in an 8-writer probe (#237). The time budget covers what a
        // lock cannot see: other processes, in-process readers
        // (Device.LoadOrCreatePipelineCache opens the file FileShare.Read),
        // and scanners. The budget is time-based because Thread.Sleep's real
        // duration depends on the OS timer tick, so an attempt count does not
        // determine a duration. A permanent ERROR_ACCESS_DENIED (read-only
        // file, a directory at the path) cannot be told apart from a
        // transient one and waits out the budget before throwing.
        //
        // The gate covers only File.Move; the sleep runs after the lock
        // statement has released it. The exception filters run before that
        // release, so ShouldRetryRename must stay a pure check.
        long start = Stopwatch.GetTimestamp();
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                lock (RenameGate)
                    File.Move(tmp, path, overwrite: true);
                return;
            }
            catch (UnauthorizedAccessException e) when (ShouldRetryRename(e, start, budget)) { }
            catch (IOException e) when (ShouldRetryRename(e, start, budget)) { }
            Thread.Sleep(RenameBackoffMilliseconds(attempt, start, budget));
        }
    }

    internal static bool IsTransientReplaceFailure(Exception e)
        => OperatingSystem.IsWindows()
           && (e is UnauthorizedAccessException
               || (e is IOException && e.HResult == ErrorSharingViolationHResult));

    private static bool ShouldRetryRename(Exception e, long start, TimeSpan budget)
        => IsTransientReplaceFailure(e) && Stopwatch.GetElapsedTime(start) < budget;

    private static int RenameBackoffMilliseconds(int attempt, long start, TimeSpan budget)
    {
        // Doubling backoff 1, 2, 4, …, capped at MaxRenameBackoffMilliseconds;
        // the shift count is capped first so it cannot overflow.
        int delay = Math.Min(1 << Math.Min(attempt - 1, 6), MaxRenameBackoffMilliseconds);
        double remaining = (budget - Stopwatch.GetElapsedTime(start)).TotalMilliseconds;
        if (remaining <= 0)
            return 0;
        return remaining >= delay ? delay : (int)Math.Ceiling(remaining);
    }
}
