namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeAV1SessionParametersCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const StdVideoAV1SequenceHeader *")]
    public StdVideoAV1SequenceHeader* pStdSequenceHeader;

    [NativeTypeName("const StdVideoEncodeAV1DecoderModelInfo *")]
    public StdVideoEncodeAV1DecoderModelInfo* pStdDecoderModelInfo;

    [NativeTypeName("uint32_t")]
    public uint stdOperatingPointCount;

    [NativeTypeName("const StdVideoEncodeAV1OperatingPointInfo *")]
    public StdVideoEncodeAV1OperatingPointInfo* pStdOperatingPoints;
}
