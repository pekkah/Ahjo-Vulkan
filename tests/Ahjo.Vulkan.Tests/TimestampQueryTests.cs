using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Covers the timestamp query-pool surface (#198): <see cref="QueryPool"/>,
/// <see cref="Device.CreateQueryPool"/>, <see cref="Device.TimestampPeriod"/>,
/// and <see cref="CommandRecorder.ResetQueryPool"/> /
/// <see cref="CommandRecorder.WriteTimestamp"/> plus the three readback
/// methods.
/// </summary>
/// <remarks>
/// <para>The elapsed-ticks oracle runs with <c>VK_LAYER_KHRONOS_validation</c>
/// loaded and asserts the layer logged no errors — the layer is what checks
/// the reset-before-use rule (<c>VUID-vkCmdWriteTimestamp2-None-03864</c>),
/// the readback initialization rule
/// (<c>VUID-vkGetQueryPoolResults-None-09401</c>) and the stride/flags rules
/// on <c>vkGetQueryPoolResults</c>. The not-ready / availability / WAIT tests
/// need only a real hardware driver: their oracle is the driver's own
/// per-query availability state.</para>
/// <para>Every test skips without a real driver; submitting ones also skip on
/// a software ICD (issue #32), and the timestamp-writing ones skip when the
/// chosen queue family reports <c>timestampValidBits == 0</c>
/// (<c>VUID-vkCmdWriteTimestamp2-timestampValidBits-03863</c>).</para>
/// </remarks>
public sealed unsafe class TimestampQueryTests
{
    private const int ElementCount = 256;

    [Fact]
    public void CreateQueryPool_IsOwningAndDisposes()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _, out _);

        using var pool = device.CreateQueryPool(4);
        Assert.False(pool.IsNull);
        Assert.True(pool.OwnsHandle);
        Assert.Equal(4u, pool.QueryCount);
    }

    [Fact]
    public void CreateQueryPool_ZeroCount_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _, out _);

        Assert.Throws<ArgumentOutOfRangeException>(() => device.CreateQueryPool(0));
    }

    [Fact]
    public void WriteTimestampPair_MeasuresElapsedTicks()
    {
        SkipUnlessValidatedSubmitPossible();

        var errors = new List<DebugMessage>();
        using var instance = CreateValidatedInstance(errors);
        using var device   = CreateGraphicsDevice(instance, out uint family, out uint validBits);
        RequireTimestamps(validBits);

        using var pool    = device.CreateQueryPool(2);
        using var buffer  = CreateDeviceBuffer(device);
        using var cmdPool   = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        Queue queue = device.GetQueue(family, queueIndex: 0);

        QueryPool captured = pool;
        Buffer    target   = buffer;
        SubmitAndWait(queue, cmdPool, fencePool, (ref CommandRecorder rec) =>
        {
            rec.ResetQueryPool(in captured, 0, 2);
            rec.WriteTimestamp(in captured, Stage.TopOfPipe, 0);
            rec.FillBuffer(in target, 0xA5A5A5A5u);
            rec.WriteTimestamp(in captured, Stage.BottomOfPipe, 1);
        });

        Span<ulong> ticks = stackalloc ulong[2];
        Assert.True(pool.TryGetResults(0, ticks));

        ulong mask = ValidBitsMask(validBits);
        ulong t0 = ticks[0] & mask;
        ulong t1 = ticks[1] & mask;
        Assert.True(t1 >= t0,
            $"BottomOfPipe timestamp ({t1}) must not precede TopOfPipe ({t0}) after masking to {validBits} bits.");
        Assert.True(device.TimestampPeriod > 0);

        AssertNoValidationErrors(errors);
    }

    [Fact]
    public void TryGetResults_BeforeWrite_ReturnsFalseWithoutThrowing()
    {
        SkipUnlessSubmitPossible();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family, out _);

        using var pool      = device.CreateQueryPool(2);
        using var cmdPool   = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        Queue queue = device.GetQueue(family, queueIndex: 0);

        // A submitted reset is what makes the readback legal
        // (VUID-vkGetQueryPoolResults-None-09401): the queries are now
        // initialized but unavailable, so "not ready" is the correct answer.
        QueryPool captured = pool;
        SubmitAndWait(queue, cmdPool, fencePool, (ref CommandRecorder rec) =>
        {
            rec.ResetQueryPool(in captured, 0, 2);
        });

        Span<ulong> ticks = stackalloc ulong[2];
        Assert.False(pool.TryGetResults(0, ticks));
    }

    [Fact]
    public void TryGetResults_WithAvailability_ReportsPerQueryState()
    {
        SkipUnlessSubmitPossible();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family, out uint validBits);
        RequireTimestamps(validBits);

        using var pool      = device.CreateQueryPool(2);
        using var cmdPool   = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        Queue queue = device.GetQueue(family, queueIndex: 0);

        // Reset both, write only query 0 — query 1 stays unavailable.
        QueryPool captured = pool;
        SubmitAndWait(queue, cmdPool, fencePool, (ref CommandRecorder rec) =>
        {
            rec.ResetQueryPool(in captured, 0, 2);
            rec.WriteTimestamp(in captured, Stage.AllCommands, 0);
        });

        Span<QueryResult> results = stackalloc QueryResult[2];
        Assert.False(pool.TryGetResults(0, results));

        Assert.True(results[0].IsAvailable);
        Assert.NotEqual(0ul, results[0].Value & ValidBitsMask(validBits));
        Assert.False(results[1].IsAvailable);
    }

    [Fact]
    public void GetResults_Wait_ReturnsBothValues()
    {
        SkipUnlessSubmitPossible();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family, out uint validBits);
        RequireTimestamps(validBits);

        using var pool      = device.CreateQueryPool(2);
        using var buffer    = CreateDeviceBuffer(device);
        using var cmdPool   = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        Queue queue = device.GetQueue(family, queueIndex: 0);

        // WAIT overload smoke: only ever constructed against a fully
        // submitted, fenced pair — a reset-but-never-written query would
        // make this wait forever, which is exactly the documented hazard.
        QueryPool captured = pool;
        Buffer    target   = buffer;
        SubmitAndWait(queue, cmdPool, fencePool, (ref CommandRecorder rec) =>
        {
            rec.ResetQueryPool(in captured, 0, 2);
            rec.WriteTimestamp(in captured, Stage.TopOfPipe, 0);
            rec.FillBuffer(in target, 0x5A5A5A5Au);
            rec.WriteTimestamp(in captured, Stage.BottomOfPipe, 1);
        });

        Span<ulong> ticks = stackalloc ulong[2];
        pool.GetResults(0, ticks);

        ulong mask = ValidBitsMask(validBits);
        Assert.True((ticks[1] & mask) >= (ticks[0] & mask));
    }

    [Fact]
    public void RecorderMethods_NullPool_FailUnderValidation()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family, out _);
        using var cmdPool  = new CommandBufferPool(device, family);

        // AhjoValidation.Enabled is process-global; the suite runs
        // single-threaded (xunit.runner.json: maxParallelThreads = 1).
        bool prior = AhjoValidation.Enabled;
        AhjoValidation.Enabled = true;
        try
        {
            var writeEx = Assert.Throws<AhjoValidationException>(() =>
            {
                QueryPool nullPool = default;
                using var rec = cmdPool.Begin();
                rec.WriteTimestamp(in nullPool, Stage.TopOfPipe, 0);
            });
            Assert.Contains("Device.CreateQueryPool", writeEx.Message, StringComparison.Ordinal);

            var resetEx = Assert.Throws<AhjoValidationException>(() =>
            {
                QueryPool nullPool = default;
                using var rec = cmdPool.Begin();
                rec.ResetQueryPool(in nullPool, 0, 1);
            });
            Assert.Contains("Device.CreateQueryPool", resetEx.Message, StringComparison.Ordinal);
        }
        finally
        {
            AhjoValidation.Enabled = prior;
        }
    }

    [Fact]
    public void WriteTimestamp_NonSingleBitStage_FailsUnderValidation()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family, out _);
        using var cmdPool  = new CommandBufferPool(device, family);
        using var pool     = device.CreateQueryPool(4);

        QueryPool captured = pool;
        bool prior = AhjoValidation.Enabled;
        AhjoValidation.Enabled = true;
        try
        {
            // Stage.None (zero bits) and a multi-bit mask are both invalid
            // (VUID-vkCmdWriteTimestamp2-stage-03859).
            var zeroEx = Assert.Throws<AhjoValidationException>(() =>
            {
                using var rec = cmdPool.Begin();
                rec.WriteTimestamp(in captured, Stage.None, 0);
            });
            Assert.Contains("exactly one Stage bit", zeroEx.Message, StringComparison.Ordinal);

            var multiEx = Assert.Throws<AhjoValidationException>(() =>
            {
                using var rec = cmdPool.Begin();
                rec.WriteTimestamp(in captured, Stage.TopOfPipe | Stage.BottomOfPipe, 0);
            });
            Assert.Contains("03859", multiEx.Message, StringComparison.Ordinal);
        }
        finally
        {
            AhjoValidation.Enabled = prior;
        }
    }

    [Fact]
    public void WriteTimestamp_QueryOutOfRange_FailsUnderValidation()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family, out _);
        using var cmdPool  = new CommandBufferPool(device, family);
        using var pool     = device.CreateQueryPool(4);

        QueryPool captured = pool;
        bool prior = AhjoValidation.Enabled;
        AhjoValidation.Enabled = true;
        try
        {
            var ex = Assert.Throws<AhjoValidationException>(() =>
            {
                using var rec = cmdPool.Begin();
                rec.WriteTimestamp(in captured, Stage.TopOfPipe, query: 4);
            });
            Assert.Contains("out of range", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            AhjoValidation.Enabled = prior;
        }
    }

    [Fact]
    public void TryGetResults_EmptySpan_IsTrueNoOp()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _, out _);
        using var pool     = device.CreateQueryPool(2);

        // The pool's queries were never reset, so a driver readback would be
        // a validation error (VUID-vkGetQueryPoolResults-None-09401) — the
        // empty span must return true before touching the driver.
        Assert.True(pool.TryGetResults(0, Span<ulong>.Empty));
        Assert.True(pool.TryGetResults(0, Span<QueryResult>.Empty));
        pool.GetResults(0, Span<ulong>.Empty);
    }

    [Fact]
    public void FromRawPool_Readback_Throws()
    {
        // Driverless: the borrowed-handle guard fires before any Vulkan
        // call — a FromRaw pool has no device to dispatch through.
        QueryPool borrowed = QueryPool.FromRaw(0x1234);
        ulong[] ticks = new ulong[2];
        QueryResult[] results = new QueryResult[2];

        var ex = Assert.Throws<InvalidOperationException>(() => { _ = borrowed.TryGetResults(0, ticks); });
        Assert.Contains("borrowed", ex.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => { _ = borrowed.TryGetResults(0, results.AsSpan()); });
        Assert.Throws<InvalidOperationException>(() => borrowed.GetResults(0, ticks));
    }

    // ---- Helpers ----

    private static void SkipUnlessSubmitPossible()
    {
        TestGate.RequireDriver();
        TestGate.RequireHardwareDriver(
            "Software ICD (Mesa lavapipe): vkQueueSubmit2 SIGSEGVs during command-buffer execution.");
    }

    private static void SkipUnlessValidatedSubmitPossible()
    {
        SkipUnlessSubmitPossible();
        TestGate.RequireValidationLayer();
    }

    private static void RequireTimestamps(uint validBits)
        => TestGate.RequireDeviceFeature(validBits != 0,
            "The chosen queue family reports timestampValidBits == 0 — timestamps are unsupported there "
            + "(VUID-vkCmdWriteTimestamp2-timestampValidBits-03863).");

    private static ulong ValidBitsMask(uint bits)
        => bits >= 64 ? ulong.MaxValue : (1UL << (int)bits) - 1;

    private static Instance CreateValidatedInstance(List<DebugMessage> errors)
        => Instance.Create(new InstanceDescription
        {
            ApiVersion       = VulkanVersion.V1_4,
            EnableValidation = true,
            DebugCallback    = m =>
            {
                if ((m.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) != 0)
                    lock (errors) errors.Add(m);
            },
        });

    private static void AssertNoValidationErrors(List<DebugMessage> errors)
    {
        lock (errors)
            Assert.True(errors.Count == 0,
                "Validation errors recorded: " + string.Join("; ", errors.ConvertAll(e => e.Message)));
    }

    /// <summary>
    /// Records one command buffer via <paramref name="record"/>, submits it
    /// with a fence and waits for completion — the query results the tests
    /// read afterwards are therefore final, not in flight.
    /// </summary>
    private static void SubmitAndWait(
        Queue             queue,
        CommandBufferPool cmdPool,
        FencePool         fencePool,
        ImmediateRecord   record)
    {
        Fence fence = fencePool.Acquire();
        try
        {
            var rec = cmdPool.Begin();
            try
            {
                record(ref rec);
                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(5)));
        }
        finally { fencePool.Release(fence); }
    }

    private static Buffer CreateDeviceBuffer(Device device)
        => device.Allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = ElementCount * sizeof(uint),
                Usage = BufferUsage.TransferDst,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

    private static Device CreateGraphicsDevice(Instance instance, out uint family, out uint timestampValidBits)
    {
        uint f    = uint.MaxValue;
        uint bits = 0;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    f    = info.QueueFamilies[i].Index;
                    bits = info.QueueFamilies[i].TimestampValidBits;
                    return true;
                }
            }
            return false;
        });
        family             = f;
        timestampValidBits = bits;
        return gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });
    }
}
