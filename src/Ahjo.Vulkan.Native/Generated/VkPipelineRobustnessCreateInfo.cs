namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineRobustnessCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkPipelineRobustnessBufferBehavior storageBuffers;

    public VkPipelineRobustnessBufferBehavior uniformBuffers;

    public VkPipelineRobustnessBufferBehavior vertexInputs;

    public VkPipelineRobustnessImageBehavior images;
}
