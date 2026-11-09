using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Slang;

namespace Ahjo.Vulkan.Samples.HelloFrameGen;

/// <summary>
/// The UI and composite pipelines from <c>overlay.slang</c>: one premultiplied
/// translucent quad into <see cref="FrameGenTargets.Ui"/>, then
/// <c>Backbuffer = Ui + (1 − α)·Hudless</c> into
/// <see cref="FrameGenTargets.Backbuffer"/> (DLSS-FG guide l. 742-754).
/// </summary>
/// <remarks>
/// Same shape as <see cref="CubePipeline"/>: an <see cref="IDisposable"/>
/// owning everything from the compiler down, with a <see cref="Failed"/> the
/// caller turns into exit code 2. Neither pipeline takes vertex input; both
/// draw from <c>SV_VertexID</c>, which is why the device enables
/// <c>shaderDrawParameters</c> (Ahjo.Vulkan.Slang README, #168).
/// </remarks>
internal sealed class OverlayPipelines : IDisposable
{
    private readonly SlangCompiler?      _compiler;
    private readonly SlangSession?       _session;
    private readonly SlangProgram?       _program;
    private readonly ShaderModule        _uiVertex;
    private readonly ShaderModule        _uiFragment;
    private readonly ShaderModule        _compositeVertex;
    private readonly ShaderModule        _compositeFragment;
    private readonly PipelineLayout      _uiLayout;
    private readonly DescriptorSetLayout _compositeSetLayout;
    private readonly PipelineLayout      _compositeLayout;
    private readonly GraphicsPipeline    _ui;
    private readonly GraphicsPipeline    _composite;

    /// <summary><see langword="true"/> when the shader did not compile; the
    /// caller exits 2 and nothing below is valid.</summary>
    public bool Failed { get; }

    public ref readonly GraphicsPipeline UiPipeline        => ref _ui;
    public ref readonly GraphicsPipeline CompositePipeline => ref _composite;
    public ref readonly PipelineLayout   CompositeLayout   => ref _compositeLayout;

    public OverlayPipelines(Device device, string shaderPath, VkFormat uiFormat, VkFormat backbufferFormat)
    {
        _compiler = SlangCompiler.Create();

        try
        {
            _session = _compiler.CreateSession(new SlangSessionDescription());
            _program = _session.Compile(new SlangCompileRequest
            {
                Path        = shaderPath,
                // The order here IS the order Spirv(i) indexes.
                EntryPoints = ["uiVertexMain", "uiFragmentMain", "compositeVertexMain", "compositeFragmentMain"],
            });
        }
        catch (SlangCompilationException ex)
        {
            Console.Error.WriteLine($"Slang failed to compile {shaderPath}:\n{ex.Diagnostics}");
            Failed = true;
            return;
        }

        if (!string.IsNullOrEmpty(_program.Warnings))
            Console.WriteLine("Slang warnings:\n" + _program.Warnings);

        _uiVertex          = device.CreateShaderModule(_program.Spirv(0));
        _uiFragment        = device.CreateShaderModule(_program.Spirv(1));
        _compositeVertex   = device.CreateShaderModule(_program.Spirv(2));
        _compositeFragment = device.CreateShaderModule(_program.Spirv(3));

        // UI: no descriptors at all.
        _uiLayout = device.CreatePipelineLayout(new PipelineLayoutDescription());

        // Composite: two sampled images, read with Load (no sampler).
        DescriptorBinding[] bindings =
        [
            new DescriptorBinding
            {
                Slot   = 0,
                Type   = VkDescriptorType.VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE,
                Count  = 1,
                Stages = ShaderStages.Fragment,
            },
            new DescriptorBinding
            {
                Slot   = 1,
                Type   = VkDescriptorType.VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE,
                Count  = 1,
                Stages = ShaderStages.Fragment,
            },
        ];
        _compositeSetLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            Bindings       = bindings,
            PushDescriptor = true,
        });
        DescriptorSetLayout[] setLayouts = [_compositeSetLayout];
        _compositeLayout = device.CreatePipelineLayout(new PipelineLayoutDescription { SetLayouts = setLayouts });

        // No blending in either: the UI shader writes the premultiplied value
        // itself into a target cleared to 0, and the composite does the
        // "over" arithmetic in the shader, on the raw texels.
        VkFormat[] uiFormats = [uiFormat];
        _ui = device.BuildGraphicsPipeline()
            .WithStages(in _uiVertex, in _uiFragment)
            .WithDynamicRendering(uiFormats)
            .WithRasterization(cullMode: VkCullModeFlagBits.VK_CULL_MODE_NONE)
            .WithLayout(in _uiLayout)
            .Build();

        VkFormat[] backbufferFormats = [backbufferFormat];
        _composite = device.BuildGraphicsPipeline()
            .WithStages(in _compositeVertex, in _compositeFragment)
            .WithDynamicRendering(backbufferFormats)
            .WithRasterization(cullMode: VkCullModeFlagBits.VK_CULL_MODE_NONE)
            .WithLayout(in _compositeLayout)
            .Build();
    }

    public void Dispose()
    {
        if (!Failed)
        {
            _composite.Dispose();
            _ui.Dispose();
            _compositeLayout.Dispose();
            _compositeSetLayout.Dispose();
            _uiLayout.Dispose();
            _compositeFragment.Dispose();
            _compositeVertex.Dispose();
            _uiFragment.Dispose();
            _uiVertex.Dispose();
        }

        _program?.Dispose();
        _session?.Dispose();
        _compiler?.Dispose();
    }
}
