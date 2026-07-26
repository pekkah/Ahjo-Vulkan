using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Covers the sync2 split-barrier surface (#155): <see cref="Event"/>,
/// <see cref="Device.CreateEvent"/>, and
/// <see cref="CommandRecorder.SetEvent"/> /
/// <see cref="CommandRecorder.WaitEvent"/> /
/// <see cref="CommandRecorder.ResetEvent"/>.
/// </summary>
/// <remarks>
/// <para>The submitting tests run with <c>VK_LAYER_KHRONOS_validation</c>
/// loaded and assert the layer logged no errors. That layer is the <em>only</em>
/// oracle for most of this surface: a single-queue fill→copy sequence produces
/// the correct bytes on a desktop driver with or without the barrier, and
/// <see cref="CommandRecorder.ResetEvent"/> has no observable effect on the
/// data at all. With the layer on, these become real checks of
/// <c>VUID-vkCmdWaitEvents2-pEvents-10788</c> (the Set and the Wait must record
/// exactly equal dependency infos), <c>-pEvents-03841</c>,
/// <c>VUID-vkCmdResetEvent2-event-03832</c>, the <c>-renderpass</c> rules and
/// the <c>-cmdpool</c> queue-capability rules.</para>
/// <para><see cref="WaitEvent_MismatchedDependency_TripsValidation"/> is the
/// negative control: it proves the 10788 oracle is live rather than silently
/// passing, so the positive tests' empty-error assertion means something.</para>
/// <para>Every test skips without a real driver; the submitting ones also skip
/// on a software ICD (issue #32) and without the validation layer installed.</para>
/// </remarks>
public sealed unsafe class SplitBarrierTests
{
    private const int  ElementCount = 256;
    private const uint FirstValue   = 0xA5A5A5A5u;
    private const uint SecondValue  = 0x5A5A5A5Au;

    [Fact]
    public void CreateEvent_DeviceOnly_IsOwningAndDisposes()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);

        using (var evt = device.CreateEvent())
        {
            Assert.False(evt.IsNull);
            Assert.True(evt.IsDeviceOnly);
            Assert.True(evt.OwnsHandle);
        }

        using (var hostCapable = device.CreateEvent(EventCreateFlags.None))
        {
            Assert.False(hostCapable.IsNull);
            Assert.False(hostCapable.IsDeviceOnly);
            Assert.True(hostCapable.OwnsHandle);
        }
    }

    [Fact]
    public void SetEvent_WaitEvent_Pair_Orders_Fill_Before_Copy()
    {
        SkipUnlessValidatedSubmitPossible();

        var errors = new List<DebugMessage>();
        using var instance = CreateValidatedInstance(errors);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        using var src = CreateDeviceSource(device);
        using var dst = CreateHostReadback(device);
        using var evt = device.CreateEvent();

        using var cmdPool   = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        Queue queue = device.GetQueue(family, queueIndex: 0);

        // ONE array, passed to both halves — the 10788 pairing contract.
        MemoryBarrier[] bars = TransferBarriers();

        RunFillSetWaitCopy(queue, cmdPool, fencePool, in evt, in src, in dst, FirstValue, bars, bars);

        ReadOnlySpan<uint> data = dst.AsReadOnlySpan<uint>();
        for (int i = 0; i < ElementCount; i++)
            Assert.Equal(FirstValue, data[i]);

        AssertNoValidationErrors(errors);
    }

    [Fact]
    public void ResetEvent_InLaterSubmission_AllowsReuse()
    {
        SkipUnlessValidatedSubmitPossible();

        var errors = new List<DebugMessage>();
        using var instance = CreateValidatedInstance(errors);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        using var src = CreateDeviceSource(device);
        using var dst = CreateHostReadback(device);
        using var evt = device.CreateEvent();

        using var cmdPool   = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        Queue queue = device.GetQueue(family, queueIndex: 0);

        MemoryBarrier[] bars = TransferBarriers();

        RunFillSetWaitCopy(queue, cmdPool, fencePool, in evt, in src, in dst, FirstValue, bars, bars);
        ReadOnlySpan<uint> first = dst.AsReadOnlySpan<uint>();
        for (int i = 0; i < ElementCount; i++)
            Assert.Equal(FirstValue, first[i]);

        // The reset must live in a submission ordered AFTER the wait
        // completed — never in the same command buffer as the wait
        // (VUID-vkCmdResetEvent2-event-03832). ImmediateSubmit's WaitIdle
        // also guarantees the previous submission has retired.
        //
        // The data assertions below cannot detect a broken reset on their
        // own: if ResetEvent were a no-op the event would stay signaled from
        // round 1, round 2's wait would be satisfied by the stale signal, and
        // the copy would still land SecondValue. The validation layer is what
        // makes this a real recycling test.
        Event recycled = evt;
        queue.ImmediateSubmit(cmdPool, (ref CommandRecorder r) =>
        {
            r.ResetEvent(in recycled, Stage.AllTransfer);
        });

        RunFillSetWaitCopy(queue, cmdPool, fencePool, in evt, in src, in dst, SecondValue, bars, bars);
        ReadOnlySpan<uint> second = dst.AsReadOnlySpan<uint>();
        for (int i = 0; i < ElementCount; i++)
            Assert.Equal(SecondValue, second[i]);

        AssertNoValidationErrors(errors);
    }

    /// <summary>
    /// Negative control for the 10788 oracle. Passing a <em>different</em>
    /// barrier list to <see cref="CommandRecorder.WaitEvent"/> than the one
    /// given to <see cref="CommandRecorder.SetEvent"/> must make the
    /// validation layer fire — which is what proves the no-errors assertion in
    /// the positive tests is checking a live oracle rather than a silent one,
    /// and that the shared <c>RecordDependency</c> marshalling is what keeps
    /// the matched case clean.
    /// </summary>
    [Fact]
    public void WaitEvent_MismatchedDependency_TripsValidation()
    {
        SkipUnlessValidatedSubmitPossible();

        var errors = new List<DebugMessage>();
        using var instance = CreateValidatedInstance(errors);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        using var src = CreateDeviceSource(device);
        using var dst = CreateHostReadback(device);
        using var evt = device.CreateEvent();

        using var cmdPool   = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);
        Queue queue = device.GetQueue(family, queueIndex: 0);

        MemoryBarrier[] setBars = TransferBarriers();
        MemoryBarrier[] waitBars =
        [
            MemoryBarrier.Between(
                Stage.ComputeShader,  Access.ShaderStorageWrite,
                Stage.FragmentShader, Access.ShaderSampledRead),
        ];

        // Deliberately mismatched. The work still executes (the event is
        // signaled and waited), so this does not hang — the layer reports the
        // violation at vkQueueSubmit2 time.
        RunFillSetWaitCopy(queue, cmdPool, fencePool, in evt, in src, in dst, FirstValue, setBars, waitBars);

        lock (errors)
        {
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Message.Contains("10788", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void SetEvent_NullEvent_FailsUnderValidation()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);
        using var pool     = new CommandBufferPool(device, family);

        MemoryBarrier[] bars = TransferBarriers();

        // AhjoValidation.Enabled is process-global; the suite runs
        // single-threaded (xunit.runner.json: maxParallelThreads = 1).
        bool prior = AhjoValidation.Enabled;
        AhjoValidation.Enabled = true;
        try
        {
            var ex = Assert.Throws<AhjoValidationException>(() =>
            {
                Event nullEvent = default;
                using var rec = pool.Begin();
                rec.SetEvent(in nullEvent, bars, default, default);
            });
            Assert.Contains("Device.CreateEvent()", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            AhjoValidation.Enabled = prior;
        }
    }

    [Fact]
    public void SetEvent_EmptyDependency_FailsUnderValidation()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);
        using var pool     = new CommandBufferPool(device, family);
        using var evt      = device.CreateEvent();

        // An all-empty mix is NOT dropped the way PipelineBarrier drops it —
        // that would discard the signal and hang the paired wait. Validation
        // rejects it instead.
        Event captured = evt;
        bool prior = AhjoValidation.Enabled;
        AhjoValidation.Enabled = true;
        try
        {
            var ex = Assert.Throws<AhjoValidationException>(() =>
            {
                using var rec = pool.Begin();
                rec.SetEvent(in captured, default, default, default);
            });
            Assert.Contains("the dependency is empty", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            AhjoValidation.Enabled = prior;
        }
    }

    [Fact]
    public void ResetEvent_NullEvent_FailsUnderValidation()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);
        using var pool     = new CommandBufferPool(device, family);

        // VUID-vkCmdResetEvent2-event-parameter has no VK_NULL_HANDLE
        // exemption, so ResetEvent rejects a null handle for the same reason
        // SetEvent/WaitEvent do.
        bool prior = AhjoValidation.Enabled;
        AhjoValidation.Enabled = true;
        try
        {
            var ex = Assert.Throws<AhjoValidationException>(() =>
            {
                Event nullEvent = default;
                using var rec = pool.Begin();
                rec.ResetEvent(in nullEvent, Stage.AllTransfer);
            });
            Assert.Contains("Device.CreateEvent()", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            AhjoValidation.Enabled = prior;
        }
    }

    // ---- Helpers ----

    private static void SkipUnlessValidatedSubmitPossible()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipWhen(VulkanDriverProbe.IsSoftwareDriver,
            "Software ICD (Mesa lavapipe): vkQueueSubmit2 SIGSEGVs during command-buffer execution.");
        Assert.SkipUnless(VulkanDriverProbe.HasValidationLayer, "Validation layer not installed.");
    }

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

    private static MemoryBarrier[] TransferBarriers() =>
    [
        MemoryBarrier.Between(
            Stage.AllTransfer, Access.TransferWrite,
            Stage.AllTransfer, Access.TransferRead),
    ];

    /// <summary>
    /// Fill the device-local source, split-barrier the fill against the
    /// copy, then copy into the host-visible readback and fence-wait.
    /// <paramref name="setBars"/> and <paramref name="waitBars"/> are separate
    /// parameters solely so
    /// <see cref="WaitEvent_MismatchedDependency_TripsValidation"/> can pass
    /// unequal lists; every other caller passes the same array twice, which is
    /// the documented 10788 idiom.
    /// </summary>
    private static void RunFillSetWaitCopy(
        Queue             queue,
        CommandBufferPool cmdPool,
        FencePool         fencePool,
        in Event          evt,
        in Buffer         src,
        in Buffer         dst,
        uint              value,
        MemoryBarrier[]   setBars,
        MemoryBarrier[]   waitBars)
    {
        Fence fence = fencePool.Acquire();
        try
        {
            var rec = cmdPool.Begin();
            try
            {
                rec.FillBuffer(in src, value);
                rec.SetEvent(in evt, setBars, default, default);
                rec.WaitEvent(in evt, waitBars, default, default);
                rec.CopyBuffer(in src, in dst);
                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(5)));
        }
        finally { fencePool.Release(fence); }
    }

    private static Buffer CreateDeviceSource(Device device)
        => device.Allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = ElementCount * sizeof(uint),
                Usage = BufferUsage.TransferDst | BufferUsage.TransferSrc,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

    private static Buffer CreateHostReadback(Device device)
        => device.Allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = ElementCount * sizeof(uint),
                Usage = BufferUsage.TransferDst,
            },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessRandom | AllocationFlags.Mapped,
            });

    private static Device CreateGraphicsDevice(Instance instance, out uint family)
    {
        uint f = uint.MaxValue;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    f = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });
        family = f;
        return gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });
    }
}
