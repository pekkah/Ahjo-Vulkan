namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDataGraphPipelineOpticalFlowCreateInfoARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint width;

    [NativeTypeName("uint32_t")]
    public uint height;

    public VkFormat imageFormat;

    public VkFormat flowVectorFormat;

    public VkFormat costFormat;

    [NativeTypeName("VkDataGraphOpticalFlowGridSizeFlagsARM")]
    public uint outputGridSize;

    [NativeTypeName("VkDataGraphOpticalFlowGridSizeFlagsARM")]
    public uint hintGridSize;

    public VkDataGraphOpticalFlowPerformanceLevelARM performanceLevel;

    [NativeTypeName("VkDataGraphOpticalFlowCreateFlagsARM")]
    public uint flags;
}
