namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoDecodeH265PictureInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const StdVideoDecodeH265PictureInfo *")]
    public StdVideoDecodeH265PictureInfo* pStdPictureInfo;

    [NativeTypeName("uint32_t")]
    public uint sliceSegmentCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pSliceSegmentOffsets;
}
