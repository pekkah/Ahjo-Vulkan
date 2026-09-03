# Vulkan test coverage: the declared tier

A green tick has to say what it covered. Most of this repo's test suite needs a
Vulkan device, and a test that skips for want of one is indistinguishable from a
test that passed unless you open the log. Before issue #158 that gap was 225 of
392 wrapper tests — 57% of the suite absent, reported green.

Every CI lane that runs a Vulkan-touching suite therefore **declares** what it
expects the host to provide, in `AHJO_VULKAN_TIER`. A single test enforces the
declaration, every skip carries a machine-readable class, and a job-summary step
turns the counts into the run's headline verdict.

## The tier ladder

| `AHJO_VULKAN_TIER` | The host must provide |
|---|---|
| `none` | nothing; every driver-gated test may skip |
| `software` | `vkCreateInstance` succeeds and `vkEnumeratePhysicalDevices` reports ≥ 1 device (a CPU `deviceType` is allowed) |
| `hardware` | as `software`, and the first enumerated device is **not** `VK_PHYSICAL_DEVICE_TYPE_CPU` |
| `validation` | as `hardware`, and `VK_LAYER_KHRONOS_validation` is enumerable |

The rungs are ordered and each implies the ones below it. Unset or empty means
`none`. An unrecognized value is an error, not a silent `none`.

The ladder lives in `tests/Shared/VulkanEnvironment.cs`, linked into
`Ahjo.Vulkan.Tests`, `Ahjo.Vulkan.Native.Tests` and
`Ahjo.Vulkan.Vma.Native.Tests`. `Ahjo.Vulkan.Ktx.Native.Tests` is deliberately
outside the system: its contract is to pass with no loader and no ICD at all.

## What enforces it

**1. One contract test.** `VulkanTierContractTests.DeclaredTier_IsSatisfiedByHost`
compares the declared tier against a live probe of the host. Below the
declaration it fails with one message naming the tier, the observed capability
and the reason the probe stopped. Gates themselves always skip and never fail —
231 red tests would bury the one actionable line.

On a pass it prints the observation, which is the line to quote as evidence:

```
AHJO_VULKAN_TIER declared=validation observed=validation (hardware device + VK_LAYER_KHRONOS_validation)
```

**2. Classified skips.** Every skip goes through `Ahjo.Vulkan.Testing.TestGate`,
which prefixes the reason with its class:

| Class | Method | Nature |
|---|---|---|
| `[gate:driver]` | `TestGate.RequireDriver()` | coverage gap — needs tier `software` |
| `[gate:hardware]` | `TestGate.RequireHardwareDriver(reason)` | coverage gap — needs tier `hardware` |
| `[gate:validation]` | `TestGate.RequireValidationLayer()` | coverage gap — needs tier `validation` |
| `[gate:spirv]` | `TestGate.RequireSpirv(path)` | toolchain gap — `glslc` absent at build time |
| `[gate:platform]` | `TestGate.RequirePlatform(cond, reason)` | correct and permanent on this OS / window system |
| `[gate:feature]` | `TestGate.RequireDeviceFeature(cond, reason)`, `TestGate.Unsupported(reason)` | correct — the device does not advertise it |

The distinction is the point: a Wayland surface test skipping on Windows is
correct forever, and a mechanism that reports it identically to a driver-gated
hole is worse than no mechanism.

**3. A job-summary step that fails on unclassified skips.** `Vulkan coverage
summary` in `.github/workflows/ci.yml` reads `TestResults/wrapper.trx`, buckets
the skips by class, writes the table to the run summary, and **fails the job**
when any skip carries no `[gate:*]` class. That is what stops the classification
rotting: the next ad-hoc `Assert.Skip("…")` cannot slip back into invisibility.
It also fails when a gap class has a non-zero count at or below the declared
tier, which means a tier-aware gate is miswired.

**4. The tier in the check name.** `Build & Test (windows-latest, Vulkan none)`.
The checks list, not the logs, says what green covered. Changing the tier changes
the check name — deliberate friction.

## Where each lane stands

| Lane | Declared | Why |
|---|---|---|
| `build-test` (windows-latest) | `none` | The lane provisions the Khronos loader and a SwiftShader ICD and wires `VK_DRIVER_FILES` at it, but the ICD does not answer `vkCreateInstance` — issue #152. `none` is the honest description of the runner as it is. #152's fix raises this to `software`, and the contract test is what will prove it. |
| `vma-linux` (both RIDs) | `software` | Mesa lavapipe is a CPU ICD, and the lane only does allocation work. This is the #144 guard: a `libvma.so` that SIGSEGVed on the first `vmaCreateAllocator` shipped to NuGet because nothing ever executed it. |
| `ktx-native` | — | Outside the system by contract: libktx is built with both uploaders off, so needing a loader would itself be the bug. |
| `slang-native` | — | Outside the system by contract: Slang compiles shader text to SPIR-V bytes and the package does not even reference `Ahjo.Vulkan.Native`. Its gate is the pinned SHA-256 plus a smoke suite that loads the binary and compiles a shader — a checksum proves the bytes, only running them proves they work. |
| `ngx-native` | — | Outside the tier system by contract: the `ahjo_ngx` shim links no `vulkan-1` (it only *includes* the headers), so needing a loader would itself be the bug. It proves the shim loads, resolves all 27 exports and agrees with the bindings on every NGX struct layout; it **cannot** evaluate DLSS, which needs an NVIDIA driver that no hosted runner has. It declares one thing instead of a tier — `AHJO_NGX_REQUIRE_SHIM=1`; see below. |

**Lowering a declared tier to make a lane green is prohibited.** The tier
describes the host; if the host regressed, fix the host.

### The `ngx-native` lane's one declaration

`Ahjo.Vulkan.Ngx.Native.Tests` has no Vulkan tier to declare — the shim links
no loader — so it declares one narrower fact in the same spirit, and by the
same rule: **the lane states what its host provides; the suite never sniffs
for it.**

| Variable | Declares | Set by | If absent |
|---|---|---|---|
| `AHJO_NGX_REQUIRE_SHIM=1` | the `ahjo_ngx` shim must be loadable | `ngx-native` only | an unloadable shim **skips** the suite instead of failing it |

Without it, a failed SDK fetch or a broken cmake step would leave every test
skipped and the lane green — the silent-coverage failure #158 closed. Don't
drop it to make a red run green.

### What a driverless runner can actually answer

`GetFeatureInstanceExtensionRequirements` is **driver-independent**, and that
is measured rather than assumed. It takes no Vulkan object and is answered out
of NVIDIA's static client library; it never loads the driver-side NGX core.
Both host kinds agree exactly:

| Host | Result |
|---|---|
| `windows-latest` CI runner, no NVIDIA driver | `Success`, `extensionCount` 1, `VK_KHR_get_physical_device_properties2` specVersion 2 |
| RTX 4070 Ti, driver 610.47 | `Success`, `extensionCount` 1, `VK_KHR_get_physical_device_properties2` specVersion 2 |

Issue #216's spec originally guessed the opposite — that no driver implied no
`Success` — and briefly carried an `AHJO_NGX_EXPECT_NO_DRIVER` declaration to
assert it. CI disproved the premise, and both the variable and the assertion
were removed. The suite now asserts only what holds on every host: the call
returns rather than faulting or hanging, and a `Success` carries a plausible
count and a non-null array. **Don't reintroduce a driver-conditional
expectation for this call.**

`CreateFeature` / `EvaluateFeature` are a different matter: they do need the
driver and the consumer-supplied feature DLL, so real DLSS coverage stays a
local-NVIDIA-hardware item. No hosted runner can provide it, and the feature
DLL never enters CI at all.

## The ceiling, stated plainly

No GitHub-hosted runner exposes a hardware Vulkan device. Even a fully fixed
#152 yields at best a CPU ICD, which the standing #32/#144 policy excludes from
the submitting tests because software rasterizers are not honest coverage.

So the `validation` tier is **not reachable in CI**, and roughly 15 tests whose
only oracle is `VK_LAYER_KHRONOS_validation` — including
`SplitBarrierTests.WaitEvent_MismatchedDependency_TripsValidation`, the negative
control that proves `VUID-vkCmdWaitEvents2-pEvents-10788` is a live oracle — will
remain declared-absent there. This design does not fix that. It makes it a
counted, named, non-degradable fact instead of a green tick.

A self-hosted or GPU-enabled runner is the only route to a CI-reachable
`validation` tier. None is proposed here; if one arrives, provisioning it and
raising the lane's declared tier is the whole change.

## Running locally

Nothing declared — every gate is free to skip, the contract test always passes:

```bash
dotnet test
```

Declaring what your host actually has turns the run into checkable evidence. If
your feature's only oracle is the validation layer, run this and quote the
contract test's output line in the PR:

```bash
# bash
AHJO_VULKAN_TIER=validation dotnet test tests/Ahjo.Vulkan.Tests
```

```powershell
# PowerShell
$env:AHJO_VULKAN_TIER = "validation"; dotnet test tests/Ahjo.Vulkan.Tests
```

"7 tests passed locally" with no tier is not evidence — it is indistinguishable
from 7 tests skipping. The declared tier is what makes it a machine-checked
claim.

To reproduce a driverless lane on a host that has a driver, point the loader at
a manifest that does not exist:

```powershell
$env:VK_DRIVER_FILES = "C:\nope\no_such_icd.json"
```

## `AHJO_REQUIRE_VULKAN_DEVICE` is retired

The old variable turned one suite's driverless skip into a hard failure. It is
now **an error if set at all**: any suite in the tier system throws

```
AHJO_REQUIRE_VULKAN_DEVICE is no longer read (issue #158).
Set AHJO_VULKAN_TIER=software instead, then unset AHJO_REQUIRE_VULKAN_DEVICE.
```

Fail-closed on purpose — a stale lane or shell script must not silently lose its
guard.

## Follow-ups, not folded in here

- **#152** — the Windows lane's SwiftShader ICD does not answer. This design is
  independent of the root cause and gives it a finish line.
- **Provision `glslc` on the Windows lane** and add a declared SPIR-V
  requirement. Worthless before #152: no shader test reaches its `[gate:spirv]`
  check while the driver gate fires first.
- **Whether SwiftShader-on-Windows may run the submitting tests**, i.e. whether
  the hardware gate should narrow from "all software ICDs" to "lavapipe on
  Linux". Needs a working ICD first, and its own evidence.
