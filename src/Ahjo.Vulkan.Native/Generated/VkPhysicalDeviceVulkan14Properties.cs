using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Native;

public unsafe partial struct VkPhysicalDeviceVulkan14Properties
{
    public VkStructureType sType;

    public void* pNext;

    [NativeTypeName("uint32_t")]
    public uint lineSubPixelPrecisionBits;

    [NativeTypeName("uint32_t")]
    public uint maxVertexAttribDivisor;

    [NativeTypeName("VkBool32")]
    public uint supportsNonZeroFirstInstance;

    [NativeTypeName("uint32_t")]
    public uint maxPushDescriptors;

    [NativeTypeName("VkBool32")]
    public uint dynamicRenderingLocalReadDepthStencilAttachments;

    [NativeTypeName("VkBool32")]
    public uint dynamicRenderingLocalReadMultisampledAttachments;

    [NativeTypeName("VkBool32")]
    public uint earlyFragmentMultisampleCoverageAfterSampleCounting;

    [NativeTypeName("VkBool32")]
    public uint earlyFragmentSampleMaskTestBeforeSampleCounting;

    [NativeTypeName("VkBool32")]
    public uint depthStencilSwizzleOneSupport;

    [NativeTypeName("VkBool32")]
    public uint polygonModePointSize;

    [NativeTypeName("VkBool32")]
    public uint nonStrictSinglePixelWideLinesUseParallelogram;

    [NativeTypeName("VkBool32")]
    public uint nonStrictWideLinesUseParallelogram;

    [NativeTypeName("VkBool32")]
    public uint blockTexelViewCompatibleMultipleLayers;

    [NativeTypeName("uint32_t")]
    public uint maxCombinedImageSamplerDescriptorCount;

    [NativeTypeName("VkBool32")]
    public uint fragmentShadingRateClampCombinerInputs;

    public VkPipelineRobustnessBufferBehavior defaultRobustnessStorageBuffers;

    public VkPipelineRobustnessBufferBehavior defaultRobustnessUniformBuffers;

    public VkPipelineRobustnessBufferBehavior defaultRobustnessVertexInputs;

    public VkPipelineRobustnessImageBehavior defaultRobustnessImages;

    [NativeTypeName("uint32_t")]
    public uint copySrcLayoutCount;

    public VkImageLayout* pCopySrcLayouts;

    [NativeTypeName("uint32_t")]
    public uint copyDstLayoutCount;

    public VkImageLayout* pCopyDstLayouts;

    [NativeTypeName("uint8_t[16]")]
    public _optimalTilingLayoutUUID_e__FixedBuffer optimalTilingLayoutUUID;

    [NativeTypeName("VkBool32")]
    public uint identicalMemoryTypeRequirements;

    [InlineArray(16)]
    public partial struct _optimalTilingLayoutUUID_e__FixedBuffer
    {
        public byte e0;
    }
}
