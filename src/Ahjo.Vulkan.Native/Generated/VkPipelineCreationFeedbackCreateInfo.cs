namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineCreationFeedbackCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkPipelineCreationFeedback* pPipelineCreationFeedback;

    [NativeTypeName("uint32_t")]
    public uint pipelineStageCreationFeedbackCount;

    public VkPipelineCreationFeedback* pPipelineStageCreationFeedbacks;
}
