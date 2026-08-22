using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Fluent builder for <see cref="GraphicsPipeline"/>. <c>ref struct</c>
/// so the in-progress configuration cannot escape the build scope and
/// the inline UTF-8 entry-point bytes don't need a heap allocation.
/// </summary>
/// <remarks>
/// <para>The builder targets the modern Vulkan 1.4 path: dynamic
/// rendering, dynamic viewport / scissor, no <c>VkRenderPass</c>. The
/// only attachment-formats path is <see cref="WithDynamicRendering"/>.</para>
/// <para>Defaults match the dominant case so a triangle pipeline is two
/// or three lines: <c>TRIANGLE_LIST</c> topology, fill polygon, no
/// culling, CCW front-face, depth disabled, single opaque color blend
/// per color attachment, dynamic viewport + scissor.</para>
/// <para><b>Mesh path.</b> <see cref="WithMeshStages"/> (optionally plus
/// <see cref="WithTaskStage"/>) selects the mesh-shading front end instead
/// of <see cref="WithStages"/>. Mesh and classic stages are mutually
/// exclusive — Vulkan requires a pipeline's geometric stages to come all
/// from the mesh-shading family (task/mesh) or all from the
/// primitive-shading family (vertex/tess/geometry), so
/// <see cref="Build"/> rejects any mix, along with the vertex-input,
/// topology, patch-size and dynamic-state configuration a mesh pipeline
/// would otherwise silently discard. Everything else — rasterization,
/// multisample, depth-stencil, color blend, dynamic rendering, layout,
/// cache — is configured identically on both paths. Requires
/// <c>VK_EXT_mesh_shader</c> and the <c>meshShader</c> (plus, for a task
/// stage, <c>taskShader</c>) feature on the device.</para>
/// <para><b>Aliasing.</b> Each <c>WithX</c> returns the builder by
/// value, so an aliased reference (<c>var b1 = builder.WithA(...);
/// builder.WithB(...);</c>) yields two independent copies that diverge
/// silently — the captured span fields (<see cref="WithVertexInput"/>,
/// <see cref="WithDynamicRendering"/>, <see cref="WithColorBlend"/>,
/// <see cref="WithDynamicState"/>) point at the original caller's
/// memory and don't crash, but the second builder won't see the first's
/// edits and vice versa. The intended pattern is a single chained
/// expression <c>builder.WithA(...).WithB(...).Build()</c>; do not
/// stash intermediate copies. (<c>ref this</c> returns would prevent
/// the divergence, but with C# 14's ref-safety analysis the captured
/// stack-bound spans become non-escapable through an
/// <c>[UnscopedRef]</c> return and the chain stops compiling at the
/// span-passing call site.)</para>
/// </remarks>
public unsafe ref struct GraphicsPipelineBuilder
{
    private const int MaxStages = 5;

    private readonly Device _device;

    // Stages. On the classic path vert + frag are required and geom +
    // tessControl + tessEval are optional; on the mesh path mesh + frag are
    // required and task is optional. The two paths are mutually exclusive.
    private VkShaderModule_T* _vert;
    private VkShaderModule_T* _frag;
    private VkShaderModule_T* _geom;
    private VkShaderModule_T* _tessControl;
    private VkShaderModule_T* _tessEval;
    private VkShaderModule_T* _task;
    private VkShaderModule_T* _mesh;
    private EntryPointBuffer  _vertEntry;
    private EntryPointBuffer  _fragEntry;
    private EntryPointBuffer  _geomEntry;
    private EntryPointBuffer  _tessControlEntry;
    private EntryPointBuffer  _tessEvalEntry;
    private EntryPointBuffer  _taskEntry;
    private EntryPointBuffer  _meshEntry;

    // Vertex input.
    private ReadOnlySpan<VertexBindingDescription>   _vBindings;
    private ReadOnlySpan<VertexAttributeDescription> _vAttrs;

    // Input assembly. _topologySet exists only so the mesh path can reject an
    // explicit WithTopology: _topology defaults to TRIANGLE_LIST in the ctor
    // and cannot otherwise be told apart from "never called".
    private VkPrimitiveTopology _topology;
    private bool                _topologySet;

    // Tessellation.
    private uint _patchControlPoints;

    // Rasterization.
    private VkCullModeFlagBits _cullMode;
    private VkFrontFace        _frontFace;
    private VkPolygonMode      _polygonMode;
    private bool               _depthBiasEnable;
    private float              _depthBiasConstantFactor;
    private float              _depthBiasSlopeFactor;
    private float              _depthBiasClamp;

    // Multisample.
    private VkSampleCountFlagBits _samples;
    private bool                  _sampleShadingEnable;
    private float                 _minSampleShading;

    // Depth stencil.
    private bool        _depthTestEnable;
    private bool        _depthWriteEnable;
    private VkCompareOp _depthCompareOp;

    // Color blend.
    private ReadOnlySpan<ColorBlendAttachment> _blendAttachments;
    private bool           _blendLogicOpEnable;
    private VkLogicOp      _blendLogicOp;
    private BlendConstants _blendConstants;

    // Dynamic rendering.
    private ReadOnlySpan<VkFormat> _colorFormats;
    private VkFormat               _depthFormat;
    private VkFormat               _stencilFormat;

    // Dynamic state. Defaults to viewport + scissor when the caller doesn't override.
    private ReadOnlySpan<VkDynamicState> _dynamicStates;

    // Specialization constants — one slot per stage; null entries mean
    // "no specialization" and the corresponding pSpecializationInfo stays
    // null when the stage is built.
    private void*                       _vertSpecDataPtr;
    private int                         _vertSpecDataSize;
    private VkSpecializationMapEntry[]? _vertSpecEntries;
    private void*                       _fragSpecDataPtr;
    private int                         _fragSpecDataSize;
    private VkSpecializationMapEntry[]? _fragSpecEntries;
    private void*                       _geomSpecDataPtr;
    private int                         _geomSpecDataSize;
    private VkSpecializationMapEntry[]? _geomSpecEntries;
    private void*                       _tessControlSpecDataPtr;
    private int                         _tessControlSpecDataSize;
    private VkSpecializationMapEntry[]? _tessControlSpecEntries;
    private void*                       _tessEvalSpecDataPtr;
    private int                         _tessEvalSpecDataSize;
    private VkSpecializationMapEntry[]? _tessEvalSpecEntries;
    private void*                       _taskSpecDataPtr;
    private int                         _taskSpecDataSize;
    private VkSpecializationMapEntry[]? _taskSpecEntries;
    private void*                       _meshSpecDataPtr;
    private int                         _meshSpecDataSize;
    private VkSpecializationMapEntry[]? _meshSpecEntries;

    // Layout + cache.
    private VkPipelineLayout_T* _layout;
    private VkPipelineCache_T*  _cache;

    private bool _stagesSet;
    private bool _renderingSet;

    internal GraphicsPipelineBuilder(Device device)
    {
        _device         = device;
        _topology       = VkPrimitiveTopology.VK_PRIMITIVE_TOPOLOGY_TRIANGLE_LIST;
        _cullMode       = VkCullModeFlagBits.VK_CULL_MODE_NONE;
        _frontFace      = VkFrontFace.VK_FRONT_FACE_COUNTER_CLOCKWISE;
        _polygonMode    = VkPolygonMode.VK_POLYGON_MODE_FILL;
        _samples        = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT;
        _minSampleShading = 1.0f;
        _depthCompareOp = VkCompareOp.VK_COMPARE_OP_LESS;
        // Default entry points = "main\0"
        InitMain(ref _vertEntry);
        InitMain(ref _fragEntry);
        InitMain(ref _geomEntry);
        InitMain(ref _tessControlEntry);
        InitMain(ref _tessEvalEntry);
        InitMain(ref _taskEntry);
        InitMain(ref _meshEntry);
    }

    private static void InitMain(ref EntryPointBuffer buf)
    {
        buf[0] = (byte)'m';
        buf[1] = (byte)'a';
        buf[2] = (byte)'i';
        buf[3] = (byte)'n';
        buf[4] = 0;
    }

    public GraphicsPipelineBuilder WithStages(in ShaderModule vertex, in ShaderModule fragment)
    {
        // Rejected here, not at Build(): a null module reaches the driver as
        // VUID-VkPipelineShaderStageCreateInfo-module-parameter, a message
        // that names neither the stage nor the builder call that supplied it.
        if (vertex.IsNull)
            throw new ArgumentException("Vertex ShaderModule is null (default).", nameof(vertex));
        if (fragment.IsNull)
            throw new ArgumentException("Fragment ShaderModule is null (default).", nameof(fragment));

        _vert = vertex.Handle;
        _frag = fragment.Handle;
        _stagesSet = true;
        return this;
    }

    /// <summary>
    /// Adds a geometry-shader stage. Pipeline must already have vert + frag
    /// via <see cref="WithStages"/>; the device must support geometry
    /// shaders (<c>geometryShader</c> feature, enabled at device-create
    /// time).
    /// </summary>
    public GraphicsPipelineBuilder WithGeometryStage(in ShaderModule geometry)
    {
        _geom = geometry.Handle;
        return this;
    }

    /// <summary>
    /// Adds the tessellation control + evaluation shader stages. Both stages
    /// must be present together — Vulkan rejects a tess pipeline with only
    /// one. Caller still has to call <see cref="WithTessellation"/> to set
    /// the patch control-point count.
    /// </summary>
    public GraphicsPipelineBuilder WithTessellationStages(in ShaderModule control, in ShaderModule evaluation)
    {
        _tessControl = control.Handle;
        _tessEval    = evaluation.Handle;
        return this;
    }

    /// <summary>
    /// Selects the mesh-shading path: a mesh stage plus fragment, replacing the
    /// vertex / tessellation / geometry front end. Mutually exclusive with
    /// <see cref="WithStages"/>, <see cref="WithGeometryStage"/> and
    /// <see cref="WithTessellationStages"/> — Vulkan requires every geometric
    /// stage in a pipeline to come from one family or the other
    /// (VUID-VkGraphicsPipelineCreateInfo-pStages-02095). Requires
    /// VK_EXT_mesh_shader and the meshShader feature.
    /// </summary>
    /// <remarks>
    /// <see cref="Build"/> rejects a mesh stage on a device where
    /// <c>VK_EXT_mesh_shader</c> was never enabled, so the misconfiguration
    /// surfaces at the call site rather than as a driver/validation error.
    /// That guard is <b>partial</b>: it can only see whether the
    /// <i>extension</i> was enabled (the mesh entry points resolved), not
    /// whether the <c>meshShader</c> feature was — Vulkan exposes no query
    /// for the enabled feature chain after <c>vkCreateDevice</c>. A device
    /// with the extension but without the feature builds past this wrapper
    /// and is caught by the driver
    /// (VUID-VkPipelineShaderStageCreateInfo-stage-02091).
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Either module is a <c>default</c> <see cref="ShaderModule"/>. A null
    /// mesh module would leave <see cref="Build"/> on the classic path with a
    /// null vertex stage, so it is rejected at the call site.
    /// </exception>
    public GraphicsPipelineBuilder WithMeshStages(in ShaderModule mesh, in ShaderModule fragment)
    {
        // A null mesh module is worse than a null vertex one: Build() selects
        // the mesh path on `_mesh != null`, so a default ShaderModule here
        // sets _stagesSet without selecting it. Every mesh guard would then be
        // skipped and Build() would emit a VERTEX stage with a null module —
        // a rejection (VUID-VkPipelineShaderStageCreateInfo-module-parameter)
        // that never mentions mesh at all.
        if (mesh.IsNull)
            throw new ArgumentException("Mesh ShaderModule is null (default).", nameof(mesh));
        if (fragment.IsNull)
            throw new ArgumentException("Fragment ShaderModule is null (default).", nameof(fragment));

        _mesh      = mesh.Handle;
        _frag      = fragment.Handle;
        _stagesSet = true;
        return this;
    }

    /// <summary>
    /// Adds a task (amplification) stage ahead of the mesh stage. Requires
    /// <see cref="WithMeshStages"/> — a task-only pipeline is invalid
    /// (VUID-VkGraphicsPipelineCreateInfo-stage-02096) — plus the taskShader
    /// feature.
    /// </summary>
    /// <remarks>
    /// <c>taskShader</c> is independently optional: a device may advertise
    /// <c>VK_EXT_mesh_shader</c> and <c>meshShader</c> without it.
    /// <see cref="Build"/>'s extension check cannot tell the difference (see
    /// <see cref="WithMeshStages"/>), so a task stage on a device without the
    /// feature is caught by the driver
    /// (VUID-VkPipelineShaderStageCreateInfo-stage-02092), not here.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="task"/> is a <c>default</c> <see cref="ShaderModule"/>.
    /// </exception>
    public GraphicsPipelineBuilder WithTaskStage(in ShaderModule task)
    {
        // Symmetrical with WithMeshStages: a null handle here would leave
        // _task null, silently dropping the stage the caller asked for
        // instead of emitting it.
        if (task.IsNull)
            throw new ArgumentException("Task ShaderModule is null (default).", nameof(task));

        _task = task.Handle;
        return this;
    }

    public GraphicsPipelineBuilder WithVertexEntryPoint(ReadOnlySpan<byte> name) { CopyName(name, ref _vertEntry); return this; }
    public GraphicsPipelineBuilder WithFragmentEntryPoint(ReadOnlySpan<byte> name) { CopyName(name, ref _fragEntry); return this; }
    public GraphicsPipelineBuilder WithGeometryEntryPoint(ReadOnlySpan<byte> name) { CopyName(name, ref _geomEntry); return this; }
    public GraphicsPipelineBuilder WithTessellationControlEntryPoint(ReadOnlySpan<byte> name) { CopyName(name, ref _tessControlEntry); return this; }
    public GraphicsPipelineBuilder WithTessellationEvaluationEntryPoint(ReadOnlySpan<byte> name) { CopyName(name, ref _tessEvalEntry); return this; }
    public GraphicsPipelineBuilder WithMeshEntryPoint(ReadOnlySpan<byte> name) { CopyName(name, ref _meshEntry); return this; }
    public GraphicsPipelineBuilder WithTaskEntryPoint(ReadOnlySpan<byte> name) { CopyName(name, ref _taskEntry); return this; }

    /// <summary>
    /// Specializes the vertex shader's <c>constant_id</c> values from the
    /// fields of <typeparamref name="T"/>. See
    /// <see cref="SpecializationInfo{T}"/> for the field-layout rules and
    /// the caller's lifetime obligations.
    /// </summary>
    public GraphicsPipelineBuilder WithVertexSpecialization<T>(SpecializationInfo<T> spec) where T : unmanaged
    { _vertSpecDataPtr = spec.DataPtr; _vertSpecDataSize = spec.DataSize; _vertSpecEntries = spec.Entries; return this; }

    /// <summary>Specializes the fragment shader's <c>constant_id</c> values.</summary>
    public GraphicsPipelineBuilder WithFragmentSpecialization<T>(SpecializationInfo<T> spec) where T : unmanaged
    { _fragSpecDataPtr = spec.DataPtr; _fragSpecDataSize = spec.DataSize; _fragSpecEntries = spec.Entries; return this; }

    /// <summary>Specializes the geometry shader's <c>constant_id</c> values.</summary>
    public GraphicsPipelineBuilder WithGeometrySpecialization<T>(SpecializationInfo<T> spec) where T : unmanaged
    { _geomSpecDataPtr = spec.DataPtr; _geomSpecDataSize = spec.DataSize; _geomSpecEntries = spec.Entries; return this; }

    /// <summary>Specializes the tessellation control shader's <c>constant_id</c> values.</summary>
    public GraphicsPipelineBuilder WithTessellationControlSpecialization<T>(SpecializationInfo<T> spec) where T : unmanaged
    { _tessControlSpecDataPtr = spec.DataPtr; _tessControlSpecDataSize = spec.DataSize; _tessControlSpecEntries = spec.Entries; return this; }

    /// <summary>Specializes the tessellation evaluation shader's <c>constant_id</c> values.</summary>
    public GraphicsPipelineBuilder WithTessellationEvaluationSpecialization<T>(SpecializationInfo<T> spec) where T : unmanaged
    { _tessEvalSpecDataPtr = spec.DataPtr; _tessEvalSpecDataSize = spec.DataSize; _tessEvalSpecEntries = spec.Entries; return this; }

    /// <summary>Specializes the mesh shader's <c>constant_id</c> values.</summary>
    public GraphicsPipelineBuilder WithMeshSpecialization<T>(SpecializationInfo<T> spec) where T : unmanaged
    { _meshSpecDataPtr = spec.DataPtr; _meshSpecDataSize = spec.DataSize; _meshSpecEntries = spec.Entries; return this; }

    /// <summary>Specializes the task shader's <c>constant_id</c> values.</summary>
    public GraphicsPipelineBuilder WithTaskSpecialization<T>(SpecializationInfo<T> spec) where T : unmanaged
    { _taskSpecDataPtr = spec.DataPtr; _taskSpecDataSize = spec.DataSize; _taskSpecEntries = spec.Entries; return this; }

    private static void CopyName(ReadOnlySpan<byte> name, ref EntryPointBuffer dst)
    {
        if (name.Length > 31)
            throw new ArgumentException("Entry-point name exceeds 31 bytes (wrapper ceiling).", nameof(name));
        Span<byte> view = MemoryMarshal.CreateSpan(ref dst[0], 32);
        view.Clear();
        name.CopyTo(view);
    }

    public GraphicsPipelineBuilder WithVertexInput(in VertexInputDescription desc)
    {
        _vBindings = desc.Bindings;
        _vAttrs    = desc.Attributes;
        return this;
    }

    public GraphicsPipelineBuilder WithTopology(VkPrimitiveTopology topology)
    {
        _topology    = topology;
        _topologySet = true;
        return this;
    }

    /// <summary>
    /// Sets the patch control-point count for tessellation pipelines. Must
    /// be paired with <see cref="WithTessellationStages"/> and a
    /// <c>VK_PRIMITIVE_TOPOLOGY_PATCH_LIST</c> topology.
    /// </summary>
    public GraphicsPipelineBuilder WithTessellation(uint patchControlPoints)
    {
        _patchControlPoints = patchControlPoints;
        return this;
    }

    public GraphicsPipelineBuilder WithRasterization(
        VkCullModeFlagBits cullMode  = VkCullModeFlagBits.VK_CULL_MODE_NONE,
        VkFrontFace        frontFace = VkFrontFace.VK_FRONT_FACE_COUNTER_CLOCKWISE,
        VkPolygonMode      polygonMode = VkPolygonMode.VK_POLYGON_MODE_FILL)
    {
        _cullMode    = cullMode;
        _frontFace   = frontFace;
        _polygonMode = polygonMode;
        return this;
    }

    /// <summary>
    /// Enables depth bias on the rasterizer. <paramref name="constantFactor"/>
    /// maps to <c>depthBiasConstantFactor</c>, <paramref name="slopeFactor"/>
    /// to <c>depthBiasSlopeFactor</c>, and <paramref name="clamp"/> to
    /// <c>depthBiasClamp</c>. Default-unset behaviour leaves
    /// <c>depthBiasEnable = false</c> on every pipeline that doesn't call
    /// this — existing pipelines see no change.
    /// </summary>
    /// <remarks>
    /// Typical engine uses: positive (slope, constant) on cascaded shadow
    /// casters to bias caster geometry away from the receiver and dodge
    /// self-shadow acne; a small negative <paramref name="constantFactor"/>
    /// on a main pass that follows a Z-prepass with a <c>LessOrEqual</c>
    /// depth function, to keep the prepass-vs-main equality stable at
    /// silhouettes.
    /// </remarks>
    public GraphicsPipelineBuilder WithDepthBias(float constantFactor, float slopeFactor, float clamp = 0f)
    {
        _depthBiasEnable         = true;
        _depthBiasConstantFactor = constantFactor;
        _depthBiasSlopeFactor    = slopeFactor;
        _depthBiasClamp          = clamp;
        return this;
    }

    /// <summary>
    /// Multisample state. <paramref name="sampleShadingEnable"/> + a
    /// <paramref name="minSampleShading"/> below 1.0 reduces the per-fragment
    /// shading rate; the default (false / 1.0) means standard MSAA with
    /// one shader invocation per pixel.
    /// </summary>
    public GraphicsPipelineBuilder WithMultisample(
        VkSampleCountFlagBits samples,
        bool                  sampleShadingEnable = false,
        float                 minSampleShading    = 1.0f)
    {
        _samples             = samples;
        _sampleShadingEnable = sampleShadingEnable;
        _minSampleShading    = minSampleShading;
        return this;
    }

    /// <summary>
    /// Per-attachment color blend state. <paramref name="description"/>'s
    /// <c>Attachments</c> span is consumed synchronously inside
    /// <see cref="Build"/>; callers can use the
    /// <see cref="ColorBlendAttachment.AlphaBlend"/> /
    /// <see cref="ColorBlendAttachment.Additive"/> presets or build the
    /// state by hand.
    /// </summary>
    public GraphicsPipelineBuilder WithColorBlend(in ColorBlendDescription description)
    {
        _blendAttachments    = description.Attachments;
        _blendLogicOpEnable  = description.LogicOpEnable;
        _blendLogicOp        = description.LogicOp;
        _blendConstants      = description.BlendConstants;
        return this;
    }

    /// <summary>
    /// Override the dynamic-state list (default is viewport + scissor).
    /// Add the new states; the wrapper does not auto-include
    /// <c>VIEWPORT</c> / <c>SCISSOR</c> when overridden, so include them
    /// explicitly if you still want them dynamic.
    /// </summary>
    public GraphicsPipelineBuilder WithDynamicState(ReadOnlySpan<VkDynamicState> dynamicStates)
    {
        _dynamicStates = dynamicStates;
        return this;
    }

    public GraphicsPipelineBuilder WithDepthStencil(
        bool        testEnable,
        bool        writeEnable,
        VkCompareOp compareOp = VkCompareOp.VK_COMPARE_OP_LESS)
    {
        _depthTestEnable  = testEnable;
        _depthWriteEnable = writeEnable;
        _depthCompareOp   = compareOp;
        return this;
    }

    public GraphicsPipelineBuilder WithDynamicRendering(
        ReadOnlySpan<VkFormat> colorFormats,
        VkFormat               depthFormat   = VkFormat.VK_FORMAT_UNDEFINED,
        VkFormat               stencilFormat = VkFormat.VK_FORMAT_UNDEFINED)
    {
        _colorFormats   = colorFormats;
        _depthFormat    = depthFormat;
        _stencilFormat  = stencilFormat;
        _renderingSet   = true;
        return this;
    }

    public GraphicsPipelineBuilder WithLayout(in PipelineLayout layout)
    {
        _layout = layout.Handle;
        return this;
    }

    public GraphicsPipelineBuilder WithCache(VkPipelineCache_T* cache)
    {
        _cache = cache;
        return this;
    }

    /// <summary>
    /// Wires a wrapper-managed <see cref="PipelineCache"/> into the build.
    /// The driver merges newly-compiled pipelines into the cache as they
    /// finish; persist via <see cref="PipelineCache.Save"/> on shutdown
    /// for fast restarts.
    /// </summary>
    public GraphicsPipelineBuilder WithCache(in PipelineCache cache)
    {
        _cache = cache.Handle;
        return this;
    }

    /// <summary>
    /// Issues <c>vkCreateGraphicsPipelines</c>. Builder fields, including
    /// the inline entry-point buffers, are <c>fixed</c>'d for the duration
    /// of the native call so the <c>const char*</c> pointers stay valid.
    /// </summary>
    public GraphicsPipeline Build()
    {
        if (!_stagesSet)    throw new InvalidOperationException("GraphicsPipelineBuilder requires WithStages or WithMeshStages.");
        if (!_renderingSet) throw new InvalidOperationException("GraphicsPipelineBuilder requires WithDynamicRendering.");
        if (_layout == null) throw new InvalidOperationException("GraphicsPipelineBuilder requires WithLayout.");

        // ---- Mesh-shading path ----
        // A mesh pipeline replaces the primitive-shading front end wholesale.
        // Everything below either prevents a driver-rejected pipeline (stage
        // family mixing, forbidden dynamic states) or converts state the mesh
        // path would silently discard (vertex input, topology, patch size)
        // into an error at the call site.
        //
        // The stage-FAMILY guards run ahead of the tessellation guards below,
        // not after them: a builder carrying both WithMeshStages and
        // WithTessellationStages is a family mix whichever order it is
        // inspected in, and letting the tess guards win first would answer it
        // with "add WithTessellation(patchControlPoints > 0)" — advice that
        // makes the caller add a call which then throws a *different* error.
        bool meshPath = _mesh != null;
        if (_task != null && !meshPath)
            throw new InvalidOperationException(
                "WithTaskStage requires WithMeshStages — a task shader amplifies a mesh shader and a " +
                "task-only pipeline has no pre-rasterization stage " +
                "(VUID-VkGraphicsPipelineCreateInfo-stage-02096).");
        if (meshPath && _vert != null)
            throw new InvalidOperationException(
                "WithStages (vertex) and WithMeshStages are mutually exclusive; a pipeline's geometric " +
                "stages must all come from the primitive-shading family or all from the mesh-shading " +
                "family (VUID-VkGraphicsPipelineCreateInfo-pStages-02095). Pick one.");
        if (meshPath && _geom != null)
            throw new InvalidOperationException(
                "WithGeometryStage and WithMeshStages are mutually exclusive; a geometry shader is a " +
                "primitive-shading-family stage (VUID-VkGraphicsPipelineCreateInfo-pStages-02095). " +
                "Drop WithGeometryStage.");
        if (meshPath && (_tessControl != null || _tessEval != null))
            throw new InvalidOperationException(
                "WithTessellationStages and WithMeshStages are mutually exclusive; tessellation control " +
                "and evaluation are primitive-shading-family stages " +
                "(VUID-VkGraphicsPipelineCreateInfo-pStages-02095). Drop WithTessellationStages.");

        if ((_tessControl == null) != (_tessEval == null))
            throw new InvalidOperationException("Tessellation requires both control + evaluation stages (WithTessellationStages).");
        // WithTessellationStages without WithTessellation(patchControlPoints)
        // would have left pTessellationState as null below (the gate was
        // _patchControlPoints > 0), feeding the driver tess shaders with
        // no patch-size — Vulkan rejects, but the wrapper-side error is
        // clearer.
        if (_tessControl != null && _patchControlPoints == 0)
            throw new InvalidOperationException(
                "Tessellation pipeline requires WithTessellation(patchControlPoints > 0); pair it with WithTessellationStages.");

        if (meshPath && (!_vBindings.IsEmpty || !_vAttrs.IsEmpty))
            throw new InvalidOperationException(
                "WithVertexInput has no effect on a mesh pipeline — a mesh shader has no vertex-input " +
                "stage and reads its data through descriptors or buffer device addresses. " +
                "Drop WithVertexInput.");
        if (meshPath && _topologySet)
            throw new InvalidOperationException(
                "WithTopology has no effect on a mesh pipeline — the mesh shader emits primitives " +
                "directly, so there is no input-assembly stage to configure. Drop WithTopology.");
        if (meshPath && _patchControlPoints != 0)
            throw new InvalidOperationException(
                "WithTessellation has no effect on a mesh pipeline — the mesh shader emits primitives " +
                "directly and there are no patches to subdivide. Drop WithTessellation.");
        if (meshPath)
        {
            // Only an explicit WithDynamicState override can trip this; the
            // builder's viewport + scissor default contains none of the
            // forbidden states, so scanning _dynamicStates is sufficient.
            for (int i = 0; i < _dynamicStates.Length; i++)
            {
                string? vuid = MeshForbiddenDynamicStateVuid(_dynamicStates[i]);
                if (vuid != null)
                    throw new InvalidOperationException(
                        $"{_dynamicStates[i]} is not allowed on a mesh pipeline — the mesh shader has no " +
                        "vertex-input or input-assembly stage for the state to apply to " +
                        $"(VUID-VkGraphicsPipelineCreateInfo-{vuid}). Drop it from WithDynamicState.");
            }

            // Extension-enabled check, deliberately PARTIAL — see
            // MeshShaderSupport.PartialGuardNote. A non-null CmdDrawMeshTasks
            // is the recorder's own oracle for "VK_EXT_mesh_shader was in the
            // list this wrapper passed to vkCreateDevice", because
            // DeviceFunctionTable resolves the mesh entry points only when it
            // was. Without this guard a mesh stage on a plain device reaches
            // vkCreateGraphicsPipelines and surfaces as
            // VUID-VkPipelineShaderStageCreateInfo-stage-02091, while
            // CommandRecorder.DrawMeshTasks' friendly message — which names
            // the same extension for the same misconfiguration — is
            // unreachable, because the builder runs first.
            //
            // Ordered LAST among the mesh guards on purpose: everything above
            // is static misuse of the builder, true regardless of which device
            // the caller happens to hold, and stays the more actionable
            // message when both are wrong at once.
            if (_device.Functions.CmdDrawMeshTasks == null)
                throw new InvalidOperationException(
                    "WithMeshStages / WithTaskStage require a Device created with VK_EXT_mesh_shader " +
                    "enabled; vkCmdDrawMeshTasksEXT did not resolve on this device, so the mesh stage " +
                    "would be rejected by the driver " +
                    "(VUID-VkPipelineShaderStageCreateInfo-stage-02091). " +
                    MeshShaderSupport.EnableInstructions + " " +
                    MeshShaderSupport.PartialGuardNote);
        }
        // WithColorBlend(...) is optional — when omitted every color
        // attachment defaults to opaque. When provided, the attachment
        // count must match the rendering color-format count exactly:
        // the previous fall-back-to-opaque-on-mismatch loop silently
        // dropped tail entries (caller asks for 4 blends with 2 formats
        // → only 2 written, no warning) AND quietly produced opaque
        // tails (1 format with 0 blends after WithColorBlend([]) → 1
        // opaque written, but the caller's intent to disable blending
        // entirely was discarded). Reject the mismatch here so the
        // builder fails loud at Build instead of producing a pipeline
        // whose blend state doesn't match what the caller asked for.
        if (!_blendAttachments.IsEmpty && _blendAttachments.Length != _colorFormats.Length)
            throw new InvalidOperationException(
                $"WithColorBlend supplied {_blendAttachments.Length} attachment(s) but WithDynamicRendering declared {_colorFormats.Length} color format(s) — counts must match. " +
                "Pass one ColorBlendAttachment per color format, or omit WithColorBlend to default every attachment to opaque.");

        // ---- Vertex input native arrays ----
        Span<VkVertexInputBindingDescription>   nativeBindings = stackalloc VkVertexInputBindingDescription[Math.Max(_vBindings.Length, 1)];
        Span<VkVertexInputAttributeDescription> nativeAttrs    = stackalloc VkVertexInputAttributeDescription[Math.Max(_vAttrs.Length, 1)];
        for (int i = 0; i < _vBindings.Length; i++)
        {
            nativeBindings[i] = new VkVertexInputBindingDescription
            {
                binding   = _vBindings[i].Slot,
                stride    = _vBindings[i].Stride,
                inputRate = _vBindings[i].InputRate,
            };
        }
        for (int i = 0; i < _vAttrs.Length; i++)
        {
            nativeAttrs[i] = new VkVertexInputAttributeDescription
            {
                location = _vAttrs[i].Location,
                binding  = _vAttrs[i].Binding,
                format   = _vAttrs[i].Format,
                offset   = _vAttrs[i].Offset,
            };
        }

        // ---- Color blend attachments. User-provided overrides take
        // precedence; any unspecified tail entries fall back to opaque so
        // the attachment count always matches color-attachment count.
        int blendCount = _colorFormats.Length;
        Span<VkPipelineColorBlendAttachmentState> blendAttachments =
            stackalloc VkPipelineColorBlendAttachmentState[Math.Max(blendCount, 1)];
        for (int i = 0; i < blendCount; i++)
        {
            ColorBlendAttachment ca = i < _blendAttachments.Length
                ? _blendAttachments[i]
                : ColorBlendAttachment.Opaque;
            blendAttachments[i] = ca.ToNative();
        }

        // ---- Dynamic state. Default = viewport + scissor; any explicit
        // override (WithDynamicState) replaces the default wholesale.
        Span<VkDynamicState> defaultDynamic = stackalloc VkDynamicState[2]
        {
            VkDynamicState.VK_DYNAMIC_STATE_VIEWPORT,
            VkDynamicState.VK_DYNAMIC_STATE_SCISSOR,
        };
        ReadOnlySpan<VkDynamicState> dynamicStates = _dynamicStates.IsEmpty ? defaultDynamic : _dynamicStates;

        VkPipeline_T* raw = null;
        bool hasVertSpec        = _vertSpecEntries        is { Length: > 0 };
        bool hasFragSpec        = _fragSpecEntries        is { Length: > 0 };
        bool hasGeomSpec        = _geomSpecEntries        is { Length: > 0 };
        bool hasTessControlSpec = _tessControlSpecEntries is { Length: > 0 };
        bool hasTessEvalSpec    = _tessEvalSpecEntries    is { Length: > 0 };
        bool hasTaskSpec        = _taskSpecEntries        is { Length: > 0 };
        bool hasMeshSpec        = _meshSpecEntries        is { Length: > 0 };
        fixed (byte* pVertEntry        = &_vertEntry[0])
        fixed (byte* pFragEntry        = &_fragEntry[0])
        fixed (byte* pGeomEntry        = &_geomEntry[0])
        fixed (byte* pTessControlEntry = &_tessControlEntry[0])
        fixed (byte* pTessEvalEntry    = &_tessEvalEntry[0])
        fixed (byte* pTaskEntry        = &_taskEntry[0])
        fixed (byte* pMeshEntry        = &_meshEntry[0])
        fixed (VkSpecializationMapEntry* pVertSpecEntries        = _vertSpecEntries)
        fixed (VkSpecializationMapEntry* pFragSpecEntries        = _fragSpecEntries)
        fixed (VkSpecializationMapEntry* pGeomSpecEntries        = _geomSpecEntries)
        fixed (VkSpecializationMapEntry* pTessControlSpecEntries = _tessControlSpecEntries)
        fixed (VkSpecializationMapEntry* pTessEvalSpecEntries    = _tessEvalSpecEntries)
        fixed (VkSpecializationMapEntry* pTaskSpecEntries        = _taskSpecEntries)
        fixed (VkSpecializationMapEntry* pMeshSpecEntries        = _meshSpecEntries)
        fixed (VkVertexInputBindingDescription*    pBindings = nativeBindings)
        fixed (VkVertexInputAttributeDescription*  pAttrs    = nativeAttrs)
        fixed (VkFormat*                           pColors   = _colorFormats)
        fixed (VkPipelineColorBlendAttachmentState* pBlend   = blendAttachments)
        fixed (VkDynamicState*                     pDyn      = dynamicStates)
        {
            // Per-stage VkSpecializationInfo storage. Each stage's slot
            // is initialized regardless; pSpecializationInfo on the stage
            // create-info points at the slot only when the stage actually
            // has entries.
            VkSpecializationInfo vertSpec        = SpecInfo(_vertSpecEntries,        pVertSpecEntries,        _vertSpecDataSize,        _vertSpecDataPtr);
            VkSpecializationInfo fragSpec        = SpecInfo(_fragSpecEntries,        pFragSpecEntries,        _fragSpecDataSize,        _fragSpecDataPtr);
            VkSpecializationInfo geomSpec        = SpecInfo(_geomSpecEntries,        pGeomSpecEntries,        _geomSpecDataSize,        _geomSpecDataPtr);
            VkSpecializationInfo tessControlSpec = SpecInfo(_tessControlSpecEntries, pTessControlSpecEntries, _tessControlSpecDataSize, _tessControlSpecDataPtr);
            VkSpecializationInfo tessEvalSpec    = SpecInfo(_tessEvalSpecEntries,    pTessEvalSpecEntries,    _tessEvalSpecDataSize,    _tessEvalSpecDataPtr);
            VkSpecializationInfo taskSpec        = SpecInfo(_taskSpecEntries,        pTaskSpecEntries,        _taskSpecDataSize,        _taskSpecDataPtr);
            VkSpecializationInfo meshSpec        = SpecInfo(_meshSpecEntries,        pMeshSpecEntries,        _meshSpecDataSize,        _meshSpecDataPtr);

            // MaxStages stays 5. The classic path's maximum is 5 (vert +
            // frag + geom + tessC + tessE); the mesh path's is 3 (task +
            // mesh + frag); the two are mutually exclusive (rejected in the
            // preamble above), so the ceiling is the larger of the two.
            var stages = stackalloc VkPipelineShaderStageCreateInfo[MaxStages];
            uint stageCount = 0;
            if (meshPath)
            {
                // Order within pStages is not significant; task -> mesh ->
                // fragment reads in pipeline order.
                if (_task != null)
                    stages[stageCount++] = ShaderStage(VkShaderStageFlagBits.VK_SHADER_STAGE_TASK_BIT_EXT, _task, pTaskEntry, hasTaskSpec ? &taskSpec : null);
                stages[stageCount++] = ShaderStage(VkShaderStageFlagBits.VK_SHADER_STAGE_MESH_BIT_EXT,     _mesh, pMeshEntry, hasMeshSpec ? &meshSpec : null);
                stages[stageCount++] = ShaderStage(VkShaderStageFlagBits.VK_SHADER_STAGE_FRAGMENT_BIT,     _frag, pFragEntry, hasFragSpec ? &fragSpec : null);
            }
            else
            {
                stages[stageCount++] = ShaderStage(VkShaderStageFlagBits.VK_SHADER_STAGE_VERTEX_BIT,   _vert, pVertEntry, hasVertSpec ? &vertSpec : null);
                stages[stageCount++] = ShaderStage(VkShaderStageFlagBits.VK_SHADER_STAGE_FRAGMENT_BIT, _frag, pFragEntry, hasFragSpec ? &fragSpec : null);
                if (_geom != null)
                    stages[stageCount++] = ShaderStage(VkShaderStageFlagBits.VK_SHADER_STAGE_GEOMETRY_BIT, _geom, pGeomEntry, hasGeomSpec ? &geomSpec : null);
                if (_tessControl != null)
                {
                    stages[stageCount++] = ShaderStage(VkShaderStageFlagBits.VK_SHADER_STAGE_TESSELLATION_CONTROL_BIT,    _tessControl, pTessControlEntry, hasTessControlSpec ? &tessControlSpec : null);
                    stages[stageCount++] = ShaderStage(VkShaderStageFlagBits.VK_SHADER_STAGE_TESSELLATION_EVALUATION_BIT, _tessEval,    pTessEvalEntry,    hasTessEvalSpec    ? &tessEvalSpec    : null);
                }
            }

            var vertexInput = new VkPipelineVertexInputStateCreateInfo
            {
                sType                           = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_VERTEX_INPUT_STATE_CREATE_INFO,
                vertexBindingDescriptionCount   = (uint)_vBindings.Length,
                pVertexBindingDescriptions      = _vBindings.Length > 0 ? pBindings : null,
                vertexAttributeDescriptionCount = (uint)_vAttrs.Length,
                pVertexAttributeDescriptions    = _vAttrs.Length > 0 ? pAttrs : null,
            };

            var inputAssembly = new VkPipelineInputAssemblyStateCreateInfo
            {
                sType    = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_INPUT_ASSEMBLY_STATE_CREATE_INFO,
                topology = _topology,
            };

            var tessellation = new VkPipelineTessellationStateCreateInfo
            {
                sType              = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_TESSELLATION_STATE_CREATE_INFO,
                patchControlPoints = _patchControlPoints,
            };

            // Viewport state — counts only, real values come from dynamic state.
            var viewportState = new VkPipelineViewportStateCreateInfo
            {
                sType         = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_VIEWPORT_STATE_CREATE_INFO,
                viewportCount = 1,
                scissorCount  = 1,
            };

            var rasterization = new VkPipelineRasterizationStateCreateInfo
            {
                sType                   = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_RASTERIZATION_STATE_CREATE_INFO,
                depthClampEnable        = 0,
                rasterizerDiscardEnable = 0,
                polygonMode             = _polygonMode,
                cullMode                = (uint)_cullMode,
                frontFace               = _frontFace,
                depthBiasEnable         = _depthBiasEnable ? 1u : 0u,
                depthBiasConstantFactor = _depthBiasConstantFactor,
                depthBiasClamp          = _depthBiasClamp,
                depthBiasSlopeFactor    = _depthBiasSlopeFactor,
                lineWidth               = 1.0f,
            };

            var multisample = new VkPipelineMultisampleStateCreateInfo
            {
                sType                 = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_MULTISAMPLE_STATE_CREATE_INFO,
                rasterizationSamples  = _samples,
                sampleShadingEnable   = _sampleShadingEnable ? 1u : 0u,
                minSampleShading      = _minSampleShading,
            };

            var depthStencil = new VkPipelineDepthStencilStateCreateInfo
            {
                sType                 = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_DEPTH_STENCIL_STATE_CREATE_INFO,
                depthTestEnable       = _depthTestEnable  ? 1u : 0u,
                depthWriteEnable      = _depthWriteEnable ? 1u : 0u,
                depthCompareOp        = _depthCompareOp,
                depthBoundsTestEnable = 0,
                stencilTestEnable     = 0,
            };

            var colorBlend = new VkPipelineColorBlendStateCreateInfo
            {
                sType           = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_COLOR_BLEND_STATE_CREATE_INFO,
                logicOpEnable   = _blendLogicOpEnable ? 1u : 0u,
                logicOp         = _blendLogicOp,
                attachmentCount = (uint)blendCount,
                pAttachments    = blendCount > 0 ? pBlend : null,
            };
            colorBlend.blendConstants[0] = _blendConstants.R;
            colorBlend.blendConstants[1] = _blendConstants.G;
            colorBlend.blendConstants[2] = _blendConstants.B;
            colorBlend.blendConstants[3] = _blendConstants.A;

            var dynamicState = new VkPipelineDynamicStateCreateInfo
            {
                sType             = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_DYNAMIC_STATE_CREATE_INFO,
                dynamicStateCount = (uint)dynamicStates.Length,
                pDynamicStates    = pDyn,
            };

            // viewMask stays 0 (never set by the builder), which is what
            // keeps VUID-VkGraphicsPipelineCreateInfo-renderPass-07720 —
            // mesh shader + non-zero viewMask requires the
            // multiviewMeshShader feature — structurally unreachable.
            var renderingInfo = new VkPipelineRenderingCreateInfo
            {
                sType                   = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_RENDERING_CREATE_INFO,
                colorAttachmentCount    = (uint)_colorFormats.Length,
                pColorAttachmentFormats = pColors,
                depthAttachmentFormat   = _depthFormat,
                stencilAttachmentFormat = _stencilFormat,
            };

            var ci = new VkGraphicsPipelineCreateInfo
            {
                sType               = VkStructureType.VK_STRUCTURE_TYPE_GRAPHICS_PIPELINE_CREATE_INFO,
                pNext               = &renderingInfo,
                stageCount          = stageCount,
                pStages             = stages,
                // A mesh pipeline has neither stage; both members are
                // optional + noautovalidity, and nulling them is the only way
                // to make "no vertex input" true in the struct the driver
                // sees. The guards above already rejected WithVertexInput /
                // WithTopology on this path, so nothing is being discarded.
                pVertexInputState   = meshPath ? null : &vertexInput,
                pInputAssemblyState = meshPath ? null : &inputAssembly,
                // No meshPath branch needed: the pStages-02095 guard above
                // guarantees _tessControl == null whenever meshPath is true.
                pTessellationState  = _tessControl != null ? &tessellation : null,
                pViewportState      = &viewportState,
                pRasterizationState = &rasterization,
                pMultisampleState   = &multisample,
                pDepthStencilState  = &depthStencil,
                pColorBlendState    = &colorBlend,
                pDynamicState       = &dynamicState,
                layout              = _layout,
                renderPass          = null,
                subpass             = 0,
                basePipelineIndex   = -1,
            };

            Vk.vkCreateGraphicsPipelines(_device.Handle, _cache, 1, &ci, null, &raw).ThrowIfFailed();
        }
        return new GraphicsPipeline(raw, _layout, _device.Handle);
    }

    /// <summary>
    /// Returns the VUID suffix a mesh pipeline would violate by declaring
    /// <paramref name="state"/> dynamic, or <see langword="null"/> when the
    /// state is legal on the mesh path. Cold: only called from
    /// <see cref="Build"/>'s setup-time guard scan.
    /// </summary>
    private static string? MeshForbiddenDynamicStateVuid(VkDynamicState state) => state switch
    {
        VkDynamicState.VK_DYNAMIC_STATE_PRIMITIVE_TOPOLOGY            => "pDynamicStates-07065",
        VkDynamicState.VK_DYNAMIC_STATE_VERTEX_INPUT_BINDING_STRIDE   => "pDynamicStates-07065",
        VkDynamicState.VK_DYNAMIC_STATE_PRIMITIVE_RESTART_ENABLE      => "pDynamicStates-07066",
        VkDynamicState.VK_DYNAMIC_STATE_PATCH_CONTROL_POINTS_EXT      => "pDynamicStates-07066",
        VkDynamicState.VK_DYNAMIC_STATE_VERTEX_INPUT_EXT              => "pDynamicStates-07067",
        _                                                             => null,
    };

    private static VkPipelineShaderStageCreateInfo ShaderStage(
        VkShaderStageFlagBits stage,
        VkShaderModule_T*     module,
        byte*                 entry,
        VkSpecializationInfo* spec)
        => new()
        {
            sType               = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO,
            stage               = stage,
            module              = module,
            pName               = (sbyte*)entry,
            pSpecializationInfo = spec,
        };

    private static VkSpecializationInfo SpecInfo(
        VkSpecializationMapEntry[]? entries,
        VkSpecializationMapEntry*   pEntries,
        int                         dataSize,
        void*                       dataPtr)
        => new()
        {
            mapEntryCount = (uint)(entries?.Length ?? 0),
            pMapEntries   = pEntries,
            dataSize      = (nuint)dataSize,
            pData         = dataPtr,
        };

    [InlineArray(32)]
    private struct EntryPointBuffer { internal byte e0; }
}
