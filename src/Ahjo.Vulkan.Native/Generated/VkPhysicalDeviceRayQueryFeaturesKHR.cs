namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceRayQueryFeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint rayQuery;
}
