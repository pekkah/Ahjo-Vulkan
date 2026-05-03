namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRenderPassCreateInfo2
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkRenderPassCreateFlags")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint attachmentCount;

    [NativeTypeName("const VkAttachmentDescription2 *")]
    public VkAttachmentDescription2* pAttachments;

    [NativeTypeName("uint32_t")]
    public uint subpassCount;

    [NativeTypeName("const VkSubpassDescription2 *")]
    public VkSubpassDescription2* pSubpasses;

    [NativeTypeName("uint32_t")]
    public uint dependencyCount;

    [NativeTypeName("const VkSubpassDependency2 *")]
    public VkSubpassDependency2* pDependencies;

    [NativeTypeName("uint32_t")]
    public uint correlatedViewMaskCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pCorrelatedViewMasks;
}
