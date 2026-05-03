namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageMemoryBarrier
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkAccessFlags")]
    public uint srcAccessMask;

    [NativeTypeName("VkAccessFlags")]
    public uint dstAccessMask;

    public VkImageLayout oldLayout;

    public VkImageLayout newLayout;

    [NativeTypeName("uint32_t")]
    public uint srcQueueFamilyIndex;

    [NativeTypeName("uint32_t")]
    public uint dstQueueFamilyIndex;

    [NativeTypeName("VkImage")]
    public VkImage_T* image;

    public VkImageSubresourceRange subresourceRange;
}
