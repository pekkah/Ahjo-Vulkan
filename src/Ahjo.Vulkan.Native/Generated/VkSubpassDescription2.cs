namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSubpassDescription2
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkSubpassDescriptionFlags")]
    public uint flags;

    public VkPipelineBindPoint pipelineBindPoint;

    [NativeTypeName("uint32_t")]
    public uint viewMask;

    [NativeTypeName("uint32_t")]
    public uint inputAttachmentCount;

    [NativeTypeName("const VkAttachmentReference2 *")]
    public VkAttachmentReference2* pInputAttachments;

    [NativeTypeName("uint32_t")]
    public uint colorAttachmentCount;

    [NativeTypeName("const VkAttachmentReference2 *")]
    public VkAttachmentReference2* pColorAttachments;

    [NativeTypeName("const VkAttachmentReference2 *")]
    public VkAttachmentReference2* pResolveAttachments;

    [NativeTypeName("const VkAttachmentReference2 *")]
    public VkAttachmentReference2* pDepthStencilAttachment;

    [NativeTypeName("uint32_t")]
    public uint preserveAttachmentCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pPreserveAttachments;
}
