namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkGeneratedCommandsMemoryRequirementsInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkPipelineBindPoint pipelineBindPoint;

    [NativeTypeName("VkPipeline")]
    public VkPipeline_T* pipeline;

    [NativeTypeName("VkIndirectCommandsLayoutNV")]
    public VkIndirectCommandsLayoutNV_T* indirectCommandsLayout;

    [NativeTypeName("uint32_t")]
    public uint maxSequencesCount;
}
