namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSubpassDescriptionDepthStencilResolve
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkResolveModeFlagBits depthResolveMode;

    public VkResolveModeFlagBits stencilResolveMode;

    [NativeTypeName("const VkAttachmentReference2 *")]
    public VkAttachmentReference2* pDepthStencilResolveAttachment;
}
