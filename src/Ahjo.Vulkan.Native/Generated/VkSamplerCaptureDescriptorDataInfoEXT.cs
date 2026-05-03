namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkSamplerCaptureDescriptorDataInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkSampler")]
    public VkSampler_T* sampler;
}
