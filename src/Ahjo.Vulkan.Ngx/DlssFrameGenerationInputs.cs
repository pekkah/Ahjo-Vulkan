namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// Everything one <see cref="DlssFrameGenerationFeature.Evaluate"/> reads: four
/// required images, three optional ones, the camera block and the per-frame
/// scalars.
/// </summary>
/// <remarks>
/// <para><b>Image layout is your contract, and the wrapper cannot check it.</b>
/// Before <see cref="DlssFrameGenerationFeature.Evaluate"/>:</para>
/// <list type="bullet">
///   <item><description>every <i>input</i> image
///   (<see cref="Backbuffer"/>, <see cref="Depth"/>, <see cref="MotionVectors"/>,
///   <see cref="Hudless"/>, <see cref="UiColorAndAlpha"/>) must be in
///   <c>VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL</c> or another shader-read
///   layout;</description></item>
///   <item><description><see cref="OutputInterpolated"/> and
///   <see cref="OutputReal"/> must be in
///   <c>VK_IMAGE_LAYOUT_GENERAL</c>;</description></item>
///   <item><description>the evaluate must be recorded <b>outside</b> any
///   <c>BeginRendering</c> scope.</description></item>
/// </list>
/// <para>These are the layouts the Super Resolution path established
/// (<see cref="DlssEvaluateInputs"/>). <c>samples/HelloFrameGen</c> runs them
/// with every slot bound and two presents per frame, layer-clean (#253 PR C
/// step 18); <c>docs/ngx-frame-generation-notes.md</c> §6 records the
/// findings.</para>
/// <para><b>Layouts after the evaluate are not all the layouts you set.</b>
/// Unlike Super Resolution, Frame Generation does not restore every image.
/// <b>When <see cref="OutputReal"/> is bound, <see cref="Backbuffer"/> exits in
/// <c>VK_IMAGE_LAYOUT_GENERAL</c></b>: NGX transitions it for its own
/// Backbuffer → <see cref="OutputReal"/> copy and leaves it there. The next
/// barrier on the Backbuffer must name <c>GENERAL</c> as its
/// <c>oldLayout</c>, or the layer reports
/// <c>VUID-VkImageMemoryBarrier2-oldLayout-01197</c>. Every other image exits
/// in the layout it entered: the other inputs in
/// <c>SHADER_READ_ONLY_OPTIMAL</c>, both outputs in <c>GENERAL</c>. With
/// <see cref="OutputReal"/> unbound, the Backbuffer does too. Measured on an
/// RTX 4070 Ti / driver 616.92 (#253 PR B); one driver's behaviour, not a
/// documented contract.</para>
/// <para><b>Rebind after evaluate.</b> The evaluate clobbers the command
/// buffer's bound pipeline, descriptor sets and dynamic state (DLSS-FG guide
/// l. 302-303). <see cref="CommandRecorder"/>
/// caches no bound state, so the <i>caller</i> rebinds before the next draw or
/// dispatch.</para>
/// <para><c>Ahjo.Vulkan</c> deliberately does not track image layout — it is a
/// pipeline-stage concern owned by the recorder (issue #17,
/// <c>Resources/Image.cs:19-24</c>) — so there is no value the wrapper could
/// compare against and no barrier it could emit. What the wrapper does
/// instead: validates the <i>usage</i> bits that are the necessary
/// precondition for those layouts (see
/// <see cref="DlssFrameGenerationFeature.Evaluate"/>), and turns a failing
/// <c>NVSDK_NGX_Result</c> into a legible <see cref="NgxException"/>. Run with
/// <c>VK_LAYER_KHRONOS_validation</c>: it catches what this type only
/// documents.</para>
/// <para>A plain <c>readonly struct</c>, not a <c>record struct</c> — it
/// carries pointers through <see cref="NgxImage"/> (#218 E16). The explicit
/// parameterless constructor runs the valid-by-default field initializers
/// (CS8983), the <see cref="ImageViewDescription"/> pattern from issue
/// #119.</para>
/// </remarks>
public readonly struct DlssFrameGenerationInputs
{
    /// <summary>
    /// Runs the valid-by-default initializers: both motion-vector scales, the
    /// multi-frame count and index start at 1, <see cref="CameraMotionIncluded"/>
    /// at <see langword="true"/>, and <see cref="Camera"/> with an identity
    /// <see cref="DlssFrameGenerationCamera.ClipToLensClip"/>. Required
    /// explicitly for a struct with field initializers (CS8983).
    /// </summary>
    public DlssFrameGenerationInputs() { }

    /// <summary>
    /// The final colour of the rendered frame, <b>including UI</b>. Required.
    /// UNORM (never <c>_SRGB</c>), at the feature's
    /// <see cref="DlssFrameGenerationFeature.Width"/> ×
    /// <see cref="DlssFrameGenerationFeature.Height"/>.
    /// </summary>
    /// <remarks>
    /// When <see cref="OutputReal"/> is bound, also give it
    /// <see cref="ImageUsage.TransferSrc"/>, and expect it back in
    /// <c>VK_IMAGE_LAYOUT_GENERAL</c>. Frame Generation copies it into
    /// <see cref="OutputReal"/> with <c>vkCmdCopyImage</c>
    /// (<c>VUID-vkCmdCopyImage-aspect-06662</c> without the bit) and does not
    /// restore its layout. Observed on driver 616.92; under
    /// <see cref="AhjoValidation.Enabled"/> a missing bit is a warning, not a
    /// failure.
    /// </remarks>
    public NgxImage Backbuffer { get; init; }

    /// <summary>Hardware (non-linear) depth at the render extent. Required. Set
    /// <see cref="DepthInverted"/> if it is reversed-Z.</summary>
    public NgxImage Depth { get; init; }

    /// <summary>
    /// Screen-space motion vectors at the render extent. Required.
    /// <c>previous − current</c>, in pixels of this resource, (0, 0) top-left
    /// and +Y down (DLSS-FG guide l. 4022-4036) — the same convention as Super
    /// Resolution's (<c>docs/ngx-notes.md</c> §4.2).
    /// </summary>
    public NgxImage MotionVectors { get; init; }

    /// <summary>The frame's colour <b>without</b> UI. Optional, and strongly
    /// recommended (DLSS-FG guide l. 737).</summary>
    public NgxImage Hudless { get; init; }

    /// <summary>
    /// The UI layer, colour plus alpha. Optional. <b>Premultiplied</b>, so that
    /// <c>Backbuffer = UI + (1 − α) · Hudless</c> (DLSS-FG guide l. 748-754).
    /// </summary>
    public NgxImage UiColorAndAlpha { get; init; }

    /// <summary>
    /// Where Frame Generation writes the generated frame. Required. Same
    /// extent and format as <see cref="Backbuffer"/> (DLSS-FG guide l.
    /// 764-765), with <see cref="ImageUsage.Storage"/>.
    /// </summary>
    public NgxImage OutputInterpolated { get; init; }

    /// <summary>
    /// Where Frame Generation writes the real frame back out. Optional, and
    /// recommended over retaining the backbuffer yourself: the dev build's
    /// debug overlay is drawn into it (DLSS-FG guide l. 874-876). Same extent
    /// and format as <see cref="Backbuffer"/>, with
    /// <see cref="ImageUsage.Storage"/> <b>and
    /// <see cref="ImageUsage.TransferDst"/></b>. Frame Generation fills it by
    /// copying <see cref="Backbuffer"/> into it with <c>vkCmdCopyImage</c>,
    /// which needs the transfer bit (<c>VUID-vkCmdCopyImage-aspect-06663</c>
    /// without it; observed on driver 616.92 and not stated in NVIDIA's
    /// headers). <see cref="OutputInterpolated"/> measured clean without it.
    /// </summary>
    public NgxImage OutputReal { get; init; }

    /// <summary>The camera block. Defaults to an all-zero camera with an
    /// identity <see cref="DlssFrameGenerationCamera.ClipToLensClip"/>.</summary>
    public DlssFrameGenerationCamera Camera { get; init; } = new();

    /// <summary>
    /// This frame's camera jitter, X. <b>In clip space</b>
    /// (<c>nvsdk_ngx_defs_dlssg.h:138-140</c>) — not render pixels, which is
    /// what <see cref="DlssEvaluateInputs.JitterOffsetX"/> takes. Copying the
    /// Super Resolution value across is a silent unit error.
    /// </summary>
    public float JitterOffsetX { get; init; }

    /// <summary>This frame's camera jitter, Y, <b>in clip space</b> — see
    /// <see cref="JitterOffsetX"/>.</summary>
    public float JitterOffsetY { get; init; }

    /// <summary>
    /// Scale applied to motion-vector X (<c>DLSSG.MvecScaleX</c>). Defaults to
    /// <c>1.0f</c>; a literal <c>0f</c> is treated as <c>1.0f</c>.
    /// </summary>
    /// <remarks>
    /// <b>A pixel-unit rescale, not a normalization.</b> Give
    /// <see cref="MotionVectors"/> in pixels of the motion-vector resource and
    /// leave the scale at <c>1.0f</c>, as the DLSS-FG guide says; use the scale
    /// only to convert vectors stored in some other unit into those pixels.
    /// The header's "normalize to [-1,1]" reading (<c>1/RenderWidth</c>) is
    /// wrong for this path: measured with the dev DLL's "Motion Vector
    /// Resampling" visualizer, <c>(1, 1)</c> over pixel vectors highlights only
    /// occlusion edges and the screen-static UI, while
    /// <c>(1/RenderWidth, 1/RenderHeight)</c> highlights the moving texture
    /// inside the cube's faces (#253 PR C step 19b, RTX 4070 Ti / driver
    /// 616.92). See <c>docs/ngx-frame-generation-notes.md</c> §4.
    /// </remarks>
    public float MotionVectorScaleX { get; init; } = 1f;

    /// <summary>Scale applied to motion-vector Y. Defaults to <c>1.0f</c>; see
    /// <see cref="MotionVectorScaleX"/>.</summary>
    public float MotionVectorScaleY { get; init; } = 1f;

    /// <summary>Discard the history: set on the first frame, after a resize,
    /// and after any camera cut.</summary>
    public bool Reset { get; init; }

    /// <summary><see cref="Depth"/> is reversed-Z.</summary>
    public bool DepthInverted { get; init; }

    /// <summary>The colour buffers hold HDR values.</summary>
    public bool ColorBuffersHdr { get; init; }

    /// <summary>
    /// <see cref="MotionVectors"/> include camera motion, not only object
    /// motion. Defaults to <see langword="true"/> — the convention a renderer
    /// that reprojects through the previous view-projection (as
    /// <c>samples/HelloDlaa</c> does) produces.
    /// </summary>
    public bool CameraMotionIncluded { get; init; } = true;

    /// <summary>The app is not rendering game frames right now (menus,
    /// cut-scenes).</summary>
    public bool NotRenderingGameFrames { get; init; }

    /// <summary>The projection is orthographic.</summary>
    public bool OrthographicProjection { get; init; }

    /// <summary>
    /// How many frames to generate between this rendered frame and the
    /// previous one. Defaults to 1 (2x). At most
    /// <see cref="DlssFrameGenerationFeature.MaxGeneratedFrames"/>.
    /// </summary>
    public uint MultiFrameCount { get; init; } = 1;

    /// <summary>
    /// Which of the <see cref="MultiFrameCount"/> generated frames this
    /// evaluate produces, 1-based. Defaults to 1.
    /// </summary>
    public uint MultiFrameIndex { get; init; } = 1;

    /// <summary>
    /// The rendered frame's id. Increment it by one per rendered frame,
    /// <b>even while Frame Generation is off</b>
    /// (<c>nvsdk_ngx_defs_dlssg.h:361-364</c>).
    /// </summary>
    public ulong BackbufferFrameId { get; init; }
}
