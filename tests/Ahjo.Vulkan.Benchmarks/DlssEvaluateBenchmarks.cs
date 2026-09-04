using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Ngx;
using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Issue 218: <see cref="DlssFeature.Evaluate"/> is a per-frame path, so it
/// carries the same <b>0 B per call</b> obligation as everything under
/// <c>Recording/</c> even though it lives in a different package. Four
/// properties hold that (spec D9), and this class is what keeps them true:
/// the six <c>NVSDK_NGX_Resource_VK</c> values are stack locals, the ~30
/// parameter names are <c>static readonly Utf8Name</c> fields, the parameter
/// map is allocated once at feature creation, and the command buffer arrives
/// as <c>ref CommandRecorder</c>.
/// </summary>
/// <remarks>
/// <para>Deliberately a separate class from <see cref="CommandRecorderBenchmarks"/>,
/// for the reason <see cref="MeshShaderBenchmarks"/> and
/// <see cref="DescriptorSetPoolVariableCountBenchmarks"/> are separate: this
/// <see cref="Setup"/> requires an NVIDIA GPU, a DLSS-capable driver <b>and</b>
/// a consumer-supplied <c>nvngx_dlss.dll</c>, and a host without them must not
/// take the issue-29 canary (<c>CommandRecorder.RenderingPass100Cmds</c>) down
/// with it.</para>
/// <para><see cref="Setup"/> <b>throws</b> rather than skipping. BenchmarkDotNet
/// has no skip, and a filtered-in benchmark that silently measures nothing is
/// worse than a loud failure — the stance
/// <c>MeshShaderBenchmarks.cs:124-127</c> already takes.</para>
/// </remarks>
[MemoryDiagnoser]
public unsafe class DlssEvaluateBenchmarks
{
    // 16, not the *_1024 used elsewhere: one DLSS evaluate records many
    // dispatches, not one command, so a thousand of them would produce a
    // command buffer nothing resembles in practice (and a very slow run).
    private const int EvaluatesPerInvoke = 16;

    private const uint OutputWidth  = 3840;
    private const uint OutputHeight = 2160;

    private Instance          _instance = null!;
    private Device            _device   = null!;
    private CommandBufferPool _cmdPool  = null!;
    private NgxContext        _ngx      = null!;
    private DlssFeature       _feature  = null!;

    private Image _color, _depth, _motionVectors, _output, _exposure, _biasMask;
    private NgxImage _colorView, _depthView, _motionVectorsView, _outputView;
    private NgxImage _exposureView, _biasMaskView;

    // Built once in Setup so the measured body constructs nothing of its own.
    private DlssEvaluateInputs _inputs;

    // The same inputs with both optional slots bound. Without this, hasExposure
    // and hasBias are always false, ToNative runs 4x instead of 6x, and the two
    // ternaries at the SetVoidPointer calls are only ever measured on their null
    // leg — which is exactly the shape a future allocation would hide in.
    private DlssEvaluateInputs _inputsAllSlots;

    [GlobalSetup]
    public void Setup()
    {
        var description = new NgxDescription
        {
            ProjectId     = "8d19b5b3-8f7d-4a2f-9f6e-6c2d9a1f4e73",
            EngineVersion = "0.1.0-benchmarks",
            // The developer-machine location ./tools/setup-ngx.ps1 stages the
            // rel/ feature DLL into. Git-ignored and never packed; this is a
            // convenience for running the benchmark from a checkout, not a
            // deployment path.
            DlssSearchPaths = StagedFeatureDllPaths(),
        };

        // Both extension lists are mandatory, not advisory: without the
        // instance ones NGX access-violates, without the device ones DLSS
        // reports unavailable. See NgxSupport's remarks.
        if (!NgxSupport.TryGetInstanceExtensions(in description, out NgxExtensionSet? instanceExtensions))
        {
            throw new InvalidOperationException(
                "NGX could not report its required instance extensions. The ahjo_ngx shim is probably not staged — " +
                "run ./tools/setup-ngx.ps1 and rebuild.");
        }

        using (instanceExtensions)
        {
            var instanceDescription = new InstanceDescription { Extensions = instanceExtensions.Names };
            _instance = Instance.Create(in instanceDescription);
        }

        uint family = uint.MaxValue;
        PhysicalDevice gpu = _instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    family = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });

        if (!NgxSupport.IsSuperSamplingSupported(gpu, in description))
        {
            throw new InvalidOperationException(
                "DLSS Super Resolution is not supported on this GPU/driver. DlssEvaluateBenchmarks needs an NVIDIA GPU " +
                "with a DLSS-capable driver; there is no software fallback.");
        }

        NgxSupport.TryGetDeviceExtensions(gpu, in description, out NgxExtensionSet? deviceExtensions);
        using (deviceExtensions)
        {
            var deviceDescription = new DeviceDescription
            {
                Queues     = [new QueueRequest(family, count: 1, priority: 1.0f)],
                Extensions = deviceExtensions is null ? default : deviceExtensions.Names,
            };
            _device = gpu.CreateDevice(in deviceDescription);
        }

        _cmdPool = new CommandBufferPool(_device, family);

        try
        {
            _ngx = NgxContext.Create(_device, in description);
        }
        catch (NgxFeatureLibraryNotFoundException ex)
        {
            throw new InvalidOperationException(
                "nvngx_dlss.dll was not found. It is NOT shipped by Ahjo.Vulkan.Ngx — place NVIDIA's rel/ feature DLL " +
                "beside the benchmark binary, or point NgxDescription.DlssSearchPaths at it. " + ex.Message, ex);
        }

        DlssOptimalSettings settings = _ngx.GetOptimalSettings(OutputWidth, OutputHeight, DlssQualityMode.MaxQuality);
        if (!settings.IsAvailable)
        {
            throw new InvalidOperationException(
                $"DLSS MaxQuality is not offered at {OutputWidth}x{OutputHeight} on this GPU.");
        }

        Allocator allocator = _device.Allocator;
        _color         = CreateImage(allocator, VkFormat.VK_FORMAT_R16G16B16A16_SFLOAT, settings.RenderWidth, settings.RenderHeight, ImageUsage.Sampled | ImageUsage.Storage | ImageUsage.ColorAttachment);
        _depth         = CreateImage(allocator, VkFormat.VK_FORMAT_D32_SFLOAT, settings.RenderWidth, settings.RenderHeight, ImageUsage.Sampled | ImageUsage.DepthStencilAttachment);
        _motionVectors = CreateImage(allocator, VkFormat.VK_FORMAT_R16G16_SFLOAT, settings.RenderWidth, settings.RenderHeight, ImageUsage.Sampled | ImageUsage.Storage | ImageUsage.ColorAttachment);
        // TransferDst because DLSS clears this image itself — see
        // DlssEvaluateInputs.Output.
        _output        = CreateImage(allocator, VkFormat.VK_FORMAT_R16G16B16A16_SFLOAT, OutputWidth, OutputHeight, ImageUsage.Storage | ImageUsage.Sampled | ImageUsage.TransferDst);

        // The two optional slots. Exposure is 1x1 (a single value, the shape
        // NGX documents); the bias mask is render-resolution.
        _exposure = CreateImage(allocator, VkFormat.VK_FORMAT_R32_SFLOAT, 1, 1, ImageUsage.Sampled);
        _biasMask = CreateImage(allocator, VkFormat.VK_FORMAT_R8_UNORM, settings.RenderWidth, settings.RenderHeight, ImageUsage.Sampled);

        _colorView         = NgxImage.CreateView(_device, in _color, ColorView);
        _depthView         = NgxImage.CreateView(_device, in _depth, DepthView);
        _motionVectorsView = NgxImage.CreateView(_device, in _motionVectors, ColorView);
        _outputView        = NgxImage.CreateView(_device, in _output, ColorView);
        _exposureView      = NgxImage.CreateView(_device, in _exposure, ColorView);
        _biasMaskView      = NgxImage.CreateView(_device, in _biasMask, ColorView);

        // CreateFeature1 records real initialization work, so the recorder that
        // carries it must be submitted and completed before the first Evaluate.
        Queue queue = _device.GetQueue(family, queueIndex: 0);
        DlssFeature? created = null;
        queue.ImmediateSubmit(_cmdPool, (ref CommandRecorder recorder) =>
        {
            created = _ngx.CreateDlss(ref recorder, new DlssFeatureDescription
            {
                RenderWidth  = settings.RenderWidth,
                RenderHeight = settings.RenderHeight,
                OutputWidth  = OutputWidth,
                OutputHeight = OutputHeight,
                Mode         = DlssQualityMode.MaxQuality,
                Flags        = DlssFeatureFlags.MotionVectorsLowRes | DlssFeatureFlags.AutoExposure,
            });
        });
        _feature = created!;

        _inputs = new DlssEvaluateInputs
        {
            Color         = _colorView,
            Depth         = _depthView,
            MotionVectors = _motionVectorsView,
            Output        = _outputView,
            RenderWidth   = settings.RenderWidth,
            RenderHeight  = settings.RenderHeight,
            JitterOffsetX = 0.25f,
            JitterOffsetY = -0.25f,
        };

        _inputsAllSlots = _inputs with
        {
            ExposureTexture      = _exposureView,
            BiasCurrentColorMask = _biasMaskView,
        };
    }

    /// <summary>
    /// Sixteen full <see cref="DlssFeature.Evaluate"/> calls recorded into one
    /// command buffer per invoke. Never submitted: this measures the managed
    /// recording cost, not GPU time.
    /// </summary>
    [Benchmark(OperationsPerInvoke = EvaluatesPerInvoke)]
    public void Evaluate_16()
    {
        // Dispose the recorder BEFORE ResetForFrame — Retire fires on Dispose,
        // not End, so the buffer must reach _spent before the reset drains
        // _spent -> _idle. Backwards, the pool ping-pongs two buffers and the
        // numbers go bimodal (#188/#199, docs/benchmarks.md:109).
        // try/finally rather than `using`: a using variable cannot be passed
        // by ref, and Evaluate takes `ref CommandRecorder`.
        CommandRecorder recorder = _cmdPool.Begin();
        try
        {
            for (int i = 0; i < EvaluatesPerInvoke; i++)
                _feature.Evaluate(ref recorder, in _inputs);
            recorder.End();
        }
        finally
        {
            recorder.Dispose();
        }

        _cmdPool.ResetForFrame();
    }

    /// <summary>
    /// The same sixteen iterations through the parameter-population seam with
    /// <c>EvaluateFeature_C</c> skipped: parameter-map population plus
    /// resource-struct fill, which is exactly the managed half issue #218 asks
    /// to measure.
    /// </summary>
    [Benchmark(OperationsPerInvoke = EvaluatesPerInvoke)]
    public void PackParameters_16()
    {
        CommandRecorder recorder = _cmdPool.Begin();
        try
        {
            var commandBuffer = (VkCommandBuffer_T*)recorder.RawHandle;
            for (int i = 0; i < EvaluatesPerInvoke; i++)
                _feature.EvaluateCore(commandBuffer, in _inputs, invokeNgx: false);
            recorder.End();
        }
        finally
        {
            recorder.Dispose();
        }

        _cmdPool.ResetForFrame();
    }

    /// <summary>
    /// <see cref="PackParameters_16"/> with <b>both optional slots bound</b>, so
    /// the two extra <c>NgxImage.ToNative</c> calls and the non-null leg of both
    /// <c>SetVoidPointer</c> ternaries are measured. The delta against the row
    /// above is the cost of the optional slots; the point of the row is that
    /// neither leg allocates.
    /// </summary>
    [Benchmark(OperationsPerInvoke = EvaluatesPerInvoke)]
    public void PackParameters_16_AllSlots()
    {
        CommandRecorder recorder = _cmdPool.Begin();
        try
        {
            var commandBuffer = (VkCommandBuffer_T*)recorder.RawHandle;
            for (int i = 0; i < EvaluatesPerInvoke; i++)
                _feature.EvaluateCore(commandBuffer, in _inputsAllSlots, invokeNgx: false);
            recorder.End();
        }
        finally
        {
            recorder.Dispose();
        }

        _cmdPool.ResetForFrame();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _feature?.Dispose();

        _biasMaskView.Dispose();
        _exposureView.Dispose();
        _outputView.Dispose();
        _motionVectorsView.Dispose();
        _depthView.Dispose();
        _colorView.Dispose();

        _biasMask.Dispose();
        _exposure.Dispose();
        _output.Dispose();
        _motionVectors.Dispose();
        _depth.Dispose();
        _color.Dispose();

        _ngx?.Dispose();
        _cmdPool?.Dispose();
        _device?.Dispose();
        _instance?.Dispose();
    }

    /// <summary>
    /// Walks up from the benchmark binary to the repository root and points at
    /// the staged <c>rel/</c> feature DLL, if one is there. Empty otherwise —
    /// <see cref="Setup"/> then fails with the actionable message rather than
    /// here.
    /// </summary>
    private static string[] StagedFeatureDllPaths()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "native", "ngx")))
            directory = directory.Parent;

        if (directory is null) return [];

        string rid = OperatingSystem.IsWindows() ? "win-x64" : "linux-x64";
        string staged = Path.Combine(directory.FullName, "native", "ngx", "staged", rid, "rel");
        return Directory.Exists(staged) ? [staged] : [];
    }

    private static ImageViewDescription ColorView =>
        new() { Aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT };

    private static ImageViewDescription DepthView =>
        new() { Aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_DEPTH_BIT };

    private static Image CreateImage(Allocator allocator, VkFormat format, uint width, uint height, ImageUsage usage)
        => allocator.CreateImage(
            new ImageDescription { Format = format, Width = width, Height = height, Usage = usage },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });
}
