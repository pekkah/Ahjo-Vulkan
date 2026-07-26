# Implementation plan — sync2 split barriers (#155)

Paired with `../specs/2026-07-26-issue-155-sync2-split-barriers-design.md`.
Ship: `Event` (owning handle, `Sync/`), `EventCreateFlags` shadow enum,
`Device.CreateEvent`, `CommandRecorder.SetEvent` / `WaitEvent` / `ResetEvent`
sharing one dependency-marshalling helper with `PipelineBarrier`, three new
`DeviceFunctionTable` pointers. No `EventPool`, no host-side event ops, no
convenience overloads.

Nothing under `src/*/Generated/` is touched — every native entry point
already exists (`Ahjo.Vulkan.Native/Generated/Vk.cs:229-241, 1309-1315`).

## Step 1 — `EventCreateFlags` shadow enum

New file `src/Ahjo.Vulkan/Sync/EventCreateFlags.cs`, `namespace Ahjo.Vulkan`,
modeled on `src/Ahjo.Vulkan/Pipelines/DescriptorBindingFlags.cs:1-17`:

```csharp
[Flags]
public enum EventCreateFlags : uint
{
    None       = 0,
    DeviceOnly = 0x00000001,
}
```

XML doc: shadow of `VkEventCreateFlagBits` (Vulkan 1.3 core). State that
`DeviceOnly` is the default for split barriers and that it makes the host
event commands (`vkSetEvent` / `vkGetEventStatus` / `vkResetEvent`) illegal on
the event — `VUID-vkSetEvent-event-03941`, `VUID-vkGetEventStatus-event-03940`,
`VUID-vkResetEvent-event-03823` — which is why `None` exists even though the
wrapper exposes no host event ops today.

## Step 2 — `Event` handle

New file `src/Ahjo.Vulkan/Sync/Event.cs`, `namespace Ahjo.Vulkan`, following
the owning-handle template in `src/Ahjo.Vulkan/Pipelines/ShaderModule.cs:16-45`:

```csharp
public readonly unsafe struct Event : IVulkanHandle<Event>, IDisposable
{
    public   readonly VkEvent_T*       Handle;
    internal readonly VkDevice_T*      DeviceHandle;
    private  readonly EventCreateFlags _flags;

    internal Event(VkEvent_T* handle, VkDevice_T* device, EventCreateFlags flags)
    {
        Handle       = handle;
        DeviceHandle = device;
        _flags       = flags;
        HandleRegistry.TrackCreate(this);   // last statement, as in ShaderModule:25
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_EVENT;
    public static Event FromRaw(nint handle) => new((VkEvent_T*)handle, null, EventCreateFlags.None);
    public ulong RawHandle    => (ulong)Handle;
    public bool  IsNull       => Handle == null;
    public bool  OwnsHandle   => DeviceHandle != null;
    public bool  IsDeviceOnly => (_flags & EventCreateFlags.DeviceOnly) != 0;

    public void Dispose()
    {
        if (Handle == null) return;
        if (!OwnsHandle) return;            // borrowed (FromRaw/default) — caller owns the lifetime
        HandleRegistry.TrackDispose(this);
        Vk.vkDestroyEvent(DeviceHandle, Handle, null);
    }
}
```

Doc comments to write, in this order:

- Summary: a `VkEvent` used as a **split barrier** — signal at the producer
  with `CommandRecorder.SetEvent`, wait at the consumer with
  `CommandRecorder.WaitEvent`, so intervening commands overlap the hazard
  instead of stalling at one `PipelineBarrier`.
- **Ownership divergence from its `Sync/` neighbours:** `Fence`,
  `BinarySemaphore` and `TimelineSemaphore` report `OwnsHandle => false`
  because `FencePool`/`SemaphorePool` own them (`Sync/Fence.cs:50-53`);
  `Event` is caller-owned and destroys on `Dispose`. There is no `EventPool`
  (see the spec's decision 7).
- Lifetime: do not dispose while a submission that references the event is
  still pending; `default(Event)` is a legal null handle; double-dispose is
  UB (the standard handle contract, `Internal/IVulkanHandle.cs:21-40`).
- `IsDeviceOnly` on a `FromRaw`/`default` handle is `false` meaning
  **unknown**, not "host-capable" — the wrapper never learns a borrowed
  event's create flags.

## Step 3 — `Device.CreateEvent`

`src/Ahjo.Vulkan/Lifecycle/Device.cs`, placed after `CreateSampler`
(`:411-463`) and implemented in the same shape (static P/Invoke +
`ThrowIfFailed()` + stamp the handle):

```csharp
public Event CreateEvent(EventCreateFlags flags = EventCreateFlags.DeviceOnly)
{
    var ci = new VkEventCreateInfo
    {
        sType = VkStructureType.VK_STRUCTURE_TYPE_EVENT_CREATE_INFO,
        flags = (uint)flags,
    };
    VkEvent_T* raw = null;
    Vk.vkCreateEvent(Handle, &ci, null, &raw).ThrowIfFailed();
    return new Event(raw, Handle, flags);
}
```

Doc comment: default is device-only (the split-barrier case); pass
`EventCreateFlags.None` only if the event must be reachable from host event
commands, which the wrapper does not expose today. Add the portability-subset
caveat: on a device exposing `VK_KHR_portability_subset` without the `events`
feature, `vkCreateEvent` must not be used at all
(`VUID-vkCreateEvent-events-04468`) — relevant to the README's "macOS via
MoltenVK is on the roadmap".

## Step 4 — `DeviceFunctionTable`

`src/Ahjo.Vulkan/Internal/DeviceFunctionTable.cs`.

Fields, in a new `// ---- Split barriers (sync2 events) ----` region directly
after the existing `CmdPipelineBarrier2` field (`:105-108`):

```csharp
public readonly delegate* unmanaged[Stdcall]<
    VkCommandBuffer_T*, VkEvent_T*, VkDependencyInfo*, void> CmdSetEvent2;

public readonly delegate* unmanaged[Stdcall]<
    VkCommandBuffer_T*, uint, VkEvent_T**, VkDependencyInfo*, void> CmdWaitEvents2;

public readonly delegate* unmanaged[Stdcall]<
    VkCommandBuffer_T*, VkEvent_T*, ulong, void> CmdResetEvent2;
```

Resolution, immediately after the `CmdPipelineBarrier2` resolve (`:261-263`),
using `ResolveRequired` + `Utf8Name.FromLiteral("…"u8)` exactly like its
neighbours: `"vkCmdSetEvent2"u8`, `"vkCmdWaitEvents2"u8`,
`"vkCmdResetEvent2"u8`. All three are core since Vulkan 1.3 and the wrapper's
device floor is 1.3, so `ResolveRequired` (not `ResolveWithFallback`) is
correct — no `KHR` fallback.

**OPEN:** the issue also asks for `vkCreateEvent`/`vkDestroyEvent` on this
table. The spec deliberately declines (the table documents itself as
hot-path-only at `:29-31`, and all eleven cold-path creates in the wrapper use
the static `Vk.*` P/Invokes). If the issue author wants them on the table
anyway, stop and ask before implementing this step differently.

## Step 5 — `CommandRecorder`: shared marshalling + three methods

`src/Ahjo.Vulkan/Recording/CommandRecorder.cs`, in the
`// ---- Pipeline barriers (sync2) ----` region (`:584-646`), which gets
renamed `// ---- Pipeline barriers + split barriers (sync2) ----`.

**5a. Extract the marshalling.** Move the body of `PipelineBarrier`
(`:600-636` — the three `stackalloc` slabs, the three `RentForOverflow`
calls, the `ToNative()` loops, the nested `fixed`, the `VkDependencyInfo`
construction, the `ArrayPool` returns in `finally`) verbatim into a new
private method, changing only the final dispatch:

```csharp
private enum DependencyOp { Barrier, SetEvent, WaitEvent }

private void RecordDependency(
    DependencyOp                op,
    VkEvent_T*                  @event,
    ReadOnlySpan<MemoryBarrier> memory,
    ReadOnlySpan<BufferBarrier> buffer,
    ReadOnlySpan<ImageBarrier>  image)
```

Tail, inside the innermost `fixed` where `&dep` is available:

```csharp
switch (op)
{
    case DependencyOp.Barrier:  Fns.CmdPipelineBarrier2(Handle, &dep); break;
    case DependencyOp.SetEvent: Fns.CmdSetEvent2(Handle, @event, &dep); break;
    default:
    {
        VkEvent_T* e = @event;
        Fns.CmdWaitEvents2(Handle, 1, &e, &dep);
        break;
    }
}
```

`RecordDependency` performs **no** empty-mix check — that stays at the public
`PipelineBarrier` entry point, which becomes:

```csharp
public void PipelineBarrier(
    ReadOnlySpan<MemoryBarrier> memory,
    ReadOnlySpan<BufferBarrier> buffer,
    ReadOnlySpan<ImageBarrier>  image)
{
    if (memory.IsEmpty && buffer.IsEmpty && image.IsEmpty) return;
    RecordDependency(DependencyOp.Barrier, null, memory, buffer, image);
}
```

The two convenience overloads (`:640-646`) are unchanged.

**5b. The three new public methods:**

```csharp
public void SetEvent(
    in Event evt,
    ReadOnlySpan<MemoryBarrier> memory,
    ReadOnlySpan<BufferBarrier> buffer,
    ReadOnlySpan<ImageBarrier>  image)
{
    AssertSplitBarrierUsable("SetEvent", in evt, memory, buffer, image);
    RecordDependency(DependencyOp.SetEvent, evt.Handle, memory, buffer, image);
}

public void WaitEvent(
    in Event evt,
    ReadOnlySpan<MemoryBarrier> memory,
    ReadOnlySpan<BufferBarrier> buffer,
    ReadOnlySpan<ImageBarrier>  image)
{
    AssertSplitBarrierUsable("WaitEvent", in evt, memory, buffer, image);
    RecordDependency(DependencyOp.WaitEvent, evt.Handle, memory, buffer, image);
}

public void ResetEvent(in Event evt, Stage stageMask)
    => Fns.CmdResetEvent2(Handle, evt.Handle, (ulong)stageMask);
```

**5c. The validation helper** — same gate and shape as
`AssertSetsMatchLayout` (`:254-283`), i.e. `if (!AhjoValidation.IsEnabled) return;`
first so a Release build pays one volatile read plus a predictable branch:

```csharp
private static void AssertSplitBarrierUsable(
    string caller, in Event evt,
    ReadOnlySpan<MemoryBarrier> memory,
    ReadOnlySpan<BufferBarrier> buffer,
    ReadOnlySpan<ImageBarrier>  image)
```

Two checks, each calling `AhjoValidation.Fail("CommandRecorder", …)` on the
failure branch only:

- null handle →
  `$"{caller}: event is a null handle. Create one with Device.CreateEvent()."`
- all three spans empty →
  `$"{caller}: the dependency is empty. A split barrier with no barriers has an empty synchronization scope and orders nothing — the paired wait would block on a signal that means nothing. Pass at least one barrier (e.g. MemoryBarrier.Between(srcStage, Access.None, dstStage, Access.None))."`

There is deliberately **no** early return on empty: dropping the
`vkCmdSetEvent2` call would silently discard a signal the paired
`vkCmdWaitEvents2` blocks on.

**OPEN:** this introduces a wrapper-invented failure (validation builds only)
for a call the driver would accept. The spec recommends it because the failure
mode it prevents is a GPU hang; if the reviewer prefers doc-only, drop
`AssertSplitBarrierUsable` and keep the doc lines. Confirm before merging.

**5d. Doc comments.** On all three methods, plus one shared remark block:

- `SetEvent` — `vkCmdSetEvent2`. Signals `evt` when the union of the
  barriers' `SrcStage` masks completes; the dependency's second half is
  applied at the matching `WaitEvent`. **The `VkDependencyInfo` recorded here
  must be exactly equal to the one passed to `WaitEvent`
  (`VUID-vkCmdWaitEvents2-pEvents-10788`)** — hold one barrier list (a field
  or a local array) and pass it to both calls; the wrapper does not enforce
  this. Must be recorded outside a `BeginRendering`/`EndRendering` scope
  (`VUID-vkCmdSetEvent2-renderpass`). `Stage.Host` must not appear in any
  barrier (`-09391`, `-09392`).
- `WaitEvent` — `vkCmdWaitEvents2` with `eventCount = 1`. The event must have
  been signaled by a `SetEvent` earlier in submission order
  (`VUID-vkCmdWaitEvents2-pEvents-03841`). May be recorded inside a
  render pass instance, unlike `SetEvent`/`ResetEvent`, provided no barrier
  carries `Stage.Host` (`-03844`). The multi-event form is not wrapped: it
  needs one dependency info per event and no caller batches today.
- `ResetEvent` — `vkCmdResetEvent2`. Returns the event to the unsignaled
  state so it can be reused next frame. Record it in a submission ordered
  **after** the wait completed (the frame-N+1 command buffer for a frame-N
  event, or after an intervening `PipelineBarrier`): the spec requires an
  execution dependency between the reset and any wait on the same event
  (`VUID-vkCmdResetEvent2-event-03831`, `-03832`). `stageMask` must not
  include `Stage.Host` (`-03830`), and the command must be outside a
  render pass instance (`VUID-vkCmdResetEvent2-renderpass`).

**5e.** Extend the recorder's type-level `<remarks>` surface list
(`:22-27`) with "split barriers (SetEvent / WaitEvent / ResetEvent)".

## Step 6 — Host-side tests (these actually run in CI, see #152)

`tests/Ahjo.Vulkan.Tests/HandleConventionsTests.cs`:

- Add `AssertBorrowContract<Event>();` to
  `BorrowContract_HoldsForEveryHandleType` (`:60-85`) and update the comment's
  "fifteen"/"all" wording to sixteen types.
- Add to `OwningHandles_ReportOwnsHandle` (`:88-110`), in the owning block,
  **not** the pool-owned block, with a comment that `Event` is the first
  owning handle in `Sync/`:
  `Assert.True(new Event((VkEvent_T*)0x2000, device, EventCreateFlags.DeviceOnly).OwnsHandle);`
  (sentinel pointers — do not dispose).
- New `[Fact] Event_ObjectType_IsEvent` →
  `Assert.Equal(VkObjectType.VK_OBJECT_TYPE_EVENT, Event.ObjectType);`
- New `[Fact] Event_FromRaw_ReportsDeviceOnlyUnknown` →
  `Assert.False(Event.FromRaw(0x1234_5678).IsDeviceOnly);` with the comment
  that `false` means *unknown* for a borrowed handle.

`tests/Ahjo.Vulkan.Tests/ShadowEnumDriftTests.cs`: new
`[Fact] EventCreateFlags_MatchesNative` →
`Assert.Equal((uint)VkEventCreateFlagBits.VK_EVENT_CREATE_DEVICE_ONLY_BIT, (uint)EventCreateFlags.DeviceOnly);`

## Step 7 — Driver-gated tests

New file `tests/Ahjo.Vulkan.Tests/SplitBarrierTests.cs`, `sealed unsafe class
SplitBarrierTests`, with the private `CreateGraphicsDevice(Instance, out uint
family)` helper copied from `PipelineBarrierTests.cs:329-349`. Every test
opens with `Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver
on host.")`; the submitting ones add
`Assert.SkipWhen(VulkanDriverProbe.IsSoftwareDriver, "Software ICD (Mesa
lavapipe): vkQueueSubmit2 SIGSEGVs during command-buffer execution.")` —
matching `PipelineBarrierTests.cs:188-190`.

1. `CreateEvent_DeviceOnly_IsOwningAndDisposes` — driver only, no submit:
   `using var evt = device.CreateEvent();` → assert `!evt.IsNull`,
   `evt.IsDeviceOnly`, `evt.OwnsHandle`. Second case:
   `device.CreateEvent(EventCreateFlags.None)` → `!IsDeviceOnly`.
2. `SetEvent_WaitEvent_Pair_Orders_Fill_Before_Copy` — the execution oracle.
   - Device-local `src` buffer (`BufferUsage.TransferDst | TransferSrc`) and a
     host-visible mapped `dst` (`MemoryUsage.AutoPreferHost`,
     `AllocationFlags.HostAccessRandom | Mapped`), as in
     `CommandRecorderTests.cs:74-85`.
   - One shared barrier array, used for **both** halves of the pair — this is
     what makes the test a `VUID-vkCmdWaitEvents2-pEvents-10788` check when
     the validation layer is loaded:
     `MemoryBarrier[] bars = [MemoryBarrier.Between(Stage.AllTransfer, Access.TransferWrite, Stage.AllTransfer, Access.TransferRead)];`
   - Record: `rec.FillBuffer(in src, 0xA5A5A5A5u)` →
     `rec.SetEvent(in evt, bars, default, default)` →
     `rec.WaitEvent(in evt, bars, default, default)` →
     `rec.CopyBuffer(in src, in dst)` → `queue.Submit2(ref rec, in fence)`.
   - `Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(5)))`,
     then assert every `uint` in `dst.AsReadOnlySpan<uint>()` is `0xA5A5A5A5`.
3. `ResetEvent_InLaterSubmission_AllowsReuse` — the recycling story.
   After test 2's submission has been fence-waited, reset and reuse **in a
   separate submission** (never in the same command buffer as the wait —
   `VUID-vkCmdResetEvent2-event-03832`):
   `queue.ImmediateSubmit(cmdPool, (ref CommandRecorder r) => r.ResetEvent(in evt, Stage.AllTransfer));`
   then repeat the fill/set/wait/copy round-trip with a different fill value
   and assert the new value lands.
4. `SetEvent_NullEvent_FailsUnderValidation` and
   `SetEvent_EmptyDependency_FailsUnderValidation` — driver-gated (a recorder
   needs a pool and a device) but no submit. Wrap in
   `bool prior = AhjoValidation.Enabled; AhjoValidation.Enabled = true;` /
   `finally { AhjoValidation.Enabled = prior; }` (the pattern
   `AhjoValidationTests.cs:25-35` uses; safe because the suite is
   single-threaded — `xunit.runner.json` sets `maxParallelThreads = 1`), assert
   `Assert.Throws<AhjoValidationException>(...)`. Drop these two if the
   step 5c OPEN resolves to doc-only.

Do **not** add any of this to the `vma-linux` / `ktx-native` CI lanes (#32,
`.github/CLAUDE.md`).

## Step 8 — Benchmarks

`tests/Ahjo.Vulkan.Benchmarks/PipelineBarrierBenchmarks.cs`:

- Add `private Event _event;`; create it in `[GlobalSetup]`
  (`_event = _device.CreateEvent();`, after the device/image setup at `:44-63`)
  and dispose it in `[GlobalCleanup]` **before** `_device?.Dispose()` (`:73-80`).
- `[Benchmark(OperationsPerInvoke = CallsPerInvoke)] public void SetWaitEventPair_SingleImage()`
  — same body shape as `LargeBatch_8x8x1` (`:100-122`): build one
  `ImageBarrier` into a `Span<ImageBarrier> bars = stackalloc ImageBarrier[1]`,
  `using scoped var rec = _cmdPool.Begin();`, loop `CallsPerInvoke` times
  recording `rec.SetEvent(in _event, default, default, bars)` followed by
  `rec.WaitEvent(in _event, default, default, bars)`, then `rec.End()` and
  `_cmdPool.ResetForFrame()`. One op = one Set+Wait pair.
- `[Benchmark(OperationsPerInvoke = CallsPerInvoke)] public void ResetEvent_Single()`
  — loop `rec.ResetEvent(in _event, Stage.AllTransfer)`.
- XML comment on both: **recording only, never submitted** — the command
  buffer is reset, not queued, so the repeated / unmatched waits cannot hang a
  queue and are not a validation error against any executed workload.

Run (`/run-bench`, Release, Windows host with a real ICD):

```
dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter "*PipelineBarrier*"
```

Both new rows must report `Allocated = -`.

**OPEN (stop condition):** step 5a re-routes the shipped `PipelineBarrier`
path through `RecordDependency`. If the recapture shows
`PipelineBarrier.SingleImageTransition` regressing more than 20% against the
`docs/benchmarks.md:74` baseline (131.6 ns), stop and report — do not
hand-optimize, and do not merge on the assumption that it is noise.

## Step 9 — Docs

- `docs/benchmarks.md`: add two rows to the baseline table (after the two
  `PipelineBarrier.*` rows at `:74-75`) with the captured numbers and
  `Allocated = -`; refresh the two existing `PipelineBarrier.*` rows from the
  same capture. Add a caveat bullet in the `## Caveats` list: `PipelineBarrier`,
  `SetEvent` and `WaitEvent` now share one `RecordDependency` marshalling
  implementation (#155), which is what keeps the Set/Wait pair's
  `VkDependencyInfo`s byte-identical; the barrier rows were recaptured after
  the extraction.
- No change needed to `src/Ahjo.Vulkan/README.md` (it describes layers, not
  individual recording commands), `docs/aot-notes.md` (no new AOT-relevant
  pattern), or `docs/migration-vortice-to-ahjo.md` (no Vortice counterpart
  being mapped). Stated so nobody hunts for them.

## Step 10 — Verify

- `dotnet build Ahjo.Vulkan.slnx` clean (`TreatWarningsAsErrors`), then
  `dotnet test`. Locally on Windows with a real ICD, confirm the four
  `SplitBarrierTests` actually **run** (not skip) and pass with
  `VK_LAYER_KHRONOS_validation` enabled — that layer is the only oracle for
  `10788`, and per #152 CI will report green whether they ran or not.
- Benchmarks per step 8, including the stop condition.
- Run the `vulkan-validation-reviewer` agent (the diff touches `Recording/`
  and `Sync/`) and the `bench-coverage-checker` agent (new hot-path calls +
  a refactor of a benchmarked path).
- Commit style: `Recording: add sync2 split barriers (Event, SetEvent/WaitEvent/ResetEvent)`;
  PR references `Closes #155`.

## Risk notes

- **Hot-path refactor on a shipped path.** Step 5a is behavior-neutral by
  construction (the moved code is verbatim; only the tail dispatch is a
  switch), but it is the one part of this change that can regress something
  that works today. The benchmark gate in step 8 is the control.
- **A wait recorded without its set hangs the GPU.** Nothing the wrapper can
  detect at record time (the pairing spans command buffers and submissions).
  Documented on `WaitEvent`; the tests keep set and wait in one command buffer
  so a mistake fails fast.
- **`Event` breaks the `Sync/` folder's pool-owned convention.** Called out in
  its doc comment and in the `HandleConventionsTests` owning-side block; a
  reader who pattern-matches on `Fence` will otherwise expect
  `OwnsHandle => false`.
- **VUID numbers can be renumbered by a headers bump.** The doc comments quote
  the requirement text as well as the number, so a stale number is a doc nit
  rather than a lost rule.
</content>
