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

    [Fact]
    public void Pick_DriverVersion_NonZero()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        uint observedDriverVersion = 0;
        var picker = (PhysicalDevicePicker)((in PhysicalDeviceInfo info) =>
        {
            observedDriverVersion = info.Properties.driverVersion;
            return true;
        });

        instance.PickPhysicalDevice(picker);

        Assert.NotEqual(0u, observedDriverVersion);
    }

    [Fact]
    public void Pick_NameSpan_RoundTripsToString()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        byte[] capturedName = [];
        var picker = (PhysicalDevicePicker)((in PhysicalDeviceInfo info) =>
        {
            capturedName = info.Name.ToArray();   // copy out — span itself can't escape
            return true;
        });

        PhysicalDevice gpu = instance.PickPhysicalDevice(picker);

        // Cross-check the slice by re-querying the raw API and computing
        // the same NUL-terminated slice on the deviceName fixed buffer.
        var props = default(VkPhysicalDeviceProperties);
        Vk.vkGetPhysicalDeviceProperties(gpu.Handle, &props);
        int nulOffset = 0;
        while (nulOffset < 256 && props.deviceName[nulOffset] != 0) nulOffset++;

        Assert.True(nulOffset > 0, "VkPhysicalDeviceProperties.deviceName was empty.");
        Assert.Equal(nulOffset, capturedName.Length);
        for (int i = 0; i < nulOffset; i++)
            Assert.Equal((byte)props.deviceName[i], capturedName[i]);
    }
}
