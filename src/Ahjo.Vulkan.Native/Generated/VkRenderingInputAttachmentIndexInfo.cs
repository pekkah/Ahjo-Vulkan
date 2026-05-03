namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRenderingInputAttachmentIndexInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint colorAttachmentCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pColorAttachmentInputIndices;

    [NativeTypeName("const uint32_t *")]
    public uint* pDepthInputAttachmentIndex;

    [NativeTypeName("const uint32_t *")]
    public uint* pStencilInputAttachmentIndex;
}
