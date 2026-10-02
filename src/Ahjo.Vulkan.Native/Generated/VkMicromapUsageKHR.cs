namespace Ahjo.Vulkan.Native;

public partial struct VkMicromapUsageKHR
{
    [NativeTypeName("uint32_t")]
    public uint count;

    [NativeTypeName("uint32_t")]
    public uint subdivisionLevel;

    public VkOpacityMicromapFormatKHR format;
}
