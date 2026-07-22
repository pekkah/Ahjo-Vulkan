using Ahjo.Vulkan.Vma.Native;
using VmaApi = Ahjo.Vulkan.Vma.Native.Vma;

namespace Ahjo.Vulkan;

/// <summary>
/// A VMA allocation with no resource bound to it — device memory the caller sub-allocates
/// itself by creating resources at chosen offsets
/// (<see cref="Allocator.CreateAliasingImage"/> /
/// <see cref="Allocator.CreateAliasingBuffer"/>).
/// </summary>
/// <remarks>
/// <para>This is the escape hatch from one-resource-one-allocation, and it exists for
/// exactly one reason: resources whose lifetimes do not overlap can share bytes. A frame
/// graph places its transients into one block and creates each one where its own packing
/// says, which no per-resource <see cref="Allocator.CreateImage"/> call can express.</para>
/// <para><b>The block owns the memory; the resources bound into it do not.</b> Disposing an
/// aliasing <see cref="Image"/> or <see cref="Buffer"/> destroys that resource and frees
/// nothing. Dispose every resource FIRST, then the block — the reverse order leaves live
/// resources pointing at freed memory, which no validation layer will catch for you.</para>
/// <para><b>Aliased contents are undefined.</b> Vulkan says so, and it means what it says:
/// after a second resource is used in bytes a first one wrote, the first one's contents are
/// gone. Every aliasing resource must be (re)initialized before it is read, and every
/// hand-off between two resources sharing bytes needs a barrier — with the incoming image
/// transitioning from <c>VK_IMAGE_LAYOUT_UNDEFINED</c>, never from whatever layout it was
/// left in.</para>
/// <para><c>readonly struct</c> handle, like <see cref="Allocator"/>: copy-by-value,
/// <c>default(MemoryBlock)</c> is a legal null handle, and <see cref="Dispose"/> is not
/// idempotent because the struct cannot null its own fields.</para>
/// </remarks>
public readonly unsafe struct MemoryBlock : IDisposable
{
    internal readonly VmaAllocation_T* Handle;

    /// <summary>The allocator that produced this block, and the one that frees it.</summary>
    public readonly Allocator Owner;

    /// <summary>
    /// Bytes the block spans. At least the size asked for — VMA may round up, and offsets
    /// passed to the aliasing creators are validated against THIS number.
    /// </summary>
    public readonly ulong Size;

    /// <summary>
    /// The memory type index VMA picked, always one of the bits set in the
    /// <see cref="MemoryRequirements.MemoryTypeBits"/> the block was allocated from.
    /// </summary>
    public readonly uint MemoryTypeIndex;

    internal MemoryBlock(VmaAllocation_T* handle, Allocator owner, ulong size, uint memoryTypeIndex)
    {
        Handle = handle;
        Owner = owner;
        Size = size;
        MemoryTypeIndex = memoryTypeIndex;
    }

    /// <summary>Whether this is the null handle.</summary>
    public bool IsNull => Handle == null;

    /// <summary>
    /// Frees the memory. Every resource created into this block must already be disposed —
    /// see the type remarks.
    /// </summary>
    public void Dispose()
    {
        if (Handle == null) return;
        VmaApi.vmaFreeMemory(Owner.Handle, Handle);
    }
}
