namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkImageViewHandleInfoNVX
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkImageView")]
    public VkImageView_T* imageView;

    public VkDescriptorType descriptorType;

    [NativeTypeName("VkSampler")]
    public VkSampler_T* sampler;
}
