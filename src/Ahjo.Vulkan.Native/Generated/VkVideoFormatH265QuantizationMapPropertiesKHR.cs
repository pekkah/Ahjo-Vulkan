namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoFormatH265QuantizationMapPropertiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkVideoEncodeH265CtbSizeFlagsKHR")]
    public uint compatibleCtbSizes;
}
