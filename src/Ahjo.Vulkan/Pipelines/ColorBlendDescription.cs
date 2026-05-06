using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Inputs to <see cref="GraphicsPipelineBuilder.WithColorBlend"/>. One
/// <see cref="ColorBlendAttachment"/> per color attachment in the
/// pipeline; if shorter than the color-attachment count the remaining
/// attachments fall back to opaque defaults.
/// </summary>
/// <remarks>
/// <c>ref struct</c> because <see cref="Attachments"/> is a span — the
/// builder consumes it synchronously inside <c>Build()</c>.
/// </remarks>
public ref struct ColorBlendDescription
{
    public ReadOnlySpan<ColorBlendAttachment> Attachments;

    /// <summary>Bitwise framebuffer logic op (rare; defaults off).</summary>
    public bool      LogicOpEnable;
    public VkLogicOp LogicOp;

    /// <summary>RGBA blend constants used when a blend factor names <c>CONSTANT_*</c>.</summary>
    public BlendConstants BlendConstants;
}

/// <summary>
/// Per-attachment blend state. Mirrors <c>VkPipelineColorBlendAttachmentState</c>
/// with idiomatic types and convenience presets.
/// </summary>
public readonly record struct ColorBlendAttachment
{
    public bool          BlendEnable         { get; init; }
    public VkBlendFactor SrcColorBlendFactor { get; init; }
    public VkBlendFactor DstColorBlendFactor { get; init; }
    public VkBlendOp     ColorBlendOp        { get; init; }
    public VkBlendFactor SrcAlphaBlendFactor { get; init; }
    public VkBlendFactor DstAlphaBlendFactor { get; init; }
    public VkBlendOp     AlphaBlendOp        { get; init; }
    public VkColorComponentFlagBits ColorWriteMask { get; init; }

    private const VkColorComponentFlagBits Rgba =
        VkColorComponentFlagBits.VK_COLOR_COMPONENT_R_BIT |
        VkColorComponentFlagBits.VK_COLOR_COMPONENT_G_BIT |
        VkColorComponentFlagBits.VK_COLOR_COMPONENT_B_BIT |
        VkColorComponentFlagBits.VK_COLOR_COMPONENT_A_BIT;

    /// <summary>No blending — straight overwrite, full RGBA write mask. Matches the builder's default.</summary>
    public static ColorBlendAttachment Opaque { get; } = new()
    {
        ColorWriteMask = Rgba,
    };

    /// <summary>Standard alpha blending: <c>src.rgb*src.a + dst.rgb*(1-src.a)</c>, alpha accumulated.</summary>
    public static ColorBlendAttachment AlphaBlend { get; } = new()
    {
        BlendEnable         = true,
        SrcColorBlendFactor = VkBlendFactor.VK_BLEND_FACTOR_SRC_ALPHA,
        DstColorBlendFactor = VkBlendFactor.VK_BLEND_FACTOR_ONE_MINUS_SRC_ALPHA,
        ColorBlendOp        = VkBlendOp.VK_BLEND_OP_ADD,
        SrcAlphaBlendFactor = VkBlendFactor.VK_BLEND_FACTOR_ONE,
        DstAlphaBlendFactor = VkBlendFactor.VK_BLEND_FACTOR_ONE_MINUS_SRC_ALPHA,
        AlphaBlendOp        = VkBlendOp.VK_BLEND_OP_ADD,
        ColorWriteMask      = Rgba,
    };

    /// <summary>Additive blending — useful for HDR particles / glows: <c>src + dst</c>.</summary>
    public static ColorBlendAttachment Additive { get; } = new()
    {
        BlendEnable         = true,
        SrcColorBlendFactor = VkBlendFactor.VK_BLEND_FACTOR_ONE,
        DstColorBlendFactor = VkBlendFactor.VK_BLEND_FACTOR_ONE,
        ColorBlendOp        = VkBlendOp.VK_BLEND_OP_ADD,
        SrcAlphaBlendFactor = VkBlendFactor.VK_BLEND_FACTOR_ONE,
        DstAlphaBlendFactor = VkBlendFactor.VK_BLEND_FACTOR_ONE,
        AlphaBlendOp        = VkBlendOp.VK_BLEND_OP_ADD,
        ColorWriteMask      = Rgba,
    };

    internal VkPipelineColorBlendAttachmentState ToNative() => new()
    {
        blendEnable         = BlendEnable ? 1u : 0u,
        srcColorBlendFactor = SrcColorBlendFactor,
        dstColorBlendFactor = DstColorBlendFactor,
        colorBlendOp        = ColorBlendOp,
        srcAlphaBlendFactor = SrcAlphaBlendFactor,
        dstAlphaBlendFactor = DstAlphaBlendFactor,
        alphaBlendOp        = AlphaBlendOp,
        colorWriteMask      = (uint)ColorWriteMask,
    };
}

/// <summary>RGBA constants consumed by <c>VK_BLEND_FACTOR_CONSTANT_*</c> factors.</summary>
public readonly record struct BlendConstants
{
    public float R { get; init; }
    public float G { get; init; }
    public float B { get; init; }
    public float A { get; init; }

    public static BlendConstants RGBA(float r, float g, float b, float a) => new() { R = r, G = g, B = b, A = a };
}
