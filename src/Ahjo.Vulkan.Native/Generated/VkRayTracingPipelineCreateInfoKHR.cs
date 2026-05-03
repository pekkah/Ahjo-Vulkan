namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkRayTracingPipelineCreateInfoKHR
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

    [NativeTypeName("const VkRayTracingShaderGroupCreateInfoKHR *")]
    public VkRayTracingShaderGroupCreateInfoKHR* pGroups;

    [NativeTypeName("uint32_t")]
    public uint maxPipelineRayRecursionDepth;

    [NativeTypeName("const VkPipelineLibraryCreateInfoKHR *")]
    public VkPipelineLibraryCreateInfoKHR* pLibraryInfo;

    [NativeTypeName("const VkRayTracingPipelineInterfaceCreateInfoKHR *")]
    public VkRayTracingPipelineInterfaceCreateInfoKHR* pLibraryInterface;

    [NativeTypeName("const VkPipelineDynamicStateCreateInfo *")]
    public VkPipelineDynamicStateCreateInfo* pDynamicState;

    [NativeTypeName("VkPipelineLayout")]
    public VkPipelineLayout_T* layout;

    [NativeTypeName("VkPipeline")]
    public VkPipeline_T* basePipelineHandle;

    [NativeTypeName("int32_t")]
    public int basePipelineIndex;
}
