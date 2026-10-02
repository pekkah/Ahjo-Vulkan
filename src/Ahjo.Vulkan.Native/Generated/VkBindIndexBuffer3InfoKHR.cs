namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBindIndexBuffer3InfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkDeviceAddressRangeKHR addressRange;

    [NativeTypeName("VkAddressCommandFlagsKHR")]
    public uint addressFlags;

    public VkIndexType indexType;
}
