namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMaintenance7FeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint maintenance7;
}
