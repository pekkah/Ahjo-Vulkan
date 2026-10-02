namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDataGraphPipelineSingleNodeCreateInfoARM
{
    public VkStructureType sType;

    public void* pNext;

    public VkDataGraphPipelineNodeTypeARM nodeType;

    [NativeTypeName("uint32_t")]
    public uint connectionCount;

    [NativeTypeName("const VkDataGraphPipelineSingleNodeConnectionARM *")]
    public VkDataGraphPipelineSingleNodeConnectionARM* pConnections;
}
