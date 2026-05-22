namespace Ahjo.Vulkan.Native;

/// <summary>
/// Typed access to <c>VkFormatFeatureFlags2</c> bit values. Same shape as
/// <see cref="VkPipelineStageFlags2"/> / <see cref="VkAccessFlags2"/> — the
/// underlying spec definition is <c>#define ((VkFlags64)0x…)</c>, which
/// lands as <c>public const ulong VK_FORMAT_FEATURE_2_*</c> on <c>Vk</c>.
///
/// Scope: core Vulkan 1.3 baseline only. <c>_KHR</c> aliases and vendor
/// extensions remain accessible via <c>Vk.VK_FORMAT_FEATURE_2_*</c>.
/// </summary>
public static class VkFormatFeatureFlags2
{
    public const ulong SampledImage                                                = Vk.VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_BIT;
    public const ulong StorageImage                                                = Vk.VK_FORMAT_FEATURE_2_STORAGE_IMAGE_BIT;
    public const ulong StorageImageAtomic                                          = Vk.VK_FORMAT_FEATURE_2_STORAGE_IMAGE_ATOMIC_BIT;
    public const ulong UniformTexelBuffer                                          = Vk.VK_FORMAT_FEATURE_2_UNIFORM_TEXEL_BUFFER_BIT;
    public const ulong StorageTexelBuffer                                          = Vk.VK_FORMAT_FEATURE_2_STORAGE_TEXEL_BUFFER_BIT;
    public const ulong StorageTexelBufferAtomic                                    = Vk.VK_FORMAT_FEATURE_2_STORAGE_TEXEL_BUFFER_ATOMIC_BIT;
    public const ulong VertexBuffer                                                = Vk.VK_FORMAT_FEATURE_2_VERTEX_BUFFER_BIT;
    public const ulong ColorAttachment                                             = Vk.VK_FORMAT_FEATURE_2_COLOR_ATTACHMENT_BIT;
    public const ulong ColorAttachmentBlend                                        = Vk.VK_FORMAT_FEATURE_2_COLOR_ATTACHMENT_BLEND_BIT;
    public const ulong DepthStencilAttachment                                      = Vk.VK_FORMAT_FEATURE_2_DEPTH_STENCIL_ATTACHMENT_BIT;
    public const ulong BlitSrc                                                     = Vk.VK_FORMAT_FEATURE_2_BLIT_SRC_BIT;
    public const ulong BlitDst                                                     = Vk.VK_FORMAT_FEATURE_2_BLIT_DST_BIT;
    public const ulong SampledImageFilterLinear                                    = Vk.VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_FILTER_LINEAR_BIT;
    public const ulong TransferSrc                                                 = Vk.VK_FORMAT_FEATURE_2_TRANSFER_SRC_BIT;
    public const ulong TransferDst                                                 = Vk.VK_FORMAT_FEATURE_2_TRANSFER_DST_BIT;
    public const ulong SampledImageFilterMinmax                                    = Vk.VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_FILTER_MINMAX_BIT;
    public const ulong MidpointChromaSamples                                       = Vk.VK_FORMAT_FEATURE_2_MIDPOINT_CHROMA_SAMPLES_BIT;
    public const ulong SampledImageYcbcrConversionLinearFilter                     = Vk.VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_YCBCR_CONVERSION_LINEAR_FILTER_BIT;
    public const ulong SampledImageYcbcrConversionSeparateReconstructionFilter     = Vk.VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_YCBCR_CONVERSION_SEPARATE_RECONSTRUCTION_FILTER_BIT;
    public const ulong SampledImageYcbcrConversionChromaReconstructionExplicit     = Vk.VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_YCBCR_CONVERSION_CHROMA_RECONSTRUCTION_EXPLICIT_BIT;
    public const ulong SampledImageYcbcrConversionChromaReconstructionExplicitForceable = Vk.VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_YCBCR_CONVERSION_CHROMA_RECONSTRUCTION_EXPLICIT_FORCEABLE_BIT;
    public const ulong Disjoint                                                    = Vk.VK_FORMAT_FEATURE_2_DISJOINT_BIT;
    public const ulong CositedChromaSamples                                        = Vk.VK_FORMAT_FEATURE_2_COSITED_CHROMA_SAMPLES_BIT;
    public const ulong StorageReadWithoutFormat                                    = Vk.VK_FORMAT_FEATURE_2_STORAGE_READ_WITHOUT_FORMAT_BIT;
    public const ulong StorageWriteWithoutFormat                                   = Vk.VK_FORMAT_FEATURE_2_STORAGE_WRITE_WITHOUT_FORMAT_BIT;
    public const ulong SampledImageDepthComparison                                 = Vk.VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_DEPTH_COMPARISON_BIT;
    public const ulong SampledImageFilterCubic                                     = Vk.VK_FORMAT_FEATURE_2_SAMPLED_IMAGE_FILTER_CUBIC_BIT;
    public const ulong HostImageTransfer                                           = Vk.VK_FORMAT_FEATURE_2_HOST_IMAGE_TRANSFER_BIT;
}
