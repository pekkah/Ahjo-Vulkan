# Explicit handle ownership and the `IVulkanHandle` `unmanaged` constraint

**Issue:** [#118](https://github.com/pekkah/Ahjo-Vulkan/issues/118) — *Design: explicit handle ownership (borrowed vs owning) and the IVulkanHandle unmanaged constraint*
**Resolves the structural cause of:** [#106](https://github.com/pekkah/Ahjo-Vulkan/issues/106) (null-owner guards landed in d70f31e; this spec prevents the recurrence)
**Lands consistently with:** [#102](https://github.com/pekkah/Ahjo-Vulkan/issues/102) §2 (FromRaw AV hardening on device-bound sync calls)
**Date:** 2026-06-12

## Problem

Two related defects in the handle contract, both downstream of the same
interface declaration (`IVulkanHandle.cs:34-35`):

1. **Ownership is a field-value convention, not a type-level fact.** A handle
   owns its Vulkan object iff "the device/allocator field happens to be
   non-null", and `FromRaw` produces the borrowed flavor by leaving that field
   null. Nothing forces a handle type to honor the convention — #106 found
   seven of eleven `IDisposable` handle types dispatching
   `vkDestroy*`/`vmaDestroy*` through a null device/allocator because each type
   has to *rediscover* the guard. The guards (and the
   `FromRawHandles_Dispose_IsNoOp` test matrix) landed in d70f31e, but the next
   handle type added to the wrapper starts the cycle again.

2. **The `unmanaged` constraint forces device-scoped metadata into
   process-global side tables.** `PipelineLayout` cannot carry its declared
   push-constant ranges and set-layout handles as fields (managed arrays are
   forbidden by `unmanaged`), so they live in two `static Dictionary`s under a
   global lock (`PipelineLayout.cs:38-71`). Those tables are keyed by raw
   pointer values — exposed to driver handle reuse the moment any dispose path
   skips `UnregisterMetadata` — and hold device-scoped data in process-wide
   state.

## Evidence: what the `unmanaged` constraint actually buys

A full audit of `src/` (none of this holds for `tests/`, which only exercise
the contract):

- **Exactly one generic consumer.** The only `where T : unmanaged,
  IVulkanHandle<T>` in `src/` outside the interface itself is
  `ObjectName.Set<T>` (`Diagnostics/DebugMarker.cs:57`), and it reads only
  `T.ObjectType` + `handle.RawHandle` + `handle.IsNull`. The `unmanaged` half
  of the constraint is incidental there.
- **No hot path relies on handle structs being unmanaged.**
  `CommandRecorder.BindDescriptorSets` / `BindVertexBuffers` unwrap wrapper
  spans into `stackalloc nint[]` at method entry
  (`CommandRecorder.cs:225-237, 346-367`); `PipelineCache.Merge` does the same
  (`PipelineCache.cs:121-132`). Pools store raw `nint`s and wrap on handout
  (`FencePool.cs:28-29`, `DescriptorSetPool.cs:68-69`). There is no
  `stackalloc`, `fixed`, or `MemoryMarshal.Cast` over a wrapper struct
  anywhere in `src/`.
- **The metadata consumers are debug-only.** The side tables are read by
  `[Conditional("DEBUG")]` assertions (`CommandRecorder.cs:240-296`) — Release
  hot paths never touch the metadata at all.
- **Recording signatures already pass `PipelineLayout` by readonly reference**
  (`in PipelineLayout`, `CommandRecorder.cs:213, 275, 383, 409`), so a larger
  struct does not change the call ABI on the paths that matter.

And on what `FromRaw` is actually for:

- **Zero call sites in `src/` and `samples/`.** Every in-repo caller is a
  test.
- **But it is sanctioned consumer API, and borrowed handles flow into wrapper
  APIs that take the owning type.** The Vortice migration guide documents
  `Surface.FromRaw` as the borrowing path (vs `Surface.WrapExternal` for
  ownership transfer) — `docs/migration-vortice-to-ahjo.md` §8. Spec #119
  deliberately made `Image.FromRaw` stamp `MipLevels/ArrayLayers/Depth = 1/1/1`
  *so that borrowed images work with the barrier/copy factories* that read
  those fields. `CommandRecorder`'s debug assertions explicitly no-op on
  layouts/sets "constructed via FromRaw" to keep those flows usable
  (`CommandRecorder.cs:244, 251-254`).

## Decision: option 3 — relax `unmanaged` to `struct`, metadata rides the handle, ownership becomes an interface member

### Why not the alternatives

- **Option 1 (guards only)** already landed with #106 (d70f31e) and is
  necessary but not sufficient: it fixes the eleven types that exist today and
  does nothing for the twelfth, and it leaves the side tables, their lock, and
  the pointer-reuse hazard in place. (The issue's suggested "debug assert that
  disposing a borrowed handle is a caller bug" is **rejected**: no-op `Dispose`
  on a borrowed handle is the *documented contract* — `SurfaceTests.
  FromRaw_DoesNotOwn_DisposeIsNoOp` and the migration guide both rely on
  `using` working uniformly over borrowed and owning handles.)
- **Option 2 (`Borrowed<T>` struct)** rests on the premise that FromRaw
  consumers "need only RawHandle + ObjectType". The audit contradicts that:
  borrowed images carry subresource metadata consumed by barrier factories
  (#119), borrowed surfaces flow into surface-inspection APIs, and borrowed
  layouts/sets flow into recorder bind/push paths. A `Borrowed<T>` exposing
  only the raw handle would need duplicate overloads across `CommandRecorder`,
  the barrier/copy factories, and `Device`/`Swapchain` creation — a large API
  bifurcation purchased for zero in-repo callers. Rejected.
- **Option 4 (per-`Device` side tables)** keeps the lock and the
  pointer-keyed tables and merely rescopes the handle-reuse hazard from
  process to device (drivers reuse handle values *within* a device). It is
  strictly weaker than option 3 once option 3 is shown to be safe — and the
  audit above shows it is.

### Shape of the change

**1. The interface** (`Internal/IVulkanHandle.cs`):

```csharp
public interface IVulkanHandle<TSelf>
    where TSelf : struct, IVulkanHandle<TSelf>   // was: unmanaged
{
    static abstract VkObjectType ObjectType { get; }
    static abstract TSelf FromRaw(nint handle);

    ulong RawHandle { get; }
    bool IsNull { get; }
    bool OwnsHandle { get; }                     // new
}
```

- `struct` (not unconstrained) keeps copy-by-value semantics and
  `default(T)`-is-a-legal-null-handle, which the whole wrapper assumes.
- `OwnsHandle` makes ownership a **compile-enforced part of the contract**:
  every current and future handle type must answer "does Dispose destroy?",
  and the test matrix can assert the borrow contract generically instead of
  per type. This is the structural fix for #106's recurrence — forgetting the
  guard is no longer possible silently, because the property the guard reads
  is demanded by the compiler and locked down by a generic test.
- `FromRaw` stays (see evidence above). Its doc comment gains the explicit
  postcondition: *`FromRaw` produces a borrowed handle —
  `OwnsHandle == false`, `Dispose` is a no-op, device-bound members throw.*

**2. Dispose guards read the contract member.** Every `IDisposable` handle
type's guard becomes the uniform

```csharp
public void Dispose()
{
    if (!OwnsHandle) return;   // borrowed (FromRaw) or default — caller owns the lifetime
    ...destroy...
}
```

with `OwnsHandle` implemented per type as the existing null-owner check
(`DeviceHandle != null`, `!Owner.IsNull`, `InstanceHandle != null`). Pool-owned
non-disposable types (`Fence`, `BinarySemaphore`, `TimelineSemaphore`,
`DescriptorSet`) return `false` — the pool owns the Vulkan object; the struct
never does.

**3. PipelineLayout metadata rides the handle; the side tables are deleted**
(`Pipelines/PipelineLayout.cs`):

```csharp
internal sealed class LayoutMetadata
{
    public required PushConstantRange[] PushRanges  { get; init; }
    public required nint[]              SetLayouts  { get; init; }
}

public readonly unsafe struct PipelineLayout : IVulkanHandle<PipelineLayout>, IDisposable
{
    public   readonly VkPipelineLayout_T* Handle;
    internal readonly VkDevice_T*         DeviceHandle;
    internal readonly LayoutMetadata?     Metadata;   // null for FromRaw / default
    ...
}
```

- `Device.CreatePipelineLayout` builds the metadata object once at creation
  (setup-time allocation — explicitly allowed) and stamps it on the struct.
- `CommandRecorder.AssertSetsMatchLayout` / `AssertPushRangeFits` read
  `layout.Metadata` directly — the dictionary lookups, the global lock, and
  `Register/TryGet/UnregisterMetadata` are deleted, along with the
  unregister-on-dispose obligation and the pointer-reuse hazard (metadata
  lifetime is now exactly the lifetime of the struct copies that reference it,
  enforced by the GC instead of by every dispose path running).
- `FromRaw` layouts carry `Metadata == null`; the assertions already no-op on
  that (`CommandRecorder.cs:244-247`), unchanged behavior.

**4. `ObjectName.Set<T>`** relaxes to `where T : struct, IVulkanHandle<T>` —
its body is already constraint-agnostic.

**5. #102 §2 lands consistently:** device-bound members on borrowed sync
handles (`Fence.IsSignaled/Wait/Reset`, `TimelineSemaphore.Value/Signal/
WaitFor`) get a `DeviceHandle == null` →`InvalidOperationException` guard with
the same message shape as `PipelineLayout.CreatePushDescriptorTemplate` /
`DescriptorSetLayout.CreateUpdateTemplate`, turning the loader AV into a
diagnosable managed error. (`Fence.Wait` runs per frame in `FrameRing`; the
guard is one null compare in front of a host syscall — negligible, and it
documents the borrow contract at the exact place it bites.)

### Costs, stated honestly

- **Handles are no longer `unmanaged`.** External consumers who wrote their
  own `where T : unmanaged, IVulkanHandle<T>` generic code, embedded a handle
  in an unmanaged struct, or stackalloc'd wrapper arrays will break. The
  wrapper is pre-1.0 and its own code never does any of those (audit above);
  the changelog documents the contract change.
- **A struct with an object reference is GC-trackable.** Copies stored to the
  *heap* incur a write barrier. No per-frame path does that: recording
  signatures take `in`/by-value parameters on the stack, and pools store raw
  `nint`s. `FrameRing.Slot` stores `Fence`/`BinarySemaphore` structs, which
  gain no managed field (no type gains one except `PipelineLayout`).
- **`PipelineLayout` grows 16 → 24 bytes.** Recording paths already take it
  `in` (by-ref); host-side creation/bind cost is unaffected at any measurable
  level. To be *proven*, not asserted: see Benchmarks below.

## Invariants honored

- **Zero per-frame allocations:** the only new allocation is one
  `LayoutMetadata` per `CreatePipelineLayout` (setup-time). Release recording
  paths are bit-identical in behavior (metadata consumers are
  `[Conditional("DEBUG")]`). Benchmarks must keep reading `Allocated = -`.
- **AOT-clean:** no reflection, no dynamic codegen; static abstract dispatch
  (`T.ObjectType`, `T.FromRaw`) unchanged. `struct` constraint keeps
  devirtualization through constrained generics.
- **TreatWarningsAsErrors:** no suppressions.
- **Generated dirs untouched.**

## Benchmarks

- New `HandleOwnershipBenchmarks` (`[MemoryDiagnoser]`, driver-free — runs in
  any container): pass-and-return `PipelineLayout` through a call chain,
  read `Metadata` off owning and borrowed handles, `ObjectName`-style generic
  dispatch. Every `Allocated` cell must read `-`; this is the evidence that a
  one-reference handle struct stays allocation-free to *use*.
- Recording/pool benchmarks (`PushConstants`, `PushDescriptors`,
  `CommandRecorder`, `PipelineBarrier`, `FrameRing`) are driver-bound and must
  be recaptured on the Windows host before merge; expected result is noise
  (Release paths don't touch metadata) with `Allocated = -` throughout.
  `docs/benchmarks.md` gains the new class's baseline row.

## Tests (extend `HandleConventionsTests`)

- **Generic ownership matrix** (replaces per-type rediscovery): a single
  generic helper `AssertBorrowContract<T>()` over **all fifteen**
  `IVulkanHandle` struct types asserting `T.FromRaw(sentinel).OwnsHandle ==
  false` and `default(T).OwnsHandle == false`; for the eleven `IDisposable`
  types, `Dispose()` on the FromRaw'd handle is a no-op (the full #106
  matrix — today's test covers seven; pipelines/cache/surface live in
  sibling files).
- **Borrowed handles can't reach destroy:** covered by the same matrix (a
  no-op `Dispose` on a non-null sentinel handle would AV in the loader
  trampoline if the guard regressed — same canary as today, now exhaustive).
- **Metadata is no longer process-global:** `PipelineLayout` type asserts (via
  `InternalsVisibleTo`) that a created layout's `Metadata` rides struct copies
  (`copy.Metadata` reference-equals the original's), that `FromRaw`/`default`
  carry `null`, and a reflection probe in the *test* (tests aren't AOT-bound)
  asserts `typeof(PipelineLayout)` declares no static `Dictionary` fields —
  locking the side tables out of coming back.
- **#102 guards:** `Fence.FromRaw(x).Wait(...)`, `.IsSignaled`, `.Reset()` and
  `TimelineSemaphore.FromRaw(x).Value/Signal/WaitFor` throw
  `InvalidOperationException` (not AV).

## Decisions log (for review)

1. **Option 3 over 2/4** — the audit shows the constraint protects nothing the
   wrapper does, while the side tables it forces are the worst state in the
   codebase (global lock + pointer-keyed lifetime hazard). Option 2's premise
   is factually wrong for this wrapper (#119 made borrowed images carry
   metadata *into* owning-type APIs on purpose). Option 4 keeps the disease
   and treats a symptom.
2. **Keep `FromRaw`, reject a separate borrowed type** — borrowed handles are
   consumer API that must interoperate with every API taking the owning type.
   The borrow contract is enforced by `OwnsHandle` + the generic test matrix
   instead of by type bifurcation.
3. **No debug assert on borrowed `Dispose`** — no-op is the documented,
   tested, migration-guide-relied-upon contract; an assert would make `using`
   over borrowed handles a Debug-build landmine.
4. **`struct` constraint retained** (not fully unconstrained) — preserves
   copy-by-value + `default(T)` null-handle semantics the wrapper is built on.
5. **`OwnsHandle` added to the interface** (binary-breaking for hypothetical
   external implementers, of which there are none pre-1.0) — it is the
   mechanism that turns the per-type convention into a compile-time demand.
