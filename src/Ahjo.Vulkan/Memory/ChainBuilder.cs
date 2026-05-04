using System.Runtime.CompilerServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Stack-only bump allocator that lays out a Vulkan <c>pNext</c> chain into
/// a caller-owned <see cref="Span{Byte}"/>. Zero allocations: the caller
/// stack-allocs (or rents) the backing buffer and the builder writes typed
/// structs into it in declaration order, linking each new node into the
/// previous tail's <c>pNext</c>.
/// </summary>
/// <remarks>
/// <para>The builder is a <c>ref struct</c> so it can never escape to the
/// heap, which keeps the raw pointers it stitches into the chain valid for
/// as long as the backing span lives. Pass GC-heap memory only when pinned —
/// the builder doesn't pin on your behalf.</para>
/// <para>Layout assumptions:</para>
/// <list type="bullet">
///   <item><description>Every chainable Vulkan struct begins with
///     <c>VkStructureType sType; void* pNext;</c> — i.e. the layout of
///     <see cref="VkBaseOutStructure"/>. The builder casts each node's first
///     bytes through that view to write <c>sType</c> and link <c>pNext</c>.</description></item>
///   <item><description>Each node is aligned to <see cref="nint"/>. The cursor
///     advances by <c>sizeof(T)</c> rounded up to the pointer alignment,
///     which is safe for every <c>VkXxxFeatures*</c> / <c>VkXxxProperties*</c>
///     struct in the spec (none have stricter alignment than 8).</description></item>
/// </list>
/// <para>Usage:</para>
/// <code>
/// [SkipLocalsInit]
/// Span&lt;byte&gt; scratch = stackalloc byte[512];
/// var chain = ChainBuilder.From(scratch);
/// ref var head = ref chain.Root&lt;VkPhysicalDeviceFeatures2&gt;(VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2);
/// ref var f13  = ref chain.Push&lt;VkPhysicalDeviceVulkan13Features&gt;(VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_3_FEATURES);
/// f13.synchronization2 = 1;
/// // pass chain.IntoNative&lt;VkPhysicalDeviceFeatures2&gt;() to the loader
/// </code>
/// </remarks>
public unsafe ref struct ChainBuilder
{
    private readonly Span<byte> _buffer;
    private int _cursor;
    private int _tailOffset;
    private bool _hasRoot;

    private ChainBuilder(Span<byte> buffer)
    {
        _buffer = buffer;
        _cursor = 0;
        _tailOffset = -1;
        _hasRoot = false;
    }

    /// <summary>
    /// Wraps <paramref name="scratch"/> as the builder's backing storage.
    /// The span is not zero-initialized by the builder; call this with
    /// <c>[SkipLocalsInit]</c> on the surrounding method to skip the
    /// stackalloc clear too.
    /// </summary>
    public static ChainBuilder From(Span<byte> scratch) => new(scratch);

    /// <summary>
    /// Writes the head node. Must be called exactly once before any
    /// <see cref="Push{T}"/>. Returns a <c>ref</c> into the backing buffer
    /// that lives as long as the buffer does.
    /// </summary>
    public ref T Root<T>(VkStructureType sType) where T : unmanaged
    {
        if (_hasRoot)
        {
            ThrowAlreadyHasRoot();
        }
        ref var slot = ref Reserve<T>(out var offset);
        WriteHeader(offset, sType);
        _tailOffset = offset;
        _hasRoot = true;
        return ref slot;
    }

    /// <summary>
    /// Appends a node to the tail of the chain, writes its <c>sType</c>,
    /// and links the previous tail's <c>pNext</c> to point at it.
    /// </summary>
    public ref T Push<T>(VkStructureType sType) where T : unmanaged
    {
        if (!_hasRoot)
        {
            ThrowNoRoot();
        }
        ref var slot = ref Reserve<T>(out var offset);
        WriteHeader(offset, sType);

        // Link previous tail -> this node. We walk through VkBaseOutStructure
        // because it's the canonical "first 16 bytes of any chainable struct"
        // view; sType is at offset 0 and pNext immediately after the natural
        // alignment slot.
        ref var prevBase = ref Unsafe.As<byte, VkBaseOutStructure>(
            ref _buffer[_tailOffset]);
        prevBase.pNext = (VkBaseOutStructure*)Unsafe.AsPointer(ref slot);

        _tailOffset = offset;
        return ref slot;
    }

    /// <summary>
    /// Pointer to the head of the chain, typed as <typeparamref name="T"/>.
    /// Pass to APIs that take a <c>VkXxx*</c> in the head position. Returns
    /// <see langword="null"/> if no root has been written.
    /// </summary>
    public T* IntoNative<T>() where T : unmanaged
    {
        if (!_hasRoot)
        {
            return null;
        }
        return (T*)Unsafe.AsPointer(ref _buffer[0]);
    }

    /// <summary>
    /// Untyped variant of <see cref="IntoNative{T}"/>. Useful when wiring
    /// a chain into an <c>extends</c> slot whose head type isn't conveniently
    /// nameable at the call site.
    /// </summary>
    public void* IntoNative()
    {
        if (!_hasRoot)
        {
            return null;
        }
        return Unsafe.AsPointer(ref _buffer[0]);
    }

    /// <summary>
    /// Bytes consumed so far, including alignment padding. Useful from tests
    /// and diagnostics; not part of the runtime hot path.
    /// </summary>
    public int Length => _cursor;

    private ref T Reserve<T>(out int offset) where T : unmanaged
    {
        var size = Unsafe.SizeOf<T>();
        var aligned = AlignTo(_cursor, sizeof(nint));
        var end = aligned + size;
        if (end > _buffer.Length)
        {
            ThrowBufferTooSmall();
        }

        // Zero the slot so any padding inside T (and any sentinel writes the
        // caller skips) lands on a known-good baseline. Writes here cost
        // O(sizeof(T)); an opt-out would be possible later if it shows up
        // in profiles.
        _buffer.Slice(aligned, size).Clear();

        offset = aligned;
        _cursor = end;
        return ref Unsafe.As<byte, T>(ref _buffer[aligned]);
    }

    private void WriteHeader(int offset, VkStructureType sType)
    {
        ref var view = ref Unsafe.As<byte, VkBaseOutStructure>(
            ref _buffer[offset]);
        view.sType = sType;
        view.pNext = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int AlignTo(int value, int alignment)
        => (value + alignment - 1) & ~(alignment - 1);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowBufferTooSmall()
        => throw new ArgumentException("Chain buffer too small for next node.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowAlreadyHasRoot()
        => throw new InvalidOperationException("ChainBuilder already has a root; call Push for subsequent nodes.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowNoRoot()
        => throw new InvalidOperationException("ChainBuilder has no root yet; call Root before Push.");
}
