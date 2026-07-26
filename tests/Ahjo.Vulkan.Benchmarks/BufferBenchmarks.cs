using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Vma.Native;
using BenchmarkDotNet.Attributes;
using VmaApi = Ahjo.Vulkan.Vma.Native.Vma;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Measures <see cref="Buffer.AsSpan{T}"/> on a persistent-mapped host-visible
/// buffer. <see cref="Buffer.Map{T}"/> is deliberately not benchmarked because
/// <c>Map</c> instantiates a <see cref="MappedRegion{T}"/> class — that
/// allocation is a property of the <c>MemoryManager{T}</c>-shaped API, not the
/// hot path. <c>AsSpan</c> is the alloc-free surface the engine uses for
/// per-frame uniform updates, and every row here must read <c>-</c> in the
/// <c>Allocated</c> column.
/// </summary>
/// <remarks>
/// <para><b>One op = one <c>AsSpan&lt;int&gt;()</c> call through a
/// <see cref="MethodImplOptions.NoInlining"/> static helper.</b> Every row
/// shares that same call boundary and differs only in what the helper does
/// with the span, so a difference between two rows localizes the cost to the
/// work that differs.</para>
/// <para><b>Why the boundary exists.</b> <c>AsSpan</c> on a loop-invariant
/// field is itself loop-invariant, so an inlined call is a candidate for
/// hoisting straight out of the loop — an inlined row would measure the loop
/// counter, not the API. <see cref="AsSpan_ViewOnly"/> is therefore an
/// <b>upper bound</b>: it includes a non-inlined call a real call site would
/// not pay. The fully inlined cost is not observable by a wall-clock
/// benchmark; read <c>Resources/Buffer.cs</c> for that.</para>
/// <para><b>Why <see cref="AsSpan_ViewOnly"/> never indexes the span.</b> It
/// consumes the span's pointer XOR its length — enough to force the span to be
/// materialised, without dereferencing it. Issue #157: the old
/// <c>Map_AsSpan</c> row did <c>sum += span[0]</c>, so its 166.8 ns was a host
/// read from (likely uncached, write-combined) mapped memory rather than the
/// cost of the API it named.</para>
/// <para><b>The two <c>WriteThenRead</c> rows are memory-behaviour probes, not
/// wrapper canaries.</b> Identical bodies, identical size and buffer usage, one
/// allocation flag apart. Their Means are properties of the host's memory
/// topology; only the ratio between them carries information. <c>[GlobalSetup]</c>
/// prints the memory type VMA actually selected for each allocation so the rows
/// stay interpretable on a host other than the one in <c>docs/benchmarks.md</c>.</para>
/// <para>Nothing here is ever submitted to a queue and no GPU-written data is
/// read, so no <see cref="Buffer.Flush"/>/<see cref="Buffer.Invalidate"/>
/// bracketing is involved.</para>
/// </remarks>
[MemoryDiagnoser]
public unsafe class BufferBenchmarks
{
    private const int CallsPerInvoke = 1024;

    private Instance _instance = null!;
    private Device   _device   = null!;

    private Buffer _seqWrite;   // HostAccessSequentialWrite | Mapped
    private Buffer _hostRandom; // HostAccessRandom          | Mapped

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

        // One description for both allocations: the seq-write / random A/B is
        // worthless if anything other than the host-access flag differs.
        var desc = new BufferDescription
        {
            Size  = CallsPerInvoke * sizeof(int),   // 4096 — one int per op, so one invoke fills the buffer once
            Usage = BufferUsage.UniformBuffer,
        };
        _seqWrite = _device.Allocator.CreateBuffer(desc, new AllocationDescription
        {
            Usage = MemoryUsage.AutoPreferHost,
            Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
        });
        _hostRandom = _device.Allocator.CreateBuffer(desc, new AllocationDescription
        {
            Usage = MemoryUsage.AutoPreferHost,
            Flags = AllocationFlags.HostAccessRandom | AllocationFlags.Mapped,
        });

        // AsSpan_SequentialWrite writes span[i] for i in [0, CallsPerInvoke),
        // so Size and CallsPerInvoke are coupled. Fail loudly if either is
        // edited without the other — the benchmark project has no soft-skip
        // path (docs/benchmarks.md), so this throws rather than skips.
        int length = _seqWrite.AsSpan<int>().Length;
        if (length != CallsPerInvoke)
            throw new InvalidOperationException(
                $"BufferBenchmarks: expected a {CallsPerInvoke}-int span, got {length}. " +
                "Size and CallsPerInvoke must stay in sync — AsSpan_SequentialWrite writes one int per op.");

        ReportMemoryType("seq-write alloc", in _seqWrite);
        ReportMemoryType("host-random alloc", in _hostRandom);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _hostRandom.Dispose();
        _seqWrite.Dispose();
        _device?.Dispose();
        _instance?.Dispose();
    }

    /// <summary>
    /// What the API costs: one <c>AsSpan&lt;int&gt;()</c> per op, consuming the
    /// span's pointer + length and touching no device-visible memory. An upper
    /// bound — it includes the non-inlined call.
    /// </summary>
    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public nuint AsSpan_ViewOnly()
    {
        nuint acc = 0;
        for (int i = 0; i < CallsPerInvoke; i++)
        {
            acc ^= SpanIdentity(in _seqWrite);
        }
        return acc;
    }

    /// <summary>
    /// What the flag's endorsed pattern costs: one <c>AsSpan&lt;int&gt;()</c> +
    /// one sequential <c>int</c> store per op, so one invoke is exactly one
    /// 4 KiB sequential fill of a <c>HostAccessSequentialWrite</c> allocation.
    /// </summary>
    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public void AsSpan_SequentialWrite()
    {
        for (int i = 0; i < CallsPerInvoke; i++)
        {
            WriteOne(in _seqWrite, i, i);
        }
    }

    /// <summary>
    /// Memory-behaviour probe (#157), not a wrapper canary: the pre-#157
    /// <c>Map_AsSpan</c> body verbatim — store then read the same element back —
    /// on a <c>HostAccessSequentialWrite</c> allocation.
    /// </summary>
    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public int AsSpan_WriteThenRead_SeqWriteAlloc()
    {
        int sum = 0;
        for (int i = 0; i < CallsPerInvoke; i++)
        {
            sum += WriteThenReadFirst(in _seqWrite, i);
        }
        return sum;
    }

    /// <summary>
    /// Memory-behaviour probe (#157), not a wrapper canary: the identical body
    /// on a <c>HostAccessRandom</c> allocation. Only its ratio against
    /// <see cref="AsSpan_WriteThenRead_SeqWriteAlloc"/> carries information.
    /// </summary>
    [Benchmark(OperationsPerInvoke = CallsPerInvoke)]
    public int AsSpan_WriteThenRead_RandomAlloc()
    {
        int sum = 0;
        for (int i = 0; i < CallsPerInvoke; i++)
        {
            sum += WriteThenReadFirst(in _hostRandom, i);
        }
        return sum;
    }

    // Materialises the span and consumes its pointer + length. Never indexes
    // it: this row must not touch device-visible memory (that is the #157
    // defect). Do not "simplify" this to return span.Length alone — the
    // pointer operand is what forces the span to be materialised.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static nuint SpanIdentity(in Buffer buffer)
    {
        Span<int> span = buffer.AsSpan<int>();
        return (nuint)Unsafe.AsPointer(ref MemoryMarshal.GetReference(span)) ^ (nuint)span.Length;
    }

    // One sequential store — the pattern HostAccessSequentialWrite promises.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteOne(in Buffer buffer, int index, int value)
        => buffer.AsSpan<int>()[index] = value;

    // The pre-#157 body verbatim: store then read the SAME element back.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int WriteThenReadFirst(in Buffer buffer, int value)
    {
        Span<int> span = buffer.AsSpan<int>();
        span[0] = value;
        return span[0];
    }

    // Prints what VMA actually selected, once per benchmark process per
    // allocation. Observes without asserting: HOST_CACHED is only a
    // not-preferred penalty for HostAccessSequentialWrite
    // (native/vma/include/vk_mem_alloc.h:4085-4090) and some platforms expose
    // no uncached host-visible type at all, so an assertion here would fail on
    // integrated/UMA hosts. Allocates (interpolation, enum ToString) but runs
    // outside every measured region.
    private static void ReportMemoryType(string label, in Buffer buffer)
    {
        uint flags = 0;
        VmaApi.vmaGetAllocationMemoryProperties(buffer.Owner.Handle, buffer.AllocationHandle, &flags);

        VmaAllocationInfo info = default;
        VmaApi.vmaGetAllocationInfo(buffer.Owner.Handle, buffer.AllocationHandle, &info);

        VkPhysicalDeviceMemoryProperties* props = null;
        VmaApi.vmaGetMemoryProperties(buffer.Owner.Handle, &props);

        VkMemoryType* types = &props->memoryTypes.e0;
        uint heap = types[info.memoryType].heapIndex;
        VkMemoryHeap* heaps = &props->memoryHeaps.e0;
        ulong heapSize = heaps[heap].size;

        Console.WriteLine(
            $"[BufferBenchmarks] {label}: memoryType={info.memoryType} " +
            $"flags={(MemoryProperties)flags} heap={heap} heapSizeMiB={heapSize / (1024 * 1024)}");
    }
}
