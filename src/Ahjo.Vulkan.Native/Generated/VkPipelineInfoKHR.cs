namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipeline")]
    public VkPipeline_T* pipeline;
}
