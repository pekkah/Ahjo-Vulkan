# DLSS Frame Generation with Ahjo.Vulkan — the consumer contract

> **Stub.** The measured contract lands with `samples/HelloFrameGen` (#253 PR C).

This page grows into the Frame Generation counterpart of
[ngx-notes.md](ngx-notes.md). Like that page, everything on it is either cited
from NVIDIA's pinned documentation (`native/ngx/doc/DLSS-FG_Programming_Guide.pdf`,
`NgxVersion` `v310.9.1`) or **measured on hardware**, with the driver version.

## What you get, and what you must supply

DLSS Frame Generation runs inside the NVIDIA display driver, reached through a
second feature library: `nvngx_dlssg.dll` on Windows,
`libnvidia-ngx-dlssg.so.<version>` on Linux. Like `nvngx_dlss.dll`, it is
NVIDIA's to license and **yours to ship**. No Ahjo package ever contains it, and
this repository never commits or packs it (decision recorded in
[#214](https://github.com/pekkah/Ahjo-Vulkan/issues/214)). Take it from
[NVIDIA/DLSS](https://github.com/NVIDIA/DLSS) at the pinned tag, from
`lib/<plat>/rel/` **only**. The `dev/` build draws an on-screen
"DLSSG SDK - DO NOT DISTRIBUTE" watermark and must never ship. Place it beside
your executable, or point `NgxDescription.DlssSearchPaths` at its directory.
NGX searches that one list for every feature DLL.

`NgxContext.Create` throws for a missing `nvngx_dlssg.dll` only when
`NgxDescription.RequiredFeatures` includes `NgxFeatures.FrameGeneration`.
Otherwise the context is created, `IsFrameGenerationAvailable` is `false`, and
the Frame Generation create call throws the same exception later. The message,
verbatim (Windows; the first directory is `AppContext.BaseDirectory`, then each
`DlssSearchPaths` entry):

```
DLSS Frame Generation is unavailable: the NVIDIA feature library was not found.
Expected file: nvngx_dlssg.dll
Searched:
  <AppContext.BaseDirectory>
  <each DlssSearchPaths entry>
This library is NOT shipped by Ahjo.Vulkan.Ngx — the application supplies it from NVIDIA's DLSS SDK (https://github.com/NVIDIA/DLSS, lib/<plat>/rel/). See docs/ngx-frame-generation-notes.md.
```

The exception is `NgxFeatureLibraryNotFoundException`, with
`Feature == NgxFeature.FrameGeneration`, `ExpectedFile` and
`SearchedDirectories` carrying the same facts as the message.

*Measured, RTX 4070 Ti / driver 616.92, NGX v310.9.1 (#253 PR A).* A missing
`nvngx_dlssg.dll` shows up on the same key as Super Resolution's:
`FrameGeneration.Available = 0` with
`FrameGeneration.FeatureInitResult = FAIL_FeatureNotFound` (`0xBAD00004`). In
the same state `NgxSupport.TryGetRequirements(NgxFeature.FrameGeneration, …)`
returns `false` (NGX answers `FAIL_NotImplemented`), not "unsupported".

## Extension lists

Measured on an RTX 4070 Ti, driver 616.92, NGX v310.9.1, with `rel/` staged (#253 PR A):

| Query | `NgxFeature.SuperResolution` | `NgxFeature.FrameGeneration` |
|---|---|---|
| Instance extensions | `VK_KHR_get_physical_device_properties2` | `VK_KHR_get_physical_device_properties2` |
| Device extensions | `VK_NVX_binary_import`, `VK_NVX_image_view_handle`, `VK_KHR_buffer_device_address`, `VK_KHR_push_descriptor` | `VK_NVX_binary_import`, `VK_NVX_image_view_handle`, `VK_KHR_buffer_device_address`, `VK_KHR_push_descriptor` |

The two features asked for identical lists on this host. Query both and create
with the **union** anyway: the lists are NGX's answer for one GPU and one driver,
not a contract. NGX did **not** ask for `VK_NV_optical_flow`, and Frame
Generation created successfully on a device with a single graphics queue and no
optical-flow queue.

## The API

All in `Ahjo.Vulkan.Ngx` (#253 PR B):

- **`NgxContext.CreateFrameGeneration(ref CommandRecorder, in DlssFrameGenerationDescription)`**
  creates the feature and records its initialization work. Submit the recorder
  and **wait for it** before the first evaluate. If Frame Generation is
  unavailable on the context, it throws the diagnosed exception described above.
  Recreate the feature on any resize (DLSS-FG guide l. 657-668).
- **`DlssFrameGenerationDescription`** holds `Width`/`Height` (the backbuffer and
  output extent), `BackbufferFormat`, `RenderWidth`/`RenderHeight` (the depth and
  motion-vector extent, written to `DLSSG.InternalWidth/Height`) and
  `DynamicResolution`. An `_SRGB` `BackbufferFormat` throws `ArgumentException`:
  the outputs share the backbuffer's format and need storage usage, which no
  `_SRGB` format supports on mainstream desktop drivers (`docs/ngx-notes.md` §6).
- **`DlssFrameGenerationFeature.Evaluate(ref CommandRecorder, in DlssFrameGenerationInputs)`**
  records one evaluation. It allocates nothing per frame
  (`DlssFrameGenerationEvaluateBenchmarks`). It is a recorded command, not a
  present: presenting `OutputInterpolated` and `OutputReal` is the
  application's job (DLSS-FG guide l. 849-850).
- **`DlssFrameGenerationInputs`** takes the required `Backbuffer`, `Depth`,
  `MotionVectors` and `OutputInterpolated`, and the optional `Hudless`,
  `UiColorAndAlpha` and `OutputReal`. It also takes the `Camera` block
  (`DlssFrameGenerationCamera`: five unjittered `Matrix4x4`s with
  `ClipToLensClip` defaulting to identity, plus position, basis, near/far,
  FOV in radians, aspect and pinhole offset), and the per-frame scalars:
  clip-space jitter, motion-vector scale, `Reset`, the depth/HDR/camera-motion
  flags, `MultiFrameCount`/`MultiFrameIndex` (both default 1) and
  `BackbufferFrameId`.

Under `AhjoValidation.Enabled`, `Evaluate` runs these checks:

- the required slots are set;
- every slot's extent and format are known;
- `Backbuffer` matches the feature's extent **and format**;
- without `DynamicResolution`, `Depth` and `MotionVectors` match the feature's
  render extent. The guide says "When dynamic resolution scaling is not enabled,
  your depth and motion vector resources must match the Render Width and Height
  specified during feature creation" (DLSS-FG guide l. 3931-3932). `Hudless` and
  `UiColorAndAlpha` are not checked against it;
- both outputs match `Backbuffer` in extent and format;
- the usage bits: `Storage` on outputs and `Sampled` on inputs, with
  `ImageUsage.None` skipped;
- `1 ≤ MultiFrameIndex ≤ MultiFrameCount ≤ MaxGeneratedFrames`.

It also issues one **non-failing** advisory: a `DiagnosticSeverity.Warning`
through `AhjoDiagnostics.Sink` when `OutputReal` is bound and the `Backbuffer`'s
known usage lacks `TransferSrc` (below).

Matrix orientation and `MvecScale` semantics are **measured in #253 PR C**, not
asserted here. Until then the wrapper copies the matrices as given. The
usage and layout findings below were measured in PR B; PR C confirms the entry
layouts in a presenting sample.

## Usages and layouts the layer found (#253 PR B)

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

Neither output showed a Super Resolution-style `vkCmdClearColorImage`.

**Layouts after the evaluate.** The test records a real transition out of every
image right after `Evaluate`, in the same command buffer. Each transition's
`oldLayout` is the documented exit layout, so the layer checks what NGX left
behind. A same-layout barrier is not a transition, and the layer does not check
it.

- **`Backbuffer`, with `OutputReal` bound: exits in `GENERAL`, not in the
  `SHADER_READ_ONLY_OPTIMAL` it entered in.** NGX transitions it for its copy
  and does not restore it. A barrier assuming `SHADER_READ_ONLY_OPTIMAL` is
  `VUID-VkImageMemoryBarrier2-oldLayout-01197`.
- **`Backbuffer`, with `OutputReal` unbound:** exits in
  `SHADER_READ_ONLY_OPTIMAL`.
- `Depth`, `MotionVectors`, `Hudless` and `UiColorAndAlpha` exit in
  `SHADER_READ_ONLY_OPTIMAL`.
- `OutputInterpolated` and `OutputReal` exit in `GENERAL`.

So the next barrier on the `Backbuffer` must name `GENERAL` as its `oldLayout`
whenever `OutputReal` was bound. Unlike Super Resolution (DLSS Programming Guide
§3.4), Frame Generation does not return every image in the layout you set.
