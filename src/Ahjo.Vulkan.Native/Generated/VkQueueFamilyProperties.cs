namespace Ahjo.Vulkan.Native;

public partial struct VkQueueFamilyProperties
{
    [NativeTypeName("VkQueueFlags")]
    public uint queueFlags;

    [NativeTypeName("uint32_t")]
    public uint queueCount;

    [NativeTypeName("uint32_t")]
    public uint timestampValidBits;

    public VkExtent3D minImageTransferGranularity;
}
