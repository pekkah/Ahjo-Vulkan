namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkHostImageLayoutTransitionInfo
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkImage")]
    public VkImage_T* image;

    public VkImageLayout oldLayout;

    public VkImageLayout newLayout;

    public VkImageSubresourceRange subresourceRange;
}
