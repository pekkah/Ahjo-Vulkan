namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePerformanceCountersByRegionFeaturesARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint performanceCountersByRegion;
}
