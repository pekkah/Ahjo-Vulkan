namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceVulkan14Features
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint globalPriorityQuery;

    [NativeTypeName("VkBool32")]
    public uint shaderSubgroupRotate;

    [NativeTypeName("VkBool32")]
    public uint shaderSubgroupRotateClustered;

    [NativeTypeName("VkBool32")]
    public uint shaderFloatControls2;

    [NativeTypeName("VkBool32")]
    public uint shaderExpectAssume;

    [NativeTypeName("VkBool32")]
    public uint rectangularLines;

    [NativeTypeName("VkBool32")]
    public uint bresenhamLines;

    [NativeTypeName("VkBool32")]
    public uint smoothLines;

    [NativeTypeName("VkBool32")]
    public uint stippledRectangularLines;

    [NativeTypeName("VkBool32")]
    public uint stippledBresenhamLines;

    [NativeTypeName("VkBool32")]
    public uint stippledSmoothLines;

    [NativeTypeName("VkBool32")]
    public uint vertexAttributeInstanceRateDivisor;

    [NativeTypeName("VkBool32")]
    public uint vertexAttributeInstanceRateZeroDivisor;

    [NativeTypeName("VkBool32")]
    public uint indexTypeUint8;

    [NativeTypeName("VkBool32")]
    public uint dynamicRenderingLocalRead;

    [NativeTypeName("VkBool32")]
    public uint maintenance5;

    [NativeTypeName("VkBool32")]
    public uint maintenance6;

    [NativeTypeName("VkBool32")]
    public uint pipelineProtectedAccess;

    [NativeTypeName("VkBool32")]
    public uint pipelineRobustness;

    [NativeTypeName("VkBool32")]
    public uint hostImageCopy;

    [NativeTypeName("VkBool32")]
    public uint pushDescriptor;
}
