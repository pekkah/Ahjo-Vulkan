# Migration guide — Vortice.Vulkan → Ahjo.Vulkan

This doc maps the typical `Vortice.Vulkan` patterns onto Ahjo.Vulkan idioms,
using the Logos engine (`c:\p\logos-engine`) as the worked example. Future
consumers porting off Vortice should follow the same map.

Tracking issues are linked inline. The wrapper-side work is bundled across
[#54], [#55], [#56], [#57], [#58], [#59], [#60], [#61], [#62], [#64], [#65],
[#66] under the [#68] meta.

[#54]: https://github.com/pekkah/Ahjo-Vulkan/issues/54
[#55]: https://github.com/pekkah/Ahjo-Vulkan/issues/55
[#56]: https://github.com/pekkah/Ahjo-Vulkan/issues/56
[#57]: https://github.com/pekkah/Ahjo-Vulkan/issues/57
[#58]: https://github.com/pekkah/Ahjo-Vulkan/issues/58
[#59]: https://github.com/pekkah/Ahjo-Vulkan/issues/59
[#60]: https://github.com/pekkah/Ahjo-Vulkan/issues/60
[#61]: https://github.com/pekkah/Ahjo-Vulkan/issues/61
[#62]: https://github.com/pekkah/Ahjo-Vulkan/issues/62
[#64]: https://github.com/pekkah/Ahjo-Vulkan/issues/64
[#65]: https://github.com/pekkah/Ahjo-Vulkan/issues/65
[#66]: https://github.com/pekkah/Ahjo-Vulkan/issues/66
[#68]: https://github.com/pekkah/Ahjo-Vulkan/issues/68

## 1. Two-phase context init

The engine creates the Vulkan instance during plugin load (before any window
exists) and defers device creation until SDL produces a surface
(`Logos.Engine/EngineApp.cs:805-818` → `VulkanContext.SetSurface`). Ahjo
splits the same way:

```csharp
// Phase 1: instance + debug layer at app start.
using var instance = Instance.Create(new InstanceDescription
{
    Extensions      = [VulkanExtensions.KhrSurface, VulkanExtensions.KhrWin32Surface, ...],
    EnableValidation = true,
    DebugMessage    = msg => sink.Receive(msg),
});

// Phase 2: surface arrives later (engine: SDL_Vulkan_CreateSurface).
//   Wrap an externally-created surface (#64) so Dispose calls vkDestroySurfaceKHR.
Surface surface = Surface.WrapExternal(instance, raw: sdlSurfaceHandle);

// Picker can now consult the surface for present support.
PhysicalDevice gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
{
    for (int i = 0; i < info.QueueFamilies.Length; i++)
        if (info.QueueFamilies[i].SupportsGraphics &&
            gpu.SupportsPresent(info.QueueFamilies[i].Index, in surface))
            return true;
    return false;
});

using var device = gpu.CreateDevice(new DeviceDescription
{
    Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
});
```

Engine files affected: `Logos.Engine/EngineApp.cs`, `Logos.Renderer/Core/VulkanContext.cs`.

## 2. Sync1 → Sync2 barrier mapping

Ahjo records exclusively through `synchronization2` (1.3 core), the wrapper has
no `VkImageMemoryBarrier` / `vkCmdPipelineBarrier` (the unsuffixed v1 entry
points). Vortice consumers map as follows.

| Sync1 (Vortice)                            | Sync2 (Ahjo)                                                            |
|--------------------------------------------|--------------------------------------------------------------------------|
| `vkCmdPipelineBarrier`                     | `CommandRecorder.PipelineBarrier(memory, buffer, image)` (one call)     |
| `VkImageMemoryBarrier`                     | `ImageBarrier` (`Recording/ImageBarrier.cs`)                            |
| `VkBufferMemoryBarrier`                    | `BufferBarrier` (`Recording/BufferBarrier.cs`)                          |
| `VkMemoryBarrier`                          | `MemoryBarrier` (`Recording/MemoryBarrier.cs`)                          |
| `srcStageMask` (32-bit `VkPipelineStageFlags`)        | `Stage` (64-bit `[Flags]` shadow of `VkPipelineStageFlags2`) |
| `srcAccessMask` (32-bit `VkAccessFlags`)              | `Access` (64-bit `[Flags]` shadow of `VkAccessFlags2`)       |
| `srcStageMask = COLOR_ATTACHMENT_OUTPUT`              | `SrcStage = Stage.ColorAttachmentOutput`                     |
| `srcAccessMask = COLOR_ATTACHMENT_WRITE`              | `SrcAccess = Access.ColorAttachmentWrite`                    |
| `srcStageMask = TRANSFER` (umbrella)                  | `Stage.Copy` / `Stage.Blit` / `Stage.Resolve` / `Stage.Clear` (split per cmd) — or `Stage.AllTransfer` for the umbrella |
| Queue ownership transfer (split barrier pair)         | `ImageBarrier.Release` + `ImageBarrier.Acquire` factories    |

Use `ImageBarrier.Transition(in image, from, to, srcStage, srcAccess, dstStage, dstAccess, aspect?)`
for the dominant case — full subresource range + queue families ignored.

Engine files affected: every barrier site in `Logos.Renderer`.

## 3. Sync1 → Sync2 submit mapping

```csharp
// Sync1 (Vortice):
//   var submit = new VkSubmitInfo { ... pWaitDstStageMask = COLOR_ATTACHMENT_OUTPUT, ... };
//   vkQueueSubmit(queue, 1, &submit, fence);

// Sync2 (Ahjo):
queue.Submit2(
    ref recorder, in fence,
    waits:   [new SemaphoreSubmit(in imageAcquired, Stage.ColorAttachmentOutput)],
    signals: [new SemaphoreSubmit(in renderingDone, Stage.AllGraphics)]);
```

`SemaphoreSubmit { Stage }` is where the per-semaphore wait/signal stage lives
in sync2 — there is no separate `pWaitDstStageMask` array.

## 4. Raw `vkAllocateMemory` → VMA

The engine uses raw `vkAllocateMemory` today; Ahjo has no raw memory path.
Migrate every allocation site to `Allocator.CreateBuffer` /
`Allocator.CreateImage` with a `MemoryUsage` enum + `AllocationFlags`. The
allocator hangs off `device.Allocator` (created on first access).

```csharp
// Vortice path: vkCreateBuffer + vkAllocateMemory + vkBindBufferMemory.
using var buffer = device.Allocator.CreateBuffer(
    new BufferDescription
    {
        Size  = byteCount,
        Usage = BufferUsage.StorageBuffer | BufferUsage.TransferDst,
    },
    new AllocationDescription
    {
        Usage = MemoryUsage.AutoPreferDevice,
    });
```

VMA picks the memory type. Override with `AllocationDescription.RequiredFlags`
when the call site needs a specific `VkMemoryPropertyFlags` mask.

Engine files affected: `Logos.Renderer/Memory/GpuBuffer.cs`, `GpuImage.cs`,
asset loaders.

## 5. Persistent-mapped UBO / SSBO ring

Use `AllocationFlags.Mapped` + `AllocationFlags.HostAccessSequentialWrite`
(write-combining; `HostAccessRandom` for buffers the host also reads back).
The buffer exposes `AsSpan<T>()` for direct typed writes — no
`vkMapMemory` / `vkUnmapMemory` dance.

```csharp
using var ubo = device.Allocator.CreateBuffer(
    new BufferDescription { Size = sizeof(SceneUniforms), Usage = BufferUsage.UniformBuffer },
    new AllocationDescription
    {
        Usage = MemoryUsage.AutoPreferHost,
        Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
    });

ubo.AsSpan<SceneUniforms>()[0] = new SceneUniforms { /* ... */ };
```

`Buffer.IsHostCoherent` reports whether `Flush` / `Invalidate` are needed —
on most desktop discrete GPUs HOST_VISIBLE memory is also coherent and the
flush is a no-op. Mobile / UMA targets need the explicit
`buffer.Flush(offset, size)` after a write and `buffer.Invalidate(...)`
before a read.

## 6. Descriptor patterns

### Templated vs non-templated writes

Two paths:

- **Templated** (`DescriptorTemplate<T>` + `CommandRecorder.PushDescriptors<T>`):
  preferred when a fixed binding shape repeats every frame (per-pass UBO +
  fixed image inputs). Zero per-call allocations after template creation.
- **Non-templated** ([#59], `DescriptorWrite` + `DescriptorSet.Update` /
  `CommandRecorder.PushDescriptorSet`): preferred when binding shape varies
  per call site (bindless arrays, per-pass push descriptors with
  heterogeneous bindings).

```csharp
// Bindless single-element write at array index 42:
ReadOnlySpan<DescriptorWrite> writes =
[
    DescriptorWrite.CombinedImageSampler(
        binding: 0, arrayElement: 42,
        ImageDescriptorWrite.Of(samplerHandle, in textureView, ShaderReadOnlyOptimal)),
];
set.Update(device, writes);
```

### Bindless: `UpdateAfterBind | PartiallyBound`

Single permanent set, not per-frame. Engine pattern: a 2 048-slot
`COMBINED_IMAGE_SAMPLER` array with `UpdateAfterBind | PartiallyBound`,
allocations land via `DescriptorWrite` writes at distinct
`dstArrayElement` indices.

```csharp
DescriptorBinding[] bindless =
[
    new DescriptorBinding
    {
        Slot         = 0,
        Type         = VkDescriptorType.VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
        Count        = 2048,
        Stages       = ShaderStages.Fragment,
        BindingFlags = DescriptorBindingFlags.UpdateAfterBind |
                       DescriptorBindingFlags.PartiallyBound,
    },
];
using var layout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
{
    UpdateAfterBindPool = true,
    Bindings            = bindless,
});
```

When the set's highest binding also carries
`DescriptorBindingFlags.VariableDescriptorCount`, allocate it with
`pool.Acquire(layout.Handle, variableDescriptorCount: n)` — the one-argument
overload gives that binding a count of zero. The pool's `poolSizes` budget is
then consumed at `n` per set rather than at the layout's declared maximum, so
size it for the sum of the counts of the live sets.

### Set-0 push-descriptor migration

Replace each per-pass `vkUpdateDescriptorSets` call site with a
`PushDescriptorSet` recording. The set's layout must have
`PushDescriptor = true`.

```csharp
using var passLayout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
{
    PushDescriptor = true,
    Bindings       = passBindings,
});

// On every pass:
ReadOnlySpan<DescriptorWrite> writes = [...];
recorder.PushDescriptorSet(VK_PIPELINE_BIND_POINT_GRAPHICS, in pipelineLayout, set: 0, writes);
```

### Growable pool ([#60])

`DescriptorSetPool` auto-grows on `OUT_OF_POOL_MEMORY` /
`FRAGMENTED_POOL` (default `growOnExhaustion: true`). The engine's
hand-rolled `DescriptorPoolManager` (`Logos.Renderer/Core/DescriptorPoolManager.cs`)
maps directly:

```csharp
using var pool = new DescriptorSetPool(
    device, maxSets: 64,
    [
        new VkDescriptorPoolSize { type = VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, descriptorCount = 64 },
        new VkDescriptorPoolSize { type = VK_DESCRIPTOR_TYPE_STORAGE_BUFFER, descriptorCount = 64 },
    ]);
// Allocations past 64 succeed — pool transparently grows by adding sub-pools.
```

## 7. Push constants

The wrapper accepts up to the device's `maxPushConstantsSize` ([#57]) — desktop
GPUs typically expose 256 B. The engine's 224 B `CullPushConstants` block
goes through unchanged on native:

```csharp
PushConstantRange[] ranges =
[
    new PushConstantRange { Stages = ShaderStages.Compute, Offset = 0, Size = 224 },
];
using var layout = device.CreatePipelineLayout(new PipelineLayoutDescription
{
    PushConstantRanges = ranges,
});

recorder.PushConstants(in layout, ShaderStages.Compute, in cullPC);
```

The wgpu mirror (`WgpuCullFrustumUbo`) is a WebGPU-cap fallback (WebGPU caps
push at 128 B); native Vulkan keeps the 224 B struct.

## 8. Surface handoff ([#64])

```csharp
nint sdlSurface = /* SDL_Vulkan_CreateSurface */;
Surface surface = Surface.WrapExternal(instance, sdlSurface);
// Caller transfers ownership: do not call vkDestroySurfaceKHR by hand.
// surface.Dispose() (or `using`) destroys it.
```

`Surface.FromRaw(handle)` is the borrowing path (no-op `Dispose`) — use
when only inspecting an externally-managed surface.

Engine files affected: `Logos.Engine/EngineApp.cs:805-818`,
`Logos.Renderer/Core/VulkanContext.cs`.

## 9. `ImmediateSubmit` adoption ([#61])

The engine has ~50 sites that allocate-cb / begin / record / end / submit /
wait-idle / free by hand (`Logos.Renderer/Core/VulkanHelpers.cs:52-77` and
asset loaders). Replace with one call:

```csharp
queue.ImmediateSubmit(pool, (ref CommandRecorder rec) =>
{
    rec.CopyBuffer(in staging, in destination);
    // mip generation, IBL convolution, asset hot-swap, …
});
```

The helper begins with `ONE_TIME_SUBMIT`, runs the callback, ends, submits via
`Submit2` (no waits/signals/fence), and `vkQueueWaitIdle`s. The buffer is
retired in `finally` so a record-time throw or device-lost still leaves
the pool's accounting consistent.

> **CLAUDE.md tripwire.** `ForwardRenderer.LoadEnvironment(path)` is safe
> mid-frame because `ImmediateSubmit`'s implicit `vkQueueWaitIdle(graphicsQueue)`
> drains references to the old environment before the swap. Do **not**
> reintroduce a global `vkDeviceWaitIdle` here.

## 10. Cubemap creation ([#55])

Set `Flags = VkImageCreateFlagBits.VK_IMAGE_CREATE_CUBE_COMPATIBLE_BIT` on the
description. View types (`VkImageViewType.Cube` / `CubeArray`) pass through
`ImageViewDescription.ViewType` unchanged.

```csharp
using var cube = device.Allocator.CreateImage(
    new ImageDescription
    {
        ImageType   = VK_IMAGE_TYPE_2D,
        Format      = format,
        Width       = 1024, Height = 1024, Depth = 1,
        MipLevels   = mipCount,
        ArrayLayers = 6,
        Flags       = VK_IMAGE_CREATE_CUBE_COMPATIBLE_BIT,
        // …
    },
    new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

using var cubeView = cube.CreateView(device, new ImageViewDescription
{
    ViewType = VK_IMAGE_VIEW_TYPE_CUBE,
    LevelCount = mipCount, LayerCount = 6,
    // …
});
```

Engine files affected: `Logos.Renderer/Lighting/EnvironmentMap.cs:579-589`,
`IblCache.cs`.

## 11. Mip generation ([#62])

```csharp
queue.ImmediateSubmit(pool, (ref CommandRecorder rec) =>
{
    // Caller transitions mip 0 to TRANSFER_DST_OPTIMAL + copies in the source.
    rec.GenerateMips(in image, finalLayout: VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL);
});
```

Replaces ~80 lines of barrier+blit machinery in
`Logos.Renderer/Materials/GpuTexture.cs:38-279`. Pass
`VkFilter.VK_FILTER_NEAREST` when the source format doesn't advertise both
`BLIT_SRC` and `SAMPLED_IMAGE_FILTER_LINEAR` (probe via [#58], section 12).

## 12. Format probing ([#58])

```csharp
bool linearOk = device.PhysicalDevice.SupportsOptimalTilingFeature(
    candidateFormat,
    VkFormatFeatureFlagBits.VK_FORMAT_FEATURE_BLIT_SRC_BIT |
    VkFormatFeatureFlagBits.VK_FORMAT_FEATURE_SAMPLED_IMAGE_FILTER_LINEAR_BIT);

VkFilter filter = linearOk ? VK_FILTER_LINEAR : VK_FILTER_NEAREST;
```

Replace inline `vkGetPhysicalDeviceFormatProperties` calls in
`Logos.Renderer/Materials/GpuTexture.cs:38-279` and the wgpu-mirror
`WgpuDepthHistoryFormat.Probe`.

## 13. Pipeline cache adoption

One-line on every `Build()`:

```csharp
using var cache = device.LoadOrCreatePipelineCache(cachePath);
// later:
using var pipeline = device.BuildGraphicsPipeline()
    // …
    .WithCache(in cache)
    .Build();
// at shutdown:
cache.Save(cachePath);
```

`LoadOrCreatePipelineCache` validates the file's header (vendor / device /
UUID) against the current device — header mismatches log to
`Console.Error` and start with an empty cache.

## 14. Debug labels

The engine has zero `vkCmdBeginDebugUtilsLabelEXT` / `EndDebugUtilsLabelEXT`
sites today. Adopt opportunistically for RenderDoc capture readability —
typically one label scope per render-graph pass:

```csharp
using (recorder.LabelScope("ShadowCascade0"u8, color: Color.FromRgb(0xFF, 0x88, 0x44)))
{
    // record cascade 0 draws
}
```

`LabelScope` no-ops when `VK_EXT_debug_utils` is not loaded on the instance
(production builds without the layer), so the call is free in shipping
configurations.

## 15. Validation message sink

The engine's `IValidationMessageSink` (severity + VUID + message) maps onto
the wrapper's `Action<DebugMessage>`:

```csharp
using var instance = Instance.Create(new InstanceDescription
{
    EnableValidation = true,
    DebugMessage = msg => sink.Receive(msg.Severity, msg.MessageIdName, msg.Message),
});
```

`DebugMessage` exposes severity, message id name (the `VUID-…` token), and the
formatted message text. The hook fires from the validation-layer thread —
the engine's existing thread-safe sink dispatch maps directly.

## 16. Other helpers

- `ClearColor.Float / .UInt / .Int` ([#66]) for type-safe
  `VkClearColorValue` construction. Use `UInt` for the engine's
  `R16G16B16A16_UINT` G-buffer material RT — the float ctor would
  bit-reinterpret garbage.
- Auto-detected swapchain `imageSharingMode` ([#65]) when graphics ≠ present
  family. The engine's `Logos.Renderer/Core/SwapChain.cs:54-60` logic
  disappears once on the wrapper.
- `GraphicsPipelineBuilder.WithDepthBias(constant, slope, clamp?)` ([#56])
  for the cascaded shadow casters and the Z-prepass-stable opaque
  pipeline.
- `Sampler` + `SamplerDescription` ([#54]) for the engine's 16+
  `vkCreateSampler` sites. The factory clamps `MaxAnisotropy` to the
  device limit and validates anisotropy support before submit.

## Migration checklist (engine-side)

These live in the engine repo, not here, but listed for visibility:

- [ ] Sync1 → sync2 barriers / submits — one PR, mechanical mapping.
- [ ] `vkAllocateMemory` → VMA.
- [ ] `Surface.WrapExternal` for the SDL handoff.
- [ ] `Queue.ImmediateSubmit` at the ~50 call sites in `VulkanHelpers.cs`.
- [ ] `PipelineCache` (one-line per `Build()`).
- [ ] Optional: `LabelScope` per render-graph pass.
