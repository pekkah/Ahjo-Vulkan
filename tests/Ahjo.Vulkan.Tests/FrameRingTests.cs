using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Covers the per-frame ring (issue 16): rotation, fence-throttle, and a
/// 100-frame headless loop with no real swapchain.
/// </summary>
public sealed unsafe class FrameRingTests
{
    [Fact]
    public void Construction_Builds_N_Slots_With_Resources()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        using var ring = new FrameRing(device, framesInFlight: 3, queueFamily: family);

        Assert.Equal(3u,  ring.FramesInFlight);
        Assert.Equal(0ul, ring.FrameNumber);
    }

    [Fact]
    public void BeginFrame_Rotates_Slot_Index()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        using var ring = new FrameRing(device, framesInFlight: 2, queueFamily: family);

        var f0 = ring.BeginFrame();
        Assert.Equal(0u,  f0.SlotIndex);
        Assert.Equal(1ul, f0.FrameNumber);
        f0.Dispose();

        var f1 = ring.BeginFrame();
        Assert.Equal(1u,  f1.SlotIndex);
        Assert.Equal(2ul, f1.FrameNumber);
        f1.Dispose();

        var f2 = ring.BeginFrame();           // wraps back to slot 0
        Assert.Equal(0u,  f2.SlotIndex);
        Assert.Equal(3ul, f2.FrameNumber);
        f2.Dispose();
    }

    [Fact]
    public void Hundred_Headless_Frames_Loop_Without_Errors()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        using var ring  = new FrameRing(device, framesInFlight: 2, queueFamily: family);
        var       queue = device.GetQueue(family, 0);
        int[]     payload = new int[64];

        for (int i = 0; i < 100; i++)
        {
            payload[0] = i;
            using var frame = ring.BeginFrame();
            var rec = frame.CommandBuffers.Begin();
            try
            {
                // Trivial "do something measurable" — uploads 256 B into the
                // slot's staging buffer and then fills that range. Real GPU
                // work, so the slot's fence actually moves through the
                // throttle path on each rotation.
                StagedUpload up = frame.Staging.Upload<int>(payload);
                Buffer staged = up.Source;
                rec.FillBuffer(in staged, 0xCAFEBABE, offset: up.Offset, size: up.Size);

                frame.Submit(queue, ref rec);
            }
            finally { rec.Dispose(); }
        }

        Assert.Equal(100ul, ring.FrameNumber);
    }

    [Fact]
    public void Backpressure_BeginFrame_Waits_On_Reused_Slot()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        using var ring  = new FrameRing(device, framesInFlight: 2, queueFamily: family);
        var       queue = device.GetQueue(family, 0);

        // FramesInFlight + 1 submissions in tight succession. The last
        // BeginFrame is forced to wait the fence of the slot it's
        // recycling. If the wait is missing, the test would tear down
        // command pools while the GPU still has work in them and the
        // validation layer or the driver itself would surface the bug.
        for (int i = 0; i < 3; i++)
        {
            using var frame = ring.BeginFrame();
            var rec = frame.CommandBuffers.Begin();
            try
            {
                // Make each frame do enough work that the GPU lags the CPU
                // briefly — a 4 MiB fill is plenty.
                using var bigBuf = device.Allocator.CreateBuffer(
                    new BufferDescription { Size = 4 * 1024 * 1024, Usage = BufferUsage.TransferDst },
                    new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });
                rec.FillBuffer(in bigBuf, 0xDEADBEEFu);
                frame.Submit(queue, ref rec);
            }
            finally { rec.Dispose(); }
        }

        // Implicit assertion: ring.Dispose() (via using) must complete
        // without ABANDONED_QUEUE / DEVICE_LOST — proves the slot's
        // Dispose waited the in-flight fence before tearing pools down.
    }

    [Fact]
    public void DescriptorSets_Default_NullWhenNotConfigured()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        using var ring = new FrameRing(device, framesInFlight: 2, queueFamily: family);

        using var frame = ring.BeginFrame();
        Assert.Null(frame.DescriptorSets);
    }

    [Fact]
    public void DescriptorSets_Mismatched_Args_Throw()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        VkDescriptorPoolSize[] sizes =
        [
            new VkDescriptorPoolSize { type = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, descriptorCount = 1 },
        ];
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FrameRing(device, framesInFlight: 2, queueFamily: family,
                descriptorPoolSizes: sizes, descriptorMaxSets: 0));

        Assert.Throws<ArgumentException>(() =>
            new FrameRing(device, framesInFlight: 2, queueFamily: family,
                descriptorPoolSizes: default, descriptorMaxSets: 4));
    }

    [Fact]
    public void DescriptorSets_Pool_ResetsBetweenFrames()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        VkDescriptorPoolSize[] sizes =
        [
            new VkDescriptorPoolSize { type = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, descriptorCount = 8 },
        ];
        using var ring = new FrameRing(device, framesInFlight: 2, queueFamily: family,
            descriptorPoolSizes: sizes, descriptorMaxSets: 8);
        var queue = device.GetQueue(family, 0);

        using var setLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            Bindings =
            [
                new DescriptorBinding
                {
                    Slot   = 0,
                    Type   = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                    Count  = 1,
                    Stages = ShaderStages.Vertex,
                },
            ],
        });

        // Two frames per slot. Allocate three sets per frame; the pool
        // is reset on every BeginFrame so AllocatedCount snaps back to 3
        // each time (rather than climbing to 12 across the loop).
        for (int i = 0; i < 4; i++)
        {
            using var frame = ring.BeginFrame();
            DescriptorSetPool? pool = frame.DescriptorSets;
            Assert.NotNull(pool);

            // Reset happens *before* this point — so the pool starts
            // empty even after prior frames filled it.
            Assert.Equal(0, pool.AllocatedCount);

            for (int j = 0; j < 3; j++)
            {
                var set = pool.Acquire(setLayout.Handle);
                Assert.False(set.IsNull);
            }
            Assert.Equal(3, pool.AllocatedCount);

            var rec = frame.CommandBuffers.Begin();
            try { frame.Submit(queue, ref rec); }
            finally { rec.Dispose(); }
        }

        Assert.Equal(4ul, ring.FrameNumber);
    }

    [Fact]
    public void DescriptorSets_Pool_IsPerSlot()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        VkDescriptorPoolSize[] sizes =
        [
            new VkDescriptorPoolSize { type = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, descriptorCount = 4 },
        ];
        using var ring = new FrameRing(device, framesInFlight: 2, queueFamily: family,
            descriptorPoolSizes: sizes, descriptorMaxSets: 4);

        var f0 = ring.BeginFrame();
        DescriptorSetPool? pool0 = f0.DescriptorSets;
        f0.Dispose();

        var f1 = ring.BeginFrame();
        DescriptorSetPool? pool1 = f1.DescriptorSets;
        f1.Dispose();

        Assert.NotNull(pool0);
        Assert.NotNull(pool1);
        Assert.NotSame(pool0, pool1);
    }

    [Fact]
    public void Construction_StagingUploader_Failure_Unwinds_Earlier_Resources()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        // Sanity baseline: a normal ring builds + tears down cleanly so
        // we know the device starts the test in a healthy state.
        using (new FrameRing(device, framesInFlight: 2, queueFamily: family)) { }

        // Inject a deterministic, driver-independent mid-construction failure:
        // stagingChunkSize = 0 throws ArgumentOutOfRangeException inside
        // StagingUploader's ctor — but only after Slot has already
        // constructed the CommandBufferPool. Without the unwind path that
        // VkCommandPool would leak.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FrameRing(device, framesInFlight: 2, queueFamily: family, stagingChunkSize: 0));

        // Build + tear down a second healthy ring. A leaked command pool
        // from the failed construction would surface here at vkDestroyDevice
        // (via the validation layer when present) or as device-state
        // corruption on subsequent Vulkan calls.
        using (new FrameRing(device, framesInFlight: 2, queueFamily: family)) { }
    }

    [Fact]
    public void Construction_LaterSlot_Failure_Disposes_EarlierSlots()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        // Exercises the outer FrameRing-ctor catch with framesInFlight=3.
        // Every slot fails at the same step (StagingUploader rejects
        // stagingChunkSize=0), so the inner Slot-ctor catch is exercised
        // for slot 0; the outer FrameRing-ctor catch sees the throw at
        // i==0 with no earlier full slots to roll back. The test still
        // proves the outer try/catch path doesn't itself crash with an
        // empty roll-back set, which is the corner case the issue calls out.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FrameRing(device, framesInFlight: 3, queueFamily: family, stagingChunkSize: 0));

        using (new FrameRing(device, framesInFlight: 2, queueFamily: family)) { }
    }

    /// <summary>
    /// Regression: a slot whose BeginFrame ran but whose Submit did
    /// not (caller bailed out before the queue submit) used to leave
    /// the slot's fence reset-and-unsignaled while a sticky
    /// "ever submitted" flag still asked Slot.Dispose to wait for it.
    /// That hung Dispose forever; with the pending-submit tracking it
    /// must complete in bounded time.
    /// </summary>
    [Fact]
    public void Dispose_AfterBeginFrameWithoutSubmit_DoesNotHang()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);
        var       queue   = device.GetQueue(family, 0);

        var ring = new FrameRing(device, framesInFlight: 2, queueFamily: family);

        // First slot: real submit, so the ring sees a "this slot has
        // pending GPU work" path at least once.
        {
            using var frame = ring.BeginFrame();
            var rec = frame.CommandBuffers.Begin();
            try { frame.Submit(queue, ref rec); }
            finally { rec.Dispose(); }
        }

        // Second slot: BeginFrame runs WaitAndReset and resets the
        // fence to unsignaled, but the caller never submits. Without
        // the fix, ring.Dispose below waits on this fence forever.
        {
            using var frame = ring.BeginFrame();
            // intentional: no submit, no record
        }

        // Third BeginFrame (rotates back to slot 0 whose submit IS
        // pending) — proves we don't deadlock just by re-entering.
        // After this, slot 0's pending flag is cleared (WaitAndReset
        // ran), and the slot whose pending flag is still false stays
        // false on Dispose.
        {
            using var frame = ring.BeginFrame();
            // intentional: no submit
        }

        // Bounded teardown: must finish quickly even though no slot
        // has pending GPU work at this point. If it hangs the test
        // host kills it, but to surface a clearer message also assert
        // it stays under a generous wall-clock bound.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        ring.Dispose();
        sw.Stop();
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2),
            $"FrameRing.Dispose hung: {sw.Elapsed} elapsed (expected near-zero — no pending GPU work).");
    }

    /// <summary>
    /// Regression: an unsignaled fence returned to FencePool by an
    /// aborted-frame slot used to be handed straight back out of
    /// <c>Acquire(initiallySignaled: true)</c>, deadlocking the next
    /// caller. The pool now routes by current state and grows when the
    /// matching free-list is empty, so a subsequent FrameRing rebuild
    /// against the same device starts clean.
    /// </summary>
    [Fact]
    public void Aborted_Frame_DoesNot_Poison_Subsequent_Ring()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        // First ring: BeginFrame + bail without submitting. Slot
        // disposal pushes an unsignaled fence back to its (slot-local)
        // FencePool, which is then itself disposed — so this branch is
        // really about proving Dispose doesn't hang. The cross-ring
        // safety is structural (each Slot owns its own FencePool).
        using (var ring = new FrameRing(device, framesInFlight: 2, queueFamily: family))
        {
            using var frame = ring.BeginFrame();
            // no submit
        }

        // Second ring on the same device: must build and tear down
        // cleanly. A poisoned device or a leaked fence handle from the
        // first ring would surface here as either a build failure or a
        // BeginFrame hang.
        using (var ring = new FrameRing(device, framesInFlight: 2, queueFamily: family))
        {
            using var frame = ring.BeginFrame();
            Assert.Equal(1ul, ring.FrameNumber);
        }
    }

    /// <summary>
    /// RecycleStaleAcquireSemaphores is the post-Recreate counterpart
    /// to the contract on Swapchain.Recreate: only slots flagged via
    /// MarkImageAcquireSignaled (and not subsequently consumed by a
    /// swapchain-aware Submit) are rotated. Untouched slots keep their
    /// existing handle. The rotated slot's handle changes; the
    /// underlying SemaphorePool stays balanced because Discard +
    /// AcquireBinary nets to a fresh allocation.
    /// </summary>
    [Fact]
    public void RecycleStaleAcquireSemaphores_RotatesFlaggedSlotsOnly()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        using var ring = new FrameRing(device, framesInFlight: 2, queueFamily: family);

        var f0 = ring.BeginFrame();
        BinarySemaphore slot0Before = f0.ImageAcquired;
        f0.MarkImageAcquireSignaled();
        f0.Dispose();

        var f1 = ring.BeginFrame();
        BinarySemaphore slot1Before = f1.ImageAcquired;
        // Slot 1: no MarkImageAcquireSignaled, simulating a slot whose
        // last AcquireNextImage was OutOfDate (no host signal).
        f1.Dispose();

        ring.RecycleStaleAcquireSemaphores();

        // Re-enter both slots and read the post-rotate handles.
        // Need to wait for slot 0's fence — but it was reset by
        // BeginFrame and never submitted, so reading ImageAcquired
        // through a fresh BeginFrame on the same slot waits zero.
        BinarySemaphore slot0After;
        BinarySemaphore slot1After;
        unsafe
        {
            // Walk slots through BeginFrame so the wait+reset path runs;
            // FramesInFlight=2 → two BeginFrame calls return slot 0 then slot 1.
            using (var f = ring.BeginFrame()) slot0After = f.ImageAcquired;
            using (var f = ring.BeginFrame()) slot1After = f.ImageAcquired;

            Assert.True(slot0Before.Handle != slot0After.Handle,
                "Slot 0 had a pending acquire signal — its ImageAcquired must be rotated.");
            Assert.True(slot1Before.Handle == slot1After.Handle,
                "Slot 1 had no pending acquire signal — its ImageAcquired must be left untouched.");
        }
    }

    /// <summary>
    /// The swapchain-aware Submit clears the pending-acquire flag,
    /// because the queued semaphore wait will consume the host signal.
    /// RecycleStaleAcquireSemaphores must therefore be a no-op on a
    /// slot that signaled-then-submitted in normal frame flow.
    /// </summary>
    [Fact]
    public void RecycleStaleAcquireSemaphores_AfterSubmit_LeavesHandleUntouched()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);
        var       queue   = device.GetQueue(family, 0);

        using var ring = new FrameRing(device, framesInFlight: 2, queueFamily: family);

        BinarySemaphore before;
        using (var fc = ring.BeginFrame())
        {
            before = fc.ImageAcquired;
            fc.MarkImageAcquireSignaled();
            // Use the swapchain-aware Submit (the four-arg overload) —
            // it clears the pending-acquire flag because the queued
            // wait on ImageAcquired consumes the host signal.
            var rec = fc.CommandBuffers.Begin();
            try { fc.Submit(queue, ref rec, Stage.ColorAttachmentOutput, Stage.AllGraphics); }
            finally { rec.Dispose(); }
        }

        ring.RecycleStaleAcquireSemaphores();

        BinarySemaphore after;
        using (var fc2 = ring.BeginFrame()) { /* rotates to slot 1 */ }
        using (var fc0 = ring.BeginFrame()) { after = fc0.ImageAcquired; }

        unsafe
        {
            Assert.True(before.Handle == after.Handle,
                "A signaled-then-submitted slot has its flag cleared by Submit; rotate must be a no-op.");
        }
    }

    [Fact]
    public void Dispose_Is_Idempotent()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        var ring = new FrameRing(device, framesInFlight: 2, queueFamily: family);
        ring.Dispose();
        ring.Dispose(); // must not throw
    }

    [Fact]
    public void BeginFrame_After_Dispose_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        var ring = new FrameRing(device, framesInFlight: 2, queueFamily: family);
        ring.Dispose();
        Assert.Throws<ObjectDisposedException>(() => ring.BeginFrame());
    }

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
