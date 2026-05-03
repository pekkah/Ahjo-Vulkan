namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPipelineRasterizationConservativeStateCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineRasterizationConservativeStateCreateFlagsEXT")]
    public uint flags;

    public VkConservativeRasterizationModeEXT conservativeRasterizationMode;

    public float extraPrimitiveOverestimationSize;
}
