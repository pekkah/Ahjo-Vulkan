namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkCopyMemoryToImageIndirectInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkAddressCopyFlagsKHR")]
    public uint srcCopyFlags;

    [NativeTypeName("uint32_t")]
    public uint copyCount;

    public VkStridedDeviceAddressRangeKHR copyAddressRange;

    [NativeTypeName("VkImage")]
    public VkImage_T* dstImage;

    public VkImageLayout dstImageLayout;

    [NativeTypeName("const VkImageSubresourceLayers *")]
    public VkImageSubresourceLayers* pImageSubresources;
}
