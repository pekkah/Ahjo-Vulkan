namespace Ahjo.Vulkan.Native;

public partial struct VkPipelineColorBlendAttachmentState
{
    [NativeTypeName("VkBool32")]
    public uint blendEnable;

    public VkBlendFactor srcColorBlendFactor;

    public VkBlendFactor dstColorBlendFactor;

    public VkBlendOp colorBlendOp;

    public VkBlendFactor srcAlphaBlendFactor;

    public VkBlendFactor dstAlphaBlendFactor;

    public VkBlendOp alphaBlendOp;

    [NativeTypeName("VkColorComponentFlags")]
    public uint colorWriteMask;
}
