namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkGeneratedCommandsPipelineInfoEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkPipeline")]
    public VkPipeline_T* pipeline;
}
