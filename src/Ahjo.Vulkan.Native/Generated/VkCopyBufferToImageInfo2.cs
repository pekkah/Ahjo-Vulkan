namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCopyBufferToImageInfo2
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBuffer")]
    public VkBuffer_T* srcBuffer;

    [NativeTypeName("VkImage")]
    public VkImage_T* dstImage;

    public VkImageLayout dstImageLayout;

    [NativeTypeName("uint32_t")]
    public uint regionCount;

    [NativeTypeName("const VkBufferImageCopy2 *")]
    public VkBufferImageCopy2* pRegions;
}
