namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// How aggressively DLSS upscales: the render resolution it asks for, relative
/// to the output. Shadow of <c>NVSDK_NGX_PerfQuality_Value</c>, hand-copied so
/// the public API does not carry <c>NVSDK_NGX_</c>-prefixed member names.
/// </summary>
/// <remarks>
/// Values are pinned member-by-member by <c>NgxShadowEnumDriftTests</c>, plus a
/// member-count assertion so an SDK bump that adds a mode is a visible decision
/// rather than a silent gap. Ask
/// <see cref="NgxContext.GetOptimalSettings"/> what render extent a mode wants
/// on this GPU rather than deriving it from a ratio — not every mode is offered
/// at every output resolution.
/// </remarks>
public enum DlssQualityMode : uint
{
    /// <summary>Lowest render resolution of the four scaling modes; highest frame rate.</summary>
    MaxPerformance = 0,

    /// <summary>Between <see cref="MaxPerformance"/> and <see cref="MaxQuality"/>.</summary>
    Balanced = 1,

    /// <summary>Highest render resolution of the scaling modes; closest to native.</summary>
    MaxQuality = 2,

    /// <summary>Lowest render resolution offered. Intended for very high output resolutions.</summary>
    UltraPerformance = 3,

    /// <summary>Above <see cref="MaxQuality"/>. Not offered by every driver/DLL
    /// pairing — check <see cref="DlssOptimalSettings.IsAvailable"/>.</summary>
    UltraQuality = 4,

    /// <summary>Deep Learning Anti-Aliasing: render resolution equals output
    /// resolution, so DLSS anti-aliases instead of upscaling.</summary>
    Dlaa = 5,
}
