namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkGraphicsPipelineCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineCreateFlags")]
    public uint flags;

    [NativeTypeName("uint32_t")]
    public uint stageCount;

    [NativeTypeName("const VkPipelineShaderStageCreateInfo *")]
    public VkPipelineShaderStageCreateInfo* pStages;

    [NativeTypeName("const VkPipelineVertexInputStateCreateInfo *")]
    public VkPipelineVertexInputStateCreateInfo* pVertexInputState;

    [NativeTypeName("const VkPipelineInputAssemblyStateCreateInfo *")]
    public VkPipelineInputAssemblyStateCreateInfo* pInputAssemblyState;

    [NativeTypeName("const VkPipelineTessellationStateCreateInfo *")]
    public VkPipelineTessellationStateCreateInfo* pTessellationState;

    [NativeTypeName("const VkPipelineViewportStateCreateInfo *")]
    public VkPipelineViewportStateCreateInfo* pViewportState;

    [NativeTypeName("const VkPipelineRasterizationStateCreateInfo *")]
    public VkPipelineRasterizationStateCreateInfo* pRasterizationState;

    [NativeTypeName("const VkPipelineMultisampleStateCreateInfo *")]
    public VkPipelineMultisampleStateCreateInfo* pMultisampleState;

    [NativeTypeName("const VkPipelineDepthStencilStateCreateInfo *")]
    public VkPipelineDepthStencilStateCreateInfo* pDepthStencilState;

    [NativeTypeName("const VkPipelineColorBlendStateCreateInfo *")]
    public VkPipelineColorBlendStateCreateInfo* pColorBlendState;

    [NativeTypeName("const VkPipelineDynamicStateCreateInfo *")]
    public VkPipelineDynamicStateCreateInfo* pDynamicState;

    [NativeTypeName("VkPipelineLayout")]
    public VkPipelineLayout_T* layout;

    [NativeTypeName("VkRenderPass")]
    public VkRenderPass_T* renderPass;

    [NativeTypeName("uint32_t")]
    public uint subpass;

    [NativeTypeName("VkPipeline")]
    public VkPipeline_T* basePipelineHandle;

    [NativeTypeName("int32_t")]
    public int basePipelineIndex;
}
