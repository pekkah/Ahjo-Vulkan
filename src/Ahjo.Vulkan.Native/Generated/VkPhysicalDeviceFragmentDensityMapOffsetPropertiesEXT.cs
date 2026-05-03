namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceFragmentDensityMapOffsetPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    public VkExtent2D fragmentDensityOffsetGranularity;
}
