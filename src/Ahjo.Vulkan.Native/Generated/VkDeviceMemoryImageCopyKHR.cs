namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDeviceMemoryImageCopyKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkDeviceAddressRangeKHR addressRange;

    [NativeTypeName("VkAddressCommandFlagsKHR")]
    public uint addressFlags;

    [NativeTypeName("uint32_t")]
    public uint addressRowLength;

    [NativeTypeName("uint32_t")]
    public uint addressImageHeight;

    public VkImageSubresourceLayers imageSubresource;

    public VkImageLayout imageLayout;

    public VkOffset3D imageOffset;

    public VkExtent3D imageExtent;
}
