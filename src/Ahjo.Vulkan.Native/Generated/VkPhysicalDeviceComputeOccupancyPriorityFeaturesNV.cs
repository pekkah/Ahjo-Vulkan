namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceComputeOccupancyPriorityFeaturesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint computeOccupancyPriority;
}
