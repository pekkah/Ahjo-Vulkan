namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRenderPassCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkRenderPassCreateFlags")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint attachmentCount;

    [NativeTypeName("const VkAttachmentDescription *")]
    public VkAttachmentDescription* pAttachments;

    [NativeTypeName("uint32_t")]
    public uint subpassCount;

    [NativeTypeName("const VkSubpassDescription *")]
    public VkSubpassDescription* pSubpasses;

    [NativeTypeName("uint32_t")]
    public uint dependencyCount;

    [NativeTypeName("const VkSubpassDependency *")]
    public VkSubpassDependency* pDependencies;
}
