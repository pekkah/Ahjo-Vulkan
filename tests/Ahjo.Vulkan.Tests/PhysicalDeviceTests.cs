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

    [Fact]
    public void Pick_QueueFamiliesNeverEmpty()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        int observedFamilyCount = -1;
        var picker = (PhysicalDevicePicker)((in PhysicalDeviceInfo info) =>
        {
            observedFamilyCount = info.QueueFamilies.Length;
            return true;
        });

        instance.PickPhysicalDevice(picker);

        Assert.True(observedFamilyCount >= 1,
            $"Vulkan spec guarantees ≥1 queue family per device; saw {observedFamilyCount}.");
    }

    [Fact]
    public void Pick_PicksDeviceWithGraphicsQueue()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        bool observedGraphicsFamily = false;
        var picker = (PhysicalDevicePicker)((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    observedGraphicsFamily = true;
                    return true;
                }
            }
            return false;
        });

        PhysicalDevice gpu = instance.PickPhysicalDevice(picker);

        Assert.False(gpu.IsNull);
        Assert.True(observedGraphicsFamily);
    }

    [Fact]
    public void Pick_ExtensionsContainsCommon()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        bool observedMaintenance1 = false;
        bool observedFakeExtension = false;
        var picker = (PhysicalDevicePicker)((in PhysicalDeviceInfo info) =>
        {
            // VK_KHR_maintenance1 promoted to core in 1.1 but still
            // reported as a device extension on every shipping driver.
            // If a future minimal software rasteriser (lavapipe / SwiftShader)
            // omits it, swap to "VK_KHR_storage_buffer_storage_class"u8 or
            // "VK_KHR_dedicated_allocation"u8 — both are equally universal.
            observedMaintenance1  = info.SupportsExtension("VK_KHR_maintenance1"u8);
            observedFakeExtension = info.SupportsExtension("VK_FAKE_does_not_exist"u8);
            return true;
        });

        instance.PickPhysicalDevice(picker);

        Assert.True (observedMaintenance1,
            "Every shipping Vulkan driver advertises VK_KHR_maintenance1.");
        Assert.False(observedFakeExtension);
    }

    [Fact]
    public void Pick_Vulkan13Features_Readable()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        bool pickerInvoked = false;
        uint dynamicRendering = 0;
        var picker = (PhysicalDevicePicker)((in PhysicalDeviceInfo info) =>
        {
            pickerInvoked   = true;
            dynamicRendering = info.Features13.dynamicRendering;
            // No assertion on the value — software rasterisers may report
            // 0; the test only proves the chain was wired.
            return true;
        });

        instance.PickPhysicalDevice(picker);

        Assert.True(pickerInvoked);
        Assert.True(dynamicRendering == 0u || dynamicRendering == 1u,
            "VkBool32 must be 0 or 1.");
    }

    [Fact]
    public void Pick_PrefersDiscrete_OrFallsBack()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        // First pass: prefer discrete.
        PhysicalDevice gpu;
        try
        {
            gpu = instance.PickPhysicalDevice(static (in PhysicalDeviceInfo info) =>
                info.Type == VkPhysicalDeviceType.VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU);
        }
        catch (VulkanException)
        {
            // No discrete GPU on this host (CI / llvmpipe). Fall back to any.
            gpu = instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => true);
        }

        Assert.False(gpu.IsNull);
    }

    /// <summary>
    /// <c>VK_FORMAT_R8G8B8A8_UNORM</c> with optimal tiling is required by
    /// the spec to support sampling and color-attachment use. The probe
    /// must surface non-zero <c>optimalTilingFeatures</c> for it on every
    /// conformant device.
    /// </summary>
    [Fact]
    public void GetFormatProperties_Rgba8Unorm_OptimalTilingNonZero()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        var gpu = instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => true);

        VkFormatProperties props = gpu.GetFormatProperties(VkFormat.VK_FORMAT_R8G8B8A8_UNORM);
        Assert.NotEqual(0u, props.optimalTilingFeatures);
    }

    /// <summary>
    /// The engine's runtime mip-generation path needs both
    /// <c>BlitSrc</c> + <c>SampledImageFilterLinear</c> on the source
    /// format to use linear filtering during downsample blits. Probe both
    /// flags at once to mirror the engine's call shape.
    /// </summary>
    [Fact]
    public void SupportsOptimalTilingFeature_LinearFilterableBlitSrc_Rgba8Unorm()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        var gpu = instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => true);

        bool ok = gpu.SupportsOptimalTilingFeature(
            VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
            VkFormatFeatureFlagBits.VK_FORMAT_FEATURE_BLIT_SRC_BIT |
            VkFormatFeatureFlagBits.VK_FORMAT_FEATURE_SAMPLED_IMAGE_FILTER_LINEAR_BIT);

        Assert.True(ok, "VK_FORMAT_R8G8B8A8_UNORM must support BLIT_SRC + SAMPLED_IMAGE_FILTER_LINEAR with optimal tiling on a conformant device.");
    }

    /// <summary>
    /// <c>VK_FORMAT_UNDEFINED</c> is a sentinel — no optimal-tiling
    /// features are valid on it; the probe must report no support.
    /// </summary>
    [Fact]
    public void SupportsOptimalTilingFeature_Undefined_ReturnsFalse()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        var gpu = instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => true);

        bool ok = gpu.SupportsOptimalTilingFeature(
            VkFormat.VK_FORMAT_UNDEFINED,
            VkFormatFeatureFlagBits.VK_FORMAT_FEATURE_SAMPLED_IMAGE_BIT);
        Assert.False(ok);
    }

    [Fact]
    public void Pick_TwoCalls_ReturnSameInstance()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        PhysicalDevice a = instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => true);
        PhysicalDevice b = instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => true);

        Assert.Same(a, b);
    }
}
