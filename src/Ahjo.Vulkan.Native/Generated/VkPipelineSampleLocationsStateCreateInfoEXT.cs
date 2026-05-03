namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineSampleLocationsStateCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint sampleLocationsEnable;

    public VkSampleLocationsInfoEXT sampleLocationsInfo;
}
