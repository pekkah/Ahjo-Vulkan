namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineCoverageModulationStateCreateInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineCoverageModulationStateCreateFlagsNV")]
    public uint flags;

    public VkCoverageModulationModeNV coverageModulationMode;

    [NativeTypeName("VkBool32")]
    public uint coverageModulationTableEnable;

    [NativeTypeName("uint32_t")]
    public uint coverageModulationTableCount;

    [NativeTypeName("const float *")]
    public float* pCoverageModulationTable;
}
