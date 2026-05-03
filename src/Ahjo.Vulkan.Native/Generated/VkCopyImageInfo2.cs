namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCopyImageInfo2
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

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
