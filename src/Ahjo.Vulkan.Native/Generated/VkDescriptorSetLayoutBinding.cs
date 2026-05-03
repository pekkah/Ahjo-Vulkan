namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDescriptorSetLayoutBinding
{
    [NativeTypeName("uint32_t")]
    public uint binding;

    public VkDescriptorType descriptorType;

    [NativeTypeName("uint32_t")]
    public uint descriptorCount;

    [NativeTypeName("VkShaderStageFlags")]
    public uint stageFlags;

    [NativeTypeName("const VkSampler *")]
    public VkSampler_T** pImmutableSamplers;
}
