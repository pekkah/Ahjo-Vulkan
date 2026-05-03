namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDescriptorMappingSourceConstantOffsetEXT
{
    [NativeTypeName("uint32_t")]
    public uint heapOffset;

    [NativeTypeName("uint32_t")]
    public uint heapArrayStride;

    [NativeTypeName("const VkSamplerCreateInfo *")]
    public VkSamplerCreateInfo* pEmbeddedSampler;

    [NativeTypeName("uint32_t")]
    public uint samplerHeapOffset;

    [NativeTypeName("uint32_t")]
    public uint samplerHeapArrayStride;
}
