using System.IO;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed class AllocatorTests
{
    [Fact]
    public void Create_AllocateBuffer_DestroyBuffer_Dispose_RoundTrips()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        uint gfxFamily = PickGraphicsFamily(instance, out var gpu);

        using var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(gfxFamily, count: 1, priority: 1.0f)],
        });

        using var allocator = Allocator.Create(device);
        Assert.False(allocator.IsNull);

        var buffer = allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = 64 * 1024,
                Usage = BufferUsage.TransferSrc,
            },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
            });

        Assert.False(buffer.IsNull);
        unsafe { Assert.True(buffer.AllocationHandle != null); }

        buffer.Dispose();
    }

    [Fact]
    public void Device_AllocatorProperty_LazyAndCached()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        uint gfxFamily = PickGraphicsFamily(instance, out var gpu);

        using var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(gfxFamily, count: 1, priority: 1.0f)],
        });

        Allocator a = device.Allocator;
        Allocator b = device.Allocator;

        Assert.False(a.IsNull);
        // readonly struct equality on the wrapped pointer.
        unsafe { Assert.True(a.Handle == b.Handle); }
    }

    [Fact]
    public void Dispose_LeakedBuffer_LogsWarning()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        uint gfxFamily = PickGraphicsFamily(instance, out var gpu);

        using var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(gfxFamily, count: 1, priority: 1.0f)],
        });

        var allocator = Allocator.Create(device);
        var leaked = allocator.CreateBuffer(
            new BufferDescription { Size = 4096, Usage = BufferUsage.TransferSrc },
            new AllocationDescription { Usage = MemoryUsage.Auto });

        var originalErr = Console.Error;
        var captured   = new StringWriter();
        Console.SetError(captured);
        try
        {
            allocator.Dispose();
        }
        finally
        {
            Console.SetError(originalErr);
        }

        // Free the still-valid handles after we've already destroyed the
        // allocator: vmaDestroyAllocator on a leak path leaves them behind
        // for the driver's vkDestroyDevice to clean up. We don't call
        // DestroyBuffer here because the allocator is gone.
        _ = leaked;

        string log = captured.ToString();
        Assert.Contains("[VMA]", log);
        Assert.Contains("live allocation", log);
    }

    private static uint PickGraphicsFamily(Instance instance, out PhysicalDevice gpu)
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
        return family;
    }
}
