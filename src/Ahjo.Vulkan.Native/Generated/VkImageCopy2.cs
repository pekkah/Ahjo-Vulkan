namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageCopy2
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkImageSubresourceLayers srcSubresource;

    public VkOffset3D srcOffset;

    public VkImageSubresourceLayers dstSubresource;

    public VkOffset3D dstOffset;

    public VkExtent3D extent;
}
