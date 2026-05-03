namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceMutableDescriptorTypeFeaturesEXT
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("VkBool32")]
    public uint mutableDescriptorType;
}
