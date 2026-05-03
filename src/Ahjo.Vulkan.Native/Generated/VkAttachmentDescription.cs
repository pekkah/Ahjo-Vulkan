namespace Ahjo.Vulkan.Native;

public partial struct VkAttachmentDescription
{
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
