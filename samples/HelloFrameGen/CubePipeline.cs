using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Slang;

namespace Ahjo.Vulkan.Samples.HelloFrameGen;

/// <summary>
/// The scene half: compile <c>cube.slang</c>'s four entry points with Slang at
/// run time, and build the two two-colour-attachment graphics pipelines they
/// need: the cube, and the full-screen background pass drawn after it.
/// </summary>
/// <remarks>
/// <para>Same shape as <c>samples/HelloRayQuery/RayQueryPipeline.cs</c>: an
/// <see cref="IDisposable"/> owning everything from the compiler down to the
/// pipeline, with a <see cref="Failed"/> the caller turns into exit code 2.</para>
/// <para>The <b>two</b> colour attachments are the point: attachment 0 is the
/// shaded, sRGB-encoded HUD-less colour, attachment 1 is the motion-vector
/// buffer Frame Generation reprojects with. Copied from
/// <c>samples/HelloDlaa/CubePipeline.cs</c>, plus the background pipeline.</para>
/// </remarks>
internal sealed class CubePipeline : IDisposable
{
    private readonly SlangCompiler?      _compiler;
    private readonly SlangSession?       _session;
    private readonly SlangProgram?       _program;
    private readonly ShaderModule        _vertexModule;
    private readonly ShaderModule        _fragmentModule;
    private readonly ShaderModule        _backgroundVertexModule;
    private readonly ShaderModule        _backgroundFragmentModule;
    private readonly DescriptorSetLayout _setLayout;
    private readonly PipelineLayout      _pipelineLayout;
    private readonly GraphicsPipeline    _pipeline;
    private readonly GraphicsPipeline    _backgroundPipeline;

    /// <summary><see langword="true"/> when the shader did not compile; the
    /// caller exits 2 and nothing below is valid.</summary>
    public bool Failed { get; }

    public ref readonly GraphicsPipeline Pipeline => ref _pipeline;

    /// <summary>The full-screen far-plane pass; same layout and descriptors as
    /// <see cref="Pipeline"/>, no vertex input, drawn with 3 vertices.</summary>
    public ref readonly GraphicsPipeline BackgroundPipeline => ref _backgroundPipeline;
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
                EntryPoints = ["vertexMain", "fragmentMain", "backgroundVertexMain", "backgroundFragmentMain"],
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
        _vertexModule             = device.CreateShaderModule(_program.Spirv(0));
        _fragmentModule           = device.CreateShaderModule(_program.Spirv(1));
        _backgroundVertexModule   = device.CreateShaderModule(_program.Spirv(2));
        _backgroundFragmentModule = device.CreateShaderModule(_program.Spirv(3));

        DescriptorBinding[] bindings =
        [
            new DescriptorBinding
            {
                Slot   = 0,
                Type   = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                Count  = 1,
                // The vertex stage pulls the matrices; the fragment stage pulls
                // renderExtent and, in the background pass, clipToPrevClip.
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

        // An array, not a stack span: two builders consume it, and a ref-struct
        // builder may not capture a stack-allocated span past one statement.
        VkFormat[] colorFormats = [colorFormat, motionFormat];

        // One ColorBlendAttachment per declared colour format: the builder
        // rejects a non-empty attachment span whose length does not match the
        // colour-attachment count (ColorBlendDescription's own remarks).
        // Omitting WithColorBlend entirely would also be legal — but stating
        // the two explicitly is what says "two attachments" out loud.
        ColorBlendAttachment[] blend =
        [
            ColorBlendAttachment.Opaque,
            ColorBlendAttachment.Opaque,
        ];

        _pipeline = device.BuildGraphicsPipeline()
            .WithStages(in _vertexModule, in _fragmentModule)
            .WithVertexInput(new VertexInputDescription { Bindings = vertexBindings, Attributes = vertexAttributes })
            .WithDynamicRendering(colorFormats, depthFormat)
            .WithColorBlend(new ColorBlendDescription { Attachments = blend })
            // Standard depth, near 0 / far 1, which is why
            // DlssFrameGenerationInputs.DepthInverted stays false.
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

        // The background: a far-plane full-screen triangle that passes only
        // where the cube left the cleared depth of 1.0 (LESS_OR_EQUAL), and
        // writes no depth so the depth Frame Generation reads stays the cube's.
        // No culling: the triangle's winding is not the cube's concern.
        _backgroundPipeline = device.BuildGraphicsPipeline()
            .WithStages(in _backgroundVertexModule, in _backgroundFragmentModule)
            .WithDynamicRendering(colorFormats, depthFormat)
            .WithColorBlend(new ColorBlendDescription { Attachments = blend })
            .WithDepthStencil(testEnable: true, writeEnable: false, VkCompareOp.VK_COMPARE_OP_LESS_OR_EQUAL)
            .WithRasterization(cullMode: VkCullModeFlagBits.VK_CULL_MODE_NONE)
            .WithLayout(in _pipelineLayout)
            .Build();
    }

    public void Dispose()
    {
        if (!Failed)
        {
            _backgroundPipeline.Dispose();
            _pipeline.Dispose();
            _pipelineLayout.Dispose();
            _setLayout.Dispose();
            _backgroundFragmentModule.Dispose();
            _backgroundVertexModule.Dispose();
            _fragmentModule.Dispose();
            _vertexModule.Dispose();
        }

        _program?.Dispose();
        _session?.Dispose();
        _compiler?.Dispose();
    }
}
