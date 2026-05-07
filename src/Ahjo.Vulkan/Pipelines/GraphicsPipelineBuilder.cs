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
    private readonly Device _device;

    // Stages. vert + frag are required; geom + tessControl + tessEval are optional.
    private VkShaderModule_T* _vert;
    private VkShaderModule_T* _frag;
    private VkShaderModule_T* _geom;
    private VkShaderModule_T* _tessControl;
    private VkShaderModule_T* _tessEval;
    private EntryPointBuffer  _vertEntry;
    private EntryPointBuffer  _fragEntry;
    private EntryPointBuffer  _geomEntry;
    private EntryPointBuffer  _tessControlEntry;
    private EntryPointBuffer  _tessEvalEntry;

    // Vertex input.
    private ReadOnlySpan<VertexBindingDescription>   _vBindings;
    private ReadOnlySpan<VertexAttributeDescription> _vAttrs;

    // Input assembly.
    private VkPrimitiveTopology _topology;

    // Tessellation.
    private uint _patchControlPoints;

    // Rasterization.
    private VkCullModeFlagBits _cullMode;
    private VkFrontFace        _frontFace;
    private VkPolygonMode      _polygonMode;

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

    public GraphicsPipelineBuilder WithVertexEntryPoint(ReadOnlySpan<byte> name) { CopyName(name, ref _vertEntry); return this; }
    public GraphicsPipelineBuilder WithFragmentEntryPoint(ReadOnlySpan<byte> name) { CopyName(name, ref _fragEntry); return this; }
    public GraphicsPipelineBuilder WithGeometryEntryPoint(ReadOnlySpan<byte> name) { CopyName(name, ref _geomEntry); return this; }
    public GraphicsPipelineBuilder WithTessellationControlEntryPoint(ReadOnlySpan<byte> name) { CopyName(name, ref _tessControlEntry); return this; }
    public GraphicsPipelineBuilder WithTessellationEvaluationEntryPoint(ReadOnlySpan<byte> name) { CopyName(name, ref _tessEvalEntry); return this; }

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
        _topology = topology;
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
    /// Issues <c>vkCreateGraphicsPipelines</c>. Builder fields, including
    /// the inline entry-point buffers, are <c>fixed</c>'d for the duration
    /// of the native call so the <c>const char*</c> pointers stay valid.
    /// </summary>
    public GraphicsPipeline Build()
    {
        if (!_stagesSet)    throw new InvalidOperationException("GraphicsPipelineBuilder requires WithStages.");
        if (!_renderingSet) throw new InvalidOperationException("GraphicsPipelineBuilder requires WithDynamicRendering.");
        if (_layout == null) throw new InvalidOperationException("GraphicsPipelineBuilder requires WithLayout.");
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
        fixed (byte* pVertEntry        = &_vertEntry[0])
        fixed (byte* pFragEntry        = &_fragEntry[0])
        fixed (byte* pGeomEntry        = &_geomEntry[0])
        fixed (byte* pTessControlEntry = &_tessControlEntry[0])
        fixed (byte* pTessEvalEntry    = &_tessEvalEntry[0])
        fixed (VkVertexInputBindingDescription*    pBindings = nativeBindings)
        fixed (VkVertexInputAttributeDescription*  pAttrs    = nativeAttrs)
        fixed (VkFormat*                           pColors   = _colorFormats)
        fixed (VkPipelineColorBlendAttachmentState* pBlend   = blendAttachments)
        fixed (VkDynamicState*                     pDyn      = dynamicStates)
        {
            // Up to five stages: vert, frag, optional geom, optional tess
            // control + tess eval. Built inline; size is bounded.
            var stages = stackalloc VkPipelineShaderStageCreateInfo[5];
            uint stageCount = 0;
            stages[stageCount++] = ShaderStage(VkShaderStageFlagBits.VK_SHADER_STAGE_VERTEX_BIT,   _vert, pVertEntry);
            stages[stageCount++] = ShaderStage(VkShaderStageFlagBits.VK_SHADER_STAGE_FRAGMENT_BIT, _frag, pFragEntry);
            if (_geom != null)
                stages[stageCount++] = ShaderStage(VkShaderStageFlagBits.VK_SHADER_STAGE_GEOMETRY_BIT, _geom, pGeomEntry);
            if (_tessControl != null)
            {
                stages[stageCount++] = ShaderStage(VkShaderStageFlagBits.VK_SHADER_STAGE_TESSELLATION_CONTROL_BIT,    _tessControl, pTessControlEntry);
                stages[stageCount++] = ShaderStage(VkShaderStageFlagBits.VK_SHADER_STAGE_TESSELLATION_EVALUATION_BIT, _tessEval,    pTessEvalEntry);
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
                pVertexInputState   = &vertexInput,
                pInputAssemblyState = &inputAssembly,
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

    private static VkPipelineShaderStageCreateInfo ShaderStage(
        VkShaderStageFlagBits stage,
        VkShaderModule_T*     module,
        byte*                 entry)
        => new()
        {
            sType  = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO,
            stage  = stage,
            module = module,
            pName  = (sbyte*)entry,
        };

    [InlineArray(32)]
    private struct EntryPointBuffer { internal byte e0; }
}
