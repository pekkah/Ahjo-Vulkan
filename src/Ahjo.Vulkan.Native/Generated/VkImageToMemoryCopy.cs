namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageToMemoryCopy
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public void* pHostPointer;

    [NativeTypeName("uint32_t")]
    public uint memoryRowLength;

    [NativeTypeName("uint32_t")]
    public uint memoryImageHeight;

    public VkImageSubresourceLayers imageSubresource;

    public VkOffset3D imageOffset;

    public VkExtent3D imageExtent;
}
