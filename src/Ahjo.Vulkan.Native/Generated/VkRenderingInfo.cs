namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRenderingInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkRenderingFlags")]
    public uint flags;

    public VkRect2D renderArea;

    [NativeTypeName("uint32_t")]
    public uint layerCount;

    [NativeTypeName("uint32_t")]
    public uint viewMask;

    [NativeTypeName("uint32_t")]
    public uint colorAttachmentCount;

    [NativeTypeName("const VkRenderingAttachmentInfo *")]
    public VkRenderingAttachmentInfo* pColorAttachments;

    [NativeTypeName("const VkRenderingAttachmentInfo *")]
    public VkRenderingAttachmentInfo* pDepthAttachment;

    [NativeTypeName("const VkRenderingAttachmentInfo *")]
    public VkRenderingAttachmentInfo* pStencilAttachment;
}
