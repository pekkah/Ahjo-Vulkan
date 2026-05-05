using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed class CommandBufferPoolTests
{
    [Fact]
    public void Begin_End_Allocates_Once()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        uint family = PickGraphicsFamily(instance, out var gpu);

        using var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });

        using var pool = new CommandBufferPool(device, family);

        Assert.Equal(family, pool.QueueFamilyIndex);
        Assert.Equal(0, pool.AllocatedCount);

        using (var rec = pool.Begin())
        {
            Assert.False(rec.IsNull);
            Assert.Equal(1, pool.OutstandingCount);
        }

        Assert.Equal(0, pool.OutstandingCount);
        Assert.Equal(1, pool.AllocatedCount);
    }

    [Fact]
    public void Begin_Twice_AllocatesTwo_BeforeReset()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        uint family = PickGraphicsFamily(instance, out var gpu);
        using var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });
        using var pool = new CommandBufferPool(device, family);

        // No reset between Begin/Dispose pairs — the spent buffer can't be
        // reused this frame, so the pool grows on the second Begin.
        using (pool.Begin()) { }
        using (pool.Begin()) { }

        Assert.Equal(2, pool.AllocatedCount);
    }

    [Fact]
    public void ResetForFrame_RecyclesSpentBuffers()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        uint family = PickGraphicsFamily(instance, out var gpu);
        using var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });
        using var pool = new CommandBufferPool(device, family);

        // Frame 1: warmup.
        using (pool.Begin()) { }
        using (pool.Begin()) { }
        int afterFrame1 = pool.AllocatedCount;

        pool.ResetForFrame();

        // Frame 2: same number of begins should hit the recycle path.
        using (pool.Begin()) { }
        using (pool.Begin()) { }
        Assert.Equal(afterFrame1, pool.AllocatedCount);
    }

    [Fact]
    public void Begin_AfterDispose_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        uint family = PickGraphicsFamily(instance, out var gpu);
        using var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });

        var pool = new CommandBufferPool(device, family);
        pool.Dispose();

        Assert.Throws<ObjectDisposedException>(() => pool.Begin());
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
