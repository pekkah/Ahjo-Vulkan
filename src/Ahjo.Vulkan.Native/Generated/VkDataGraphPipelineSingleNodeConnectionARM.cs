namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDataGraphPipelineSingleNodeConnectionARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint set;

    [NativeTypeName("uint32_t")]
    public uint binding;

    public VkDataGraphPipelineNodeConnectionTypeARM connection;
}
