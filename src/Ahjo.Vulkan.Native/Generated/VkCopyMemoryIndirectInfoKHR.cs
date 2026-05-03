namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCopyMemoryIndirectInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkAddressCopyFlagsKHR")]
    public uint srcCopyFlags;

    [NativeTypeName("VkAddressCopyFlagsKHR")]
    public uint dstCopyFlags;

    [NativeTypeName("uint32_t")]
    public uint copyCount;

    public VkStridedDeviceAddressRangeKHR copyAddressRange;
}
