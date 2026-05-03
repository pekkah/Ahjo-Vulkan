namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDataGraphPipelineInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipeline")]
    public VkPipeline_T* dataGraphPipeline;
}
