# CI — lanes and the decisions behind them

## Wrapper test suite: Windows only

The **wrapper test suite** runs on `windows-latest` only. Linux is parked for it — issue #32 established that SwiftShader on Linux SIGSEGVs mid-suite across every loader+build combination tested, and gating driver-dependent tests behind a software rasterizer isn't honest coverage. When a self-hosted Linux runner with real Vulkan drivers becomes available, the Linux job can come back. **Don't add general Linux wrapper lanes** — that reopens a closed decision.

Windows CI provisions the Khronos Vulkan loader + Silk.NET-packaged SwiftShader ICD and routes the loader at it via `VK_DRIVER_FILES`.

## `vma-linux` lane — build-artifact check, not wrapper coverage

Both Linux RIDs, Mesa lavapipe, runs `Ahjo.Vulkan.Vma.Native.Tests` and nothing else. It does not reopen the issue-32 decision: `Ahjo.Vulkan.Vma.Native` publishes Linux binaries, so something has to execute one before it reaches NuGet. Issue #144 shipped a `libvma.so` that SIGSEGVed on the first `vmaCreateAllocator` precisely because nothing ever did. Allocation-only work is both what lavapipe handles reliably and what actually broke, which is why the lane stops there — don't grow it into a general Linux test lane.

It declares `AHJO_VULKAN_TIER=software`, so a broken ICD install can't report green while executing nothing — see the tier rules below.

## Declared Vulkan tier — every lane says what it covers

Every lane that runs a Vulkan-touching suite declares `AHJO_VULKAN_TIER` (`none` < `software` < `hardware` < `validation`), and `VulkanTierContractTests` fails the run when the host falls below the declaration. **Lowering a declared tier to make a lane green is prohibited** — the tier describes the host; if the host regressed, fix the host. Where each lane stands and why: `docs/ci-coverage.md`.

Hosted runners cap at `software`: no GitHub-hosted runner exposes a hardware Vulkan device, so the `validation` tier — and the ~15 tests whose only oracle is `VK_LAYER_KHRONOS_validation` — is not reachable in CI at all. The Windows lane currently declares `none` because its provisioned SwiftShader ICD does not answer `vkCreateInstance` (#152); that fix is what raises it to `software`.

Every skip carries a `[gate:driver|hardware|validation|spirv|platform|feature]` class from `Ahjo.Vulkan.Testing.TestGate`, and the `Vulkan coverage summary` step **fails the job on any unclassified skip**. Don't relax that check to get green — classify the gate.

`AHJO_REQUIRE_VULKAN_DEVICE` is retired (#158) and is now a hard error if set anywhere, so a stale lane can't silently lose its guard.

## `ktx-native` lane — one definition, proven before shipping

win-x64 + linux-x64, defined ONCE in `build-ktx-native.yml`, called by both `ci.yml` and `publish.yml` — so the binary a release attaches comes from the definition CI proves. Each job builds libktx for its RID **and runs `Ahjo.Vulkan.Ktx.Native.Tests` against it before uploading the artifact** (#144's lesson applied up front).

It provisions no ICD and no loader on purpose: libktx ships with both uploaders off, so needing one would mean something got linked in that the package's contract says isn't there. The staged binary under `native/ktx/staged/<rid>/` is both the cache key and the artifact; a cache hit skips the clone and cmake but still runs the tests.

## `slang-native` lane — a checksum proves the bytes, not that they run

win-x64 + linux-x64, defined ONCE in `build-slang-native.yml`, called by both `ci.yml` and `publish.yml` — so the binary a release attaches comes from the definition CI proves. Each job stages the pinned Slang release archive for its RID **and runs `Ahjo.Vulkan.Slang.Native.Tests` against it before uploading the artifact**, same as `ktx-native`.

The difference from the other native lanes is that nothing is compiled here: the binary is upstream's own release artifact, pinned by tag *and* by SHA-256 in `Directory.Build.props`. The checksum tells you the bytes are the ones we pinned; only the smoke suite tells you they load and compile a shader on this runner. That is why the tests are not optional and why they run on a cache hit too.

It provisions no ICD and no loader on purpose, and `AHJO_VULKAN_TIER` stays unset: Slang compiles shader text to SPIR-V bytes and the package does not even reference `Ahjo.Vulkan.Native`. If a test in this suite ever needs a Vulkan device, something got linked in that the package's contract says isn't there. The staged binaries under `native/slang/staged/<rid>/` are both the cache key and the artifact.

It is a build-artifact check, not wrapper coverage. **Don't grow it** — wrapper-level Slang tests belong in the Windows `build-test` job with the rest of the wrapper suite.

`*-arm64` and `osx-*` stay unshipped even though upstream publishes assets for them at every tag. Add the lane first, then the RID.

## `ngx-native` lane — the one binary we compile ourselves

win-x64 + linux-x64, defined ONCE in `build-ngx-native.yml`, called by both `ci.yml` and `publish.yml` — so the binary a release attaches comes from the definition CI proves. Each job stages the pinned NGX SDK for its RID, compiles the `ahjo_ngx` shim, **and runs `Ahjo.Vulkan.Ngx.Native.Tests` against it before uploading the artifact**, same as `ktx-native` and `slang-native`.

What is different here is that NVIDIA ships NGX as a **static client library with no DLL**, so unlike every other native package the shipped binary is one *we* compile: `native/ngx/src/ahjo_ngx.cpp` links that archive into a shared library exporting exactly 27 symbols.

**The SDK is fetched per-run with `-SkipFeatureDll`, so no feature DLL can enter CI.** That switch drops the `rel/` and `dev/` entries from the fetch manifest entirely — it is structurally impossible for a CI run to pull `nvngx_dlss.dll` / `libnvidia-ngx-dlss.so`, rather than merely not being asked to. Consumers supply the feature DLL (#214); nothing in this repo commits, packs or downloads one.

**What the lane proves:** the shim compiles on MSVC and GCC, loads, resolves all 27 exports against the two hand-maintained export lists, and agrees with the generated C# about every NGX struct layout down to individual field offsets — which is why both RIDs run and why `fail-fast` is off. **What it cannot prove:** that DLSS runs. Evaluating a feature needs an NVIDIA driver, and no GitHub-hosted runner has one. Real `GetFeatureRequirements` / create / evaluate coverage is a local-NVIDIA-hardware item, named as such in `docs/ci-coverage.md` rather than papered over.

### What the lane declares

One variable, set on the test step in `build-ngx-native.yml`. Like `AHJO_VULKAN_TIER`, it is something the **lane states about its host**, never something the suite sniffs for — a heuristic is a thing that can be wrong in a way nobody notices.

| Variable | Declares | Set by | If absent |
|---|---|---|---|
| `AHJO_NGX_REQUIRE_SHIM=1` | the `ahjo_ngx` shim must be loadable | `ngx-native` only | an unloadable shim **skips** the suite instead of failing it |

It is what stops the lane reporting green while executing nothing. The suite skips wholesale when the shim is absent — correct for a contributor who has never opted into DLSS — so without that variable a failed fetch or a broken cmake step would produce an all-skipped, green run. Same idea as `AHJO_VULKAN_TIER` (#158), applied to a suite with no Vulkan tier to declare. **Don't drop it to make a red run green**, and don't set it on a host where the shim genuinely isn't built.

### `GetFeatureInstanceExtensionRequirements` is driver-independent — measured

Worth recording as a fact, because the opposite was assumed first. NGX's pre-instance discovery call takes no Vulkan object and is answered out of NVIDIA's **static client library**; it never loads the driver-side NGX core. Measured on both host kinds, and they agree exactly:

| Host | Result |
|---|---|
| `windows-latest` CI runner, no NVIDIA driver | `Success`, `extensionCount` 1, `VK_KHR_get_physical_device_properties2` specVersion 2 |
| RTX 4070 Ti, driver 610.47 | `Success`, `extensionCount` 1, `VK_KHR_get_physical_device_properties2` specVersion 2 |

Issue #216's spec originally guessed that a driverless host could not get `Success` here, and briefly carried an `AHJO_NGX_EXPECT_NO_DRIVER` declaration to assert it. CI disproved the premise; the variable and its assertion were removed. The suite now asserts only what holds everywhere: the call returns rather than faulting or hanging, and a `Success` carries a plausible count and a non-null array. **Don't reintroduce a driver-conditional expectation for this call** — the two-host agreement above is why.

This says nothing about `CreateFeature` / `EvaluateFeature`, which do need the driver and the consumer-supplied feature DLL. Those remain out of reach for any hosted runner.

It provisions no ICD and no loader and leaves `AHJO_VULKAN_TIER` unset, on purpose: the shim links no `vulkan-1` — it only *includes* the headers, the same reason `VMA_STATIC_VULKAN_FUNCTIONS=0` exists. The built DLL's only imports are `KERNEL32`, `USER32` and `ADVAPI32`, verified. If a test in this suite ever needs a Vulkan device, something got linked in that the package's contract says isn't there.

The cache holds the SDK's static **client library** — someone else's input to our build — not our output. The shim itself is rebuilt every run: it is one translation unit, it costs seconds, and it is the thing the tests execute. Caching our own artifact would be caching the answer.

It is a build-artifact check, not wrapper coverage. **Don't grow it.**

Only `win-x64` and `linux-x64` exist, and that is not the usual "add the lane first, then the RID" note: NVIDIA publishes NGX client libraries for `Windows_x86_64` and `Linux_x86_64` and nothing else, so this matrix is already the complete set.

## Publishing

`publish.yml` ships preview packages on `push:main` (MinVer-derived pre-release version) and stable packages on `release:published` events. Tag with `v0.x.y` → create a GitHub release → all seven packages publish under that single tag. The publish workflow can override MinVer via `MinVerVersionOverride`.
