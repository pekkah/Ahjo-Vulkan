using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Snapshot of one <c>VkQueueFamilyProperties</c> entry. Plain <c>struct</c>
/// — pure value data the picker reads via bit-test getters. The
/// <see cref="Index"/> is the family's position in
/// <c>vkGetPhysicalDeviceQueueFamilyProperties2</c>'s output array.
/// </summary>
/// <remarks>
/// No <c>SupportsPresent</c>: present is per-surface in core Vulkan and the
/// surface API is out of scope for issue #7. The surface/swapchain issue
/// composes a present check on top of <see cref="Index"/>.
/// </remarks>
public readonly struct QueueFamilyInfo
{
    public readonly uint             Index;
    public readonly VkQueueFlagBits  Flags;
    public readonly uint             QueueCount;
    public readonly uint             TimestampValidBits;
    public readonly VkExtent3D       MinImageTransferGranularity;

    public QueueFamilyInfo(
        uint            index,
        VkQueueFlagBits flags,
        uint            queueCount,
        uint            timestampValidBits,
        VkExtent3D      minImageTransferGranularity)
    {
        Index                       = index;
        Flags                       = flags;
        QueueCount                  = queueCount;
        TimestampValidBits          = timestampValidBits;
        MinImageTransferGranularity = minImageTransferGranularity;
    }

    public bool SupportsGraphics      => (Flags & VkQueueFlagBits.VK_QUEUE_GRAPHICS_BIT)       != 0;
    public bool SupportsCompute       => (Flags & VkQueueFlagBits.VK_QUEUE_COMPUTE_BIT)        != 0;
    public bool SupportsTransfer      => (Flags & VkQueueFlagBits.VK_QUEUE_TRANSFER_BIT)       != 0;
    public bool SupportsSparseBinding => (Flags & VkQueueFlagBits.VK_QUEUE_SPARSE_BINDING_BIT) != 0;
}
