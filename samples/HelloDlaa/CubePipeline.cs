using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Slang;

namespace Ahjo.Vulkan.Samples.HelloDlaa;

/// <summary>
/// The shader half: compile <c>cube.slang</c>'s two entry points with Slang at
/// run time, and build the two-colour-attachment graphics pipeline they need.
/// </summary>
/// <remarks>
/// <para>Same shape as <c>samples/HelloRayQuery/RayQueryPipeline.cs</c>: an
/// <see cref="IDisposable"/> owning everything from the compiler down to the
/// pipeline, with a <see cref="Failed"/> the caller turns into exit code 2.</para>
/// <para>The <b>two</b> colour attachments are the point: attachment 0 is the
/// shaded, sRGB-encoded colour DLSS reconstructs, attachment 1 is the
/// render-resolution motion-vector buffer it reprojects with.</para>
/// </remarks>
internal sealed class CubePipeline : IDisposable
{
    private readonly SlangCompiler?      _compiler;
    private readonly SlangSession?       _session;
    private readonly SlangProgram?       _program;
    private readonly ShaderModule        _vertexModule;
    private readonly ShaderModule        _fragmentModule;
    private readonly DescriptorSetLayout _setLayout;
    private readonly PipelineLayout      _pipelineLayout;
    private readonly GraphicsPipeline    _pipeline;

    /// <summary><see langword="true"/> when the shader did not compile; the
    /// caller exits 2 and nothing below is valid.</summary>
    public bool Failed { get; }

    public ref readonly GraphicsPipeline Pipeline => ref _pipeline;
    public ref readonly PipelineLayout   Layout   => ref _pipelineLayout;

    public unsafe CubePipeline(
        Device   device,
        string   shaderPath,
        VkFormat colorFormat,
        VkFormat motionFormat,
        VkFormat depthFormat)
    {
        _compiler = SlangCompiler.Create();
        Console.WriteLine($"Slang {_compiler.BuildTag} loaded.");

        try
        {
            // No capability declaration: nothing in cube.slang needs anything
            // above the default spirv_1_5 profile.
            _session = _compiler.CreateSession(new SlangSessionDescription());
            _program = _session.Compile(new SlangCompileRequest
            {
                Path        = shaderPath,
                // The order here IS the order Spirv(i) indexes.
                EntryPoints = ["vertexMain", "fragmentMain"],
            });
        }
        catch (SlangCompilationException ex)
        {
            Console.Error.WriteLine($"Slang failed to compile {shaderPath}:\n{ex.Diagnostics}");
            Failed = true;
            return;
        }

        if (!string.IsNullOrEmpty(_program.Warnings))
        {
            // A warning is information, not noise — print it rather than swallow.
            Console.WriteLine("Slang warnings:\n" + _program.Warnings);
        }

        // Entry point i's SPIR-V, in EntryPoints order. No WithVertexEntryPoint
        // / WithFragmentEntryPoint: Slang emits every entry point into SPIR-V
        // as `main` regardless of its Slang-side name, and the builder's
        // default is already `main`. Naming the Slang function is what fails,
        // with VUID-VkPipelineShaderStageCreateInfo-pName-00707.
        _vertexModule   = device.CreateShaderModule(_program.Spirv(0));
        _fragmentModule = device.CreateShaderModule(_program.Spirv(1));

        DescriptorBinding[] bindings =
        [
            new DescriptorBinding
            {
                Slot   = 0,
                Type   = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                Count  = 1,
                // The vertex stage pulls the three matrices; the fragment stage
                // pulls renderExtent for the motion-vector scale.
                Stages = ShaderStages.Vertex | ShaderStages.Fragment,
            },
            new DescriptorBinding
            {
                Slot   = 1,
                Type   = VkDescriptorType.VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
                Count  = 1,
                Stages = ShaderStages.Fragment,
            },
        ];

        // Push descriptors: two bindings that change every frame is what they
        // exist for, and it keeps the sample free of a descriptor pool.
        _setLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            Bindings       = bindings,
            PushDescriptor = true,
        });

        DescriptorSetLayout[] setLayouts = [_setLayout];
        _pipelineLayout = device.CreatePipelineLayout(new PipelineLayoutDescription
        {
            SetLayouts = setLayouts,
        });

        VertexBindingDescription[] vertexBindings =
        [
            new()
            {
                Slot      = 0,
                Stride    = (uint)sizeof(CubeVertex),
                InputRate = VkVertexInputRate.VK_VERTEX_INPUT_RATE_VERTEX,
            },
        ];
        VertexAttributeDescription[] vertexAttributes =
        [
            new() { Location = 0, Binding = 0, Format = VkFormat.VK_FORMAT_R32G32B32_SFLOAT, Offset = CubeVertex.PositionOffset },
            new() { Location = 1, Binding = 0, Format = VkFormat.VK_FORMAT_R32G32_SFLOAT,    Offset = CubeVertex.UvOffset },
            new() { Location = 2, Binding = 0, Format = VkFormat.VK_FORMAT_R32G32B32_SFLOAT, Offset = CubeVertex.NormalOffset },
        ];

        ReadOnlySpan<VkFormat> colorFormats = [colorFormat, motionFormat];

        // One ColorBlendAttachment per declared colour format: the builder
        // rejects a non-empty attachment span whose length does not match the
        // colour-attachment count (ColorBlendDescription's own remarks).
        // Omitting WithColorBlend entirely would also be legal — but stating
        // the two explicitly is what says "two attachments" out loud.
        ReadOnlySpan<ColorBlendAttachment> blend =
        [
            ColorBlendAttachment.Opaque,
            ColorBlendAttachment.Opaque,
        ];

        _pipeline = device.BuildGraphicsPipeline()
            .WithStages(in _vertexModule, in _fragmentModule)
            .WithVertexInput(new VertexInputDescription { Bindings = vertexBindings, Attributes = vertexAttributes })
            .WithDynamicRendering(colorFormats, depthFormat)
            .WithColorBlend(new ColorBlendDescription { Attachments = blend })
            // Standard depth, near 0 / far 1 — which is why
            // DlssFeatureFlags.DepthInverted stays clear (guide §3.8).
            .WithDepthStencil(testEnable: true, writeEnable: true, VkCompareOp.VK_COMPARE_OP_LESS)
            // Back-face culling; the cube is closed. FRONT_FACE_CLOCKWISE
            // rather than the CCW default because the geometry is wound
            // CCW-when-viewed-from-outside (CubeScene.BuildVertices) and
            // `proj.M22 *= -1f` flips Y between NDC and the framebuffer,
            // reversing the apparent winding exactly once.
            .WithRasterization(
                cullMode:  VkCullModeFlagBits.VK_CULL_MODE_BACK_BIT,
                frontFace: VkFrontFace.VK_FRONT_FACE_CLOCKWISE)
            .WithLayout(in _pipelineLayout)
            .Build();
    }

    public void Dispose()
    {
        if (!Failed)
        {
            _pipeline.Dispose();
            _pipelineLayout.Dispose();
            _setLayout.Dispose();
            _fragmentModule.Dispose();
            _vertexModule.Dispose();
        }

        _program?.Dispose();
        _session?.Dispose();
        _compiler?.Dispose();
    }
}
