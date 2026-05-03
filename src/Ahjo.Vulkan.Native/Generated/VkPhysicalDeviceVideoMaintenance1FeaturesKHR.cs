namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceVideoMaintenance1FeaturesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint videoMaintenance1;
}
