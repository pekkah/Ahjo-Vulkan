namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDisplayProperties2KHR
{
    public VkStructureType sType;

    public void* pNext;

    public VkDisplayPropertiesKHR displayProperties;
}
