# `Buffer.Map_AsSpan` measures write-combined memory, not `AsSpan<T>`

**Issue:** [#157](https://github.com/pekkah/Ahjo-Vulkan/issues/157) — *Benchmarks: Buffer.Map_AsSpan measures a write-combined read-back, not AsSpan*
**Surfaced by:** [#155](https://github.com/pekkah/Ahjo-Vulkan/issues/155) (the `≈ ns/op` annotation correction that exposed the real per-call figure)
**Lands consistently with:** [#29](https://github.com/pekkah/Ahjo-Vulkan/issues/29) (the benchmark surface this row was created for), [#34](https://github.com/pekkah/Ahjo-Vulkan/issues/34) (`StagingUploader` — the wrapper's own sequential-write consumer)
**Test strategy constrained by:** [#32](https://github.com/pekkah/Ahjo-Vulkan/issues/32) (wrapper coverage is Windows-only; benchmarks are a manual capture and never run in CI)
**Date:** 2026-07-26

## Problem

`docs/benchmarks.md:75` records:

```
| `Buffer.Map_AsSpan` | 166.8 ns | - | Persistent-mapped buffer, alloc-free `AsSpan<T>` round-trip. |
```

`OperationsPerInvoke = 1024` is set (`BufferBenchmarks.cs:18, :64`) and BenchmarkDotNet divides the reported Mean by it, so 166.8 ns is a **per-call** figure. The row therefore claims one `Buffer.AsSpan<T>()` on an already-persistent-mapped buffer costs ~170 ns.

Two defects follow:

1. **The published number misrepresents the API.** A reader budgeting per-frame uniform updates against this table sees ~170 ns for a span view that costs a handful of nanoseconds.
2. **The row cannot detect a regression in the thing it names.** Whatever produces the 170 ns swamps `AsSpan<T>`'s own cost by two orders of magnitude, so a change that made `AsSpan` ten times slower would not move this row measurably.

The row is not a regression: it has read this way since it was first captured in `6121fff` (2026-05-10, issue #29). It went unquestioned because the original row carried the tail `"166.8 ns / 1024 ops ≈ **0.16 ns/op**"` — an arithmetic error that made the figure look like a sub-nanosecond field read. #155 removed the bogus division (`docs/benchmarks.md:96-101`), which is what exposed the real number.

## Evidence

### `AsSpan<T>` cannot cost 170 ns

`src/Ahjo.Vulkan/Resources/Buffer.cs:122-129` in full:

```csharp
public Span<T> AsSpan<T>() where T : unmanaged
{
    if (PersistentMapped == null)
        throw new InvalidOperationException(…);
    return MemoryMarshal.Cast<byte, T>(new Span<byte>(PersistentMapped, checked((int)Size)));
}
```

Three operations: a null compare against a field, a two-field span construction over a pointer the buffer already holds, and a `MemoryMarshal.Cast` that is a length division by a constant at runtime. `PersistentMapped` and `Size` are fields of a `readonly struct` read through `_buffer` — L1 hits. `HandleRegistry.TrackCreate` is in the constructor (`Buffer.cs:73`), not here; nothing in this method dispatches to VMA, the loader, or the driver.

`AsReadOnlySpan<T>()` (`Buffer.cs:132`) forwards to it verbatim, so it has identical cost and identical exposure.

### What the benchmark actually measures

`tests/Ahjo.Vulkan.Benchmarks/BufferBenchmarks.cs:64-75`:

```csharp
[Benchmark(OperationsPerInvoke = CallsPerInvoke)]
public int Map_AsSpan()
{
    int sum = 0;
    for (int i = 0; i < CallsPerInvoke; i++)
    {
        Span<int> span = _buffer.AsSpan<int>();
        span[0] = i;
        sum += span[0];      // read-back from device-visible memory
    }
    return sum;
}
```

The allocation under test (`BufferBenchmarks.cs:47-53`): `Size = 4096`, `Usage = BufferUsage.UniformBuffer`, `MemoryUsage.AutoPreferHost`, `Flags = HostAccessSequentialWrite | Mapped`.

So each measured op is: one `AsSpan` call, one 4-byte store to mapped device-visible memory, and one 4-byte load *of the address just stored to*.

### VMA's own selection code says that memory is uncached

The pinned VMA source is in-tree. `native/vma/include/vk_mem_alloc.h:652-658` documents the flag the benchmark passes:

> Declares that mapped memory will only be written sequentially, e.g. using `memcpy()` or a loop writing number-by-number, never read or accessed randomly, so a memory type can be selected that is **uncached and write-combined**.
>
> \warning Violating this declaration may work correctly, but will likely be very slow. Watch out for **implicit reads** introduced by doing e.g. `pMappedData[i] += x;`

This is not merely a hint in prose — it is implemented. `vk_mem_alloc.h:4085-4090`:

```c
// CPU sequential write - may be CPU or host-visible GPU memory, uncached and write-combined.
else if(hostAccessSequentialWrite)
{
    // Want uncached and write-combined.
    outNotPreferredFlags |= VK_MEMORY_PROPERTY_HOST_CACHED_BIT;
```

With this benchmark's exact inputs (`AUTO_PREFER_HOST`, `deviceAccess = true` because the usage is `UNIFORM_BUFFER`, no `ALLOW_TRANSFER_INSTEAD`), the else-branch at `:4096-4105` adds `outRequiredFlags |= HOST_VISIBLE` and `outNotPreferredFlags |= DEVICE_LOCAL`. The scoring loop (`:13930-13945`) then picks the candidate type with the fewest not-preferred bits set, i.e. VMA **actively avoids** a `HOST_CACHED` type for this allocation whenever a non-cached host-visible one exists.

Two consequences worth stating precisely:

- On a host that exposes an uncached `HOST_VISIBLE | HOST_COHERENT` system-memory type (typical desktop discrete-GPU driver on Windows), the benchmark's buffer lands in write-combined memory, where the `span[0] = i; sum += span[0];` sequence forces the pending write out of the write-combining buffer and then reads back through an uncached path — per iteration. Two orders of magnitude over a cached load is the expected shape.
- `notPreferred` is a *penalty*, not a requirement. VMA's own comment two branches up (`:4068-4069`) notes platforms with no `HOST_CACHED` type at all. On an integrated/UMA host where every host-visible type is cached, the same benchmark lands on cached memory and the read-back is nearly free. **The row's number is therefore a property of the capture host's memory topology, not of the wrapper** — which is the deeper reason it does not belong in a column labelled as the wrapper's per-call cost.

### The repo already documented this, in the wrong place

`samples/HelloVma/Program.cs:185-190`, written for the same flag combination the benchmark uses:

> `HostAccessSequentialWrite` — promises VMA you'll only write this from the CPU, never read it back. VMA prefers write-combined (uncached) memory in that case, which is fast for streaming writes but very slow for reads. *Never read* a sequential-write buffer from the CPU; it'll hit uncached memory and **your perf numbers will look haunted**.

The knowledge exists in a sample comment. It is absent from the XML docs on `Buffer.AsSpan<T>` / `AsReadOnlySpan<T>` (`Buffer.cs:116-132`) — the two API surfaces through which a caller performs the read — and it was not honoured by the benchmark. The predicted symptom and the observed symptom are the same sentence.

### Consumer audit: who reads through a mapped span, and on which flag

Every `AsSpan<T>` / `AsReadOnlySpan<T>` call site in `src/`, `samples/`, `tests/` (48 sites; `Generated/` excluded):

- **`src/` — two consumers, both write-only, both on sequential-write allocations.** `StagingUploader.Upload` (`StagingUploader.cs:120-121`, allocation at `:185`) and `StagingBatch` (`StagingBatch.cs:105`, allocation at `:278`) do `AsSpan<byte>().Slice(...)` followed by `CopyTo` — a `memmove` *into* write-combined memory with no read. This is exactly the pattern VMA's doc endorses, and it is the wrapper's only hot-path use of the API.
- **`samples/` — correct on both sides.** Read-back paths allocate `HostAccessRandom` (`AotSmoke/Program.cs:94`, `HeadlessTriangle/Program.cs:77`, `HeadlessExport/Program.cs:100`, `HelloVma/Program.cs:308`); write paths allocate `HostAccessSequentialWrite` (`HelloCube/Program.cs:158, 168, 521`, `HelloVma/Program.cs:218`, `HelloVmaWindowed/Program.cs:184`). The canonical per-frame write is `ubo.AsSpan<Frame>()[0] = BuildFrame(t, aspect);` (`HelloVmaWindowed/Program.cs:319`) — a whole-struct store, no read.
- **`tests/` — one functional read through a sequential-write allocation:** `MappedRegionTests.cs:54` allocates `HostAccessSequentialWrite | Mapped`, then `:59-62` writes `ints[0]` and reads it back through `AsReadOnlySpan<int>()[0]`. One read in a correctness test costs ~170 ns once; it is not a performance defect and is out of scope here.
- **`tests/Ahjo.Vulkan.Benchmarks/BufferBenchmarks.cs:70-72` is the only place in the repo where the read sits inside a measured loop.**

So the defect is confined to the benchmark. No shipped code path reads from a sequential-write allocation.

### The 170 ns is stable, not noise

An untracked local artifact from the #155 session is still in the working tree — `BenchmarkDotNet.Artifacts/results/Ahjo.Vulkan.Benchmarks.BufferBenchmarks-report-github.md` (the folder is gitignored, `docs/benchmarks.md:30`):

```
| Method     | Mean     | Error   | StdDev  | Allocated |
| Map_AsSpan | 169.7 ns | 1.87 ns | 1.75 ns |         - |
```

BDN v0.14.0, .NET 10.0.8 (SDK 10.0.204), Windows 11 10.0.26200.8894, Ryzen 9 7900X. StdDev is 1% of the Mean: whatever produces the figure is deterministic, which rules out scheduling noise, thermal effects, and driver hiccups as the explanation. (The artifact does not record the GPU; the same session recaptured the `PipelineBarrier.*` rows on an RTX 4070 Ti per `docs/benchmarks.md:61-65`, so that is the likely ICD — treat it as probable, not established.)

### Every other candidate explanation is bounded well below 170 ns

The issue's hypothesis has to beat the alternatives, so each was checked against something measurable in this repo:

| Candidate | Bound | Source |
|---|---|---|
| `checked((int)Size)` overflow check | one `cmp`/`jae` on a register-resident constant (`Size == 4096`) | `Buffer.cs:128` |
| `AsSpan` not inlined across the assembly boundary | a *non-inlined* call through a handle struct measures 3.69 ns on this host | `docs/benchmarks.md:87` (`HandleOwnership.PassAndReturn_ByValue`, `HandleOwnershipBenchmarks.cs:92-93`) |
| `[MemoryDiagnoser]` per-op overhead | < 0.47 ns — a row under the same attribute reports 0.47 ns/op | `docs/benchmarks.md:89`, `HandleOwnershipBenchmarks.cs:65-76` |
| BDN's `OperationsPerInvoke` accounting inflating the figure | it *divides*; a 0.47 ns row at `OperationsPerInvoke = 1_000_000` is impossible otherwise | same two citations; settled in #155 |
| `Span<int>` bounds check on `span[0]` | one `cmp`/`jae` | JIT-emitted |
| Handle-registry / diagnostics on the call | none reachable — `TrackCreate` is constructor-only | `Buffer.cs:73` |

Nothing on that list is within two orders of magnitude of the observed figure, and the load/store to mapped device-visible memory is the only remaining operation in the loop. The hypothesis survives elimination — but elimination is not measurement, which is why the plan gathers the confirming capture rather than asserting the conclusion (see *Uncertainty, stated*).

### Benchmark-surface facts the fix has to respect

- **`[MemoryDiagnoser]` on every class, `Allocated` must read `-`** — the file's stated contract (`docs/benchmarks.md:5-8, 37-41`; `tests/CLAUDE.md`).
- **No soft skip in the benchmark project.** Driver-bound classes fail at `[GlobalSetup]` on a host without an ICD, deliberately (`docs/benchmarks.md:109-113`).
- **The repo's existing DCE defence is a `[MethodImpl(MethodImplOptions.NoInlining)]` static helper**, not BDN's `Consumer`: `HandleOwnershipBenchmarks.cs:92-97` (`RoundTrip`, `DescribeHandle`). `Consumer` and `DeadCodeEliminationHelper` appear nowhere in `tests/` today.
- **`InternalsVisibleTo` covers the benchmark project** (`src/Ahjo.Vulkan/Ahjo.Vulkan.csproj:27`), and the VMA bindings are available transitively (`Ahjo.Vulkan.Tests` already uses `VmaAllocationCreateFlagBits` at `ShadowEnumDriftTests.cs:70` with no direct reference). Everything needed to interrogate the allocation is reachable from the benchmark without a csproj change and without new wrapper API.
- **`Buffer.Map<T>()` is deliberately unbenchmarked** because `MappedRegion<T>` is a class (`MappedRegion.cs:27`) and its allocation is a property of the `MemoryManager<T>`-shaped API — the rationale at `BufferBenchmarks.cs:8-13` still holds and survives this change.
- **The row name has two out-of-table references**: `docs/benchmarks.md:93` (the `OperationsPerInvoke` example list), `docs/benchmarks.md:109` ("Buffer.Map" in the driver-dependency caveat), and `.claude/skills/run-bench/SKILL.md:70` (the driver-bound class list). A rename has to carry all three.
- **Table rows already drift from method names**: `docs/benchmarks.md:87-90` lists `HandleOwnership.PassAndReturn_ByValue` etc. while the methods are `PassAndReturn_ByValue_TightLoop` etc. (`HandleOwnershipBenchmarks.cs:38, 52, 66, 79`). Not fixed here (see non-goals), but it is why the new rows are specified to match their method names character-for-character.

## Decision

Four decisions, all inside `tests/` and `docs/` except two XML-doc paragraphs. **No wrapper behaviour changes.**

### D1 — one op = one `AsSpan` call through a `NoInlining` boundary; four rows, not one

`BufferBenchmarks` gets four `[Benchmark]` methods, all with `OperationsPerInvoke = CallsPerInvoke` (1024) and all routing the call under test through a static `[MethodImpl(MethodImplOptions.NoInlining)]` helper taking `in Buffer`. The helpers differ only in what they do with the span, so **row differences localize cost**:

| Method | Helper does | Allocation | Answers |
|---|---|---|---|
| `AsSpan_ViewOnly` | `AsSpan<int>()`, consumes pointer ^ length, touches no device memory | sequential-write | *what does the API cost?* |
| `AsSpan_SequentialWrite` | `AsSpan<int>()`, one store at index `i` | sequential-write | *what does the flag's endorsed pattern cost?* |
| `AsSpan_WriteThenRead_SeqWriteAlloc` | the old body verbatim: store at index 0, load index 0 | sequential-write | *reproduces 166.8 ns; documents the footgun* |
| `AsSpan_WriteThenRead_RandomAlloc` | same body | `HostAccessRandom` | *is the cost memory-type dependent?* |

Rationale for each part of that shape:

- **The `NoInlining` boundary is the only DCE defence that survives the JIT here.** `AsSpan<int>()` on a loop-invariant field is loop-invariant, so an inlined call is a candidate for hoisting out of the loop entirely; a row that measures the loop counter is the exact failure the removed `≈0.16 ns/op` annotations already inflicted on this file. Consuming a derived scalar (`span.Length`) does not help, because the scalar is loop-invariant too. A non-inlined call with a byref argument cannot be hoisted. The cost is that the row includes the call, i.e. it is an **upper bound** on `AsSpan` — which the row's Notes must say, exactly as `docs/benchmarks.md:87` already says "copied through a non-inlined call" for `HandleOwnership.PassAndReturn_ByValue`.
- **Consuming pointer ^ length, not the span's contents,** is what keeps `AsSpan_ViewOnly` off the memory. The pointer forces the span to be materialised; neither operand dereferences it. This is the specific error the old row made, and it cannot recur by construction: the helper never indexes the span.
- **The write row uses index `i`, not index 0.** `Size = 4096` and `T = int` make the span exactly `CallsPerInvoke` elements, so one invoke is precisely one sequential 4 KiB fill — the "loop writing number-by-number" pattern VMA's doc endorses (`vk_mem_alloc.h:652-654`), and the shape of `HelloVmaWindowed/Program.cs:319`. Repeated stores to a single address would additionally invite redundant-store elimination.
- **The read-back rows keep index 0 and the old body verbatim** so the recaptured number is directly comparable to 166.8 ns. Same-address write-then-read is also the pathological case for write-combining — the read must observe the just-issued write — so it is the sharpest available probe.
- **Four rows rather than one** because the issue asks for three distinct things the table has to answer: what the API costs (row 1), what the endorsed pattern costs (row 2, the issue's "keeping the write is honest"), and whether the 170 ns is memory-type-dependent (rows 3 vs 4, the issue's requested `HostAccessRandom` comparison). Rows 3 and 4 are labelled in the table as **memory-behaviour probes, not wrapper canaries**; their `Allocated` cells are `-` like everything else, but their Mean is a property of the host, and only their *ratio* carries information.

Bulk write throughput into a sequential-write mapped span is already covered by `StagingUploader.Upload_4KiB_Float` (`StagingUploaderBenchmarks.cs:59-65`, which `memcpy`s 4 KiB through `AsSpan<byte>().Slice`), so row 2 is deliberately per-element, not a second `memcpy` row.

### D2 — the old row is renamed and recaptured, never deleted

`Buffer.Map_AsSpan` becomes `Buffer.AsSpan_WriteThenRead_SeqWriteAlloc` — same allocation, same body, recaptured on the same host. Its number is expected to stay ≈166-175 ns.

This is the answer to "don't let git history read as a 100x win". A reader diffing this PR sees: one benchmark renamed with its number unchanged, three benchmarks added, and **zero executable changes under `src/`** (the only `Buffer.cs` edit is comment text). There is no plausible reading in which the wrapper got faster. The alternative — deleting the row and publishing a new ~3 ns row under a name resembling the old one — would encode exactly the false claim the issue wants avoided.

`docs/benchmarks.md` additionally carries a `## Caveats` bullet stating in prose that the rename happened, that the number did not change, and that `AsSpan_ViewOnly` is a *new measurement of a different thing*, not an improvement.

### D3 — the diagnosis is confirmed by a permanent memory-type report plus a controlled A/B, and the docs wording is gated on the outcome

Two mechanisms, both in the benchmark class:

1. **`[GlobalSetup]` prints what VMA selected**, once per benchmark process, for both allocations: memory type index, the property flags decoded through the wrapper's own `MemoryProperties` shadow enum (`MemoryProperties.cs:14-38`, which has `HostCached` at `:31`), the heap index, and the heap size. `vmaGetAllocationInfo` supplies `memoryType` (`Generated/Vma.cs:93`; the same field `Allocator.cs:247` already reads), `vmaGetAllocationMemoryProperties` supplies the flags (`Generated/Vma.cs:105`; already called at `Allocator.cs:153`), and `vmaGetMemoryProperties` (`Generated/Vma.cs:21`) maps type to heap. This is what makes the rows interpretable on a host other than the capture host — necessary precisely because §Evidence establishes the numbers are a memory-topology property. It also distinguishes uncached system memory (`HostVisible, HostCoherent`, large system heap) from a BAR mapping (`DeviceLocal, HostVisible, HostCoherent`, small device heap), which are different explanations for a slow read.
2. **The A/B is rows 3 vs 4** — identical body, identical size, identical buffer usage, one flag different. That is the controlled experiment the issue asks for.

The benchmark fix (D1, D2) is correct regardless of *why* the read is slow. Only the explanatory prose depends on the diagnosis, so the plan gates the wording on four stop conditions (reproduce ≈170 ns; `ViewOnly` in single digits; `HostCached` absent from the selected type; the Random row substantially faster). If any fails, the implementer reports rather than writing an explanation the evidence does not support.

### D4 — `AsReadOnlySpan` gets documentation, not a new API

`Buffer.AsSpan<T>` gets a `<remarks>` paragraph stating that the *view* is nearly free but that read latency through it is a property of the memory type the allocation landed on; that `AllocationFlags.HostAccessSequentialWrite` lets VMA pick uncached/write-combined memory where a host read costs orders of magnitude more than a cached one; and that callers who need to read should allocate `AllocationFlags.HostAccessRandom`. `AsReadOnlySpan<T>` gets the matching warning: a read-only view does not make reads cheap, and this API is the one that invites the mistake.

Deliberately **not** in scope: exposing `Buffer.IsHostCached` (or a memory-type index) so callers could branch at runtime. It is a real gap — `Allocator.CreateBuffer` already has the property bits in hand (`Allocator.cs:152-163`) and discards everything but `HOST_VISIBLE`/`HOST_COHERENT`, and `MemoryBlock` exposes `MemoryTypeIndex` (`MemoryBlock.cs:48`) while `Buffer` exposes nothing comparable — but it is a public-API decision about a by-value hot-path struct, it should be decided together with that asymmetry, and this spec is about the honesty of a measurement. Recommend a follow-up issue.

### Why not the alternatives

- **Drop the read-back and keep a single row.** Leaves 166.8 ns unexplained and unreproducible, discards the footgun documentation the issue explicitly asks for (item 4), and makes the row's number appear to collapse by 100x. Rejected.
- **Keep the read-back but allocate `HostAccessRandom`** ("make the allocation match the access"). Produces a fast, honest-looking number that still does not measure `AsSpan` — it measures an L1 load. It also drops coverage of the flag combination the wrapper itself uses on its only hot-path consumer (`StagingUploader.cs:185`). Rejected as a replacement; kept as row 4, which is its correct role.
- **BDN's `Consumer` / `DeadCodeEliminationHelper.KeepAliveWithoutBoxing`.** `Span<T>` is a `ref struct` and cannot be a generic type argument for either under BDN 0.14 (`Directory.Packages.props:47`), so the consumer would have to take a derived scalar — which is loop-invariant, so LICM can still hoist the whole call. It would also introduce a DCE idiom that appears nowhere in `tests/` today, against the existing `NoInlining`-helper precedent (`HandleOwnershipBenchmarks.cs:92-97`). Rejected.
- **Measure `AsSpan` with no DCE barrier at all** (the "measure what the engine sees, inlined" position). The call is loop-invariant; the likely result is a sub-nanosecond row measuring the loop counter. That is the same class of error as the `≈0.16 ns/op` tails #155 removed (`docs/benchmarks.md:96-101`). Rejected — an upper bound that is honest about including a call beats a lower bound that is a JIT artifact.
- **Keep the row as-is with an explanatory note.** Cheapest option, and it fixes defect 1 only. The row still cannot detect an `AsSpan` regression, so the canary stays dead. Rejected.
- **Make `AsReadOnlySpan<T>` throw / warn on a sequential-write allocation.** Reads from such an allocation are legal, sometimes deliberate (`MappedRegionTests.cs:59-62`), and only sometimes latency-relevant. Turning a legal, correct operation into a failure for performance reasons is not the wrapper's call. Rejected.
- **Debug-only `AhjoValidation` diagnostic on the read.** Technically impossible: the wrapper hands out a `Span<T>` and the read happens with no wrapper frame involved. Rejected as unimplementable, and worth recording so it is not re-proposed.
- **Add `Buffer.IsHostCached`.** Deferred, not rejected on merit — see D4.

## What this does not change (non-goals)

- `src/Ahjo.Vulkan/Resources/Buffer.cs` gains comment text only. No signature, no field, no behaviour.
- `MappedRegionTests.cs:59-62` keeps its read through a sequential-write allocation. One read in a functional test is not a perf defect.
- `AllocationFlags` (`AllocationFlags.cs:11-25`) stays without per-member XML docs. Documenting two of fifteen members would be lopsided; documenting all fifteen is a separate docs task. The enum's summary already points at `VmaAllocationCreateFlagBits`, whose in-tree text carries the warning verbatim.
- The `HandleOwnership.*` table-row / method-name drift (`docs/benchmarks.md:87-90` vs `HandleOwnershipBenchmarks.cs:38, 52, 66, 79`) is not fixed here. Separate one-line docs change; noted so the next person does not think this spec blessed the drift.
- The baseline table's coverage gaps (`GraphicsPipelineBuilder`, `PushConstants`, `SpecializationInfo`, `StagingUploader`, `SyncPool`, `ResultPolicy`, `PhysicalDevicePicker` all have benchmark classes and no rows) are untouched.
- No CI change. Benchmarks are a manual capture (`docs/benchmarks.md:160-161`) and the wrapper suite is Windows-only (#32).

## Invariants honored

- **Zero per-frame allocations** — all four benchmark bodies stay stack-only; `Allocated` must read `-` for each. The `[GlobalSetup]` report allocates (string interpolation, enum `ToString`) but runs outside every measured region, like the existing device/buffer creation in the same method.
- **UTF-8 literals** — no new string reaches a Vulkan `const char*`.
- **Native AOT** — the benchmark project is not AOT-published; no reflection, no dynamic codegen, no trim-unsafe pattern is introduced anywhere reachable from the wrapper.
- **Generated code untouched** — every VMA entry point needed already exists (`src/Ahjo.Vulkan.Vma.Native/Generated/Vma.cs:21, 93, 105`). No `.rsp` change, no regen.
- **`TreatWarningsAsErrors`** — no suppression required. Interpolated `Console.WriteLine` builds clean elsewhere in the repo (`samples/HelloVma/Program.cs:229-230`); the class becomes `unsafe`, matching `DescriptorSetPoolBenchmarks.cs:20` and `HandleOwnershipBenchmarks.cs:18`, and `AllowUnsafeBlocks` is repo-wide (`Directory.Build.props`).

## Uncertainty, stated

- **The write-combining diagnosis is inference, not yet measurement.** Everything above is either upstream source, in-repo citation, or elimination of alternatives by measured bounds. What is *not* yet in hand: the actual memory type the capture host selected. The plan's step 2 obtains it, and the plan will not let a WC explanation be written into `docs/benchmarks.md` or `Buffer.cs` if the selected type turns out to carry `HostCached`. If it does, the 170 ns has no established cause and the correct output is a report, not a story.
- **The numbers in this spec are expectations, not results.** `AsSpan_ViewOnly` is expected in the low single digits (bounded below by ~3.69 ns for a comparable non-inlined round trip on this host, `docs/benchmarks.md:87`); `AsSpan_SequentialWrite` well under 20 ns (a posted store into a write-combining buffer); `AsSpan_WriteThenRead_SeqWriteAlloc` ≈166-175 ns; `AsSpan_WriteThenRead_RandomAlloc` single digits to low tens. They exist so a wrong result is recognisable, not to be transcribed into the table.
- **`AsSpan_ViewOnly` measures an upper bound.** It includes a non-inlined call that a real engine call site would not pay, and it cannot measure the fully-inlined cost, because a fully-inlined loop-invariant call is not observable by a wall-clock benchmark. The row's Notes must say so; a reader who wants the inlined figure has to read `Buffer.cs:122-129`.
- **The rows will read differently on integrated/UMA hosts and under MoltenVK**, where a `HostCached` host-visible type may be the only option, collapsing rows 3 and 4 onto each other. That is not a regression; the `[GlobalSetup]` report is what lets the next reader tell the two situations apart.

## Cross-links

- **Resolves:** #157 (all four requested items: confirm/refute, fix the measurement, recapture with a note, document the `AsReadOnlySpan` read cost).
- **Corrects a row introduced by:** #29 (`6121fff`).
- **Depends on the arithmetic correction from:** #155 (`docs/benchmarks.md:92-103`).
- **Consistent with:** #34 (`StagingUploader`'s sequential-write-and-never-read discipline is what the new docs paragraph tells callers to imitate), #118 (the `NoInlining`-helper benchmark idiom this reuses).
- **Recommends a follow-up:** expose the selected memory type on `Buffer` (`IsHostCached` and/or a type index) to close the asymmetry with `MemoryBlock.MemoryTypeIndex` (`MemoryBlock.cs:48`) — explicitly out of scope here (D4).
</content>
</invoke>
