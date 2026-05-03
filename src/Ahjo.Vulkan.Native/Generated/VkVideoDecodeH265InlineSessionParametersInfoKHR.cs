namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoDecodeH265InlineSessionParametersInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const StdVideoH265VideoParameterSet *")]
    public StdVideoH265VideoParameterSet* pStdVPS;

    [NativeTypeName("const StdVideoH265SequenceParameterSet *")]
    public StdVideoH265SequenceParameterSet* pStdSPS;

    [NativeTypeName("const StdVideoH265PictureParameterSet *")]
    public StdVideoH265PictureParameterSet* pStdPPS;
}
