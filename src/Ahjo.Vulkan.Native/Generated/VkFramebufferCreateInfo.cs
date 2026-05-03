namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkFramebufferCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkFramebufferCreateFlags")]
    public uint flags;

    [NativeTypeName("VkRenderPass")]
    public VkRenderPass_T* renderPass;

    [NativeTypeName("uint32_t")]
    public uint attachmentCount;

    [NativeTypeName("const VkImageView *")]
    public VkImageView_T** pAttachments;

    [NativeTypeName("uint32_t")]
    public uint width;

    [NativeTypeName("uint32_t")]
    public uint height;

    [NativeTypeName("uint32_t")]
    public uint layers;
}
