namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkDescriptorMappingSourcePushIndexEXT
{
    [NativeTypeName("uint32_t")]
    public uint heapOffset;

    [NativeTypeName("uint32_t")]
    public uint pushOffset;

    [NativeTypeName("uint32_t")]
    public uint heapIndexStride;

    [NativeTypeName("uint32_t")]
    public uint heapArrayStride;

    [NativeTypeName("const VkSamplerCreateInfo *")]
    public VkSamplerCreateInfo* pEmbeddedSampler;

    [NativeTypeName("VkBool32")]
    public uint useCombinedImageSamplerIndex;

    [NativeTypeName("uint32_t")]
    public uint samplerHeapOffset;

    [NativeTypeName("uint32_t")]
    public uint samplerPushOffset;

    [NativeTypeName("uint32_t")]
    public uint samplerHeapIndexStride;

    [NativeTypeName("uint32_t")]
    public uint samplerHeapArrayStride;
}
