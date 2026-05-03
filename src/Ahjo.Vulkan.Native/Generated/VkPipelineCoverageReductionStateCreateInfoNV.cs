namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineCoverageReductionStateCreateInfoNV
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineCoverageReductionStateCreateFlagsNV")]
    public uint flags;

    public VkCoverageReductionModeNV coverageReductionMode;
}
