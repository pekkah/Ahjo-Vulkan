using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed unsafe class DeviceTests
{
    [Fact]
    public void CreateDevice_DefaultDescription_OneGraphicsQueue()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        uint gfxFamily = uint.MaxValue;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    gfxFamily = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });

        Assert.NotEqual(uint.MaxValue, gfxFamily);

        var desc = new DeviceDescription
        {
            Queues = [new QueueRequest(gfxFamily, count: 1, priority: 1.0f)],
        };

        using var device = gpu.CreateDevice(in desc);

        Queue gfx = device.GetQueue(gfxFamily, queueIndex: 0);
        Assert.False(gfx.IsNull);
        Assert.Same(device, gfx.Device);
    }

    [Fact]
    public void CreateDevice_EmptyQueues_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        var gpu = instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => true);

        // ref struct can't be captured in a lambda; build it inside the throwing call.
        Assert.Throws<ArgumentException>(() =>
        {
            var d = default(DeviceDescription);
            gpu.CreateDevice(in d);
        });
    }

    [Fact]
    public void CreateDevice_BogusFamilyIndex_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        var gpu = instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => true);

        var ex = Assert.Throws<ArgumentException>(() =>
        {
            var d = new DeviceDescription
            {
                Queues = [new QueueRequest(familyIndex: 99, count: 1, priority: 0.5f)],
            };
            gpu.CreateDevice(in d);
        });
        Assert.Contains("FamilyIndex 99", ex.Message);
    }

    [Fact]
    public void CreateDevice_QueueOversubscribed_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        uint gfxFamily = uint.MaxValue;
        uint gfxAvail  = 0;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    gfxFamily = info.QueueFamilies[i].Index;
                    gfxAvail  = info.QueueFamilies[i].QueueCount;
                    return true;
                }
            }
            return false;
        });

        var ex = Assert.Throws<ArgumentException>(() =>
        {
            var d = new DeviceDescription
            {
                Queues = [new QueueRequest(gfxFamily, count: gfxAvail + 1, priority: 0.5f)],
            };
            gpu.CreateDevice(in d);
        });
        Assert.Contains("requests", ex.Message);
    }

    [Fact]
    public void CreateDevice_GetQueueByFamilyIndex_ReturnsCachedInstance()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        uint gfxFamily = PickGraphicsFamily(instance, out var gpu);

        using var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(gfxFamily, count: 1, priority: 1.0f)],
        });

        Queue a = device.GetQueue(gfxFamily, queueIndex: 0);
        Queue b = device.GetQueue(gfxFamily, queueIndex: 0);

        Assert.Same(a, b);
    }

    [Fact]
    public void CreateDevice_GetQueue_UnknownSlot_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        uint gfxFamily = PickGraphicsFamily(instance, out var gpu);

        using var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(gfxFamily, count: 1, priority: 1.0f)],
        });

        var ex = Assert.Throws<ArgumentException>(() => device.GetQueue(gfxFamily, queueIndex: 5));
        Assert.Contains("No queue requested", ex.Message);
    }

    [Fact]
    public void CreateDevice_ConfigureFeaturesCallback_Invoked()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        uint gfxFamily = PickGraphicsFamily(instance, out var gpu);

        bool invoked = false;
        var desc = new DeviceDescription
        {
            Queues = [new QueueRequest(gfxFamily, count: 1, priority: 1.0f)],
            ConfigureFeatures = (ref ChainBuilder<VkDeviceCreateInfo> _) => invoked = true,
        };

        using var device = gpu.CreateDevice(in desc);
        Assert.True(invoked);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        uint gfxFamily = PickGraphicsFamily(instance, out var gpu);

        var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(gfxFamily, count: 1, priority: 1.0f)],
        });

        device.Dispose();
        device.Dispose();
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
