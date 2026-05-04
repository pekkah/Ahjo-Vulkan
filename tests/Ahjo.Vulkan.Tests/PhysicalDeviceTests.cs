using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed unsafe class PhysicalDeviceTests
{
    [Fact]
    public void Pick_AcceptAny_ReturnsFirstDevice()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        PhysicalDevice gpu = instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => true);

        Assert.False(gpu.IsNull);
    }

    [Fact]
    public void Pick_NoMatch_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        var ex = Assert.Throws<VulkanException>(() =>
            instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => false));

        Assert.Contains("No physical device matched", ex.Message);
    }
}
