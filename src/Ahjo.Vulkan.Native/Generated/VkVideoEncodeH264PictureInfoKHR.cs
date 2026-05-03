namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeH264PictureInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint naluSliceEntryCount;

    [NativeTypeName("const VkVideoEncodeH264NaluSliceInfoKHR *")]
    public VkVideoEncodeH264NaluSliceInfoKHR* pNaluSliceEntries;

    [NativeTypeName("const StdVideoEncodeH264PictureInfo *")]
    public StdVideoEncodeH264PictureInfo* pStdPictureInfo;

    [NativeTypeName("VkBool32")]
    public uint generatePrefixNalu;
}
