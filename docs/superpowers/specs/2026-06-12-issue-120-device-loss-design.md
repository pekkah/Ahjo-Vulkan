# Pluggable diagnostics sink + first-class device-loss / swapchain state

**Issue:** [#120](https://github.com/pekkah/Ahjo-Vulkan/issues/120) — *Design: pluggable diagnostics sink + first-class device-loss / swapchain state*
**Absorbs:** [#107](https://github.com/pekkah/Ahjo-Vulkan/issues/107) (point fix landed in d70f31e; this design closes it),
[#104](https://github.com/pekkah/Ahjo-Vulkan/issues/104), [#110](https://github.com/pekkah/Ahjo-Vulkan/issues/110),
[#111](https://github.com/pekkah/Ahjo-Vulkan/issues/111), [#112](https://github.com/pekkah/Ahjo-Vulkan/issues/112)
**Builds on:** #123 (vk.xml-derived result policy — the `ResultExtensions` choke point), #118 (`IVulkanHandle` relaxed to `struct`, which makes a managed `Device` reference on `Fence` legal)
**Date:** 2026-06-12

## Problem

Two related gaps in "what happens when things go wrong":

1. **Diagnostics are hard-wired to stderr.** Five call sites write directly to
   `Console.Error`: `Device.Dispose` (failed `vkDeviceWaitIdle`),
   `Allocator.Dispose` (VMA leak report), `FrameRing.Slot.Dispose`
   (non-signaled teardown wait), `Device.LoadOrCreatePipelineCache` (header
   mismatch), and `Instance.DefaultCallback` (debug-utils messages). An engine
   host (Logos) with its own logging cannot capture any of it.

2. **Device loss has no shared state.** Each error path independently decides
   how to behave after `VK_ERROR_DEVICE_LOST`, which produced #107 (teardown
   throws through a fence status query), #111 (semaphores destroyed while
   operations pending), and #112 (frame-loop bookkeeping desyncs on submit
   failure). The swapchain likewise has no legal representation for "window is
   minimized" (#110) or "recreate failed and the old handle is retired" (#112
   §2), so those situations corrupt object state instead of being states.

## Part 1 — `AhjoDiagnostics.Sink`

### Shape

```csharp
namespace Ahjo.Vulkan;

public enum DiagnosticSeverity { Info, Warning, Error }

public delegate void DiagnosticSink(DiagnosticSeverity severity, string source, string message);

public static class AhjoDiagnostics
{
    // volatile reference-typed field: replacement is an atomic pointer store,
    // readers never observe a torn value. No lock needed.
    private static volatile DiagnosticSink s_sink = DefaultSink;

    public static DiagnosticSink Sink
    {
        get => s_sink;
        set => s_sink = value ?? throw new ArgumentNullException(nameof(value));
    }

    internal static void Write(DiagnosticSeverity severity, string source, string message)
        => s_sink(severity, source, message);

    private static void DefaultSink(DiagnosticSeverity severity, string source, string message)
        => Console.Error.WriteLine(message);
}
```

- **Managed delegate, not `delegate*`.** Both are AOT-clean (no reflection, no
  dynamic codegen — a static delegate field is plain AOT-compatible IL). The
  managed delegate wins because the consumer's sink almost always closes over
  an instance (`msg => _logger.Log(...)`); a `delegate*` cannot capture, so
  every host would need a hand-rolled static-trampoline + state-stash — the
  exact ceremony this wrapper exists to remove. The cost difference (one
  delegate invoke vs. one calli) is irrelevant on cold paths.
- **Static, process-wide — not per-`Device`.** The debug-utils callback fires
  from `Instance` before any `Device` exists, and `Allocator`/`FrameRing`
  teardown diagnostics would each need their own plumbing for a per-device
  sink. One process-wide hook matches the one-logger reality of an engine
  host. (Per-device routing, if ever needed, can be layered on by the host —
  the `source` argument makes that possible without API change.)
- **Non-null contract.** `Sink` may be replaced but never nulled; call sites
  invoke unconditionally with no null check. Restoring default = assigning a
  stderr-writing delegate again (test helper exposes the default).
- **Behavior-preserving default.** Call sites pass the same message text they
  print today; the default sink writes `message` verbatim to `Console.Error`.
  `severity`/`source` are metadata for hosts that want filtering.

### Call sites and the zero-per-frame-cost claim, verified

| Call site | Frequency | Severity / source |
|---|---|---|
| `Device.Dispose` (wait-idle failure) | once per device lifetime | Warning / `"Device"` |
| `Allocator.Dispose` (VMA leak report) | once per allocator lifetime | Warning / `"Allocator"` |
| `FrameRing.Slot.Dispose` (non-signaled wait) | once per slot, teardown only | Warning / `"FrameRing"` |
| `Device.LoadOrCreatePipelineCache` (header mismatch) | once per load, setup only | Warning / `"PipelineCache"` |
| `Instance.DefaultCallback` (debug-utils) | per validation message | mapped from `VkDebugUtilsMessageSeverityFlagBitsEXT` / `"Vulkan"` |

The first four are dispose/setup paths — strictly cold. The debug-utils
callback can fire per frame, but only when `EnableValidation = true` (a debug
configuration, never a shipping frame loop), and it already allocates today
(`Utf8.ToString` + string interpolation); routing the resulting string through
a delegate adds no allocation. **No path inside the per-frame
Recording/Sync/Pools/Memory surface gains a sink call** — the claim holds.
`Debugger.Break()` on error severity stays in the callback (debugger policy is
not a logging concern).

The README/XML-doc references to `Console.Error` ("default callback writes to
Console.Error", `LoadOrCreatePipelineCache` doc) update to reference
`AhjoDiagnostics.Sink` with its stderr default.

## Part 2 — `Device.IsLost`

### State

```csharp
public sealed unsafe class Device : IDisposable
{
    private volatile bool _lost;

    /// <summary>True once any wrapper call has observed VK_ERROR_DEVICE_LOST
    /// for this device. Set-once; never cleared. After loss: waits return
    /// immediately, status queries throw deterministically without calling
    /// the driver, pools release without querying, teardown destroys without
    /// draining.</summary>
    public bool IsLost => _lost;

    internal void MarkLost() => _lost = true;
}
```

**Thread-safety justification (frame loop vs. teardown race).** The flag is
monotonic — it transitions `false → true` exactly once and is never reset, so
there is no lost-update hazard and no need for `Interlocked`. A `volatile`
bool gives every reader a non-torn, eventually-visible value. The two
possible races are both benign:

- *Reader misses a just-set flag:* it proceeds to make the real Vulkan call
  and observes `VK_ERROR_DEVICE_LOST` from the driver itself — the same
  outcome the fast path would have produced, minus the shortcut.
- *Teardown reads while frame loop sets:* teardown either sees `true` (skips
  queries) or `false` (performs the real call, which returns
  `DEVICE_LOST` in bounded time per the spec's forward-progress guarantee).
  No ordering of the race produces a hang or a double-destroy.

A volatile read is one ordinary load with acquire semantics on every target
ISA — zero allocation, sub-nanosecond, safe under the per-frame canary.

### Where the flag gets set

The **primary hook is `ResultExtensions.Throw`** — the single cold-path
funnel that every `ThrowIfFailed`/`ThrowIfErrored` failure passes through
(established by the #123 result policy). Its `VK_ERROR_DEVICE_LOST` arm
notifies before throwing the cached exception. `Throw` is static with no
device context, so notification goes through a registry:

```csharp
// On Device: a process-wide registry of live devices. Registered in the
// ctor, unregistered in Dispose. WeakReference so the registry can never
// keep an undisposed Device alive past user reachability (the finalizer
// must stay able to run). Lock-protected; every touch is cold-path
// (ctor, Dispose, and the already-throwing loss path).
private static readonly List<WeakReference<Device>> s_live = [];

internal static void NotifyDeviceLossObserved()  // called from ResultExtensions.Throw
{
    lock (s_live) { /* mark every live entry lost; prune dead entries */ }
}
```

A context-free `DEVICE_LOST` **marks every live device**. Justification and
honest caveat:

- The dominant deployment (Logos, every sample, every test) is exactly one
  `Device`; "all" = "the one that errored".
- In a hypothetical multi-device process, a loss on device B also marks
  healthy device A. The consequences on A are bounded: `Fence.Wait`
  fast-returns `DeviceLost` (frame loop exits to its recovery path) and
  `Device.Dispose` **still performs its real `vkDeviceWaitIdle`**
  (see teardown policy below — Dispose never trusts the flag enough to skip
  the drain), so A is torn down early but *safely drained*. We trade a rare
  false-positive teardown in a configuration the wrapper doesn't target for
  not threading device identity through every `ThrowIfFailed` call site
  (which would burn the inlining budget the #123 design explicitly protects).

The few paths that observe `DEVICE_LOST` **without** flowing through
`Throw` mark their own device directly (they all already hold one):

- `Fence.Wait` / `TimelineSemaphore.WaitFor` — `ToWaitState` maps
  `DEVICE_LOST` to a *returned* `WaitState.DeviceLost`, no throw. The wrapper
  marks the owning device when that mapping fires.
- `Swapchain.AcquireNextImage` / `Present` — their `VK_ERROR_DEVICE_LOST`
  arms throw `new VulkanException` directly (multi-success APIs, outside the
  choke point); they mark `_device` first.

That is the complete set — `rg VK_ERROR_DEVICE_LOST src/` confirms no other
wrapper code handles the code outside `ResultExtensions`, `WaitState`,
`Fence`, `Swapchain`, and doc comments. This is still "one choke point plus
the spec-mandated non-throwing returns", not the scatter #120 warns against:
no call site *decides behavior*; they all only feed the same flag.

### Who consults the flag, and what changes

`Fence` (and `TimelineSemaphore`) currently carry only a raw
`VkDevice_T*` — they cannot read a managed flag. **They gain a
`Device?` owner reference** (legal since #118 relaxed `IVulkanHandle` to
`struct`; precedented by `PipelineLayout.Metadata`). `FencePool.Acquire` /
`SemaphorePool.AcquireTimeline` stamp `_device`; `FromRaw`/`default` carry
`null` (borrowed handles have no loss tracking — their device-bound members
already throw `InvalidOperationException` per #102/#118). The raw
`DeviceHandle` field is replaced by `_owner.Handle` at the call sites — the
struct stays two words (handle + reference).

| Consumer | After `IsLost` |
|---|---|
| `Fence.Wait` / `TimelineSemaphore.WaitFor` | return `WaitState.DeviceLost` immediately — no driver call. (Healthy path cost: one null check + one volatile read in front of a host syscall.) |
| `Fence.IsSignaled` | throw the cached `DeviceLost` exception immediately — deterministic, no driver call. Post-loss fence state is unknowable; the existing contract ("anything outside SUCCESS/NOT_READY throws") is preserved, just made deterministic. |
| `FencePool.Release(Fence)` | when `_device.IsLost`, skip the `vkGetFenceStatus` query and file the fence on the unsignaled list (bucket immaterial — `Dispose` destroys all handles; same rationale as the #107 `knownSignaled` overload, which stays for callers that *know* the state). |
| `FrameRing.BeginFrame` → `Slot.WaitAndReset` | unchanged contract: the wait observes `DeviceLost` (now via fast path) and throws — "dispose the ring, rebuild" recovery stands. |
| `FrameRing.Slot.Dispose` | wait fast-returns `DeviceLost`; the existing log routes through the sink; teardown proceeds and `FencePool.Release(_, knownSignaled)` keeps the loop alive — the full #107 scenario now completes all slots. |
| `Device.Dispose` | **still calls `vkDeviceWaitIdle` unconditionally** — on a truly lost device it returns `DEVICE_LOST` in bounded time, on a falsely-marked device (multi-device caveat above) it actually drains. Skipping it would convert the registry's conservatism into UB. Non-success logs via sink (existing message). |
| `Swapchain.Recreate` | when `_device.IsLost`, throw the cached `DeviceLost` up front instead of attempting `vkDeviceWaitIdle` + create against a dead device. |

`Device.WaitIdle()`, `Fence.Reset`, pool `Acquire`s: no fast path — they are
cold or already throw correctly via the choke point (which also sets the
flag, making *subsequent* calls deterministic).

## Part 3 — Swapchain state machine

### States

```csharp
public enum SwapchainState
{
    /// <summary>Swapchain exists and matches the surface; acquire/present legal.</summary>
    Ready,
    /// <summary>Acquire or Present returned OutOfDate. Acquire/present remain
    /// legal (they will keep reporting OutOfDate); call Recreate.</summary>
    NeedsRecreate,
    /// <summary>The surface currently has zero extent (minimized window).
    /// No usable swapchain; Acquire/Present throw. Poll window size and call
    /// Recreate when restored.</summary>
    Minimized,
    /// <summary>A Recreate failed after the old swapchain was retired, the
    /// surface was lost, or the device was lost. No swapchain handle is held.
    /// Acquire/Present throw; Recreate attempts a from-scratch create
    /// (surface/device loss permitting); Dispose is always legal.</summary>
    Poisoned,
}
```

`Swapchain.State` is a public get-only property. `Recreate` changes signature
from `void` to `SwapchainState` (returns the post-call state), so the frame
loop's minimize handling is one expression:

```csharp
if (swapchain.Recreate(desc, ring.WaitForInFlightFences) == SwapchainState.Minimized)
    return; // skip this frame; retry when the window restores
```

### Transitions (exhaustive)

| From | Event | To | Notes |
|---|---|---|---|
| — | ctor, non-zero extent | Ready | today's behavior |
| — | ctor, zero extent (`currentExtent == (0,0)` or sentinel-clamp produced 0) | Minimized | **#110 §1**: no `vkCreateSwapchainKHR` call; `_handle == null`. Covers "app launched minimized". |
| Ready | `AcquireNextImage`/`Present` → `OutOfDate` | NeedsRecreate | result still returned to caller |
| Ready | `AcquireNextImage`/`Present` → `Suboptimal` | Ready | image is usable per spec; recreate stays the caller's choice |
| Ready / NeedsRecreate | `Recreate`, computed extent is zero | Minimized | early-out **before** drain/destroy: caps are queried first, nothing is touched — old handle, views, and per-image semaphores stay intact for the next attempt |
| Minimized | `Recreate`, non-zero extent | Ready | if a prior handle exists it is passed as `oldSwapchain`; else fresh create |
| Ready / NeedsRecreate | `Recreate`, create succeeds | Ready | old handle destroyed (or deferred — see #111 below) |
| Ready / NeedsRecreate / Minimized | `Recreate`, create **throws** | Poisoned | **#112 §2**: old swapchain is retired-by-spec even on failure → destroy `old`, null `_handle`, discard views + per-image semaphores, set state, rethrow. Object is never left referencing a retired handle. |
| any | `AcquireNextImage`/`Present` → `SurfaceLost` | Poisoned | same-surface `Recreate` cannot succeed; result still returned so the caller branches to surface + swapchain rebuild |
| any | `AcquireNextImage`/`Present` → `VK_ERROR_DEVICE_LOST` | Poisoned | `_device.MarkLost()` + throw (existing throw, now preceded by state/flag updates) |
| Poisoned | `Recreate` (device not lost, surface still valid) | Ready / Minimized / Poisoned | from-scratch create, `oldSwapchain = null` — recovery without disposing the wrapper object |
| Minimized / Poisoned | `AcquireNextImage` / `Present` | — | throw `InvalidOperationException` naming the state and the recovery step. Guards the "loop forever re-acquiring a dead swapchain" failure mode at the API boundary. |
| any | `Dispose` | — | always legal; flushes the deferred-destroy list (below) |

### Point fixes riding in the same functions

- **#104 — image-count clamp:** `maxClamp = caps.maxImageCount == 0 ?
  uint.MaxValue : caps.maxImageCount;` — `PreferredImageCount` below
  `minImageCount` now clamps up instead of throwing `ArgumentException`,
  matching the `SwapchainDescription` doc promise.
- **#110 §2 — compositeAlpha:** prefer `OPAQUE` when
  `caps.supportedCompositeAlpha` advertises it, else take the lowest set bit
  (the spec guarantees at least one). Latent on Windows, real on
  Wayland/Android.
- **#112 §1 — submit bookkeeping:** `FrameContext.Submit` (swapchain-aware
  overload) calls `Slot.MarkSubmitted()` / `MarkAcquireWaitConsumed()`
  **after** `queue.Submit2` returns. A throwing submit now leaves the acquire
  signal flagged pending (so `RecycleStaleAcquireSemaphores` rotates it) and
  the fence un-armed (so the next `BeginFrame` doesn't wait for a submit that
  never happened). The headless overload moves `MarkSubmitted` identically.

### #111 — semaphore (and retired-swapchain) lifetime on the drain-callback path: **deferred destruction**

Decision: **defer destruction; do not silently upgrade the callback to
`vkDeviceWaitIdle`.**

- Per-frame fences signal at *submit* completion and prove nothing about
  `vkQueuePresentKHR`'s semaphore-wait. Destroying the per-image
  `RenderingDone` semaphores after only a fence drain violates
  VUID-vkDestroySemaphore-semaphore-01137. The same logic applies to the
  retired old `VkSwapchainKHR` itself (VUID-vkDestroySwapchainKHR-swapchain-01282
  — its presentable images may still be queued), so the deferral covers
  **both** the semaphores and the old handle.
- Forcing `vkDeviceWaitIdle` whenever `syncBeforeDestroy != null` would make
  the callback parameter pure decoration — the entire point of the callback
  (#recreate without stalling all frames in flight) dies.
- So: when the drain was the caller's callback, `Recreate` moves the old
  handle + old per-image semaphores onto a retire list instead of destroying
  them. The list is flushed (destroyed) at the three points where device-wide
  completion is proven: a later `Recreate` that used the `vkDeviceWaitIdle`
  default, `Dispose` (whose documented contract is already "call after the
  device is idle"), and a flush helper invoked by `Device.Dispose`'s
  wait-idle. Growth is bounded by recreates-per-session × imageCount tiny
  kernel objects — and resizes are user-interactive-rate, not per-frame.
  (`VK_EXT_swapchain_maintenance1` present-fences are the real fix; noted as
  a follow-up, not taken here — SwiftShader CI doesn't expose it.)
- **#111 §2** (teardown with `_acquireSignalPending`): `FrameRing.Slot.Dispose`
  issues `vkDeviceWaitIdle` before releasing `ImageAcquired` when the acquire
  signal is pending — teardown path, cost irrelevant, and on a lost device
  the call returns `DEVICE_LOST` in bounded time (result ignored, teardown
  proceeds; destroy-after-loss is spec-legal).

## What lands in this PR vs. separately

| Issue | Disposition |
|---|---|
| #120 sink + `Device.IsLost` + swapchain states | this PR |
| #107 | point fix already landed (d70f31e); this PR adds the `IsLost`-aware plain `Release` and closes it |
| #104, #110 (both halves), #112 (both halves) | this PR — each fix is a few lines inside the exact functions the state machine rewrites; landing them separately would mean rebasing the same hunks twice |
| #111 (both halves) | this PR — the deferred-destroy list is part of `Recreate`'s new structure |
| `VK_EXT_swapchain_maintenance1` present-fence path | **separate** follow-up issue |
| Per-device sink routing, structured (non-string) diagnostics | **not planned** — YAGNI until a host demands it |

## Invariants honored

- **Zero per-frame allocations:** the only hot-path deltas are one volatile
  bool read in `Fence.Wait`/`IsSignaled`/`TimelineSemaphore.WaitFor` and the
  `Device?` field on the sync structs (no heap writes on any per-frame path —
  slots hold the structs in fields assigned at setup). Covered by the
  existing `Sync_HostOps_RoundTrip` (#118) and `FrameRing` benchmark
  canaries; every `Allocated` cell must keep reading `-`. Sink calls and
  state-enum writes live on dispose/recreate/error paths only.
- **AOT-clean:** static delegate field, enum, volatile bool, `WeakReference`
  list — no reflection, no dynamic codegen, nothing trim-unsafe.
- **TreatWarningsAsErrors:** no suppressions anticipated.
- **Generated dirs untouched.**
- **UTF-8 literals:** not implicated (sink deals in managed strings on cold
  paths only).

## Tests

- **Sink:** swap in a capturing sink → trigger the `Allocator` leak warning
  and the `LoadOrCreatePipelineCache` header mismatch (driver-gated, like the
  rest of the suite) → assert captured text matches today's stderr strings;
  assert null assignment throws; assert default restores. Concurrent-swap
  smoke (set from one thread while another writes) for the volatile contract.
- **`IsLost` without a GPU-loss simulator:** `InternalsVisibleTo` is already
  in place — tests call `device.MarkLost()` and assert: `Fence.Wait` returns
  `DeviceLost` without blocking; `Fence.IsSignaled` throws `VulkanException`
  with `Result == VK_ERROR_DEVICE_LOST`; `FencePool.Release` files without
  querying; a `FrameRing` with a pending submit **disposes completely**
  (all slots — the #107 regression test); `Swapchain.Recreate` throws
  `DeviceLost` without touching the driver.
- **Choke-point hook:** force a `DEVICE_LOST` through
  `ResultExtensions.Throw` (internal) and assert a live registered device's
  `IsLost` flips; assert a disposed/collected device doesn't keep the
  registry growing.
- **Swapchain states:** extent/image-count/compositeAlpha negotiation moves
  into internal static pure helpers (`ComputeExtent(in caps, in desc)`,
  `ComputeImageCount`, `PickCompositeAlpha`) — unit-tested without a driver,
  covering #104's `Clamp` throw and #110's zero-extent and alpha-fallback
  matrices. State transitions that need a real swapchain (OutOfDate →
  NeedsRecreate, Poisoned recovery, Minimized via zero-extent desc on the
  sentinel path) ride the existing driver-gated `SwapchainTests`
  infrastructure and skip without a driver (Windows CI runs them on
  SwiftShader). Minimized/Poisoned guard throws (`Acquire`/`Present` →
  `InvalidOperationException`) are testable with an internal state setter.
- **#112 §1:** submit-failure bookkeeping — fake a throwing submit (invalid
  recorder state) and assert `AcquireSignalPending` stays set and the next
  `BeginFrame` does not hang.

## Decisions log (for review)

1. **Managed delegate over `delegate*`** — equal AOT cleanliness, but only
   the delegate composes with instance loggers without trampoline ceremony.
2. **Static sink over per-Device** — instance-level call sites exist before
   any device; one host logger is the reality; `source` string keeps routing
   possible later.
3. **Registry-marks-all on context-free loss** — preserves the #123 choke
   point's signature and inlining budget; multi-device false positive is
   bounded to "early but safely drained teardown" because `Device.Dispose`
   never skips its real `vkDeviceWaitIdle`.
4. **`volatile bool`, set-once, no `Interlocked`** — monotonic flag; both
   race directions degrade to the driver's own `DEVICE_LOST` answer.
5. **`Fence`/`TimelineSemaphore` gain a `Device?` owner** — the only way a
   raw-pointer struct can read per-device state; #118 made it legal and
   established the pattern; struct stays two words.
6. **`IsSignaled` throws (deterministically) after loss rather than
   returning a value** — neither `true` nor `false` is truthful, and a
   `false` would turn caller polling loops into spins; the skip-the-query
   policy belongs to the pool/teardown layer, which has the context.
7. **Deferred destruction over forced `vkDeviceWaitIdle`** for the
   drain-callback Recreate path — keeping the callback meaningful is the
   feature; bounded growth, flushed at three proven-idle points; extended to
   the retired swapchain handle which has the same lifetime hazard.
8. **`Recreate` returns `SwapchainState`** — breaking (pre-1.0) but makes the
   mandatory minimize-check one expression instead of a property the caller
   forgets to read.
9. **`Suboptimal` does not set `NeedsRecreate`** — the image is presentable
   per spec; auto-flagging would push callers into recreate storms on
   rotation/HDR transitions they may deliberately ignore.
10. **All five point bugs ride this PR** — every fix lands inside functions
    this design already rewrites; separate PRs would conflict on the same
    hunks.
