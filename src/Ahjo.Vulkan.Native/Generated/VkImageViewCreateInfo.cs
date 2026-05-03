namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageViewCreateInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkImageViewCreateFlags")]
    public uint flags;

    [NativeTypeName("VkImage")]
    public VkImage_T* image;

    public VkImageViewType viewType;

    public VkFormat format;

    public VkComponentMapping components;

    public VkImageSubresourceRange subresourceRange;
}
