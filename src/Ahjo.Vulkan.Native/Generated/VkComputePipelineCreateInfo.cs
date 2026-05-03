namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkComputePipelineCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineCreateFlags")]
    public uint flags;

    public VkPipelineShaderStageCreateInfo stage;

    [NativeTypeName("VkPipelineLayout")]
    public VkPipelineLayout_T* layout;

    [NativeTypeName("VkPipeline")]
    public VkPipeline_T* basePipelineHandle;

    [NativeTypeName("int32_t")]
    public int basePipelineIndex;
}
