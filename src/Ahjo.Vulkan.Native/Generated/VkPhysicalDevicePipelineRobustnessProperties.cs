namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDevicePipelineRobustnessProperties
{
    public VkStructureType sType;

    public void* pNext;

    public VkPipelineRobustnessBufferBehavior defaultRobustnessStorageBuffers;

    public VkPipelineRobustnessBufferBehavior defaultRobustnessUniformBuffers;

    public VkPipelineRobustnessBufferBehavior defaultRobustnessVertexInputs;

    public VkPipelineRobustnessImageBehavior defaultRobustnessImages;
}
