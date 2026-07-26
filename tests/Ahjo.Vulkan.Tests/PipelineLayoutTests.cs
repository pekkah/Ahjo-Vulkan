using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed class PipelineLayoutTests
{
    [Fact]
    public void Default_DescriptorSetLayout_IsNull_DisposeNoOp()
    {
        DescriptorSetLayout dsl = default;
        Assert.True(dsl.IsNull);
        dsl.Dispose();

        PipelineLayout pl = default;
        Assert.True(pl.IsNull);
        pl.Dispose();
    }

    [Fact]
    public void DescriptorSetLayout_OneUniformBufferBinding_RoundTrips()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        var desc = new DescriptorSetLayoutDescription
        {
            Bindings =
            [
                new DescriptorBinding
                {
                    Slot   = 0,
                    Type   = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                    Count  = 1,
                    Stages = ShaderStages.Vertex | ShaderStages.Fragment,
                },
            ],
        };

        using var dsl = device.CreateDescriptorSetLayout(in desc);
        Assert.False(dsl.IsNull);
    }

    [Fact]
    public void DescriptorSetLayout_BindingFlags_ChainsBindingFlagsCreateInfo()
    {
        TestGate.RequireDriver();
        TestGate.RequireDeviceFeature(VulkanDriverProbe.SupportsBindlessSampledImage,
            "Device does not advertise descriptorBindingPartiallyBound + " +
            "descriptorBindingVariableDescriptorCount + descriptorBindingSampledImageUpdateAfterBind; " +
            "this bindless sampled-image layout test requires all three.");

        using var instance = Instance.Create(default);
        using var device   = CreateBindlessGraphicsDevice(instance);

        var desc = new DescriptorSetLayoutDescription
        {
            UpdateAfterBindPool = true,
            Bindings =
            [
                new DescriptorBinding
                {
                    Slot         = 0,
                    Type         = VkDescriptorType.VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE,
                    Count        = 1024,
                    Stages       = ShaderStages.Fragment,
                    BindingFlags = DescriptorBindingFlags.PartiallyBound
                                 | DescriptorBindingFlags.VariableDescriptorCount
                                 | DescriptorBindingFlags.UpdateAfterBind,
                },
            ],
        };

        using var dsl = device.CreateDescriptorSetLayout(in desc);
        Assert.False(dsl.IsNull);
    }

    [Fact]
    public void PipelineLayout_OneSet_OnePushConstantRange_RoundTrips()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        using var dsl = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            Bindings =
            [
                new DescriptorBinding
                {
                    Slot = 0, Type = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                    Count = 1, Stages = ShaderStages.Vertex,
                },
            ],
        });

        DescriptorSetLayout[]   layouts = [dsl];
        PushConstantRange[] ranges = [PushConstantRange.For<PushBlock>(ShaderStages.Vertex)];

        using var pl = device.CreatePipelineLayout(new PipelineLayoutDescription
        {
            SetLayouts         = layouts,
            PushConstantRanges = ranges,
        });

        Assert.False(pl.IsNull);
    }

    [Fact]
    public void PushConstantRange_For_SizesFromTypeof()
    {
        var r = PushConstantRange.For<PushBlock>(ShaderStages.Fragment, offset: 16);
        Assert.Equal(ShaderStages.Fragment, r.Stages);
        Assert.Equal(16u, r.Offset);
        Assert.Equal((uint)System.Runtime.CompilerServices.Unsafe.SizeOf<PushBlock>(), r.Size);
    }

    [Fact]
    public void PipelineLayout_PoolPath_RoundtripsAcquire()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        using var dsl = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            Bindings =
            [
                new DescriptorBinding
                {
                    Slot = 0, Type = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                    Count = 1, Stages = ShaderStages.Vertex,
                },
            ],
        });

        ReadOnlySpan<VkDescriptorPoolSize> sizes =
        [
            new VkDescriptorPoolSize { type = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, descriptorCount = 1 },
        ];
        using var pool = new DescriptorSetPool(device, maxSets: 1, sizes);

        unsafe
        {
            var set = pool.Acquire(dsl.Handle);
            Assert.False(set.IsNull);
        }
    }

    /// <summary>
    /// Issue 57: a 224 B push struct (engine's CullPushConstants shape)
    /// must build cleanly when the device's <c>maxPushConstantsSize</c>
    /// covers it. The previous 128 B literal assert in
    /// <see cref="CommandRecorder.PushConstants{T}"/> rejected this even
    /// though Vulkan accepts it on every desktop driver.
    /// </summary>
    [Fact]
    public unsafe void CreatePipelineLayout_LargePushRange_FitsDeviceLimit()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        VkPhysicalDeviceProperties props;
        Vk.vkGetPhysicalDeviceProperties(device.PhysicalDevice.Handle, &props);
        uint deviceLimit = props.limits.maxPushConstantsSize;
        TestGate.RequireDeviceFeature(deviceLimit >= 224,
            $"Device's maxPushConstantsSize ({deviceLimit}) is below the 224 B test target.");

        // 224 B push range — engine's GpuCullPipeline (CullPushConstants)
        // shape: 56 floats / ints depending on layout; the wrapper must
        // accept it on devices that report ≥224 B.
        PushConstantRange[] ranges =
        [
            new PushConstantRange
            {
                Stages = ShaderStages.Compute,
                Offset = 0,
                Size   = 224,
            },
        ];
        using var layout = device.CreatePipelineLayout(new PipelineLayoutDescription
        {
            PushConstantRanges = ranges,
        });
        Assert.False(layout.IsNull);
    }

    /// <summary>
    /// A push range whose <c>offset + size</c> exceeds the device's
    /// <c>maxPushConstantsSize</c> must throw at create time. Without
    /// this, the driver would either reject the layout with an opaque
    /// VK_ERROR or accept it and surprise the next push call.
    /// </summary>
    [Fact]
    public unsafe void CreatePipelineLayout_PushRangeExceedsDeviceLimit_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        VkPhysicalDeviceProperties props;
        Vk.vkGetPhysicalDeviceProperties(device.PhysicalDevice.Handle, &props);
        uint deviceLimit = props.limits.maxPushConstantsSize;
        // Pick something well past the limit — pad with extra to dodge
        // alignment quirks if a driver reported a non-aligned ceiling.
        uint over = deviceLimit + 64;

        PushConstantRange[] ranges =
        [
            new PushConstantRange
            {
                Stages = ShaderStages.Compute,
                Offset = 0,
                Size   = over,
            },
        ];

        var ex = Assert.Throws<ArgumentException>(() =>
        {
            using var _ = device.CreatePipelineLayout(new PipelineLayoutDescription
            {
                PushConstantRanges = ranges,
            });
        });
        Assert.Contains("maxPushConstantsSize", ex.Message);
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Size = 16)]
    private struct PushBlock { }

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

    // Same picker as CreateGraphicsDevice but also opts into the
    // descriptor-indexing bits the bindless sampled-image layout test
    // needs. UPDATE_AFTER_BIND_POOL + PARTIALLY_BOUND |
    // VARIABLE_DESCRIPTOR_COUNT | UPDATE_AFTER_BIND on a
    // SAMPLED_IMAGE binding require these feature bits to be enabled at
    // device-creation time, otherwise driver paths can SIGSEGV.
    private static unsafe Device CreateBindlessGraphicsDevice(Instance instance)
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
            Queues            = [new QueueRequest(family, count: 1, priority: 1.0f)],
            ConfigureFeatures = static (
                ref ChainBuilder<VkDeviceCreateInfo> _,
                ref VkPhysicalDeviceFeatures2 _,
                ref VkPhysicalDeviceVulkan12Features f12,
                ref VkPhysicalDeviceVulkan13Features _,
                ref VkPhysicalDeviceVulkan14Features _) =>
            {
                f12.descriptorBindingPartiallyBound             = 1;
                f12.descriptorBindingVariableDescriptorCount    = 1;
                f12.descriptorBindingSampledImageUpdateAfterBind = 1;
            },
        });
    }
}
