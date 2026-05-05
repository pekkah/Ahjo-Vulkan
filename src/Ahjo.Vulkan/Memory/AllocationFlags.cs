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
