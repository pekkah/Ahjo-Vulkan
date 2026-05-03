namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceFragmentShadingRateKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkSampleCountFlags")]
    public uint sampleCounts;

    public VkExtent2D fragmentSize;
}
