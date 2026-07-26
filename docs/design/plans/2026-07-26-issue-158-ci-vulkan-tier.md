Paired with [../specs/2026-07-26-issue-158-ci-vulkan-tier-design.md](../specs/2026-07-26-issue-158-ci-vulkan-tier-design.md)

# Implementation plan — issue #158: declared Vulkan capability tier

Ten steps. Steps 1-3 are the mechanism, 4-7 close the four silent-degradation
paths, 8-10 are CI, docs, and verification. **Step 8 contains an OPEN item that
must be answered before it is implemented.**

Nothing in `src/Ahjo.Vulkan*/` production code changes. `Generated/` is untouched.
`tests/Ahjo.Vulkan.Ktx.Native.Tests/` is untouched by design.

---

## Step 1 — New shared source file: the tier vocabulary and probe

Create `tests/Shared/VulkanEnvironment.cs`, namespace `Ahjo.Vulkan.Testing`.
It depends only on `Ahjo.Vulkan.Native` (`Vk`, `VkResult`, `VkPhysicalDeviceType`,
`VkLayerProperties`), which all three consuming projects already reference.

```csharp
namespace Ahjo.Vulkan.Testing;

/// <summary>Ordered ladder of Vulkan capability a test host can offer.</summary>
internal enum VulkanCapability { None = 0, Software = 1, Hardware = 2, Validation = 3 }

internal static unsafe class VulkanEnvironment
{
    public const string TierVariable    = "AHJO_VULKAN_TIER";
    public const string RetiredVariable = "AHJO_REQUIRE_VULKAN_DEVICE";

    /// <summary>Parsed <c>AHJO_VULKAN_TIER</c>. Unset => <see cref="VulkanCapability.None"/>.</summary>
    public static VulkanCapability Declared { get; }

    /// <summary>What this host actually offers. Probed once, cached.</summary>
    public static VulkanCapability Observed { get; }

    /// <summary>One sentence naming why <see cref="Observed"/> stopped where it did.</summary>
    public static string ObservedDetail { get; }

    public static bool HasDriver          { get; }
    public static bool IsSoftwareDriver   { get; }
    public static bool HasValidationLayer { get; }
}
```

Behaviour, exactly:

- `Declared` parses case-insensitively against `none`/`software`/`hardware`/`validation`.
  Unset or empty → `None`.
- An unrecognised value throws `InvalidOperationException` with:
  `AHJO_VULKAN_TIER='<value>' is not a recognized tier. Expected one of: none, software, hardware, validation.`
- If `AHJO_REQUIRE_VULKAN_DEVICE` is set to anything, `Declared` throws
  `InvalidOperationException`:
  `AHJO_REQUIRE_VULKAN_DEVICE is no longer read (issue #158). Set AHJO_VULKAN_TIER=software instead, then unset AHJO_REQUIRE_VULKAN_DEVICE.`
  Fail-closed: a stale lane or shell script must not silently lose its guard.
- `Observed` probes once and sets `ObservedDetail` to the reason it stopped:
  - `vkCreateInstance` non-success → `None`, detail
    `vkCreateInstance returned <VkResult>` (catch `DllNotFoundException` →
    `None`, detail `no vulkan-1 loader on this host`).
  - zero physical devices → `None`, detail `vkEnumeratePhysicalDevices reported zero devices`.
  - first device `deviceType == VK_PHYSICAL_DEVICE_TYPE_CPU` → `Software`, detail
    `first enumerated device reports VK_PHYSICAL_DEVICE_TYPE_CPU (software ICD)`.
  - layer not enumerable → `Hardware`, detail
    `VK_LAYER_KHRONOS_validation is not installed`.
  - otherwise → `Validation`, detail `hardware device + VK_LAYER_KHRONOS_validation`.
- `HasDriver` = `Observed >= Software`; `IsSoftwareDriver` = `Observed == Software`;
  `HasValidationLayer` = `Observed >= Validation`.

Move the probe bodies from `tests/Ahjo.Vulkan.Tests/VulkanDriverProbe.cs:13-33`
(`_hasDriver`), `:35-59` (`_hasValidationLayer`) and `:67-102` (`_isSoftwareDriver`)
here, plus the `Match(sbyte*, ReadOnlySpan<byte>)` helper at `:239-246`. Keep the
instance `apiVersion` values as they are today (1.0 for the driver probe, 1.3 for
the device-type probe) — do not "tidy" them; a change there changes what the probe
accepts.

Preserve the existing coupling: `HasValidationLayer` stays reachable only when a
driver exists. Documented as deliberate in the spec's uncertainty section — do not
decouple it in this change.

## Step 2 — New shared source file: `TestGate`

Create `tests/Shared/TestGate.cs`, namespace `Ahjo.Vulkan.Testing`.

```csharp
internal static class TestGate
{
    public static void RequireDriver();                                     // [gate:driver]
    public static void RequireHardwareDriver(string reason);                // [gate:hardware]
    public static void RequireValidationLayer();                           // [gate:validation]
    public static void RequireSpirv(string spvPath);                        // [gate:spirv]
    public static void RequirePlatform(bool condition, string reason);      // [gate:platform]
    public static void RequireDeviceFeature(bool condition, string reason); // [gate:feature]
    public static void Unsupported(string reason);                          // [gate:feature], always skips
}
```

Every method **skips, never fails** (spec: the contract test in step 3 is the
single point of failure). Reason strings, exactly:

| Method | Skip reason emitted |
|---|---|
| `RequireDriver` | `[gate:driver] No Vulkan driver on host.` |
| `RequireHardwareDriver(reason)` | `[gate:hardware] {reason}` |
| `RequireValidationLayer` | `[gate:validation] VK_LAYER_KHRONOS_validation is not installed.` |
| `RequireSpirv(path)` | `[gate:spirv] Compiled shader missing: {path} (glslc not on PATH at build time).` |
| `RequirePlatform(_, reason)` | `[gate:platform] {reason}` |
| `RequireDeviceFeature(_, reason)` | `[gate:feature] {reason}` |
| `Unsupported(reason)` | `[gate:feature] {reason}` |

Predicates: `RequireDriver` → `VulkanEnvironment.HasDriver`;
`RequireHardwareDriver` → `!VulkanEnvironment.IsSoftwareDriver` (and it implies
a driver — call `RequireDriver()` first at the call sites that do so today, do not
fold it in); `RequireValidationLayer` → `VulkanEnvironment.HasValidationLayer`;
`RequireSpirv` → `File.Exists(spvPath)`.

## Step 3 — New shared source file: the contract test

Create `tests/Shared/VulkanTierContractTests.cs`, namespace `Ahjo.Vulkan.Testing`,
one public class with one `[Fact]`:

```csharp
public sealed class VulkanTierContractTests
{
    [Fact]
    public void DeclaredTier_IsSatisfiedByHost() { … }
}
```

Body: read `VulkanEnvironment.Declared` and `.Observed`. If
`Observed >= Declared`, pass and write one informational line through
`ITestOutputHelper`:
`AHJO_VULKAN_TIER declared=<declared> observed=<observed> (<detail>)`.

Otherwise `Assert.Fail` with exactly:

```
AHJO_VULKAN_TIER=<declared> was declared, but this host only reaches '<observed>':
<ObservedDetail>.
Driver-gated tests will have skipped instead of running. Fix this lane's Vulkan
provisioning. Do not lower the declared tier to make CI green — see
docs/ci-coverage.md and .github/CLAUDE.md.
```

Link all three shared files into the three suites (and only these three) via
`<Compile Include="..\Shared\*.cs" LinkBase="Shared" />` in:

- `tests/Ahjo.Vulkan.Tests/Ahjo.Vulkan.Tests.csproj`
- `tests/Ahjo.Vulkan.Native.Tests/Ahjo.Vulkan.Native.Tests.csproj`
- `tests/Ahjo.Vulkan.Vma.Native.Tests/Ahjo.Vulkan.Vma.Native.Tests.csproj`

**Not** `Ahjo.Vulkan.Ktx.Native.Tests` — its contract is to run with no loader and
no ICD.

## Step 4 — Rewrite the wrapper suite's gates through `TestGate`

`tests/Ahjo.Vulkan.Tests/`, 361 `Assert.Skip*` sites across 41 files. Mechanical;
do it as five passes so each is reviewable.

1. **231 sites**, exact-string substitution:
   `Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");`
   → `TestGate.RequireDriver();`
2. **40 sites**: `Assert.SkipWhen(VulkanDriverProbe.IsSoftwareDriver, "<reason>");`
   → `TestGate.RequireHardwareDriver("<reason>");` — reason text preserved verbatim,
   including the multi-line ones (`CommandRecorderTests.cs:36`, `FrameRingTests.cs:56`,
   `SplitBarrierTests.cs:279`, …).
3. **13 sites**: `Assert.SkipUnless(VulkanDriverProbe.HasValidationLayer, "…");`
   → `TestGate.RequireValidationLayer();` (drops the per-site reason text in favour
   of the canonical one).
4. **35 sites**: `Assert.SkipUnless(File.Exists(<X>SpvPath), "…");`
   → `TestGate.RequireSpirv(<X>SpvPath);`
5. **42 remaining sites** → `TestGate.RequirePlatform` / `RequireDeviceFeature` /
   `Unsupported`, choosing by nature not by predicate shape:
   - `RequirePlatform`: `IsWindows`/`IsLinux`/`IsMacOS`, `HasInstanceExtension("VK_KHR_xlib_surface"u8)`,
     `HasInstanceExtension("VK_KHR_wayland_surface"u8)`, `HasInstanceExtension("VK_EXT_headless_surface"u8)`,
     `SdlWindow.IsAvailable`, `LinuxXlibWindow.IsAvailable`, `WAYLAND_DISPLAY`,
     `wlDisplay == 0 || wlSurface == 0` — plus the bare
     `Assert.Skip($"SDL Metal view unavailable: {ex.Message}")`.
   - `RequireDeviceFeature`: `SupportsBindlessStorageBuffer`, `SupportsBindlessSampledImage`,
     `samplerAnisotropy`, `anisotropySupported`, `supportsImmediate`, `deviceLimit < 224`,
     `device is null`.
   - `Unsupported`: the two bare
     `Assert.Skip("No physical device with separate graphics + dedicated-transfer queue families.")`
     sites.

Then reduce `tests/Ahjo.Vulkan.Tests/VulkanDriverProbe.cs`: delete `_hasDriver`,
`_hasValidationLayer`, `_isSoftwareDriver` and `Match`; forward the three public
properties (`HasDriver`, `HasValidationLayer`, `IsSoftwareDriver` at `:104-106`)
to `VulkanEnvironment` so the remaining consumers keep compiling —
`MemoryAliasingTests.cs:121-122, 201` and `SplitBarrierTests` (post-rewrite) must
still build without further edits. Keep `_features12`, `SupportsBindless*`,
`HasInstanceExtension` and `_extensionCache` where they are; they are
wrapper-suite-specific.

**Do not change any test's observable behaviour in this step** beyond the reason
string. No test gains or loses a gate.

## Step 5 — `Ahjo.Vulkan.Native.Tests`: stop passing on nothing

`tests/Ahjo.Vulkan.Native.Tests/VulkanLoaderSmokeTests.cs`:

- `:66-78` — the `result == VK_SUCCESS || result == VK_ERROR_INCOMPATIBLE_DRIVER`
  tolerance becomes tier-conditional: tolerate `VK_ERROR_INCOMPATIBLE_DRIVER` only
  when `VulkanEnvironment.Declared == VulkanCapability.None`; otherwise require
  `VK_SUCCESS`. Assertion message:
  `vkCreateInstance returned {result}; AHJO_VULKAN_TIER={declared} requires a working ICD.`
- `:99-105` and `:113-118` — replace the bare `return;` early-outs with
  `TestGate.RequireDriver();` at the top of the test, so a driverless run reports a
  classified **skip** rather than a pass. Keep the `count == 0` early-out only
  under `Declared == None`; above that, assert `count >= 1`.
- Update the stale comments at `:68-70` and `:100-104` that describe the tolerance
  as intentional.

## Step 6 — `Ahjo.Vulkan.Vma.Native.Tests`: migrate off the retired variable

`tests/Ahjo.Vulkan.Vma.Native.Tests/VmaSmokeTests.cs`: delete the `NoDriver` helper
(`:183-192`) and its `[DoesNotReturn]` plumbing; replace the two call sites
(`:52`, `:61`) with `TestGate.RequireDriver();` moved to the top of
`CreateAllocator_AllocateBuffer_DestroyAllocator_RoundTrips`, and keep the
`deviceCount == 0` assertion as `Assert.True(deviceCount >= 1, …)`.

The #144 protection is preserved by the linked contract test (step 3) plus the
lane declaring `AHJO_VULKAN_TIER=software` (step 8) — verify this explicitly during
step 10.

`LoadVulkanLoader` (`:202-218`) stays; it intentionally mirrors
`VulkanLoaderResolver`'s candidate list.

## Step 7 — `samples/AotSmoke`: stop exiting 0 on a driverless host

`samples/AotSmoke/Program.cs`:

- At `:35-38`, gate the early `return 0` on the declared tier. When
  `AHJO_VULKAN_TIER` is unset or `none`, keep today's behaviour (print, exit 0).
  Otherwise print to `Console.Error`:
  `AHJO_VULKAN_TIER=<value> requires a Vulkan device, but none was found; AOT publish succeeded, smoke run did not execute.`
  and `return 3` (2 is already taken by the missing-shader path at `:45-49`).
- Read the variable **locally** — three lines, `Environment.GetEnvironmentVariable`
  plus a string compare. Do **not** link `tests/Shared/*.cs` into a sample:
  `samples/` must not depend on test sources. Add a comment saying the duplication
  is deliberate so a reviewer does not "fix" it.
- Rewrite the comment at `:29-34` — it cites issue 32 as the reason no ICD is
  present, which is now #152's territory.
- AOT constraint: env-var read plus string compare only. No reflection, no new
  dependency. `PublishAot` must stay warning-free.

## Step 8 — `.github/workflows/ci.yml`

### 8a — declare the tier on the wrapper lane

Give `build-test` (`ci.yml:27-29`) a single-entry matrix so the tier reaches the
job name (`env` is not a valid context in `jobs.<id>.name`; `matrix` is):

```yaml
  build-test:
    name: Build & Test (windows-latest, Vulkan ${{ matrix.vulkan-tier }})
    runs-on: windows-latest
    strategy:
      matrix:
        include:
          - vulkan-tier: <see OPEN below>
    env:
      AHJO_VULKAN_TIER: ${{ matrix.vulkan-tier }}
```

> **OPEN — needs a human decision before implementing 8a.** What tier does the
> Windows lane declare on landing?
> - **`none`** (recommended): honest about today, lands green, and #152's PR is
>   the change that raises it to `software` — at which point that lane's green
>   means something for the first time. Costs: green still does not prove GPU
>   tests ran, only that nothing regressed *and that the check name says so*.
> - **`software`**: the lane provisions an ICD on purpose, so `software` is what it
>   *intends*; declaring it turns #152 red immediately and blocks all PRs until
>   fixed. Maximum loudness, immediate cost to unrelated work.
>
> Do not pick one by inference. The rest of the plan is identical either way.

### 8b — replace the file-existence check with a real one

`ci.yml:87-98` keeps the manifest-missing throw, and gains a line noting that
manifest presence is *not* proof the ICD answers — the contract test is.

### 8c — trx logger + the coverage summary step

Add `--logger "trx;LogFileName=wrapper.trx"` to the `Test — Ahjo.Vulkan.Tests`
step (`ci.yml:121-122`); `--results-directory TestResults` is already there.

New step after it, `if: always()`, `shell: pwsh`, name
`Vulkan coverage summary`. It reads `TestResults/wrapper.trx` (`Select-Xml`),
counts `UnitTestResult` outcomes, buckets `NotExecuted` results by the
`[gate:<class>]` prefix in their `Output/ErrorInfo/Message`, and appends to
`$env:GITHUB_STEP_SUMMARY`:

```markdown
## Vulkan test coverage — windows-latest

| | |
|---|---|
| Declared tier | `none` |
| Tests total | 392 |
| Executed | 159 |
| Skipped | 233 |

### Skips by class
| Class | Count | Meaning at declared tier `none` |
|---|---|---|
| `driver` | 225 | NOT PROVEN — needs tier `software` |
| `hardware` | 0 | NOT PROVEN — needs tier `hardware` |
| `validation` | 0 | NOT PROVEN — needs tier `validation` |
| `spirv` | 0 | NOT PROVEN — glslc absent (follow-up) |
| `platform` | 8 | expected and permanent on this OS |
| `feature` | 0 | device does not advertise the feature |

**225 of 392 wrapper tests did not execute.** Tier `none` is declared, so this run
proves nothing about GPU behaviour. See docs/ci-coverage.md.
```

The step fails (`::error::` + `exit 1`) on either condition:

- **Unclassified skip** — any `NotExecuted` result whose message lacks a `[gate:`
  prefix:
  `::error::<N> skipped tests carry no [gate:*] class. Every skip must go through Ahjo.Vulkan.Testing.TestGate so the coverage summary can tell a permanent platform skip from a coverage gap. Unclassified: <fully-qualified name> — "<reason>"` (list all).
- **Gap at or below the declared tier** — e.g. `driver` count > 0 while
  `AHJO_VULKAN_TIER` is `software` or higher:
  `::error::gate class '<class>' skipped <N> tests, but AHJO_VULKAN_TIER=<declared> declares that capability present. A tier-aware gate is miswired.`

It must not mask a test failure: the test step's own result stands on its own
because the summary step uses `if: always()`.

### 8d — the `vma-linux` lane

Replace `AHJO_REQUIRE_VULKAN_DEVICE: "1"` with `AHJO_VULKAN_TIER: "software"`
(`ci.yml:229-232`) and update the rationale comment at `:225-228` to name the
contract test as the enforcement point. Add `Vulkan software` to the job name
(`ci.yml:171`) for consistency with 8a.

### 8e — correct the false AOT comment

`ci.yml:132-137` claims the AOT exe "runs the full render→PNG round-trip". It does
not (`samples/AotSmoke/Program.cs:35-38`). Rewrite to say the load-bearing check is
the `PublishAot` build, and that the smoke *run* executes only at tier `software`
or above.

## Step 9 — Docs

- **New `docs/ci-coverage.md`** — the tier table, what each tier requires, why
  hosted runners cap at `software`, why the 15 validation-layer-oracle tests can
  never run in CI, and how to run locally with `AHJO_VULKAN_TIER=validation`. This
  is the file the contract test's and summary step's failure messages point at, so
  it must exist before those messages ship.
- **`.github/CLAUDE.md`** — rewrite `:13`: every lane running a Vulkan-touching
  suite declares `AHJO_VULKAN_TIER`; the contract test enforces it; **lowering a
  declared tier to make a lane green is prohibited**; hosted runners cap at
  `software`. Add the retirement of `AHJO_REQUIRE_VULKAN_DEVICE`. Leave the #32
  paragraph at `:5` alone.
- **`tests/CLAUDE.md`** — replace the `AHJO_REQUIRE_VULKAN_DEVICE` bullet (`:14`)
  with the tier ladder; add: new gates go through `TestGate.Require*`, never a bare
  `Assert.Skip` — CI fails on unclassified skips; if your feature's only oracle is
  the validation layer, run with `AHJO_VULKAN_TIER=validation` and quote that run.
- **New `.github/pull_request_template.md`** — none exists. Minimal: summary,
  `Closes #NN`, and a **Vulkan coverage** section asking for the declared tier of
  the run whose results are quoted, with a note that "N passed locally" without a
  tier is not evidence.
- **`README.md`** — one line under the test-project list pointing at
  `docs/ci-coverage.md`.

No `docs/benchmarks.md` change: no hot path is touched and no benchmark is needed
(this change adds no per-frame code — `TestGate` and `VulkanEnvironment` are
test-only and setup-time).

## Step 10 — Verification

Local, on the developer host that has a hardware driver + the layer:

1. `dotnet build Ahjo.Vulkan.slnx -c Release` — clean under
   `TreatWarningsAsErrors`. No suppressions added anywhere.
2. Unset both variables → `dotnet test` → contract test passes and prints
   `declared=none observed=validation`; no test's pass/skip outcome differs from
   `main` except that the 233 skip reasons now carry `[gate:*]` prefixes.
3. `AHJO_VULKAN_TIER=validation dotnet test tests/Ahjo.Vulkan.Tests` → contract
   test passes; all 7 `SplitBarrierTests` execute, including
   `WaitEvent_MismatchedDependency_TripsValidation`. **Record the numbers in the
   PR body** — this run is the evidence #156 lacked.
4. `AHJO_VULKAN_TIER=nonsense dotnet test` → the exact "not a recognized tier"
   message.
5. `AHJO_REQUIRE_VULKAN_DEVICE=1 dotnet test` → the exact retirement message.
6. `AHJO_VULKAN_TIER=validation` with `VK_DRIVER_FILES` pointed at nothing
   resolvable → the contract test fails with `observed=none` and its full message;
   no other test *fails* (the 231 driver gates skip).
7. `dotnet publish samples/AotSmoke/AotSmoke.csproj -c Release -r win-x64 -p:IlcUseEnvironmentalTools=true`
   — no new trim/AOT warnings. Run with `AHJO_VULKAN_TIER=software` on a driverless
   shell (e.g. `VK_DRIVER_FILES` set to a nonexistent path) → exit 3.

In CI, on the PR:

8. The wrapper lane's check name shows the declared tier; the run summary page
   shows the coverage table; no log reading required.
9. The `Vulkan coverage summary` step reports `unclassified: 0`. If it does not,
   step 4 pass 5 is incomplete — finish it rather than relaxing the check.
10. `vma-linux` (both RIDs) still passes with `AHJO_VULKAN_TIER=software`, and its
    contract test reports `observed=software`. This is the #144 guard; if it is
    not green, do not merge.

## Out of scope — file as follow-ups, do not fold in

- **#152's root cause.** This change makes it visible and gives it a finish line;
  it does not fix it. If the OPEN in 8a resolves to `software`, this PR will be red
  until #152 lands — that is the point of that option, not a bug in this one.
- **Provisioning `glslc` on the Windows lane** and adding a declared SPIR-V
  requirement. Worthless before #152.
- **Narrowing `IsSoftwareDriver`** from "all software ICDs" to "lavapipe on Linux",
  which is what would let SwiftShader-on-Windows run the submitting tests and
  potentially make the validation-layer oracle CI-provable. Needs a working ICD
  first (#152) and its own evidence.
- **Self-hosted / GPU-enabled runners.** Named as the only route to a CI-reachable
  `validation` tier; not designed, costed, or recommended here.
