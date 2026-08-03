using Ahjo.Vulkan.Native;
using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Issue 182: the variable-descriptor-count <c>Acquire</c> overload runs on the
/// same per-frame path as its one-argument sibling, so it carries the same
/// <b>0 B per cycle</b> obligation. Two things could break it and neither is
/// visible by reading the code: the chained
/// <c>VkDescriptorSetVariableDescriptorCountAllocateInfo</c> must stay a stack
/// local (it does — both it and the <c>uint</c> it points at live in the frame
/// that makes the native call), and the composite
/// <c>(layout, count)</c> free-list key must not box on the dictionary lookup
/// (a <c>readonly record struct</c> gets <see cref="IEquatable{T}"/>, so
/// <c>EqualityComparer&lt;T&gt;.Default</c> devirtualizes).
/// </summary>
/// <remarks>
/// Deliberately a separate class from <see cref="DescriptorSetPoolBenchmarks"/>:
/// this <see cref="Setup"/> requires an optional device feature
/// (<c>descriptorBindingVariableDescriptorCount</c>) and a host without it must
/// not take the issue-114 canary down with it. The fixture asks for that one
/// bit and nothing else — no <c>PartiallyBound</c>, no <c>UpdateAfterBind</c>,
/// no update-after-bind pool — which is the minimum Vulkan requires for a
/// variable-count binding.
/// </remarks>
[MemoryDiagnoser]
public unsafe class DescriptorSetPoolVariableCountBenchmarks
{
    private const int CallsPerInvoke = 1000;
    private const uint CountA = 256;
    private const uint CountB = 512;

    private Instance                 _instance = null!;
    private Device                   _device   = null!;
    private DescriptorSetLayout      _layout;
    private VkDescriptorSetLayout_T* _layoutHandle;
    private DescriptorSetPool        _pool = null!;

    [GlobalSetup]
    public void Setup()
    {
        _instance = Instance.Create(default);

        uint family = uint.MaxValue;
        var gpu = _instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics ||
                    info.QueueFamilies[i].SupportsCompute)
                {
                    family = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });
        _device = gpu.CreateDevice(new DeviceDescription
        {
            Queues            = [new QueueRequest(family, count: 1, priority: 1.0f)],
            ConfigureFeatures = static (
                ref ChainBuilder<VkDeviceCreateInfo> _,
                ref VkPhysicalDeviceFeatures2 _,
                ref VkPhysicalDeviceVulkan12Features f12,
                ref VkPhysicalDeviceVulkan13Features _,
                ref VkPhysicalDeviceVulkan14Features _) =>
            {
                f12.descriptorBindingVariableDescriptorCount = 1;
            },
        });

        DescriptorBinding[] bindings =
        [
            new DescriptorBinding
            {
                Slot = 0, Type = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
                Count = 4096, Stages = ShaderStages.Compute,
                BindingFlags = DescriptorBindingFlags.VariableDescriptorCount,
            },
        ];
        _layout = _device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            Bindings = bindings,
        });
        _layoutHandle = _layout.Handle;

        ReadOnlySpan<VkDescriptorPoolSize> sizes =
        [
            new VkDescriptorPoolSize
            {
                type = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
                descriptorCount = 4096,
            },
        ];
        _pool = new DescriptorSetPool(_device, maxSets: 64, sizes);

        // Warm both counts, not just one: each distinct count is its own
        // free-list bucket, so both dictionary entries and both Stack backing
        // arrays must exist before measurement or the two-count benchmark
        // measures dictionary growth instead of steady state.
        for (int i = 0; i < 2; i++)
        {
            var a = _pool.Acquire(_layoutHandle, CountA);
            _pool.Release(_layoutHandle, a);
            var b = _pool.Acquire(_layoutHandle, CountB);
            _pool.Release(_layoutHandle, b);
            _pool.Reset();
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _pool?.Dispose();
        _layout.Dispose();
        _device?.Dispose();
        _instance?.Dispose();
    }

    /// <summary>
    /// Single-count steady state: proves the chained allocate-info is
    /// stack-only and the <c>IdleKey</c> lookup does not box.
    /// </summary>
    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public void AcquireReleaseReset_VariableCount_Cycle()
    {
        for (int i = 0; i < CallsPerInvoke; i++)
        {
            var set = _pool.Acquire(_layoutHandle, CountA);
            _pool.Release(_layoutHandle, set);
            _pool.Reset();
        }
    }

    /// <summary>
    /// Two distinct counts per cycle — the supported bounded-count case, and
    /// the one the composite key could regress in exactly the issue-114 shape
    /// (a fresh <c>Stack&lt;nint&gt;</c> per bucket per frame).
    /// </summary>
    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public void AcquireReleaseReset_TwoCounts_Cycle()
    {
        for (int i = 0; i < CallsPerInvoke; i++)
        {
            var a = _pool.Acquire(_layoutHandle, CountA);
            _pool.Release(_layoutHandle, a);
            var b = _pool.Acquire(_layoutHandle, CountB);
            _pool.Release(_layoutHandle, b);
            _pool.Reset();
        }
    }
}
