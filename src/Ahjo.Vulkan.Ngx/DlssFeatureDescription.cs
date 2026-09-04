namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// What DLSS feature to create. Passed to
/// <see cref="NgxContext.CreateDlss"/>; fixed for the feature's lifetime —
/// changing the resolution or quality mode means releasing the feature and
/// creating another.
/// </summary>
/// <remarks>
/// Take <see cref="RenderWidth"/> / <see cref="RenderHeight"/> from
/// <see cref="NgxContext.GetOptimalSettings"/> rather than deriving them from a
/// ratio: NGX's answer is the tuned one, and it is the answer the dynamic range
/// on the created feature is expressed against.
/// </remarks>
public readonly record struct DlssFeatureDescription
{
    /// <summary>Width the renderer draws at.</summary>
    public uint RenderWidth { get; init; }

    /// <summary>Height the renderer draws at.</summary>
    public uint RenderHeight { get; init; }

    /// <summary>Width DLSS produces. Must be at least
    /// <see cref="RenderWidth"/> and at least 32.</summary>
    public uint OutputWidth { get; init; }

    /// <summary>Height DLSS produces. Must be at least
    /// <see cref="RenderHeight"/> and at least 32.</summary>
    public uint OutputHeight { get; init; }

    /// <summary>Quality mode. Also selects which
    /// <see cref="Preset"/> hint key is written — NGX keys presets per
    /// mode.</summary>
    public DlssQualityMode Mode { get; init; }

    /// <summary>What the renderer's inputs look like. Getting these wrong
    /// produces ghosting or a black frame, not an error.</summary>
    public DlssFeatureFlags Flags { get; init; }

    /// <summary>Which trained model to hint for.
    /// <see cref="DlssPreset.Default"/> — the default — writes no hint and
    /// lets the driver choose.</summary>
    public DlssPreset Preset { get; init; }

    /// <summary>
    /// Allow the output to be written into a subrectangle of a larger image,
    /// addressed by <see cref="DlssSubrects.OutputBaseX"/> /
    /// <see cref="DlssSubrects.OutputBaseY"/>. Off by default.
    /// </summary>
    public bool EnableOutputSubrects { get; init; }

    /// <summary>
    /// Ask NGX to release the feature's video memory when the feature is
    /// released, rather than keeping it pooled for the next feature.
    /// Defaults to <see langword="false"/> — NGX's own behaviour (guide §3.14).
    /// Set it when a resolution change would otherwise leave the old feature's
    /// history and scratch resident.
    /// </summary>
    public bool FreeMemoryOnRelease { get; init; }
}
