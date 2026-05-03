namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoDecodeH265SessionParametersAddInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint stdVPSCount;

    [NativeTypeName("const StdVideoH265VideoParameterSet *")]
    public StdVideoH265VideoParameterSet* pStdVPSs;

    [NativeTypeName("uint32_t")]
    public uint stdSPSCount;

    [NativeTypeName("const StdVideoH265SequenceParameterSet *")]
    public StdVideoH265SequenceParameterSet* pStdSPSs;

    [NativeTypeName("uint32_t")]
    public uint stdPPSCount;

    [NativeTypeName("const StdVideoH265PictureParameterSet *")]
    public StdVideoH265PictureParameterSet* pStdPPSs;
}
