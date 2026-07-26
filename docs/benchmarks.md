# Benchmarks

The wrapper's "zero per-frame allocations" claim is a regression-prevention
target, not a marketing line. This page is the host-captured baseline that
the BenchmarkDotNet harness in `tests/Ahjo.Vulkan.Benchmarks/` produces, with
`[MemoryDiagnoser]` enabled on every benchmark class so the **Allocated**
column is the canary: any non-zero entry means a hot path leaked through to
the managed heap.

## How to run

The benchmark project is a normal exe. Pick a filter and run in Release —
Debug builds short-circuit JIT tiering and produce noisy numbers:

```
dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter "*"
```

Useful subsets:

```
# Allocation-only round-trips (no driver required):
dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter "*ChainBuilder*|*ResultPolicy*|*HandleOwnership*"

# Driver-bound: needs a real Vulkan ICD on the host. Fails at GlobalSetup
# if the host cannot create a VkInstance.
dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter "*FrameRing*|*PushDescriptors*|*PipelineBarrier*|*CommandRecorder*|*BufferBenchmarks*"
```

`BenchmarkDotNet.Artifacts/` is gitignored — the run produces CSV / Markdown
/ HTML reports under that folder per benchmark class.

## What we measure

The benchmark surface tracks the engine's per-frame hot paths:

- **0 B/op canary** — every benchmark uses `[MemoryDiagnoser]`. The
  steady-state value should be `-` (BDN's marker for zero managed bytes).
  Any number larger than `-` flags a wrapper change that needs investigation
  — either the wrapper started allocating on a hot path, or the benchmark
  itself regressed.
- **Mean** — for sense-checking, not as a contract. Per-iteration timings
  are noisy on a desktop host (background processes, thermal throttling,
  driver overhead). The wrapper's intent is the allocation column; the
  timing column is informational.

The numbers below were captured on a single Windows desktop and are
host-dependent. Treat them as a **baseline for regressions**, not as an
absolute SLA.

## Host

Captured on:

- **CPU**: AMD Ryzen 9 7900X (24 logical / 12 physical)
- **OS**: Windows 11 (10.0.26200)
- **Runtime**: .NET 10.0.7 (SDK 10.0.203), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
- **BenchmarkDotNet**: v0.14.0
- **Vulkan**: instance v1.4.341 (vulkaninfo)

The table is **not a single capture**. The four `PipelineBarrier.*` rows were
recaptured for #155 on .NET 10.0.8 (SDK 10.0.204) / Windows 11 10.0.26200.8894
with an NVIDIA RTX 4070 Ti; the `HandleOwnership.*` rows came from a Linux
container. Rows are comparable to their own successors, not to each other —
re-measure the row you care about before drawing a conclusion from it.

## Baseline

Each row maps to one `[Benchmark]` method. **Allocated** is the per-op
managed-byte count BDN's `MemoryDiagnoser` reports; `-` is zero.

| Benchmark                                       | Mean       | Allocated | Notes                                                                     |
|-------------------------------------------------|-----------:|----------:|---------------------------------------------------------------------------|
| `ChainBuilder.BuildThreeNodeChain`              |   3.66 ns  |        -  | Pure host: features2 + vk13 + vk12 over a stack-only `ChainBuilder`.      |
| `Buffer.AsSpan_ViewOnly`                        |   1.54 ns  |        -  | Persistent-mapped `AsSpan<T>` through a non-inlined helper, consuming pointer + length and **touching no device memory** — an upper bound on the API (includes the call). |
| `Buffer.AsSpan_SequentialWrite`                 |   1.85 ns  |        -  | One `AsSpan<T>` + one sequential `int` store per op; one invoke = one 4 KiB sequential fill of a `HostAccessSequentialWrite` allocation. |
| `Buffer.AsSpan_WriteThenRead_SeqWriteAlloc`     | 173.4 ns   |        -  | #157 probe, **not a wrapper canary**: store + read-back of the same element on a `HostAccessSequentialWrite` allocation. This is the former `Buffer.Map_AsSpan` (166.8 ns) renamed — the number did not change. |
| `Buffer.AsSpan_WriteThenRead_RandomAlloc`       |   1.53 ns  |        -  | #157 probe, **not a wrapper canary**: identical body on a `HostAccessRandom` allocation. Only the ratio against the row above carries information. |
| `CommandBufferPool.Frame_Begin_100Cmds_End_Reset` | 6.44 µs |        -  | Begin → 100 dynamic-state + fill commands → End → ResetForFrame.          |
| `CommandRecorder.RenderingPass100Cmds`          |   3.23 µs  |        -  | BeginRendering → 100 SetViewport → EndRendering, dynamic rendering path.  |
| `CommandRecorder.CopyBuffer_8Regions`           | 810.0 ns   |        -  | #141 canary: multi-region CopyBuffer; stackalloc ≤16 path stays 0 B/op.   |
| `CommandRecorder.CopyBuffer_24Regions`          |   1.57 µs  |        -  | #141 canary: multi-region CopyBuffer; ArrayPool >16 path stays 0 B/op.    |
| `PipelineBarrier.SingleImageTransition`         | 178.8 ns   |        -  | One `vkCmdPipelineBarrier2` with a single image barrier. Recaptured for #155 — see the split-barrier caveat. |
| `PipelineBarrier.LargeBatch_8x8x1`              |   2.80 µs  |        -  | One `vkCmdPipelineBarrier2` with 64 image barriers. Recaptured for #155 — **minimum of 5 runs**; strongly bimodal (median 3.61 µs, range 2.80–4.61 µs), so compare minima across ≥3 runs, not single samples. |
| `PipelineBarrier.SetWaitEventPair_SingleImage`  | 260.7 ns   |        -  | #155 canary: one `vkCmdSetEvent2` + `vkCmdWaitEvents2` pair, one image barrier each. Recording only, never submitted. |
| `PipelineBarrier.ResetEvent_Single`             |  33.4 ns   |        -  | #155 canary: one `vkCmdResetEvent2` — bare stage-mask pass-through, no dependency marshalling. |
| `FrameRing.Frame_Begin_Submit_Wait`             |  56.20 µs  |        -  | Full headless frame: BeginFrame → submit no-op cmd → wait fence.          |
| `PushDescriptors.PushDescriptors_StorageBuffer` |  69.34 ns  |        -  | `vkCmdPushDescriptorSetWithTemplate` × 1024 in one Begin/End scope; bimodal under driver overhead. |
| `DescriptorSetPool.AcquireReleaseReset_Cycle`   |  39.62 ns  |        -  | #114 canary: per-frame Acquire → Release → Reset; Reset retains the per-layout idle `Stack`s instead of discarding them. |
| `HandleOwnership.PassAndReturn_ByValue`         |   3.69 ns  |        -  | #118 canary: `PipelineLayout` (one managed metadata ref) copied through a non-inlined call — stays stack-only, no write barrier, no box. Captured on a Linux container host (driver-free benchmark). |
| `HandleOwnership.MetadataRead_OwningAndBorrowed` |  0.92 ns  |        -  | Field read replacing the old side-table dictionary lookup + lock.       |
| `HandleOwnership.OwnershipPredicate`            |   0.47 ns  |        -  | `OwnsHandle` — the Dispose guard / borrow check.                        |
| `HandleOwnership.ConstrainedGenericDispatch`    |   3.69 ns  |        -  | `ObjectName.Set`-shaped `struct, IVulkanHandle<T>` dispatch — devirtualized, box-free under the relaxed constraint. |

**Reading the Mean column.** Many benchmarks here unroll an inner loop and
declare `OperationsPerInvoke` (e.g. `Buffer.AsSpan_*`, `PipelineBarrier.*`,
`PushDescriptors.*`, `PushConstants.*`, `SyncPool.*`, `ResultPolicy.*`,
`HandleOwnership.*`, `DescriptorSetPool.*` — not an exhaustive list). When it
is set, **BDN has already divided: the reported Mean and Allocated are
per-operation. Do not divide by the loop count again.** Earlier revisions of
this table carried `"166.8 ns / 1024 ops ≈ 0.16 ns/op"`-style tails that did
exactly that; they were arithmetically wrong (0.16 ns is well under one cycle)
and have been removed without re-running, since the BDN Means they annotated
were already the correct per-call numbers. For
`PipelineBarrier.SetWaitEventPair_SingleImage` one operation is one Set+Wait
**pair**, i.e. two recorded commands.

## Caveats

- **Variance**: timings on a desktop host vary 5-15% run-to-run. Treat
  changes < 20% as noise; investigate larger swings.
- **Driver dependency**: the FrameRing / `BufferBenchmarks` / CommandRecorder /
  PipelineBarrier / PushDescriptors / StagingUploader / SyncPool benchmarks
  fail at `[GlobalSetup]` on a host without a Vulkan ICD. That is the
  expected behavior — there is no soft skip in the benchmark project (BDN
  reports the failure and moves on).
- **Host reads are a memory-type property, not a wrapper property (#157)**:
  the old `Buffer.Map_AsSpan` row's 166.8 ns was a **host read** from the
  mapped allocation, not the cost of `AsSpan<T>`. The method
  (`src/Ahjo.Vulkan/Resources/Buffer.cs`) is a null check, a span construction
  over the cached `pMappedData`, and a cast that is a length division; its
  loop body did `span[0] = i; sum += span[0];`, and the read-back was the
  whole figure. The row is **renamed, not deleted** — it is now
  `Buffer.AsSpan_WriteThenRead_SeqWriteAlloc` and its number did not change
  (166.8 → 173.4 ns, same body, same allocation). `AsSpan_ViewOnly` at 1.54 ns
  is **a new measurement of a different thing, not a speed-up**: no wrapper
  code changed in #157, only comments.
  - **Why.** `HostAccessSequentialWrite` "[d]eclares that mapped memory will
    only be written sequentially […] never read or accessed randomly, so a
    memory type can be selected that is uncached and write-combined"
    (`native/vma/include/vk_mem_alloc.h:652-658`, which also warns about
    "implicit reads introduced by doing e.g. `pMappedData[i] += x`"). That is
    implemented, not just documented: VMA's type selection sets
    `HOST_CACHED` as **not preferred** for this flag
    (`vk_mem_alloc.h:4085-4090`), and the scoring loop then picks the
    candidate with the fewest not-preferred bits.
  - **Captured evidence** — `[GlobalSetup]` prints what VMA actually selected,
    and on the capture host the two allocations landed on *different* memory
    types, exactly as that code predicts:

    ```
    [BufferBenchmarks] seq-write alloc: memoryType=2 flags=HostVisible, HostCoherent heap=1 heapSizeMiB=32336
    [BufferBenchmarks] host-random alloc: memoryType=3 flags=HostVisible, HostCoherent, HostCached heap=1 heapSizeMiB=32336
    ```

    `HostCached` is **absent** from the sequential-write type and **present**
    on the random-access one. The 32 GiB heap identifies both as system
    memory, not a small-BAR device mapping — so the slow read is an uncached
    host access, not a PCIe round trip.
  - **It is the read, not the store.** A one-off probe writing index 0 (the
    same address the `WriteThenRead` rows touch, so identical addressing)
    through the same non-inlined helper measured **1.688 ns/op** on the
    sequential-write allocation. Stores into write-combined memory are cheap;
    adding the read-back of that same element takes the op to 173.4 ns. The
    probe was not kept as a permanent row.
  - **Portability.** The flag makes `HOST_CACHED` unlikely, not impossible —
    `notPreferred` is a penalty, and VMA notes platforms with no
    `HOST_CACHED` type at all (`vk_mem_alloc.h:4068-4069`). On an
    integrated/UMA host, or under MoltenVK, both `WriteThenRead` rows may
    collapse onto each other. That is not a regression; run the class and read
    the two `[BufferBenchmarks]` lines to see what your own host chose.
- **Inline-array threshold**: `CommandRecorder.PipelineBarrier` uses a
  method-local `stackalloc` for both the single-barrier and 64-barrier
  paths. There is no fast/slow split today; `LargeBatch_8x8x1` is therefore
  a regression canary against a future `ArrayPool` rental, not a comparison
  between two distinct allocation regimes.
- **Device-loss fast path (#120)**: `Fence.Wait`/`Fence.IsSignaled`/
  `TimelineSemaphore.WaitFor` carry a `Device.IsLost` volatile-read branch
  in front of the host syscall, and the sync structs carry a managed
  `Device` owner reference. Both sit under the existing `SyncPool`
  (`Sync_HostOps_RoundTrip`) and `FrameRing` canaries; the allocation
  column was re-verified at `-` across all four `SyncPool` benchmarks after
  the change (Linux container, Mesa lavapipe — Mean values from that host
  are not comparable to this baseline and were not recorded here).
- **Per-device dispatch table (#121)**: `CommandRecorder` now dispatches
  every `vkCmd*` (plus `vkBeginCommandBuffer` / `vkEndCommandBuffer` /
  `vkQueueSubmit2`) through `delegate* unmanaged` pointers resolved once per
  device via `vkGetDeviceProcAddr`, instead of the static `[DllImport]`s that
  route each call through the loader's dispatch trampoline. The pointers live
  in `DeviceFunctionTable` (a value field on `Device`) and are read by
  `ref readonly` at the call site, so no managed allocation is introduced —
  the `CommandRecorder.RenderingPass100Cmds` /
  `CommandBufferPool.Frame_Begin_100Cmds_End_Reset` /
  `PipelineBarrier.*` / `PushDescriptors.*` allocation columns stay `-`. The
  expected win is in the **Mean** column on Windows + a real ICD (the loader
  trampoline is thickest there); refresh those rows from a Windows capture
  before and after to confirm the change earns its keep on real drivers.
- **Split barriers share the barrier marshalling (#155)**: `PipelineBarrier`,
  `SetEvent` and `WaitEvent` now route through one private
  `CommandRecorder.RecordDependency` implementation, which is what keeps a
  Set/Wait pair's two `VkDependencyInfo`s byte-identical from equal inputs
  (`VUID-vkCmdWaitEvents2-pEvents-10788`) as a structural property rather
  than a review obligation. The four `PipelineBarrier.*` rows were recaptured
  after that extraction. **Neither barrier row regressed** — measured against
  unmodified `main` (a799194) on this host in the same session, not assumed:
  - `SingleImageTransition` 131.6 ns → 178.8 ns is **host drift**: `main`
    measured 180.2 / 184.0 ns today, i.e. at or above the post-change
    180.3 / 178.8 ns.
  - `LargeBatch_8x8x1` 3.08 µs → 2.80 µs is **a change of statistic, not of
    speed**: the old row was a single sample, the new one is the minimum of 5.
    `main` today gives a 2.769 µs minimum against the branch's 2.795 µs
    (+0.9%). Single samples on this benchmark span 2.77–4.61 µs on both
    trees, so one reading can look like a ±25% swing in either direction.

  These four rows were captured on .NET 10.0.8 (SDK 10.0.204) while the rest
  of the table predates that on .NET 10.0.7 (SDK 10.0.203) — compare within a
  capture, never across one.
- **Not run in CI**: benchmark numbers are too noisy on hosted runners.
  This file is a manual capture; refresh it when a hot path changes.
