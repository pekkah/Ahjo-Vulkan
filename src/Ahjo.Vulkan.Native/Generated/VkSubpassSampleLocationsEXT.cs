namespace Ahjo.Vulkan.Native;

public partial struct VkSubpassSampleLocationsEXT
{
    [NativeTypeName("uint32_t")]
    public uint subpassIndex;

    public VkSampleLocationsInfoEXT sampleLocationsInfo;
}
