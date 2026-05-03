namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkShaderDescriptorSetAndBindingMappingInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint mappingCount;

    [NativeTypeName("const VkDescriptorSetAndBindingMappingEXT *")]
    public VkDescriptorSetAndBindingMappingEXT* pMappings;
}
