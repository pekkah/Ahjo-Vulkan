namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSparseImageFormatProperties2
{
    public VkStructureType sType;

    public void* pNext;

    public VkSparseImageFormatProperties properties;
}
