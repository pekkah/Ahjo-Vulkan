namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkMutableDescriptorTypeListEXT
{
    [NativeTypeName("uint32_t")]
    public uint descriptorTypeCount;

    [NativeTypeName("const VkDescriptorType *")]
    public VkDescriptorType* pDescriptorTypes;
}
