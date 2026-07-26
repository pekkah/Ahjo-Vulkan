# Declared Vulkan capability tier: make CI's coverage ceiling explicit and non-degradable

**Issue:** [#158](https://github.com/pekkah/Ahjo-Vulkan/issues/158) — *CI: driver-gated tests skip silently, so a green Windows lane proves nothing about GPU correctness*
**Must land consistently with:** [#152](https://github.com/pekkah/Ahjo-Vulkan/issues/152) (the Windows lane's provisioned ICD does not answer `vkCreateInstance`; this spec deliberately does **not** fix that, it makes it visible and gives #152 a green-means-something finish line)
**Does not reopen:** [#32](https://github.com/pekkah/Ahjo-Vulkan/issues/32) — no Linux wrapper lane, no software-rasterizer coverage is proposed here
**Surfaced by:** [#155](https://github.com/pekkah/Ahjo-Vulkan/issues/155) / [#156](https://github.com/pekkah/Ahjo-Vulkan/issues/156)
**Date:** 2026-07-26

## Problem

The `Build & Test (windows-latest)` lane reports green whether or not any
GPU-touching test executed, and there are **four independent silent-degradation
paths** on that one job, not one:

1. **Driver-gated tests skip invisibly.** 225 of 392 wrapper tests skip with the
   reason `"No Vulkan driver on host."` and the job is green. The skip and a pass
   are indistinguishable without opening the log.
2. **The lane's own provisioning check asserts a file, not a capability.**
   `ci.yml:92` throws if `vk_swiftshader_icd.json` is missing. It exists; the ICD
   still never answers. A file-existence check standing in for a capability check
   is the same defect one level up.
3. **Two `Ahjo.Vulkan.Native.Tests` tests pass while executing nothing.**
   `VulkanLoaderSmokeTests.cs:68-73` accepts `VK_ERROR_INCOMPATIBLE_DRIVER` as a
   pass; `VulkanLoaderSmokeTests.cs:99-105` and `:113-118` `return` early on a
   null instance / zero devices. That step reports `Passed: 11`.
4. **The AOT smoke run prints a skip and exits 0.** `samples/AotSmoke/Program.cs:35-38`
   returns 0 on a driverless host. `ci.yml:132-137` claims the opposite in a
   comment — *"With SwiftShader provisioned the published exe also runs the full
   render→PNG round-trip, exercising the whole wrapper surface under AOT
   codegen"*. That claim is false in every run on `main` today.

The motivating case (#156) is the sharpest form. `SplitBarrierTests` has 7 tests;
3 of them (`SplitBarrierTests.cs:60, 89, 146`) route through
`SkipUnlessValidatedSubmitPossible()` (`SplitBarrierTests.cs:276-282`), which
requires a driver **and** a non-software ICD **and** the Khronos validation layer.
`WaitEvent_MismatchedDependency_TripsValidation` (`SplitBarrierTests.cs:146-180`)
is the negative control proving `VUID-vkCmdWaitEvents2-pEvents-10788` is a live
oracle. All 7 skipped on CI; the 7-passed evidence exists only on one developer's
NVIDIA host.

## Evidence

### What the runner actually does — measured, not reasoned

From run [30200160392](https://github.com/pekkah/Ahjo-Vulkan/actions/runs/30200160392)
(`main`, the merge of #156), `Test — Ahjo.Vulkan.Tests` step:

```
Total tests: 392
     Passed: 159
    Skipped: 233
```

Every one of the 233 skip reasons, tallied from the step's own output:

| Reason string | Count | Class |
|---|---|---|
| `No Vulkan driver on host.` | **225** | coverage gap |
| `Xlib surface test.` | 3 | correct + permanent on Windows |
| `Wayland surface test.` | 3 | correct + permanent on Windows |
| `Metal surface test.` | 2 | correct + permanent on Windows |

So the invisible hole is **225 of 392 tests (57%)**, and exactly **8** skips are
the correct-and-permanent kind. The two are today reported identically.

The lane *does* provision an ICD and it *is* wired: `VK_DRIVER_FILES` reaches the
test step (`ci.yml:87-98`; the step's env dump shows
`VK_DRIVER_FILES=D:\a\Ahjo-Vulkan\Ahjo-Vulkan\build\vulkan\win-x64\vk_swiftshader_icd.json`
and the provisioned directory first on `PATH`). A loader loads —
`EnumerateInstanceVersion_ReturnsAtLeast_1_0` passes, which requires a real
`vulkan-1.dll`. What fails is the ICD: `VulkanDriverProbe._hasDriver`
(`VulkanDriverProbe.cs:13-33`) calls `Vk.vkCreateInstance` and gets a non-success
result, cleanly returning `false` (had the P/Invoke thrown, the 225 tests would
have errored, not skipped).

**Root-causing that belongs to #152 and this design does not depend on it.** Two
candidates worth recording: the loader that wins `LoadLibrary("vulkan-1.dll")`
may be a system copy predating `VK_DRIVER_FILES` support (loader 1.3.207+), or
the cached Silk.NET SwiftShader payload's manifest may not resolve its
`library_path` on win-x64. The cache hit (`vk-win-x64-loader2025.9.12-sws2025.9.8`)
means the provisioning step's file listing was not printed in this run, so the
staged contents are not in evidence.

### A third invisible gate nobody has noticed: no SPIR-V on the runner

`glslc` is not on `windows-latest`. The build step emits 20+
`warning MSB3073 … exited with code 9009` for every `CompileShaders` target
(`tests/Ahjo.Vulkan.Tests.csproj:44-52` uses `ContinueOnError="WarnAndContinue"`,
so the build stays green; `TreatWarningsAsErrors` covers compiler diagnostics, not
MSBuild task warnings). 35 gate sites in the wrapper suite are
`Assert.SkipUnless(File.Exists(<…>SpvPath), …)`. None of them showed up in the
233 skips **because the driver gate short-circuits first** — the shader hole is
hiding behind the driver hole.

### How the gates are implemented today — not uniform, and not classified

361 `Assert.Skip*(…)` call sites across 41 files in `tests/Ahjo.Vulkan.Tests/`,
by predicate:

| Class | Sites | Predicate | Nature |
|---|---|---|---|
| driver | **231** | `VulkanDriverProbe.HasDriver` | coverage gap without an ICD |
| hardware | **40** | `VulkanDriverProbe.IsSoftwareDriver` (`VulkanDriverProbe.cs:67-102`, `deviceType == CPU`) | coverage gap on a CPU ICD |
| validation | **13** | `VulkanDriverProbe.HasValidationLayer` (`VulkanDriverProbe.cs:35-59`) | coverage gap without the layer |
| SPIR-V | **35** | `File.Exists(<…>SpvPath)` | toolchain gap (glslc) |
| other | 42 | `IsWindows`/`IsLinux`/`IsMacOS`, `HasInstanceExtension`, `SdlWindow.IsAvailable`, `WAYLAND_DISPLAY`, `SupportsBindless*`, `samplerAnisotropy`, `deviceLimit < 224`, 3 bare `Assert.Skip(…)` | mostly correct + permanent |

Site counts are not test counts: `SplitBarrierTests.cs:276-282` is one site shared
by three tests, so the validation class covers 15 tests, not 13, and the hardware
class covers 42.

Nothing in the reason strings distinguishes the four gap classes from the 42
"other" sites. A tool cannot tell `"Wayland surface test."` (correct forever on
Windows) from `"No Vulkan driver on host."` (a hole) without hardcoding strings.

### The mechanism this design needs already exists — in one lane only

`AHJO_REQUIRE_VULKAN_DEVICE=1` turns a driverless skip into a hard failure. It is
set on exactly one lane (`ci.yml:225-232`, `vma-linux`), read in exactly one place
(`VmaSmokeTests.cs:183-192`), and documented in two spots (`.github/CLAUDE.md:13`,
`tests/CLAUDE.md:14`). It exists *because* #144 shipped a `libvma.so` that
SIGSEGVed, proven by nothing. The wrapper suite — 225 skipped tests — does not
read it. The precedent and its rationale are already the repo's, they were just
never applied to the lane that matters most.

### Two conditional oracles that the tier makes safe rather than needing rewrites

`MemoryAliasingTests.cs:121-122` sets `EnableValidation = VulkanDriverProbe.HasValidationLayer`
and `:201-204` asserts `errorCount == 0` only `if (HasValidationLayer)`. The test
passes either way — its oracle silently evaporates. This is *not* a defect to fix
by rewriting the test: once a lane **declares** that it has the layer and that
declaration is enforced, the condition is guaranteed true wherever it matters, and
guaranteed-irrelevant where the tier says the layer is absent. Same reasoning for
the SPIR-V `File.Exists` gates.

### The hard ceiling, stated plainly

`WaitEvent_MismatchedDependency_TripsValidation` requires a driver **and**
`!IsSoftwareDriver` **and** the validation layer (`SplitBarrierTests.cs:276-282`).
No GitHub-hosted runner exposes a hardware Vulkan device — the repo's own workflow
records this (`ci.yml:15-19`: *"Hosted Windows runners ship no Vulkan loader /
ICD"*). Even a fully fixed #152 yields at best a CPU ICD, which `IsSoftwareDriver`
excludes by the standing #32/#144 policy that software rasterizers are not honest
coverage.

**Therefore: option 1 of the issue — "make green mean the GPU tests ran" — is
unachievable on hosted runners for the 15-test validation-layer class, and no
amount of provisioning changes that.** Installing the validation layer (option 4)
does not help either: the layer needs an instance, an instance needs an ICD, and
the tests it would unblock are gated on `!IsSoftwareDriver` before they ever ask
about the layer.

## Decision

**Every lane declares the Vulkan capability tier it expects in
`AHJO_VULKAN_TIER`; the test suites enforce that declaration and fail loudly when
the host falls short; every skip carries a machine-readable `[gate:*]` class; and
a job-summary step turns the classified counts into the run's headline verdict.
The declared tier appears in the job's check name, so the checks list — not the
logs — says what a green tick covered.**

The tier ladder, ordered:

| `AHJO_VULKAN_TIER` | Host must provide |
|---|---|
| `none` | nothing; every driver-gated test may skip |
| `software` | `vkCreateInstance` succeeds and `vkEnumeratePhysicalDevices` reports ≥ 1 device (CPU `deviceType` allowed) |
| `hardware` | as `software`, and the first enumerated device is **not** `VK_PHYSICAL_DEVICE_TYPE_CPU` |
| `validation` | as `hardware`, and `VK_LAYER_KHRONOS_validation` is enumerable |

Four teeth, each independent:

1. **A contract test** (`DeclaredTier_IsSatisfiedByHost`) is the *single* point of
   failure when the host is below the declaration. Gates themselves always skip,
   never fail — 225 red tests would bury the one actionable message.
2. **Classified skips.** `TestGate.Require*` emits `[gate:driver]`,
   `[gate:hardware]`, `[gate:validation]`, `[gate:spirv]`, `[gate:platform]`,
   `[gate:feature]`. The summary step **fails the job on any unclassified skip**,
   so the next ad-hoc `Assert.Skip("…")` cannot slip back into invisibility.
3. **A cross-check.** If a gap class at or below the declared tier has a non-zero
   count, that contradicts tooth 1 and the summary emits `::error::` — a
   tier-aware gate that got miswired cannot hide.
4. **The tier in the check name and the summary.** `Build & Test (windows-latest,
   Vulkan <tier>)` plus a `$GITHUB_STEP_SUMMARY` table. Changing the tier changes
   the check name, which is deliberate friction. Free to do: `main` has no branch
   protection and no rulesets (`gh api …/protection` → 404, `…/rulesets` → `[]`).

### What this does and does not achieve

- **Silent degradation: solved.** The day the runner loses its ICD or the layer,
  the lane that declared them goes red with one named failure.
- **Positive proof at the achievable tier: solved.** Once #152 lands and the lane
  declares `software`, green means the `software`-tier tests ran.
- **Positive proof for the validation-layer class: not solved, and not solvable
  here.** 15 tests including the #156 negative control will remain declared-absent
  on hosted runners. The design's job is to make that a counted, named,
  non-degradable fact instead of a green tick.
- **The developer's local run becomes checkable evidence.** A contributor who
  exports `AHJO_VULKAN_TIER=validation` gets a run in which the contract test
  itself proves the layer was live. "7 passed locally" stops being a claim and
  becomes a machine-checked one. This is the recurrence fix for #156's shape.

### Why not the alternatives

- **Option 1 as written — fail the lane when there's no driver.** Correct
  instinct, wrong as an unconditional rule: it hardcodes one expectation into a
  suite that runs on hosted CI (no ICD), a lavapipe lane (CPU ICD), and developer
  boxes (hardware + layer). Adopted in the tier-relative form — "fail when the
  host is below what *this lane declared*" — which is the same tooth with a
  parameter.
- **Option 2 — pin an expected skip count.** Rejected as a gate. The count is
  measured to move for reasons unrelated to coverage: #152 recorded 226 skipped of
  382 on 2026-07-22; the same lane four days later shows 233 of 392 with the
  driver-gap subset at 225. Every PR that adds a driver-gated test must bump it,
  which is precisely the shape that becomes a rubber stamp. A *tier* changes only
  when the runner's capabilities change — exactly when a human should think. The
  count survives as reported output (tooth 4), never as the gate.
- **Option 3 — split into `Build & Test` (zero driver skips) + a `no-GPU` job.**
  Rejected: on hosted runners the "must be fully green with zero driver-gated
  skips" job can never exist, so the split ships a permanently-red or
  permanently-empty job. The tier in the check name achieves the same
  visible-in-the-checks-list goal for one job's cost.
- **Option 4 — install `VK_LAYER_KHRONOS_validation` on the Windows runner.**
  Rejected as a standalone step, on evidence: the layer needs an instance, an
  instance needs an ICD (#152 open), and the 15 tests it would unblock skip on
  `IsSoftwareDriver` first (`SplitBarrierTests.cs:279`). Worth revisiting only
  after #152, and only together with a decision about whether SwiftShader-on-Windows
  may run submitting tests (see follow-ups).
- **Classify skips by xUnit `[Trait]` instead of reason-string prefixes.**
  Rejected: traits are static per test, but a test can be gated on several things
  and only one gate fires in a given run. The runtime reason string is the honest
  record of why *this* run skipped.
- **Have `TestGate.Require*` fail instead of skip when the tier demands it.**
  Rejected for ergonomics: a lane that declares `software` against a driverless
  host would go red with 231 failures. One contract failure plus a counted summary
  says the same thing legibly.
- **Parse the console log for skip counts.** Rejected in favour of a `trx` logger:
  a structured artifact beats regexing `verbosity=detailed` output, and `--blame`
  already writes to `TestResults/`.
- **Fix #152's root cause in this change.** Rejected: separate defect, separate
  issue, and folding it in would make this design's correctness depend on an
  unresolved investigation. This spec is what gives #152 a finish line —
  raise the declared tier to `software` and let the contract test prove it.
- **Reopen Linux wrapper coverage / add software-rasterizer coverage.** Out of
  scope by standing decision (#32, `.github/CLAUDE.md:5`). Nothing here does that.

### Uncertainty, recorded

- **Why the provisioned SwiftShader does not answer is unresolved** (#152). The
  design is deliberately independent of the answer; the declared tier for the
  Windows lane is the one thing that depends on it, and that is the OPEN item.
- **Whether SwiftShader-on-Windows can execute the submitting tests is unknown.**
  The #32/#144 SIGSEGV evidence is lavapipe- and SwiftShader-*on-Linux*. The lane
  has never had a working ICD, so it has never been tested. If it can, the
  validation-layer class could become CI-provable — a large prize, and the reason
  the ladder separates `software` from `hardware` rather than collapsing them.
- **`VulkanDriverProbe._hasValidationLayer` short-circuits on `_hasDriver`**
  (`VulkanDriverProbe.cs:37`). Layer enumeration does not actually require an ICD,
  so this couples two independent facts. The coupling to *a driver* is kept: every
  layer-gated test needs a device to do anything with the layer, so a driverless
  host should report the driver gap, not a layer gap.

  **Corrected after review (2026-07-26).** An earlier draft of this note claimed the
  coupling "has no effect on any current outcome (every layer-gated test needs a
  device anyway)" and proposed deriving the layer bit from the ordered ladder as
  `Observed >= Validation`. That was wrong, and the implementation does not do it:
  - True for `RequireDriver`, false for `RequireHardwareDriver`. Ten sites gate on
    driver + validation with **no** hardware gate — `InstanceCreateTests.cs:36, 102,
    154, 188, 226, 250`, `InstanceFunctionTableTests.cs:12`,
    `QueueOwnershipTransferTests.cs:25, 141`, `CommandRecorderTests.cs:744`. A CPU
    device caps `Observed` at `Software`, so on a software-ICD host that *has* the
    layer those ten tests ran before this change and would skip after it, reported
    as `[gate:validation] … not installed` when the layer is installed and the real
    cause is the device type. A misdiagnosis by the mechanism whose purpose is
    honest classification.
  - It would also cap the Windows lane at `software` forever, foreclosing the prize
    named two bullets down — the one route to CI-provable validation-layer coverage.

  So: the ordered enum describes the **declaration** only. `HasValidationLayer` is
  `HasDriver && <independent layer probe>`, and the `Validation` rung of `Observed`
  reads that same independent bit. Adding `RequireHardwareDriver` to those ten sites
  was considered and rejected — it would silently narrow ten tests' coverage to buy
  a tidier ladder.

## Stopping the recurrence

Three durable hooks, all cheap:

1. **`.github/CLAUDE.md`** — every lane that runs a Vulkan-touching suite declares
   `AHJO_VULKAN_TIER`; **lowering a declared tier to make a lane green is
   prohibited**, and the file records why hosted runners cap at `software`.
2. **`tests/CLAUDE.md`** — new gates go through `TestGate.Require*`, never a bare
   `Assert.Skip`; contributors whose feature's only oracle is the validation layer
   run with `AHJO_VULKAN_TIER=validation` and quote that run.
3. **`.github/pull_request_template.md`** (none exists today) — a Vulkan-coverage
   line requiring the declared tier of the run whose results the PR quotes. This
   is what would have caught #156: "7 passed" with no tier is not evidence.

## Named as a future option, not designed here

A self-hosted or GPU-enabled runner is the only thing that makes the
`validation` tier reachable in CI, and the repo already anticipates one
(`.github/CLAUDE.md:5`, #32's closing note). This design is what makes such a
runner a one-line change when it arrives: provision it and raise the lane's
declared tier. **No self-hosted runner is designed, costed, or recommended here.**

Two follow-up issues should be filed rather than folded in: (a) provision `glslc`
on the Windows lane and add a declared SPIR-V requirement — worthless before #152,
since no shader test can reach its SPIR-V gate while the driver gate fires first;
(b) determine whether SwiftShader-on-Windows may run submitting tests, i.e.
whether the `IsSoftwareDriver` gate should narrow from "all software ICDs" to
"lavapipe on Linux".

## Cross-links

- **Resolves:** #158.
- **Gives a finish line to:** #152 — its PR raises the Windows lane's declared
  tier from `none` to `software`, and the contract test is what proves the fix.
- **Preserves:** the #144 protection currently carried by
  `AHJO_REQUIRE_VULKAN_DEVICE` on `vma-linux`, re-expressed as
  `AHJO_VULKAN_TIER=software`. The old variable becomes a hard error if set, so a
  stale lane or script cannot silently lose its guard.
- **Does not touch:** `Ahjo.Vulkan.Ktx.Native.Tests` — contract is that it runs
  with no loader and no ICD (`tests/CLAUDE.md`, `.github/CLAUDE.md:19`), so it
  stays outside the tier system entirely. `publish.yml` runs no `dotnet test` of
  its own; it calls `build-ktx-native.yml`, which is also excluded.
- **Consistent with:** #32 (`.github/CLAUDE.md:5, 11`) — no new Linux wrapper
  lane, no software-rasterizer coverage claimed as honest coverage.
