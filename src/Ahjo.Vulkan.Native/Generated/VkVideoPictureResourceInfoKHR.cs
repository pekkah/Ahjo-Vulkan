namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoPictureResourceInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkOffset2D codedOffset;

    public VkExtent2D codedExtent;

    [NativeTypeName("uint32_t")]
    public uint baseArrayLayer;

    [NativeTypeName("VkImageView")]
    public VkImageView_T* imageViewBinding;
}
