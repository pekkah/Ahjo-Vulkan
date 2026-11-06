using System.Numerics;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Ngx.Native;

namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// One created DLSS Frame Generation feature: a fixed backbuffer extent and
/// format, a fixed render extent, plus the parameter map it reuses every frame.
/// Created by <see cref="NgxContext.CreateFrameGeneration"/>.
/// </summary>
/// <remarks>
/// <para><b>The feature presents nothing.</b> <see cref="Evaluate"/> is a
/// recorded command that writes
/// <see cref="DlssFrameGenerationInputs.OutputInterpolated"/> (and optionally
/// <see cref="DlssFrameGenerationInputs.OutputReal"/>) into images you own;
/// presenting them, and pacing those presents, is the application's job
/// (DLSS-FG guide l. 849-850). See
/// <c>docs/ngx-frame-generation-notes.md</c>.</para>
/// <para>Any resize means disposing this and creating another (DLSS-FG guide l.
/// 657-668) — the extents are creation-time facts.</para>
/// <para>Dispose it <b>before</b> the <see cref="NgxContext"/> that created
/// it.</para>
/// </remarks>
public sealed unsafe class DlssFrameGenerationFeature : IDisposable
{
    private readonly NgxContext _context;
    private NVSDK_NGX_Handle*    _handle;
    private NVSDK_NGX_Parameter* _parameters;
    private readonly uint _maxGeneratedFrames;
    private readonly bool _dynamicResolution;
    private bool _disposed;

    internal DlssFrameGenerationFeature(
        NgxContext context,
        NVSDK_NGX_Handle* handle,
        NVSDK_NGX_Parameter* parameters,
        in DlssFrameGenerationDescription description,
        uint maxGeneratedFrames)
    {
        _context            = context;
        _handle             = handle;
        _parameters         = parameters;
        _maxGeneratedFrames = maxGeneratedFrames;
        _dynamicResolution  = description.DynamicResolution;

        Width            = description.Width;
        Height           = description.Height;
        BackbufferFormat = description.BackbufferFormat;
        RenderWidth      = description.RenderWidth;
        RenderHeight     = description.RenderHeight;
    }

    /// <summary>The backbuffer and output width this feature was created
    /// for.</summary>
    public uint Width { get; }

    /// <summary>The backbuffer and output height this feature was created
    /// for.</summary>
    public uint Height { get; }

    /// <summary>The backbuffer format this feature was created for — and so
    /// the format both outputs must have.</summary>
    public VkFormat BackbufferFormat { get; }

    /// <summary>The depth and motion-vector width this feature was created
    /// for.</summary>
    public uint RenderWidth { get; }

    /// <summary>The depth and motion-vector height this feature was created
    /// for.</summary>
    public uint RenderHeight { get; }

    /// <summary>
    /// The largest <see cref="DlssFrameGenerationInputs.MultiFrameCount"/>
    /// this feature accepts — the owning context's
    /// <see cref="NgxContext.MaxGeneratedFrames"/>. 1 means 2x only, which is
    /// what RTX 40-series GPUs report.
    /// </summary>
    public uint MaxGeneratedFrames => _maxGeneratedFrames;

    /// <summary>
    /// Records one Frame Generation evaluation into <paramref name="recorder"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Rebind after evaluate.</b> <c>EvaluateFeature_C</c> clobbers the
    /// command buffer's bound pipeline, descriptor sets and dynamic state
    /// (DLSS-FG guide l. 302-303). <see cref="CommandRecorder"/> caches no bound
    /// state, so the <i>caller</i> must rebind everything before the next draw
    /// or dispatch.</para>
    /// <para><b>Image layout is the caller's contract</b> and cannot be checked
    /// here: inputs in a shader-read layout, both outputs in
    /// <c>VK_IMAGE_LAYOUT_GENERAL</c>, and this call recorded outside any
    /// <c>BeginRendering</c> scope. See <see cref="DlssFrameGenerationInputs"/>
    /// for the full statement.</para>
    /// <para><b>The NGX API is not thread safe.</b> Under
    /// <see cref="AhjoValidation.Enabled"/> the owning context's re-entrancy
    /// guard reports a concurrent call rather than letting it corrupt the
    /// parameter map.</para>
    /// <para><b>Allocates nothing.</b> The seven resource structs <b>and the
    /// five camera matrices</b> are stack locals of this frame — the parameter
    /// map stores raw pointers to all twelve, and NGX dereferences them inside
    /// <c>EvaluateFeature_C</c>.</para>
    /// </remarks>
    public void Evaluate(ref CommandRecorder recorder, in DlssFrameGenerationInputs inputs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RequireLiveContext(nameof(Evaluate));

        _context.EnterExclusive(nameof(Evaluate));
        try
        {
            if (NgxValidation.IsEnabled)
                ValidateInputs(in inputs, Width, Height, BackbufferFormat, RenderWidth, RenderHeight, _dynamicResolution, _maxGeneratedFrames);

            EvaluateCore((VkCommandBuffer_T*)recorder.RawHandle, in inputs, invokeNgx: true);
        }
        finally
        {
            _context.ExitExclusive();
        }
    }

    /// <summary>
    /// Fills the parameter map from <paramref name="inputs"/> and, when
    /// <paramref name="invokeNgx"/> is <see langword="true"/>, calls
    /// <c>NVSDK_NGX_VULKAN_EvaluateFeature_C</c>.
    /// </summary>
    /// <remarks>
    /// A managed transcription of <c>NGX_VK_EVALUATE_DLSSG</c>
    /// (<c>nvsdk_ngx_helpers_dlssg_vk.h:62</c> onward), limited to the keys this
    /// wrapper exposes. One method and one stack frame in both modes,
    /// deliberately: the resource structs and matrices below are stack locals
    /// whose addresses the map retains, so they must not outlive — or be
    /// separated from — the call that consumes them. <paramref name="invokeNgx"/>
    /// exists so <c>DlssFrameGenerationEvaluateBenchmarks.PackParameters_16</c>
    /// can measure the managed half alone without changing that shape.
    /// </remarks>
    internal void EvaluateCore(VkCommandBuffer_T* commandBuffer, in DlssFrameGenerationInputs inputs, bool invokeNgx)
    {
        NVSDK_NGX_Parameter* parameters = _parameters;

        // Stack locals, all seven. Their addresses go into the map.
        NVSDK_NGX_Resource_VK backbuffer    = inputs.Backbuffer.ToNative(readWrite: false);
        NVSDK_NGX_Resource_VK depth         = inputs.Depth.ToNative(readWrite: false);
        NVSDK_NGX_Resource_VK motionVectors = inputs.MotionVectors.ToNative(readWrite: false);
        // The two read-write slots, set from the slot rather than by the
        // caller (#218 D3).
        NVSDK_NGX_Resource_VK outputInterpolated = inputs.OutputInterpolated.ToNative(readWrite: true);
        NVSDK_NGX_Resource_VK hudless    = default;
        NVSDK_NGX_Resource_VK ui         = default;
        NVSDK_NGX_Resource_VK outputReal = default;

        bool hasHudless    = !inputs.Hudless.IsNull;
        bool hasUi         = !inputs.UiColorAndAlpha.IsNull;
        bool hasOutputReal = !inputs.OutputReal.IsNull;
        if (hasHudless)    hudless    = inputs.Hudless.ToNative(readWrite: false);
        if (hasUi)         ui         = inputs.UiColorAndAlpha.ToNative(readWrite: false);
        if (hasOutputReal) outputReal = inputs.OutputReal.ToNative(readWrite: true);

        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgBackbuffer.Ptr, &backbuffer);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgDepth.Ptr, &depth);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgMVecs.Ptr, &motionVectors);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgOutputInterpolated.Ptr, &outputInterpolated);
        // An absent optional slot is written as null rather than left alone:
        // the map is reused across frames, so a stale pointer from a previous
        // frame would otherwise still be live.
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgHudless.Ptr, hasHudless ? &hudless : null);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgUi.Ptr, hasUi ? &ui : null);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgOutputReal.Ptr, hasOutputReal ? &outputReal : null);

        // The five matrices, COPIED to stack locals before their addresses are
        // taken: `inputs` is an `in` parameter that may alias heap storage (a
        // field of a class), and a pointer into the heap is not stable across a
        // GC. 320 bytes of stack, no pinning (#253 spec D7.1).
        //
        // MEASURE hook (#253 plan step 19a): the copies are as-is. If the dev
        // DLL's Clip-to-Previous-Clip visualizer shows the transposed
        // orientation is required, these five become Matrix4x4.Transpose(...) —
        // a change confined to this block, recorded here with the measurement.
        DlssFrameGenerationCamera camera = inputs.Camera;
        Matrix4x4 viewToClip     = camera.ViewToClip;
        Matrix4x4 clipToView     = camera.ClipToView;
        Matrix4x4 clipToLensClip = camera.ClipToLensClip;
        Matrix4x4 clipToPrevClip = camera.ClipToPrevClip;
        Matrix4x4 prevClipToClip = camera.PrevClipToClip;

        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgCameraViewToClip.Ptr, &viewToClip);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgClipToCameraView.Ptr, &clipToView);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgClipToLensClip.Ptr, &clipToLensClip);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgClipToPrevClip.Ptr, &clipToPrevClip);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgPrevClipToClip.Ptr, &prevClipToClip);

        // Clip-space jitter (nvsdk_ngx_defs_dlssg.h:138-140), not SR's pixels.
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.DlssgJitterOffsetX.Ptr, inputs.JitterOffsetX);
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.DlssgJitterOffsetY.Ptr, inputs.JitterOffsetY);

        // 0f -> 1f, the same rule DlssFeature applies: a caller who built the
        // struct without the parameterless constructor still gets identity
        // scales.
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.DlssgMvecScaleX.Ptr, OneIfZero(inputs.MotionVectorScaleX));
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.DlssgMvecScaleY.Ptr, OneIfZero(inputs.MotionVectorScaleY));

        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.DlssgCameraPinholeOffsetX.Ptr, camera.PinholeOffset.X);
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.DlssgCameraPinholeOffsetY.Ptr, camera.PinholeOffset.Y);

        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.DlssgCameraPosX.Ptr, camera.Position.X);
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.DlssgCameraPosY.Ptr, camera.Position.Y);
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.DlssgCameraPosZ.Ptr, camera.Position.Z);
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.DlssgCameraUpX.Ptr, camera.Up.X);
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.DlssgCameraUpY.Ptr, camera.Up.Y);
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.DlssgCameraUpZ.Ptr, camera.Up.Z);
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.DlssgCameraRightX.Ptr, camera.Right.X);
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.DlssgCameraRightY.Ptr, camera.Right.Y);
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.DlssgCameraRightZ.Ptr, camera.Right.Z);
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.DlssgCameraFwdX.Ptr, camera.Forward.X);
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.DlssgCameraFwdY.Ptr, camera.Forward.Y);
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.DlssgCameraFwdZ.Ptr, camera.Forward.Z);
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.DlssgCameraNear.Ptr, camera.Near);
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.DlssgCameraFar.Ptr, camera.Far);
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.DlssgCameraFov.Ptr, camera.FieldOfView);
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.DlssgCameraAspectRatio.Ptr, camera.AspectRatio);

        // The six flags, as UI the way the helper writes them.
        NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.DlssgColorBuffersHdr.Ptr, inputs.ColorBuffersHdr ? 1u : 0u);
        NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.DlssgDepthInverted.Ptr, inputs.DepthInverted ? 1u : 0u);
        NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.DlssgCameraMotionIncluded.Ptr, inputs.CameraMotionIncluded ? 1u : 0u);
        NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.DlssgReset.Ptr, inputs.Reset ? 1u : 0u);
        NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.DlssgNotRenderingGameFrames.Ptr, inputs.NotRenderingGameFrames ? 1u : 0u);
        NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.DlssgOrthoProjection.Ptr, inputs.OrthographicProjection ? 1u : 0u);

        NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.DlssgMultiFrameCount.Ptr, inputs.MultiFrameCount);
        NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.DlssgMultiFrameIndex.Ptr, inputs.MultiFrameIndex);
        NgxApi.NVSDK_NGX_Parameter_SetULL(parameters, NgxParameterNames.DlssgBackbufferFrameId.Ptr, inputs.BackbufferFrameId);

        // The keys NVIDIA's helper also writes and this wrapper does not
        // expose — the subrects, UIAlpha, the bidirectional distortion field,
        // OutputDisableInterpolation, AutomodeOverrideReset, the
        // MvecInvalidValue/MvecDilated/MenuDetection tuning and the
        // linearized-depth knobs — are deliberately NOT written (#253 spec D7,
        // scope boundary). All-zero subrects mean the full resource.

        // Every exit BELOW this point must leave the map holding no pointer
        // into this frame. The map outlives the frame (it is allocated once and
        // reused), the twelve SetVoidPointer writes above stored the addresses
        // of stack locals — seven resource structs and five matrices — and
        // those locals die at the closing brace, so a return that skipped the
        // clear would leave twelve dangling void* in live NGX state, which
        // Dispose then hands back to ReleaseFeature. Three ways out:
        // invokeNgx: false (the benchmark), a throwing EvaluateFeature_C, and
        // the normal path. The finally covers all three. Same reasoning that
        // already makes an absent optional slot write null rather than be
        // skipped.
        //
        // The try deliberately does NOT open before the writes above. That
        // window is native parameter writes with no allocation and no throw
        // site in it — SetF/SetUI/SetULL/SetVoidPointer are void P/Invokes that
        // cannot fail — so widening it would buy nothing. If a throwing call is
        // ever added above, move the try up with it.
        try
        {
            if (!invokeNgx) return;

            NgxResult.ThrowIfFailed(
                NgxApi.NVSDK_NGX_VULKAN_EvaluateFeature_C(commandBuffer, _handle, parameters, null),
                "DLSS frame generation evaluate");
        }
        finally
        {
            ClearPointerSlots(parameters);
        }
    }

    /// <summary>
    /// Nulls all twelve pointer keys — seven resources, five matrices — so the
    /// reused parameter map never outlives the stack frame whose addresses it
    /// was given.
    /// </summary>
    private static void ClearPointerSlots(NVSDK_NGX_Parameter* parameters)
    {
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgBackbuffer.Ptr, null);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgDepth.Ptr, null);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgMVecs.Ptr, null);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgHudless.Ptr, null);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgUi.Ptr, null);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgOutputInterpolated.Ptr, null);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgOutputReal.Ptr, null);

        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgCameraViewToClip.Ptr, null);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgClipToCameraView.Ptr, null);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgClipToLensClip.Ptr, null);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgClipToPrevClip.Ptr, null);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.DlssgPrevClipToClip.Ptr, null);
    }

    /// <summary>
    /// The <see cref="AhjoValidation.Enabled"/>-gated checks
    /// <see cref="Evaluate"/> runs, over an explicit set of creation-time facts
    /// rather than a created feature's.
    /// </summary>
    /// <remarks>
    /// Static so the tests can drive every branch with no NGX, no NVIDIA driver
    /// and no Vulkan device — a created feature needs all three.
    /// </remarks>
    internal static void ValidateInputs(
        in DlssFrameGenerationInputs inputs,
        uint width,
        uint height,
        VkFormat backbufferFormat,
        uint renderWidth,
        uint renderHeight,
        bool dynamicResolution,
        uint maxGeneratedFrames)
    {
        // 1. Required slots.
        RequireSlot(inputs.Backbuffer, nameof(DlssFrameGenerationInputs.Backbuffer));
        RequireSlot(inputs.Depth, nameof(DlssFrameGenerationInputs.Depth));
        RequireSlot(inputs.MotionVectors, nameof(DlssFrameGenerationInputs.MotionVectors));
        RequireSlot(inputs.OutputInterpolated, nameof(DlssFrameGenerationInputs.OutputInterpolated));

        // 2. Metadata. Extent and format are fields NGX READS out of
        // NVSDK_NGX_ImageViewInfo_VK, so an Image.FromRaw handle's 0x0 /
        // VK_FORMAT_UNDEFINED is a silent wrong answer and fails — unlike the
        // usage bits below (#218 invariant (b)).
        RequireMetadata(inputs.Backbuffer, nameof(DlssFrameGenerationInputs.Backbuffer));
        RequireMetadata(inputs.Depth, nameof(DlssFrameGenerationInputs.Depth));
        RequireMetadata(inputs.MotionVectors, nameof(DlssFrameGenerationInputs.MotionVectors));
        RequireMetadata(inputs.Hudless, nameof(DlssFrameGenerationInputs.Hudless));
        RequireMetadata(inputs.UiColorAndAlpha, nameof(DlssFrameGenerationInputs.UiColorAndAlpha));
        RequireMetadata(inputs.OutputInterpolated, nameof(DlssFrameGenerationInputs.OutputInterpolated));
        RequireMetadata(inputs.OutputReal, nameof(DlssFrameGenerationInputs.OutputReal));

        // 3. Backbuffer extent.
        if (inputs.Backbuffer.Width != width || inputs.Backbuffer.Height != height)
        {
            NgxValidation.Fail("DlssFrameGenerationFeature.Evaluate",
                $"DlssFrameGenerationInputs.Backbuffer is {inputs.Backbuffer.Width}x{inputs.Backbuffer.Height}, but this " +
                $"feature was created for {width}x{height}. Frame Generation does not resize; recreate the feature on " +
                "resize (DLSS-FG guide, When to Recreate the Feature).");
        }

        // 3b. Backbuffer format. Written to DLSSG.BackbufferFormat at create;
        // the outputs are checked against the Backbuffer below, so this is
        // what ties all three to the created feature.
        if (inputs.Backbuffer.Format != backbufferFormat)
        {
            NgxValidation.Fail("DlssFrameGenerationFeature.Evaluate",
                $"DlssFrameGenerationInputs.Backbuffer is {inputs.Backbuffer.Format}, but this feature was created for " +
                $"{backbufferFormat} (DLSSG.BackbufferFormat). The format is a creation-time fact; recreate the feature " +
                "with the new format.");
        }

        // 3c. Depth and motion-vector extent. Only without dynamic resolution:
        // "When dynamic resolution scaling is not enabled, your depth and motion
        // vector resources must match the Render Width and Height specified
        // during feature creation" (DLSS-FG guide l. 3931-3932, 3940-3941);
        // with it, "NGX ignores the InternalWidth/Height parameters" (l.
        // 3918-3920). The guide's "(or its subrect, if used)" does not apply:
        // the wrapper exposes no subrects. Hudless and UI are backbuffer-sized
        // inputs and are deliberately not checked here.
        if (!dynamicResolution)
        {
            RequireRenderExtent(inputs.Depth, renderWidth, renderHeight, nameof(DlssFrameGenerationInputs.Depth));
            RequireRenderExtent(inputs.MotionVectors, renderWidth, renderHeight, nameof(DlssFrameGenerationInputs.MotionVectors));
        }

        // 4. Output shape.
        RequireSameShape(inputs.OutputInterpolated, inputs.Backbuffer, nameof(DlssFrameGenerationInputs.OutputInterpolated));
        RequireSameShape(inputs.OutputReal, inputs.Backbuffer, nameof(DlssFrameGenerationInputs.OutputReal));

        // 5. Usage.
        RequireUsage(inputs.OutputInterpolated, ImageUsage.Storage, nameof(DlssFrameGenerationInputs.OutputInterpolated));
        RequireUsage(inputs.OutputReal, ImageUsage.Storage, nameof(DlssFrameGenerationInputs.OutputReal));
        RequireUsage(inputs.Backbuffer, ImageUsage.Sampled, nameof(DlssFrameGenerationInputs.Backbuffer));
        RequireUsage(inputs.Depth, ImageUsage.Sampled, nameof(DlssFrameGenerationInputs.Depth));
        RequireUsage(inputs.MotionVectors, ImageUsage.Sampled, nameof(DlssFrameGenerationInputs.MotionVectors));
        RequireUsage(inputs.Hudless, ImageUsage.Sampled, nameof(DlssFrameGenerationInputs.Hudless));
        RequireUsage(inputs.UiColorAndAlpha, ImageUsage.Sampled, nameof(DlssFrameGenerationInputs.UiColorAndAlpha));

        // 5b. Advisory, not enforced: with OutputReal bound, Frame Generation
        // vkCmdCopyImage's from the Backbuffer, which then needs TransferSrc
        // (VUID-vkCmdCopyImage-aspect-06662, observed on one driver — RTX 4070
        // Ti / 616.92, #253 PR B). One driver version cannot establish "FG
        // always copies", so this warns through the sink rather than failing —
        // the same stance #218 took on TransferDst for SR's output. Unknown
        // usage (ImageUsage.None) is skipped, as everywhere else.
        if (!inputs.OutputReal.IsNull
            && inputs.Backbuffer.Usage != ImageUsage.None
            && (inputs.Backbuffer.Usage & ImageUsage.TransferSrc) == 0)
        {
            AhjoDiagnostics.Sink(DiagnosticSeverity.Warning, "DlssFrameGenerationFeature.Evaluate",
                $"DlssFrameGenerationInputs.Backbuffer was created with usage {inputs.Backbuffer.Usage}, which does not " +
                "include ImageUsage.TransferSrc, and OutputReal is bound. Frame Generation copies the Backbuffer into " +
                "OutputReal with vkCmdCopyImage, so the validation layer will report " +
                "VUID-vkCmdCopyImage-aspect-06662 (observed on driver 616.92). Add ImageUsage.TransferSrc to the " +
                "Backbuffer's ImageDescription. Advisory only: not enforced.");
        }

        // 6. Multi-frame.
        if (inputs.MultiFrameCount < 1)
        {
            NgxValidation.Fail("DlssFrameGenerationFeature.Evaluate",
                $"DlssFrameGenerationInputs.MultiFrameCount is {inputs.MultiFrameCount}; it must be at least 1 (one " +
                "generated frame per rendered frame, 2x). Build the inputs with the parameterless constructor, which " +
                "defaults it to 1.");
        }

        if (inputs.MultiFrameIndex < 1 || inputs.MultiFrameIndex > inputs.MultiFrameCount)
        {
            NgxValidation.Fail("DlssFrameGenerationFeature.Evaluate",
                $"DlssFrameGenerationInputs.MultiFrameIndex is {inputs.MultiFrameIndex}, outside " +
                $"[1, MultiFrameCount = {inputs.MultiFrameCount}]. The index is 1-based: it names which of the " +
                "MultiFrameCount generated frames this evaluate produces.");
        }

        if (inputs.MultiFrameCount > maxGeneratedFrames)
        {
            NgxValidation.Fail("DlssFrameGenerationFeature.Evaluate",
                $"DlssFrameGenerationInputs.MultiFrameCount is {inputs.MultiFrameCount}, above this device's " +
                $"MaxGeneratedFrames of {maxGeneratedFrames} (DLSSG.MultiFrameCountMax). RTX 40-series GPUs support 2x " +
                "only (MultiFrameCount = 1); Multi-Frame Generation requires an RTX 50-series GPU (DLSS-FG guide, " +
                "System Requirements).");
        }

        static void RequireSlot(in NgxImage image, string slot)
        {
            if (image.IsNull)
            {
                NgxValidation.Fail("DlssFrameGenerationFeature.Evaluate",
                    $"DlssFrameGenerationInputs.{slot} is not set. Backbuffer, Depth, MotionVectors and " +
                    "OutputInterpolated are required; Hudless, UiColorAndAlpha and OutputReal are optional.");
            }
        }

        static void RequireMetadata(in NgxImage image, string slot)
        {
            if (image.IsNull) return;   // optional slot, nothing bound

            if (image.Width == 0 || image.Height == 0)
            {
                NgxValidation.Fail("DlssFrameGenerationFeature.Evaluate",
                    $"DlssFrameGenerationInputs.{slot} reports a {image.Width}x{image.Height} extent, which NGX cannot " +
                    "use: it reads Width and Height out of NVSDK_NGX_ImageViewInfo_VK. A zero extent means the Image came " +
                    "from Image.FromRaw (a swapchain-owned handle), which carries no extent — build the NgxImage from a " +
                    "VMA-created Image instead.");
            }

            if (image.Format == VkFormat.VK_FORMAT_UNDEFINED)
            {
                NgxValidation.Fail("DlssFrameGenerationFeature.Evaluate",
                    $"DlssFrameGenerationInputs.{slot} reports VK_FORMAT_UNDEFINED, which NGX cannot use: it reads Format " +
                    "out of NVSDK_NGX_ImageViewInfo_VK. Set ImageViewDescription.Format explicitly, or build the NgxImage " +
                    "from an Image that knows its own format.");
            }
        }

        static void RequireRenderExtent(in NgxImage image, uint renderWidth, uint renderHeight, string slot)
        {
            if (image.Width != renderWidth || image.Height != renderHeight)
            {
                NgxValidation.Fail("DlssFrameGenerationFeature.Evaluate",
                    $"DlssFrameGenerationInputs.{slot} is {image.Width}x{image.Height}, but this feature was created with " +
                    $"a render extent of {renderWidth}x{renderHeight} (DLSSG.InternalWidth/Height). Without dynamic " +
                    "resolution the depth and motion-vector resources must match it (DLSS-FG guide, InternalWidth / " +
                    "InternalHeight). Recreate the feature with the new render extent, or create it with " +
                    "DynamicResolution = true.");
            }
        }

        static void RequireSameShape(in NgxImage output, in NgxImage backbuffer, string slot)
        {
            if (output.IsNull) return;   // OutputReal is optional

            if (output.Width != backbuffer.Width || output.Height != backbuffer.Height || output.Format != backbuffer.Format)
            {
                NgxValidation.Fail("DlssFrameGenerationFeature.Evaluate",
                    $"DlssFrameGenerationInputs.{slot} is {output.Width}x{output.Height} {output.Format}, but Backbuffer " +
                    $"is {backbuffer.Width}x{backbuffer.Height} {backbuffer.Format}. Frame Generation's outputs must be " +
                    "the same size and format as the backbuffer (DLSS-FG guide l. 764-765).");
            }
        }

        static void RequireUsage(in NgxImage image, ImageUsage required, string slot)
        {
            // Optional slots left unset have nothing to check.
            if (image.IsNull) return;

            // ImageUsage.None is the Image.FromRaw "unknown" state, not "no
            // usage bits", and NGX never reads usage — skipped, not failed.
            // This is NOT a general FromRaw carve-out: RequireMetadata above
            // fails such a handle on its 0x0 extent (#218 invariant (b)).
            if (image.Usage == ImageUsage.None) return;

            if ((image.Usage & required) == 0)
            {
                // OutputReal needs TransferDst too — it is the destination of
                // FG's own Backbuffer -> OutputReal vkCmdCopyImage
                // (VUID-vkCmdCopyImage-aspect-06663). Naming only Storage would
                // send the reader back for a second round trip, the reason SR's
                // message carries the same hint. Advisory, not enforced: one
                // driver (616.92). OutputInterpolated measured clean without
                // TransferDst, so it gets no hint.
                string alsoTransferDst = slot == nameof(DlssFrameGenerationInputs.OutputReal)
                    ? " Give it ImageUsage.TransferDst as well: Frame Generation copies the Backbuffer into this image with vkCmdCopyImage."
                    : string.Empty;

                NgxValidation.Fail("DlssFrameGenerationFeature.Evaluate",
                    $"DlssFrameGenerationInputs.{slot} was created with usage {image.Usage}, which does not include " +
                    $"ImageUsage.{required}. Frame Generation binds this slot as {(required == ImageUsage.Storage ? "a storage image" : "a sampled image")}; " +
                    $"add ImageUsage.{required} to the ImageDescription and recreate it.{alsoTransferDst}");
            }
        }
    }

    /// <summary>
    /// Releases the feature and destroys its parameter map. Idempotent.
    /// </summary>
    /// <remarks>
    /// <para><b>Two preconditions the wrapper cannot check</b>, both of which
    /// the validation layer will report if you get them wrong:</para>
    /// <list type="number">
    ///   <item><description><b>No command buffer that recorded this feature may
    ///   still be in flight.</b> Wait on a fence, or <c>vkDeviceWaitIdle</c>,
    ///   first: <c>ReleaseFeature</c> destroys NGX's own Vulkan objects, and a
    ///   still-executing submit that referenced them is an object-in-use
    ///   violation.</description></item>
    ///   <item><description>Dispose this <b>before</b> the
    ///   <see cref="NgxContext"/> that created it.</description></item>
    /// </list>
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;
        RequireLiveContext(nameof(Dispose));
        _disposed = true;

        if (_handle != null)
        {
            NgxApi.NVSDK_NGX_VULKAN_ReleaseFeature(_handle);
            _handle = null;
        }

        if (_parameters != null)
        {
            NgxApi.NVSDK_NGX_VULKAN_DestroyParameters(_parameters);
            _parameters = null;
        }
    }

    /// <summary>
    /// Fails when the owning <see cref="NgxContext"/> has already shut NGX down
    /// for the device — the same checkable precondition
    /// <see cref="DlssFeature"/> checks.
    /// </summary>
    private void RequireLiveContext(string operation)
    {
        if (!NgxValidation.IsEnabled || !_context.IsDisposed) return;

        NgxValidation.Fail("DlssFrameGenerationFeature",
            $"DlssFrameGenerationFeature.{operation} was called after its NgxContext was disposed. " +
            "NVSDK_NGX_VULKAN_Shutdown1 has already destroyed the driver-side objects this would touch. Dispose every " +
            "DlssFrameGenerationFeature BEFORE the NgxContext that created it.");
    }

    private static float OneIfZero(float value) => value == 0f ? 1f : value;
}
