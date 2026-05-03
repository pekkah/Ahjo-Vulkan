namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceGroupDeviceCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint physicalDeviceCount;

    [NativeTypeName("const VkPhysicalDevice *")]
    public VkPhysicalDevice_T** pPhysicalDevices;
}
