using Ahjo.Vulkan.Native;
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
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

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
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

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
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

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
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

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
}
