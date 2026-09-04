namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// Top-left origins, in pixels, of the subrectangle DLSS should read from (or
/// write to) within each bound image. All zero — the default — means "the
/// whole image, from its origin", which is what a renderer with dedicated
/// DLSS targets wants.
/// </summary>
/// <remarks>
/// <para>These exist for renderers that pack several render targets into one
/// larger image, or that draw at a smaller extent into a fixed-size
/// allocation. The <i>extent</i> of the read is
/// <see cref="DlssEvaluateInputs.RenderWidth"/> /
/// <see cref="DlssEvaluateInputs.RenderHeight"/>, not a member here.</para>
/// <para><see cref="OutputBaseX"/> / <see cref="OutputBaseY"/> are only
/// honoured when the feature was created with
/// <see cref="DlssFeatureDescription.EnableOutputSubrects"/>.</para>
/// </remarks>
public readonly record struct DlssSubrects
{
    /// <summary>Origin X within <see cref="DlssEvaluateInputs.Color"/>.</summary>
    public uint ColorBaseX { get; init; }

    /// <summary>Origin Y within <see cref="DlssEvaluateInputs.Color"/>.</summary>
    public uint ColorBaseY { get; init; }

    /// <summary>Origin X within <see cref="DlssEvaluateInputs.Depth"/>.</summary>
    public uint DepthBaseX { get; init; }

    /// <summary>Origin Y within <see cref="DlssEvaluateInputs.Depth"/>.</summary>
    public uint DepthBaseY { get; init; }

    /// <summary>Origin X within <see cref="DlssEvaluateInputs.MotionVectors"/>.</summary>
    public uint MotionVectorsBaseX { get; init; }

    /// <summary>Origin Y within <see cref="DlssEvaluateInputs.MotionVectors"/>.</summary>
    public uint MotionVectorsBaseY { get; init; }

    /// <summary>Origin X within <see cref="DlssEvaluateInputs.BiasCurrentColorMask"/>.</summary>
    public uint BiasCurrentColorBaseX { get; init; }

    /// <summary>Origin Y within <see cref="DlssEvaluateInputs.BiasCurrentColorMask"/>.</summary>
    public uint BiasCurrentColorBaseY { get; init; }

    /// <summary>Origin X within <see cref="DlssEvaluateInputs.Output"/>.
    /// Requires <see cref="DlssFeatureDescription.EnableOutputSubrects"/>.</summary>
    public uint OutputBaseX { get; init; }

    /// <summary>Origin Y within <see cref="DlssEvaluateInputs.Output"/>.
    /// Requires <see cref="DlssFeatureDescription.EnableOutputSubrects"/>.</summary>
    public uint OutputBaseY { get; init; }
}
