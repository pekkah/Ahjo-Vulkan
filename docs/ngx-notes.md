# DLSS / DLAA with Ahjo.Vulkan — the consumer contract

Everything on this page is either in the Vulkan specification, in NVIDIA's
pinned DLSS programming guide (`native/ngx/doc/DLSS_Programming_Guide_Release.pdf`,
`NgxVersion` `v310.7.0`), or was **measured on hardware** — an RTX 4070 Ti on
driver 610.47. Traps found by measurement rather than read in a header say so,
with the driver version, because that provenance is the difference between a
rule and a rumour.

`samples/HelloDlaa` is the worked call site for all of it.

---

## 1. What you get, and what you must supply

`Ahjo.Vulkan.Ngx` is the managed wrapper. `Ahjo.Vulkan.Ngx.Native` carries the
`ahjo_ngx` shim over NVIDIA's **static** NGX client library, plus
`NGX-LICENSE.txt`. Neither package contains DLSS itself.

DLSS runs inside the NVIDIA display driver, reached through a feature library —
`nvngx_dlss.dll` on Windows, `libnvidia-ngx-dlss.so.<version>` on Linux — that
is NVIDIA's to license and **yours to ship** (decision recorded in
[#214](https://github.com/pekkah/Ahjo-Vulkan/issues/214)). There is no software
path and no silent fallback.

Without the file, `NgxContext.Create` throws
`NgxFeatureLibraryNotFoundException` naming the expected file and **every
directory it searched**:

```
DLSS is unavailable: the NVIDIA feature library was not found.
Expected file: nvngx_dlss.dll
Searched:
  C:\p\ahjo-vulkan\samples\HelloDlaa\bin\Release\net10.0\
  C:\p\ahjo-vulkan\native\ngx\staged\win-x64\rel
This library is NOT shipped by Ahjo.Vulkan.Ngx — the application supplies it
from NVIDIA's DLSS SDK (https://github.com/NVIDIA/DLSS, lib/<plat>/rel/).
See docs/ngx-notes.md.
```

Print that message verbatim. It is the diagnosis the typed exception exists to
produce, and reformatting it loses the directory list.

---

## 2. Getting `nvngx_dlss.dll`

Download from [NVIDIA/DLSS](https://github.com/NVIDIA/DLSS) and take the file
from **`lib/<plat>/rel/`**.

**Never `dev/`.** That build draws an on-screen watermark and debug overlay. It
is for development only and must never be redistributed. If you are unsure which
one you shipped, `NgxContext.TryGetStats` will tell you — a `rel/` library
reports `OptLevel == 40` and `IsDevSnippetBranch == false`:

```csharp
if (ngx.TryGetStats(out DlssStats stats) && (stats.OptLevel != 40 || stats.IsDevSnippetBranch))
    throw new InvalidOperationException("A dev/ build of nvngx_dlss is deployed.");
```

**Shipping it beside your executable** is the deployment model. One MSBuild item:

```xml
<ItemGroup Condition="Exists('$(MSBuildProjectDirectory)\nvngx_dlss.dll')">
  <None Include="nvngx_dlss.dll" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

Keep the `Exists(...)` condition. A repository that does not commit the DLL —
this one does not, and neither should yours — must still build on a machine that
has never downloaded it, and `TreatWarningsAsErrors=true` means the item must not
even warn.

**Out of tree**, point NGX at the directory instead:

```csharp
var description = new NgxDescription
{
    ProjectId       = "…your own GUID…",
    EngineVersion   = "1.0.0",
    DlssSearchPaths = [@"C:\redist\dlss\rel"],
};
```

`samples/HelloDlaa` uses `DlssSearchPaths` to reach this repository's
git-ignored `native/ngx/staged/<rid>/rel/`. That is a developer-machine
convenience for working *on* Ahjo.Vulkan, not a deployment pattern.

**Leave `NgxDescription.ApplicationDataPath` unset.** The wrapper materializes
`Path.GetTempPath()` for you. A null reaching NGX **access-violates the
process**, and no managed `catch` recovers. *(Measured, RTX 4070 Ti / 610.47.)*

---

## 3. Licence obligations that land on your application

These are the **application's** obligations, because this repository ships no
feature DLL. From NVIDIA's RTX SDKs licence and the programming guide:

- **Notify NVIDIA before a commercial release** that ships DLSS.
- Follow the **RTX UI branding guidelines** wherever the feature is named or
  configured in your UI.
- **Reproduce the third-party notices** from guide §9.5 in your product's
  attribution.
- **Never redistribute the `dev/` build.**

`Ahjo.Vulkan.Ngx.Native` ships the `ahjo_ngx` shim with `NGX-LICENSE.txt` beside
it and no feature DLL; `Ahjo.Vulkan.Ngx` ships no native files at all. Both are
MIT. The NVIDIA obligations arrive with the DLL you add.

---

## 4. The renderer contract

This is the half the wrapper **cannot** hold. Every item below fails silently:
the picture looks right in a still frame and smears, shimmers or ghosts in
motion. No validation layer, no `AhjoValidation` check and no NGX result code
reports any of it.

The conventions below assume `System.Numerics` row-vector matrices (`v * M`,
hence `model * view * proj`), a projection built with
`Matrix4x4.CreatePerspectiveFieldOfView` and then `proj.M22 *= -1f`, and a
**positive-height** viewport. That gives a y-**down** NDC and a `[0,1]` depth
range — the coordinate system DLSS pixel space already uses (guide §3.6.1:
origin at the top-left, +X right, +Y down). **No negation appears anywhere in
this contract.** If you find yourself adding one, re-read this section.

### 4.1 Jitter

Apply it by **post-multiplying a clip-space translation**:

```csharp
Matrix4x4 t = Matrix4x4.Identity;
t.M41 = 2f * jitterPixels.X / renderWidth;
t.M42 = 2f * jitterPixels.Y / renderHeight;
Matrix4x4 jitteredViewProjection = viewProjection * t;
```

Then hand NGX **the same numbers, unchanged**:

```csharp
JitterOffsetX = jitterPixels.X,
JitterOffsetY = jitterPixels.Y,
```

`jitterPixels` is a Halton(2,3) sequence recentred on `[-0.5, +0.5]`, in render
pixels, cycling over `8 * (target / render)^2` phases (guide §3.7.1.1: 8 at
DLAA, 18 at Quality, 24 Balanced, 32 Performance, 72 Ultra Performance).

**Why this form and not the guide's `ProjectionMatrix.M[2][0] += jitter.X`
(§3.7.2).** That recipe presumes a projection whose `w` is `+z`.
`CreatePerspectiveFieldOfView` is right-handed with `M34 = -1`, so `clip.w = -z`
and `ndc.x = (x·M11 + z·M31) / (-z) = -x·M11/z - M31`: adding `δ` to `M31` shifts
NDC by **minus** `δ`. Both the sign and the magnitude of the guide's form are
convention-dependent. Post-multiplying is immune to both — row-vector
composition gives `clip'.x = clip.x + clip.w · t.M41`, hence
`ndc'.x = ndc.x + t.M41` exactly, with no dependence on the sign of `w`. The
image moves by exactly `+jitterPixels` in DLSS pixel space, which is precisely
what §3.7.3 defines `JitterOffset` to be.

**Measured** (RTX 4070 Ti / 610.47, `HelloDlaa --mode dlaa` on a frozen scene,
comparing two captures four jitter phases apart):

| Jitter sign | Mean abs. difference between the two frames | Gradient energy (sharpness) |
|---|---|---|
| As derived above | **0.028 / 255** — converged | **227** |
| Negated | 0.300 / 255 — 11x more unstable | 193 |

A wrong sign does not smear. It leaves fine detail permanently **shimmering**
instead of resolving, and costs ~15% of the sharpness DLAA was supposed to buy.
The derivation above is the measured-correct one.

### 4.2 Motion vectors

**`previous − current`**, in **render-resolution pixels**:

```hlsl
float2 curUV  = curClip.xy  / curClip.w  * 0.5 + 0.5;
float2 prevUV = prevClip.xy / prevClip.w * 0.5 + 0.5;
outMotion = (prevUV - curUV) * float2(renderWidth, renderHeight);
```

`previous − current` is guide §3.6's definition verbatim: adding the vector to a
pixel's current location must give where that pixel was last frame. The render
extent puts it in §3.6.1's units. Both clip positions come from **unjittered**
matrices — the vertex stage outputs three clip positions (jittered to
`SV_Position`, unjittered current and previous as interpolants).

`VK_FORMAT_R16G16_SFLOAT` is enough: §3.6.1 permits it, and in *pixel* space FP16
carries ~3 decimal digits of relative precision — a 32-pixel motion resolves to
≈0.03 px. The calculus inverts for UV-space vectors, where values are ~1/1000 the
magnitude and FP16's *absolute* precision becomes the binding constraint. That is
a reason to encode in pixels, not a reason to widen the format.

**Two affordances §3.6.3 offers** if your existing motion vectors do not match
the convention above:

- Vectors in **UV space** rather than pixels: set
  `MotionVectorScaleX = renderWidth`, `MotionVectorScaleY = renderHeight`.
- Vectors pointing **along the direction of motion** rather than towards the
  previous frame: set both scales negative.

Both let you set two floats instead of editing a shader. The cost is that the
convention is no longer visible in the shader — a reader of the fragment stage
can no longer tell what space the values are in. Never set either to `0.0f`.
`HelloDlaa` deliberately uses neither, so its shader states the convention
outright.

### 4.3 Mip bias

```
mipLodBias = log2(renderWidth / outputWidth) - 1.0f
```

Guide §3.5, with `NativeBias = 0` and `epsilon = 0`. It applies to **every** DLSS
mode: at DLAA the ratio is 1 and the formula still yields **−1.0**. Recompute and
rebuild the sampler whenever the render extent changes — which for a
resolution-scaling renderer is every resize.

§3.5.1 warns that high-frequency textures may moiré under an aggressive bias.
**Measured** (RTX 4070 Ti / 610.47, `HelloDlaa --mode dlaa` at 1600×900 over a
4-texel checkerboard, on faces angled steeply away from the camera): at −1.00 the
fine checker resolves into a **stable, crisp weave** — no moiré rosettes, no
beat pattern, and no crawl (mean abs. difference between two frames four jitter
phases apart on a frozen scene: 0.028/255). It is **67% sharper** than the same
frame at bias 0 (gradient energy 227 vs 136), which is the detail the bias exists
to recover. Ship the formula.

### 4.4 Depth and exposure

- `DlssFeatureFlags.DepthInverted` describes **reversed-Z**. Guide §3.8: the
  algorithm assumes near = 0.0 and far = 1.0, "but this can be inverted".
  `CreatePerspectiveFieldOfView` gives near → 0, so leave the flag **clear**
  unless you actually run reversed-Z.
- `DlssFeatureFlags.AutoExposure` lets DLSS derive exposure itself. Set it when
  you bind no `ExposureTexture`. The alternative — a 1×1 `R32_SFLOAT` exposure
  image plus `ExposureScale` / `PreExposure` — is what an HDR renderer with its
  own metering wants.
- `DlssFeatureFlags.Hdr` says the colour buffer is linear. **Clear** it and you
  are in LDR mode, where guide §3.1.2 requires colour in `[0,1]` in a
  perceptually linear encoding: linear values in LDR mode "exhibit visible color
  banding, color shifting or other visual artifacts".

  The trap: an `_SRGB` colour attachment is the intuitive way to get a perceptual
  encoding and it is **wrong** here. The hardware would decode on the sampled
  read NGX performs and hand DLSS linear values anyway. Render into a UNORM
  target and apply the sRGB transfer function **in the shader** — use the exact
  piecewise encode, not `pow(c, 1/2.2)`; the approximation is wrong in the toe,
  and the toe is where the banding shows.

### 4.5 `Reset`

Set `DlssEvaluateInputs.Reset` on the first frame after anything that
invalidates the temporal history: a camera cut, a level load, a teleport, and
**every feature re-creation** — start-up and each resize. Pair it with making the
previous-frame matrix equal to the current one, so that frame's motion vectors
are zero rather than garbage. Symptom of forgetting: ghosting for the first few
frames after the event, then clean.

### 4.6 Image layouts, usage, and where to record the evaluate

**`Ahjo.Vulkan` deliberately does not track image layout** (issue #17), so this
is entirely yours. Before `Evaluate`:

| Slot | Layout | Created with |
|---|---|---|
| `Color` | `SHADER_READ_ONLY_OPTIMAL` | `Sampled \| ColorAttachment` |
| `Depth` | `SHADER_READ_ONLY_OPTIMAL` (depth aspect) | `Sampled \| DepthStencilAttachment` |
| `MotionVectors` | `SHADER_READ_ONLY_OPTIMAL` | `Sampled \| ColorAttachment` |
| `Output` | `GENERAL` | `Storage \| TransferDst` (+ `TransferSrc` if you read it back) |

`Storage | TransferDst` on the output is **not optional**, and it is documented
nowhere in NVIDIA's headers: DLSS clears that image itself with
`vkCmdClearColorImage`, which needs the transfer bit
(`VUID-vkCmdClearColorImage-image-00002`). *(Measured, RTX 4070 Ti / 610.47 —
[#218](https://github.com/pekkah/Ahjo-Vulkan/issues/218) D3.)* Note the wrapper's
own usage advisory only fires when `Storage` is **also** missing, so
`Storage`-without-`TransferDst` produces no warning at all — just a layer error,
on hardware, at evaluate time.

The output's barrier therefore needs a destination scope covering both stages
that touch it next:

```csharp
ImageBarrier.Transition(
    in presentation,
    VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED, VkImageLayout.VK_IMAGE_LAYOUT_GENERAL,
    Stage.AllCommands, Access.None,
    Stage.ComputeShader | Stage.AllTransfer, Access.ShaderWrite | Access.TransferWrite);
```

`ComputeShader | ShaderWrite` alone leaves DLSS's own clear outside the barrier's
destination scope — a write-after-write the layer may or may not report,
depending on whether DLSS emits its own barrier first.

**Record `Evaluate` outside any `BeginRendering` scope.** It is not a draw.

**Rebind after `Evaluate`.** `EvaluateFeature_C` clobbers the command buffer's
bound pipeline, descriptor sets and dynamic state (guide §5.2.5).
`CommandRecorder` caches none of that, so there is nothing for the wrapper to
invalidate — but the next draw or dispatch must rebind everything itself.

**`CreateDlss` records real initialization work.** Its recorder must be
submitted **and completed** before the first `Evaluate`. `Queue.ImmediateSubmit`
does both.

### 4.7 Extension lists — both, and they fail differently

The order is a contract, stated on `NgxSupport`: instance extensions → instance →
physical device → device extensions → device → `NgxContext.Create`.

- **Missing instance extensions** → an **access violation** inside NVIDIA's
  client library. Every NGX entry point taking a `VkInstance` resolves
  `vkGetPhysicalDeviceProperties2KHR` through `vkGetInstanceProcAddr`, the loader
  returns null unless `VK_KHR_get_physical_device_properties2` was enabled, and
  NGX does not null-check it. No managed `catch` recovers.
- **Missing device extensions** → `Init` succeeds and then reports
  `SuperSampling.Available = 0` with `FAIL_PlatformError`, which reads exactly
  like an unsupported GPU.

*(Both measured, RTX 4070 Ti / 610.47.)* Use
`NgxSupport.TryGetInstanceExtensions` and `NgxSupport.TryGetDeviceExtensions`,
and dispose each `NgxExtensionSet` **after** the create call that consumed it.

### 4.8 RenderDoc

**RenderDoc does not work with DLSS applications** (guide §8.6). Take your own
frame dump instead: copy the output image to a host-visible buffer and write a
PNG. `HelloDlaa --capture <path.png>` shows how.

---

## 5. VMA and VRAM accounting

DLSS's history and scratch surfaces are allocated **inside the driver**. VMA
never sees them, so they never appear in `MemoryHeapBudget.AllocationBytes`.
They do appear in the driver's own figure, `MemoryHeapBudget.Usage` — but only
if you opt in, and the opt-in is two things that must agree:

```csharp
bool memoryBudget = gpu.SupportsExtension(VulkanExtensions.ExtMemoryBudget);

using Device device = gpu.CreateDevice(new DeviceDescription
{
    Queues     = [new QueueRequest(family, count: 1, priority: 1f)],
    Extensions = […, VulkanExtensions.ExtMemoryBudget],
    Allocator  = new AllocatorDescription { EnableMemoryBudget = memoryBudget },
});
```

Both, or neither: the wrapper fails the pairing check under `AhjoValidation` when
the flag is set without the extension. Then:

```csharp
Span<MemoryHeapBudget> budgets = stackalloc MemoryHeapBudget[16];   // VK_MAX_MEMORY_HEAPS
int count = device.Allocator.GetHeapBudgets(budgets);
```

`NgxContext.TryGetStats` reports DLSS's own figure directly, which is the number
to compare against. **Measured** (RTX 4070 Ti / 610.47, `HelloDlaa --mode dlaa`
resized three times in one session):

| Output extent | VMA `AllocationBytes`, device-local heap | Driver `Usage` | `DlssStats.VramAllocatedBytes` |
|---|---|---|---|
| 1600×900 | 51 MiB | 167 MiB | 63 MiB |
| 1184×661 | 29 MiB | 342 MiB | 102 MiB |
| 1904×1001 | 60 MiB | 374 MiB | 171 MiB |
| 884×481 | 15 MiB | 425 MiB | 200 MiB |

VMA's number tracks the application's own targets and falls when they shrink;
the driver's climbs as DLSS retains per-extent state. One caveat when reading
these numbers: VMA **caches** the `VK_EXT_memory_budget` query and refetches it
only once 30 allocation operations have gone by
(`if (m_Budget.m_OperationsSinceBudgetFetch < 30)`, VMA 3.3.0
`vk_mem_alloc.h:14244`), so a `GetHeapBudgets` call taken immediately before and
immediately after `CreateDlss` returns the identical value — `CreateDlss`
performs no VMA allocations, because DLSS's are the driver's. The movement shows
up on the next rebuild, which is what the table's rows are.

Allocate full-screen, session-lifetime DLSS targets with
`AllocationFlags.DedicatedMemory` — they are exactly what that flag is for
(#214), and sub-allocating them out of a shared block buys nothing.

---

## 6. Output targets and the swapchain

`HelloDlaa` writes DLSS output into an **application-owned presentation image**
and then blits that to the swapchain. That is not a workaround; it is the correct
shape, for four reasons:

1. It unifies every mode — DLSS writes the image in `dlaa`/`quality`, a blit
   writes it in the non-DLSS controls, and there is one presentation path.
2. It makes `--capture` possible on an application RenderDoc cannot inspect
   (§4.8), in an image the application fully describes.
3. It keeps the `Storage | TransferDst` pairing (§4.6) visible in every
   configuration, so a reader cannot copy the non-DLSS branch and lose it.
4. A real renderer pays that cost anyway: DLSS's output is followed by
   post-process and UI, not by present.

`Swapchain.GetImage(uint index)` is what makes the final blit expressible. It
returns a **borrowed** `Image` carrying the swapchain's real format, extent and
usage (plus `1/1/1` for depth, mips and layers). `Dispose` on it is a no-op and
it is never entered into the handle registry, because it owns no allocation. Use
it wherever the destination's *metadata* is read —
`ImageBlitRegion.WholeImage`, `BufferImageCopy.WholeImage`,
`ImageBarrier.Transition`. `Image.FromRaw` reports `0×0` on purpose (#119:
unknown, not wrong), and a `WholeImage` region built over one of those is a
degenerate box that copies nothing, silently. `Swapchain.GetImageHandle` still
exists and is still the right shape for `ImageBarrier`'s object-initializer form,
which takes a raw `nint`.

**One thing to get right on the way to the swapchain.** If your last write to
the swapchain image is a **copy, blit or compute dispatch** rather than a colour
attachment write, the usual acquire/present stage masks are wrong:

```csharp
fc.Submit(queue, ref rec, swap, imageIndex,
    imageAcquireWaitStage:    Stage.AllTransfer,   // not ColorAttachmentOutput
    renderingDoneSignalStage: Stage.AllCommands);  // not AllGraphics
```

`VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT` neither includes transfer stages
nor logically precedes them, so waiting the acquire semaphore there does not gate
a `vkCmdBlitImage` — it can run while the presentation engine still owns the
image. And `VK_PIPELINE_STAGE_ALL_GRAPHICS_BIT` is by definition the graphics
pipeline stages only, so signalling `RenderingDone` there does not wait for that
blit to finish before present. For the same reason, give the acquiring
`UNDEFINED → TRANSFER_DST_OPTIMAL` barrier a **source** scope equal to the stage
the semaphore is waited on, not `TopOfPipe`: a transition out of `UNDEFINED` is a
real write, and a barrier whose first scope is `TopOfPipe` is not gated by a
semaphore wait at any later stage.

Neither mistake produces a validation error. We checked: with
`VK_LAYER_KHRONOS_validation` **and** synchronization validation enabled
(`VK_LAYER_ENABLES=VK_VALIDATION_FEATURE_ENABLE_SYNCHRONIZATION_VALIDATION_EXT`),
an RTX 4070 Ti on driver 610.47 reports zero errors both before and after the
fix. The correction rests on the specification's stage-ordering rules, not on a
layer report — which is worth knowing before you treat a green sync-validation
run as proof of swapchain synchronisation.

**Could DLSS write straight into the swapchain image?** It is expressible, and
it is a portability judgement you make for your own target hardware. Two
conditions must hold, and `Ahjo.Vulkan` can supply neither:

- **The surface must advertise `STORAGE`.** `Swapchain` forwards the requested
  usage to `vkCreateSwapchainKHR` with no clamp against
  `VkSurfaceCapabilitiesKHR.supportedUsageFlags`, which the specification only
  requires to contain `COLOR_ATTACHMENT`. Asking for `ImageUsage.Storage` on a
  surface that does not advertise it is a creation failure, not a downgrade.
- **The swapchain format must not be `_SRGB`.** DLSS's output is a storage
  image, and no `VK_FORMAT_*_SRGB` supports `VK_IMAGE_USAGE_STORAGE_BIT` on any
  mainstream desktop driver. That forces a UNORM swapchain and, with it, the
  manual sRGB encode of §4.4.

This repository deliberately does not demonstrate a configuration that only
works on some surfaces.

---

## 7. Running `samples/HelloDlaa`

```
HelloDlaa [--mode dlaa|quality|off|bilinear] [--frames N] [--capture <path.png>]
          [--require-dlss] [--no-validation]
```

| Mode | Render extent | DLSS | Presentation path |
|---|---|---|---|
| `dlaa` *(default)* | output extent | `DlssQualityMode.Dlaa` | DLSS → presentation image |
| `quality` | `GetOptimalSettings(w, h, MaxQuality)` | `DlssQualityMode.MaxQuality` | DLSS → presentation image |
| `off` | output extent | none | 1:1 `NEAREST` blit |
| `bilinear` | the extent `quality` would use | none | `LINEAR` upscale blit |

`bilinear` is the honest control for `quality`: comparing DLSS against a
native-resolution render flatters nothing, comparing it against the *same
low-resolution render* upscaled naively is what shows the reconstruction. Jitter
and mip bias are applied in `dlaa`/`quality` only. (`bilinear` still asks NGX for
the render extent so the pixel counts match exactly; if NGX is unavailable it
falls back to §3.7.1.1's ratio and runs anyway — a control that needs a
proprietary DLL is not a control.)

Exit codes: `0` ran or skipped cleanly, `2` bad command line or shader,
`3` the validation layer reported an error, `5` `--require-dlss` and DLSS was
unavailable.

**Feature-DLL resolution order**, first hit wins:

1. `-p:NvidiaDlssDll=<path>` on the build — copied to the output directory.
2. `nvngx_dlss.dll` dropped beside `samples/HelloDlaa/HelloDlaa.csproj` —
   git-ignored, copied to the output directory.
3. `NgxDescription.DlssSearchPaths` → `native/ngx/staged/<rid>/rel`, populated by
   `./tools/setup-ngx.ps1`.

Nothing in the build ever downloads a feature DLL.

**CI builds this sample and never runs it.** There is no NVIDIA hardware in CI
(#32) and software rasterizers are not honest coverage, so every real DLSS
create/evaluate in this repository happens on a developer machine. Quote the
local run in the PR; a green CI proves the sample compiles, nothing more.

---

## 8. What the wrapper cannot check — and what can

`Ahjo.Vulkan.Ngx` closes three classes of mistake by construction:

- **The view / image / range triple cannot disagree.** `NgxImage.CreateView`
  builds the view *and* the subresource range from one `ImageViewDescription`.
  `NgxImage.Wrap` borrows a view you already have and takes the description that
  made it — nothing can recover a `VkImageView`'s range after the fact, so
  `Wrap`'s contract is the one thing you must get right yourself. Prefer
  `CreateView`.
- **`ReadWrite` is not part of the API.** The wrapper sets it from the slot, so
  "read-write on an image with no `VK_IMAGE_USAGE_STORAGE_BIT`" is not a sentence
  you can say.
- **Metadata-free inputs are refused, loudly.** An `NgxImage` over an
  `Image.FromRaw` handle fails `RequireMetadata` under `AhjoValidation` with a
  message naming `Image.FromRaw` and telling you to build from a VMA-created
  `Image` instead.

Three it cannot, in the consumer's words:

1. **Image layout.** Nothing in `Ahjo.Vulkan` tracks it (#17), so §4.6 is on you.
2. **The renderer conventions of §4.1–4.5.** Jitter sign, motion-vector
   direction and space, mip bias, `Reset`. Every one is a plausible number the
   wrapper has no way to falsify.
3. **Thread safety.** The NGX API is not thread safe and neither is
   `NgxContext`. Under `AhjoValidation.Enabled` a re-entrancy guard says so
   rather than corrupting the parameter map.

**`VK_LAYER_KHRONOS_validation` is the oracle for #1** — and it is the only one.
It is what found the `TransferDst`/`vkCmdClearColorImage` requirement of §4.6.
Run your DLSS path with the layer on and treat any error as a failure, not as a
log line. `HelloDlaa` does exactly that: validation is on by default and any
layer error exits 3.

For #2 there is no oracle but your own eyes and a measurement. The technique
§4.1 and §4.3 use — freeze the scene, capture two frames a few jitter phases
apart, and compare mean absolute difference and gradient energy — turns "does it
shimmer?" into a number, and it is cheap enough to keep.
