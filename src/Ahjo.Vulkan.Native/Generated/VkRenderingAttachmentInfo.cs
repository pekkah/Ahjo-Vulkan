namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRenderingAttachmentInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkImageView")]
    public VkImageView_T* imageView;

    public VkImageLayout imageLayout;

    public VkResolveModeFlagBits resolveMode;

    [NativeTypeName("VkImageView")]
    public VkImageView_T* resolveImageView;

    public VkImageLayout resolveImageLayout;

    public VkAttachmentLoadOp loadOp;

    public VkAttachmentStoreOp storeOp;

    public VkClearValue clearValue;
}
