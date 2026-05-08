using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed class SamplerTests
{
    [Fact]
    public void Default_IsNull_DisposeIsNoOp()
    {
        Sampler s = default;
        Assert.True(s.IsNull);
        Assert.Equal(0UL, s.RawHandle);
        s.Dispose();
    }

    [Fact]
    public void ObjectType_IsSampler()
    {
        Assert.Equal(VkObjectType.VK_OBJECT_TYPE_SAMPLER, Sampler.ObjectType);
    }

    [Fact]
    public void CreateSampler_AnisotropicFilter_RoundTrips()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device = CreateGraphicsDeviceWithAnisotropy(instance, out bool anisotropySupported);
        Assert.SkipUnless(anisotropySupported, "Physical device does not advertise samplerAnisotropy.");

        using var sampler = device.CreateSampler(new SamplerDescription
        {
            MagFilter        = VkFilter.VK_FILTER_LINEAR,
            MinFilter        = VkFilter.VK_FILTER_LINEAR,
            MipmapMode       = VkSamplerMipmapMode.VK_SAMPLER_MIPMAP_MODE_LINEAR,
            AddressModeU     = VkSamplerAddressMode.VK_SAMPLER_ADDRESS_MODE_REPEAT,
            AddressModeV     = VkSamplerAddressMode.VK_SAMPLER_ADDRESS_MODE_REPEAT,
            AddressModeW     = VkSamplerAddressMode.VK_SAMPLER_ADDRESS_MODE_REPEAT,
            AnisotropyEnable = true,
            MaxAnisotropy    = 16f,
            MaxLod           = 1000f,
        });

        Assert.False(sampler.IsNull);
        Assert.NotEqual(0UL, sampler.RawHandle);
    }

    [Fact]
    public void CreateSampler_AnisotropyRequestedButFeatureUnsupported_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device = CreateGraphicsDevice(instance);

        VkPhysicalDeviceFeatures features;
        unsafe { Vk.vkGetPhysicalDeviceFeatures(device.PhysicalDevice.Handle, &features); }
        Assert.SkipWhen(features.samplerAnisotropy != 0,
            "Physical device advertises samplerAnisotropy; cannot exercise the unsupported branch.");

        var ex = Assert.Throws<ArgumentException>(() => device.CreateSampler(
            new SamplerDescription { AnisotropyEnable = true, MaxAnisotropy = 16f }));
        Assert.Contains("samplerAnisotropy", ex.Message);
    }

    [Fact]
    public void CreateSampler_ComparisonSampler_RoundTrips()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        // Shadow comparison sampler — used by every cascaded shadow pipeline.
        using var sampler = device.CreateSampler(new SamplerDescription
        {
            MagFilter     = VkFilter.VK_FILTER_LINEAR,
            MinFilter     = VkFilter.VK_FILTER_LINEAR,
            MipmapMode    = VkSamplerMipmapMode.VK_SAMPLER_MIPMAP_MODE_NEAREST,
            AddressModeU  = VkSamplerAddressMode.VK_SAMPLER_ADDRESS_MODE_CLAMP_TO_EDGE,
            AddressModeV  = VkSamplerAddressMode.VK_SAMPLER_ADDRESS_MODE_CLAMP_TO_EDGE,
            AddressModeW  = VkSamplerAddressMode.VK_SAMPLER_ADDRESS_MODE_CLAMP_TO_EDGE,
            CompareEnable = true,
            CompareOp     = VkCompareOp.VK_COMPARE_OP_LESS,
            MaxLod        = 1f,
        });

        Assert.False(sampler.IsNull);
    }

    [Fact]
    public void CreateSampler_ClampToBorderShadow_FloatOpaqueWhite_RoundTrips()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        // Border-clamp sampler used by shadow taps that must read 1.0 (no
        // shadow) outside the cascade footprint — FloatOpaqueWhite gives the
        // depth-compare a "max depth" sentinel.
        using var sampler = device.CreateSampler(new SamplerDescription
        {
            MagFilter    = VkFilter.VK_FILTER_LINEAR,
            MinFilter    = VkFilter.VK_FILTER_LINEAR,
            MipmapMode   = VkSamplerMipmapMode.VK_SAMPLER_MIPMAP_MODE_NEAREST,
            AddressModeU = VkSamplerAddressMode.VK_SAMPLER_ADDRESS_MODE_CLAMP_TO_BORDER,
            AddressModeV = VkSamplerAddressMode.VK_SAMPLER_ADDRESS_MODE_CLAMP_TO_BORDER,
            AddressModeW = VkSamplerAddressMode.VK_SAMPLER_ADDRESS_MODE_CLAMP_TO_BORDER,
            BorderColor  = VkBorderColor.VK_BORDER_COLOR_FLOAT_OPAQUE_WHITE,
        });

        Assert.False(sampler.IsNull);
    }

    [Fact]
    public void Sampler_FlowsIntoSamplerDescriptorWrite()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        using var sampler = device.CreateSampler(new SamplerDescription
        {
            MagFilter    = VkFilter.VK_FILTER_LINEAR,
            MinFilter    = VkFilter.VK_FILTER_LINEAR,
            AddressModeU = VkSamplerAddressMode.VK_SAMPLER_ADDRESS_MODE_REPEAT,
            AddressModeV = VkSamplerAddressMode.VK_SAMPLER_ADDRESS_MODE_REPEAT,
            AddressModeW = VkSamplerAddressMode.VK_SAMPLER_ADDRESS_MODE_REPEAT,
        });

        SamplerDescriptorWrite w = SamplerDescriptorWrite.Of(in sampler);
        unsafe { Assert.True(sampler.Handle == w.Sampler); }
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

    private static unsafe Device CreateGraphicsDeviceWithAnisotropy(Instance instance, out bool anisotropySupported)
    {
        uint family = uint.MaxValue;
        bool supported = false;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    family = info.QueueFamilies[i].Index;
                    supported = info.Features.samplerAnisotropy != 0;
                    return true;
                }
            }
            return false;
        });
        anisotropySupported = supported;

        var desc = new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
            ConfigureFeatures = supported
                ? (
                    ref ChainBuilder<VkDeviceCreateInfo> chain,
                    ref VkPhysicalDeviceVulkan12Features _,
                    ref VkPhysicalDeviceVulkan13Features _,
                    ref VkPhysicalDeviceVulkan14Features _) =>
                {
                    ref var f2 = ref chain.Push<VkPhysicalDeviceFeatures2>();
                    f2.features.samplerAnisotropy = 1;
                }
                : null,
        };
        return gpu.CreateDevice(in desc);
    }
}
