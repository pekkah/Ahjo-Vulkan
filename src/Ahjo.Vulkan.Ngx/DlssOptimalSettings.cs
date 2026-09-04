namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// What render extent DLSS wants for one output extent and quality mode on this
/// GPU, plus the dynamic-resolution range it will accept. From
/// <see cref="NgxContext.GetOptimalSettings"/>.
/// </summary>
/// <remarks>
/// <para><b>Check <see cref="IsAvailable"/> first.</b> Not every quality mode is
/// offered at every output resolution; an unavailable one comes back with
/// <see cref="IsAvailable"/> <see langword="false"/> and every dimension zero,
/// rather than as a 0×0 render target a caller might allocate.</para>
/// <para><see cref="DlssQualityMode.Dlaa"/> returns render == output. That is a
/// property of NGX's answer, not something the wrapper synthesizes.</para>
/// <para>Six dimensions, not four: the dynamic-resolution range NGX returns is
/// two independent 2-D extents, and collapsing each to a single scale factor
/// would either drop the aspect ratio or invent one (spec D8 — a recorded
/// deviation from issue #218's wording).</para>
/// </remarks>
public readonly record struct DlssOptimalSettings
{
    /// <summary>Whether this quality mode is offered at the requested output
    /// extent. When <see langword="false"/>, every other member is zero.</summary>
    public bool IsAvailable { get; init; }

    /// <summary>The render width DLSS is tuned for. Render at this.</summary>
    public uint RenderWidth { get; init; }

    /// <summary>The render height DLSS is tuned for.</summary>
    public uint RenderHeight { get; init; }

    /// <summary>Smallest render width the feature will accept, for dynamic
    /// resolution. Never below this in
    /// <see cref="DlssEvaluateInputs.RenderWidth"/>.</summary>
    public uint MinRenderWidth { get; init; }

    /// <summary>Smallest render height the feature will accept.</summary>
    public uint MinRenderHeight { get; init; }

    /// <summary>Largest render width the feature will accept.</summary>
    public uint MaxRenderWidth { get; init; }

    /// <summary>Largest render height the feature will accept.</summary>
    public uint MaxRenderHeight { get; init; }
}
