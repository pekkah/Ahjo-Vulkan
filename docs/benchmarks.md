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
dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter "*FrameRing*|*PushDescriptors*|*PipelineBarrier*|*CommandRecorder*"
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

## Baseline

Each row maps to one `[Benchmark]` method. **Allocated** is the per-op
managed-byte count BDN's `MemoryDiagnoser` reports; `-` is zero.

| Benchmark                                       | Mean       | Allocated | Notes                                                                     |
|-------------------------------------------------|-----------:|----------:|---------------------------------------------------------------------------|
| `ChainBuilder.BuildThreeNodeChain`              |   3.66 ns  |        -  | Pure host: features2 + vk13 + vk12 over a stack-only `ChainBuilder`.      |
| `Buffer.Map_AsSpan`                             | 166.8 ns / 1024 ops ≈ **0.16 ns/op** | - | Persistent-mapped buffer, alloc-free `AsSpan<T>` round-trip. |
| `CommandBufferPool.Frame_Begin_100Cmds_End_Reset` | 6.44 µs |        -  | Begin → 100 dynamic-state + fill commands → End → ResetForFrame.          |
| `CommandRecorder.RenderingPass100Cmds`          |   3.23 µs  |        -  | BeginRendering → 100 SetViewport → EndRendering, dynamic rendering path.  |
| `PipelineBarrier.SingleImageTransition`         | 131.6 ns / 256 ops ≈ **0.51 ns/op** | - | One `vkCmdPipelineBarrier2` with a single image barrier.       |
| `PipelineBarrier.LargeBatch_8x8x1`              | 3.08 µs / 256 ops ≈ **12.0 ns/op** | - | One `vkCmdPipelineBarrier2` with 64 image barriers.              |
| `FrameRing.Frame_Begin_Submit_Wait`             |  56.20 µs  |        -  | Full headless frame: BeginFrame → submit no-op cmd → wait fence.          |
| `PushDescriptors.PushDescriptors_StorageBuffer` |  69.34 ns  |        -  | `vkCmdPushDescriptorSetWithTemplate` × 1024 in one Begin/End scope; bimodal under driver overhead. |
| `HandleOwnership.PassAndReturn_ByValue`         |   3.69 ns  |        -  | #118 canary: `PipelineLayout` (one managed metadata ref) copied through a non-inlined call — stays stack-only, no write barrier, no box. Captured on a Linux container host (driver-free benchmark). |
| `HandleOwnership.MetadataRead_OwningAndBorrowed` |  0.92 ns  |        -  | Field read replacing the old side-table dictionary lookup + lock.       |
| `HandleOwnership.OwnershipPredicate`            |   0.47 ns  |        -  | `OwnsHandle` — the Dispose guard / borrow check.                        |
| `HandleOwnership.ConstrainedGenericDispatch`    |   3.69 ns  |        -  | `ObjectName.Set`-shaped `struct, IVulkanHandle<T>` dispatch — devirtualized, box-free under the relaxed constraint. |

`OperationsPerInvoke` is set on the benchmarks that use it (`Buffer.Map_AsSpan`,
`PipelineBarrier.*`, `PushDescriptors.*`) so BDN's reported Mean / Allocated
columns are already per-op — the math above just translates the BDN summary
back into the per-call cost when the unrolled span is non-trivial.

## Caveats

- **Variance**: timings on a desktop host vary 5-15% run-to-run. Treat
  changes < 20% as noise; investigate larger swings.
- **Driver dependency**: the FrameRing / Buffer.Map / CommandRecorder /
  PipelineBarrier / PushDescriptors / StagingUploader / SyncPool benchmarks
  fail at `[GlobalSetup]` on a host without a Vulkan ICD. That is the
  expected behavior — there is no soft skip in the benchmark project (BDN
  reports the failure and moves on).
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
- **Not run in CI**: benchmark numbers are too noisy on hosted runners.
  This file is a manual capture; refresh it when a hot path changes.
