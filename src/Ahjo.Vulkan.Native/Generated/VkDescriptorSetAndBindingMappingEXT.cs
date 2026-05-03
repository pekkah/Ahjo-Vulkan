namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDescriptorSetAndBindingMappingEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint descriptorSet;

    [NativeTypeName("uint32_t")]
    public uint firstBinding;

    [NativeTypeName("uint32_t")]
    public uint bindingCount;

    [NativeTypeName("VkSpirvResourceTypeFlagsEXT")]
    public uint resourceMask;

    public VkDescriptorMappingSourceEXT source;

    public VkDescriptorMappingSourceDataEXT sourceData;
}
