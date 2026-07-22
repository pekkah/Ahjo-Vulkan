namespace Ahjo.Vulkan;

/// <summary>
/// Inputs to <see cref="Allocator.CreateBuffer"/> /
/// <see cref="Allocator.CreateImage"/>. Maps onto
/// <c>VmaAllocationCreateInfo</c>.
/// </summary>
public readonly record struct AllocationDescription
{
    /// <summary>Hint to VMA about how the allocation will be used.</summary>
    public MemoryUsage     Usage { get; init; }

    /// <summary>Bitwise-OR of allocation flags (host access, mapping, strategy).</summary>
    public AllocationFlags Flags { get; init; }

    /// <summary>
    /// Memory properties the chosen type MUST have. Leave <see cref="MemoryProperties.None"/>
    /// and let <see cref="Usage"/> decide, except where it cannot: a
    /// <see cref="MemoryBlock"/> allocated before any resource exists has no resource usage
    /// for VMA's <c>Auto*</c> hints to derive from, so
    /// <see cref="Allocator.AllocateMemory"/> requires these instead.
    /// </summary>
    public MemoryProperties RequiredFlags { get; init; }

    /// <summary>
    /// Memory properties preferred but not required — VMA picks a type carrying as many as
    /// it can while still satisfying <see cref="RequiredFlags"/>.
    /// </summary>
    public MemoryProperties PreferredFlags { get; init; }
}
