namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// What the renderer's inputs look like, declared once at feature creation.
/// Shadow of <c>NVSDK_NGX_DLSS_Feature_Flags</c>.
/// </summary>
/// <remarks>
/// <para><c>int</c>-backed because the native enum is untyped, and it reaches
/// the parameter map through <c>SetI</c>.</para>
/// <para>Three native members are deliberately <b>omitted</b>, and the
/// omissions are asserted by the member-count drift test:
/// <c>DoSharpening</c> (DLSS sharpening is deprecated — guide §3.5, #214;
/// exposing it invites use), <c>IsInvalid</c> (a sentinel, not a flag) and the
/// two <c>Reserved_*</c> members (NVIDIA's, not ours to set).</para>
/// <para>Get these wrong and DLSS does not fail — it ghosts, shimmers or
/// produces a black frame. They describe facts about your G-buffer that
/// nothing can infer.</para>
/// </remarks>
[Flags]
public enum DlssFeatureFlags : int
{
    /// <summary>None of the below.</summary>
    None = 0,

    /// <summary>The colour input is HDR (linear, not tonemapped). Set it when
    /// it is: DLSS applies a different input transform.</summary>
    Hdr = 1 << 0,

    /// <summary>Motion vectors are at render resolution rather than output
    /// resolution. The common case for a renderer that runs its base pass at
    /// the DLSS render extent.</summary>
    MotionVectorsLowRes = 1 << 1,

    /// <summary>Motion vectors already include the camera jitter offset. Leave
    /// clear when they are computed from unjittered positions.</summary>
    MotionVectorsJittered = 1 << 2,

    /// <summary>The depth buffer is reversed-Z (1.0 near, 0.0 far).</summary>
    DepthInverted = 1 << 3,

    /// <summary>DLSS computes exposure itself. Set it when you do not supply
    /// <see cref="DlssEvaluateInputs.ExposureTexture"/>.</summary>
    AutoExposure = 1 << 6,

    /// <summary>Upscale the colour input's alpha channel along with RGB.</summary>
    AlphaUpscaling = 1 << 7,
}
