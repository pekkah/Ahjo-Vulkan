namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoDecodeH264PictureInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const StdVideoDecodeH264PictureInfo *")]
    public StdVideoDecodeH264PictureInfo* pStdPictureInfo;

    [NativeTypeName("uint32_t")]
    public uint sliceCount;

    [NativeTypeName("const uint32_t *")]
    public uint* pSliceOffsets;
}
