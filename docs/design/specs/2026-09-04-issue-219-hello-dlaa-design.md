# `samples/HelloDlaa` + the consumer contract: what a first real DLSS call site has to get right

**Issue:** [#219](https://github.com/pekkah/Ahjo-Vulkan/issues/219) — *NGX Phase 3: samples/HelloDlaa + consumer docs*
**Phase 3 of:** [#214](https://github.com/pekkah/Ahjo-Vulkan/issues/214) (tracking; the ship-model and package decisions are fixed there and **not reopened here**)
**Builds on:** [#216](https://github.com/pekkah/Ahjo-Vulkan/issues/216) (the shim) and [#218](https://github.com/pekkah/Ahjo-Vulkan/issues/218) (the managed wrapper), both landed on `issue-216-ngx-native` / [PR #217](https://github.com/pekkah/Ahjo-Vulkan/pull/217). This work lands on the **same branch and the same PR**.
**Lands consistently with:** [#207](https://github.com/pekkah/Ahjo-Vulkan/issues/207) (`HelloRayQuery` — the Slang-at-run-time sample shape), [#119](https://github.com/pekkah/Ahjo-Vulkan/issues/119) (valid-by-default descriptions), [#118](https://github.com/pekkah/Ahjo-Vulkan/issues/118) (handle ownership — borrowed handles are not tracked and do not destroy), [#17](https://github.com/pekkah/Ahjo-Vulkan/issues/17) (layout belongs to the recorder, not the handle), [#32](https://github.com/pekkah/Ahjo-Vulkan/issues/32) (CI has no NVIDIA hardware and does not pretend to), [#29](https://github.com/pekkah/Ahjo-Vulkan/issues/29)/[#114](https://github.com/pekkah/Ahjo-Vulkan/issues/114) (zero per-frame allocation)
**Date:** 2026-09-04 (revised the same day at the approval gate: engine-derived justification removed, the migration note declined, OPEN-1 resolved, OPEN-2 reversed into D11)

## Problem

Phases 1 and 2 shipped a working DLSS wrapper and a hardware test that proves
`create → evaluate → release` runs clean under the validation layer
(`tests/Ahjo.Vulkan.Ngx.Tests/DlssHardwareTests.cs:82-175`). What neither shipped
is a *renderer*. The hardware test evaluates DLSS over four images that were never
drawn into: no jitter, no motion, no mip chain, no swapchain, and `Reset = true` on
the only frame there is (`DlssHardwareTests.cs:145`). Every input is structurally
valid and semantically empty.

That leaves the entire consumer-facing half of DLSS unexercised and unwritten
down, and it is the half where integrations fail:

1. **The renderer's obligations are a coupled set, and each one fails silently.**
   Jitter must be applied to the projection *and* reported to DLSS in the same
   sign and space. Motion vectors must point from the current pixel to where it
   was, in render-resolution pixels, with the screen-space axis convention DLSS
   assumes. The mip bias must move when the render resolution moves. Get any of
   these wrong and the picture looks *correct while static* and smears, shimmers
   or ghosts in motion. No validation layer, no `AhjoValidation` check and no NGX
   result code reports any of it.

2. **The wrapper deliberately cannot hold the layout contract (#218 D4), so the
   only place it can be *shown* is a sample.** `DlssEvaluateInputs`' remarks state
   it; nothing demonstrates it inside a real frame with a `BeginRendering` scope
   the evaluate must sit outside of.

3. **The consumer-supplied DLL model is decided (#214) and documented nowhere a
   consumer will look.** `src/Ahjo.Vulkan.Ngx/README.md` says "the feature DLL is
   yours"; there is no page that says *how* — the MSBuild item, the search-path
   API, why never the `dev/` build, what NVIDIA's licence puts on the application.
   `docs/` currently has no `ngx-notes.md` at all.

4. **A swapchain image reaches the wrapper carrying no extent, no format and no
   usage**, because the only route to a wrapper `Image` is `Image.FromRaw`
   (`src/Ahjo.Vulkan/Resources/Image.cs:84-85`). Every whole-image region helper
   reads exactly those fields, so `ImageBlitRegion.WholeImage` produces a
   degenerate destination box for a swapchain destination and the blit silently
   does nothing (E4). Nothing in the repo has hit this yet because nothing in the
   repo blits to a swapchain image — `HelloDlaa` is the first such call site, and
   it should not be the first of many that each hand-roll the region.

And there is one design question this issue was told to answer: **#218 OPEN-7**,
deferred with the note that `samples/HelloDlaa` "is where a swapchain-output path
would first actually be written". This spec answers it (D10, refined by D11).

## Evidence

Measured on this working tree at `15b0f71` (Phases 1 and 2 merged onto
`issue-216-ngx-native`), against `src/Ahjo.Vulkan/` and `src/Ahjo.Vulkan.Ngx/` as
shipped and the pinned guide at
`native/ngx/doc/DLSS_Programming_Guide_Release.pdf` (`NgxVersion` `v310.7.0`,
`Directory.Build.props:66`).

### E1. CI builds every sample and runs exactly one

`.github/workflows/ci.yml:153` is the only place samples are compiled:

```
dotnet build Ahjo.Vulkan.slnx -c Release --nologo -p:SkipKtxNativeBuild=true -p:SkipNgxNativeBuild=true
```

Every sample in `Ahjo.Vulkan.slnx:19-26` is therefore **built**. The only sample
CI ever *executes* is `AotSmoke`, published and run at `ci.yml:338-343`. A grep of
`ci.yml` for `samples` returns those two sites and nothing else.

Two consequences, both load-bearing:

- A new sample must be added to `Ahjo.Vulkan.slnx` (README.md:148 states the rule:
  "all in the solution so a wrapper change that breaks one breaks the build"), and
  it must **compile with no NGX SDK staged**. `SkipNgxNativeBuild=true` plus the
  `Exists('$(_NgxStaticLib)')` condition on the shim-copy item
  (`src/Ahjo.Vulkan.Ngx.Native/Ahjo.Vulkan.Ngx.Native.csproj:96-102`) means CI
  produces no `ahjo_ngx.dll` at all; the managed bindings still build.
- Nothing in CI will ever run it, so no gate, no trx and no `[gate:*]` class is
  needed for the sample — it is outside the coverage-summary machinery entirely
  (`ci.yml:203-232` reads `TestResults/wrapper.trx` only). The core-package change
  of D11 is a different matter and does carry tests (D13).

Any MSBuild item that references a feature DLL must therefore be `Condition`ed on
`Exists(...)`: an unconditional item for a file nobody has is a CI build failure.
And it must not emit a warning either — `TreatWarningsAsErrors=true`
(`Directory.Build.props:8`) is repo-wide, which is exactly why
`Ahjo.Vulkan.Ngx.Native.csproj`'s `WarnNgxSdkMissing` target uses
`<Message Importance="high">` and says so in its own comment.

### E2. There are two shader conventions in `samples/`, and the newer one is Slang at run time

- **glslc at build time** — `HelloVmaWindowed.csproj:22-38` and
  `HelloCube.csproj:26-42` both carry a `CompileShaders` target invoking
  `$(VULKAN_SDK)\Bin\glslc.exe` with `ContinueOnError="WarnAndContinue"`.
- **Slang at run time** — `HelloRayQuery.csproj:22-26` copies
  `Shaders\rayquery.slang` with `CopyToOutputDirectory="PreserveNewest"` and the
  csproj comment states the reason: "the repository is moving off glslc".
  `RayQueryPipeline.cs:44-58` is the recipe: `SlangCompiler.Create()` →
  `CreateSession(new SlangSessionDescription { … })` →
  `Compile(new SlangCompileRequest { Path = shaderPath })` →
  `device.CreateShaderModule(_program.Spirv(0))`.

`SlangProgram` supports several entry points from one file:
`SlangCompileRequest.EntryPoints` (`SlangCompileRequest.cs:53`),
`SlangProgram.EntryPoint(int)` (`SlangProgram.cs:157`) and `Spirv(int)`
(`SlangProgram.cs:187`), documented as "entry point `i`'s reflection and entry
point `i`'s SPIR-V describe the same function". `RayQueryPipeline.cs:104-108`
records the matching trap: Slang emits each entry point into SPIR-V as `main`
regardless of its Slang-side name, so the pipeline builder's default `pName`
is correct and naming the Slang function is what fails.

### E3. The windowed shape is fixed, and it has no spare input key

`HelloVmaWindowed/Program.cs` is the reference: `SdlWindow` link-compiled from
`tests/Ahjo.Vulkan.Tests/SdlWindow.cs` (`HelloVmaWindowed.csproj:16-18`,
"shared with the test project (#87 / #88)"), `Instance.Create` with
`EnableValidation = true` and a static `DebugCallback` tallying errors
(`Program.cs:88-95`, `:454-467`), `Swapchain` + `FrameRing` +
`fc.Submit(queue, ref rec, swap, imageIndex)` (`:236-420`), a `--frames N` bound
so the sample can run unattended (`:65-72`), and exit code `4` when the layer
reported anything (`:429`).

`SdlWindow.PumpEvents` handles exactly two keys: `SDLK_ESCAPE` → close and
`SDLK_W` → wireframe toggle (`SdlWindow.cs:137-139`, consumed at `:162`). There is
no general key-event surface. A run-time DLSS on/off toggle would therefore mean
editing a file owned by `tests/` and link-compiled into three samples — which is
why this design uses command-line modes instead (D3).

### E4. The swapchain hands out handles, not images — and the whole-image helpers read the fields a handle does not have

| What `Swapchain` exposes | Citation |
|---|---|
| `nint GetImageHandle(uint index)` | `Rendering/Swapchain.cs:90` |
| `ReadOnlySpan<ImageView> ImageViews` | `:81` |
| `VkFormat Format`, `VkExtent2D Extent`, `ImageUsage ImageUsage` | `:75`, `:77`, `:79` |
| `imageArrayLayers = 1` hardcoded at creation | `:521` |

There is no `Swapchain.GetImage`. The only way to a wrapper `Image` is
`Image.FromRaw(nint)` (`Resources/Image.cs:84-85`), which constructs
`Width = 0`, `Height = 0`, `Format = VK_FORMAT_UNDEFINED`, `Usage = ImageUsage.None`
— the valid-by-default shape from #119, whose own comment says "Width/Height stay
0 — unknown for a bare handle".

`ImageBlitRegion.WholeImage(in src, in dst)` reads `dst.Width/Height/Depth`
(`Recording/ImageBlitRegion.cs:52-54`). Handed a `FromRaw` swapchain image it
produces `DstOffset1 = (0,0,0)` — a degenerate destination box, i.e. a blit that
silently does nothing. `BufferImageCopy.WholeImage`
(`Recording/BufferImageCopy.cs:30`) has the same shape.

**Nothing in the repo hits this today.** A sweep of `samples/` and
`tests/Ahjo.Vulkan.Tests/` finds every `*.WholeImage` call over a VMA-created
image (`AotSmoke:212`, `HeadlessExport:148`, `HeadlessTriangle:125`,
`HelloCube:551`, `HelloRayQuery:322`, `HelloVma:531`, `CommandRecorderTests:465`,
`:607`, `CopyCommandTests:321`, `:332`, `:415`), and every swapchain consumer
using `GetImageHandle` only to fill `ImageBarrier.Image`, which is an `nint`
(`Recording/ImageBarrier.cs:33`) — five sites: `HelloCube:599`,
`HelloTriangle:227`, `HelloVmaWindowed:482`, `SwapchainTests:308`,
`WindowedValidationTests:159`. `HelloDlaa` is the repo's **first** blit-to-swapchain
call site. That is what makes D11 a fix rather than a migration.

### E5. A swapchain cannot portably be a DLSS output, independent of any wrapper change

`Swapchain` passes the requested usage straight to `vkCreateSwapchainKHR`:
`_imageUsage = desc.ImageUsage == 0 ? ImageUsage.ColorAttachment : desc.ImageUsage`
(`Swapchain.cs:499`) → `imageUsage = (uint)_imageUsage` (`:522`). There is **no**
clamp against `VkSurfaceCapabilitiesKHR.supportedUsageFlags`, which the Vulkan
spec only requires to contain `COLOR_ATTACHMENT`. Asking for
`ImageUsage.Storage` on a surface that does not advertise it is a creation
failure, not a graceful downgrade.

Second, independent blocker: the swapchain's own image views are created in the
swapchain's format, and the format the `SwapchainDescription` doc comment
recommends preferring is an `_SRGB` one
(`Rendering/SwapchainDescription.cs:40-44`). `VK_FORMAT_*_SRGB` does not support
`VK_IMAGE_USAGE_STORAGE_BIT` on any mainstream desktop driver.

So "DLSS writes straight into the swapchain image" requires a surface that
advertises `STORAGE` **and** a non-sRGB swapchain format, neither of which the
wrapper can supply. This is the decisive evidence for D10.

### E6. The wrapper already refuses a metadata-free `NgxImage`, loudly

`DlssFeature.ValidateInputs` calls `RequireMetadata` on all six slots
(`src/Ahjo.Vulkan.Ngx/DlssFeature.cs:282-287`), and `RequireMetadata`
(`:319-343`) fails a bound slot whose extent is zero or whose format is
`VK_FORMAT_UNDEFINED`, with a message that names `Image.FromRaw` and tells the
caller to "build the `NgxImage` from a VMA-created `Image` instead".

That is the *current* state of OPEN-7: not a silent wrong answer, a named
diagnosis — under `AhjoValidation.Enabled` (`Diagnostics/AhjoValidation.cs:69`,
default `true` in DEBUG, `false` in Release).

### E7. Borrowed handles are already excluded from tracking and from destruction — by ownership, not by a flag

This is the evidence D11 rests on, and it says the trap is already closed:

- `Image.OwnsHandle => !Owner.IsNull` (`Resources/Image.cs:97`), where `Owner` is
  the `Allocator` that created it. `Image.FromRaw` passes `owner: default`
  (`:84-85`), so a borrowed image reports `OwnsHandle == false`.
- `HandleRegistry.TrackCreate` — called as the last statement of `Image`'s
  internal constructor (`Resources/Image.cs:73`) — begins
  `if (!AhjoValidation.IsEnabled || !handle.OwnsHandle) return;`
  (`Diagnostics/HandleRegistry.cs:67-68`). Its type remarks say so in prose:
  "Borrowed handles (`FromRaw` / `default`, where `OwnsHandle` is `false`) are
  never tracked: the wrapper doesn't destroy them, so there is no double-dispose
  to catch" (`:42-45`).
- `Image.Dispose` returns at `if (!OwnsHandle) return;` (`Resources/Image.cs:155`)
  — *before* `HandleRegistry.TrackDispose` and *before* `vmaDestroyImage` — with a
  comment naming exactly this case: "a swapchain-owned image is not a VMA
  allocation and must never reach `vmaDestroyImage`; the guard makes the borrow
  contract real."
- `Image.OwnsMemory => AllocationHandle != null` (`:120`) is likewise `false`, so
  every allocation-addressing operation no-ops.

So a borrowed `Image` constructed with `allocation: null, owner: default` is
already non-tracked, already non-destroying and already idempotent under
`Dispose`. **No new ownership flag, no `Dispose` change and no `HandleRegistry`
change is required** to give a swapchain image its metadata — see D11.

### E8. The barrier recipe and usage set are already measured on hardware — the sample must copy them, not re-derive them

`DlssHardwareTests.cs:317-350` (usages) and `:372-400` (barriers):

| Slot | Usage created with | Layout before evaluate |
|---|---|---|
| Color | `Sampled \| ColorAttachment \| Storage` | `SHADER_READ_ONLY_OPTIMAL` |
| Depth | `Sampled \| DepthStencilAttachment` | `SHADER_READ_ONLY_OPTIMAL` (depth aspect) |
| Motion vectors | `Sampled \| ColorAttachment \| Storage` | `SHADER_READ_ONLY_OPTIMAL` |
| Output | `Storage \| TransferSrc \| TransferDst \| Sampled` | `GENERAL` |

and the output barrier's destination scope is
`Stage.ComputeShader | Stage.AllTransfer` / `Access.ShaderWrite | Access.TransferWrite`,
with the test's own comment stating why: DLSS's `vkCmdClearColorImage` is a
transfer-stage write, and `ComputeShader|ShaderWrite` alone leaves it outside the
barrier's destination scope (`DlssHardwareTests.cs:387-397`). This is the
`TRANSFER_DST` finding of #218 D3 in barrier form, and it is the single most
copy-worthy thing in the suite.

### E9. The wrapper API the sample must call, exactly as shipped

- Order of operations, stated on `NgxSupport` (`src/Ahjo.Vulkan.Ngx/NgxSupport.cs:44-46`):
  instance extensions → instance → physical device → device extensions → device →
  `NgxContext.Create`. `TryGetInstanceExtensions(in NgxDescription, out NgxExtensionSet?)`
  takes no Vulkan object (`:60`); `TryGetDeviceExtensions(PhysicalDevice, in NgxDescription, out …)`
  needs the instance the physical device came from and reaches it through the
  now-public `PhysicalDevice.Instance` (`:105-107`, #218 D12).
- `NgxExtensionSet.Names` is a `ReadOnlySpan<Utf8Name>` (`NgxExtensionSet.cs:105`)
  that drops into `InstanceDescription.Extensions` / `DeviceDescription.Extensions`,
  and must be disposed **after** the create call that consumes it
  (`NgxExtensionSet.cs:24-28`; the test does exactly this at
  `NgxTestEnvironment.cs:145-147`).
- `NgxDescription.ApplicationDataPath` must be left unset — the wrapper
  materializes `Path.GetTempPath()` itself and a null reaching NGX access-violates
  (`NgxDescription.cs:36-47`).
- `NgxContext.Create(Device, in NgxDescription)`; `GetOptimalSettings(uint, uint, DlssQualityMode)`
  returning six dimensions plus `IsAvailable` (`DlssOptimalSettings.cs`);
  `CreateDlss(ref CommandRecorder, in DlssFeatureDescription)` whose recorder
  **must be submitted and completed before the first `Evaluate`**
  (`NgxContext.cs:336`, demonstrated with `Queue.ImmediateSubmit` at
  `DlssHardwareTests.cs:97-108`); `Evaluate(ref CommandRecorder, in DlssEvaluateInputs)`
  (`DlssFeature.cs:104`).
- `NgxImage.CreateView(Device, in Image, in ImageViewDescription)` is the
  documented default factory and owns the view it makes (`NgxImage.cs:117-122`).
- `TryGetStats(out DlssStats)` reports DLSS's own VRAM plus `OptLevel` /
  `IsDevSnippetBranch`; a `rel/` DLL reports `OptLevel == 40` and
  `IsDevSnippetBranch == false`, which is the deployed-a-`dev`-build guard
  (`DlssHardwareTests.cs:168-174`, #218 OPEN-3).

Nothing the sample needs is missing from `Ahjo.Vulkan.Ngx`. The one thing missing
is in the core package, and it is not NGX-specific: E4.

### E10. Facts from the pinned programming guide that the sample and the docs must state exactly

All from `native/ngx/doc/DLSS_Programming_Guide_Release.pdf` (31 March 2026
revision, staged by `./tools/setup-ngx.ps1 -IncludeDocs`):

- **§3.6** — "The motion vectors map a pixel from the current frame to its
  position in the previous frame. That is, when the motion vector for the pixel is
  added to the pixel's current location, the result is the location the pixel
  occupied in the previous frame." So `mv = previous − current`.
- **§3.6.1** — `RG32_FLOAT` or `RG16_FLOAT`; values are "the number of pixels
  calculated in screen space (ie the amount a pixel has moved at the render
  resolution)"; "Screen space pixel values use [0,0] as the upper left of the
  screen … the pixel at the bottom right is [1919,1079]" — i.e. **+X right,
  +Y down, render-resolution pixels**.
- **§3.6.2** — `MVLowRes` = 1 means motion vectors are at render resolution (the
  preferred case; DLSS dilates internally). `MVJittered` = 1 means the motion
  vectors already contain sub-pixel jitter and DLSS should subtract it.
- **§3.6.3** — `InMVScaleX/Y` exist precisely for engines whose motion vectors
  "are pointing in the direction of motion rather than towards the previous frame"
  or are "in UV space rather than pixel space"; must never be `0.0f`.
- **§3.7.3** — jitter offsets are "in pixel-space at the render target size",
  always within `[-0.5, +0.5]`, represent "the jitter applied to the projection
  matrix", and "use the same co-ordinate and direction system as motion vectors".
- **§3.7.1.1** — phase count `= 8 * (target / render)^2`; the table gives
  DLAA 8, Quality 18, Balanced 24, Performance 32, Ultra Performance 72.
- **§3.5** — `DlssMipLevelBias = NativeBias + log2(RenderX / DisplayX) - 1.0 + epsilon`;
  §3.5.1 warns that high-frequency textures may need the bias left at default.
- **§3.1.2** — LDR mode requires colour in `[0,1]` in a **perceptually linear
  encoding (like sRGB)** and states that linear colour in LDR mode "exhibits
  visible color banding, color shifting or other visual artifacts"; anything
  linear must set `IsHDR`.
- **§3.8** — "The algorithm assumes that the near plane is 0.0 and the far plane
  is 1.0 (but this can be inverted)" — i.e. `DepthInverted` describes reversed-Z.
- **§8.6** — RenderDoc does not work with DLSS applications.

### E11. The VMA half of #214 shipped in Phase 2, and nothing in the repo exercises it end to end

`AllocatorDescription.EnableMemoryBudget` (`Memory/AllocatorDescription.cs:55`),
`DeviceDescription.Allocator` (`Lifecycle/DeviceDescription.cs:22`),
`VulkanExtensions.ExtMemoryBudget` (`Rendering/VulkanExtensions.cs:59`) and
`Allocator.GetHeapBudgets(Span<MemoryHeapBudget>)` (`Memory/Allocator.cs:397`) all
exist. `MemoryHeapBudget` carries `Usage` and `Budget` alongside VMA's own block
and allocation counts (`Memory/MemoryHeapBudget.cs:24-45`).

The only coverage is a driver-gated unit test (#218 D13). Nothing anywhere shows
the thing the feature was added for: **DLSS's driver-side VRAM appearing in
`Usage` while VMA's own `AllocationBytes` does not move.** A sample that prints
budgets before and after `CreateDlss` is the demonstration, and it costs about
fifteen lines.

### E12. The existing windowed samples allocate in the frame loop

`samples/HelloVmaWindowed/Program.cs:366` — `Buffer[] vertexBuffers = [vertexBuffer];`
inside the `while (!window.ShouldClose)` body. Same at
`samples/HelloCube/Program.cs:447`. A `T[]` collection expression is a heap
allocation; the `ReadOnlySpan<T>` ones a few lines above
(`Program.cs:377-383`) are not.

`CommandRecorder.BindVertexBuffers` takes `scoped ReadOnlySpan<Buffer>`
(`Recording/CommandRecorder.cs:367-370`), so the array is not required by the API.
This sample must not copy that line.

### E13. Enabling `AhjoValidation` does not cost the frame loop anything

`AhjoValidation.Enabled` is `false` in Release by default
(`Diagnostics/AhjoValidation.cs:56-61`) and its doc comment states the cost model:
"failing checks may allocate their message strings; passing checks on the hot path
stay allocation-free because the message is only built on the failure branch"
(`:44-47`). `DlssFeature.ValidateInputs` follows that shape — every
`NgxValidation.Fail` call site builds its interpolated string inside the failure
branch (`DlssFeature.cs:288-343`).

So the sample can turn wrapper validation **on** in Release and still hold a
zero-allocation loop. The one exception is documented in
`src/Ahjo.Vulkan.Ngx/CLAUDE.md`: NGX logging above `NgxLoggingLevel.Off` allocates
per callback, by design — so the sample leaves logging off.

## Decision

Thirteen decisions. D2 (the motion-vector and jitter convention), D5 (the frame
graph and the per-slot ring), D10 (OPEN-7) and D11 (`Swapchain.GetImage`, the one
core-package change) are the load-bearing ones.

**A note on what these decisions may rest on.** Every decision below is justified
from the Vulkan specification, the pinned DLSS programming guide,
`System.Numerics` semantics, or this repository's own shipped code. No decision
appeals to what any downstream consumer currently does: a consumer is not an
authority, and in this problem domain an existing implementation is at least as
likely to be sitting on one of the sign traps D2 exists to close.

### D1. One sample project, `samples/HelloDlaa`, split across six files, in the solution, never run by CI

`samples/HelloDlaa/HelloDlaa.csproj` follows `HelloRayQuery.csproj` (Slang, no
glslc target) crossed with `HelloVmaWindowed.csproj` (SDL3, `SdlWindow.cs`
link-compiled). It project-references `Ahjo.Vulkan`, `Ahjo.Vulkan.Slang`,
`Ahjo.Vulkan.Ngx` and `Ahjo.Vulkan.Utilities`, and is added to
`Ahjo.Vulkan.slnx` after `HelloRayQuery`.

The code is split rather than kept as one `Program.cs`, because the shape of a
DLSS frame is the thing being demonstrated and it drowns in a 1 100-line file:

| File | Responsibility |
|---|---|
| `Program.cs` | CLI, instance/device/swapchain creation with the NGX extension lists, the frame loop, layer tally, exit codes |
| `DlaaOptions.cs` | parsed command line; the mode enum; derived render extent, mip bias, jitter phase count |
| `JitterSequence.cs` | Halton(2,3), phase count per §3.7.1.1, and the one function that produces the jittered projection |
| `CubePipeline.cs` | Slang compile of `cube.slang` (two entry points) + the two-colour-attachment graphics pipeline |
| `CubeScene.cs` | cube vertex/index buffers, the procedural mip-mapped texture, the mip-biased sampler, the per-slot uniform ring |
| `FrameTargets.cs` | the per-slot colour / depth / motion-vector / presentation images, their views, their `NgxImage`s, and the barrier recipe |
| `Shaders/cube.slang` | `vertexMain` + `fragmentMain`; writes colour to attachment 0 and motion vectors to attachment 1 |

`HelloRayQuery` (three files) is the precedent.

#### Why not the alternatives

- **Extend `HelloVmaWindowed` with a `--dlss` switch.** Puts a proprietary,
  NVIDIA-only, driver-gated path inside the sample that teaches VMA frame rings,
  and makes that sample unrunnable-as-documented on half the machines that build
  it. Rejected.
- **A headless sample writing a PNG, like `HelloRayQuery`.** DLSS is temporal:
  one frame proves nothing, and the artefacts this sample exists to make visible
  (ghosting, smearing, shimmer) only exist in motion. Rejected — but the
  `--capture` switch (D9) borrows the PNG half, because RenderDoc does not work
  with DLSS (guide §8.6) and a self-taken frame dump is the only capture available.
- **One `Program.cs`.** See above.

### D2. The motion-vector and jitter conventions are fixed, in one space, and stated as a closed derivation

This is the decision the issue asked to be precise enough that the implementer
cannot guess. Everything below is in **render-target pixel space**: origin at the
top-left texel corner, +X right, +Y **down** — the space §3.6.1 defines.

**1. Projection and NDC.** The sample builds
`Matrix4x4.CreatePerspectiveFieldOfView(...)` and applies `proj.M22 *= -1f`, the
existing convention at `samples/HelloCube/Program.cs:578-583`. That yields a
y-**down** NDC and a `[0,1]` depth range (near → 0, far → 1). The viewport is
positive-height (`VkViewport { y = 0, height = +renderHeight }`); a negative-height
viewport is **not** used, because it would flip Y a second time and silently
invert every derivation below.

With that, the framebuffer UV of a clip position is

```
uv = ndc.xy * 0.5 + 0.5,      ndc = clip.xy / clip.w
```

with `uv.y = 0` at the top of the image. Pixel position is `uv * renderExtent`.

**2. Applying the jitter.** For a jitter `j = (jx, jy)` in render pixels, each in
`[-0.5, +0.5]`, the jittered matrix is

```csharp
Matrix4x4 t = Matrix4x4.Identity;
t.M41 = 2f * jx / renderWidth;
t.M42 = 2f * jy / renderHeight;
Matrix4x4 jitteredViewProjection = viewProjection * t;
```

`System.Numerics` uses row-vector composition (`v * (A*B) == (v*A)*B`, which is why
`HelloCube` writes `model * view * proj`), so `clip' = clip * t` gives
`clip'.x = clip.x + clip.w * t.M41` and therefore `ndc'.x = ndc.x + t.M41` exactly.
**The image moves by `+j` pixels, by construction, with no dependence on the sign
of `clip.w`.**

That last clause is the whole point of choosing this form over the guide's
`ProjectionMatrix.M[2][0] += ProjectionJitter.X` (§3.7.2). The guide's form
presumes a projection whose `w` is `+z`. `Matrix4x4.CreatePerspectiveFieldOfView`
is right-handed with `M34 = -1`, so `clip.w = -z` and
`ndc.x = (x·M11 + z·M31) / (-z) = -x·M11/z - M31`: adding `δ` to `M31` shifts NDC
by **minus** `δ`. Both the magnitude and the sign of the guide's recipe are
therefore convention-dependent, and post-multiplying by a clip-space translation
is immune to both.

**3. What is passed to DLSS.** `JitterOffsetX = jx`, `JitterOffsetY = jy` — the
same numbers, unchanged. §3.7.3 defines the value as "the jitter applied to the
projection matrix", in render pixels, in the motion-vector coordinate system, and
this construction makes "applied to the projection" literally equal to "the image
moved by `j` pixels in the DLSS pixel space". See OPEN-3: this step is derived,
not yet measured.

**4. Motion vectors.** `cube.slang`'s vertex stage outputs three clip positions:
the jittered current one to `SV_Position`, and the **unjittered** current and
previous ones as interpolants. The fragment stage writes

```
float2 curUV  = curClip.xy  / curClip.w  * 0.5 + 0.5;
float2 prevUV = prevClip.xy / prevClip.w * 0.5 + 0.5;
outMotion = (prevUV - curUV) * float2(renderWidth, renderHeight);   // pixels
```

`previous − current` is §3.6's definition verbatim; multiplying by the render
extent puts it in §3.6.1's units; the y-down NDC of step 1 gives §3.6.1's axis
convention with no negation anywhere. **No negation appears in this sample. If the
implementer finds themselves adding one, the derivation above is what to re-read.**

**5. The flags and scales that follow.**

| Setting | Value | Why |
|---|---|---|
| `DlssFeatureFlags.MotionVectorsLowRes` | **set** | MVs are rendered at render resolution (§3.6.2 item 1, the preferred case) |
| `DlssFeatureFlags.MotionVectorsJittered` | **clear** | MVs come from unjittered matrices (§3.6.2 item 2) |
| `DlssFeatureFlags.DepthInverted` | **clear** | `CreatePerspectiveFieldOfView` gives near → 0, far → 1 (§3.8) |
| `DlssFeatureFlags.Hdr` | **clear** | LDR path, see D4 |
| `DlssFeatureFlags.AutoExposure` | **set** | no exposure image is bound; matches `DlssHardwareTests.cs:106` |
| `MotionVectorScaleX/Y` | left at `1f` | already in pixels, already the right direction |
| `DlssEvaluateInputs.Reset` | set on the first frame after every feature creation (start-up and each resize) | there are no camera cuts, and this is the only honest place to exercise it |
| Motion-vector format | `VK_FORMAT_R16G16_SFLOAT` | §3.6.1 permits it, and in **pixel** space FP16 carries ~3 decimal digits of *relative* precision: a 32-pixel motion is resolved to ≈0.03 px, a 1-pixel motion to ≈0.001 px, both far below the sub-pixel accuracy DLSS needs |
| Depth format | `VK_FORMAT_D32_SFLOAT` | issue scope; single-aspect, so nothing to exclude from the view |

#### Why not the alternatives

- **Jittered motion vectors + `MotionVectorsJittered`.** One fewer matrix in the
  uniform block, and it makes the sample's correctness depend on DLSS's internal
  jitter subtraction rather than on something the reader can verify from the
  shader. Rejected: the point of the sample is that the convention is legible.
- **UV-space motion vectors with `MotionVectorScaleX/Y = renderExtent`.** Valid
  per §3.6.3, and it hides the unit conversion inside a parameter the reader
  cannot see in the shader. Rejected for the sample; **documented** in
  `docs/ngx-notes.md` (D12) as the general affordance §3.6.3 provides for
  renderers whose existing vectors are UV-space or forward-signed and who would
  rather set two floats than touch a shader.
- **`R32G32_SFLOAT` motion vectors.** Twice the bandwidth on a full-resolution
  target for precision the pixel-space encoding does not need (see the table).
  Rejected — but note that the calculus inverts for UV-space vectors, where the
  values are ~1/1000 the magnitude and FP16's absolute precision does become the
  binding constraint; that is a reason to encode in pixels, not a reason to widen
  the format.
- **Reversed-Z depth + `DepthInverted`.** Better precision, and one more flag
  whose miswiring is invisible in a static frame. Rejected for the sample; the
  flag is documented.
- **The guide's `M[2][0] += jitter` form.** Sign- and magnitude-ambiguous under a
  right-handed projection; see step 2. Rejected, with the algebra recorded so the
  next reader does not "simplify" it back.

### D3. Four modes, chosen on the command line, and no run-time toggle

```
HelloDlaa [--mode dlaa|quality|off|bilinear] [--frames N] [--capture <path.png>]
          [--require-dlss] [--no-validation]
```

| Mode | Render extent | DLSS | Presentation path |
|---|---|---|---|
| `dlaa` *(default)* | output extent | `DlssQualityMode.Dlaa` | DLSS → presentation image |
| `quality` | `GetOptimalSettings(w, h, MaxQuality).Render*` | `DlssQualityMode.MaxQuality` | DLSS → presentation image |
| `off` | output extent | none | 1:1 `VK_FILTER_NEAREST` blit → presentation image |
| `bilinear` | the same extent `quality` would use | none | `VK_FILTER_LINEAR` upscale blit → presentation image |

`bilinear` exists so that `quality` has an honest control: comparing DLSS against
a native-resolution render flatters nothing, comparing it against the *same
low-resolution render* upscaled naively is what shows the reconstruction. Jitter
and mip bias are applied in `dlaa`/`quality` and not in `off`/`bilinear`.

`--require-dlss` turns "DLSS unavailable on this host" from a clean skip
(exit 0, the `HelloRayQuery` posture at `samples/HelloRayQuery/Program.cs:90-98`)
into exit 5. The verification protocol uses it so a silent skip cannot be mistaken
for a pass.

#### Why not the alternatives

- **A run-time key toggle.** Needs a new key in `tests/Ahjo.Vulkan.Tests/SdlWindow.cs`
  (E3), a file owned by the test project and link-compiled into three samples.
  Growing shared test infrastructure for one sample's ergonomics is the wrong
  trade. Rejected.
- **Cycling modes automatically every N seconds.** Non-deterministic output, and
  it makes `--capture` meaningless. Rejected.
- **Only `dlaa` and `quality`** (the issue's literal scope). Leaves the reader
  with nothing to compare against, in a sample whose entire value is the
  comparison. Rejected; `off` and `bilinear` are two `if`s.

### D4. LDR colour path: the shader encodes sRGB into a UNORM target, and the swapchain is requested UNORM

§3.1.2 (E10) is unusually explicit: in LDR mode the colour buffer must be
perceptually encoded and **must not** be linear. The sample therefore:

- renders colour into `VK_FORMAT_R8G8B8A8_UNORM` with the fragment shader
  applying the sRGB transfer function itself — so the *stored bits* are
  perceptual and the *view NGX reads through* is UNORM, meaning no hardware
  decode puts linear values back in front of DLSS;
- uses `VK_FORMAT_R8G8B8A8_UNORM` for the presentation image too — mandatory
  storage-image support in Vulkan, unlike `B8G8R8A8_UNORM` (optional) and every
  `_SRGB` format (never), and RGBA byte order, which is what
  `PngWriter.Write(path, rgba, w, h)` expects (`src/Ahjo.Vulkan.Utilities/PngWriter.cs:22`);
- requests `SwapchainDescription.PreferredFormats = [B8G8R8A8_UNORM, R8G8B8A8_UNORM]`
  and prints a **warning line** if the surface only offers an `_SRGB` format,
  because `vkCmdBlitImage` from a UNORM source to an `_SRGB` destination applies
  the encode a second time and the result is visibly washed out;
- leaves `DlssFeatureFlags.Hdr` clear.

#### Why not the alternatives

- **`R16G16B16A16_SFLOAT` linear colour + `Hdr`.** Closer to a real deferred
  renderer, and it needs a tone-mapping pass after the evaluate that the sample
  otherwise has no reason to own. Rejected; noted in `docs/ngx-notes.md` as the
  path a real renderer takes.
- **An `_SRGB` colour attachment.** The hardware would encode on write and
  *decode* on the sampled read NGX performs, handing DLSS linear values in LDR
  mode — the exact configuration §3.1.2 says produces banding. Rejected, and
  worth stating loudly because it is the intuitive choice.
- **Blitting into an `_SRGB` swapchain and accepting the double encode.**
  Rejected: a sample whose colours are wrong teaches the wrong thing.

### D5. Per-frame-slot targets, one shared feature, two frames in flight

`FramesInFlight = 2`, and **every** DLSS-facing image is per slot: colour, depth,
motion vectors and the presentation image, giving two complete sets. The
`DlssFeature` is **not** per slot — one feature per render extent.

The reason is the hazard `HelloVmaWindowed` documents at length for its uniform
ring (`Program.cs:150-176`): with two frames in flight, frame N+1's rasterization
into the colour target can overlap frame N's DLSS read of it on the GPU. Nothing
orders two submissions against each other except semaphores and barriers, and the
acquire semaphore only covers colour-attachment output on the swapchain image. A
single shared colour/MV/depth/presentation set is a write-after-read hazard that
standard validation does not report (synchronization validation does). Per-slot
sets remove the question: `FrameRing.BeginFrame` has already waited on that slot's
fence before handing it back.

One feature is correct and is what every DLSS integration ships: NGX's history and
scratch surfaces are internal, evaluates are recorded once per frame from one
thread and submitted in order to one queue. The wrapper's re-entrancy guard
(#218 D5) covers the thread-safety half.

Cost at 1920×1080: about 32 MB per slot, 64 MB total. Every target is allocated
with `AllocationFlags.DedicatedMemory` — #214's recommendation for full-screen
targets, and one of the things `docs/ngx-notes.md` must show rather than assert.

#### Why not the alternatives

- **`FramesInFlight = 1`.** Removes the hazard by removing the pipelining, and
  teaches a frame loop no renderer uses. Rejected.
- **Shared targets with an execution barrier at the top of each frame.** Would
  work — `AllCommands → AllCommands` serializes the frames — and it is a
  serialization dressed up as synchronization. Rejected.
- **Per-slot `DlssFeature`s.** Two features means two independent temporal
  histories each seeing every other frame, which is a worse image and twice the
  DLSS VRAM. Rejected; it is also the opposite of what §3.14's VRAM guidance
  assumes.

### D6. Mip bias is the guide's formula for every DLSS mode, printed at start-up; the sampler is recreated when the render extent changes

`mipLodBias = log2(renderWidth / outputWidth) - 1.0f` (§3.5, with `NativeBias = 0`
and `epsilon = 0`), set on `SamplerDescription.MipLodBias`
(`Memory/SamplerDescription.cs:30`), `0f` in `off`/`bilinear`. The sampler is
owned by `CubeScene` and rebuilt whenever the render extent changes, which is the
only time the value can move.

For `dlaa` the formula gives **−1.0**, a real negative bias at native resolution.
That is what the guide says, and §3.5.1 warns that high-frequency content — which
this sample's texture deliberately is — can moiré under an aggressive bias.
**Resolved by the repo owner (2026-09-04): ship the formula unmodified for every
DLSS mode, print the computed value at start-up, and have the hardware
verification step record explicitly what the fine-checker half of the texture
looks like at −1.0.** If it moirés, the follow-up is a `--mip-bias <float>`
override with the formula as the default — never a silently different formula,
because the formula is the thing consumers will copy.

The texture is generated procedurally — a 512×512 RGBA8 pattern mixing a
fine-cell and a coarse-cell checker with single-texel grid lines — uploaded with
`StagingBatch` and mipped with `CommandRecorder.GenerateMips`
(`Recording/CommandRecorder.cs:1863`); `samples/HelloCube/Program.cs:173-208` and
`:510-560` are the recipe to copy. High-frequency content is deliberate: it is
what makes DLAA's reconstruction visible, it is what makes a jitter or
motion-vector sign error show up as shimmer instead of hiding, and it is what makes
the bias question above answerable by looking.

#### Why not the alternatives

- **No texture; an untextured shaded cube.** Then mip bias is unrepresentable and
  a third of the issue's scope becomes a doc paragraph. Rejected.
- **A PNG asset like `HelloCube`'s `crate.png`.** Adds a binary to the repo for
  something twenty lines of code produce, and a procedural pattern can be tuned
  to the aliasing behaviour being demonstrated. Rejected.
- **Bias `0` for DLAA.** Contradicts §3.5's formula, which does not special-case
  a 1:1 ratio. Rejected as the *default*; the `--mip-bias` override is the
  documented escape if the hardware run shows it is needed.

### D7. The frame graph, in the order the recorder sees it

One command buffer per frame, from `FrameContext.CommandBuffers.Begin()`:

1. **Barriers in** — colour and motion vectors `UNDEFINED → COLOR_ATTACHMENT_OPTIMAL`,
   depth `UNDEFINED → DEPTH_ATTACHMENT_OPTIMAL`. `UNDEFINED` because every one is
   cleared on load.
2. **`BeginRendering`** with two colour attachments (colour, motion vectors) and
   the depth attachment; render area and viewport are the **render** extent.
   Motion vectors clear to `(0,0)`.
3. Bind pipeline, push descriptors (uniform buffer + combined image sampler),
   bind vertex/index buffers, `DrawIndexed`.
4. **`EndRendering`.**
5. **Barriers out** — colour and motion vectors `COLOR_ATTACHMENT_OPTIMAL → SHADER_READ_ONLY_OPTIMAL`,
   depth `DEPTH_ATTACHMENT_OPTIMAL → SHADER_READ_ONLY_OPTIMAL` (depth aspect),
   presentation image `UNDEFINED → GENERAL` with destination scope
   `Stage.ComputeShader | Stage.AllTransfer` / `Access.ShaderWrite | Access.TransferWrite`
   — E8's recipe, including the transfer half for DLSS's own clear.
6. **`dlss.Evaluate(ref rec, in inputs)`** — outside any rendering scope, which is
   step 4's whole purpose. In `off`/`bilinear` this is a `BlitImage` into the
   presentation image instead.
7. **Barriers** — presentation image `GENERAL → TRANSFER_SRC_OPTIMAL`; swapchain
   image `UNDEFINED → TRANSFER_DST_OPTIMAL`, recorded with
   `ImageBarrier.Transition(in swapchainImage, …)` over the `Image` from
   `Swapchain.GetImage` (D11).
8. **`BlitImage`** presentation → swapchain, `VK_FILTER_NEAREST` (the extents are
   equal, so nearest is exact and states that no resampling is intended), with the
   region from `ImageBlitRegion.WholeImage(in presentation, in swapchainImage)` —
   which is correct precisely because D11 gives the swapchain image its extent.
9. **Barrier** — swapchain `TRANSFER_DST_OPTIMAL → PRESENT_SRC_KHR`.
10. `fc.Submit(queue, ref rec, swap, imageIndex)`, then `swap.Present(queue, imageIndex)`.

The swapchain is created with `ImageUsage = ColorAttachment | TransferDst`.

There is no draw after step 6, so nothing needs rebinding — and the sample says
exactly that in a comment at that point rather than staying silent, because "rebind
after evaluate" (#218, guide §5.2.5) is one of the things a reader comes to this
file to find.

### D8. The presentation image exists in all four modes, and that is what makes the sample uniform

Every mode writes an output-resolution `R8G8B8A8_UNORM` image with usage
`Storage | TransferSrc | TransferDst` and then blits it to the swapchain. DLSS
writes it in `dlaa`/`quality`; a blit writes it in `off`/`bilinear`.

Three things fall out for free: the swapchain blit is one code path; `--capture`
reads an image the application allocated and therefore fully describes, in the
byte order `PngWriter` wants; and the `Storage | TransferDst` pairing that #218 D3
discovered the hard way is present in every configuration, so a reader cannot copy
the non-DLSS path and lose it.

### D9. `--capture` writes the presentation image to PNG, because RenderDoc cannot

Guide §8.6: RenderDoc does not work with DLSS applications. So the only frame dump
available is one the application takes itself. `--capture <path>` runs to the
frame given by `--frames` (default 240), then on that frame adds a
`GENERAL → TRANSFER_SRC_OPTIMAL` barrier, `CopyImageToBuffer` with
`BufferImageCopy.WholeImage(in presentationImage)`, waits for the device to go
idle and calls `PngWriter.Write`. `HeadlessExport/Program.cs:145-185` is the
recipe.

Because all four modes populate the same image, `--mode quality --capture q.png`
and `--mode bilinear --capture b.png` produce two same-size PNGs of the same
sub-sampled render, one reconstructed and one not. That comparison is the
verification artefact the plan asks for.

### D10. OPEN-7 stays deferred: the blit is not a workaround, it is the correct shape — and its real blockers are not the ones OPEN-7 names

**The finding.** OPEN-7 frames the problem as `NgxImage` taking extent and format
off an `Image`, which a `FromRaw` swapchain handle does not carry. Writing the
call site showed that fixing that is neither sufficient nor the interesting part:

1. **The metadata is not what stops it — and D11 supplies it anyway.** With
   `Swapchain.GetImage` landed, `NgxImage.CreateView(device, in swapchainImage, …)`
   produces an `NgxImage` carrying a real extent, a real format and real usage,
   and it passes `RequireMetadata` (E6) without any change to `NgxImage`. So
   OPEN-7's *API* question is answered in the negative: `NgxImage` needs no new
   parameters, now or later.
2. **Usage stops it.** `Swapchain` forwards the requested usage to
   `vkCreateSwapchainKHR` with no clamp against `supportedUsageFlags` (E5,
   `Swapchain.cs:499`, `:522`), and the spec only guarantees `COLOR_ATTACHMENT`
   there. On a surface that does not advertise `STORAGE`, "DLSS writes into the
   swapchain" is not expressible at all.
3. **Format stops it on exactly the machines it is meant to help.** DLSS's output
   must be a storage image; no `VK_FORMAT_*_SRGB` supports
   `VK_IMAGE_USAGE_STORAGE_BIT`, and `SwapchainDescription`'s own doc comment
   recommends preferring sRGB formats (`SwapchainDescription.cs:40-44`). Writing
   DLSS output straight to the swapchain forces the swapchain to a UNORM format
   *and* forces the application into D4's manual-encode colour path.
4. **The blit is not overhead being tolerated.** The presentation image is load
   bearing in three other ways (D8): it unifies the four modes, it makes
   `--capture` possible on a DLSS application RenderDoc cannot inspect, and it
   keeps the `Storage | TransferDst` pairing visible in every configuration. A
   full-screen `vkCmdBlitImage2` at 1:1 is also a cost a real renderer pays
   anyway, because DLSS's output is followed by post-process and UI, not by
   present.

**Decision: the sample blits, and `NgxImage` gains no extent/format parameters —
in this issue or any other.** What remains of OPEN-7 after D11 is not an API
question but a portability judgement each consumer makes for its own target
hardware, and `docs/ngx-notes.md` (D12) states it as such: it is expressible, it
constrains your swapchain format and usage, and this repository's sample
deliberately does not demonstrate a configuration that only works on some
surfaces.

#### Why not the alternatives

- **Add `NgxImage.Wrap(…, uint width, uint height, VkFormat format)`.** Grows the
  public surface of a type whose entire design rationale is that its inputs cannot
  disagree (#218 D2), to solve a problem D11 solves at the source. Rejected —
  and note that `NgxImage.Wrap` over `swap.ImageViews[i]` remains inexpressible
  for a different reason: `Swapchain` does not expose the `ImageViewDescription`
  that produced those views, and `Wrap` requires it. `CreateView` over
  `Swapchain.GetImage` is the reachable route.
- **Have the sample create the swapchain with `ImageUsage.Storage` and write DLSS
  output straight into it.** Would demonstrate a configuration that is not
  portable and that forces a UNORM swapchain, in the one file consumers will copy
  from. Rejected.

### D11. `Swapchain.GetImage(uint index)` — a borrowed `Image` carrying the swapchain's extent, format and usage

This is the one change to `src/Ahjo.Vulkan` in this issue. It is a public API
addition to the core package, made here because `HelloDlaa` is the call site that
motivates it and the repo's first blit-to-swapchain (E4).

```csharp
// src/Ahjo.Vulkan/Rendering/Swapchain.cs, beside GetImageHandle
public Image GetImage(uint index);
```

**What it returns.** An `Image` built through `Image`'s existing internal
constructor with `allocation: null, owner: default` — the same shape
`Image.FromRaw` uses — but with the four facts the swapchain actually knows
substituted for `FromRaw`'s "unknown" placeholders:

| Field | Value | Source |
|---|---|---|
| `Handle` | `_images[index]` | the same pointer `GetImageHandle` returns |
| `Format` | `_format.format` | `Swapchain.cs:75` |
| `Width`, `Height` | `_extent.width`, `_extent.height` | `:77` |
| `Depth`, `MipLevels`, `ArrayLayers` | `1`, `1`, `1` | a swapchain image is 2-D, single-mip; `imageArrayLayers = 1` is hardcoded at creation (`:521`) |
| `Usage` | `_imageUsage` | `:79` — the usage actually passed to `vkCreateSwapchainKHR` (`:522`), not a guess |
| `AllocationHandle`, `Owner`, `PersistentMapped` | `null`, `default`, `null` | it is not a VMA allocation |

**Ownership, tracking and `Dispose` — the trap, and why it is already closed.**
E7 is the evidence. Because `Owner` is `default`, `OwnsHandle` is `false`, and
therefore:

- `HandleRegistry.TrackCreate` returns on its **first branch**
  (`Diagnostics/HandleRegistry.cs:67-68`) — the returned image is never entered
  into the live set, so it cannot produce a false double-dispose report and cannot
  churn the registry when called once per frame.
- `Image.Dispose` returns at `if (!OwnsHandle) return;` (`Resources/Image.cs:155`)
  — **before** `HandleRegistry.TrackDispose` and **before** `vmaDestroyImage`.
  So `Dispose` on this value is a no-op: it destroys nothing, is idempotent, and
  cannot be made to hand a swapchain-owned `VkImage` to VMA. `using var img = swap.GetImage(i);`
  is harmless and pointless; the doc comment says both.
- `OwnsMemory` is `false` (`AllocationHandle == null`, `:120`), so every
  allocation-addressing operation no-ops as it already does for `FromRaw`.

No new ownership flag, no `Image` change, no `HandleRegistry` change, no `Dispose`
change. The mechanism is the ownership discriminator #118 already established, and
this decision's contribution is to stop conflating "borrowed" with "unknown".

**Lifetime.** The returned value is valid only while this swapchain is alive and
un-recreated: `Recreate` replaces `_images` and may change `_extent` and
`_format`. The doc comment states that it must not be cached across `Recreate` —
the same contract `ImageViews` already carries.

**Bounds.** `ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, (uint)_images.Length)`,
so an out-of-range index names the parameter instead of surfacing as
`IndexOutOfRangeException` from an array the caller cannot see.

**Construction is on demand, not cached.** No `Image[]` is materialized in
`Swapchain` or in `Recreate`: the value is eleven field assignments plus two
predictable branches inside `TrackCreate`, it allocates nothing, and not caching
means there is no second array for `Recreate` to keep in sync.

**`GetImageHandle` stays.** It returns the `nint` that `ImageBarrier.Image`
(`Recording/ImageBarrier.cs:33`) takes in its object-initializer form, and five
existing call sites use it that way (E4). Removing it would be a breaking change
for no gain. The doc comments split the two: `GetImageHandle` when you want the
raw handle for an `ImageBarrier` initializer; `GetImage` when you want an `Image`
— for `ImageBarrier.Transition`, for `ImageBlitRegion.WholeImage` /
`BufferImageCopy.WholeImage`, or for anything that reads the extent, format or
usage.

**Existing call sites are left alone.** E4 establishes that no sample or test
blits to a swapchain image today, so there is no "two conventions in `samples/`"
outcome to avoid: `HelloDlaa` introduces the only blit-to-swapchain in the repo
and uses the new API. The five `GetImageHandle` barrier sites are correct as
written, and rewriting them to `ImageBarrier.Transition(in swap.GetImage(i), …)`
would open five files this PR has no other reason to touch, in a PR already
carrying three phases of NGX work. If a future change wants that uniformity it is
a mechanical follow-up with a clean diff.

#### Why not the alternatives

- **A public `Image.FromRaw(nint, VkFormat, uint width, uint height, ImageUsage)`
  overload.** More general, and it lets any caller *assert* metadata about a
  handle nothing can verify — reintroducing exactly the class of
  self-disagreement #218 D2 spent a design on closing, in the core package. The
  swapchain is the only object in the repo that genuinely knows these facts, so
  the API belongs on it. Rejected; noted as the shape to revisit only if a second
  producer of borrowed-but-described images appears.
- **A new `SwapchainImage` type.** Everything downstream — `ImageBarrier`,
  `ImageBlitRegion`, `BufferImageCopy`, `CommandRecorder.BlitImage`,
  `GenerateMips`, `NgxImage.CreateView` — takes `in Image`. A parallel type would
  need a conversion at every one of them, or an implicit operator that makes it
  an `Image` anyway. Rejected.
- **An `IsBorrowed` / `OwnsHandle` flag added to `Image` for this.** The
  discriminator already exists and is already load-bearing (E7): `Owner` being
  null *is* the flag. A second one could disagree with the first. Rejected.
- **Fix `ImageBlitRegion.WholeImage` to tolerate a zero extent** (e.g. throw, or
  fall back to a caller-supplied extent). Treats the symptom, leaves
  `BufferImageCopy.WholeImage`, `GenerateMips` and every future extent reader with
  the same hole, and makes a helper's behaviour depend on how its argument was
  constructed. Rejected.
- **Leave it to the sample and hand-build the region** (the previous
  recommendation). Correct for one call site and wrong for the second one; the
  owner reversed it, and E4 shows the reversal is cheap because there is no
  migration to do.

### D12. `docs/ngx-notes.md` is the consumer contract, and it is written from what was measured, not from the SDK's table of contents

One page, linked from `README.md` next to the migration guide (`README.md:122-124`)
and from `src/Ahjo.Vulkan.Ngx/README.md`. Sections:

1. **What you get and what you must supply** — the #214 model in four sentences,
   and the fact that `NgxContext.Create` throws
   `NgxFeatureLibraryNotFoundException` naming every directory it searched.
2. **Getting `nvngx_dlss.dll`** — NVIDIA/DLSS `lib/<plat>/rel/`, **never** `dev/`
   (watermarked), the `<None Include="nvngx_dlss.dll" CopyToOutputDirectory="PreserveNewest" />`
   item, `NgxDescription.DlssSearchPaths` for the out-of-tree case, and the
   Linux `libnvidia-ngx-dlss.so.<version>` name.
3. **Licence obligations that land on the application** — notify NVIDIA before a
   commercial release, RTX UI branding guidelines, reproduce the third-party
   notices from guide §9.5, never redistribute the `dev/` build. Stated as the
   application's obligations, because they are: this repo ships no feature DLL.
4. **The renderer contract** — jitter (D2 steps 2–3, with the right-handed
   projection sign warning), motion vectors (D2 step 4), mip bias (§3.5 formula),
   depth flags, exposure (`AutoExposure` vs an exposure image), `Reset` on cuts
   and loads, image layouts and the `Storage | TransferDst` pairing, rebind after
   evaluate, and RenderDoc. This section also documents the two general
   affordances §3.6.3 provides — `MotionVectorScaleX/Y` for renderers whose
   existing vectors are UV-space or point along the direction of motion — as
   affordances the guide offers, with the trade-off stated (two floats instead of
   a shader edit, at the cost of a convention the shader no longer shows).
5. **VMA** — opt-in `AllocatorDescription.EnableMemoryBudget` **plus**
   `VulkanExtensions.ExtMemoryBudget` on the device (both, or the wrapper fails
   the pairing check), `Allocator.GetHeapBudgets` to read it, why DLSS's VRAM
   shows up in `Usage` but never in VMA's `AllocationBytes`, and
   `AllocationFlags.DedicatedMemory` for the full-screen targets.
6. **Output targets and the swapchain** — why the sample writes an
   application-owned presentation image and blits, what `Swapchain.GetImage` (D11)
   is for, and the two conditions a DLSS-direct-to-swapchain path would need
   (a surface advertising `STORAGE`, a non-sRGB swapchain format) stated as the
   consumer's portability judgement (D10).
7. **Running `samples/HelloDlaa`** — the four modes, the DLL resolution order,
   and the statement that it is a local-only sample CI never runs.
8. **What the wrapper cannot check** — the three invariants of #218, in the
   consumer's words, with `VK_LAYER_KHRONOS_validation` named as the oracle.

Every trap in section 4 that was found on hardware rather than read in a header
says so, with the driver version, because that provenance is the difference
between a rule and a rumour.

#### Why not the alternatives

- **Fold it into `src/Ahjo.Vulkan.Ngx/README.md`.** That file is the NuGet
  package README — it should stay short enough to read on nuget.org. Rejected;
  it gets a link.
- **Split into `ngx-notes.md` + `ngx-licence.md`.** Two pages, and the licence
  obligations are exactly the thing that must not be a click away from the
  "where do I get the DLL" instructions. Rejected.

### D13. Tests cover the core-package change; the sample carries none

`Swapchain.GetImage` is public API on `Ahjo.Vulkan`, so it gets tests in
`tests/Ahjo.Vulkan.Tests/SwapchainTests.cs`, under that file's existing two gates
— `TestGate.RequirePlatform(IsWindows, …)` and `TestGate.RequireDriver()`
(`SwapchainTests.cs:20-21`) — which is the same lane every other swapchain test
runs in and which does run on the CI runner.

**Provable there, on SwiftShader, with no NVIDIA hardware:** that `GetImage(i)`
agrees with `GetImageHandle(i)` for every index; that it reports the swapchain's
extent, format and usage and `1/1/1` for depth, mips and layers; that
`OwnsHandle` and `OwnsMemory` are `false`; that `Dispose` is a no-op and is safe
to call twice with `AhjoValidation.Enabled` on (the direct regression test for the
`HandleRegistry` question E7 answers); that
`ImageBlitRegion.WholeImage(in src, in swapchainImage)` produces a destination box
equal to the swapchain extent — the defect of E4 asserted directly; that the
values track `Recreate` at a new extent; and that an out-of-range index throws
`ArgumentOutOfRangeException`.

**Not provable there:** that a blit into a swapchain image executes correctly and
layer-clean end to end. That needs a present loop on real hardware, and it is what
`HelloDlaa`'s hardware run supplies (#32 — CI has no NVIDIA GPU, and software
rasterizers are not honest coverage).

The sample itself gets no test and no `[gate:*]` class: CI builds it and never
runs it (E1). Its frame loop is held to the zero-allocation rule by inspection
against E12, not by a benchmark — nothing under `src/` that a benchmark covers
changes here, and `DlssEvaluateBenchmarks` (#218 D13) still pins the wrapper's hot
path.

#### Why not the alternatives

- **A new test file for `GetImage`.** The gates, the `Win32Window` helper and the
  device-creation helper all already live in `SwapchainTests.cs`; a second file
  would duplicate them. Rejected.
- **An ungated test using a headless `Image`.** `GetImage` needs a real
  swapchain, and the point of the test is that it reports the *swapchain's* facts.
  Rejected.

## Scope boundary

This issue is **not** docs-and-samples only. It touches, and only touches:

- `src/Ahjo.Vulkan/Rendering/Swapchain.cs` — one public method, `GetImage(uint)`
  (D11). No other file under `src/` changes: not `Image`, not `HandleRegistry`,
  not `ImageBlitRegion`, and nothing in `Ahjo.Vulkan.Ngx`.
- `tests/Ahjo.Vulkan.Tests/SwapchainTests.cs` — the cases in D13.
- `samples/HelloDlaa/**` (new), `Ahjo.Vulkan.slnx`, `.gitignore`.
- `docs/ngx-notes.md` (new), `README.md`, `src/Ahjo.Vulkan.Ngx/README.md`.

Not designed here, and not to be inferred from anything above: `NgxImage` extent
or format parameters (D10 — closed, not deferred); a public
`Image.FromRaw` metadata overload (D11 alternatives); migrating the five existing
`GetImageHandle` barrier sites (D11); Ray Reconstruction; Frame Generation; OTA
preset updates; the vendor-neutral upscaler contract — the last four are #214
"Later".

**Declined by the repo owner (2026-09-04): the migration note.** The issue's
"add the upscaler-slot note for Logos if the engine has one" item is closed as
**declined**, not omitted, and `docs/migration-vortice-to-ahjo.md` is untouched by
this PR. The reason: this repository's documentation describes the contract DLSS
imposes; mapping a particular consumer's existing pass onto that contract is that
consumer's business, and doing it here would make a downstream implementation look
like an authority on questions — motion-vector direction, jitter sign — where it
is merely another party that has to get them right. The general affordances that
would have carried the mapping (`MotionVectorScaleX/Y` for UV-space or
forward-signed vectors, guide §3.6.3) are documented in `docs/ngx-notes.md` §4 on
their own merits (D12).

Two things a reader should **not** expect to find in `samples/HelloDlaa`: a
tone-mapping pass (D4 chooses the LDR path), and a draw after the evaluate (D7 —
so "rebind after evaluate" is documented in the sample rather than demonstrated
by it).

## OPEN

- **OPEN-1 — RESOLVED 2026-09-04 by the repo owner.** Mip bias ships as §3.5's
  formula for every DLSS mode, including the −1.0 it yields at DLAA; the computed
  value is printed at start-up; the hardware verification step records explicitly
  what the high-frequency checkerboard looks like at that bias. If it moirés, the
  follow-up is a `--mip-bias <float>` override with the formula as the default,
  never a silently different formula. Folded into **D6**.
- **OPEN-2 — RESOLVED 2026-09-04 by the repo owner, reversing the architect's
  recommendation.** `Swapchain.GetImage` is in scope for #219 and ships in
  PR #217 rather than as a follow-up issue. Designed in **D11**, tested per
  **D13**, and the scope boundary restated accordingly. The `HandleRegistry`
  question raised with the reversal turned out to be already answered by the
  existing ownership discriminator (E7): no `Image`, `Dispose` or registry change
  is needed.
- **OPEN-3 — still open; approved to proceed as recommended.** D2 step 2 is a
  closed algebraic derivation and it agrees with the reference integrations, but
  no run on hardware has confirmed it as of this writing. A wrong sign here is a
  half-pixel error: it does not smear, it leaves fine detail permanently
  shimmering instead of resolving. The plan's verification step calls this out as
  the first thing to look for in `--mode dlaa` on a static frame; if it is wrong,
  the fix is one sign in `JitterSequence` **and a correction to this spec**, not a
  quiet patch. The outcome is recorded in `docs/ngx-notes.md` either way.
- **OPEN-4 — RESOLVED 2026-09-04 by the repo owner, as recommended.**
  `--mode quality` on a resize re-queries `GetOptimalSettings`, recreates all
  per-slot targets, releases and recreates the `DlssFeature` (with a
  submitted-and-waited recorder), rebuilds the mip-biased sampler and sets `Reset`
  on the next frame. The visible hitch is accepted, with one printed line per
  recreation. A real renderer debounces; a sample that debounced would hide the
  recreation sequence, which is the part worth reading.

## Cross-links

- Tracking, research, the ship model and the licence position: **#214**.
- Phase 1 (shim, bindings, lane): **#216**,
  `docs/design/specs/2026-09-03-issue-216-ngx-native-design.md`.
- Phase 2 (the wrapper), and in particular **D2** (`NgxImage` is the only producer
  of `NVSDK_NGX_ImageViewInfo_VK`), **D3** (`ReadWrite`, the usage checks and the
  `TransferDst` amendment), **D4** (layout is the caller's, the layer is the
  oracle), **D9** (the four zero-allocation properties of `Evaluate`) and
  **OPEN-7** (answered in D10 above): **#218**,
  `docs/design/specs/2026-09-03-issue-218-ngx-wrapper-design.md`.
- Handle ownership, and why a borrowed handle is neither tracked nor destroyed:
  **#118**, `docs/design/specs/2026-06-12-issue-118-handle-ownership-design.md`,
  `src/Ahjo.Vulkan/Diagnostics/HandleRegistry.cs:42-45`.
- The Slang-at-run-time sample shape, and "the image is the assertion":
  **#207**, `docs/design/specs/2026-08-23-issue-207-hello-ray-query-design.md`.
- Why layout is not on `Image`: **#17**, `src/Ahjo.Vulkan/Resources/Image.cs:19-24`.
- Valid-by-default descriptions, and why `Image.FromRaw` reports `0×0`:
  **#119**, `src/Ahjo.Vulkan/Resources/Image.cs:78-85`.
- Why there is no NVIDIA coverage in CI and why that is recorded rather than
  faked: **#32**, `.github/CLAUDE.md`.
- The per-frame allocation rule the sample's loop is held to: **#29**, **#114**,
  `src/Ahjo.Vulkan/CLAUDE.md`, `src/Ahjo.Vulkan.Ngx/CLAUDE.md`.
- The `SdlWindow` sharing arrangement: **#87**/**#88**,
  `samples/HelloVmaWindowed/HelloVmaWindowed.csproj:16-18`.
