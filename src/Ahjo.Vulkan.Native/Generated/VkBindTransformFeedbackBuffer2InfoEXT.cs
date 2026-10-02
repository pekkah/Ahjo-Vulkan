namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBindTransformFeedbackBuffer2InfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkDeviceAddressRangeKHR addressRange;

    [NativeTypeName("VkAddressCommandFlagsKHR")]
    public uint addressFlags;
}
