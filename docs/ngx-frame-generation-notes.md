# DLSS Frame Generation with Ahjo.Vulkan — the consumer contract

This is the Frame Generation counterpart of [ngx-notes.md](ngx-notes.md). Like
that page, everything on it is either cited from NVIDIA's pinned documentation
(`native/ngx/doc/DLSS-FG_Programming_Guide.pdf`, "FG guide l. N";
`native/ngx/doc/RTX_UI_Developer_Guidelines.pdf`, "UI l. N"; both
`pdftotext -layout` line numbers, `NgxVersion` `v310.9.1`) or **measured on
hardware**, with the driver version.

**Measurement host:** RTX 4070 Ti, driver **616.92**, NGX `v310.9.1`,
Windows 11, one 2560×1440 panel at 60 Hz (WMI reports 59 Hz), Vulkan SDK
1.4.363 validation layer. All measurements 2026-11-06, `samples/HelloFrameGen`
Release build unless stated otherwise. The design record is issue #253's spec
(`docs/design/specs/2026-11-06-issue-253-dlss-frame-generation-design.md`);
Reflex is issue #261's ([reflex-notes.md](reflex-notes.md)).

---

## 1. What you get, and what you must supply

DLSS Frame Generation runs inside the NVIDIA display driver, reached through a
second feature library: `nvngx_dlssg.dll` on Windows,
`libnvidia-ngx-dlssg.so.<version>` on Linux. Like `nvngx_dlss.dll`, it is
NVIDIA's to license and **yours to ship**. No Ahjo package ever contains it, and
this repository never commits or packs it (decision recorded in
[#214](https://github.com/pekkah/Ahjo-Vulkan/issues/214)). Take it from
[NVIDIA/DLSS](https://github.com/NVIDIA/DLSS) at the pinned tag, from
`lib/<plat>/rel/` **only**. The `dev/` build draws an on-screen
"DLSSG SDK - DO NOT DISTRIBUTE" watermark and must never ship (§2). Place it
beside your executable, or point `NgxDescription.DlssSearchPaths` at its
directory. NGX searches that one list for every feature DLL.

*Measured:* when both are present, a `DlssSearchPaths` directory wins over a
copy beside the executable. `HelloFrameGen` run from its build directory, with
`dev/nvngx_dlssg.dll` copied beside it and `DlssSearchPaths` pointing at the
staged `rel/`, ran the **rel** build (no watermark). Only a copy of the build
directory with no search path loaded the dev build. That fits how the wrapper
calls NGX: it passes **only** the `DlssSearchPaths` entries as
`FeatureSearchPaths` (`NgxDescription.cs:196`), so the executable's directory
is NGX's own fallback. The diagnostic message below lists the directories in
that measured order: the `DlssSearchPaths` entries first, the application
directory last.

`NgxContext.Create` throws for a missing `nvngx_dlssg.dll` only when
`NgxDescription.RequiredFeatures` includes `NgxFeatures.FrameGeneration`.
Otherwise the context is created, `IsFrameGenerationAvailable` is `false`, and
the Frame Generation create call throws the same exception later. The message,
verbatim (Windows; each `DlssSearchPaths` entry, then
`AppContext.BaseDirectory`):

```
DLSS Frame Generation is unavailable: the NVIDIA feature library was not found.
Expected file: nvngx_dlssg.dll
Searched:
  <each DlssSearchPaths entry>
  <AppContext.BaseDirectory>
This library is NOT shipped by Ahjo.Vulkan.Ngx — the application supplies it from NVIDIA's DLSS SDK (https://github.com/NVIDIA/DLSS, lib/<plat>/rel/). See docs/ngx-frame-generation-notes.md.
```

The exception is `NgxFeatureLibraryNotFoundException`, with
`Feature == NgxFeature.FrameGeneration`, `ExpectedFile` and
`SearchedDirectories` carrying the same facts as the message.

*Measured (#253 PR A):* a missing `nvngx_dlssg.dll` shows up on the same key as
Super Resolution's: `FrameGeneration.Available = 0` with
`FrameGeneration.FeatureInitResult = FAIL_FeatureNotFound` (`0xBAD00004`). In
the same state `NgxSupport.TryGetRequirements(NgxFeature.FrameGeneration, …)`
returns `false` (NGX answers `FAIL_NotImplemented`), not "unsupported".

### The API

All in `Ahjo.Vulkan.Ngx` (#253 PR B):

- **`NgxContext.CreateFrameGeneration(ref CommandRecorder, in DlssFrameGenerationDescription)`**
  creates the feature and records its initialization work. Submit the recorder
  and **wait for it** before the first evaluate. If Frame Generation is
  unavailable on the context, it throws the diagnosed exception above.
  Recreate the feature on any resize (FG guide l. 657-668).
- **`DlssFrameGenerationDescription`** holds `Width`/`Height` (the backbuffer and
  output extent), `BackbufferFormat`, `RenderWidth`/`RenderHeight` (the depth and
  motion-vector extent, written to `DLSSG.InternalWidth/Height`) and
  `DynamicResolution`. An `_SRGB` `BackbufferFormat` throws `ArgumentException`
  (§4).
- **`DlssFrameGenerationFeature.Evaluate(ref CommandRecorder, in DlssFrameGenerationInputs)`**
  records one evaluation. It allocates nothing per frame
  (`DlssFrameGenerationEvaluateBenchmarks`). It is a recorded command, not a
  present: presenting `OutputInterpolated` and `OutputReal` is the
  application's job (§5).
- **`DlssFrameGenerationInputs`** takes the required `Backbuffer`, `Depth`,
  `MotionVectors` and `OutputInterpolated`, and the optional `Hudless`,
  `UiColorAndAlpha` and `OutputReal`. It also takes the `Camera` block
  (`DlssFrameGenerationCamera`: five unjittered `Matrix4x4`s with
  `ClipToLensClip` defaulting to identity, plus position, basis, near/far,
  FOV in radians, aspect and pinhole offset), and the per-frame scalars:
  clip-space jitter, motion-vector scale, `Reset`, the depth/HDR/camera-motion
  flags, `MultiFrameCount`/`MultiFrameIndex` (both default 1) and
  `BackbufferFrameId`.

Under `AhjoValidation.Enabled`, `Evaluate` runs these checks: the required
slots are set; every slot's extent and format are known; `Backbuffer` matches
the feature's extent **and format**; without `DynamicResolution`, `Depth` and
`MotionVectors` match the render extent ("When dynamic resolution scaling is
not enabled, your depth and motion vector resources must match the Render Width
and Height specified during feature creation", FG guide l. 3931-3932); both
outputs match `Backbuffer` in extent and format; `Storage` on outputs and
`Sampled` on inputs (`ImageUsage.None` skipped); and
`1 ≤ MultiFrameIndex ≤ MultiFrameCount ≤ MaxGeneratedFrames`. It also issues
one **non-failing** advisory (§6).

---

## 2. Licence

The obligations of [ngx-notes.md §3](ngx-notes.md#3-licence-obligations-that-land-on-your-application)
apply unchanged: notify NVIDIA before a commercial release, follow the RTX UI
Developer Guidelines, reproduce the third-party notices, never redistribute
`dev/`. The NGX licence grants distribution of "any software and materials
within the SDK … as incorporated in object code format into a software
application" (`native/ngx/NGX-LICENSE.txt:12`); `nvngx_dlssg.dll` is SDK
material the same way `nvngx_dlss.dll` is.

Frame Generation adds two pieces of evidence:

- **The dev watermark.** The dev build draws
  "DLSSG SDK - DO NOT DISTRIBUTE - CONTACT NVIDIA TO OBTAIN DLLs FOR YOUR TITLE",
  which "is rendered by any DLSS-FG dynamic library which is not intended for
  production" (FG guide l. 1046, 1054-1055).
  *Measured:* the **`rel/`** `nvngx_dlssg.dll` at `v310.9.1`, initialized with
  `Init_with_ProjectID` + `NVSDK_NGX_ENGINE_TYPE_CUSTOM` (how `NgxDescription`
  initializes), draws **no watermark**: neither `OutputInterpolated` nor
  `OutputReal` captures from `HelloFrameGen` carry any text. The `dev/` build,
  same sample, draws the line above at the top-left of both outputs.
- **`NVSDK_NGX_Result_FAIL_FeatureDenied`**: "NVIDIA has restricted the use of
  this feature in the current application" (FG guide l. 3547). *Measured:* not
  returned; `CreateFeature1` succeeds with the `rel/` build (#253 PR A).

**OPEN-4, stated and not settled.** Whether shipping `rel/nvngx_dlssg.dll` in a
commercial title needs anything from NVIDIA beyond the licence's
notify-before-release clause. The dev watermark's "CONTACT NVIDIA TO OBTAIN DLLs
FOR YOUR TITLE" leaves this ambiguous against a `rel/` build that runs
unwatermarked under a project id. That is a product and legal call; this page
quotes the evidence and does not decide it.

---

## 3. Init order

The order is `NgxSupport`'s contract, the same as Super Resolution's
([ngx-notes.md §4.7](ngx-notes.md#47-extension-lists--both-and-they-fail-differently)):
instance extensions → instance → physical device → device extensions →
device → `NgxContext.Create`. Query **both** features and create with the
**union**:

```csharp
NgxSupport.TryGetInstanceExtensions(NgxFeature.FrameGeneration, in desc, out var fgInst);
NgxSupport.TryGetInstanceExtensions(NgxFeature.SuperResolution, in desc, out var srInst);
// instance with SDL's + fgInst.Names ∪ srInst.Names; dispose each set after create
NgxSupport.TryGetDeviceExtensions(NgxFeature.FrameGeneration, gpu, in desc, out var fgDev);
NgxSupport.TryGetDeviceExtensions(NgxFeature.SuperResolution, gpu, in desc, out var srDev);
// device with KHR_swapchain + fgDev.Names ∪ srDev.Names (+ the Reflex pair, §5)
var ngx = NgxContext.Create(device, new NgxDescription { …, RequiredFeatures = NgxFeatures.FrameGeneration });
```

`RequiredFeatures` decides what `Create` **throws** for; it does not change
what it probes. A context created with the default `SuperResolution` still
reports `IsFrameGenerationAvailable` and `MaxGeneratedFrames`, and its
`CreateFrameGeneration` throws the diagnosed exception if FG is missing. An
application that uses Frame Generation without Super Resolution, like
`HelloFrameGen`, passes `RequiredFeatures = NgxFeatures.FrameGeneration` so a
missing `nvngx_dlss.dll` does not fail it.

Measured on an RTX 4070 Ti, driver 616.92, NGX v310.9.1, with `rel/` staged (#253 PR A):

| Query | `NgxFeature.SuperResolution` | `NgxFeature.FrameGeneration` |
|---|---|---|
| Instance extensions | `VK_KHR_get_physical_device_properties2` | `VK_KHR_get_physical_device_properties2` |
| Device extensions | `VK_NVX_binary_import`, `VK_NVX_image_view_handle`, `VK_KHR_buffer_device_address`, `VK_KHR_push_descriptor` | `VK_NVX_binary_import`, `VK_NVX_image_view_handle`, `VK_KHR_buffer_device_address`, `VK_KHR_push_descriptor` |

The two features asked for identical lists on this host. Create with the union
anyway: the lists are NGX's answer for one GPU and one driver, not a contract.
NGX did **not** ask for `VK_NV_optical_flow`, and Frame Generation created
successfully on a device with a single graphics queue and no optical-flow queue
(OPEN-2 did not fire). `FrameGeneration.Available = 1`,
`DLSSG.MultiFrameCountMax = 1`, so `MaxGeneratedFrames = 1`: **2x only** on
RTX 40-series.

---

## 4. The inputs contract

`HelloFrameGen` is the worked call site for every row. It renders at native
resolution with no Super Resolution and no jitter, under an **orbiting camera**
around a static cube: the camera must move for the matrix oracle below to
discriminate anything.

| Input | Contract | Source |
|---|---|---|
| `MotionVectors` | Previous minus current, in **pixels of the motion-vector resource**; the same convention as Super Resolution ([ngx-notes.md §4.2](ngx-notes.md#42-motion-vectors)). "If pMVecs is 1920x1080, and an object moves from the top-right corner of the screen to the center, the motion vector at the center of the screen would have a value of (960, -540)" | FG guide l. 725-731 |
| `MotionVectorScaleX/Y` | **A pixel-unit rescale: leave at `(1, 1)` for pixel vectors.** See the measurement below | Measured, 19b |
| `Depth` | Hardware depth at the render extent. `HelloFrameGen` uses standard depth (near 0, far 1), so `DepthInverted = false` | FG guide l. 715-721 |
| Colour chain | **UNORM only, perceptually (sRGB) encoded in the shader.** `Backbuffer`, `OutputInterpolated` and `OutputReal` share one format, and the outputs need `Storage`, which no `_SRGB` format supports on mainstream desktop drivers ([ngx-notes.md §6](ngx-notes.md#6-output-targets-and-the-swapchain)). `CreateFrameGeneration` throws on an `_SRGB` `BackbufferFormat`. LDR, so `ColorBuffersHdr = false` | #253 spec E7 |
| `Hudless` | "The value of pHudless must be exactly the same as the value of pBackbuffer for any pixel which is not UI" | FG guide l. 742-744 |
| `UiColorAndAlpha` | Premultiplied: `Backbuffer = UI + (1 − α)·Hudless`, 0 for every non-UI pixel, and not RGB10A2 ("Formats such as RGB10A2 are not appropriate") | FG guide l. 746-754 |
| Camera matrices | **Unjittered, System.Numerics row-vector form, passed as-is.** See the measurement below | Measured, 19a |
| `JitterOffsetX/Y` | Clip space (`nvsdk_ngx_defs_dlssg.h:138-140`). **Not exercised**: see below | 19c |
| `Reset` | Set on the first frame and after every resize or camera cut. `HelloFrameGen` also makes the previous-frame matrices equal the current ones on those frames, so the motion vectors are zero | FG guide (Required Input Engine Data) |
| `BackbufferFrameId` | One value per **rendered** frame, incremented even while FG is off (`nvsdk_ngx_defs_dlssg.h:361-364`). `HelloFrameGen` shares it with every Reflex and PCL marker (§5), starting at 1 | #261 D5 |

`HelloFrameGen` satisfies the `Hudless`/`UI` rows by working in **encoded**
values throughout. The cube shader writes sRGB-encoded colour into `Hudless`;
the UI pass writes a premultiplied colour authored in encoded space into a
`R16G16B16A16_SFLOAT` target cleared to 0; and a composite pass computes
`Ui.rgb + (1 − Ui.a)·Hudless.rgb` on the raw texels into `Backbuffer`. The
formula then holds on exactly what Frame Generation reads, and every non-UI
pixel of `Backbuffer` is bit-identical to `Hudless` by construction.

### 19a. Matrix orientation: as-is

The camera matrices are documented as "float[4][4] in row-major order
(assuming post-multiplication)", which does not say on which side the vector
sits. **Measured with the dev DLL's own oracle**, the in-game visualizer's
"Test - Camera ClipToPrevClip Matrix" mode (Ctrl+Shift+V, then F5 twice; FG
guide l. 1170-1218), whose description reads "Only those pixels that move
independently of camera motion should be annotated". The cube is static and the
camera orbits, so a correct matrix annotates no cube pixel.

| Run | What `EvaluateCore` hands NGX | Cube face (830..1000, 330..600 px) | Verdict |
|---|---|---|---|
| A | System.Numerics matrices as-is | mean luma 104.2, **0.00 %** annotated | **Passes:** static faces un-annotated |
| B | `Matrix4x4.Transpose(...)` of each | mean luma 186.0, **72.6 %** annotated; the whole frame washed grey | Fails |

So pass the matrices **as-is**, composed in the row-vector form
[ngx-notes.md §4](ngx-notes.md#4-the-renderer-contract) uses:
`ViewToClip = proj`, `ClipToView = Invert(proj)`,
`ClipToPrevClip = Invert(view * proj) * prevView * prevProj`,
`PrevClipToClip = Invert(prevView * prevProj) * view * proj`. This is what
Streamline documents too (`clipToPrevClip = clipToView * viewToViewPrev *
viewToClipPrev`, row-major; #253 spec F4), and `EvaluateCore` copies them
unchanged with a comment citing this run. One observation, not a failure: in
Run A the far-plane background is annotated outside a circle around the screen
centre; inside it, it is not. `HelloFrameGen` writes the background's camera
motion through the same `ClipToPrevClip` (its background pass), so this is the
visualizer's treatment of far-plane depth, not a matrix error.

### 19b. `MvecScale`: a pixel rescale, not a normalization

The FG guide says motion vectors are in pixels and `mvecScale` rescales vectors
"provided in a different scale" (l. 730-731); the header says it normalizes to
[-1, 1]; Streamline's `mvecScale` "normalize[s] motion vectors" (#253 spec F4).
**Measured** with the visualizer's "Test - Motion Vector Resampling" mode
(Ctrl+Shift+V, F5 once), whose description reads "Annotations should only
highlight those pixels whose object undergoes occlusion or de-occlusion OR is
not geometric":

| Run | `MotionVectorScaleX/Y` over pixel vectors | Result |
|---|---|---|
| A | `(1, 1)` (the guide's reading) | Highlights only the occlusion band at the cube's silhouette and the edges of the screen-static UI panel. Fine-checker face (800..1000, 330..620 px): mean luma 60.2, 0.02 % bright |
| B | `(1/RenderWidth, 1/RenderHeight)` (the header's) | No occlusion band; the moving texture **inside** the faces is highlighted. Same face: mean luma 89.8, 7.9 % bright |

Repeated at 4× the orbit speed (about 78 px/frame of background motion): A's
occlusion band widens to roughly one frame of background motion and the face
interiors stay darkest (front face mean luma 32.6); B (51.7) and a control
scale of 2.0 (63.3) both highlight the face interiors. **Run A wins:** give
pixel vectors and leave the scale at 1. The Streamline/header prior predicted
B; on this path it is wrong.

### 19c. Jitter: 0, not exercised

`HelloFrameGen` renders with no jitter, so `JitterOffsetX/Y = 0` and nothing
here tests their units. The header says clip space
(`nvsdk_ngx_defs_dlssg.h:138-140`); Streamline documents its `jitterOffset` as
"pixel space" (#253 spec F4). The conflict is noted, not resolved. **Super
Resolution + Frame Generation with jitter is not measured**; it is the Ahjo
engine's integration to measure, with the same visualizer modes.

---

## 5. Present ownership

**The wrapper owns no present, no pacer and no thread** (#253 spec D5). The
evaluate writes `OutputInterpolated` and `OutputReal`; the application presents
both, generated first: "Present the generated frame as soon as EvaluateFeature
completes. Present the retained rendered frame at the correct time to achieve
equal time intervals between frames. This typically requires you to present
asynchronously from the main render thread" (FG guide l. 884-888).

### The two-present frame (spec D5, verbatim)

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

`HelloFrameGen` acquires `i0` just before recording `rec1` rather than in the
middle of it. The GPU-side order is the same, because the submit's wait orders
the blit after the acquire, and an acquire failure then abandons a frame with
nothing recorded. `rec1` carries no fence; `rec2`'s `fc.Submit` arms the slot
fence, and a fence signal covers every earlier submission on the queue. So a
frame abandoned **after** `rec1` (the second acquire fails) leaves an unfenced
submit, and the resize path drains with `device.WaitIdle`, not only
`ring.WaitForInFlightFences`. Each per-slot extra acquire semaphore that was
signaled but not consumed is replaced (`SemaphorePool.Discard` +
`AcquireBinary`) after a recreate, alongside `ring.RecycleStaleAcquireSemaphores()`.

### Why the wrapper does not pace, and what FIFO gives you

The reference pacing is `VK_PRESENT_MODE_FIFO_KHR`: the presentation engine
takes one queued image per vblank, so two back-to-back presents come out one
refresh apart, and FIFO's blocking acquire holds the rendered rate at
≤ refresh/2. The guide's asynchronous midpoint present is the **engine's** job,
on the engine's own present thread; `Queue` has no lock, so a present thread
must synchronize with the render thread's submits itself.

*Measured, host side (`HelloFrameGen` prints these at exit; 1920×1080 windowed,
60 Hz, 300 rendered frames each).* A host timestamp marks when the
`vkQueuePresentKHR` call **returned**, not when the image was displayed; the
display-side numbers need PresentMon (below).

| Run | Rendered rate | Presents/s | Presents per rendered frame | Host interval generated → real | Host interval real → next generated |
|---|---|---|---|---|---|
| `--present fifo --mode fg` | 30.03 fps | 60.06 | 2.000 | mean 0.052 ms | mean 33.28 ms, p99 49.9 ms |
| `--present fifo --mode off` | 60.18 fps | 60.18 | 1.000 | (one present) mean 16.67 ms, p99 16.88 ms | |
| `--present immediate --mode fg` | 60.09 fps | 120.17 | 2.000 | mean 0.084 ms | mean 16.59 ms, p99 17.06 ms |
| `--present fifo --mode fg --throttle-ms 40` | 24.41 fps | 48.83 | 2.000 | mean 0.304 ms | mean 40.58 ms, p99 40.89 ms |

So both presents return back to back; under FIFO the queue, not the call
spacing, decides when each is shown. `--throttle-ms 40` puts the base rate at
24.4 fps ≈ 0.4 × refresh, spec F3's "B·M < R" row. Windowed `IMMEDIATE` on
this host renders at 60 fps with 120 presents/s on a 60 Hz panel: the
compositor shows at most one per refresh, so about half the presents are never
displayed, which is the negative control's expected shape.

**Display-side pacing (PresentMon `MsBetweenDisplayChange`): not yet measured.**
PresentMon v2.6.0 could not be run from the measurement session; the table is
filled from the commands in the #253 PR C description. VRR: not measured (the
panel's VRR state is unknown; OPEN-5).

### Where FIFO pacing stops working (spec F3)

| Case | Behaviour | Measured here |
|---|---|---|
| Fixed-refresh FIFO, B·M ≥ R | Output = R, rendered rate clamped to R/M, one vblank each | Rendered 30.0 fps, 60.0 presents/s at 60 Hz. Display spacing: pending PresentMon |
| Fixed-refresh FIFO, B·M < R (2x at 24 fps base on 60 Hz) | Generated 1 vblank, real 2 vblanks: a 1:2 cadence, judder | Base 24.4 fps via `--throttle-ms 40`; cadence pending PresentMon |
| MFG M = 3-6 | Needs B ≥ R/M and pins the base rate at R/M | Untestable: `MaxGeneratedFrames = 1` on the 4070 Ti |
| VRR | Breaks (unverified): back-to-back presents display at submission spacing | Not measured (OPEN-5) |
| MAILBOX / FIFO_LATEST_READY | The generated frame is usually replaced before display | Not run |
| IMMEDIATE / FIFO_RELAXED (late) | Tearing; generated-frame visibility depends on timing | 120 presents/s on a 60 Hz windowed surface (above) |

Outside the first row the application needs a real pacer. What is already bound
for one, and what this driver advertises (#253 PR A probe, driver 616.92):

| Mechanism | Advertised on the 4070 Ti |
|---|---|
| `VK_EXT_present_timing` (scheduled present at a target time) | **yes** |
| `VK_GOOGLE_display_timing` | no |
| `VK_KHR_present_id` + `VK_KHR_present_wait` (feedback only) | yes, yes |
| `VK_NV_low_latency2` (latency control, not a pacer) | yes |

### Reflex

- **The NGX API does not need Reflex to run.** No NGX parameter or API
  references it; the FG guide mentions it once, descriptively.
- **NVIDIA's RTX UI Developer Guidelines require it for a title shipping
  Frame Generation**, verbatim from the pinned PDF:
  - "DLSS Frame Generation uses AI to boost frame rates by generating one
    additional high-quality frame, all while optimizing responsiveness with
    NVIDIA Reflex. DLSS Frame Generation requires a GeForce RTX 40 Series GPU or
    higher and a Reflex SDK integration." (UI l. 87-89)
  - "DLSS Frame Generation requires NVIDIA Reflex Low Latency integration and
    enablement. Users should not be able to disable NVIDIA Reflex Low Latency
    when Frame Generation is ON." (UI l. 149-150)
  - "Reflex is required for DLSS Frame Generation, and will automatically turn on
    when DLSS Frame Generation is enabled." (UI l. 295)
- **PCL markers must be on for all vendors:** "Please make sure PCL Markers are
  always enabled on all vendor GPUs, whether Reflex is toggled on or off"
  (UI l. 142-143).
- **The native `VK_NV_low_latency2` status** (#253 spec F1): NVIDIA's own
  Streamline calls it "the modern Reflex path" and `sl.reflex` uses it; the
  Reflex SDK's documented Vulkan integration is a different library,
  `NvLowLatencyVk.dll`. Whether NVIDIA's verification accepts the native path is
  **OPEN-6** (= #261 OPEN-2), answered by the Reflex Test Utility below.

`Ahjo.Vulkan` provides Reflex on `Swapchain` and PCL Stats as `PclStats`
(issue #261; [reflex-notes.md](reflex-notes.md) is the consumer contract).
`HelloFrameGen` wires it like this:

- `VulkanExtensions.NvLowLatency2` and `VulkanExtensions.KhrPresentId` in
  `DeviceDescription.Extensions` when the GPU advertises LL2, beside the NGX
  unions; no `presentId` feature push (no KHR present ids are passed).
- `SwapchainDescription.LowLatency = device.IsNvLowLatency2Enabled`, the same
  value on every `Recreate`.
- `--reflex off|on|boost`, default `on`, mapped once to
  `SetLatencySleepMode(LatencySleepMode.Off/On/OnPlusBoost)`. `--mode fg
  --reflex off` is a command-line error (exit 2) citing UI l. 149-150;
  `--mode off --reflex off` is the latency baseline. Without LL2 the sample
  prints "Reflex: unavailable on this GPU — PCL markers only" and never calls
  `SetLatencySleepMode`.
- One `ulong` frame id per rendered frame, starting at 1, shared by every
  marker and `BackbufferFrameId`; never reused, not even for an abandoned
  frame.
- The #261 D5 marker sequence: `LatencySleep` once per **rendered** frame,
  `SimulationStart`/`End`, `RenderSubmitStart`, then `PresentStart`, an
  `OutOfBandPresentStart`/`End` pair with id N around **each** of the two
  presents, `PresentEnd`, `RenderSubmitEnd`. `--mode off` has one present and
  no out-of-band pair. On the LL2 swapchain the out-of-band markers go to the
  driver only; the driver writes PCL 11/12 itself
  ([reflex-notes.md §4](reflex-notes.md#4-pc-latency-stats)).
- `PclStats.Initialize` before the loop, `window.EnablePclPingHook()`,
  `PclStats.Shutdown()` in the teardown `finally`; left click sends
  `TriggerFlash`.

#### 19i. Latency (`GetLatencyTimings`, `--latency-report`)

"Sim → GPU end" is the mean of `GpuRenderEndTimeUs − SimulationStartTimeUs` over
the driver's last 64 reports, at frame 480; "OS-queue wait" is
`GpuRenderStartTimeUs − OsRenderQueueStartTimeUs`. 1920×1080 **windowed**,
FIFO, 60 Hz.

| Run | Rendered fps | Timings fps | Sim → GPU end | OS-queue wait |
|---|---|---|---|---|
| `--mode off --reflex off` | 58.8 | 57.7 | 1.66 ms | 0.78 ms |
| `--mode off --reflex on` | 60.1 | 60.0 | 1.29 ms | 0.50 ms |
| `--mode fg --reflex on` | 30.2 | 29.5-30.3 | **99.9 ms** | **66.4 ms** |
| `--mode fg --reflex boost` | 30.0 | 29.5-30.0 | **99.1 ms** | **65.7 ms** |
| `--mode fg --reflex on`, borderless 2560×1440 | 29.7 | 29.8 | 99.3 ms | 65.6 ms |
| `--mode fg --reflex on --present immediate` | 60.1 | 59.0-60.0 | 1.7 → 5.9 ms, rising | 1.2 → 5.5 ms |

**Under FIFO with two presents per frame, about two rendered frames of latency
sit in the OS render queue**, and neither Boost, borderless, nor a swapchain of
2, 3 or 4 images changes it (the image count was varied in a diagnostic build).
With one present per frame (`--mode off`) the same loop sits at about 1 ms.
Step 19j (below) finds the cause and sets the sample's default.

**#261 OPEN-3 check.**

| Condition | Result |
|---|---|
| One timing report per rendered frame id, with populated present stamps | **Pass.** 64/64 reports per read, 0 duplicate ids, 0 out of order, 0 missing `PresentStart`/`PresentEnd`, in every `--mode fg` run |
| Timings FPS = rendered rate, not twice it | **Pass.** 29.5-30.3 fps from the reports, against a rendered rate of 30.0 fps and 60 presents/s |
| PresentMon PCL within 3.0-5.7× average frame time (Reflex l. 1237) | **Above the band except with J5** (step 19j). Against the displayed 16.7 ms (band 50-95 ms): J1 (no interval) 170-176 ms; J4 (the sample's default) 106-110 ms borderless, 86-148 ms windowed; J5 70-75 ms. Against the rendered 33.3 ms (band 100-190 ms) J1 is inside |

The band is stated for "Reflex Low Latency is On or On + Boost **and GPU
bound**" (Reflex l. 1235-1236), with the utility expecting VSync off, and the
guide allows that "in some rare cases, PCL may be slightly higher than 2.7x of
average frame times (5.7x when FG On) for some game engines and/or some setups
such as windowed mode or MSHybrid, even when everything is working properly.
These cases should be analyzed in greater detail to confirm that the exception
can be waived" (Reflex l. 1247-1254). The FIFO runs above are **display-bound
and windowed**, not GPU-bound, so they are not the guide's test condition. There
is also no Frame Generation run with Reflex off to compare against: the sample
refuses `--mode fg --reflex off` (§5), as the guidelines require.

#### 19j. Why FG over FIFO queues two frames, and the default interval

Spec F5 / OPEN-9. 1920×1080 windowed, then borderless, 60 s per run,
`--latency-report 120`, measured **twice** (2026-11-09, RTX 4070 Ti / driver
616.92, 59.951 Hz panel, no VRR); PresentMon 2.6.0 with `--track_pc_latency`
captured 10 s of each run. Every swapchain reported "Hardware Composed:
Independent Flip". Ranges span both rounds; "fence" is the CPU time per frame
blocked in `FrameRing.BeginFrame`.

| # | Arguments (all `--mode fg --present fifo` except J6) | Sim → GPU end | OS-queue wait | CPU wait | PresentMon PCL | Displayed/s |
|---|---|---|---|---|---|---|
| J1 | `--reflex on` | 98-100 ms | 65-67 ms | fence 33 ms, sleep < 1 ms | 170-176 ms | 60.0 |
| J2 | `--reflex off --diagnostic-fg-without-reflex` | 98-101 ms | 65-67 ms | fence 33 ms | 169-174 ms | 60.0 |
| J3 | J2 + `--skip-sleep` | 18-66 ms, varies run to run | 1-50 ms | none | 100-131 ms | 59.4-60.0 |
| J4 | `--reflex on --reflex-interval-us frame` (33361 µs) | borderless 17-28 ms; windowed 45-83 ms | borderless 15-27 ms; windowed 44-66 ms | sleep 17-33 ms | 86-148 ms | 60.0 |
| J5 | `--reflex on --reflex-interval-us 33834` (frame + 500 µs, rounded from 60 Hz) | 11-17 ms | 10-16 ms | sleep 33.5 ms | 70-75 ms | **59.1-59.4**, 7-8 repeated frames per 10 s |
| J6 | `--mode off --present fifo --reflex on --reflex-interval-us frame` | 2.5-41 ms, varies run to run | 0.8-32 ms | sleep 10-17 ms | 89-99 ms | 60.0 |

What it shows:

- **Without an interval the LL2 sleep does not hold the CPU.** It returns in
  under 1 ms, so the loop runs ahead until the frame fence blocks it with the
  present queue full, and the queue stays full: about two rendered frames
  (four presents) of OS-queue wait. Reflex On and Off (J1, J2) are the same.
  The spec's F5 hypothesis (that the sleep paces per present and blocks in
  acquire 1) is **not** what happens: the block is in the frame fence.
- **An interval of one rendered frame holds the queue at whatever depth it
  reached at startup; it does not drain it.** That is why J4 is low-latency
  borderless and not windowed, and why the runs with no interval at all (J3,
  J6) land differently from round to round.
- **Pacing slightly slower than the display drains the queue** (J5), reliably
  in both rounds and both window modes, but FIFO then repeats a frame
  whenever it runs dry: about one per 1.3 s.
- **Draining the queue without repeats needs queue-depth control**, a pacer
  (for example one built on `VK_KHR_present_wait`). That is a new design and is
  follow-up work (#266), not part of #253.

**Decision (user, 2026-11-09):** the plan's rule landed in "any other
outcome" (J5 drains the queue but drops below 60 displayed fps; J4 keeps
60 fps but drains it only sometimes). **`HelloFrameGen` defaults to
`--reflex-interval-us frame` under `--mode fg --present fifo`**: it never
costs a displayed frame and is the low-latency case borderless.
`--reflex-interval-us 0` turns it off; `frame + 500 µs` is the documented
trade for the lowest latency. The application rule for frame generation over
FIFO is therefore: **give the LL2 sleep a minimum interval of one rendered
frame (presents per frame × refresh period)**. No wrapper change; the wrapper
does not pace (see "Why the wrapper does not pace" above).

**#261 OPEN-8 check: pass.** An elevated ETW session on the PCL provider
(`logman … -p {0d216f06-82a6-4d49-bc4f-8f38ae56efab} 0 5 -ets`, then
`tracerpt`; the [reflex-notes.md §7.5](reflex-notes.md#75-frame-generation-sequence-validation-other-vendors-pcl)
method) during `--mode fg --present fifo --reflex on --frames 600`:

- The application's thread wrote markers 0-5 600 times each and
  `PC_LATENCY_PING` (8) 95 times, ahead of `SimulationStart`. It wrote **no**
  11 or 12.
- A second thread of the same process (the driver's) wrote 11 and 12 1200 times
  each: **exactly two 11/12 pairs for every one of the 600 rendered frame ids**,
  all with the application's ids, none with an id the application did not use.
- The driver's two pairs follow the application's `PresentEnd` by roughly
  70 ms and 87 ms (frame 100: `SimulationStart` 24.0727 s, `PresentEnd`
  24.1068 s, driver 11 at 24.1788 s and 24.1955 s): they mark the real OS
  presents, which happen a refresh apart.

**Reflex Test Utility (OPEN-6 = #261 OPEN-2): not yet run.** The utility
(`Reflex_Verification/ReflexTest.exe`, Reflex §4.2) is not installed on the
measurement host. Its result is recorded verbatim, with windowed or borderless
next to it, when it is run; it is not a certification either way.

---

## 6. Layouts and usages

### Found by the validation layer (#253 PR B, hardware test)

*Observed, RTX 4070 Ti / driver 616.92, NGX v310.9.1, `rel/` DLL,
`VK_LAYER_KHRONOS_validation` (Vulkan SDK 1.4.363).* The hardware test
`FrameGeneration_EndToEnd_CreateEvaluateRelease_LayerClean` binds all seven
slots. Each finding was isolated by removing one thing at a time. NVIDIA's
headers and guide state none of them. All are one driver's behaviour, which is
why the wrapper advises rather than enforces. They are the Frame Generation
counterpart of Super Resolution's `TransferDst`-on-`Output` finding.

When `OutputReal` is bound, Frame Generation fills it with a `vkCmdCopyImage`
from the `Backbuffer`. Three requirements follow from that copy:

| Image | Requirement | Without it | With `OutputReal` unbound |
|---|---|---|---|
| `Backbuffer` | `ImageUsage.TransferSrc` | `VUID-vkCmdCopyImage-aspect-06662` (srcImage) | not needed: layer clean without it |
| `OutputReal` | `ImageUsage.TransferDst` | `VUID-vkCmdCopyImage-aspect-06663` (dstImage) | n/a |
| `OutputInterpolated` | `TransferDst` **not** needed | layer clean without it | layer clean |

Neither output showed a Super Resolution-style `vkCmdClearColorImage`. Under
`AhjoValidation.Enabled`, `Evaluate` warns (does not fail) through
`AhjoDiagnostics.Sink` when `OutputReal` is bound and the `Backbuffer`'s known
usage lacks `TransferSrc`.

**Layouts after the evaluate.** Each image's next transition names the
documented exit layout as its `oldLayout`, so the layer checks what NGX left
behind:

- **`Backbuffer`, with `OutputReal` bound: exits in `GENERAL`, not in the
  `SHADER_READ_ONLY_OPTIMAL` it entered in.** NGX transitions it for its copy
  and does not restore it. A barrier assuming `SHADER_READ_ONLY_OPTIMAL` is
  `VUID-VkImageMemoryBarrier2-oldLayout-01197`.
- **`Backbuffer`, with `OutputReal` unbound:** exits in
  `SHADER_READ_ONLY_OPTIMAL`.
- `Depth`, `MotionVectors`, `Hudless` and `UiColorAndAlpha` exit in
  `SHADER_READ_ONLY_OPTIMAL`.
- `OutputInterpolated` and `OutputReal` exit in `GENERAL`.

Unlike Super Resolution (DLSS Programming Guide §3.4), Frame Generation does not
return every image in the layout you set.

### Confirmed in a presenting loop (#253 PR C step 18)

`HelloFrameGen --mode fg --frames 600`, validation on: **0 errors**, and the
only warning is the loader's duplicate-RenderDoc-layer notice, which is the
host's and not the sample's. No further Frame Generation VUID appeared. The
recipe it uses, per slot, all `AllocationFlags.DedicatedMemory`:

| Image | Format | Usage |
|---|---|---|
| `Hudless` | R8G8B8A8_UNORM | ColorAttachment, Sampled, TransferSrc |
| `Ui` | R16G16B16A16_SFLOAT | ColorAttachment, Sampled |
| `Backbuffer` | R8G8B8A8_UNORM | ColorAttachment, Sampled, TransferSrc |
| `Depth` | D32_SFLOAT | DepthStencilAttachment, Sampled |
| `MotionVectors` | R16G16_SFLOAT | ColorAttachment, Sampled |
| `OutputInterpolated`, `OutputReal` | R8G8B8A8_UNORM | Storage, TransferSrc, TransferDst |

Before the evaluate: `Backbuffer`, `Depth`, `MotionVectors`, `Hudless` and
`Ui` in `SHADER_READ_ONLY_OPTIMAL`, both outputs `UNDEFINED → GENERAL`, with
destination scopes of `ComputeShader | AllTransfer` (NGX's copy is transfer
work). After it: both outputs `GENERAL → TRANSFER_SRC_OPTIMAL` from
`ComputeShader | AllTransfer` / `ShaderWrite | TransferWrite` to
`AllTransfer` / `TransferRead`, then blitted to the swapchain image with its own
`UNDEFINED → TRANSFER_DST_OPTIMAL` barrier and an `AllTransfer` acquire wait
([ngx-notes.md §6](ngx-notes.md#6-output-targets-and-the-swapchain)). The next
frame's render-target barriers use `UNDEFINED` as `oldLayout`, which absorbs the
`Backbuffer`'s `GENERAL` exit.

### Still camera (pekkah/ahjo#2783)

`HelloFrameGen --mode fg --freeze --capture-generated g.png --capture-real r.png`
(frame 300, `rel/`): mean absolute difference between the generated and the
following real frame **0.0016 / 255** over RGB, max channel difference 2, 4 133
of 2 073 600 pixels differing at all. For scale, the same pair from a moving
run: 2.27 / 255, max 173. The ([ngx-notes.md §4.1](ngx-notes.md#41-jitter))
technique. A generated frame of a still scene is the real frame.

---

## 7. VRAM

Frame Generation's history and scratch live **inside the driver**, like Super
Resolution's ([ngx-notes.md §5](ngx-notes.md#5-vma-and-vram-accounting)).
*Measured* with a probe that creates and disposes the feature at each
resolution, reading the device-local heap's usage straight from
`vkGetPhysicalDeviceMemoryProperties2` + `VkPhysicalDeviceMemoryBudgetPropertiesEXT`
before, after `CreateFrameGeneration` (submitted and waited) and after
`Dispose`:

| Extent | Driver usage before | After create | Δ | After dispose | FG guide estimate (l. 153-159) |
|---|---|---|---|---|---|
| 1920×1080 | 22 MiB | 238 MiB | **+215 MiB** (225 MB) | 22 MiB | 218 MB |
| 2560×1440 | 22 MiB | 389 MiB | **+366 MiB** (384 MB) | 22 MiB | 384 MB |

Repeated twice in one process with the identical result; disposing releases all
of it. VMA's `GetHeapBudgets` `Usage` read **24 → 24 MiB** across the same
create: the VMA-cache caveat of ngx-notes §5 (VMA refetches the budget only after
30 allocation operations, and `CreateFrameGeneration` performs none). Read the
budget directly, or across a rebuild, to see Frame Generation's allocation.

---

## 8. What the wrapper cannot check — and what can

1. **Pacing.** The wrapper presents nothing (§5). PresentMon's
   `MsBetweenDisplayChange` is the oracle.
2. **Matrix and motion-vector conventions.** Every one is a plausible number the
   wrapper cannot falsify. **The dev DLL's visualizer is the oracle**, the way
   the validation layer is for layouts (ngx-notes §8): Ctrl+Shift+V toggles it,
   F5 cycles "Visualize Application Inputs", "Test - Motion Vector Resampling",
   "Test - Camera ClipToPrevClip Matrix", "Test - Depth/MVec Alignment",
   "Test - UI Hints" and "Test - Motion and Camera Vectors" (as listed by the
   v310.9.1 dev DLL). The overlay is drawn into **both** outputs when
   `OutputReal` is bound, so a `--capture-real` PNG records it. The dev DLL
   must actually be the one loaded (§1).
3. **Layouts.** `VK_LAYER_KHRONOS_validation`, as for Super Resolution.

---

## 9. Running `samples/HelloFrameGen`

```
HelloFrameGen [--mode fg|off] [--present fifo|immediate] [--frames N]
              [--throttle-ms N]
              [--capture-generated <png>] [--capture-real <png>]
              [--freeze] [--require-fg] [--no-validation]
              [--reflex off|on|boost] [--latency-report] [--borderless]
  diagnostics (#253 PR C step 19): [--transpose-matrices] [--mvec-scale pixels|normalized]
```

- `--mode fg` *(default)* presents generated then real; `--mode off` presents
  `Backbuffer` once per frame, the control.
- `--present fifo` *(default)* is the pacing reference; `immediate` is the
  negative control.
- `--throttle-ms N` spin-waits N ms after the scene submit (a `Stopwatch` spin)
  to force the base rate down; 40 gives about 0.4 × 60 Hz.
- `--capture-generated` / `--capture-real` read back `OutputInterpolated` /
  `OutputReal` (in `--mode off`, `--capture-real` reads `Backbuffer`) on the
  last frame; a capture run defaults to `--frames 300`.
- `--freeze` holds the clock: the camera stops and every motion vector is zero.
- `--reflex` defaults to `on`; `--mode fg --reflex off` is refused (§5).
- `--reflex-interval-us N|frame` sets the LL2 sleep's minimum interval: `N`
  microseconds, or `frame` = presents per frame × the display's refresh period.
  It defaults to `frame` under `--mode fg --present fifo` and to 0 otherwise
  (step 19j); `--reflex-interval-us 0` turns the default off.
- `--latency-report N` prints the `GetLatencyTimings` line every N rendered
  frames, with the CPU wait split into sleep, frame fence,
  acquire 0/1 and present 0/1.
- `--diagnostic-fg-without-reflex` (allows `--mode fg --reflex off`) and
  `--skip-sleep` are the step 19j diagnostic controls, not a supported setup.
- `--borderless` makes the window borderless full-screen; the default window is
  1920×1080.
- `--transpose-matrices` and `--mvec-scale normalized` are the losing variants
  of 19a and 19b, kept so the measurement can be repeated.

At exit it prints the rendered rate (the `BackbufferFrameId` delta per second),
presents per second and per rendered frame, and the host present-return
intervals. Exit codes: `0` ran (or fell back to `--mode off`), `2` bad command
line or shader, `3` the validation layer reported an error, `5` `--require-fg`
and Frame Generation was unavailable. Without Frame Generation the sample prints
the wrapper's diagnosis verbatim and runs `--mode off`.

**Feature-DLL resolution order**, first hit wins:

1. `-p:NvidiaDlssgDll=<path>` on the build, copied to the output directory.
2. `nvngx_dlssg.dll` dropped beside `samples/HelloFrameGen/HelloFrameGen.csproj`,
   git-ignored, copied to the output directory.
3. `NgxDescription.DlssSearchPaths` → `native/ngx/staged/<rid>/rel`, populated by
   `./tools/setup-ngx.ps1`. Note that this entry takes precedence over a copy
   beside the executable when both exist (§1).

`nvngx_dlss.dll` is not needed (`RequiredFeatures = FrameGeneration`); the same
two MSBuild items exist for it (`-p:NvidiaDlssDll=`) for symmetry with HelloDlaa.

**CI builds this sample and never runs it.** There is no NVIDIA hardware in CI
(#32); every Frame Generation create, evaluate and present in this repository
happens on a developer machine.
