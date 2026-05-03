namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRayTracingPipelineCreateInfoNV
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

    [NativeTypeName("uint32_t")]
    public uint groupCount;

    [NativeTypeName("const VkRayTracingShaderGroupCreateInfoNV *")]
    public VkRayTracingShaderGroupCreateInfoNV* pGroups;

    [NativeTypeName("uint32_t")]
    public uint maxRecursionDepth;

    [NativeTypeName("VkPipelineLayout")]
    public VkPipelineLayout_T* layout;

    [NativeTypeName("VkPipeline")]
    public VkPipeline_T* basePipelineHandle;

    [NativeTypeName("int32_t")]
    public int basePipelineIndex;
}
