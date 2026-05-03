namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoDecodeH264SessionParametersAddInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint stdSPSCount;

    [NativeTypeName("const StdVideoH264SequenceParameterSet *")]
    public StdVideoH264SequenceParameterSet* pStdSPSs;

    [NativeTypeName("uint32_t")]
    public uint stdPPSCount;

    [NativeTypeName("const StdVideoH264PictureParameterSet *")]
    public StdVideoH264PictureParameterSet* pStdPPSs;
}
