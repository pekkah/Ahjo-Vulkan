namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRenderPassAttachmentBeginInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint attachmentCount;

    [NativeTypeName("const VkImageView *")]
    public VkImageView_T** pAttachments;
}
