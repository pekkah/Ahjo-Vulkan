namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeH264NaluSliceInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("int32_t")]
    public int constantQp;

    [NativeTypeName("const StdVideoEncodeH264SliceHeader *")]
    public StdVideoEncodeH264SliceHeader* pStdSliceHeader;
}
