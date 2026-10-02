namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDrawIndirectCount2InfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkStridedDeviceAddressRangeKHR addressRange;

    [NativeTypeName("VkAddressCommandFlagsKHR")]
    public uint addressFlags;

    public VkDeviceAddressRangeKHR countAddressRange;

    [NativeTypeName("VkAddressCommandFlagsKHR")]
    public uint countAddressFlags;

    [NativeTypeName("uint32_t")]
    public uint maxDrawCount;
}
