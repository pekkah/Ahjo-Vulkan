namespace Ahjo.Vulkan;

/// <summary>
/// What one resource needs from device memory — <c>VkMemoryRequirements</c>, field for
/// field, answered without creating a resource the caller has to own
/// (<see cref="Device.GetImageMemoryRequirements"/> /
/// <see cref="Device.GetBufferMemoryRequirements"/>).
/// </summary>
/// <remarks>
/// <para>This exists for the aliasing path. A caller packing several resources into one
/// allocation has to know each one's size and alignment BEFORE anything is created, and
/// has to pick a memory type every one of them accepts — which is the intersection of
/// their <see cref="MemoryTypeBits"/>, hence <see cref="CombineWith"/>.</para>
/// <para>Ordinary one-resource-one-allocation code never needs this: <see
/// cref="Allocator.CreateImage"/> and <see cref="Allocator.CreateBuffer"/> let VMA do all
/// of it in a single call.</para>
/// </remarks>
public readonly record struct MemoryRequirements
{
    /// <summary>Bytes the resource occupies. Always &gt; 0 for a valid description.</summary>
    public ulong Size { get; init; }

    /// <summary>Byte alignment the resource's offset within an allocation must be a
    /// multiple of. Always a power of two.</summary>
    public ulong Alignment { get; init; }

    /// <summary>One bit per memory type index the resource may be bound to. Always
    /// non-zero for a valid description — Vulkan guarantees at least one type.</summary>
    public uint MemoryTypeBits { get; init; }

    /// <summary>
    /// The requirements of an allocation that can host BOTH: the larger size, the stricter
    /// alignment, and the memory types they both accept. A zero
    /// <see cref="MemoryTypeBits"/> in the result means the two cannot share one
    /// allocation at all — check it before allocating rather than letting
    /// <see cref="Allocator.AllocateMemory"/> fail.
    /// </summary>
    /// <remarks>
    /// Combining does NOT account for <c>bufferImageGranularity</c>
    /// (<see cref="DeviceMemoryLimits.BufferImageGranularity"/>): that is a rule about
    /// where resources sit RELATIVE to each other inside the allocation, which only the
    /// caller's packing knows. Sizing a heap by folding this over every resource gives the
    /// un-aliased upper bound, not a layout.
    /// </remarks>
    public MemoryRequirements CombineWith(in MemoryRequirements other) => new()
    {
        Size = Math.Max(Size, other.Size),
        Alignment = Math.Max(Alignment, other.Alignment),
        MemoryTypeBits = MemoryTypeBits & other.MemoryTypeBits,
    };
}
