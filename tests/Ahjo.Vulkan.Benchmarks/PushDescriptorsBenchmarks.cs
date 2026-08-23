using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;
using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Backs acceptance criterion 2 of issue 39:
/// <see cref="CommandRecorder.PushDescriptors{T}"/> allocates 0 B per call
/// after warmup. The recorder is created once per invocation; the inner
/// loop dispatches <c>CallsPerInvoke</c> push-descriptor calls so BDN's
/// per-op allocation column converges to 0.
/// </summary>
[MemoryDiagnoser]
public unsafe class PushDescriptorsBenchmarks
{
    private const int CallsPerInvoke = 1024;

    // Sixteen writes crosses CommandRecorder.PushDescriptorSet's
    // StackThreshold of 8 and DescriptorSetExtensions.Update's, so the
    // *_16 rows measure the ArrayPool leg both of them grew a second nested
    // rental on in #202 (the VkWriteDescriptorSetAccelerationStructureKHR
    // chains buffer). Without a row above the threshold neither overflow path
    // is reachable from any benchmark.
    private const int OverflowWrites = 16;

    private Instance               _instance = null!;
    private Device                 _device   = null!;
    private DescriptorSetLayout    _setLayout;
    private PipelineLayout         _pipelineLayout;
    private DescriptorTemplate<FillWrites> _template;
    private Buffer                 _buffer;
    private CommandBufferPool      _cmdPool  = null!;

    // The non-push half of the fixture. A push-descriptor set layout cannot be
    // used with vkAllocateDescriptorSets, so DescriptorSetExtensions.Update
    // needs a layout of its own plus a pool to allocate from.
    private DescriptorSetLayout    _updateLayout;
    private DescriptorSetPool      _setPool  = null!;
    private DescriptorSet          _set;

    // Hoisted: building the write array is setup, not part of the measured
    // body (the MeshShaderBenchmarks._colorFormats shape).
    private DescriptorWrite[]      _writes16 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _instance = Instance.Create(default);

        uint family = uint.MaxValue;
        var gpu = _instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsCompute)
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

        // The template writes binding 0 only; the LAYOUT declares all sixteen
        // so PushDescriptorSet_SpanWrites_16 has somewhere valid to push.
        // vkCmdPushDescriptorSet against a binding the layout does not declare
        // is a VU violation the driver answers with an access violation, not
        // an error code — which is exactly what the 16-write row hit before
        // the layout was widened. 16 is safely under the 32 minimum every
        // conformant device guarantees for maxPushDescriptors.
        DescriptorBinding[] bindings =
        [
            new DescriptorBinding
            {
                Slot = 0, Type = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
                Count = 1, Stages = ShaderStages.Compute,
            },
        ];
        var pushBindings = new DescriptorBinding[OverflowWrites];
        for (int i = 0; i < OverflowWrites; i++)
            pushBindings[i] = new DescriptorBinding
            {
                Slot = (uint)i, Type = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
                Count = 1, Stages = ShaderStages.Compute,
            };
        _setLayout = _device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            Bindings       = pushBindings,
            PushDescriptor = true,
        });
        DescriptorSetLayout[] layouts = [_setLayout];
        _pipelineLayout = _device.CreatePipelineLayout(new PipelineLayoutDescription
        {
            SetLayouts = layouts,
        });
        _template = _pipelineLayout.CreatePushDescriptorTemplate<FillWrites>(
            set: 0, VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_COMPUTE, bindings);

        _buffer = _device.Allocator.CreateBuffer(
            new BufferDescription { Size = 1024, Usage = BufferUsage.StorageBuffer },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        // Non-push layout + pool for Update_StorageBuffer. Sixteen bindings so
        // the same layout serves both the 1-write and the 16-write rows.
        var updateBindings = new DescriptorBinding[OverflowWrites];
        for (int i = 0; i < OverflowWrites; i++)
            updateBindings[i] = new DescriptorBinding
            {
                Slot = (uint)i, Type = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
                Count = 1, Stages = ShaderStages.Compute,
            };
        _updateLayout = _device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
        {
            Bindings = updateBindings,
        });

        VkDescriptorPoolSize[] poolSizes =
        [
            new VkDescriptorPoolSize
            {
                type            = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER,
                descriptorCount = OverflowWrites,
            },
        ];
        _setPool = new DescriptorSetPool(_device, maxSets: 1, poolSizes);
        _set     = _setPool.Acquire(_updateLayout.Handle);

        var info16 = BufferDescriptorWrite.Of(in _buffer);
        _writes16 = new DescriptorWrite[OverflowWrites];
        for (int i = 0; i < OverflowWrites; i++)
            _writes16[i] = DescriptorWrite.Buffer(
                binding: (uint)i, arrayElement: 0,
                VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER, in info16);

        _cmdPool = new CommandBufferPool(_device, family);

        // Warm — fault in the pool's first buffer so steady-state Begin/Dispose pairs hit reuse.
        var rec = _cmdPool.Begin();
        rec.End();
        rec.Dispose();

        // Warm the two new paths so the measured runs hit steady state.
        Update_StorageBuffer();
        PushDescriptorSet_SpanWrites_16();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _cmdPool?.Dispose();
        _setPool?.Dispose();
        _updateLayout.Dispose();
        _buffer.Dispose();
        _template.Dispose();
        _pipelineLayout.Dispose();
        _setLayout.Dispose();
        _device?.Dispose();
        _instance?.Dispose();
    }

    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public void PushDescriptors_StorageBuffer()
    {
        var writes = new FillWrites { Out = BufferDescriptorWrite.Of(in _buffer) };
        using var rec = _cmdPool.Begin();
        for (int i = 0; i < CallsPerInvoke; i++)
            rec.PushDescriptors(in _template, in _pipelineLayout, in writes);
    }

    /// <summary>
    /// Covers the non-templated
    /// <see cref="CommandRecorder.PushDescriptorSet(VkPipelineBindPoint, in PipelineLayout, uint, System.ReadOnlySpan{DescriptorWrite})"/>
    /// span overload — the path that routes the cached
    /// <c>vkCmdPushDescriptorSet</c> pointer through <c>FlushPush</c> (issue
    /// #121). A single-element write stays on the <c>≤ 8</c> stackalloc fast
    /// path, so steady-state <b>Allocated</b> must read <c>-</c>.
    /// </summary>
    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public void PushDescriptorSet_SpanWrites()
    {
        var info = BufferDescriptorWrite.Of(in _buffer);
        ReadOnlySpan<DescriptorWrite> writes =
        [
            DescriptorWrite.Buffer(
                binding: 0, arrayElement: 0,
                VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER, in info),
        ];

        // `scoped` narrows the recorder's safe-to-escape to this method so
        // the method-local span above can flow into PushDescriptorSet
        // without tripping CS8350 (mirrors CommandRecorderBenchmarks).
        using scoped var rec = _cmdPool.Begin();
        for (int i = 0; i < CallsPerInvoke; i++)
            rec.PushDescriptorSet(
                VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_COMPUTE,
                in _pipelineLayout, set: 0, writes);
    }

    /// <summary>
    /// Covers <see cref="DescriptorSetExtensions.Update"/> — the <b>other</b>
    /// caller of <c>DescriptorWriteBuilder.BuildWrites</c>, and until #202 the
    /// one with no benchmark anywhere in the repo. It is a genuine per-frame
    /// path for engines that rebuild descriptor sets rather than push, and it
    /// grew the same <c>chains</c> span every push-descriptor call did, so it
    /// carries the same 0 B/op obligation. One write stays on the <c>≤ 8</c>
    /// stackalloc leg.
    /// </summary>
    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public void Update_StorageBuffer()
    {
        var info = BufferDescriptorWrite.Of(in _buffer);
        ReadOnlySpan<DescriptorWrite> writes =
        [
            DescriptorWrite.Buffer(
                binding: 0, arrayElement: 0,
                VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER, in info),
        ];
        for (int i = 0; i < CallsPerInvoke; i++)
            _set.Update(_device, writes);
    }

    /// <summary>
    /// <see cref="CommandRecorder.PushDescriptorSet(VkPipelineBindPoint, in PipelineLayout, uint, System.ReadOnlySpan{DescriptorWrite})"/>
    /// with sixteen writes — <b>above</b> the recorder's <c>StackThreshold</c>
    /// of 8, so this is the only benchmark that reaches the
    /// <see cref="System.Buffers.ArrayPool{T}"/> leg. #202 added a second
    /// nested rental there (the <c>chains</c> buffer), and a rent/return pair
    /// per call must still amortize to <c>-</c> in <b>Allocated</b>: a
    /// non-<c>-</c> reading here means a rental is escaping or the arrays are
    /// not being returned.
    /// </summary>
    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public void PushDescriptorSet_SpanWrites_16()
    {
        using scoped var rec = _cmdPool.Begin();
        for (int i = 0; i < CallsPerInvoke; i++)
            rec.PushDescriptorSet(
                VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_COMPUTE,
                in _pipelineLayout, set: 0, _writes16);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FillWrites
    {
        public BufferDescriptorWrite Out;
    }
}
