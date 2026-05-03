namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCopyImageToBufferInfo2
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkImage")]
    public VkImage_T* srcImage;

    public VkImageLayout srcImageLayout;

    [NativeTypeName("VkBuffer")]
    public VkBuffer_T* dstBuffer;

    [NativeTypeName("uint32_t")]
    public uint regionCount;

    [NativeTypeName("const VkBufferImageCopy2 *")]
    public VkBufferImageCopy2* pRegions;
}
