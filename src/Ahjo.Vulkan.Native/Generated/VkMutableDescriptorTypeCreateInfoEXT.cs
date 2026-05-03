namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMutableDescriptorTypeCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint mutableDescriptorTypeListCount;

    [NativeTypeName("const VkMutableDescriptorTypeListEXT *")]
    public VkMutableDescriptorTypeListEXT* pMutableDescriptorTypeLists;
}
