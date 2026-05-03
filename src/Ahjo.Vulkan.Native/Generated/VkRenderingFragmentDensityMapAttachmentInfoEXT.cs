namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRenderingFragmentDensityMapAttachmentInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkImageView")]
    public VkImageView_T* imageView;

    public VkImageLayout imageLayout;
}
