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
    public void CreateDevice_DuplicateFamilyIndex_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        uint gfxFamily = PickGraphicsFamily(instance, out var gpu);

        // VUID-VkDeviceCreateInfo-queueFamilyIndex-02802 — duplicates rejected.
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            var d = new DeviceDescription
            {
                Queues =
                [
                    new QueueRequest(gfxFamily, count: 1, priority: 1.0f),
                    new QueueRequest(gfxFamily, count: 1, priority: 0.5f),
                ],
            };
            gpu.CreateDevice(in d);
        });
        Assert.Contains($"family {gfxFamily}", ex.Message);
        Assert.Contains("merge", ex.Message);
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
            ConfigureFeatures = (
                ref ChainBuilder<VkDeviceCreateInfo> _,
                ref VkPhysicalDeviceFeatures2        _,
                ref VkPhysicalDeviceVulkan12Features _,
                ref VkPhysicalDeviceVulkan13Features _,
                ref VkPhysicalDeviceVulkan14Features _) => invoked = true,
        };

        using var device = gpu.CreateDevice(in desc);
        Assert.True(invoked);
    }

    /// <summary>
    /// The configurer receives ref access to the wrapper's pre-pushed
    /// feature structs (issue 53). The wrapper's defaults must be visible
    /// (<c>synchronization2</c>, <c>dynamicRendering</c>,
    /// <c>bufferDeviceAddress</c>, <c>timelineSemaphore</c>,
    /// <c>separateDepthStencilLayouts</c>, <c>pushDescriptor</c>) so a
    /// caller can confirm what's already on before flipping additional
    /// bits — the canonical use case is "enable maintenance4 on top of
    /// the wrapper's set".
    /// </summary>
    [Fact]
    public void CreateDevice_ConfigureFeaturesCallback_SeesWrapperDefaultsOnRefs()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipWhen(VulkanDriverProbe.IsSoftwareDriver,
            "Software ICD (SwiftShader Linux): reports Vulkan 1.3, so the wrapper's 1.4 features " +
            "struct is intentionally omitted from the create chain (see PhysicalDevice.CreateDevice " +
            "and the f14 gate in commit 1d46b60). The pushDescriptor assertion at the bottom checks a " +
            "1.4-promoted feature and only makes sense on a 1.4+ device.");

        using var instance = Instance.Create(default);
        uint gfxFamily = PickGraphicsFamily(instance, out var gpu);

        VkPhysicalDeviceVulkan12Features sawF12 = default;
        VkPhysicalDeviceVulkan13Features sawF13 = default;
        VkPhysicalDeviceVulkan14Features sawF14 = default;
        var desc = new DeviceDescription
        {
            Queues = [new QueueRequest(gfxFamily, count: 1, priority: 1.0f)],
            ConfigureFeatures = (
                ref ChainBuilder<VkDeviceCreateInfo> _,
                ref VkPhysicalDeviceFeatures2        _,
                ref VkPhysicalDeviceVulkan12Features f12,
                ref VkPhysicalDeviceVulkan13Features f13,
                ref VkPhysicalDeviceVulkan14Features f14) =>
            {
                sawF12 = f12;
                sawF13 = f13;
                sawF14 = f14;
            },
        };
        using var device = gpu.CreateDevice(in desc);

        Assert.Equal(1u, sawF12.bufferDeviceAddress);
        Assert.Equal(1u, sawF12.timelineSemaphore);
        Assert.Equal(1u, sawF12.separateDepthStencilLayouts);
        Assert.Equal(1u, sawF13.synchronization2);
        Assert.Equal(1u, sawF13.dynamicRendering);
        Assert.Equal(1u, sawF14.pushDescriptor);
    }

    /// <summary>
    /// The wrapper pre-pushes <c>VkPhysicalDeviceVulkan1{2,3,4}Features</c>
    /// onto the device-create chain. A configurer that pushes its own copy
    /// produces two nodes with the same sType — the Vulkan spec disallows
    /// this. CreateDevice must reject the chain up front with a clear
    /// error rather than letting the driver fail on it (or worse, undefined
    /// behavior).
    /// </summary>
    [Fact]
    public void CreateDevice_DuplicateVulkan13Features_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        uint gfxFamily = PickGraphicsFamily(instance, out var gpu);

        var ex = Assert.Throws<ArgumentException>(() =>
        {
            var d = new DeviceDescription
            {
                Queues = [new QueueRequest(gfxFamily, count: 1, priority: 1.0f)],
                ConfigureFeatures = (
                    ref ChainBuilder<VkDeviceCreateInfo> chain,
                    ref VkPhysicalDeviceFeatures2        _,
                    ref VkPhysicalDeviceVulkan12Features _,
                    ref VkPhysicalDeviceVulkan13Features _,
                    ref VkPhysicalDeviceVulkan14Features _) =>
                {
                    ref var dup = ref chain.Push<VkPhysicalDeviceVulkan13Features>();
                    dup.synchronization2 = 1;
                },
            };
            gpu.CreateDevice(in d);
        });
        Assert.Contains("PHYSICAL_DEVICE_VULKAN_1_3_FEATURES", ex.Message);
        Assert.Contains("ConfigureFeatures", ex.Message);
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
