# CI — lanes and the decisions behind them

## Wrapper test suite: Windows only

The **wrapper test suite** runs on `windows-latest` only. Linux is parked for it — issue #32 established that SwiftShader on Linux SIGSEGVs mid-suite across every loader+build combination tested, and gating driver-dependent tests behind a software rasterizer isn't honest coverage. When a self-hosted Linux runner with real Vulkan drivers becomes available, the Linux job can come back. **Don't add general Linux wrapper lanes** — that reopens a closed decision.

Windows CI provisions the Khronos Vulkan loader + Silk.NET-packaged SwiftShader ICD and routes the loader at it via `VK_DRIVER_FILES`.

## `vma-linux` lane — build-artifact check, not wrapper coverage

Both Linux RIDs, Mesa lavapipe, runs `Ahjo.Vulkan.Vma.Native.Tests` and nothing else. It does not reopen the issue-32 decision: `Ahjo.Vulkan.Vma.Native` publishes Linux binaries, so something has to execute one before it reaches NuGet. Issue #144 shipped a `libvma.so` that SIGSEGVed on the first `vmaCreateAllocator` precisely because nothing ever did. Allocation-only work is both what lavapipe handles reliably and what actually broke, which is why the lane stops there — don't grow it into a general Linux test lane.

It sets `AHJO_REQUIRE_VULKAN_DEVICE=1`, which turns the suite's driverless skip into a hard failure so a broken ICD install can't report green while executing nothing.

## `ktx-native` lane — one definition, proven before shipping

win-x64 + linux-x64, defined ONCE in `build-ktx-native.yml`, called by both `ci.yml` and `publish.yml` — so the binary a release attaches comes from the definition CI proves. Each job builds libktx for its RID **and runs `Ahjo.Vulkan.Ktx.Native.Tests` against it before uploading the artifact** (#144's lesson applied up front).

It provisions no ICD and no loader on purpose: libktx ships with both uploaders off, so needing one would mean something got linked in that the package's contract says isn't there. The staged binary under `native/ktx/staged/<rid>/` is both the cache key and the artifact; a cache hit skips the clone and cmake but still runs the tests.

## Publishing

`publish.yml` ships preview packages on `push:main` (MinVer-derived pre-release version) and stable packages on `release:published` events. Tag with `v0.x.y` → create a GitHub release → all four packages publish under that single tag. The publish workflow can override MinVer via `MinVerVersionOverride`.
