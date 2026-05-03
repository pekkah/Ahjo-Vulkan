namespace Ahjo.Vulkan.Native;

public partial struct VkCopyMemoryToImageIndirectCommandKHR
{
    [NativeTypeName("VkDeviceAddress")]
    public ulong srcAddress;

    [NativeTypeName("uint32_t")]
    public uint bufferRowLength;

    [NativeTypeName("uint32_t")]
    public uint bufferImageHeight;

    public VkImageSubresourceLayers imageSubresource;

    public VkOffset3D imageOffset;

    public VkExtent3D imageExtent;
}
