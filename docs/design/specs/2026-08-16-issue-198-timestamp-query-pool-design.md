# Timestamp query pools — `QueryPool` handle + `WriteTimestamp` / `ResetQueryPool` / results readback

**Issue:** [#198](https://github.com/pekkah/Ahjo-Vulkan/issues/198) — *Sync: a timestamp query-pool surface (Device.CreateQueryPool, CommandRecorder.WriteTimestamp)*
**Lands consistently with:** [#155](https://github.com/pekkah/Ahjo-Vulkan/issues/155) (`Event` — the owning-handle-in-`Sync/` template this copies), [#118](https://github.com/pekkah/Ahjo-Vulkan/issues/118) (handle ownership / `OwnsHandle` matrix), [#117](https://github.com/pekkah/Ahjo-Vulkan/issues/117) (result-policy guard — `vkGetQueryPoolResults` is multi-success), [#121](https://github.com/pekkah/Ahjo-Vulkan/issues/121) (per-device dispatch table), [#120](https://github.com/pekkah/Ahjo-Vulkan/issues/120) (device-loss choke point)
**Test strategy constrained by:** [#32](https://github.com/pekkah/Ahjo-Vulkan/issues/32) (wrapper suite is Windows-only; no software-rasterizer coverage)
**Consumer:** Ahjo engine `Ahjo.Rhi.RhiPassRecorder` (ADR-0023) — per-pass GPU time attribution against the ADR-0020/ADR-0022 budgets
**Date:** 2026-08-16

## Problem

The wrapper has no way to measure GPU time. A consumer that wants to bracket a
render-graph pass with two timestamps and read back the elapsed ticks has
nothing to call:

- No managed query-pool type anywhere in `src/Ahjo.Vulkan` — `grep -rn "QueryPool" src/Ahjo.Vulkan --include=*.cs` (excluding `Generated/`) returns **zero** hits.
- `Sync/` holds `Fence.cs`, `BinarySemaphore.cs`, `TimelineSemaphore.cs`, `WaitState.cs`, `Event.cs` — no `VkQueryPool`.
- The only timestamp-adjacent member in the whole wrapper is `QueueFamilyInfo.TimestampValidBits` (`Lifecycle/QueueFamilyInfo.cs:21`), a read-only snapshot with no producer of raw ticks to mask.
- `VkPhysicalDeviceLimits.timestampPeriod` (`Ahjo.Vulkan.Native/Generated/VkPhysicalDeviceLimits.cs:279`) — the ns-per-tick conversion factor — is unreachable after device creation: the full limits struct only surfaces inside the `ref struct` picker callback, and the post-creation accessor `PhysicalDevice.GetMemoryLimits` (`Lifecycle/PhysicalDevice.cs:131-143`) deliberately exposes only the memory subset.

Every native piece already exists in `Ahjo.Vulkan.Native/Generated/Vk.cs`:
`vkCreateQueryPool` (`:136`), `vkDestroyQueryPool` (`:139`),
`vkGetQueryPoolResults` (`:142`), `vkCmdResetQueryPool` (`:217`),
`vkCmdWriteTimestamp2` (`:1282`), plus `VkQueryPoolCreateInfo`
(`Generated/VkQueryPoolCreateInfo.cs:3-20`), `VkQueryType.VK_QUERY_TYPE_TIMESTAMP`
(`Generated/VkQueryType.cs:7`), `VkQueryResultFlagBits`
(`Generated/VkQueryResultFlagBits.cs:5-9`) and
`VkObjectType.VK_OBJECT_TYPE_QUERY_POOL` (`Generated/VkObjectType.cs:17`).
**This is managed-surface work only — no rsp change, no regen.**

The issue is explicit that this is not urgent (Ahjo ships its budget
instrument on wall-clock A/B without it); what it buys is per-pass
localisation once a chain measures over budget. As with #155, that framing
argues for the smallest surface that unblocks the consumer.

## Evidence

### The handle template (#118 / #155)

`Event` (`Sync/Event.cs:35-76`) is the template the issue names, and it fits
exactly: caller-owned `readonly unsafe struct`, `IVulkanHandle<T> + IDisposable`,
`HandleRegistry.TrackCreate(this)` as the last ctor statement (`:46`),
`OwnsHandle => DeviceHandle != null` (`:55`), `Dispose` = null guard →
borrowed guard → `TrackDispose` → `vkDestroy*` (`:66-75`). `Event` carries a
`_flags` payload field on the struct with a documented "borrowed means
*unknown*" caveat (`:29-33`) — precedent for `QueryPool` carrying its
`queryCount`.

The #118 conformance matrix is enumerated by hand so a new handle type forces
a conscious entry: `tests/Ahjo.Vulkan.Tests/HandleConventionsTests.cs:62-86`
(borrow matrix, currently sixteen types) and `:89-115` (owning-side asserts).

Minting convention: every device-created handle is created by a
`Device.CreateX` calling the **static** `Vk.*` P/Invoke — `CreateEvent`
(`Lifecycle/Device.cs:549-559`) is the nearest neighbour. The #155 spec
already litigated and rejected putting create/destroy pairs on the dispatch
table (`docs/design/specs/2026-07-26-issue-155-sync2-split-barriers-design.md`,
"Why not the alternatives").

### The pooling question — answered by #155's own reasoning

There is no `QueryPoolPool` and no slot in `Pools/` for one. A pool's value
in this repo is routing by state (`Pools/FencePool.cs:80-93` routes on
`vkGetFenceStatus`). A query pool's "state" is *N per-query availability
states* that resolve asynchronously and are read back a frame or more later —
a managed pool could not answer "is this one ready?" any more cheaply than
the caller's own `TryGetResults`. The consumer (a pass recorder) owns a fixed
ring of query indices per frame-in-flight; a caller-side array is the whole
pooling story, exactly as with `Event`.

### Result policy — `vkGetQueryPoolResults` is multi-success

`ResultPolicyData.g.cs:90` lists `["vkGetQueryPoolResults"] = ["VK_SUCCESS",
"VK_NOT_READY"]`, so `ResultPolicyGuardTests.NoMultiSuccessCommand_UsesPlainThrowIfFailed`
(`tests/Ahjo.Vulkan.Tests/ResultPolicyGuardTests.cs:52-90`) **fails the build**
if the readback uses `.ThrowIfFailed()`. The sanctioned pattern is
`ThrowIfErrored()` + branch on the returned code
(`Internal/ResultExtensions.cs:81-87`), which is also exactly the
issue's requirement: a non-throwing "not ready yet". `Fence.IsSignaled`
(`Sync/Fence.cs:71-95`) is the in-repo precedent for mapping a
`SUCCESS/NOT_READY` success set to `bool`.

`vkCreateQueryPool` is **not** in the multi-success table → plain
`ThrowIfFailed()` is correct there.

Device loss needs no new plumbing: any error thrown out of
`ThrowIfErrored` funnels through the #120 choke point
(`Internal/ResultExtensions.cs:100-101`), which marks every live device.
`QueryPool`, like `Event`, carries no managed `Owner` reference.

### Dispatch and recording conventions

`CommandRecorder` dispatches every `vkCmd*` through the per-device table
(`Recording/CommandRecorder.cs:53`; charter at
`Internal/DeviceFunctionTable.cs:9-31`): hot-path `vkCmd*` resolve once via
`vkGetDeviceProcAddr`, cold-path calls stay on the static `[DllImport]`s.
Both new commands are core-guaranteed on the wrapper's 1.3 device floor —
`vkCmdResetQueryPool` since 1.0, `vkCmdWriteTimestamp2` since 1.3
(`synchronization2` is force-enabled at `Lifecycle/PhysicalDevice.cs:225`) —
so both resolve with `ResolveRequired` (`Internal/DeviceFunctionTable.cs:352-357`),
no KHR fallback.

The readback (`vkGetQueryPoolResults`) is a device-level, once-per-frame
call, not a per-draw `vkCmd*`: it stays on the static `Vk.*` P/Invoke, the
same tier as `Fence.Wait`'s `vkWaitForFences` (`Sync/Fence.cs:116`), which is
also called per frame.

`ResetEvent` (`Recording/CommandRecorder.cs:729-741`) is the template for a
thin recorder method with an `AhjoValidation`-gated null-handle check;
`Stage` (`Recording/Stage.cs:11-37`) is the existing `VkPipelineStageFlags2`
shadow that `WriteTimestamp` takes, cast `(ulong)stage` exactly as
`ResetEvent` does (`:740`).

### The on-demand limits read

`Device.CreatePipelineLayout` reads `props.limits.maxPushConstantsSize` by
calling `vkGetPhysicalDeviceProperties` into a stack struct on demand
(`Lifecycle/Device.cs:594-596`) — zero allocation, no caching, no new field.
`Device.TimestampPeriod` follows the identical shape. The read is expected
once at consumer setup (the engine caches the float); even a per-frame read
allocates nothing.

### Host-side query reset is out of reach (and out of scope)

`vkResetQueryPool` (host reset, `Generated/Vk.cs:505`) requires the
`hostQueryReset` feature, which the default feature chain does **not** enable
(`Lifecycle/PhysicalDevice.cs:215-226` sets `f12.bufferDeviceAddress`,
`timelineSemaphore`, `separateDepthStencilLayouts`, `f13.synchronization2`,
`dynamicRendering` — no `hostQueryReset`). The issue scopes to command-buffer
reset only; that scope is therefore also the path of least feature-chain
churn. Adding host reset later is additive (enable the feature + one method).

### Vulkan rules, from the pinned registry

Audited against `native/downloaded/Vulkan-Headers-vulkan-sdk-1.4.341.0/registry/validusage.json`
(the tag pinned at `Directory.Build.props:18`):

| Rule | VUID |
|---|---|
| `stage` must include exactly one pipeline stage | `VUID-vkCmdWriteTimestamp2-stage-03859` |
| `stage` must be valid for the command pool's queue family | `VUID-vkCmdWriteTimestamp2-stage-03860` |
| Pool must be `VK_QUERY_TYPE_TIMESTAMP` | `VUID-vkCmdWriteTimestamp2-queryPool-03861` |
| Queue family must report non-zero `timestampValidBits` | `VUID-vkCmdWriteTimestamp2-timestampValidBits-03863` |
| All queries used by a `WriteTimestamp` must be **unavailable** (i.e. reset since last use) | `VUID-vkCmdWriteTimestamp2-None-03864` |
| `query` must be `< queryCount` (plus the view-mask sum rule inside a render pass) | `VUID-vkCmdWriteTimestamp2-query-04903`, `-query-03865` |
| `vkCmdWriteTimestamp2` may be recorded inside **or** outside a render pass; its cmdpool set includes **TRANSFER** | `VUID-vkCmdWriteTimestamp2-commandBuffer-cmdpool` |
| `vkCmdResetQueryPool` must be **outside** a render pass instance | `VUID-vkCmdResetQueryPool-renderpass` |
| `vkCmdResetQueryPool`'s cmdpool set is graphics/compute/optical-flow/video — **not** transfer-only | `VUID-vkCmdResetQueryPool-commandBuffer-cmdpool` |
| Reset range must be in bounds | `VUID-vkCmdResetQueryPool-firstQuery-09436`, `-09437` |
| `vkGetQueryPoolResults` range must be in bounds; queries must not be **uninitialized** (never reset since pool creation) | `VUID-vkGetQueryPoolResults-firstQuery-09436`, `-09437`, `-None-09401` |
| Timestamp pools must **not** use `VK_QUERY_RESULT_PARTIAL_BIT` | `VUID-vkGetQueryPoolResults-queryType-09439` |
| With `64_BIT`, `pData` aligned to 8 and stride a multiple of 8; with `WITH_AVAILABILITY`, stride must also hold the availability integer | `VUID-vkGetQueryPoolResults-flags-00815`, `-queryCount-12252`, `-stride-08993` |
| `dataSize` must be > 0 | `VUID-vkGetQueryPoolResults-dataSize-arraylength` |
| `queryCount` at create must be > 0 | `VUID-VkQueryPoolCreateInfo-queryCount-02763` |
| Destroy only after all submitted commands referencing the pool completed | `VUID-vkDestroyQueryPool-queryPool-00793` |

Consequences that shape the API:

1. **Not-ready is the normal case, not an error.** Without `WAIT`/`PARTIAL`,
   an unavailable query in the range makes the call return `VK_NOT_READY`
   and *skip writing* that query's value (availability is still written when
   `WITH_AVAILABILITY` is set). The readback must return `bool`, and the doc
   contract must state that value slots for unavailable queries are
   undefined on the `false` path.
2. **`PARTIAL` is illegal on timestamp pools** (`-09439`) — the wrapper must
   not expose it, which removes the strongest argument for a public
   result-flags enum.
3. **Reading a never-reset query is a validation error** (`-09401`) — the
   "not ready" contract starts *after* the first submitted
   `ResetQueryPool`, and the tests must model that.
4. **The transfer-queue asymmetry**: timestamps can be *written* on a
   transfer-only queue, but the pool cannot be *reset* there. Documented,
   not enforced — same policy as the #155 `-cmdpool` rules.

### Consumer shape (from the issue)

`Ahjo.Rhi.RhiPassRecorder` brackets each render-graph pass: reset a small
index range once per frame (outside rendering), `WriteTimestamp` at pass
start and end, `TryGetResults` for frame N's range while recording frame
N+k, never blocking. Both recorder calls sit on the per-frame recording path
→ the `Recording/**` zero-allocation contract applies; the readback runs
once per frame → it must also be allocation-free.

## Decision

Ship the minimal timestamp surface: one owning `QueryPool` handle
(timestamp-typed, created from `Device`), two recorder methods, three
readback methods on the handle, one limits property, two dispatch-table
pointers. No managed pool-of-pools, no host reset, no occlusion/statistics
query types, no public result-flags enum.

**1. `QueryPool` — owning handle in `Sync/`** (`src/Ahjo.Vulkan/Sync/QueryPool.cs`):

```csharp
public readonly unsafe struct QueryPool : IVulkanHandle<QueryPool>, IDisposable
{
    public   readonly VkQueryPool_T* Handle;
    internal readonly VkDevice_T*    DeviceHandle;
    private  readonly uint           _queryCount;

    internal QueryPool(VkQueryPool_T* handle, VkDevice_T* device, uint queryCount);
                                                     // last ctor stmt: HandleRegistry.TrackCreate(this)

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_QUERY_POOL;
    public static QueryPool FromRaw(nint handle);    // (…, null, 0)
    public ulong RawHandle  => (ulong)Handle;
    public bool  IsNull     => Handle == null;
    public bool  OwnsHandle => DeviceHandle != null; // #118 contract
    public uint  QueryCount => _queryCount;          // 0 on borrowed handles = *unknown*
    public void  Dispose();                          // guard → TrackDispose → vkDestroyQueryPool

    public bool TryGetResults(uint firstQuery, Span<ulong> results);       // 64_BIT
    public bool TryGetResults(uint firstQuery, Span<QueryResult> results); // 64_BIT | WITH_AVAILABILITY
    public void GetResults   (uint firstQuery, Span<ulong> results);       // 64_BIT | WAIT
}
```

Follows `Event` (`Sync/Event.cs:35-76`) member-for-member, including the
Dispose borrowed-guard comment and the "payload field is *unknown* on a
borrowed handle" doc caveat, here on `QueryCount` (0 = unknown, never "an
empty pool" — an empty pool cannot be created, `-02763`). It lives in
`Sync/` beside `Event`: a timestamp query is a GPU→CPU signal read back
across frames, and the issue itself files the surface under Sync.
Lifetime doc mirrors `Event`: do not dispose while a submission referencing
the pool is pending (`VUID-vkDestroyQueryPool-queryPool-00793`);
`default(QueryPool)` is a legal null handle, `Dispose` a no-op.

The readback methods live on the handle (not on `Device`) because they need
only `(DeviceHandle, Handle)` — the same reason `Fence.IsSignaled` lives on
`Fence`. All three:

- guard `ThrowIfBorrowed()` (verbatim pattern of `Sync/Fence.cs:138-144` —
  a `FromRaw` pool has no device to dispatch through);
- return `true` immediately on an empty `results` span (avoids
  `VUID-vkGetQueryPoolResults-dataSize-arraylength`; an empty read is
  trivially complete);
- call the static `Vk.vkGetQueryPoolResults` under `fixed` over the caller's
  span — `queryCount = (uint)results.Length`, `dataSize = results.Length * elementSize`,
  `stride = elementSize` — then `ThrowIfErrored()` and return
  `result != VkResult.VK_NOT_READY` (the `void` WAIT form just returns; WAIT
  never yields `NOT_READY`);
- allocate nothing on any path (errors throw through the existing cold
  helpers).

Contracts, stated in the XML docs:

- `TryGetResults` **never blocks**. On `false`, value slots for unavailable
  queries are **not written** (undefined); with the `QueryResult` overload,
  availability *is* written for every query, so the caller can see which
  entries are live.
- Results are meaningful only for queries that have been reset (submitted
  `ResetQueryPool`) since pool creation — reading uninitialized queries is a
  validation error (`-09401`), not a `false`.
- `GetResults` (WAIT) **can wait forever** if a query in the range was reset
  but its `WriteTimestamp` never submitted — debug/teardown tier, never the
  per-frame path.
- Raw ticks must be masked to `QueueFamilyInfo.TimestampValidBits` and
  multiplied by `Device.TimestampPeriod` to get nanoseconds.

**2. `QueryResult` — the availability pair** (`src/Ahjo.Vulkan/Sync/QueryResult.cs`):

```csharp
/// 16-byte element for VK_QUERY_RESULT_64_BIT | VK_QUERY_RESULT_WITH_AVAILABILITY_BIT
/// readback: the value, then the availability integer, exactly as the driver writes them.
public readonly struct QueryResult
{
    public readonly ulong Value;        // undefined when !IsAvailable
    public readonly ulong Availability; // non-zero = available
    public bool IsAvailable => Availability != 0;
}
```

Sequential 16-byte layout matches the spec's "availability is written as the
last element of each query's slot" with `stride = 16`; the driver writes
through the fixed pointer, so `readonly` fields are fine. This removes the
interleaving arithmetic (`results[2*i]` / `[2*i+1]`) from every caller.

**3. `Device.CreateQueryPool(uint queryCount)`** (`Lifecycle/Device.cs`,
placed directly after `CreateEvent`, `:549-559`): throws
`ArgumentOutOfRangeException` on `queryCount == 0` (`-02763` — fail at
create with a clear message, the `maxPushConstantsSize` eager-check
philosophy of `:592-602`), builds `VkQueryPoolCreateInfo { sType, queryType =
VK_QUERY_TYPE_TIMESTAMP, queryCount }`, calls the static
`Vk.vkCreateQueryPool(...).ThrowIfFailed()` (single-success — not in the
multi-success table), returns `new QueryPool(raw, Handle, queryCount)`.
Timestamp is the only query type this method mints; the doc says so. A
future occlusion/statistics surface adds an overload — additive, and it will
need its own results semantics anyway.

**4. `Device.TimestampPeriod`** (`Lifecycle/Device.cs`) — `public float`
property: `vkGetPhysicalDeviceProperties(PhysicalDevice.Handle, &props)`
into a stack struct, return `props.limits.timestampPeriod`. On-demand, no
caching, no new field — the `CreatePipelineLayout` limits-read shape
(`:594-596`). Doc: nanoseconds per timestamp tick; read once at setup;
values are often non-integral (e.g. 52.083 on some tile GPUs), hence
`float`, hence "multiply the masked tick delta, don't accumulate ticks as
ns".

**5. Two recorder methods** (`Recording/CommandRecorder.cs`, new
`// ---- Timestamp queries ----` section after the split-barrier block):

```csharp
public void ResetQueryPool(in QueryPool pool, uint firstQuery, uint queryCount)
    => Fns.CmdResetQueryPool(Handle, pool.Handle, firstQuery, queryCount);   // after validation guard

public void WriteTimestamp(in QueryPool pool, Stage stage, uint query)
    => Fns.CmdWriteTimestamp2(Handle, (ulong)stage, pool.Handle, query);     // after validation guard
```

Both are straight-line: no marshalling, no spans, no allocation — thinner
than `ResetEvent` (`:729-741`), which is the template. Under
`AhjoValidation.IsEnabled` (and only then — Release cost is one volatile
bool read and a predicted branch, the #155-established budget):

- both fail on a null pool handle, message naming
  `Device.CreateQueryPool(…)` (the `ResetEvent` message shape, `:736-738`);
- both fail on an out-of-bounds index/range when `pool.QueryCount != 0`
  (`-04903`, `-09436/-09437`); a borrowed pool (`QueryCount == 0`) skips the
  bounds check — unknown is not enforceable;
- `WriteTimestamp` fails unless `BitOperations.PopCount((ulong)stage) == 1`
  (`-03859` — `Stage.None` and multi-bit masks are both invalid; the message
  says "exactly one Stage bit"; meta-bits like `Stage.AllCommands` are single
  flags and pass, as the spec intends).

Documented (XML), not enforced — each traceable to the VUID table above:

- Every query must be reset by a **submitted** `ResetQueryPool` before its
  first `WriteTimestamp` and between reuses (`-03864`); the idiomatic
  per-frame shape is one `ResetQueryPool` over the frame's range at the top
  of the frame's command buffer.
- `ResetQueryPool` outside a `BeginRendering`/`EndRendering` scope
  (`-renderpass`); `WriteTimestamp` is legal inside one.
- `ResetQueryPool` needs a graphics/compute-capable queue; `WriteTimestamp`
  additionally works on transfer-only queues — the asymmetry from the
  Evidence table.
- The queue family must report non-zero `TimestampValidBits` (`-03863`) —
  check `QueueFamilyInfo.TimestampValidBits` in the device picker.
- Bracket idiom: begin with `Stage.TopOfPipe`, end with
  `Stage.BottomOfPipe` (or `Stage.AllCommands`); the timestamp is written
  when all previously submitted commands have completed the named stage.

**6. `DeviceFunctionTable` gains exactly two pointers**
(`Internal/DeviceFunctionTable.cs`), both `ResolveRequired`, UTF-8 literal
names, in a `// ---- Timestamp queries ----` group:

```csharp
delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkQueryPool_T*, uint, uint, void> CmdResetQueryPool;   // "vkCmdResetQueryPool"u8
delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, ulong, VkQueryPool_T*, uint, void> CmdWriteTimestamp2; // "vkCmdWriteTimestamp2"u8
```

`vkCreateQueryPool`/`vkDestroyQueryPool`/`vkGetQueryPoolResults` stay on the
static `Vk.*` P/Invokes per the table's charter (`:9-31`) and the
Fence-readback precedent.

### Why not the alternatives

- **A managed `QueryPoolPool` / per-frame query allocator** — pooling in
  this repo earns its keep by routing on state (`FencePool.cs:80-93`);
  query availability resolves asynchronously frames later, so the pool
  could answer nothing the caller's own `TryGetResults` doesn't. Same
  verdict as #155's `EventPool` rejection, and the consumer (a pass
  recorder with a fixed per-frame index ring) needs none.
- **`Device.CreateTimestampQueryPool` naming** — the repo names creators
  after the Vulkan noun (`CreateEvent`, `CreateSampler`, `CreateQueryPool`
  matches `vkCreateQueryPool`); a future non-timestamp pool is an additive
  overload taking a type, not a second noun.
- **A `queryType` parameter now** — no consumer for occlusion or pipeline
  statistics exists anywhere (issue, engine, samples); statistics pools
  drag in `pipelineStatistics` flags and a feature bit
  (`VUID-VkQueryPoolCreateInfo-queryType-00791`), and each type has its own
  results semantics. Speculative surface, deferred until a consumer states
  its shape.
- **A public `QueryResultFlags` shadow enum on one `GetResults`** — of the
  four core flags, `64_BIT` is mandatory here (the wrapper is 64-bit-only
  by design), `PARTIAL` is *illegal* on timestamp pools (`-09439`), and
  `WITH_AVAILABILITY` changes the element layout (8 → 16 bytes), which a
  flags argument would encode as a runtime span-length contract instead of
  a compile-time element type. Three crisp methods beat one flag-driven
  one; `Fence` splits `IsSignaled`/`Wait` the same way.
- **Interleaving availability into `Span<ulong>` (even/odd)** — pushes
  `2*i`/`2*i+1` index arithmetic and a length-doubling rule onto every
  caller; a 16-byte blittable `QueryResult` costs nothing and makes the
  layout self-documenting.
- **Throwing on `VK_NOT_READY`** — forbidden twice over: the consumer reads
  every frame and "not ready" is its steady state for in-flight queries,
  and `ResultPolicyGuardTests` fails the build on `ThrowIfFailed` against a
  multi-success command (`ResultPolicyData.g.cs:90`).
- **A `WaitState`-style enum return for `TryGetResults`** —
  `WaitStateExtensions.ToWaitState` deliberately excludes `NOT_READY`
  (`Sync/WaitState.cs:24-28`); the readback's success set is
  `SUCCESS/NOT_READY`, which is a `bool`, and device loss already throws
  through the #120 choke point like every other wrapper call.
- **Dropping the blocking WAIT form** — the issue asks for WAIT to be
  expressible; it is genuinely useful at teardown/debug tier, and it costs
  one thin overload. Its hang risk (waiting on a never-submitted query) is
  documented instead of guarded — the wrapper cannot see submission state.
- **`vkGetQueryPoolResults` on `DeviceFunctionTable`** — the table's
  charter is per-draw `vkCmd*` hot paths (`:9-31`); a once-per-frame
  device-level call belongs with `vkWaitForFences` on the static imports.
- **Caching `TimestampPeriod` in a `Device` field** — no precedent (the
  repo reads limits on demand: `Device.cs:594-596`,
  `PhysicalDevice.cs:131-143`), and the on-demand read is already
  zero-alloc; a field would be the first cached limit and invites more.
- **Host reset (`vkResetQueryPool`) now** — requires the `hostQueryReset`
  feature the default chain doesn't enable
  (`PhysicalDevice.cs:215-226`), and the consumer resets in-band anyway.
  Additive later: feature-chain entry + one method on `QueryPool`.
- **Enforcing `timestampValidBits != 0` / queue-family stage validity in
  the recorder** — the recorder doesn't know its pool's queue family
  properties, and threading them through `CommandBufferPool` adds state to
  every recording path to catch VUIDs (`-03860`, `-03863`) the validation
  layer already reports. Documented instead — the #155 policy for
  render-pass-scope rules.
- **Auto-masking results with `TimestampValidBits` inside `TryGetResults`** —
  the pool doesn't know which queue family wrote each timestamp (a pool may
  legally be written from different queues), so the wrapper would have to
  guess; the issue keeps masking with the caller, where
  `QueueFamilyInfo.TimestampValidBits` already lives.

## Invariants honored

- **Zero per-frame allocations.** `ResetQueryPool`/`WriteTimestamp` are
  straight-line dispatch-table calls; `TryGetResults` is `fixed` over the
  caller's span + one static P/Invoke; validation guards are branch-only
  when disabled. `CreateQueryPool` is setup-time. Proven by new
  `[MemoryDiagnoser]` benchmarks, not asserted.
- **AOT-clean.** Two more `delegate* unmanaged` pointers; `QueryResult` is a
  plain blittable struct; no reflection anywhere.
- **UTF-8 literals.** Both resolve names use
  `Utf8Name.FromLiteral("vkCmd…"u8)`, matching the table.
- **Generated code untouched.** Everything native already exists
  (`Generated/Vk.cs:136-142, 217, 1282`); no rsp change, no regen.
- **`TreatWarningsAsErrors`.** No suppressions; `ResultPolicyGuardTests`
  stays green because the multi-success command uses `ThrowIfErrored`.

## Test strategy (constrained by #32)

Driver-independent (runs in CI everywhere):

- `QueryPool` joins the #118 matrices in `HandleConventionsTests`
  (sixteen → seventeen types): borrow contract, owning-side assert,
  `ObjectType == VK_OBJECT_TYPE_QUERY_POOL`, `FromRaw(...).QueryCount == 0`.

Driver-gated (`tests/Ahjo.Vulkan.Tests/TimestampQueryTests.cs`, beside
`SplitBarrierTests.cs` and reusing its helper shapes — `TestGate` gates,
validated-instance error capture, `CreateGraphicsDevice`):

- Create/ownership/dispose; `CreateQueryPool(0)` throws
  `ArgumentOutOfRangeException`.
- **Execution oracle:** reset 2 → `WriteTimestamp(TopOfPipe, 0)` →
  `FillBuffer` work → `WriteTimestamp(BottomOfPipe, 1)` → submit → fence
  wait → `TryGetResults` returns `true`; masked `t1 >= t0`; delta ×
  `TimestampPeriod` is finite and non-negative; skips (via `TestGate`) on a
  family with `TimestampValidBits == 0`. Run under the validation layer
  with the no-errors assert (the `SplitBarrierTests` oracle pattern).
- **Not-ready path:** submit a reset-*only* command buffer, fence wait,
  `TryGetResults` returns `false` without throwing (queries are initialized
  but unavailable — the `-09401`-safe construction).
- **Availability overload:** reset 2, write only query 0, submit, wait →
  `Span<QueryResult>` read returns `false`, `[0].IsAvailable` true with a
  non-zero value, `[1].IsAvailable` false.
- **WAIT overload:** after a fully submitted pair, `GetResults` returns both
  values (never records an unsubmitted-wait hang scenario).
- **`AhjoValidation`:** null pool on both recorder methods; multi-bit and
  zero `Stage` on `WriteTimestamp`; out-of-range `query` against a known
  `QueryCount`.

If the validation layer is the oracle, the run is captured as
`AHJO_VULKAN_TIER=validation` per `tests/CLAUDE.md`. No Linux lanes touched.

## Benchmarks

`ResetQueryPool`/`WriteTimestamp` are `Recording/**` per-frame calls and
`TryGetResults` runs once per frame: new
`tests/Ahjo.Vulkan.Benchmarks/TimestampQueryBenchmarks.cs` on the
`PipelineBarrierBenchmarks` chassis (`:86-126` — record-only, never
submitted, `OperationsPerInvoke` loop, `ResetForFrame` per invoke) with a
reset+write-pair benchmark and a `TryGetResults` (unavailable-path)
benchmark. `docs/benchmarks.md` gains the rows; all `Allocated` cells must
read `-`. No existing hot path is re-routed, so no recapture of existing
rows is required — this is purely additive, unlike #155's step 5.

## Uncertainty, stated

- VUID numbers are read from the pinned `validusage.json`
  (`vulkan-sdk-1.4.341.0`); doc comments quote the requirement text and cite
  the number, per the #155 renumbering caveat.
- The claim that the consumer's readback cadence is "a frame or more later,
  never blocking" comes from the issue's description of
  `RhiPassRecorder`/ADR-0023 — there is no in-repo consumer to audit. If the
  engine turns out to want same-frame results, the WAIT overload already
  covers it without redesign.
- `timestampPeriod` precision: the property returns the driver's `float`
  verbatim; whether the engine accumulates in double is the engine's
  decision and out of scope here.
