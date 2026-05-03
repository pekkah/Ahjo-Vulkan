namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePerformanceCountersByRegionPropertiesARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxPerRegionPerformanceCounters;

    public VkExtent2D performanceCounterRegionSize;

    [NativeTypeName("uint32_t")]
    public uint rowStrideAlignment;

    [NativeTypeName("uint32_t")]
    public uint regionAlignment;

    [NativeTypeName("VkBool32")]
    public uint identityTransformOrder;
}
