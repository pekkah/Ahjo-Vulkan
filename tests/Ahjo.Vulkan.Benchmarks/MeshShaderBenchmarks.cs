using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;
using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Issue 201: all three <c>CommandRecorder.DrawMeshTasks*</c> forwards sit on
/// the per-frame recording path, so they carry the same <b>0 B per call</b>
/// obligation as the rest of <c>Recording/</c>. Each body is a pointer load,
/// an unconditional null test and the native call — strictly thinner than
/// <c>DrawIndirectCount</c> — and the benchmark exists to keep it that way:
/// the null test is <em>not</em> behind <c>AhjoValidation.IsEnabled</c>, so a
/// future refactor that routes it through a validation helper (or that
/// marshals anything) would show up here.
/// </summary>
/// <remarks>
/// Deliberately a separate class from <see cref="CommandRecorderBenchmarks"/>:
/// this <see cref="Setup"/> requires an optional device extension
/// (<c>VK_EXT_mesh_shader</c>) plus three features — <c>meshShader</c>,
/// <c>drawIndirectCount</c> for
/// <see cref="DrawMeshTasksIndirectCount_1024"/>, and <c>taskShader</c> for
/// <see cref="Build_MeshPipeline"/> /
/// <see cref="Build_MeshPipeline_WithSpecialization"/> — and a host without
/// them must not take
/// the issue-29 canary
/// (<c>CommandRecorder.RenderingPass100Cmds</c>) down with it. The setup builds
/// the full per-frame shape — device, mesh pipeline, color attachment, indirect
/// buffer pair — because a <c>vkCmdDrawMeshTasks*</c> with no bound mesh
/// pipeline is not a shape this repo records; it is a VU violation even when
/// the command buffer is never submitted.
/// </remarks>
[MemoryDiagnoser]
public unsafe class MeshShaderBenchmarks
{
    private const int  DrawsPerInvoke = 1024;
    private const uint Extent         = 64;

    /// <summary>
    /// Matches <c>mesh_tri.mesh</c>'s <c>layout(constant_id = 0) const float
    /// uScale</c>. The same struct is also bound to the task stage in
    /// <see cref="Build_MeshPipeline_WithSpecialization"/>: <c>mesh_tri.task</c>
    /// declares no spec constants, and the Vulkan spec makes a map entry whose
    /// <c>constantID</c> the shader does not use a no-op — which is what lets
    /// the benchmark exercise <b>both</b> extra <c>fixed</c> statements from
    /// one fixture.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MeshSpecConstants
    {
        public float Scale;
    }

    private Instance          _instance = null!;
    private Device            _device   = null!;
    private CommandBufferPool _cmdPool  = null!;
    private ShaderModule      _meshModule;
    private ShaderModule      _taskModule;
    private ShaderModule      _fragModule;
    private PipelineLayout    _layout;
    private GraphicsPipeline  _pipeline;
    private Image             _image;
    private ImageView         _view;
    private Buffer            _indirect;
    private Buffer            _indirectCount;

    // Cached so Build_MeshPipeline's measured body takes no span of its own
    // and allocates nothing (the GraphicsPipelineBuilderBenchmarks shape).
    private VkFormat[] _colorFormats = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Fail with an actionable message rather than a bare
        // FileNotFoundException from SpirvBlob.Load: CompileMeshShaders runs
        // with ContinueOnError="WarnAndContinue", so a host without glslc
        // builds green and produces no .spv at all.
        string shadersDir = Path.Combine(AppContext.BaseDirectory, "Shaders");
        foreach (string name in (string[])["mesh_tri.mesh.spv", "mesh_tri.task.spv", "triangle.frag.spv"])
        {
            if (!File.Exists(Path.Combine(shadersDir, name)))
                throw new FileNotFoundException(
                    $"MeshShaderBenchmarks needs compiled shaders at {shadersDir} (missing {name}). " +
                    "Build the benchmark project once with the Vulkan SDK on PATH (or VULKAN_SDK env var set) so glslc compiles them.");
        }

        _instance = Instance.Create(default);

        uint family = uint.MaxValue;
        var gpu = _instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            if (!info.SupportsExtension(DeviceExtensionNames.MeshShader)) return false;
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

        Utf8Name[] extensions = [VulkanExtensions.ExtMeshShader];
        _device = gpu.CreateDevice(new DeviceDescription
        {
            Queues            = [new QueueRequest(family, count: 1, priority: 1.0f)],
            Extensions        = extensions,
            ConfigureFeatures = static (
                ref ChainBuilder<VkDeviceCreateInfo> chain,
                ref VkPhysicalDeviceFeatures2 _,
                ref VkPhysicalDeviceVulkan12Features f12,
                ref VkPhysicalDeviceVulkan13Features _,
                ref VkPhysicalDeviceVulkan14Features _) =>
            {
                // vkCmdDrawMeshTasksIndirectCountEXT requires drawIndirectCount
                // (VUID-vkCmdDrawMeshTasksIndirectCountEXT-None-04445); the
                // wrapper does not enable it by default.
                f12.drawIndirectCount = 1;
                ref var mesh = ref chain.Push<VkPhysicalDeviceMeshShaderFeaturesEXT>();
                mesh.meshShader = 1;
                // taskShader is for Build_MeshPipeline, which builds the
                // widest mesh shape (task + mesh + fragment) so the builder's
                // VK_SHADER_STAGE_TASK_BIT_EXT emission is measured too. It is
                // advertised independently of meshShader, so this narrows the
                // class's host requirement — deliberately, and loudly:
                // [GlobalSetup] throws rather than silently skipping.
                mesh.taskShader = 1;
            },
        });

        _cmdPool = new CommandBufferPool(_device, family);

        using (var meshBlob = SpirvBlob.Load(ShaderPath("mesh_tri.mesh.spv")))
            _meshModule = _device.CreateShaderModule(meshBlob.Words);
        using (var taskBlob = SpirvBlob.Load(ShaderPath("mesh_tri.task.spv")))
            _taskModule = _device.CreateShaderModule(taskBlob.Words);
        using (var fragBlob = SpirvBlob.Load(ShaderPath("triangle.frag.spv")))
            _fragModule = _device.CreateShaderModule(fragBlob.Words);

        _layout = _device.CreatePipelineLayout(default);

        _colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];
        _pipeline = _device.BuildGraphicsPipeline()
            .WithMeshStages(in _meshModule, in _fragModule)
            .WithDynamicRendering(_colorFormats)
            .WithLayout(in _layout)
            .Build();

        _image = _device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType     = VkImageType.VK_IMAGE_TYPE_2D,
                Format        = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width         = Extent, Height = Extent, Depth = 1,
                MipLevels     = 1, ArrayLayers = 1,
                Samples       = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling        = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage         = ImageUsage.ColorAttachment,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        _view = _image.CreateView(_device, new ImageViewDescription
        {
            ViewType   = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D,
            Aspect     = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
            LevelCount = 1, LayerCount = 1,
        });

        // Device-local indirect + count pair. Only ever RECORDED against —
        // never mapped, never submitted — so the contents are irrelevant and
        // AutoPreferDevice needs no host-access flag.
        _indirect = _device.Allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = (ulong)sizeof(VkDrawMeshTasksIndirectCommandEXT),
                Usage = BufferUsage.IndirectBuffer,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });
        _indirectCount = _device.Allocator.CreateBuffer(
            new BufferDescription { Size = sizeof(uint), Usage = BufferUsage.IndirectBuffer },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        // Warm: fault in the pool's first command buffer and JIT every
        // recording path so the measured runs hit steady-state reuse.
        DrawMeshTasks_1024();
        DrawMeshTasksIndirect_1024();
        DrawMeshTasksIndirectCount_1024();
        Build_MeshPipeline();
        // Also warms SpecializationLayout<MeshSpecConstants>.Entries, the
        // one-per-T reflection pass, so the measured body never pays it.
        Build_MeshPipeline_WithSpecialization();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _indirectCount.Dispose();
        _indirect.Dispose();
        _view.Dispose();
        _image.Dispose();
        _pipeline.Dispose();
        _layout.Dispose();
        _fragModule.Dispose();
        _taskModule.Dispose();
        _meshModule.Dispose();
        _cmdPool?.Dispose();
        _device?.Dispose();
        _instance?.Dispose();
    }

    [Benchmark(OperationsPerInvoke = DrawsPerInvoke)]
    public void DrawMeshTasks_1024()
    {
        Span<ColorAttachment> color = stackalloc ColorAttachment[1];
        color[0] = ColorAttachmentSlot();

        var info = new RenderingInfo
        {
            RenderArea       = new VkRect2D { extent = new VkExtent2D { width = Extent, height = Extent } },
            LayerCount       = 1,
            ColorAttachments = color,
        };

        // Dispose the recorder (inner-block scope) BEFORE ResetForFrame: Retire
        // fires on Dispose, not End, so the buffer must be retired to _spent
        // before the reset drains _spent → _idle, or it never recycles. This
        // also keeps the benchmark valid under AHJO_VULKAN_TIER=validation,
        // where ResetForFrame asserts on an outstanding recorder.
        using (var rec = _cmdPool.Begin())
        {
            rec.BeginRendering(in info);
            rec.BindPipeline(in _pipeline);
            // Outside the measured loop, and required: the pipeline takes the
            // builder's default dynamic state (VIEWPORT + SCISSOR), and
            // CoreChecks validates dynamic state at RECORD time — an unset
            // viewport/scissor is VUID-vkCmdDrawMeshTasksEXT-None-07831/-07832
            // whether or not the buffer is ever submitted.
            rec.SetViewport(new VkViewport
            {
                x = 0, y = 0, width = Extent, height = Extent, minDepth = 0, maxDepth = 1,
            });
            rec.SetScissor(new VkRect2D { extent = new VkExtent2D { width = Extent, height = Extent } });
            for (int i = 0; i < DrawsPerInvoke; i++)
                rec.DrawMeshTasks(1, 1, 1);
            rec.EndRendering();
            rec.End();
        }

        _cmdPool.ResetForFrame();
    }

    /// <summary>
    /// The immediate-count indirect form. <c>drawCount: 1</c> on purpose:
    /// anything above 1 needs the <c>multiDrawIndirect</c> feature
    /// (VUID-vkCmdDrawMeshTasksIndirectEXT-drawCount-02718), which would
    /// narrow the class's host requirement for no measurement gain — the
    /// wrapper side is the same pointer load + null test either way.
    /// </summary>
    [Benchmark(OperationsPerInvoke = DrawsPerInvoke)]
    public void DrawMeshTasksIndirect_1024()
    {
        Span<ColorAttachment> color = stackalloc ColorAttachment[1];
        color[0] = ColorAttachmentSlot();

        var info = new RenderingInfo
        {
            RenderArea       = new VkRect2D { extent = new VkExtent2D { width = Extent, height = Extent } },
            LayerCount       = 1,
            ColorAttachments = color,
        };

        using (var rec = _cmdPool.Begin())
        {
            rec.BeginRendering(in info);
            rec.BindPipeline(in _pipeline);
            rec.SetViewport(new VkViewport
            {
                x = 0, y = 0, width = Extent, height = Extent, minDepth = 0, maxDepth = 1,
            });
            rec.SetScissor(new VkRect2D { extent = new VkExtent2D { width = Extent, height = Extent } });
            for (int i = 0; i < DrawsPerInvoke; i++)
            {
                rec.DrawMeshTasksIndirect(
                    in _indirect, offset: 0,
                    drawCount: 1,
                    stride: (uint)sizeof(VkDrawMeshTasksIndirectCommandEXT));
            }
            rec.EndRendering();
            rec.End();
        }

        _cmdPool.ResetForFrame();
    }

    [Benchmark(OperationsPerInvoke = DrawsPerInvoke)]
    public void DrawMeshTasksIndirectCount_1024()
    {
        Span<ColorAttachment> color = stackalloc ColorAttachment[1];
        color[0] = ColorAttachmentSlot();

        var info = new RenderingInfo
        {
            RenderArea       = new VkRect2D { extent = new VkExtent2D { width = Extent, height = Extent } },
            LayerCount       = 1,
            ColorAttachments = color,
        };

        using (var rec = _cmdPool.Begin())
        {
            rec.BeginRendering(in info);
            rec.BindPipeline(in _pipeline);
            rec.SetViewport(new VkViewport
            {
                x = 0, y = 0, width = Extent, height = Extent, minDepth = 0, maxDepth = 1,
            });
            rec.SetScissor(new VkRect2D { extent = new VkExtent2D { width = Extent, height = Extent } });
            for (int i = 0; i < DrawsPerInvoke; i++)
            {
                rec.DrawMeshTasksIndirectCount(
                    in _indirect, offset: 0,
                    in _indirectCount, countBufferOffset: 0,
                    maxDrawCount: 1,
                    stride: (uint)sizeof(VkDrawMeshTasksIndirectCommandEXT));
            }
            rec.EndRendering();
            rec.End();
        }

        _cmdPool.ResetForFrame();
    }

    /// <summary>
    /// Setup-time cost, not a per-frame one — but the builder's mesh path is
    /// new surface with its own <c>fixed</c> chain and stage-emission branch,
    /// and <c>Build()</c> must stay allocation-free like the classic path.
    /// Builds the <b>widest</b> mesh shape (task + mesh + fragment) so the
    /// <c>VK_SHADER_STAGE_TASK_BIT_EXT</c> branch is measured too.
    /// </summary>
    /// <remarks>
    /// Deliberately here and not on
    /// <see cref="GraphicsPipelineBuilderBenchmarks"/>: that class is the #44
    /// canary and must keep running on any host with an ICD, mesh-capable or
    /// not.
    /// </remarks>
    [Benchmark]
    public void Build_MeshPipeline()
    {
        using var pipeline = _device.BuildGraphicsPipeline()
            .WithMeshStages(in _meshModule, in _fragModule)
            .WithTaskStage(in _taskModule)
            .WithDynamicRendering(_colorFormats)
            .WithLayout(in _layout)
            .Build();
    }

    /// <summary>
    /// <see cref="Build_MeshPipeline"/> plus mesh <b>and</b> task
    /// specialization. Without this row two of the mesh path's four extra
    /// <c>fixed</c> statements (<c>_meshSpecEntries</c>, <c>_taskSpecEntries</c>)
    /// are only ever measured in their degenerate null-array form, so a
    /// regression that started allocating in the non-empty branch would not
    /// show up anywhere.
    /// </summary>
    /// <remarks>
    /// The <see cref="SpecializationInfo{T}"/> values are stack locals, not
    /// fields: <c>SpecializationInfo.For</c> captures a raw pointer to the
    /// caller's storage, and a field on this (heap-allocated, movable)
    /// benchmark instance is not pinned. The per-<c>T</c> map-entry array is
    /// the part that is built once — cached statically and warmed in
    /// <see cref="Setup"/>, the <c>SpecializationInfoBenchmarks</c> shape.
    /// </remarks>
    [Benchmark]
    public void Build_MeshPipeline_WithSpecialization()
    {
        var meshValues = new MeshSpecConstants { Scale = 0.5f };
        var taskValues = new MeshSpecConstants { Scale = 1.0f };

        using var pipeline = _device.BuildGraphicsPipeline()
            .WithMeshStages(in _meshModule, in _fragModule)
            .WithTaskStage(in _taskModule)
            .WithMeshSpecialization(SpecializationInfo.For<MeshSpecConstants>(in meshValues))
            .WithTaskSpecialization(SpecializationInfo.For<MeshSpecConstants>(in taskValues))
            .WithDynamicRendering(_colorFormats)
            .WithLayout(in _layout)
            .Build();
    }

    private ColorAttachment ColorAttachmentSlot() => new()
    {
        View    = _view,
        Layout  = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
        LoadOp  = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_DONT_CARE,
        StoreOp = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_DONT_CARE,
    };

    private static string ShaderPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Shaders", fileName);
}
