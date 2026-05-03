namespace Ahjo.Vulkan.Native;

public partial struct VkSubpassDependency
{
    [NativeTypeName("uint32_t")]
    public uint srcSubpass;

    [NativeTypeName("uint32_t")]
    public uint dstSubpass;

    [NativeTypeName("VkPipelineStageFlags")]
    public uint srcStageMask;

    [NativeTypeName("VkPipelineStageFlags")]
    public uint dstStageMask;

    [NativeTypeName("VkAccessFlags")]
    public uint srcAccessMask;

    [NativeTypeName("VkAccessFlags")]
    public uint dstAccessMask;

    [NativeTypeName("VkDependencyFlags")]
    public uint dependencyFlags;
}
