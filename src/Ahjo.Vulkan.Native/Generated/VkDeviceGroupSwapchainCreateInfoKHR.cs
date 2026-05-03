namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceGroupSwapchainCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDeviceGroupPresentModeFlagsKHR")]
    public uint modes;
}
