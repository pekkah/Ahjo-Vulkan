using Ahjo.Vulkan.Native;
using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Backs issue 114: a per-frame <c>Acquire → Release → Reset</c> cycle on a
/// <see cref="DescriptorSetPool"/> must stay <b>0 B per cycle</b> after
/// warmup. The original <see cref="DescriptorSetPool.Reset"/> cleared the
/// per-layout idle dictionary outright, so the next frame's
/// <see cref="DescriptorSetPool.Release"/> re-allocated a <c>Stack&lt;nint&gt;</c>
/// (plus its backing array) per layout every frame; the fix empties each
/// stack but retains the instances. The pool is pre-warmed in
/// <see cref="Setup"/> so the steady-state cycle reuses the existing stack
/// and <c>_allHandles</c> capacity instead of growing them; the inner loop
/// runs <c>CallsPerInvoke</c> cycles per invocation so BDN's per-op
/// allocation column converges to <c>-</c>.
/// </summary>
[MemoryDiagnoser]
public unsafe class DescriptorSetPoolBenchmarks
{
    private const int CallsPerInvoke = 1000;

    private Instance                _instance = null!;
    private Device                  _device   = null!;
    private DescriptorSetLayout     _layout;
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
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });

        DescriptorBinding[] bindings =
        [
            new DescriptorBinding
            {
                Slot = 0, Type = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                Count = 1, Stages = ShaderStages.Vertex,
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
                type = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                descriptorCount = 64,
            },
        ];
        _pool = new DescriptorSetPool(_device, maxSets: 64, sizes);

        // Warm — run the full Acquire→Release→Reset cycle a couple of times so
        // the per-layout Stack exists with a sized backing array and
        // _allHandles has capacity. After this, the steady-state cycle hits the
        // retained free-list rather than growing it (issue 114).
        for (int i = 0; i < 2; i++)
        {
            var set = _pool.Acquire(_layoutHandle);
            _pool.Release(_layoutHandle, set);
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

    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public void AcquireReleaseReset_Cycle()
    {
        for (int i = 0; i < CallsPerInvoke; i++)
        {
            var set = _pool.Acquire(_layoutHandle);
            _pool.Release(_layoutHandle, set);
            _pool.Reset();
        }
    }
}
