using System.Runtime.CompilerServices;
using Ahjo.Vulkan.Native;
using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Backs the issue #118 constraint decision: <see cref="PipelineLayout"/>
/// carries its declared push ranges / set layouts as one managed reference
/// field (the relaxed <c>IVulkanHandle</c> contract) instead of in static
/// side tables. These benchmarks prove a one-reference handle struct stays
/// allocation-free to copy, pass, and interrogate on a tight loop — every
/// <c>Allocated</c> cell must read <c>-</c>. Driver-free: no
/// <c>VkInstance</c> is created; the handles wrap sentinel pointers that
/// are never dispatched.
/// </summary>
[MemoryDiagnoser]
public unsafe class HandleOwnershipBenchmarks
{
    private PipelineLayout _owningLayout;
    private PipelineLayout _borrowedLayout;
    private Fence          _fence;

    [GlobalSetup]
    public void Setup()
    {
        var metadata = new PipelineLayoutMetadata
        {
            PushRanges = [new PushConstantRange { Stages = ShaderStages.Vertex, Offset = 0, Size = 128 }],
            SetLayouts = [0x4000, 0x4008],
        };
        _owningLayout   = new PipelineLayout((VkPipelineLayout_T*)0x2000, (VkDevice_T*)0x1000, metadata);
        _borrowedLayout = PipelineLayout.FromRaw(0x2000);
        _fence          = new Fence((VkFence_T*)0x3000, (VkDevice_T*)0x1000);
    }

    [Benchmark(OperationsPerInvoke = 1_000_000)]
    public ulong PassAndReturn_ByValue_TightLoop()
    {
        // The per-frame shape: a handle struct copied through a call chain.
        // With the managed Metadata field the struct is GC-trackable; this
        // proves copies stay on the stack — zero managed bytes.
        ulong sum = 0;
        for (var i = 0; i < 1_000_000; i++)
        {
            sum += RoundTrip(_owningLayout);
        }
        return sum;
    }

    [Benchmark(OperationsPerInvoke = 1_000_000)]
    public int MetadataRead_OwningAndBorrowed_TightLoop()
    {
        // What CommandRecorder's debug assertions do per bind/push: one
        // field read replacing the old dictionary-lookup-under-lock.
        var hits = 0;
        for (var i = 0; i < 1_000_000; i++)
        {
            if (_owningLayout.Metadata is { } m && m.SetLayouts.Length != 0) hits++;
            if (_borrowedLayout.Metadata is null) hits++;
        }
        return hits;
    }

    [Benchmark(OperationsPerInvoke = 1_000_000)]
    public int OwnershipPredicate_TightLoop()
    {
        // The Dispose guard / borrow check on the per-frame sync type.
        var owned = 0;
        for (var i = 0; i < 1_000_000; i++)
        {
            if (_owningLayout.OwnsHandle) owned++;
            if (_fence.OwnsHandle) owned++;
        }
        return owned;
    }

    [Benchmark(OperationsPerInvoke = 1_000_000)]
    public ulong ConstrainedGenericDispatch_TightLoop()
    {
        // ObjectName.Set's shape: ObjectType + RawHandle through the
        // `struct, IVulkanHandle<T>` constraint — must devirtualize and
        // stay box-free now that the constraint is no longer `unmanaged`.
        ulong sum = 0;
        for (var i = 0; i < 1_000_000; i++)
        {
            sum += DescribeHandle(_owningLayout);
        }
        return sum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong RoundTrip(PipelineLayout layout) => layout.RawHandle;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong DescribeHandle<T>(T handle) where T : struct, IVulkanHandle<T>
        => (ulong)T.ObjectType ^ handle.RawHandle;
}
