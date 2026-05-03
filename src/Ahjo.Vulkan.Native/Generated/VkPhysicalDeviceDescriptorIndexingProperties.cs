namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceDescriptorIndexingProperties
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint maxUpdateAfterBindDescriptorsInAllPools;

    [NativeTypeName("VkBool32")]
    public uint shaderUniformBufferArrayNonUniformIndexingNative;

    [NativeTypeName("VkBool32")]
    public uint shaderSampledImageArrayNonUniformIndexingNative;

    [NativeTypeName("VkBool32")]
    public uint shaderStorageBufferArrayNonUniformIndexingNative;

    [NativeTypeName("VkBool32")]
    public uint shaderStorageImageArrayNonUniformIndexingNative;

    [NativeTypeName("VkBool32")]
    public uint shaderInputAttachmentArrayNonUniformIndexingNative;

    [NativeTypeName("VkBool32")]
    public uint robustBufferAccessUpdateAfterBind;

    [NativeTypeName("VkBool32")]
    public uint quadDivergentImplicitLod;

    [NativeTypeName("uint32_t")]
    public uint maxPerStageDescriptorUpdateAfterBindSamplers;

    [NativeTypeName("uint32_t")]
    public uint maxPerStageDescriptorUpdateAfterBindUniformBuffers;

    [NativeTypeName("uint32_t")]
    public uint maxPerStageDescriptorUpdateAfterBindStorageBuffers;

    [NativeTypeName("uint32_t")]
    public uint maxPerStageDescriptorUpdateAfterBindSampledImages;

    [NativeTypeName("uint32_t")]
    public uint maxPerStageDescriptorUpdateAfterBindStorageImages;

    [NativeTypeName("uint32_t")]
    public uint maxPerStageDescriptorUpdateAfterBindInputAttachments;

    [NativeTypeName("uint32_t")]
    public uint maxPerStageUpdateAfterBindResources;

    [NativeTypeName("uint32_t")]
    public uint maxDescriptorSetUpdateAfterBindSamplers;

    [NativeTypeName("uint32_t")]
    public uint maxDescriptorSetUpdateAfterBindUniformBuffers;

    [NativeTypeName("uint32_t")]
    public uint maxDescriptorSetUpdateAfterBindUniformBuffersDynamic;

    [NativeTypeName("uint32_t")]
    public uint maxDescriptorSetUpdateAfterBindStorageBuffers;

    [NativeTypeName("uint32_t")]
    public uint maxDescriptorSetUpdateAfterBindStorageBuffersDynamic;

    [NativeTypeName("uint32_t")]
    public uint maxDescriptorSetUpdateAfterBindSampledImages;

    [NativeTypeName("uint32_t")]
    public uint maxDescriptorSetUpdateAfterBindStorageImages;

    [NativeTypeName("uint32_t")]
    public uint maxDescriptorSetUpdateAfterBindInputAttachments;
}
