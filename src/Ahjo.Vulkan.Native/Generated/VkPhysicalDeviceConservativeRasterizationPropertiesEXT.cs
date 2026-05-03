namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceConservativeRasterizationPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    public float primitiveOverestimationSize;

    public float maxExtraPrimitiveOverestimationSize;

    public float extraPrimitiveOverestimationSizeGranularity;

    [NativeTypeName("VkBool32")]
    public uint primitiveUnderestimation;

    [NativeTypeName("VkBool32")]
    public uint conservativePointAndLineRasterization;

    [NativeTypeName("VkBool32")]
    public uint degenerateTrianglesRasterized;

    [NativeTypeName("VkBool32")]
    public uint degenerateLinesRasterized;

    [NativeTypeName("VkBool32")]
    public uint fullyCoveredFragmentShaderInputVariable;

    [NativeTypeName("VkBool32")]
    public uint conservativeRasterizationPostDepthCoverage;
}
