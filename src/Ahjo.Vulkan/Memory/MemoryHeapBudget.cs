namespace Ahjo.Vulkan;

/// <summary>
/// One memory heap's allocation statistics and, when the allocator was created
/// with <see cref="AllocatorDescription.EnableMemoryBudget"/>, the driver's own
/// usage/budget figures for it. Projected from VMA's <c>VmaBudget</c> by
/// <see cref="Allocator.GetHeapBudgets"/>.
/// </summary>
/// <remarks>
/// <para><see cref="Usage"/> and <see cref="Budget"/> are only meaningful when
/// the allocator was created with
/// <see cref="AllocatorDescription.EnableMemoryBudget"/> (and the device
/// enabled <see cref="VulkanExtensions.ExtMemoryBudget"/>). Without it VMA
/// estimates both from its own bookkeeping, which excludes everything
/// allocated outside VMA — including DLSS's driver-side history and scratch
/// surfaces (issue #214).</para>
/// <para>The four <c>*Count</c>/<c>*Bytes</c> members are always VMA's own
/// numbers and never include non-VMA allocations, budget bit or not.</para>
/// </remarks>
public readonly record struct MemoryHeapBudget
{
    /// <summary>Index of the heap these numbers describe, into
    /// <c>VkPhysicalDeviceMemoryProperties.memoryHeaps</c>.</summary>
    public uint HeapIndex { get; init; }

    /// <summary>Number of <c>VkDeviceMemory</c> blocks VMA has allocated in this heap.</summary>
    public uint BlockCount { get; init; }

    /// <summary>Number of live VMA allocations sub-allocated from those blocks.</summary>
    public uint AllocationCount { get; init; }

    /// <summary>Total bytes of <c>VkDeviceMemory</c> VMA holds in this heap.</summary>
    public ulong BlockBytes { get; init; }

    /// <summary>Total bytes actually sub-allocated out of <see cref="BlockBytes"/>.</summary>
    public ulong AllocationBytes { get; init; }

    /// <summary>Estimated current heap usage in bytes, from the driver when
    /// the budget extension is on. See the type's remarks.</summary>
    public ulong Usage { get; init; }

    /// <summary>Estimated bytes this process may allocate from the heap before
    /// paging or an out-of-memory failure, from the driver when the budget
    /// extension is on. See the type's remarks.</summary>
    public ulong Budget { get; init; }
}
