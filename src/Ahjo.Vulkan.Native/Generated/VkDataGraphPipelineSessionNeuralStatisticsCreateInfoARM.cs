namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDataGraphPipelineSessionNeuralStatisticsCreateInfoARM
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkNeuralAcceleratorStatisticsModeARM mode;
}
