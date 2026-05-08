using Ahjo.Vulkan.Native;
using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Backs the acceptance criterion of issue 36: a 100-command frame
/// (<c>Begin → 100 cmds → end → ResetForFrame</c>) reports <b>0 bytes
/// allocated per frame</b> after warmup. The benchmark exercises the
/// dynamic-state and copy-command surfaces alongside the pool's
/// acquire/retire/reset loop — three different recorder families that
/// each could leak per-call allocations on their own. The pre-warmup
/// pass faults in the pool's first command buffer so steady-state
/// iterations hit reuse.
/// </summary>
[MemoryDiagnoser]
public class CommandBufferPoolBenchmarks
{
    private const int CommandsPerFrame = 100;

    private Instance          _instance = null!;
    private Device            _device   = null!;
    private CommandBufferPool _cmdPool  = null!;
    private Buffer            _buffer;

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
        _device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });

        _cmdPool = new CommandBufferPool(_device, family);
        _buffer  = _device.Allocator.CreateBuffer(
            new BufferDescription { Size = 1024, Usage = BufferUsage.TransferDst },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        // Warm — drive a full frame so the pool's _idle stack and inner
        // recorder retire path settle their capacities before measurement.
        Frame_Begin_100Cmds_End_Reset();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _cmdPool?.Dispose();
        _buffer.Dispose();
        _device?.Dispose();
        _instance?.Dispose();
    }

    [Benchmark]
    public void Frame_Begin_100Cmds_End_Reset()
    {
        var vp      = new VkViewport { width = 100, height = 100, maxDepth = 1.0f };
        var scissor = new VkRect2D   { extent = new VkExtent2D { width = 100, height = 100 } };

        var rec = _cmdPool.Begin();
        try
        {
            for (int i = 0; i < CommandsPerFrame; i++)
            {
                rec.SetViewport(in vp);
                rec.SetScissor(in scissor);
                rec.FillBuffer(in _buffer, data: (uint)i, offset: 0, size: 16);
            }
            rec.End();
        }
        finally { rec.Dispose(); }

        _cmdPool.ResetForFrame();
    }
}
