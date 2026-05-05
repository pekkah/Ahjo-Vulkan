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
}
