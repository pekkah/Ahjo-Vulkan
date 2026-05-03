namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeH264DpbSlotInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const StdVideoEncodeH264ReferenceInfo *")]
    public StdVideoEncodeH264ReferenceInfo* pStdReferenceInfo;
}
