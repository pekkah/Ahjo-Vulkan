namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeH265NaluSliceSegmentInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("int32_t")]
    public int constantQp;

    [NativeTypeName("const StdVideoEncodeH265SliceSegmentHeader *")]
    public StdVideoEncodeH265SliceSegmentHeader* pStdSliceSegmentHeader;
}
