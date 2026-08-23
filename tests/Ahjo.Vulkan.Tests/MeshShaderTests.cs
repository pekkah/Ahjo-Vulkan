using System.IO;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Covers the <c>VK_EXT_mesh_shader</c> wrapper surface (#201): the pipeline
/// builder's mesh path, the three <see cref="CommandRecorder"/>
/// <c>DrawMeshTasks*</c> forwards, and the gated device-extension entry-point
/// loading in <c>Internal/DeviceFunctionTable</c>.
/// </summary>
/// <remarks>
/// <para>Three tiers, deliberately weighted toward the driver-agnostic one.
/// <b>Builder rejections</b> and the <b>gating proof</b> are
/// <c>[gate:driver]</c>: <c>Build()</c>'s guards run before any native call
/// and <see cref="ShaderModule.FromRaw"/> supplies non-null handles with no
/// driver involvement, so they run on any host with an ICD — mesh-capable or
/// not. Only the <b>mesh tier</b> is <c>[gate:feature]</c>; a CI run that
/// reports every one of those as skipped is the expected outcome, not a
/// failure to fix (no probe exists for <c>VK_EXT_mesh_shader</c> on the
/// hosted Windows runner).</para>
/// </remarks>
public sealed unsafe class MeshShaderTests
{
    // ---- Tier 2: builder rejections. [gate:driver] only. ----
    //
    // Every one of these needs a Device solely to obtain the builder and a
    // PipelineLayout; Build() throws from its own preamble, so
    // vkCreateGraphicsPipelines is never reached and the 0xDEADBEEF module
    // handles never leave managed code.

    [Fact]
    public void Builder_TaskStageWithoutMeshStages_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);
        using var layout   = device.CreatePipelineLayout(default);

        ShaderModule dummy = DummyModule();
        VkFormat[] colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];

        var ex = Assert.Throws<InvalidOperationException>(() =>
            device.BuildGraphicsPipeline()
                .WithStages(in dummy, in dummy)
                .WithTaskStage(in dummy)
                .WithDynamicRendering(colorFormats)
                .WithLayout(in layout)
                .Build());

        Assert.Contains("WithTaskStage requires WithMeshStages", ex.Message, StringComparison.Ordinal);
        Assert.Contains("stage-02096", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Builder_VertexAndMeshStages_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);
        using var layout   = device.CreatePipelineLayout(default);

        ShaderModule dummy = DummyModule();
        VkFormat[] colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];

        var ex = Assert.Throws<InvalidOperationException>(() =>
            device.BuildGraphicsPipeline()
                .WithStages(in dummy, in dummy)
                .WithMeshStages(in dummy, in dummy)
                .WithDynamicRendering(colorFormats)
                .WithLayout(in layout)
                .Build());

        Assert.Contains("mutually exclusive", ex.Message, StringComparison.Ordinal);
        Assert.Contains("pStages-02095", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Builder_MeshAndGeometryStages_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);
        using var layout   = device.CreatePipelineLayout(default);

        ShaderModule dummy = DummyModule();
        VkFormat[] colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];

        var ex = Assert.Throws<InvalidOperationException>(() =>
            device.BuildGraphicsPipeline()
                .WithMeshStages(in dummy, in dummy)
                .WithGeometryStage(in dummy)
                .WithDynamicRendering(colorFormats)
                .WithLayout(in layout)
                .Build());

        Assert.Contains("WithGeometryStage", ex.Message, StringComparison.Ordinal);
        Assert.Contains("pStages-02095", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Builder_MeshAndTessellationStages_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);
        using var layout   = device.CreatePipelineLayout(default);

        ShaderModule dummy = DummyModule();
        VkFormat[] colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];

        var ex = Assert.Throws<InvalidOperationException>(() =>
            device.BuildGraphicsPipeline()
                .WithMeshStages(in dummy, in dummy)
                // No WithTessellation(patchControlPoints) needed: the
                // stage-family guards run ahead of the tessellation guards, so
                // this reports the family mix rather than sending the caller
                // off to add a call that would then throw something else.
                .WithTessellationStages(in dummy, in dummy)
                .WithDynamicRendering(colorFormats)
                .WithLayout(in layout)
                .Build());

        Assert.Contains("WithTessellationStages", ex.Message, StringComparison.Ordinal);
        Assert.Contains("pStages-02095", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Builder_MeshWithVertexInput_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);
        using var layout   = device.CreatePipelineLayout(default);

        ShaderModule dummy = DummyModule();
        VkFormat[] colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];

        VertexBindingDescription[]   bindings = [new VertexBindingDescription { Slot = 0, Stride = 16 }];
        VertexAttributeDescription[] attrs    =
        [
            new VertexAttributeDescription
            {
                Location = 0, Binding = 0, Format = VkFormat.VK_FORMAT_R32G32B32A32_SFLOAT, Offset = 0,
            },
        ];
        var ex = Assert.Throws<InvalidOperationException>(() =>
            device.BuildGraphicsPipeline()
                .WithMeshStages(in dummy, in dummy)
                // Built inside the lambda: VertexInputDescription is a ref
                // struct and cannot be captured.
                .WithVertexInput(new VertexInputDescription { Bindings = bindings, Attributes = attrs })
                .WithDynamicRendering(colorFormats)
                .WithLayout(in layout)
                .Build());

        Assert.Contains("WithVertexInput", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Builder_MeshWithTopology_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);
        using var layout   = device.CreatePipelineLayout(default);

        ShaderModule dummy = DummyModule();
        VkFormat[] colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];

        var ex = Assert.Throws<InvalidOperationException>(() =>
            device.BuildGraphicsPipeline()
                .WithMeshStages(in dummy, in dummy)
                // Deliberately the builder's own default value: the guard keys
                // on "WithTopology was called", not on the value, because
                // _topology defaults to TRIANGLE_LIST.
                .WithTopology(VkPrimitiveTopology.VK_PRIMITIVE_TOPOLOGY_TRIANGLE_LIST)
                .WithDynamicRendering(colorFormats)
                .WithLayout(in layout)
                .Build());

        Assert.Contains("WithTopology", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Builder_MeshWithTessellationPatchSize_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);
        using var layout   = device.CreatePipelineLayout(default);

        ShaderModule dummy = DummyModule();
        VkFormat[] colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];

        var ex = Assert.Throws<InvalidOperationException>(() =>
            device.BuildGraphicsPipeline()
                .WithMeshStages(in dummy, in dummy)
                .WithTessellation(3)
                .WithDynamicRendering(colorFormats)
                .WithLayout(in layout)
                .Build());

        Assert.Contains("WithTessellation", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(VkDynamicState.VK_DYNAMIC_STATE_PRIMITIVE_TOPOLOGY,          "pDynamicStates-07065")]
    [InlineData(VkDynamicState.VK_DYNAMIC_STATE_VERTEX_INPUT_BINDING_STRIDE, "pDynamicStates-07065")]
    [InlineData(VkDynamicState.VK_DYNAMIC_STATE_PRIMITIVE_RESTART_ENABLE,    "pDynamicStates-07066")]
    [InlineData(VkDynamicState.VK_DYNAMIC_STATE_PATCH_CONTROL_POINTS_EXT,    "pDynamicStates-07066")]
    [InlineData(VkDynamicState.VK_DYNAMIC_STATE_VERTEX_INPUT_EXT,            "pDynamicStates-07067")]
    public void Builder_MeshWithForbiddenDynamicState_Throws(VkDynamicState state, string vuid)
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);
        using var layout   = device.CreatePipelineLayout(default);

        ShaderModule dummy = DummyModule();
        VkFormat[] colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];
        VkDynamicState[] dynamicStates =
        [
            VkDynamicState.VK_DYNAMIC_STATE_VIEWPORT,
            VkDynamicState.VK_DYNAMIC_STATE_SCISSOR,
            state,
        ];

        var ex = Assert.Throws<InvalidOperationException>(() =>
            device.BuildGraphicsPipeline()
                .WithMeshStages(in dummy, in dummy)
                .WithDynamicState(dynamicStates)
                .WithDynamicRendering(colorFormats)
                .WithLayout(in layout)
                .Build());

        Assert.Contains(vuid, ex.Message, StringComparison.Ordinal);
        Assert.Contains(state.ToString(), ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The "no stages configured" message must name both entry points, or a
    /// caller who intended a mesh pipeline is told to call a method that would
    /// have been rejected anyway.
    /// </summary>
    [Fact]
    public void Builder_MissingStages_MessageNamesMeshStages()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);

        var ex = Assert.Throws<InvalidOperationException>(() => device.BuildGraphicsPipeline().Build());
        Assert.Contains("WithMeshStages", ex.Message, StringComparison.Ordinal);
        Assert.Contains("WithStages", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A <c>default</c> <see cref="ShaderModule"/> must be rejected at the
    /// call site. This is not cosmetic on the mesh path:
    /// <c>Build()</c> selects it on <c>_mesh != null</c>, so
    /// <c>WithMeshStages(default, frag)</c> would set <c>_stagesSet</c>
    /// without selecting the mesh path, skip all seven mesh guards, and emit a
    /// <c>VK_SHADER_STAGE_VERTEX_BIT</c> stage with a null module — a driver
    /// rejection (VUID-VkPipelineShaderStageCreateInfo-module-parameter) that
    /// never mentions mesh at all.
    /// </summary>
    [Fact]
    public void Builder_NullShaderModule_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);

        ShaderModule dummy = DummyModule();
        ShaderModule none  = default;

        var mesh = Assert.Throws<ArgumentException>(() =>
            device.BuildGraphicsPipeline().WithMeshStages(in none, in dummy));
        Assert.Equal("mesh", mesh.ParamName);

        var meshFrag = Assert.Throws<ArgumentException>(() =>
            device.BuildGraphicsPipeline().WithMeshStages(in dummy, in none));
        Assert.Equal("fragment", meshFrag.ParamName);

        var task = Assert.Throws<ArgumentException>(() =>
            device.BuildGraphicsPipeline().WithTaskStage(in none));
        Assert.Equal("task", task.ParamName);

        // Same gap on the classic path, closed for symmetry.
        var vert = Assert.Throws<ArgumentException>(() =>
            device.BuildGraphicsPipeline().WithStages(in none, in dummy));
        Assert.Equal("vertex", vert.ParamName);

        var frag = Assert.Throws<ArgumentException>(() =>
            device.BuildGraphicsPipeline().WithStages(in dummy, in none));
        Assert.Equal("fragment", frag.ParamName);
    }

    // ---- Tier 2: gating proof. [gate:driver] only. ----

    /// <summary>
    /// The direct proof that resolution is gated on the enabled list: a device
    /// created without <c>VK_EXT_mesh_shader</c> leaves all three pointers
    /// null even on a host whose driver exposes the extension.
    /// </summary>
    [Fact]
    public void DeviceWithoutMeshShaderExtension_LeavesEntryPointsNull()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);

        Assert.True(device.Functions.CmdDrawMeshTasks              == null);
        Assert.True(device.Functions.CmdDrawMeshTasksIndirect      == null);
        Assert.True(device.Functions.CmdDrawMeshTasksIndirectCount == null);
    }

    /// <summary>
    /// The builder's own extension guard. Without it, a mesh stage on a
    /// device created without <c>VK_EXT_mesh_shader</c> reaches
    /// <c>vkCreateGraphicsPipelines</c> and fails as
    /// VUID-VkPipelineShaderStageCreateInfo-stage-02091 — while
    /// <see cref="CommandRecorder.DrawMeshTasks"/>, which names the
    /// extension for exactly this misconfiguration, is never reached,
    /// because the builder runs first.
    /// </summary>
    [Fact]
    public void Builder_MeshStagesWithoutExtension_ThrowsNamingTheExtension()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);
        using var layout   = device.CreatePipelineLayout(default);

        ShaderModule dummy = DummyModule();
        VkFormat[] colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];

        var ex = Assert.Throws<InvalidOperationException>(() =>
            device.BuildGraphicsPipeline()
                .WithMeshStages(in dummy, in dummy)
                .WithDynamicRendering(colorFormats)
                .WithLayout(in layout)
                .Build());

        Assert.Contains("VK_EXT_mesh_shader", ex.Message, StringComparison.Ordinal);
        Assert.Contains("stage-02091", ex.Message, StringComparison.Ordinal);
        // The guard is extension-only. It must say so, or a caller reading it
        // will assume that getting past it means the meshShader FEATURE is on
        // too — which the wrapper cannot see after vkCreateDevice.
        Assert.Contains("only proves the extension was enabled", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ordering proof for the guard above: it is deliberately LAST among the
    /// mesh guards, so static misuse of the builder — true on any device —
    /// keeps producing the more actionable message even when the device also
    /// lacks the extension. Without this, every builder-rejection test in
    /// this file would silently start asserting on the extension message
    /// instead of the guard it names.
    /// </summary>
    [Fact]
    public void Builder_MeshMisuseOutranksTheExtensionGuard()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);
        using var layout   = device.CreatePipelineLayout(default);

        ShaderModule dummy = DummyModule();
        VkFormat[] colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];

        var ex = Assert.Throws<InvalidOperationException>(() =>
            device.BuildGraphicsPipeline()
                .WithMeshStages(in dummy, in dummy)
                .WithTopology(VkPrimitiveTopology.VK_PRIMITIVE_TOPOLOGY_TRIANGLE_LIST)
                .WithDynamicRendering(colorFormats)
                .WithLayout(in layout)
                .Build());

        Assert.Contains("WithTopology", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("vkCmdDrawMeshTasksEXT did not resolve", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Each of the three recorder forwards must throw a message naming the
    /// extension rather than dispatching through the null pointer. The check
    /// is unconditional (not behind <c>AhjoValidation</c>), so this holds in
    /// Release too. The <c>Buffer</c> arguments are never dereferenced — the
    /// throw happens before the native call.
    /// </summary>
    [Fact]
    public void DrawMeshTasks_WithoutExtension_ThrowsNamingTheExtension()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);
        using var cmdPool  = new CommandBufferPool(device, family);

        Buffer unused = default;

        // CommandRecorder is a ref struct and cannot be captured by the
        // Assert.Throws lambda, so each call is bracketed by hand.
        using (var rec = cmdPool.Begin())
        {
            InvalidOperationException? caught = null;
            try { rec.DrawMeshTasks(1, 1, 1); }
            catch (InvalidOperationException ex) { caught = ex; }
            AssertNamesMeshExtension(caught);

            caught = null;
            try { rec.DrawMeshTasksIndirect(in unused, offset: 0, drawCount: 1, stride: 12); }
            catch (InvalidOperationException ex) { caught = ex; }
            AssertNamesMeshExtension(caught);

            caught = null;
            try
            {
                rec.DrawMeshTasksIndirectCount(
                    in unused, offset: 0,
                    in unused, countBufferOffset: 0,
                    maxDrawCount: 1, stride: 12);
            }
            catch (InvalidOperationException ex) { caught = ex; }
            AssertNamesMeshExtension(caught);

            rec.End();
        }

        cmdPool.ResetForFrame();
    }

    // ---- Tier 3: needs a driver that exposes VK_EXT_mesh_shader. [gate:feature] ----

    [Fact]
    public void MeshDevice_ResolvesAllThreeEntryPoints()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using Device? device = TryCreateMeshDevice(instance, out _);
        TestGate.RequireDeviceFeature(device is not null, "Device does not expose VK_EXT_mesh_shader.");

        Assert.True(device!.Functions.CmdDrawMeshTasks              != null);
        Assert.True(device.Functions.CmdDrawMeshTasksIndirect       != null);
        Assert.True(device.Functions.CmdDrawMeshTasksIndirectCount  != null);
    }

    /// <summary>
    /// Built under the validation layer, not a bare instance: a non-null
    /// <see cref="GraphicsPipeline"/> is a weak oracle, because drivers do not
    /// validate. The nulled <c>pVertexInputState</c>/<c>pInputAssemblyState</c>
    /// and the mesh + fragment stage set are only actually *checked* by
    /// CoreChecks, so the assertion that carries the coverage is
    /// <see cref="AssertNoValidationErrors"/>.
    /// </summary>
    [Fact]
    public void MeshOnlyPipeline_Builds()
    {
        TestGate.RequireDriver();
        TestGate.RequireValidationLayer();
        TestGate.RequireSpirv(MeshSpvPath);
        TestGate.RequireSpirv(FragSpvPath);

        var errors = new List<DebugMessage>();
        using var instance = CreateValidatedInstance(errors);
        using Device? device = TryCreateMeshDevice(instance, out _);
        TestGate.RequireDeviceFeature(device is not null, MeshSkipReason);

        using var meshBlob = SpirvBlob.Load(MeshSpvPath);
        using var fragBlob = SpirvBlob.Load(FragSpvPath);
        using var meshMod  = device!.CreateShaderModule(meshBlob.Words);
        using var fragMod  = device.CreateShaderModule(fragBlob.Words);
        using var layout   = device.CreatePipelineLayout(default);

        VkFormat[] colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];

        using var pipeline = device.BuildGraphicsPipeline()
            .WithMeshStages(in meshMod, in fragMod)
            .WithDynamicRendering(colorFormats)
            .WithLayout(in layout)
            .Build();

        Assert.False(pipeline.IsNull);
        AssertNoValidationErrors(errors);
    }

    /// <summary>
    /// The only test of task-stage emission
    /// (<c>GraphicsPipelineBuilder.Build</c>'s
    /// <c>VK_SHADER_STAGE_TASK_BIT_EXT</c> branch), so it runs under the
    /// validation layer. A regression that emitted the mesh stage bit twice,
    /// or pointed a stage's <c>pName</c> at the wrong inline entry-point
    /// buffer, still returns a non-null pipeline on most drivers — only
    /// CoreChecks says otherwise.
    /// </summary>
    [Fact]
    public void TaskAndMeshPipeline_Builds()
    {
        TestGate.RequireDriver();
        TestGate.RequireValidationLayer();
        TestGate.RequireSpirv(MeshSpvPath);
        TestGate.RequireSpirv(TaskSpvPath);
        TestGate.RequireSpirv(FragSpvPath);

        var errors = new List<DebugMessage>();
        using var instance = CreateValidatedInstance(errors);
        using Device? device = TryCreateMeshDevice(instance, out _, requireTaskShader: true);
        TestGate.RequireDeviceFeature(device is not null, TaskSkipReason);

        using var taskBlob = SpirvBlob.Load(TaskSpvPath);
        using var meshBlob = SpirvBlob.Load(MeshSpvPath);
        using var fragBlob = SpirvBlob.Load(FragSpvPath);
        using var taskMod  = device!.CreateShaderModule(taskBlob.Words);
        using var meshMod  = device.CreateShaderModule(meshBlob.Words);
        using var fragMod  = device.CreateShaderModule(fragBlob.Words);
        using var layout   = device.CreatePipelineLayout(default);

        VkFormat[] colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];

        using var pipeline = device.BuildGraphicsPipeline()
            .WithMeshStages(in meshMod, in fragMod)
            .WithTaskStage(in taskMod)
            .WithDynamicRendering(colorFormats)
            .WithLayout(in layout)
            .Build();

        Assert.False(pipeline.IsNull);
        AssertNoValidationErrors(errors);
    }

    /// <summary>
    /// Entry-point override + specialization constant round-trip into a
    /// successful build: <c>mesh_tri.mesh</c> declares
    /// <c>layout(constant_id = 0) const float uScale</c>, so the map entry
    /// binds to a constant that really exists in the module. Under the
    /// validation layer, because a mis-wired <c>pSpecializationInfo</c> — an
    /// entry whose offset/size runs past the supplied data, or one naming a
    /// constant the module does not declare — is exactly what CoreChecks
    /// catches and a driver silently tolerates.
    /// </summary>
    [Fact]
    public void MeshPipeline_EntryPointAndSpecialization_RoundTrip()
    {
        TestGate.RequireDriver();
        TestGate.RequireValidationLayer();
        TestGate.RequireSpirv(MeshSpvPath);
        TestGate.RequireSpirv(FragSpvPath);

        var errors = new List<DebugMessage>();
        using var instance = CreateValidatedInstance(errors);
        using Device? device = TryCreateMeshDevice(instance, out _);
        TestGate.RequireDeviceFeature(device is not null, MeshSkipReason);

        using var meshBlob = SpirvBlob.Load(MeshSpvPath);
        using var fragBlob = SpirvBlob.Load(FragSpvPath);
        using var meshMod  = device!.CreateShaderModule(meshBlob.Words);
        using var fragMod  = device.CreateShaderModule(fragBlob.Words);
        using var layout   = device.CreatePipelineLayout(default);

        VkFormat[] colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];
        var specValues = new MeshSpecConstants { Scale = 0.5f };

        using var pipeline = device.BuildGraphicsPipeline()
            .WithMeshStages(in meshMod, in fragMod)
            .WithMeshEntryPoint("main"u8)
            .WithMeshSpecialization(SpecializationInfo.For<MeshSpecConstants>(in specValues))
            .WithDynamicRendering(colorFormats)
            .WithLayout(in layout)
            .Build();

        Assert.False(pipeline.IsNull);
        AssertNoValidationErrors(errors);
    }

    /// <summary>
    /// Negative control for the forbidden-dynamic-state scan: viewport +
    /// scissor are legal on a mesh pipeline, so the guard must not reject
    /// them. Runs at the mesh tier because the only way to prove the guard
    /// did not fire is to let the build reach — and succeed at —
    /// <c>vkCreateGraphicsPipelines</c>.
    /// </summary>
    [Fact]
    public void MeshPipeline_ViewportScissorDynamicState_IsAccepted()
    {
        TestGate.RequireDriver();
        TestGate.RequireValidationLayer();
        TestGate.RequireSpirv(MeshSpvPath);
        TestGate.RequireSpirv(FragSpvPath);

        var errors = new List<DebugMessage>();
        using var instance = CreateValidatedInstance(errors);
        using Device? device = TryCreateMeshDevice(instance, out _);
        TestGate.RequireDeviceFeature(device is not null, MeshSkipReason);

        using var meshBlob = SpirvBlob.Load(MeshSpvPath);
        using var fragBlob = SpirvBlob.Load(FragSpvPath);
        using var meshMod  = device!.CreateShaderModule(meshBlob.Words);
        using var fragMod  = device.CreateShaderModule(fragBlob.Words);
        using var layout   = device.CreatePipelineLayout(default);

        VkFormat[] colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];
        VkDynamicState[] dynamicStates =
        [
            VkDynamicState.VK_DYNAMIC_STATE_VIEWPORT,
            VkDynamicState.VK_DYNAMIC_STATE_SCISSOR,
        ];

        using var pipeline = device.BuildGraphicsPipeline()
            .WithMeshStages(in meshMod, in fragMod)
            .WithDynamicState(dynamicStates)
            .WithDynamicRendering(colorFormats)
            .WithLayout(in layout)
            .Build();

        Assert.False(pipeline.IsNull);
        AssertNoValidationErrors(errors);
    }

    /// <summary>
    /// The allocation canary for the builder's mesh path, asserted rather than
    /// read off a BenchmarkDotNet column. <c>Build()</c>'s mesh branch adds
    /// four <c>fixed</c> statements and a stage-emission branch over the
    /// classic path; the stage array, blend and dynamic-state spans are all
    /// <c>stackalloc</c> and <see cref="GraphicsPipeline"/> is a
    /// <c>readonly struct</c>, so the whole
    /// <c>WithMeshStages().WithTaskStage()…Build()</c> chain must move zero
    /// managed bytes.
    /// </summary>
    /// <remarks>
    /// <see cref="AhjoValidation"/> is forced <b>off</b> around the measured
    /// loop: the double-dispose registry it gates is a <c>HashSet</c> insert
    /// per owning handle, which allocates while growing and is on by default
    /// in DEBUG. The contract under test is the Release one — the wrapper's
    /// own marshalling — not the debug registry's. The suite runs
    /// single-threaded (<c>xunit.runner.json</c>: <c>maxParallelThreads = 1</c>),
    /// so the process-global flip is safe.
    /// </remarks>
    [Fact]
    public void MeshPipeline_Build_IsZeroAllocation()
    {
        TestGate.RequireDriver();
        TestGate.RequireSpirv(MeshSpvPath);
        TestGate.RequireSpirv(TaskSpvPath);
        TestGate.RequireSpirv(FragSpvPath);

        using var instance = Instance.Create(default);
        using Device? device = TryCreateMeshDevice(instance, out _, requireTaskShader: true);
        TestGate.RequireDeviceFeature(device is not null, TaskSkipReason);

        using var meshBlob = SpirvBlob.Load(MeshSpvPath);
        using var taskBlob = SpirvBlob.Load(TaskSpvPath);
        using var fragBlob = SpirvBlob.Load(FragSpvPath);
        using var meshMod  = device!.CreateShaderModule(meshBlob.Words);
        using var taskMod  = device.CreateShaderModule(taskBlob.Words);
        using var fragMod  = device.CreateShaderModule(fragBlob.Words);
        using var layout   = device.CreatePipelineLayout(default);

        // Hoisted: the array itself is setup, not part of the measured body —
        // the same shape MeshShaderBenchmarks.Build_MeshPipeline uses.
        VkFormat[] colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];

        void BuildOnce()
        {
            using var pipeline = device.BuildGraphicsPipeline()
                .WithMeshStages(in meshMod, in fragMod)
                .WithTaskStage(in taskMod)
                .WithDynamicRendering(colorFormats)
                .WithLayout(in layout)
                .Build();
        }

        bool priorValidation = AhjoValidation.Enabled;
        AhjoValidation.Enabled = false;
        try
        {
            // Warm: JIT + tier-up on every path the measured loop touches.
            for (int i = 0; i < 32; i++) BuildOnce();

            // Two measured passes, the ChainBuilderTests shape: a tier-1 →
            // tier-2 promotion can still fire on the first measurement-sized
            // loop and charge a one-shot allocation to this thread. Only the
            // second pass is asserted on.
            long before1 = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++) BuildOnce();
            _ = GC.GetAllocatedBytesForCurrentThread() - before1;

            long before2 = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++) BuildOnce();
            long after2 = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(0, after2 - before2);
        }
        finally
        {
            AhjoValidation.Enabled = priorValidation;
        }
    }

    /// <summary>
    /// The end-to-end oracle: record a real mesh draw inside a dynamic-
    /// rendering scope, submit it, and assert the validation layer logged no
    /// errors. This is what proves the nulled
    /// <c>pVertexInputState</c>/<c>pInputAssemblyState</c> and the
    /// mesh/fragment stage set are actually accepted, rather than merely
    /// not crashing.
    /// </summary>
    [Fact]
    public void DrawMeshTasks_EndToEnd_ProducesNoValidationErrors()
        => RunDrawMeshTasksEndToEnd(withTaskStage: false);

    /// <summary>
    /// The same oracle with a task stage ahead of the mesh stage. The
    /// task/mesh pair changes which VUIDs apply at draw time — the group
    /// counts are bounded by <c>maxTaskWorkGroupCount</c> rather than
    /// <c>maxMeshWorkGroupCount</c>
    /// (VUID-vkCmdDrawMeshTasksEXT-TaskEXT-07322 vs -07326) — and it is the
    /// only place the builder's <c>VK_SHADER_STAGE_TASK_BIT_EXT</c> emission
    /// is checked against something that actually validates it.
    /// </summary>
    [Fact]
    public void DrawMeshTasks_TaskStage_EndToEnd_ProducesNoValidationErrors()
        => RunDrawMeshTasksEndToEnd(withTaskStage: true);

    private static void RunDrawMeshTasksEndToEnd(bool withTaskStage)
    {
        TestGate.RequireDriver();
        TestGate.RequireHardwareDriver(
            "Software ICD (Mesa lavapipe): vkQueueSubmit2 SIGSEGVs during command-buffer execution.");
        TestGate.RequireValidationLayer();
        TestGate.RequireSpirv(MeshSpvPath);
        TestGate.RequireSpirv(FragSpvPath);
        if (withTaskStage) TestGate.RequireSpirv(TaskSpvPath);

        var errors = new List<DebugMessage>();
        using var instance = CreateValidatedInstance(errors);
        using Device? device = TryCreateMeshDevice(instance, out uint family, withTaskStage);
        TestGate.RequireDeviceFeature(device is not null, withTaskStage ? TaskSkipReason : MeshSkipReason);

        const uint W = 64, H = 64;
        using var image = device!.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType     = VkImageType.VK_IMAGE_TYPE_2D,
                Format        = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width         = W, Height = H, Depth = 1,
                MipLevels     = 1, ArrayLayers = 1,
                Samples       = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling        = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage         = ImageUsage.ColorAttachment,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });
        using var view = image.CreateView(device, new ImageViewDescription
        {
            ViewType   = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D,
            Aspect     = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
            LevelCount = 1, LayerCount = 1,
        });

        using var meshBlob = SpirvBlob.Load(MeshSpvPath);
        using var fragBlob = SpirvBlob.Load(FragSpvPath);
        using var meshMod  = device.CreateShaderModule(meshBlob.Words);
        using var fragMod  = device.CreateShaderModule(fragBlob.Words);
        // default(ShaderModule) on the mesh-only path; its Dispose is a no-op.
        using var taskMod  = withTaskStage ? LoadTaskModule(device) : default;
        using var layout   = device.CreatePipelineLayout(default);

        VkFormat[] colorFormats = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];
        var builder = device.BuildGraphicsPipeline().WithMeshStages(in meshMod, in fragMod);
        if (withTaskStage) builder = builder.WithTaskStage(in taskMod);
        using var pipeline = builder
            .WithDynamicRendering(colorFormats)
            .WithLayout(in layout)
            .Build();

        using var cmdPool   = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        Fence fence = fencePool.Acquire();
        try
        {
            var rec = cmdPool.Begin();
            try
            {
                rec.PipelineBarrier(ImageBarrier.Transition(in image,
                    from:     VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                    to:       VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    srcStage: Stage.TopOfPipe,             srcAccess: Access.None,
                    dstStage: Stage.ColorAttachmentOutput, dstAccess: Access.ColorAttachmentWrite));

                ReadOnlySpan<ColorAttachment> color =
                [
                    new ColorAttachment
                    {
                        View       = view,
                        Layout     = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                        LoadOp     = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_CLEAR,
                        StoreOp    = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE,
                        ClearColor = new VkClearColorValue(),
                    },
                ];

                rec.BeginRendering(new RenderingInfo
                {
                    RenderArea       = new VkRect2D { extent = new VkExtent2D { width = W, height = H } },
                    LayerCount       = 1,
                    ColorAttachments = color,
                });
                rec.BindPipeline(in pipeline);
                rec.SetViewport(new VkViewport { x = 0, y = 0, width = W, height = H, minDepth = 0, maxDepth = 1 });
                rec.SetScissor(new VkRect2D { extent = new VkExtent2D { width = W, height = H } });
                rec.DrawMeshTasks(1, 1, 1);
                rec.EndRendering();

                Queue queue = device.GetQueue(family, queueIndex: 0);
                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(5)));
        }
        finally { fencePool.Release(fence); }

        AssertNoValidationErrors(errors);
    }

    // ---- Helpers ----

    private const string MeshSkipReason =
        "Device does not expose VK_EXT_mesh_shader.";

    /// <summary>
    /// <c>taskShader</c> is advertised independently of <c>meshShader</c>, so
    /// this is a strictly narrower gate than <see cref="MeshSkipReason"/> and
    /// must only be asked for by tests that actually emit a task stage.
    /// </summary>
    private const string TaskSkipReason =
        "Device does not expose VK_EXT_mesh_shader with the taskShader feature.";

    private static ShaderModule LoadTaskModule(Device device)
    {
        using var blob = SpirvBlob.Load(TaskSpvPath);
        return device.CreateShaderModule(blob.Words);
    }

    private static void AssertNamesMeshExtension(InvalidOperationException? caught)
    {
        Assert.NotNull(caught);
        Assert.Contains("VK_EXT_mesh_shader", caught!.Message, StringComparison.Ordinal);
    }

    private struct MeshSpecConstants
    {
        public float Scale;
    }

    /// <summary>
    /// A non-null module handle with no owning device — enough for the
    /// builder's guards, which run before <c>vkCreateGraphicsPipelines</c>
    /// and never dereference it.
    /// </summary>
    private static ShaderModule DummyModule() => ShaderModule.FromRaw(unchecked((nint)0xDEADBEEF));

    private static string MeshSpvPath =>
        Path.Combine(AppContext.BaseDirectory, "Shaders", "mesh_tri.mesh.spv");

    private static string TaskSpvPath =>
        Path.Combine(AppContext.BaseDirectory, "Shaders", "mesh_tri.task.spv");

    private static string FragSpvPath =>
        Path.Combine(AppContext.BaseDirectory, "Shaders", "triangle.frag.spv");

    private static Instance CreateValidatedInstance(List<DebugMessage> errors)
        => Instance.Create(new InstanceDescription
        {
            ApiVersion       = VulkanVersion.V1_4,
            EnableValidation = true,
            DebugCallback    = m =>
            {
                if ((m.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) != 0)
                    lock (errors) errors.Add(m);
            },
        });

    private static void AssertNoValidationErrors(List<DebugMessage> errors)
    {
        lock (errors)
            Assert.True(errors.Count == 0,
                "Validation errors recorded: " + string.Join("; ", errors.ConvertAll(e => e.Message)));
    }

    private static Device CreateGraphicsDevice(Instance instance, out uint family)
    {
        uint f = uint.MaxValue;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    f = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });
        family = f;
        return gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });
    }

    /// <summary>
    /// Creates a device with <c>VK_EXT_mesh_shader</c> plus the
    /// <c>meshShader</c> feature — and <c>taskShader</c> only when
    /// <paramref name="requireTaskShader"/> asks for it — or returns
    /// <see langword="null"/> when no GPU on this host can supply them: the
    /// clean skip signal for the mesh tier. The picker screens on
    /// <see cref="PhysicalDeviceInfo.SupportsExtension"/> so a host whose
    /// first graphics-capable GPU is not mesh-capable (an integrated GPU
    /// beside a discrete one) still finds the one that is.
    /// </summary>
    /// <remarks>
    /// <paramref name="requireTaskShader"/> is opt-in, and that is the whole
    /// point: <c>taskShader</c> is optional independently of
    /// <c>meshShader</c>, so requesting it unconditionally would make
    /// <c>vkCreateDevice</c> return <c>VK_ERROR_FEATURE_NOT_PRESENT</c> on a
    /// mesh-only device and skip the <b>entire</b> mesh tier — including
    /// every test that needs nothing but <c>meshShader</c>. A partial-
    /// capability host would then read as a clean skip rather than as the
    /// coverage hole it is.
    /// </remarks>
    private static Device? TryCreateMeshDevice(Instance instance, out uint family, bool requireTaskShader = false)
    {
        uint f = uint.MaxValue;
        PhysicalDevice gpu;
        try
        {
            gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
            {
                if (!info.SupportsExtension(DeviceExtensionNames.MeshShader)) return false;
                for (int i = 0; i < info.QueueFamilies.Length; i++)
                {
                    if (info.QueueFamilies[i].SupportsGraphics)
                    {
                        f = info.QueueFamilies[i].Index;
                        return true;
                    }
                }
                return false;
            });
        }
        catch (VulkanException ex) when (ex.Result == VkResult.VK_ERROR_INITIALIZATION_FAILED)
        {
            family = 0;
            return null;
        }

        family = f;
        Utf8Name[] extensions = [VulkanExtensions.ExtMeshShader];
        try
        {
            return gpu.CreateDevice(new DeviceDescription
            {
                Queues     = [new QueueRequest(f, count: 1, priority: 1.0f)],
                Extensions = extensions,
                // Not `static`: the closure over requireTaskShader is what
                // keeps taskShader off the mesh-only path. Setup-time in a
                // test, so the capture costs nothing that matters.
                ConfigureFeatures = (
                    ref ChainBuilder<VkDeviceCreateInfo> chain,
                    ref VkPhysicalDeviceFeatures2 _,
                    ref VkPhysicalDeviceVulkan12Features _,
                    ref VkPhysicalDeviceVulkan13Features _,
                    ref VkPhysicalDeviceVulkan14Features _) =>
                {
                    ref var mesh = ref chain.Push<VkPhysicalDeviceMeshShaderFeaturesEXT>();
                    mesh.meshShader = 1;
                    if (requireTaskShader) mesh.taskShader = 1;
                },
            });
        }
        catch (VulkanException ex) when (
            ex.Result == VkResult.VK_ERROR_EXTENSION_NOT_PRESENT ||
            ex.Result == VkResult.VK_ERROR_FEATURE_NOT_PRESENT)
        {
            return null;
        }
    }
}
