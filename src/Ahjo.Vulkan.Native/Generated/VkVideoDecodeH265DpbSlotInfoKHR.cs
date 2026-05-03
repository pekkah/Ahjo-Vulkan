namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoDecodeH265DpbSlotInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const StdVideoDecodeH265ReferenceInfo *")]
    public StdVideoDecodeH265ReferenceInfo* pStdReferenceInfo;
}
