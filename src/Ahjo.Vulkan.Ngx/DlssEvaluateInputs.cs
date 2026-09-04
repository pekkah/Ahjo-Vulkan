namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// Everything one <see cref="DlssFeature.Evaluate"/> reads: the four required
/// images, two optional ones, and the per-frame scalars.
/// </summary>
/// <remarks>
/// <para><b>Image layout is your contract, and the wrapper cannot check it.</b>
/// Before <see cref="DlssFeature.Evaluate"/>:</para>
/// <list type="bullet">
///   <item><description>every <i>input</i> image
///   (<see cref="Color"/>, <see cref="Depth"/>, <see cref="MotionVectors"/>,
///   <see cref="ExposureTexture"/>, <see cref="BiasCurrentColorMask"/>) must be
///   in <c>VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL</c> or another shader-read
///   layout;</description></item>
///   <item><description><see cref="Output"/> must be in
///   <c>VK_IMAGE_LAYOUT_GENERAL</c>;</description></item>
///   <item><description>the evaluate must be recorded <b>outside</b> any
///   <c>BeginRendering</c> scope.</description></item>
/// </list>
/// <para>DLSS transitions the images internally and restores those states
/// before returning (DLSS Programming Guide §3.4), so the layouts you set are
/// the layouts you get back.</para>
/// <para><c>Ahjo.Vulkan</c> deliberately does not track image layout — it is a
/// pipeline-stage concern owned by the recorder (issue #17,
/// <c>Resources/Image.cs:19-24</c>) — so there is no value the wrapper could
/// compare against and no barrier it could emit (a barrier needs
/// <c>oldLayout</c>, which only you know). What the wrapper does instead:
/// validates the <i>usage</i> bits that are the necessary precondition for
/// those layouts (see <see cref="DlssFeature.Evaluate"/>), and turns a
/// resulting <c>FAIL_RWFlagMissing</c> / <c>FAIL_UnsupportedInputFormat</c> /
/// <c>FAIL_MissingInput</c> into a legible <see cref="NgxException"/>. Run with
/// <c>VK_LAYER_KHRONOS_validation</c>: it catches what this type only
/// documents.</para>
/// <para>A plain <c>readonly struct</c>, not a <c>record struct</c> — it
/// carries pointers through <see cref="NgxImage"/> (spec E16). The explicit
/// parameterless constructor runs the valid-by-default field initializers
/// (CS8983), the <see cref="ImageViewDescription"/> pattern from issue
/// #119.</para>
/// </remarks>
public readonly struct DlssEvaluateInputs
{
    /// <summary>
    /// Runs the valid-by-default initializers: the four scale/exposure factors
    /// start at <c>1.0f</c>, not <c>0.0f</c>. Required explicitly for a struct
    /// with field initializers (CS8983).
    /// </summary>
    public DlssEvaluateInputs() { }

    /// <summary>The rendered colour buffer, at render resolution. Required.</summary>
    public NgxImage Color { get; init; }

    /// <summary>The depth buffer, at render resolution. Required. Set
    /// <see cref="DlssFeatureFlags.DepthInverted"/> at creation if it is
    /// reversed-Z.</summary>
    public NgxImage Depth { get; init; }

    /// <summary>Screen-space motion vectors. Required. Declare their
    /// resolution and jitter with
    /// <see cref="DlssFeatureFlags.MotionVectorsLowRes"/> /
    /// <see cref="DlssFeatureFlags.MotionVectorsJittered"/>.</summary>
    public NgxImage MotionVectors { get; init; }

    /// <summary>Where DLSS writes the upscaled result, at output resolution.
    /// Required.</summary>
    /// <remarks>
    /// <para>Must have <see cref="ImageUsage.Storage"/> — DLSS binds it as a
    /// storage image, and that is the bit the wrapper checks under
    /// <see cref="AhjoValidation.Enabled"/>.</para>
    /// <para><b>Also give it <see cref="ImageUsage.TransferDst"/>.</b> DLSS
    /// calls <c>vkCmdClearColorImage</c> on this image itself — observed on an
    /// RTX 4070 Ti / driver 610.47 with <see cref="Reset"/> set — and that
    /// command requires <c>VK_IMAGE_USAGE_TRANSFER_DST_BIT</c>
    /// (VUID-vkCmdClearColorImage-image-00002). Nothing in NVIDIA's headers or
    /// guide says so; the validation layer is what says so, which is the
    /// general lesson of this type's layout contract. The wrapper does not
    /// require the bit, because "DLSS always clears" is not something one
    /// driver version can establish — but omit it and a validation-enabled run
    /// will tell you.</para>
    /// </remarks>
    public NgxImage Output { get; init; }

    /// <summary>Optional 1×1 exposure value. Leave <c>default</c> and set
    /// <see cref="DlssFeatureFlags.AutoExposure"/> to have DLSS compute
    /// it.</summary>
    public NgxImage ExposureTexture { get; init; }

    /// <summary>Optional per-pixel mask biasing DLSS towards the current
    /// frame, for pixels whose history is unreliable. Leave <c>default</c> for
    /// none.</summary>
    public NgxImage BiasCurrentColorMask { get; init; }

    /// <summary>
    /// This frame's sub-pixel camera jitter, X. <b>In render-pixel space</b>
    /// (<c>nvsdk_ngx_helpers_vk.h:69</c>) — not NDC, not output pixels.
    /// </summary>
    public float JitterOffsetX { get; init; }

    /// <summary>This frame's sub-pixel camera jitter, Y, in render-pixel
    /// space.</summary>
    public float JitterOffsetY { get; init; }

    /// <summary>Width actually rendered this frame. For dynamic resolution,
    /// vary it within the feature's
    /// <see cref="DlssFeature.MinRenderWidth"/>..<see cref="DlssFeature.MaxRenderWidth"/>
    /// range.</summary>
    public uint RenderWidth { get; init; }

    /// <summary>Height actually rendered this frame, within the feature's
    /// min/max range.</summary>
    public uint RenderHeight { get; init; }

    /// <summary>Discard the accumulated history: set on the first frame after
    /// a camera cut, level load or any discontinuity. Leaving it clear across
    /// a cut is what produces a smear.</summary>
    public bool Reset { get; init; }

    /// <summary>Scale applied to motion-vector X before use, when they are not
    /// already in pixel space. Defaults to <c>1.0f</c>; a literal <c>0f</c> is
    /// treated as <c>1.0f</c>, matching NVIDIA's own helper.</summary>
    public float MotionVectorScaleX { get; init; } = 1f;

    /// <summary>Scale applied to motion-vector Y. Defaults to <c>1.0f</c>.</summary>
    public float MotionVectorScaleY { get; init; } = 1f;

    /// <summary>Pre-exposure already baked into <see cref="Color"/>. Defaults
    /// to <c>1.0f</c>; <c>0f</c> is treated as <c>1.0f</c>.</summary>
    public float PreExposure { get; init; } = 1f;

    /// <summary>Additional exposure scale. Defaults to <c>1.0f</c>; <c>0f</c>
    /// is treated as <c>1.0f</c>.</summary>
    public float ExposureScale { get; init; } = 1f;

    /// <summary>Per-image subrectangle origins. All zero by default.</summary>
    public DlssSubrects Subrects { get; init; }
}
