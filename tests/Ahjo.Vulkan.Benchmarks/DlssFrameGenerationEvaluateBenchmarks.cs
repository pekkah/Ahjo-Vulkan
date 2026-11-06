using System.Numerics;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Ngx;
using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Issue 253: <see cref="DlssFrameGenerationFeature.Evaluate"/> is the second
/// per-frame NGX path, so it carries the same <b>0 B per call</b> obligation as
/// <see cref="DlssFeature.Evaluate"/>. The same four properties hold it (#218
/// spec D9), plus one (#253 spec D7.1): the seven
/// <c>NVSDK_NGX_Resource_VK</c> values <b>and the five camera matrices</b> are
/// stack locals, the ~50 parameter names are <c>static readonly Utf8Name</c>
/// fields, the parameter map is allocated once at feature creation, and the
/// command buffer arrives as <c>ref CommandRecorder</c>.
/// </summary>
/// <remarks>
/// <para>Deliberately a separate class from <see cref="DlssEvaluateBenchmarks"/>
/// and <see cref="CommandRecorderBenchmarks"/> (#253 spec E11): this
/// <see cref="Setup"/> additionally requires an RTX 40-series or newer GPU and
/// a consumer-supplied <c>nvngx_dlssg.dll</c>. A host with Super Resolution but
/// no Frame Generation must still be able to run the SR class, and no NGX host
/// requirement may take the issue-29 canary
/// (<c>CommandRecorder.RenderingPass100Cmds</c>) down with it.</para>
/// <para><see cref="Setup"/> <b>throws</b> rather than skipping, for the reason
/// <see cref="DlssEvaluateBenchmarks"/> gives.</para>
/// </remarks>
[MemoryDiagnoser]
public unsafe class DlssFrameGenerationEvaluateBenchmarks
{
    // 16, as on the SR class: one evaluate records many dispatches.
    private const int EvaluatesPerInvoke = 16;

    private const uint Width  = 1920;
    private const uint Height = 1080;

    private const VkFormat ColorFormat = VkFormat.VK_FORMAT_R8G8B8A8_UNORM;

    private Instance                   _instance = null!;
    private Device                     _device   = null!;
    private CommandBufferPool          _cmdPool  = null!;
    private NgxContext                 _ngx      = null!;
    private DlssFrameGenerationFeature _feature  = null!;

    private Image _backbuffer, _depth, _motionVectors, _hudless, _ui, _outputInterpolated, _outputReal;
    private NgxImage _backbufferView, _depthView, _motionVectorsView, _hudlessView, _uiView;
    private NgxImage _outputInterpolatedView, _outputRealView;

    // Built once in Setup so the measured body constructs nothing of its own.
    // Every optional slot is bound, so every ToNative and the non-null leg of
    // every SetVoidPointer ternary is measured.
    private DlssFrameGenerationInputs _inputs;

    // Only the four required slots plus the camera: Hudless, UiColorAndAlpha
    // and OutputReal unbound, so the null leg of all three optional-slot
    // branches (and their SetVoidPointer ternaries) is the one measured.
    private DlssFrameGenerationInputs _inputsRequiredOnly;

    [GlobalSetup]
    public void Setup()
    {
        // "Validation off" is part of what is measured: the frame cost of a
        // shipping build. Release already defaults it off; say so explicitly.
        AhjoValidation.Enabled = false;

        var description = new NgxDescription
        {
            ProjectId     = "8d19b5b3-8f7d-4a2f-9f6e-6c2d9a1f4e73",
            EngineVersion = "0.1.0-benchmarks",
            // Developer-machine staging (./tools/setup-ngx.ps1); never a
            // deployment path.
            DlssSearchPaths  = StagedFeatureDllPaths(),
            RequiredFeatures = NgxFeatures.FrameGeneration,
        };

        NgxExtensionSet? instanceExtensions = null;
        if (!NgxSupport.TryGetInstanceExtensions(NgxFeature.FrameGeneration, in description, out instanceExtensions))
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

        if (!NgxSupport.IsSupported(NgxFeature.FrameGeneration, gpu, in description))
        {
            throw new InvalidOperationException(
                "DLSS Frame Generation is not supported on this GPU/driver. DlssFrameGenerationEvaluateBenchmarks needs " +
                "an RTX 40-series or newer GPU with a DLSS-capable driver; there is no software fallback.");
        }

        NgxSupport.TryGetDeviceExtensions(NgxFeature.FrameGeneration, gpu, in description, out NgxExtensionSet? deviceExtensions);
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
                "nvngx_dlssg.dll was not found. It is NOT shipped by Ahjo.Vulkan.Ngx — place NVIDIA's rel/ feature DLL " +
                "beside the benchmark binary, or point NgxDescription.DlssSearchPaths at it. " + ex.Message, ex);
        }

        Allocator allocator = _device.Allocator;
        _backbuffer         = CreateImage(allocator, ColorFormat, ImageUsage.Sampled | ImageUsage.ColorAttachment | ImageUsage.TransferSrc);
        _depth              = CreateImage(allocator, VkFormat.VK_FORMAT_D32_SFLOAT, ImageUsage.Sampled | ImageUsage.DepthStencilAttachment);
        _motionVectors      = CreateImage(allocator, VkFormat.VK_FORMAT_R16G16_SFLOAT, ImageUsage.Sampled | ImageUsage.ColorAttachment);
        _hudless            = CreateImage(allocator, ColorFormat, ImageUsage.Sampled | ImageUsage.ColorAttachment);
        _ui                 = CreateImage(allocator, VkFormat.VK_FORMAT_R16G16B16A16_SFLOAT, ImageUsage.Sampled | ImageUsage.ColorAttachment);
        _outputInterpolated = CreateImage(allocator, ColorFormat, ImageUsage.Storage | ImageUsage.TransferSrc | ImageUsage.TransferDst);
        _outputReal         = CreateImage(allocator, ColorFormat, ImageUsage.Storage | ImageUsage.TransferSrc | ImageUsage.TransferDst);

        _backbufferView         = NgxImage.CreateView(_device, in _backbuffer, ColorView);
        _depthView              = NgxImage.CreateView(_device, in _depth, DepthView);
        _motionVectorsView      = NgxImage.CreateView(_device, in _motionVectors, ColorView);
        _hudlessView            = NgxImage.CreateView(_device, in _hudless, ColorView);
        _uiView                 = NgxImage.CreateView(_device, in _ui, ColorView);
        _outputInterpolatedView = NgxImage.CreateView(_device, in _outputInterpolated, ColorView);
        _outputRealView         = NgxImage.CreateView(_device, in _outputReal, ColorView);

        // CreateFeature1 records real initialization work, so the recorder that
        // carries it must be submitted and completed before the first Evaluate.
        Queue queue = _device.GetQueue(family, queueIndex: 0);
        DlssFrameGenerationFeature? created = null;
        queue.ImmediateSubmit(_cmdPool, (ref CommandRecorder recorder) =>
        {
            created = _ngx.CreateFrameGeneration(ref recorder, new DlssFrameGenerationDescription
            {
                Width            = Width,
                Height           = Height,
                BackbufferFormat = ColorFormat,
                RenderWidth      = Width,
                RenderHeight     = Height,
            });
        });
        _feature = created!;

        _inputs = new DlssFrameGenerationInputs
        {
            Backbuffer         = _backbufferView,
            Depth              = _depthView,
            MotionVectors      = _motionVectorsView,
            Hudless            = _hudlessView,
            UiColorAndAlpha    = _uiView,
            OutputInterpolated = _outputInterpolatedView,
            OutputReal         = _outputRealView,
            Camera             = new DlssFrameGenerationCamera
            {
                ViewToClip     = Matrix4x4.Identity,
                ClipToView     = Matrix4x4.Identity,
                ClipToPrevClip = Matrix4x4.Identity,
                PrevClipToClip = Matrix4x4.Identity,
                Up             = Vector3.UnitY,
                Right          = Vector3.UnitX,
                Forward        = -Vector3.UnitZ,
                Near           = 0.1f,
                Far            = 100f,
                FieldOfView    = MathF.PI / 3f,
                AspectRatio    = (float)Width / Height,
            },
            BackbufferFrameId = 1,
        };

        _inputsRequiredOnly = new DlssFrameGenerationInputs
        {
            Backbuffer         = _backbufferView,
            Depth              = _depthView,
            MotionVectors      = _motionVectorsView,
            OutputInterpolated = _outputInterpolatedView,
            Camera             = _inputs.Camera,
            BackbufferFrameId  = 1,
        };
    }

    /// <summary>
    /// Sixteen full <see cref="DlssFrameGenerationFeature.Evaluate"/> calls
    /// recorded into one command buffer per invoke, validation off. Never
    /// submitted: this measures the managed recording cost, not GPU time.
    /// </summary>
    [Benchmark(OperationsPerInvoke = EvaluatesPerInvoke)]
    public void Evaluate_16()
    {
        // Dispose the recorder BEFORE ResetForFrame — see
        // DlssEvaluateBenchmarks.Evaluate_16 (#188/#199).
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
    /// <c>EvaluateFeature_C</c> skipped: seven resource structs, five matrix
    /// copies and 31 scalar writes — the managed half alone.
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
    /// <see cref="PackParameters_16"/> with <b>only the required slots</b>
    /// bound — the other leg of every optional-slot branch. The row above binds
    /// all seven, so without this one the null writes for Hudless,
    /// UiColorAndAlpha and OutputReal would never be measured, and that is the
    /// shape a future allocation on the optional path would hide in.
    /// </summary>
    [Benchmark(OperationsPerInvoke = EvaluatesPerInvoke)]
    public void PackParameters_16_RequiredOnly()
    {
        CommandRecorder recorder = _cmdPool.Begin();
        try
        {
            var commandBuffer = (VkCommandBuffer_T*)recorder.RawHandle;
            for (int i = 0; i < EvaluatesPerInvoke; i++)
                _feature.EvaluateCore(commandBuffer, in _inputsRequiredOnly, invokeNgx: false);
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
        // Nothing recorded above is ever submitted, so nothing is in flight.
        _feature?.Dispose();

        _outputRealView.Dispose();
        _outputInterpolatedView.Dispose();
        _uiView.Dispose();
        _hudlessView.Dispose();
        _motionVectorsView.Dispose();
        _depthView.Dispose();
        _backbufferView.Dispose();

        _outputReal.Dispose();
        _outputInterpolated.Dispose();
        _ui.Dispose();
        _hudless.Dispose();
        _motionVectors.Dispose();
        _depth.Dispose();
        _backbuffer.Dispose();

        _ngx?.Dispose();
        _cmdPool?.Dispose();
        _device?.Dispose();
        _instance?.Dispose();
    }

    /// <summary>
    /// Walks up from the benchmark binary to the repository root and points at
    /// the staged <c>rel/</c> feature DLLs, if they are there.
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

    private static Image CreateImage(Allocator allocator, VkFormat format, ImageUsage usage)
        => allocator.CreateImage(
            new ImageDescription { Format = format, Width = Width, Height = Height, Usage = usage },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });
}
