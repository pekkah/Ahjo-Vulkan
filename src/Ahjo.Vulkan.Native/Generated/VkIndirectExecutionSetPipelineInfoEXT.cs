namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkIndirectExecutionSetPipelineInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipeline")]
    public VkPipeline_T* initialPipeline;

    [NativeTypeName("uint32_t")]
    public uint maxPipelineCount;
}
