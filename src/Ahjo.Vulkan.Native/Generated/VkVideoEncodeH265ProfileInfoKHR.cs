namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoEncodeH265ProfileInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public StdVideoH265ProfileIdc stdProfileIdc;
}
