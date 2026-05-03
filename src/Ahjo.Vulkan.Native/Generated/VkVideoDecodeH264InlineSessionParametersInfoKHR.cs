namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoDecodeH264InlineSessionParametersInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const StdVideoH264SequenceParameterSet *")]
    public StdVideoH264SequenceParameterSet* pStdSPS;

    [NativeTypeName("const StdVideoH264PictureParameterSet *")]
    public StdVideoH264PictureParameterSet* pStdPPS;
}
