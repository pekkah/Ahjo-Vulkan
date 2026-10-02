namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkQueueFamilyDataGraphOpticalFlowPropertiesARM
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkDataGraphOpticalFlowGridSizeFlagsARM")]
    public uint supportedOutputGridSizes;

    [NativeTypeName("VkDataGraphOpticalFlowGridSizeFlagsARM")]
    public uint supportedHintGridSizes;

    [NativeTypeName("VkBool32")]
    public uint hintSupported;

    [NativeTypeName("VkBool32")]
    public uint costSupported;

    [NativeTypeName("uint32_t")]
    public uint minWidth;

    [NativeTypeName("uint32_t")]
    public uint minHeight;

    [NativeTypeName("uint32_t")]
    public uint maxWidth;

    [NativeTypeName("uint32_t")]
    public uint maxHeight;
}
