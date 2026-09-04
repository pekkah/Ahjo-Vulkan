using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;
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
        TestGate.RequireDriver();

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
        TestGate.RequireDriver();

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
        TestGate.RequireDriver();

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
        TestGate.RequireDriver();

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
        TestGate.RequireDriver();

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
        TestGate.RequireDriver();

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
        TestGate.RequireDriver();

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

    /// <summary>
    /// Issue #228: a <c>VkDescriptorPoolSize</c> entry with
    /// <c>descriptorCount = 0</c> violates
    /// <c>VUID-VkDescriptorPoolSize-descriptorCount-00302</c> and breaks the
    /// pool's "0 per-type total means empty template" reasoning. The ctor
    /// rejects it fail-early, before <c>CreatePool()</c>, and the exception
    /// names <c>poolSizes</c>, the offending index, and its type. An empty
    /// template (issue #191) — not a zero entry — is the way to ask for a
    /// budget-less pool, so the message points there.
    /// </summary>
    [Fact]
    public void Pool_Ctor_ZeroDescriptorCount_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        // A zero entry sitting after a valid one: the guard must fire on index 1
        // and quote it, not merely reject "some entry".
        VkDescriptorPoolSize[] sizes =
        [
            new VkDescriptorPoolSize { type = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, descriptorCount = 4 },
            new VkDescriptorPoolSize { type = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER, descriptorCount = 0 },
        ];

        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new DescriptorSetPool(device, maxSets: 4, sizes));
        Assert.Equal("poolSizes", ex.ParamName);
        Assert.Contains("poolSizes[1]", ex.Message, StringComparison.Ordinal);
        Assert.Contains("STORAGE_BUFFER", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Issue #191: an empty <c>poolSizes</c> template is legal — there is no
    /// <c>VUID-VkDescriptorPoolCreateInfo-poolSizeCount-arraylength</c>, and
    /// <c>VUID-…-pPoolSizes-parameter</c> excuses the array when
    /// <c>poolSizeCount</c> is 0. <b>This test is also the measurement</b>: the
    /// spec's §E11.1 is registry-derived, and this is where
    /// <c>vkCreateDescriptorPool</c> with <c>poolSizeCount = 0</c> first meets a
    /// real driver.
    /// </summary>
    [Fact]
    public void Pool_Ctor_EmptyPoolSizes_Succeeds()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);

        using var pool = new DescriptorSetPool(device, maxSets: 4, []);
        Assert.Equal(1, pool.PoolCount);
    }

    /// <summary>
    /// A budget-less pool serves the one thing it can: a layout with zero
    /// bindings. The full free-list round trip works for it — acquire, release,
    /// re-acquire the same handle out of the bucket, reset, acquire again.
    /// </summary>
    [Fact]
    public void Pool_EmptyPoolSizes_AcquireZeroBindingLayout_RoundTrips()
    {
        TestGate.RequireDriver();

        int errorCount = 0;
        var errors = new System.Collections.Concurrent.ConcurrentQueue<string>();
        Action<DebugMessage> sink = msg =>
        {
            if ((msg.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) != 0)
            {
                System.Threading.Interlocked.Increment(ref errorCount);
                errors.Enqueue(msg.Message);
            }
        };

        bool validating = VulkanDriverProbe.HasValidationLayer;

        using var instance = Instance.Create(new InstanceDescription
        {
            EnableValidation = validating,
            DebugCallback    = validating ? sink : null,
        });
        using var device = CreateGraphicsDevice(instance);
        VkDescriptorSetLayout_T* layout = CreateEmptyLayout(device);
        try
        {
            using var pool = new DescriptorSetPool(device, maxSets: 4, []);

            var first = pool.Acquire(layout);
            Assert.False(first.IsNull);

            pool.Release(layout, first);
            var second = pool.Acquire(layout);
            Assert.True(first.Handle == second.Handle);
            Assert.Equal(1, pool.AllocatedCount);

            pool.Reset();
            var third = pool.Acquire(layout);
            Assert.False(third.IsNull);
        }
        finally { Vk.vkDestroyDescriptorSetLayout(device.Handle, layout, null); }

        if (validating)
        {
            Assert.True(
                System.Threading.Volatile.Read(ref errorCount) == 0,
                $"The layers rejected a budget-less pool serving a zero-binding layout:{Environment.NewLine}" +
                string.Join(Environment.NewLine, errors));
        }
    }

    /// <summary>
    /// A budget-less pool's only exhaustion mode is <c>maxSets</c> — which is
    /// the one budget this repo's driver was measured enforcing (#187) — and
    /// growth is the correct response to it. Modelled on
    /// <see cref="Pool_AcquireBeyondMaxSets_GrowsAndSucceeds"/>.
    /// </summary>
    [Fact]
    public void Pool_EmptyPoolSizes_AcquireBeyondMaxSets_Grows()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);
        VkDescriptorSetLayout_T* layout = CreateEmptyLayout(device);
        try
        {
            using var pool = new DescriptorSetPool(device, maxSets: 1, []);
            Assert.Equal(1, pool.PoolCount);

            var first  = pool.Acquire(layout);
            var second = pool.Acquire(layout);

            Assert.False(first.IsNull);
            Assert.False(second.IsNull);
            Assert.True(first.Handle != second.Handle);
            Assert.Equal(2, pool.PoolCount);
        }
        finally { Vk.vkDestroyDescriptorSetLayout(device.Handle, layout, null); }
    }

    /// <summary>
    /// A pool with no <c>poolSizes</c> holds no descriptors of any type, so
    /// <c>_maxPerTypeDescriptorTotal</c> is 0 and #182's pre-flight guard rejects
    /// every variable count ≥ 1 — the right answer, not merely a tolerated one.
    /// The message must name the empty template, so the guard is not quietly
    /// re-implemented as "exempt the empty case".
    /// </summary>
    [Fact]
    public void Pool_EmptyPoolSizes_AcquireVariableCount_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance);
        VkDescriptorSetLayout_T* layout = CreateEmptyLayout(device);
        try
        {
            using var pool = new DescriptorSetPool(device, maxSets: 4, []);

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => pool.Acquire(layout, 4));
            Assert.Contains("no poolSizes", ex.Message, StringComparison.Ordinal);
        }
        finally { Vk.vkDestroyDescriptorSetLayout(device.Handle, layout, null); }
    }

    [Fact]
    public void Pool_Acquire_AfterDispose_Throws()
    {
        TestGate.RequireDriver();

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
    /// <summary>
    /// Creates a layout with <b>zero</b> bindings directly via
    /// <c>vkCreateDescriptorSetLayout</c> (<c>bindingCount = 0</c>,
    /// <c>pBindings = null</c>) — issue #191's sparse-set hole. Deliberately the
    /// raw API rather than <c>Device.CreateDescriptorSetLayout</c>, so this suite
    /// keeps testing the pool rather than the wrapper's layout path.
    /// </summary>
    private static VkDescriptorSetLayout_T* CreateEmptyLayout(Device device)
    {
        var ci = new VkDescriptorSetLayoutCreateInfo
        {
            sType        = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO,
            bindingCount = 0,
            pBindings    = null,
        };
        VkDescriptorSetLayout_T* raw = null;
        Vk.vkCreateDescriptorSetLayout(device.Handle, &ci, null, &raw).ThrowIfFailed();
        return raw;
    }

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
