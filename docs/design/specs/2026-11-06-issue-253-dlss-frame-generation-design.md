# `Ahjo.Vulkan.Ngx` — DLSS Frame Generation over the NGX feature API, with presentation left to the application

Issue: [#253](https://github.com/pekkah/Ahjo-Vulkan/issues/253), filed for
pekkah/ahjo#2783. Plan:
[../plans/2026-11-06-issue-253-dlss-frame-generation.md](../plans/2026-11-06-issue-253-dlss-frame-generation.md).

Lands consistently with: #214 (feature DLLs are bring-your-own), #216 (the
`ahjo_ngx` shim and its bindings), #218 (`Ahjo.Vulkan.Ngx`, specifically D5
`NgxContext` diagnosis and D9 the zero-allocation evaluate), #219 (`HelloDlaa`,
the measured-contract precedent), #239 (the NGX pin is **v310.9.1**, not v310.7.0).

---

## Problem

`Ahjo.Vulkan.Ngx` wraps DLSS Super Resolution and DLAA and nothing else. The
gap has four parts.

**P1. No frame-generation surface exists.** The bindings traverse five NGX
headers and none of them is a DLSS-G header (`tools/generate-ngx.rsp:24-29`).
The rsp's own comment notes that the dlssg headers are excluded by traversal
alone. The only frame-generation symbol in `Generated/` is the enum member
`NVSDK_NGX_Feature_FrameGeneration = 11`
(`src/Ahjo.Vulkan.Ngx.Native/Generated/NVSDK_NGX_Feature.cs:17`). `setup-ngx.ps1`
pins the four dlssg headers "against a later phase" (`tools/setup-ngx.ps1:102-105`)
but fetches no frame-generation DLL (`:122-136`).

**P2. The context and support layer is hard-wired to Super Resolution.**
- `NgxContext.Create` reads only the `SuperSampling.*` availability keys and
  throws when SR is unavailable (`src/Ahjo.Vulkan.Ngx/NgxContext.cs:150-163`).
- The missing-library message hard-codes `nvngx_dlss.dll`
  (`NgxContext.cs:636-638`).
- `NgxSupport.TryGetInstanceExtensions`, `TryGetDeviceExtensions` and
  `TryGetSuperSamplingRequirements` pass `NVSDK_NGX_Feature_SuperSampling`
  unconditionally (`src/Ahjo.Vulkan.Ngx/NgxSupport.cs:71`, `:110`, `:160`).

A consumer cannot even ask what frame generation needs.

**P3. Nothing settles who presents.** Frame generation inserts frames at
present time. `Swapchain` presents one image per call through a static
`[DllImport]` (`src/Ahjo.Vulkan/Rendering/Swapchain.cs:710`). `FrameRing` gives
each frame one acquire semaphore (`src/Ahjo.Vulkan/Rendering/FrameContext.cs:54`).
No document says how a second, generated image per rendered frame fits that
model, or who paces it.

**P4. The task framing assumed a delivery path the evidence does not support
on its own.** The issue, and the brief this spec was written against, describe
Streamline as *the* documented Vulkan path. E1 shows that the pinned NGX SDK
documents a direct Vulkan path too. Choosing between the two is the main
decision here (D1).

## Evidence

Call-site counts below came from `Grep` over `src/`, `samples/` and `tests/`,
excluding `obj/`. The `LSP` tool was not loaded for this audit. The counted
names are distinctive, so a grep over-count is unlikely, but these are text
matches, not resolved references.

### E1. The pinned NGX SDK documents a direct Vulkan DLSS-G path

- The pin is `<NgxVersion>v310.9.1</NgxVersion>` (`Directory.Build.props:66`,
  bumped in #239). `native/ngx/pins.sha256:3` records `tag: v310.9.1`.
  `src/Ahjo.Vulkan.Ngx/CLAUDE.md` and `docs/ngx-notes.md` still say v310.7.0, and
  `native/ngx/downloaded/v310.7.0/` is a stale cache.
- The upstream tree at that tag ships `doc/DLSS-FG Programming Guide.pdf` and
  `lib/Windows_x86_64/{rel,dev}/nvngx_dlssg.dll`, plus
  `lib/Linux_x86_64/{rel,dev}/libnvidia-ngx-dlssg.so.310.9.1`. This was listed
  with `gh api repos/NVIDIA/DLSS/git/trees/v310.9.1?recursive=1`. The `.dll`
  sits beside `nvngx_dlss.dll` and is covered by the same RTX SDKs licence.
- `native/ngx/include/nvsdk_ngx_helpers_dlssg_vk.h:45-60` defines
  `NGX_VK_CREATE_DLSSG` over `NVSDK_NGX_VULKAN_CreateFeature(…,
  NVSDK_NGX_Feature_FrameGeneration, …)`. `:62` onward defines
  `NGX_VK_EVALUATE_DLSSG`, which is a sequence of `NVSDK_NGX_Parameter_Set*`
  calls followed by an evaluate.
- The FG guide (the PDF above, "Version 310.7.0"; line numbers refer to a
  `pdftotext -layout` extraction):
  - It recommends Streamline but documents the NGX path in full: "NVIDIA
    recommends integration of DLSS-FG via the NVIDIA Streamline SDK" (l. 61).
    Sections 1-7 then cover the NGX path step by step.
  - It names Vulkan: "your application must use … Vulkan (version 1.1 or
    higher)" (l. 217), and "Using Vulkan Resources … create a
    NVSDK_NGX_Resource_VK" (l. 700-701).
  - It gives the runtime DLL: "nvngx_dlssg.dll" / "libnvidia-ngx-dlssg.so.<ver>"
    (l. 259-262).
  - It puts presentation on the application: "The DLSS-FG feature itself does
    not handle timing or presentation; this is the responsibility of your
    application" (l. 849-850). "Present the generated frame as soon as
    EvaluateFeature completes. Present the retained rendered frame at the
    correct time to achieve equal time intervals" (l. 886-888).

### E2. The shim already exports everything the NGX path calls

`native/ngx/src/ahjo_ngx.def` exports these names: `NVSDK_NGX_VULKAN_CreateFeature1`,
`NVSDK_NGX_VULKAN_EvaluateFeature_C`, `NVSDK_NGX_VULKAN_ReleaseFeature`,
`AllocateParameters`, `GetCapabilityParameters`, `DestroyParameters`, all twelve
`NVSDK_NGX_Parameter_{Set,Get}{ULL,F,D,UI,I,VoidPointer}`, and the three
`ahjo_ngx_vulkan_get_feature_*_utf8` discovery calls. Each discovery call
already takes an `NVSDK_NGX_Feature featureId` (`native/ngx/src/ahjo_ngx.h:210-224`).
So **no shim C++ change and no new export is needed**, and `NgxExportDriftTests`
is unaffected.

### E3. One dlssg header is C-clean; the other two are not

- `nvsdk_ngx_defs_dlssg.h` contains three enums, about 130 `#define` string
  names (`"DLSSG.Backbuffer"`, `"FrameGeneration.Available"`, …), and two
  `NVSDK_CONV` callback typedefs (`:32-46`, `:48-318`, `:320-346`). Under the
  rsp's DELTA 1 (`generate-macro-bindings`, `tools/generate-ngx.rsp`) the names
  come out as `ReadOnlySpan<byte> … => "…"u8`, the form invariant 1 requires.
- `nvsdk_ngx_params_dlssg.h` is C++. It uses default member initializers
  (`unsigned int multiFrameCount = 1;`, `:45`, `:48`;
  `NVSDK_NGX_Coordinates mvecsSubrectBase = { 0, 0 };`, `:109`) and includes
  the D3D entry-point header (`#include "nvsdk_ngx.h" // DX11/12 here`, `:28`).
  The rsp parses `--language c` (`tools/generate-ngx.rsp:61-62`).
- `nvsdk_ngx_helpers_dlssg_vk.h` contains only `static inline` helpers
  (`:45`, `:62`). Nothing in it is an export to P/Invoke.

### E4. Streamline's Vulkan integration would rewire the core wrapper

From Streamline v2.14.16 (latest release, 2026-10-16), `docs/*.md` at that tag:

- Vulkan requires **eight hooks**: `vkQueuePresentKHR`, `vkCreateSwapchainKHR`,
  `vkDestroySwapchainKHR`, `vkGetSwapchainImagesKHR`, `vkAcquireNextImageKHR`,
  `vkDeviceWaitIdle`, `vkCreateWin32SurfaceKHR`, `vkDestroySurfaceKHR`
  (`ProgrammingGuideManualHooking.md`, `enum class FunctionHookID`).
- There are two ways to supply them. Either load `sl.interposer.dll` instead of
  `vulkan-1.dll` (`ProgrammingGuide.md:32`), or fetch per-function proxies
  through SL's `vkGetDeviceProcAddr` (manual hooking §4.2). In both, the device
  "must be created **after** the `slInit` call" (manual hooking §3.0). Manual
  hooking also leaves the host responsible for the extra queues, including an
  optical-flow queue at index 0 of an exclusive family (§5.2.1).
- The wrapper reaches those calls through generated `[DllImport]` statics
  against `vulkan-1`: `Vk.vkAcquireNextImageKHR` (`Swapchain.cs:549`),
  `Vk.vkQueuePresentKHR` (`:710`) and `Vk.vkCreateSwapchainKHR` (`:823`).
  Redirecting them means a dispatch seam through `Swapchain`, `Surface`,
  `Device` and `Instance`, plus `DeviceFunctionTable`
  (`src/Ahjo.Vulkan/Internal/DeviceFunctionTable.cs:31-41` already explains why
  extension entry points need `vkGetDeviceProcAddr`).
- SL presents asynchronously. "When calling these methods from the host side, SL
  returns the 'last known error'" (`ProgrammingGuideDLSS_G.md:628`). That
  breaks the per-call `AcquireResult`/`SwapchainState` mapping in
  `Swapchain.MapPresentationResult` (`Swapchain.cs:567-596`, #120/#222).
- `sl.reflex` is mandatory: "Any existing Reflex SDK integration that does not
  use Streamline cannot be used with DLSS-G" (`ProgrammingGuideDLSS_G.md:684`).
- Two documented Vulkan gaps: "VSync with Frame Generation is only supported on
  D3D12" (`:1159`), and Dynamic MFG is "currently limited to D3D12 only"
  (`:552`).
- The headers are C++ (`enum class`, `sl::Preferences`, `sl::` namespaces in the
  snippets above). No rsp in this repo parses C++, and the NGX rsp's
  `--language c` exists precisely to avoid binding a C++ ABI
  (`tools/generate-ngx.rsp:63-70`).

### E5. Reflex has a native Vulkan form and is already bound; the NGX API does not require it, but NVIDIA's shipping guidelines do

(F1 below has the full audit. The NGX *API* has no Reflex dependency. NVIDIA's
RTX UI Developer Guidelines require Reflex for any title that ships FG.)

- The NGX FG guide mentions Reflex once, descriptively: "DLSS-FG works with
  NVIDIA Reflex to maintain a responsive experience" (l. 118). No NGX parameter
  or API references Reflex.
- `VK_NV_low_latency2` is already in the generated bindings:
  `vkSetLatencySleepModeNV`, `vkLatencySleepNV`, `vkSetLatencyMarkerNV`,
  `vkGetLatencyTimingsNV` and `vkQueueNotifyOutOfBandNV`
  (`src/Ahjo.Vulkan.Native/Generated/Vk.cs:3308-3320`), together with
  `VkLatencySubmissionPresentIdNV`, `VkSwapchainLatencyCreateInfoNV` and
  `VkPresentIdKHR`.
- `sl.reflex` and `sl.pcl` are Streamline plugins. The issue brief names them
  only because it assumed Streamline.

### E6. The present primitives a two-present frame needs already exist

- `Swapchain.AcquireNextImage(in BinarySemaphore signaled, …)` takes any
  semaphore (`Swapchain.cs:534`).
- `Swapchain.Present(Queue, uint, in BinarySemaphore)` presents with a
  caller-chosen wait (`:691`).
- `Queue.Submit2(ref CommandRecorder, in Fence, ReadOnlySpan<SemaphoreSubmit>,
  ReadOnlySpan<SemaphoreSubmit>)` takes any number of waits and signals
  (`src/Ahjo.Vulkan/Lifecycle/Queue.cs:147-193`), and `default(Fence)` is a
  legal null (`:103`).
- `FrameContext` owns one acquire semaphore (`FrameContext.cs:54`). Its
  swapchain-aware `Submit` arms the slot fence and clears the acquire flag only
  after `Submit2` returns (`:174-175`).
- `SemaphorePool.AcquireBinary` exists (`src/Ahjo.Vulkan/Pools/SemaphorePool.cs:36`).
- Fence signal operations cover all earlier submissions on the queue (Vulkan
  spec, "Fence Signaling"). So one fence on the **last** of two submits
  protects the slot's command pool for both.

### E7. The per-frame inputs are SR's inputs plus a camera block, and three conventions need measuring

- **Motion vectors.** "Motion vectors map a pixel from the current frame to its
  position in the previous frame … in units of screen-space pixels at the
  resolution of the motion vector resource", with (0, 0) at the top left and +Y
  down (FG guide l. 4022-4036). That is exactly SR's `previous − current`
  render-pixel convention, which was measured as correct (`docs/ngx-notes.md`
  §4.2). `HelloDlaa`'s motion-vector shader can be reused.
- **Depth.** Hardware (non-linear) units, with an inversion flag (l. 717-719).
  Same as SR's `DepthInverted` (`docs/ngx-notes.md` §4.4).
- **Colour.**
  - The backbuffer format must have "at least three channels containing
    floating-point data (FLOAT, UNORM, or SNORM)" (l. 3908-3911, 4002-4004).
  - The outputs "must be the same size and format as pBackbuffer" (l. 764-765)
    and are written, so they need storage usage.
  - No `_SRGB` format supports `VK_IMAGE_USAGE_STORAGE_BIT` on mainstream
    desktop drivers (`docs/ngx-notes.md` §6). So FG's colour chain is UNORM
    with an in-shader encode, the same rule `HelloDlaa` already follows.
- **HUD-less and UI.** "Strongly recommended" (l. 737). UI colour must be
  premultiplied, so that `Backbuffer = UI + (1 − α)·Hudless` (l. 748-754).
- **`OutputReal`.** Recommended over manual retention (l. 874-879), and needed
  so the dev build's indicator does not flicker (l. 1077-1078).
- **Camera matrices.** "each as a float[4][4] in row-major order (assuming
  post-multiplication)" (l. 786), and they must "NOT contain temporal AA jitter
  offset" (`nvsdk_ngx_defs_dlssg.h:125`). **Unverified:** "post-multiplication"
  does not say whether the vector is on the left or the right, so whether
  `System.Numerics.Matrix4x4` (row-major, `v * M`) goes in as-is or transposed
  is a measurement, not a reading. See D9.
- **Jitter units differ from SR.** FG's `DLSSG.JitterOffsetX/Y` is "clip space
  jitter offset" (`nvsdk_ngx_defs_dlssg.h:138-140`). SR's is render-pixel space
  (`src/Ahjo.Vulkan.Ngx/DlssEvaluateInputs.cs:93-97`). Copying SR's value
  across would be a silent unit error.
- **`MvecScale` is documented two ways.** The header says "scale factors used to
  normalize motion vectors (so the values are in [-1,1] range)"
  (`nvsdk_ngx_defs_dlssg.h:142`). The guide says "rescale your input motion
  vector values to the required pixel-units. Common values are 1 (if the values
  are already in the correct units)" (l. 783-785). These disagree, and one
  measurement decides which is right (D9).

### E8. The guide ships its own oracle for E7's open conventions

The dev build of `nvngx_dlssg.dll` has four debug visualization modes: Motion
Vector Resampling Test, **Clip to Previous Clip Matrix Test**, Depth and Motion
Vector Alignment Test, and UI Hints Test (FG guide l. 1197-1240). The matrix
test "should not highlight" scene geometry that moves only because the camera
moved (l. 1216-1218). That turns the matrix-convention, jitter and `MvecScale`
questions into visual pass/fail checks on the 4070 Ti.

### E9. Licensing and the shipping risk

- `native/ngx/NGX-LICENSE.txt:12` grants distribution of "any software and
  materials within the SDK … as incorporated in object code format into a
  software application". `nvngx_dlssg.dll` is SDK material, the same way
  `nvngx_dlss.dll` is.
- The dev build draws "DLSSG SDK - DO NOT DISTRIBUTE - CONTACT NVIDIA TO OBTAIN
  DLLs FOR YOUR TITLE", and that text "is rendered by any DLSS-FG dynamic
  library which is not intended for production" (FG guide l. 1046, 1054-1055).
- `NVSDK_NGX_Result_FAIL_FeatureDenied`: "NVIDIA has restricted the use of this
  feature in the current application" (l. 3547).
- **Unverified:** whether the public `rel/` build runs unwatermarked and
  un-denied under `Init_with_ProjectID` + `NVSDK_NGX_ENGINE_TYPE_CUSTOM`, which is
  how `Ahjo.Vulkan.Ngx` initializes (`NgxDescription.ProjectId`). That has to be
  measured before anything is built on it (OPEN-1).

### E10. Consumer audit

- **`NgxSupport.TryGetInstanceExtensions` / `TryGetDeviceExtensions` /
  `NgxContext.Create`.** 13 call sites:
  - sample: `samples/HelloDlaa/Program.cs:128`, `:193`, `:290`
  - benchmarks: `tests/Ahjo.Vulkan.Benchmarks/DlssEvaluateBenchmarks.cs:77`,
    `:111`, `:126`
  - tests: `tests/Ahjo.Vulkan.Ngx.Tests/DlssHardwareTests.cs:214`, `:264`,
    `NgxSupportTests.cs:33`, `NgxTestEnvironment.cs:90`, `:135`, `:180`
  - definition: `src/Ahjo.Vulkan.Ngx/NgxContext.cs:87`

  Of these, `HelloDlaa/Program.cs:290`, `NgxTestEnvironment.cs:180` and the
  definition are `NgxContext.Create`, which D4 leaves source-compatible. The
  other 9 are extension queries. Add the two
  `TryGetSuperSamplingRequirements` callers
  (`DlssEvaluateBenchmarks.cs:104`, `NgxTestEnvironment.cs:162`), and D4's
  feature argument touches **11 lines**: two in the sample, three in the
  benchmarks and six in the tests.
- **`NgxFeatureLibraryNotFoundException`.** Constructed at one place
  (`NgxContext.cs:606`), caught at `samples/HelloDlaa/Program.cs:309` and
  `tests/…/NgxTestEnvironment.cs:181`, and asserted at
  `DlssHardwareTests.cs:39`.
- **`DlssSearchPaths`.** 18 references. NGX's path list applies to every feature
  DLL, so the name stays (D2).
- **The Ahjo engine, the only consumer.** pekkah/ahjo#2783 accepts "through
  Streamline, or the SDK on Vulkan". Its exit criteria: a generated frame
  between each pair of rendered frames, measured pacing and frame count, no
  still-camera artifacts versus FG off, off by default, and "hardware without it
  runs the engine unchanged". That last criterion is why D4 must not make
  frame-generation unavailability fatal for a context that only wants SR.

### E11. Hot path and benchmark precedent

`DlssFeature.Evaluate` is the repo's one non-`Recording/` per-frame path. Four
properties hold its 0 B guarantee (`src/Ahjo.Vulkan.Ngx/CLAUDE.md:12-35`), and
the plan follows the same split:

- `EvaluateCore(…, invokeNgx)` (`DlssFeature.cs:136-232`) packs the parameters,
  with the clear in a `finally` (`:220-231`).
- `DlssEvaluateBenchmarks` (`tests/Ahjo.Vulkan.Benchmarks/DlssEvaluateBenchmarks.cs:29`)
  is its own class, and it throws rather than skips on a host without the
  hardware.

### E12. The target GPU supports 2x only

"GPU: An NVIDIA GeForce RTX 40-series GPU or higher is required for DLSS Frame
Generation. DLSS Multi-Frame Generation requires an NVIDIA GeForce RTX 50-series
GPU" (FG guide l. 65-66). On the RTX 4070 Ti, `DLSSG.MultiFrameCountMax` is
expected to be ≤ 1. The multi-frame index and count can be wired up and
validated, but MFG cannot be measured locally.

---

## Decision

### D1. Delivery: the NGX feature API (`NVSDK_NGX_Feature_FrameGeneration`) through the existing `ahjo_ngx` shim. Not Streamline.

Frame generation becomes a second NGX feature beside SR, in the same
`NgxContext`, over the same shim and the same parameter-map mechanism:

- create: `NVSDK_NGX_VULKAN_CreateFeature1(device, cmd, FrameGeneration, params, &handle)`
- evaluate: `NVSDK_NGX_VULKAN_EvaluateFeature_C`

The helper macros are transcribed in managed code, as `CreateDlss` already
transcribes `NGX_VULKAN_CREATE_DLSS_EXT1` (`NgxContext.cs:332-369`).
`Ahjo.Vulkan` itself does not change. Instance, Device, Swapchain and the loader
stay exactly as they are (E2, E6).

This corrects the brief's premise. Streamline is NVIDIA's *recommendation*, and
the NGX path is *documented*, for Vulkan, at the pinned tag (E1). The
recommendation buys automatic pacing and sl.reflex. In this repo it costs a
core-wrapper rewrite and the loss of synchronous present results (E4).

#### Why not the alternatives

- **Streamline with the interposer** (`sl.interposer.dll` instead of
  `vulkan-1.dll`). The binding layer is `[DllImport("vulkan-1")]` statics.
  Retargeting them process-wide changes the dispatch path of *every* Vulkan
  call for every consumer. SL's own guide says the interposer adds CPU
  overhead on Vulkan (manual hooking §1.1). The async "last known error"
  present also breaks #120/#222's `SwapchainState` contract (E4).
- **Streamline with manual hooking.**
  - It needs a proxy-dispatch seam through four core types, for eight
    functions (E4).
  - It forces device creation after `slInit`, plus an optical-flow queue at
    index 0 of an exclusive family.
  - It needs a second native pin from a second repository (signed binaries
    with signature verification, `ProgrammingGuide.md:100-104`), and a
    C++-parsing binding path the repo does not have.
  - It makes `sl.reflex` mandatory.
  - It still leaves VSync-with-FG and dynamic MFG unavailable on Vulkan.

  For one consumer, that is the most expensive option with the fewest Vulkan
  capabilities.
- **P/Invoking `NGX_VK_CREATE_DLSSG` / `NGX_VK_EVALUATE_DLSSG` directly.** They
  are `static inline` in a header (E3), so there is no symbol to bind. Exporting
  C wrappers from the shim would move per-frame parameter packing into C++,
  where the D9 zero-allocation discipline and its benchmark cannot see it, and
  would add a struct ABI (`NVSDK_NGX_DLSSG_Opt_Eval_Params`, 300+ bytes with
  C++ initializers) to keep in sync across the boundary.
- **AMD FSR / Intel XeSS frame generation.** Out of scope per the issue and
  pekkah/ahjo#2783.

**This decision is gated on OPEN-1.** If the 4070 Ti probe shows the `rel/`
DLL unavailable, denied or watermarked under a `ProjectId` init, stop. D1 is
then reopened with that measurement in hand. The fallback is not to quietly
build the Streamline path.

### D2. Pinning and download: same pin, same script, same bring-your-own rule

- No new pin. `NgxVersion` v310.9.1 already covers the FG headers, and it covers
  the FG DLL because NVIDIA ships both from the same repository at the same tag
  (E1).
- `tools/setup-ngx.ps1` gains `nvngx_dlssg.dll` (rel + dev, Windows) and
  `libnvidia-ngx-dlssg.so.<ver>` (rel + dev, Linux) in its *feature* lists. That
  way `-SkipFeatureDll` still makes CI structurally unable to fetch them (#214).
  It also gains `doc/DLSS-FG Programming Guide.pdf` in `-IncludeDocs`.
  `pins.sha256` is regenerated with `-UpdatePins`.
- The FG DLL is **never packed and never committed**, the same as
  `nvngx_dlss.dll` (#214, `native/CLAUDE.md`).
- `NgxDescription.DlssSearchPaths` keeps its name and its meaning. It already
  goes to NGX's single feature path list, which applies to every feature DLL
  (E10). The doc comment is widened to say it covers both DLLs.

### D3. Bindings: traverse `nvsdk_ngx_defs_dlssg.h` only, through the hand-maintained parse root

- `native/ngx/src/ahjo_ngx.h` gains `#include "nvsdk_ngx_defs_dlssg.h"` after
  `nvsdk_ngx_vk.h`.
- `tools/generate-ngx.rsp` adds the header to `--traverse`, with a comment
  saying why the other two dlssg headers stay out (E3).
- `/regen-bindings` regenerates.

This adds the `DLSSG.*` / `FrameGeneration.*` names as `u8` properties on
`NgxApi`, and the `NVSDK_NGX_DLSSG_ResourceFlags` / `_EvalFlags` /
`_FullscreenMode` enums. The managed wrapper writes parameters by name.
`nvsdk_ngx_params_dlssg.h` and `nvsdk_ngx_helpers_dlssg_vk.h` are **not**
traversed: one is C++, the other has no exports (E3). There are no new
`AhjoNgxLayoutId`s, because no FG struct crosses the boundary.

Including the header in the shim's own translation unit changes no export: it
declares only enums, macros and typedefs.

#### Why not the alternatives

- **Hand-written `"DLSSG.*"u8` literals.** This is the drift DELTA 1 exists to
  prevent (`tools/generate-ngx.rsp`, DELTA 1 comment). It would also violate
  invariant 4's intent: generated names come from the pinned header.
- **Traversing `params_dlssg.h` under `--language c++`.** That would flip the
  whole rsp to C++ and re-expose the `NVSDK_NGX_Parameter` vtable the rsp
  deliberately avoids (`:63-70`), only to bind two structs the wrapper does not
  need.

### D4. `NgxContext` probes both features and requires only the ones the caller asks for

- New public `[Flags] enum NgxFeatures : uint { None = 0, SuperResolution = 1,
  FrameGeneration = 2 }`. Also a non-flags `enum NgxFeature { SuperResolution,
  FrameGeneration }` for single-feature APIs.
- `NgxDescription.RequiredFeatures`, valid-by-default
  `= NgxFeatures.SuperResolution`, so every existing caller keeps today's
  behaviour.
- `NgxContext.Create` reads **both** availability blocks: `SuperSampling.*` and
  `FrameGeneration.{Available, FeatureInitResult, NeedsUpdatedDriver,
  MinDriverVersionMajor, MinDriverVersionMinor}`, plus
  `DLSSG.MultiFrameCountMax` (E12). It throws the diagnosed exception (#218 D5)
  only for features in `RequiredFeatures`. It keeps the facts of an optional,
  unavailable feature, so that the matching create call can throw the same
  diagnosed exception later.
- New public surface: `IsFrameGenerationAvailable`, `MaxGeneratedFrames`
  (`MultiFrameCountMax`, clamped to ≥ 1 when FG is available), and
  `CreateFrameGeneration` (D7).
- `NgxSupport.TryGetInstanceExtensions`, `TryGetDeviceExtensions` and
  `TryGetSuperSamplingRequirements` → `TryGetRequirements` all take an
  `NgxFeature`. This breaks 11 call sites (E10). A consumer that wants both
  features unions both sets.
- `NgxFeatureLibraryNotFoundException` gains `Feature`, `ExpectedFile` and
  `SearchedDirectories`. The message names `nvngx_dlssg.dll` /
  `libnvidia-ngx-dlssg.so.<version>` for FG, and its directory list is built
  exactly as `BuildMissingLibraryMessage` builds it today (`NgxContext.cs:634-652`).

This meets pekkah/ahjo#2783's "hardware without it runs the engine unchanged".
The engine requests `SuperResolution`, reads `IsFrameGenerationAvailable`, and
offers the option only when it is true.

**MEASURE (plan step 3):** which key reports a missing `nvngx_dlssg.dll`. The SR
precedent (#218 D5) is `FeatureInitResult == FAIL_FeatureNotFound`. Assume the
FG keys mirror it, and confirm by renaming the DLL away on the 4070 Ti. If they
do not, the plan step says what to record and where.

#### Why not the alternatives

- **Probe FG and throw whenever it is unavailable.** Every 20/30-series machine
  with working SR would lose its context.
- **Never throw from `Create`; throw only from `CreateDlss` /
  `CreateFrameGeneration`.** This drops #218 D5's "`Create` throws rather than
  handing back a context whose first create fails opaquely", and the
  `HelloDlaa` contract that prints the exception from `Create`.
- **A separate `NgxFrameGenerationContext`.** NGX init and shutdown are per
  device (`Shutdown1(VkDevice)`, `NgxContext.cs:441`), so two contexts on one
  device would tear each other down. One context is the shape NGX itself has.

### D5. Present ownership: the application presents. The wrapper owns no present, no pacer and no thread.

Frame-generation evaluate is a **recorded command**, exactly like SR's. It
writes `OutputInterpolated`, and optionally `OutputReal`, into
application-owned images. The application then presents twice per rendered
frame, on the unchanged `Swapchain` and `FrameRing` (E6):

```
BeginFrame (slot k)
  render frame N → Backbuffer (UNORM, + Depth, MotionVectors, Hudless, UI)
  FG evaluate (N−1 ↔ N) → OutputInterpolated, OutputReal
  acquire i0 on extra[k]            ── app-owned per-slot semaphore
  blit OutputInterpolated → i0
  queue.Submit2(rec1, default(Fence), wait extra[k], signal RenderingDone[i0])
  present i0                         ── "as soon as EvaluateFeature completes"
  acquire i1 on fc.ImageAcquired; MarkImageAcquireSignaled
  blit OutputReal → i1
  fc.Submit(queue, ref rec2, swap, i1, …)   ── arms the slot fence, covers both submits (E6)
  present i1                         ── "at the correct time"
```

**Pacing reference: `VK_PRESENT_MODE_FIFO_KHR`.** In FIFO the presentation
engine shows queued images one per vblank. So two back-to-back presents come out
one refresh interval apart, and FIFO's blocking acquire holds the rendered rate
at ≤ refresh/2. That is equal spacing with no thread and no clock in the
wrapper, and it is what `samples/HelloFrameGen` uses. `IMMEDIATE`/`MAILBOX` with
no pacing is the **negative control**: the measured contract must show the
generated frame's display time collapsing there (D9). The FG guide's
asynchronous midpoint present (l. 887-888) is the engine's job, on the engine's
own present thread. The consumer contract says so.

#### Why not the alternatives

- **A wrapper-owned present thread / pacer.**
  - `Swapchain`'s state machine and `vkQueuePresentKHR`'s queue
    external-synchronization rule make a hidden second thread a correctness
    hazard in every consumer.
  - The Ahjo engine already owns its frame loop.
  - The only pacing policy the evidence justifies, "equal spacing", has more
    than one implementation: FIFO, a timed thread, or a future
    `VK_EXT_present_timing`. Picking one in the wrapper now would be
    speculative.
- **A `Swapchain.PresentWithGeneratedFrame` API.** It would bake the sample's
  two-submit shape into the core type and still not solve pacing.
- **Multi-acquire `FrameRing` slots** (a second `ImageAcquired` plus
  `RecycleStaleAcquireSemaphores` coverage). This is genuinely tidier than the
  sample's own semaphore array, but it is a core API shape with exactly one
  caller. If the engine's integration needs the same thing, file it then, with
  two call sites as evidence.

### D6. Reflex is designed in its own `Ahjo.Vulkan` issue, as a native `VK_NV_low_latency2` + PCL Stats wrapper, and that issue gates PR C

The NGX *API* does not require Reflex (E5). NVIDIA's RTX UI Developer
Guidelines do require it for a title that ships FG (F1). So Reflex is out of
scope for **#253**, not optional for **shipping**. `sl.reflex` and `sl.pcl`
only exist on the path D1 rejects. Reflex on Vulkan is a core concern: a swapchain-scoped
extension, present-id chaining into `vkQueuePresentKHR`, and device proc
addresses through `DeviceFunctionTable`. Its design touches the `Swapchain`
present path and the hot-path rules, not NGX. Folding it in would put two
independent designs in one spec, so Reflex gets its own issue with its own
spec and plan (OPEN-3, decided).

**User decision at approval (2026-11-06):** that issue **gates #253 PR C**.
#253 does not close until Reflex has landed, so that `HelloFrameGen`
demonstrates the full latency setup NVIDIA's guidelines require (F1). This spec
does **not** design the Reflex API. That issue is **#261**, and its approved
spec is `2026-11-06-issue-261-reflex-low-latency-design.md`. PR C consumes the #261 API:
- the low-latency members on `Swapchain`;
- `SwapchainDescription.LowLatency`;
- `PclStats`;
- the #261 D5 marker mapping for D5's two presents per rendered frame, which
  puts an `OutOfBandPresentStart`/`End` pair with the rendered frame's id
  around each present, inside one `PresentStart`/`End`.

Plan steps 17b and 19i spell it out. Whether the revision-2 driver honours that
mapping is #261 OPEN-3, measured in plan step 19i, and it is a stop condition
for PR C. The FG consumer contract states the guideline requirement verbatim
and measures latency with Reflex on and off (D9).

### D7. The API: `DlssFrameGenerationFeature`, mirroring `DlssFeature` and its D9 discipline

Public types, all in `Ahjo.Vulkan.Ngx`:

- **`DlssFrameGenerationDescription`** (`readonly record struct`, no pointers):
  - `Width`, `Height`: the backbuffer and output extent.
  - `BackbufferFormat` (`VkFormat`).
  - `RenderWidth`, `RenderHeight`: depth and motion-vector extent, written to
    `DLSSG.InternalWidth/Height`.
  - `DynamicResolution` (`bool`).
- **`DlssFrameGenerationCamera`** (`readonly struct`, valid-by-default):
  - Five `Matrix4x4` members: `ViewToClip`, `ClipToView`, `ClipToPrevClip`,
    `PrevClipToClip` and `ClipToLensClip`. `ClipToLensClip` defaults to
    `Identity`.
  - `Position`, `Up`, `Right`, `Forward` (`Vector3`).
  - `Near`, `Far`, `FieldOfView` (radians) and `AspectRatio`.
  - `PinholeOffset` (`Vector2`, default zero).
- **`DlssFrameGenerationInputs`** (`readonly struct`; it carries `NgxImage`
  pointers, per #218 E16). Members:
  - **Required images:** `Backbuffer`, `Depth`, `MotionVectors`,
    `OutputInterpolated`.
  - **Optional images:** `Hudless`, `UiColorAndAlpha`, `OutputReal`.
  - **Camera:** `Camera` (a `DlssFrameGenerationCamera`).
  - **Motion vectors and jitter:** `JitterOffsetX`, `JitterOffsetY`,
    `MotionVectorScaleX = 1f`, `MotionVectorScaleY = 1f`.
  - **Flags:** `Reset`, `DepthInverted`, `ColorBuffersHdr`,
    `CameraMotionIncluded`, `NotRenderingGameFrames`, `OrthographicProjection`.
  - **Multi-frame:** `MultiFrameCount = 1`, `MultiFrameIndex = 1`.
  - **Frame id:** `BackbufferFrameId` (`ulong`).
- **`DlssFrameGenerationFeature`** (`sealed class`, `IDisposable`):
  - `Evaluate(ref CommandRecorder, in DlssFrameGenerationInputs)`.
  - Read-only `Width`, `Height`, `BackbufferFormat`, `RenderWidth`,
    `RenderHeight`.
- **`NgxContext.CreateFrameGeneration(ref CommandRecorder, in
  DlssFrameGenerationDescription)`**.

The evaluate holds the same four properties as #218 D9, with one addition:

1. All seven `NVSDK_NGX_Resource_VK` values **and all five matrices** are stack
   locals of `EvaluateCore`. The map stores `void*` to the matrices too
   (`SetVoidPointer(…CameraViewToClip…)`, `nvsdk_ngx_helpers_dlssg_vk.h`), and
   an `in` parameter may alias heap storage. So each matrix is copied to a local
   before its address is taken. That is 320 bytes of stack and no pinning.
2. Names are `static readonly Utf8Name` fields over the regenerated `NgxApi`
   `u8` properties (D3).
3. The parameter map is allocated once per feature.
4. `ref CommandRecorder`.

Every resource and matrix key is nulled in a `finally`, as in
`DlssFeature.cs:220-231`.

`AhjoValidation`-gated checks, all in a static, device-free `ValidateInputs`:

- required slots are set;
- metadata is known (`Width`/`Height`/`Format` ≠ 0/`UNDEFINED`, the #218 (b)
  asymmetry);
- `Backbuffer` extent equals the feature's `Width × Height`;
- `OutputInterpolated` and `OutputReal` match `Backbuffer` in extent and format
  (FG guide l. 764-765);
- outputs have `Storage` usage and inputs have `Sampled` usage, with
  `ImageUsage.None` skipped (#218 D3);
- `1 ≤ MultiFrameIndex ≤ MultiFrameCount ≤ MaxGeneratedFrames`.

Create writes `DLSSG.ResourceNeverProvidedFlags = UIAlpha |
BidirectionalDistortionField`, because the wrapper exposes no slot for either.
The header says the flags "may enable memory optimizations"
(`nvsdk_ngx_defs_dlssg.h:101-110`).

**Scope boundary (not exposed in this issue):**

- per-resource subrects (all-zero means the full resource,
  `fg.txt:1294-1295`);
- `UIAlpha` (one of UI or UIAlpha suffices, `nvsdk_ngx_defs_dlssg.h:85`);
- the bidirectional distortion field;
- `OutputDisableInterpolation` (a `VkBuffer` slot, which `NgxImage` cannot
  express);
- `UserInterfaceRecompositionEnabled`;
- `AsyncCreateEnabled`;
- the linearized-depth tuning knobs.

Each one can be added later without reshaping the type.

#### Why not the alternatives

- **Reusing `DlssEvaluateInputs` with extra members.** SR and FG disagree on
  jitter units (E7) and on which slots are required. One struct would make the
  wrong one compile.
- **A `Prepare` / `Evaluate` split.** It is ruled out for the reason it is ruled
  out on SR: the map would hold addresses into a dead frame (`src/Ahjo.Vulkan.Ngx/CLAUDE.md:18-23`).
- **`float[16]` or `Span<float>` camera fields.** These allocate, or force
  callers to pin. `Matrix4x4` is a blittable 64-byte value that is already
  row-major (E7).

### D8. Licensing and shipping

The DLL is bring-your-own (D2). The consumer contract states what changed: two
NVIDIA files now, the `rel/`-only rule, and the watermark as the tell for a
`dev/` build (E9). Whether a commercial Ahjo title needs NVIDIA's sign-off for
`nvngx_dlssg.dll` beyond the licence's notify clause is a human/legal call
(OPEN-4). It does not block the code.

### D9. The measured contract and `samples/HelloFrameGen`

- **Sample.** `samples/HelloFrameGen` is a standalone copy of `HelloDlaa`'s cube
  scene, motion vectors and UNORM-with-in-shader-sRGB colour chain, rendered at
  native resolution with no SR and no jitter. It adds a HUD-less colour target,
  a premultiplied UI target, FG evaluate, and D5's two-present loop under FIFO.
- **Doc.** The new doc is `docs/ngx-frame-generation-notes.md`, written the way
  `docs/ngx-notes.md` is: every claim either cited or measured, with the driver
  version.
- **Recorded on the RTX 4070 Ti** (the plan lists the exact runs):
  1. **Availability.** The `FrameGeneration.*` keys and `MultiFrameCountMax`,
     the instance and device extension lists for `NgxFeature.FrameGeneration`,
     the driver version, and the missing-DLL behaviour (D4).
  2. **The three open conventions, using the dev DLL's own oracles (E8).**
     Matrix orientation (Clip-to-Previous-Clip test mode), FG jitter units, and
     `MvecScale` semantics (Motion Vector Resampling test mode). Each is
     recorded as "tried A and B; A passes the mode, B highlights static
     geometry".
  3. **Pacing.** Per-present display-change intervals under FIFO and under the
     `IMMEDIATE` negative control. Primary source: PresentMon's
     `MsBetweenDisplayChange`. Fallback if PresentMon is unavailable:
     host-side timestamps of `AcquireNextImage` returns.
  4. **Frame count.** Presents per rendered frame = 2.
  5. **The still-camera check from pekkah/ahjo#2783.** Mean absolute difference
     between a generated frame and the following real frame on a frozen scene,
     with the #219 technique.
  6. **The `rel/` build has no watermark**, from a capture.
  7. **Validation layer clean**, with any FG-specific VUIDs recorded, as
     #218 D3 recorded `vkCmdClearColorImage`.
  8. **Latency with Reflex,** off/on/boost with FG off and on, plus the Reflex
     Test Utility result. This uses the #261 API (D6; plan step 19i), and
     includes the #261 OPEN-3 attribution check.

### D10. Tests and benchmark

- **No-device validation tests** (`DlssFrameGenerationValidationTests`) drive
  every branch of the static `ValidateInputs`.
- **Context projection tests** cover the `RequiredFeatures` throw/no-throw
  matrix, through an `internal static` projection over raw key values (the
  `ProjectOptimalSettings` pattern, `NgxContext.cs:461-479`).
- **`[gate:feature]` hardware tests** are gated on a new
  `NgxTestEnvironment.IsFrameGenerationAvailable`.
- **`DlssFrameGenerationEvaluateBenchmarks`** is its own class beside
  `DlssEvaluateBenchmarks`. It throws, never skips, and targets `Allocated: -`
  on both `PackParameters` (`invokeNgx: false`) and the full evaluate.

### D11. Delivery: three PRs, each branched from `main` and merged in order, never stacked

The full exit (wrapper, sample, contract) is too large to review as one diff,
and it has a hard measurement gate at the front (OPEN-1). Each PR below is
branched from `main` **after its predecessor has merged**, per the no-stacked-PR
rule.

| PR | Contents | Gate |
|---|---|---|
| **A** | `setup-ngx.ps1` + pins, rsp + regen, `NgxFeature(s)`, `NgxSupport` feature argument, `NgxContext` FG availability + diagnosed exception, tests, the `docs/ngx-frame-generation-notes.md` stub | OPEN-1 measured first (plan step 1) |
| **B** | `DlssFrameGenerationDescription/Camera/Inputs/Feature`, `CreateFrameGeneration`, validation, tests, benchmark, package CLAUDE.md | A merged |
| **R** (#261) | Native Reflex (`VK_NV_low_latency2` + PCL Stats) in `Ahjo.Vulkan`. Its own spec, plan and PR | Independent of A/B. It can land in parallel, branched from `main` |
| **C** | `samples/HelloFrameGen` with Reflex wired in, `docs/ngx-frame-generation-notes.md` with the D9 measurements, README updates | **B merged and R merged** |

The current branch `issue-253-dlss-frame-generation` carries this spec and plan
plus PR A, which closes nothing. PR C branches from `main` only after both PR B
and the #261 PR have merged. Neither one is stacked on the other.

**User decision, 2026-11-07: PR C ships as "Refs #253", not "Closes #253".**
The Reflex Test Utility (OPEN-6) is deferred. A small follow-up PR, also
branched from `main`, records its result in `docs/ngx-frame-generation-notes.md`
and closes #253 (plan step 22).

---

## Future-proofing and Reflex

Added at the approval gate. Every claim carries a source. Line numbers on PDFs
refer to `pdftotext -layout` extractions. The two documents not cited earlier
are:

- the **RTX UI Developer Guidelines**, `doc/RTX UI Developer Guidelines.pdf` in
  NVIDIA/DLSS at v310.9.1, © 2026, cited as "UI l. N";
- the **NVIDIA Reflex SDK Integration Guide** v1.6, `NVIDIA-RTX/REFLEX` HEAD,
  last updated 2026-09-01, cited as "Reflex l. N".

### F1. Is Reflex required to ship DLSS Frame Generation?

**Yes, by NVIDIA's guidelines. No, by the licence. No, by the NGX API.**

| Source | What it says about Reflex | Binding? |
|---|---|---|
| RTX SDKs licence + RTX supplement (`native/ngx/NGX-LICENSE.txt`, all 107 lines) | Nothing. The word does not occur. The supplement does impose notify-before-release (§4, l. 77), trademark placement (§7(b), l. 89), and NVIDIA's right to "temporarily disable the DLSS integration" over quality issues (§6.2, l. 84) | Contractual, and silent on Reflex |
| NGX FG guide v310.7.0 | "DLSS-FG works with NVIDIA Reflex" (l. 118). No API, parameter or checklist item | Not a requirement |
| **RTX UI Developer Guidelines** (shipped in the pinned SDK) | "DLSS Frame Generation requires a GeForce RTX 40 Series GPU or higher **and a Reflex SDK integration**" (UI l. 88-89). "DLSS Frame Generation requires NVIDIA Reflex Low Latency integration and enablement. **Users should not be able to disable NVIDIA Reflex Low Latency when Frame Generation is ON**" (UI l. 149-150). "Reflex is required for DLSS Frame Generation, and will automatically turn on when DLSS Frame Generation is enabled" (UI l. 295). "Please make sure PCL Markers are always enabled on all vendor GPUs" (UI l. 142-143) | NVIDIA's integration requirement. The DLSS guide's deployment checklist points to this document (DLSS Programming Guide §2.5, "see section 3.2.1 and RTX UI Developer Guidelines", and §3.2.1). `docs/ngx-notes.md` §3 already lists following these guidelines as an application obligation |
| Reflex SDK QA checklist | "Reflex Test Utility Report passed without warning with DLSS Frame Generation" (Reflex l. 1020), run with "FG to On" (l. 1060) | NVIDIA's own verification procedure |
| Streamline DLSS-G guide | "**It is required** for sl.reflex to be integrated … Any existing Reflex SDK integration that does not use Streamline cannot be used with DLSS-G" (`ProgrammingGuideDLSS_G.md:684`) | Streamline only. It is a mechanical requirement of SL's pacer, and does not apply to the NGX path |

**On the direct NGX path,** nothing breaks without Reflex. The requirement is
a shipping and integration-quality rule from NVIDIA's guidelines, and NVIDIA
has a contractual lever behind quality rules (licence §6.2). **On Streamline,**
it is also a hard technical prerequisite, and it must be *sl.reflex*
specifically.

**Does native `VK_NV_low_latency2` satisfy it? Partly evidenced; OPEN-6.**

For it:
- NVIDIA's own Streamline source calls it "the modern Reflex path"
  (`source/plugins/sl.reflex/reflexEntry.cpp:289` at v2.14.16).
- `sl.reflex` itself enables `VK_NV_LOW_LATENCY_2_EXTENSION_NAME` on Turing+
  (`:296-317`) and calls `vkLatencySleepNV` (`:937`).

Against it, or at least unresolved:
- The Reflex SDK's documented Vulkan integration is a different library,
  `NvLowLatencyVk.dll` with `NvLL_VK_InitLowLatencyDevice` / `NvLL_VK_Sleep`
  (`Reflex_Vulkan/inc/NvLowLatencyVk.h:109-159`; Reflex l. 207-215, 316-320).
  The guide never mentions `VK_NV_low_latency2`.
- "A Reflex SDK integration" also includes **PCL Stats**: ETW-based
  `PCLSTATS_*` macros that "do not interact with NvAPI, DirectX, or Vulkan in
  any way" (Reflex l. 169-186). The UI guidelines want these on every vendor's
  GPU (UI l. 142-143). `VK_NV_low_latency2` does not provide them.

So a native wrapper covers the *low-latency* half on evidence NVIDIA wrote. Whether
NVIDIA's verification accepts it, and how PCL Stats would be emitted from C#
(ETW TraceLogging, AOT-clean), are open.

**OPEN-3 changes, D1 does not.** OPEN-3 is no longer "optional follow-up". Its
scope is `VK_NV_low_latency2` **plus PCL Stats**, and it is required before the
Ahjo engine ships FG (pekkah/ahjo#2783).

The architect's first recommendation was not to gate #253 PR C on it, because
the NGX path runs without Reflex. **At approval the user decided to gate PR C
on it anyway (2026-11-06):** #253 closes only once `HelloFrameGen` shows FG
together with Reflex. The cost is the serialization noted under D6: PR C now
waits for a core `Swapchain`/present-path design. The user accepted that
explicitly. PR C's contract still states the guideline requirement verbatim
(plan step 20 §5).

D1 survives because the requirement does not favour Streamline. Streamline
would supply sl.reflex and sl.pcl ready-made, but it would still cost E4's core
rewrite. Reflex has to exist in this repo on either path, because Streamline
requires that *its* Reflex be the one used (`ProgrammingGuideDLSS_G.md:684`).

### F2. The direct NGX FG path's future

**No deprecation signal was found, and the evidence shows active maintenance.**
The evidence does not show NVIDIA's *intent* (OPEN-7).

- **The path is new, not legacy.** The dlssg headers, the FG guide and
  `nvngx_dlssg.dll` are absent from the NVIDIA/DLSS trees at v310.1.0, v310.2.1
  and v310.3.0. They first appear at **v310.4.0** (2025-08-26). This was
  measured by listing each tag's tree with
  `gh api repos/NVIDIA/DLSS/git/trees/<tag>?recursive=1`.
- **It is updated at every tag since.**
  - `nvngx_dlssg.dll` has a new blob at all seven tags from v310.4.0 to
    v310.9.16.
  - `nvsdk_ngx_defs_dlssg.h` changed at v310.5.0, v310.6.0, v310.7.0, v310.9.1
    and v310.9.16.
  - v310.9.1 added `nvsdk_ngx_helpers_dlssg_d3d.h` and Windows aarch64/arm64ec
    and Linux aarch64 builds of the FG DLL.
  - v310.9.16 (2026-10-16, the latest) added
    `DLSSG.LiteCandidateBlendingEnabled`.
- **It ships new FG features.** v310.6.0's release notes: "Added DLSS Frame
  Generation 5x and 6x Modes; Added DLSS Frame Generation User Interface
  Recomposition support (Preset B)" (`gh api repos/NVIDIA/DLSS/releases`).
  No release note from v310.1.0 to v310.9.16 mentions deprecating the NGX FG
  path.
- **The guide has not changed since v310.7.0.** The FG guide's blob is
  identical at v310.7.0, v310.9.1 and v310.9.16 (`f754b0f`), so the documented
  surface lags the header.
- **There is no public tracker to check.** NVIDIA/DLSS has issues disabled and
  no discussions (`gh issue list` fails with "repository has disabled issues";
  `has_discussions: false`). So "no statement found" is all GitHub can show.
- **Maintenance cost: names move between pins.** Between v310.7.0 and v310.9.16,
  `NVSDK_NGX_Parameter_DLSSGEstimateVRAMCallback`, `…DLSSGInvertXAxis/YAxis`
  and `…DLSSGGetCurrentSettingsCallback` were removed or renamed. That shows up
  as a C# compile error in `NgxParameterNames` on the next `/regen-bindings`,
  which is loud, not silent. A pin bump must be treated as an API review (the
  existing `native/ngx/README.md` rule).

Which FG features reach which path:

| Feature | NGX direct | Streamline |
|---|---|---|
| 2x FG | Yes (FG guide §6) | Yes |
| Fixed MFG 3x-6x (RTX 50) | **Yes.** `multiFrameCount`/`Index` (FG guide l. 800-810), 5x/6x since v310.6.0 | Yes |
| UI Recomposition | Yes. `DLSSG.UserInterfaceRecompositionEnabled` (`nvsdk_ngx_defs_dlssg.h:366-370`) | Yes |
| Dynamic MFG | **Not documented.** The header has `DLSSG.TargetFrameRate` and `DLSSG.FullscreenMode` (`:330-338`), but the FG guide never mentions them (no match for TargetFrameRate, FullscreenMode or vsync). Treat as unavailable | Since SL v2.11.1, **D3D12 only** (`ProgrammingGuideDLSS_G.md:552`) |
| VSync with FG | The app's own present mode (FG guide l. 849-850). See F3 | Since SL v2.14.1 for Dynamic mode (release notes), **D3D12 only** (`ProgrammingGuideDLSS_G.md:1159`) |
| Frame pacing | **The application's job** (l. 849-850, 886-888) | SL's pacer, on its async present path (`:628`, `:908-916`) |
| Reflex / PCL | Not provided; native (F1) | sl.reflex / sl.pcl, mandatory |

Every Streamline-only feature in this table is also D3D12-only on Streamline.
So on **Vulkan** today, choosing Streamline buys automatic pacing and bundled
Reflex/PCL, and no FG feature the NGX path lacks.

### F3. Where "FIFO spaces the generated frames" stops working

Two facts drive the analysis.

- **How FIFO behaves.** In `VK_PRESENT_MODE_FIFO_KHR` the presentation engine
  keeps a queue of pending presents and takes **one per vertical blanking
  period**. An image stays displayed until a newer one is taken (Vulkan spec,
  `VkPresentModeKHR`).
- **What D5's loop does.** It queues M presents per rendered frame, back to
  back, where M = 1 + generated frames. That is 2 for 2x and up to 6 for 6x.

Let R be the refresh rate and B the rate at which the app *could* render if
unthrottled. Then:

| Case | Behaviour | Why |
|---|---|---|
| **Fixed-refresh FIFO, B·M ≥ R** | **Works.** Output = R, rendered rate is clamped to R/M by FIFO's blocking acquire, and the frames are evenly spaced, one vblank each | Queue never drains, one present per vblank |
| **Fixed-refresh FIFO, B·M < R** (e.g. 2x at B = 40 on 120 Hz) | **Breaks.** The M frames show on M consecutive vblanks, then the last one is held until the next batch arrives. At 2x with B = 40: generated 1 vblank, real 2 vblanks, a 1:2 cadence, which is judder | FIFO paces against the vblank, not against the rendered-frame interval the FG guide asks for (l. 887-888) |
| **MFG M = 3-6** | The same rule, made stricter: it needs B ≥ R/M **and** it pins the base rate at R/M. At 4x on 120 Hz that is 30 fps base, with latency to match. The CPU also blocks in acquire for M−1 vblanks per rendered frame | The FIFO queue depth and the acquire throttle do the pacing. MFG is untestable on the 4070 Ti (E12) |
| **VRR (G-Sync / adaptive sync)** | **Breaks (unverified, OPEN-5).** With VRR the display refreshes when a present arrives, within the panel's range. Back-to-back presents then display at submission spacing (the generated frame for about one minimum refresh interval), not at half the frame interval | Vulkan's FIFO definition assumes a fixed vblank cadence. VRR behaviour is a driver/OS property no source here states, so measure it with PresentMon on a VRR panel |
| **MAILBOX / FIFO_LATEST_READY** | **Breaks.** A newer pending image *replaces* the older one, so the generated frame is usually discarded before display | Spec definition of both modes |
| **IMMEDIATE / FIFO_RELAXED (late)** | **Breaks.** No wait for vblank, so tearing, and the generated frame's visibility depends on timing. This is D5's negative control | Spec definition |

**What the app needs outside the first row is a real pacer.** That means
presenting the real frame at the midpoint of the measured rendered-frame
interval, as the FG guide asks (l. 887-888). Ways to get there with what is
already bound:

| Mechanism | Bound? | What it gives | Status on 4070 Ti / Windows |
|---|---|---|---|
| Present thread + CPU clock | n/a | Timed second present. Needs `vkQueuePresentKHR` external synchronization against the render thread's submits on the same queue | Always possible |
| `VK_EXT_present_timing` | **Yes.** `vkSetSwapchainPresentTimingQueueSizeEXT`, `vkGetPastPresentationTimingEXT` (`Vk.cs:2736-2745`), and `VkPresentTimingInfoEXT.targetTime` (`Generated/VkPresentTimingInfoEXT.cs:14`) | A *scheduled* present at a target time. This is the right primitive | **Unknown.** Probe it (plan step 2) |
| `VK_GOOGLE_display_timing` | **Yes.** `vkGetRefreshCycleDurationGOOGLE`, `vkGetPastPresentationTimingGOOGLE` (`Vk.cs:2507-2510`), `VkPresentTimesInfoGOOGLE` | `desiredPresentTime` per present, plus feedback | **Unknown.** Probe it |
| `VK_KHR_present_id` + `VK_KHR_present_wait` | **Yes.** `vkWaitForPresentKHR` (`Vk.cs:2008`), `VkPresentIdKHR` | *Feedback only* ("this present was displayed"). Lets a pacer thread measure, but does not schedule | Probe it |
| `VK_NV_low_latency2` | **Yes** (E5) | Latency control. Not a pacer, but Streamline couples its pacer to Reflex markers | Probe it |

**The API has room for any of these without breaking anything.**

- `DlssFrameGenerationFeature.Evaluate` knows nothing about presentation (D5),
  so a pacer changes the consumer's loop, not the FG type.
- `Swapchain.Present` builds a `VkPresentInfoKHR` with no `pNext`
  (`Swapchain.cs:699-708`). Present-id, present-timing and latency chaining all
  need a `pNext`. That is an **additive** overload, e.g.
  `Present(Queue, uint, in BinarySemaphore, in PresentTiming)`, on the same
  `MapPresentationResult` path. The existing overloads are untouched.
- `Queue` has no lock, so a pacer thread presenting while the render thread
  submits is the caller's synchronization problem. Document it then; nothing
  in #253 forecloses it.
- The only thing #253 would have to *undo* is the sample's FIFO-only loop, and
  that is a sample.

### F4. The cost of switching to Streamline later

**Carries over:**
- the measured input conventions: motion vectors, depth, the UNORM colour chain,
  HUD-less/UI premultiply, and camera matrices. Streamline's `sl::Constants`
  takes the same five matrices, unjittered and row-major, with
  `clipToPrevClip = clipToView * viewToViewPrev * viewToClipPrev`
  (`ProgrammingGuide.md:1156-1171`). That is the row-vector composition the
  plan's sample already uses;
- `HelloFrameGen`'s scene and targets, minus its present loop;
- the BYO-DLL and licence documentation;
- the measured-contract method;
- the follow-up Reflex work, as long as it is designed as "Reflex markers and
  sleep" and not tied to NGX.

**Thrown away:**
- `DlssFrameGenerationFeature`/`Inputs`/`EvaluateCore` and its benchmark.
  Streamline takes resources via `slSetTagForFrame` and constants via
  `slSetConstants`, not NGX parameter maps;
- `CreateFrameGeneration` and the FG half of `NgxContext`. Streamline drives
  NGX for FG itself. Whether our SR `NgxContext` can coexist with Streamline's
  NGX on one device is unknown;
- the D5 two-present loop. Streamline presents;
- the PR A pin and rsp additions, which are small and harmless to leave.

**Newly paid:** everything in E4 (the core dispatch seam, a second pin, a C++
binding path, sl.reflex in place of any native Reflex), and #253 does not reduce
any of it. In round numbers: PR B and the sample loop are lost (one package
type plus a benchmark plus about one sample file), PR A and the documentation
mostly survive, and the dominant cost is the same as choosing Streamline today.
Two discrepancies Streamline's docs expose are worth carrying into PR C's
MEASURE steps as priors, not answers:
- `sl::Constants::jitterOffset` is "pixel space", where the NGX header says
  "clip space";
- `mvecScale` "normalize[s] motion vectors (so the values are in [-1,1] range)",
  which agrees with the NGX header and not the FG guide
  (`ProgrammingGuide.md:1174-1177`; spec E7).

### F5. Latency of the FIFO two-present loop (added 2026-11-07, from PR C measurements)

**What was measured.** RTX 4070 Ti, driver 616.92, LL2 revision 2, 60 Hz,
windowed and borderless (`docs/ngx-frame-generation-notes.md` §5, 19i).
`GetLatencyTimings` sim → GPU end:
- `--mode fg --present fifo --reflex on` (and Boost, borderless, and 2, 3 or
  4 swapchain images): **about 99 ms, of which about 66 ms is OS render-queue
  wait**;
- `--mode off`: about 1.3 ms;
- FG + IMMEDIATE: 1.7 → 5.9 ms and rising.

The CPU blocks in the **second acquire**, not in `LatencySleep`. The timings
report the rendered 30 fps, and the driver writes two PCL 11/12 pairs per
rendered frame, so #261 OPEN-3 and OPEN-8 pass. The driver's real OS presents
land about 70 and 87 ms after the application's `PresentEnd` (the 19i ETW
capture).

**What is inherent.** Interpolation holds the real frame back "by the time
taken to generate and present frame 1.5" (FG guide l. 114-118). At 60 Hz with
2x that is about one refresh, 16.7 ms. Under FIFO a generated frame shown one
vblank after the previous real one adds at most about one more refresh. **About
66 ms of queue wait is roughly three refreshes more than either.** It is the
classic VSync back-pressure Reflex exists to move into the sleep: "you should
never hit a stall in Present, since you are effectively moving that delay to
this Sleep call" (Reflex l. 522-523). With one present per frame, the same
loop and driver do move it (`--mode off`: 0.5 ms queue wait).

**Root-cause hypothesis (not yet proven; OPEN-9).** The driver's sleep paces
against the swapchain's *present* cadence, while the FG loop issues two
presents per sleep. The evidence:
1. The spec defines the sleep interval per present: `minimumIntervalUs` "is
   the microseconds between `vkQueuePresentKHR` calls … that `vkLatencySleepNV`
   will enforce" (wsi l. 9175-9177, 9181). Nothing in the LL2 text says how the
   low-latency logic weights two presents attributed to one frame (wsi
   l. 9124-9132, 9578-9597 define attribution, not pacing).
2. The one open-source LL2 integration does not rely on the driver to find the
   VSync interval. With low latency On, DXVK raises `minimumIntervalUs` to the
   display's frame interval itself (`doitsujin/dxvk` @ `829e377d`,
   `src/dxvk/dxvk_presenter.cpp:152-167`).
3. NVIDIA does not ship this configuration. Streamline: "**Vulkan**: VSync
   with Frame Generation is only supported on D3D12"
   (`ProgrammingGuideDLSS_G.md:1159` at v2.14.16). VSync with FG "relies on
   Independent Flip … If VSync is enabled with Frame Generation on a system
   that does not support IFLIP, high latency is expected" (`:1163-1176`). On
   Vulkan, SL presents asynchronously through its own pacer
   (`:628`, `:813-831`), not through FIFO. For a fixed rate it points to
   `frameLimitUs`, the field that becomes `minimumIntervalUs`
   (`:769`; `sl.chi/vknvll2.cpp:210-225`).
4. The swapchain image count does not matter, so the backlog lives in the
   driver's or OS's present queue, not in `VkSwapchainKHR` images. That
   matches the driver's deferred present thread seen in the ETW capture.

**Consequence for D5, narrowly.** D5's present ownership and marker sequence
stand, and so does F3 row 1's *spacing* claim. What D5 did not state is that
FIFO spacing alone does not bound latency. Under FG + FIFO the application must
also tell the sleep the rendered interval:
`LatencySleepMode.MinimumIntervalUs = M × refresh period`, plus a small margin
so a standing queue can drain. The value is set through #261's existing
`SetLatencySleepMode`, so **no `Ahjo.Vulkan` API change**. This is adopted only
if OPEN-9's measurements confirm it.

If they do not, the remedy is a real pacer (F3's `VK_EXT_present_timing` row,
or present-wait gating, which needs a `Swapchain.WaitForPresent` the wrapper
does not have, #261 D4). That is a design for a new issue, not a PR C change.

**Why not the other levers.**
- **Out-of-band queues.** The loop has one queue, and marking one changes
  frame delimiting device-wide (#261 E5).
- **Fewer swapchain images.** Already measured to make no difference.
- **MAILBOX.** It drops the generated frame (F3).

## OPEN

- **OPEN-1 (blocking, measured first).** On the RTX 4070 Ti, the public
  `rel/nvngx_dlssg.dll` at v310.9.1 under `Init_with_ProjectID` +
  `ENGINE_TYPE_CUSTOM` has to show three things: `FrameGeneration.Available = 1`,
  `CreateFeature1` succeeding, and no watermark. The plan's step 1 measures this
  with a throwaway probe before any API work. **If any of the three fails, stop
  and report.** D1 is then reopened by a human with the measurement, because the
  remaining options (Streamline, or an NVIDIA application ID) are product
  decisions, not implementation choices.
- **OPEN-2 (human call if it fires).** If
  `NgxSupport.TryGetDeviceExtensions(FrameGeneration)` returns
  `VK_NV_optical_flow` and FG creation fails, the probe first retries with one
  extra `QueueRequest` on the first family that carries
  `VK_QUEUE_OPTICAL_FLOW_BIT_NV`. `DeviceDescription.Queues` already takes
  arbitrary families, and `QueueFamilyInfo.Flags` exposes the bit
  (`src/Ahjo.Vulkan/Lifecycle/QueueFamilyInfo.cs:19`). If that retry succeeds,
  OPEN-2 is answered with evidence and needs no core change, only a documented
  queue request in the init order. Stop and ask only if the retry also
  fails. The FG guide says nothing on this
  for the NGX path. Only Streamline's guide does, for its own interop mode (E4).
- **OPEN-3 (resolved at approval, 2026-11-06).** A separate `Ahjo.Vulkan` issue
  is filed now for native Reflex: `VK_NV_low_latency2` sleep and markers **plus
  PCL Stats** (ETW). That is **#261**. It **gates #253 PR C** and
  pekkah/ahjo#2783. Its API is designed in `2026-11-06-issue-261-reflex-low-latency-design.md`, not here, and was approved on
  2026-11-06. Plan steps 17b and 19i consume it.
- **OPEN-4.** Whether shipping `rel/nvngx_dlssg.dll` in a commercial Ahjo title
  needs anything from NVIDIA beyond the licence's notify-before-release
  clause. The dev watermark's "CONTACT NVIDIA TO OBTAIN DLLs FOR YOUR TITLE"
  (E9) leaves this ambiguous. It is a product/legal call. The consumer contract
  will quote the evidence and not settle it.
- **OPEN-5 (new, F3).** FIFO pacing under VRR (G-Sync / adaptive sync) is not
  stated by any source found. Measure it with PresentMon if a VRR panel is
  available (plan step 19d). Otherwise the contract says "unmeasured on VRR".
  **2026-11-07: the user reports the measurement panel has no G-Sync/VRR. Record
  "VRR not measured"; OPEN-5 stays open for a future VRR host.**
- **OPEN-6 (new, F1).** Does NVIDIA's Reflex verification (ReflexTest.exe, the
  Reflex QA checklist) accept a native `VK_NV_low_latency2` integration, or
  does it require `NvLowLatencyVk.dll` (the Reflex SDK's documented Vulkan
  path)? It is answerable by running the Reflex Test Utility against the
  OPEN-3 implementation, or by asking NVIDIA. It belongs to #261, where it is
  #261 OPEN-2. #261 runs the Reflex On and On + Boost passes; the FG-on pass
  is plan step 19i. **2026-11-07: deferred by the user.** PR C ships "Refs #253";
  the follow-up PR (plan step 22) runs the utility with FG on, records the
  result verbatim, and closes #253.
- **OPEN-9 (new, F5; measured in PR C, plan step 19j).** Is the about 66 ms
  queue wait under FG + FIFO + Reflex On a pacing failure that
  `MinimumIntervalUs = M × refresh period` fixes, or FIFO's inherent cost with
  Reflex making no difference? Decide it from a no-Reflex diagnostic run and
  the interval runs. **If no interval brings the queue wait down to about one
  refresh or less, stop and report.** The fix is then a pacer, a new design,
  and PR C ships with the measured numbers and that statement.
  **Measured 2026-11-09 (two rounds, `docs/ngx-frame-generation-notes.md` §5,
  19j):** neither branch of the rule. Without an interval the sleep does not
  hold the CPU and the loop is frame-fence bound with the queue full (the F5
  hypothesis of a per-present sleep blocking in acquire 1 is not what
  happens). An interval of one rendered frame keeps 60 displayed fps but only
  holds the queue at its startup depth (17-28 ms borderless, 45-83 ms
  windowed); frame + 500 µs drains it (11-17 ms) but repeats a frame about
  every 1.3 s. **Resolved by the user:** the sample defaults to one rendered
  frame under FG + FIFO, that interval is the application rule for FG over
  FIFO, and a queue-depth pacer is follow-up work outside #253 (#266).
- **OPEN-7 (new, F2).** NVIDIA has published nothing either way on the NGX FG
  path's long-term status. Tag history shows active maintenance since v310.4.0,
  and the only public channel (the repo tracker) is disabled. Re-check at
  every NGX pin bump.
