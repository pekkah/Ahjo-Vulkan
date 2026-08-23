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
dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter "*FrameRing*|*PushDescriptors*|*PipelineBarrier*|*CommandRecorder*|*BufferBenchmarks*|*MeshShader*|*AccelerationStructure*"
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
first recaptured for #155 on .NET 10.0.8 (SDK 10.0.204) / Windows 11
10.0.26200.8894 with an NVIDIA RTX 4070 Ti, and **recaptured again for #201**
on Windows 11 10.0.26200.9168 with the same RTX 4070 Ti after the
recorder-disposal fix described in their row notes (minimum of 5 post-fix runs,
against 3 pre-fix control runs in the same session); the five `MeshShader.*`
rows were captured for #201 on .NET 10.0.8 (SDK 10.0.204) / Windows 11
10.0.26200.9168 with the same RTX 4070 Ti
(`Build_MeshPipeline_WithSpecialization` last, in the review-fix pass, on the
same host and toolchain — the other four re-read 16.15 / 21.88 / 24.23 ns and
36.13 µs on that pass's single confirming run, all within their recorded
spreads, so they were left at their minimum-of-N figures); the `HandleOwnership.*` rows came
from a Linux container. Rows are comparable to their own successors, not to each other —
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
| `CommandBufferPool.Frame_Begin_100Cmds_End_Reset` | 6.44 µs |        -  | Begin → **300** commands → End → ResetForFrame. The name counts loop iterations, not commands: the body is 100 × (`SetViewport` + `SetScissor` + `FillBuffer`) (`CommandBufferPoolBenchmarks.cs:77-82`). No `OperationsPerInvoke`, so the Mean is the whole cycle. |
| `CommandRecorder.RenderingPass100Cmds`          |   3.23 µs  |        -  | BeginRendering → 100 SetViewport → EndRendering, dynamic rendering path.  |
| `CommandRecorder.CopyBuffer_8Regions`           | 810.0 ns   |        -  | #141 canary: multi-region CopyBuffer; stackalloc ≤16 path stays 0 B/op.   |
| `CommandRecorder.CopyBuffer_24Regions`          |   1.57 µs  |        -  | #141 canary: multi-region CopyBuffer; ArrayPool >16 path stays 0 B/op.    |
| `MeshShader.DrawMeshTasks_1024`                 |  15.49 ns  |        -  | #201 canary: `vkCmdDrawMeshTasksEXT` × 1024 inside one BeginRendering scope with a mesh pipeline bound. Pointer load + unconditional null test + native call; the null test is not behind `AhjoValidation`, so it is measured here rather than compiled away. Recording only, never submitted. **Minimum of 5 runs** (15.49 / 15.52 / 15.53 ns on the three quiet runs; two noisy runs read 23.2 and 24.9 ns with every row in the same run elevated). Needs `VK_EXT_mesh_shader` — see the driver-dependency caveat. Includes the per-invoke bracket; see the note below the table. |
| `MeshShader.DrawMeshTasksIndirect_1024`         |  21.43 ns  |        -  | #201 canary: `vkCmdDrawMeshTasksIndirectEXT` × 1024 with `drawCount: 1`, `stride: 12`. `drawCount` is 1 on purpose — above 1 needs the `multiDrawIndirect` feature (VUID-vkCmdDrawMeshTasksIndirectEXT-drawCount-02718), which would narrow the host requirement for no wrapper-side difference. **Minimum of 5 runs** (range 21.43–24.43 ns). Includes the per-invoke bracket; see the note below the table. |
| `MeshShader.DrawMeshTasksIndirectCount_1024`    |  23.20 ns  |        -  | #201 canary: `vkCmdDrawMeshTasksIndirectCountEXT` × 1024 with `maxDrawCount: 1`, `stride: 12` — Ahjo's actual shape (compute writes the count, raster reads it). **Minimum of 5 runs** (range 23.20–24.05 ns, StdDev 0.04–0.90 ns). Was 34.52 ns and strongly bimodal (BDN mValue 3.33) when first captured, before the recorder-disposal fix — the recorder was disposed *after* `ResetForFrame` rather than before, so the command buffer never made it back to `_idle` and the pool settled into alternating two buffers instead of re-recording one. **What the fix is credited with is the bimodality, not the mean.** The bimodality claim is well-evidenced: mValue 3.33 pre-fix, not flagged in any of the 5 post-fix runs, with the StdDev collapsing to 0.04–0.90 ns. The **mean** shift is not: 34.52 ns came from a different session on Windows 11 10.0.26200.8894, and the same host drift moved `PipelineBarrier.SingleImageTransition` from 178.8 to 143.9 ns — a 20% drop with **no code change at all** (see that row). An unknown share of 34.52 → 23.20 is therefore drift, and a clean pre-fix control is no longer recoverable (this file's benchmark body also gained `SetViewport`/`SetScissor` since, which changes what is measured) — so do not try to reconstruct one. The minimum-of-5 discipline is retained rather than re-argued. Needs `VK_EXT_mesh_shader` **and** the `drawIndirectCount` feature (VUID-vkCmdDrawMeshTasksIndirectCountEXT-None-04445) — the wrapper does not enable the latter by default. |
| `MeshShader.Build_MeshPipeline`                 |  35.44 µs  |        -  | #201: `vkCreateGraphicsPipelines` for the **widest** mesh shape — task + mesh + fragment — so the builder's `VK_SHADER_STAGE_TASK_BIT_EXT` emission and the mesh path's extra four `fixed` statements are measured, not just compiled. Setup-time, not per-frame; the row exists for the allocation column. **Minimum of 9 runs** (range 35.44–37.18 µs). `-` on 8 of those 9; one run reported `1 B`, **not attributable to the measured body**, which is allocation-free both by inspection (stage array, blend and dynamic-state spans are all `stackalloc`, every array goes through `fixed`, and `GraphicsPipeline` is a `readonly unsafe struct` so `using var` does not box) and by assertion — `MeshShaderTests.MeshPipeline_Build_IsZeroAllocation` measures `GC.GetAllocatedBytesForCurrentThread()` across 128 of exactly this chain and asserts a zero delta. (`1 B/op` at `OperationsPerInvoke = 1` and ~35 µs/op would mean ~10–20 KB inside one iteration, i.e. hundreds of objects, not one stray allocation; whatever it was, it was not this code.) Treat a *reproducible* non-`-` here as a real regression. Deliberately not on `GraphicsPipelineBuilderBenchmarks`: that class is the #44 canary and must keep running on hosts with no mesh support. Additionally needs the `taskShader` feature, which is advertised independently of `meshShader`. |
| `MeshShader.Build_MeshPipeline_WithSpecialization` |  35.55 µs  |        -  | #201: `Build_MeshPipeline` plus **mesh and task** specialization, so the mesh path's `_meshSpecEntries` / `_taskSpecEntries` `fixed` statements are measured in their non-empty form — without this row two of the four extra `fixed` statements only ever run over a null array. The `SpecializationInfo<T>` values are stack locals (the wrapper stores a raw pointer to the caller's storage, so a field on the benchmark instance would be unpinned); the per-`T` map-entry array is cached statically and warmed in `[GlobalSetup]`, the `SpecializationInfo.Build_WithSpecialization` shape. `mesh_tri.task` declares no spec constants — an unused `constantID` is a spec-defined no-op, which is what lets one fixture drive both stages. **Minimum of 5 runs** (35.55 / 36.31 / 36.63 / 37.37 µs on the four quiet runs; one noisy run read 48.14 µs with StdDev 8.28 µs), `-` on all five. Sits within `Build_MeshPipeline`'s own spread, which is the expected result: two extra non-null `fixed` statements and two 4-byte `pData` blocks are not measurable against a 35 µs `vkCreateGraphicsPipelines`. The row exists for the allocation column, not the Mean. Same `taskShader` requirement as the row above. |
| `AccelerationStructure.BuildTlas_1024`          |   2.84 µs  |        -  | #202 canary: `vkCmdBuildAccelerationStructuresKHR` × 1024 recording the **per-frame TLAS-rebuild shape** — one build, one `Instances` geometry — into one command buffer, never submitted. This is the only new hot-path method in #202, and it is not a thin forward: it validates, carves three native scratch spans and runs `AccelerationStructureBuildTranslator` over them. One build / one geometry is inside both stack thresholds (`BuildStackThreshold = 8`, `GeometryStackThreshold = 16`), so this row measures the `stackalloc` path; `BuildBlasBatch_16x1_1024` below measures the `ArrayPool` path. Note the threshold test is `&&`, so exceeding **either** threshold sends all three native scratch buffers to the pool. **The Mean is dominated by the driver, not the wrapper** — ~2.8 µs per recorded build against 15 ns for `MeshShader.DrawMeshTasks_1024` is real BVH setup work the ICD does at record time, so treat this row's `Allocated` column as the signal and its Mean as host-specific. Setup builds a real BLAS so the instance entry carries a live device address — a build against a garbage `accelerationStructureReference` is a VU violation even when the command buffer is never submitted. Needs `VK_KHR_acceleration_structure` + `VK_KHR_ray_query` + `VK_KHR_deferred_host_operations` and the `accelerationStructure` / `rayQuery` / `bufferDeviceAddress` features — see the driver-dependency caveat. |
| `AccelerationStructure.BuildBlasBatch_16x1_1024` |  44.30 µs  |        -  | #202 canary: sixteen BLAS builds, one triangle geometry each, × 1024 into one command buffer, never submitted. Two gaps in one row. **(a) The rental leg** — 16 builds is above `BuildStackThreshold = 8`, so all three native scratch buffers come from `ArrayPool` in nested `try/finally` instead of `stackalloc`; this is the only benchmark that reaches that path, and three rent/return pairs per call must still amortize to `-`. **(b) The `Triangles` union arm** — `BuildTlas_1024` only ever drives `AccelerationStructureGeometry.WriteNative`'s `Instances` arm (three field writes); `Triangles` is the widest at eight and before this row ran only in `[GlobalSetup]`, where BDN measures nothing. (The `Aabbs` arm remains unmeasured — narrowest of the three, no per-frame consumer; its correctness is covered instead by the Tier-3 `AccelerationStructureTests.Blas_OverAabbs_*` builds, issue 206.) **This is a per-frame shape, not load-time**: BLAS refits for skinned and deformable geometry are rebuilt every frame, and nine animated meshes already cross the threshold. The fixture suballocates sixteen distinct structures into one backing buffer at 256-byte-aligned offsets with sixteen non-overlapping scratch ranges — reusing one destination or one scratch address across a batch violates `VUID-vkCmdBuildAccelerationStructuresKHR-scratchData-03704`, so the shortcut would measure a shape no correct caller can record. Same host requirement as the row above. **ShortRun capture** (`--job short`); the Mean is per *recording call*, and each call records 16 builds, so ~2.8 µs per build — consistent with `BuildTlas_1024`, and again driver-dominated. |
| `PipelineBarrier.SingleImageTransition`         | 143.9 ns   |        -  | One `vkCmdPipelineBarrier2` with a single image barrier. Recaptured for #201 — **minimum of 5 runs** (143.9 / 144.6 / 144.7 / 147.1 / 165.7 ns). Was 178.8 ns at #155; see the split-barrier caveat and the recorder-disposal note on the row below. |
| `PipelineBarrier.LargeBatch_8x8x1`              |   2.72 µs  |        -  | One `vkCmdPipelineBarrier2` with 64 image barriers. Recaptured for #201 — **minimum of 5 runs** (2.718 / 2.728 / 2.743 / 2.748 / 2.768 µs, every run BDN-unimodal at MValue 2). **The bimodality this row used to document is gone, but not because of the fix in the same commit.** The class had the `ResetForFrame()`-before-recorder-`Dispose()` ordering bug (#188/#199 shape) in all four of its methods and it was fixed here — but three pre-fix control runs in the same session read 2.716 / 2.755 / 2.780 µs, i.e. already tight and already below the old 2.80 µs figure. Fix and control are within each other's noise, so the fix cost nothing and bought nothing measurable at this scale. **What the bad ordering costs is pool state, not per-invoke work**: it grows **one** extra `VkCommandBuffer`, not one per invoke — the pool settles into ping-ponging two buffers after the second invoke (`Pools/CommandBufferPool.cs:98-118`, `:155-159`, `:168-178`). That one-off allocation is amortized across millions of ops and is therefore invisible at *every* scale; it is not why the fix showed up on one row and not another. The part that is per-invoke and steady-state is the **ping-pong itself**: post-fix one buffer is re-recorded every invoke, pre-fix two alternate, so each invoke records into driver-side memory last touched two invokes ago. That is a recurring locality cost, and it scales with how much of the measurement is recording. Here it is invisible because 2.7 µs of driver per-barrier work for 64 barriers dominates any locality delta by two orders of magnitude, and because this method records 256 commands per invoke against `MeshShader.DrawMeshTasksIndirectCount_1024`'s 1024 — that row, where recording is nearly the whole measurement, is where two-buffers-in-rotation can plausibly show as the two-mode signature it lost. The old "strongly bimodal, median 3.61 µs, range 2.80–4.61 µs" reading is therefore **host/driver drift since #155, unexplained**, not something this branch repaired — keep comparing minima across ≥3 runs until a run reproduces the wide spread and identifies it. |
| `PipelineBarrier.SetWaitEventPair_SingleImage`  | 247.1 ns   |        -  | #155 canary: one `vkCmdSetEvent2` + `vkCmdWaitEvents2` pair, one image barrier each. Recording only, never submitted. Recaptured for #201 — **minimum of 5 runs** (247.1–257.2 ns); was 260.7 ns. |
| `PipelineBarrier.ResetEvent_Single`             |  33.3 ns   |        -  | #155 canary: one `vkCmdResetEvent2` — bare stage-mask pass-through, no dependency marshalling. Recaptured for #201 — **minimum of 5 runs**; unchanged from the #155 figure (33.4 → 33.3 ns), and unchanged by the recorder-disposal fix (pre-fix control minimum 33.25 ns). One of the five runs read 46.7 ns with MValue 3.76 — a noisy run, not a mode. |
| `FrameRing.Frame_Begin_Submit_Wait`             |  56.20 µs  |        -  | Full headless frame: BeginFrame → submit no-op cmd → wait fence.          |
| `PushDescriptors.PushDescriptors_StorageBuffer` |  69.34 ns  |        -  | `vkCmdPushDescriptorSetWithTemplate` × 1024 in one Begin/End scope; bimodal under driver overhead. |
| `PushDescriptors.PushDescriptorSet_SpanWrites`  |  26.61 ns  |        -  | The non-templated `vkCmdPushDescriptorSet` span overload (#121). **Baseline was missing until #202** — this is the benchmark guarding `DescriptorWriteBuilder.BuildWrites`, which #202 widened with a `chains` span, so it needed a number to regress against. One write stays on the `<= 8` stackalloc leg. **ShortRun capture** (`--job short`) — treat the Mean as indicative, not a full-precision baseline; the `-` is the load-bearing part. |
| `PushDescriptors.PushDescriptorSet_SpanWrites_16` | 136.65 ns |       -  | #202 canary: the same call with **sixteen** writes, above the recorder's `StackThreshold` of 8 — the only benchmark that reaches `PushDescriptorSet`'s `ArrayPool` leg. #202 added a second nested rental there (the `VkWriteDescriptorSetAccelerationStructureKHR` chains buffer); a non-`-` reading means a rental is escaping or an array is not being returned. **ShortRun capture** (`--job short`). Adding this row immediately paid for itself: the first version pushed 16 writes at a layout declaring only binding 0, and the driver answered a VU violation with an access violation (0xC0000005) rather than an error code — proof the row reaches real driver work, and a reminder that `vkCmdPushDescriptorSet` against an undeclared binding is unrecoverable, not diagnosable. |
| `PushDescriptors.Update_StorageBuffer`          |  17.52 ns  |        -  | #202 canary: `DescriptorSetExtensions.Update` — the **other** caller of `DescriptorWriteBuilder.BuildWrites`, and until #202 the one with no benchmark anywhere in the repo. It is a genuine per-frame path for engines that rebuild descriptor sets rather than push, and it grew the same `chains` span every push-descriptor call did. Needs a non-push set layout plus a `DescriptorSetPool`, since a push-descriptor layout cannot be used with `vkAllocateDescriptorSets`. One write, so the `<= 8` stackalloc leg. **ShortRun capture** (`--job short`). |
| `BindDescriptorSets.Bind_1Set`                  | n/m        |        -  | #188 canary: `vkCmdBindDescriptorSets` with one set × 1024 in one Begin/End scope — the common per-draw bind. The single handle is copied into a `stackalloc nint[1]` (the recorder's `<= 32` branch), so no managed allocation. **Mean not yet captured** — the authoring host had no Vulkan ICD; the `-` is from static analysis of the stackalloc branch and must be confirmed on the first measured run. |
| `BindDescriptorSets.Bind_4Sets`                 | n/m        |        -  | #188 canary: same call with 4 sets — still on the `<= 32` stackalloc branch, so the per-element copy grows but stays allocation-free. 4 is the guaranteed `maxBoundDescriptorSets` floor, so the bind is portable on every conformant device. The gap to `Bind_1Set` is the copy cost, proportional to `sizeof(DescriptorSet)` (24 B since #182); an unrelated change to that struct moves this number, which is why the row exists. **Mean not yet captured** — same as the row above. |
| `TimestampQuery.ResetAndWritePair`              |  55.49 ns  |        -  | #198 canary: one `vkCmdResetQueryPool` + two `vkCmdWriteTimestamp2` per op — the per-pass bracket a render-graph recorder emits. Recording only, never submitted. Driver-bound. |
| `TimestampQuery.TryGetResults_NotReady`         |  26.64 ns  |        -  | #198 canary: `vkGetQueryPoolResults` against initialized-but-unavailable queries — the steady-state per-frame readback, returning `false` with no allocation. Driver-bound. |
| `TimestampQuery.TryGetResults_WithAvailability_NotReady` | 26.71 ns |  -  | #198 canary: the availability-reporting overload over the same unavailable queries — a distinct marshaling shape (16-byte `QueryResult` stride, `WITH_AVAILABILITY` bit) that is equally per-frame-callable. Driver-bound. |
| `DescriptorSetPool.AcquireReleaseReset_Cycle`   |  39.62 ns  |        -  | #114 canary: per-frame Acquire → Release → Reset; Reset retains the per-bucket `(layout, count)` idle `Stack`s instead of discarding them. Re-measured for #182 at 38.67 ns after the composite key landed — within noise, so the number is left as captured. Re-measured for #191 at 38.01 ns after the empty-`poolSizes` relaxation — also within noise, number again left as captured. The pre-flight guard's message was extracted into a `NoInlining` throw helper because the inline ternary form measured 43.82 ns in that same session; `DescriptorSetPool.cs` carries a standing "do not fold it back inline" for that reason. |
| `DescriptorSetPoolVariableCount.AcquireReleaseReset_VariableCount_Cycle` |  88.23 ns  |        -  | #182 canary: `Acquire(layout, count)` chains `VkDescriptorSetVariableDescriptorCountAllocateInfo` from the stack; the `(layout, count)` free-list key must not box. Mean not comparable to the row above — different layout, pool template and per-set descriptor count. |
| `DescriptorSetPoolVariableCount.AcquireReleaseReset_TwoCounts_Cycle` | 221.69 ns  |        -  | #182 canary: two distinct counts per cycle — the bounded-count case must reuse both retained `Stack`s, not rebuild them (the #114 shape, one key deeper). Mean not comparable to `DescriptorSetPool.AcquireReleaseReset_Cycle` — different layout, pool template and per-set descriptor count. |
| `HandleOwnership.PassAndReturn_ByValue`         |   3.69 ns  |        -  | #118 canary: `PipelineLayout` (one managed metadata ref) copied through a non-inlined call — stays stack-only, no write barrier, no box. Captured on a Linux container host (driver-free benchmark). |
| `HandleOwnership.MetadataRead_OwningAndBorrowed` |  0.92 ns  |        -  | Field read replacing the old side-table dictionary lookup + lock.       |
| `HandleOwnership.OwnershipPredicate`            |   0.47 ns  |        -  | `OwnsHandle` — the Dispose guard / borrow check.                        |
| `HandleOwnership.ConstrainedGenericDispatch`    |   3.69 ns  |        -  | `ObjectName.Set`-shaped `struct, IVulkanHandle<T>` dispatch — devirtualized, box-free under the relaxed constraint. |

**Reading the Mean column.** Many benchmarks here unroll an inner loop and
declare `OperationsPerInvoke` (e.g. `Buffer.AsSpan_*`, `PipelineBarrier.*`,
`PushDescriptors.*`, `PushConstants.*`, `SyncPool.*`, `ResultPolicy.*`,
`HandleOwnership.*`, `DescriptorSetPool.*`, `MeshShader.DrawMeshTasks*` — not an
exhaustive list; the two `MeshShader.Build_MeshPipeline*` rows are one build
per op and set none). When it
is set, **BDN has already divided: the reported Mean and Allocated are
per-operation. Do not divide by the loop count again.** Earlier revisions of
this table carried `"166.8 ns / 1024 ops ≈ 0.16 ns/op"`-style tails that did
exactly that; they were arithmetically wrong (0.16 ns is well under one cycle)
and have been removed without re-running, since the BDN Means they annotated
were already the correct per-call numbers. For
`PipelineBarrier.SetWaitEventPair_SingleImage` one operation is one Set+Wait
**pair**, i.e. two recorded commands.

**The per-invoke bracket is inside the mean.** The three
`MeshShader.DrawMeshTasks*` rows each record `Begin` → `BeginRendering` →
`BindPipeline` → `SetViewport` → `SetScissor` → 1024 draws → `EndRendering` →
`End` → `ResetForFrame`, and `OperationsPerInvoke = 1024` divides *all* of that
by 1024 — so the bracket is a real component of a ~15–24 ns figure, not an
error bar. A bound on its size, from
`CommandRecorder.RenderingPass100Cmds` (3.23 µs for
Begin → BeginRendering → 100 × SetViewport → EndRendering → End → ResetForFrame,
`CommandRecorderBenchmarks.cs:9-10`): the mesh bracket nests inside that shape
except for `BindPipeline` — same `Begin`/`End`/`ResetForFrame`, same
`BeginRendering`/`EndRendering` pair, and 100 `SetViewport`s more than cover the
bracket's one `SetViewport` + one `SetScissor`. So the bracket is
**under ~3.3 ns/op here, plus one `BindPipeline`**. The row this note used to
cite — `CommandBufferPool.Frame_Begin_100Cmds_End_Reset`, 6.44 µs — is not a
bound: it records 300 commands, not 100, and contains neither a
`BeginRendering`/`EndRendering` pair nor a `BindPipeline`, so its shape is not a
superset and "strictly less" was unearned. If you want a bound that covers
`BindPipeline` too without measuring it, the union of the two rows
(6.44 + 3.23 µs over 1024 ops) puts the bracket under 9.5 ns/op. **The benchmarks are deliberately not
changed to exclude it** — every unrolled row in this table carries its own
bracket the same way, and the rows are regression canaries compared against
themselves, so subtracting it would break comparability with every prior
capture for no gain. `SetViewport`/`SetScissor` are part of that bracket
because the pipeline takes the builder's default dynamic state and CoreChecks
validates dynamic state at **record** time — recording the draws without them
is `VUID-vkCmdDrawMeshTasksEXT-None-07831`/`-07832` whether or not the buffer
is ever submitted.

## Caveats

- **Variance**: timings on a desktop host vary 5-15% run-to-run. Treat
  changes < 20% as noise; investigate larger swings.
- **Driver dependency**: the FrameRing / `BufferBenchmarks` / CommandRecorder /
  PipelineBarrier / PushDescriptors / BindDescriptorSets / StagingUploader /
  SyncPool / TimestampQuery benchmarks
  fail at `[GlobalSetup]` on a host without a Vulkan ICD. That is the
  expected behavior — there is no soft skip in the benchmark project (BDN
  reports the failure and moves on).
  `DescriptorSetPoolVariableCountBenchmarks` additionally needs a device that
  advertises `descriptorBindingVariableDescriptorCount` and fails at
  `[GlobalSetup]` without it; that is why it is a class of its own rather than
  two more methods on `DescriptorSetPoolBenchmarks`, whose
  `AcquireReleaseReset_Cycle` (the #114 canary) must keep running on any host
  with an ICD.
  `MeshShaderBenchmarks` is the same shape one step further: its
  `[GlobalSetup]` needs a device that exposes `VK_EXT_mesh_shader` plus the
  `meshShader` feature, `drawIndirectCount` for the indirect-count row, and
  `taskShader` for the two `Build_MeshPipeline*` rows — `taskShader` is advertised
  independently of `meshShader`, so a mesh-only device fails
  `vkCreateDevice` with `VK_ERROR_FEATURE_NOT_PRESENT` here. That failure is
  loud and intended; contrast `MeshShaderTests`, where the same request would
  turn a partial-capability host into a silent skip of the whole mesh tier and
  so is made per-test instead. The picker will find no physical device at all
  without the extension. That is exactly why it is
  its own class and not two more methods on `CommandRecorderBenchmarks`, whose
  `RenderingPass100Cmds` is the #29 canary and must keep running on any host
  with an ICD — mesh-capable or not.
  `AccelerationStructureBenchmarks` is the same shape again, with the
  narrowest host requirement in the project: its `[GlobalSetup]` picks a
  physical device advertising **all three** of
  `VK_KHR_acceleration_structure`, `VK_KHR_ray_query` and
  `VK_KHR_deferred_host_operations`, then creates a device with the
  `accelerationStructure`, `rayQuery` and `bufferDeviceAddress` features. A
  host with no ray-tracing-capable ICD finds no physical device at all and
  fails loudly at setup. That is again exactly why it is its own class rather
  than one more method on `CommandRecorderBenchmarks` — the #29 canary must
  not be taken down by a missing optional extension. It also does real GPU
  work in setup (it builds a BLAS and waits on a fence) so that the TLAS
  instance entry the measured body rebuilds against holds a live device
  address.
- **No row for the compaction commands, deliberately.**
  `CommandRecorder.WriteAccelerationStructuresProperties` and
  `CommandRecorder.CopyAccelerationStructure` are **asset-load-time**, not
  per-frame: an acceleration structure is compacted once, after it is first
  built, and never again — compaction is what you do to a *finished* BLAS
  before it enters the scene. Both are nevertheless written to the same
  `stackalloc`-then-`ArrayPool` rule as the build path, so neither allocates;
  they simply have no per-frame caller to regress. `BuildAccelerationStructures`
  is the only new hot-path method in #202, which is why it is the only one with
  rows. (Stated explicitly so the next reviewer does not have to re-derive it
  from the call graph.)
- **No row for the physical-device property queries, deliberately.**
  `PhysicalDevice.SupportsExtension`, `PhysicalDevice.TryGetProperties<T>`,
  `PhysicalDevice.TryGetMeshShaderLimits` and
  `PhysicalDevice.TryGetAccelerationStructureLimits` (plus the
  `MeshShaderLimits` / `AccelerationStructureLimits` projections) are
  **setup-time**, not per-frame, and cache nothing. The
  version-gated `TryGetProperties` issues two native queries when the gate
  passes (`vkGetPhysicalDeviceProperties`, then
  `vkGetPhysicalDeviceProperties2`); the name-gated overloads and
  `TryGetMeshShaderLimits` issue three
  (`vkEnumerateDeviceExtensionProperties` twice — count, then fill — then
  `vkGetPhysicalDeviceProperties2`); `TryGetAccelerationStructureLimits` is
  the same name-gated three. `Device.GetAccelerationStructureBuildSizes` is
  setup-time for the same reason and has no row either: it is a sizing query a
  caller runs before it can allocate anything, and it is stack-only at 16
  geometries or fewer. `Lifecycle/` is not on the
  zero-per-frame-allocation list in `src/Ahjo.Vulkan/CLAUDE.md`, and the two
  closest existing accessors — `PhysicalDevice.GetMemoryLimits` and
  `Device.TimestampPeriod` — have no rows here either. Accounting anyway, for
  the record: the `VulkanVersion`-gated `TryGetProperties` overload allocates
  nothing (one `stackalloc` sized from two compile-time struct sizes), and the
  extension-gated overloads rent and return a pooled
  `VkExtensionProperties[]` exactly as `Instance.IsExtensionSupported` does.
  A native driver query is the wrong thing to have on a per-frame path
  whatever its allocation profile, so the answer is "don't call it per frame",
  not "benchmark it". Stated here and in
  `.claude/agents/bench-coverage-checker.md` so it does not get re-litigated
  on a later diff.
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
