using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Issue #182: <see cref="DescriptorSetPool.Acquire(VkDescriptorSetLayout_T*, uint)"/>
/// chains <c>VkDescriptorSetVariableDescriptorCountAllocateInfo</c>, so a
/// bindless heap can be allocated through the pool instead of by hand.
/// </summary>
/// <remarks>
/// The oracle for the two allocation-correctness tests is
/// <c>VK_LAYER_KHRONOS_validation</c>: this driver returns
/// <c>VK_SUCCESS</c> for the broken allocation too, and only the write
/// afterwards is rejected — with the *allocated* count quoted back
/// (VUID-VkWriteDescriptorSet-dstArrayElement-00321). Run with
/// <c>AHJO_VULKAN_TIER=validation</c> or the two tests skip rather than
/// assert.
/// </remarks>
public sealed unsafe class DescriptorSetPoolVariableCountTests
{
    private const string FeatureGateReason =
        "Device does not advertise descriptorBindingPartiallyBound + " +
        "descriptorBindingVariableDescriptorCount + " +
        "descriptorBindingStorageBufferUpdateAfterBind; this variable-descriptor-count " +
        "test requires all three.";

    /// <summary>
    /// A set allocated with a variable count of 8 accepts a write at
    /// element 7. Without the chained
    /// <c>VkDescriptorSetVariableDescriptorCountAllocateInfo</c> the binding's
    /// effective count is zero and the layer rejects the write twice over
    /// (VUID-VkWriteDescriptorSet-dstBinding-00316 and
    /// VUID-VkWriteDescriptorSet-dstArrayElement-00321), which is what makes
    /// this test sensitive to the chain being dropped.
    /// </summary>
    [Fact]
    public void Acquire_WithVariableCount_WriteInsideTheCount_PassesValidation()
    {
        TestGate.RequireDriver();
        TestGate.RequireValidationLayer();
        TestGate.RequireDeviceFeature(
            VulkanDriverProbe.SupportsBindlessVariableCountStorageBuffer, FeatureGateReason);

        var errors = new List<DebugMessage>();
        using var instance = CreateValidationInstance(errors);
        using var device   = CreateVariableCountDevice(instance);
        using var layout   = CreateVariableCountLayout(device, declaredCount: 32);
        using var pool     = CreatePool(device, budget: 256, maxSets: 4);

        using var buffer = device.Allocator.CreateBuffer(
            new BufferDescription { Size = 256, Usage = BufferUsage.StorageBuffer },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        DescriptorSet set = pool.Acquire(layout.Handle, variableDescriptorCount: 8);
        Assert.False(set.IsNull);

        var info = BufferDescriptorWrite.Of(in buffer);
        ReadOnlySpan<DescriptorWrite> writes =
        [
            DescriptorWrite.Buffer(
                binding: 0, arrayElement: 7,
                VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
                in info),
        ];
        set.Update(device, writes);

        lock (errors)
            Assert.True(errors.Count == 0,
                "Validation errors recorded: " + string.Join("; ", errors.ConvertAll(e => e.Message)));
    }

    /// <summary>
    /// The negative control for
    /// <see cref="Acquire_WithVariableCount_WriteInsideTheCount_PassesValidation"/>:
    /// the same fixture, allocated through the one-argument overload, produces
    /// a set whose variable binding holds zero descriptors, and the layer says
    /// so. Without this the green above would be indistinguishable from a
    /// fixture that cannot observe the failure at all.
    /// </summary>
    [Fact]
    public void Acquire_WithoutVariableCount_OnVariableLayout_WriteFailsValidation()
    {
        TestGate.RequireDriver();
        TestGate.RequireValidationLayer();
        TestGate.RequireDeviceFeature(
            VulkanDriverProbe.SupportsBindlessVariableCountStorageBuffer, FeatureGateReason);

        var captured = new List<DebugMessage>();
        using var instance = CreateValidationInstance(captured, includeWarnings: true);
        using var device   = CreateVariableCountDevice(instance);
        using var layout   = CreateVariableCountLayout(device, declaredCount: 32);
        using var pool     = CreatePool(device, budget: 256, maxSets: 4);

        using var buffer = device.Allocator.CreateBuffer(
            new BufferDescription { Size = 256, Usage = BufferUsage.StorageBuffer },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        DescriptorSet set = pool.Acquire(layout.Handle);
        Assert.False(set.IsNull);

        // The allocation itself is diagnosed, and this assertion is why
        // AllocateFromCurrentPool chains the struct only when the count is
        // non-zero: the layer's check is "was a chain provided", not "was a
        // non-zero count provided", so chaining a zero unconditionally would
        // silence the one message that names the mistake. The write-time
        // error below cannot see that difference — a chained zero still
        // yields an effective count of zero.
        lock (captured)
            Assert.Contains(captured, m =>
                m.MessageIdName == "WARNING-CoreValidation-AllocateDescriptorSets-VariableDescriptorCount");

        var info = BufferDescriptorWrite.Of(in buffer);
        ReadOnlySpan<DescriptorWrite> writes =
        [
            DescriptorWrite.Buffer(
                binding: 0, arrayElement: 0,
                VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
                in info),
        ];
        set.Update(device, writes);

        lock (captured)
            Assert.Contains(captured, m => m.MessageIdName == "VUID-VkWriteDescriptorSet-dstBinding-00316");
    }

    /// <summary>
    /// The free-list bucket is <c>(layout, variableDescriptorCount)</c>, not
    /// the layout alone: a set allocated with count 4 physically holds four
    /// descriptors, so recycling it for a request of 8 would hand back a set
    /// the driver rejects writes past element 3 on
    /// (VUID-VkWriteDescriptorSet-dstArrayElement-00321). The count-4 request
    /// afterwards must get the original set back — the key discriminates, it
    /// does not merely disable reuse.
    /// </summary>
    [Fact]
    public void Acquire_SameLayout_DifferentCounts_DoNotShareTheFreeList()
    {
        TestGate.RequireDriver();
        TestGate.RequireDeviceFeature(
            VulkanDriverProbe.SupportsBindlessVariableCountStorageBuffer, FeatureGateReason);

        using var instance = Instance.Create(default);
        using var device   = CreateVariableCountDevice(instance);
        using var layout   = CreateVariableCountLayout(device, declaredCount: 32);
        using var pool     = CreatePool(device, budget: 256, maxSets: 8);

        DescriptorSet a = pool.Acquire(layout.Handle, 4);
        pool.Release(layout.Handle, a);

        DescriptorSet b = pool.Acquire(layout.Handle, 8);
        Assert.True(a.Handle != b.Handle,
            "Count-8 request was served the count-4 set from the free-list — the bucket key ignores the count.");
        Assert.Equal(2, pool.AllocatedCount);

        pool.Release(layout.Handle, b);

        DescriptorSet c = pool.Acquire(layout.Handle, 4);
        Assert.True(a.Handle == c.Handle,
            "Count-4 request did not recycle the count-4 set — the bucket key over-discriminates.");
        Assert.Equal(2, pool.AllocatedCount);
    }

    /// <summary>
    /// A count exceeding the largest <i>per-descriptor-type total</i> in the
    /// pool's <c>poolSizes</c> template is rejected before Vulkan is reached.
    /// This driver accepts every per-type over-subscription tried, so the
    /// guard is the only thing that can fail here — and portable code cannot
    /// rely on that acceptance. The boundary is inclusive.
    /// </summary>
    [Fact]
    public void Acquire_CountExceedingLargestPerTypeTotal_Throws()
    {
        TestGate.RequireDriver();
        TestGate.RequireDeviceFeature(
            VulkanDriverProbe.SupportsBindlessVariableCountStorageBuffer, FeatureGateReason);

        using var instance = Instance.Create(default);
        using var device   = CreateVariableCountDevice(instance);
        using var layout   = CreateVariableCountLayout(device, declaredCount: 128);
        using var pool     = CreatePool(device, budget: 64, maxSets: 4);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => pool.Acquire(layout.Handle, 65));
        Assert.Contains("64", ex.Message);

        DescriptorSet atBoundary = pool.Acquire(layout.Handle, 64);
        Assert.False(atBoundary.IsNull);
    }

    /// <summary>
    /// The bound is the per-type <i>total</i>, not the largest single
    /// <c>poolSizes</c> entry: <c>vkCreateDescriptorPool</c> sums duplicate
    /// same-type entries ("the pool will be created with enough storage for
    /// the total number of descriptors of each type"), and only
    /// <c>VK_DESCRIPTOR_TYPE_MUTABLE_EXT</c> restricts repeats
    /// (VUID-VkDescriptorPoolCreateInfo-pPoolSizes-04787). Two 64-descriptor
    /// storage-buffer entries therefore hold 128, and a request of 100 — above
    /// either entry, below their sum — must be served rather than rejected.
    /// This is the case that distinguishes the per-type-total rule from the
    /// max-over-entries rule; without it the two are indistinguishable.
    /// </summary>
    [Fact]
    public void Acquire_CountAboveASingleEntryButWithinThePerTypeTotal_Succeeds()
    {
        TestGate.RequireDriver();
        TestGate.RequireDeviceFeature(
            VulkanDriverProbe.SupportsBindlessVariableCountStorageBuffer, FeatureGateReason);

        using var instance = Instance.Create(default);
        using var device   = CreateVariableCountDevice(instance);
        using var layout   = CreateVariableCountLayout(device, declaredCount: 256);

        // Two entries of the same type: 64 + 64 = 128 storage-buffer descriptors.
        ReadOnlySpan<VkDescriptorPoolSize> sizes =
        [
            new VkDescriptorPoolSize { type = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER, descriptorCount = 64 },
            new VkDescriptorPoolSize { type = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER, descriptorCount = 64 },
        ];
        using var pool = new DescriptorSetPool(device, maxSets: 4, sizes, updateAfterBind: true);

        DescriptorSet set = pool.Acquire(layout.Handle, 100);
        Assert.False(set.IsNull);

        // 128 is the summed boundary and is inclusive; 129 is past it.
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => pool.Acquire(layout.Handle, 129));
        Assert.Contains("128", ex.Message);
    }

    /// <summary>
    /// <c>Acquire(layout, 0)</c> and <c>Acquire(layout)</c> are the same
    /// request and must land in the same bucket — zero is not a sentinel.
    /// </summary>
    [Fact]
    public void Acquire_ZeroCount_SharesTheFreeListWithTheOneArgumentOverload()
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

            DescriptorSet x = pool.Acquire(layout, 0);
            pool.Release(layout, x);
            DescriptorSet y = pool.Acquire(layout);

            Assert.True(x.Handle == y.Handle,
                "Acquire(layout) did not recycle the set Acquire(layout, 0) produced — zero is being treated as a distinct bucket.");
            Assert.Equal(1, pool.AllocatedCount);
        }
        finally { Vk.vkDestroyDescriptorSetLayout(device.Handle, layout, null); }
    }

    /// <summary>
    /// Issue #114's Reset contract re-pinned under the composite key: Reset
    /// empties every bucket's stack (the handles in them are invalidated by
    /// <c>vkResetDescriptorPool</c>) while keeping the <c>Stack</c> instances.
    /// </summary>
    [Fact]
    public void Acquire_VariableCount_AfterReset_ReallocatesFresh()
    {
        TestGate.RequireDriver();
        TestGate.RequireDeviceFeature(
            VulkanDriverProbe.SupportsBindlessVariableCountStorageBuffer, FeatureGateReason);

        using var instance = Instance.Create(default);
        using var device   = CreateVariableCountDevice(instance);
        using var layout   = CreateVariableCountLayout(device, declaredCount: 32);
        using var pool     = CreatePool(device, budget: 256, maxSets: 4);

        DescriptorSet set = pool.Acquire(layout.Handle, 8);
        pool.Release(layout.Handle, set);

        pool.Reset();
        Assert.Equal(0, pool.AllocatedCount);

        DescriptorSet fresh = pool.Acquire(layout.Handle, 8);
        Assert.False(fresh.IsNull);
        Assert.Equal(1, pool.AllocatedCount);
    }

    /// <summary>
    /// Guard order: a disposed pool reports disposal, not a budget complaint,
    /// even when the requested count would also trip the budget guard.
    /// </summary>
    [Fact]
    public void Acquire_VariableCount_Guards()
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
            var pool = new DescriptorSetPool(device, maxSets: 4, sizes);

            Assert.Throws<ArgumentNullException>(() => pool.Acquire(null, 4));

            pool.Dispose();
            Assert.Throws<ObjectDisposedException>(() => pool.Acquire(layout, 999_999));
        }
        finally { Vk.vkDestroyDescriptorSetLayout(device.Handle, layout, null); }
    }

    // includeWarnings widens the capture to WARNING severity, which the
    // negative-control test needs: the only diagnostic that distinguishes
    // "no chain" from "a chain carrying zero" is the layer's allocation-time
    // WARNING-CoreValidation-AllocateDescriptorSets-VariableDescriptorCount.
    private static Instance CreateValidationInstance(List<DebugMessage> captured, bool includeWarnings = false)
    {
        VkDebugUtilsMessageSeverityFlagBitsEXT wanted =
            VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT;
        if (includeWarnings)
            wanted |= VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT;

        return Instance.Create(new InstanceDescription
        {
            ApiVersion       = VulkanVersion.V1_4,
            EnableValidation = true,
            DebugCallback    = m =>
            {
                if ((m.Severity & wanted) != 0)
                    lock (captured) captured.Add(m);
            },
        });
    }

    // The three descriptor-indexing bits a variable-count storage-buffer heap
    // needs, enabled at device-creation time. Same shape as
    // PipelineLayoutTests.CreateBindlessGraphicsDevice, with the
    // storage-buffer update-after-bind bit in place of the sampled-image one.
    private static Device CreateVariableCountDevice(Instance instance)
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
                f12.descriptorBindingPartiallyBound                = 1;
                f12.descriptorBindingVariableDescriptorCount       = 1;
                f12.descriptorBindingStorageBufferUpdateAfterBind  = 1;
            },
        });
    }

    // Binding 0 is the only binding, so it is trivially the highest binding
    // number (VUID-VkDescriptorSetLayoutBindingFlagsCreateInfo-pBindingFlags-03004),
    // and STORAGE_BUFFER is not one of the two dynamic types
    // VUID-VkDescriptorSetLayoutBindingFlagsCreateInfo-pBindingFlags-03015 forbids.
    private static DescriptorSetLayout CreateVariableCountLayout(Device device, uint declaredCount)
    {
        ReadOnlySpan<DescriptorBinding> bindings =
        [
            new DescriptorBinding
            {
                Slot         = 0,
                Type         = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
                Count        = declaredCount,
                Stages       = ShaderStages.Compute,
                BindingFlags = DescriptorBindingFlags.PartiallyBound
                             | DescriptorBindingFlags.UpdateAfterBind
                             | DescriptorBindingFlags.VariableDescriptorCount,
            },
        ];
        return device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            UpdateAfterBindPool = true,
            Bindings            = bindings,
        });
    }

    // updateAfterBind: true to match the layout's UpdateAfterBindPool
    // (VUID-VkDescriptorSetAllocateInfo-pSetLayouts-03044).
    private static DescriptorSetPool CreatePool(Device device, uint budget, uint maxSets)
    {
        ReadOnlySpan<VkDescriptorPoolSize> sizes =
        [
            new VkDescriptorPoolSize
            {
                type            = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
                descriptorCount = budget,
            },
        ];
        return new DescriptorSetPool(device, maxSets, sizes, updateAfterBind: true);
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
