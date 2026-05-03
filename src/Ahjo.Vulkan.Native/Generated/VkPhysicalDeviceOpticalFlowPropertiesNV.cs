namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceOpticalFlowPropertiesNV
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkOpticalFlowGridSizeFlagsNV")]
    public uint supportedOutputGridSizes;

    [NativeTypeName("VkOpticalFlowGridSizeFlagsNV")]
    public uint supportedHintGridSizes;

    [NativeTypeName("VkBool32")]
    public uint hintSupported;

    [NativeTypeName("VkBool32")]
    public uint costSupported;

    [NativeTypeName("VkBool32")]
    public uint bidirectionalFlowSupported;

    [NativeTypeName("VkBool32")]
    public uint globalFlowSupported;

    [NativeTypeName("uint32_t")]
    public uint minWidth;

    [NativeTypeName("uint32_t")]
    public uint minHeight;

    [NativeTypeName("uint32_t")]
    public uint maxWidth;

    [NativeTypeName("uint32_t")]
    public uint maxHeight;

    [NativeTypeName("uint32_t")]
    public uint maxNumRegionsOfInterest;
}
