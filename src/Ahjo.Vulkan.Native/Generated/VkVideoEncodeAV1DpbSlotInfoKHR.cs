namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeAV1DpbSlotInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const StdVideoEncodeAV1ReferenceInfo *")]
    public StdVideoEncodeAV1ReferenceInfo* pStdReferenceInfo;
}
