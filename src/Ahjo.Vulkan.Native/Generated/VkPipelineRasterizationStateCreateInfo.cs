namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineRasterizationStateCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineRasterizationStateCreateFlags")]
    public uint flags;

    [NativeTypeName("VkBool32")]
    public uint depthClampEnable;

    [NativeTypeName("VkBool32")]
    public uint rasterizerDiscardEnable;

    public VkPolygonMode polygonMode;

    [NativeTypeName("VkCullModeFlags")]
    public uint cullMode;

    public VkFrontFace frontFace;

    [NativeTypeName("VkBool32")]
    public uint depthBiasEnable;

    public float depthBiasConstantFactor;

    public float depthBiasClamp;

    public float depthBiasSlopeFactor;

    public float lineWidth;
}
