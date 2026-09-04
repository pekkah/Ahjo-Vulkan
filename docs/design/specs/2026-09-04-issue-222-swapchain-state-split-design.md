# Issue #222 — `SwapchainState.Poisoned` splits into `RecreateFailed` / `SurfaceLost` / `DeviceLost`

Paired plan: `../plans/2026-09-04-issue-222-swapchain-state-split.md`

## Problem

`SwapchainState.Poisoned` (`src/Ahjo.Vulkan/Rendering/SwapchainState.cs:37-46`) is assigned from four
sites in `src/Ahjo.Vulkan/Rendering/Swapchain.cs` covering three causes whose recovery obligations are
opposite, and the value a caller reads back cannot tell them apart:

| Site | Cause | Recovery |
|---|---|---|
| `Swapchain.cs:324` | `CreateOrRecreate` threw after the old swapchain was retired (#112) | **Retry `Recreate`** — the documented recovery |
| `Swapchain.cs:438` | `VK_ERROR_SURFACE_LOST_KHR` from acquire or present | Terminal for this surface: `Dispose`, rebuild surface + swapchain |
| `Swapchain.cs:272` | `Recreate` entered with `_device.IsLost` | Terminal: tear the device down |
| `Swapchain.cs:446` | `VK_ERROR_DEVICE_LOST` from acquire or present | Terminal: tear the device down |

The member's own doc comment carries the strain in prose — it lists all three causes in one sentence
and then hedges the recovery with "(surface/device loss permitting)"
(`SwapchainState.cs:42-43`). "Permitting" is the discriminator the type does not have.

Three concrete consequences, all verified below rather than asserted:

1. **The member's factual claim is false for two of its three causes.** "No swapchain handle is
   held" (`SwapchainState.cs:40`) is true only on the `:324` route, which nulls `_handle`
   (`Swapchain.cs:321`). `MapPresentationResult` assigns `Poisoned` for `SURFACE_LOST` and
   `DEVICE_LOST` (`:438`, `:446`) and touches nothing else — the `VkSwapchainKHR` is still held and
   is still destroyed by `Dispose` (`:551-555`). The internal comment on `ThrowIfNotPresentable`
   repeats the same wrong claim ("in Poisoned no handle is held at all", `:456`).
2. **`ThrowIfNotPresentable` cannot say what recovery it wants.** Its `Poisoned` message is one
   string enumerating all three causes and offering both recoveries at once: *"a Recreate failed, or
   the surface/device was lost. Call Recreate to attempt a from-scratch create, or Dispose."*
   (`:465`). For a lost surface the first half of that sentence is advice to do the one thing that
   cannot work.
3. **Every windowed sample pays for it in bookkeeping.** Four samples carry a sticky `bool
   presentable` alongside the state the wrapper already tracks, plus a duplicated report-and-break on
   `AcquireResult.SurfaceLost` at both the acquire and the present site
   (`HelloTriangle/Program.cs:110,153,224`; `HelloCube/Program.cs:345,404,511`;
   `HelloVmaWindowed/Program.cs:277,319,426`; `HelloDlaa/Program.cs:384,435,699`).

This was recorded as the one rejected alternative with merit while fixing #220
(`docs/design/specs/2026-09-04-issue-220-sample-swapchain-states-design.md:291-295`).

## Evidence

### Consumers of `SwapchainState`, counted

A repo-wide audit (`src/`, `samples/`, `tests/`) finds the enum named in exactly three places outside
its own file and `Swapchain.cs`:

- **Four samples**, all with the same shape: `HelloTriangle/Program.cs:302`,
  `HelloCube/Program.cs:738`, `HelloVmaWindowed/Program.cs:539`, `HelloDlaa/Program.cs:823` — each a
  `return state == SwapchainState.Ready;` at the end of a private `TryRecreate` helper. No sample
  ever names `Poisoned`.
- **`tests/Ahjo.Vulkan.Tests/SwapchainTests.cs`** — `:387`, `:405`, `:433`, `:443`, `:473`. Two
  tests name `Poisoned`: `AcquireAndPresent_InMinimizedOrPoisoned_Throw` (`:433`, iterating
  `Minimized` and `Poisoned` through the `OverrideStateForTesting` seam, `Swapchain.cs:561`) and
  `Recreate_AfterDeviceLoss_ThrowsAndPoisons` (`:473`).
- **`src/Ahjo.Vulkan/Rendering/AcquireResult.cs`** — the handling table added by #221 names
  `SwapchainState.Poisoned` in the `SurfaceLost` row (`:50`) and in the `SurfaceLost` member doc
  (`:88-97`).

Nothing else. `docs/` mentions the enum only inside the #120 and #220 spec/plan pairs; `README.md`,
`docs/migration-vortice-to-ahjo.md` and every other shipped doc are silent. No `Generated/` file and
no `tools/*.rsp` references it — this is hand-written wrapper surface, so no codegen change is
implied.

### What the sticky `presentable` flag actually does

It has two jobs, and only one of them is the enum's fault.

**Job 1 — the sticky loop-top re-entry term.** The comment above the flag says why it exists: while
minimized, `resized` is false and `swap.Extent` still equals the window size, so "every term of the
test below therefore goes false … and without this flag the loop would fall straight through to
`AcquireNextImage` and throw" (`HelloVmaWindowed/Program.cs:270-276`, and the same block in the other
three). This term is a hand-rolled `swap.State != SwapchainState.Ready`.

**Job 2 — gating dependent rebuilds on the success leg.** `HelloCube` recreates its depth buffer only
when `TryRecreate` returned true (`:378-380`, `:499-503`, `:527-531`) and `HelloDlaa` sets
`rebuildPending` the same way (`:407`, `:431`, `:711`). That is the *return value* of one call, used
immediately — a local, not sticky state, and unaffected by this change.

**Honest reading of "would retry forever".** The issue says a loop written
`if (swap.State != Ready) TryRecreate(...)` retries forever over a lost surface. That is right about
the shape but only reachable in these samples because they break out at the acquire/present sites
first: the safety today is an emergent property of two checks in two places, not a local property of
the guard. And it is *not* certain the failure mode is a spin: `Recreate` calls
`vkGetPhysicalDeviceSurfaceCapabilitiesKHR(...).ThrowIfFailed()` before anything else
(`Swapchain.cs:281-283`), and the spec allows that query to return `VK_ERROR_SURFACE_LOST_KHR`, in
which case the retry throws out of the sample instead of spinning. **Which of the two happens is
driver-dependent and we cannot test it** — surface loss is not provocable in CI, and wrapper tests
are Windows-only anyway (#32). Both outcomes are wrong; neither is the documented recovery. This
uncertainty is recorded rather than resolved.

### `Recreate` can never be the recovery from surface loss

`Recreate` rejects a description carrying a different `Surface`:

```csharp
if (desc.Surface.Handle != _surface.Handle)
    throw new ArgumentException("Recreate must use the same Surface this Swapchain was constructed with.", nameof(desc));
```
(`Swapchain.cs:263-266`)

So after `VK_ERROR_SURFACE_LOST_KHR` the only surface `Recreate` will accept is the dead one. Surface
recovery necessarily means a new `Surface` and therefore a new `Swapchain`. This is not a heuristic —
it is unconditional from the wrapper's own precondition, and it is what makes a fail-fast on this
state honest rather than presumptuous.

### `Device.IsLost` is a near-miss discriminator, not a substitute

`Device.IsLost` is set-once and is deliberately over-broad: a `DEVICE_LOST` observed at the
context-free `ResultExtensions` choke point marks *every* live device because the throw site carries
no device identity (`ResultExtensions.cs:93-101`, `Device.cs:98-106`). So in a multi-device process
`_device.IsLost` can read true for a swapchain whose own device is healthy. Using it to answer "why
is this swapchain poisoned?" therefore mis-attributes a `RecreateFailed` as a device loss in exactly
the configuration the wrapper documents as approximate. Separately: after this split, `RecreateFailed`
and `DeviceLost` would be the only two remaining candidates for a `Poisoned` value, and folding
device loss into either would make the member's documented recovery a lie (retry is legal for one and
not the other). There is no honest place to put it except its own member.

### `AcquireResult` already distinguishes at the call site — and that is the whole problem

`MapPresentationResult` (`Swapchain.cs:426-452`) returns `AcquireResult.SurfaceLost` *and* sets the
state. A caller can therefore learn the cause exactly once, at the instant of the call, and must
persist it themselves if it is to survive to the next loop iteration. That persistence is the sticky
flag. Moving the fact into the state is moving it from the caller's memory into the object that owns
it.

### Hot paths and benchmarks

`Rendering/` **is** covered by the zero-per-frame-allocation rule. The directory list in
`src/Ahjo.Vulkan/CLAUDE.md:34-37` (`Recording/`, `Sync/`, `Pools/`, `Memory/`) is not the boundary —
`:38-40` extends it to "any other API expected to run inside a per-frame loop" and states outright
that "the rule follows the *call frequency*, not the directory". `Swapchain.AcquireNextImage` and
`Swapchain.Present` run once per frame, so they carry the obligation in full. The only per-frame code
this design touches is the guard test in `ThrowIfNotPresentable`, which stays one branch over
constant enum values (it folds to a single unsigned `> 1` compare), with message construction moved
behind a cold `NoInlining` helper — the shape `ResultExtensions` already uses (`:89-90`).

There is nonetheless no benchmark to run, and the reason is *not* that the directory is exempt:
there is no `SwapchainBenchmarks` class in `tests/Ahjo.Vulkan.Benchmarks/` (24 benchmark files, none
touching `Swapchain`) because the harness is headless. Benchmarking acquire/present needs a surface,
a window and a message pump, and what it would then measure is compositor pacing, not wrapper
allocation. Allocation coverage for this change is the code shape above plus review, not a row in
`docs/benchmarks.md`.

### Versioning surface

There is **no `CHANGELOG.md`** in the repo and no `PublicAPI.*.txt` baseline; releases are cut with
`gh release create vX.Y.Z --generate-notes` (`.github/workflows/publish.yml:22-25`), i.e. release
notes are assembled from PR titles. The latest tag is `v0.9.0`. So the repo currently has *no*
mechanism that records a breaking change other than the PR body and specs like this one — and #120
already broke this same area pre-1.0 by changing `Recreate`'s return type from `void`
(`docs/design/specs/2026-06-12-issue-120-device-loss-design.md:401`).

## Decision

Replace `SwapchainState.Poisoned` with three members, and make the two terminal ones enforceable
rather than advisory.

### D1 — the enum

```csharp
public enum SwapchainState
{
    Ready,          // 0, unchanged
    NeedsRecreate,  // 1, unchanged
    Minimized,      // 2, unchanged
    RecreateFailed, // 3 — was Poisoned; retry Recreate (#112)
    SurfaceLost,    // 4 — terminal for this surface; Dispose, rebuild Surface + Swapchain
    DeviceLost,     // 5 — terminal; tear down the device
}
```

Ordering is deliberate: recoverable states first, terminal states last, and the three unchanged
members keep their numeric values. `RecreateFailed` inherits `Poisoned`'s ordinal `3`. That is a
silent hazard for an assembly compiled against the old enum (the inlined constant `3` would now mean
something narrower), accepted here because the source break is unavoidable, the repo keeps no binary
baseline, and pre-1.0 is the window for it.

`SwapchainState.SurfaceLost` deliberately shares its simple name with `AcquireResult.SurfaceLost`.
The parallel is the point: `AcquireResult.SurfaceLost` is what the call reports, `SwapchainState.SurfaceLost`
is what the object remembers.

### D2 — device loss gets its own member

Answering the issue's first open question: **yes**. Not because `Device.IsLost` is unavailable, but
because (a) after the split there is no member left that could hold device loss without
mis-documenting its recovery, and (b) `Device.IsLost` is documented as conservatively over-broad in
multi-device processes (`Device.cs:98-106`), so it cannot be the authority on *this swapchain's*
cause of death. `Device.IsLost` remains the process-wide policy flag it was designed to be; the
swapchain state records the swapchain's own history.

### D3 — the terminal states become enforceable, not just documented

`Recreate` gains a fast-fail on `SurfaceLost`, mirroring the existing `_device.IsLost` fast-fail at
`Swapchain.cs:270-274`, and throwing the same *kind* of exception (a cached
`VulkanException(VK_ERROR_SURFACE_LOST_KHR)` through a new `ResultExtensions.ThrowSurfaceLost()`,
mirroring `ThrowDeviceLost()` at `ResultExtensions.cs:112-121`). Rationale: without this, "terminal"
is once again a claim only the doc comment makes, which is the alternative this issue exists to
reject; and per the evidence above, `Recreate` after surface loss is *unconditionally* futile because
it refuses any other `Surface`. A single exception type across the two neighbouring terminal causes
keeps a caller's `catch (VulkanException)` around `Recreate` covering both.

`Recreate` from `RecreateFailed` stays legal and unchanged — that is the #112 recovery and the reason
the split exists.

D3 is separable: if a reviewer wants `Recreate` to keep its current permissive contract, dropping it
costs one plan step and two sentences of enum doc, and leaves D1/D2/D4 intact.

### D3a — a failure *inside* `Recreate` is classified by its `VkResult`, not by "something threw"

Added after review. The fast-fail above only fires on a swapchain that has **already** reached
`SurfaceLost`, and the only route that sets it there is acquire/present. But
`VK_ERROR_SURFACE_LOST_KHR` is a documented return code of every surface query `Recreate` makes —
`vkGetPhysicalDeviceSurfaceCapabilitiesKHR`, `…SurfaceFormatsKHR`, `…SurfacePresentModesKHR`,
`…SurfaceSupportKHR` — and of `vkCreateSwapchainKHR` itself. The likeliest sequence by which a real
surface loss is first observed is therefore *driver restart → `OUT_OF_DATE` from present → the app
calls `Recreate` → the capability query returns `SURFACE_LOST`*. Assigning `RecreateFailed` on "the
`try` block threw" files that under the one state whose documented recovery is *retry*, makes D3's
fast-fail unreachable in practice, and turns the samples' loop-top terminal guard into dead code.

So every failure region inside `Recreate` derives the state from the exception's `VkResult`:
`VK_ERROR_SURFACE_LOST_KHR` → `SurfaceLost`, `VK_ERROR_DEVICE_LOST` → `DeviceLost`, anything else →
a per-site fallback. One `ClassifyFailure(Exception, SwapchainState fallback)` helper expresses all
three, and the fallback is chosen by **what has been destroyed at that point**:

| Region | Destroyed so far | Fallback |
|---|---|---|
| Pre-drain capability query | nothing | the current state — leave it alone |
| The drain (`vkDeviceWaitIdle`, or the caller's `syncBeforeDestroy` callback) | nothing | the current state — leave it alone |
| The create (`CreateOrRecreate` + the views) | the old swapchain has been retired | `RecreateFailed` |

The drain is a separate region rather than part of the create's `try` because that block's cleanup
dereferences `old`/`oldSems`, which are not captured until after the drain returns. It earns its own
catch on merit too: `vkDeviceWaitIdle` documents `VK_ERROR_DEVICE_LOST`, and the intended callback
argument `FrameRing.WaitForInFlightFences` throws the same code out of its fence wait
(`src/Ahjo.Vulkan/Pools/FrameRing.cs:315-317`) — so without it a real device loss would leave the
state `Ready`, `ThrowIfNotPresentable` would accept, and the next `AcquireNextImage` would issue
`vkAcquireNextImageKHR` against a dead device. Self-correcting (`Device.IsLost` is set on both
routes) but a contract gap, and neither call can return `VK_ERROR_SURFACE_LOST_KHR`, so the
surface-lost path is unaffected either way.

**Consequence for D1's doc text.** This makes `SurfaceLost` and `DeviceLost` reachable with
`_handle == null`, so neither member may claim the handle is still held. Nor may `RecreateFailed`
claim to be the only handle-less state: a `Swapchain` constructed over a zero-extent surface returns
`Minimized` from `CreateOrRecreate` *before* `vkCreateSwapchainKHR`, which is the explicitly
supported "app launched minimized" case (#110). The honest formulation, and the one the enum doc
carries, is that **whether a handle is held is a property of where a state was entered, not of the
state** — and that `Dispose` is legal from every state and cleans up whatever remains
(`Swapchain.cs` null-checks the handle before destroying it).

State that rule and *stop*. It is tempting to make it concrete by adding "…and a `Recreate` that
throws has already retired the old handle", but that is false in the other direction: the two
fast-fails and the two pre-drain regions in the table above all throw with `_handle`, `_views` and
`_renderingDone` intact, so `SurfaceLost` and `DeviceLost` are each reachable with a **live** handle
as well as a null one. A caller who believed the retired-handle version would leak a
`VkSwapchainKHR` plus its views and trip `VUID-vkDestroySurfaceKHR-surface-01266` at teardown. The
route-independent sentence is not vague — it is the only form that is true from every entry.

### D4 — `ThrowIfNotPresentable` inverts and gets one message per state

The guard becomes `if (_state is not (SwapchainState.Ready or SwapchainState.NeedsRecreate))` — a
new state is non-presentable by default, which is the safe direction — and the message becomes a
switch over constant strings, each naming its own state and its own recovery, evaluated only in the
cold `NoInlining` throw helper. This is what dissolves the three-causes-in-one-string problem
(`Swapchain.cs:465`).

### D4a — the two-argument `Present` runs the guard before it indexes

Added after review. `Present(Queue, uint)` forwarded as
`Present(queue, imageIndex, in _renderingDone[imageIndex])`, which indexes the per-image semaphore
array *before* the callee's `ThrowIfNotPresentable` runs. In every shape that array is empty —
`Recreate`'s failure path clears it, and a construction-time `Minimized` never allocates it — so the
real `RecreateFailed` swapchain threw `IndexOutOfRangeException`, not the `InvalidOperationException`
D4's message table exists to deliver. The guard (plus the null and disposed checks, so the exception
precedence for a null queue is unchanged) is hoisted into the forwarding overload. The forwarded call
re-runs the same checks: three predictable, allocation-free branches, which is the right trade
against handing the caller the wrong exception type on the one path that matters.

### Consequences for the samples

Verified against the real code, not assumed. All four windowed samples:

- **drop the sticky `bool presentable`** and read `swap.State != SwapchainState.Ready` at the loop
  top. This is a strict superset of the old term: it also catches a `NeedsRecreate` that reached the
  loop top, which is currently unreachable (every state-mutating `AcquireResult` is followed
  immediately by `TryRecreate`) but is the correct handling if it ever becomes reachable.
- **collapse the two `ReportSurfaceLost(); break;` blocks into one loop-top terminal guard**
  (`if (swap.State is SwapchainState.SurfaceLost) { ReportSurfaceLost(); break; }`, placed *before*
  the recreate guard). A `SurfaceLost` from acquire falls into the existing
  `if (acq != AcquireResult.Success) { …; continue; }` catch-all; a `SurfaceLost` from present
  matches no branch and reaches the loop top on the next iteration. Both land on the single guard.
  This is the payoff: the two-checks-in-two-places safety property becomes one check in one place.
- **keep their `TryRecreate` helper and its `bool` return** — `HelloCube`'s depth-buffer rebuild
  (`:378-380`, `:499-503`, `:527-531`) and `HelloDlaa`'s `rebuildPending` (`:407`, `:431`, `:711`)
  legitimately need "did *this* recreate succeed" as an immediate local. Job 2 above is not the
  enum's problem and does not go away.
- **absorb `VK_ERROR_SURFACE_LOST_KHR` inside `TryRecreate` and return `false`** (added after
  review, and required by D3a). Without it the loop-top guard is only two-thirds of the promise: a
  surface loss first observed *inside* `Recreate` — per D3a the likeliest route there is — unwinds
  out of `Main` with a stack trace instead of reaching the guard, so `swap.State` would be the
  authority for the acquire and present routes but not for the third. The catch is a narrow
  `when (e.Result is VkResult.VK_ERROR_SURFACE_LOST_KHR)` filter, not a blanket
  `catch (VulkanException)`: the state it absorbs is the one the frame loop knows how to act on, and
  every other code — device loss included — still terminates the sample loudly, which is the
  existing documented policy (the loop-top guard has no `DeviceLost` branch precisely because device
  loss throws out of acquire, present and `Recreate` alike). Returning `false` also keeps
  `HelloCube`'s depth rebuild and `HelloDlaa`'s `rebuildPending` on the success leg, where they
  belong.

Net per sample: one field removed, one duplicated block removed, one guard added, one narrow catch.

### Why not the alternatives

- **Document the distinction only (leave the enum alone).** Rejected: the three-causes-in-one-string
  message at `Swapchain.cs:465` *is* the documentation attempt, and the false "no swapchain handle is
  held" claim at `SwapchainState.cs:40` shows what happens when one member has to describe three
  situations. Prose cannot be read by an `if`.
- **Expose `bool CanRecreate` (or `Swapchain.IsTerminal`) and keep `Poisoned`.** Rejected: it answers
  the retry question but throws away the cause, so `ThrowIfNotPresentable` still cannot produce a
  correct message, a caller still cannot distinguish "rebuild the surface" from "tear down the
  device", and the object grows a second source of truth that must stay consistent with `State`.
- **Split into `RecreateFailed` + `SurfaceLost` only, folding device loss in.** Rejected: there is no
  honest member left to fold it into — `RecreateFailed` would advertise a legal retry and
  `SurfaceLost` would name the wrong resource. Keeping `Poisoned` as the device-lost bucket preserves
  the exact name whose vagueness is the defect.
- **Keep `Poisoned` as an `[Obsolete(error: true)]` alias of `RecreateFailed`.** Rejected: an enum
  alias makes `ToString()`/`Enum.GetNames` ambiguous, the source break is forced anyway (the
  semantics of the old member do not survive), and the repo has broken this area pre-1.0 before
  (#120 changed `Recreate`'s return type).
- **Add a `SwapchainStateExtensions.IsTerminal()` / `IsRecoverable()` helper alongside the split.**
  Rejected *for now*, not on principle: with one consumer shape in the repo (four samples that all
  need the specific cause anyway, to pick a message and an exit path) a membership helper would be a
  second way to ask a question the samples do not actually ask. Worth adding when a second consumer —
  most plausibly Logos (#68) — wants the predicate without the cause.
- **A new `docs/swapchain-lifecycle.md`.** Rejected for the same reason #220 rejected it
  (`…issue-220-…-design.md:296-297`): a third home for a table that belongs on the enum.

## Cross-links

- **Resolves** #222.
- **Follows up** #220 / PR #221, where this was the one rejected alternative rated as having merit
  (`docs/design/specs/2026-09-04-issue-220-sample-swapchain-states-design.md:291-295`,
  `:346-347`).
- **Must land consistently with** #120 (`docs/design/specs/2026-06-12-issue-120-device-loss-design.md`):
  its transition table rows `:263-267` are the normative description of the four assignment sites and
  are superseded, per-row, by D1/D2. The `Device.IsLost` single-policy-flag decision (`:265`) is
  preserved unchanged.
- **Preserves** #112 (recreate-failure retry) and #110 (`Minimized`) semantics verbatim; both keep
  their own members.
- **Constrained by** #32: no CI lane can provoke surface loss or device loss on real hardware, so the
  new states are exercised through the existing `OverrideStateForTesting` seam
  (`Swapchain.cs:561`) on Windows only.
- **Versioning**: pre-1.0 breaking change to public API with no `CHANGELOG.md` and no PublicAPI
  baseline in the repo to record it. See the OPEN item in the plan.

## Uncertainty recorded

1. Whether a `Recreate` over a lost surface today spins or throws is driver-dependent
   (`vkGetPhysicalDeviceSurfaceCapabilitiesKHR` may or may not report `VK_ERROR_SURFACE_LOST_KHR`)
   and is not testable here. The design does not depend on which it is — D3 makes it deterministic.
2. Whether any out-of-repo consumer (Logos, #68) reads `SwapchainState.Poisoned` today is unknown
   from inside this repository. The rename is a compile error there, not a silent behaviour change,
   which is the failure mode we want.
