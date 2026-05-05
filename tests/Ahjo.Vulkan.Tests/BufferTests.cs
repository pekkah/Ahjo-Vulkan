using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed class BufferTests
{
    [Fact]
    public void Default_IsNull_DisposeIsNoOp()
    {
        Buffer b = default;
        Assert.True(b.IsNull);
        b.Dispose(); // no throw
    }

    [Fact]
    public void CreateBuffer_HostVisible_ReportsMetadata()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device = CreateGraphicsDevice(instance, out _);
        var allocator = device.Allocator;

        using var buffer = allocator.CreateBuffer(
            new BufferDescription { Size = 4096, Usage = BufferUsage.TransferSrc },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
            });

        Assert.False(buffer.IsNull);
        Assert.Equal(4096UL, buffer.Size);
        Assert.Equal(BufferUsage.TransferSrc, buffer.Usage);
        Assert.True(buffer.IsHostVisible);
    }

    [Fact]
    public void GetDeviceAddress_ReturnsNonZero_WhenShaderDeviceAddressUsageSet()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device = CreateGraphicsDevice(instance, out _);
        var allocator = device.Allocator;

        using var buffer = allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = 256,
                Usage = BufferUsage.StorageBuffer | BufferUsage.ShaderDeviceAddress,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        ulong addr = buffer.GetDeviceAddress(device);
        Assert.NotEqual(0UL, addr);
    }

    [Fact]
    public void Dispose_FreesAllocator_NoLeakWarning()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device = CreateGraphicsDevice(instance, out _);

        var allocator = Allocator.Create(device);
        var buffer = allocator.CreateBuffer(
            new BufferDescription { Size = 1024, Usage = BufferUsage.TransferDst },
            new AllocationDescription { Usage = MemoryUsage.Auto });
        buffer.Dispose();

        var originalErr = Console.Error;
        var captured   = new StringWriter();
        Console.SetError(captured);
        try { allocator.Dispose(); }
        finally { Console.SetError(originalErr); }

        Assert.DoesNotContain("[VMA]", captured.ToString());
    }

    private static Device CreateGraphicsDevice(Instance instance, out PhysicalDevice gpu)
    {
        uint family = uint.MaxValue;
        gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
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
