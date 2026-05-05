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
/// </remarks>
public unsafe ref struct GraphicsPipelineBuilder
{
    private readonly Device _device;

    // Stages.
    private VkShaderModule_T* _vert;
    private VkShaderModule_T* _frag;
    private EntryPointBuffer  _vertEntry;
    private EntryPointBuffer  _fragEntry;

    // Vertex input.
    private ReadOnlySpan<VertexBindingDescription>   _vBindings;
    private ReadOnlySpan<VertexAttributeDescription> _vAttrs;

    // Input assembly.
    private VkPrimitiveTopology _topology;

    // Rasterization.
    private VkCullModeFlagBits _cullMode;
    private VkFrontFace        _frontFace;
    private VkPolygonMode      _polygonMode;

    // Depth stencil.
    private bool        _depthTestEnable;
    private bool        _depthWriteEnable;
    private VkCompareOp _depthCompareOp;

    // Dynamic rendering.
    private ReadOnlySpan<VkFormat> _colorFormats;
    private VkFormat               _depthFormat;
    private VkFormat               _stencilFormat;

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
        _depthCompareOp = VkCompareOp.VK_COMPARE_OP_LESS;
        // Default entry points = "main\0"
        InitMain(ref _vertEntry);
        InitMain(ref _fragEntry);
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

    public GraphicsPipelineBuilder WithVertexEntryPoint(ReadOnlySpan<byte> name) { CopyName(name, ref _vertEntry); return this; }
    public GraphicsPipelineBuilder WithFragmentEntryPoint(ReadOnlySpan<byte> name) { CopyName(name, ref _fragEntry); return this; }

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

        // ---- Color blend attachments (default opaque, sized to color formats) ----
        Span<VkPipelineColorBlendAttachmentState> blendAttachments =
            stackalloc VkPipelineColorBlendAttachmentState[Math.Max(_colorFormats.Length, 1)];
        for (int i = 0; i < _colorFormats.Length; i++)
        {
            blendAttachments[i] = new VkPipelineColorBlendAttachmentState
            {
                blendEnable = 0,
                colorWriteMask =
                    (uint)(VkColorComponentFlagBits.VK_COLOR_COMPONENT_R_BIT |
                           VkColorComponentFlagBits.VK_COLOR_COMPONENT_G_BIT |
                           VkColorComponentFlagBits.VK_COLOR_COMPONENT_B_BIT |
                           VkColorComponentFlagBits.VK_COLOR_COMPONENT_A_BIT),
            };
        }

        // ---- Dynamic state (viewport + scissor) ----
        Span<VkDynamicState> dynamicStates = stackalloc VkDynamicState[]
        {
            VkDynamicState.VK_DYNAMIC_STATE_VIEWPORT,
            VkDynamicState.VK_DYNAMIC_STATE_SCISSOR,
        };

        VkPipeline_T* raw = null;
        fixed (byte* pVertEntry = &_vertEntry[0])
        fixed (byte* pFragEntry = &_fragEntry[0])
        fixed (VkVertexInputBindingDescription*   pBindings = nativeBindings)
        fixed (VkVertexInputAttributeDescription* pAttrs    = nativeAttrs)
        fixed (VkFormat*                           pColors   = _colorFormats)
        fixed (VkPipelineColorBlendAttachmentState* pBlend  = blendAttachments)
        fixed (VkDynamicState*                     pDyn     = dynamicStates)
        {
            // Two stages. VkPipelineShaderStageCreateInfo size is fixed; build inline.
            var stages = stackalloc VkPipelineShaderStageCreateInfo[2];
            stages[0] = new VkPipelineShaderStageCreateInfo
            {
                sType  = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO,
                stage  = VkShaderStageFlagBits.VK_SHADER_STAGE_VERTEX_BIT,
                module = _vert,
                pName  = (sbyte*)pVertEntry,
            };
            stages[1] = new VkPipelineShaderStageCreateInfo
            {
                sType  = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO,
                stage  = VkShaderStageFlagBits.VK_SHADER_STAGE_FRAGMENT_BIT,
                module = _frag,
                pName  = (sbyte*)pFragEntry,
            };

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
                sType                = VkStructureType.VK_STRUCTURE_TYPE_PIPELINE_MULTISAMPLE_STATE_CREATE_INFO,
                rasterizationSamples = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
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
                logicOpEnable   = 0,
                attachmentCount = (uint)_colorFormats.Length,
                pAttachments    = _colorFormats.Length > 0 ? pBlend : null,
            };

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
                stageCount          = 2,
                pStages             = stages,
                pVertexInputState   = &vertexInput,
                pInputAssemblyState = &inputAssembly,
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

    [InlineArray(32)]
    private struct EntryPointBuffer { internal byte e0; }
}
