namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDescriptorSetBindingReferenceVALVE
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkDescriptorSetLayout")]
    public VkDescriptorSetLayout_T* descriptorSetLayout;

    [NativeTypeName("uint32_t")]
    public uint binding;
}
