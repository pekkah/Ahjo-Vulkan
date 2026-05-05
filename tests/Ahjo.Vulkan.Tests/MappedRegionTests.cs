using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed class MappedRegionTests
{
    [Fact]
    public void Map_HostVisible_WriteRead_Roundtrips()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        using var buffer = device.Allocator.CreateBuffer(
            new BufferDescription { Size = 4096, Usage = BufferUsage.TransferSrc },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite,
            });

        using (var region = buffer.Map<float>())
        {
            Span<float> floats = region.GetSpan();
            Assert.Equal(1024, floats.Length);
            for (int i = 0; i < floats.Length; i++)
                floats[i] = i * 0.5f;
        }

        // Re-map and verify. AutoPreferHost allocation lands in cached host
        // memory, so the second mapping observes the prior writes.
        using (var region = buffer.Map<float>())
        {
            Span<float> floats = region.GetSpan();
            for (int i = 0; i < floats.Length; i++)
                Assert.Equal(i * 0.5f, floats[i]);
        }
    }

    [Fact]
    public void PersistentMapped_AsSpan_NoExplicitMap()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        using var buffer = device.Allocator.CreateBuffer(
            new BufferDescription { Size = 4096, Usage = BufferUsage.TransferSrc },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
            });

        unsafe { Assert.True(buffer.PersistentMapped != null); }

        Span<int> ints = buffer.AsSpan<int>();
        Assert.Equal(1024, ints.Length);
        ints[0] = 0xDEADBEEF.GetHashCode();
        Assert.Equal(0xDEADBEEF.GetHashCode(), buffer.AsReadOnlySpan<int>()[0]);
    }

    [Fact]
    public void PersistentMapped_Map_ReusesPointer_NoMapCall()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        using var buffer = device.Allocator.CreateBuffer(
            new BufferDescription { Size = 1024, Usage = BufferUsage.TransferSrc },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
            });

        // Persistent-mapped: Map() returns a region wrapping pMappedData,
        // and Dispose is a no-op. We can repeat without VMA refcount churn.
        for (int i = 0; i < 3; i++)
        {
            using var region = buffer.Map<byte>();
            Span<byte> span = region.GetSpan();
            Assert.Equal(1024, span.Length);
            unsafe
            {
                fixed (byte* p = span)
                    Assert.True(p == buffer.PersistentMapped);
            }
        }
    }

    [Fact]
    public void Map_NonHostVisible_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        using var buffer = device.Allocator.CreateBuffer(
            new BufferDescription { Size = 256, Usage = BufferUsage.StorageBuffer },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        if (buffer.IsHostVisible)
        {
            // Some integrated GPUs land AutoPreferDevice in shared host-visible
            // memory; in that case the throw isn't reachable. Skip rather than
            // misreport.
            return;
        }

        Assert.Throws<InvalidOperationException>(() => buffer.Map<byte>());
    }

    [Fact]
    public void AsSpan_NonPersistent_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        using var buffer = device.Allocator.CreateBuffer(
            new BufferDescription { Size = 256, Usage = BufferUsage.TransferSrc },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite, // no Mapped
            });

        Assert.Throws<InvalidOperationException>(() => buffer.AsSpan<float>());
    }

    [Fact]
    public void Map_MemoryProperty_RoundtripsThroughAsyncBoundary()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        using var buffer = device.Allocator.CreateBuffer(
            new BufferDescription { Size = 4096, Usage = BufferUsage.TransferSrc },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
            });

        using var region = buffer.Map<int>();
        Memory<int> mem = region.Memory;
        mem.Span[0] = 42;
        Assert.Equal(42, mem.Span[0]);
    }

    private static Device CreateGraphicsDevice(Instance instance)
    {
        uint family = uint.MaxValue;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
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
        return gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });
    }
}
