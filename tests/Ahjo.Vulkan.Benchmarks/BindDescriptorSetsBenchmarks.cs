using Ahjo.Vulkan.Native;
using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Backs issue 188: <see cref="CommandRecorder.BindDescriptorSets"/> is a
/// per-frame hot path (bind happens on essentially every draw) that had no
/// benchmark — <see cref="PushDescriptorsBenchmarks"/> covers the
/// push-descriptor path only, a different call with a different allocation
/// story.
/// <para>The part worth pinning is the handle copy: the recorder copies each
/// <see cref="DescriptorSet"/>'s <c>Handle</c> out of the
/// <c>ReadOnlySpan&lt;DescriptorSet&gt;</c> into a
/// <c>stackalloc nint[sets.Length]</c> before the native call. For the
/// <c>sets.Length &lt;= 32</c> case that copy is stack-only, so steady-state
/// <b>Allocated</b> must read <c>-</c>. Two sizes are recorded: 1 set (the
/// common single-bind case) and 4 sets (where the per-element copy starts to
/// matter but still stays on the stackalloc branch).</para>
/// <para>The second reason this exists: the copy cost is proportional to
/// <c>sizeof(DescriptorSet)</c>, and the struct
/// (<c>Pipelines/DescriptorSet.cs</c>) lives in a different file from the loop
/// (<c>Recording/CommandRecorder.cs</c>), so an unrelated change to the struct
/// silently changes this path's cost. #182 grew <c>DescriptorSet</c> from 16
/// to 24 bytes and nothing measured the consequence; this benchmark is that
/// missing measurement.</para>
/// </summary>
[MemoryDiagnoser]
public unsafe class BindDescriptorSetsBenchmarks
{
    private const int CallsPerInvoke = 1024;

    // 4 sets is the larger bind size. maxBoundDescriptorSets is >= 4 by spec
    // (the guaranteed floor — SwiftShader reports exactly 4), so 4 pipeline-
    // layout slots and a 4-set bind are portable on every conformant device,
    // and both bind sizes stay on the recorder's <= 32 stackalloc branch. A
    // larger count would only be safe after querying the device limit; 4 is
    // enough to make the per-element handle copy this benchmark exists to
    // measure show up against the single-set case.
    private const int SetCount = 4;

    private Instance                 _instance = null!;
    private Device                   _device   = null!;
    private CommandBufferPool        _cmdPool  = null!;
    private DescriptorSetLayout      _setLayout;
    private VkDescriptorSetLayout_T* _layoutHandle;
    private PipelineLayout           _pipelineLayout;
    private DescriptorSetPool        _pool = null!;
    private DescriptorSet[]          _sets = null!;

    [GlobalSetup]
    public void Setup()
    {
        _instance = Instance.Create(default);

        uint family = uint.MaxValue;
        var gpu = _instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
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
        _setLayout = _device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            Bindings = bindings,
        });
        _layoutHandle = _setLayout.Handle;

        // A pipeline layout declaring SetCount slots, every one the same
        // descriptor set layout — a VkPipelineLayout may list the same
        // VkDescriptorSetLayout at multiple set indices. This makes the
        // SetCount-set bind at firstSet 0 valid, and lets the recorder's
        // AssertSetsMatchLayout pass when validation is enabled (it is off in
        // Release, where these run, but the recorded bind stays Vulkan-correct
        // either way).
        DescriptorSetLayout[] layouts = new DescriptorSetLayout[SetCount];
        for (int i = 0; i < SetCount; i++) layouts[i] = _setLayout;
        _pipelineLayout = _device.CreatePipelineLayout(new PipelineLayoutDescription
        {
            SetLayouts = layouts,
        });

        ReadOnlySpan<VkDescriptorPoolSize> sizes =
        [
            new VkDescriptorPoolSize
            {
                type = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                descriptorCount = SetCount,
            },
        ];
        _pool = new DescriptorSetPool(_device, maxSets: SetCount, sizes);

        _cmdPool = new CommandBufferPool(_device, family);

        // Acquire the sets once, up front. They are bound repeatedly but never
        // written or submitted, so the same handles serve every iteration and
        // there is nothing to release between binds.
        _sets = new DescriptorSet[SetCount];
        for (int i = 0; i < SetCount; i++)
            _sets[i] = _pool.Acquire(_layoutHandle);

        // Warm — fault in the pool's first command buffer and JIT the bind
        // path so the steady-state Begin/End pairs hit reuse and the recorded
        // bind surface is tier-1+.
        Bind_1Set();
        Bind_4Sets();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _cmdPool?.Dispose();
        _pool?.Dispose();
        _pipelineLayout.Dispose();
        _setLayout.Dispose();
        _device?.Dispose();
        _instance?.Dispose();
    }

    /// <summary>
    /// Binds a single descriptor set — the common per-draw case. One set stays
    /// well inside the recorder's <c>&lt;= 32</c> stackalloc branch, so
    /// steady-state <b>Allocated</b> must read <c>-</c>.
    /// </summary>
    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public void Bind_1Set()
    {
        // Dispose the recorder (inner-block scope) BEFORE ResetForFrame: Retire
        // fires on Dispose, not End, so the buffer must be retired to _spent
        // before the reset drains _spent → _idle, or it never recycles. This
        // also keeps the benchmark valid under AHJO_VULKAN_TIER=validation,
        // where ResetForFrame asserts on an outstanding recorder.
        using (var rec = _cmdPool.Begin())
        {
            for (int i = 0; i < CallsPerInvoke; i++)
                rec.BindDescriptorSets(
                    VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_GRAPHICS,
                    in _pipelineLayout, firstSet: 0, _sets.AsSpan(0, 1));
            rec.End();
        }
        _cmdPool.ResetForFrame();
    }

    /// <summary>
    /// Binds 4 descriptor sets — still on the <c>&lt;= 32</c> stackalloc
    /// branch, so the per-element handle copy grows but stays allocation-free;
    /// <b>Allocated</b> must read <c>-</c>. The gap to <see cref="Bind_1Set"/>
    /// is the copy cost, which is proportional to <c>sizeof(DescriptorSet)</c>
    /// — the coupling this benchmark exists to pin.
    /// </summary>
    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public void Bind_4Sets()
    {
        using (var rec = _cmdPool.Begin())
        {
            for (int i = 0; i < CallsPerInvoke; i++)
                rec.BindDescriptorSets(
                    VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_GRAPHICS,
                    in _pipelineLayout, firstSet: 0, _sets.AsSpan(0, SetCount));
            rec.End();
        }
        _cmdPool.ResetForFrame();
    }
}
