namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMultiDrawPropertiesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxMultiDrawCount;
}
