namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageFormatProperties2
{
    public VkStructureType sType;

    public void* pNext;

    public VkImageFormatProperties imageFormatProperties;
}
