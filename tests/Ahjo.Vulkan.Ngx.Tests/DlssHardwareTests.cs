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

    // ---- fixtures -----------------------------------------------------------

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
