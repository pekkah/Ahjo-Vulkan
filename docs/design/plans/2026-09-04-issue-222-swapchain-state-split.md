Paired with ../specs/2026-09-04-issue-222-swapchain-state-split-design.md

# Plan — issue #222: split `SwapchainState.Poisoned`

Branch: `issue-222-swapchain-state-split`. Ten steps. Steps 1-4 are the wrapper, 5 the samples,
6 the tests, 7-10 verification and wrap-up. Nothing under `Generated/`, `native/` or `tools/` is
touched; this is hand-written wrapper surface only.

---

## 1. `src/Ahjo.Vulkan/Rendering/SwapchainState.cs` — replace `Poisoned` with three members

Delete the `Poisoned` member (`:37-46`). Add three members **in this order**, after `Minimized`:

```csharp
    RecreateFailed,
    SurfaceLost,
    DeviceLost,
```

Leave `Ready`, `NeedsRecreate` and `Minimized` — declarations and docs — byte-identical.

Doc comments to write (content, not literal text; match the file's existing `<see cref="…"/>`
density and line width):

> **Corrected after review.** The three bullets below originally asserted handle lifetimes per
> member — "no handle is held, and this is the only member of which that is true" for
> `RecreateFailed`, "the handle **is** still held" for the two terminal members. All three are false
> on reachable routes (see spec D3a), and the text as shipped states only what holds. The bullets are
> kept, corrected, so the plan and the code agree.

- **`RecreateFailed`** — a `Swapchain.Recreate` threw after the old swapchain was retired (#112) for
  a reason other than the two terminal ones. **No swapchain handle is held** (`Swapchain.cs:321`
  nulls `_handle`) — but do **not** claim this is the only such state: a `Swapchain` constructed over
  a zero-extent surface sits in `Minimized` with nothing ever created (#110), and the two terminal
  members are reachable both ways (see the next paragraph). **Retry is the
  documented recovery** — call `Recreate` again; it runs a from-scratch create with
  `oldSwapchain = null`. Acquire/present throw `InvalidOperationException`.
- **`SurfaceLost`** — `VK_ERROR_SURFACE_LOST_KHR` was observed for this swapchain's `Surface`: either
  reported by `AcquireNextImage` / `Present` (returned as `AcquireResult.SurfaceLost`, without
  throwing) **or thrown out of `Recreate`**, where it is a documented return code of every surface
  query and of `vkCreateSwapchainKHR` — and where it is in fact the likeliest first observation
  (spec D3a). **Terminal for this surface.** Say why in one sentence citing the wrapper's own
  precondition: `Recreate` accepts only the `Surface` the swapchain was constructed with
  (`Swapchain.cs:263-266`), and that surface is gone — so `Recreate` throws `VulkanException` with
  `VK_ERROR_SURFACE_LOST_KHR` rather than pretending. Recovery: `Dispose` this swapchain, dispose and
  rebuild the `Surface`, construct a new `Swapchain`. Do **not** claim the `VkSwapchainKHR` handle is
  still held — entered via `Recreate` it is not; say `Dispose` is legal and destroys whatever remains.
- **`DeviceLost`** — the device was lost, observed by `Recreate`'s fast-fail on `Device.IsLost`
  (`Swapchain.cs:270-274`), by `VK_ERROR_DEVICE_LOST` out of a call `Recreate` itself makes, or by
  the same code from acquire/present (`:444-448`). All three throw. **Terminal.** Recovery is the
  `Device.IsLost` policy: dispose every dependent resource, dispose the device, rebuild from a fresh
  `PhysicalDevice` — link `<see cref="Device.IsLost"/>` rather than restating it. Same handle caveat
  as `SurfaceLost`. Add the one caveat that justifies the member existing: `Device.IsLost` is
  deliberately over-broad in a multi-device process (`Device.cs:98-106`), so it is not a substitute
  for reading this state.

State the handle rule **once**, on the enum's type-level summary, in the form that is actually true:
whether a `VkSwapchainKHR` is held is a property of *where* a state was entered, not of the state,
and `Dispose` is legal from every state and cleans up whatever remains.

**Do not add a concrete-sounding clause to that rule.** "…and a `Recreate` that throws has already
retired the old handle" is as false as the claims it replaced, in the opposite direction: `Recreate`
has five throw sites and three of them — the `Device.IsLost` fast-fail, the `SurfaceLost` fast-fail
and the two pre-drain catches of step 3c-bis — throw with `_handle`, `_views` and `_renderingDone`
intact, so both terminal members are reachable with a **live** handle. A caller acting on the
retired-handle version leaks a `VkSwapchainKHR` plus its views and trips
`VUID-vkDestroySurfaceKHR-surface-01266` at teardown. The route-independent sentence is the whole
rule; anything appended to it narrows it into a falsehood.

Also update the enum's type-level summary (`:3-7`): it currently credits #120 with making
"minimized" and "recreate failed" legal states. Extend the sentence to name the terminal pair and
#222, and state the invariant a reader needs: **`Ready` and `NeedsRecreate` are the only presentable
states; `RecreateFailed` is recoverable by `Recreate`; `SurfaceLost` and `DeviceLost` are terminal
and only `Dispose` is legal.**

## 2. `src/Ahjo.Vulkan/Internal/ResultExtensions.cs` — cached surface-lost throw

Add, next to the existing cached instances (`:26-33`):

```csharp
    private static readonly VulkanException SurfaceLost =
        new(VkResult.VK_ERROR_SURFACE_LOST_KHR, "vulkan call");
```

and, next to `ThrowDeviceLost` (`:112-121`):

```csharp
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowSurfaceLost() => throw SurfaceLost;
```

with a short doc comment in the same shape as `ThrowDeviceLost`'s, naming its single call site
(`Swapchain.Recreate`).

**Do not** add `VK_ERROR_SURFACE_LOST_KHR` to the `Throw` switch at `:103-108`. That switch is the
generic failure path and its cached arm would strip the `[CallerMemberName]` from every surface-lost
throw in the wrapper — out of scope and a regression in diagnostics. The cached instance here is
used only by `ThrowSurfaceLost()`.

Extend the existing cached-exception comment (`:15-25`) so its parenthetical list of cached codes
stays accurate: the new instance is cached but *not* wired into `Throw`.

## 3. `src/Ahjo.Vulkan/Rendering/Swapchain.cs` — assignment sites, fast-fail, guard

Five edits.

**3a. `Recreate` device-lost fast-fail (`:270-274`).** `_state = SwapchainState.Poisoned;` →
`_state = SwapchainState.DeviceLost;`. Leave `ResultExtensions.ThrowDeviceLost();` and the comment
above it unchanged.

**3b. `Recreate` surface-lost fast-fail — new, immediately after 3a's block and before the
minimize check at `:281`.** Order matters: device loss is checked first because it is the wider
failure.

```csharp
        // A lost surface is terminal for this Swapchain (#222). Recreate
        // accepts only the Surface this object was constructed with (see the
        // ArgumentException above), so the only surface it would retry over is
        // the dead one. Fail fast with the driver's own verdict instead of
        // letting vkGetPhysicalDeviceSurfaceCapabilitiesKHR decide per-driver
        // whether this spins or throws. Recovery is a new Surface + a new
        // Swapchain; Dispose stays legal.
        if (_state == SwapchainState.SurfaceLost)
            ResultExtensions.ThrowSurfaceLost();
```

`_state` is left as `SurfaceLost` — the call changes nothing.

**3c. `Recreate` catch block (`:324`).** ~~`_state = SwapchainState.Poisoned;` →
`_state = SwapchainState.RecreateFailed;`~~ — **corrected after review (spec D3a).** Assigning a
constant classifies on "something threw" and would file a real surface loss under the retryable
member. Add

```csharp
internal static SwapchainState ClassifyFailure(Exception e, SwapchainState fallback)
    => (e as VulkanException)?.Result switch
    {
        VkResult.VK_ERROR_SURFACE_LOST_KHR => SwapchainState.SurfaceLost,
        VkResult.VK_ERROR_DEVICE_LOST      => SwapchainState.DeviceLost,
        _                                  => fallback,
    };
```

widen the `catch` to `catch (Exception e)` (it still rethrows, so CA1031 does not fire), and assign
`_state = ClassifyFailure(e, fallback: SwapchainState.RecreateFailed);` after the existing cleanup.
`internal` rather than `private` so the mapping is unit-testable — no CI-portable way exists to make
a live driver lose a surface.

**3c-bis. The two pre-drain regions.** Both sit *outside* the `try` and so leave `_state` untouched
entirely. Give each its own `try`/`catch (Exception e)` that assigns
`_state = ClassifyFailure(e, fallback: _state);` and rethrows. The fallback is the current state, not
`RecreateFailed`: nothing has been destroyed at either point, so the swapchain, its views and its
semaphores are all still current and a non-terminal failure must not relabel them.

- **The capability query (`:305-306`).** Exactly where a driver-restart surface loss lands.
- **The drain (`:337-340`) — added in the second review round.** `vkDeviceWaitIdle` documents
  `VK_ERROR_DEVICE_LOST`, and the intended `syncBeforeDestroy` argument
  `FrameRing.WaitForInFlightFences` throws the same code out of its fence wait
  (`src/Ahjo.Vulkan/Pools/FrameRing.cs:315-317`). Unclassified, a real device loss leaves the state
  `Ready`/`NeedsRecreate`, `ThrowIfNotPresentable` accepts, and the next `AcquireNextImage` issues
  `vkAcquireNextImageKHR` against a dead device. Self-correcting and `Device.IsLost` is set either
  way, so this is a contract gap plus one wasted call rather than a VUID violation — but `Recreate`'s
  `<returns>` now promises classification, so make the contract true rather than narrowing the doc.

Keep these as **separate** `try` blocks, not one wide one folded into the create's: that block's
cleanup dereferences `old`/`oldSems`, which are not captured until after the drain returns, and its
fallback (`RecreateFailed`) is wrong for anything that fails before the old swapchain is retired.

**3d. `MapPresentationResult` (`:437-448`).** `VK_ERROR_SURFACE_LOST_KHR` arm:
`_state = SwapchainState.SurfaceLost;`. `VK_ERROR_DEVICE_LOST` arm:
`_state = SwapchainState.DeviceLost;`. Both keep everything else (`_device.MarkLost()`, the returned
`AcquireResult.SurfaceLost`, the `VulkanException` message) unchanged.

**3e. `ThrowIfNotPresentable` (`:454-467`) — invert and split the message.** Replace the whole
method with a hot guard plus a cold thrower:

```csharp
    // Guards the "loop forever re-acquiring a dead swapchain" failure mode at
    // the API boundary. Written as "not (Ready or NeedsRecreate)" rather than a
    // positive list of bad states (#222): a state added later is
    // non-presentable until someone proves otherwise. NeedsRecreate stays
    // advisory — acquire/present remain legal and keep reporting OutOfDate.
    private void ThrowIfNotPresentable()
    {
        if (_state is not (SwapchainState.Ready or SwapchainState.NeedsRecreate))
            ThrowNotPresentable(_state);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowNotPresentable(SwapchainState state)
        => throw new InvalidOperationException(state switch { … });
```

Four constant-string arms plus a default. **Each message must literally contain its enum member
name** — `SwapchainTests` asserts `Assert.Contains(state.ToString(), ex.Message)`. Message shapes:

| State | Message |
|---|---|
| `Minimized` | `"Swapchain is in the Minimized state (zero-extent surface). Skip rendering, poll the window size, and call Recreate when it is restored."` (unchanged from `:464`) |
| `RecreateFailed` | `"Swapchain is in the RecreateFailed state: a Recreate threw after the old swapchain was retired, so no swapchain handle is held. Call Recreate again to attempt a from-scratch create, or Dispose."` |
| `SurfaceLost` | `"Swapchain is in the SurfaceLost state: VK_ERROR_SURFACE_LOST_KHR was reported for the VkSurfaceKHR this swapchain was built over. This is terminal — Recreate over the same surface cannot succeed. Dispose this swapchain, rebuild the Surface, and construct a new Swapchain."` |
| `DeviceLost` | `"Swapchain is in the DeviceLost state: the VkDevice was lost. This is terminal — dispose every dependent resource, dispose the Device, and rebuild from a fresh PhysicalDevice."` |
| default (incl. `Ready`/`NeedsRecreate`, unreachable) | `$"Swapchain is not presentable (state: {state})."` — the only interpolated arm, reachable only if a future member is added without updating this switch. |

Requires `using System.Runtime.CompilerServices;` in the file if not already present — check before
adding.

**3f. Comment/doc references to `Poisoned` inside this file.** Four of them, all stale after the
above:

- `:258` — `Recreate`'s `<returns>`: "A create failure sets `SwapchainState.Poisoned` and rethrows."
  → `RecreateFailed`. Add one clause to the same `<returns>`: entering `Recreate` on a swapchain
  already in `SurfaceLost` or with `Device.IsLost` throws `VulkanException` without changing state.
- `:417-419` — `MapPresentationResult`'s summary: `SurfaceLost` → `SwapchainState.SurfaceLost`;
  `DEVICE_LOST` → "mark the device + `SwapchainState.DeviceLost`, then throw".
- `:456` — the `ThrowIfNotPresentable` comment. Superseded by 3e; the false claim "in Poisoned no
  handle is held at all" must not survive into the new comment. **Corrected after review:** nor may
  it be replaced by "only `RecreateFailed` holds no handle" — that is false too (spec D3a). Say that
  none of the rejected states can assume a live handle.
- `:511-515` — `Present`'s remarks on `VK_ERROR_DEVICE_LOST`: `Poisoned` → `SwapchainState.DeviceLost`.

Also add `<exception>` documentation or a remarks sentence on `Recreate` for the new fast-fail if
the surrounding style has one; otherwise fold it into the `<returns>` edit above.

**3g. `Present(Queue, uint)` — hoist the guard above the index (added after review, spec D4a).**
The expression body `=> Present(queue, imageIndex, in _renderingDone[imageIndex]);` indexes before
the callee's `ThrowIfNotPresentable` runs, and the array is empty in exactly the states that reject a
present — so the real `RecreateFailed` shape throws `IndexOutOfRangeException`, contradicting both
3e's message table and the `RecreateFailed` member doc. Convert to a block body running
`ArgumentNullException.ThrowIfNull(queue);`, `ObjectDisposedException.ThrowIf(_disposed, this);` and
`ThrowIfNotPresentable();` — in that order, which preserves the exception precedence a null queue has
today — before forwarding. Accept the duplicated checks in the 3-arg overload: three predictable,
allocation-free branches.

**3h. Test seam — reproduce the real `RecreateFailed` shape (added after review).**
`OverrideStateForTesting` only flips `_state`, so a test built on it exercises an ordinary recreate
with a live `oldSwapchain`. Add an `internal void ForceRecreateFailedForTesting()` next to it that
leaves the object in the shape the failure path actually produces: `vkDeviceWaitIdle`, `DestroyViews`,
`DiscardRenderingDoneSemaphores`, `FlushRetired`, destroy + null `_handle`, then
`_state = SwapchainState.RecreateFailed`.

## 4. `src/Ahjo.Vulkan/Rendering/AcquireResult.cs` — the #221 handling table

Three edits; the table's structure and the `Success`/`Suboptimal`/`OutOfDate`/`Timeout`/`NotReady`
rows stay as they are.

- `:49-53` — the `SurfaceLost` row's middle cell: `<see cref="SwapchainState.Poisoned"/>` →
  `<see cref="SwapchainState.SurfaceLost"/>`. The third cell keeps "**Terminal.** Rebuild the
  `VkSurfaceKHR` as well, or stop." and gains one clause: the state alone is now enough to make that
  decision at the top of a frame loop.
- `:11-18` — the intro paragraph: "only `SurfaceLost` leaves the swapchain in a state the
  API-boundary guard rejects" stays true; update the `(#220)` attribution to `(#220, #222)` and
  replace the implicit "Poisoned" framing if present.
- `:88-97` — the `SurfaceLost` member doc: "has **already** moved to `SwapchainState.Poisoned`" →
  `SwapchainState.SurfaceLost`. Keep the "a caller that merely `continue`s will get an
  `InvalidOperationException` out of the next acquire or present" paragraph — still exactly true.

## 5. Samples — drop the sticky flag, one terminal guard

Apply the identical transformation to all four windowed samples. **`HelloDlaa` is included** (it got
this shape in #217/#219, not #221) — its `rebuildPending` interplay is preserved verbatim.

Per sample, in `Program.cs`:

**5a. Delete the sticky field and its comment block.**
`HelloTriangle:88-110`, `HelloCube:330-345`, `HelloVmaWindowed:262-277`, `HelloDlaa:370-384` (the
`bool presentable = true;` declaration plus the multi-line comment above it explaining why the
`||` chain cannot rediscover a minimize). Replace with a shorter comment on the loop-top guard
explaining that `swap.State` is now the authority — see 5b.

**5b. Loop top: terminal guard, then the recreate guard.** Immediately after
`window.PumpEvents(); if (window.ShouldClose) break;` and before `bool resized = window.ConsumeResize();`:

```csharp
                // Terminal first (#222): a lost surface can never be recovered
                // by Recreate, so this must be tested BEFORE the "not Ready ->
                // recreate" guard below, which would otherwise retry over it.
                // Both the acquire and the present paths funnel here rather
                // than each carrying their own report-and-break.
                if (swap.State is SwapchainState.SurfaceLost)
                {
                    ReportSurfaceLost();
                    break;
                }
```

Then change the recreate guard's first term (`HelloTriangle:123`, `HelloCube:365`,
`HelloVmaWindowed:292`, `HelloDlaa:396`) from `!presentable ||` to
`swap.State != SwapchainState.Ready ||`, with a comment noting it is the first term so a Minimized
or NeedsRecreate loop keeps re-entering the recreate path instead of falling through.

Inside that block, `presentable = TryRecreate(...); if (!presentable) { … }` becomes
`if (!TryRecreate(...)) { … }` — the sleep-and-`continue` body and the success-leg work
(`HelloCube`'s depth rebuild, `HelloDlaa`'s `rebuildPending = true`) are unchanged.

**`SwapchainState.DeviceLost` is deliberately not in the guard.** Device loss throws out of
`AcquireNextImage`/`Present`/`Recreate`, so a sample never observes the state; adding a branch for it
would be unreachable code. Say so in one comment line so a future reader does not "fix" the omission.

**5c. Acquire site — delete the `SurfaceLost` block.**
`HelloTriangle:151-157`, `HelloCube:401-408`, `HelloVmaWindowed:316-323`, `HelloDlaa:435-439`.
`AcquireResult.SurfaceLost` now falls into the existing catch-all below it. Update that catch-all's
comment (currently "Everything left is Timeout or NotReady …") to read: everything left is `Timeout`,
`NotReady` or `SurfaceLost`; the first two touch no state and are safe to retry, and `SurfaceLost`
has already moved the swapchain to `SwapchainState.SurfaceLost`, which the loop-top guard catches on
the next iteration.

`HelloDlaa`'s catch-all is `if (acq != AcquireResult.Success) continue;` with no print (`:443`); give
it the same comment treatment but do not add a print.

**5d. Present site — delete the `SurfaceLost` block.**
`HelloTriangle:224-228`, `HelloCube:511-515`, `HelloVmaWindowed:426-430`, `HelloDlaa:699-703`.
The `if (pres is AcquireResult.OutOfDate or AcquireResult.Suboptimal)` branch below it is unchanged
except for its `presentable = TryRecreate(...)` line, which becomes a plain call (`HelloTriangle`,
`HelloVmaWindowed`) or `if (TryRecreate(...)) { … }` where the success leg does work (`HelloCube`'s
depth rebuild `:527-531`, `HelloDlaa`'s `rebuildPending` `:710-711`). Update the "No sleep on the
false leg: the loop top sees `!presentable`" comment to say the loop top sees a non-`Ready` state.

**5e. `TryRecreate` — keep the shape, absorb the terminal code (corrected after review, spec D3a).**
`TryRecreate`'s `return state == SwapchainState.Ready;` (`HelloTriangle:302`, `HelloCube:738`,
`HelloVmaWindowed:539`, `HelloDlaa:823`) is still correct and still needed as an immediate
success/failure answer, and its signature and `bool` contract do not change. But wrap the
`swap.Recreate(...)` call — not `device.WaitIdle()` — in

```csharp
        SwapchainState state;
        try
        {
            state = swap.Recreate(new SwapchainDescription { … });
        }
        catch (VulkanException e) when (e.Result is VkResult.VK_ERROR_SURFACE_LOST_KHR)
        {
            return false;
        }
```

Otherwise 5b's loop-top guard is dead code for the route D3a identifies as the likeliest: a surface
loss first observed inside `Recreate` unwinds out of `Main` instead of reaching the guard. Every
`false` leg in all four samples either sleeps-and-`continue`s or falls through to the next iteration,
so the guard fires one tick later and the sample exits through `ReportSurfaceLost` as designed.

Keep the filter narrow. A blanket `catch (VulkanException)` would swallow genuine bugs and would
contradict 5b's own comment about `DeviceLost` being deliberately absent from the guard — device
loss must keep terminating the sample loudly, since no sample state resumes from it. Returning
`false` rather than a new enum value also keeps `HelloCube`'s depth rebuild and `HelloDlaa`'s
`rebuildPending` on the success leg unchanged.

Update 5b's funnel comment accordingly: it now names three observation points, not two.
`ReportSurfaceLost`'s XML doc mentions `SwapchainState.Poisoned`
(`HelloTriangle:256`, `HelloCube:689`, `HelloVmaWindowed:490`, `HelloDlaa:772`) — update to
`SwapchainState.SurfaceLost` and to the new single-call-site reality ("reached from the loop-top
guard, which both the acquire and the present path feed").

## 6. Tests — `tests/Ahjo.Vulkan.Tests/SwapchainTests.cs`

All new/changed cases keep the existing `TestGate.RequirePlatform(IsWindows, …)` +
`TestGate.RequireDriver()` preamble and the `Win32Window`/`Instance`/`Surface`/`CreatePresentDevice`
setup used by the neighbouring tests. Wrapper tests are Windows-only (#32); nothing here needs a
Linux lane.

**6a. Rename + widen `AcquireAndPresent_InMinimizedOrPoisoned_Throw` (`:416-445`).** New name
`AcquireAndPresent_InNonPresentableStates_Throw`. Iterate all four:

```csharp
foreach (var state in new[] { SwapchainState.Minimized, SwapchainState.RecreateFailed,
                              SwapchainState.SurfaceLost, SwapchainState.DeviceLost })
```

Keep both assertions per state (`Assert.Throws<InvalidOperationException>` on `AcquireNextImage` and
on `Present`) and keep `Assert.Contains(state.ToString(), acquireEx.Message)` — that is what pins
step 3e's message shapes. Keep the `OverrideStateForTesting(SwapchainState.Ready)` restore before
`Dispose`. Update the XML doc to name #222 alongside #110/#112.

**6b. New — `AcquireAndPresent_InNeedsRecreate_DoNotThrow`.** Proves the inverted guard's accept
side, which no test covers today. `OverrideStateForTesting(SwapchainState.NeedsRecreate)`, then
`swap.AcquireNextImage(in acquireSem, TimeSpan.Zero, out _)` and assert it does **not** throw
`InvalidOperationException` (any `AcquireResult`, including `Timeout`/`NotReady`, is a pass). Do not
call `Present` in this test — presenting an image index that was not acquired is a validation error.
Because a zero-timeout acquire can still succeed and leave the acquire semaphore signalled, dispose
the `SemaphorePool` through `Discard` rather than `Release`, matching how `FrameRing` handles a stale
acquire signal. **If that turns out to be awkward with the existing helpers in this file, drop this
case and say so in the PR** — it is the least load-bearing of the six.

**6c. New — `Recreate_AfterSurfaceLost_FailsFast`.** `OverrideStateForTesting(SwapchainState.SurfaceLost)`,
then `Recreate` through the same local-function wrapper the file already uses for `ref struct`
descriptions (`SwapchainTests.cs:475-485`). Assert: a `VulkanException` was thrown, its `Result` is
`VkResult.VK_ERROR_SURFACE_LOST_KHR`, and `swap.State` is still `SwapchainState.SurfaceLost`.
Restore `Ready` before `Dispose`.

**6d. New — `Recreate_AfterRecreateFailed_Succeeds`.** The other half of the split, and the case that
proves it is load-bearing. **Corrected after review:** drive it through step 3h's
`ForceRecreateFailedForTesting()`, not `OverrideStateForTesting` — the latter leaves a live handle,
so the recreate under test passes a real `oldSwapchain` and proves nothing about the from-scratch
path the member documents. Assert `State == RecreateFailed` and `ImageCount == 0` first; then that
`Present(queue, 0)` throws `InvalidOperationException` naming `RecreateFailed` (the regression guard
for step 3g — before that fix this shape threw `IndexOutOfRangeException`); then
`Assert.Equal(SwapchainState.Ready, swap.Recreate(in desc));` against the live window. No restore
needed.

**6d-bis. New — `ClassifyFailure_MapsTerminalResults_AndFallsBackOtherwise` (added after review).**
Step 3c's mapping asserted directly: `VK_ERROR_SURFACE_LOST_KHR` → `SurfaceLost`,
`VK_ERROR_DEVICE_LOST` → `DeviceLost`, another error code → the fallback, a non-`VulkanException`
→ the fallback, and a non-`RecreateFailed` fallback passed through unchanged (the pre-drain case).
Construct the `VulkanException` directly via its internal constructor — raising it through
`ThrowIfFailed` would route `VK_ERROR_DEVICE_LOST` into `Device.NotifyDeviceLossObserved`, marking
every live device in the process lost and poisoning the rest of the suite. Needs no driver and no
gate. This is the honest substitute for an end-to-end test: no CI-portable way exists to make a live
driver return `SURFACE_LOST` from a capability query.

**6e. Rename `Recreate_AfterDeviceLoss_ThrowsAndPoisons` (`:446-485`) to
`Recreate_AfterDeviceLoss_ThrowsAndMarksDeviceLost`** and change its final assertion (`:473`) to
`Assert.Equal(SwapchainState.DeviceLost, swap.State);`. Body otherwise unchanged.

**6f.** Grep the whole `tests/` tree for `Poisoned` after the edits; expect zero hits.

## 7. Build + test

`dotnet build Ahjo.Vulkan.slnx` then `dotnet test`. `TreatWarningsAsErrors=true` — no `#pragma`
suppressions. Expect the compiler to find any `Poisoned` reference the plan missed; that is the
intended failure mode of this break.

Also build the samples explicitly (they are in the solution, but confirm all four compile):
`dotnet build samples/HelloTriangle samples/HelloCube samples/HelloVmaWindowed samples/HelloDlaa`.

## 8. Benchmarks — none to run, and why

**Corrected after review — the original premise here was wrong.** `Rendering/` *is* covered by the
zero-per-frame-allocation rule: `src/Ahjo.Vulkan/CLAUDE.md:38-40` extends the four-directory list to
"any other API expected to run inside a per-frame loop" and states that "the rule follows the *call
frequency*, not the directory", and acquire/present run once per frame.

The conclusion is unchanged, for a different reason. No benchmark class covers `Swapchain` (24 files
in `tests/Ahjo.Vulkan.Benchmarks/`, none touching it) because the harness is headless: a swapchain
benchmark would need a surface, a window and a message pump, and would then be measuring compositor
pacing rather than wrapper allocation. **Do not add one in this PR.** What holds the invariant up
here instead is the code shape: the only per-frame code changed is `ThrowIfNotPresentable`, a single
branch over enum constants (it folds to one unsigned `> 1` compare) with all message construction
behind a `NoInlining` cold helper, plus step 3g's two extra constant-fold guards on the forwarding
`Present`. Record this paragraph's reasoning in the PR body so the `bench-coverage-checker` verdict
is not mistaken for an omission.

## 9. Docs

- No shipped doc references `SwapchainState` (verified: `README.md`, `docs/*.md`,
  `docs/migration-vortice-to-ahjo.md` are all silent; only the #120 and #220 spec/plan pairs mention
  it, and those are historical records that must **not** be edited).
- No `Generated/`, `native/` or `tools/*.rsp` change: this enum is hand-written wrapper surface.
- The durable record of the break is this spec plus the PR body — see the OPEN item below.

## 10. Review + PR

Run `vulkan-validation-reviewer` on the diff (it touches the swapchain lifecycle and a `Recreate`
precondition) and `bench-coverage-checker` (which should confirm step 8's reasoning). Then open a PR
from `issue-222-swapchain-state-split` to `main`, titled
`Rendering: split SwapchainState.Poisoned into RecreateFailed, SurfaceLost and DeviceLost (#222)`,
with `Closes #222` and a **Breaking change** section listing: `SwapchainState.Poisoned` removed;
three members added; `Recreate` now throws `VulkanException(VK_ERROR_SURFACE_LOST_KHR)` when called
on a `SurfaceLost` swapchain; `ThrowIfNotPresentable` messages changed.

---

## OPEN items — stop and ask

**OPEN 1 — how the repo records a breaking change.** There is no `CHANGELOG.md`, no
`PublicAPI.*.txt` baseline, and releases are cut with `--generate-notes`
(`.github/workflows/publish.yml:22-25`), so today a public API break is recorded nowhere except the
PR title. The latest tag is `v0.9.0`. The maintainer should decide between (a) PR body section only —
what step 10 assumes; (b) start a `CHANGELOG.md` with this entry; (c) start a
`docs/breaking-changes.md`. The architect will not invent a repo-wide convention inside a
five-file change. **Do not create either file without an answer.**

**OPEN 2 — D3 is separable and may be vetoed.** Step 3b (and step 2, and step 6c) implement the
`Recreate` fast-fail on `SurfaceLost`, which narrows `Recreate`'s throw contract. The spec argues for
it (`Recreate` refuses any other `Surface`, so a same-surface retry is unconditionally futile), but a
maintainer may prefer `Recreate` stay permissive and let the driver decide. If so: drop steps 2, 3b
and 6c, and soften the `SurfaceLost` member doc in step 1 from "throws" to "cannot succeed". Steps
1, 3a/3c-3f, 4, 5, 6a/6b/6d/6e are unaffected either way. **Confirm before implementing step 2.**
