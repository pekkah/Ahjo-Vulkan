namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkQueryPoolPerformanceQueryCreateInfoINTEL
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkQueryPoolSamplingModeINTEL performanceCountersSampling;
}
