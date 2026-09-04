namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// Which trained DLSS model to hint for. Shadow of
/// <c>NVSDK_NGX_DLSS_Hint_Render_Preset</c>; letters are NVIDIA's own naming.
/// </summary>
/// <remarks>
/// <para><see cref="Default"/> lets the driver choose, and is the right answer
/// unless you have compared the alternatives on your own content. The hint is
/// applied at feature creation, for the one
/// <see cref="DlssFeatureDescription.Mode"/> the feature is created with — NGX
/// keys presets per quality mode.</para>
/// <para>Guidance carried from #214's research:</para>
/// <list type="bullet">
///   <item><description><see cref="K"/> — the transformer-model default for
///   <see cref="DlssQualityMode.Dlaa"/>, <see cref="DlssQualityMode.MaxQuality"/>
///   and <see cref="DlssQualityMode.Balanced"/>.</description></item>
///   <item><description><see cref="L"/> and <see cref="M"/> — the defaults for
///   <see cref="DlssQualityMode.UltraPerformance"/> and
///   <see cref="DlssQualityMode.MaxPerformance"/>.</description></item>
///   <item><description><see cref="J"/> — trades more flicker for less
///   ghosting on fast-moving content.</description></item>
///   <item><description><see cref="E"/> and <see cref="F"/> — the deprecated
///   CNN-model presets. Kept because existing content may pin them; do not
///   choose one for new work.</description></item>
/// </list>
/// <para>The two native <c>*_Reserved</c> members (<c>H</c>, <c>I</c>) are
/// omitted; the member-count drift test asserts that.</para>
/// </remarks>
public enum DlssPreset : uint
{
    /// <summary>Let the driver pick. Recommended.</summary>
    Default = 0,

    /// <summary>Deprecated CNN preset.</summary>
    E = 5,

    /// <summary>Deprecated CNN preset.</summary>
    F = 6,

    G = 7,

    /// <summary>Less ghosting, more flicker, than <see cref="K"/>.</summary>
    J = 10,

    /// <summary>Transformer default for DLAA / Quality / Balanced.</summary>
    K = 11,

    /// <summary>Transformer default for Ultra Performance.</summary>
    L = 12,

    /// <summary>Transformer default for Performance.</summary>
    M = 13,

    N = 14,

    O = 15,
}
