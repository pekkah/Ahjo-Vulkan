namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeH265PictureInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint naluSliceSegmentEntryCount;

    [NativeTypeName("const VkVideoEncodeH265NaluSliceSegmentInfoKHR *")]
    public VkVideoEncodeH265NaluSliceSegmentInfoKHR* pNaluSliceSegmentEntries;

    [NativeTypeName("const StdVideoEncodeH265PictureInfo *")]
    public StdVideoEncodeH265PictureInfo* pStdPictureInfo;
}
