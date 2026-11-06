using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// What DLSS Frame Generation feature to create. Passed to
/// <see cref="NgxContext.CreateFrameGeneration"/>; fixed for the feature's
/// lifetime — any resize means disposing the feature and creating another
/// (DLSS-FG guide, When to Recreate the Feature).
/// </summary>
/// <remarks>
/// <para>Frame Generation does not scale: <see cref="Width"/> ×
/// <see cref="Height"/> is the extent of the backbuffer it reads <b>and</b> of
/// both images it writes. <see cref="RenderWidth"/> × <see cref="RenderHeight"/>
/// is the extent of the depth and motion-vector inputs, which may be smaller
/// when Super Resolution runs before it.</para>
/// <para><see cref="BackbufferFormat"/> must be a UNORM (or float) format,
/// never <c>_SRGB</c>: the outputs must share the backbuffer's format and need
/// storage usage, which no <c>_SRGB</c> format supports on mainstream desktop
/// drivers. See <c>docs/ngx-notes.md</c> §6.</para>
/// </remarks>
public readonly record struct DlssFrameGenerationDescription
{
    /// <summary>Width of the backbuffer and of both outputs.</summary>
    public uint Width { get; init; }

    /// <summary>Height of the backbuffer and of both outputs.</summary>
    public uint Height { get; init; }

    /// <summary>
    /// The backbuffer's format, and therefore the outputs' — written to
    /// <c>DLSSG.BackbufferFormat</c>. UNORM, not <c>_SRGB</c>.
    /// </summary>
    public VkFormat BackbufferFormat { get; init; }

    /// <summary>Width of the depth and motion-vector inputs, written to
    /// <c>DLSSG.InternalWidth</c>.</summary>
    public uint RenderWidth { get; init; }

    /// <summary>Height of the depth and motion-vector inputs, written to
    /// <c>DLSSG.InternalHeight</c>.</summary>
    public uint RenderHeight { get; init; }

    /// <summary>
    /// Whether the render extent varies frame to frame (dynamic resolution),
    /// written to <c>DLSSG.DynamicResolution</c>. Off by default.
    /// </summary>
    public bool DynamicResolution { get; init; }
}
