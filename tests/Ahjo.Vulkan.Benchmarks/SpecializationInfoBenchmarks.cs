using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;
using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Backs acceptance criterion 2 of issue 42: building a compute pipeline
/// with <see cref="SpecializationInfo{T}"/> still allocates 0 managed
/// bytes per Build call once the per-<c>T</c> map-entry cache has warmed
/// (which happens during <see cref="Setup"/>'s touch). The measured
/// benchmark only drives the builder + native vkCreateComputePipelines
/// + Dispose.
/// </summary>
[MemoryDiagnoser]
public class SpecializationInfoBenchmarks
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SpecConstants
    {
        public uint LocalSizeX;
        public uint Tag;
    }

    private Instance       _instance       = null!;
    private Device         _device         = null!;
    private ShaderModule   _module;
    private DescriptorSetLayout _setLayout;
    private PipelineLayout _layout;

    [GlobalSetup]
    public void Setup()
    {
        string shadersDir = Path.Combine(AppContext.BaseDirectory, "Shaders");
        string spv        = Path.Combine(shadersDir, "spec_fill.comp.spv");
        if (!File.Exists(spv))
            throw new FileNotFoundException(
                $"SpecializationInfoBenchmarks needs the compiled spec_fill.comp shader at {shadersDir}. " +
                "Build the benchmark project once with the Vulkan SDK on PATH (or VULKAN_SDK env var set) so glslc compiles it.");

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

        using var blob = SpirvBlob.Load(spv);
        _module = _device.CreateShaderModule(blob.Words);

        _setLayout = _device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            Bindings =
            [
                new DescriptorBinding
                {
                    Slot = 0, Type = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
                    Count = 1, Stages = ShaderStages.Compute,
                },
            ],
            PushDescriptor = true,
        });
        DescriptorSetLayout[] layouts = [_setLayout];
        _layout = _device.CreatePipelineLayout(new PipelineLayoutDescription { SetLayouts = layouts });

        // Warm the per-T entry cache so the measured path doesn't pay the
        // first-call reflection cost.
        var warm = new SpecConstants { LocalSizeX = 64, Tag = 0 };
        _ = SpecializationInfo.For<SpecConstants>(in warm);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _layout.Dispose();
        _setLayout.Dispose();
        _module.Dispose();
        _device?.Dispose();
        _instance?.Dispose();
    }

    [Benchmark]
    public bool Build_WithSpecialization()
    {
        var values = new SpecConstants { LocalSizeX = 64, Tag = 0xC0FFEE };
        var spec   = SpecializationInfo.For<SpecConstants>(in values);
        using var pipeline = _device.BuildComputePipeline()
            .WithShader(in _module)
            .WithLayout(in _layout)
            .WithSpecialization(spec)
            .Build();
        return pipeline.IsNull;
    }
}
