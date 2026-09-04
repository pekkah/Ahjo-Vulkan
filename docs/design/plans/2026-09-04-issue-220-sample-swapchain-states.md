# Windowed samples: non-presentable swapchain states — implementation plan

Paired with [../specs/2026-09-04-issue-220-sample-swapchain-states-design.md](../specs/2026-09-04-issue-220-sample-swapchain-states-design.md).
Issue [#220](https://github.com/pekkah/Ahjo-Vulkan/issues/220).

The reference implementation already exists on `main`: `samples/HelloDlaa/Program.cs` (PR #217,
commits `90d092f` and `6e0b0e9`). **Read it before starting** — steps 1–3 propagate its shape
verbatim, and where this plan is ambiguous, HelloDlaa is the tiebreaker.

Line numbers below are as of `6e0b0e9` and will drift as you edit; they identify the site, not a
byte offset.

Scope: three sample `Program.cs` files and two wrapper files (XML comments only). No project files
change, no markdown changes, no new files. `samples/HelloRayQuery`, `HelloVma`, `HeadlessTriangle`,
`HeadlessExport` and `AotSmoke` are headless — **do not touch them**.

---

## 1. `samples/HelloTriangle/Program.cs`

### 1a. Two new private statics

Add a constant next to the other class-level members, with HelloDlaa's comment
(`HelloDlaa/Program.cs:46-51`) reworded for this sample:

```csharp
private const int MinimizedPollMilliseconds = 16;
```

Add two private static methods next to `RecordSwapchainBarrier` / `CreatePresentDevice`
(after `:211`):

```csharp
private static bool TryRecreate(
    Device device, Swapchain swap, FrameRing ring, in Surface surface, SdlWindow window)
```

Body: `device.WaitIdle();` then `SwapchainState state = swap.Recreate(new SwapchainDescription
{ Surface = surface, Width = window.Width, Height = window.Height });` then
`ring.RecycleStaleAcquireSemaphores();` then `return state == SwapchainState.Ready;`. That is exactly
the three statements the three existing call sites already run, plus the return. Copy the
`<summary>`/`<remarks>` from `HelloDlaa/Program.cs:797-807` — the remarks paragraph that says callers
must test the return value rather than `swap.Extent`, because `CreateOrRecreate` returns `Minimized`
*before* assigning `_extent`, is the load-bearing part and must survive.

```csharp
private static void ReportSurfaceLost()
```

Body: a single `Console.Error.WriteLine` with the message from `HelloDlaa/Program.cs:790-793`
verbatim:

> `The window surface was lost (VK_ERROR_SURFACE_LOST_KHR — typically a display-driver restart, a session switch or a monitor change). A swapchain over a lost surface cannot be recreated, so this sample exits rather than retrying.`

Copy the `<summary>` + `<remarks>` from `HelloDlaa/Program.cs:768-787` unchanged apart from the
"this sample" wording. The **do not turn this into a retry** paragraph is the point of the method;
it must not be trimmed.

### 1b. The sticky flag and the loop-top condition

Before `while (!window.ShouldClose)` (`:90`), declare:

```csharp
bool presentable = true;
```

with the comment block from `HelloDlaa/Program.cs:371-383` — the one enumerating *why* neither
`ConsumeResize()` nor an extent comparison can rediscover a minimize. Reword only where it names
HelloDlaa-specific members.

Replace `:95-106` with:

```csharp
bool resized = window.ConsumeResize();

if (!presentable || resized ||
    swap.Extent.width != window.Width || swap.Extent.height != window.Height)
{
    presentable = TryRecreate(device, swap, ring, in surface, window);
    if (!presentable)
    {
        System.Threading.Thread.Sleep(MinimizedPollMilliseconds);
        continue;
    }
}
```

Three things are load-bearing and each needs its comment (mirror `HelloDlaa/Program.cs:391-408`):

- `resized` is consumed into a local **before** the `||` chain. Inside the chain, a short-circuit on
  `!presentable` would leave `_resized` set for a frame that no longer needs it.
- `!presentable` is the **first** term, so the minimized loop re-enters the recreate path.
- the sleep is on the false leg, so the minimized loop polls rather than spins.

Use the fully-qualified `System.Threading.Thread.Sleep` as HelloDlaa does (`:404`) rather than adding
a `using System.Threading;` — `Thread` would otherwise read ambiguously next to the Vulkan types.

### 1c. The acquire-path branch

Replace `:117-133` with, in this order:

```csharp
if (acq is AcquireResult.OutOfDate or AcquireResult.Suboptimal)
{
    presentable = TryRecreate(device, swap, ring, in surface, window);
    if (!presentable) System.Threading.Thread.Sleep(MinimizedPollMilliseconds);
    continue;
}
if (acq == AcquireResult.SurfaceLost)
{
    ReportSurfaceLost();
    break;
}
if (acq != AcquireResult.Success)
{
    Console.Error.WriteLine($"AcquireNextImage: {acq}");
    continue;
}
```

Keep the existing `MarkImageAcquireSignaled` call and its comment (`:112-116`) ahead of this block,
unchanged.

Keep the `Console.Error.WriteLine` — behaviour for the remaining two results is unchanged — but put
HelloDlaa's explanation above it (`HelloDlaa/Program.cs:440-443`): everything left is `Timeout` or
`NotReady`, neither touches `_state`, neither signalled the acquire semaphore, so the bare retry is
correct and there is nothing to recycle.

Ordering matters: `SurfaceLost` must be tested **before** the `!= Success` catch-all, or it falls
into the print-and-continue that is the current bug.

### 1d. The present-path branch

Replace `:190-201` with:

```csharp
var pres = swap.Present(queue, imageIndex);
if (pres == AcquireResult.SurfaceLost)
{
    ReportSurfaceLost();
    break;
}
if (pres is AcquireResult.OutOfDate or AcquireResult.Suboptimal)
{
    presentable = TryRecreate(device, swap, ring, in surface, window);
}
```

No sleep here, and a comment saying why: on a false return the loop top sees `!presentable` on the
next iteration and sleeps there. One un-slept iteration is bounded; this is HelloDlaa's shape
(`:698-712`).

Add HelloDlaa's note (`:705-707`) that present can only report `Success`, `Suboptimal`, `OutOfDate`
or `SurfaceLost`, because `Timeout`/`NotReady` are gated `when fromAcquire` in
`MapPresentationResult` and a present returning either throws as a broken ICD — so there is no
catch-all on this side by design.

`break` (not `return`) so the post-loop `device.WaitIdle()` + "Rendered N frames" tail at `:206-209`
still runs.

---

## 2. `samples/HelloCube/Program.cs`

Same five edits as step 1, at `:337-349` (loop top), `:357-375` (acquire), `:468-480` (present),
with the flag declared before `:327` and the helpers + constant added near `OnValidationMessage`
(`:624`).

The one difference: **the depth buffer.** Each of the three sites currently pairs the recreate with
`depth.Dispose(); depth = DepthBuffer.Create(device, swap.Extent.width, swap.Extent.height);`
(`:346-347`, `:366-367`, `:478-479`). Keep those two lines inline — do not hoist them into
`TryRecreate` (`depth` is a local `DepthBuffer` struct at `:314`, and `TryRecreate` must stay the
same four lines it is in the other two samples) — but move them **inside the success leg**:

```csharp
presentable = TryRecreate(device, swap, ring, in surface, window);
if (!presentable)
{
    System.Threading.Thread.Sleep(MinimizedPollMilliseconds);
    continue;
}
depth.Dispose();
depth = DepthBuffer.Create(device, swap.Extent.width, swap.Extent.height);
```

Rebuilding the depth buffer on the failed leg would allocate a full-size attachment against the
*stale* extent every 16 ms while the window stays minimized. Add a one-line comment saying so.

At the present site there is no `continue`, so it reads
`if (presentable) { depth.Dispose(); depth = DepthBuffer.Create(...); }`.

Check that `break` on `SurfaceLost` still reaches the `finally` at `:490-500` — it does; the
`pipelineCache.Save(cachePath)` there must not be skipped, and this is why the decision is `break`
and not `return`.

---

## 3. `samples/HelloVmaWindowed/Program.cs`

Same five edits as step 1, at `:264-275` (loop top), `:282-298` (acquire), `:391-402` (present),
with the flag declared before `:259` and the helpers + constant added near `s_validationErrors`
(`:60`) / `OnValidationMessage` (`:430`).

No per-extent resources to rebuild — the per-frame UBO ring is sized by `FramesInFlight`, not by the
extent — so the loop-top and acquire legs are `TryRecreate` + sleep + `continue` with nothing after.

`break` on `SurfaceLost` must reach the tail at `:405-413`, which prints the validation summary and
returns `errors == 0 ? 0 : 4`. Leave that expression alone: a lost surface exits 0.

---

## 4. `src/Ahjo.Vulkan/Rendering/AcquireResult.cs` — XML docs (D3)

Comments only. No member is added, removed or renamed; no behaviour changes.

**4a.** Add a `<remarks>` to the **type** (after the existing `<summary>` at `:3-9`) containing the
six-row handling table as a `<list type="table">` with `<listheader>` terms
`Result` / `Swapchain state after` / `What the caller must do`, and one `<item>` per member:

| Result | State after | Caller |
|---|---|---|
| `Success` | untouched | render |
| `Suboptimal` | untouched (stays `Ready`) | usable frame; recreate when convenient |
| `OutOfDate` | `NeedsRecreate` | recreate; acquire/present stay legal meanwhile |
| `SurfaceLost` | `Poisoned` | terminal — rebuild the surface, or stop; **do not retry the same surface** |
| `Timeout` | untouched | retry; nothing to clean up |
| `NotReady` | untouched | retry; nothing to clean up |

Escape the markup properly (`<c>`, `<see cref="…"/>`); raw `<`/`>` and `&` in doc text must be
entity-escaped. Cite the issue as `(#220)` the way the surrounding code cites `(#120)`.

**4b.** Extend the `SurfaceLost` member `<summary>` (`:20-28`) — keep the existing text, add: the
swapchain has **already** moved to `SwapchainState.Poisoned` when this is returned, so a caller that
merely `continue`s will get an `InvalidOperationException` out of the *next*
`Swapchain.AcquireNextImage` or `Swapchain.Present`. `Swapchain.Recreate` over the same
`VkSurfaceKHR` cannot succeed.

**4c.** Extend `Timeout` (`:30-31`) and `NotReady` (`:32-34`): both leave the swapchain state
untouched and neither signals the semaphore passed to `AcquireNextImage`, so a bare retry on the next
iteration is correct and there is no stale semaphore to rotate. Both are `vkAcquireNextImageKHR`
results only — see the `Swapchain.Present` remark from 5b.

Do not restate the whole #120 transition table here; `<see cref="SwapchainState"/>` already carries
the per-state docs and must not be contradicted.

---

## 5. `src/Ahjo.Vulkan/Rendering/Swapchain.cs` — XML docs (D3)

Comments only.

**5a.** `AcquireNextImage` (`<summary>` at `:374-382`): add a `<remarks>` pointing at
`<see cref="AcquireResult"/>`'s table for the full six-result handling, and stating that all six are
reachable from this method.

**5b.** `Present(Queue, uint, in BinarySemaphore)` (`<remarks>` at `:481-488`): add a paragraph
recording the asymmetry — present can return only `Success`, `Suboptimal`, `OutOfDate` and
`SurfaceLost`; `VK_TIMEOUT` and `VK_NOT_READY` are acquire-only and a present returning either is
treated as a broken ICD and throws `VulkanException`. This promotes a fact currently visible only in
the private `MapPresentationResult` summary (`:407-418`). Note that `VK_ERROR_DEVICE_LOST` is
outside `AcquireResult` on both paths and throws.

The no-argument `Present(Queue, uint)` overload (`:462-471`) forwards to this one; a
`<seealso>`/one-line pointer is enough there.

Leave `MapPresentationResult`'s own summary as it is — it is accurate, and it is the code comment
these docs are derived from.

---

## 6. Docs

**No change to any `.md` file.** Checked: `src/Ahjo.Vulkan/README.md`'s "Quick start" (`:47-76`)
covers instance creation and `AhjoDiagnostics.Sink` only — it never shows an acquire/present loop, so
there is no claim there that this design contradicts and nothing to amend. `docs/` has no swapchain
note (the only files mentioning a swapchain are `docs/migration-vortice-to-ahjo.md:431`, a changelog
line about `imageSharingMode`, and `docs/ngx-notes.md` §6, which is about DLSS output targets).

No new file under `docs/`. Per D3 the material lives on the enum. If you believe a `docs/` note is
needed after all, stop and ask — that is a decision the spec made explicitly.

---

## 7. Tests

**No new automated tests, deliberately.** Justify it in the PR body with the spec's Verification
section: CI builds the samples but runs only `AotSmoke` (`.github/workflows/ci.yml:333-343`), there
is no harness driving a sample frame loop, and the wrapper-side behaviour these samples now respect
is already covered by `tests/Ahjo.Vulkan.Tests/SwapchainTests.cs:416-445`
(`AcquireAndPresent_InMinimizedOrPoisoned_Throw`, driving the states through
`OverrideStateForTesting`).

What must be run instead:

1. `dotnet build Ahjo.Vulkan.slnx` — green under `TreatWarningsAsErrors=true`. No `#pragma`
   suppressions; if the XML in step 4/5 trips a diagnostic, fix the XML.
2. `dotnet test` — no regressions. Nothing here should move a test, so any change is a signal.
3. **Manual, on hardware, per sample** (`HelloTriangle`, `HelloCube`, `HelloVmaWindowed`), run
   without `--frames` so the loop is open-ended:
   - launch; confirm it renders;
   - minimize; leave minimized 10+ seconds;
   - while minimized, check CPU in Task Manager — must be near idle, not a pegged core
     (`HelloDlaa` measured ~2.7%);
   - restore **without resizing** (the same-size restore is the edge `SdlWindow.cs:144` filters out);
     confirm it resumes rendering;
   - resize the window; confirm it still renders (regression check on the recreate path);
   - minimize/restore a second time (proves the flag is not one-shot);
   - close with the window button, then repeat with Esc;
   - pass = no `InvalidOperationException`, no `[VK ERROR]` line on stderr, exit code 0.
   - `HelloCube` additionally: confirm the pipeline-cache file is written on exit, and that the
     `w` wireframe toggle still works after a restore.
   - `HelloVmaWindowed` additionally: the "Validation: 0 error(s)" line, and exit code 0 (not 4).
4. Record the results per sample in the PR body. State explicitly that the `SurfaceLost` path was
   **not** exercised — see below.

**OPEN:** the `SurfaceLost` path cannot be provoked without a display-driver restart or a session
switch and was not exercised in #217 either. Do **not** write "verified" against it. If you have a
safe way to force `VK_ERROR_SURFACE_LOST_KHR` on the test machine, stop and propose it before doing
it; otherwise report the path as reasoned-from-source and identical to the merged `HelloDlaa` shape.

---

## 8. Benchmarks

None. Nothing under `Recording/`, `Sync/`, `Pools/` or `Memory/` changes, and the only `src/` edits
are XML comments. Say so in the PR body so `bench-coverage-checker` has the answer up front.

---

## 9. Review + PR

- `vulkan-validation-reviewer` on the diff: the questions it should be asked are whether `break` on
  `SurfaceLost` leaves any semaphore or fence in a state the post-loop `device.WaitIdle()` +
  `Dispose` chain cannot drain, and whether skipping `RecycleStaleAcquireSemaphores` is possible on
  any new path (it is not — it lives inside `TryRecreate`, which every recreate site now goes
  through).
- One PR to `main`, branched from `main` (never stacked). Title in repo style, e.g.
  `Samples: handle minimize and a lost surface in the three remaining windowed samples`.
  Body references `Closes #220` and links this plan and its spec.

---

## Open items carried from the spec

**OPEN:** `GenerateDocumentationFile` is set nowhere in the repo (`Directory.Build.props:107-126`
holds every packaging property and does not include it), so the step 4/5 XML docs do not ship in the
nupkgs and do not reach a package consumer's IntelliSense. Turning it on is one line but surfaces
CS1591 across eight public projects under `TreatWarningsAsErrors=true`. **Do not turn it on in this
PR.** If the human wants it, it gets its own issue.

**OPEN:** `HelloTriangle` and `HelloCube` print `[VK ERROR]` lines but `return 0` unconditionally
(`HelloTriangle/Program.cs:252-259, :209`; `HelloCube/Program.cs:624-631, :488`), so "0 validation
errors" is eyeballed on stderr rather than encoded in the exit code — unlike `HelloVmaWindowed`,
which returns 4. The spec rejected adding counters as scope creep. If you think the verification step
is unreliable without them, stop and ask rather than adding them.
