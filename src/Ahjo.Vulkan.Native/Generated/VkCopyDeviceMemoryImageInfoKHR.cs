namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCopyDeviceMemoryImageInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkImage")]
    public VkImage_T* image;

    [NativeTypeName("uint32_t")]
    public uint regionCount;

    [NativeTypeName("const VkDeviceMemoryImageCopyKHR *")]
    public VkDeviceMemoryImageCopyKHR* pRegions;
}
