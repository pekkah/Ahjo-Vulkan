namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoDecodeAV1DpbSlotInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const StdVideoDecodeAV1ReferenceInfo *")]
    public StdVideoDecodeAV1ReferenceInfo* pStdReferenceInfo;
}
