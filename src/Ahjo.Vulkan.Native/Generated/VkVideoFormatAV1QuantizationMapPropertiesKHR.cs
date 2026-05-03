namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoFormatAV1QuantizationMapPropertiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkVideoEncodeAV1SuperblockSizeFlagsKHR")]
    public uint compatibleSuperblockSizes;
}
