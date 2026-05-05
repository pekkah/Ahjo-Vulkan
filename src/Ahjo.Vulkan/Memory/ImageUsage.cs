namespace Ahjo.Vulkan;

/// <summary>
/// Strongly-typed shadow of <c>VkImageUsageFlagBits</c>. Bit values match
/// the underlying enum; the cast to <c>VkImageUsageFlags</c> (a plain
/// <c>uint</c> in the bindings) is a no-op.
/// </summary>
[Flags]
public enum ImageUsage : uint
{
    None                          = 0,
    TransferSrc                   = 0x00000001,
    TransferDst                   = 0x00000002,
    Sampled                       = 0x00000004,
    Storage                       = 0x00000008,
    ColorAttachment               = 0x00000010,
    DepthStencilAttachment        = 0x00000020,
    TransientAttachment           = 0x00000040,
    InputAttachment               = 0x00000080,
    HostTransfer                  = 0x00400000,
    FragmentShadingRateAttachment = 0x00000100,
}
