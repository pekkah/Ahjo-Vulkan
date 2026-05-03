namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCopyImageToImageInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkHostImageCopyFlags")]
    public uint flags;

    [NativeTypeName("VkImage")]
    public VkImage_T* srcImage;

    public VkImageLayout srcImageLayout;

    [NativeTypeName("VkImage")]
    public VkImage_T* dstImage;

    public VkImageLayout dstImageLayout;

    [NativeTypeName("uint32_t")]
    public uint regionCount;

    [NativeTypeName("const VkImageCopy2 *")]
    public VkImageCopy2* pRegions;
}
