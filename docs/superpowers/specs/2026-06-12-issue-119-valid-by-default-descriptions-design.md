# Valid-by-default description structs via field initializers

**Issue:** [#119](https://github.com/pekkah/Ahjo-Vulkan/issues/119) — *Design: valid-by-default description structs via field initializers (one zero-default convention)*
**Subsumes:** [#113](https://github.com/pekkah/Ahjo-Vulkan/issues/113) (invalid zero-defaults on creation paths), [#105](https://github.com/pekkah/Ahjo-Vulkan/issues/105) (present-mode zero-conflation)
**Date:** 2026-06-12

## Problem

Two competing conventions coexist in the wrapper today:

1. **Creation paths pass description fields through raw.** `Allocator.CreateImage`,
   `Image.CreateView`, and friends copy `desc.MipLevels`, `desc.LevelCount`,
   `desc.ImageType`, … straight into the native `VkCreateInfo` with no
   normalization. A zero-initialized struct therefore produces an *invalid*
   create-info: `mipLevels = 0`, `levelCount = 0`, `samples = 0` all trip
   Vulkan validation (`VUID-VkImageCreateInfo-mipLevels-00947`,
   `VUID-VkImageSubresourceRange-levelCount-01720`, …). This is bug #113.
   Concretely, `new ImageViewDescription { Aspect = … }` (no `LevelCount` set)
   creates a view with `levelCount = 0` — broken today.

2. **Recording helpers normalize ad hoc.** `ImageBarrier`, `ImageCopyRegion`,
   `ImageBlitRegion`, `BufferImageCopy`, `CommandRecorder.GenerateMips` /
   `ClearColorImage` / `ClearDepthStencilImage`, `Device.CreateDescriptorSetLayout`,
   and `DescriptorTemplate` each carry their own `x == 0 ? 1u : x` branch to
   paper over the zero-default. Same disease, scattered cure.

3. **`PreferredPresentMode == default` is overloaded** as both "unset" and a
   meaningful enum value. `VK_PRESENT_MODE_IMMEDIATE_KHR` is **0**, so the
   current `NegotiatePresentMode` treats an explicit IMMEDIATE request as
   "caller didn't choose → ship FIFO". IMMEDIATE is therefore *unrequestable*.
   This is bug #105 — the same zero-conflation, one layer up.

## Goal

Adopt **one** convention across every description struct:

> **`new Description() { … }` (object-initializer syntax) is valid, and an
> unset field means the obvious thing.**

Implement it with **record-struct field initializers** (`= 1`, `= VK_..._2D`,
`= VK_REMAINING_MIP_LEVELS`) rather than scattered call-site normalization, so
the default lives in one place — the type — and every consumer inherits it.

## The `default(T)` caveat (decided)

C# field initializers run **only** when a constructor runs. `new Description()`
(and record-struct object-initializer syntax) runs them; `default(Description)`
and array-element zeroing (`new Description[n]`, `stackalloc`) **bypass** them and
produce all-zero fields.

**Convention, stated precisely:** the supported way to build a description is
`new Description() { … }`. `default(Description)` is *not* contractually valid.

**But** spans of descriptions (`ReadOnlySpan<DescriptorBinding>`,
`ReadOnlySpan<ImageCopyRegion>`) can legitimately contain array elements the
caller filled by index, and a partially-filled `new T[n]` leaves zeroed slots.
The issue explicitly sanctions keeping "a cheap validate-or-normalize for the
true-`default` case." We do exactly that at the **native-conversion boundary**
(`ToNative`) and at the **layout-build loop** (`CreateDescriptorSetLayout`,
`DescriptorTemplate`), where the cost is one branch already on a setup path:
those `x == 0 ? 1u : x` guards stay as documented belt-and-braces against
`default(T)` elements. The *image-derived* guards (those reading an `Image`
handle's `MipLevels`/`ArrayLayers`/`Depth`) become genuinely dead and are
deleted — see below.

## Design

### Field initializers (the core change)

| Struct | Kind | Initializers |
|---|---|---|
| `ImageDescription` | record struct | `ImageType = VK_IMAGE_TYPE_2D`, `Depth = 1`, `MipLevels = 1`, `ArrayLayers = 1`, `Samples = VK_SAMPLE_COUNT_1_BIT` |
| `ImageViewDescription` | record struct | `ViewType = VK_IMAGE_VIEW_TYPE_2D`, `LevelCount = VK_REMAINING_MIP_LEVELS`, `LayerCount = VK_REMAINING_ARRAY_LAYERS` |
| `SwapchainDescription` | ref struct | `PreferredPresentMode = VK_PRESENT_MODE_FIFO_KHR` (+ explicit parameterless ctor) |
| `DescriptorBinding` | record struct | `Count = 1` |

`VK_REMAINING_MIP_LEVELS` / `VK_REMAINING_ARRAY_LAYERS` (`~0u`) are the
spec-blessed "rest of the image" sentinels and are valid in a view's
subresource range — they make a default view cover the whole image, which is
the dominant intent and strictly better than today's invalid `levelCount = 0`.

**`ViewType = 2D` + `LayerCount = REMAINING` interaction (validation-review
finding).** For the dominant **single-layer 2D image** the resolved layer count
is 1, which a 2D view type requires
(`VUID-VkImageViewCreateInfo-imageViewType-04973`) — defaults are mutually
consistent. For an **array/cube/3D** parent image a bare
`new ImageViewDescription { Aspect }` keeps `ViewType = 2D` and resolves
`layerCount > 1`, which validation rejects. We **keep REMAINING** (per the
issue) rather than defaulting `LayerCount = 1`, because: (a) for the common
single-layer case the two are identical; (b) on a multi-layer image a *loud*
04973 reject is better than `LayerCount = 1` silently viewing only layer 0; and
(c) array/cube/3D callers must set `ViewType` explicitly anyway (a 2D view of an
array image is itself the mistake). The single-layer assumption is documented on
the struct.

### `ref struct` field initializers need an explicit parameterless ctor

Record structs synthesize a parameterless constructor that runs field
initializers. A plain `ref struct` does **not** — `new SwapchainDescription()`
would silently skip the initializer and yield `PreferredPresentMode = 0`
(IMMEDIATE!), reintroducing #105. So `SwapchainDescription` gets an explicit
`public SwapchainDescription() { }`; the initializer then runs for both
`new SwapchainDescription()` and `new SwapchainDescription { … }`.
(`DescriptorSetLayoutDescription`, `PipelineLayoutDescription`,
`VertexInputDescription`, `ColorBlendDescription`, `InstanceDescription`,
`DeviceDescription` need no initializer, so they stay as-is.)

### #105 fix: honor IMMEDIATE in `NegotiatePresentMode`

With FIFO as the *type-level* default, an unset present mode is already FIFO by
the time `NegotiatePresentMode` runs. The function no longer needs to treat
`== default` as "unset", so it drops that conflation and honors whatever the
caller requested (querying the surface and falling back to FIFO only when the
requested mode isn't actually supported). IMMEDIATE (0) is now distinguishable
from "unset" (FIFO, 2) and therefore requestable.

### Making the image-derived `== 0 ? 1u` guards genuinely dead

The factories on `ImageBarrier` / `ImageCopyRegion` / `ImageBlitRegion` /
`BufferImageCopy` and the `CommandRecorder` whole-image helpers read
`image.MipLevels` / `image.ArrayLayers` / `image.Depth` off an **`Image`
handle**, not off a description. Field initializers on descriptions don't touch
those — an `Image` carries whatever `CreateImage` / `FromRaw` stamped on it.

- `Allocator.CreateImage` now receives `≥1` for mip/array/depth (via the
  description initializers), so VMA-allocated images are valid-by-default.
- `Image.FromRaw` (the borrowed-handle path, e.g. wrapping a single raw
  `VkImage`) currently stamps `0/0/0` for depth/mip/array. We extend the
  convention to it: `FromRaw` stamps `1/1/1`, which is *correct* for any
  single raw image you'd wrap (notably swapchain images are exactly 1 mip,
  1 layer, depth 1). Swapchain-owned images are vended as raw `nint` handles
  via `GetImageHandle` (not as `Image`), so this doesn't change swapchain
  barriers; it only makes the public `FromRaw` valid-by-default.

With both image sources guaranteed `≥1`, the image-derived `image.X == 0 ? 1u`
branches are dead → deleted (read the field directly). The description-own-field
guards at `ToNative` stay (the `default(T)` belt-and-braces above).

### Audit of the remaining description structs

| Struct | Verdict |
|---|---|
| `BufferDescription` | `Size` and `Usage` have no "obvious" non-zero default — the caller *must* state them; `default` is meaningfully empty, not a normalization target. No initializer. (`Size == 0` remains a caller error, out of scope here.) |
| `SamplerDescription` | All-zero already maps to a **valid** sampler (NEAREST/REPEAT/COMPARE_OP_NEVER/…). Already valid-by-default; no initializer needed. |
| `AllocationDescription` | `Usage`/`Flags` zero defaults are intentional VMA "unknown/none". No change. |
| `ColorBlendAttachment`, `Vertex*Description`, `PipelineLayoutDescription`, `ColorBlendDescription`, `VertexInputDescription` | Zero-defaults already valid or caller-specified (presets handle blending). No change. |
| `InstanceDescription`, `DeviceDescription` | Documented legal zero-defaults. No change. |
| `ImageBarrier` | Already documents that `Aspect`/queue-family must be set explicitly (queue family 0 is valid, so no sentinel). Subresource `LevelCount`/`LayerCount` keep their `ToNative` `== 0 ? 1u` guard as `default(T)` belt-and-braces. No field initializer (a barrier built directly is expected to name its range; the factories set it). |
| `ImageCopyRegion`, `ImageBlitRegion`, `BufferImageCopy` | `ToNative` keeps the `LayerCount == 0 ? 1u` and `Aspect == 0 ? COLOR` guards as `default(T)` belt-and-braces. Factory image-derived guards deleted. |

### Swapchain advisory sentinels are *not* touched

`PreferredImageCount == 0` → `minImageCount + 1`, `ImageUsage == 0` →
`ColorAttachment`, `Width/Height == 0` clamp — these are documented sentinel
semantics where `0` is a genuinely nonsensical value (you can't have a
0-image swapchain), so overloading `0` as "negotiate" is correct and stays.
Only `PreferredPresentMode` is a bug, because IMMEDIATE legitimately *is* `0`.

## Invariants honored

- **Zero per-frame allocations:** field initializers compile into the
  constructor — no heap, no per-frame cost. Descriptions are built at setup.
  The retained `== 0 ? 1u` guards are the same single integer compare that was
  already there. Recording-hot-path benchmarks (`PipelineBarrier`,
  `CommandRecorder`, `PushDescriptors`) must still report `Allocated = -`.
- **AOT-clean:** no reflection, no dynamic codegen — just literals.
- **TreatWarningsAsErrors:** no suppressions introduced.
- **Generated dirs untouched.**

## Tests

New `ValidByDefaultDescriptionTests` (no GPU needed for the round-trip half):

- `new ImageDescription()` → `ImageType == 2D`, `Depth/MipLevels/ArrayLayers == 1`,
  `Samples == 1_BIT`.
- `new ImageViewDescription()` → `ViewType == 2D`,
  `LevelCount == VK_REMAINING_MIP_LEVELS`, `LayerCount == VK_REMAINING_ARRAY_LAYERS`.
- `new SwapchainDescription()` → `PreferredPresentMode == FIFO`; an explicitly
  set IMMEDIATE is preserved (proves #105 distinguishability at the type level).
- `new DescriptorBinding()` → `Count == 1`.
- `Image.FromRaw(h)` → `MipLevels/ArrayLayers/Depth == 1`.
- GPU-gated: `Allocator.CreateImage` with a minimal `new ImageDescription { Format, Width, Height, Usage }`
  succeeds and the image round-trips (proves valid VkImageCreateInfo);
  `CreateView` with a minimal `new ImageViewDescription { Aspect }` succeeds
  (proves valid VkImageViewCreateInfo where `levelCount = 0` would have failed).
- GPU-gated (Win32): requesting `VK_PRESENT_MODE_IMMEDIATE_KHR` yields
  `swap.PresentMode == IMMEDIATE` when the surface supports it (skip otherwise) —
  proves IMMEDIATE is requestable end-to-end.

## Decisions log (for review)

1. **Keep `ToNative` / layout-build `== 0 ? 1u` guards** as documented
   `default(T)` belt-and-braces, rather than delete everything. Rationale: span
   elements bypass field initializers; the issue explicitly allows this; cost is
   one already-present branch on a setup/recording-prep path.
2. **Delete the image-derived guards** by making `Image.FromRaw` stamp `1/1/1`.
   Rationale: extends the same valid-by-default convention to the `Image` handle;
   `1/1/1` is correct for any single wrapped raw image; lets the factories read
   the field directly.
3. **Explicit parameterless ctor on `SwapchainDescription`** so the ref-struct
   field initializer actually runs — without it `new SwapchainDescription()`
   would re-open #105.
