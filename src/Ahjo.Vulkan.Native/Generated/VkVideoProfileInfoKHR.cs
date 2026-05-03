namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoProfileInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkVideoCodecOperationFlagBitsKHR videoCodecOperation;

    [NativeTypeName("VkVideoChromaSubsamplingFlagsKHR")]
    public uint chromaSubsampling;

    [NativeTypeName("VkVideoComponentBitDepthFlagsKHR")]
    public uint lumaBitDepth;

    [NativeTypeName("VkVideoComponentBitDepthFlagsKHR")]
    public uint chromaBitDepth;
}
