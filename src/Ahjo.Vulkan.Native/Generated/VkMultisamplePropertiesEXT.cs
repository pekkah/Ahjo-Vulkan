namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMultisamplePropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    public VkExtent2D maxSampleLocationGridSize;
}
