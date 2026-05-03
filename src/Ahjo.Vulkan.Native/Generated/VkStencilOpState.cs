namespace Ahjo.Vulkan.Native;

public partial struct VkStencilOpState
{
    public VkStencilOp failOp;

    public VkStencilOp passOp;

    public VkStencilOp depthFailOp;

    public VkCompareOp compareOp;

    [NativeTypeName("uint32_t")]
    public uint compareMask;

    [NativeTypeName("uint32_t")]
    public uint writeMask;

    [NativeTypeName("uint32_t")]
    public uint reference;
}
