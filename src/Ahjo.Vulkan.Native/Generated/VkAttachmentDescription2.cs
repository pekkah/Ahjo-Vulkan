namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAttachmentDescription2
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkAttachmentDescriptionFlags")]
    public uint flags;

    public VkFormat format;

    public VkSampleCountFlagBits samples;

    public VkAttachmentLoadOp loadOp;

    public VkAttachmentStoreOp storeOp;

    public VkAttachmentLoadOp stencilLoadOp;

    public VkAttachmentStoreOp stencilStoreOp;

    public VkImageLayout initialLayout;

    public VkImageLayout finalLayout;
}
