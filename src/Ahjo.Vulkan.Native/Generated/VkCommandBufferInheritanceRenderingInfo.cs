namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCommandBufferInheritanceRenderingInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkRenderingFlags")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint viewMask;

    [NativeTypeName("uint32_t")]
    public uint colorAttachmentCount;

    [NativeTypeName("const VkFormat *")]
    public VkFormat* pColorAttachmentFormats;

    public VkFormat depthAttachmentFormat;

    public VkFormat stencilAttachmentFormat;

    public VkSampleCountFlagBits rasterizationSamples;
}
