using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Timestamp query-pool canaries (#198): the two per-frame recorder calls
/// (<see cref="CommandRecorder.ResetQueryPool"/> +
/// <see cref="CommandRecorder.WriteTimestamp"/>) and the once-per-frame
/// readback (<see cref="QueryPool.TryGetResults(uint, System.Span{ulong})"/>)
/// must report 0 B/op after warmup.
/// </summary>
[MemoryDiagnoser]
public class TimestampQueryBenchmarks
{
    private const int CallsPerInvoke = 256;

    private Instance          _instance = null!;
    private Device            _device   = null!;
    private CommandBufferPool _cmdPool  = null!;
    private QueryPool         _pool;
    private readonly ulong[]        _readback             = new ulong[2];
    private readonly QueryResult[]  _availabilityReadback = new QueryResult[2];

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
        _pool    = _device.CreateQueryPool(64);

        // Submit one reset over the full range and wait, so TryGetResults
        // polls initialized-but-unavailable queries — the -09401-legal
        // construction of the steady-state "not ready" frame readback.
        Queue queue = _device.GetQueue(family, queueIndex: 0);
        QueryPool pool = _pool;
        queue.ImmediateSubmit(_cmdPool, (ref CommandRecorder r) =>
        {
            r.ResetQueryPool(in pool, 0, 64);
        });

        // Warm — first Begin grows the pool by one buffer; subsequent
        // begins/resets hit reuse and steady-state recording is alloc-free.
        var rec = _cmdPool.Begin();
        rec.End();
        rec.Dispose();
        _cmdPool.ResetForFrame();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _cmdPool?.Dispose();
        _pool.Dispose();
        _device?.Dispose();
        _instance?.Dispose();
    }

    /// <summary>
    /// One <c>vkCmdResetQueryPool</c> + two <c>vkCmdWriteTimestamp2</c> per
    /// op — the per-pass bracket a render-graph recorder emits.
    /// </summary>
    /// <remarks>
    /// <b>Recording only — never submitted.</b> The command buffer is reset,
    /// not queued, so the repeated resets/writes recorded here are not a
    /// validation error against any executed workload.
    /// </remarks>
    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public void ResetAndWritePair()
    {
        // Dispose the recorder (inner-block scope) BEFORE ResetForFrame: Retire
        // fires on Dispose, not End, so the buffer must be retired to _spent
        // before the reset drains _spent → _idle, or it never recycles. This
        // also keeps the benchmark valid under AHJO_VULKAN_TIER=validation,
        // where ResetForFrame asserts on an outstanding recorder.
        using (var rec = _cmdPool.Begin())
        {
            for (int i = 0; i < CallsPerInvoke; i++)
            {
                rec.ResetQueryPool(in _pool, 0, 2);
                rec.WriteTimestamp(in _pool, Stage.TopOfPipe, 0);
                rec.WriteTimestamp(in _pool, Stage.BottomOfPipe, 1);
            }
            rec.End();
        }
        _cmdPool.ResetForFrame();
    }

    /// <summary>
    /// The steady-state frame readback: <c>vkGetQueryPoolResults</c> against
    /// initialized-but-unavailable queries (the setup submitted a reset-only
    /// command buffer over the whole range), returning <see langword="false"/>
    /// every time. The point is the 0 B/op cell.
    /// </summary>
    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public bool TryGetResults_NotReady()
    {
        bool ready = false;
        for (int i = 0; i < CallsPerInvoke; i++)
            ready |= _pool.TryGetResults(0, _readback);
        return ready;
    }

    /// <summary>
    /// The availability-reporting readback overload against the same
    /// initialized-but-unavailable queries — a different marshaling shape
    /// (16-byte <see cref="QueryResult"/> stride,
    /// <c>VK_QUERY_RESULT_WITH_AVAILABILITY_BIT</c>) that is equally
    /// per-frame-callable, so it needs its own 0 B/op cell.
    /// </summary>
    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public bool TryGetResults_WithAvailability_NotReady()
    {
        bool ready = false;
        for (int i = 0; i < CallsPerInvoke; i++)
            ready |= _pool.TryGetResults(0, _availabilityReadback);
        return ready;
    }
}
