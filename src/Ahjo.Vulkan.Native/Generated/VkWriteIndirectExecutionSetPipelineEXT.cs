namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkWriteIndirectExecutionSetPipelineEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint index;

    [NativeTypeName("VkPipeline")]
    public VkPipeline_T* pipeline;
}
