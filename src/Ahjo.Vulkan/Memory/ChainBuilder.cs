using System.Runtime.CompilerServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Stack-only bump allocator that lays out a Vulkan <c>pNext</c> chain into
/// a caller-owned <see cref="Span{Byte}"/>. Zero allocations; the caller
/// stack-allocs (or rents) the backing buffer and the builder writes typed
/// structs into it in declaration order, linking each new node into the
/// previous tail's <c>pNext</c>.
/// </summary>
/// <remarks>
/// <para>The builder is parameterized on the chain's <typeparamref name="TRoot"/>
/// so the C# compiler enforces structural validity: the constraint
/// <c>where T : IChainable&lt;TRoot&gt;</c> on <see cref="Push{T}"/> is
/// satisfied only by structs that <c>vk.xml</c>'s <c>structextends</c>
/// attribute actually permits. Chaining an incompatible struct is a
/// compile error, not a runtime exception. <c>SType</c> is read from the
/// type's static abstract member, so call sites never pass it as an
/// argument.</para>
/// <para>The builder is a <c>ref struct</c> so it can never escape to the
/// heap, which keeps the raw pointers it stitches into the chain valid
/// for as long as the backing span lives. Pass GC-heap memory only when
/// pinned — the builder doesn't pin on your behalf.</para>
/// <para>Layout: every chainable Vulkan struct begins with
/// <c>VkStructureType sType; void* pNext;</c> (the layout of
/// <see cref="VkBaseOutStructure"/>). Each node aligns to <see cref="nint"/>;
/// no Vulkan struct has stricter alignment.</para>
/// <para>Usage:</para>
/// <code>
/// [SkipLocalsInit]
/// Span&lt;byte&gt; scratch = stackalloc byte[1024];
/// var chain = ChainBuilder.For&lt;VkPhysicalDeviceFeatures2&gt;(scratch);
/// ref var head = ref chain.Root();
/// ref var v13  = ref chain.Push&lt;VkPhysicalDeviceVulkan13Features&gt;();
/// v13.synchronization2 = 1;
/// // hand chain.Head to vkGetPhysicalDeviceFeatures2 (or device.GetFeatures(...))
/// </code>
/// </remarks>
public unsafe ref struct ChainBuilder<TRoot>
    where TRoot : unmanaged, IChainRoot
{
    private readonly Span<byte> _buffer;
    private int _cursor;
    private int _rootOffset;
    private int _tailOffset;
    private bool _hasRoot;

    internal ChainBuilder(Span<byte> buffer)
    {
        _buffer = buffer;
        _cursor = 0;
        _rootOffset = -1;
        _tailOffset = -1;
        _hasRoot = false;
    }

    /// <summary>
    /// Writes the head node. Must be called exactly once before any
    /// <see cref="Push{T}"/>. The <c>sType</c> comes from
    /// <typeparamref name="TRoot"/>'s static abstract <c>RootSType</c>.
    /// </summary>
    public ref TRoot Root()
    {
        if (_hasRoot)
        {
            ThrowAlreadyHasRoot();
        }
        ref var slot = ref Reserve<TRoot>(out var offset);
        WriteHeader(offset, TRoot.RootSType);
        _rootOffset = offset;
        _tailOffset = offset;
        _hasRoot = true;
        return ref slot;
    }

    /// <summary>
    /// Appends a node to the tail of the chain. The constraint
    /// <c>where T : IChainable&lt;TRoot&gt;</c> means this only compiles
    /// when <c>T</c>'s <c>vk.xml</c> <c>structextends</c> entry actually
    /// includes <typeparamref name="TRoot"/>. <c>sType</c> comes from
    /// <c>T.SType</c> at the call site (no runtime argument).
    /// </summary>
    public ref T Push<T>() where T : unmanaged, IChainable<TRoot>
    {
        if (!_hasRoot)
        {
            ThrowNoRoot();
        }
        ref var slot = ref Reserve<T>(out var offset);
        WriteHeader(offset, T.SType);

        ref var prevBase = ref Unsafe.As<byte, VkBaseOutStructure>(
            ref _buffer[_tailOffset]);
        prevBase.pNext = (VkBaseOutStructure*)Unsafe.AsPointer(ref slot);

        _tailOffset = offset;
        return ref slot;
    }

    /// <summary>
    /// Pointer to the head of the chain, typed as <typeparamref name="TRoot"/>.
    /// Pass to APIs that take a <c>VkXxx*</c> in the head position.
    /// Returns <see langword="null"/> if no root has been written.
    /// </summary>
    public TRoot* Head
    {
        get
        {
            if (!_hasRoot)
            {
                return null;
            }
            // Read off _rootOffset rather than assuming the root sits at
            // _buffer[0]. Today Reserve places the root at offset 0
            // (it's the first allocation), but if a leading sentinel /
            // header is ever added before Root, _buffer[0] would no
            // longer name the head node.
            return (TRoot*)Unsafe.AsPointer(ref _buffer[_rootOffset]);
        }
    }

    /// <summary>
    /// Bytes consumed so far, including alignment padding. Useful from
    /// tests and diagnostics; not part of the runtime hot path.
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

        // Zero the slot so any struct field the caller skips lands on a
        // known baseline. Cost is O(sizeof(T)); add an opt-out if it
        // ever shows up in profiles.
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

/// <summary>
/// Factory for <see cref="ChainBuilder{TRoot}"/>. Plain class so the
/// generic argument can be specified at the call site:
/// <c>ChainBuilder.For&lt;VkPhysicalDeviceFeatures2&gt;(scratch)</c>.
/// </summary>
public static class ChainBuilder
{
    public static ChainBuilder<TRoot> For<TRoot>(Span<byte> scratch)
        where TRoot : unmanaged, IChainRoot
        => new(scratch);
}
