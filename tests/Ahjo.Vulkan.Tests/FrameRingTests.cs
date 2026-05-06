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
