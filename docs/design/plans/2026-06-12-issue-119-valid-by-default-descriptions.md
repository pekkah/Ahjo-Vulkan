# Implementation plan — valid-by-default description structs (#119)

Paired with `../specs/2026-06-12-issue-119-valid-by-default-descriptions-design.md`.

## Step 1 — Field initializers on the description structs

- `src/Ahjo.Vulkan/Memory/ImageDescription.cs`
  - `ImageType = VK_IMAGE_TYPE_2D`, `Depth = 1`, `MipLevels = 1`,
    `ArrayLayers = 1`, `Samples = VK_SAMPLE_COUNT_1_BIT`. Update the remarks to
    state the new defaults.
- `src/Ahjo.Vulkan/Memory/ImageViewDescription.cs`
  - `ViewType = VK_IMAGE_VIEW_TYPE_2D`, `LevelCount = VK_REMAINING_MIP_LEVELS`,
    `LayerCount = VK_REMAINING_ARRAY_LAYERS`. Rewrite the per-field doc that
    currently says "defaults to 1D".
- `src/Ahjo.Vulkan/Pipelines/DescriptorBinding.cs`
  - `Count = 1`. (Doc already claims this — make it real.)
- `src/Ahjo.Vulkan/Rendering/SwapchainDescription.cs`
  - `PreferredPresentMode = VK_PRESENT_MODE_FIFO_KHR` + explicit
    `public SwapchainDescription() { }`. Note in the `<summary>` that a default
    means FIFO and IMMEDIATE must be set explicitly (now possible).

## Step 2 — #105: honor IMMEDIATE in present-mode negotiation

- `src/Ahjo.Vulkan/Rendering/Swapchain.cs` `NegotiatePresentMode`
  - Drop the `desc.PreferredPresentMode == default ||` clause; keep the FIFO
    fast-path as `== VK_PRESENT_MODE_FIFO_KHR` (no query needed for the
    guaranteed mode). Everything else queries the surface and honors the request
    or falls back to FIFO. Update the comment.

## Step 3 — `Image.FromRaw` valid-by-default

- `src/Ahjo.Vulkan/Resources/Image.cs` `FromRaw`
  - Pass `depth: 1, mipLevels: 1, arrayLayers: 1` instead of `0, 0, 0`.
    Width/Height stay 0 (not known for a bare handle; not used by the
    subresource guards). Add a one-line note.

## Step 4 — Delete the now-dead image-derived `== 0 ? 1u` guards

Read the `Image` field directly (it is now always `≥1`):

- `src/Ahjo.Vulkan/Recording/ImageBarrier.cs` — `Transition`/`Release`/`Acquire`
  (6 sites: `image.MipLevels`, `image.ArrayLayers`).
- `src/Ahjo.Vulkan/Recording/ImageCopyRegion.cs` — `WholeImage`
  (`src/dst.ArrayLayers`, `src.Depth`).
- `src/Ahjo.Vulkan/Recording/ImageBlitRegion.cs` — `WholeImage`
  (`src/dst.ArrayLayers`, `src/dst.Depth`).
- `src/Ahjo.Vulkan/Recording/BufferImageCopy.cs` — `WholeImage`
  (`image.ArrayLayers`, `image.Depth`).
- `src/Ahjo.Vulkan/Recording/CommandRecorder.cs` — `GenerateMips`
  (`image.MipLevels`/`ArrayLayers`/`Depth`), `ClearColorImage`,
  `ClearDepthStencilImage` (`image.MipLevels`/`ArrayLayers`).

## Step 5 — Keep `default(T)` belt-and-braces (with a comment)

Leave, but annotate with a one-line "default(T) array elements bypass field
initializers — see #119" note where not already obvious:

- `ImageBarrier.ToNative` (`LevelCount`/`LayerCount == 0 ? 1u`).
- `ImageCopyRegion.ToNative` / `ImageBlitRegion.ToNative` / `BufferImageCopy.ToNative`
  (`*LayerCount == 0 ? 1u`, `Aspect == 0 ? COLOR`).
- `CommandRecorder.BeginRendering` (`info.LayerCount == 0 ? 1u` — `RenderingInfo`,
  unrelated but same family; leave untouched).
- `Device.CreateDescriptorSetLayout` (`b.Count == 0 ? 1u`) and
  `DescriptorTemplate.BuildEntries` (`b.Count == 0 ? 1u`) — bindings arrive as a
  span whose elements can be `default(DescriptorBinding)`.

## Step 6 — Tests

- New `tests/Ahjo.Vulkan.Tests/ValidByDefaultDescriptionTests.cs`:
  - Pure (no GPU): default-value assertions for the four structs + `FromRaw`.
  - GPU-gated (`VulkanDriverProbe.HasDriver`): minimal `CreateImage` + `CreateView`
    round-trip; build a default-element `DescriptorBinding` span through
    `CreateDescriptorSetLayout` to prove the belt-and-braces path still works.
- Extend `tests/Ahjo.Vulkan.Tests/SwapchainTests.cs` (Win32 + driver gated):
  request IMMEDIATE, assert `swap.PresentMode == IMMEDIATE` when supported, else
  skip.

## Step 7 — Verify invariants

- `dotnet build Ahjo.Vulkan.slnx` clean (warnings = errors).
- `dotnet test` (Windows CI runs the GPU-gated half via SwiftShader).
- `bench-coverage-checker` + `run-bench` on the touched hot paths
  (`PipelineBarrier`, `CommandRecorder`, `PushDescriptors`) → `Allocated = -`.
- `vulkan-validation-reviewer` over the diff.

## Risk notes

- Changing `ImageViewDescription.LevelCount` default `0 → REMAINING` and
  `ViewType 0 → 2D`, and `ImageDescription` defaults `0 → 1`/`2D`, is a
  behavior change *only* for callers who relied on the old (invalid) zero —
  there are none, because the old zero produced invalid create-infos. Explicit
  callers are unaffected. Samples set these fields explicitly today; grep
  confirms no caller reads a description field expecting `0`.
- `Image.FromRaw` 0→1: only consumer is `HandleConventionsTests` (disposes,
  doesn't inspect mip levels) and the public API; safe.
