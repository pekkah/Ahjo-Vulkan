namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkAttachmentSampleCountInfoAMD
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint colorAttachmentCount;

    [NativeTypeName("const VkSampleCountFlagBits *")]
    public VkSampleCountFlagBits* pColorAttachmentSamples;

    public VkSampleCountFlagBits depthStencilAttachmentSamples;
}
