using Ahjo.Vulkan.Native;
using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Backs acceptance criterion 2 of issue 44: building the extended
/// graphics pipeline (alpha-blend color attachment + 4x MSAA + dynamic
/// line-width) still allocates 0 managed bytes per Build call. The
/// inputs (shader modules, layout, format / blend / dynamic-state spans)
/// are produced once in <see cref="Setup"/>; the measured benchmark only
/// drives the builder + native vkCreateGraphicsPipelines + Dispose, all
/// of which are stack-only on the wrapper side.
/// </summary>
[MemoryDiagnoser]
public class GraphicsPipelineBuilderBenchmarks
{
    private Instance       _instance = null!;
    private Device         _device   = null!;
    private ShaderModule   _vMod;
    private ShaderModule   _fMod;
    private PipelineLayout _layout;

    private VkFormat[]             _colorFormats   = null!;
    private ColorBlendAttachment[] _blendAttach    = null!;
    private VkDynamicState[]       _dynamicStates  = null!;

    [GlobalSetup]
    public void Setup()
    {
        string shadersDir = Path.Combine(AppContext.BaseDirectory, "Shaders");
        string vertSpv    = Path.Combine(shadersDir, "triangle.vert.spv");
        string fragSpv    = Path.Combine(shadersDir, "triangle.frag.spv");
        if (!File.Exists(vertSpv) || !File.Exists(fragSpv))
            throw new FileNotFoundException(
                $"GraphicsPipelineBuilderBenchmarks needs compiled triangle shaders at {shadersDir}. " +
                "Build the benchmark project once with the Vulkan SDK on PATH (or VULKAN_SDK env var set) so glslc compiles them.");

        _instance = Instance.Create(default);

        uint family = uint.MaxValue;
        var gpu = _instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
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
        _device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });

        using var vBlob = SpirvBlob.Load(vertSpv);
        using var fBlob = SpirvBlob.Load(fragSpv);
        _vMod  = _device.CreateShaderModule(vBlob.Words);
        _fMod  = _device.CreateShaderModule(fBlob.Words);
        _layout = _device.CreatePipelineLayout(default);

        // Cached inputs to keep the benchmark surface free of allocations.
        // The measured method takes no spans of its own — every span field
        // points at one of these arrays, captured once in Setup.
        _colorFormats  = [VkFormat.VK_FORMAT_R8G8B8A8_UNORM];
        _blendAttach   = [ColorBlendAttachment.AlphaBlend];
        _dynamicStates =
        [
            VkDynamicState.VK_DYNAMIC_STATE_VIEWPORT,
            VkDynamicState.VK_DYNAMIC_STATE_SCISSOR,
            VkDynamicState.VK_DYNAMIC_STATE_LINE_WIDTH,
        ];
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _layout.Dispose();
        _fMod.Dispose();
        _vMod.Dispose();
        _device?.Dispose();
        _instance?.Dispose();
    }

    [Benchmark]
    public bool Build_AlphaBlend_Msaa4x_DynamicLineWidth()
    {
        using var pipeline = _device.BuildGraphicsPipeline()
            .WithStages(in _vMod, in _fMod)
            .WithDynamicRendering(_colorFormats)
            .WithLayout(in _layout)
            .WithColorBlend(new ColorBlendDescription { Attachments = _blendAttach })
            .WithMultisample(VkSampleCountFlagBits.VK_SAMPLE_COUNT_4_BIT)
            .WithDynamicState(_dynamicStates)
            .Build();
        return pipeline.IsNull;
    }
}
