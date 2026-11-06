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
