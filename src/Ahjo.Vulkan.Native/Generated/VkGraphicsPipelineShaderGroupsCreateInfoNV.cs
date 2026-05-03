namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkGraphicsPipelineShaderGroupsCreateInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint groupCount;

    [NativeTypeName("const VkGraphicsShaderGroupCreateInfoNV *")]
    public VkGraphicsShaderGroupCreateInfoNV* pGroups;

    [NativeTypeName("uint32_t")]
    public uint pipelineCount;

    [NativeTypeName("const VkPipeline *")]
    public VkPipeline_T** pPipelines;
}
