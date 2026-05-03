namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceGroupPresentInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint swapchainCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pDeviceMasks;

    public VkDeviceGroupPresentModeFlagBitsKHR mode;
}
