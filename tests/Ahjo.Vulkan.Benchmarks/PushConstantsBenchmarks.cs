using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Backs acceptance criterion 2 of issue 40:
/// <see cref="CommandRecorder.PushConstants{T}(in PipelineLayout, ShaderStages, in T, uint)"/>
/// allocates 0 B per call after warmup. The recorder is created once per
/// invocation; the inner loop dispatches <c>OperationsPerInvoke</c>
/// PushConstants calls so BDN's per-op allocation column converges to 0.
/// </summary>
[MemoryDiagnoser]
public class PushConstantsBenchmarks
{
    private const int CallsPerInvoke = 1024;

    private Instance          _instance = null!;
    private Device            _device   = null!;
    private PipelineLayout    _layout;
    private CommandBufferPool _cmdPool  = null!;

    [GlobalSetup]
    public void Setup()
    {
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
        _device  = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });

        PushConstantRange[] ranges = [PushConstantRange.For<PushBlock64>(ShaderStages.Compute)];
        _layout = _device.CreatePipelineLayout(new PipelineLayoutDescription
        {
            PushConstantRanges = ranges,
        });

        _cmdPool = new CommandBufferPool(_device, family);

        // Warm — fault in the pool's first buffer so steady-state Begin/Dispose pairs hit reuse.
        var rec = _cmdPool.Begin();
        rec.End();
        rec.Dispose();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _cmdPool?.Dispose();
        _layout.Dispose();
        _device?.Dispose();
        _instance?.Dispose();
    }

    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public void PushConstants_64B()
    {
        var pc = default(PushBlock64);
        using var rec = _cmdPool.Begin();
        for (int i = 0; i < CallsPerInvoke; i++)
            rec.PushConstants(in _layout, ShaderStages.Compute, in pc);
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    private struct PushBlock64 { }
}
