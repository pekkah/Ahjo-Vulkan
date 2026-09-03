Paired with [../specs/2026-09-03-issue-216-ngx-native-design.md](../specs/2026-09-03-issue-216-ngx-native-design.md).

# Implementation plan — issue #216, `Ahjo.Vulkan.Ngx.Native`

Fifteen steps, ordered so each one is verifiable on its own. Steps 1–4 need the
NVIDIA SDK staged locally (`./tools/setup-ngx.ps1`); steps 5–8 need it too, for
the regen. Steps 9–15 do not.

Branch from `main` (#215 is a separate PR; if it has not merged, rebase onto it
rather than stacking — see the repo's no-stacked-PRs rule).

Nothing in `src/Ahjo.Vulkan/`, `Recording/`, `Sync/`, `Pools/` or `Memory/` is
touched, so no benchmark moves and `docs/benchmarks.md` is unchanged (spec D9).

---

## 1. `tools/setup-ngx.ps1` — add `-SkipFeatureDll`

New switch parameter `[switch] $SkipFeatureDll`, documented in the comment-based
help next to `-IncludeDocs`.

In the manifest assembly block (currently `tools/setup-ngx.ps1:110-123`), split
`$WinFiles` / `$LinuxFiles` into a client half and a feature half:

- `$WinClientFiles` — `lib/Windows_x86_64/x64/nvsdk_ngx_s.lib`,
  `lib/Windows_x86_64/x64/nvsdk_ngx_s_dbg.lib`
- `$WinFeatureFiles` — the two `nvngx_dlss.dll` entries
- `$LinuxClientFiles` — `lib/Linux_x86_64/libnvsdk_ngx.a`
- `$LinuxFeatureFiles` — the two `libnvidia-ngx-dlss.so.$Bare` entries

Client files are always added for a selected platform; feature files are added
only when `-not $SkipFeatureDll`. The trailing "Feature DLL for running
samples/tests locally" banner prints only when feature files were staged;
otherwise print
`"Feature DLL skipped (-SkipFeatureDll). This staging can build the shim but cannot run DLSS."`

Do **not** touch `pins.sha256`: it is a superset keyed by upstream path, and
skipping files verifies fine against it.

**Verify:** `./tools/setup-ngx.ps1 -Platform all -SkipFeatureDll -Force` stages
`include/`, `NGX-LICENSE.txt`, `staged/win-x64/nvsdk_ngx_s*.lib` and
`staged/linux-x64/libnvsdk_ngx.a`, and creates no `rel/` or `dev/` directory.

## 2. `native/ngx/stubs/` — the two parse-time shims

Committed, hand-maintained, headers only. Model the file comments on
`native/ktx/stubs/stdbool.h`.

- `native/ngx/stubs/wchar.h`:
  ```c
  #pragma once
  typedef __WCHAR_TYPE__ wchar_t;
  ```
  Comment: the NGX headers `#include <wchar.h>` in C mode
  (`nvsdk_ngx_vk.h:76`, `nvsdk_ngx_defs.h:20`) purely so `wchar_t` resolves;
  `__WCHAR_TYPE__` is clang's own predefine for the target, so it follows
  `--target` instead of the host. Not for compilation.
- `native/ngx/stubs/stdbool.h`: the same three macros as
  `native/ktx/stubs/stdbool.h` (`bool`/`true`/`false`), because
  `nvsdk_ngx_defs_vk.h:18` includes it for `NVSDK_NGX_Resource_VK.ReadWrite`.

Add a `native/ngx/stubs/` line to the `native/CLAUDE.md` bullet list alongside
the existing `ktx/stubs/` and `slang/stubs/` mentions.

## 3. `native/ngx/src/ahjo_ngx.h` — the shim header (also the generator input)

New, committed, hand-maintained. Guard with `#ifndef AHJO_NGX_H`, then

```c
#include <vulkan/vulkan.h>
#include "nvsdk_ngx_vk.h"
```

(the NGX headers do not include Vulkan themselves — `nvsdk_ngx_vk.h:69-77`),
then `#ifdef __cplusplus extern "C" {`.

Declare exactly, in this order:

1. `AhjoNgxInitInfo` — the twelve fields, names, order and comments exactly as
   in spec D2. Its C `sizeof` on both RIDs must be 80.
2. `AhjoNgxLayoutId` — an `enum` with these members in this order, terminated by
   `AHJO_NGX_LAYOUT_COUNT`:
   ```
   AHJO_NGX_LAYOUT_RESOURCE_VK_SIZE = 0
   AHJO_NGX_LAYOUT_RESOURCE_VK_ALIGN
   AHJO_NGX_LAYOUT_RESOURCE_VK_OFFSET_RESOURCE
   AHJO_NGX_LAYOUT_RESOURCE_VK_OFFSET_TYPE
   AHJO_NGX_LAYOUT_RESOURCE_VK_OFFSET_READWRITE
   AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_SIZE
   AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_ALIGN
   AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_IMAGE_VIEW
   AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_IMAGE
   AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_SUBRESOURCE_RANGE
   AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_FORMAT
   AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_WIDTH
   AHJO_NGX_LAYOUT_IMAGE_VIEW_INFO_VK_OFFSET_HEIGHT
   AHJO_NGX_LAYOUT_BUFFER_INFO_VK_SIZE
   AHJO_NGX_LAYOUT_BUFFER_INFO_VK_ALIGN
   AHJO_NGX_LAYOUT_BUFFER_INFO_VK_OFFSET_BUFFER
   AHJO_NGX_LAYOUT_BUFFER_INFO_VK_OFFSET_SIZE_IN_BYTES
   AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_SIZE
   AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_ALIGN
   AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_OFFSET_FEATURE_SUPPORTED
   AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_OFFSET_MIN_HW_ARCHITECTURE
   AHJO_NGX_LAYOUT_FEATURE_REQUIREMENT_OFFSET_MIN_OS_VERSION
   AHJO_NGX_LAYOUT_INIT_INFO_SIZE
   AHJO_NGX_LAYOUT_COUNT
   ```
3. The seven function declarations, verbatim from spec D1/D2/D3:
   ```c
   unsigned int     ahjo_ngx_version_api(void);
   unsigned int     ahjo_ngx_layout(AhjoNgxLayoutId id);
   unsigned int     ahjo_ngx_result_to_utf8(NVSDK_NGX_Result result, char* buffer, unsigned int bufferSize);

   NVSDK_NGX_Result ahjo_ngx_vulkan_init_utf8(const AhjoNgxInitInfo* info,
                                              VkInstance instance, VkPhysicalDevice physicalDevice, VkDevice device,
                                              PFN_vkGetInstanceProcAddr getInstanceProcAddr,
                                              PFN_vkGetDeviceProcAddr getDeviceProcAddr);

   NVSDK_NGX_Result ahjo_ngx_vulkan_get_feature_requirements_utf8(
                                              VkInstance instance, VkPhysicalDevice physicalDevice,
                                              NVSDK_NGX_Feature featureId, const AhjoNgxInitInfo* info,
                                              NVSDK_NGX_FeatureRequirement* outRequirement);

   NVSDK_NGX_Result ahjo_ngx_vulkan_get_feature_instance_extension_requirements_utf8(
                                              NVSDK_NGX_Feature featureId, const AhjoNgxInitInfo* info,
                                              unsigned int* outExtensionCount,
                                              VkExtensionProperties** outExtensionProperties);

   NVSDK_NGX_Result ahjo_ngx_vulkan_get_feature_device_extension_requirements_utf8(
                                              VkInstance instance, VkPhysicalDevice physicalDevice,
                                              NVSDK_NGX_Feature featureId, const AhjoNgxInitInfo* info,
                                              unsigned int* outExtensionCount,
                                              VkExtensionProperties** outExtensionProperties);
   ```

No `__declspec(dllexport)` anywhere: the `.def` (step 5) is the single Windows
export list. Header comments should say why the file exists (it is both the
shim's own header and the rsp's parse root) and that `#include` order matters.

## 4. `native/ngx/src/ahjo_ngx.cpp` — the shim implementation

Behaviour, function by function:

- `ahjo_ngx_version_api` returns `NVSDK_NGX_VERSION_API_MACRO`.
- `ahjo_ngx_layout` is a `switch` over every id, each arm `sizeof(...)`,
  `alignof(...)` or `offsetof(...)`; `default: return 0xFFFFFFFFu;`. Include
  `AHJO_NGX_LAYOUT_COUNT` in the default arm.
- `ahjo_ngx_result_to_utf8` calls `GetNGXResultAsString`, narrows the returned
  `const wchar_t*` to UTF-8 into `buffer`, and returns the byte count including
  the terminating NUL. If `buffer` is null or `bufferSize` is too small, write
  nothing and return the required size. Never returns 0 for a known result.
- Two internal helpers, hand-rolled, no CRT locale functions and no
  `MultiByteToWideChar` (spec D2): `AhjoUtf8ToWide(const char*)` and
  `AhjoWideToUtf8(const wchar_t*, char*, unsigned int)`. Both must handle
  `wchar_t` being 2 bytes (UTF-16, surrogate pairs) and 4 bytes (UTF-32) —
  select on `sizeof(wchar_t)` with `if constexpr`, not on `_WIN32`. Invalid
  UTF-8 is replaced with U+FFFD rather than rejected; a path this deep in is not
  the place to invent an error code.
- `ahjo_ngx_vulkan_init_utf8`:
  1. `if (info == nullptr || info->StructSize != sizeof(AhjoNgxInitInfo)) return NVSDK_NGX_Result_FAIL_InvalidParameter;`
  2. Heap-allocate and **retain forever** (spec D8): the widened
     `ApplicationDataPath`, the widened search-path array plus each element, and
     one `NVSDK_NGX_FeatureCommonInfo` populated from `info`. Retention is a
     `static std::vector<void*>` behind a `std::mutex`; add a comment citing
     `nvsdk_ngx_defs.h:405` and the fact that search paths are read at
     `CreateFeature1` time.
  3. Dispatch on `info->IdentifierType`:
     `NVSDK_NGX_Application_Identifier_Type_Project_Id` →
     `NVSDK_NGX_VULKAN_Init_with_ProjectID(...)`, otherwise
     `NVSDK_NGX_VULKAN_Init(info->ApplicationId, ...)`. Both get
     `NVSDK_NGX_Version_API` as the last argument.
- The three `*_requirements_utf8` shims build a local
  `NVSDK_NGX_FeatureDiscoveryInfo` + `NVSDK_NGX_FeatureCommonInfo` from `info`
  (same `StructSize` guard, same identifier dispatch, `SDKVersion` stamped by
  the shim), call the corresponding `NVSDK_NGX_VULKAN_GetFeature*` and **free
  their widened storage before returning** (spec D8). Small path counts should
  use a stack buffer; fall back to the heap above a fixed threshold.

Guard the C++ standard at the top of the file the way `native/vma/src/vma.cpp`
does (`#if !(__cplusplus >= 201703L || _MSVC_LANG >= 201703L) #error ...`).

## 5. `native/ngx/src/ahjo_ngx.def` and `native/ngx/src/ahjo_ngx.map`

Both list the same 27 names, and only those (spec D1). `.def`:

```
LIBRARY ahjo_ngx
EXPORTS
    NVSDK_NGX_Parameter_SetULL
    NVSDK_NGX_Parameter_SetF
    NVSDK_NGX_Parameter_SetD
    NVSDK_NGX_Parameter_SetUI
    NVSDK_NGX_Parameter_SetI
    NVSDK_NGX_Parameter_SetVoidPointer
    NVSDK_NGX_Parameter_GetULL
    NVSDK_NGX_Parameter_GetF
    NVSDK_NGX_Parameter_GetD
    NVSDK_NGX_Parameter_GetUI
    NVSDK_NGX_Parameter_GetI
    NVSDK_NGX_Parameter_GetVoidPointer
    NVSDK_NGX_VULKAN_Shutdown1
    NVSDK_NGX_VULKAN_AllocateParameters
    NVSDK_NGX_VULKAN_GetCapabilityParameters
    NVSDK_NGX_VULKAN_DestroyParameters
    NVSDK_NGX_VULKAN_GetScratchBufferSize
    NVSDK_NGX_VULKAN_CreateFeature1
    NVSDK_NGX_VULKAN_ReleaseFeature
    NVSDK_NGX_VULKAN_EvaluateFeature_C
    ahjo_ngx_version_api
    ahjo_ngx_layout
    ahjo_ngx_result_to_utf8
    ahjo_ngx_vulkan_init_utf8
    ahjo_ngx_vulkan_get_feature_requirements_utf8
    ahjo_ngx_vulkan_get_feature_instance_extension_requirements_utf8
    ahjo_ngx_vulkan_get_feature_device_extension_requirements_utf8
```

`.map` is a version script with the same 27 in `global:` and `local: *;`:

```
AHJO_NGX_1.0 {
    global:
        NVSDK_NGX_Parameter_SetULL;
        …
        ahjo_ngx_vulkan_get_feature_device_extension_requirements_utf8;
    local:
        *;
};
```

Both files carry a one-line header comment: *"This list is checked against
`ahjo_ngx.map` / `ahjo_ngx.def` and against the generated P/Invokes by
`NgxExportDriftTests`. Add a name here and there, or the test fails."*

The parser in step 12 must be able to read both, so keep the formatting
regular: one name per line, `.def` names indented, `.map` names
semicolon-terminated.

## 6. `native/ngx/CMakeLists.txt`

Hand-maintained, modelled on `native/vma/CMakeLists.txt`.

- `cmake_minimum_required(VERSION 3.21)`, `project(ahjo_ngx LANGUAGES CXX)`,
  `cmake_policy(SET CMP0091 NEW)`.
- `CMAKE_CXX_STANDARD 17`, `CXX_STANDARD_REQUIRED ON`, `CXX_EXTENSIONS OFF`,
  `POSITION_INDEPENDENT_CODE ON`.
- Required cache inputs, each with a `FATAL_ERROR` when unset:
  `AHJO_NGX_INCLUDE_DIR` (`native/ngx/include`),
  `AHJO_NGX_LIB_DIR` (`native/ngx/staged/<rid>`),
  `AHJO_VULKAN_HEADERS_DIR` (`native/include`).
- Locate the static library explicitly and `FATAL_ERROR` with the
  `./tools/setup-ngx.ps1` command in the message if it is absent:
  `nvsdk_ngx_s.lib` on MSVC, `libnvsdk_ngx.a` elsewhere.
- `add_library(ahjo_ngx SHARED src/ahjo_ngx.cpp)`, `OUTPUT_NAME "ahjo_ngx"`
  (no `PREFIX` override — CMake already produces `ahjo_ngx.dll` /
  `libahjo_ngx.so`).
- **MSVC:** `MSVC_RUNTIME_LIBRARY "MultiThreaded"` (the archive is the static-CRT
  release build — spec E3; comment that fact), `/DEF:` the `.def`,
  `target_link_libraries(ahjo_ngx PRIVATE <static lib> kernel32 user32 advapi32)`,
  `/W4` with the same "upstream warnings are not our build breaks" comment style
  VMA uses.
- **GCC/Clang:** `-fvisibility=hidden` on our TU,
  `-Wl,--whole-archive <static lib> -Wl,--no-whole-archive`,
  `-Wl,--version-script=${CMAKE_CURRENT_SOURCE_DIR}/src/ahjo_ngx.map`,
  and `stdc++ dl pthread`. Comment that E3 measured the archive's undefined set
  and it contains no D3D/CUDA/NvAPI import, which is why `--whole-archive` is
  safe.

**Verify:** configure + build locally on Windows; `dumpbin /exports` (or
`llvm-nm`) lists exactly the 27 names and nothing else.

## 7. `tools/generate-ngx.rsp`

New file. Structure and comment density follow `tools/generate-ktx.rsp` and
`tools/generate-slang.rsp` — every non-obvious line gets the *why*, citing the
spec's evidence tags.

```
--file
native/ngx/src/ahjo_ngx.h
--traverse
native/ngx/src/ahjo_ngx.h
native/ngx/include/nvsdk_ngx_vk.h
native/ngx/include/nvsdk_ngx_defs.h
native/ngx/include/nvsdk_ngx_defs_vk.h
native/ngx/include/nvsdk_ngx_params.h
--include-directory
native/ngx/stubs
--include-directory
native/stubs
--include-directory
native/include
--include-directory
native/ngx/include
--namespace
Ahjo.Vulkan.Ngx.Native
--methodClassName
NgxApi
--libraryPath
ahjo_ngx
--output
src/Ahjo.Vulkan.Ngx.Native/Generated
--output-mode
CSharp
--language
c
--additional
--target=x86_64-unknown-linux-gnu
--config
latest-codegen
generate-helper-types
generate-file-scoped-namespaces
generate-macro-bindings
generate-unmanaged-constants
generate-disable-runtime-marshalling
generate-aggressive-inlining
exclude-enum-operators
multi-file
--remap
size_t=nuint
VkInstance_T=Ahjo.Vulkan.Native.VkInstance_T
VkPhysicalDevice_T=Ahjo.Vulkan.Native.VkPhysicalDevice_T
VkDevice_T=Ahjo.Vulkan.Native.VkDevice_T
VkCommandBuffer_T=Ahjo.Vulkan.Native.VkCommandBuffer_T
VkImage_T=Ahjo.Vulkan.Native.VkImage_T
VkImageView_T=Ahjo.Vulkan.Native.VkImageView_T
VkBuffer_T=Ahjo.Vulkan.Native.VkBuffer_T
VkFormat=Ahjo.Vulkan.Native.VkFormat
VkImageSubresourceRange=Ahjo.Vulkan.Native.VkImageSubresourceRange
VkExtensionProperties=Ahjo.Vulkan.Native.VkExtensionProperties
--exclude
  … (see below)
--with-access-specifier
*=public
```

Three house-style deltas, each of which needs its own comment block in the file:

- `generate-macro-bindings` **added** — without it the 204
  `NVSDK_NGX_Parameter_*` u8 constants do not exist at all (spec E6).
- `strip-enum-member-type-name` **absent** — with it, `NVSDK_NGX_Result`'s
  sibling-referencing initializers produce 18 × `CS0103` (spec E8). Do not add
  it back.
- The `NVSDK_NGX_EParameter_*` family **excluded** — 74 constants that embed raw
  `0x01`–`0x1f` bytes in committed source (spec E7).

The `--exclude` block, in this order with a comment per group:

1. Wide-string structs and their sub-structs (spec D2/E5):
   `NVSDK_NGX_PathListInfo`, `NVSDK_NGX_LoggingInfo`,
   `NVSDK_NGX_FeatureCommonInfo`, `NVSDK_NGX_FeatureCommonInfo_Internal`,
   `NVSDK_NGX_FeatureDiscoveryInfo`, `NVSDK_NGX_ProjectIdDescription`,
   `NVSDK_NGX_Application_Identifier`.
2. Entry points the shim does not export (spec D1): `NVSDK_NGX_VULKAN_Init`,
   `NVSDK_NGX_VULKAN_Init_with_ProjectID`,
   `NVSDK_NGX_VULKAN_GetFeatureRequirements`,
   `NVSDK_NGX_VULKAN_GetFeatureInstanceExtensionRequirements`,
   `NVSDK_NGX_VULKAN_GetFeatureDeviceExtensionRequirements`,
   `GetNGXResultAsString`, `NVSDK_NGX_VULKAN_RequiredExtensions`,
   `NVSDK_NGX_VULKAN_CreateFeature`.
3. D3D parameter accessors: `NVSDK_NGX_Parameter_SetD3d11Resource`,
   `NVSDK_NGX_Parameter_SetD3d12Resource`,
   `NVSDK_NGX_Parameter_GetD3d11Resource`,
   `NVSDK_NGX_Parameter_GetD3d12Resource`.
4. Foreign-API opaque declarations the headers forward-declare: `IUnknown`,
   `ID3D11Resource`, `ID3D11Buffer`, `ID3D11Texture2D`, `ID3D12Resource`,
   `D3D11_TEXTURE2D_DESC`, `D3D11_BUFFER_DESC`, `D3D12_RESOURCE_DESC`,
   `CD3DX12_HEAP_PROPERTIES`, `NVSDK_NGX_CUDADevice`,
   `NVSDK_NGX_DLDenoise_Create_Params`.
5. The 74 `NVSDK_NGX_EParameter_*` macros. Generate the list mechanically rather
   than typing it:
   ```bash
   grep -o '^#define \(NVSDK_NGX_EParameter_[A-Za-z0-9_]*\)' \
     native/ngx/include/nvsdk_ngx_defs.h | sed 's/^#define //'
   ```

Then add a `tools/CLAUDE.md` row:
`generate-ngx.rsp` → `src/Ahjo.Vulkan.Ngx.Native/Generated/` (from
`native/ngx/src/ahjo_ngx.h` + the pinned NGX headers; C mode; **the only rsp
that turns `generate-macro-bindings` on and the only one that leaves
`strip-enum-member-type-name` off — read its header comments before touching
either**), and mention `NgxVersion` in the pin sentence.

## 8. `src/Ahjo.Vulkan.Ngx.Native/Ahjo.Vulkan.Ngx.Native.csproj`

Model on `Ahjo.Vulkan.Vma.Native.csproj` for the cmake/host-RID mechanics and on
`Ahjo.Vulkan.Slang.Native.csproj` for the pack/licence mechanics.

Properties:

- `RootNamespace` / `AssemblyName` `Ahjo.Vulkan.Ngx.Native`,
  `GeneratedDir`, `GeneratorResponseFile = $(ToolsDir)generate-ngx.rsp`.
- `IsPackable=true`, `PackageId`/`Title` `Ahjo.Vulkan.Ngx.Native`,
  `MinVerTagPrefix=v`, `MinVerDefaultPreReleaseIdentifiers=alpha.0`.
  `Description`: raw P/Invoke bindings over the NVIDIA NGX (DLSS) Vulkan C API
  at `$(NgxVersion)`, shipping a shim for `win-x64` and `linux-x64`; must state
  that **`nvngx_dlss.dll` is not included and is supplied by the application**.
  `PackageTags`: `vulkan;dlss;ngx;nvidia;upscaling;dlaa;native;pinvoke;bindings`.
- `_NgxRid`: `win-x64` / `linux-x64` only. Anything else leaves it empty — that
  is not an error, it just means no shim on this host.
- `_NgxLibFile`: `ahjo_ngx.dll` / `libahjo_ngx.so`.
- `_NgxSrcDir = $(NativeDir)ngx\`, `_NgxIncludeDir = $(_NgxSrcDir)include\`,
  `_NgxStagedDir = $(_NgxSrcDir)staged\$(_NgxRid)\`,
  `_NgxBuildRootDir = $(_NgxSrcDir)build\`,
  `_NgxBuildDir = $(_NgxBuildRootDir)$(_NgxRid)\`,
  `_NgxStagedBinary = $(_NgxBuildDir)$(_NgxLibFile)`,
  `_NgxStaticLib` = `$(_NgxStagedDir)nvsdk_ngx_s.lib` on Windows,
  `$(_NgxStagedDir)libnvsdk_ngx.a` on Linux.
- `_VulkanHeadersIncludeDir = $(NativeDir)include\`.

Items: `MinVer`, `Microsoft.SourceLink.GitHub`, `README.md` packed at root, and
`<ProjectReference Include="..\Ahjo.Vulkan.Native\Ahjo.Vulkan.Native.csproj" />`
(spec D5 — ten `Vk*` remaps make this a genuine Vulkan consumer).

Targets:

- `EnsureVulkanHeaders` — identical to VMA's (invoke `CopyGeneratedHeaders` on
  `Ahjo.Vulkan.Native`).
- `Regenerate` → `DependsOnTargets="EnsureVulkanHeaders;RunClangSharpNgxGenerator"`.
  No fetch target: the headers are committed by #215.
  `RunClangSharpNgxGenerator` runs
  `dotnet tool run ClangSharpPInvokeGenerator @"$(GeneratorResponseFile)"` from
  `$(RepositoryRoot)`. Add an `<Error>` before it if
  `!Exists('$(_NgxIncludeDir)nvsdk_ngx_vk.h')`, naming `./tools/setup-ngx.ps1`.
- `BuildNgxForHost`, `BeforeTargets="AssignTargetPaths"`, with
  `Condition="'$(_NgxRid)' != '' and '$(SkipNgxNativeBuild)' != 'true' and Exists('$(_NgxStaticLib)') and !Exists('$(_NgxStagedBinary)')"`,
  `DependsOnTargets="EnsureVulkanHeaders"`. Two `Exec`s (cmake configure with
  `-DAHJO_NGX_INCLUDE_DIR`, `-DAHJO_NGX_LIB_DIR`, `-DAHJO_VULKAN_HEADERS_DIR`;
  then `cmake --build --config Release`) and the VMA-style normalization copy
  out of MSVC's `Release\` subdirectory.
- `WarnNgxSdkMissing`, same `BeforeTargets`, with the inverse condition
  (`_NgxRid` non-empty, not skipped, `!Exists('$(_NgxStaticLib)')`): a single
  `<Message Importance="high">` reading
  `"NGX SDK not staged at $(_NgxStagedDir); skipping the ahjo_ngx shim build. Run ./tools/setup-ngx.ps1 to enable DLSS locally. Ahjo.Vulkan.Ngx.Native.Tests will skip."`
  **`<Message>`, not `<Warning>`** — `TreatWarningsAsErrors=true` is repo-wide
  (spec D4).
- A `None` item staging `$(_NgxStagedBinary)` with
  `CopyToOutputDirectory="PreserveNewest" Pack="false" Link="$(_NgxLibFile)"`,
  conditioned on the file existing.
- `PackNgxRuntimes`, `BeforeTargets="_GetPackageFiles;GenerateNuspec"`: for
  `win-x64`/`linux-x64`, pack
  `$(_NgxBuildRootDir)<rid>\<libfile>` → `runtimes\<rid>\native\<libfile>` when
  it exists, plus `$(_NgxSrcDir)NGX-LICENSE.txt` → `\` (the
  `PackSlangRuntimes` licence pattern).

Also:

- `src/Ahjo.Vulkan.Ngx.Native/README.md` — package README. Must say, in the
  first screen: this package contains **no feature DLL**; the application ships
  `nvngx_dlss.dll` from NVIDIA beside its executable; `dev/` builds must never
  be redistributed; the licence obligations from `NGX-LICENSE.txt` are the
  consumer's.
- `src/Ahjo.Vulkan.Ngx.Native/CLAUDE.md` — modelled on
  `src/Ahjo.Vulkan.Slang.Native/CLAUDE.md`. Must state: the export list lives in
  three places and `NgxExportDriftTests` is what keeps them equal; the
  `wchar_t` rule (nothing wide may ever appear in `Generated/`; if a regen
  introduces one, exclude the struct — do not remap `wchar_t`); why the rsp
  turns `generate-macro-bindings` on and `strip-enum-member-type-name` off; and
  that the shim build is opt-in on a staged SDK.
- Add the project and its test project to `Ahjo.Vulkan.slnx`.
- `.gitignore`: add `native/ngx/build/` next to the existing
  `native/ngx/staged/` block, with a one-line reason.

## 9. Generate and commit the bindings

```bash
./tools/setup-ngx.ps1 -SkipFeatureDll
dotnet build src/Ahjo.Vulkan.Ngx.Native -t:Regenerate
dotnet build src/Ahjo.Vulkan.Ngx.Native -c Release
```

Expected output, from a real trial run of this exact configuration: **35 files,
27 `DllImport`s (20 `NVSDK_NGX_*` + 7 `ahjo_ngx_*`), 0 warnings, 0 errors.**
Two checks that must pass before committing `Generated/`:

- `grep -r wchar_t src/Ahjo.Vulkan.Ngx.Native/Generated` returns **nothing**.
- `grep -rc 'NVSDK_NGX_EParameter' src/Ahjo.Vulkan.Ngx.Native/Generated` returns
  **0**.

Never hand-edit `Generated/`; if either check fails, fix the rsp and regenerate.

## 10. `Directory.Build.props` and `README.md` — the package list

- `Directory.Build.props`: extend the "publishable projects" comment block with
  `src/Ahjo.Vulkan.Ngx.Native (Ahjo.Vulkan.Ngx.Native)` and change "six in all"
  to "seven in all". Leave `NgxVersion` where it is; add one sentence to its
  comment noting that a bump now also requires `/regen-bindings`.
- `README.md`: "six packages" → "seven packages" at line 5, add the
  `Ahjo.Vulkan.Ngx.Native` bullet after `Ahjo.Vulkan.Slang` (leading with
  "**the feature DLL is not included**"), add the `src/` and `tests/` inventory
  lines around lines 108 and 118, add the regen command near line 136, and
  update the "All six packages … share a single `v*` tag" sentence at line 162.

## 11. `.claude/skills/regen-bindings/SKILL.md`

- Change "the three Native projects (Vulkan, VMA, libktx)" to name all five.
- Add the missing `Ahjo.Vulkan.Slang.Native` row (spec E14) **and** the new row:
  `dotnet build src/Ahjo.Vulkan.Ngx.Native -t:Regenerate` | `NgxVersion` |
  `tools/generate-ngx.rsp` | prerequisite: `./tools/setup-ngx.ps1` must have
  staged `native/ngx/include/` (no network needed once staged; the headers are
  committed).
- Add `src/Ahjo.Vulkan.Ngx.Native/Generated/` to the "generated code is
  generated" sentence and
  `dotnet test tests/Ahjo.Vulkan.Ngx.Native.Tests` to the step-4 list, noting it
  needs the shim built.

## 12. `tests/Ahjo.Vulkan.Ngx.Native.Tests`

New xUnit v3 project, `PackageReference`s matching
`Ahjo.Vulkan.Slang.Native.Tests.csproj`, one `ProjectReference` to
`src/Ahjo.Vulkan.Ngx.Native`. Copy `native/ngx/src/ahjo_ngx.def` and
`native/ngx/src/ahjo_ngx.map` to the output directory as `None` /
`CopyToOutputDirectory="PreserveNewest"` so step 12.1 can read them.

`NgxShimFixture` (internal static): tries `NativeLibrary.TryLoad("ahjo_ngx",
typeof(NgxApi).Assembly, null, out var handle)`. Exposes `IsAvailable` and the
handle. Every test begins with

```csharp
if (!NgxShimFixture.IsAvailable) { NgxShimFixture.SkipOrFail(); return; }
```

where `SkipOrFail()` throws when `AHJO_NGX_REQUIRE_SHIM` is `1` — message:
`"AHJO_NGX_REQUIRE_SHIM=1 but ahjo_ngx could not be loaded. The lane that sets this variable is required to have built the shim."` — and otherwise
`Assert.Skip("ahjo_ngx is not built. Run ./tools/setup-ngx.ps1 then rebuild src/Ahjo.Vulkan.Ngx.Native.")` (spec D4). No `TestGate`: this suite is
outside the wrapper's tier system, like the ktx and slang native suites.

Concrete cases:

**12.1 `NgxExportDriftTests`**
- `RequiredExports` — a literal `string[]` of the 27 names from step 5, grouped
  and commented the way `SlangExportDriftTests.RequiredExports` is.
- `EveryRequiredExport_IsPresentInTheShippedShim` — `TryGetExport` each;
  aggregate the misses into one message.
- `DefFile_ListsExactlyTheRequiredExports` — parse `ahjo_ngx.def` (skip
  `LIBRARY`/`EXPORTS`, trim), assert set equality both ways.
- `MapFile_ListsExactlyTheRequiredExports` — parse `ahjo_ngx.map` between
  `global:` and `local:`, strip trailing `;`, assert set equality both ways.

**12.2 `NgxStructLayoutTests`**
- `ResourceVk_LayoutMatchesTheShim` — assert against `ahjo_ngx_layout` for
  size, alignment and the three offsets; the managed offsets come from
  `(byte*)&value.Field - (byte*)&value`. Expected values (measured, and the
  test should assert the literals too so a *matching pair* of wrong values still
  fails): size 56, align 8, offsets 0 / 48 / 52.
- `ImageViewInfoVk_LayoutMatchesTheShim` — size 48, align 8, offsets
  0 / 8 / 16 / 36 / 40 / 44.
- `BufferInfoVk_LayoutMatchesTheShim` — size 16, align 8, offsets 0 / 8.
- `FeatureRequirement_LayoutMatchesTheShim` — size 264, align 4, offsets
  0 / 4 / 8.
- `InitInfo_SizeMatchesTheShim` — 80; this is the oracle for the `StructSize`
  guard in `ahjo_ngx_vulkan_init_utf8`.
- `EveryLayoutId_IsCoveredByThisSuite` — loop `0 .. (uint)AHJO_NGX_LAYOUT_COUNT - 1`,
  assert `ahjo_ngx_layout(id) != 0xFFFFFFFF`, and assert that the count matches
  the number of ids the tests above actually query. A native id added without a
  managed assertion fails here.
- `ReadWriteField_IsAOneByteBoolAtOffset52` — pins spec E11 so Phase 2 cannot
  silently regress the `true` vs `1` question: assert
  `sizeof(bool) == 1`, the offset, and that writing `true` sets the byte at
  offset 52 to a non-zero value.

**12.3 `NgxSmokeTests`**
- `VersionApi_MatchesTheGeneratedBindings` —
  `ahjo_ngx_version_api() == (uint)NVSDK_NGX_Version.NVSDK_NGX_Version_API`
  (expected `0x15`).
- `ResultToUtf8_ProducesAsciiForKnownResults` — for `Success`,
  `FAIL_FeatureNotFound` and `FAIL_FeatureNotSupported`: a `stackalloc byte[128]`
  fills with a non-empty, NUL-terminated, all-ASCII string, and the returned
  length matches the NUL position + 1.
- `ResultToUtf8_WithNullBuffer_ReturnsRequiredSize` — the null-buffer call
  returns the same length the successful call reported.
- `LayoutQuery_UnknownId_ReturnsSentinel` —
  `ahjo_ngx_layout((AhjoNgxLayoutId)0xDEAD) == 0xFFFFFFFF`.
- `GetFeatureInstanceExtensionRequirements_OnADriverlessHost_FailsCleanly` —
  build an `AhjoNgxInitInfo` with `StructSize` set, project-id identity, a UTF-8
  temp path, no search paths, no log callback; call
  `ahjo_ngx_vulkan_get_feature_instance_extension_requirements_utf8` with
  `NVSDK_NGX_Feature_SuperSampling`; assert the result is **not** `Success` and
  the process is still alive. See **OPEN-1** below before writing this one.
- `InitInfo_WithWrongStructSize_IsRejected` — set `StructSize = 4`, call
  `ahjo_ngx_vulkan_init_utf8` with all-null Vulkan handles, assert
  `FAIL_InvalidParameter`. This must not reach NGX, so the null handles are safe.

Add a `tests/CLAUDE.md` row and a rule bullet: the NGX native suite must not
acquire a Vulkan device (the shim links no `vulkan-1`); it skips without a
staged SDK and **fails instead of skipping** when `AHJO_NGX_REQUIRE_SHIM=1`.

## 13. `.github/workflows/build-ngx-native.yml`

New reusable workflow, structured exactly like `build-ktx-native.yml` (build and
test in the same job, before uploading — the #144 rule).

- Triggers: `workflow_call: {}`, `workflow_dispatch: {}`, and `push` to
  `main`/`master` filtered on `native/ngx/**`,
  `src/Ahjo.Vulkan.Ngx.Native/**`, `tests/Ahjo.Vulkan.Ngx.Native.Tests/**`,
  `tools/setup-ngx.ps1`, `Directory.Build.props`, and the workflow itself.
- Matrix: `win-x64` on `windows-latest`, `linux-x64` on `ubuntu-latest`,
  `fail-fast: false`. Comment that no other RID exists upstream (#214), so this
  is not the usual "add the lane first" note.
- Cache `native/ngx/staged/${{ matrix.rid }}` keyed on
  `hashFiles('Directory.Build.props', 'native/ngx/pins.sha256', 'tools/setup-ngx.ps1')`.
  The cached content is the SDK client library, not our output — comment that
  the shim is rebuilt every run because it is cheap and it is what the tests
  execute.
- Steps: checkout → cache → setup-dotnet → **`pwsh ./tools/setup-ngx.ps1
  -Platform ${{ matrix.rid }} -SkipFeatureDll`** (comment: this is what makes it
  structurally impossible for CI to pull a feature DLL — #214) →
  `dotnet build src/Ahjo.Vulkan.Ngx.Native -c Release` → `ls -la` the build dir →
  `dotnet test tests/Ahjo.Vulkan.Ngx.Native.Tests -c Release -l "console;verbosity=detailed"`
  with `env: AHJO_NGX_REQUIRE_SHIM: "1"` → `upload-artifact` named
  `ngx-native-${{ matrix.rid }}` from `native/ngx/build/${{ matrix.rid }}/`,
  `if-no-files-found: error`.
- Comment block, following `build-slang-native.yml`'s: no Vulkan loader and no
  ICD are provisioned and `AHJO_VULKAN_TIER` stays unset, because the shim links
  no `vulkan-1`; and this lane cannot evaluate DLSS — there is no NVIDIA driver
  on any hosted runner.

## 14. `ci.yml`, `publish.yml`, and the coverage doc

- `ci.yml`: add an `ngx-native:` job — `uses: ./.github/workflows/build-ngx-native.yml`
  — with the same "the definition lives in the reusable workflow so CI and the
  release path cannot disagree" comment `ktx-native` and `slang-native` carry,
  plus one sentence stating what this lane does and does not prove (loads,
  resolves 27 exports, agrees on struct layout; does **not** run DLSS).
  Add `-p:SkipNgxNativeBuild=true` to the Windows `build-test` job's
  `dotnet build Ahjo.Vulkan.slnx` line, next to the existing
  `-p:SkipKtxNativeBuild=true`, with a matching comment.
- `publish.yml`: add `include_ngx` to the `workflow_dispatch` inputs (default
  `true`), a `build-ngx:` job calling the reusable workflow, `build-ngx` in
  `needs` and in the `always() && (… == 'success' || … == 'skipped')` guard, a
  "Download NGX native artifacts" step (`pattern: ngx-native-*`), a "Stage NGX
  binaries for pack" step copying into `native/ngx/build/<rid>/` for
  `win-x64 linux-x64`, and a "Pack (NGX native)" step running
  `dotnet pack src/Ahjo.Vulkan.Ngx.Native/Ahjo.Vulkan.Ngx.Native.csproj -c Release -p:SkipNgxNativeBuild=true -o artifacts`.
- `.github/CLAUDE.md`: a `## ngx-native lane` section after the `slang-native`
  one. It must say: the SDK is fetched per-run with `-SkipFeatureDll` so no
  feature DLL can enter CI; the lane proves load + export resolution + struct
  layout and **cannot** evaluate DLSS because no hosted runner has an NVIDIA
  driver; `AHJO_NGX_REQUIRE_SHIM=1` is what stops it reporting green while
  executing nothing; and it is a build-artifact check — **don't grow it**.
- `docs/ci-coverage.md`: add an `ngx-native` row to the "Where each lane stands"
  table with `Declared: —` and the reason ("outside the tier system by contract:
  the shim links no `vulkan-1`; DLSS evaluation needs an NVIDIA driver, which no
  hosted runner has"). While in that table, add the missing `slang-native` row
  too (spec E14) — one line, no other change.

## 15. Full verification pass

```bash
dotnet build Ahjo.Vulkan.slnx -c Release
dotnet test tests/Ahjo.Vulkan.Ngx.Native.Tests -c Release
dotnet test                                   # nothing else may have moved
```

Plus, by hand:

1. **Fresh-clone simulation.** With `native/ngx/staged/` renamed away,
   `dotnet build Ahjo.Vulkan.slnx` must succeed, print the
   `WarnNgxSdkMissing` message, produce **zero** warnings, and
   `dotnet test tests/Ahjo.Vulkan.Ngx.Native.Tests` must skip rather than fail.
   Then `AHJO_NGX_REQUIRE_SHIM=1 dotnet test tests/Ahjo.Vulkan.Ngx.Native.Tests`
   must **fail**.
2. **Export surface.** `dumpbin /exports ahjo_ngx.dll` (or `llvm-nm -D`) lists
   exactly 27 names.
3. **Package contents.** `dotnet pack src/Ahjo.Vulkan.Ngx.Native` and unzip the
   nupkg: it must contain `runtimes/win-x64/native/ahjo_ngx.dll` (host RID
   only, locally), `NGX-LICENSE.txt`, `LICENSE`, `README.md` — and **no**
   `nvngx_dlss.dll` and no `.lib`/`.a`. Grep the nupkg listing for `nvngx` and
   expect nothing.
4. **AOT.** `dotnet publish samples/AotSmoke -c Release -r win-x64
   -p:IlcUseEnvironmentalTools=true` still publishes clean. (AotSmoke does not
   reference the NGX package; this is a regression check on the solution, not on
   NGX.)

---

## OPEN — stop and ask rather than improvise

- **OPEN-1 — RESOLVED 2026-09-03: the call is driver-independent; the premise
  was wrong.** Spec OPEN-1 carries the full record.

  *As originally written:* the driverless behaviour of
  `ahjo_ngx_vulkan_get_feature_instance_extension_requirements_utf8` had never
  been executed, because the shim did not exist. Step 12.3 was to assert a
  non-`Success` result, and to stop and report on a fault, a hang, or a
  `Success` with a bogus count.

  *Outcome:* neither branch applied. The call returns `Success` with
  `extensionCount` 1 and `VK_KHR_get_physical_device_properties2` specVersion 2
  on a driverless `windows-latest` runner **and** on an RTX 4070 Ti with driver
  610.47 — identical, because it is a static query answered out of NVIDIA's
  client library that never loads the driver-side core. Step 12.3's test now
  asserts only the host-independent property (returns without faulting; a
  `Success` carries a plausible count and a non-null array) and is named
  `GetFeatureInstanceExtensionRequirements_ReturnsCleanly_OnAnyHost`. The
  contingency — reducing the lane to the four driver-free assertions — was not
  needed. Nothing was weakened to "any result is acceptable".
- **OPEN-2 — `NVSDK_NGX_VULKAN_AllocateParameters` before `Init`.** Spec
  OPEN-2. It is exported and bound, but nothing asserts on it. Leave it that
  way; do not add a lane assertion on a guess about whether it needs the driver.
- **OPEN-3 — RESOLVED 2026-09-03: keep fetching it.** `nvsdk_ngx_s_dbg.lib`
  stays in the fetch manifest and in `pins.sha256`, against a future debug-shim
  variant. This plan still never links it — the shim is always the `/MT` release
  build against `nvsdk_ngx_s.lib`. Do not remove it from the manifest in step 1,
  and do not add a debug shim: that remains a separate decision.
