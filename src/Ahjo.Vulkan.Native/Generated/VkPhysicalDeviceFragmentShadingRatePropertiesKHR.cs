namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceFragmentShadingRatePropertiesKHR
{
    public VkStructureType sType;

    public void* pNext;

    public VkExtent2D minFragmentShadingRateAttachmentTexelSize;

    public VkExtent2D maxFragmentShadingRateAttachmentTexelSize;

    [NativeTypeName("uint32_t")]
    public uint maxFragmentShadingRateAttachmentTexelSizeAspectRatio;

    [NativeTypeName("VkBool32")]
    public uint primitiveFragmentShadingRateWithMultipleViewports;

    [NativeTypeName("VkBool32")]
    public uint layeredShadingRateAttachments;

    [NativeTypeName("VkBool32")]
    public uint fragmentShadingRateNonTrivialCombinerOps;

    public VkExtent2D maxFragmentSize;

    [NativeTypeName("uint32_t")]
    public uint maxFragmentSizeAspectRatio;

    [NativeTypeName("uint32_t")]
    public uint maxFragmentShadingRateCoverageSamples;

    public VkSampleCountFlagBits maxFragmentShadingRateRasterizationSamples;

    [NativeTypeName("VkBool32")]
    public uint fragmentShadingRateWithShaderDepthStencilWrites;

    [NativeTypeName("VkBool32")]
    public uint fragmentShadingRateWithSampleMask;

    [NativeTypeName("VkBool32")]
    public uint fragmentShadingRateWithShaderSampleMask;

    [NativeTypeName("VkBool32")]
    public uint fragmentShadingRateWithConservativeRasterization;

    [NativeTypeName("VkBool32")]
    public uint fragmentShadingRateWithFragmentShaderInterlock;

    [NativeTypeName("VkBool32")]
    public uint fragmentShadingRateWithCustomSampleLocations;

    [NativeTypeName("VkBool32")]
    public uint fragmentShadingRateStrictMultiplyCombiner;
}
