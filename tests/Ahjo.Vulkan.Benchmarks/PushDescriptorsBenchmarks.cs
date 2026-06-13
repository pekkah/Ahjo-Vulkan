using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;
using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Backs acceptance criterion 2 of issue 39:
/// <see cref="CommandRecorder.PushDescriptors{T}"/> allocates 0 B per call
/// after warmup. The recorder is created once per invocation; the inner
/// loop dispatches <c>CallsPerInvoke</c> push-descriptor calls so BDN's
/// per-op allocation column converges to 0.
/// </summary>
[MemoryDiagnoser]
public class PushDescriptorsBenchmarks
{
    private const int CallsPerInvoke = 1024;

    private Instance               _instance = null!;
    private Device                 _device   = null!;
    private DescriptorSetLayout    _setLayout;
    private PipelineLayout         _pipelineLayout;
    private DescriptorTemplate<FillWrites> _template;
    private Buffer                 _buffer;
    private CommandBufferPool      _cmdPool  = null!;

    [GlobalSetup]
    public void Setup()
    {
        _instance = Instance.Create(default);

        uint family = uint.MaxValue;
        var gpu = _instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsCompute)
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

        DescriptorBinding[] bindings =
        [
            new DescriptorBinding
            {
                Slot = 0, Type = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
                Count = 1, Stages = ShaderStages.Compute,
            },
        ];
        _setLayout = _device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            Bindings       = bindings,
            PushDescriptor = true,
        });
        DescriptorSetLayout[] layouts = [_setLayout];
        _pipelineLayout = _device.CreatePipelineLayout(new PipelineLayoutDescription
        {
            SetLayouts = layouts,
        });
        _template = _pipelineLayout.CreatePushDescriptorTemplate<FillWrites>(
            set: 0, VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_COMPUTE, bindings);

        _buffer = _device.Allocator.CreateBuffer(
            new BufferDescription { Size = 1024, Usage = BufferUsage.StorageBuffer },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

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
        _buffer.Dispose();
        _template.Dispose();
        _pipelineLayout.Dispose();
        _setLayout.Dispose();
        _device?.Dispose();
        _instance?.Dispose();
    }

    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public void PushDescriptors_StorageBuffer()
    {
        var writes = new FillWrites { Out = BufferDescriptorWrite.Of(in _buffer) };
        using var rec = _cmdPool.Begin();
        for (int i = 0; i < CallsPerInvoke; i++)
            rec.PushDescriptors(in _template, in _pipelineLayout, in writes);
    }

    /// <summary>
    /// Covers the non-templated
    /// <see cref="CommandRecorder.PushDescriptorSet(VkPipelineBindPoint, in PipelineLayout, uint, System.ReadOnlySpan{DescriptorWrite})"/>
    /// span overload — the path that routes the cached
    /// <c>vkCmdPushDescriptorSet</c> pointer through <c>FlushPush</c> (issue
    /// #121). A single-element write stays on the <c>≤ 8</c> stackalloc fast
    /// path, so steady-state <b>Allocated</b> must read <c>-</c>.
    /// </summary>
    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public void PushDescriptorSet_SpanWrites()
    {
        var info = BufferDescriptorWrite.Of(in _buffer);
        ReadOnlySpan<DescriptorWrite> writes =
        [
            DescriptorWrite.Buffer(
                binding: 0, arrayElement: 0,
                VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER, in info),
        ];

        // `scoped` narrows the recorder's safe-to-escape to this method so
        // the method-local span above can flow into PushDescriptorSet
        // without tripping CS8350 (mirrors CommandRecorderBenchmarks).
        using scoped var rec = _cmdPool.Begin();
        for (int i = 0; i < CallsPerInvoke; i++)
            rec.PushDescriptorSet(
                VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_COMPUTE,
                in _pipelineLayout, set: 0, writes);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FillWrites
    {
        public BufferDescriptorWrite Out;
    }
}
