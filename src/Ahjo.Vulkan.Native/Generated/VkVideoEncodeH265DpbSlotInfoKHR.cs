namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeH265DpbSlotInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const StdVideoEncodeH265ReferenceInfo *")]
    public StdVideoEncodeH265ReferenceInfo* pStdReferenceInfo;
}
