# sync2 split barriers — `Event` handle + `SetEvent` / `WaitEvent` / `ResetEvent`

**Issue:** [#155](https://github.com/pekkah/Ahjo-Vulkan/issues/155) — *Surface: sync2 split barriers — Event handle + CmdSetEvent2/CmdWaitEvents2/CmdResetEvent2*
**Lands consistently with:** [#118](https://github.com/pekkah/Ahjo-Vulkan/issues/118) (handle ownership / `OwnsHandle`), [#122](https://github.com/pekkah/Ahjo-Vulkan/issues/122) (shadow-enum drift tests + `AhjoValidation`), [#121](https://github.com/pekkah/Ahjo-Vulkan/issues/121) (per-device dispatch table), [#93](https://github.com/pekkah/Ahjo-Vulkan/issues/93) (`Stage`/`Access` sync2 shadow enums)
**Test strategy constrained by:** [#32](https://github.com/pekkah/Ahjo-Vulkan/issues/32) (wrapper suite is Windows-only), [#152](https://github.com/pekkah/Ahjo-Vulkan/issues/152) (the Windows lane currently skips every driver-gated test)
**Date:** 2026-07-26

## Problem

The wrapper can express a sync2 dependency only as a *single point* in the
command stream: `CommandRecorder.PipelineBarrier`
(`Recording/CommandRecorder.cs:593-646`) is the entire sync2 surface, and
`DeviceFunctionTable` resolves exactly one dependency command,
`vkCmdPipelineBarrier2` (`Internal/DeviceFunctionTable.cs:107-108, 261-263`).

A dependency between a producer pass and a consumer pass that sit far apart
in execution order can be *split* — signal at the producer with
`vkCmdSetEvent2`, wait at the consumer with `vkCmdWaitEvents2` — so the
intervening passes overlap the hazard instead of stalling at one
`vkCmdPipelineBarrier2`. Ahjo's render graph (R2 slice 3, ADR-0005) wants
exactly that heuristic and is gated on the wrapper exposing it.

Nothing of that surface exists today:

- No managed `Event` type anywhere in `src/Ahjo.Vulkan` — `grep -rn "\bEvent\b" src/Ahjo.Vulkan --include=*.cs` (excluding `Generated/`) returns **zero** hits, as does a search for `class/struct/enum Event` across `src`, `samples` and `tests`.
- `Sync/` holds `Fence.cs`, `BinarySemaphore.cs`, `TimelineSemaphore.cs`, `WaitState.cs` — all pool-owned handles, no `VkEvent`.
- The raw entry points are already generated: `vkCreateEvent` (`Ahjo.Vulkan.Native/Generated/Vk.cs:229`), `vkDestroyEvent` (`:232`), `vkCmdSetEvent2` (`:1309`), `vkCmdResetEvent2` (`:1312`), `vkCmdWaitEvents2` (`:1315`), plus the host trio `vkGetEventStatus`/`vkSetEvent`/`vkResetEvent` (`:235`, `:238`, `:241`) and `VkEventCreateFlagBits.VK_EVENT_CREATE_DEVICE_ONLY_BIT` (`Generated/VkEventCreateFlagBits.cs:5`). **This is managed-surface work only — no codegen regen.**

The issue is explicit that this is *not* urgent: the second half of its
trigger (a frame with real overlap profit) has not fired. That framing is
load-bearing for the decision below — it argues for the smallest surface
that unblocks the consumer, not for speculative infrastructure.

## Evidence

### The handle conventions this must match (#118)

`IVulkanHandle<TSelf>` (`Internal/IVulkanHandle.cs:55-73`) demands
`ObjectType`, `FromRaw`, `RawHandle`, `IsNull`, `OwnsHandle`; the ownership
bullet (`:26-34`) states the contract: `FromRaw`/`default` are borrowed
(`OwnsHandle == false`, no-op `Dispose`, device-bound members throw).

There are two shapes in the repo:

- **Self-owning, device-created**, e.g. `ShaderModule`
  (`Pipelines/ShaderModule.cs:16-45`): two pointers, `HandleRegistry.TrackCreate(this)`
  as the last ctor statement (`:25`), `OwnsHandle => DeviceHandle != null`
  (`:34`), `Dispose` = null guard → `if (!OwnsHandle) return;` →
  `HandleRegistry.TrackDispose(this)` → `vkDestroy*` (`:36-45`). `Sampler`,
  `PipelineCache`, `DescriptorSetLayout`, `PipelineLayout` are identical in
  shape.
- **Pool-owned**, e.g. `Fence` (`Sync/Fence.cs:19-53`): `OwnsHandle => false`
  always (`:50-53`), no `IDisposable`, plus a `ThrowIfBorrowed` guard on every
  device-bound member (`:138-144`).

Every device-created handle is minted by a `Device.CreateX` method that calls
the **static** `Vk.*` P/Invoke and stamps `Handle` on the struct:
`CreateDescriptorSetLayout` (`Lifecycle/Device.cs:246-248`),
`CreateShaderModule` (`:283-285`), `CreatePipelineCache` (`:315-316`),
`CreateSampler` (`:461-462`), `CreatePipelineLayout` (`:520-536`). The one
counter-example, `ExportableSemaphore.CreateBinary(device, …)`
(`Interop/ExportableSemaphore.cs:55-63`), is a static factory on a type that
deliberately does **not** implement `IVulkanHandle`.

The `#118` conformance matrix lives in
`tests/Ahjo.Vulkan.Tests/HandleConventionsTests.cs:60-85` (fifteen types,
enumerated by hand "so adding a handle type forces a conscious entry") with
owning-side asserts at `:88-110` and the generic contract helper at
`:186-199`.

### The barrier recording path this must model

`PipelineBarrier(memory, buffer, image)` (`Recording/CommandRecorder.cs:593-637`):
early-return on an all-empty mix (`:598`), three 16-element `stackalloc`
slabs with `ArrayPool` overflow via `RentForOverflow` (`:601-607`, helper at
`:1243-1257`), a `ToNative()` conversion loop per kind, one `VkDependencyInfo`
built under three nested `fixed`, one `Fns.CmdPipelineBarrier2` call, and an
`ArrayPool` return in `finally`. The image-only and single-image convenience
overloads (`:640-646`) forward to it.

`vkCmdSetEvent2` and `vkCmdWaitEvents2` need **the same 40-line block**, and
Vulkan requires the two ends to agree bit-for-bit (see the VUID audit below).
Copy-pasting it twice more creates three copies that must never drift — the
exact failure the spec requires them not to have.

Call-site audit: `.PipelineBarrier(` appears **43 times across 17 files**
(7 samples, 9 test files, 1 benchmark). All are source-compatible with this
change: the new surface is purely additive and no existing signature moves.

Dispatch: the recorder calls through `Fns`, a `ref readonly DeviceFunctionTable`
off the owning `Device` (`Recording/CommandRecorder.cs:52`). The table's
charter is written down (`Internal/DeviceFunctionTable.cs:9-31`): hot-path
`vkCmd*` + `vkBeginCommandBuffer`/`vkEndCommandBuffer`/`vkQueueSubmit2`;
"Cold-path and instance-level calls keep using the static `[DllImport]`s on
`Vk`" (`:29-31`). Core-guaranteed commands resolve through `ResolveRequired`,
which throws at `Device` construction if the loader returns null (`:328-341`).

The wrapper already requires and enables the feature these commands need:
`PhysicalDevice.cs:225` sets `f13.synchronization2 = 1` in the default feature
chain, and the device floor is Vulkan 1.3.

`Stage` (`Recording/Stage.cs:11-37`) is the wrapper's shadow of
`VkPipelineStageFlags2` — the type the issue calls `PipelineStage2`. It
already carries `Host = 0x00004000` (`:28`), which matters below.

### Pooling precedent — and why it does not transfer

`FencePool` exists because it can *route by state*: `Release` queries
`vkGetFenceStatus` and pushes onto the signaled or unsignaled free-list
(`Pools/FencePool.cs:80-93`), which is what lets `Acquire(initiallySignaled)`
honor its contract. `SemaphorePool` keeps typed free-lists plus an
`AhjoValidation`-gated foreign-handle scan (`Pools/SemaphorePool.cs:105-127`)
and a `Discard` escape hatch for semaphores stuck in a bad state (`:143-156`).

Neither mechanism is available for a device-only event: **`vkGetEventStatus`
must not be called on an event created with `VK_EVENT_CREATE_DEVICE_ONLY_BIT`**
(VUID-vkGetEventStatus-event-03940). A pool could therefore not answer "is
this event still set?" — it would degrade to a `Stack<nint>` whose correctness
depends entirely on the caller having recorded a properly ordered
`ResetEvent`, i.e. it would add a type, a dispose path and an ownership scan
while providing nothing a caller-side array does not.

### Vulkan rules, from the pinned spec

Audited against `registry/validusage.json` at the tag this repo pins,
`vulkan-sdk-1.4.341.0` (`Directory.Build.props:18`; the file self-reports
`api version 1.4.341`):

| Rule | VUID |
|---|---|
| Wait's dependency info must be **exactly equal** to the one recorded at `vkCmdSetEvent2` | `VUID-vkCmdWaitEvents2-pEvents-10788` |
| The event must have been signaled by a corresponding `vkCmdSetEvent2` **earlier in submission order** | `VUID-vkCmdWaitEvents2-pEvents-03841` |
| `vkCmdSetEvent2` / `vkCmdResetEvent2` must be recorded **outside a render pass instance** (and not between suspended ones) | `VUID-vkCmdSetEvent2-renderpass`, `VUID-vkCmdResetEvent2-renderpass`, `-suspended` |
| `vkCmdWaitEvents2` has **no** outside-render-pass rule — only "no `HOST` stage inside a render pass instance" and the suspended rule | `VUID-vkCmdWaitEvents2-dependencyFlags-03844`, `-suspended` |
| No `VK_PIPELINE_STAGE_2_HOST_BIT` in a `SetEvent2` dependency info | `VUID-vkCmdSetEvent2-srcStageMask-09391`, `-dstStageMask-09392` |
| No `HOST` bit in the `ResetEvent2` stage mask | `VUID-vkCmdResetEvent2-stageMask-03830` |
| A reset needs an **execution dependency** against any wait on the same event | `VUID-vkCmdResetEvent2-event-03831`, `-03832` |
| `dependencyFlags` must be 0 (or the KHR asymmetric bit, which is not core 1.3) | `VUID-vkCmdSetEvent2-dependencyFlags-03825` |
| Host `vkGetEventStatus`/`vkSetEvent`/`vkResetEvent` are illegal on a `DEVICE_ONLY` event | `-03940`, `VUID-vkSetEvent-event-03941`, `VUID-vkResetEvent-event-03823` |
| On a portability-subset device without the `events` feature, `vkCreateEvent` must not be used | `VUID-vkCreateEvent-events-04468` |

**Correction to the issue text.** The issue cites *VUID-vkCmdWaitEvents2-pEvents-03847*
for the dependency-info match. In the pinned registry `03847` belongs to the
**sync1** command (`VUID-vkCmdWaitEvents-pEvents-03847`); the sync2 rule is
`VUID-vkCmdWaitEvents2-pEvents-10788`. The wrapper docs must cite `10788`.

Two consequences fall straight out of this table:

1. The host trio being illegal on `DEVICE_ONLY` events means the create flag
   and host-side event ops are **mutually exclusive**. The flag is not a
   tuning knob; it is the switch between two usage modes. Any API shape that
   hard-codes it forecloses the other mode.
2. `PipelineBarrier`'s "empty mix → return without calling the driver"
   (`:598`) is **wrong for events**: skipping a `SetEvent` silently drops a
   signal that a later `WaitEvent` blocks on forever. An empty dependency
   info is also degenerate on its own terms — `vkCmdSetEvent2`'s first
   synchronization scope is the union of the barriers' `srcStageMask`s, so
   with zero barriers the signal orders nothing.

## Decision

Ship the minimal split-barrier surface: one owning `Event` handle created
from `Device` with a device-only default, three recorder methods sharing one
marshalling implementation with `PipelineBarrier`, three dispatch-table
pointers. No pool, no host-side event ops, no convenience overloads.

**1. `Event` — owning handle in `Sync/`** (`src/Ahjo.Vulkan/Sync/Event.cs`):

```csharp
public readonly unsafe struct Event : IVulkanHandle<Event>, IDisposable
{
    public   readonly VkEvent_T*       Handle;
    internal readonly VkDevice_T*      DeviceHandle;
    private  readonly EventCreateFlags _flags;

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_EVENT;
    public static Event FromRaw(nint handle) => new((VkEvent_T*)handle, null, EventCreateFlags.None);
    public ulong RawHandle   => (ulong)Handle;
    public bool  IsNull      => Handle == null;
    public bool  OwnsHandle  => DeviceHandle != null;      // #118 contract member
    public bool  IsDeviceOnly => (_flags & EventCreateFlags.DeviceOnly) != 0;
    public void  Dispose();                                 // guard → TrackDispose → vkDestroyEvent
}
```

It follows the `ShaderModule` template exactly (`Pipelines/ShaderModule.cs:16-45`),
including `HandleRegistry.TrackCreate(this)` in the ctor and
`TrackDispose(this)` in `Dispose`. It lives in `Sync/` because a `VkEvent` is
a synchronization primitive, next to `Fence`/`BinarySemaphore`/`TimelineSemaphore`
— while being the **first owning handle in that folder**: its three neighbours
report `OwnsHandle => false` because a pool owns them (`Sync/Fence.cs:50-53`),
`Event` reports `DeviceHandle != null` because the caller does. That divergence
is deliberate and gets a doc comment, since a reader scanning `Sync/` will
otherwise assume the pool convention.

`_flags` rides the struct so a future host-side `Set()`/`Reset()`/`Status`
can reject a device-only event with a managed error instead of the driver's
undefined behavior, and so a caller can assert what it holds. Borrowed
handles (`FromRaw`, `default`) carry `EventCreateFlags.None` — documented as
*unknown*, not as *host-capable*.

**2. `EventCreateFlags` shadow enum** (`src/Ahjo.Vulkan/Sync/EventCreateFlags.cs`):

```csharp
[Flags] public enum EventCreateFlags : uint { None = 0, DeviceOnly = 0x00000001 }
```

Shadows `VkEventCreateFlagBits` in the established style
(`Pipelines/DescriptorBindingFlags.cs:10-17`) and gets a drift assert in
`ShadowEnumDriftTests` per #122.

**3. `Device.CreateEvent(EventCreateFlags flags = EventCreateFlags.DeviceOnly)`**
(`Lifecycle/Device.cs`) — device-only is the *default*, not the *law*.
`EventCreateFlags.None` is the escape hatch that a future host-op surface
needs, and it exists from day one so adding host ops later is additive
instead of breaking. Implementation mirrors `CreateSampler`
(`Lifecycle/Device.cs:440-462`): build `VkEventCreateInfo`, call the static
`Vk.vkCreateEvent`, `ThrowIfFailed()`, return `new Event(raw, Handle, flags)`.

**4. Three recorder methods** (`Recording/CommandRecorder.cs`):

```csharp
public void SetEvent  (in Event evt, ReadOnlySpan<MemoryBarrier> memory,
                       ReadOnlySpan<BufferBarrier> buffer, ReadOnlySpan<ImageBarrier> image);
public void WaitEvent (in Event evt, ReadOnlySpan<MemoryBarrier> memory,
                       ReadOnlySpan<BufferBarrier> buffer, ReadOnlySpan<ImageBarrier> image);
public void ResetEvent(in Event evt, Stage stageMask);
```

`WaitEvent` is the single-event form (`eventCount = 1`); the multi-event form
of `vkCmdWaitEvents2` needs one `VkDependencyInfo` per event and waits for a
caller that batches. No image-only / single-image convenience overloads yet:
they would have to be added in matched pairs on both halves, and the pairing
contract (10788) is better served by the documented "hold one barrier list
and pass it to both calls" idiom. Adding them later is source- and
binary-additive.

Neither `SetEvent` nor `WaitEvent` early-returns on an empty mix (see
Evidence §2 above): in Release the call reaches the driver unchanged. Under
`AhjoValidation.IsEnabled` (`Diagnostics/AhjoValidation.cs:80-98`) an
all-empty mix, or a null event handle, fails with a message naming the fix —
same gate and message shape as `CommandRecorder.AssertSetsMatchLayout`
(`:254-283`). Cost when validation is off: one volatile bool read and a
predictable branch, on a path that already costs ~130 ns (`docs/benchmarks.md:74`).

**5. One marshalling implementation.** A private
`RecordDependency(DependencyOp op, VkEvent_T* @event, memory, buffer, image)`
holds the stackalloc/rent/`ToNative()`/`fixed`/`VkDependencyInfo` block once
and ends in a three-way dispatch (`CmdPipelineBarrier2` / `CmdSetEvent2` /
`CmdWaitEvents2` with `eventCount = 1`). `PipelineBarrier` keeps its
empty-mix early return at the public entry point and then delegates. This is
what makes "the Set and the Wait produce byte-identical `VkDependencyInfo`s
from equal inputs" a structural property rather than a review obligation.
It touches a benchmarked hot path, so the plan gates it on a benchmark
recapture.

**6. `DeviceFunctionTable` gains the three `vkCmd*` pointers only**
(`Internal/DeviceFunctionTable.cs`), resolved with `ResolveRequired`
(core since 1.3, device floor is 1.3):

```csharp
delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkEvent_T*, VkDependencyInfo*, void>          CmdSetEvent2;
delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, uint, VkEvent_T**, VkDependencyInfo*, void>   CmdWaitEvents2;
delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkEvent_T*, ulong, void>                      CmdResetEvent2;
```

**Deliberate deviation from the issue:** `vkCreateEvent`/`vkDestroyEvent` do
**not** go on the table. The table documents itself as hot-path-only, with
cold-path calls staying on the static `[DllImport]`s (`:29-31`), and all five
existing `Device.CreateX` methods plus both pools call `Vk.*` directly
(`Device.cs:246, 283, 315, 461, 520`; `FencePool.cs:69, 127`;
`SemaphorePool.cs:51, 100, 171`). Putting one create/destroy pair on the
table would make it the only exception, for a setup/teardown call.

**7. No `EventPool`.** Deferred, with a named revisit trigger: the render-graph
consumer landing and stating its acquire/release pattern.

**8. Documented rules** (XML docs on the three methods + `Device.CreateEvent`),
each traceable to the VUID table above:

- The `VkDependencyInfo` at `WaitEvent` must be *exactly equal* to the one
  recorded at `SetEvent` (`VUID-vkCmdWaitEvents2-pEvents-10788`) — hold one
  barrier list across the pair. No wrapper-side enforcement.
- `SetEvent` and `ResetEvent` must be recorded outside a
  `BeginRendering`/`EndRendering` scope; `WaitEvent` may sit inside one.
- No `Stage.Host` in a split-barrier dependency or in a `ResetEvent` stage
  mask.
- Recycling: record `ResetEvent` in a submission ordered after the wait has
  completed (the frame-N+1 command buffer for a frame-N event, or after an
  intervening `PipelineBarrier`) — `VUID-vkCmdResetEvent2-event-03832` wants
  an execution dependency against the wait.
- Host `vkSetEvent`/`vkGetEventStatus`/`vkResetEvent` are out of scope and
  are illegal on the default (device-only) event anyway (`-03941`, `-03940`,
  `-03823`).
- `vkCreateEvent` is unavailable on a portability-subset device that does not
  advertise the `events` feature (`VUID-vkCreateEvent-events-04468`) — the
  MoltenVK caveat, matching the README's "macOS on the roadmap".

Debug naming needs no new code: `ObjectName.Set<T>` takes any
`struct, IVulkanHandle<T>` (`Diagnostics/DebugMarker.cs`), and `Event`
reports `VK_OBJECT_TYPE_EVENT`.

### Why not the alternatives

- **An `EventPool` alongside `FencePool`/`SemaphorePool`** — a pool's value in
  this repo is state routing (`FencePool.cs:80-93`), and state routing is
  impossible for device-only events because `vkGetEventStatus` is forbidden on
  them (`-03940`); with zero consumers in-repo it would be speculative
  infrastructure whose safety story is "trust the caller's `ResetEvent`",
  which a caller-side array already gives for free.
- **Hard-coded `DEVICE_ONLY` with no parameter** — the flag and host event ops
  are mutually exclusive by VUID, so hard-coding picks one of two usage modes
  permanently and makes the other a breaking change to reach.
- **`bool deviceOnly = true`** — reads as `CreateEvent(true)` at call sites,
  cannot grow a second bit, and diverges from the shadow-enum convention the
  repo applies to every other `Vk*FlagBits` it exposes.
- **An `EventDescription` struct (#119 valid-by-default)** — description structs
  earn their ceremony at four-plus fields (`SamplerDescription`,
  `ImageDescription`); for one bit, "valid by default" would mean a field
  initializer whose default is a *restriction*, which reads backwards.
- **A separate `SplitBarrierEvent` / `GpuEvent` name** — the repo names handles
  after the Vulkan noun (`Fence`, `Buffer`, `Image`, `Surface`); `Event`
  collides with nothing in `Ahjo.Vulkan`, `System`, or the implicit usings
  (verified by grep across `src`, `samples`, `tests`).
- **Multi-event `WaitEvents(ReadOnlySpan<Event>, …)`** — needs one
  `VkDependencyInfo` per event, i.e. a jagged marshalling shape, for a caller
  that batches hazards; no such caller exists and the issue agrees.
- **Image-only / single-image convenience overloads now** — every overload has
  to exist on both halves of the pair to be useful, doubling the surface where
  a 10788 mismatch can be introduced, for a consumer that is not here yet.
- **Duplicating the barrier marshalling per command** — three copies of a
  40-line `stackalloc`/rent block whose *equality* between two of them is a
  Vulkan validity requirement; drift there is undetectable by the compiler
  and shows up as a validation error at the consumer's call site.
- **Early-return on an empty mix, matching `PipelineBarrier`** — silently drops
  the event signal and turns the paired wait into a GPU hang; the degenerate
  case must reach the driver (Release) or fail loudly (validation on).
- **Tracking render-pass scope in `CommandRecorder` to assert the
  outside-render-pass rule** — would add mutable state to a `ref struct` whose
  fields are currently `_pool`, `Handle`, `_ended`, `_retired`
  (`Recording/CommandRecorder.cs:31-34`), on every recording path, to catch
  one VUID the validation layer already catches. Documented instead.
- **`vkCreateEvent`/`vkDestroyEvent` on `DeviceFunctionTable`** (as the issue
  requests) — contradicts the table's written charter (`:29-31`) and would be
  the only create/destroy pair there among eleven cold-path creates that use
  the static P/Invokes.
- **Host-side `Set`/`Reset`/`GetStatus` now** — out of scope per the issue, and
  illegal on the default event; the `EventCreateFlags.None` path plus the
  `_flags` field keep the door open at zero cost.

## Invariants honored

- **Zero per-frame allocations.** `SetEvent`/`WaitEvent`/`ResetEvent` allocate
  nothing: the shared helper uses the same 16-element `stackalloc` +
  `ArrayPool` overflow pattern as `PipelineBarrier` (`:601-607`), the event is
  passed `in`, and `Event` gains no managed reference field. `Device.CreateEvent`
  is setup-time. Proven, not asserted: new `[MemoryDiagnoser]` benchmarks.
- **AOT-clean.** No reflection, no dynamic codegen; one more `delegate* unmanaged`
  trio resolved via `vkGetDeviceProcAddr`, exactly like the existing table.
- **UTF-8 literals.** The three new entry-point names are resolved with
  `Utf8Name.FromLiteral("vkCmdSetEvent2"u8)` etc., matching `:189-313`.
- **Generated code untouched.** All native pieces already exist
  (`Generated/Vk.cs:229-241, 1309-1315`); no rsp/codegen change.
- **`TreatWarningsAsErrors`.** No suppressions needed.

## Test strategy (constrained by #32 and #152)

Driver-independent coverage — the part that actually runs in CI today, given
#152 reports the Windows lane skipping all 226 driver-gated tests:

- `Event` in the #118 borrow matrix (fifteen → sixteen types), the owning-side
  assert, `ObjectType`, and `FromRaw(...).IsDeviceOnly == false`.
- `EventCreateFlags` shadow-enum drift assert.

Driver-gated coverage — the honest oracle, run locally on Windows with a real
ICD and the Khronos validation layer, skipped elsewhere (never mocked, per
#32):

- Create a device-only event, assert `IsDeviceOnly`/`OwnsHandle`, dispose.
- Execution oracle: `FillBuffer` → `SetEvent(bars)` → `WaitEvent(bars)` →
  `CopyBuffer` to a host-visible buffer → submit → fence wait → assert the
  bytes. The *same* barrier array is passed to both halves, which is also the
  10788 conformance check when the validation layer is loaded.
- Recycling: `ResetEvent` in a second, later submission, then a second
  Set/Wait round-trip on the same event.
- `AhjoValidation` rejects an all-empty mix and a null event handle.

No Linux wrapper lane is assumed; nothing is added to the `vma-linux` /
`ktx-native` lanes.

## Benchmarks

`SetEvent`/`WaitEvent`/`ResetEvent` are per-frame recording calls, so they
fall under the `Recording/**` zero-allocation rule and need canaries:
`PipelineBarrierBenchmarks` (`tests/Ahjo.Vulkan.Benchmarks/PipelineBarrierBenchmarks.cs:82-122`)
gains a Set/Wait-pair benchmark and a reset benchmark, both recording-only
(never submitted — an unsubmitted, unmatched wait cannot hang). Because step 5
re-routes `PipelineBarrier` through the shared helper, the existing
`PipelineBarrier.SingleImageTransition` / `LargeBatch_8x8x1` rows
(`docs/benchmarks.md:74-75`) must be recaptured on the Windows host; the plan
makes a >20% Mean regression a stop-and-report condition.

## Uncertainty, stated

- The VUID numbers above are read from `registry/validusage.json` at
  `vulkan-sdk-1.4.341.0`, the tag this repo pins. `10788` is recent (it
  replaced the older wording when `VK_DEPENDENCY_ASYMMETRIC_EVENT_BIT_KHR`
  was introduced); a future headers bump could renumber it, so the doc
  comments should quote the *requirement text* and cite the number, not rely
  on the number alone.
- The claim "the render graph only needs the single-event wait" comes from the
  issue, not from code in this repo — there is no in-repo consumer to audit.
  If the graph turns out to batch hazards, the multi-event form is an additive
  follow-up, not a redesign.
</content>
</invoke>
