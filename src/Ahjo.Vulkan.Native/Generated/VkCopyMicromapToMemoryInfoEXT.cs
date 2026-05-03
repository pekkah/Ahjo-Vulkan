namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCopyMicromapToMemoryInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkMicromapEXT")]
    public VkMicromapEXT_T* src;

    public VkDeviceOrHostAddressKHR dst;

    public VkCopyMicromapModeEXT mode;
}
