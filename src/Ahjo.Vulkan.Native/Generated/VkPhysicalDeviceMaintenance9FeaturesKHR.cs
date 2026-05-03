namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMaintenance9FeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint maintenance9;
}
