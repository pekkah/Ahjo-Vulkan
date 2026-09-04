# Windowed samples mishandle the non-presentable swapchain states

Issue: [#220](https://github.com/pekkah/Ahjo-Vulkan/issues/220). Written 2026-09-04.

## Problem

Three windowed samples crash when their window is minimized, and would crash again — one state
over — if the platform surface were lost. Both are the same defect: **the return value of
`Swapchain.Recreate` and the non-`Success` members of `AcquireResult` are treated as noise.**

The wrapper is not at fault. `SwapchainState` (#120) exists precisely so that "minimized" and
"surface lost" are *reported* legal states rather than corrupted object state
(`src/Ahjo.Vulkan/Rendering/SwapchainState.cs:3-7`), and `ThrowIfNotPresentable`
(`src/Ahjo.Vulkan/Rendering/Swapchain.cs:451-460`) is the API-boundary guard that stops a caller
looping forever against a dead handle. The samples discard the report and then hit the guard.

### Defect 1 — minimize

Reproduced on hardware in #219 / PR #217 against `samples/HelloDlaa`, which carried the identical
bug before `90d092f` fixed it.

The sequence, all of it verified against source:

1. The window is minimized. `vkAcquireNextImageKHR` returns `VK_ERROR_OUT_OF_DATE_KHR`, which maps
   to `AcquireResult.OutOfDate` and sets `_state = NeedsRecreate` (`Swapchain.cs:427-429`).
2. The sample takes its `OutOfDate or Suboptimal` branch and calls `swap.Recreate(...)`
   (`HelloTriangle/Program.cs:120`, `HelloCube/Program.cs:360`, `HelloVmaWindowed/Program.cs:285`).
3. `Recreate` queries surface caps *before* the drain, sees a zero extent, sets
   `_state = Minimized` and returns it without touching anything (`Swapchain.cs:284-288`).
   **The samples discard that return value.**
4. `continue`. At the top of the loop the recreate condition
   `window.ConsumeResize() || swap.Extent.width != window.Width || swap.Extent.height != window.Height`
   is false on every term (see below), so the loop falls straight through.
5. `swap.AcquireNextImage(...)` → `ThrowIfNotPresentable` → `InvalidOperationException:
   "Swapchain is in the Minimized state (zero-extent surface)."` (`Swapchain.cs:453-458`).

Neither term of that condition can fire, and this is the part worth writing down because both
guards *look* correct:

- **No resize event is produced.** `SdlWindow.PumpEvents` raises `_resized` only inside
  `if (w > 0 && h > 0 && ((uint)w != Width || (uint)h != Height))`
  (`tests/Ahjo.Vulkan.Tests/SdlWindow.cs:144`). A minimize reports `w == h == 0` and is filtered;
  a restore to the *same* size fails the inequality. Neither edge sets the flag.
- **The extent comparison cannot see it either.** `SdlWindow.Width`/`Height` are only ever assigned
  inside that same guarded block (`SdlWindow.cs:145-147`), so they keep their last non-zero values.
  On the swapchain side, `CreateOrRecreate` returns `Minimized` at `Swapchain.cs:549-550` — one line
  *before* `_extent = extent` at `:551` — and the pre-drain early-out at `:284-288` never reaches
  `CreateOrRecreate` at all. `swap.Extent` therefore also keeps its last good value. The two sides
  compare equal, forever.

Contrary to the issue body's "a variant of the same bug also busy-spins": in these three samples as
they stand today the loop does **not** spin, it crashes on the very next iteration. The spin is what
appears once a naive fix adds a `continue` on the not-presentable leg without a sleep — which is
what happened during #217, and is why the sleep is part of the prescribed shape rather than an
optimization.

### Defect 2 — `SurfaceLost` falls through

Found by the merge-gate review of #217, fixed in `samples/HelloDlaa` by `6e0b0e9`, and still open in
all three samples here. Verified against `MapPresentationResult`:

`VK_ERROR_SURFACE_LOST_KHR` sets `_state = Poisoned` and returns `AcquireResult.SurfaceLost`
**without throwing** (`Swapchain.cs:430-432`). `SurfaceLost` matches neither the
`is OutOfDate or Suboptimal` branch nor anything else, so the loop reaches its catch-all
`if (acq != AcquireResult.Success) { Console.Error.WriteLine(...); continue; }`
(`HelloTriangle/Program.cs:129-133`, `HelloCube/Program.cs:371-375`,
`HelloVmaWindowed/Program.cs:294-298`), prints one line, and the next `AcquireNextImage` throws —
`ThrowIfNotPresentable` rejects `Poisoned` exactly as it rejects `Minimized` (`Swapchain.cs:453`).

On the present side there is no catch-all at all: `if (pres is OutOfDate or Suboptimal)`
(`HelloTriangle/Program.cs:191`, `HelloCube/Program.cs:469`, `HelloVmaWindowed/Program.cs:392`) is
the only test, so a `SurfaceLost` from present is silently ignored and reaches the same throw.

A lost surface is **terminal, not retryable**. `Recreate` over the same `VkSurfaceKHR` cannot
succeed; recovery means destroying and rebuilding the surface too, which is the window system's
business. The #120 transition table already says so
(`docs/design/specs/2026-06-12-issue-120-device-loss-design.md:264`).

## Evidence

### The audit: nine discarded `Recreate` returns, three samples

`Swapchain.Recreate` returns `SwapchainState`. Every call site in the three samples ignores it:

| Sample | loop-top recreate | acquire-path recreate | present-path recreate |
|---|---|---|---|
| `samples/HelloTriangle/Program.cs` | `:98` | `:120` | `:194` |
| `samples/HelloCube/Program.cs` | `:340` | `:360` | `:472` |
| `samples/HelloVmaWindowed/Program.cs` | `:267` | `:285` | `:395` |

All nine construct an identical `new SwapchainDescription { Surface, Width = window.Width,
Height = window.Height }` — no `PreferredFormats`, no `ImageUsage`, no `PreferredImageCount` — and
all nine are followed by `ring.RecycleStaleAcquireSemaphores()`. That uniformity is what makes a
per-sample `TryRecreate` helper a mechanical extraction rather than a design.

`samples/HelloDlaa/Program.cs` is the fourth copy and is already fixed: `bool presentable` declared
at `:384` with the explanation at `:371-383`, `bool resized = window.ConsumeResize()` hoisted out of
the `||` chain at `:394`, `presentable = TryRecreate(...)` at all three recreate sites (`:399`,
`:430`, `:710`), `ReportSurfaceLost(); break;` on both the acquire (`:435-439`) and present
(`:699-703`) sides, `TryRecreate` itself at `:808-825`, and `MinimizedPollMilliseconds = 16` at
`:51`.

### Per-sample differences that a shared helper would have to absorb

- `HelloTriangle` and `HelloVmaWindowed` recreate the swapchain and nothing else.
- `HelloCube` also rebuilds a depth attachment at each of the three sites —
  `depth.Dispose(); depth = DepthBuffer.Create(device, swap.Extent.width, swap.Extent.height);`
  (`:346-347`, `:366-367`, `:478-479`), over a local `DepthBuffer` struct (`:314`, disposed in the
  `finally` at `:492`).
- `HelloDlaa` defers its per-extent rebuild through a `rebuildPending` flag (`:370`, `:410-419`)
  because it reconstructs `FrameTargets`, a `DlssFeature`, a `JitterSequence` and a readback buffer.
- `HelloCube` further carries a `wireframe` toggle and a pipeline-cache save in its `finally`
  (`:490-500`) that a `break` must not skip.

So four samples have four different "and then rebuild this" bodies. Any shared loop helper takes a
callback for that body; a shared *recreate* helper is four lines with one caller each.

### Where a shared helper could live — every candidate is worse

- **`src/Ahjo.Vulkan.Utilities/`** contains exactly two files, `PngReader.cs` and `PngWriter.cs`, and
  its csproj has **no `ItemGroup` at all** — zero `ProjectReference`s, zero `PackageReference`s. Its
  own comment states the charter: *"Dep-free helpers usable from samples and tests … Not published"*
  (`src/Ahjo.Vulkan.Utilities/Ahjo.Vulkan.Utilities.csproj:9-12`). A swapchain-loop helper needs
  `Ahjo.Vulkan`, and a window-aware one needs `ppy.SDL3-CS`. Both would propagate to all eight
  consumers, including `samples/AotSmoke` (`AotSmoke.csproj:32`), which publishes with
  `PublishAot=true` — dragging SDL3 into the ILC run to fix a bug in three samples that AotSmoke does
  not have. Separately, `HelloTriangle` and `HelloVmaWindowed` do not reference Utilities today
  (`HelloTriangle.csproj:11-13`), so they would each grow a reference too.
- **`tests/Ahjo.Vulkan.Tests/SdlWindow.cs`** is link-compiled into four sample projects
  (`HelloCube.csproj:20`, `HelloDlaa.csproj:20`, `HelloTriangle.csproj:19`,
  `HelloVmaWindowed.csproj:17`) *and* compiled into the test assembly. Anything added there is added
  to all five. The issue itself flags this cost.
- **`src/Ahjo.Vulkan/`** — see D4.

### The six-member enumeration, re-verified against source

The issue comment's table is correct. Checked line by line against `MapPresentationResult`
(`Swapchain.cs:419-444`) rather than taken on trust:

| Result | Source | State after | Safe to `continue`? |
|---|---|---|---|
| `Success` | `:423-424` | untouched | n/a — renders |
| `Suboptimal` | `:425-426` | untouched (stays `Ready`) | yes; the image is presentable per spec |
| `OutOfDate` | `:427-429` | `NeedsRecreate` | yes — `ThrowIfNotPresentable` lists only `Minimized`/`Poisoned` (`:453`), so it is advisory |
| `SurfaceLost` | `:430-432` | **`Poisoned`** | **no — the defect** |
| `Timeout` | `:433-434` | untouched | yes |
| `NotReady` | `:435-436` | untouched | yes |

`Timeout` and `NotReady` are safe for a reason worth recording: neither assigns `_state`, and
neither signalled the acquire semaphore, because `FrameContext.MarkImageAcquireSignaled` is called
by the samples only for `Success or Suboptimal` (`HelloTriangle/Program.cs:115-116`,
`HelloCube/Program.cs:355-356`, `HelloVmaWindowed/Program.cs:280-281`,
`HelloDlaa/Program.cs:426-427`). There is no stale semaphore to rotate. A bare retry is correct.

Two asymmetries confirmed:

- `VK_TIMEOUT` and `VK_NOT_READY` are gated `when fromAcquire` (`:433`, `:435`), so a *present*
  returning either falls to `default:` and throws as a broken ICD (`:442-443`). The present side
  exposes only four of the six members. This is documented today only in the `<summary>` of the
  **private** `MapPresentationResult` (`:407-418`) — invisible to any consumer.
- `VK_ERROR_DEVICE_LOST` is outside `AcquireResult` entirely: `_device.MarkLost()`,
  `_state = Poisoned`, then `throw new VulkanException` (`:437-441`). It terminates rather than
  reaching a later `AcquireNextImage` in a non-presentable state. Already safe; no sample change
  needed.

### Public documentation of all this: partial, and not shipped

`AcquireResult`'s per-member docs (`src/Ahjo.Vulkan/Rendering/AcquireResult.cs:11-35`) are good on
*meaning* and silent on *consequence*. Specifically, `SurfaceLost` (`:20-28`) explains that recovery
needs surface + swapchain but never says the swapchain has already moved to `Poisoned` and that the
next acquire will therefore throw — which is exactly the fact a caller writing `continue` needs.
`Timeout` (`:30-31`) and `NotReady` (`:32-34`) say nothing about a bare retry being safe, or about
being acquire-only.

Independent finding: **no project in this repository sets `GenerateDocumentationFile` or
`DocumentationFile`** (absent from `Directory.Build.props`, which is where all packaging properties
live, `:107-126`). XML docs are not emitted and do not ship in the nupkgs, so they reach source
readers and Source Link steppers but not a package consumer's IntelliSense. That is a real
limitation of D3 and is called out as an open item rather than papered over.

### Scope check

`samples/HelloRayQuery` — the issue asks for it to be checked. It is **headless**: no `SdlWindow`
link in `HelloRayQuery.csproj`, and a grep for `Swapchain`/`Window` in
`samples/HelloRayQuery/Program.cs` returns nothing but an unrelated comment. Same for `HelloVma`,
`HeadlessTriangle`, `HeadlessExport` and `AotSmoke`. **No change.**

## Decision

Four decisions, one shape.

### D1 — Write the pattern into each sample; do not hoist it

Each of `samples/HelloTriangle`, `samples/HelloCube` and `samples/HelloVmaWindowed` gets its own
copy of the `HelloDlaa` shape: a `const int MinimizedPollMilliseconds = 16`, a sticky
`bool presentable`, `bool resized = window.ConsumeResize()` hoisted out of the `||` chain,
`presentable = TryRecreate(...)` at all three recreate sites, a sleep on every false leg, and a
private `static bool TryRecreate(Device, Swapchain, FrameRing, in Surface, SdlWindow)`. Four samples
will then carry four near-identical copies, deliberately.

Three reasons, in order of weight:

1. **There is no home that does not cost more than the duplication.** Utilities is dep-free by
   charter and feeds an AOT publish; `SdlWindow.cs` is link-compiled into five assemblies including
   the test suite; the wrapper is the wrong layer (D4). Each option imports a dependency edge into
   projects that do not have this bug, to save ~25 lines in three that do.
2. **The bodies differ where it matters.** Four samples, four different per-extent rebuilds (see
   Evidence). A shared loop helper would take a rebuild callback and a description factory — at
   which point the helper's signature is harder to read than the loop it replaced.
3. **Samples are the teaching artefact.** The frame loop *is* what a reader opens `HelloTriangle`
   for. A loop whose swapchain handling is a call into `SampleFrameLoop.Run(...)` teaches the reader
   about a helper that ships in no package. And a reader comparing two samples should see the same
   shape, not a call and a body.

The mitigation for rot is that the copies are commented with *why* each term exists — `HelloDlaa`'s
comments at `:364-383` and `:788-806` are the model — so a future edit that deletes one has to
delete an explanation of what it breaks.

### D2 — `SurfaceLost` reports and breaks out of the loop

At both the acquire and the present site, in all three samples:

```csharp
if (acq == AcquireResult.SurfaceLost) { ReportSurfaceLost(); break; }
```

`break`, not `return`, so the normal post-loop tail runs — `device.WaitIdle()`, the "Rendered N
frames" line, the validation summary in `HelloVmaWindowed`, and critically `HelloCube`'s `finally`
block with its `pipelineCache.Save(cachePath)` (`HelloCube/Program.cs:490-500`). Exit code stays 0
(`HelloVmaWindowed` keeps its `errors == 0 ? 0 : 4`): a lost surface is an environment failure, not a
defect in the sample.

Each site carries the comment that this must **not** be turned into a retry, with the reason
(`Recreate` over the same `VkSurfaceKHR` cannot succeed).

### D3 — Capture the enumeration as XML docs on `AcquireResult`, `AcquireNextImage` and `Present`

The analysis currently lives in a GitHub comment and in the `<summary>` of a **private** method
(`Swapchain.cs:407-418`). It moves to:

- a `<remarks>` block on the `AcquireResult` **type**, holding the six-row handling table
  (result → state after → what the caller must do);
- an addition to the `SurfaceLost` member doc stating that the swapchain is already `Poisoned` and
  that the next acquire/present will throw, so the caller must terminate rather than `continue`;
- additions to `Timeout` and `NotReady` stating that a bare retry is correct, why (no state change,
  no semaphore signalled), and that both are acquire-only;
- a `<remarks>` on `Swapchain.Present(Queue, uint, in BinarySemaphore)` recording that the present
  side exposes only four of the six members and that `VK_TIMEOUT`/`VK_NOT_READY` from a present
  throw as a broken ICD.

No new `docs/` file. The material is "what to do with each of six enum members" — it belongs beside
the enum, where it cannot drift from it. `docs/ngx-notes.md` exists because NGX imposes obligations
the wrapper *cannot* express in types (licensing, DLL sourcing, jitter correctness); this is six
members with six documented handlings, and a second prose home would be a third copy alongside the
#120 spec's transition table.

### D4 — No wrapper code change

`src/Ahjo.Vulkan/` gets XML comments and nothing else. The samples were discarding a value the
wrapper already returns for this exact purpose; `ThrowIfNotPresentable` throwing is correct and
stays.

### Why not the alternatives

- **`SdlWindow.IsMinimized` (the issue's own suggestion).** Rejected as plausible-looking and wrong.
  The loop's question is not "is the window minimized" but "did the last `Recreate` produce a
  presentable swapchain"; the authority is `vkGetPhysicalDeviceSurfaceCapabilitiesKHR` on the
  *surface*, which `Recreate` consults twice (`Swapchain.cs:281-288` pre-drain, `:540-550`
  post-drain) precisely because the extent can go to zero while the drain blocks — a window flag read
  before the call cannot see that race. Zero extent is also not exclusive to minimize (a compositor
  may report `currentExtent == (0,0)` otherwise, and `ComputeExtent` can clamp to zero). And it does
  nothing at all for `SurfaceLost`, which is half of this issue. Growing a file compiled into five
  assemblies to add a proxy signal is the worst trade on the table.
- **Test `swap.State` instead of a local flag.** Rejected on a concrete failure. `Poisoned` is
  reached both by "a `Recreate` failed" — recoverable by another `Recreate`
  (`docs/design/specs/2026-06-12-issue-120-device-loss-design.md:266`) — and by "the surface was
  lost", which is not (`:264`). A loop written as `if (swap.State != Ready) TryRecreate(...)` would
  retry forever over a lost surface: exactly the failure D2 exists to prevent. `AcquireResult`
  distinguishes the two at the call site; `State` does not. (`Suboptimal` leaving the state `Ready`,
  `Swapchain.cs:425-426`, makes it an incomplete *trigger* as well — the point the issue body makes.)
- **A shared frame-loop helper in `src/Ahjo.Vulkan.Utilities/`.** Rejected: breaks the project's
  stated dep-free charter, imports `Ahjo.Vulkan` + `ppy.SDL3-CS` into five projects that do not have
  this bug (one of them the AOT publish canary), and still needs a rebuild callback per sample.
- **A shared helper file link-compiled into the four windowed samples** (the `SdlWindow.cs`
  mechanism, applied to a new `SampleSwapchainLoop.cs`). Rejected: it inherits `SdlWindow`'s problem
  — the file would be owned by `tests/` and compiled into the test assembly too — and it still hides
  the loop from the reader, which is D1's third reason.
- **A `Swapchain.IsPresentable` convenience property** (`State is Ready or NeedsRecreate`).
  Rejected: it is the `swap.State` alternative with a nicer name, and it would actively invite the
  infinite-retry-over-a-lost-surface loop.
- **Split `SwapchainState.Poisoned` into `SurfaceLost` and `RecreateFailed`.** This would be a
  genuine improvement to the wrapper's expressiveness and is the only rejected option with merit.
  Rejected *here*: it is a breaking change to a public #120-era enum, it is not needed to fix the
  samples (`AcquireResult` already distinguishes them at the call site), and folding an API break
  into a samples bugfix is how a small PR becomes an unreviewable one. Worth its own issue.
- **A new `docs/swapchain-lifecycle.md`.** Rejected as a third home for a table that already exists
  in the #120 spec and belongs on the enum.
- **Adding validation-error counters + a non-zero exit code to `HelloTriangle`/`HelloCube`** so the
  "0 validation errors" criterion is machine-checkable. Both currently print `[VK ERROR]` lines and
  `return 0` unconditionally (`HelloTriangle/Program.cs:252-259, :209`;
  `HelloCube/Program.cs:624-631, :488`). Rejected as scope creep — it changes what the samples
  *report*, not what they do wrong, and would be a behaviour change reviewers have to weigh
  separately. Verification for those two reads stderr instead.

## Verification

Honest about what can and cannot be shown.

**The minimize half is reproducible on hardware and must actually be run.** For each of
`HelloTriangle`, `HelloCube`, `HelloVmaWindowed`: launch, minimize, wait several seconds, restore,
let it render, close. Pass requires all four of — survives (no `InvalidOperationException`); does not
spin (CPU stays near-idle while minimized; `HelloDlaa` measured ~2.7% of a core); prints no
`[VK ERROR]` line; exits 0. Restore-to-the-same-size must be included, because that is the edge
`SdlWindow.cs:144` filters out. `HelloCube` additionally must still write its pipeline cache.

**The `SurfaceLost` half was not exercised on hardware in #217 and is not claimed as verified here.**
Provoking a real `VK_ERROR_SURFACE_LOST_KHR` needs a display-driver restart, a session switch or a
monitor topology change. The handling is *reasoned from* `Swapchain.cs:430-432` and `:451-460` and is
identical to the shape already merged in `HelloDlaa`. Anyone who does provoke it should report back
on the issue.

**No automated coverage is added, and that is a finding rather than an omission.** CI builds the
samples but runs only `AotSmoke` (`.github/workflows/ci.yml:333-343`); there is no harness that
drives a sample's frame loop. The wrapper side of this — that `Minimized` and `Poisoned` make
acquire/present throw — is already covered by `tests/Ahjo.Vulkan.Tests/SwapchainTests.cs:416-445`
via the `OverrideStateForTesting` seam. Covering the *sample* side would mean a scripted window
manager, and wrapper tests are Windows-only anyway (#32).

**No benchmarks.** Nothing under `Recording/`, `Sync/`, `Pools/` or `Memory/` changes; the only
`src/` edits are XML comments. The added `bool` and `Thread.Sleep` allocate nothing, and the three
samples are not zero-allocation samples to begin with (`HelloVmaWindowed/Program.cs:366` allocates a
`Buffer[]` per frame, as `HelloDlaa/Program.cs:28-30` notes) — this change neither fixes nor worsens
that.

## Cross-links

- Fixes: [#220](https://github.com/pekkah/Ahjo-Vulkan/issues/220).
- Propagates the fix merged in [#219](https://github.com/pekkah/Ahjo-Vulkan/issues/219) / PR #217
  (`samples/HelloDlaa/Program.cs`, commits `90d092f` minimize and `6e0b0e9` `SurfaceLost`).
- Must stay consistent with `docs/design/specs/2026-06-12-issue-120-device-loss-design.md` — the
  `SwapchainState` transition table at `:257-267` is the authority this design defers to; D3's XML
  docs restate a subset of it and must not contradict it.
- Depends on the `Minimized` semantics introduced by #110 and the retire-on-failure semantics of
  #112, both recorded in that same spec.
- Prevents recurrence of the #217 merge-gate finding in the remaining windowed samples.
- Suggested follow-ups, deliberately **not** in scope: split `SwapchainState.Poisoned`; enable
  `GenerateDocumentationFile` repo-wide; validation-error exit codes for `HelloTriangle`/`HelloCube`.

## Open items

Both are flagged in the plan as **OPEN:**; the implementer stops and asks.

1. **`GenerateDocumentationFile` is off repo-wide.** D3's XML docs therefore do not reach package
   consumers' IntelliSense. Turning it on is a one-line change to `Directory.Build.props` but would
   surface CS1591 (missing doc comment) across eight public projects under
   `TreatWarningsAsErrors=true`. Out of scope here; needs its own issue and a human decision.
2. **Whether `HelloTriangle`/`HelloCube` should gain validation-error exit codes** so the "0
   validation errors" pass criterion is machine-checkable rather than eyeballed. Rejected above as
   scope creep, but it is a judgement call the human may want to overturn while the files are open.
