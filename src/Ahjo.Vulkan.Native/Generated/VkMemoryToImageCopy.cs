namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMemoryToImageCopy
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("const void *")]
    public void* pHostPointer;

    [NativeTypeName("uint32_t")]
    public uint memoryRowLength;

    [NativeTypeName("uint32_t")]
    public uint memoryImageHeight;

    public VkImageSubresourceLayers imageSubresource;

    public VkOffset3D imageOffset;

    public VkExtent3D imageExtent;
}
