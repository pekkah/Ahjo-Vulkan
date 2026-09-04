using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Ngx.Native;

namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// NGX, initialized against one <see cref="Device"/>. One per device; the entry
/// point for every DLSS query and for creating a <see cref="DlssFeature"/>.
/// </summary>
/// <remarks>
/// <para><b>Not thread safe.</b> The NGX API is not (DLSS Programming Guide
/// §5.2.4) and this context's capability parameter map is mutable shared state
/// that <see cref="GetOptimalSettings"/> writes into. Under
/// <see cref="AhjoValidation.Enabled"/> a re-entrancy guard says so with a
/// message rather than letting two threads corrupt the map; with validation off
/// it is one predictable branch.</para>
/// <para><b>Dispose every <see cref="DlssFeature"/> first.</b>
/// <see cref="Dispose"/> shuts NGX down for the device.</para>
/// </remarks>
public sealed unsafe class NgxContext : IDisposable
{
    private readonly Device _device;
    private nint _loader;
    private NVSDK_NGX_Parameter* _capabilityParameters;

    private readonly bool _superSamplingAvailable;
    private readonly uint _minDriverVersionMajor;
    private readonly uint _minDriverVersionMinor;

    // Re-entrancy guard. 0 = free, 1 = a call is inside. Only touched when
    // validation is on.
    private int _busy;
    private bool _disposed;

    private NgxContext(
        Device device,
        nint loader,
        NVSDK_NGX_Parameter* capabilityParameters,
        bool superSamplingAvailable,
        uint minDriverVersionMajor,
        uint minDriverVersionMinor)
    {
        _device                 = device;
        _loader                 = loader;
        _capabilityParameters   = capabilityParameters;
        _superSamplingAvailable = superSamplingAvailable;
        _minDriverVersionMajor  = minDriverVersionMajor;
        _minDriverVersionMinor  = minDriverVersionMinor;
    }

    /// <summary>
    /// Initializes NGX for <paramref name="device"/> and reads DLSS's
    /// availability off the capability parameter map.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="description"/> is
    /// malformed — see <see cref="NgxDescription.ProjectId"/>.</exception>
    /// <exception cref="NgxFeatureLibraryNotFoundException">
    /// NVIDIA's feature library was not found. The message lists every
    /// directory searched. This package does not ship it — see the package
    /// README.
    /// </exception>
    /// <exception cref="NgxDriverTooOldException">The installed NVIDIA driver
    /// is below the minimum DLSS reports.</exception>
    /// <exception cref="NgxException">Any other NGX failure.</exception>
    /// <remarks>
    /// <para><b>A successful <c>Init</c> does not mean DLSS works.</b> The
    /// feature library is located later, and the capability map is the
    /// documented probe — so this method reads that map and throws rather than
    /// handing back a context whose first <see cref="CreateDlss"/> fails
    /// opaquely.</para>
    /// <para><b><paramref name="device"/> must have been created with the
    /// extensions <see cref="NgxSupport.TryGetDeviceExtensions"/> reports, and
    /// its instance with those <see cref="NgxSupport.TryGetInstanceExtensions"/>
    /// reports.</b> Without the device ones, <c>Init</c> succeeds and the
    /// capability map then reports DLSS unavailable with
    /// <c>FAIL_PlatformError</c> — measured on an RTX 4070 Ti / driver 610.47.
    /// See <see cref="NgxSupport"/> for the full order.</para>
    /// <para>The context loads the Vulkan loader itself and holds the OS handle
    /// for its lifetime, the way <c>Allocator.Create</c> does: NGX needs raw
    /// <c>vkGetInstanceProcAddr</c> / <c>vkGetDeviceProcAddr</c> pointers, and
    /// <c>[DllImport]</c> statics expose none (CS8757).</para>
    /// </remarks>
    public static NgxContext Create(Device device, in NgxDescription description)
    {
        ArgumentNullException.ThrowIfNull(device);
        description.Validate();

        nint loader = NgxLoader.Load();
        NVSDK_NGX_Parameter* capabilityParameters = null;
        bool initialized = false;

        try
        {
            // Both are typed delegate* unmanaged[Cdecl] by the generator (the
            // rsp parses at a fixed Linux target, where NVSDK_CONV is empty and
            // ClangSharp defaults to Cdecl). On the two RIDs NGX ships for —
            // both x86-64 — Cdecl and Vulkan's VKAPI_PTR are the same ABI, and
            // the address arrives as an nint from the loader, so no conversion
            // between two delegate types ever happens (spec E12).
            var getInstanceProcAddr =
                (delegate* unmanaged[Cdecl]<VkInstance_T*, sbyte*, delegate* unmanaged[Cdecl]<void>>)
                NgxLoader.GetExport(loader, "vkGetInstanceProcAddr");
            var getDeviceProcAddr =
                (delegate* unmanaged[Cdecl]<VkDevice_T*, sbyte*, delegate* unmanaged[Cdecl]<void>>)
                NgxLoader.GetExport(loader, "vkGetDeviceProcAddr");

            description.MeasureUtf8(out int byteCapacity, out int stringCapacity);
            var block = new NgxUtf8Block(byteCapacity, stringCapacity);
            NVSDK_NGX_Result initResult;
            try
            {
                AhjoNgxInitInfo info = description.ToNative(ref block);

                // The block only has to survive the call: the shim copies and
                // retains every string on the init path (spec E5).
                initResult = NgxApi.ahjo_ngx_vulkan_init_utf8(
                    &info,
                    (VkInstance_T*)(nint)device.PhysicalDevice.Instance.RawHandle,
                    (VkPhysicalDevice_T*)(nint)device.PhysicalDevice.RawHandle,
                    (VkDevice_T*)(nint)device.RawHandle,
                    getInstanceProcAddr,
                    getDeviceProcAddr);
            }
            finally
            {
                block.Dispose();
            }

            if (!NgxResult.Succeeded(initResult))
            {
                ThrowDiagnosed(
                    initResult,
                    featureInitResult: NVSDK_NGX_Result.NVSDK_NGX_Result_Success,
                    needsUpdatedDriver: 0, minDriverMajor: 0, minDriverMinor: 0,
                    in description, "NGX Vulkan initialization");
            }

            initialized = true;

            NgxResult.ThrowIfFailed(
                NgxApi.NVSDK_NGX_VULKAN_GetCapabilityParameters(&capabilityParameters),
                "NVSDK_NGX_VULKAN_GetCapabilityParameters");

            // The availability triple. THIS, not Init's return code, is where a
            // missing nvngx_dlss.dll shows up (spec D5).
            int  available          = GetIntOrZero(capabilityParameters, NgxParameterNames.SuperSamplingAvailable);
            int  needsUpdatedDriver = GetIntOrZero(capabilityParameters, NgxParameterNames.SuperSamplingNeedsUpdatedDriver);
            uint featureInitResult  = GetUIntOrZero(capabilityParameters, NgxParameterNames.SuperSamplingFeatureInitResult);
            uint minMajor           = GetUIntOrZero(capabilityParameters, NgxParameterNames.SuperSamplingMinDriverVersionMajor);
            uint minMinor           = GetUIntOrZero(capabilityParameters, NgxParameterNames.SuperSamplingMinDriverVersionMinor);

            if (available == 0)
            {
                ThrowDiagnosed(
                    NVSDK_NGX_Result.NVSDK_NGX_Result_Success,
                    (NVSDK_NGX_Result)featureInitResult,
                    (uint)needsUpdatedDriver, minMajor, minMinor,
                    in description, "DLSS Super Resolution capability query");
            }

            var context = new NgxContext(device, loader, capabilityParameters, superSamplingAvailable: true, minMajor, minMinor);
            loader               = 0;      // ownership transferred to the context
            capabilityParameters = null;
            return context;
        }
        finally
        {
            // Only the failure path runs these: the success path zeroed both.
            if (capabilityParameters != null) NgxApi.NVSDK_NGX_VULKAN_DestroyParameters(capabilityParameters);
            if (initialized && loader != 0)   NgxApi.NVSDK_NGX_VULKAN_Shutdown1((VkDevice_T*)(nint)device.RawHandle);
            if (loader != 0)                  NativeLibrary.Free(loader);
        }
    }

    /// <summary>
    /// Whether DLSS Super Resolution is available on this device. Always
    /// <see langword="true"/> for a context that was constructed successfully —
    /// <see cref="Create"/> throws rather than returning an unusable context —
    /// and kept as a property so a caller holding a context can assert it.
    /// </summary>
    public bool IsSuperSamplingAvailable => _superSamplingAvailable;

    /// <summary>
    /// What render extent DLSS wants for <paramref name="outputWidth"/> ×
    /// <paramref name="outputHeight"/> at <paramref name="mode"/>, plus its
    /// dynamic-resolution range.
    /// </summary>
    /// <remarks>
    /// <para>A managed transcription of <c>NGX_DLSS_GET_OPTIMAL_SETTINGS</c>
    /// (<c>nvsdk_ngx_helpers.h:64-113</c>) against the <b>capability</b> map —
    /// the callback only exists there, not on an
    /// <c>AllocateParameters</c> map (spec E10).</para>
    /// <para>Not a pure read: it writes <c>Width</c>, <c>Height</c>,
    /// <c>PerfQualityValue</c> and <c>RTXValue</c> into the shared capability
    /// map, which is half the reason for the re-entrancy guard.</para>
    /// <para>A mode this GPU does not offer comes back with
    /// <see cref="DlssOptimalSettings.IsAvailable"/> <see langword="false"/> and
    /// every dimension zero, rather than throwing: a settings screen
    /// enumerates all six modes, and an exception per unavailable one turns a
    /// normal query into control flow.</para>
    /// </remarks>
    public DlssOptimalSettings GetOptimalSettings(uint outputWidth, uint outputHeight, DlssQualityMode mode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        EnterExclusive(nameof(GetOptimalSettings));
        try
        {
            void* callback = null;
            NgxApi.NVSDK_NGX_Parameter_GetVoidPointer(
                _capabilityParameters, NgxParameterNames.OptimalSettingsCallback.Ptr, &callback);

            if (callback == null)
            {
                // The header's own two causes, carried verbatim in substance.
                throw new NgxException(
                    NVSDK_NGX_Result.NVSDK_NGX_Result_FAIL_OutOfDate,
                    "DLSS did not publish its optimal-settings callback. Either the installed nvngx_dlss.dll is out of " +
                    "date and does not support this query, or the parameter map came from NVSDK_NGX_AllocateParameters " +
                    "instead of NVSDK_NGX_GetCapabilityParameters (nvsdk_ngx_helpers.h:79-83).");
            }

            NgxApi.NVSDK_NGX_Parameter_SetUI(_capabilityParameters, NgxParameterNames.Width.Ptr, outputWidth);
            NgxApi.NVSDK_NGX_Parameter_SetUI(_capabilityParameters, NgxParameterNames.Height.Ptr, outputHeight);
            NgxApi.NVSDK_NGX_Parameter_SetI(_capabilityParameters, NgxParameterNames.PerfQualityValue.Ptr, (int)mode);
            // "Some older DLSS dlls still expect this value to be set"
            // (nvsdk_ngx_helpers.h:89).
            NgxApi.NVSDK_NGX_Parameter_SetI(_capabilityParameters, NgxParameterNames.RtxValue.Ptr, 0);

            var invoke = (delegate* unmanaged[Cdecl]<NVSDK_NGX_Parameter*, NVSDK_NGX_Result>)callback;
            NgxResult.ThrowIfFailed(invoke(_capabilityParameters), "DLSS optimal-settings query");

            uint renderWidth  = GetUIntOrZero(_capabilityParameters, NgxParameterNames.OutWidth);
            uint renderHeight = GetUIntOrZero(_capabilityParameters, NgxParameterNames.OutHeight);

            // Seed min/max from the optimal pair BEFORE reading the dynamic
            // keys: an older feature DLL leaves those unset, and the helper
            // does exactly this so the range degrades to a single point rather
            // than to zero.
            uint minWidth  = renderWidth,  minHeight  = renderHeight;
            uint maxWidth  = renderWidth,  maxHeight  = renderHeight;
            TryGetUInt(_capabilityParameters, NgxParameterNames.DynamicMinRenderWidth,  ref minWidth);
            TryGetUInt(_capabilityParameters, NgxParameterNames.DynamicMinRenderHeight, ref minHeight);
            TryGetUInt(_capabilityParameters, NgxParameterNames.DynamicMaxRenderWidth,  ref maxWidth);
            TryGetUInt(_capabilityParameters, NgxParameterNames.DynamicMaxRenderHeight, ref maxHeight);

            // Sharpness is deliberately NOT read: DLSS sharpening is
            // deprecated (guide §3.5, #214).

            return ProjectOptimalSettings(renderWidth, renderHeight, minWidth, minHeight, maxWidth, maxHeight);
        }
        finally
        {
            ExitExclusive();
        }
    }

    /// <summary>
    /// What DLSS reports about its own video-memory use. Meaningful once at
    /// least one <see cref="DlssFeature"/> exists.
    /// </summary>
    /// <returns><see langword="false"/> when the installed feature library
    /// publishes no stats callback; then <paramref name="stats"/> is
    /// <c>default</c>.</returns>
    public bool TryGetStats(out DlssStats stats)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        EnterExclusive(nameof(TryGetStats));
        try
        {
            void* callback = null;
            NgxApi.NVSDK_NGX_Parameter_GetVoidPointer(
                _capabilityParameters, NgxParameterNames.GetStatsCallback.Ptr, &callback);

            if (callback == null)
            {
                stats = default;
                return false;
            }

            var invoke = (delegate* unmanaged[Cdecl]<NVSDK_NGX_Parameter*, NVSDK_NGX_Result>)callback;
            if (!NgxResult.Succeeded(invoke(_capabilityParameters)))
            {
                stats = default;
                return false;
            }

            ulong bytes = 0;
            NgxApi.NVSDK_NGX_Parameter_GetULL(_capabilityParameters, NgxParameterNames.SizeInBytes.Ptr, &bytes);

            // OptLevel and IsDevSnippetBranch by their string names. NVIDIA's
            // helper reads them through the excluded NVSDK_NGX_EParameter_*
            // hash aliases; that the string forms are accepted as equivalents
            // was measured rather than assumed — DlssStats' remarks carry the
            // numbers and the negative control. A feature library that does not
            // publish them leaves the fields at their defaults instead of
            // failing the query.
            uint optLevel = 0;
            uint isDevSnippetBranch = 0;
            NgxApi.NVSDK_NGX_Parameter_GetUI(_capabilityParameters, NgxParameterNames.OptLevel.Ptr, &optLevel);
            NgxApi.NVSDK_NGX_Parameter_GetUI(_capabilityParameters, NgxParameterNames.IsDevSnippetBranch.Ptr, &isDevSnippetBranch);

            stats = new DlssStats
            {
                VramAllocatedBytes = bytes,
                OptLevel           = optLevel,
                IsDevSnippetBranch = isDevSnippetBranch != 0,
            };
            return true;
        }
        finally
        {
            ExitExclusive();
        }
    }

    /// <summary>
    /// Creates a DLSS feature, recording its initialization work into
    /// <paramref name="recorder"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Submit <paramref name="recorder"/> and wait for it to complete
    /// before the first <see cref="DlssFeature.Evaluate"/>.</b>
    /// <c>NVSDK_NGX_VULKAN_CreateFeature1</c> records real GPU work here; an
    /// evaluate against a feature whose initialization has not executed is
    /// undefined.</para>
    /// <para>Transcribes <c>NGX_VULKAN_CREATE_DLSS_EXT1</c>
    /// (<c>nvsdk_ngx_helpers_vk.h:113-135</c>) with a non-null device, so it
    /// takes the multi-device <c>CreateFeature1</c> path.</para>
    /// </remarks>
    public DlssFeature CreateDlss(ref CommandRecorder recorder, in DlssFeatureDescription description)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateFeatureDescription(in description);

        EnterExclusive(nameof(CreateDlss));
        try
        {
            NVSDK_NGX_Parameter* parameters = null;
            // Only ever reached from an initialized context, which is what
            // satisfies #216 OPEN-2 (whether AllocateParameters works before
            // Init is unknown) by construction rather than by a probe.
            NgxResult.ThrowIfFailed(
                NgxApi.NVSDK_NGX_VULKAN_AllocateParameters(&parameters),
                "NVSDK_NGX_VULKAN_AllocateParameters");

            try
            {
                NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.CreationNodeMask.Ptr, 1);
                NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.VisibilityNodeMask.Ptr, 1);
                NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.Width.Ptr, description.RenderWidth);
                NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.Height.Ptr, description.RenderHeight);
                NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.OutWidth.Ptr, description.OutputWidth);
                NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, NgxParameterNames.OutHeight.Ptr, description.OutputHeight);
                NgxApi.NVSDK_NGX_Parameter_SetI(parameters, NgxParameterNames.PerfQualityValue.Ptr, (int)description.Mode);
                NgxApi.NVSDK_NGX_Parameter_SetI(parameters, NgxParameterNames.DlssFeatureCreateFlags.Ptr, (int)description.Flags);
                NgxApi.NVSDK_NGX_Parameter_SetI(parameters, NgxParameterNames.DlssEnableOutputSubrects.Ptr, description.EnableOutputSubrects ? 1 : 0);

                if (description.Preset != DlssPreset.Default)
                {
                    // NGX keys presets per quality mode, so exactly one hint key
                    // is written — the one matching the mode this feature is
                    // being created with.
                    NgxApi.NVSDK_NGX_Parameter_SetUI(parameters, PresetKeyFor(description.Mode).Ptr, (uint)description.Preset);
                }

                NVSDK_NGX_Handle* handle = null;
                NgxResult.ThrowIfFailed(
                    NgxApi.NVSDK_NGX_VULKAN_CreateFeature1(
                        (VkDevice_T*)(nint)_device.RawHandle,
                        (VkCommandBuffer_T*)recorder.RawHandle,
                        NVSDK_NGX_Feature.NVSDK_NGX_Feature_SuperSampling,
                        parameters,
                        &handle),
                    "NVSDK_NGX_VULKAN_CreateFeature1");

                // The created feature's own dynamic range, read once so the
                // per-frame validation has numbers to compare against without
                // touching NGX.
                uint minWidth  = description.RenderWidth,  minHeight  = description.RenderHeight;
                uint maxWidth  = description.RenderWidth,  maxHeight  = description.RenderHeight;
                TryGetUInt(parameters, NgxParameterNames.DynamicMinRenderWidth,  ref minWidth);
                TryGetUInt(parameters, NgxParameterNames.DynamicMinRenderHeight, ref minHeight);
                TryGetUInt(parameters, NgxParameterNames.DynamicMaxRenderWidth,  ref maxWidth);
                TryGetUInt(parameters, NgxParameterNames.DynamicMaxRenderHeight, ref maxHeight);

                var feature = new DlssFeature(
                    this, handle, parameters, in description,
                    minWidth, minHeight, maxWidth, maxHeight);
                parameters = null;   // ownership transferred to the feature
                return feature;
            }
            finally
            {
                if (parameters != null) NgxApi.NVSDK_NGX_VULKAN_DestroyParameters(parameters);
            }
        }
        finally
        {
            ExitExclusive();
        }
    }

    /// <summary>
    /// Destroys the capability parameter map, shuts NGX down for the device and
    /// releases the Vulkan loader handle. Idempotent.
    /// </summary>
    /// <remarks>
    /// <para>Ordering, all three of which the wrapper cannot enforce:</para>
    /// <list type="number">
    ///   <item><description><b>Dispose every <see cref="DlssFeature"/> this
    ///   context created first.</b> Releasing a feature after its context has
    ///   shut down is undefined.</description></item>
    ///   <item><description><b>No command buffer that recorded a
    ///   <see cref="CreateDlss"/> or <see cref="DlssFeature.Evaluate"/> may still
    ///   be in flight.</b> <c>Shutdown1</c> tears down NGX's driver-side objects,
    ///   and a still-executing submit that referenced them is an object-in-use
    ///   violation (DLSS Programming Guide §5.5). Wait on a fence or
    ///   <c>vkDeviceWaitIdle</c> first.</description></item>
    ///   <item><description>Dispose this <b>before</b> the <see cref="Device"/>
    ///   it was created against — the device handle is what
    ///   <c>Shutdown1</c> is given.</description></item>
    /// </list>
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_capabilityParameters != null)
        {
            NgxApi.NVSDK_NGX_VULKAN_DestroyParameters(_capabilityParameters);
            _capabilityParameters = null;
        }

        NgxApi.NVSDK_NGX_VULKAN_Shutdown1((VkDevice_T*)(nint)_device.RawHandle);

        if (_loader != 0)
        {
            NativeLibrary.Free(_loader);
            _loader = 0;
        }
    }

    /// <summary>
    /// Turns NGX's six raw numbers into a <see cref="DlssOptimalSettings"/>.
    /// </summary>
    /// <remarks>
    /// A 0x0 render extent is not a size, it is the mode saying "not on this
    /// GPU" (guide §5.2.8): it is reported as unavailability with every
    /// dimension zero, rather than as a shape a caller who ignores the flag
    /// might allocate.
    /// <para><c>internal</c> so the availability semantics are testable with no
    /// device (spec D13).</para>
    /// </remarks>
    internal static DlssOptimalSettings ProjectOptimalSettings(
        uint renderWidth, uint renderHeight,
        uint minWidth, uint minHeight,
        uint maxWidth, uint maxHeight)
    {
        if (renderWidth == 0 || renderHeight == 0)
            return new DlssOptimalSettings { IsAvailable = false };

        return new DlssOptimalSettings
        {
            IsAvailable     = true,
            RenderWidth     = renderWidth,
            RenderHeight    = renderHeight,
            MinRenderWidth  = minWidth,
            MinRenderHeight = minHeight,
            MaxRenderWidth  = maxWidth,
            MaxRenderHeight = maxHeight,
        };
    }

    // ---- the re-entrancy guard (spec D5 / E10) ------------------------------

    /// <summary>
    /// Whether <see cref="Dispose"/> has run. Read by <see cref="DlssFeature"/>
    /// so a feature can refuse to touch NGX after its context shut it down.
    /// </summary>
    internal bool IsDisposed => _disposed;

    /// <summary>
    /// Claims exclusive use of the context. A single predictable branch when
    /// validation is off — the cost model
    /// <c>Diagnostics/AhjoValidation.cs:41-46</c> already commits to.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void EnterExclusive(string operation)
    {
        if (!NgxValidation.IsEnabled) return;
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            NgxValidation.Fail("NgxContext",
                $"NgxContext.{operation} was entered while another NGX call on the same context was still running. " +
                "The NGX API is not thread safe (DLSS Programming Guide §5.2.4) and this context's capability " +
                "parameter map is mutable shared state — GetOptimalSettings writes into it. Serialize NGX calls, " +
                "or give each thread its own device and context.");
        }
    }

    /// <summary>Releases what <see cref="EnterExclusive"/> claimed. Always from
    /// a <c>finally</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ExitExclusive()
    {
        if (!NgxValidation.IsEnabled) return;
        Interlocked.Exchange(ref _busy, 0);
    }

    // ---- logging ------------------------------------------------------------

    /// <summary>
    /// NGX's logging callback. <c>[UnmanagedCallersOnly]</c> rather than a
    /// managed delegate: no <c>GCHandle</c> to keep alive and nothing
    /// trim-hostile, which is what the AOT invariant wants.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    internal static void LogThunk(sbyte* message, NVSDK_NGX_Logging_Level level, NVSDK_NGX_Feature feature)
    {
        try
        {
            // Info for everything: NGX's logging level is a verbosity dial the
            // caller set, not a per-message severity. Mapping Verbose to a
            // lower severity than On would invert that, and NGX has no error
            // level to map at all — genuine failures come back as an
            // NVSDK_NGX_Result, not as a log line.
            AhjoDiagnostics.Sink(
                DiagnosticSeverity.Info, "NGX", $"[NGX {feature}/{level}] {NgxUtf8.ToString(message)}");
        }
        catch
        {
            // Never throw across the unmanaged-to-managed boundary — the same
            // contract Instance.DefaultCallback holds for the Vulkan loader.
        }
    }

    // ---- helpers -------------------------------------------------------------

    private static Utf8Name PresetKeyFor(DlssQualityMode mode) => mode switch
    {
        DlssQualityMode.Dlaa             => NgxParameterNames.HintRenderPresetDlaa,
        DlssQualityMode.MaxQuality       => NgxParameterNames.HintRenderPresetQuality,
        DlssQualityMode.Balanced         => NgxParameterNames.HintRenderPresetBalanced,
        DlssQualityMode.MaxPerformance   => NgxParameterNames.HintRenderPresetPerformance,
        DlssQualityMode.UltraPerformance => NgxParameterNames.HintRenderPresetUltraPerformance,
        DlssQualityMode.UltraQuality     => NgxParameterNames.HintRenderPresetUltraQuality,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Not a DLSS quality mode."),
    };

    /// <summary>
    /// Always-on checks on the create description. Setup-time, so the message
    /// strings cost nothing that matters, and a bad extent here would otherwise
    /// surface as an opaque NGX result.
    /// </summary>
    private static void ValidateFeatureDescription(in DlssFeatureDescription description)
    {
        if (description.RenderWidth == 0 || description.RenderHeight == 0)
        {
            throw new ArgumentException(
                $"DlssFeatureDescription render extent is {description.RenderWidth}x{description.RenderHeight}; both " +
                "dimensions must be non-zero. Take them from NgxContext.GetOptimalSettings, and check its IsAvailable first.",
                nameof(description));
        }

        if (description.OutputWidth < 32 || description.OutputHeight < 32)
        {
            throw new ArgumentException(
                $"DlssFeatureDescription output extent is {description.OutputWidth}x{description.OutputHeight}; DLSS " +
                "requires at least 32x32 (DLSS Programming Guide §3.3).",
                nameof(description));
        }

        if (description.RenderWidth > description.OutputWidth || description.RenderHeight > description.OutputHeight)
        {
            throw new ArgumentException(
                $"DlssFeatureDescription render extent {description.RenderWidth}x{description.RenderHeight} exceeds the " +
                $"output extent {description.OutputWidth}x{description.OutputHeight}. DLSS upscales; it does not downscale. " +
                "For render == output, use DlssQualityMode.Dlaa.",
                nameof(description));
        }
    }

    /// <summary>
    /// Maps a failed init or an unavailable feature onto the most specific
    /// exception the evidence supports (spec D5).
    /// </summary>
    private static void ThrowDiagnosed(
        NVSDK_NGX_Result result,
        NVSDK_NGX_Result featureInitResult,
        uint needsUpdatedDriver,
        uint minDriverMajor,
        uint minDriverMinor,
        in NgxDescription description,
        string operation)
    {
        const NVSDK_NGX_Result NotFound = NVSDK_NGX_Result.NVSDK_NGX_Result_FAIL_FeatureNotFound;

        if (result == NotFound || featureInitResult == NotFound)
            throw new NgxFeatureLibraryNotFoundException(NotFound, BuildMissingLibraryMessage(in description));

        if (needsUpdatedDriver != 0)
        {
            throw new NgxDriverTooOldException(
                NVSDK_NGX_Result.NVSDK_NGX_Result_FAIL_FeatureNotSupported,
                $"DLSS is unavailable: the installed NVIDIA driver is older than DLSS requires. " +
                $"Minimum driver version: {minDriverMajor}.{minDriverMinor}.",
                minDriverMajor, minDriverMinor);
        }

        // When the call itself succeeded, the feature's own verdict is the
        // interesting one.
        NVSDK_NGX_Result reported = NgxResult.Succeeded(result) ? featureInitResult : result;
        if (NgxResult.Succeeded(reported))
        {
            // Available == 0 with no reason recorded anywhere. Say exactly
            // that instead of "failed: Success".
            throw new NgxException(
                NVSDK_NGX_Result.NVSDK_NGX_Result_FAIL_FeatureNotSupported,
                $"{operation}: NGX reports DLSS Super Resolution as unavailable on this device and gave no reason " +
                "(SuperSampling.Available is 0, SuperSampling.FeatureInitResult is Success). The GPU most likely " +
                "does not support DLSS.");
        }

        throw new NgxException(reported, NgxResult.Format(reported, operation));
    }

    private static string BuildMissingLibraryMessage(in NgxDescription description)
    {
        string expected = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "nvngx_dlss.dll"
            : "libnvidia-ngx-dlss.so.<version>";

        var text = new StringBuilder(512);
        text.Append("DLSS is unavailable: the NVIDIA feature library was not found.\n");
        text.Append("Expected file: ").Append(expected).Append('\n');
        text.Append("Searched:\n  ").Append(AppContext.BaseDirectory).Append('\n');

        IReadOnlyList<string>? paths = description.DlssSearchPaths;
        for (int i = 0; i < (paths?.Count ?? 0); i++)
            text.Append("  ").Append(paths![i]).Append('\n');

        text.Append("This library is NOT shipped by Ahjo.Vulkan.Ngx — the application supplies it from NVIDIA's DLSS SDK ");
        text.Append("(https://github.com/NVIDIA/DLSS, lib/<plat>/rel/). See docs/ngx-notes.md.");
        return text.ToString();
    }

    private static int GetIntOrZero(NVSDK_NGX_Parameter* parameters, Utf8Name name)
    {
        int value = 0;
        NgxApi.NVSDK_NGX_Parameter_GetI(parameters, name.Ptr, &value);
        return value;
    }

    private static uint GetUIntOrZero(NVSDK_NGX_Parameter* parameters, Utf8Name name)
    {
        uint value = 0;
        NgxApi.NVSDK_NGX_Parameter_GetUI(parameters, name.Ptr, &value);
        return value;
    }

    /// <summary>
    /// Overwrites <paramref name="value"/> only when the key is present and the
    /// read succeeds — the "seed then refine" shape both
    /// <c>NGX_DLSS_GET_OPTIMAL_SETTINGS</c> and the create path need for older
    /// feature DLLs that leave the dynamic keys unset.
    /// </summary>
    private static void TryGetUInt(NVSDK_NGX_Parameter* parameters, Utf8Name name, ref uint value)
    {
        uint read = 0;
        if (NgxResult.Succeeded(NgxApi.NVSDK_NGX_Parameter_GetUI(parameters, name.Ptr, &read)) && read != 0)
            value = read;
    }
}
