Paired with [../specs/2026-08-16-issue-198-timestamp-query-pool-design.md](../specs/2026-08-16-issue-198-timestamp-query-pool-design.md) — read it first; this plan only says *how*.

# Implementation plan — issue #198: timestamp query-pool surface

Managed-surface work only: no rsp change, no regen, nothing under
`Generated/` moves. All native entry points and structs already exist
(`src/Ahjo.Vulkan.Native/Generated/Vk.cs:136, 139, 142, 217, 1282`).

## Step 1 — `QueryResult` struct

New file `src/Ahjo.Vulkan/Sync/QueryResult.cs`:

```csharp
namespace Ahjo.Vulkan;

public readonly struct QueryResult
{
    public readonly ulong Value;        // undefined when !IsAvailable
    public readonly ulong Availability; // non-zero = available
    public bool IsAvailable => Availability != 0;
}
```

- Field order is load-bearing: `Value` first, `Availability` second, 16
  bytes total — the exact layout `vkGetQueryPoolResults` writes with
  `VK_QUERY_RESULT_64_BIT | VK_QUERY_RESULT_WITH_AVAILABILITY_BIT` and
  `stride = 16` (availability is the last element of each query's slot).
  Say so in the XML doc. No explicit `[StructLayout]` needed (two `ulong`
  fields cannot be reordered or padded), but adding
  `LayoutKind.Sequential` explicitly is acceptable if the implementer
  prefers the layout stated in code.
- XML docs: value meaningful only when `IsAvailable`; produced by the
  `TryGetResults(uint, Span<QueryResult>)` overload.

## Step 2 — `QueryPool` handle + readback

New file `src/Ahjo.Vulkan/Sync/QueryPool.cs`. Template is
`src/Ahjo.Vulkan/Sync/Event.cs:35-76`, member for member:

```csharp
public readonly unsafe struct QueryPool : IVulkanHandle<QueryPool>, IDisposable
{
    public   readonly VkQueryPool_T* Handle;
    internal readonly VkDevice_T*    DeviceHandle;
    private  readonly uint           _queryCount;

    internal QueryPool(VkQueryPool_T* handle, VkDevice_T* device, uint queryCount)
    {
        Handle       = handle;
        DeviceHandle = device;
        _queryCount  = queryCount;
        HandleRegistry.TrackCreate(this);   // last ctor statement, as in Event.cs:46
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_QUERY_POOL;
    public static QueryPool FromRaw(nint handle) => new((VkQueryPool_T*)handle, null, 0);
    public ulong RawHandle  => (ulong)Handle;
    public bool  IsNull     => Handle == null;
    public bool  OwnsHandle => DeviceHandle != null;
    public uint  QueryCount => _queryCount;

    public void Dispose()
    {
        if (Handle == null) return;
        if (!OwnsHandle) return;            // borrowed — caller owns the lifetime (Event.cs:69-72 comment)
        HandleRegistry.TrackDispose(this);
        Vk.vkDestroyQueryPool(DeviceHandle, Handle, null);
    }
}
```

Readback methods on the same struct:

```csharp
public bool TryGetResults(uint firstQuery, Span<ulong> results);       // 64_BIT
public bool TryGetResults(uint firstQuery, Span<QueryResult> results); // 64_BIT | WITH_AVAILABILITY
public void GetResults   (uint firstQuery, Span<ulong> results);       // 64_BIT | WAIT
```

Shared shape (each method, no shared helper needed — three short bodies):

1. `if (results.IsEmpty) return true;` (`return;` for the WAIT form) —
   avoids `VUID-vkGetQueryPoolResults-dataSize-arraylength`.
2. `ThrowIfBorrowed();` — private helper copied from the `Fence` pattern
   (`Sync/Fence.cs:138-144`), message: `"QueryPool requires an owning device
   for result readback; a FromRaw-constructed (borrowed) pool has none."`
3. `fixed` over the span, call the **static**
   `Vk.vkGetQueryPoolResults(DeviceHandle, Handle, firstQuery,
   (uint)results.Length, dataSize, p, stride, flags)` where:
   - `Span<ulong>` forms: `dataSize = (nuint)results.Length * 8`, `stride = 8`.
   - `Span<QueryResult>` form: `dataSize = (nuint)results.Length * 16`, `stride = 16`.
   - `flags`: `(uint)VkQueryResultFlagBits.VK_QUERY_RESULT_64_BIT`, OR'd
     with `VK_QUERY_RESULT_WITH_AVAILABILITY_BIT` / `VK_QUERY_RESULT_WAIT_BIT`
     per overload.
4. `result.ThrowIfErrored();` — **not** `ThrowIfFailed` (multi-success
   command, `ResultPolicyData.g.cs:90`; the guard test
   `ResultPolicyGuardTests` fails the build otherwise).
5. `return result != VkResult.VK_NOT_READY;` (WAIT form: plain return).

Optional `AhjoValidation`-gated bounds check at the top of each (after the
empty-span return): when `_queryCount != 0` and
`firstQuery + (uint)results.Length > _queryCount`, fail with a message
naming the range and the pool's `QueryCount`
(`VUID-vkGetQueryPoolResults-firstQuery-09436/-09437`).

XML docs must carry (see spec's Decision §1 for full text):

- `TryGetResults` never blocks; on `false`, value slots for unavailable
  queries are unwritten/undefined; the `QueryResult` overload still writes
  availability for every query.
- Queries must have been reset by a **submitted** `ResetQueryPool` since
  pool creation before any readback (`VUID-vkGetQueryPoolResults-None-09401`).
- `GetResults` (WAIT) can block forever on a reset-but-never-written query —
  debug/teardown tier.
- Ticks → ns: mask to `QueueFamilyInfo.TimestampValidBits`, multiply by
  `Device.TimestampPeriod`.
- Lifetime remarks block mirroring `Event.cs:24-28` (no dispose while a
  referencing submission is pending, `VUID-vkDestroyQueryPool-queryPool-00793`;
  `default` legal; double-dispose UB) and the borrowed-`QueryCount`-means-
  unknown caveat mirroring `Event.cs:29-33`.

## Step 3 — `Device.CreateQueryPool` + `Device.TimestampPeriod`

File `src/Ahjo.Vulkan/Lifecycle/Device.cs`, both placed directly after
`CreateEvent` (`:549-559`):

```csharp
public QueryPool CreateQueryPool(uint queryCount)
{
    if (queryCount == 0)
        throw new ArgumentOutOfRangeException(nameof(queryCount),
            "A query pool must contain at least one query (VUID-VkQueryPoolCreateInfo-queryCount-02763).");

    var ci = new VkQueryPoolCreateInfo
    {
        sType      = VkStructureType.VK_STRUCTURE_TYPE_QUERY_POOL_CREATE_INFO,
        queryType  = VkQueryType.VK_QUERY_TYPE_TIMESTAMP,
        queryCount = queryCount,
    };
    VkQueryPool_T* raw = null;
    Vk.vkCreateQueryPool(Handle, &ci, null, &raw).ThrowIfFailed();   // single-success — plain ThrowIfFailed is correct
    return new QueryPool(raw, Handle, queryCount);
}

public float TimestampPeriod
{
    get
    {
        VkPhysicalDeviceProperties props;
        Vk.vkGetPhysicalDeviceProperties(PhysicalDevice.Handle, &props);
        return props.limits.timestampPeriod;
    }
}
```

XML docs:

- `CreateQueryPool`: timestamp-typed only (`VK_QUERY_TYPE_TIMESTAMP`);
  caller-owned, dispose after no submission references it; queries start
  **uninitialized** — record + submit `CommandRecorder.ResetQueryPool`
  before first use; point at `CommandRecorder.WriteTimestamp`,
  `QueryPool.TryGetResults`, `TimestampPeriod`.
- `TimestampPeriod`: nanoseconds per timestamp tick
  (`VkPhysicalDeviceLimits::timestampPeriod`); read on demand from the
  physical device (zero-alloc, same shape as the `maxPushConstantsSize`
  read at `:594-596`); typically read once at setup; often non-integral.

## Step 4 — dispatch-table entries

File `src/Ahjo.Vulkan/Internal/DeviceFunctionTable.cs`. Add a
`// ---- Timestamp queries ----` group after the split-barrier trio
(fields after `:119`, resolves after `:287`):

```csharp
public readonly delegate* unmanaged[Stdcall]<
    VkCommandBuffer_T*, VkQueryPool_T*, uint, uint, void> CmdResetQueryPool;

public readonly delegate* unmanaged[Stdcall]<
    VkCommandBuffer_T*, ulong, VkQueryPool_T*, uint, void> CmdWriteTimestamp2;
```

Resolved in the ctor with `ResolveRequired` (no KHR fallback — comment that
`vkCmdResetQueryPool` is core 1.0 and `vkCmdWriteTimestamp2` core 1.3, the
wrapper's device floor):

```csharp
CmdResetQueryPool  = (…)ResolveRequired(Utf8Name.FromLiteral("vkCmdResetQueryPool"u8));
CmdWriteTimestamp2 = (…)ResolveRequired(Utf8Name.FromLiteral("vkCmdWriteTimestamp2"u8));
```

## Step 5 — recorder methods

File `src/Ahjo.Vulkan/Recording/CommandRecorder.cs`. New section
`// ---- Timestamp queries ----` after the split-barrier block (i.e. after
`AssertSplitBarrierUsable`, `:830`), before the copy section. `using
System.Numerics;` if not present (for `BitOperations`).

```csharp
public void ResetQueryPool(in QueryPool pool, uint firstQuery, uint queryCount)
{
    if (AhjoValidation.IsEnabled)
    {
        if (pool.IsNull)
            AhjoValidation.Fail("CommandRecorder",
                "ResetQueryPool: query pool is a null handle. Create one with Device.CreateQueryPool(count).");
        if (pool.QueryCount != 0 && firstQuery + queryCount > pool.QueryCount)
            AhjoValidation.Fail("CommandRecorder",
                $"ResetQueryPool: range [{firstQuery}, {firstQuery + queryCount}) exceeds the pool's "
                + $"queryCount ({pool.QueryCount}).");
    }
    Fns.CmdResetQueryPool(Handle, pool.Handle, firstQuery, queryCount);
}

public void WriteTimestamp(in QueryPool pool, Stage stage, uint query)
{
    if (AhjoValidation.IsEnabled)
    {
        if (pool.IsNull)
            AhjoValidation.Fail("CommandRecorder",
                "WriteTimestamp: query pool is a null handle. Create one with Device.CreateQueryPool(count).");
        if (System.Numerics.BitOperations.PopCount((ulong)stage) != 1)
            AhjoValidation.Fail("CommandRecorder",
                "WriteTimestamp: stage must be exactly one Stage bit "
                + "(VUID-vkCmdWriteTimestamp2-stage-03859); Stage.None and multi-bit masks are invalid.");
        if (pool.QueryCount != 0 && query >= pool.QueryCount)
            AhjoValidation.Fail("CommandRecorder",
                $"WriteTimestamp: query {query} is out of range for the pool's queryCount ({pool.QueryCount}).");
    }
    Fns.CmdWriteTimestamp2(Handle, (ulong)stage, pool.Handle, query);
}
```

Exact message text may be tuned, but must keep the `"Device.CreateQueryPool"`
fix-naming shape (matching `ResetEvent`'s message at `:736-738`) because a
test asserts on it (Step 6). A borrowed pool (`QueryCount == 0`) skips the
bounds checks — unknown is not enforceable.

XML docs (requirement text + VUID number, per the spec's documented-rules
list): reset-before-use (`-None-03864`) with the once-per-frame reset idiom;
`ResetQueryPool` outside a rendering scope (`-renderpass`) and not on
transfer-only queues (`-commandBuffer-cmdpool`); `WriteTimestamp` legal
inside rendering and on transfer queues; non-zero `TimestampValidBits`
required (`-03863`); bracket idiom `Stage.TopOfPipe` / `Stage.BottomOfPipe`
(or `Stage.AllCommands`).

## Step 6 — tests

**`tests/Ahjo.Vulkan.Tests/HandleConventionsTests.cs`:**

- `:62` comment: "sixteen" → "seventeen".
- `:70-85` borrow matrix: add `AssertBorrowContract<QueryPool>();`.
- `:89-115` owning asserts: add
  `Assert.True(new QueryPool((VkQueryPool_T*)0x2000, device, queryCount: 4).OwnsHandle);`
  in the owning group (it is caller-owned like `Event`, not pool-owned).
- New facts mirroring the `Event` pair at `:117-129`:
  `QueryPool_ObjectType_IsQueryPool`
  (`Assert.Equal(VkObjectType.VK_OBJECT_TYPE_QUERY_POOL, QueryPool.ObjectType)`)
  and `QueryPool_FromRaw_ReportsQueryCountUnknown`
  (`Assert.Equal(0u, QueryPool.FromRaw(0x1234_5678).QueryCount)` with the
  "0 means unknown, not empty" comment).

**New `tests/Ahjo.Vulkan.Tests/TimestampQueryTests.cs`** — model the file on
`SplitBarrierTests.cs` (same gates, same `CreateValidatedInstance` /
`AssertNoValidationErrors` / `CreateGraphicsDevice` helper shapes; reuse of
its private helpers is by copy, they are file-local). Cases:

1. `CreateQueryPool_IsOwningAndDisposes` — `TestGate.RequireDriver()`;
   create count 4; assert `!IsNull`, `OwnsHandle`, `QueryCount == 4`;
   dispose via `using`.
2. `CreateQueryPool_ZeroCount_Throws` — `TestGate.RequireDriver()`;
   `Assert.Throws<ArgumentOutOfRangeException>(() => device.CreateQueryPool(0))`.
3. `WriteTimestampPair_MeasuresElapsedTicks` — gate exactly like
   `SplitBarrierTests.SkipUnlessValidatedSubmitPossible` (driver + hardware
   + validation layer); additionally `TestGate`-skip if the chosen family's
   `QueueFamilyInfo.TimestampValidBits == 0` (capture it in the picker
   callback next to `family`). Record one command buffer:
   `ResetQueryPool(pool, 0, 2)` → `WriteTimestamp(pool, Stage.TopOfPipe, 0)`
   → `FillBuffer` on a device buffer → `WriteTimestamp(pool,
   Stage.BottomOfPipe, 1)` → submit with fence → `fence.Wait`. Then:
   `TryGetResults(0, span2)` returns `true`; mask both ticks with
   `TimestampValidBits` (`bits >= 64 ? ulong.MaxValue : (1UL << bits) - 1`);
   assert `masked1 >= masked0`; assert `device.TimestampPeriod > 0`;
   assert no validation errors.
4. `TryGetResults_BeforeWrite_ReturnsFalseWithoutThrowing` — same gating as
   3 minus the validation layer (driver + hardware is enough). Submit a
   command buffer containing **only** `ResetQueryPool(pool, 0, 2)` (this is
   what keeps the read `-09401`-legal), fence-wait, then assert
   `pool.TryGetResults(0, span2) == false` and the call did not throw.
5. `TryGetResults_WithAvailability_ReportsPerQueryState` — gating as 4.
   Submit reset(0,2) + `WriteTimestamp(query: 0)` only; fence-wait; read
   `Span<QueryResult>` length 2: overall `false`, `[0].IsAvailable` true
   with `Value != 0` after masking, `[1].IsAvailable` false.
6. `GetResults_Wait_ReturnsBothValues` — gating as 4; after the full pair
   from case 3 is submitted and fenced, `GetResults(0, span2)` returns with
   two values (WAIT overload smoke — never constructed against an
   unsubmitted query).
7. `AhjoValidation` trio, modeled on
   `SplitBarrierTests.ResetEvent_NullEvent_FailsUnderValidation`
   (`:246-273`, including the save/restore of `AhjoValidation.Enabled`):
   - null pool on `WriteTimestamp` and on `ResetQueryPool` → message
     contains `"Device.CreateQueryPool"`;
   - `WriteTimestamp(pool, Stage.None, 0)` and
     `WriteTimestamp(pool, Stage.TopOfPipe | Stage.BottomOfPipe, 0)` →
     message contains `"03859"` or `"exactly one Stage bit"`;
   - `WriteTimestamp(pool, Stage.TopOfPipe, query: 4)` on a
     `QueryCount == 4` pool → message contains `"out of range"`.
8. `TryGetResults_EmptySpan_IsTrueNoOp` — driverless is impossible (needs a
   pool), so under `TestGate.RequireDriver`: empty span returns `true`
   without touching the driver-visible range.
9. Borrowed-pool guard: `QueryPool.FromRaw(0x1234).TryGetResults(...)`
   throws `InvalidOperationException` naming the borrowed-handle cause
   (driverless — no device needed; the guard fires before any Vulkan call).

Run evidence: cases 3-7 use the validation layer / real driver — capture a
local `AHJO_VULKAN_TIER=validation dotnet test tests/Ahjo.Vulkan.Tests` run
and quote the tier-contract line, per `tests/CLAUDE.md`.

## Step 7 — benchmarks

New `tests/Ahjo.Vulkan.Benchmarks/TimestampQueryBenchmarks.cs`, chassis
copied from `PipelineBarrierBenchmarks.cs` (`[MemoryDiagnoser]`, instance +
graphics device + `CommandBufferPool` in `GlobalSetup`, warm-up
begin/end/reset, `OperationsPerInvoke` loop, `ResetForFrame` per invoke,
dispose in `GlobalCleanup`). Setup also creates `_pool =
_device.CreateQueryPool(64)` and — for the readback benchmark — submits one
reset-only command buffer over the full range and fence-waits
(`Queue.ImmediateSubmit` is fine), so `TryGetResults` polls initialized,
unavailable queries.

Benchmarks (all record-only / never-submitted except the setup reset;
comment that, as `PipelineBarrierBenchmarks:134-138` does):

1. `ResetAndWritePair` — per op: `rec.ResetQueryPool(_pool, 0, 2)`;
   `rec.WriteTimestamp(in _pool, Stage.TopOfPipe, 0)`;
   `rec.WriteTimestamp(in _pool, Stage.BottomOfPipe, 1)`.
2. `TryGetResults_NotReady` — per op: `_pool.TryGetResults(0, _span2)` on a
   pre-allocated `ulong[2]` field's span; returns `false` every time; the
   point is the 0 B/op cell.

`docs/benchmarks.md`: add the two rows to the appropriate table with
`Allocated = -`; note they are driver-bound. No existing rows need
recapture (nothing existing was re-routed).

**OPEN:** the actual baseline numbers must be captured on the maintainer's
Windows host (`/run-bench`, `-c Release`, filter `*TimestampQuery*`) — the
implementer fills in the Mean cells from that run, and any non-`-`
Allocated cell is a stop-and-report condition.

## Step 8 — build + verification sweep

1. `dotnet build Ahjo.Vulkan.slnx` — zero warnings
   (`TreatWarningsAsErrors`).
2. `dotnet test` — driverless portion everywhere;
   `AHJO_VULKAN_TIER=validation` run locally for the driver-gated portion
   (Step 6 evidence).
3. `ResultPolicyGuardTests` must stay green — it will scan the new
   `vkGetQueryPoolResults` call sites and fail if any uses `ThrowIfFailed`.
4. AOT smoke is unaffected (no reflection, no new packages), but the
   standard pre-PR `dotnet publish samples/AotSmoke … -p:PublishAot=true`
   check applies if run locally.
5. Reviewer agents per `src/Ahjo.Vulkan/CLAUDE.md`:
   `vulkan-validation-reviewer` (diff touches `Recording/` + `Sync/`) and
   `bench-coverage-checker` (new hot-path methods) before the PR.

## Explicitly out of scope (do not add while implementing)

- Any `queryType` parameter, occlusion/pipeline-statistics support, or a
  public result-flags enum.
- Host reset (`vkResetQueryPool`) and the `hostQueryReset` feature-chain
  entry.
- A managed pool/allocator around `QueryPool`, or `Owner`/`Device`
  references on the struct.
- Masking or period-multiplication inside `TryGetResults` — ticks out,
  caller converts.
- Entries on `DeviceFunctionTable` for create/destroy/get-results.

## Release note (for the caller, not a plan step)

Ahjo pins 0.11.0 and wants this in the next tag; `main` already carries
#190/#194/#195 past `v0.11.0`, so the next `v0.x.y` tag ships them together.
Tagging is a human release action outside this plan.
