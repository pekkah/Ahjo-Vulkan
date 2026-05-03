namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkFramebufferAttachmentsCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint attachmentImageInfoCount;

    [NativeTypeName("const VkFramebufferAttachmentImageInfo *")]
    public VkFramebufferAttachmentImageInfo* pAttachmentImageInfos;
}
