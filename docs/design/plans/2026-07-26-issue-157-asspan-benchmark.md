# Implementation plan — make `Buffer.AsSpan` measurable (#157)

Paired with `../specs/2026-07-26-issue-157-asspan-benchmark-design.md`.

Ship: `tests/Ahjo.Vulkan.Benchmarks/BufferBenchmarks.cs` rewritten to four
benchmarks behind `NoInlining` helpers plus a `[GlobalSetup]` memory-type
report; two XML-doc paragraphs on `Buffer.AsSpan<T>` / `AsReadOnlySpan<T>`;
`docs/benchmarks.md` rows + caveat; one line in
`.claude/skills/run-bench/SKILL.md`.

**No executable change under `src/`.** `Resources/Buffer.cs` receives comment
text only — if a step here makes you want to change a signature, a field, or a
statement in `src/`, stop and report instead.

Nothing under `src/*/Generated/` is touched: every VMA entry point needed
already exists (`src/Ahjo.Vulkan.Vma.Native/Generated/Vma.cs:21, 93, 105`), and
the benchmark project reaches them transitively (precedent:
`tests/Ahjo.Vulkan.Tests/ShadowEnumDriftTests.cs:70` uses
`VmaAllocationCreateFlagBits` with no direct project reference). No csproj
change anywhere.

## Step 1 — rewrite `BufferBenchmarks`

File: `tests/Ahjo.Vulkan.Benchmarks/BufferBenchmarks.cs`. Whole-file rewrite.

**1a. Usings and class declaration.** Add `using System.Runtime.CompilerServices;`
(for `MethodImpl`, `Unsafe`), `using System.Runtime.InteropServices;` (for
`MemoryMarshal`), `using Ahjo.Vulkan.Native;` (for
`VkPhysicalDeviceMemoryProperties`, `VkMemoryType`, `VkMemoryHeap`), and
`using VmaApi = Ahjo.Vulkan.Vma.Native.Vma;` — the alias spelling
`src/Ahjo.Vulkan/Memory/Allocator.cs:4` uses. Keep
`using BenchmarkDotNet.Attributes;`. The class becomes
`public unsafe class BufferBenchmarks` (precedent:
`DescriptorSetPoolBenchmarks.cs:20`, `HandleOwnershipBenchmarks.cs:18`);
`[MemoryDiagnoser]` stays.

**1b. Fields.** Keep `private const int CallsPerInvoke = 1024;`, `_instance`,
`_device`. Replace `_buffer` with two fields:

```csharp
private Buffer _seqWrite;   // HostAccessSequentialWrite | Mapped
private Buffer _hostRandom; // HostAccessRandom          | Mapped
```

**1c. `[GlobalSetup]`.** Keep the instance/physical-device/device block
verbatim (`:27-45`). Then create both buffers from one local
`BufferDescription`-shaped pair of calls, differing **only** in the host-access
flag — the A/B is worthless if anything else differs:

```csharp
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
```

Then a hard guard — `AsSpan_SequentialWrite` indexes `span[i]` for
`i ∈ [0, CallsPerInvoke)`, so the coupling between `Size` and `CallsPerInvoke`
must fail loudly if either is edited later. Throw, do not skip (the benchmark
project has no soft-skip path, `docs/benchmarks.md:109-113`):

```csharp
if (_seqWrite.AsSpan<int>().Length != CallsPerInvoke)
    throw new InvalidOperationException(
        $"BufferBenchmarks: expected a {CallsPerInvoke}-int span, got {_seqWrite.AsSpan<int>().Length}. " +
        "Size and CallsPerInvoke must stay in sync — AsSpan_SequentialWrite writes one int per op.");
```

Finish with the two report calls from step 1d.

**1d. The memory-type report.** One private static method, called once per
benchmark process from `[GlobalSetup]` for each buffer:

```csharp
private static void ReportMemoryType(string label, in Buffer buffer)
```

Body: fetch and print, no asserts.

- `VmaApi.vmaGetAllocationMemoryProperties(buffer.Owner.Handle, buffer.AllocationHandle, &flags)` — `Allocator.Handle` is `internal` and reachable via `InternalsVisibleTo` (`src/Ahjo.Vulkan/Ahjo.Vulkan.csproj:27`); the same call shape is at `Allocator.cs:153`.
- `VmaApi.vmaGetAllocationInfo(buffer.Owner.Handle, buffer.AllocationHandle, &info)` → `info.memoryType` (field confirmed at `Generated/VmaAllocationInfo.cs:6`; `Allocator.cs:247` reads it).
- `VmaApi.vmaGetMemoryProperties(buffer.Owner.Handle, &props)` → heap index and size, via the pointer-arithmetic form already used in the wrapper (`Interop/ExportableImage.cs:250-251`): `VkMemoryType* types = &props->memoryTypes.e0;` then `uint heap = types[info.memoryType].heapIndex;` and `VkMemoryHeap* heaps = &props->memoryHeaps.e0;` → `heaps[heap].size`.
- Decode the flags through the wrapper's own shadow enum, not by hand:
  `(MemoryProperties)flags` (`src/Ahjo.Vulkan/Memory/MemoryProperties.cs:14-38`, `HostCached` at `:31`). A `[Flags]` `ToString()` renders e.g. `HostVisible, HostCoherent`.

One line per buffer, plain text (no `//` prefix — that is BDN's own log
prefix), exact shape:

```
[BufferBenchmarks] seq-write alloc: memoryType=1 flags=HostVisible, HostCoherent heap=1 heapSizeMiB=32079
```

Interpolated `Console.WriteLine` is fine under `TreatWarningsAsErrors`
(precedent: `samples/HelloVma/Program.cs:229-230`).

**Known risk, do not fight it:** BDN runs each benchmark in a child process and
forwards its stdout to the log, so these lines should appear once per benchmark
in the run output. If BDN v0.14.0 swallows or garbles them, keep the code
(it is correct and cheap) and obtain the same values for step 6 by any local
means you prefer — a scratch console project that is **not committed**, or a
debugger breakpoint in `[GlobalSetup]`. Do not restructure the benchmark to
work around BDN's logging, and do not add a test to print it.

**1e. `[GlobalCleanup]`.** Dispose both buffers before `_device`, keeping the
existing null-conditional shape (`:56-62`):
`_hostRandom.Dispose(); _seqWrite.Dispose(); _device?.Dispose(); _instance?.Dispose();`

**1f. The four benchmarks.** Each is a `for` loop of `CallsPerInvoke`
iterations calling one helper; each carries
`[Benchmark(OperationsPerInvoke = CallsPerInvoke)]`.

```csharp
[Benchmark(OperationsPerInvoke = CallsPerInvoke)]
public nuint AsSpan_ViewOnly()                      // acc ^= SpanIdentity(in _seqWrite);

[Benchmark(OperationsPerInvoke = CallsPerInvoke)]
public void AsSpan_SequentialWrite()                // WriteOne(in _seqWrite, i, i);

[Benchmark(OperationsPerInvoke = CallsPerInvoke)]
public int AsSpan_WriteThenRead_SeqWriteAlloc()     // sum += WriteThenReadFirst(in _seqWrite, i);

[Benchmark(OperationsPerInvoke = CallsPerInvoke)]
public int AsSpan_WriteThenRead_RandomAlloc()       // sum += WriteThenReadFirst(in _hostRandom, i);
```

**1g. The three helpers.** All `private static`, all
`[MethodImpl(MethodImplOptions.NoInlining)]`, all taking `in Buffer` (no
defensive copy — `Buffer` is a `readonly struct`; mirrors
`CommandRecorder`'s `in PipelineLayout` convention):

```csharp
// Materialises the span and consumes its pointer + length. Never indexes it:
// this row must not touch device-visible memory (that is the #157 defect).
private static nuint SpanIdentity(in Buffer buffer)
{
    Span<int> span = buffer.AsSpan<int>();
    return (nuint)Unsafe.AsPointer(ref MemoryMarshal.GetReference(span)) ^ (nuint)span.Length;
}

// One sequential store — the pattern HostAccessSequentialWrite promises.
private static void WriteOne(in Buffer buffer, int index, int value)
    => buffer.AsSpan<int>()[index] = value;

// The pre-#157 body verbatim: store then read the SAME element back.
private static int WriteThenReadFirst(in Buffer buffer, int value)
{
    Span<int> span = buffer.AsSpan<int>();
    span[0] = value;
    return span[0];
}
```

Do not "simplify" `SpanIdentity` to return `span.Length` alone: the pointer
operand is what forces the span to be materialised.

**1h. Doc comments.** Replace the class summary (`:5-14`) — keep its surviving
claim (`Map<T>()` is not benchmarked because `MappedRegion<T>` is a class,
`src/Ahjo.Vulkan/Memory/MappedRegion.cs:27`) and add:

- What one op is: one `AsSpan<int>()` call through a non-inlined static helper,
  so every row shares the same call boundary and row differences localize cost.
- Why the boundary exists: `AsSpan` on a loop-invariant field is hoistable; an
  inlined row would measure the loop counter. `AsSpan_ViewOnly` is therefore an
  **upper bound** that includes a call a real call site would not pay.
- Why `AsSpan_ViewOnly` consumes pointer ^ length and never indexes the span
  (#157: the old row's `sum += span[0]` measured a host read from
  write-combined memory, not the API).
- What the two `WriteThenRead` rows are for: **memory-behaviour probes, not
  wrapper canaries.** Identical bodies, one flag apart; only their ratio
  carries information, and both numbers are properties of the host's memory
  topology.
- That nothing here is ever submitted to a queue and no GPU-written data is
  read, so no `Flush`/`Invalidate` bracketing is involved.

Per-method XML doc on all four, one or two lines each, in the same voice.

## Step 2 — capture, and check the four stop conditions

Release, Windows host with a real ICD (`/run-bench`). Note the filter: `*Buffer*`
would also match `CommandBufferPoolBenchmarks`.

```
dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter "*BufferBenchmarks*"
```

Record, for the PR body and step 6: the four Means, the four `Allocated` cells,
and both `[BufferBenchmarks]` report lines.

**Every `Allocated` cell must read `-`.** Anything else is a defect in the new
benchmark code, not a finding — fix it before proceeding.

Then evaluate, in order. Each is a **stop-and-report** condition, not a
judgement call:

1. `AsSpan_WriteThenRead_SeqWriteAlloc` must reproduce the baseline
   (`docs/benchmarks.md:75` = 166.8 ns; the #155 local re-measure was 169.7 ns
   ± 1.75). Outside roughly ±30% → **stop**: the published figure had some
   other cause, or this host is not the baseline host, and everything
   downstream is built on an anchor that does not hold.
2. `AsSpan_ViewOnly` must be small — expect low single digits, bounded below by
   ~3.69 ns for a comparable non-inlined round trip on this host
   (`docs/benchmarks.md:87`). Above ~30 ns → **stop**: `AsSpan` itself would be
   the cost, which contradicts the spec's premise and needs a new design, not a
   docs edit.
3. The seq-write report line must **not** list `HostCached`. If it does →
   **stop**: the write-combining explanation is refuted, the 170 ns has no
   established cause, and no WC wording may be written into `docs/benchmarks.md`
   or `Buffer.cs`. Steps 1 and 2 still stand; report and wait.
4. `AsSpan_WriteThenRead_RandomAlloc` must be substantially faster than the
   seq-write row (expect single digits to low tens, i.e. ≥ 5x). Within ~2x →
   **stop**: the read cost is not memory-type dependent, so the diagnosis is
   wrong regardless of what the flags say.

Optional one-shot probe, if you want the read's cost isolated on identical
addressing (steps 3-6 do not depend on it): temporarily add a fifth benchmark
calling `WriteOne(in _seqWrite, 0, i)` — index 0, same address as the
`WriteThenRead` rows — run it, note the number in the PR body, then **delete
it**. Do not commit a fifth row.

## Step 3 — XML docs on `Buffer` (comment text only)

File: `src/Ahjo.Vulkan/Resources/Buffer.cs`. Two edits, both inside doc
comments. Do not touch a single statement in this file.

**3a. `AsSpan<T>()` (`:116-121`)** — keep the existing `<summary>`, add a
`<remarks>` with two short paragraphs:

- The view itself is nearly free (a null check, a span construction over the
  cached `pMappedData`, a cast that is a length division). What is *not* free is
  reading through it: latency is a property of the memory type VMA chose.
- `AllocationFlags.HostAccessSequentialWrite` declares that the memory is
  written sequentially and never read, which lets VMA select an **uncached,
  write-combined** memory type; a host read from such an allocation costs orders
  of magnitude more than a cached one — measured at *N* ns vs *M* ns per read on
  the baseline host (fill from step 2, and cite `docs/benchmarks.md`). Callers
  who need to read should allocate `AllocationFlags.HostAccessRandom`, which
  makes VMA prefer a `HOST_CACHED` type. Mention that `Flush`/`Invalidate` are
  unrelated to this (they are about coherency, not caching) so nobody reaches
  for the wrong tool.

Reuse the wording already in `samples/HelloVma/Program.cs:185-190` rather than
inventing new phrasing; that comment predates the defect and says the right
thing.

**3b. `AsReadOnlySpan<T>()` (`:131-132`)** — the one-line summary becomes
summary + `<remarks>`: a read-only view does **not** make reads cheap. This is
the surface that invites the mistake on a `HostAccessSequentialWrite`
allocation; see `AsSpan{T}`'s remarks and prefer
`AllocationFlags.HostAccessRandom` when the CPU has to read. Note that the
wrapper cannot detect the mistake — it hands out a span and the read happens
with no wrapper frame involved.

If step 2's condition 3 or 4 tripped, write **only** the flag-neutral half
("read latency depends on the memory type the allocation landed on; use
`HostAccessRandom` when you need to read") and omit every write-combining
claim.

## Step 4 — tests: none, deliberately

No test is added, and this is a decision, not an omission:

- The behavioural change set is empty — `src/` gains comment text only, so
  there is nothing new to assert.
- Benchmarks are not unit-testable; their contract (`Allocated == -`) is checked
  by running them (step 2).
- An asserting test on the memory type — e.g. "the sequential-write allocation
  is not `HOST_CACHED`" — would be **wrong to add**: VMA sets `HOST_CACHED` only
  as a *not-preferred* penalty (`native/vma/include/vk_mem_alloc.h:4088`) and
  its own comment notes platforms with no `HOST_CACHED` type at all (`:4068-4069`),
  so such a test would fail on integrated/UMA hosts and under MoltenVK. The
  `[GlobalSetup]` report (step 1d) is the right mechanism: it observes without
  asserting.
- The existing suite already covers the API's contract
  (`tests/Ahjo.Vulkan.Tests/MappedRegionTests.cs:42-63` for the persistent-mapped
  span, `:130-136` for the non-mapped throw). Nothing there changes.

`dotnet test` must still pass unchanged — run it (step 7) as a regression check
on the `Buffer.cs` comment edit, not because new cases exist.

## Step 5 — `.claude/skills/run-bench/SKILL.md`

`:70` lists `Buffer.Map_AsSpan` among the driver-bound benchmarks. Rename that
entry to `BufferBenchmarks` (the class, so it cannot drift again as rows are
added).

**OPEN:** this is the only file in the change set outside `src/`, `tests/`, and
`docs/`, and it is agent tooling. If editing `.claude/` is outside your remit,
**stop and report** — do not silently leave a stale benchmark name behind.

## Step 6 — `docs/benchmarks.md`

Five edits. Use the numbers from step 2 verbatim; do not carry forward any
number this plan quotes as an expectation.

**6a. Baseline table** — replace the single `Buffer.Map_AsSpan` row (`:75`)
with four rows, in this order, `Allocated` = `-` for all four:

| Row | Notes column must say |
|---|---|
| `Buffer.AsSpan_ViewOnly` | Persistent-mapped `AsSpan<T>` through a non-inlined helper, consuming pointer + length and **touching no device memory** — an upper bound on the API (includes the call). |
| `Buffer.AsSpan_SequentialWrite` | One `AsSpan<T>` + one sequential `int` store per op; one invoke = one 4 KiB sequential fill of a `HostAccessSequentialWrite` allocation. |
| `Buffer.AsSpan_WriteThenRead_SeqWriteAlloc` | #157 probe, **not a wrapper canary**: store + read-back of the same element on a `HostAccessSequentialWrite` allocation. This is the former `Buffer.Map_AsSpan` (166.8 ns) renamed — the number did not change. |
| `Buffer.AsSpan_WriteThenRead_RandomAlloc` | #157 probe, **not a wrapper canary**: identical body on a `HostAccessRandom` allocation. Only the ratio against the row above carries information. |

**6b. `## Caveats`** (list starts `:106`) — add one bullet,
**Host reads are a memory-type property, not a wrapper property (#157)**,
covering, in this order:

1. The old `Buffer.Map_AsSpan` row's 166.8 ns was a host read from the mapped
   allocation, not `AsSpan<T>`'s cost (`Resources/Buffer.cs:122-129` is a null
   check, a span construction and a cast). The row is **renamed, not deleted**,
   and its number is unchanged; `AsSpan_ViewOnly` is a new measurement of a
   different thing and **not a speed-up** — no wrapper code changed in #157.
2. Why (subject to step 2's conditions 3-4 passing): `HostAccessSequentialWrite`
   lets VMA select an uncached/write-combined type — quote
   `native/vma/include/vk_mem_alloc.h:652-658` and note the selection code at
   `:4085-4090` sets `HOST_CACHED` as *not preferred*.
3. The captured evidence: both `[BufferBenchmarks]` report lines verbatim
   (memory type index, decoded property flags, heap index, heap size).
4. Portability: the flag makes `HOST_CACHED` unlikely, not impossible
   (`vk_mem_alloc.h:4068-4069`). On an integrated/UMA host the two
   `WriteThenRead` rows may collapse onto each other; that is not a regression,
   and `[GlobalSetup]` prints what the reader's own host chose.
5. The optional index-0 write probe's number from step 2, if you ran it, as
   prose — no permanent row.

**6c. Driver-dependency caveat (`:109-110`)** — "FrameRing / Buffer.Map /
CommandRecorder / …" becomes "… / `BufferBenchmarks` / …".

**6d. `OperationsPerInvoke` reading note (`:92-103`)** — the example list at
`:93` names `Buffer.Map_AsSpan`; change it to `Buffer.AsSpan_*`. Leave the rest
of that paragraph alone (it is #155's correction and is still accurate).

**6e. Driver-bound filter example (`:27`)** — add `*BufferBenchmarks*` to the
pipe-separated list. Use the class-qualified pattern, not `*Buffer*`, which also
matches `CommandBufferPoolBenchmarks`.

Do **not** refresh any other row: the table's own rule is that rows are
comparable to their own successors, not to each other (`:61-65`).

## Step 7 — verify

- `dotnet build Ahjo.Vulkan.slnx` clean under `TreatWarningsAsErrors`, then
  `dotnet test` (step 4: expected unchanged).
- Step 2's capture, including all four stop conditions and `Allocated = -` on
  every new row.
- Run `bench-coverage-checker` — the diff is entirely a benchmark rewrite of a
  hot-path canary; it should confirm `AsSpan` coverage improved rather than
  regressed and that no `Allocated` cell slipped off `-`.
- Run `vulkan-validation-reviewer` — the diff touches `Resources/`, which the
  repo rule covers (`src/Ahjo.Vulkan/CLAUDE.md`). Expect a no-finding review
  (comments only); the point is that the rule is not skipped by judgement.
- Commit: `Benchmarks: measure Buffer.AsSpan, not a write-combined read-back`.
  PR references `Closes #157` and **states in the body** that `src/` changed by
  comment text only, that the old row was renamed with its number intact, and
  that `AsSpan_ViewOnly` is a new row rather than a 100x improvement. Paste the
  four Means, the four `Allocated` cells, and both report lines.

## Risk notes

- **The row rename is the load-bearing part of the history story.** If you find
  yourself deleting `AsSpan_WriteThenRead_SeqWriteAlloc` because it "does not
  measure the wrapper", stop: it exists to keep 166.8 ns attached to a live,
  reproducible benchmark. Without it the table looks like a 100x win.
- **`AsSpan_ViewOnly` can be broken silently.** Any future edit that indexes the
  span inside `SpanIdentity` re-creates the exact #157 defect and the row will
  quietly grow by ~150 ns. The helper's comment says so; keep it.
- **The `NoInlining` boundary is a floor, not overhead to be optimised away.**
  If the row reads ~3-4 ns, that is mostly the call. Removing the attribute to
  "get a cleaner number" would produce a hoisted, meaningless row.
- **Four buffers' worth of setup per BDN process.** `[GlobalSetup]` runs once
  per benchmark process, so the run creates an instance + device + two buffers
  four times and prints the report four times. Expected; not a leak.
- **The docs wording is downstream of the capture.** If step 2's conditions 3-4
  trip, steps 1, 2, 5, 6a, 6c-6e still stand; 3a/3b lose their write-combining
  claims and 6b becomes "cause not established — see #157". Report before
  writing prose the evidence does not support.
</content>
