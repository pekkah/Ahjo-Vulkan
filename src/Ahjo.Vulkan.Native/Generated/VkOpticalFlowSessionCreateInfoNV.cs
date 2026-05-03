namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkOpticalFlowSessionCreateInfoNV
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

    [NativeTypeName("VkOpticalFlowGridSizeFlagsNV")]
    public uint outputGridSize;

    [NativeTypeName("VkOpticalFlowGridSizeFlagsNV")]
    public uint hintGridSize;

    public VkOpticalFlowPerformanceLevelNV performanceLevel;

    [NativeTypeName("VkOpticalFlowSessionCreateFlagsNV")]
    public uint flags;
}
