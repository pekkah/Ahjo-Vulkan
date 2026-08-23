using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Slang;

namespace Ahjo.Vulkan.Samples.HelloRayQuery;

/// <summary>
/// The shader half: compile <c>rayquery.slang</c> with Slang at run time, and
/// build the compute pipeline and push-descriptor layout it needs.
/// </summary>
/// <remarks>
/// <para>Compiled at run time rather than precompiled with <c>glslc</c>: ray
/// query in GLSL needs <c>GL_EXT_ray_query</c>, and the repository is moving
/// off glslc. It also means this sample exercises <c>Ahjo.Vulkan.Slang</c> on
/// a path that matters rather than on a hello-triangle.</para>
/// <para><b>The capability declaration is load-bearing.</b> A ray-query entry
/// point uses <c>spvRayQueryKHR</c>, which the default <c>spirv_1_5</c> profile
/// does not carry. Without declaring it Slang still compiles — it upgrades the
/// profile itself and warns <c>E41012: profile implicitly upgraded</c>.
/// Declaring it here is how the sample says "I meant that" and gets a clean
/// compile; the emitted SPIR-V is byte-identical either way
/// (<c>SlangCompilerTests.Compile_RayQuery_WithCapability_IsWarningFreeAndEmitsTheSameSpirv</c>).
/// Note it is <em>not</em> part of the profile string:
/// <c>"spirv_1_5+spvRayQueryKHR"</c> is rejected by
/// <c>IGlobalSession::findProfile</c> as an unknown profile.</para>
/// </remarks>
internal sealed class RayQueryPipeline : IDisposable
{
    private readonly SlangCompiler?      _compiler;
    private readonly SlangSession?       _session;
    private readonly SlangProgram?       _program;
    private readonly ShaderModule        _module;
    private readonly DescriptorSetLayout _setLayout;
    private readonly PipelineLayout      _pipelineLayout;
    private readonly ComputePipeline     _pipeline;

    /// <summary><see langword="true"/> when the shader did not compile; the
    /// caller exits 2 and nothing below is valid.</summary>
    public bool Failed { get; }

    public ref readonly ComputePipeline Pipeline => ref _pipeline;
    public ref readonly PipelineLayout  Layout   => ref _pipelineLayout;

    public RayQueryPipeline(Device device, string shaderPath)
    {
        _compiler = SlangCompiler.Create();
        Console.WriteLine($"Slang {_compiler.BuildTag} loaded.");

        try
        {
            _session = _compiler.CreateSession(new SlangSessionDescription
            {
                Capabilities = [Utf8Name.FromLiteral("spvRayQueryKHR"u8)],
            });
            _program = _session.Compile(new SlangCompileRequest { Path = shaderPath });
        }
        catch (SlangCompilationException ex)
        {
            Console.Error.WriteLine($"Slang failed to compile {shaderPath}:\n{ex.Diagnostics}");
            Failed = true;
            return;
        }

        if (!string.IsNullOrEmpty(_program.Warnings))
        {
            // Should not happen now that spvRayQueryKHR is declared, but a
            // warning is information, not noise — print it rather than swallow.
            Console.WriteLine("Slang warnings:\n" + _program.Warnings);
        }

        _module = device.CreateShaderModule(_program.Spirv(0));

        DescriptorBinding[] bindings =
        [
            new DescriptorBinding
            {
                Slot   = 0,
                Type   = VkDescriptorType.VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR,
                Count  = 1,
                Stages = ShaderStages.Compute,
            },
            new DescriptorBinding
            {
                Slot   = 1,
                Type   = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_IMAGE,
                Count  = 1,
                Stages = ShaderStages.Compute,
            },
        ];

        // Push descriptors: two bindings that change every dispatch is exactly
        // the shape they exist for, and it keeps the sample free of a
        // descriptor pool.
        _setLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            Bindings       = bindings,
            PushDescriptor = true,
        });

        DescriptorSetLayout[] layouts = [_setLayout];
        _pipelineLayout = device.CreatePipelineLayout(new PipelineLayoutDescription
        {
            SetLayouts = layouts,
        });

        // No WithEntryPoint: Slang names the Slang-side entry point
        // `computeMain`, but emits it into SPIR-V as `main` — SPIR-V keeps the
        // GLSL-style default regardless of the source-language name. The
        // builder's default is `main`, so asking for "computeMain" here is what
        // fails, with VUID-VkPipelineShaderStageCreateInfo-pName-00707.
        _pipeline = device.BuildComputePipeline()
            .WithShader(in _module)
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
            _module.Dispose();
        }

        _program?.Dispose();
        _session?.Dispose();
        _compiler?.Dispose();
    }
}
