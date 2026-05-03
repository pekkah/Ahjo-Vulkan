namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkGraphicsShaderGroupCreateInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint stageCount;

    [NativeTypeName("const VkPipelineShaderStageCreateInfo *")]
    public VkPipelineShaderStageCreateInfo* pStages;

    [NativeTypeName("const VkPipelineVertexInputStateCreateInfo *")]
    public VkPipelineVertexInputStateCreateInfo* pVertexInputState;

    [NativeTypeName("const VkPipelineTessellationStateCreateInfo *")]
    public VkPipelineTessellationStateCreateInfo* pTessellationState;
}
