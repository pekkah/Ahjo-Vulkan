namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkQueryPoolCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkQueryPoolCreateFlags")]
    public uint flags;

    public VkQueryType queryType;

    [NativeTypeName("uint32_t")]
    public uint queryCount;

    [NativeTypeName("VkQueryPipelineStatisticFlags")]
    public uint pipelineStatistics;
}
