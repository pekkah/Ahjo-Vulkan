# `Ahjo.Vulkan.Ngx` — the managed DLSS/DLAA wrapper: what the binding shape can enforce, and what it cannot

**Issue:** [#218](https://github.com/pekkah/Ahjo-Vulkan/issues/218) — *NGX Phase 2: Ahjo.Vulkan.Ngx — the managed DLSS/DLAA wrapper*
**Phase 2 of:** [#214](https://github.com/pekkah/Ahjo-Vulkan/issues/214) (tracking; research summary and the fixed ship-model decisions — **not reopened here**)
**Builds on:** [#216](https://github.com/pekkah/Ahjo-Vulkan/issues/216) / [PR #217](https://github.com/pekkah/Ahjo-Vulkan/pull/217) (Phase 1, landed on this branch: the shim, the bindings, the `ngx-native` lane)
**Lands consistently with:** [#166](https://github.com/pekkah/Ahjo-Vulkan/issues/166) (`Ahjo.Vulkan.Slang` — the wrapper-over-native package shape), [#209](https://github.com/pekkah/Ahjo-Vulkan/issues/209)/[#213](https://github.com/pekkah/Ahjo-Vulkan/issues/213) (the `readonly` recording surface), [#119](https://github.com/pekkah/Ahjo-Vulkan/issues/119) (valid-by-default descriptions), [#158](https://github.com/pekkah/Ahjo-Vulkan/issues/158) (a lane declares what it has), [#32](https://github.com/pekkah/Ahjo-Vulkan/issues/32) (software rasterizers are not coverage)
**Date:** 2026-09-03 (paired with the Phase 1 documents; both ship in PR #217)

## Problem

Phase 1 shipped `Ahjo.Vulkan.Ngx.Native`: 27 exports, blittable structs, 204 UTF-8
parameter names, all measured. What it did **not** ship is anything a renderer can
call. `NVSDK_NGX_VULKAN_EvaluateFeature_C` takes an opaque parameter map that the
caller must populate by string name with ~30 entries per frame, four of which are
`void*` pointers to stack-constructed `NVSDK_NGX_Resource_VK` structs
(`src/Ahjo.Vulkan.Ngx.Native/Generated/NgxApi.cs:66`, `:24`). Getting one of those
thirty wrong produces a black frame, a driver fault, or silent ghosting — never a
compile error.

Three properties of that surface make "wrap the calls" underspecified, and each has
to be decided rather than assumed:

1. **The wrapper's Vulkan handles are not publicly reachable in the shape NGX
   wants.** `Instance.Handle`, `PhysicalDevice.Handle`, `Device.Handle`,
   `PhysicalDevice.Instance` and `CommandRecorder.Handle` are all `internal`
   (`Lifecycle/Instance.cs:26`, `Lifecycle/PhysicalDevice.cs:24-25`,
   `Lifecycle/Device.cs:24`, `Recording/CommandRecorder.cs:48`).
   `Ahjo.Vulkan.Ngx` is a **different assembly** and `Ahjo.Vulkan.csproj:26-27`
   grants internals only to `Ahjo.Vulkan.Tests` and `Ahjo.Vulkan.Benchmarks`. The
   design has to reach `VkInstance` / `VkPhysicalDevice` / `VkDevice` /
   `VkCommandBuffer` through the public surface only, and one of the four is not
   reachable at all today.

2. **`NVSDK_NGX_ImageViewInfo_VK` needs six correlated facts about one image view,
   and the wrapper's `ImageView` carries none of them.** `ImageView` is two
   pointers — the view handle and the device that destroys it
   (`Resources/ImageView.cs:20-21`). The image, the subresource range, the format
   and the extent live on `Image` (`Resources/Image.cs:27-36`) or on the
   `ImageViewDescription` that was thrown away after `Image.CreateView`
   (`Resources/Image.cs:113`). Nothing in the wrapper can check that a caller's
   `(view, image, range)` triple describes one view — which is precisely the first
   of the three invariants `vulkan-validation-reviewer` carried over from PR #217.

3. **The three carried-over invariants have three different answers, and the issue
   asks for one decision each.** Validation layers catch all three at
   `EvaluateFeature_C` time; the wrapper's job is to stop two of them from being
   expressible and to be honest that the third cannot be.

Beyond that, the issue's own items contain two claims that do not survive contact
with the code: `AllocatorDescription` does not exist (E11), and the
`EnableMemoryBudget` flag it asks for has nothing to read (E11 again). Both need a
decision here rather than a discovery during implementation.

## Evidence

Every measurement below was taken on this working tree at commit `5937182`
(Phase 1 merged), against `src/Ahjo.Vulkan.Ngx.Native/Generated/` as committed and
`native/ngx/include/` at `NgxVersion` `v310.7.0` (`Directory.Build.props:66`).

### E1. Exactly one of the four Vulkan handles NGX needs has no public path

| Handle | Public accessor | Citation |
|---|---|---|
| `VkDevice` | `Device.RawHandle` (`ulong`) | `Lifecycle/Device.cs:159` |
| `VkPhysicalDevice` | `Device.PhysicalDevice` (public field) → `PhysicalDevice.RawHandle` | `Lifecycle/Device.cs:27`, `Lifecycle/PhysicalDevice.cs:33` |
| `VkCommandBuffer` | `CommandRecorder.RawHandle` (`nint`) | `Recording/CommandRecorder.cs:80` |
| `VkInstance` | **none from a `Device`** — `PhysicalDevice.Instance` is `internal` | `Lifecycle/PhysicalDevice.cs:25` |

`Instance.RawHandle` itself is public (`Lifecycle/Instance.cs:58`); what is missing
is the *edge* from a device to its instance. Every NGX discovery call and
`Init_with_ProjectID` takes `VkInstance`
(`Generated/NgxApi.cs:81`, `:84`, `:90`).

### E2. `ImageView` carries no metadata, and the description that produced it is not retained

`Resources/ImageView.cs:19-27` is the whole struct: `Handle` and `DeviceHandle`.
`Image.CreateView` (`Resources/Image.cs:113-147`) builds a
`VkImageSubresourceRange` from the caller's `ImageViewDescription`, hands it to
`vkCreateImageView`, and returns `new ImageView(raw, device.Handle)` — the range,
the format override and the view type are all dropped on the floor.

Consequence: an API of the shape
`Evaluate(color: Image, colorView: ImageView, colorRange: VkImageSubresourceRange, …)`
is **checkable by nothing**. There is no query that recovers a `VkImageView`'s
range, and the wrapper deliberately does not track it.

### E3. `Image` knows its usage and extent — except when it was wrapped from a raw handle

`Resources/Image.cs:27-36` carries `Format`, `Width`, `Height`, `Depth`,
`MipLevels`, `ArrayLayers` and `Usage`. But `Image.FromRaw`
(`Resources/Image.cs:84-85`) constructs `ImageUsage.None`, `Width = 0`,
`Height = 0`, `MipLevels = 1`, `ArrayLayers = 1` — the "valid-by-default" shape from
#119, used for swapchain-owned images.

So a usage check is sound only when `Usage != ImageUsage.None`. Treating `None` as
"missing `Storage`" would reject every swapchain image; treating it as "has
`Storage`" would silently pass. The check has to name the third state.

### E4. A satellite package can run the `AhjoValidation` protocol without an internals grant

`AhjoValidation.Enabled` is `public` (`Diagnostics/AhjoValidation.cs:69`),
`AhjoValidationException` is `public` (`:15-17`), and `AhjoDiagnostics.Sink` is a
`public` property returning a never-null delegate (`Diagnostics/AhjoDiagnostics.cs:59-64`).
What is `internal` is only the two-line convenience — `AhjoValidation.IsEnabled`
(`:80`) and `AhjoValidation.Fail` (`:94`), whose body is
`AhjoDiagnostics.Write(...)` + `throw` — and `AhjoDiagnostics.Write` itself (`:74`),
whose body is `s_sink(severity, source, message)`.

Measured against `Ahjo.Vulkan.Slang`: it references `Ahjo.Vulkan`
(`src/Ahjo.Vulkan.Slang/Ahjo.Vulkan.Slang.csproj:35`) and uses **neither**
`AhjoValidation` nor `AhjoDiagnostics` — it throws `SlangCompilationException`. So
there is no precedent to follow, only a protocol to re-implement in three lines.

### E5. The shim copies and retains every string the init path hands it; the managed side needs no retention

`native/ngx/src/ahjo_ngx.cpp:755-756` copies `ProjectId` and `EngineVersion`
through `AhjoRetainedCopyUtf8`, and `:707`, `:719`, `:728`, `:738` retain the
widened `ApplicationDataPath`, the search-path array, each path, and the
`NVSDK_NGX_FeatureCommonInfo` itself. The discovery path is call-scoped by
construction (spec #216 D8).

So `AhjoNgxInitInfo.ProjectId` / `EngineVersion` / `ApplicationDataPath` /
`FeatureSearchPaths` only have to be valid **for the duration of the P/Invoke**.
A `stackalloc`-or-pooled UTF-8 block that dies when `Create` returns is correct;
the pinned-GC-array trap PR #217 fixed on the shim side does not recur on the
managed side, provided the encoder writes the NUL terminator itself.

### E6. The `NVSDK_NGX_Resource_VK` structs must outlive the map, and the map outlives the frame

`NGX_VULKAN_EVALUATE_DLSS_EXT` stores each resource with
`NVSDK_NGX_Parameter_SetVoidPointer(pInParams, …, pInDlssEvalParams->Feature.pInColor)`
(`native/ngx/include/nvsdk_ngx_helpers_vk.h:169-172`) — the map holds the caller's
pointer, not a copy, and NGX dereferences it inside `EvaluateFeature_C`
(`:229`).

The parameter map, by contrast, is allocated once (`AllocateParameters`) and reused.
So the map retains dangling `void*`s the moment the frame that produced the
resource structs returns. **The only shape that is safe without heap-allocating the
resources per frame is one method that fills the map and calls
`EvaluateFeature_C` before returning.** Splitting "prepare" from "evaluate" across
two public calls would put four dangling pointers in a live NGX map.

### E7. The parameter names are already the exact shape `Utf8Name` wants

`Generated/NgxApi.cs:99-708` emits 204 `public static ReadOnlySpan<byte> … => "…"u8;`
properties, NUL-terminated with the terminator outside `Length` (pinned by
`NgxSmokeTests`, PR #217). `Utf8Name.FromLiteral(ReadOnlySpan<byte>)`
(`Lifecycle/Utf8Name.cs:36-41`) turns exactly that into a stable `sbyte*` with no
`fixed`, no pinning and no allocation, and documents that RVA-backed literals are
the *only* legal input. The ~30 names an evaluate needs can therefore be hoisted
into `static readonly Utf8Name` fields once per process.

### E8. NGX's returned extension array has no documented lifetime

`nvsdk_ngx_vk.h:639-649` documents `OutExtensionProperties` as "an output pointer
that will be populated with an array of `VkExtensionProperties` structures" and says
nothing about who owns it or how long it lives. #216's spec D8 records the same
observation from the shim side ("returns a pointer to NGX-owned storage, not to
ours").

`Utf8Name.FromLiteral`'s contract (`Lifecycle/Utf8Name.cs:28-35`) is that the
pointer must outlive every use and must not be GC-movable. Pointing a `Utf8Name`
into undocumented NGX storage satisfies neither requirement provably. The names
have to be copied into storage this wrapper owns.

### E9. `NGX_DLSS_GET_STATS` reads two of its three outputs through macros Phase 1 deliberately excluded

`native/ngx/include/nvsdk_ngx_helpers.h:42-43` reads `OptLevel` and
`IsDevSnippetBranch` with `NVSDK_NGX_EParameter_OptLevel` /
`NVSDK_NGX_EParameter_IsDevSnippetBranch` — members of the 74-macro hash-encoded
family (`"#\x10"`, `nvsdk_ngx_defs.h:588`) that #216 D7/E7 excluded from the
binding surface because ClangSharp emits their control bytes literally into a
`.cs` file under `text=auto eol=lf` normalization.

The string-form constants exist (`NgxApi.cs:99` `"Snippet.OptLevel"u8`, `:102`
`"Snippet.IsDevBranch"u8`), but whether NGX's map treats the two forms as aliases is
**not documented and not measured**. Only `VRAMAllocatedBytes` comes from a
name the wrapper can spell with confidence:
`NVSDK_NGX_Parameter_SizeInBytes` (`nvsdk_ngx_helpers.h:41`, `NgxApi.cs:369`).

### E10. Optimal settings come from the **capability** map, not from `AllocateParameters`

`nvsdk_ngx_helpers.h:79-83` fetches `NVSDK_NGX_Parameter_DLSSOptimalSettingsCallback`
by `GetVoidPointer` and returns `FAIL_OutOfDate` when it is null, with the header's
own comment naming the cause: "You used `NVSDK_NGX_AllocateParameters()` for
creating InParams. Try using `NVSDK_NGX_GetCapabilityParameters()` instead."
`nvsdk_ngx_vk.h:383-400` confirms the split: `AllocateParameters` maps "do not come
pre-populated", and both map kinds must be freed with `DestroyParameters`
(`:355-357`, `:398-400`).

`NGX_DLSS_GET_OPTIMAL_SETTINGS` also **writes** `Width`, `Height`,
`PerfQualityValue` and `RTXValue` into that same capability map
(`nvsdk_ngx_helpers.h:86-89`). So the capability map is mutable shared state owned
by the context, and querying optimal settings is not a pure read.

### E11. `AllocatorDescription` does not exist, and nothing in the public API can observe a memory budget

- There is no `AllocatorDescription` type: `src/Ahjo.Vulkan/Memory/` contains
  `AllocationDescription.cs` (per-allocation) and no allocator-level description.
- `Allocator.Create` takes a bare `Device` (`Memory/Allocator.cs:57`) and hardcodes
  `ci.flags = VMA_ALLOCATOR_CREATE_BUFFER_DEVICE_ADDRESS_BIT`
  (`Memory/Allocator.cs:111`).
- The allocator is created lazily and parameterlessly from `Device.Allocator`
  (`Lifecycle/Device.cs:143-156`), so a flag has to reach it through
  `DeviceDescription` or not at all.
- `vmaGetHeapBudgets` is bound (`src/Ahjo.Vulkan.Vma.Native/Generated/Vma.cs:33`)
  and **called nowhere**. The only statistics call in the wrapper is
  `vmaCalculateStatistics` inside `Allocator.Dispose`
  (`Memory/Allocator.cs:330-331`), for the leak warning. There is no public
  statistics API of any kind.
- `VMA_ALLOCATOR_CREATE_EXT_MEMORY_BUDGET_BIT = 0x8` exists in the bindings
  (`Generated/VmaAllocatorCreateFlagBits.cs:8`).

So `EnableMemoryBudget` as #218 words it is **inert**: it would make VMA track a
budget that no wrapper API can read.

### E12. The loader bootstrap `Allocator.Create` uses is private, and cannot be avoided

`Allocator.LoadVulkanLoader` (`Memory/Allocator.cs:342-364`) is `private static`,
and its comment states the reason the wrapper re-loads the loader DLL at all:
"VMA needs raw function pointers and `[DllImport]` static methods don't expose
theirs (CS8757)". `ahjo_ngx_vulkan_init_utf8` needs exactly the same two pointers
(`Generated/NgxApi.cs:81`).

The generated NGX signature types them `delegate* unmanaged[Cdecl]` (parsed with the
fixed Linux target; `NVSDK_CONV` is `__cdecl` on MSVC and empty on GCC,
`nvsdk_ngx_defs.h:42-46`) where VMA's are `delegate* unmanaged[Stdcall]`. On the two
RIDs NGX ships for — both x86-64 — those are the same ABI, and in any case the
wrapper obtains the pointer as an `nint` from `NativeLibrary.GetExport` and casts
once, so no conversion between the two delegate types occurs.

### E13. `CommandRecorder` caches no bound state, and `ref CommandRecorder` is the established parameter shape

The whole field set is `_pool`, `Handle`, `_ended`, `_retired`
(`Recording/CommandRecorder.cs:47-50`). There is no bound-pipeline, bound-descriptor
or dynamic-state cache to invalidate after an NGX evaluate clobbers them
(guide §5.2.5). The contract is therefore purely documentary today — and stays
correct only as long as that field set stays that way.

`ref CommandRecorder` is what the repo already does at five in-`src` sites:
`Queue.Submit2` (`Lifecycle/Queue.cs:106`), `Queue.ImmediateSubmit`'s delegate
(`Recording/ImmediateRecord.cs:16`), `Queue.cs:148`, `Rendering/FrameContext.cs:87`,
`:131`, `:164`. `tests/Ahjo.Vulkan.Tests/ScopedSpanProbe.cs:37-72` proves that a
`ref CommandRecorder` *parameter* still lets the callee `stackalloc` freely — the
#209 safe-context problem was about the receiver of a non-`readonly` member, not
about parameters.

### E14. The CI coverage summary reads one trx, and every skip must carry a `[gate:*]` class

`.github/workflows/ci.yml:203-208` parses `TestResults/wrapper.trx` only — the
`Ahjo.Vulkan.Slang.Tests` step (`:171-173`) runs with no trx logger at all. But
`:226-232` and `:281-285` fail the job on any skip in that trx whose reason lacks a
`[gate:<class>]` prefix, and `tests/CLAUDE.md` states the rule for every suite:
"New gates go through `Ahjo.Vulkan.Testing.TestGate`, never a bare `Assert.Skip`."

The existing classes and their gap semantics (`ci.yml:219`):
`driver`/`hardware`/`validation` are tier-aware gaps (a non-zero count at or above
the declared tier **fails** the job as "miswired"), `spirv` is a toolchain gap, and
`platform` / `feature` are permanent-and-correct (`needs = -1`, never a gap, never a
miswire). `TestGate.RequirePlatform` and `TestGate.RequireDeviceFeature`
(`tests/Shared/TestGate.cs:47-53`) are the two that fit an optional vendor SDK and an
optional vendor GPU.

`Ahjo.Vulkan.Slang.Tests` is the precedent for a wrapper-over-native suite: it links
`..\Shared\*.cs` (`Ahjo.Vulkan.Slang.Tests.csproj:29`) and gates its one device test
on `TestGate.RequireDriver`.

### E15. The benchmark split precedent is explicit, and `[GlobalSetup]` throws rather than skips

`MeshShaderBenchmarks` (`tests/Ahjo.Vulkan.Benchmarks/MeshShaderBenchmarks.cs:18-31`)
documents the rule in its own remarks: "Deliberately a separate class … this
`Setup` requires an optional device extension … and a host without them must not
take the issue-29 canary down with it", and `:124-127` states the stance —
"`[GlobalSetup]` throws rather than silently skipping".
`.claude/agents/bench-coverage-checker.md:46` says the same about
`DescriptorSetPoolVariableCountBenchmarks`: "split out because its `[GlobalSetup]`
requires a device advertising `descriptorBindingVariableDescriptorCount` and would
otherwise take the #114 canary down on a host without it. Listing only the first lets
the second rot unnoticed."

`docs/benchmarks.md:101-105` shows the row format, including the
"needs `VK_EXT_mesh_shader` — see the driver-dependency caveat" annotation that a
host-gated row carries.

### E16. Pointer-carrying structs cannot be `record struct`s

A synthesized record `Equals` compares each field through
`EqualityComparer<TField>.Default`, and a struct containing pointer fields cannot be
a generic type argument. `ImageViewDescription` is a `readonly record struct`
(`Memory/ImageViewDescription.cs:34`) precisely because it holds no pointers;
`Image` and `ImageView` are plain `readonly unsafe struct`s
(`Resources/Image.cs:25`, `Resources/ImageView.cs:19`). Any Phase 2 type that
carries a `VkImage_T*` must follow the second form, and the ones that do not may use
the first — including the `= 1f` field initializers plus explicit parameterless
constructor that `ImageViewDescription` uses for valid-by-default (`:69`, CS8983).

## Decision

Thirteen decisions. D2, D3, D4 and D9 are the load-bearing ones — the first three
are the answers to the carried-over invariants, and the fourth is the
zero-allocation shape.

### D1. Package shape mirrors `Ahjo.Vulkan.Slang`; namespace is `Ahjo.Vulkan.Ngx`

`src/Ahjo.Vulkan.Ngx` is the eighth package. It `ProjectReference`s
`Ahjo.Vulkan`, `Ahjo.Vulkan.Ngx.Native` and (explicitly, for `VkImage_T*` and
friends) `Ahjo.Vulkan.Native`, exactly as
`src/Ahjo.Vulkan.Slang/Ahjo.Vulkan.Slang.csproj:34-36` does for its pair. It ships
**no native files** — the shim rides in `Ahjo.Vulkan.Ngx.Native` and the feature DLL
is the consumer's, per the fixed #214 decision. `Ahjo.Vulkan` gains no dependency in
the other direction.

Root namespace is `Ahjo.Vulkan.Ngx`. Note the #166 lesson recorded at
`tools/generate-slang.rsp:19-21` and applied in #216 D7: `Ahjo.Vulkan.Ngx.Native`'s
generated entry-point class is `NgxApi`, *not* `Ngx`, precisely so a type name in
this namespace cannot shadow it.

The publish step is a copy of the Slang wrapper step
(`.github/workflows/publish.yml:331-341`) carrying `SkipVmaNativeBuild=true` and
`SkipNgxNativeBuild=true`.

#### Why not the alternatives

- **Fold the wrapper into `Ahjo.Vulkan.Ngx.Native`.** One package instead of two,
  but it puts an idiomatic API in the same assembly as the raw bindings and breaks
  the shape #166 established for exactly this pairing. Rejected.
- **Put DLSS in `Ahjo.Vulkan` itself.** Would give the core package a hard
  dependency on a proprietary NVIDIA-only SDK for a feature most consumers never
  enable. Rejected by #214 and restated here.

### D2. Invariant (a) — the view/image/range triple is made **unrepresentable** by `NgxImage`

`NVSDK_NGX_ImageViewInfo_VK` has exactly one producer in this design:

```csharp
public readonly unsafe struct NgxImage
{
    public static NgxImage CreateView(Device device, in Image image, in ImageViewDescription view);
    public static NgxImage Wrap(in Image image, in ImageView view, in ImageViewDescription viewDescription);
}
```

Both factories resolve the subresource range from **the same `ImageViewDescription`
that describes the view**, using the identical field-to-range mapping
`Image.CreateView` uses (`Resources/Image.cs:127-134`); `CreateView` additionally
*creates* the view from it, so there is nothing for a caller to get out of step.
`Format` falls back to `image.Format` on `VK_FORMAT_UNDEFINED` the same way
`Image.CreateView` does (`:139`), and `Width`/`Height` come off the `Image`.
`ReadWrite` is not a member (D3).

`CreateView` owns the view it made and destroys it on `Dispose`; `Wrap` borrows and
`Dispose` is a no-op — the `OwnsHandle` split `Image` and `ImageView` already use
(`Resources/Image.cs:97`, `Resources/ImageView.cs:39`), exposed here as `OwnsView`.

`VK_REMAINING_MIP_LEVELS` / `VK_REMAINING_ARRAY_LAYERS`, which
`ImageViewDescription` defaults to (`Memory/ImageViewDescription.cs:56`, `:64`), are
**resolved to concrete counts** against `image.MipLevels` / `image.ArrayLayers`
before the range reaches NGX. Nothing documents how the feature DLL consumes that
range, and a concrete count is exactly equivalent for every Vulkan use of a
subresource range; `Image.FromRaw` reports 1/1 (`Resources/Image.cs:84-85`), which
is the correct answer for a swapchain image. This is an inference, not a
measurement — see OPEN-4.

`CreateView` is the documented default and what the tests use. `Wrap` exists because
a renderer usually already has an attachment view for the colour/depth/MV targets
and creating a second is pure waste; its doc comment states the one contract the
compiler cannot check — *the description must be the one that created the view* —
and E2 is why: nothing can recover a `VkImageView`'s range to verify it.

#### Why not the alternatives

- **Take `(Image, ImageView, VkImageSubresourceRange)` as three parameters**
  (the literal reading of `NVSDK_NGX_ImageViewInfo_VK`). Every call site can
  disagree with itself and nothing can detect it (E2). Rejected — this is the
  invariant the issue asked to make unrepresentable.
- **Add the range/format/extent to `Ahjo.Vulkan`'s `ImageView`.** Would fix it for
  everyone, but it grows a two-pointer hot handle to seven fields for one
  satellite package's benefit, and `ImageView` is copied into descriptor writes and
  attachment structs all over `Recording/`. Rejected on blast radius; revisit if a
  second consumer ever needs view metadata.
- **`NgxImage` always creates and owns its view.** Simplest and fully
  unrepresentable, but forces a second `VkImageView` per DLSS-bound target on every
  renderer that already has one. Rejected in favour of `CreateView` + a documented
  `Wrap`.

### D3. Invariant (b) — `ReadWrite` never crosses the public API, and the usage bit is validated where it is knowable

`NVSDK_NGX_Resource_VK.ReadWrite` is set by the wrapper from the **slot**, never by
the caller: `Color`, `Depth`, `MotionVectors`, `ExposureTexture` and
`BiasCurrentColorMask` get `false`; `Output` gets `true`. It is written as C# `true`,
not `1` — Phase 1 measured the field as `bool`
(`Generated/NVSDK_NGX_Resource_VK.cs:11-12`, spec #216 E11), against #214's prose.
There is no `NgxImage.ReadWrite` member and no overload that accepts one, so
"`ReadWrite = true` on an image without `VK_IMAGE_USAGE_STORAGE_BIT`" is not a
sentence this API can say.

What remains checkable is the image's usage, and `Image` carries it
(`Resources/Image.cs:36`). Under `AhjoValidation.Enabled`, `Evaluate` checks:

- `Output.Usage` includes `ImageUsage.Storage`;
- `Color.Usage`, `Depth.Usage`, `MotionVectors.Usage` (and the optional inputs, when
  present) include `ImageUsage.Sampled`;
- `ImageUsage.None` — the `Image.FromRaw` case (E3) — is treated as **unknown and
  skipped**, not as a failure. The message on a real failure names the slot, the
  usage the image was created with, and the bit that is missing.

**Amendment (2026-09-04, measured):** the output image also needs
`VK_IMAGE_USAGE_TRANSFER_DST_BIT`, and nothing in NVIDIA's headers or guide says
so — the validation layer does. DLSS clears the output itself with
`vkCmdClearColorImage` (`VUID-vkCmdClearColorImage-image-00002`), observed on an
RTX 4070 Ti / driver 610.47. It is named in the `Output`/`Storage` failure
message and documented on `DlssEvaluateInputs.Output`, but **not enforced**: one
driver version cannot establish "DLSS always clears", and a check that turns a
working configuration into a hard failure on that evidence is worse than a
sentence in the message.

#### Why not the alternatives

- **Expose `ReadWrite` and validate it.** Keeps the native shape visible, but a
  validation check is off in Release by default (`Diagnostics/AhjoValidation.cs:57-62`)
  whereas an absent API member is off in every configuration. Rejected.
- **Reject `ImageUsage.None` outright.** Would make every swapchain-image output
  throw. Rejected; the third state is named instead.
- **Query the usage back from the driver.** Vulkan has no `vkGetImageUsage`.
  Not available.

### D4. Invariant (c) — image layout is a **documented contract**, and that is stated as a limitation rather than papered over

The wrapper cannot hold it, and the reason is a deliberate prior decision, not an
oversight: `Resources/Image.cs:19-24` states that "layout tracking is deliberately
not on this struct. Layout is a pipeline-stage concern owned by the recorder
(issue 17 — pipeline barriers); pushing it onto the handle would either lie …
or force every consumer to thread the layout through their data flow."
`CommandRecorder` tracks nothing either (E13). There is therefore no value the
wrapper could compare against, and no barrier it could emit — a barrier needs
`oldLayout`, which only the caller knows.

So the design does three things and claims no more:

1. **Documents the requirement precisely** on `DlssEvaluateInputs` and on
   `DlssFeature.Evaluate`: inputs in `VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL` (or
   another shader-read layout), output in `VK_IMAGE_LAYOUT_GENERAL`, per guide §3.4;
   DLSS transitions internally and restores those states before returning; the
   evaluate must be recorded **outside** any `BeginRendering` scope.
2. **Validates the two halves that *are* knowable** — the usage bits of D3, which
   are the necessary preconditions for those layouts.
3. **Makes the failure legible.** A non-`Success` from `EvaluateFeature_C` is
   turned into an `NgxException` carrying the `NVSDK_NGX_Result`, its
   `ahjo_ngx_result_to_utf8` text (formatted into a `stackalloc byte[128]`, #216 D2)
   and a reminder of the layout contract — `FAIL_RWFlagMissing`,
   `FAIL_UnsupportedInputFormat` and `FAIL_MissingInput`
   (`Generated/NVSDK_NGX_Result.cs:12-14`, `:17`) are the results this class of
   mistake produces.

**Amendment (2026-09-04):** this decision paid for itself immediately. Running
the hardware suite with the layer installed — and with the suite *failing* on any
layer error, rather than merely logging it — is what found the undocumented
`VK_IMAGE_USAGE_TRANSFER_DST_BIT` requirement on the output image
(`VUID-vkCmdClearColorImage-image-00002`, DLSS clearing its own output). Nothing
in the wrapper, the headers or the guide could have produced that. The lesson
generalizes: for this package the layer is not a nice-to-have during
development, it is the specification of last resort, and the hardware suite
should keep asserting on it.

Recording this as "cannot be enforced" is the finding. The alternative — inventing a
layout-tracking side-table inside `Ahjo.Vulkan.Ngx` — would be a second, divergent
source of truth for something #17 decided belongs to the recorder.

#### Why not the alternatives

- **Track layouts in `NgxImage`.** A DLSS-local layout cache would be wrong the
  moment any other code path transitions the image, and it would be the only layout
  tracker in the repo. Rejected.
- **Make `DlssEvaluateInputs` carry a declared `VkImageLayout` per slot and
  validate it is one NGX accepts.** Adds six fields and still cannot detect the
  actual bug (a caller who states the right layout and forgets the barrier).
  Rejected as ceremony that buys nothing.
- **Emit the barriers from `Evaluate`.** Needs `oldLayout` per image, which only the
  caller knows; passing it in is the previous alternative wearing a hat. Rejected.

### D5. `NgxContext` — one per `Device`, created from a `Device` alone, with a dedicated missing-DLL exception

```csharp
public sealed class NgxContext : IDisposable
{
    public static NgxContext Create(Device device, in NgxDescription description);
    public bool IsSuperSamplingAvailable { get; }
    public DlssOptimalSettings GetOptimalSettings(uint outputWidth, uint outputHeight, DlssQualityMode mode);
    public bool TryGetStats(out DlssStats stats);
    public DlssFeature CreateDlss(ref CommandRecorder recorder, in DlssFeatureDescription description);
    public void Dispose();
}
```

`Create` loads the Vulkan loader itself (E12 — the `Allocator.Create` bootstrap is
private and `[DllImport]` statics yield no function pointer), keeps the OS handle for
the context's lifetime and frees it in `Dispose`, mirroring `Allocator.Loader`
(`Memory/Allocator.cs:36-40`, `:340`). It fills an `AhjoNgxInitInfo` with
`StructSize = (uint)sizeof(AhjoNgxInitInfo)` — the shim rejects a mismatch with
`FAIL_InvalidParameter` (#216 D2) — and calls `ahjo_ngx_vulkan_init_utf8`. Every
string is encoded into one caller-owned UTF-8 block with explicit NUL terminators
and released when `Create` returns, which E5 shows is sufficient.

After a successful `Init` it takes **one capability parameter map**
(`GetCapabilityParameters`, E10), destroys it in `Dispose` with `DestroyParameters`
(`nvsdk_ngx_vk.h:398-400`), and reads the DLSS availability triple from it:
`SuperSampling.Available`, `SuperSampling.NeedsUpdatedDriver`,
`SuperSampling.FeatureInitResult`, plus `SuperSampling.MinDriverVersionMajor/Minor`
(`NgxApi.cs:111`, `:132`, `:201`, `:156`, `:180`).

**That triple, not `Init`'s return code, is where a missing `nvngx_dlss.dll`
shows up.** `Create` therefore throws:

- `NgxFeatureLibraryNotFoundException` when `SuperSampling.Available == 0` and
  `FeatureInitResult == NVSDK_NGX_Result_FAIL_FeatureNotFound` (or `Init` itself
  returned it). Message names the expected file
  (`nvngx_dlss.dll` / `libnvidia-ngx-dlss.so`), every directory searched — the
  process directory plus each `NgxDescription.DlssSearchPaths` entry, listed — and
  the docs page. Never a silent fallback.
- `NgxDriverTooOldException` when `NeedsUpdatedDriver != 0`, naming the required
  major/minor from the capability map.
- `NgxException` otherwise, carrying the result and its UTF-8 text.

Dispose order is `DestroyParameters` → `NVSDK_NGX_VULKAN_Shutdown1(device)` →
`NativeLibrary.Free(loader)`.

#### Amendment (2026-09-04, measured on RTX 4070 Ti / driver 610.47)

Two prerequisites this design took to be advisory turned out to be hard, and both
were found the first time the code met real hardware. They are recorded here
rather than as OPEN entries because they are settled, not open.

1. **`ApplicationDataPath` must never reach NGX as null.** The SDK documents the
   field as optional and the plan's step 6 described `null` as "process temp path
   chosen by NGX". It is not:
   `NVSDK_NGX_VULKAN_GetFeatureRequirements` dereferences it unconditionally, and
   a null produces an **access violation** inside NVIDIA's client library - not a
   failure result, and not something any managed `catch` can turn into a skip.
   `NgxDescription` therefore materializes `Path.GetTempPath()` itself when the
   caller leaves it unset, so the documented behaviour is delivered by the
   wrapper rather than trusted to the SDK.
2. **Both extension lists are mandatory, and in order.** An instance created
   without the names `TryGetInstanceExtensions` returns makes every
   instance-taking NGX entry point access-violate the same way: NGX resolves
   `vkGetPhysicalDeviceProperties2KHR` through `vkGetInstanceProcAddr`, the
   loader answers null unless `VK_KHR_get_physical_device_properties2` was
   enabled, and NGX does not null-check it. A *device* created without the names
   `TryGetDeviceExtensions` returns fails more quietly and more misleadingly:
   `Init` succeeds, and the capability map then reports
   `SuperSampling.Available = 0` with `FAIL_PlatformError`, which reads exactly
   like an unsupported GPU. The required order is: instance extensions ->
   instance -> physical device -> device extensions -> device ->
   `NgxContext.Create`. It is documented on `NgxSupport` and on
   `NgxContext.Create`, and it is what both the hardware suite and the benchmark
   do.

**Logging** goes through one `[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]`
static (the `Instance.DefaultCallback` shape, `Lifecycle/Instance.cs:593-619`,
including the `catch { }` that guarantees nothing throws across the boundary) which
converts the `const char*` message and forwards it to `AhjoDiagnostics.Sink`.
Cdecl matches the generated field type (`Generated/AhjoNgxInitInfo.cs:31-32`) and
`NVSDK_CONV` (E12).

**Thread affinity.** The NGX API is not thread-safe (guide §5.2.4) and the
capability map is mutable shared state (E10). `NgxContext`, `GetOptimalSettings`,
`CreateDlss` and `DlssFeature.Evaluate` therefore carry an
`AhjoValidation.Enabled`-gated re-entrancy guard: an `Interlocked.CompareExchange`
on one `int` per context, released in a `finally`, that throws
`AhjoValidationException` naming both operations when a second thread is inside.
With validation off it is a single predictable branch on a `bool` — the cost model
`Diagnostics/AhjoValidation.cs:41-46` already commits to.

#### Why not the alternatives

- **`Create(Instance, Device, …)`.** Avoids touching `Ahjo.Vulkan` (D12) but lets a
  caller pass an instance the device did not come from, which is a class of error
  the type system can close. Rejected — see D12.
- **Treat `Init` returning `Success` as "DLSS works".** It does not: the feature DLL
  is found at `CreateFeature1` time, and the capability map is the documented probe.
  Rejected as the exact silent-fallback the issue forbids.
- **A logging `Action<string>` on `NgxDescription`.** A managed delegate reachable
  from a native callback needs a `GCHandle` and the AOT invariant prefers a static
  sink; `AhjoDiagnostics.Sink` is already the repo's installable hook. Rejected.

### D6. `NgxSupport` + `NgxExtensionSet` — extension names are **copied**, never aliased into NGX storage

```csharp
public static class NgxSupport
{
    public static bool TryGetInstanceExtensions(in NgxDescription description, out NgxExtensionSet extensions);
    public static bool TryGetDeviceExtensions(PhysicalDevice physicalDevice, in NgxDescription description, out NgxExtensionSet extensions);
    public static bool IsSuperSamplingSupported(PhysicalDevice physicalDevice, in NgxDescription description);
    public static bool TryGetSuperSamplingRequirements(PhysicalDevice physicalDevice, in NgxDescription description, out DlssRequirements requirements);
}

public sealed class NgxExtensionSet : IDisposable
{
    public ReadOnlySpan<Utf8Name> Names { get; }
}
```

`NgxExtensionSet` copies each `VkExtensionProperties.extensionName` into one
`NativeMemory.Alloc` block with an explicit NUL terminator and builds a `Utf8Name[]`
of pointers into it. E8 is why: NGX's array has no documented lifetime and
`Utf8Name`'s contract (`Lifecycle/Utf8Name.cs:28-35`) demands stable, non-movable
storage. `Names` drops straight into `InstanceDescription.Extensions` /
`DeviceDescription.Extensions`, both `ReadOnlySpan<Utf8Name>`
(`Lifecycle/InstanceDescription.cs:21`, `Lifecycle/DeviceDescription.cs:14`), and
composes with the caller's own names through a collection expression. `Dispose`
frees the block; the names only need to outlive `vkCreateInstance` /
`vkCreateDevice`, which copy them.

`TryGetInstanceExtensions` is a pre-instance static query and takes no Vulkan
object — CI measured it identical on a driverless `windows-latest` runner and on an
RTX 4070 Ti / driver 610.47 (#216 OPEN-1, resolved). `TryGetDeviceExtensions`,
`IsSuperSamplingSupported` and `TryGetSuperSamplingRequirements` all need a
`VkInstance` (`Generated/NgxApi.cs:84`, `:90`) and are therefore **not callable on
a driverless lane** — #216's finding 4, restated so Phase 2 does not re-derive it.

`DlssRequirements { bool IsSupported, NgxFeatureSupport Reason, uint MinimumArchitecture, string MinimumOsVersion }`
projects `NVSDK_NGX_FeatureRequirement` (`Generated/NVSDK_NGX_FeatureRequirement.cs`),
whose `MinOSVersion` is a `char[255]` inline array read as UTF-8.

The conversion from `ReadOnlySpan<VkExtensionProperties>` to `NgxExtensionSet` is an
`internal static` seam so CI can exercise it with a fabricated array — see D13.

#### Why not the alternatives

- **Return `Utf8Name[]` pointing into NGX's own array** (the issue's literal
  wording). Undocumented lifetime, and a `Utf8Name` that outlives its storage is
  the bug class `Utf8Name.FromLiteral`'s doc comment exists to prevent. Rejected.
- **Copy into a `byte[]` and pin it.** GC-movable; `Utf8Name` explicitly forbids it.
  Rejected.
- **Allocate the copies and never free them** (the shim's D8 retain-forever posture).
  Defensible for `Init`, wrong here: a settings screen that re-queries leaks per
  call. Rejected.

### D7. Three shadow enums, drift-tested member by member; presets carry only the letters NVIDIA documents

`DlssQualityMode`, `DlssPreset` and `DlssFeatureFlags` are hand-written with
hand-copied values and a per-member `Assert.Equal` drift test, the
`ShadowEnumDriftTests` pattern (`tests/Ahjo.Vulkan.Tests/ShadowEnumDriftTests.cs:24-56`)
— no reflection, so the failure message names the offending member.

- `DlssQualityMode` ← `NVSDK_NGX_PerfQuality_Value` (`Generated/NVSDK_NGX_PerfQuality_Value.cs`):
  `MaxPerformance`, `Balanced`, `MaxQuality`, `UltraPerformance`, `UltraQuality`, `Dlaa`.
- `DlssFeatureFlags` ← `NVSDK_NGX_DLSS_Feature_Flags` (`Generated/NVSDK_NGX_DLSS_Feature_Flags.cs`):
  `None`, `Hdr` (`IsHDR`), `MotionVectorsLowRes` (`MVLowRes`),
  `MotionVectorsJittered` (`MVJittered`), `DepthInverted`, `AutoExposure`,
  `AlphaUpscaling`. `DoSharpening` is **omitted** — sharpening is deprecated (#214,
  guide §3.5) — as are `IsInvalid` and the two `Reserved_*` members.
- `DlssPreset` ← `NVSDK_NGX_DLSS_Hint_Render_Preset` (`Generated/NVSDK_NGX_DLSS_Hint_Render_Preset.cs`):
  `Default`, `E`, `F`, `G`, `J`, `K`, `L`, `M`, `N`, `O`. The two
  `*_Reserved` members (`H`, `I`) are omitted; `E`/`F` are kept but documented as the
  deprecated CNN presets (#214).

A fourth, `NgxLoggingLevel` ← `NVSDK_NGX_Logging_Level`, is drift-tested with the
rest. Omissions are asserted too: one test per enum pins the **count** of shadowed
members so a future SDK bump that adds a member is a visible decision rather than a
silent gap.

#### Why not the alternatives

- **Alias the generated enums** (`using DlssQualityMode = …NVSDK_NGX_PerfQuality_Value;`).
  Leaks `NVSDK_NGX_`-prefixed member names into the public API and forecloses
  omitting deprecated members. Rejected — and it is the opposite of what every other
  shadow enum in the repo does.
- **Generate the drift test by reflection.** Cheaper to write, trim-hostile, and the
  failure message stops naming the member. Rejected per #122.

### D8. `GetOptimalSettings` returns six dimensions plus availability; 0×0 is reported as unavailable

```csharp
public readonly record struct DlssOptimalSettings
{
    public bool IsAvailable          { get; init; }
    public uint RenderWidth          { get; init; }
    public uint RenderHeight         { get; init; }
    public uint MinRenderWidth       { get; init; }
    public uint MinRenderHeight      { get; init; }
    public uint MaxRenderWidth       { get; init; }
    public uint MaxRenderHeight      { get; init; }
}
```

Six dimensions, not the issue's four: the dynamic-resolution range NGX returns is two
independent 2-D extents
(`DLSS.Get.Dynamic.{Min,Max}.Render.{Width,Height}`, `NgxApi.cs:681-690`) and
collapsing each to one number would either drop the aspect or invent one. Deviation
from #218's wording, recorded here.

The implementation is a managed transcription of `NGX_DLSS_GET_OPTIMAL_SETTINGS`
(`nvsdk_ngx_helpers.h:64-113`) against the **capability** map (E10): fetch the
callback with `GetVoidPointer`, cast to
`delegate* unmanaged[Cdecl]<NVSDK_NGX_Parameter*, NVSDK_NGX_Result>`, set
`Width`/`Height`/`PerfQualityValue`/`RTXValue`, invoke, read the six outputs,
seeding min/max from the optimal pair first exactly as the helper does (older feature
DLLs leave the dynamic keys unset). A null callback maps to
`NgxException(FAIL_OutOfDate)` with the header's own two causes in the message.
`Sharpness` is read and **discarded** — sharpening is deprecated.

`RenderWidth == 0 || RenderHeight == 0` sets `IsAvailable = false` and leaves the
other five at zero: a 0×0 render target is not a size, it is the mode saying "not on
this GPU" (#214, guide §5.2.8). `Dlaa` returns render == output, which is a
property of NGX's answer and is asserted in the hardware test, not synthesized.

#### Why not the alternatives

- **Throw when a mode is unavailable.** A settings screen enumerates all six modes;
  an exception per unavailable one turns a normal query into control flow.
  Rejected.
- **Return the optimal size with `IsAvailable = false`.** Invites a caller who
  ignores the flag to allocate a zero-size target. Rejected; the fields stay zero.
- **Add a `Sharpness` field.** Deprecated upstream; exposing it invites use.
  Rejected.

### D9. `DlssFeature.Evaluate` — one method fills the map and calls `EvaluateFeature_C`, and allocates nothing

```csharp
public sealed class DlssFeature : IDisposable
{
    public void Evaluate(ref CommandRecorder recorder, in DlssEvaluateInputs inputs);
    public uint RenderWidth  { get; }   // …plus Output*/Min*/Max* from creation
    public void Dispose();
}
```

Four properties hold the zero-allocation guarantee, each resting on a Phase 1
measurement (#216 D9):

1. **The resource structs are stack locals of `Evaluate` itself.** Up to six
   `NVSDK_NGX_Resource_VK` values (56 bytes each, blittable, `Generated/…:E10`) are
   built in `Evaluate`'s own frame and handed to `SetVoidPointer`. E6 is why this
   *has* to be one method: the map retains the pointers, so a public "prepare" step
   would leave the map holding dead stack addresses.
2. **Parameter names are `static readonly Utf8Name` fields** in an internal
   `NgxParameterNames` class, each `Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_*)`
   (E7). No `fixed`, no pinning, no per-call pointer derivation — the JIT reads a
   static field.
3. **The parameter map is created once**, at feature creation, with
   `AllocateParameters`, and reused every frame. `DestroyParameters` runs in
   `Dispose` after `ReleaseFeature`.
4. **`ref CommandRecorder`**, matching the five existing in-`src` call sites (E13);
   the command buffer is `recorder.RawHandle` cast to `VkCommandBuffer_T*`.
   `Evaluate` records only — it never calls `End` — so a `readonly` member would
   also work; `ref` is chosen for consistency with `Queue.Submit2` and the
   `ImmediateRecord` delegate, and E13 shows it costs the caller no safe-context.

The parameter set written per frame is the DLSS subset of
`NGX_VULKAN_EVALUATE_DLSS_EXT` (`nvsdk_ngx_helpers_vk.h:145-229`): the four required
resources, the two optional ones, jitter, reset, MV scale (defaulting to 1.0 the way
the helper does), pre-exposure and exposure scale (likewise), the render-subrect
dimensions and the subrect bases. The G-buffer / research-only slots
(`GBuffer.*`, `MotionVectors3D`, `IsParticleMask`, `AnimatedTextureMask`,
`DepthHighRes`, `Position.ViewSpace`, `RayTracingHitDistance`,
`MotionVectorsReflection`, `TonemapperType`, `FrameTimeDeltaInMsec`) are **not**
written: they are documented as research-only, the wrapper never sets them, and
setting them to null on every frame would be ~11 wasted native calls per frame.
`Sharpness` is not written (deprecated). The debug overlay keys are not exposed.

`CreateDlss(ref CommandRecorder, in DlssFeatureDescription)` transcribes
`NGX_VULKAN_CREATE_DLSS_EXT1` (`nvsdk_ngx_helpers_vk.h:113-135`) with `InDevice`
non-null so it takes the multi-device `CreateFeature1` path, sets the per-mode
`DLSS.Hint.Render.Preset.*` key matching `description.Mode` before creating, and
documents that **the recorder must be submitted and completed before the first
`Evaluate`** — `CreateFeature1` records initialization work.
`DlssFeatureDescription.FreeMemoryOnRelease` (default `false`, NGX's own behaviour)
writes `FreeMemOnReleaseFeature = 1` into the map before `ReleaseFeature` when set
(guide §3.14).

`DlssEvaluateInputs` and `NgxImage` are plain `readonly struct`s, **not**
`record struct`s — they carry pointers (E16). `DlssEvaluateInputs` uses
`{ get; init; }` plus an explicit parameterless constructor so the valid-by-default
initializers (`MotionVectorScaleX/Y = 1f`, `PreExposure = 1f`, `ExposureScale = 1f`)
run, the `ImageViewDescription` pattern (`Memory/ImageViewDescription.cs:69`).
`DlssFeatureDescription`, `DlssOptimalSettings`, `DlssStats`, `DlssRequirements` and
`NgxDescription` carry no pointers and are `readonly record struct`s.

Under `AhjoValidation.Enabled`, `Evaluate` additionally checks: all four required
slots are non-null; output ≥ 32×32 (guide §3.3); the render subrect dimensions lie
within the feature's `[Min, Max]` range; plus the usage checks of D3. Every message
names the slot.

#### Why not the alternatives

- **`Prepare(inputs)` + `Evaluate()`.** Would let a caller reuse a prepared map
  across frames — and would leave six dangling `void*`s in it (E6). Rejected as
  unsound, not merely inelegant.
- **Heap-allocate the resource structs on the feature.** Removes the lifetime
  hazard, but a fixed native block per feature is state to keep in sync with the
  inputs and buys nothing the stack does not. Rejected.
- **Write all ~45 helper parameters including the research slots.** Bit-for-bit
  fidelity to NVIDIA's helper, at ~11 extra native calls per frame for keys DLSS
  ignores. Rejected; the omission is documented.
- **Take the command buffer as a raw `nint`.** Sidesteps `ref CommandRecorder`
  entirely, and abandons the wrapper's vocabulary at exactly the call the renderer
  makes most. Rejected.

### D10. Errors: `NgxException` hierarchy, and an `AhjoValidation` protocol re-implemented rather than granted

Exceptions live in `Ahjo.Vulkan.Ngx`:

- `NgxException : Exception` — carries `public NVSDK_NGX_Result Result`. Message is
  `"<operation> failed: <result-name> (0x…) — <ahjo_ngx_result_to_utf8 text>"`, the
  text decoded from a `stackalloc byte[128]` (#216 D2 gives the caller-buffer API
  precisely so this allocates nothing beyond the message string itself).
- `NgxFeatureLibraryNotFoundException : NgxException` — the missing-`nvngx_dlss.dll`
  case (D5).
- `NgxDriverTooOldException : NgxException` — `NeedsUpdatedDriver`.

Wrapper-contract violations throw the **wrapper's own**
`AhjoValidationException` (public, `Diagnostics/AhjoValidation.cs:15`), gated on the
**public** `AhjoValidation.Enabled` (`:69`) and reported through the **public**
`AhjoDiagnostics.Sink` (`Diagnostics/AhjoDiagnostics.cs:59`). E4 shows this
reproduces `AhjoValidation.Fail`'s exact behaviour in three lines with no
`InternalsVisibleTo`. A private `NgxValidation.Fail(string source, string message)`
in `Ahjo.Vulkan.Ngx/Internal/` is the single choke point, so the duplication is one
method, not one per call site.

#### Why not the alternatives

- **`InternalsVisibleTo("Ahjo.Vulkan.Ngx")` on `Ahjo.Vulkan`.** Grants a *published*
  package access to every internal of another published package to save three lines,
  and the two ship independently versioned. Rejected.
- **Make `AhjoDiagnostics.Write` and `AhjoValidation.Fail` public.** A defensible
  future move for satellite packages generally, but it is a public-API addition to
  the core package driven by one consumer, and #218 is not the issue to decide it in.
  Rejected here; noted as a candidate if a third satellite needs it.
- **Throw `InvalidOperationException`.** Loses the distinction
  `AhjoValidationException` exists to draw between wrapper misuse and driver error.
  Rejected.

### D11. VMA: `AllocatorDescription` carries `EnableMemoryBudget`, **and** `Allocator` gains the one query that makes it observable

Four coordinated pieces, because E11 shows the flag alone does nothing:

1. `Memory/AllocatorDescription.cs` — new `readonly record struct` with one member
   today, `bool EnableMemoryBudget`, documented as also requiring
   `VulkanExtensions.ExtMemoryBudget` in `DeviceDescription.Extensions`.
2. `Allocator.Create(Device)` keeps its signature and delegates to a new
   `Allocator.Create(Device, in AllocatorDescription)`, which ORs
   `VMA_ALLOCATOR_CREATE_EXT_MEMORY_BUDGET_BIT` into the existing
   `BUFFER_DEVICE_ADDRESS_BIT` (`Memory/Allocator.cs:111`). VMA's other prerequisite,
   `VK_KHR_get_physical_device_properties2`, is core from Vulkan 1.1 and the wrapper
   requires 1.3+ devices, so nothing more is needed.
3. `DeviceDescription` gains `public AllocatorDescription Allocator;`
   (`Lifecycle/DeviceDescription.cs`), `PhysicalDevice.CreateDevice` passes it to the
   `Device` constructor (`Lifecycle/PhysicalDevice.cs:642`), and `Device.Allocator`'s
   lazy creation (`Lifecycle/Device.cs:143-156`) hands it to `Allocator.Create`.
   Under `AhjoValidation.IsEnabled`, `CreateDevice` fails loudly when
   `EnableMemoryBudget` is set but `VK_EXT_memory_budget` is absent from
   `desc.Extensions` — a one-off linear scan of a handful of names at device
   creation, catching the two-step trap at the one place both facts are in scope.
4. `Allocator.GetHeapBudgets(Span<MemoryHeapBudget> destination) : int` — the reader.
   `stackalloc VmaBudget[16]` (`VK_MAX_MEMORY_HEAPS`), one `vmaGetHeapBudgets` call
   (`Generated/Vma.cs:33`), project into
   `MemoryHeapBudget { uint HeapIndex; uint BlockCount; uint AllocationCount; ulong BlockBytes; ulong AllocationBytes; ulong Usage; ulong Budget; }`,
   return the heap count. `Allocator` gains one `internal readonly uint HeapCount`
   captured in `Create` from `vkGetPhysicalDeviceMemoryProperties`, so the caller is
   not handed sixteen mostly-empty rows. Setup/diagnostic path, caller-provided span,
   zero managed allocation.

The extension name is added to the catalogue as `VulkanExtensions.ExtMemoryBudget`
(`Rendering/VulkanExtensions.cs`), with a doc comment saying what it buys —
DLSS allocates its history and scratch inside the driver where VMA cannot see it
(#214) — and `Memory/AllocationFlags.cs`'s `DedicatedMemory` gets a cross-reference
for the full-screen DLSS targets. **This is not a default change**:
`AllocatorDescription`'s default is `EnableMemoryBudget = false`, so
`Allocator.Create(device)` and every existing call site behave byte-identically.

Piece 4 is beyond #218's literal text — see **OPEN-1**.

#### Why not the alternatives

- **Ship the flag with no reader** (the issue's literal scope). Inert: no wrapper
  API observes a VMA budget (E11). Rejected — and the alternative is 60 lines.
- **`bool EnableMemoryBudget` directly on `DeviceDescription`.** One field instead
  of a new type, but it puts a VMA concept on the Vulkan device description with no
  room to grow the next allocator option. Rejected.
- **Auto-append `VK_EXT_memory_budget` to `DeviceDescription.Extensions` when the
  flag is set.** Removes the two-step trap, and silently changes `vkCreateDevice`'s
  inputs — on a device that does not support the extension, creation fails with no
  clue why. Rejected in favour of the validation check.
- **Set the budget bit unconditionally.** Requires the device extension everywhere;
  #214 already fixed this as opt-in. Rejected.

### D12. `PhysicalDevice.Instance` becomes public

One word (`internal` → `public`) at `Lifecycle/PhysicalDevice.cs:25`. It is the
missing edge from E1 and it lets `NgxContext.Create` take a `Device` alone, which
makes "an instance that does not match the device" unrepresentable rather than
documented. `Instance` is a public sealed class whose own handle stays internal, so
nothing else leaks; a physical device belonging to an instance is a fact the Vulkan
object model already states.

Because this touches `Ahjo.Vulkan`'s public API for a satellite's benefit, it is
recorded as **OPEN-2** for the approver.

#### Why not the alternatives

- **`NgxContext.Create(Instance, Device, …)`.** Zero change to `Ahjo.Vulkan`, and it
  reopens the mismatch class that D2/D3 spent effort closing elsewhere. Rejected,
  with the note that it is the fallback if OPEN-2 is declined.
- **`Device.Instance` instead.** Same information one hop further; `PhysicalDevice`
  is where the field already lives. Rejected as redundant.

### D13. Tests split by what each host can actually prove; the benchmark is its own class

**`tests/Ahjo.Vulkan.Ngx.Tests`**, linking `..\Shared\*.cs` like
`Ahjo.Vulkan.Slang.Tests` (E14), with `InternalsVisibleTo` from
`Ahjo.Vulkan.Ngx` for the two seams noted below.

*Runs everywhere, including a driverless CI runner with no NGX SDK staged* — these
touch no shim and no device:

- Shadow-enum drift, four enums, member by member plus a member-count pin (D7).
- `NgxDescription` validation: empty/whitespace `ProjectId`, a `ProjectId` that is
  not GUID-shaped, empty `EngineVersion`, a null entry in `DlssSearchPaths`.
- `DlssOptimalSettings` availability semantics for a 0×0 answer, over the internal
  projection seam.
- `NgxExtensionSet` construction from a **fabricated** `VkExtensionProperties[]`
  (internal seam): the names round-trip byte-for-byte, each `Utf8Name` points at a
  NUL-terminated copy, `Names.Length` matches, and `Dispose` is idempotent. This is
  the direct regression test for the class of bug PR #217 fixed on the shim side.
- `AhjoValidation`-gated checks that need no device: render size outside
  `[Min, Max]`, output below 32×32, a missing required slot — asserted over
  `default`-handle `NgxImage`s with `AhjoValidation.Enabled = true`.

*Runs on the SwiftShader CI runner (`TestGate.RequireDriver`)*:

- Extension-list plumbing: an `NgxExtensionSet` built from names the host actually
  advertises reaches `vkCreateDevice` through `DeviceDescription.Extensions` and the
  device is created. This proves the pointer/termination contract against a real
  loader without needing NGX.
- The `EnableMemoryBudget` pairing check throws when the extension is missing from
  `DeviceDescription.Extensions`, and `Allocator.GetHeapBudgets` returns
  `heapCount > 0` rows on a device that has the extension (skipped via
  `TestGate.RequireDeviceFeature` when SwiftShader does not advertise it).

*Requires the staged NGX SDK, i.e. skips in CI* —
`TestGate.RequirePlatform(NgxTestEnvironment.ShimPresent, …)`, reusing the existing
`[gate:platform]` class (E14) so no new class and no `ci.yml` edit is needed:

- `NgxSupport.TryGetInstanceExtensions` returns `Success` with a plausible set.
  (The native-level version of this assertion already runs in the `ngx-native` lane,
  `tests/Ahjo.Vulkan.Ngx.Native.Tests/NgxSmokeTests.cs:193`; this one proves the
  *wrapper's* copy path over it.)

*Requires NVIDIA hardware with a DLSS-capable driver and `nvngx_dlss.dll`* —
`TestGate.RequireDeviceFeature(NgxTestEnvironment.IsDlssAvailable, …)`, the
`[gate:feature]` class, which `ci.yml:219` treats as permanent-and-correct rather
than a coverage gap:

- End-to-end: create context → `IsSuperSamplingAvailable` → `GetOptimalSettings` for
  all six modes (asserting DLAA returns render == output and that an unavailable
  mode reports `IsAvailable = false` rather than 0×0 dimensions) → `CreateDlss` →
  submit and wait → `Evaluate` on real images → `Dispose`.
- `DlssStats` reports a non-zero VRAM figure after a feature exists.
- The missing-DLL diagnosis: with `DlssSearchPaths` pointed at an empty directory and
  no DLL beside the binary, `Create` throws `NgxFeatureLibraryNotFoundException`
  whose message contains the file name and each searched directory.

CI has no NVIDIA driver, so the last group never runs there and its
`[gate:feature]` classification says exactly that. Phase 2 is the first point at
which a real evaluate is possible, and the dev machine (RTX 4070 Ti, driver 610.47)
is where that evidence comes from — quoted in the PR, the way #217 quoted its
`nm -D` and `dumpbin` output.

**`DlssEvaluateBenchmarks`** is its own `[MemoryDiagnoser]` class, for the reason
`MeshShaderBenchmarks` and `DescriptorSetPoolVariableCountBenchmarks` are
(E15): its `[GlobalSetup]` requires an NVIDIA GPU, a DLSS-capable driver **and** a
consumer-supplied feature DLL, and it must not take the #29 canary
(`CommandRecorder.RenderingPass100Cmds`) down with it on any other host. Following
that precedent, `[GlobalSetup]` **throws** with an actionable message rather than
skipping — a filtered-in benchmark that silently measures nothing is worse than a
loud failure. Two methods:

- `Evaluate_16` — 16 `Evaluate` calls recorded into one command buffer per invoke,
  never submitted. 16 rather than the `*_1024` used elsewhere because one DLSS
  evaluate records many dispatches, not one command. The recorder is disposed
  **before** `CommandBufferPool.ResetForFrame`, the #188/#199 ordering that
  `docs/benchmarks.md:109` documents at length.
- `PackParameters_16` — the same 16 iterations through the internal
  parameter-population seam without `EvaluateFeature_C`, isolating exactly what the
  issue asks to measure: parameter-map population plus resource-struct fill. Both
  must read `Allocated: -`.

`docs/benchmarks.md` gains both rows with the host-gated caveat the mesh rows carry,
and `.claude/agents/bench-coverage-checker.md` gains a mapping row
`src/Ahjo.Vulkan.Ngx/DlssFeature.cs` → `DlssEvaluateBenchmarks.cs`.

#### Why not the alternatives

- **Put the DLSS benchmarks on `CommandRecorderBenchmarks`.** Exactly the mistake
  E15's two precedents were created to avoid. Rejected.
- **`[GlobalSetup]` that skips on a non-NVIDIA host.** BenchmarkDotNet has no skip;
  the class would report a fabricated zero. Rejected, consistent with
  `MeshShaderBenchmarks.cs:124-127`.
- **A new `[gate:ngx]` class.** Needs a matching edit to `ci.yml`'s `$needs` map for
  a class no trx the summary reads would ever contain — dead configuration.
  Rejected; `platform` and `feature` already carry the right semantics.
- **Run `Ahjo.Vulkan.Ngx.Tests` inside the `ngx-native` lane** (which has the shim).
  That lane's contract is "no loader, no ICD" (`tests/CLAUDE.md`), and
  `.github/CLAUDE.md` says not to grow it into wrapper coverage. Rejected; the suite
  runs in `build-test` and its shim-dependent tests skip there.

## Scope boundary

Phase 2 stops at the wrapper. Not designed here and not to be inferred from anything
above: `samples/HelloDlaa`, `docs/ngx-notes.md`, the Logos migration note (all Phase
3), Ray Reconstruction, Frame Generation, OTA preset updates
(`NVSDK_NGX_UpdateFeature`), and the vendor-neutral upscaler contract (#214 "Later").

Two things the *reader* of this spec should not expect to find in the code: a layout
tracker (D4), and any wrapper API that writes the DLSS debug-overlay parameters.

## OPEN

- **OPEN-1 — scope, needs the approver's call.** `Allocator.GetHeapBudgets` +
  `MemoryHeapBudget` + `Allocator.HeapCount` (D11 piece 4) are beyond #218's literal
  wording, which asks only for the `EnableMemoryBudget` flag. E11 shows the flag is
  inert without them: no public API in this repo reads a VMA budget.
  **Recommendation: include them.** If declined, `EnableMemoryBudget` still ships and
  is honestly documented as "sets the VMA flag; read the budget through
  `Ahjo.Vulkan.Vma.Native.Vma.vmaGetHeapBudgets` yourself for now", and the two
  budget tests in D13 drop.
- **OPEN-2 — scope, needs the approver's call.** Making `PhysicalDevice.Instance`
  public (D12) is a public-API addition to the core package driven by a satellite.
  **Recommendation: take it** — it closes a mismatch class. If declined, the fallback
  is `NgxContext.Create(Instance, Device, in NgxDescription)` and
  `NgxSupport.*(Instance, PhysicalDevice, …)`, with the mismatch documented.
- **OPEN-3 - RESOLVED 2026-09-04 by measurement. The string forms work; the two
  fields ship.**

  *The question.* `NGX_DLSS_GET_STATS_2` reads `OptLevel` and
  `IsDevSnippetBranch` through the excluded `NVSDK_NGX_EParameter_*` hash
  aliases (E9). Whether NGX's parameter map treats the plain string constants
  `"Snippet.OptLevel"` / `"Snippet.IsDevBranch"` (`NgxApi.cs:99`, `:102`) as
  equivalents was undocumented and unmeasured.

  *What was measured.* RTX 4070 Ti, driver 610.47, NGX `v310.7.0`, against the
  `rel/` `nvngx_dlss.dll`, read off the capability parameter map immediately
  after a successful `DLSSGetStatsCallback` invocation with one DLSS feature
  live:

  | Key | `NVSDK_NGX_Parameter_GetUI` result | Value |
  |---|---|---|
  | `"Snippet.OptLevel"` | `NVSDK_NGX_Result_Success` (`0x00000001`) | `40` = `NVSDK_NGX_OPT_LEVEL_RELEASE` |
  | `"Snippet.IsDevBranch"` | `NVSDK_NGX_Result_Success` (`0x00000001`) | `0` |
  | `"Snippet.NoSuchKeyAtAll"` *(negative control)* | `0xBAD00010` = `FAIL_UnsupportedParameter` | - |

  `SizeInBytes` read `322,265,826` bytes in the same call.

  *Why this is conclusive.* The control is what makes it so: the map does **not**
  answer `Success` for an arbitrary key, so `Success` on the two real ones means
  they were genuinely present. And both values are the *correct* answers rather
  than merely plausible ones - `40` is exactly the opt level of a `rel/` build,
  and `0` is exactly right for a non-dev-branch DLL.

  *What changed.* `DlssStats` gains `uint OptLevel` and
  `bool IsDevSnippetBranch`, read by name in `NgxContext.TryGetStats`. A
  non-`Success` on either leaves the field at its default rather than failing the
  query - a feature library that does not publish them is a degraded answer, not
  an error. The `DlssHardwareTests` end-to-end case asserts both, which
  incidentally guards against a watermarked `dev/` DLL being deployed. The
  prohibition on `NVSDK_NGX_EParameter_*` in `NgxParameterNames` is untouched:
  these are the string constants, not the hash family.

- **OPEN-4 — inference, not measurement.** D2 resolves
  `VK_REMAINING_MIP_LEVELS`/`VK_REMAINING_ARRAY_LAYERS` to concrete counts before the
  subresource range reaches NGX, because no NVIDIA documentation says how the feature
  DLL consumes that range. The resolved value is equivalent for every Vulkan use and
  strictly safer, so this is not a blocker — but if a DLSS resource binding is ever
  traced to a subresource-range fault, this is the first assumption to re-examine.
- **OPEN-5 — carried forward from #216 OPEN-2, still open.** Whether
  `NVSDK_NGX_VULKAN_AllocateParameters` succeeds before `Init` is unknown and
  deliberately unasserted. Phase 2 must not call it before a successful `Init`;
  `DlssFeature` only ever gets its map from an initialized `NgxContext`, which
  satisfies this by construction. Do not add a probe on a guess.
- **OPEN-7 — NEW, found during implementation; needs the architect's call.**
  D2 resolves `VK_REMAINING_*` against `Image.FromRaw`'s 1/1 and calls that
  correct, which it is — but the same paragraph takes `Width`, `Height` and
  `Format` off the `Image` too, and for a `FromRaw` handle those are `0`, `0`
  and `VK_FORMAT_UNDEFINED` (E3). NGX **reads** all three out of
  `NVSDK_NGX_ImageViewInfo_VK`, so unlike `ImageUsage.None` there is no benign
  "unknown" reading available: forwarding them is a silent wrong answer, and the
  swapchain-image case D3 was designed to accommodate is exactly the case that
  produces it.

  *What was done, and deliberately no more.* `DlssFeature.ValidateInputs` now
  **fails** on a bound slot with a zero extent or an undefined format, naming
  `Image.FromRaw` as the cause. That converts silence into a diagnosis without
  touching the public API, which is D4's own "make the failure legible"
  principle applied one level up.

  *Resolution: the user deferred it (2026-09-04).* Ship as implemented — the
  validation failure is clear, and the API question is revisited in Phase 3
  alongside `samples/HelloDlaa`, which is where a swapchain-output path would
  first actually be written. This entry stays as an **accepted, deferred
  limitation**, not an unanswered question.

  *What the deferred question is.* Format has an escape hatch — set
  `ImageViewDescription.Format` explicitly — but **extent has none**. A renderer
  that wants DLSS to write straight into a swapchain image cannot express it
  today. The candidate fix is extent parameters on `NgxImage.Wrap` (and possibly
  `CreateView`), which is a public-API addition to a type whose whole design
  rationale is that its inputs cannot disagree — so it is the architect's call,
  not the implementer's. Until then the documented answer is "build the
  `NgxImage` from a VMA-created `Image`", which is what
  `samples/HelloDlaa` (Phase 3) should do.

- **OPEN-6 — the recorder contract is documentary and stays that way.** D4 and E13
  establish that `CommandRecorder` caches nothing to invalidate after an evaluate
  clobbers pipeline/descriptor/dynamic state. If `CommandRecorder` ever grows a
  bound-state cache, `DlssFeature.Evaluate` becomes the one call site that must
  invalidate it, and this entry is the pointer back to why.

## Cross-links

- Tracking, research and the fixed ship-model decisions: **#214**. Fetch step: #215.
  Phase 1: **#216** / PR #217, and
  `docs/design/specs/2026-09-03-issue-216-ngx-native-design.md` — in particular
  **D9** (the four zero-allocation guarantees this design consumes) and **E11**
  (`ReadWrite` is `bool`, not `byte`).
- Wrapper-over-native package shape, and why `Ahjo.Vulkan` gains no dependency:
  **#166**, `docs/design/specs/2026-08-01-issue-166-slang-support-design.md`.
- Why `Evaluate` may take `ref CommandRecorder` without pushing callers into
  caller-wide safe-context: **#209**/**#213**, and
  `tests/Ahjo.Vulkan.Tests/ScopedSpanProbe.cs`.
- Valid-by-default descriptions and the CS8983 explicit-parameterless-constructor
  pattern: **#119**, `Memory/ImageViewDescription.cs`.
- Why layout is not on `Image`: **#17**, `Resources/Image.cs:19-24`.
- Why a lane must declare what it has, and why every skip carries a class:
  **#158**, `docs/ci-coverage.md`, `.github/workflows/ci.yml:176-290`.
- Why there is no NVIDIA coverage in CI and why that is recorded rather than faked:
  **#32**, `.github/CLAUDE.md`.
- Benchmark-class splitting for host-gated setups: **#201** (`MeshShaderBenchmarks`),
  **#114** (`DescriptorSetPoolVariableCountBenchmarks`),
  `.claude/agents/bench-coverage-checker.md:46`.
- The recorder-dispose-before-`ResetForFrame` ordering the new benchmark must
  follow: **#188**/**#199**, `docs/benchmarks.md:109`.
