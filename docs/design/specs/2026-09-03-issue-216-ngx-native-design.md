# `Ahjo.Vulkan.Ngx.Native` — the NGX shim, its export contract, and the `wchar_t` boundary

**Issue:** [#216](https://github.com/pekkah/Ahjo-Vulkan/issues/216) — *NGX Phase 1: Ahjo.Vulkan.Ngx.Native — shim, bindings, CI lane*
**Phase 1 of:** [#214](https://github.com/pekkah/Ahjo-Vulkan/issues/214) (tracking; research summary and the fixed ship-model decisions)
**Depends on:** [#215](https://github.com/pekkah/Ahjo-Vulkan/pull/215) (the pinned fetch step — `tools/setup-ngx.ps1`, `native/ngx/`)
**Lands consistently with:** [#166](https://github.com/pekkah/Ahjo-Vulkan/issues/166) (`Ahjo.Vulkan.Slang.Native` package/lane shape), [#144](https://github.com/pekkah/Ahjo-Vulkan/issues/144) (a shipped native binary must be executed by the job that produces it), [#32](https://github.com/pekkah/Ahjo-Vulkan/issues/32) (no Linux wrapper lanes)
**Date:** 2026-09-03

## Problem

DLSS Super Resolution / DLAA is reachable from .NET only through NVIDIA's NGX
SDK, and that SDK has no DLL to P/Invoke. What ships is a **static library** —
`native/ngx/staged/win-x64/nvsdk_ngx_s.lib` (4 740 514 bytes) and
`native/ngx/staged/linux-x64/libnvsdk_ngx.a` (93 222 bytes) — which finds and
loads the NGX runtime inside the display driver at call time. So this repo has
to compile and ship a shared library of its own before any binding can exist.

Three further properties of the SDK make "re-export it and generate bindings"
wrong as stated in the issue, and each has to be decided rather than assumed:

1. **`wchar_t` crosses the ABI.** `NVSDK_NGX_VULKAN_Init_with_ProjectID` takes
   `const wchar_t* InApplicationDataPath` (`native/ngx/include/nvsdk_ngx_vk.h:260`),
   `NVSDK_NGX_PathListInfo.Path` is `wchar_t const* const*`
   (`native/ngx/include/nvsdk_ngx_defs.h:355`),
   `NVSDK_NGX_FeatureDiscoveryInfo.ApplicationDataPath` is `const wchar_t*`
   (`native/ngx/include/nvsdk_ngx_defs.h:532`), and `GetNGXResultAsString`
   returns `const wchar_t*` (`native/ngx/include/nvsdk_ngx_vk.h:761`).
   `wchar_t` is 2 bytes on Windows and 4 on Linux, and every rsp in this repo
   parses with a fixed `--target=x86_64-unknown-linux-gnu`
   (`tools/generate-ktx.rsp:30-38`, `tools/generate-vma.rsp`,
   `tools/generate-slang.rsp`) so that generated output is a function of the pin
   and not of the host.
2. **The parse-time surface and the link-time surface are different sets.**
   `nvsdk_ngx_vk.h` declares functions under `NGX_SNIPPET_BUILD`,
   `NGX_ENABLE_DEPRECATED_SHUTDOWN` and `NGX_ENABLE_DEPRECATED_GET_PARAMETERS`
   branches, plus a C++-only `EvaluateFeature` with a default argument
   (`nvsdk_ngx_vk.h:753`). What the static library actually defines is a third
   list.
3. **`NVSDK_NGX_Parameter` is a C++ abstract class** with nine pure virtuals
   (`native/ngx/include/nvsdk_ngx_params.h:53-76`). Binding its vtable would be
   both wrong (the object is produced by the driver, not by us) and unstable.

Phase 1 has to answer, with evidence: what the shim exports, what the rsp
parses, how the two are kept in agreement, how the SDK reaches a CI runner that
cannot commit it, and what a driverless lane is actually allowed to assert.

## Evidence

All measurements below were taken on this working tree at `NgxVersion`
`v310.7.0` (`Directory.Build.props:63`), against the staged artifacts
`tools/setup-ngx.ps1` produced. The binding measurements come from real
ClangSharp `21.1.8.3` runs (`.config/dotnet-tools.json`) into a scratch
directory, compiled against `src/Ahjo.Vulkan.Native` with the repo's
`TreatWarningsAsErrors=true` / `AnalysisLevel=latest`.

### E1. The C parameter accessors are real exported symbols, not inline helpers

This was the open question with the largest blast radius: if
`NVSDK_NGX_Parameter_Set*` / `Get*` were inline C++ helpers, the shim would have
to implement all sixteen of them against the vtable. They are not.

`llvm-nm --defined-only` over both staged libraries lists every one of them as a
defined text symbol, unmangled (C linkage):

```
libnvsdk_ngx.a      0000000000000000 T NVSDK_NGX_Parameter_SetULL   … 16 accessors
nvsdk_ngx_s.lib                      T NVSDK_NGX_Parameter_SetULL   … 16 accessors
```

`GetNGXResultAsString` is likewise defined in both (`T GetNGXResultAsString`).
So `--language c` plus "bind only the exported C accessors, never the vtable"
(#214) is not a workaround — it is the SDK's own supported C path, and the shim
needs to write no accessor code at all.

### E2. What the two static libraries actually define

Windows `nvsdk_ngx_s.lib` defines 80 `NVSDK_NGX_*` / `GetNGXResultAsString`
symbols: 17 CUDA, 15 D3D11, 14 D3D12, 16 `Parameter_*`, 16 `VULKAN_*`,
`NVSDK_NGX_UpdateFeature`, `GetNGXResultAsString`. Linux `libnvsdk_ngx.a`
defines the same shape minus the D3D families.

Two consequences:

- `NVSDK_NGX_VULKAN_Init_Ext` / `Init_Ext2` are **absent from both libraries**.
  They are declared only under `NGX_SNIPPET_BUILD` (`nvsdk_ngx_vk.h:173-178`),
  which is the driver-side build. Nothing may bind them.
- `NVSDK_NGX_VULKAN_Shutdown` and `NVSDK_NGX_VULKAN_GetParameters` are defined
  in the libraries but declared only behind `NGX_ENABLE_DEPRECATED_SHUTDOWN` /
  `NGX_ENABLE_DEPRECATED_GET_PARAMETERS` (`nvsdk_ngx_vk.h:285-288`, `:290`), so
  a C parse never sees them. Good: the parse is already narrower than the
  library, in the right direction.

### E3. Linking the whole archive pulls in no D3D, CUDA, or NvAPI dependency

`llvm-nm --undefined-only`:

- **Linux** (`libnvsdk_ngx.a`): libstdc++ (`_ZNSt7__cxx1112basic_string…`),
  `dlopen`/`dlsym`/`dlclose`/`dladdr`, pthread, and libc. Nothing else. The
  mangled names carry the `B5cxx11` tag, i.e. the archive was built with
  `_GLIBCXX_USE_CXX11_ABI=1` — the modern default, so no ABI flag is needed.
- **Windows** (`nvsdk_ngx_s.lib`): 113 undefined symbols, all kernel32
  (`LoadLibraryW`, `GetModuleFileNameW`, `VerifyVersionInfoW`), advapi32
  (`RegOpenKeyExW`, `RegQueryValueExW`, `RegCloseKey`), user32
  (`AllocConsole`, `GetConsoleWindow`, `GetWindowThreadProcessId`) and the CRT.
  **Zero** `d3d*`, `dxgi*`, `cuda*` or `nvapi*` imports.

So `--whole-archive` (Linux) and `/DEF`-driven pull (Windows) are both safe —
the D3D and CUDA entry points cost nothing but their own code size and do not
drag in an import library. Object-file names embedded in the Windows archive
(`_out/wddm_amd64_release_static_release_vs2015/…`) confirm the library is the
**static-CRT release** build, so the shim must be MSVC `/MT`.

### E4. The fixed Linux target really does mis-size `wchar_t`

A real generation run over `nvsdk_ngx_vk.h` with
`--additional --target=x86_64-unknown-linux-gnu --language c` produced:

```csharp
public static extern NVSDK_NGX_Result NVSDK_NGX_VULKAN_Init_with_ProjectID(
    …, [NativeTypeName("const wchar_t *")] int* InApplicationDataPath, …);

public unsafe partial struct NVSDK_NGX_PathListInfo
{
    [NativeTypeName("const wchar_t *const *")] public int** Path;
}
```

`int*` — a UTF-32 buffer. On Windows the SDK reads UTF-16 from that pointer.
Note that the *struct layouts* are unaffected (a pointer is 8 bytes either way);
what breaks is the encoding of the data behind the pointer, silently, only on
Windows, only for paths — i.e. exactly the failure that surfaces as
`FAIL_FeatureNotFound` with no clue why.

### E5. The `wchar_t` surface is larger than the issue lists — and it is reachable through structs

Verified against the headers, not the issue text. The complete `wchar_t` surface
of the Vulkan + core C API is:

| Where | Citation |
|---|---|
| `NVSDK_NGX_VULKAN_Init(…, const wchar_t* InApplicationDataPath, …)` | `nvsdk_ngx_vk.h:184` |
| `NVSDK_NGX_VULKAN_Init_with_ProjectID(…, const wchar_t* InApplicationDataPath, …)` | `nvsdk_ngx_vk.h:260` |
| `NVSDK_NGX_PathListInfo.Path` (`wchar_t const* const*`) | `nvsdk_ngx_defs.h:355` |
| `NVSDK_NGX_FeatureCommonInfo.PathListInfo` — carries the above into every `Init*` | `nvsdk_ngx_defs.h:400-403` |
| `NVSDK_NGX_FeatureDiscoveryInfo.ApplicationDataPath` (`const wchar_t*`) | `nvsdk_ngx_defs.h:532` |
| `NVSDK_NGX_FeatureDiscoveryInfo.FeatureInfo` (`const NVSDK_NGX_FeatureCommonInfo*`) | `nvsdk_ngx_defs.h:533` |
| `GetNGXResultAsString` (returns `const wchar_t*`) | `nvsdk_ngx_vk.h:761` |

`NVSDK_NGX_FeatureDiscoveryInfo` is the *first parameter* of all three discovery
entry points (`nvsdk_ngx_vk.h:605`, `:646`, `:693`). So **four of the five entry
points a DLSS integration must call before it can render** —
`Init_with_ProjectID`, `GetFeatureRequirements`,
`GetFeatureInstanceExtensionRequirements`,
`GetFeatureDeviceExtensionRequirements` — touch `wchar_t`, transitively.

This is the finding that decides the shape of the shim: `--exclude`-ing the
*functions* (what #216 proposes) would still leave `NVSDK_NGX_PathListInfo` and
`NVSDK_NGX_FeatureDiscoveryInfo` generated with `int**` / `int*` fields that
Phase 2 could populate with `Encoding.UTF32` and ship a Windows-only bug. The
structs have to leave the binding surface too.

### E6. `generate-unmanaged-constants` alone emits **zero** parameter-name constants

`nvsdk_ngx_defs.h` defines 204 `#define NVSDK_NGX_Parameter_*` string macros
(`"Width"`, `"Color"`, `"Jitter.Offset.X"`, `"DLSS.Hint.Render.Preset.DLAA"`, …).
Generated with the same `--config` block the other three rsps use, **none of
them appeared** in the output — `grep` for `Width` in the generated tree matched
only struct fields.

They appear only when `generate-macro-bindings` is added, which no existing rsp
in this repo uses. With it:

```csharp
[NativeTypeName("#define NVSDK_NGX_Parameter_Width \"Width\"")]
public static ReadOnlySpan<byte> NVSDK_NGX_Parameter_Width => "Width"u8;
```

278 `u8` constants. #216's claim that this is "satisfied for free" is wrong as
written: it is satisfied by a config flag nothing else in the repo turns on, and
turning it on has a second-order cost (E7).

### E7. `generate-macro-bindings` also emits 74 constants containing raw control bytes

The same run emits the legacy hash-encoded alias family:

```c
#define NVSDK_NGX_EParameter_Width                "#\x10"     // nvsdk_ngx_defs.h:588
#define NVSDK_NGX_EParameter_..._ScaleFactor_4_3  "#\x0d"     // nvsdk_ngx_defs.h:585
```

ClangSharp escapes `\t`, `\n`, `\r` and `\0` but emits `0x01`–`0x08`, `0x0b`,
`0x0c` and `0x0e`–`0x1f` **literally**. A byte histogram of the generated
`NgxApi.cs` found 28 distinct raw control bytes in the file. Those 74 constants
are the deprecated alias of the string names in E6, nothing in the DLSS path
uses them, and committing raw control characters into a `*.cs` file that
`.gitattributes:2` puts under `text=auto eol=lf` normalization is a hazard with
no upside.

### E8. Raw ClangSharp output over these headers does not compile — one fix, verified

`strip-enum-member-type-name` (used by all three existing rsps) strips the type
prefix from enum *member names* but not from initializer expressions that
reference sibling members. `NVSDK_NGX_Result` is defined as a bitwise-or chain
off one member (`nvsdk_ngx_defs.h:96-178`):

```c
NVSDK_NGX_Result_Fail = 0xBAD00000,
NVSDK_NGX_Result_FAIL_FeatureNotSupported = NVSDK_NGX_Result_Fail | 1,
```

which generates

```csharp
Fail = 0xBAD00000,
FAIL_FeatureNotSupported = NVSDK_NGX_Result_Fail | 1,   // CS0103
```

— 18 × `CS0103: The name 'NVSDK_NGX_Result_Fail' does not exist`. Measured.
Dropping `strip-enum-member-type-name` from this one rsp fixes all 18 and the
result builds with **0 warnings, 0 errors** under `TreatWarningsAsErrors=true`
and `AnalysisLevel=latest`. No other enum in the traversal set is affected
(`NVSDK_NGX_Feature_Support_Result`'s members are prefixed
`NVSDK_NGX_FeatureSupportResult_`, which the stripper does not match anyway).

### E9. `--exclude` works on macros and on structs, not only on functions

Verified by a run that excluded `NVSDK_NGX_EParameter_Width` (a macro),
`NVSDK_NGX_PathListInfo` and `NVSDK_NGX_FeatureDiscoveryInfo` (structs): all
three vanished from the output, no other file changed. This is what makes D2
below implementable.

### E10. The measured binding surface, and its struct layout

The candidate configuration (D2 + D7) generates 35 files and exactly 27
`DllImport`s — 20 verbatim NGX entry points and 7 `ahjo_ngx_*` — with **zero
occurrences of `wchar_t` anywhere in the generated tree**. Compiled and executed,
the layouts are:

| Struct | `sizeof` | Field offsets |
|---|---|---|
| `NVSDK_NGX_Resource_VK` | 56 | `Resource` 0, `Type` 48, `ReadWrite` 52 |
| `NVSDK_NGX_ImageViewInfo_VK` | 48 | `ImageView` 0, `Image` 8, `SubresourceRange` 16, `Format` 36, `Width` 40, `Height` 44 |
| `NVSDK_NGX_BufferInfo_VK` | 16 | `Buffer` 0, `SizeInBytes` 8 |
| `NVSDK_NGX_FeatureRequirement` | 264 | `FeatureSupported` 0, `MinHWArchitecture` 4, `MinOSVersion` 8 |

**Sizes alone cannot verify this layout.** `NVSDK_NGX_Resource_VK` ends with a
4-byte enum and a 1-byte bool inside 8 bytes of tail; swapping `Type` and
`ReadWrite` leaves `sizeof` at 56 and changes the meaning of every DLSS resource
binding. Offsets are therefore load-bearing, not a nicety.

### E11. `bool ReadWrite` generates as C# `bool`, not `byte`

#216 states it "becomes `byte` under `generate-disable-runtime-marshalling`".
Measured, ClangSharp `21.1.8.3` emits:

```csharp
[NativeTypeName("_Bool")] public bool ReadWrite;
```

The struct still measures 56 bytes with `ReadWrite` at offset 52 (E10) and
compiles clean, because the struct is only ever handed to NGX through
`NVSDK_NGX_Parameter_SetVoidPointer(…, void*)` — it is never a marshalled
parameter, so `DisableRuntimeMarshalling`'s blittable-only rule is not engaged.
`bool` is a valid C# `unmanaged` type, so `sizeof` and `&resource.ReadWrite`
both work. Phase 2 therefore writes `resource.ReadWrite = true`, not `1`. The
issue's guidance here is stale and the layout test is what pins it.

### E12. The repo's precedent for re-exporting an upstream C ABI is verbatim, not re-typed

`native/vma/CMakeLists.txt:47-52` solves the structurally identical problem —
"VMA's headers declare its C API as `extern "C"` but never tag it with
`__declspec(dllexport)`" — with `WINDOWS_EXPORT_ALL_SYMBOLS ON`, i.e. the
upstream names are re-exported unchanged and the rsp parses upstream's own
header. `native/CLAUDE.md` states the same principle for inputs: `slang/include`
and `ngx/include` are committed because they are "the generator input of record,
so a version bump shows the API diff before the generated output is re-derived
from it."

`WINDOWS_EXPORT_ALL_SYMBOLS` itself does not carry over: CMake computes that
`.def` from the target's **own** object files, and our symbols come from a
linked static library, so the export list has to be written out.

### E13. The fetch script cannot be used by CI as it stands

`tools/setup-ngx.ps1:110-123` unconditionally adds the `rel/` and `dev/` feature
DLLs to the fetch manifest for any selected platform. On `win-x64` that is
2 × ~56 MB; on `linux-x64` 2 × ~57 MB. #216 requires that "the CI fetch pulls
only headers, static client libs and licence text — never a feature DLL". There
is no switch for that today.

### E14. The `/regen-bindings` skill is already one project behind

`.claude/skills/regen-bindings/SKILL.md` says "the three Native projects (Vulkan,
VMA, libktx)" and its table has no row for `Ahjo.Vulkan.Slang.Native`, which
shipped in #166. `docs/ci-coverage.md:75-78` likewise has no `slang-native` row.

### E15. What a driverless lane can and cannot call

`GetFeatureInstanceExtensionRequirements` takes no Vulkan object at all
(`nvsdk_ngx_vk.h:646`) and is documented as callable before `VkInstance`
creation. `GetFeatureRequirements` takes `VkInstance` + `VkPhysicalDevice`
(`nvsdk_ngx_vk.h:605`). The `ngx-native` lane, following `ktx-native` and
`slang-native`, provisions no loader and no ICD, so it has no instance to pass
and calling it with `NULL` is undefined behaviour inside NGX, not a test.
#216's "call `GetFeatureRequirements`" is not executable on the lane it
describes.

## Decision

Nine decisions. D1–D3 are the load-bearing ones.

### D1. A hybrid export contract: verbatim re-export for the `wchar_t`-free C API, `ahjo_ngx_*` additions for everything else

`native/ngx/src/ahjo_ngx.{h,cpp}` builds `ahjo_ngx.dll` / `libahjo_ngx.so`,
linking the staged static client library, and exports exactly 27 symbols:

**20 verbatim, re-exported unchanged from the static library** — 12
`NVSDK_NGX_Parameter_{Set,Get}{ULL,F,D,UI,I,VoidPointer}` and 8
`NVSDK_NGX_VULKAN_{Shutdown1, AllocateParameters, GetCapabilityParameters,
DestroyParameters, GetScratchBufferSize, CreateFeature1, ReleaseFeature,
EvaluateFeature_C}`. No wrapper code, no re-typing: the rsp parses NVIDIA's own
headers for these, so a pin bump shows the API diff in
`git diff native/ngx/include/` and is absorbed by a regen (E12,
`native/CLAUDE.md`). `EvaluateFeature_C` in particular is on Phase 2's per-frame
path, and this keeps it a direct call with no shim frame.

**7 `ahjo_ngx_*` additions** (D2, D3): `ahjo_ngx_version_api`,
`ahjo_ngx_layout`, `ahjo_ngx_result_to_utf8`, `ahjo_ngx_vulkan_init_utf8`,
`ahjo_ngx_vulkan_get_feature_requirements_utf8`,
`ahjo_ngx_vulkan_get_feature_instance_extension_requirements_utf8`,
`ahjo_ngx_vulkan_get_feature_device_extension_requirements_utf8`.

The export list is written twice — `native/ngx/src/ahjo_ngx.def` (Windows
`/DEF:`) and `native/ngx/src/ahjo_ngx.map` (Linux version script, with
`--whole-archive` on the static library and `local: *;` hiding everything else,
which E3 shows is safe). Both files are hand-maintained, which is a drift risk;
D6 makes that drift a test failure rather than an `EntryPointNotFoundException`.

Excluded from the export surface on purpose: the four
`NVSDK_NGX_Parameter_{Set,Get}D3d1{1,2}Resource` accessors, every
D3D11/D3D12/CUDA entry point, `NVSDK_NGX_UpdateFeature` (OTA — #214 "Later"),
`NVSDK_NGX_VULKAN_RequiredExtensions` (deprecated in favour of the two discovery
calls, by its own header comment at `nvsdk_ngx_vk.h:83-86`),
`NVSDK_NGX_VULKAN_CreateFeature` (superseded by `CreateFeature1`, which is the
multi-device form) and `NVSDK_NGX_VULKAN_Init` / `Shutdown` / `GetParameters`.

#### Why not the alternatives

- **Thin `ahjo_ngx_*` pass-throughs for all 27.** Uniform naming, and the C++
  compiler would catch an upstream signature change. But it re-types 20
  functions by hand, moves the generator input of record from NVIDIA's headers
  into ours (against `native/CLAUDE.md`), and puts a shim frame on
  `EvaluateFeature_C`. Rejected: the compile-time check it buys is bought more
  cheaply by D6's three-way export test.
- **Pure verbatim re-export of everything, `wchar_t` included.** What the VMA
  precedent would suggest, and what makes E4/E5 ship a Windows-only encoding
  bug. Rejected.
- **`WINDOWS_EXPORT_ALL_SYMBOLS ON`, as VMA uses.** Does not export symbols that
  come from a linked static library (E12); would silently produce a DLL with 7
  exports. Rejected.
- **No shim; P/Invoke the driver's `_nvngx.dll` directly.** That is the NGX Core
  ABI: undocumented, versioned with the driver, and the whole reason the SDK
  ships a client library. Rejected.

### D2. The `wchar_t` boundary is closed by keeping the wide-string *structs* out of the binding surface, not by excluding entry points

The four wide-string-bearing structs — `NVSDK_NGX_PathListInfo`,
`NVSDK_NGX_LoggingInfo`, `NVSDK_NGX_FeatureCommonInfo`,
`NVSDK_NGX_FeatureDiscoveryInfo` — are `--exclude`d (E9), along with
`NVSDK_NGX_ProjectIdDescription` and `NVSDK_NGX_Application_Identifier`, which
exist only to be embedded in them. In their place `ahjo_ngx.h` declares one
UTF-8 mirror that the generator emits from *our* header, so there is exactly one
definition and no cross-language layout agreement to maintain:

```c
typedef struct AhjoNgxInitInfo
{
    unsigned int                          StructSize;      /* = sizeof(AhjoNgxInitInfo) */
    NVSDK_NGX_Application_Identifier_Type IdentifierType;
    unsigned long long                    ApplicationId;
    const char*                           ProjectId;             /* UTF-8 */
    NVSDK_NGX_EngineType                  EngineType;
    const char*                           EngineVersion;         /* UTF-8 */
    const char*                           ApplicationDataPath;   /* UTF-8 -> widened natively */
    const char* const*                    FeatureSearchPaths;    /* UTF-8 -> widened natively */
    unsigned int                          FeatureSearchPathCount;
    NVSDK_NGX_AppLogCallback              LogCallback;           /* may be NULL */
    NVSDK_NGX_Logging_Level               MinimumLoggingLevel;
    unsigned char                         DisableOtherLoggingSinks;
} AhjoNgxInitInfo;   /* measured: 80 bytes */
```

One struct serves both init and all three discovery calls, because
`NVSDK_NGX_FeatureDiscoveryInfo` carries exactly the same identity, path and
logging data as `Init_with_ProjectID` plus `NVSDK_NGX_FeatureCommonInfo`. The
shim stamps `SDKVersion = NVSDK_NGX_Version_API` itself, so the pinned API
version (0x15, `nvsdk_ngx_defs.h:56`) is a property of the compiled shim rather
than something managed code can get wrong. `StructSize` is checked on entry and
a mismatch returns `NVSDK_NGX_Result_FAIL_InvalidParameter`, which is what a
stale `ahjo_ngx.dll` on a consumer's search path looks like.

Measured result: **zero `wchar_t` in the generated tree** (E10).
`GetNGXResultAsString` is replaced by
`unsigned int ahjo_ngx_result_to_utf8(NVSDK_NGX_Result, char* buffer, unsigned int bufferSize)`,
returning the byte count including the NUL (or the required size when the buffer
is too small), so managed callers use `stackalloc byte[128]` and allocate
nothing.

Conversion is a hand-rolled UTF-8 decoder in the shim, **not** `mbstowcs` /
`MultiByteToWideChar`: `mbstowcs` depends on the process locale, which a library
must not set, and `MultiByteToWideChar` is Windows-only.

#### Why not the alternatives

- **`--exclude` only the `wchar_t`-taking functions** (what #216 says). Leaves
  `NVSDK_NGX_PathListInfo.Path` as `int**` and
  `NVSDK_NGX_FeatureDiscoveryInfo.ApplicationDataPath` as `int*` in the public
  binding surface with nothing stopping Phase 2 from filling them in (E5).
  Rejected.
- **`--remap wchar_t=ushort`.** Makes Windows right and Linux wrong; the fixed
  Linux parse target exists precisely so the output is host-independent, and a
  remap turns it into a per-RID lie. Rejected.
- **Two rsps, one per RID target.** Doubles the committed generated tree and
  breaks the "generated output is a function of the pin" rule that
  `tools/generate-ktx.rsp:30-38` states. Rejected.
- **Flat parameter lists instead of `AhjoNgxInitInfo`.** Sixteen-parameter
  P/Invokes for a setup-time call; the mirror struct is generated from our own
  header, so it carries no drift risk the flat form avoids. Rejected on
  ergonomics.

### D3. Layout verification: one export, an enumerated id, offsets as well as sizes

```c
typedef enum AhjoNgxLayoutId { /* … */ AHJO_NGX_LAYOUT_COUNT } AhjoNgxLayoutId;

/* Returns the queried value, or 0xFFFFFFFF for an unrecognised id. */
unsigned int ahjo_ngx_layout(AhjoNgxLayoutId id);
```

One export, one `uint32` return, no shared struct whose own layout would have to
be verified first. The ids cover `sizeof` **and** `alignof` **and** every field
offset of `NVSDK_NGX_Resource_VK`, `NVSDK_NGX_ImageViewInfo_VK`,
`NVSDK_NGX_BufferInfo_VK` and `NVSDK_NGX_FeatureRequirement`, plus
`sizeof(AhjoNgxInitInfo)`. `AHJO_NGX_LAYOUT_COUNT` is generated too, so a test
that walks `0 .. COUNT-1` fails when a native id is added without managed
coverage.

Offsets are included because sizes cannot detect the failure that matters (E10).
The managed side reads offsets with `(byte*)&value.Field - (byte*)&value` — no
`Marshal.OffsetOf` (unusable under `DisableRuntimeMarshalling`) and no
reflection, so the test stays AOT-shaped like the rest of the repo.

#### Why not the alternatives

- **One `ahjo_ngx_sizeof_*` export per struct** (#216's wording). Four exports
  that cannot catch a field reorder. Rejected.
- **One export returning a `struct` of sizes.** That struct's own layout becomes
  the thing you have to trust before you can verify anything. Rejected.

### D4. Acquisition: explicit, never automatic; an absent SDK is a skip locally and a failure in CI

`tools/setup-ngx.ps1` gains `-SkipFeatureDll`, which drops the `rel/` and `dev/`
entries from the manifest (E13) and leaves headers, the static client library
and `NGX-LICENSE.txt`. `build-ngx-native.yml` runs
`./tools/setup-ngx.ps1 -Platform <rid> -SkipFeatureDll` before cmake, so the CI
fetch provably cannot pull a feature DLL.

Locally, the shim build is **conditioned on the staged static library existing**
and never triggers a download. A fresh clone of this repo must keep building
with no NVIDIA SDK present and no network: `dotnet build Ahjo.Vulkan.slnx`
compiles the managed bindings, prints one high-importance message naming
`./tools/setup-ngx.ps1`, and skips the shim. `Ahjo.Vulkan.Ngx.Native.Tests` then
skips its whole suite — **unless `AHJO_NGX_REQUIRE_SHIM=1` is set**, which the
CI lane sets, turning "the shim was not there" from a green run into a red one.
That is the `AHJO_VULKAN_TIER` idea (`.github/CLAUDE.md`, #158) applied to a lane
that has no Vulkan tier to declare: a lane must not report green while executing
nothing.

#### Why not the alternatives

- **Auto-fetch on build, like VMA's `FetchVma` and KTX's clone.** Downloads
  proprietary, licence-encumbered binaries as a side effect of an unrelated
  build. Rejected on both licence and DX grounds.
- **A hard `<Error>` when the SDK is absent.** Breaks `git clone && dotnet build`
  for every contributor who will never touch DLSS. Rejected.
- **Committing the static libraries.** `.gitignore:290-292` and
  `native/CLAUDE.md` already forbid it, and §3(b) of the NVIDIA licence makes it
  the wrong call independently.

### D5. Package and project shape mirror `Ahjo.Vulkan.Slang.Native`

`src/Ahjo.Vulkan.Ngx.Native` packs `runtimes/<rid>/native/ahjo_ngx.dll` /
`libahjo_ngx.so` for `win-x64` and `linux-x64` plus `NGX-LICENSE.txt` at the
nupkg root, exactly as `PackSlangRuntimes` does with `SLANG-LICENSE.txt`. It
`ProjectReference`s `Ahjo.Vulkan.Native` — unlike Slang and KTX, and like VMA,
because the generated surface genuinely is a Vulkan consumer: ten `Vk*` types
are remapped into `Ahjo.Vulkan.Native` (D7). `Ahjo.Vulkan` itself gains nothing.
Package count goes six → seven here, eight after Phase 2.

The shim binary is **not** propagated to downstream projects unconditionally the
way `vma.dll` is; it is staged only when it exists, so a solution build without
the SDK stays clean.

### D6. Proving it: a three-way export test, a layout test, and a lane that asserts only what a driverless host can answer

`tests/Ahjo.Vulkan.Ngx.Native.Tests` carries three guards:

1. **Export drift, three ways.** A literal 27-name array in managed code (the
   `SlangExportDriftTests` shape — no reflection), checked against
   `NativeLibrary.TryGetExport` on the loaded shim, **and** against the names
   parsed out of `ahjo_ngx.def` and `ahjo_ngx.map`, both copied to the test
   output as content. This is what makes D1's two hand-maintained export lists
   safe: any of the four surfaces drifting fails one test with the missing name
   in the message.
2. **Layout**, per D3, run in both RID jobs — the whole point is that MSVC and
   GCC agree.
3. **Version identity.** `ahjo_ngx_version_api()` must equal
   `(uint)NVSDK_NGX_Version.NVSDK_NGX_Version_API` from the committed bindings,
   which proves the shim binary and the generated C# came from the same pinned
   header — the analogue of Slang's `BuildTag_MatchesPinnedVersion`.

The `ngx-native` lane (`build-ngx-native.yml`, called by both `ci.yml` and
`publish.yml`, so the release artifact comes from the definition CI proves — the
`ktx-native` / `slang-native` rule) additionally calls
`ahjo_ngx_result_to_utf8` and
`ahjo_ngx_vulkan_get_feature_instance_extension_requirements_utf8` and requires
that the latter return without a crash or a hang, and that a `Success` carry a
plausible extension count and a non-null array.

> **Amended 2026-09-03 (OPEN-1 resolved).** This decision originally said the
> lane must require a **non-`Success`** result from that call. That was wrong,
> and CI measured it: a driverless `windows-latest` runner returns `Success`
> with `extensionCount` 1 and `VK_KHR_get_physical_device_properties2`
> specVersion 2 — byte-identical to a host with an RTX 4070 Ti on driver
> 610.47. The call is a pre-instance static query answered out of NVIDIA's
> static client library and never loads the driver-side NGX core. The
> assertion is therefore host-independent, and the `AHJO_NGX_EXPECT_NO_DRIVER`
> declaration briefly introduced to express the old expectation has been
> removed. `AHJO_NGX_REQUIRE_SHIM` is unaffected. See OPEN-1 below.

It **does not** call
`GetFeatureRequirements`: that needs a `VkInstance` the lane deliberately has no
ICD to create (E15). Real `GetFeatureRequirements` / create / evaluate coverage
is a local-NVIDIA-hardware item, recorded as such in `docs/ci-coverage.md`,
consistent with #32's "software rasterizers aren't honest coverage".

The lane provisions no Vulkan loader and no ICD and leaves `AHJO_VULKAN_TIER`
unset: the shim links no `vulkan-1` import library (it only *includes* the
headers, the same reason `VMA_STATIC_VULKAN_FUNCTIONS=0` exists in
`native/vma/src/vma.cpp:8-15`), so if this suite ever needs a loader, something
was linked in that the package's contract says is not there. It is a
build-artifact check and must not grow into wrapper coverage.

### D7. `tools/generate-ngx.rsp` — three deltas from the house style, each forced

Parse root is `native/ngx/src/ahjo_ngx.h` (which `#include`s
`<vulkan/vulkan.h>` and then `nvsdk_ngx_vk.h` — the NGX headers do **not**
include Vulkan themselves, verified at `nvsdk_ngx_vk.h:69-77`), with
`--traverse` naming `ahjo_ngx.h` plus `nvsdk_ngx_vk.h`, `nvsdk_ngx_defs.h`,
`nvsdk_ngx_defs_vk.h`, `nvsdk_ngx_params.h`. Traversal alone excludes the entire
D3D/CUDA *entry-point* surface — measured; no `--exclude` is needed for it.
`--language c`, fixed `--target=x86_64-unknown-linux-gnu`,
`--methodClassName NgxApi` (**not** `Ngx`: a type named `Ngx` inside
`Ahjo.Vulkan.Ngx.Native` is unreachable from `Ahjo.Vulkan.Ngx` — the #166 lesson
at `tools/generate-slang.rsp:19-21`), `--libraryPath ahjo_ngx`, and ten `Vk*`
remaps into `Ahjo.Vulkan.Native` (the `tools/generate-vma.rsp` pattern).

The three deltas:

- **`generate-macro-bindings` is added** (E6), because without it the 204 UTF-8
  parameter-name constants that satisfy invariant 1 do not exist.
- **`strip-enum-member-type-name` is removed** (E8), because with it the output
  does not compile. Enum members therefore keep their `NVSDK_NGX_`-prefixed
  names in this one project.
- **74 `NVSDK_NGX_EParameter_*` macros are `--exclude`d** (E7).

Also excluded: the six wide-string structs (D2), the four D3D parameter
accessors, the D3D/CUDA opaque type declarations that `nvsdk_ngx_params.h` and
`nvsdk_ngx_defs.h` forward-declare, and the deprecated/superseded entry points
listed in D1.

Parse-time stubs `native/ngx/stubs/wchar.h` (`typedef __WCHAR_TYPE__ wchar_t;`)
and `native/ngx/stubs/stdbool.h` are committed alongside `native/ktx/stubs/` and
`native/slang/stubs/`, for the reason `native/CLAUDE.md` gives: the output must
be a function of the pin, not of whose C toolchain was installed.

### D8. Widened strings handed to `Init` are retained for the process lifetime; discovery calls free theirs

`NVSDK_NGX_FeatureCommonInfo` carries
`NVSDK_NGX_FeatureCommonInfo_Internal* InternalData; // Used internally by NGX`
(`nvsdk_ngx_defs.h:405`), and the feature-DLL search paths it holds are consulted
when the feature DLL is loaded — at `CreateFeature1` time, long after `Init`
returns. The shim therefore allocates the widened `Init` strings and their
`NVSDK_NGX_FeatureCommonInfo` on the heap and **never frees them**. This is
setup-time allocation of a few hundred bytes per `Init` call (invariant 3 is a
per-frame constraint), and it is the only construction that is correct without
knowing NGX's internal copy semantics.

The three discovery shims consume their `NVSDK_NGX_FeatureDiscoveryInfo` entirely
within the call — `GetFeatureInstanceExtensionRequirements` returns a pointer to
NGX-owned storage, not to ours — so they convert into scoped storage and release
it on return. Without that split, a settings screen that re-queries the optimal
modes would leak per call.

**This is the one place the design is inferring rather than measuring.** If a
driver-side fault is ever traced to a discovery call, the fix is to promote the
discovery path to the same retention, and the inference above is what to
re-examine first.

### D9. What Phase 1 must expose for Phase 2 to be zero-allocation

Recorded here so Phase 2 does not have to re-derive it: `EvaluateFeature_C` is a
direct export with no shim frame (D1); `NVSDK_NGX_Resource_VK` is blittable,
56 bytes, stack-constructible (E10); the parameter names are
`ReadOnlySpan<byte>` u8 constants in read-only data (E6, invariant 1);
`ahjo_ngx_result_to_utf8` takes a caller buffer so error formatting can
`stackalloc` (D2); and the only shim functions that allocate — init and
discovery — are setup-time by construction (D8). Nothing in `Recording/`,
`Sync/`, `Pools/` or `Memory/` is touched by Phase 1, so no benchmark moves and
`docs/benchmarks.md` is unchanged.

## Scope boundary

Phase 1 stops at the native layer. Not designed here, and not to be inferred
from anything above: `NgxContext`, `DlssFeature`, the shadow enums, the
`Allocator.Create` memory-budget flag, `DlssEvaluateBenchmark`,
`samples/HelloDlaa`, `docs/ngx-notes.md`. Those are Phase 2 (#214).

Two drive-by gaps were found and are **not** fixed by this spec beyond one
mechanical line each in the docs the plan already edits: `/regen-bindings` omits
`Ahjo.Vulkan.Slang.Native`, and `docs/ci-coverage.md` omits `slang-native`
(E14).

## OPEN

- **OPEN-1 — RESOLVED 2026-09-03 by measurement. The premise was wrong.**

  *As originally written:* `GetFeatureInstanceExtensionRequirements` takes no
  Vulkan object and is documented as pre-instance, so it should return a
  **failure** result rather than fault when NGX cannot load the driver-side
  core library. That had not been executed on a driverless host, because the
  shim did not exist yet.

  *What was measured.* Two hosts, and they agree to the byte:

  | Host | Result |
  |---|---|
  | `windows-latest` CI runner, no NVIDIA driver | `Success`, `extensionCount` 1, `VK_KHR_get_physical_device_properties2` specVersion 2 |
  | RTX 4070 Ti, driver 610.47 | `Success`, `extensionCount` 1, `VK_KHR_get_physical_device_properties2` specVersion 2 |

  So "no driver implies not `Success`" was never true. The call is a
  pre-instance **static query answered out of NVIDIA's static client library**;
  it never loads the driver-side NGX core at all, which is precisely why it is
  safe to call before `VkInstance` creation — the same property that made it
  the only NGX entry point this lane may call.

  *What changed.* The lane asserts the host-independent property instead: the
  call returns rather than faulting or hanging, and a `Success` carries a
  plausible count and a non-null array. An `AHJO_NGX_EXPECT_NO_DRIVER`
  declaration was added to express the old expectation and then removed once CI
  measured both host kinds; `AHJO_NGX_REQUIRE_SHIM` is a separate concern and
  is unaffected. D6 above carries the amendment.

  *Why this entry stays.* It recorded a genuine unknown, and the record is more
  useful showing the unknown was closed by measurement than showing it never
  existed. The contingency it described — reducing the lane to the four
  driver-free calls — was not needed and was not applied.
- **OPEN-2 — `NVSDK_NGX_VULKAN_AllocateParameters` before `Init`.** Whether it
  succeeds, fails cleanly, or requires the driver is unknown. It stays out of
  the lane's assertions in the first iteration; do not add it on a guess.

## Cross-links

- Tracking and research: #214. Fetch step: #215.
- Package/lane shape and the "a checksum proves the bytes, execution proves they
  run" rule: #166 and
  `docs/design/specs/2026-08-01-issue-166-slang-support-design.md`.
- Why a shipped native binary must be executed by the job that produces it: #144.
- Why there is no Linux wrapper lane and why software rasterizers are not
  coverage: #32, `.github/CLAUDE.md`.
- Why a lane must declare what it has instead of reporting green while executing
  nothing: #158, `docs/ci-coverage.md`.
