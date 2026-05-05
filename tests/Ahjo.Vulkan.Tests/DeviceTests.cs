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
}
