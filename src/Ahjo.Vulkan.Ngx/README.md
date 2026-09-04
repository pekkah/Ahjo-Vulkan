# Ahjo.Vulkan.Ngx

NVIDIA **DLSS Super Resolution** and **DLAA** for [Ahjo.Vulkan](https://github.com/pekkah/Ahjo-Vulkan),
over the raw bindings in `Ahjo.Vulkan.Ngx.Native`.

```csharp
using Ahjo.Vulkan;
using Ahjo.Vulkan.Ngx;

var description = new NgxDescription
{
    ProjectId     = "a0f57b54-1daf-4934-90ae-c4035c19df04", // your own GUID
    EngineVersion = "1.0.0",
};

using NgxContext ngx = NgxContext.Create(device, description);

DlssOptimalSettings settings = ngx.GetOptimalSettings(3840, 2160, DlssQualityMode.MaxQuality);
if (!settings.IsAvailable) return;                      // that mode is not offered here

using var color  = NgxImage.CreateView(device, colorImage,  new ImageViewDescription { Aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT });
// …depth, motion vectors, output the same way

using DlssFeature dlss = ngx.CreateDlss(ref setupRecorder, new DlssFeatureDescription
{
    RenderWidth  = settings.RenderWidth, RenderHeight = settings.RenderHeight,
    OutputWidth  = 3840,                 OutputHeight = 2160,
    Mode         = DlssQualityMode.MaxQuality,
    Flags        = DlssFeatureFlags.MotionVectorsLowRes | DlssFeatureFlags.DepthInverted,
});
// submit setupRecorder and WAIT before the first Evaluate — CreateDlss records init work.

dlss.Evaluate(ref recorder, new DlssEvaluateInputs
{
    Color = color, Depth = depth, MotionVectors = motionVectors, Output = output,
    JitterOffsetX = jitterX, JitterOffsetY = jitterY,
    RenderWidth = settings.RenderWidth, RenderHeight = settings.RenderHeight,
});
```

`Evaluate` allocates nothing per frame.

## This package does not contain DLSS

It ships **no native files at all** — the `ahjo_ngx` shim rides in
`Ahjo.Vulkan.Ngx.Native`, and the feature DLL is **yours**. `nvngx_dlss.dll`
(Windows) and `libnvidia-ngx-dlss.so.<version>` (Linux) are NVIDIA's, covered by
the NVIDIA RTX SDKs licence: download them from
[NVIDIA/DLSS](https://github.com/NVIDIA/DLSS) (`lib/<plat>/rel/`, never `dev/` —
that build carries an on-screen watermark) and place the file beside your
executable or point `NgxDescription.DlssSearchPaths` at it.

Without it, `NgxContext.Create` throws `NgxFeatureLibraryNotFoundException`
naming the expected file and every directory searched. There is no silent
fallback, and there is no software path: DLSS runs inside the NVIDIA display
driver.

The full consumer contract — where to get the DLL, the NVIDIA licence
obligations that land on **your** application, and the renderer conventions
(jitter sign, motion-vector direction and space, mip bias, `Reset`, image
layouts) that this wrapper cannot check — is
[`docs/ngx-notes.md`](https://github.com/pekkah/Ahjo-Vulkan/blob/main/docs/ngx-notes.md).
`samples/HelloDlaa` is a worked call site for all of it.

## Three contracts the wrapper holds, and one it cannot

- **The view/image/range triple cannot disagree.** `NgxImage.CreateView` builds
  the view *and* the subresource range from one `ImageViewDescription`;
  `NgxImage.Wrap` borrows a view you already have and takes the description
  that made it. Nothing can recover a `VkImageView`'s range after the fact, so
  `Wrap`'s contract is the one thing you must get right yourself.
- **`ReadWrite` is not part of the API.** The wrapper sets it from the slot —
  `false` for every input, `true` for `Output` — so "read-write on an image with
  no `VK_IMAGE_USAGE_STORAGE_BIT`" is not a sentence you can say. The usage bits
  themselves are checked under `AhjoValidation.Enabled`.
- **Image layout is yours.** Inputs must be in a shader-read layout and the
  output in `VK_IMAGE_LAYOUT_GENERAL` before `Evaluate`, which must be recorded
  outside any `BeginRendering` scope. `Ahjo.Vulkan` deliberately does not track
  layout (issue #17), so the wrapper cannot check this — enable
  `VK_LAYER_KHRONOS_validation`, which does.

Give the output image `ImageUsage.Storage | ImageUsage.TransferDst`: DLSS binds
it as a storage image *and* clears it itself with `vkCmdClearColorImage`, which
needs the transfer bit. That second half is not in NVIDIA's headers — the
validation layer is what says so.

**Rebind after `Evaluate`.** NGX clobbers the command buffer's bound pipeline,
descriptor sets and dynamic state. `CommandRecorder` caches none of that, so
there is nothing for the wrapper to invalidate — but you must rebind before the
next draw or dispatch.

**Not thread safe.** The NGX API is not; neither is `NgxContext`. Under
`AhjoValidation.Enabled` a re-entrancy guard says so rather than corrupting the
parameter map.

## Requirements

NVIDIA hardware with a DLSS-capable driver, `win-x64` or `linux-x64`, and the
feature DLL above. `Ahjo.Vulkan` takes no dependency on this package.

Part of [Ahjo.Vulkan](https://github.com/pekkah/Ahjo-Vulkan). MIT licensed —
the NVIDIA licence obligations that come with the feature DLL are yours.
