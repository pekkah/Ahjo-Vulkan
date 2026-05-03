namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDescriptorImageInfo
{
    [NativeTypeName("VkSampler")]
    public VkSampler_T* sampler;

    [NativeTypeName("VkImageView")]
    public VkImageView_T* imageView;

    public VkImageLayout imageLayout;
}
