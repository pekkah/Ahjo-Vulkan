namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageMemoryBarrier2
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkPipelineStageFlags2")]
    public ulong srcStageMask;

    [NativeTypeName("VkAccessFlags2")]
    public ulong srcAccessMask;

    [NativeTypeName("VkPipelineStageFlags2")]
    public ulong dstStageMask;

    [NativeTypeName("VkAccessFlags2")]
    public ulong dstAccessMask;

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
