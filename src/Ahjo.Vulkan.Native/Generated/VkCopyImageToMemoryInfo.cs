namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCopyImageToMemoryInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkHostImageCopyFlags")]
    public uint flags;

    [NativeTypeName("VkImage")]
    public VkImage_T* srcImage;

    public VkImageLayout srcImageLayout;

    [NativeTypeName("uint32_t")]
    public uint regionCount;

    [NativeTypeName("const VkImageToMemoryCopy *")]
    public VkImageToMemoryCopy* pRegions;
}
