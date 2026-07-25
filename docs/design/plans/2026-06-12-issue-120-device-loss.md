# Implementation plan — diagnostics sink + device-loss / swapchain state (#120)

Paired with `../specs/2026-06-12-issue-120-device-loss-design.md`.
Static `AhjoDiagnostics.Sink` managed delegate; `Device.IsLost` volatile flag
fed from the `ResultExtensions` choke point + the non-throwing `DEVICE_LOST`
returns; `SwapchainState` machine absorbing #104/#107/#110/#111/#112.

## Step 1 — `AhjoDiagnostics` (new `src/Ahjo.Vulkan/Diagnostics/AhjoDiagnostics.cs`)

- `DiagnosticSeverity` enum, `DiagnosticSink` delegate, static
  `AhjoDiagnostics` class per the spec: `volatile` field, non-null setter,
  `internal static Write(...)`, `DefaultSink` writing `message` to
  `Console.Error`. Expose `public static DiagnosticSink DefaultSink` (or a
  `ResetToDefault()` helper) so tests/hosts can restore.
- Convert the five `Console.Error` call sites (message text unchanged):
  - `Lifecycle/Device.cs:466` → `Warning`, source `"Device"`.
  - `Lifecycle/Device.cs:266` → `Warning`, source `"PipelineCache"`.
  - `Memory/Allocator.cs:254` → `Warning`, source `"Allocator"`.
  - `Pools/FrameRing.cs:385` → `Warning`, source `"FrameRing"`.
  - `Lifecycle/Instance.cs:549` (`DefaultCallback`) → severity mapped from
    `VkDebugUtilsMessageSeverityFlagBitsEXT` (ERROR→Error, WARNING→Warning,
    else Info), source `"Vulkan"`; keep the `try/catch` + `Debugger.Break`.
- Update `src/Ahjo.Vulkan/README.md:62-68` + the
  `LoadOrCreatePipelineCache`/`Allocator` XML docs to name
  `AhjoDiagnostics.Sink` (stderr default).

## Step 2 — `Device.IsLost` + registry

- `Lifecycle/Device.cs`:
  - `private volatile bool _lost;` + `public bool IsLost => _lost;` +
    `internal void MarkLost() => _lost = true;`.
  - `private static readonly List<WeakReference<Device>> s_live = [];` with a
    private static lock object; register in ctor, unregister in `Dispose`
    (prune dead entries on both).
  - `internal static void NotifyDeviceLossObserved()` — under the lock, mark
    every live target lost, prune the rest.
  - `Dispose`: keep the unconditional `vkDeviceWaitIdle`; its failure log now
    goes through the sink (Step 1).
- `Internal/ResultExtensions.cs` `Throw`: in the
  `VK_ERROR_DEVICE_LOST` arm call `Device.NotifyDeviceLossObserved()` before
  throwing the cached exception. (Cold path — already `NoInlining`.)

## Step 3 — Sync structs gain the owner reference and the fast paths

- `Sync/Fence.cs`:
  - Replace `internal readonly VkDevice_T* DeviceHandle` with
    `internal readonly Device? Owner`; internal ctor takes `Device?`;
    call sites use `Owner.Handle`. `FromRaw` → `null` owner (borrowed guard
    message unchanged — `ThrowIfBorrowed` now checks `Owner is null`).
  - `Wait`: after the borrow guard, `if (Owner.IsLost) return
    WaitState.DeviceLost;`; when the real wait maps to `DeviceLost`, call
    `Owner.MarkLost()` before returning.
  - `IsSignaled`: after the borrow guard, `if (Owner.IsLost)` throw via
    `ResultExtensions` (cached `DeviceLost`); on a driver-returned
    `DEVICE_LOST`, mark + throw.
  - `Reset`: no fast path (choke point covers it).
- `Sync/TimelineSemaphore.cs`: same owner-field swap; `WaitFor` gets the same
  fast path + mark-on-`DeviceLost`; `Value`/`Signal` unchanged (choke point).
- `Pools/FencePool.cs`:
  - `Acquire` constructs `new Fence(raw, _device)`.
  - `Release(Fence)`: `if (_device.IsLost) { bucket = _freeUnsignaled; }`
    skipping `IsSignaled` (doc comment: bucket immaterial pre-`Dispose`; #107).
    Keep the `knownSignaled` overload as-is.
- `Pools/SemaphorePool.cs` `AcquireTimeline` → pass `_device`.
- Audit remaining `new Fence(`/`new TimelineSemaphore(` sites (`rg`) — adjust
  ctor args.

## Step 4 — Swapchain state machine (`Rendering/Swapchain.cs`)

- New `Rendering/SwapchainState.cs` enum (XML docs per spec table).
- `private SwapchainState _state;` + `public SwapchainState State => _state;`.
- Extract internal static pure helpers for testability:
  `ComputeExtent(in VkSurfaceCapabilitiesKHR, uint descW, uint descH)` (returns
  the extent; zero ⇒ minimized), `ComputeImageCount(in caps, uint preferred)`
  (#104: `maxClamp = caps.maxImageCount == 0 ? uint.MaxValue : ...`),
  `PickCompositeAlpha(uint supported)` (#110 §2: OPAQUE else lowest set bit).
- `CreateOrRecreate` reordered: caps query → extent compute → **zero-extent
  early-out returning `Minimized` before any destroy/create** → rest as today
  using the helpers.
- ctor: zero extent ⇒ `_state = Minimized`, `_handle = null`, no create.
- `Recreate(in desc, callback)` returns `SwapchainState`:
  1. disposed/surface checks as today; `if (_device.IsLost)` throw cached
     `DeviceLost` (state → `Poisoned`).
  2. caps + extent first; zero ⇒ `_state = Minimized; return` (nothing
     touched).
  3. drain (callback or `vkDeviceWaitIdle`).
  4. `try { DestroyViews(); CreateOrRecreate(desc, old); }`
     `catch { /* old retired by spec */ destroy old; _handle = null;
     DiscardRenderingDoneSemaphores(); _state = Poisoned; throw; }`
  5. success: old handle + old semaphores → destroy immediately when the
     drain was `vkDeviceWaitIdle`, else push onto the retire list (#111 §1);
     `_state = Ready`.
- Retire list: `private readonly List<(nint Swapchain, BinarySemaphore[] Sems)>
  _retired = [];` + `FlushRetired()` called from `Dispose` and from any
  `Recreate` that used the wait-idle drain (after the drain). The old
  per-image semaphores move to the list **instead of**
  `DiscardRenderingDoneSemaphores` on the callback path; fresh ones are
  allocated as today.
- `AcquireNextImage`/`Present`:
  - leading `_state is Minimized or Poisoned` ⇒ `InvalidOperationException`
    naming state + recovery.
  - `OutOfDate` ⇒ `_state = NeedsRecreate`; `SurfaceLost` ⇒ `Poisoned`;
    `DEVICE_LOST` arm ⇒ `_device.MarkLost(); _state = Poisoned;` then the
    existing throw. `Suboptimal` leaves state untouched.
- Poisoned recovery: `Recreate` with `_handle == null` runs the fresh-create
  path (`oldSwapchain: null`).

## Step 5 — Frame-loop bookkeeping (#112 §1, #111 §2)

- `Rendering/FrameContext.cs`: in both `Submit` bodies move
  `Slot.MarkSubmitted()` (and `MarkAcquireWaitConsumed()` in the
  swapchain-aware one) to **after** `queue.Submit2` returns; update the
  comment to record why (a throwing submit must not arm the fence wait or
  un-flag the acquire signal).
- `Pools/FrameRing.cs` `Slot.Dispose`: when `AcquireSignalPending`, call
  `vkDeviceWaitIdle` (ignore result; sink-log non-success) before
  `SemaphorePool.Release(ImageAcquired)` (#111 §2). Existing stderr line →
  sink (Step 1).

## Step 6 — Tests (`tests/Ahjo.Vulkan.Tests/`)

- New `DiagnosticsSinkTests.cs`: capture-sink swap/restore (`try/finally` —
  the sink is process-global; mark the class as a non-parallel collection);
  null-set throws; Allocator-leak + pipeline-cache-mismatch messages captured
  (driver-gated); concurrent set/write smoke.
- New `DeviceLossTests.cs` (driver-gated, no real loss needed —
  `device.MarkLost()` via `InternalsVisibleTo`): `Fence.Wait` returns
  `DeviceLost` immediately on a never-signaled fence (would hang otherwise —
  assert with a short stopwatch bound); `IsSignaled` throws with
  `Result == VK_ERROR_DEVICE_LOST`; `FencePool.Release` completes; full
  `FrameRing` with a pending submit disposes all slots (#107 regression);
  `Swapchain`-less `Recreate` guard via `DeviceLossTests` +
  `SwapchainTests` where a surface exists; registry: internal
  `ResultExtensions.Throw`-path test asserting the flag flips and disposed
  devices unregister.
- `SwapchainTests.cs` additions: pure-helper matrices for
  `ComputeExtent` (zero/sentinel/clamp), `ComputeImageCount` (#104 repro:
  preferred=1, min=2, max=0 → 2, no throw), `PickCompositeAlpha`
  (OPAQUE-present, OPAQUE-absent → lowest bit); state transitions with a
  real swapchain where the driver allows; Minimized/Poisoned
  `Acquire`/`Present` guard throws via internal state setter.
- `FrameRingTests.cs`: #112 §1 — throwing submit leaves
  `AcquireSignalPending == true` and next `BeginFrame` returns without
  waiting on the un-armed fence.

## Step 7 — Benchmarks + docs

- `Sync_HostOps_RoundTrip` (`SyncPoolBenchmarks`) already exercises
  `Fence.IsSignaled`/`Reset` + timeline ops — now with the volatile-read
  branch in front. `FrameRingBenchmarks` covers `BeginFrame`'s wait path.
  Recapture both on the Windows host; every `Allocated` cell stays `-`.
  No new benchmark class needed (no new per-frame API); add a note to
  `docs/benchmarks.md` that the #120 loss-flag branch is covered by these
  two canaries.
- `docs/` — if a migration note file exists for breaking changes, record:
  `Recreate` now returns `SwapchainState`; `Fence`/`TimelineSemaphore`
  internal ctor shape changed (internal — no consumer impact); diagnostics
  now route through `AhjoDiagnostics.Sink`.

## Step 8 — Verify + review cycle

- `dotnet build Ahjo.Vulkan.slnx` (warnings = errors) + `dotnet test`
  (GPU-gated tests skip without a driver; Windows CI runs them on
  SwiftShader).
- `dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter
  "*SyncPool*"` → `Allocated = -` (plus `*FrameRing*` where a driver exists).
- `vulkan-validation-reviewer` + `bench-coverage-checker` over the final diff.
- PR references #120, closes #104/#107/#110/#111/#112.

## Risk notes

- **`Fence` ctor signature is internal but widely constructed** — `rg "new
  Fence\(" src/ tests/` before assuming only `FencePool` builds them.
- **Registry must never strongly root a `Device`** — `WeakReference` +
  prune; otherwise the leak-backstop finalizer can never run.
- **Sink is process-global state in tests** — capture/restore with
  `try/finally` and isolate in a non-parallel xUnit collection, or parallel
  test runs will cross-talk.
- **`Recreate` return-type change** ripples into samples
  (`HelloVmaWindowed`, etc.) — `rg "\.Recreate\(" samples/ tests/` and update
  call sites (ignoring the return stays legal, so most sites compile as-is).
- **Zero-extent early-out must precede the drain** or a minimized window
  still pays a `vkDeviceWaitIdle` per frame while minimized.
