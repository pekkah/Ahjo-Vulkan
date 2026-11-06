Paired with [../specs/2026-11-06-issue-253-dlss-frame-generation-design.md](../specs/2026-11-06-issue-253-dlss-frame-generation-design.md).

# Implementation plan — issue #253, DLSS Frame Generation in `Ahjo.Vulkan.Ngx`

The work ships as three PRs (spec D11). Each PR branches from `main` **after the
previous one has merged**. Never stack them.

- **PR A** — steps 1–10: the measurement gate, pins, bindings, and context/support availability.
- **PR B** — steps 11–16: the feature API, validation, tests and benchmark.
- **PR C** — steps 17–21: `samples/HelloFrameGen` (with Reflex), the measured contract and docs.
  It is gated on **both** PR B and the #261 Reflex PR (spec D6/D11).

**Hardware is required.** Steps 2, 9 (the hardware half), 15, 18 and 19 run
only on the RTX 4070 Ti with the FG DLL staged. CI cannot run them (#32). Quote
each local run in its PR, with the driver version.

**Stop conditions.** Step 2 is a gate. If OPEN-1 or OPEN-2 fires there, stop,
report the recorded numbers, and do not start step 3. Anything marked
**MEASURE** has a defined oracle. Record the result where the step says, and do
not choose a convention by reading headers.

---

# PR A — branch `issue-253-dlss-frame-generation` (this branch)

## 1. `tools/setup-ngx.ps1` + `native/ngx/pins.sha256` — fetch the FG DLL and guide

Make three additions to the existing manifests.

- Add these to `$WinFeatureFiles`:
  ```powershell
  @{ Path = 'lib/Windows_x86_64/rel/nvngx_dlssg.dll'; Stage = 'staged/win-x64/rel/nvngx_dlssg.dll' }
  @{ Path = 'lib/Windows_x86_64/dev/nvngx_dlssg.dll'; Stage = 'staged/win-x64/dev/nvngx_dlssg.dll' }
  ```
- Add these to `$LinuxFeatureFiles`:
  ```powershell
  @{ Path = "lib/Linux_x86_64/rel/libnvidia-ngx-dlssg.so.$Bare"; Stage = "staged/linux-x64/rel/libnvidia-ngx-dlssg.so.$Bare" }
  @{ Path = "lib/Linux_x86_64/dev/libnvidia-ngx-dlssg.so.$Bare"; Stage = "staged/linux-x64/dev/libnvidia-ngx-dlssg.so.$Bare" }
  ```
- Add this to `$DocFiles`:
  `@{ Path = 'doc/DLSS-FG Programming Guide.pdf'; Stage = 'doc/DLSS-FG_Programming_Guide.pdf' }`.
  Also add `@{ Path = 'doc/RTX UI Developer Guidelines.pdf'; Stage = 'doc/RTX_UI_Developer_Guidelines.pdf' }`.
  It is the source of the FG-requires-Reflex rule (spec F1), and
  `docs/ngx-notes.md` §3 already cites it as an obligation. The URL needs the
  space encoded. Build it as `"$BaseUrl/$([uri]::EscapeUriString($relPath))"`
  in the fetch loop, which fixes it for every entry.

Make these text changes too:

- Replace the comment at `:102-103`, "not bound yet (#214 "Later")". FG is now
  bound (#253). RR is not.
- The closing `Write-Host` block lists the dlssg file under "Feature DLL for
  running samples/tests locally".
- `.DESCRIPTION` names `nvngx_dlssg.dll` beside `nvngx_dlss.dll`.

Run `./tools/setup-ngx.ps1 -Platform all -IncludeDocs -Force -UpdatePins` and
commit `pins.sha256`. The tag line stays `v310.9.1`.

Update `native/ngx/README.md` and `native/CLAUDE.md` (the `ngx/` bullet): the
staged feature DLLs are now `nvngx_dlss` **and** `nvngx_dlssg`. Neither is ever
committed or packed. Check that `.gitignore` already covers
`native/ngx/staged/**`. It must; do not add a `lib/` path.

## 2. GATE — hardware probe for OPEN-1 and OPEN-2 (not committed)

The probe goes in a scratch console project **outside the repo**. Put it in your
session scratchpad, with a `ProjectReference` to `src/Ahjo.Vulkan.Ngx` and
`src/Ahjo.Vulkan`. It may use raw `"FrameGeneration.Available"u8`-style
literals, because it is never committed. It does the following:

1. It calls `ahjo_ngx_vulkan_get_feature_instance_extension_requirements_utf8`
   and `…device_extension_requirements_utf8` with
   `NVSDK_NGX_Feature.NVSDK_NGX_Feature_FrameGeneration`, through `NgxApi` with
   an `AhjoNgxInitInfo` built the way `NgxSupport` builds it, and prints both
   lists. It creates the instance and device with the **union** of the SR and FG
   lists, and with `DlssSearchPaths = [native/ngx/staged/win-x64/rel]`.
2. It runs `NgxContext.Create` as today. Then, on the capability map, it reads
   and prints `FrameGeneration.Available`, `.FeatureInitResult`,
   `.NeedsUpdatedDriver`, `.MinDriverVersionMajor/Minor` and
   `DLSSG.MultiFrameCountMax`.
3. It calls `NVSDK_NGX_VULKAN_AllocateParameters`, then sets these:
   - `CreationNodeMask`/`VisibilityNodeMask` = 1;
   - `Width`/`Height` = 1920/1080;
   - `DLSSG.BackbufferFormat` = `(uint)VK_FORMAT_R8G8B8A8_UNORM`;
   - `DLSSG.InternalWidth/Height` = 1920/1080.

   Then it calls `CreateFeature1(device, cmd, FrameGeneration, …)` inside
   `Queue.ImmediateSubmit`, and prints the result.
4. It renames `rel/nvngx_dlssg.dll` away, reruns step 2, and records which key
   changes and to what. This is the D4 **MEASURE**.
5. **Record only; this never stops the work.** It prints whether the 4070 Ti's
   device extensions include `VK_EXT_present_timing`,
   `VK_GOOGLE_display_timing`, `VK_KHR_present_id`, `VK_KHR_present_wait` and
   `VK_NV_low_latency2` (`PhysicalDevice.SupportsExtension`). Spec F3's pacer
   table needs these answers, and they go into step 20 §5.

Record the following in the PR A description: the driver version, both
extension lists, the five `FrameGeneration.*` values, `MultiFrameCountMax`, the
`CreateFeature1` result, the missing-DLL signal, and the five
present/latency extension answers.

- **Stop (OPEN-1)** if `Available != 1` or if `CreateFeature1` fails. That
  includes `FAIL_FeatureDenied` (`0xBAD0000D`; confirm the value against
  `Generated/NVSDK_NGX_Result.cs`).
- **OPEN-2.** If the device list contains `VK_NV_optical_flow`, enable it.
  If `CreateFeature1` then fails, retry with one extra
  `QueueRequest(family, count: 1, priority: 1f)` on the first family whose
  `QueueFamilyInfo.Flags` has `VK_QUEUE_OPTICAL_FLOW_BIT_NV`.
  - If that succeeds, record "needs an optical-flow queue". Step 17 and
    step 20 §3 add the queue request to the init order. No core change, no
    stop.
  - **Stop** only if the retry also fails, or if no family carries the bit.

The watermark half of OPEN-1 is checked visually in step 19. If it watermarks
there, stop PR C, not PR A.

## 3. Bindings — `native/ngx/src/ahjo_ngx.h`, `tools/generate-ngx.rsp`, regen

**`native/ngx/src/ahjo_ngx.h`**

- Add `#include "nvsdk_ngx_defs_dlssg.h"` immediately after
  `#include "nvsdk_ngx_vk.h"`.
- Add a header-comment paragraph saying the include exists so the rsp's
  traversal reaches the FG names. It declares only enums, macros and
  typedefs, so it changes no export.

**`tools/generate-ngx.rsp`**

- Add `native/ngx/include/nvsdk_ngx_defs_dlssg.h` to `--traverse`, after
  `nvsdk_ngx_params.h`.
- Add a comment block above it:
  - `params_dlssg.h` is C++ (default member initializers at `:45`, `:48`, `:109`)
    and includes the D3D `nvsdk_ngx.h` (`:28`). The wrapper writes parameters by
    name instead.
  - `helpers_dlssg_vk.h` contains only `static inline` functions.
  - Do not traverse either one (spec D3).

Run `/regen-bindings` for NGX. The Regenerate target must **exit 0**. If a new
function-like macro trips the GROUP 6 warning mechanism, add it to GROUP 6 with
a one-line reason. Expected new output:

- `NVSDK_NGX_DLSSG_ResourceFlags.cs`, `NVSDK_NGX_DLSSG_EvalFlags.cs` and
  `NVSDK_NGX_DLSSG_FullscreenMode.cs`;
- `NVSDK_NGX_DLSSG_Parameter_*` and `NVSDK_NGX_Parameter_FrameGeneration_*`
  `u8` properties on `NgxApi`;
- **no new `[DllImport]`**.

If any `wchar_t` or `int*`-string shape appears, exclude it (rsp GROUP 1 rule).
Then rebuild `native/ngx` and run `tests/Ahjo.Vulkan.Ngx.Native.Tests`:
`NgxExportDriftTests` must pass **unchanged**.

## 4. `src/Ahjo.Vulkan.Ngx/NgxFeature.cs` (new) and `NgxDescription.cs`

**`src/Ahjo.Vulkan.Ngx/NgxFeature.cs`** (new):

```csharp
public enum NgxFeature { SuperResolution = 0, FrameGeneration = 1 }

[Flags]
public enum NgxFeatures : uint { None = 0, SuperResolution = 1u << 0, FrameGeneration = 1u << 1 }
```

Add an `internal static` mapping `NgxFeature → NVSDK_NGX_Feature`
(`SuperSampling` / `FrameGeneration`), and the per-feature expected file name:

| | Windows | Linux |
|---|---|---|
| SR | `nvngx_dlss.dll` | `libnvidia-ngx-dlss.so.<version>` |
| FG | `nvngx_dlssg.dll` | `libnvidia-ngx-dlssg.so.<version>` |

**`NgxDescription.cs`**:

- Add `public NgxFeatures RequiredFeatures { get; init; } = NgxFeatures.SuperResolution;`.
  If the record struct has no explicit parameterless constructor running
  initializers, add one, following the #119 pattern. Check `NgxDescription.cs:15`.
- In `Validate`, `RequiredFeatures == None` and undefined bits throw
  `ArgumentException` naming the property.
- Widen the `DlssSearchPaths` doc comment so it says the list is searched for
  every NGX feature DLL (`nvngx_dlss.dll` and `nvngx_dlssg.dll`). Do not rename
  the property.

## 5. `src/Ahjo.Vulkan.Ngx/Internal/NgxParameterNames.cs` — FG names

Add a `// ---- Frame generation ----` group. Every field is
`Utf8Name.FromLiteral(NgxApi.<generated name>)`. Use exactly these member names:

- **Capability:** `FrameGenerationAvailable`, `FrameGenerationFeatureInitResult`,
  `FrameGenerationNeedsUpdatedDriver`, `FrameGenerationMinDriverVersionMajor`,
  `FrameGenerationMinDriverVersionMinor`, `DlssgMultiFrameCountMax`.
- **Create:** `DlssgBackbufferFormat`, `DlssgInternalWidth`,
  `DlssgInternalHeight`, `DlssgDynamicResolution`,
  `DlssgResourceNeverProvidedFlags`.
- **Resources:** `DlssgBackbuffer`, `DlssgDepth`, `DlssgMVecs`, `DlssgHudless`,
  `DlssgUi`, `DlssgOutputInterpolated`, `DlssgOutputReal`.
- **Matrices:** `DlssgCameraViewToClip`, `DlssgClipToCameraView`,
  `DlssgClipToLensClip`, `DlssgClipToPrevClip`, `DlssgPrevClipToClip`.
- **Scalars:**
  - jitter and motion vectors: `DlssgJitterOffsetX/Y`, `DlssgMvecScaleX/Y`;
  - camera: `DlssgCameraPinholeOffsetX/Y`, `DlssgCameraPosX/Y/Z`,
    `DlssgCameraUpX/Y/Z`, `DlssgCameraRightX/Y/Z`, `DlssgCameraFwdX/Y/Z`,
    `DlssgCameraNear`, `DlssgCameraFar`, `DlssgCameraFov`,
    `DlssgCameraAspectRatio`;
  - flags: `DlssgColorBuffersHdr`, `DlssgDepthInverted`,
    `DlssgCameraMotionIncluded`, `DlssgReset`, `DlssgNotRenderingGameFrames`,
    `DlssgOrthoProjection`;
  - multi-frame and frame id: `DlssgMultiFrameCount`, `DlssgMultiFrameIndex`,
    `DlssgBackbufferFrameId`.

PR A uses only the capability names. Per the file's "exactly the keys the
wrapper uses" rule (`NgxParameterNames.cs` remarks), the create, resource,
matrix and scalar names are **added in step 12 (PR B)**, not here.

## 6. `src/Ahjo.Vulkan.Ngx/NgxSupport.cs` + `DlssRequirements.cs` — feature argument

These are breaking signature changes, made as stated:

```csharp
public static bool TryGetInstanceExtensions(NgxFeature feature, in NgxDescription description, [NotNullWhen(true)] out NgxExtensionSet? extensions)
public static bool TryGetDeviceExtensions(NgxFeature feature, PhysicalDevice physicalDevice, in NgxDescription description, [NotNullWhen(true)] out NgxExtensionSet? extensions)
public static bool TryGetRequirements(NgxFeature feature, PhysicalDevice physicalDevice, in NgxDescription description, out DlssRequirements requirements)
public static bool IsSupported(NgxFeature feature, PhysicalDevice physicalDevice, in NgxDescription description)
```

- `TryGetSuperSamplingRequirements` and `IsSuperSamplingSupported` are
  **removed**. They have no shims (breaking changes are fine).
- Each method passes `NgxFeatureMap.ToNative(feature)` where it passed
  `NVSDK_NGX_Feature_SuperSampling` (`:71`, `:110`, `:160`). An undefined
  `feature` throws `ArgumentOutOfRangeException`.
- Update the class remarks' ordering contract (`:31-45`). For both features,
  union the two sets before create; dispose each set after its create.
- In `DlssRequirements.cs:8`, change the doc reference to `TryGetRequirements`.

## 7. `src/Ahjo.Vulkan.Ngx/NgxContext.cs` + `NgxException.cs` — FG availability

### 7a. `NgxException.cs`

`NgxFeatureLibraryNotFoundException` becomes:

```csharp
public sealed class NgxFeatureLibraryNotFoundException : NgxException
{
    public NgxFeature Feature { get; }
    public string ExpectedFile { get; }
    public IReadOnlyList<string> SearchedDirectories { get; }
    public NgxFeatureLibraryNotFoundException(NVSDK_NGX_Result result, NgxFeature feature, string expectedFile, IReadOnlyList<string> searchedDirectories, string message)
}
```

Widen the summary to "an NVIDIA feature library (`nvngx_dlss.dll` or
`nvngx_dlssg.dll`)".

### 7b. `NgxContext.cs`

**Create.** After the SR availability read (`:150-154`), read the five
`FrameGeneration.*` values and `DLSSG.MultiFrameCountMax` with the existing
`GetIntOrZero` / `GetUIntOrZero`. Then:

- **SR unavailable.** If SR is unavailable **and**
  `description.RequiredFeatures.HasFlag(SuperResolution)`, call
  `ThrowDiagnosed(…, NgxFeature.SuperResolution, …)` as today.
- **FG unavailable.** If FG is unavailable **and** `RequiredFeatures` has
  `FrameGeneration`, call `ThrowDiagnosed(…, NgxFeature.FrameGeneration, …,
  "DLSS Frame Generation capability query")`.

Store these private readonly fields:

- `_superSamplingAvailable` and `_frameGenerationAvailable`;
- one `FeatureDiagnosis` record per feature (an `internal readonly record
  struct` holding `featureInitResult`, `needsUpdatedDriver`, `minMajor` and
  `minMinor`);
- `_maxGeneratedFrames = available ? Math.Max(1u, multiFrameCountMax) : 0u`;
- a copy of the `NgxDescription`'s search paths and its `DlssSearchPaths`
  list, which the deferred diagnosis needs.

**New public members:**

- `public bool IsFrameGenerationAvailable`
- `public uint MaxGeneratedFrames`. The remarks say that 1 means 2x only and
  that RTX 40-series is expected at 1 (spec E12).
- `IsSuperSamplingAvailable` now reports the real value. It is no longer
  "always true". Update its doc.

**Diagnosis.** `ThrowDiagnosed` and `BuildMissingLibraryMessage` take
`NgxFeature`.

- The not-found message keeps today's exact shape with the feature-specific
  names. For FG:
  ```
  DLSS Frame Generation is unavailable: the NVIDIA feature library was not found.
  Expected file: nvngx_dlssg.dll
  Searched:
    <AppContext.BaseDirectory>
    <each DlssSearchPaths entry>
  This library is NOT shipped by Ahjo.Vulkan.Ngx — the application supplies it from NVIDIA's DLSS SDK (https://github.com/NVIDIA/DLSS, lib/<plat>/rel/). See docs/ngx-frame-generation-notes.md.
  ```
  The SR message is unchanged apart from passing `Feature`/`ExpectedFile`/the
  directory list to the new constructor.
- The "available == 0, no reason" message names the feature: "DLSS Frame
  Generation … `FrameGeneration.Available` is 0 … The GPU most likely is not an
  RTX 40-series or newer (DLSS-FG guide, System Requirements)."
- **The not-found key.** If step 2's MEASURE showed a key other than
  `FeatureInitResult == FAIL_FeatureNotFound`, map that key here. Record which
  one in the PR and in a one-line comment citing the measurement.

**Projection for tests.** Add `internal static void ThrowIfRequiredUnavailable(NgxFeatures required, bool srAvailable, in FeatureDiagnosis sr, bool fgAvailable, in FeatureDiagnosis fg, IReadOnlyList<string>? searchPaths)`.
`Create` calls it, so the throw matrix is testable with no device (the
`ProjectOptimalSettings` pattern, `:461-479`).

**`CreateDlss`.** Before allocating parameters, if `!_superSamplingAvailable`,
throw the stored diagnosis via `ThrowDiagnosed(NgxFeature.SuperResolution, …)`.
This path is now reachable because a context created with `RequiredFeatures =
FrameGeneration` may lack SR.

## 8. Update the 11 call sites (spec E10)

Pass `NgxFeature.SuperResolution` at every existing call. These lines, as
counted on `main`, are:

- `samples/HelloDlaa/Program.cs:128`, `:193`
- `tests/Ahjo.Vulkan.Benchmarks/DlssEvaluateBenchmarks.cs:77`, `:104`
  (`TryGetSuperSamplingRequirements` → `TryGetRequirements`), `:111`
- `tests/Ahjo.Vulkan.Ngx.Tests/DlssHardwareTests.cs:214`, `:264`
- `tests/Ahjo.Vulkan.Ngx.Tests/NgxSupportTests.cs:33`
- `tests/Ahjo.Vulkan.Ngx.Tests/NgxTestEnvironment.cs:90`, `:135`, `:162`

`NgxContext.Create` call sites need no change, because the default
`RequiredFeatures` keeps them as they are. The catch sites
(`HelloDlaa/Program.cs:309`, `NgxTestEnvironment.cs:181`) compile unchanged.
Re-grep for `NgxSupport.Try` and `IsSuperSamplingSupported` after editing and
reconcile the count.

## 9. Tests (PR A)

**`tests/Ahjo.Vulkan.Ngx.Tests/NgxContextAvailabilityTests.cs`** (new, no
device; drives `ThrowIfRequiredUnavailable`):

1. **Required SR, SR available, FG unavailable** → no throw.
2. **Required SR | FG, FG unavailable, FG `FeatureInitResult = FAIL_FeatureNotFound`**
   → `NgxFeatureLibraryNotFoundException` with `Feature == FrameGeneration`,
   `ExpectedFile == "nvngx_dlssg.dll"` (on Windows), and `SearchedDirectories`
   = `[AppContext.BaseDirectory, …paths]` in order. The message contains each
   directory on its own line.
3. **Required FG, FG `NeedsUpdatedDriver = 1`, min 600.10**
   → `NgxDriverTooOldException` with major 600 and minor 10.
4. **Required FG, available 0, everything else zero** → `NgxException`
   (`FAIL_FeatureNotSupported`), message names "DLSS Frame Generation".
5. **Required SR, SR not found** → the exception carries
   `Feature == SuperResolution` and `ExpectedFile == "nvngx_dlss.dll"`. This is
   the regression guard on the existing message shape.

**`NgxDescriptionTests.cs`** (additions):

- `RequiredFeatures` defaults to `SuperResolution` via `new NgxDescription()`.
- `None` throws.
- `(NgxFeatures)8` throws.

**`NgxSupportTests.cs`**: an undefined `NgxFeature` throws
`ArgumentOutOfRangeException` for all four methods.

**Hardware.** `NgxTestEnvironment` gains `IsFrameGenerationAvailable`
(`Lazy<bool>`, created with SR and FG extension unions and `RequiredFeatures =
SuperResolution`, reading `IsFrameGenerationAvailable`). Skips use
`[gate:feature]`. Two new cases in `DlssHardwareTests.cs` (or a new
`FrameGenerationHardwareTests.cs`):

- `TryGetDeviceExtensions(FrameGeneration)` returns `true` with a non-null set.
- A context reports `IsFrameGenerationAvailable == true` and
  `MaxGeneratedFrames >= 1`.

Update `NgxShadowEnumDriftTests` only if a shadow enum was added. None is
planned: `NgxFeature` is wrapper-owned.

## 10. Docs (PR A)

- `src/Ahjo.Vulkan.Ngx/CLAUDE.md:5` and `docs/ngx-notes.md` (header paragraph):
  the pin is `v310.9.1`, not `v310.7.0`. Keep the 610.47 measurement
  provenance as it is. Those numbers were measured on 310.7.0, so say so.
- `docs/ngx-notes.md` §4.7: the `NgxSupport` calls now take
  `NgxFeature.SuperResolution`. Update the snippet.
- `src/Ahjo.Vulkan.Ngx/README.md`: one paragraph on `RequiredFeatures`.
- `docs/ngx-frame-generation-notes.md` (new **stub**; step 7b's message points
  here, so it must exist in PR A). It holds a title, the BYO paragraph (the
  `rel/`-only rule, never packed, #214), the FG missing-library message quoted
  verbatim, step 2's measured extension lists, and the placeholder line
  "The measured contract lands with `samples/HelloFrameGen` (#253 PR C)." 

PR A description:

- Step 2's numbers.
- "Refs #253", not "Closes".
- The breaking changes, named: the `NgxSupport` signatures, the two removed
  methods, and the `NgxFeatureLibraryNotFoundException` constructor.

---

# PR B — branch from `main` after PR A merges

## 11. Public types — `src/Ahjo.Vulkan.Ngx/`

**`DlssFrameGenerationDescription.cs`**:

```csharp
public readonly record struct DlssFrameGenerationDescription
{
    public uint     Width             { get; init; }   // backbuffer + outputs
    public uint     Height            { get; init; }
    public VkFormat BackbufferFormat  { get; init; }
    public uint     RenderWidth       { get; init; }   // depth + MV extent → DLSSG.InternalWidth
    public uint     RenderHeight      { get; init; }
    public bool     DynamicResolution { get; init; }
}
```

**`DlssFrameGenerationCamera.cs`**: a `readonly struct` with an explicit
parameterless constructor (CS8983, #119) so that `ClipToLensClip =
Matrix4x4.Identity`.

- Members: `ViewToClip`, `ClipToView`, `ClipToPrevClip`, `PrevClipToClip`,
  `ClipToLensClip` (`Matrix4x4`); `Position`, `Up`, `Right`, `Forward`
  (`Vector3`); `Near`, `Far`, `FieldOfView` (radians), `AspectRatio` (`float`);
  `PinholeOffset` (`Vector2`).
- Doc on the matrices: "Unjittered (`nvsdk_ngx_defs_dlssg.h:125`). Orientation:
  see `docs/ngx-frame-generation-notes.md`." The convention is filled in by
  step 19's MEASURE. Until then the doc says "orientation is measured in #253
  PR C". Do not assert one.

**`DlssFrameGenerationInputs.cs`**: a `readonly struct` with an explicit
parameterless constructor.

| Member | Type | Default | Doc essentials |
|---|---|---|---|
| `Backbuffer` | `NgxImage` | — | Required. Final colour incl. UI, UNORM (never `_SRGB`), extent = feature `Width×Height` |
| `Depth` | `NgxImage` | — | Required. Hardware depth, render extent |
| `MotionVectors` | `NgxImage` | — | Required. `previous − current`, pixels of this resource (FG guide l. 4022-4036; same as SR) |
| `Hudless` | `NgxImage` | default | Optional, strongly recommended |
| `UiColorAndAlpha` | `NgxImage` | default | Optional. Premultiplied: `Backbuffer = UI + (1−α)·Hudless` |
| `OutputInterpolated` | `NgxImage` | — | Required. Same extent + format as `Backbuffer`; `Storage` usage |
| `OutputReal` | `NgxImage` | default | Optional, recommended (debug overlay, FG guide l. 874-876) |
| `Camera` | `DlssFrameGenerationCamera` | `new()` | |
| `JitterOffsetX/Y` | `float` | 0 | **Clip space**, not SR's render pixels (`nvsdk_ngx_defs_dlssg.h:138-140`) |
| `MotionVectorScaleX/Y` | `float` | 1f | 0f treated as 1f; semantics per step 19 MEASURE |
| `Reset` | `bool` | false | |
| `DepthInverted` | `bool` | false | |
| `ColorBuffersHdr` | `bool` | false | |
| `CameraMotionIncluded` | `bool` | true | Motion vectors include camera motion (the HelloDlaa convention) |
| `NotRenderingGameFrames` | `bool` | false | Menus/cut-scenes |
| `OrthographicProjection` | `bool` | false | |
| `MultiFrameCount` | `uint` | 1 | |
| `MultiFrameIndex` | `uint` | 1 | |
| `BackbufferFrameId` | `ulong` | 0 | Increments by one per rendered frame, even while FG is off (`nvsdk_ngx_defs_dlssg.h:361-364`) |

Copy the remarks block from `DlssEvaluateInputs.cs:7-40` and adapt it. It
should say: layout is the caller's contract (inputs in a shader-read layout,
outputs in `GENERAL`, to be confirmed by step 19's validation run), record
outside `BeginRendering`, and rebind after evaluate (FG guide l. 302-303).

## 12. `DlssFrameGenerationFeature.cs` + `NgxContext.CreateFrameGeneration` + parameter names

**Parameter names.** Add the create, resource, matrix and scalar names from
step 5 to `NgxParameterNames.cs`.

**`NgxContext.CreateFrameGeneration`:**

```csharp
public DlssFrameGenerationFeature CreateFrameGeneration(ref CommandRecorder recorder, in DlssFrameGenerationDescription description)
```

1. `ObjectDisposedException.ThrowIf`. If `!_frameGenerationAvailable`, call
   `ThrowDiagnosed(NgxFeature.FrameGeneration, …)` with the stored diagnosis.
2. Validate the description. These are always-on `ArgumentException`s with
   messages in the `ValidateFeatureDescription` style (`NgxContext.cs:562-588`):
   - `Width`, `Height`, `RenderWidth` and `RenderHeight` are all non-zero.
   - `BackbufferFormat` is not `VK_FORMAT_UNDEFINED`.
   - `BackbufferFormat` is not an `_SRGB` format. The message cites spec E7
     and `docs/ngx-notes.md` §6.
3. `EnterExclusive(nameof(CreateFrameGeneration))`. Then
   `AllocateParameters` and set:
   - `CreationNodeMask` = 1, `VisibilityNodeMask` = 1;
   - `Width` / `Height` (generic names, as `NGX_VK_CREATE_DLSSG` does,
     `nvsdk_ngx_helpers_dlssg_vk.h:53-57`);
   - `DlssgBackbufferFormat` = `(uint)description.BackbufferFormat`;
   - `DlssgInternalWidth` / `DlssgInternalHeight`;
   - `DlssgDynamicResolution` = 0/1;
   - `DlssgResourceNeverProvidedFlags` =
     `(uint)(NVSDK_NGX_DLSSG_ResourceFlags_UIAlpha | NVSDK_NGX_DLSSG_ResourceFlags_BidirectionalDistortionField)`.
4. `CreateFeature1(device, cmd, NVSDK_NGX_Feature_FrameGeneration, params, &handle)`
   via `NgxResult.ThrowIfFailed(…, "NVSDK_NGX_VULKAN_CreateFeature1 (frame generation)")`.
5. Transfer ownership into `new DlssFrameGenerationFeature(this, handle,
   parameters, in description, _maxGeneratedFrames)`, with the same
   `finally` destroy-on-failure as `CreateDlss`.

The remarks match `CreateDlss`: submit and **complete** the recorder before the
first evaluate. Recreate on any resize (FG guide l. 657-668).

**`DlssFrameGenerationFeature`** is a `sealed unsafe class`, `IDisposable`.

- **Fields:** `_context`, `_handle`, `_parameters`, `_maxGeneratedFrames`,
  `_disposed`.
- **Public read-only:** `Width`, `Height`, `BackbufferFormat`, `RenderWidth`,
  `RenderHeight`, `MaxGeneratedFrames`.
- `public void Evaluate(ref CommandRecorder recorder, in DlssFrameGenerationInputs inputs)`
  has exactly the shape of `DlssFeature.Evaluate` (`DlssFeature.cs:104-121`):
  dispose check, `RequireLiveContext`, `EnterExclusive`, validation if enabled,
  then `EvaluateCore(…, invokeNgx: true)`.

`internal void EvaluateCore(VkCommandBuffer_T* commandBuffer, in DlssFrameGenerationInputs inputs, bool invokeNgx)`:

- **Resources.** Seven `NVSDK_NGX_Resource_VK` stack locals, built with
  `ToNative`. `readWrite: true` goes on `OutputInterpolated` and `OutputReal`;
  everything else is `false`. Optional slots that are `IsNull` are written as a
  `null` pointer, never skipped.
- **Matrices.** Five `Matrix4x4` stack locals, copied from `inputs.Camera`, then
  `SetVoidPointer(…, &local)`. **MEASURE hook:** if step 19 shows the transposed
  orientation is required, the copy becomes `Matrix4x4.Transpose(...)`. That is
  a one-line change confined to this method. Record it there with a comment
  citing the measurement.
- **Scalars.** Set every scalar in step 5's list:
  - jitter, `MvecScale` (via `OneIfZero`), pinhole offset;
  - camera position and the up/right/forward vectors (`Vector3.X/Y/Z`);
  - near, far, FOV, aspect;
  - the six flags as `SetUI(0/1)`;
  - `MultiFrameCount` and `MultiFrameIndex` (`SetUI`);
  - `BackbufferFrameId` (`SetULL`).
- **Clear in `finally`.** `try { if (!invokeNgx) return;
  ThrowIfFailed(EvaluateFeature_C(cb, _handle, parameters, null), "DLSS frame
  generation evaluate"); } finally { ClearPointerSlots(parameters); }`.
  `ClearPointerSlots` nulls all **twelve** pointer keys (7 resources and 5
  matrices). The comment block from `DlssFeature.cs:203-219` is adapted to say
  matrices too.
- **Validation.** `internal static void ValidateInputs(in DlssFrameGenerationInputs inputs, uint width, uint height, uint maxGeneratedFrames)`
  runs these checks. Each failure calls `NgxValidation.Fail("DlssFrameGenerationFeature.Evaluate", …)`,
  and messages follow the `DlssFeature.ValidateInputs` style (slot name, the
  value seen, the rule and its source):
  1. **Required slots.** `Backbuffer`, `Depth`, `MotionVectors` and
     `OutputInterpolated` are set. Message: "… are required; Hudless,
     UiColorAndAlpha and OutputReal are optional."
  2. **Metadata.** `RequireMetadata` on all seven slots, with the same text as
     `DlssFeature.cs:319-339` and the slot type name swapped.
  3. **Backbuffer extent.** `Backbuffer.Width/Height == width/height`.
     Otherwise: "… recreate the feature on resize (DLSS-FG guide, When to
     Recreate the Feature)".
  4. **Output shape.** `OutputInterpolated`, and `OutputReal` if set, match
     `Backbuffer` in `Width`, `Height` and `Format` (FG guide l. 764-765).
  5. **Usage.** `RequireUsage` with `Storage` on both outputs and `Sampled` on
     the five inputs. `ImageUsage.None` is skipped, as on SR.
  6. **Multi-frame.** `MultiFrameCount >= 1`,
     `1 <= MultiFrameIndex <= MultiFrameCount`, and
     `MultiFrameCount <= maxGeneratedFrames`. The last message says that RTX
     40-series supports 2x only (spec E12).
- **`Dispose`.** Mirrors `DlssFeature.Dispose` without the free-memory key:
  `ReleaseFeature`, then `DestroyParameters`. It is idempotent. Doc: dispose
  before the context, and only once no command buffer is in flight.

**AOT and allocation.** There are no lambdas, no LINQ and no boxing in
`Evaluate`/`EvaluateCore`. Validation messages are interpolated only inside the
`Fail` branches (setup cost under validation is allowed; the frame cost with
validation off is not).

## 13. Tests (PR B) — `tests/Ahjo.Vulkan.Ngx.Tests/`

**`DlssFrameGenerationValidationTests.cs`** (no device; mirrors
`DlssValidationTests.cs`). One test per branch, each asserting the
`AhjoValidationException` message contains the slot name:

- each of the 4 required slots unset;
- a zero-extent `Hudless`;
- an `UNDEFINED`-format `OutputReal`;
- a `Backbuffer` extent that differs from the feature's;
- an `OutputInterpolated` format ≠ `Backbuffer`'s;
- an `OutputReal` extent ≠ `Backbuffer`'s;
- an output without `Storage`;
- an input without `Sampled`;
- an input with `ImageUsage.None`, which **passes**;
- `MultiFrameIndex = 0`;
- `MultiFrameIndex = 2` with `MultiFrameCount = 1`;
- `MultiFrameCount = 2` with `maxGeneratedFrames = 1`;
- a fully valid input, which passes.

Build `NgxImage` values the way `DlssValidationTests` does.

**`DlssFrameGenerationDescriptionTests.cs`**: the always-on create checks. To
make them testable without a device, factor them into
`internal static void ValidateFrameGenerationDescription(in DlssFrameGenerationDescription)`.
Cases: a zero `Width`, a zero `RenderHeight`, `UNDEFINED`,
`VK_FORMAT_B8G8R8A8_SRGB` (throws, and the message mentions UNORM), and
`VK_FORMAT_R8G8B8A8_UNORM` (passes).

**`DlssFrameGenerationInputs` defaults test**: `new
DlssFrameGenerationInputs()` has scales 1, `MultiFrameCount` and `Index` 1,
`CameraMotionIncluded` true, and `Camera.ClipToLensClip == Identity`.

**Hardware (`[gate:feature]`, `IsFrameGenerationAvailable`).**

- Create a 1920×1080 R8G8B8A8_UNORM feature via `ImmediateSubmit`, then
  dispose it.
- Run one `Evaluate` against VMA-created images (Storage|TransferDst outputs,
  Sampled inputs) transitioned to the documented layouts, with the validation
  layer on. Assert no exception and no layer error, using the existing layer
  capture used by `DlssHardwareTests`.
- On a `RequiredFeatures = SuperResolution` context where FG is available,
  `CreateFrameGeneration` succeeds.

## 14. Benchmark — `tests/Ahjo.Vulkan.Benchmarks/DlssFrameGenerationEvaluateBenchmarks.cs` (new)

Copy the structure of `DlssEvaluateBenchmarks.cs`:

- a separate `[MemoryDiagnoser]` class whose class remarks say why it is
  separate (E11);
- `[GlobalSetup]` **throws** when FG is unavailable;
- `EvaluatesPerInvoke = 16`, at 1920×1080.

Two benchmarks:

- `PackParameters_16`: calls `EvaluateCore(cb, in inputs, invokeNgx: false)`
  16 times.
- `Evaluate_16`: calls the full `Evaluate` 16 times on a recorder, with
  validation **off**.

Target **`Allocated: -`** on both. Do not fold this into
`DlssEvaluateBenchmarks` or `CommandRecorderBenchmarks`. Run via `/run-bench`
(Release) filtered to `*DlssFrameGeneration*`, and paste the table into the PR.

## 15. Hardware run (PR B)

Run `dotnet test tests/Ahjo.Vulkan.Ngx.Tests -c Release` on the 4070 Ti with
`rel/` staged. Quote the pass and skip counts and the driver version. Also
confirm the AOT smoke still publishes (root CLAUDE.md command). `Ahjo.Vulkan.Ngx`
is not referenced from `AotSmoke`, so this is a regression check only.

## 16. Package docs (PR B)

**`src/Ahjo.Vulkan.Ngx/CLAUDE.md`**: add a section, "`DlssFrameGenerationFeature.Evaluate`
is the second hot path". It covers:

- the same four properties;
- the **matrices are stack locals too** rule, and why (spec D7.1);
- twelve pointer keys cleared in `finally`;
- jitter is clip-space, not SR's pixel space;
- benchmark name `DlssFrameGenerationEvaluateBenchmarks`.

Root `CLAUDE.md` invariant 3 names `DlssFeature.Evaluate` as the per-frame
example. Add `DlssFrameGenerationFeature.Evaluate` beside it. This edit goes in
the PR for human review like any other.

**`src/Ahjo.Vulkan.Ngx/README.md`**: a "Frame generation" subsection pointing to
`docs/ngx-frame-generation-notes.md`, the stub step 10 created. Add the API
summary (types, `CreateFrameGeneration`, `Evaluate`) to that stub.

---

# PR C — branch from `main` after PR B **and** the #261 Reflex PR have merged

**Prerequisites (check both before branching):**
1. PR B is merged.
2. #261 (native `VK_NV_low_latency2` + PCL Stats in `Ahjo.Vulkan`, spec
   OPEN-3) is merged to `main`. Its spec is `docs/design/specs/2026-11-06-issue-261-reflex-low-latency-design.md` ("#261 spec").

If either is missing, do not start PR C. Never branch PR C from the #261
branch or from PR B's branch.

**The Reflex API below is #261's, as approved.** Steps 17b and 19i use the
names, signatures and call order from the #261 spec, section "What #253 PR C
gets", and from its D3-D7. If the merged API differs from what is written
here, the merged code and the #261 spec win; report the difference. If a step
needs something #261 does not provide, **stop and ask**. Do not add to the
Reflex API from inside PR C.

## 17. `samples/HelloFrameGen/` (new)

**`HelloFrameGen.csproj`**:

- `RootNamespace` `Ahjo.Vulkan.Samples.HelloFrameGen`;
- the same references and `SdlWindow.cs` link as `samples/HelloDlaa/HelloDlaa.csproj:20`;
- the same Slang shader compile items;
- the conditional feature-DLL copy items, for **both** `nvngx_dlss.dll` and
  `nvngx_dlssg.dll`, using the `Exists(...)` pattern of `docs/ngx-notes.md` §2,
  plus a `-p:NvidiaDlssgDll=<path>` property mirroring `NvidiaDlssDll`.

Add it to `Ahjo.Vulkan.slnx` beside `HelloDlaa` (`:28`).

**Files.**

- Copy `CubeScene.cs`, `CubePipeline.cs` and the cube shaders from `HelloDlaa`.
  Samples are standalone worked call sites. Remove the jitter (no SR here):
  delete the `JitterSequence` use and pass the unjittered matrices.
- `FrameGenTargets.cs` holds the per-slot app images, all
  `AllocationFlags.DedicatedMemory`:
  - `Hudless` (R8G8B8A8_UNORM, ColorAttachment|Sampled|TransferSrc);
  - `Ui` (R16G16B16A16_SFLOAT, ColorAttachment|Sampled; the FG guide says
    RGB10A2 is not enough for UI, l. 753-754);
  - `Backbuffer` (R8G8B8A8_UNORM, ColorAttachment|Sampled|TransferSrc);
  - `Depth` (D32_SFLOAT, DepthStencilAttachment|Sampled);
  - `MotionVectors` (R16G16_SFLOAT, ColorAttachment|Sampled);
  - `OutputInterpolated` and `OutputReal` (R8G8B8A8_UNORM,
    Storage|TransferSrc|TransferDst).
- A UI pass draws one premultiplied translucent quad into `Ui`. A composite pass
  writes `Backbuffer = Ui + (1−α)·Hudless` with the in-shader sRGB encode.
  `Hudless` gets the same encode, so non-UI pixels are bit-identical
  (FG guide l. 742-744).

**`FrameGenOptions.cs`.** The command line is:

```
HelloFrameGen [--mode fg|off] [--present fifo|immediate] [--frames N]
              [--throttle-ms N]
              [--capture-generated <png>] [--capture-real <png>]
              [--freeze] [--require-fg] [--no-validation]
```

`--throttle-ms N` makes the render thread spin-wait N ms after the scene
submit, which forces the base rate below refresh/2. That reproduces spec F3's
"B·M < R" row. The wait is a `Stopwatch` spin, not `Thread.Sleep`, so the
interval is precise. Default 0.

The defaults are `fg` and `fifo`. The exit codes follow `HelloDlaa`: 0 for ran
or skipped, 2 for a bad command line, 3 for a layer error, 5 for
`--require-fg` when FG is unavailable.

**Init order.**

1. NGX instance extensions, the union of SR and FG.
2. The instance.
3. The physical device.
4. NGX device extensions, the union of SR and FG.
5. The device.
6. `NgxContext.Create` with `RequiredFeatures = NgxFeatures.FrameGeneration`
   **always**. The sample uses no SR, so it must not require `nvngx_dlss.dll`.
7. Catch `NgxException`, including the typed `NgxFeatureLibraryNotFoundException`,
   at `Create`. Print `e.Message` **verbatim**, which exercises step 7b's FG
   message. Then run `--mode off`, or exit 5 under `--require-fg`. This is
   the `HelloDlaa` shape (`Program.cs:309`).
8. `CreateFrameGeneration` with `Width/Height` = the swapchain extent,
   `BackbufferFormat` = R8G8B8A8_UNORM and the render extent = the same.
9. Submit and complete it via `ImmediateSubmit`.

**Swapchain.** UNORM only, with `PreferredFormats = [B8G8R8A8_UNORM,
R8G8B8A8_UNORM]` and the `--present` mode. Usage is
`ColorAttachment|TransferDst`. The default image count works because the frame
holds at most one acquired image at a time (spec D5 sequence).

**Frame loop.** It follows spec D5 exactly:

- Keep a sample-owned `BinarySemaphore[] extraAcquire` of length
  `FramesInFlight`, filled from a sample-owned `SemaphorePool`.
- For each frame:
  1. Render the scene, UI and composite.
  2. Transition `Backbuffer`, `Depth`, `MotionVectors`, `Hudless` and `Ui` to
     `SHADER_READ_ONLY_OPTIMAL`, and both outputs to `GENERAL`.
  3. `fg.Evaluate`.
  4. Barrier `OutputInterpolated` and `OutputReal` from `GENERAL` to
     `TRANSFER_SRC_OPTIMAL`, from `ComputeShader | AllTransfer` with
     `ShaderWrite | TransferWrite` to `AllTransfer` with `TransferRead`. This is
     the same shape as HelloDlaa's DLSS-output blit, and step 18's layer run
     confirms it. Then acquire `i0` on `extraAcquire[slot]`, and blit
     `OutputInterpolated → i0` with the swapchain image's
     `UNDEFINED → TRANSFER_DST_OPTIMAL` barrier and the `AllTransfer`
     acquire-wait discipline of `docs/ngx-notes.md` §6.
  5. `queue.Submit2(ref rec1, default, [new SemaphoreSubmit(extraAcquire[slot],
     Stage.AllTransfer)], [new SemaphoreSubmit(swap.GetRenderingDoneFor(i0),
     Stage.AllCommands)])`.
  6. `swap.Present(queue, i0)`.
  7. Acquire `i1` on `fc.ImageAcquired` and call `fc.MarkImageAcquireSignaled()`.
  8. In a second recorder from `fc.CommandBuffers`, blit `OutputReal → i1`.
  9. `fc.Submit(queue, ref rec2, swap, i1, Stage.AllTransfer, Stage.AllCommands)`.
  10. `swap.Present(queue, i1)`.
- `--mode off` presents only `Backbuffer` once per frame. That is the control.

**Edge cases.**

- **First frame and after every resize.** Set `Reset = true`. Make the
  previous-frame matrix equal the current one, so the motion vectors are zero.
- **Resize.** Recreate the swapchain with `ring.WaitForInFlightFences`, then
  call `ring.RecycleStaleAcquireSemaphores()`.
- **`extraAcquire` semaphores.** For every slot whose extra acquire returned
  `Success`/`Suboptimal` and was not consumed by step 5's submit, call
  `pool.Discard(s)` and then `pool.AcquireBinary()`. Track that with a
  `bool[] extraPending`.
- **Feature recreate.** Dispose and recreate the FG feature after
  `device.WaitIdle` (resize is not per-frame).
- **Acquire failure.** If either acquire returns `OutOfDate`, abandon the
  frame and handle it as in `HelloDlaa`'s loop (`Program.cs:434-451`).

**Per-frame inputs.** `BackbufferFrameId` is incremented once per rendered
frame. `Camera` is built from the same unjittered view and projection as the
motion vectors:

- `ViewToClip = proj`, `ClipToView = Invert(proj)`;
- `ClipToPrevClip = Invert(viewProj) * prevViewProj`;
- `PrevClipToClip = Invert(prevViewProj) * viewProj`;
- `Position`/`Up`/`Right`/`Forward` from the inverse view;
- near, far, FOV and aspect from the projection parameters.

All of these are in the row-vector form `docs/ngx-notes.md` §4 uses. Step 19
decides whether `EvaluateCore` transposes.

**Shutdown order.** `device.WaitIdle`, the FG feature, `NgxContext`, the
targets, the swapchain, then the device. Dispose order follows
`docs/ngx-notes.md` and `DlssFeature.Dispose`.

The sample must not allocate per frame. Hoist the `SemaphoreSubmit` spans to
stack locals (`MemoryMarshal.CreateReadOnlySpan(ref local, 1)`, as
`FrameContext.cs:163-171` does).

### 17b. Wire Reflex into HelloFrameGen — the #261 API

All names are `Ahjo.Vulkan` public API from #261. Citations like "D5" in this
step refer to the **#261 spec** unless they say otherwise.

- **Enablement.** At init step 4/5, if
  `gpu.SupportsExtension(VulkanExtensions.NvLowLatency2)`, add
  `VulkanExtensions.NvLowLatency2` and `VulkanExtensions.KhrPresentId` to
  `DeviceDescription.Extensions`, alongside the NGX unions (#261 D2). Do
  **not** push `VkPhysicalDevicePresentIdFeaturesKHR`: the sample passes no
  KHR present ids, because Reflex does not need them (#261 D4/E4).
- **Swapchain.** `new SwapchainDescription { …, LowLatency = device.IsNvLowLatency2Enabled }`.
  Pass the **same** value on every `Recreate`, because a mismatch throws
  `ArgumentException` (#261 D3). The wrapper re-applies the sleep mode after
  each recreate. The sample does not.
- **Mode.** Reflex Low Latency is **on whenever `--mode fg`**, and the user
  cannot turn it off while FG is on (RTX UI Developer Guidelines, UI l. 149-150;
  spec F1).
  - Add `--reflex off|on|boost`, default `on`. Map these once after swapchain
    creation to `swap.SetLatencySleepMode(LatencySleepMode.Off / .On / .OnPlusBoost)`.
  - Combining `--mode fg --reflex off` is a command-line error, exit 2, with a
    message citing the guideline.
  - `--mode off --reflex off` is allowed, because it is the latency baseline.
  - If `device.IsNvLowLatency2Enabled` is false (non-NVIDIA, or a driver
    without the extension), do not call `SetLatencySleepMode`, because it
    throws. Print `"Reflex: unavailable on this GPU — PCL markers only"`. With
    `--mode fg`, FG is unavailable anyway (step 7), so the run is `--mode off`.
- **Per-frame sleep and markers.** These are the #261 D5 sequence, verbatim.
  Per rendered frame N:
  ```
  swap.LatencySleep(TimeSpan.FromSeconds(1))   once per RENDERED frame, before input
  swap.SetLatencyMarker(LatencyMarker.SimulationStart, N)   (emits a pending PCL ping first)
    input, simulation; on left click: SetLatencyMarker(TriggerFlash, N)
  swap.SetLatencyMarker(SimulationEnd, N)
  swap.SetLatencyMarker(RenderSubmitStart, N)
    steps 1-5 of the frame loop (render, FG evaluate, blit → i0, Submit2 rec1)
  swap.SetLatencyMarker(PresentStart, N)
    SetLatencyMarker(OutOfBandPresentStart, N); swap.Present(queue, i0); SetLatencyMarker(OutOfBandPresentEnd, N)
    steps 7-9 (acquire i1, blit OutputReal → i1, fc.Submit rec2)
    SetLatencyMarker(OutOfBandPresentStart, N); swap.Present(queue, i1); SetLatencyMarker(OutOfBandPresentEnd, N)
  swap.SetLatencyMarker(PresentEnd, N)
  swap.SetLatencyMarker(RenderSubmitEnd, N)
  ```
  - `--mode off` uses the same loop with one present and **no** OOB pair.
  - Every marker goes on the thread that presents. In this sample there is
    only one thread.
  - `SetLatencyMarker` sends the Vulkan marker when the swapchain is opted in,
    and sends the mapped PCL marker. The exception is the two OOB present
    markers on an opted-in swapchain: they go to the driver only, because the
    driver writes PCL 11/12 around its real OS present (#261 spec E6/D7).
    Do not call `PclStats.Marker` separately for any of these. That would
    stamp markers twice, and for 11/12 it would put an early timestamp in
    front of the driver's.
  - On an abandoned frame (acquire `OutOfDate`), still emit `SimulationEnd` and
    `RenderSubmitEnd` for N, and do not reuse N.
  - **Two presents per rendered frame** are attributed to frame N by the
    OOB-present markers (#261 D5, Vulkan frame-attribution rule 1). Whether the
    revision-2 driver on the 4070 Ti actually does this is **#261 OPEN-3**,
    measured in 19i. That is the stop condition: if 19i shows it does not, stop
    and report. Do not change the marker sequence or the API from PR C.
- **PCL Stats.** Wire it at startup and shutdown. It runs on every vendor's
  GPU (UI l. 142-143).
  - `PclStats.Initialize(new PclStatsOptions())` before the loop.
  - `window.EnablePclPingHook()`, from #261's `SdlWindow` addition, so the
    ping message calls `PclStats.NotifyPingReceived()`.
  - `PclStats.Shutdown()` in the teardown `finally`.
  - Left-click: `window.ConsumeLeftClick()` → `TriggerFlash`.
- **Frame ids.** Use one `ulong` per rendered frame, **starting at 1**
  (`frameId` 0 throws). The same value goes to the Reflex markers, the PCL
  markers (via `SetLatencyMarker`) and `DlssFrameGenerationInputs.BackbufferFrameId`.
  The Streamline checklist states the same matching rule for its path
  (`ProgrammingGuideDLSS_G.md:951`). KHR present ids are a different number
  space (#261 E4), and the sample uses none.
- **Zero per-frame allocation still holds.** #261's `LowLatencyBenchmarks` and
  `PclStatsBenchmarks` cover `LatencySleep`, `SetLatencyMarker`,
  `GetLatencyTimings` and `PclStats.Marker`. If a call the sample makes is not
  covered there, report it rather than adding a benchmark here.

## 18. Build and validation run (PR C)

Run `dotnet build samples/HelloFrameGen -c Release`. Then, on the 4070 Ti with
`rel/` staged, run `HelloFrameGen --frames 600`. It must exit 0 with
validation on. Record every FG-specific VUID and fix the sample's layouts and
usages until the layer is clean. Any requirement found this way (as
`TransferDst` was for SR) goes into the step 20 doc and into a validation
advisory in `ValidateInputs`, with the #218 "advisory, not enforced" framing.

## 19. The measured contract (PR C) — every MEASURE, with a defined oracle

Use the **dev** DLL (`dev/nvngx_dlssg.dll`, via `DlssSearchPaths`) for a–c, and
**rel** for d–g. Record the driver version once, at the top.

**a. Matrix orientation.** Run with `--freeze` off and orbit the camera, then
enable the visualizer: Ctrl+Shift+V, then F5 to "Clip to Previous Clip Matrix
Test" (FG guide l. 1170-1218).

- Run A: `EvaluateCore` copies the matrices as-is.
- Run B: it copies `Matrix4x4.Transpose(...)`.

Prior, not answer: Streamline documents `clipToPrevClip = clipToView *
viewToViewPrev * viewToClipPrev`, row-major (`ProgrammingGuide.md:1156-1171`,
spec F4). That is row-vector composition, which predicts Run A. The variant
that leaves static cube faces un-highlighted wins. Keep it in
`EvaluateCore` with a comment citing this run, and record both screenshots'
verdict.

**b. `MvecScale` semantics.** Use "Motion Vector Resampling Test Mode".

- Run A: `MotionVectorScale = (1, 1)`, with pixel motion vectors (the guide's
  reading).
- Run B: `(1/RenderWidth, 1/RenderHeight)` (the header's "normalize to
  [-1,1]" reading).

Prior, not answer: Streamline's `mvecScale` "normalize[s] motion vectors (so
the values are in [-1,1] range)" (`ProgrammingGuide.md:1176-1177`), which agrees
with the NGX header and predicts Run B. Record which run highlights only
occlusion edges, and make `DlssFrameGenerationInputs`' doc state the winner.

**c. Jitter.** The sample has no jitter, so record "0, not exercised". The FG
units remain as the header states (clip space). Streamline documents its
`jitterOffset` as "pixel space" (spec F4), so note the conflict. Mark the cross-feature case,
SR + FG with jitter, as **not measured**. It is the Ahjo engine's integration to
measure.

**d. Pacing.** Run PresentMon (`PresentMon.exe -process_name HelloFrameGen.exe
-output_file …`) for 10 s in each of these runs:
- `--present fifo --mode fg`;
- `--present fifo --mode off`;
- `--present immediate --mode fg`;
- **`--present fifo --mode fg --throttle-ms T`**, with T chosen so the
  unthrottled base rate falls to about 0.4 × refresh. This is spec F3's
  breakdown row.
- If the panel supports VRR, repeat the first and fourth runs with VRR on
  (OPEN-5). Otherwise record "VRR not measured".

Report the following in a table: mean and p99 of `MsBetweenDisplayChange`,
displayed frames per second, and the rendered rate (`BackbufferFrameId` delta
per second, printed by the sample).

The expected shape, not asserted beforehand:

- **FIFO fg:** displayed ≈ refresh, rendered ≈ refresh/2, evenly spaced.
- **IMMEDIATE fg:** uneven intervals, and the generated frames are visible for
  a fraction of an interval. This is the negative control.
- **FIFO fg, throttled:** a 1:2-vblank cadence (generated 1, real 2), i.e.
  judder. This proves where FIFO pacing ends.

If PresentMon cannot run, fall back to host timestamps of each `Present`
return and of `AcquireNextImage`, and say so.

**e. Frame count.** Two presents per rendered frame. The sample prints the
present counter / `BackbufferFrameId` ratio at exit.

**f. Still camera (pekkah/ahjo#2783).** Run `--freeze --capture-generated g.png
--capture-real r.png` at frame 300. Report the mean absolute difference between
`g` and `r` with the #219 technique (`docs/ngx-notes.md` §4.1). Expect ≈ 0;
quote the number.

**g. Watermark (OPEN-1, second half).** Capture with `rel/` and inspect it for
the "DLSSG SDK - DO NOT DISTRIBUTE" text. **If it is present, stop and
report.** Do not publish a contract that claims shippable output.

**h. VRAM.** Read `GetHeapBudgets` `Usage` before and after
`CreateFrameGeneration` at 1080p and 1440p, using the method and the VMA-cache
caveat of `docs/ngx-notes.md` §5. Compare against the guide's 218/384 MB
estimates (FG guide l. 153-159).

**i. Latency with Reflex (#261).** Use two sources.
- `swap.GetLatencyTimings(stackalloc LatencyTimings[64])`: the mean of
  `GpuRenderEndTimeUs - SimulationStartTimeUs` over entries with
  `FrameId != 0`. Print it every 120 frames with a `--latency-report` flag
  modelled on #261's HelloCube.
- PresentMon's PC-latency column, which reads PCL Stats.

Report the average PC latency and FPS for each of these runs:
- `--mode off --reflex off`
- `--mode off --reflex on`
- `--mode fg --reflex on`
- `--mode fg --reflex boost`

**#261 OPEN-3 check (stop condition).** In the `--mode fg` runs, confirm three
things:
- `GetLatencyTimings` reports **one** entry per rendered frame id, not two,
  with populated `PresentStartTimeUs`/`PresentEndTimeUs`;
- the FPS reported by timings equals the rendered rate, not twice it;
- PresentMon's PCL falls inside the guide's FG-on band of 3.0-5.7× average
  frame time (Reflex guide l. 1237).

**#261 OPEN-8 check (stop condition).** During one `--mode fg --reflex on` run,
capture an elevated ETW session on the PCL GUID
(`logman create trace … -p {0d216f06-82a6-4d49-bc4f-8f38ae56efab} 0 5 -ets`,
then `tracerpt`). This is the method of `docs/reflex-notes.md` §7.5. Confirm:
- the driver's thread writes **two** 11/12 pairs per rendered frame id N;
- no 11/12 appears on the sample's own thread.

If either does not hold, **stop and report**. Do not change the marker
sequence, the forwarding rule or the #261 API from PR C.

If any of these fails, **stop and report**. Do not change the marker sequence
or the #261 API from PR C.

Also run NVIDIA's Reflex Test Utility (`Reflex_Verification/ReflexTest.exe`,
Reflex guide §4.2) with FG on, and record pass or warnings verbatim. That is
the FG-on evidence for **OPEN-6** (= **#261 OPEN-2**): does a native
`VK_NV_low_latency2` + C# PCL integration pass NVIDIA's verification? #261
recorded the Reflex On / On + Boost passes without FG. If warnings point at the
integration path rather than marker placement, stop and report. Otherwise record
the result, and do not interpret it as certification. Record windowed or
borderless next to every number, because the utility expects fullscreen
exclusive (#261 spec, OPEN section).

## 20. `docs/ngx-frame-generation-notes.md` (fill the PR B stub)

Use the `docs/ngx-notes.md` style: cited or measured, with provenance on every
trap.

1. **What you get and what you supply.** `nvngx_dlssg.dll` is BYO, `rel/` only,
   and the missing-library message is quoted verbatim.
2. **Licence.** The same RTX SDKs licence obligations as `docs/ngx-notes.md`
   §3, plus FG-specific evidence: the watermark text and `FAIL_FeatureDenied`.
   OPEN-4 is quoted, not resolved.
3. **Init order.** Unions of SR and FG extensions, and `RequiredFeatures` vs
   `IsFrameGenerationAvailable`, with step 2's measured extension lists.
4. **The inputs contract.** Motion vectors (same as SR, cite §4.2), depth, the
   UNORM-only colour chain and why, HUD-less/UI premultiply, camera matrices
   (the 19a result), `MvecScale` (19b), jitter units (19c), `Reset`, and
   `BackbufferFrameId`.
5. **Present ownership.** The D5 sequence verbatim, why the wrapper does not
   pace, the FIFO reference and the 19d table, the guide's async-midpoint rule
   as the engine's job, the F3 breakdown table with the measured throttled and
   VRR rows, and step 2's present/latency extension answers.

   On Reflex, say:
   - the NGX API does not need it to run;
   - **NVIDIA's RTX UI Developer Guidelines require it for a title shipping
     FG.** Quote UI l. 88-89, 149-150 and 295 verbatim from the fetched PDF;
   - PCL markers must be on for all vendors;
   - the native `VK_NV_low_latency2` status, quoting spec F1 and OPEN-6
     (= #261 OPEN-2);
   - a link to #261 and `docs/reflex-notes.md` (the Reflex consumer contract),
     how the sample wires Reflex (step 17b: the FG marker sequence, OOB pairs
     around both presents), and the 19i latency table, OPEN-3 check and
     Reflex Test Utility result.
6. **Layouts and usages.** Step 18's results.
7. **VRAM.** Step 19h.
8. **What the wrapper cannot check.** Pacing, matrix and motion-vector
   conventions, and layouts. Name the dev DLL's visualizer modes as the oracle,
   just as the validation layer is the oracle in `docs/ngx-notes.md` §8.
9. **Running HelloFrameGen.** The command line, the DLL resolution order, and
   "CI builds it and never runs it".

Also add a cross-link line to `docs/ngx-notes.md`'s intro, and a
`samples/HelloFrameGen` row to the README samples list if one exists
(check the root `README.md`).

## 21. PR C description

- "Closes #253".
- The D9 measurement table (19a–i) and the driver version.
- "Depends on #261", which is merged.
- The `/run-bench` table from PR B, linked.
- A statement that CI builds but does not run the sample.
- OPEN-4 and OPEN-6 restated for the human.
