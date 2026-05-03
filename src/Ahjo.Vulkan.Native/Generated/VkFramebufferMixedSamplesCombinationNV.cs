namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkFramebufferMixedSamplesCombinationNV
{
    public VkStructureType sType;

    public void* pNext;

    public VkCoverageReductionModeNV coverageReductionMode;

    public VkSampleCountFlagBits rasterizationSamples;

    [NativeTypeName("VkSampleCountFlags")]
    public uint depthStencilSamples;

    [NativeTypeName("VkSampleCountFlags")]
    public uint colorSamples;
}
