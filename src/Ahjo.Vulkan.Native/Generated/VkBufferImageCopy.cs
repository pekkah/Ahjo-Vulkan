namespace Ahjo.Vulkan.Native;

public partial struct VkBufferImageCopy
{
    [NativeTypeName("VkDeviceSize")]
    public ulong bufferOffset;

    [NativeTypeName("uint32_t")]
    public uint bufferRowLength;

    [NativeTypeName("uint32_t")]
    public uint bufferImageHeight;

    public VkImageSubresourceLayers imageSubresource;

    public VkOffset3D imageOffset;

    public VkExtent3D imageExtent;
}
