namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineDepthStencilStateCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineDepthStencilStateCreateFlags")]
    public uint flags;

    [NativeTypeName("VkBool32")]
    public uint depthTestEnable;

    [NativeTypeName("VkBool32")]
    public uint depthWriteEnable;

    public VkCompareOp depthCompareOp;

    [NativeTypeName("VkBool32")]
    public uint depthBoundsTestEnable;

    [NativeTypeName("VkBool32")]
    public uint stencilTestEnable;

    public VkStencilOpState front;

    public VkStencilOpState back;

    public float minDepthBounds;

    public float maxDepthBounds;
}
