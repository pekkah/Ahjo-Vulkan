namespace Ahjo.Vulkan.Native;

public partial struct VkDescriptorUpdateTemplateEntry
{
    [NativeTypeName("uint32_t")]
    public uint dstBinding;

    [NativeTypeName("uint32_t")]
    public uint dstArrayElement;

    [NativeTypeName("uint32_t")]
    public uint descriptorCount;

    public VkDescriptorType descriptorType;

    [NativeTypeName("size_t")]
    public nuint offset;

    [NativeTypeName("size_t")]
    public nuint stride;
}
