namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkVideoDecodeAV1SessionParametersCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const StdVideoAV1SequenceHeader *")]
    public StdVideoAV1SequenceHeader* pStdSequenceHeader;
}
