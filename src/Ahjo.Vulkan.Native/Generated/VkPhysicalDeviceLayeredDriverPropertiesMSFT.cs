namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceLayeredDriverPropertiesMSFT
{
    public VkStructureType sType;

    public void* pNext;

    public VkLayeredDriverUnderlyingApiMSFT underlyingAPI;
}
