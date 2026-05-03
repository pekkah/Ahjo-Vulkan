namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkResourceDescriptorInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkDescriptorType type;

    public VkResourceDescriptorDataEXT data;
}
