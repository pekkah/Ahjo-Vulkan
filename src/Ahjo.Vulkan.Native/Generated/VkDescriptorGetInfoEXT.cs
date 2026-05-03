namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDescriptorGetInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    public VkDescriptorType type;

    public VkDescriptorDataEXT data;
}
