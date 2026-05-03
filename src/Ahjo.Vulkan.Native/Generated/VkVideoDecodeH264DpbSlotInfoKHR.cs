namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoDecodeH264DpbSlotInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const StdVideoDecodeH264ReferenceInfo *")]
    public StdVideoDecodeH264ReferenceInfo* pStdReferenceInfo;
}
