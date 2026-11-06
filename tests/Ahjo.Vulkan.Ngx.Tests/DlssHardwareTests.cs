using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;
using Xunit;

namespace Ahjo.Vulkan.Ngx.Tests;

/// <summary>
/// The only tests in this repository that execute real DLSS.
/// </summary>
/// <remarks>
/// <para>They need an NVIDIA GPU with a DLSS-capable driver <b>and</b> a
/// consumer-supplied <c>nvngx_dlss.dll</c>. No hosted CI runner has the first
/// (#32) and nothing in this repo ships the second (#214), so these carry
/// <c>[gate:feature]</c> — permanent-and-correct, never a coverage gap — and
/// their evidence comes from a developer machine, quoted in the PR the way
/// #217 quoted its <c>nm -D</c> and <c>dumpbin</c> output.</para>
/// <para>The end-to-end case is the point of the whole phase: it is where the
/// layout contract of <see cref="DlssEvaluateInputs"/> — the invariant the
/// wrapper documents but cannot enforce — is actually exercised. Run this suite
/// with <c>VK_LAYER_KHRONOS_validation</c> enabled
/// (<c>AHJO_VULKAN_TIER=validation</c>): the layer is its only oracle.</para>
/// </remarks>
public sealed unsafe class DlssHardwareTests
{
    private static void RequireDlss()
        => TestGate.RequireDeviceFeature(
            NgxTestEnvironment.IsDlssAvailable,
            "No NVIDIA GPU with a DLSS-capable driver and nvngx_dlss.dll on this host.");

    private const uint OutputWidth  = 3840;
    private const uint OutputHeight = 2160;

    [Fact]
    public void Create_Succeeds_AndReportsSuperSamplingAvailable()
    {
        RequireDlss();

        using var host = DlssHost.Create();
        Assert.True(host.Ngx.IsSuperSamplingAvailable);
    }

    [Fact]
    public void GetOptimalSettings_AnswersForEveryQualityMode()
    {
        RequireDlss();

        using var host = DlssHost.Create();

        foreach (DlssQualityMode mode in Enum.GetValues<DlssQualityMode>())
        {
            DlssOptimalSettings settings = host.Ngx.GetOptimalSettings(OutputWidth, OutputHeight, mode);

            if (!settings.IsAvailable)
            {
                // An unavailable mode reports zeros, not a 0x0 render target.
                Assert.Equal(0u, settings.RenderWidth);
                Assert.Equal(0u, settings.RenderHeight);
                Assert.Equal(0u, settings.MinRenderWidth);
                Assert.Equal(0u, settings.MaxRenderWidth);
                continue;
            }

            Assert.InRange(settings.RenderWidth, settings.MinRenderWidth, settings.MaxRenderWidth);
            Assert.InRange(settings.RenderHeight, settings.MinRenderHeight, settings.MaxRenderHeight);

            if (mode == DlssQualityMode.Dlaa)
            {
                // A property of NGX's answer, not something the wrapper
                // synthesizes: DLAA anti-aliases at native resolution.
                Assert.Equal(OutputWidth, settings.RenderWidth);
                Assert.Equal(OutputHeight, settings.RenderHeight);
            }
            else
            {
                Assert.True(settings.RenderWidth <= OutputWidth);
                Assert.True(settings.RenderHeight <= OutputHeight);
            }
        }
    }

    [Fact]
    public void EndToEnd_CreateEvaluateRelease()
    {
        RequireDlss();

        using var host = DlssHost.Create();

        DlssOptimalSettings settings = host.Ngx.GetOptimalSettings(OutputWidth, OutputHeight, DlssQualityMode.MaxQuality);
        TestGate.RequireDeviceFeature(settings.IsAvailable, "DLSS MaxQuality is not offered at 3840x2160 on this GPU.");

        using var targets = DlssTargets.Create(host.Device, settings.RenderWidth, settings.RenderHeight, OutputWidth, OutputHeight);

        DlssFeature? feature = null;
        try
        {
            // CreateFeature1 records real initialization work, so this recorder
            // must be submitted AND completed before the first Evaluate.
            host.Queue.ImmediateSubmit(host.Pool, (ref CommandRecorder recorder) =>
            {
                feature = host.Ngx.CreateDlss(ref recorder, new DlssFeatureDescription
                {
                    RenderWidth  = settings.RenderWidth,
                    RenderHeight = settings.RenderHeight,
                    OutputWidth  = OutputWidth,
                    OutputHeight = OutputHeight,
                    Mode         = DlssQualityMode.MaxQuality,
                    Flags        = DlssFeatureFlags.MotionVectorsLowRes | DlssFeatureFlags.AutoExposure,
                });
            });

            Assert.NotNull(feature);
            Assert.Equal(settings.RenderWidth, feature.RenderWidth);
            Assert.Equal(OutputWidth, feature.OutputWidth);

            // Spec D4: the wrapper CANNOT check the image-layout contract, and
            // the validation layer is its only oracle. Capture what the layer
            // says across the evaluate and fail on any error, so a run with the
            // layer installed proves the contract rather than merely not
            // throwing. Without the layer this collects nothing and the
            // assertion below is vacuous — which is why the instance turns the
            // layer on whenever the host has it.
            var layerErrors = new List<string>();
            DiagnosticSink previousSink = AhjoDiagnostics.Sink;
            AhjoDiagnostics.Sink = (severity, source, message) =>
            {
                if (severity == DiagnosticSeverity.Error)
                {
                    lock (layerErrors) layerErrors.Add($"[{source}] {message}");
                }
                previousSink(severity, source, message);
            };

            DlssFeature created = feature;
            try
            {
                host.Queue.ImmediateSubmit(host.Pool, (ref CommandRecorder recorder) =>
                {
                    targets.RecordLayoutTransitions(ref recorder);

                    created.Evaluate(ref recorder, new DlssEvaluateInputs
                    {
                        Color         = targets.Color,
                        Depth         = targets.Depth,
                        MotionVectors = targets.MotionVectors,
                        Output        = targets.Output,
                        RenderWidth   = settings.RenderWidth,
                        RenderHeight  = settings.RenderHeight,
                        JitterOffsetX = 0.25f,
                        JitterOffsetY = -0.25f,
                        Reset         = true,
                    });
                });
            }
            finally
            {
                AhjoDiagnostics.Sink = previousSink;
            }

            Assert.True(layerErrors.Count == 0,
                "The Vulkan validation layer reported errors during the DLSS create/evaluate:"
                + Environment.NewLine + "  "
                + string.Join(Environment.NewLine + "  ", layerErrors));

            // Stats only mean anything once a feature exists.
            Assert.True(host.Ngx.TryGetStats(out DlssStats stats));
            Assert.True(stats.VramAllocatedBytes > 0,
                $"DLSS reported {stats.VramAllocatedBytes} bytes of VRAM after a feature was created.");

            // The OPEN-3 fields, and the reason they are on DlssStats at all:
            // the string-form keys were measured to work, so a rel/ feature DLL
            // must report NVSDK_NGX_OPT_LEVEL_RELEASE and no dev branch. This
            // is also the guard against a dev/ DLL being deployed by mistake —
            // that build carries an on-screen watermark.
            Assert.Equal(40u, stats.OptLevel);
            Assert.False(stats.IsDevSnippetBranch);
        }
        finally
        {
            feature?.Dispose();
        }
    }

    [Fact]
    public void MissingFeatureLibrary_ThrowsNamingTheFileAndEveryDirectory()
    {
        RequireDlss();

        // An empty directory, and no feature DLL beside the test binary: this
        // is only meaningful when the staged DLL is reached through
        // DlssSearchPaths rather than from the output folder.
        string empty = Path.Combine(Path.GetTempPath(), "ahjo-ngx-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);

        try
        {
            if (File.Exists(Path.Combine(AppContext.BaseDirectory, "nvngx_dlss.dll")))
            {
                TestGate.Unsupported(
                    "nvngx_dlss.dll sits beside the test binary, so NGX finds it regardless of DlssSearchPaths; " +
                    "moving a file the developer installed is not this suite's business.");
            }

            NgxDescription description = NgxTestEnvironment.Description with { DlssSearchPaths = [empty] };

            using Instance instance = NgxTestEnvironment.CreateInstance(out NgxExtensionSet? required);
            required?.Dispose();

            // Same device factory as everything else: this test is about the
            // feature library being absent, not about taking a shortcut through
            // the extension contract.
            using Device device = NgxTestEnvironment.CreateDevice(instance, out _);

            var ex = Assert.Throws<NgxFeatureLibraryNotFoundException>(
                () => NgxContext.Create(device, in description));

            Assert.Contains("nvngx_dlss.dll", ex.Message, StringComparison.Ordinal);
            Assert.Contains(empty, ex.Message, StringComparison.Ordinal);
            Assert.Contains(AppContext.BaseDirectory, ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
        }
    }

    // ---- Frame Generation availability (#253 PR A) ---------------------------
    //
    // In this class rather than their own, deliberately: NGX's init and
    // shutdown are process-global, and xUnit runs test CLASSES in parallel. A
    // second class creating NgxContexts races this one's Shutdown1 into
    // FAIL_NotInitialized from GetCapabilityParameters — measured on the
    // first attempt. One class is one serial stream of NGX calls.

    private const NgxFeatures BothFeatures = NgxFeatures.SuperResolution | NgxFeatures.FrameGeneration;

    private static void RequireFrameGeneration()
        => TestGate.RequireDeviceFeature(
            NgxTestEnvironment.IsFrameGenerationAvailable,
            "No RTX 40-series or newer GPU with a DLSS-capable driver, nvngx_dlss.dll and nvngx_dlssg.dll on this host.");

    [Fact]
    public void TryGetDeviceExtensions_FrameGeneration_ReturnsASet()
    {
        RequireFrameGeneration();

        using Instance instance = NgxTestEnvironment.CreateInstance(BothFeatures);
        PhysicalDevice gpu = instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => true);
        NgxDescription description = NgxTestEnvironment.Description;

        Assert.True(NgxSupport.TryGetDeviceExtensions(NgxFeature.FrameGeneration, gpu, in description, out NgxExtensionSet? extensions));
        using (extensions)
        {
            Assert.NotNull(extensions);
            Assert.Equal(extensions.Count, extensions.Names.Length);
        }
    }

    [Fact]
    public void Create_RequiringSuperResolution_ReportsFrameGenerationAvailable()
    {
        RequireFrameGeneration();

        using Instance instance = NgxTestEnvironment.CreateInstance(BothFeatures);
        using Device device = NgxTestEnvironment.CreateDevice(instance, BothFeatures, out _);

        NgxDescription description = NgxTestEnvironment.Description;
        Assert.Equal(NgxFeatures.SuperResolution, description.RequiredFeatures);

        using NgxContext ngx = NgxContext.Create(device, in description);
        Assert.True(ngx.IsSuperSamplingAvailable);
        Assert.True(ngx.IsFrameGenerationAvailable);
        Assert.True(ngx.MaxGeneratedFrames >= 1);
    }

    [Fact]
    public void FrameGenerationOnlyContext_WithoutSuperResolution_DiagnosesEverySuperResolutionEntryPoint()
    {
        RequireFrameGeneration();

        if (File.Exists(Path.Combine(AppContext.BaseDirectory, "nvngx_dlss.dll")))
        {
            TestGate.Unsupported(
                "nvngx_dlss.dll sits beside the test binary, so NGX finds it regardless of DlssSearchPaths; " +
                "moving a file the developer installed is not this suite's business.");
        }

        // A search directory holding ONLY the Frame Generation library, so
        // Super Resolution is unavailable and Frame Generation is not.
        string staged = NgxTestEnvironment.FeatureDllSearchPaths[0];
        string fgOnly = Path.Combine(Path.GetTempPath(), "ahjo-ngx-fg-only-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fgOnly);
        File.Copy(Path.Combine(staged, "nvngx_dlssg.dll"), Path.Combine(fgOnly, "nvngx_dlssg.dll"));

        try
        {
            NgxDescription description = NgxTestEnvironment.Description with
            {
                DlssSearchPaths  = [fgOnly],
                RequiredFeatures = NgxFeatures.FrameGeneration,
            };

            using Instance instance = NgxTestEnvironment.CreateInstance(BothFeatures);
            using Device device = NgxTestEnvironment.CreateDevice(instance, BothFeatures, out uint family);
            using var pool = new CommandBufferPool(device, family);

            using (NgxContext ngx = NgxContext.Create(device, in description))
            {
                Assert.False(ngx.IsSuperSamplingAvailable);
                Assert.True(ngx.IsFrameGenerationAvailable);

                var settings = Assert.Throws<NgxFeatureLibraryNotFoundException>(
                    () => ngx.GetOptimalSettings(OutputWidth, OutputHeight, DlssQualityMode.MaxQuality));
                Assert.Equal(NgxFeature.SuperResolution, settings.Feature);
                Assert.Contains(fgOnly, settings.SearchedDirectories);

                Assert.False(ngx.TryGetStats(out DlssStats stats));
                Assert.Equal(default, stats);

                CommandRecorder recorder = pool.Begin();
                try
                {
                    // CommandRecorder is a ref struct, so no Assert.Throws lambda.
                    NgxFeatureLibraryNotFoundException? create = null;
                    try
                    {
                        using DlssFeature unexpected = ngx.CreateDlss(ref recorder, new DlssFeatureDescription
                        {
                            RenderWidth = OutputWidth, RenderHeight = OutputHeight,
                            OutputWidth = OutputWidth, OutputHeight = OutputHeight,
                            Mode        = DlssQualityMode.Dlaa,
                        });
                    }
                    catch (NgxFeatureLibraryNotFoundException ex)
                    {
                        create = ex;
                    }

                    Assert.NotNull(create);
                    Assert.Equal(NgxFeature.SuperResolution, create.Feature);
                }
                finally
                {
                    recorder.Dispose();
                }
            }
        }
        finally
        {
            // Best effort: NGX keeps nvngx_dlssg.dll mapped after the context
            // is disposed (measured — Delete fails with access denied), so the
            // copy can only go once the process exits. It is in %TEMP%.
            try
            {
                Directory.Delete(fgOnly, recursive: true);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
            }
        }
    }

    // ---- Frame Generation feature (#253 PR B) --------------------------------
    //
    // Same class as everything above, for the process-global NGX reason given
    // at the start of the PR A section.

    private const uint FgWidth  = 1920;
    private const uint FgHeight = 1080;

    private static DlssFrameGenerationDescription FgDescription => new()
    {
        Width            = FgWidth,
        Height           = FgHeight,
        BackbufferFormat = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
        RenderWidth      = FgWidth,
        RenderHeight     = FgHeight,
    };

    [Fact]
    public void CreateFrameGeneration_RequiringFrameGeneration_CreatesAndDisposes()
    {
        RequireFrameGeneration();

        using var host = FrameGenerationHost.Create(BothFeatures);

        DlssFrameGenerationFeature? feature = null;
        try
        {
            // CreateFeature1 records real initialization work, so this recorder
            // must be submitted AND completed before any Evaluate.
            host.Queue.ImmediateSubmit(host.Pool, (ref CommandRecorder recorder) =>
            {
                feature = host.Ngx.CreateFrameGeneration(ref recorder, FgDescription);
            });

            Assert.NotNull(feature);
            Assert.Equal(FgWidth, feature.Width);
            Assert.Equal(FgHeight, feature.Height);
            Assert.Equal(VkFormat.VK_FORMAT_R8G8B8A8_UNORM, feature.BackbufferFormat);
            Assert.Equal(FgWidth, feature.RenderWidth);
            Assert.Equal(FgHeight, feature.RenderHeight);
            Assert.Equal(host.Ngx.MaxGeneratedFrames, feature.MaxGeneratedFrames);
            Assert.True(feature.MaxGeneratedFrames >= 1);
        }
        finally
        {
            feature?.Dispose();
        }

        // Idempotent.
        feature.Dispose();
    }

    [Fact]
    public void CreateFrameGeneration_OnSuperResolutionContext_WhereFrameGenerationIsAvailable_Succeeds()
    {
        RequireFrameGeneration();

        // The engine's shape (pekkah/ahjo#2783): require only Super Resolution,
        // read IsFrameGenerationAvailable, and create FG when it is true.
        using var host = FrameGenerationHost.Create(NgxFeatures.SuperResolution);
        Assert.True(host.Ngx.IsFrameGenerationAvailable);

        DlssFrameGenerationFeature? feature = null;
        try
        {
            host.Queue.ImmediateSubmit(host.Pool, (ref CommandRecorder recorder) =>
            {
                feature = host.Ngx.CreateFrameGeneration(ref recorder, FgDescription);
            });

            Assert.NotNull(feature);
        }
        finally
        {
            feature?.Dispose();
        }
    }

    [Fact]
    public void FrameGeneration_EndToEnd_CreateEvaluateRelease_LayerClean()
    {
        RequireFrameGeneration();

        using var host = FrameGenerationHost.Create(NgxFeatures.SuperResolution);
        using var targets = FrameGenerationTargets.Create(host.Device, FgWidth, FgHeight);

        DlssFrameGenerationFeature? feature = null;
        try
        {
            host.Queue.ImmediateSubmit(host.Pool, (ref CommandRecorder recorder) =>
            {
                feature = host.Ngx.CreateFrameGeneration(ref recorder, FgDescription);
            });
            Assert.NotNull(feature);

            // The validation layer is the only oracle for the layout contract
            // (see EndToEnd_CreateEvaluateRelease). Capture its errors across
            // the evaluate and fail on any.
            var layerErrors = new List<string>();
            DiagnosticSink previousSink = AhjoDiagnostics.Sink;
            AhjoDiagnostics.Sink = (severity, source, message) =>
            {
                if (severity == DiagnosticSeverity.Error)
                {
                    lock (layerErrors) layerErrors.Add($"[{source}] {message}");
                }
                previousSink(severity, source, message);
            };

            // The wrapper's own checks too, against real VMA images — so a
            // Release run proves ValidateInputs accepts what the documented
            // contract builds, not only the FromRaw fixtures.
            bool previousValidation = AhjoValidation.Enabled;
            AhjoValidation.Enabled = true;

            DlssFrameGenerationFeature created = feature;
            try
            {
                host.Queue.ImmediateSubmit(host.Pool, (ref CommandRecorder recorder) =>
                {
                    targets.RecordLayoutTransitions(ref recorder);

                    created.Evaluate(ref recorder, new DlssFrameGenerationInputs
                    {
                        Backbuffer         = targets.Backbuffer,
                        Depth              = targets.Depth,
                        MotionVectors      = targets.MotionVectors,
                        Hudless            = targets.Hudless,
                        UiColorAndAlpha    = targets.Ui,
                        OutputInterpolated = targets.OutputInterpolated,
                        OutputReal         = targets.OutputReal,
                        Camera             = new DlssFrameGenerationCamera
                        {
                            ViewToClip     = System.Numerics.Matrix4x4.Identity,
                            ClipToView     = System.Numerics.Matrix4x4.Identity,
                            ClipToPrevClip = System.Numerics.Matrix4x4.Identity,
                            PrevClipToClip = System.Numerics.Matrix4x4.Identity,
                            Up             = System.Numerics.Vector3.UnitY,
                            Right          = System.Numerics.Vector3.UnitX,
                            Forward        = -System.Numerics.Vector3.UnitZ,
                            Near           = 0.1f,
                            Far            = 100f,
                            FieldOfView    = MathF.PI / 3f,
                            AspectRatio    = (float)FgWidth / FgHeight,
                        },
                        Reset             = true,
                        BackbufferFrameId = 1,
                    });

                    // The exit-layout contract: a barrier whose oldLayouts are
                    // the documented post-evaluate layouts, so the layer checks
                    // what NGX actually left each image in.
                    targets.RecordPostEvaluateBarrier(ref recorder);
                });
            }
            finally
            {
                AhjoValidation.Enabled = previousValidation;
                AhjoDiagnostics.Sink = previousSink;
            }

            Assert.True(layerErrors.Count == 0,
                "The Vulkan validation layer reported errors during the DLSS Frame Generation create/evaluate:"
                + Environment.NewLine + "  "
                + string.Join(Environment.NewLine + "  ", layerErrors));
        }
        finally
        {
            feature?.Dispose();
        }
    }

    // ---- fixtures -----------------------------------------------------------

    /// <summary>Instance and device with the SR + FG extension unions, queue,
    /// command pool and NGX context requiring <c>required</c>.</summary>
    private sealed class FrameGenerationHost : IDisposable
    {
        public required Instance          Instance { get; init; }
        public required Device            Device   { get; init; }
        public required Queue             Queue    { get; init; }
        public required CommandBufferPool Pool     { get; init; }
        public required NgxContext        Ngx      { get; init; }

        public static FrameGenerationHost Create(NgxFeatures required)
        {
            Instance instance = NgxTestEnvironment.CreateInstance(BothFeatures);

            Device?            device = null;
            CommandBufferPool? pool   = null;
            NgxContext?        ngx    = null;
            try
            {
                device = NgxTestEnvironment.CreateDevice(instance, BothFeatures, out uint family);
                NgxDescription description = NgxTestEnvironment.Description with { RequiredFeatures = required };

                pool = new CommandBufferPool(device, family);
                ngx  = NgxContext.Create(device, in description);

                return new FrameGenerationHost
                {
                    Instance = instance,
                    Device   = device,
                    Queue    = device.GetQueue(family, queueIndex: 0),
                    Pool     = pool,
                    Ngx      = ngx,
                };
            }
            catch
            {
                ngx?.Dispose();
                pool?.Dispose();
                device?.Dispose();
                instance.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            Ngx.Dispose();
            Pool.Dispose();
            Device.Dispose();
            Instance.Dispose();
        }
    }

    /// <summary>All seven Frame Generation images at one extent, their views,
    /// and the layout transitions the evaluate contract requires.</summary>
    private sealed class FrameGenerationTargets : IDisposable
    {
        private Image _backbuffer, _depth, _motionVectors, _hudless, _ui, _outputInterpolated, _outputReal;

        public NgxImage Backbuffer         { get; private init; }
        public NgxImage Depth              { get; private init; }
        public NgxImage MotionVectors      { get; private init; }
        public NgxImage Hudless            { get; private init; }
        public NgxImage Ui                 { get; private init; }
        public NgxImage OutputInterpolated { get; private init; }
        public NgxImage OutputReal         { get; private init; }

        public static FrameGenerationTargets Create(Device device, uint width, uint height)
        {
            Allocator allocator = device.Allocator;

            Image Make(VkFormat format, ImageUsage usage)
                => allocator.CreateImage(
                    new ImageDescription { Format = format, Width = width, Height = height, Usage = usage },
                    new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

            const VkFormat Color = VkFormat.VK_FORMAT_R8G8B8A8_UNORM;
            // TransferSrc is not in NVIDIA's headers or guide: with OutputReal
            // bound, Frame Generation vkCmdCopyImage's FROM the backbuffer
            // (into OutputReal). Without the bit the layer reports
            // VUID-vkCmdCopyImage-aspect-06662 on this image; with OutputReal
            // unbound and no TransferSrc the layer is clean. Observed on an
            // RTX 4070 Ti / driver 616.92 (#253 PR B), the FG counterpart of
            // SR's TransferDst-on-Output finding.
            Image backbuffer    = Make(Color, ImageUsage.Sampled | ImageUsage.ColorAttachment | ImageUsage.TransferSrc);
            Image depth         = Make(VkFormat.VK_FORMAT_D32_SFLOAT, ImageUsage.Sampled | ImageUsage.DepthStencilAttachment);
            Image motionVectors = Make(VkFormat.VK_FORMAT_R16G16_SFLOAT, ImageUsage.Sampled | ImageUsage.ColorAttachment);
            Image hudless       = Make(Color, ImageUsage.Sampled | ImageUsage.ColorAttachment);
            Image ui            = Make(VkFormat.VK_FORMAT_R16G16B16A16_SFLOAT, ImageUsage.Sampled | ImageUsage.ColorAttachment);
            // TransferDst, measured per output by dropping it from each in turn
            // (RTX 4070 Ti / driver 616.92, #253 PR B):
            //  - OutputReal NEEDS it: it is the dstImage of FG's
            //    Backbuffer -> OutputReal vkCmdCopyImage, and without the bit the
            //    layer reports VUID-vkCmdCopyImage-aspect-06663.
            //  - OutputInterpolated does NOT: the layer is clean without it. No
            //    SR-style vkCmdClearColorImage was observed on either output.
            // TransferSrc on both only because RecordPostEvaluateBarrier moves
            // them to TRANSFER_SRC_OPTIMAL for a blit, as a present path would.
            Image outputInterpolated = Make(Color, ImageUsage.Storage | ImageUsage.TransferSrc);
            Image outputReal         = Make(Color, ImageUsage.Storage | ImageUsage.TransferSrc | ImageUsage.TransferDst);

            var colorView = new ImageViewDescription { Aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT };
            var depthView = new ImageViewDescription { Aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_DEPTH_BIT };

            return new FrameGenerationTargets
            {
                _backbuffer         = backbuffer,
                _depth              = depth,
                _motionVectors      = motionVectors,
                _hudless            = hudless,
                _ui                 = ui,
                _outputInterpolated = outputInterpolated,
                _outputReal         = outputReal,
                Backbuffer         = NgxImage.CreateView(device, in backbuffer, colorView),
                Depth              = NgxImage.CreateView(device, in depth, depthView),
                MotionVectors      = NgxImage.CreateView(device, in motionVectors, colorView),
                Hudless            = NgxImage.CreateView(device, in hudless, colorView),
                Ui                 = NgxImage.CreateView(device, in ui, colorView),
                OutputInterpolated = NgxImage.CreateView(device, in outputInterpolated, colorView),
                OutputReal         = NgxImage.CreateView(device, in outputReal, colorView),
            };
        }

        /// <summary>
        /// The layout contract, recorded: the five inputs to a shader-read
        /// layout, both outputs to GENERAL — with the compute + transfer
        /// destination scope the SR test uses on its output.
        /// </summary>
        public void RecordLayoutTransitions(ref CommandRecorder recorder)
        {
            Span<ImageBarrier> barriers =
            [
                ToShaderRead(in _backbuffer),
                ImageBarrier.Transition(
                    in _depth, VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
                    Stage.AllCommands, Access.None, Stage.ComputeShader, Access.ShaderRead,
                    VkImageAspectFlagBits.VK_IMAGE_ASPECT_DEPTH_BIT),
                ToShaderRead(in _motionVectors),
                ToShaderRead(in _hudless),
                ToShaderRead(in _ui),
                ToGeneral(in _outputInterpolated),
                ToGeneral(in _outputReal),
            ];

            recorder.PipelineBarrier(barriers);

            static ImageBarrier ToShaderRead(in Image image)
                => ImageBarrier.Transition(
                    in image, VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
                    Stage.AllCommands, Access.None, Stage.ComputeShader, Access.ShaderRead);

            static ImageBarrier ToGeneral(in Image image)
                => ImageBarrier.Transition(
                    in image, VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_GENERAL,
                    Stage.AllCommands, Access.None,
                    Stage.ComputeShader | Stage.AllTransfer, Access.ShaderWrite | Access.TransferWrite);
        }

        /// <summary>
        /// A real transition out of every image — inputs to <c>GENERAL</c>
        /// (the Backbuffer back to <c>SHADER_READ_ONLY_OPTIMAL</c>), outputs to
        /// <c>TRANSFER_SRC_OPTIMAL</c> for a blit — whose
        /// <c>oldLayout</c> is the layout the evaluate leaves it in, so the
        /// layer checks the exit-layout contract. (A same-layout barrier is
        /// not a transition and the layer does not check it.)
        /// </summary>
        /// <remarks>
        /// Inputs exit in <c>SHADER_READ_ONLY_OPTIMAL</c> and outputs in
        /// <c>GENERAL</c>, as they entered — <b>except the Backbuffer, which
        /// exits in <c>GENERAL</c> when <c>OutputReal</c> is bound</b>. NGX
        /// transitions it for its Backbuffer → OutputReal copy and does not
        /// restore it: assuming <c>SHADER_READ_ONLY_OPTIMAL</c> here is
        /// <c>VUID-VkImageMemoryBarrier2-oldLayout-01197</c>. With
        /// <c>OutputReal</c> unbound the Backbuffer exits as it entered.
        /// Measured on an RTX 4070 Ti / driver 616.92 (#253 PR B).
        /// </remarks>
        public void RecordPostEvaluateBarrier(ref CommandRecorder recorder)
        {
            Span<ImageBarrier> barriers =
            [
                ImageBarrier.Transition(
                    in _backbuffer, VkImageLayout.VK_IMAGE_LAYOUT_GENERAL, VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
                    Stage.ComputeShader | Stage.AllTransfer, Access.ShaderRead | Access.TransferRead,
                    Stage.AllTransfer, Access.TransferRead),
                ImageBarrier.Transition(
                    in _depth, VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL, VkImageLayout.VK_IMAGE_LAYOUT_GENERAL,
                    Stage.ComputeShader | Stage.AllTransfer, Access.ShaderRead | Access.TransferRead,
                    Stage.AllTransfer, Access.TransferRead,
                    VkImageAspectFlagBits.VK_IMAGE_ASPECT_DEPTH_BIT),
                FromShaderRead(in _motionVectors),
                FromShaderRead(in _hudless),
                FromShaderRead(in _ui),
                FromGeneral(in _outputInterpolated),
                FromGeneral(in _outputReal),
            ];

            recorder.PipelineBarrier(barriers);

            static ImageBarrier FromShaderRead(in Image image)
                => ImageBarrier.Transition(
                    in image, VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL, VkImageLayout.VK_IMAGE_LAYOUT_GENERAL,
                    Stage.ComputeShader | Stage.AllTransfer, Access.ShaderRead | Access.TransferRead,
                    Stage.AllTransfer, Access.TransferRead);

            static ImageBarrier FromGeneral(in Image image)
                => ImageBarrier.Transition(
                    in image, VkImageLayout.VK_IMAGE_LAYOUT_GENERAL, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                    Stage.ComputeShader | Stage.AllTransfer, Access.ShaderWrite | Access.TransferWrite,
                    Stage.AllTransfer, Access.TransferRead);
        }

        public void Dispose()
        {
            Backbuffer.Dispose();
            Depth.Dispose();
            MotionVectors.Dispose();
            Hudless.Dispose();
            Ui.Dispose();
            OutputInterpolated.Dispose();
            OutputReal.Dispose();
            _backbuffer.Dispose();
            _depth.Dispose();
            _motionVectors.Dispose();
            _hudless.Dispose();
            _ui.Dispose();
            _outputInterpolated.Dispose();
            _outputReal.Dispose();
        }
    }

    /// <summary>Instance, NVIDIA device, queue, command pool and NGX context.</summary>
    private sealed class DlssHost : IDisposable
    {
        public required Instance          Instance { get; init; }
        public required Device            Device   { get; init; }
        public required Queue             Queue    { get; init; }
        public required CommandBufferPool Pool     { get; init; }
        public required NgxContext        Ngx      { get; init; }

        public static DlssHost Create()
        {
            // The instance MUST carry NGX's required extensions — see
            // NgxTestEnvironment.CreateInstance for what happens otherwise.
            Instance instance = NgxTestEnvironment.CreateInstance(out NgxExtensionSet? required);
            required?.Dispose();   // vkCreateInstance copied the names

            // Held in locals rather than built straight into the initializer so
            // the failure path can unwind them. Disposing only the instance
            // while a VkDevice it parents is still alive is
            // VUID-vkDestroyInstance-instance-00629, and the path that hits it
            // is not hypothetical: a host with the shim but no feature DLL
            // creates the device fine and then throws out of NgxContext.Create.
            Device?            device = null;
            CommandBufferPool? pool   = null;
            NgxContext?        ngx    = null;
            try
            {
                // The same factory the gate probes through — see
                // NgxTestEnvironment.CreateDevice for why that identity matters.
                // It carries the device extensions NGX names; without them Init
                // succeeds and the capability map then reports
                // SuperSampling.Available = 0 with FAIL_PlatformError.
                device = NgxTestEnvironment.CreateDevice(instance, out uint family);
                NgxDescription ngxDescription = NgxTestEnvironment.Description;

                pool = new CommandBufferPool(device, family);
                ngx  = NgxContext.Create(device, in ngxDescription);

                return new DlssHost
                {
                    Instance = instance,
                    Device   = device,
                    Queue    = device.GetQueue(family, queueIndex: 0),
                    Pool     = pool,
                    Ngx      = ngx,
                };
            }
            catch
            {
                // Reverse construction order, and the instance last — the same
                // order Dispose() uses on the success path.
                ngx?.Dispose();
                pool?.Dispose();
                device?.Dispose();
                instance.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            Ngx.Dispose();
            Pool.Dispose();
            Device.Dispose();
            Instance.Dispose();
        }
    }

    /// <summary>The four required DLSS images, their views, and the layout
    /// transitions the evaluate contract requires.</summary>
    private sealed class DlssTargets : IDisposable
    {
        private Image _color, _depth, _motionVectors, _output;

        public NgxImage Color         { get; private init; }
        public NgxImage Depth         { get; private init; }
        public NgxImage MotionVectors { get; private init; }
        public NgxImage Output        { get; private init; }

        public static DlssTargets Create(Device device, uint renderWidth, uint renderHeight, uint outputWidth, uint outputHeight)
        {
            Allocator allocator = device.Allocator;

            Image color = allocator.CreateImage(
                new ImageDescription
                {
                    Format = VkFormat.VK_FORMAT_R16G16B16A16_SFLOAT,
                    Width  = renderWidth,
                    Height = renderHeight,
                    Usage  = ImageUsage.Sampled | ImageUsage.ColorAttachment | ImageUsage.Storage,
                },
                new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

            Image depth = allocator.CreateImage(
                new ImageDescription
                {
                    Format = VkFormat.VK_FORMAT_D32_SFLOAT,
                    Width  = renderWidth,
                    Height = renderHeight,
                    Usage  = ImageUsage.Sampled | ImageUsage.DepthStencilAttachment,
                },
                new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

            Image motionVectors = allocator.CreateImage(
                new ImageDescription
                {
                    Format = VkFormat.VK_FORMAT_R16G16_SFLOAT,
                    Width  = renderWidth,
                    Height = renderHeight,
                    Usage  = ImageUsage.Sampled | ImageUsage.ColorAttachment | ImageUsage.Storage,
                },
                new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

            Image output = allocator.CreateImage(
                new ImageDescription
                {
                    Format = VkFormat.VK_FORMAT_R16G16B16A16_SFLOAT,
                    Width  = outputWidth,
                    Height = outputHeight,
                    // TransferDst is not optional in practice: DLSS clears this
                    // image itself with vkCmdClearColorImage. See
                    // DlssEvaluateInputs.Output.
                    Usage  = ImageUsage.Storage | ImageUsage.TransferSrc | ImageUsage.TransferDst | ImageUsage.Sampled,
                },
                new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

            return new DlssTargets
            {
                _color         = color,
                _depth         = depth,
                _motionVectors = motionVectors,
                _output        = output,
                Color          = NgxImage.CreateView(device, in color, new ImageViewDescription { Aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT }),
                Depth          = NgxImage.CreateView(device, in depth, new ImageViewDescription { Aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_DEPTH_BIT }),
                MotionVectors  = NgxImage.CreateView(device, in motionVectors, new ImageViewDescription { Aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT }),
                Output         = NgxImage.CreateView(device, in output, new ImageViewDescription { Aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT }),
            };
        }

        /// <summary>
        /// The layout contract, recorded: inputs to a shader-read layout,
        /// output to GENERAL. The wrapper cannot do this for you — it tracks no
        /// layout — which is exactly why the test does it explicitly.
        /// </summary>
        public void RecordLayoutTransitions(ref CommandRecorder recorder)
        {
            Span<ImageBarrier> barriers =
            [
                ImageBarrier.Transition(
                    in _color, VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
                    Stage.AllCommands, Access.None, Stage.ComputeShader, Access.ShaderRead),
                ImageBarrier.Transition(
                    in _depth, VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
                    Stage.AllCommands, Access.None, Stage.ComputeShader, Access.ShaderRead,
                    VkImageAspectFlagBits.VK_IMAGE_ASPECT_DEPTH_BIT),
                ImageBarrier.Transition(
                    in _motionVectors, VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
                    Stage.AllCommands, Access.None, Stage.ComputeShader, Access.ShaderRead),
                // The destination scope covers BOTH stages that touch this image
                // next: DLSS's compute passes AND its own vkCmdClearColorImage,
                // which is a transfer-stage TRANSFER_WRITE (see
                // DlssEvaluateInputs.Output). ComputeShader|ShaderWrite alone
                // would leave that clear outside the barrier's destination scope
                // — a write-after-write the layer may or may not report
                // depending on whether DLSS emits its own barrier first.
                ImageBarrier.Transition(
                    in _output, VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_GENERAL,
                    Stage.AllCommands, Access.None,
                    Stage.ComputeShader | Stage.AllTransfer, Access.ShaderWrite | Access.TransferWrite),
            ];

            recorder.PipelineBarrier(barriers);
        }

        public void Dispose()
        {
            Color.Dispose();
            Depth.Dispose();
            MotionVectors.Dispose();
            Output.Dispose();
            _color.Dispose();
            _depth.Dispose();
            _motionVectors.Dispose();
            _output.Dispose();
        }
    }
}
