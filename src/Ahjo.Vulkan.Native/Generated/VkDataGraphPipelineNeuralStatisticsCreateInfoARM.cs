namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDataGraphPipelineNeuralStatisticsCreateInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint allowNeuralStatistics;
}
