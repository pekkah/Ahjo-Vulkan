namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMaintenance5Properties
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint earlyFragmentMultisampleCoverageAfterSampleCounting;

    [NativeTypeName("VkBool32")]
    public uint earlyFragmentSampleMaskTestBeforeSampleCounting;

    [NativeTypeName("VkBool32")]
    public uint depthStencilSwizzleOneSupport;

    [NativeTypeName("VkBool32")]
    public uint polygonModePointSize;

    [NativeTypeName("VkBool32")]
    public uint nonStrictSinglePixelWideLinesUseParallelogram;

    [NativeTypeName("VkBool32")]
    public uint nonStrictWideLinesUseParallelogram;
}
