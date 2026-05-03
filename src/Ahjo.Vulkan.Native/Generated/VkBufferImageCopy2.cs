namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkBufferImageCopy2
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

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
