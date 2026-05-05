using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed unsafe class DescriptorSetPoolTests
{
    [Fact]
    public void Default_DescriptorSet_IsNull()
    {
        DescriptorSet s = default;
        Assert.True(s.IsNull);
    }

    [Fact]
    public void Pool_Acquire_AllocatesNewSet()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);
        VkDescriptorSetLayout_T* layout = CreateUniformBufferLayout(device);
        try
        {
            ReadOnlySpan<VkDescriptorPoolSize> sizes =
            [
                new VkDescriptorPoolSize { type = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, descriptorCount = 4 },
            ];
            using var pool = new DescriptorSetPool(device, maxSets: 4, sizes);

            var set = pool.Acquire(layout);
            Assert.False(set.IsNull);
            Assert.Equal(1, pool.AllocatedCount);
        }
        finally { Vk.vkDestroyDescriptorSetLayout(device.Handle, layout, null); }
    }

    [Fact]
    public void Pool_Release_Acquire_RecyclesHandle()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);
        VkDescriptorSetLayout_T* layout = CreateUniformBufferLayout(device);
        try
        {
            ReadOnlySpan<VkDescriptorPoolSize> sizes =
            [
                new VkDescriptorPoolSize { type = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, descriptorCount = 4 },
            ];
            using var pool = new DescriptorSetPool(device, maxSets: 4, sizes);

            var first = pool.Acquire(layout);
            pool.Release(layout, first);
            var second = pool.Acquire(layout);

            Assert.True(first.Handle == second.Handle);
            Assert.Equal(1, pool.AllocatedCount);
        }
        finally { Vk.vkDestroyDescriptorSetLayout(device.Handle, layout, null); }
    }

    [Fact]
    public void Pool_Reset_InvalidatesAndRebuilds()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);
        VkDescriptorSetLayout_T* layout = CreateUniformBufferLayout(device);
        try
        {
            ReadOnlySpan<VkDescriptorPoolSize> sizes =
            [
                new VkDescriptorPoolSize { type = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, descriptorCount = 8 },
            ];
            using var pool = new DescriptorSetPool(device, maxSets: 8, sizes);

            for (int i = 0; i < 3; i++) pool.Acquire(layout);
            Assert.Equal(3, pool.AllocatedCount);

            pool.Reset();
            Assert.Equal(0, pool.AllocatedCount);

            var fresh = pool.Acquire(layout);
            Assert.False(fresh.IsNull);
            Assert.Equal(1, pool.AllocatedCount);
        }
        finally { Vk.vkDestroyDescriptorSetLayout(device.Handle, layout, null); }
    }

    [Fact]
    public void Pool_Acquire_AfterDispose_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);
        VkDescriptorSetLayout_T* layout = CreateUniformBufferLayout(device);
        try
        {
            ReadOnlySpan<VkDescriptorPoolSize> sizes =
            [
                new VkDescriptorPoolSize { type = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, descriptorCount = 1 },
            ];
            var pool = new DescriptorSetPool(device, maxSets: 1, sizes);
            pool.Dispose();

            Assert.Throws<ObjectDisposedException>(() => pool.Acquire(layout));
        }
        finally { Vk.vkDestroyDescriptorSetLayout(device.Handle, layout, null); }
    }

    /// <summary>
    /// Creates a one-binding layout (<c>binding 0 = uniform buffer, vertex
    /// stage</c>) directly via <c>vkCreateDescriptorSetLayout</c>. The
    /// strongly-typed wrapper lands in #23 (22 — PipelineLayout +
    /// DescriptorSetLayout); these tests use the raw API in the meantime.
    /// </summary>
    private static VkDescriptorSetLayout_T* CreateUniformBufferLayout(Device device)
    {
        var binding = new VkDescriptorSetLayoutBinding
        {
            binding         = 0,
            descriptorType  = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
            descriptorCount = 1,
            stageFlags      = (uint)VkShaderStageFlagBits.VK_SHADER_STAGE_VERTEX_BIT,
        };
        var ci = new VkDescriptorSetLayoutCreateInfo
        {
            sType        = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO,
            bindingCount = 1,
            pBindings    = &binding,
        };
        VkDescriptorSetLayout_T* raw = null;
        Vk.vkCreateDescriptorSetLayout(device.Handle, &ci, null, &raw).ThrowIfFailed();
        return raw;
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
}
