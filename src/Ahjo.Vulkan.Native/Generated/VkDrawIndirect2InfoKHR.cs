namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDrawIndirect2InfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkStridedDeviceAddressRangeKHR addressRange;

    [NativeTypeName("VkAddressCommandFlagsKHR")]
    public uint addressFlags;

    [NativeTypeName("uint32_t")]
    public uint drawCount;
}
