namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCopyMemoryToImageInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkHostImageCopyFlags")]
    public uint flags;

    [NativeTypeName("VkImage")]
    public VkImage_T* dstImage;

    public VkImageLayout dstImageLayout;

    [NativeTypeName("uint32_t")]
    public uint regionCount;

    [NativeTypeName("const VkMemoryToImageCopy *")]
    public VkMemoryToImageCopy* pRegions;
}
