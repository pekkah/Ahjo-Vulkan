namespace Ahjo.Vulkan;

/// <summary>
/// Strongly-typed shadow of <c>VmaAllocationCreateFlagBits</c>. Bit values
/// match the underlying enum; the cast to <c>VmaAllocationCreateFlags</c>
/// (a plain <c>uint</c> in the bindings) is a no-op.
/// </summary>
[Flags]
public enum AllocationFlags : uint
{
    None                            = 0,

    /// <summary>
    /// <c>VMA_ALLOCATION_CREATE_DEDICATED_MEMORY_BIT</c> — give this resource
    /// its own <c>VkDeviceMemory</c> rather than a suballocation.
    /// </summary>
    /// <seealso cref="VulkanExtensions.ExtMemoryBudget"/>
    /// <remarks>
    /// Full-screen render targets handed to DLSS (<c>Ahjo.Vulkan.Ngx</c>) are
    /// the canonical case: they are large, long-lived and read by the driver's
    /// own upscaling passes, and they sit next to DLSS's driver-side history
    /// and scratch allocations that VMA never sees at all. Pair with
    /// <see cref="AllocatorDescription.EnableMemoryBudget"/> +
    /// <see cref="VulkanExtensions.ExtMemoryBudget"/> when you want
    /// <see cref="Allocator.GetHeapBudgets"/> to account for both halves
    /// (issue #214).
    /// </remarks>
    DedicatedMemory                 = 0x00000001,
    NeverAllocate                   = 0x00000002,
    Mapped                          = 0x00000004,
    UserDataCopyString              = 0x00000020,
    UpperAddress                    = 0x00000040,
    DontBind                        = 0x00000080,
    WithinBudget                    = 0x00000100,
    CanAlias                        = 0x00000200,
    HostAccessSequentialWrite       = 0x00000400,
    HostAccessRandom                = 0x00000800,
    HostAccessAllowTransferInstead  = 0x00001000,
    StrategyMinMemory               = 0x00010000,
    StrategyMinTime                 = 0x00020000,
    StrategyMinOffset               = 0x00040000,
}
