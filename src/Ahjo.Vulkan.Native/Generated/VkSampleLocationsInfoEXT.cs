namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSampleLocationsInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkSampleCountFlagBits sampleLocationsPerPixel;

    public VkExtent2D sampleLocationGridSize;

    [NativeTypeName("uint32_t")]
    public uint sampleLocationsCount;

    [NativeTypeName("const VkSampleLocationEXT *")]
    public VkSampleLocationEXT* pSampleLocations;
}
