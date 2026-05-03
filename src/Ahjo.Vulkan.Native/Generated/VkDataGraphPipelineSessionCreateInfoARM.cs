namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDataGraphPipelineSessionCreateInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDataGraphPipelineSessionCreateFlagsARM")]
    public ulong flags;

    [NativeTypeName("VkPipeline")]
    public VkPipeline_T* dataGraphPipeline;
}
