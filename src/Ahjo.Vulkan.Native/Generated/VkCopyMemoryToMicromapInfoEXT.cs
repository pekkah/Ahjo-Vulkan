namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCopyMemoryToMicromapInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkDeviceOrHostAddressConstKHR src;

    [NativeTypeName("VkMicromapEXT")]
    public VkMicromapEXT_T* dst;

    public VkCopyMicromapModeEXT mode;
}
