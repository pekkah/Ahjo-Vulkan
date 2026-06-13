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

    /// <summary>
    /// Issue 60: allocating past <c>maxSets</c> against a chain (default
    /// growth on) succeeds — the pool transparently allocates a fresh
    /// <c>VkDescriptorPool</c> with the same template. Sub-pool count
    /// rises to 2 once the first sub-pool is exhausted.
    /// </summary>
    [Fact]
    public void Pool_AcquireBeyondMaxSets_GrowsAndSucceeds()
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
            Assert.Equal(1, pool.PoolCount);

            DescriptorSet[] sets = new DescriptorSet[10];
            for (int i = 0; i < sets.Length; i++)
            {
                sets[i] = pool.Acquire(layout);
                Assert.False(sets[i].IsNull);
            }

            Assert.Equal(10, pool.AllocatedCount);
            // 4 per sub-pool, 10 sets → at least three sub-pools.
            Assert.True(pool.PoolCount >= 3,
                $"Expected ≥3 sub-pools after 10 allocations on a maxSets=4 pool; saw {pool.PoolCount}.");
        }
        finally { Vk.vkDestroyDescriptorSetLayout(device.Handle, layout, null); }
    }

    /// <summary>
    /// Reset on a grown chain wipes every sub-pool and lets the caller
    /// re-allocate the same workload without duplicate-allocation fault.
    /// Sub-pool count stays put — Reset does not destroy the grown
    /// pools, since the next frame will refill them.
    /// </summary>
    [Fact]
    public void Pool_GrownChain_ResetThenReallocate_Succeeds()
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

            for (int i = 0; i < 10; i++) pool.Acquire(layout);
            int chainAfterFirstFill = pool.PoolCount;
            Assert.True(chainAfterFirstFill >= 3);

            pool.Reset();
            Assert.Equal(0, pool.AllocatedCount);
            Assert.Equal(chainAfterFirstFill, pool.PoolCount);

            for (int i = 0; i < 10; i++)
            {
                var s = pool.Acquire(layout);
                Assert.False(s.IsNull);
            }
            Assert.Equal(10, pool.AllocatedCount);
        }
        finally { Vk.vkDestroyDescriptorSetLayout(device.Handle, layout, null); }
    }

    /// <summary>
    /// With growth disabled the pool surfaces the original Vulkan
    /// failure (typically <c>OUT_OF_POOL_MEMORY</c>) rather than
    /// silently growing. Use this opt-out on debug builds where
    /// hitting the budget should be loud and audited.
    /// </summary>
    [Fact]
    public void Pool_GrowDisabled_AcquireBeyondBudget_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);
        VkDescriptorSetLayout_T* layout = CreateUniformBufferLayout(device);
        try
        {
            ReadOnlySpan<VkDescriptorPoolSize> sizes =
            [
                new VkDescriptorPoolSize { type = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, descriptorCount = 2 },
            ];
            using var pool = new DescriptorSetPool(device, maxSets: 2, sizes, growOnExhaustion: false);

            // Two acquires fit; the third should throw because growth is
            // off and the pool is exhausted.
            pool.Acquire(layout);
            pool.Acquire(layout);
            Assert.Throws<VulkanException>(() => pool.Acquire(layout));
            Assert.Equal(1, pool.PoolCount);
        }
        finally { Vk.vkDestroyDescriptorSetLayout(device.Handle, layout, null); }
    }

    /// <summary>
    /// A <c>maxSets</c> of 0 would otherwise flow into
    /// <c>vkCreateDescriptorPool</c> and surface as an opaque
    /// driver/validation error. The ctor rejects it fail-early — the guard
    /// runs before <c>CreatePool()</c>, so no GPU work happens. The device
    /// is real only because the device-null check precedes the new guard.
    /// </summary>
    [Fact]
    public void Pool_Ctor_ZeroMaxSets_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        // Array (not a span local) so it can be captured by the lambda below;
        // it converts to the ctor's ReadOnlySpan<VkDescriptorPoolSize> param.
        VkDescriptorPoolSize[] sizes =
        [
            new VkDescriptorPoolSize { type = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, descriptorCount = 1 },
        ];

        Assert.Throws<ArgumentOutOfRangeException>(() => new DescriptorSetPool(device, maxSets: 0, sizes));
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
