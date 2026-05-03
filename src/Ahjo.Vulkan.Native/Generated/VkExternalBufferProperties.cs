namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkExternalBufferProperties
{
    public VkStructureType sType;

    public void* pNext;

    public VkExternalMemoryProperties externalMemoryProperties;
}
