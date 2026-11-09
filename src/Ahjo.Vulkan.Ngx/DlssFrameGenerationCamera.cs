using System.Numerics;

namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// The per-frame camera block DLSS Frame Generation reads: five matrices plus
/// the camera's position, basis and projection parameters.
/// </summary>
/// <remarks>
/// <para><b>Every matrix is unjittered</b> — it must "NOT contain temporal AA
/// jitter offset" (<c>nvsdk_ngx_defs_dlssg.h:125</c>). Build them from the same
/// projection the motion vectors were built from, not from the jittered one an
/// upscaler wants.</para>
/// <para><b>Pass System.Numerics matrices as they are: row-vector form
/// (<c>v * M</c>), no transpose.</b> NVIDIA documents the matrices as
/// "float[4][4] in row-major order (assuming post-multiplication)" (DLSS-FG
/// guide), which does not say on which side the vector sits, so this was
/// measured: with the matrices as-is, the dev DLL's "Camera ClipToPrevClip
/// Matrix" visualizer leaves static geometry un-annotated under a moving
/// camera; transposed, it annotates the whole frame (#253 PR C step 19a, RTX
/// 4070 Ti / driver 616.92). Compose them the same way:
/// <c>ClipToPrevClip = Invert(view * proj) * prevView * prevProj</c>. See
/// <c>docs/ngx-frame-generation-notes.md</c> §4.</para>
/// <para>A plain <c>readonly struct</c>; the explicit parameterless constructor
/// runs the valid-by-default initializer (<see cref="ClipToLensClip"/> is
/// <see cref="Matrix4x4.Identity"/>), which CS8983 requires — the
/// <see cref="ImageViewDescription"/> pattern from issue #119.</para>
/// </remarks>
public readonly struct DlssFrameGenerationCamera
{
    /// <summary>
    /// Runs the valid-by-default initializer: <see cref="ClipToLensClip"/>
    /// starts at <see cref="Matrix4x4.Identity"/>, not all-zero.
    /// </summary>
    public DlssFrameGenerationCamera() { }

    /// <summary>View space to clip space — the unjittered projection
    /// (<c>DLSSG.CameraViewToClip</c>). Row-vector form, as-is (measured; see
    /// the type remarks).</summary>
    public Matrix4x4 ViewToClip { get; init; }

    /// <summary>Clip space to view space — the inverse of
    /// <see cref="ViewToClip"/> (<c>DLSSG.ClipToCameraView</c>). Unjittered.
    /// Orientation: see <c>docs/ngx-frame-generation-notes.md</c>.</summary>
    public Matrix4x4 ClipToView { get; init; }

    /// <summary>This frame's clip space to the previous frame's
    /// (<c>DLSSG.ClipToPrevClip</c>). Unjittered. Orientation: see
    /// <c>docs/ngx-frame-generation-notes.md</c>.</summary>
    public Matrix4x4 ClipToPrevClip { get; init; }

    /// <summary>The previous frame's clip space to this frame's
    /// (<c>DLSSG.PrevClipToClip</c>). Unjittered. Orientation: see
    /// <c>docs/ngx-frame-generation-notes.md</c>.</summary>
    public Matrix4x4 PrevClipToClip { get; init; }

    /// <summary>Clip space to lens-distorted clip space
    /// (<c>DLSSG.ClipToLensClip</c>). Defaults to
    /// <see cref="Matrix4x4.Identity"/> — no lens distortion. Orientation: see
    /// <c>docs/ngx-frame-generation-notes.md</c>.</summary>
    public Matrix4x4 ClipToLensClip { get; init; } = Matrix4x4.Identity;

    /// <summary>Camera position in world space (<c>DLSSG.CameraPos*</c>).</summary>
    public Vector3 Position { get; init; }

    /// <summary>Camera up vector in world space (<c>DLSSG.CameraUp*</c>).</summary>
    public Vector3 Up { get; init; }

    /// <summary>Camera right vector in world space (<c>DLSSG.CameraRight*</c>).</summary>
    public Vector3 Right { get; init; }

    /// <summary>Camera forward vector in world space (<c>DLSSG.CameraFwd*</c>).</summary>
    public Vector3 Forward { get; init; }

    /// <summary>Near plane distance (<c>DLSSG.CameraNear</c>).</summary>
    public float Near { get; init; }

    /// <summary>Far plane distance (<c>DLSSG.CameraFar</c>).</summary>
    public float Far { get; init; }

    /// <summary>Vertical field of view, <b>in radians</b>
    /// (<c>DLSSG.CameraFOV</c>).</summary>
    public float FieldOfView { get; init; }

    /// <summary>Projection aspect ratio, width / height
    /// (<c>DLSSG.CameraAspectRatio</c>).</summary>
    public float AspectRatio { get; init; }

    /// <summary>Pinhole offset of an off-centre projection
    /// (<c>DLSSG.CameraPinholeOffset*</c>). Zero by default.</summary>
    public Vector2 PinholeOffset { get; init; }
}
