namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSubpassDescription
{
    [NativeTypeName("VkSubpassDescriptionFlags")]
    public uint flags;

    public VkPipelineBindPoint pipelineBindPoint;

    [NativeTypeName("uint32_t")]
    public uint inputAttachmentCount;

    [NativeTypeName("const VkAttachmentReference *")]
    public VkAttachmentReference* pInputAttachments;

    [NativeTypeName("uint32_t")]
    public uint colorAttachmentCount;

    [NativeTypeName("const VkAttachmentReference *")]
    public VkAttachmentReference* pColorAttachments;

    [NativeTypeName("const VkAttachmentReference *")]
    public VkAttachmentReference* pResolveAttachments;

    [NativeTypeName("const VkAttachmentReference *")]
    public VkAttachmentReference* pDepthStencilAttachment;

    [NativeTypeName("uint32_t")]
    public uint preserveAttachmentCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pPreserveAttachments;
}
