using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Ngx.Native;

namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// One created DLSS feature: a fixed render extent, output extent and quality
/// mode, plus the parameter map it reuses every frame. Created by
/// <see cref="NgxContext.CreateDlss"/>.
/// </summary>
/// <remarks>
/// <para>A resolution or quality-mode change means disposing this and creating
/// another — those are creation-time facts.</para>
/// <para>Dispose it <b>before</b> the <see cref="NgxContext"/> that created
/// it.</para>
/// </remarks>
public sealed unsafe class DlssFeature : IDisposable
{
    private readonly NgxContext _context;
    private NVSDK_NGX_Handle*    _handle;
    private NVSDK_NGX_Parameter* _parameters;
    private readonly bool _freeMemoryOnRelease;
    private bool _disposed;

    internal DlssFeature(
        NgxContext context,
        NVSDK_NGX_Handle* handle,
        NVSDK_NGX_Parameter* parameters,
        in DlssFeatureDescription description,
        uint minRenderWidth,
        uint minRenderHeight,
        uint maxRenderWidth,
        uint maxRenderHeight)
    {
        _context             = context;
        _handle              = handle;
        _parameters          = parameters;
        _freeMemoryOnRelease = description.FreeMemoryOnRelease;

        RenderWidth  = description.RenderWidth;
        RenderHeight = description.RenderHeight;
        OutputWidth  = description.OutputWidth;
        OutputHeight = description.OutputHeight;

        MinRenderWidth  = minRenderWidth;
        MinRenderHeight = minRenderHeight;
        MaxRenderWidth  = maxRenderWidth;
        MaxRenderHeight = maxRenderHeight;
    }

    /// <summary>The render width this feature was created for.</summary>
    public uint RenderWidth { get; }

    /// <summary>The render height this feature was created for.</summary>
    public uint RenderHeight { get; }

    /// <summary>The output width this feature produces.</summary>
    public uint OutputWidth { get; }

    /// <summary>The output height this feature produces.</summary>
    public uint OutputHeight { get; }

    /// <summary>Smallest render width accepted by
    /// <see cref="DlssEvaluateInputs.RenderWidth"/>.</summary>
    public uint MinRenderWidth { get; }

    /// <summary>Smallest render height accepted by
    /// <see cref="DlssEvaluateInputs.RenderHeight"/>.</summary>
    public uint MinRenderHeight { get; }

    /// <summary>Largest render width accepted by
    /// <see cref="DlssEvaluateInputs.RenderWidth"/>.</summary>
    public uint MaxRenderWidth { get; }

    /// <summary>Largest render height accepted by
    /// <see cref="DlssEvaluateInputs.RenderHeight"/>.</summary>
    public uint MaxRenderHeight { get; }

    /// <summary>
    /// Records one DLSS evaluation into <paramref name="recorder"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Rebind after evaluate.</b> <c>EvaluateFeature_C</c> clobbers the
    /// command buffer's bound pipeline, descriptor sets and dynamic state (DLSS
    /// Programming Guide §5.2.5). <see cref="CommandRecorder"/> caches no bound
    /// state, so there is nothing for the wrapper to invalidate — but the
    /// <i>caller</i> must rebind everything before the next draw or
    /// dispatch.</para>
    /// <para><b>Image layout is the caller's contract</b> and cannot be checked
    /// here: inputs in a shader-read layout, <see cref="DlssEvaluateInputs.Output"/>
    /// in <c>VK_IMAGE_LAYOUT_GENERAL</c>, and this call recorded outside any
    /// <c>BeginRendering</c> scope. See <see cref="DlssEvaluateInputs"/> for the
    /// full statement and why the wrapper cannot enforce it.</para>
    /// <para><b>The NGX API is not thread safe.</b> Under
    /// <see cref="AhjoValidation.Enabled"/> the owning context's re-entrancy
    /// guard reports a concurrent call rather than letting it corrupt the
    /// parameter map.</para>
    /// <para><b>Allocates nothing.</b> The resource structs are stack locals of
    /// this frame — they have to be, because the parameter map stores raw
    /// pointers to them and NGX dereferences those inside
    /// <c>EvaluateFeature_C</c>. That is why there is no separate "prepare"
    /// step to hoist out of the frame loop (spec E6/D9).</para>
    /// </remarks>
    public void Evaluate(ref CommandRecorder recorder, in DlssEvaluateInputs inputs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RequireLiveContext(nameof(Evaluate));

        _context.EnterExclusive(nameof(Evaluate));
        try
        {
            if (NgxValidation.IsEnabled)
                ValidateInputs(in inputs);

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
    /// One method and one stack frame in both modes, deliberately: the resource
    /// structs below are stack locals whose addresses the map retains, so they
    /// must not outlive — or be separated from — the call that consumes them
    /// (spec E6). <paramref name="invokeNgx"/> exists so
    /// <c>DlssEvaluateBenchmarks.PackParameters_16</c> can measure the managed
    /// half alone without changing that shape.
    /// </remarks>
    internal void EvaluateCore(VkCommandBuffer_T* commandBuffer, in DlssEvaluateInputs inputs, bool invokeNgx)
    {
        NVSDK_NGX_Parameter* parameters = _parameters;

        // Stack locals, all six. Their addresses go into the map.
        NVSDK_NGX_Resource_VK color         = inputs.Color.ToNative(readWrite: false);
        NVSDK_NGX_Resource_VK depth         = inputs.Depth.ToNative(readWrite: false);
        NVSDK_NGX_Resource_VK motionVectors = inputs.MotionVectors.ToNative(readWrite: false);
        // The one read-write slot, set from the slot rather than by the caller
        // (spec D3).
        NVSDK_NGX_Resource_VK output        = inputs.Output.ToNative(readWrite: true);
        NVSDK_NGX_Resource_VK exposure      = default;
        NVSDK_NGX_Resource_VK bias          = default;

        bool hasExposure = !inputs.ExposureTexture.IsNull;
        bool hasBias     = !inputs.BiasCurrentColorMask.IsNull;
        if (hasExposure) exposure = inputs.ExposureTexture.ToNative(readWrite: false);
        if (hasBias)     bias     = inputs.BiasCurrentColorMask.ToNative(readWrite: false);

        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.Color.Ptr, &color);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.Output.Ptr, &output);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.Depth.Ptr, &depth);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.MotionVectors.Ptr, &motionVectors);
        // An absent optional slot is written as null rather than left alone:
        // the map is reused across frames, so a stale pointer from a previous
        // frame would otherwise still be live.
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.ExposureTexture.Ptr, hasExposure ? &exposure : null);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.BiasCurrentColorMask.Ptr, hasBias ? &bias : null);

        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.JitterOffsetX.Ptr, inputs.JitterOffsetX);
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.JitterOffsetY.Ptr, inputs.JitterOffsetY);
        NgxApi.NVSDK_NGX_Parameter_SetI(parameters, NgxParameterNames.Reset.Ptr, inputs.Reset ? 1 : 0);

        // 0f -> 1f on all four, the helper's own behaviour
        // (nvsdk_ngx_helpers_vk.h:177-178, :224-225): a caller who built the
        // struct without the parameterless constructor still gets identity
        // scales rather than a black frame.
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.MvScaleX.Ptr, OneIfZero(inputs.MotionVectorScaleX));
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.MvScaleY.Ptr, OneIfZero(inputs.MotionVectorScaleY));

        NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.RenderSubrectWidth.Ptr, inputs.RenderWidth);
        NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.RenderSubrectHeight.Ptr, inputs.RenderHeight);

        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.PreExposure.Ptr, OneIfZero(inputs.PreExposure));
        NgxApi.NVSDK_NGX_Parameter_SetF(parameters, NgxParameterNames.ExposureScale.Ptr, OneIfZero(inputs.ExposureScale));

        DlssSubrects subrects = inputs.Subrects;
        NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.ColorSubrectBaseX.Ptr, subrects.ColorBaseX);
        NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.ColorSubrectBaseY.Ptr, subrects.ColorBaseY);
        NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.DepthSubrectBaseX.Ptr, subrects.DepthBaseX);
        NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.DepthSubrectBaseY.Ptr, subrects.DepthBaseY);
        NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.MotionVectorsSubrectBaseX.Ptr, subrects.MotionVectorsBaseX);
        NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.MotionVectorsSubrectBaseY.Ptr, subrects.MotionVectorsBaseY);
        NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.BiasCurrentColorSubrectBaseX.Ptr, subrects.BiasCurrentColorBaseX);
        NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.BiasCurrentColorSubrectBaseY.Ptr, subrects.BiasCurrentColorBaseY);
        NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.OutputSubrectBaseX.Ptr, subrects.OutputBaseX);
        NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.OutputSubrectBaseY.Ptr, subrects.OutputBaseY);

        // The research-only slots NVIDIA's helper also writes — GBuffer.*,
        // MotionVectors3D, IsParticleMask, AnimatedTextureMask, DepthHighRes,
        // Position.ViewSpace, FrameTimeDeltaInMsec, RayTracingHitDistance,
        // MotionVectorsReflection, TonemapperType, TransparencyMask — are
        // deliberately NOT written: this wrapper never sets them, so writing
        // null on every frame would be ~11 wasted native calls (spec D9).
        // Sharpness is not written either: DLSS sharpening is deprecated.
        // The debug-overlay keys are not exposed at all.

        // Every exit BELOW this point must leave the map holding no pointer
        // into this frame. The map outlives the frame (it is allocated once and
        // reused), the six SetVoidPointer writes above stored the addresses of
        // stack locals, and those locals die at the closing brace — so a return
        // that skipped the clear would leave six dangling void* in live NGX
        // state, which Dispose then hands back to ReleaseFeature. Three ways
        // out: invokeNgx: false (the benchmark), a throwing EvaluateFeature_C,
        // and the normal path. The finally covers all three. Same reasoning
        // that already makes an absent optional slot write null rather than be
        // skipped.
        //
        // The try deliberately does NOT open before the writes above. That
        // window is ~25 more native parameter writes with no allocation and no
        // throw site in it — SetF/SetI/SetUI/SetVoidPointer are void P/Invokes
        // that cannot fail — so widening it would buy nothing and would put the
        // clear on a path where the map was never populated. If a throwing call
        // is ever added above, move the try up with it.
        try
        {
            if (!invokeNgx) return;

            NgxResult.ThrowIfFailed(
                NgxApi.NVSDK_NGX_VULKAN_EvaluateFeature_C(commandBuffer, _handle, parameters, null),
                "DLSS evaluate");
        }
        finally
        {
            ClearResourceSlots(parameters);
        }
    }

    /// <summary>
    /// Nulls the six resource keys, so the reused parameter map never outlives
    /// the stack frame whose addresses it was given.
    /// </summary>
    private static void ClearResourceSlots(NVSDK_NGX_Parameter* parameters)
    {
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.Color.Ptr, null);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.Output.Ptr, null);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.Depth.Ptr, null);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.MotionVectors.Ptr, null);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.ExposureTexture.Ptr, null);
        NgxApi.NVSDK_NGX_Parameter_SetVoidPointer(parameters, NgxParameterNames.BiasCurrentColorMask.Ptr, null);
    }

    /// <summary>
    /// The <see cref="AhjoValidation.Enabled"/>-gated checks
    /// <see cref="Evaluate"/> runs. Factored out so it can be driven without a
    /// device.
    /// </summary>
    internal void ValidateInputs(in DlssEvaluateInputs inputs)
        => ValidateInputs(in inputs, MinRenderWidth, MinRenderHeight, MaxRenderWidth, MaxRenderHeight);

    /// <summary>
    /// The checks themselves, over an explicit dynamic range rather than a
    /// created feature's.
    /// </summary>
    /// <remarks>
    /// Static so the tests can drive every branch with no NGX, no NVIDIA driver
    /// and no Vulkan device (spec D13) — a created <see cref="DlssFeature"/>
    /// needs all three.
    /// </remarks>
    internal static void ValidateInputs(
        in DlssEvaluateInputs inputs,
        uint minRenderWidth,
        uint minRenderHeight,
        uint maxRenderWidth,
        uint maxRenderHeight)
    {
        RequireSlot(inputs.Color, nameof(DlssEvaluateInputs.Color));
        RequireSlot(inputs.Depth, nameof(DlssEvaluateInputs.Depth));
        RequireSlot(inputs.MotionVectors, nameof(DlssEvaluateInputs.MotionVectors));
        RequireSlot(inputs.Output, nameof(DlssEvaluateInputs.Output));

        // Extent and format are metadata NGX genuinely READS
        // (NVSDK_NGX_ImageViewInfo_VK.Width/Height/Format), so unlike the usage
        // bits below there is no benign "unknown" here: an Image.FromRaw handle
        // reports 0x0 and VK_FORMAT_UNDEFINED (Resources/Image.cs:84-85), and
        // forwarding those to EvaluateFeature_C is a silent wrong answer.
        RequireMetadata(inputs.Color, nameof(DlssEvaluateInputs.Color));
        RequireMetadata(inputs.Depth, nameof(DlssEvaluateInputs.Depth));
        RequireMetadata(inputs.MotionVectors, nameof(DlssEvaluateInputs.MotionVectors));
        RequireMetadata(inputs.Output, nameof(DlssEvaluateInputs.Output));
        RequireMetadata(inputs.ExposureTexture, nameof(DlssEvaluateInputs.ExposureTexture));
        RequireMetadata(inputs.BiasCurrentColorMask, nameof(DlssEvaluateInputs.BiasCurrentColorMask));

        if (inputs.Output.Width < 32 || inputs.Output.Height < 32)
        {
            NgxValidation.Fail("DlssFeature.Evaluate",
                $"DlssEvaluateInputs.Output is {inputs.Output.Width}x{inputs.Output.Height}; DLSS requires an output of at " +
                "least 32x32 (DLSS Programming Guide §3.3).");
        }

        if (inputs.RenderWidth < minRenderWidth || inputs.RenderWidth > maxRenderWidth)
        {
            NgxValidation.Fail("DlssFeature.Evaluate",
                $"DlssEvaluateInputs.RenderWidth is {inputs.RenderWidth}, outside this feature's dynamic range " +
                $"[{minRenderWidth}, {maxRenderWidth}]. Recreate the feature to change the range, or clamp the frame's " +
                "render width into it.");
        }

        if (inputs.RenderHeight < minRenderHeight || inputs.RenderHeight > maxRenderHeight)
        {
            NgxValidation.Fail("DlssFeature.Evaluate",
                $"DlssEvaluateInputs.RenderHeight is {inputs.RenderHeight}, outside this feature's dynamic range " +
                $"[{minRenderHeight}, {maxRenderHeight}]. Recreate the feature to change the range, or clamp the frame's " +
                "render height into it.");
        }

        RequireUsage(inputs.Output, ImageUsage.Storage, nameof(DlssEvaluateInputs.Output));
        RequireUsage(inputs.Color, ImageUsage.Sampled, nameof(DlssEvaluateInputs.Color));
        RequireUsage(inputs.Depth, ImageUsage.Sampled, nameof(DlssEvaluateInputs.Depth));
        RequireUsage(inputs.MotionVectors, ImageUsage.Sampled, nameof(DlssEvaluateInputs.MotionVectors));
        RequireUsage(inputs.ExposureTexture, ImageUsage.Sampled, nameof(DlssEvaluateInputs.ExposureTexture));
        RequireUsage(inputs.BiasCurrentColorMask, ImageUsage.Sampled, nameof(DlssEvaluateInputs.BiasCurrentColorMask));

        static void RequireMetadata(in NgxImage image, string slot)
        {
            if (image.IsNull) return;   // optional slot, nothing bound

            if (image.Width == 0 || image.Height == 0)
            {
                NgxValidation.Fail("DlssFeature.Evaluate",
                    $"DlssEvaluateInputs.{slot} reports a {image.Width}x{image.Height} extent, which NGX cannot use: it " +
                    "reads Width and Height out of NVSDK_NGX_ImageViewInfo_VK. A zero extent means the Image came from " +
                    "Image.FromRaw (a swapchain-owned handle), which carries no extent — build the NgxImage from a " +
                    "VMA-created Image instead.");
            }

            if (image.Format == VkFormat.VK_FORMAT_UNDEFINED)
            {
                NgxValidation.Fail("DlssFeature.Evaluate",
                    $"DlssEvaluateInputs.{slot} reports VK_FORMAT_UNDEFINED, which NGX cannot use: it reads Format out of " +
                    "NVSDK_NGX_ImageViewInfo_VK and validates it against the formats DLSS supports (guide §3.3). Set " +
                    "ImageViewDescription.Format explicitly, or build the NgxImage from an Image that knows its own format.");
            }
        }

        static void RequireSlot(in NgxImage image, string slot)
        {
            if (image.IsNull)
            {
                NgxValidation.Fail("DlssFeature.Evaluate",
                    $"DlssEvaluateInputs.{slot} is not set. Color, Depth, MotionVectors and Output are all required; " +
                    "only ExposureTexture and BiasCurrentColorMask are optional.");
            }
        }

        static void RequireUsage(in NgxImage image, ImageUsage required, string slot)
        {
            // Optional slots left unset have nothing to check.
            if (image.IsNull) return;

            // ImageUsage.None is the Image.FromRaw "unknown" state, not "no
            // usage bits" (spec E3, Resources/Image.cs:84-85), and NGX never
            // reads usage — so an unknown one is genuinely harmless and is
            // skipped rather than failed.
            //
            // This is NOT a general "FromRaw handles are fine" carve-out, and
            // saying so matters because the asymmetry reads like an
            // inconsistency: RequireMetadata above FAILS such a handle on its
            // 0x0 extent, because extent is a field NGX does read. Usage is
            // skipped on its own merits, not because the slot came from FromRaw.
            if (image.Usage == ImageUsage.None) return;

            if ((image.Usage & required) == 0)
            {
                // The Storage slot is Output, and Output needs TransferDst too —
                // DLSS clears it itself with vkCmdClearColorImage
                // (VUID-vkCmdClearColorImage-image-00002). Naming only Storage
                // here would send the reader back for a second round trip.
                // Advisory, not enforced: one driver version cannot establish
                // "DLSS always clears".
                string alsoTransferDst = required == ImageUsage.Storage
                    ? " Give it ImageUsage.TransferDst as well: DLSS clears this image itself with vkCmdClearColorImage."
                    : string.Empty;

                NgxValidation.Fail("DlssFeature.Evaluate",
                    $"DlssEvaluateInputs.{slot} was created with usage {image.Usage}, which does not include " +
                    $"ImageUsage.{required}. DLSS binds this slot as {(required == ImageUsage.Storage ? "a storage image" : "a sampled image")}; " +
                    $"add ImageUsage.{required} to the ImageDescription and recreate it.{alsoTransferDst}");
            }
        }
    }

    /// <summary>
    /// Releases the feature and destroys its parameter map. Idempotent.
    /// </summary>
    /// <remarks>
    /// <para><b>Two preconditions the wrapper cannot check</b>, both of which the
    /// validation layer will report if you get them wrong:</para>
    /// <list type="number">
    ///   <item><description><b>No command buffer that recorded this feature may
    ///   still be in flight.</b> Wait on a fence, or
    ///   <c>vkDeviceWaitIdle</c>, first. <c>ReleaseFeature</c> destroys NGX's own
    ///   <c>VkImage</c> / <c>VkImageView</c> / <c>VkDeviceMemory</c> objects
    ///   through the loader, so a still-executing submit that referenced them is
    ///   an object-in-use violation like any other (DLSS Programming Guide
    ///   §5.5).</description></item>
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
            if (_freeMemoryOnRelease && _parameters != null)
            {
                // Guide §3.14: read by ReleaseFeature, so it has to be in the
                // map before the release, not after.
                NgxApi.NVSDK_NGX_Parameter_SetI(_parameters, NgxParameterNames.FreeMemOnReleaseFeature.Ptr, 1);
            }

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
    /// for the device.
    /// </summary>
    /// <remarks>
    /// Unlike the image-layout contract this one <b>is</b> checkable — the
    /// context is a field and both types live in this assembly — so it is
    /// checked rather than documented. <c>NVSDK_NGX_VULKAN_Shutdown1</c>
    /// destroys the driver-side objects that <c>EvaluateFeature_C</c> and
    /// <c>ReleaseFeature</c> then operate on, so either call after it is
    /// use-after-free inside NVIDIA's runtime — a crash with no diagnostic,
    /// which is precisely the class of failure worth spending a branch on.
    /// </remarks>
    private void RequireLiveContext(string operation)
    {
        if (!NgxValidation.IsEnabled || !_context.IsDisposed) return;

        NgxValidation.Fail("DlssFeature",
            $"DlssFeature.{operation} was called after its NgxContext was disposed. NVSDK_NGX_VULKAN_Shutdown1 has " +
            "already destroyed the driver-side objects this would touch. Dispose every DlssFeature BEFORE the " +
            "NgxContext that created it.");
    }

    private static float OneIfZero(float value) => value == 0f ? 1f : value;
}
