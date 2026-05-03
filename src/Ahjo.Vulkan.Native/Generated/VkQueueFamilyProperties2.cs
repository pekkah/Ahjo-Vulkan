namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkQueueFamilyProperties2
{
    public VkStructureType sType;

    public void* pNext;

    public VkQueueFamilyProperties queueFamilyProperties;
}
