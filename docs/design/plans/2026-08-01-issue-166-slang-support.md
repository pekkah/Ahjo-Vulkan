Paired with [../specs/2026-08-01-issue-166-slang-support-design.md](../specs/2026-08-01-issue-166-slang-support-design.md).

# Implementation plan — issue #166, Slang support

Four phases. **Phase 1, Phase 2 and Phase 3a each ship independently**; Phase 3b
requires 3a. Do not start a phase before the previous one is merged and its lane
is green.

**Revised 2026-08-01.** The consumer requirement (an Unreal-Substrate-style
material system) made composed-program reflection and `ParameterBlock<T>`
load-bearing.

**Phase 1 is unchanged and is not to be re-opened.** Verified against the
generated tree the implementer committed in `e6efccc` — 138 files under
`src/Ahjo.Vulkan.Slang.Native/Generated/`, `0` mangled `EntryPoint = "_Z…"`
`DllImport`s — every symbol 3a/3b needs is present and none is in the `.rsp`'s
`--exclude` list:

| needed by | symbol | where |
|---|---|---|
| 3a | `createCompositeComponentType`, `createTypeConformanceComponentType`, `loadModuleFromSourceString` | `Generated/ISession.cs` |
| 3a | `ITypeConformance`, `SpecializationArg` | `Generated/ITypeConformance.cs`, `Generated/SpecializationArg.cs` |
| 3a/3b | `link`, `getLayout`, `getTargetCode`, `getEntryPointCode`, `getEntryPointMetadata`, `getSpecializationParamCount`, `specialize` | `Generated/IComponentType.cs` |
| 3b | `isParameterLocationUsed` | `Generated/IMetadata.cs:45` |
| 3b | all 13 extra `spReflection*` flat exports listed in §3b.8 | `Generated/SlangApi.cs` |

**Phase 2 gains §2.8** (the composition surface it must expose for 3a).
**Phase 3 splits into 3a (composition) and 3b (reflection)**, and its former
§3.4 multi-set guard is deleted — spec OPEN-2 is resolved.

Nothing in this plan touches `src/Ahjo.Vulkan/`. If a step appears to require
editing a type under `src/Ahjo.Vulkan/Pipelines/`, stop — that contradicts D5
and needs a decision, not an edit.

Every generated file lands under `src/Ahjo.Vulkan.Slang.Native/Generated/` and
is **never hand-edited** (invariant #4). If generated output is wrong, the fix
is in `tools/generate-slang.rsp`.

---

## Phase 1 — `Ahjo.Vulkan.Slang.Native`

### 1.1 Pin the version and the checksums

`Directory.Build.props`, in the pinned-version `PropertyGroup` (after
`KtxVersion`, currently line 28), add with a comment in the style of its
neighbours:

```xml
<SlangVersion>v2026.14.1</SlangVersion>
<SlangWinX64Sha256>5ed0a59d650a0af0aca45d5db4e083b3d8fb5cea05748747dd95dfbe9c580658</SlangWinX64Sha256>
<SlangLinuxX64Sha256>21f2d7847385a770e569fb61b1507a7794d742d97850bce0432bff0032ca005f</SlangLinuxX64Sha256>
```

Asset URLs are
`https://github.com/shader-slang/slang/releases/download/$(SlangVersion)/slang-<ver-without-v>-windows-x86_64.zip`
and `…-linux-x86_64.tar.gz`. Derive the `<ver-without-v>` form with
`$(SlangVersion.TrimStart('v'))`.

The comment must state: **checksums are pinned because we consume a prebuilt
archive rather than building from source (spec D3); a `SlangVersion` bump
requires re-recording both hashes and a regen run.**

### 1.2 Stub headers

New directory `native/slang/stubs/`, mirroring `native/ktx/stubs/`:

- `inttypes.h`
  ```c
  #pragma once
  #include <stdint.h>
  typedef long long          intmax_t;
  typedef unsigned long long uintmax_t;
  ```
- `features.h`
  ```c
  #pragma once
  #define __GLIBC__ 2
  ```

Header comment on both, matching `native/stubs/stdint.h:1-7`: these exist so
libclang can parse `slang.h:517` (`#include <inttypes.h>`) and `slang.h:501`
(`#include <features.h>`, taken because the pinned parse target is
`x86_64-unknown-linux-gnu`) without a system C toolchain, so the generated
bindings are a function of `SlangVersion` alone and not of who regenerated them.
`native/stubs/stddef.h` and `native/stubs/stdint.h` are reused as-is.

### 1.3 `tools/generate-slang.rsp`

New file. This exact flag set was verified against the `v2026.14.1` release
archive's `include/` headers: 138 files, 0 errors, 0 warnings when compiled
under `net10.0`/C# 14, and the resulting binding ran the full
create-session → compile → link → `getEntryPointCode` → `getLayout` sequence
against the shipped `libslang.so`.

```
--file
native/slang/include/slang.h
# slang.h:2379 #includes slang-deprecated.h, but ClangSharp only emits
# declarations from files named here. Leave slang-deprecated.h off and the
# entire flat spReflection_* surface (173 exports) silently disappears and
# ShaderReflection / TypeLayoutReflection come out as empty structs.
--traverse
native/slang/include/slang.h
native/slang/include/slang-deprecated.h
native/slang/include/slang-image-format-defs.h
--include-directory
native/slang/stubs
--include-directory
native/stubs
--include-directory
native/slang/include
--namespace
Ahjo.Vulkan.Slang.Native
# NOT "Slang": a type named Slang inside namespace Ahjo.Vulkan.Slang.Native is
# unreachable from namespace Ahjo.Vulkan.Slang, because the identifier binds to
# the enclosing namespace before any imported type.
--methodClassName
SlangApi
--libraryPath
slang
--output
src/Ahjo.Vulkan.Slang.Native/Generated
--output-mode
CSharp
--language
c++
# Same reproducibility argument as tools/generate-ktx.rsp:30-38. slang.h:218-226
# expands SLANG_MCALL to __stdcall only on the Microsoft family; both shipped
# RIDs are x64, which has exactly one calling convention, so pinning the parse
# to linux-gnu changes nothing at runtime and keeps the output host-independent.
--additional
--target=x86_64-unknown-linux-gnu
--config
latest-codegen
implicit-vtbls
generate-vtbl-index-attribute
generate-native-inheritance-attribute
generate-callconv-member-function
generate-helper-types
generate-file-scoped-namespaces
generate-unmanaged-constants
generate-disable-runtime-marshalling
generate-aggressive-inlining
exclude-enum-operators
exclude-funcs-with-body
strip-enum-member-type-name
multi-file
# slang::Attribute (slang.h:2402) shadows System.Attribute inside the generated
# namespace, which makes every [NativeTypeName] a CS0616. 2032 errors from one
# name collision.
--remap
Attribute=SlangUserAttributeInfo
# ClangSharp propagates the enclosing member function's calling convention onto
# nested C-callback parameter types, so the vtbl slot and the helper parameter
# disagree ([MemberFunction] vs [Cdecl], CS1503). Remapping the four callback
# typedefs to nint makes both sides agree. The slang:: qualification on the
# last two is REQUIRED — the bare names are silently ignored.
FileSystemContentsCallBack=nint
SlangDiagnosticCallback=nint
slang::VMExtFunction=nint
slang::VMPrintFunc=nint
# TypeReflection::Kind (slang.h:2446) and DeclReflection::Kind (slang.h:3857)
# are nested `enum class`es emitted without an underlying type, while their
# members come from uint-backed SlangTypeKind / SlangDeclKind: 29x CS0266.
--with-type
Kind=uint
# Three declarations that would produce EntryPointNotFoundException or worse.
# Same reasoning as tools/generate-ktx.rsp:61-68.
#
# spReflection_GetSession is declared at slang-deprecated.h:1070, OUTSIDE that
# file's extern "C" block, so it has C++ linkage. It exists in the binary only
# as _Z23spReflection_GetSessionP18SlangProgramLayout, which is the Itanium
# mangling — the binding would resolve on Linux and throw on Windows.
#
# slang_getEmbeddedCoreModule (slang.h:5852) has the same problem.
#
# slang::VariableReflection::getDefaultValueBlob (slang.h:3228) is an exported
# C++ member function; ClangSharp emits CallingConvention.MemberFunction on a
# DllImport, which is not a member of System.Runtime.InteropServices.CallingConvention
# (CS0117), plus a mangled EntryPoint.
#
# NONE of these is a virtual member. Never --exclude a virtual member name in
# this file: ClangSharp removes the vtable slot and silently shifts every later
# index. Verified — excluding ISlangFileSystemExt::enumeratePathContents moved
# getOSPathKind from index 11 to 10 while its body still called lpVtbl[10].
--exclude
spReflection_GetSession
slang_getEmbeddedCoreModule
getDefaultValueBlob
--with-access-specifier
*=public
```

Add `generate-slang.rsp` to the table in `tools/CLAUDE.md` and mention
`SlangVersion` in its "Header versions are pinned" sentence.

### 1.4 `src/Ahjo.Vulkan.Slang.Native/Ahjo.Vulkan.Slang.Native.csproj`

Model on `Ahjo.Vulkan.Ktx.Native.csproj`. Differences from that file, all
deliberate, all commented:

- **No `ProjectReference`.** Slang has no Vulkan surface (spec D1/E8).
- Properties: `RootNamespace`/`AssemblyName` = `Ahjo.Vulkan.Slang.Native`,
  `GeneratedDir` = `Generated\`,
  `GeneratorResponseFile` = `$(ToolsDir)generate-slang.rsp`.
- Package metadata: `IsPackable=true`, `PackageId=Ahjo.Vulkan.Slang.Native`,
  `Description` naming `$(SlangVersion)`, `PackageTags` =
  `slang;shader;spirv;hlsl;compiler;reflection;gpu;interop;native;pinvoke;bindings`,
  `MinVerTagPrefix=v`, `MinVerDefaultPreReleaseIdentifiers=alpha.0`.
- `PackageReference` on `MinVer` and `Microsoft.SourceLink.GitHub`, both
  `PrivateAssets="all"`; `None Include="README.md" Pack="true" PackagePath="\"`.
- Host RID detection `_SlangRid` → `win-x64` / `linux-x64` only, with the
  no-lane-no-RID comment (copy the reasoning from
  `Ahjo.Vulkan.Ktx.Native.csproj:33-38`, substituting: Slang publishes
  `windows-aarch64`, `linux-aarch64`, `macos-x86_64` and `macos-aarch64` and all
  four stay unshipped until a lane runs them).
- Paths:
  ```
  _SlangSrcDir        = $(NativeDir)slang\
  _SlangDownloadDir   = $(_SlangSrcDir)downloaded\
  _SlangIncludeDir    = $(_SlangSrcDir)include\
  _SlangStagedRootDir = $(_SlangSrcDir)staged\
  _SlangStagedDir     = $(_SlangStagedRootDir)$(_SlangRid)\
  ```
- Per-RID file lists:
  - `win-x64`: `slang.dll`, `slang-compiler.dll` (both extracted from `bin/`).
    `slang.dll` is a 158 KB forwarder that loads `slang-compiler.dll` at
    runtime; shipping only one of them yields a `DllNotFoundException` at the
    first call.
  - `linux-x64`: `libslang.so`, produced by **copying and renaming**
    `lib/libslang-compiler.so.0.$(SlangVersion.TrimStart('v'))`. In the archive
    `libslang.so` is a symlink and a nupkg cannot carry symlinks
    (`Ahjo.Vulkan.Ktx.Native.csproj:55-61`). The rename was verified to load
    and run.
  - Not shipped, with a comment saying why: `libslang-llvm.so` /
    `slang-llvm.dll` (152 MB / 84 MB, CPU targets only),
    `libslang-glsl-module` (GLSL input only), `libgfx`, `libslang-rt`, the
    `slang-standard-module-*` tree (the core module is embedded — a compile was
    verified with none of it present), `bin/`, `share/doc/`.
    `slang-glslang` — see **OPEN-1**, not shipped in Phase 1.
- `None Include="$(_SlangStagedDir)<file>" CopyToOutputDirectory="PreserveNewest"
  Visible="false" Pack="false" Link="<file>"` for each host-RID file, gated on
  `'$(_SlangRid)' != '' and '$(SkipSlangNativeFetch)' != 'true'` — the same
  Content+Link propagation trick `Ahjo.Vulkan.Ktx.Native.csproj:79-85` uses.
- Targets:
  1. `FetchSlang` (`Condition="!Exists('$(_SlangStagedDir)<primary file>')"`,
     `BeforeTargets="AssignTargetPaths"`):
     - `<DownloadFile SourceUrl="…" DestinationFolder="$(_SlangDownloadDir)" />`
     - `<GetFileHash Files="…" Algorithm="SHA256" HashEncoding="hex">` →
       `<Error>` when the hash does not equal `$(SlangWinX64Sha256)` /
       `$(SlangLinuxX64Sha256)`. The error text must print both the expected and
       the actual hash.
     - Extract: `<Unzip>` on Windows; `<Exec Command="tar -xzf … -C …" />` on
       Linux (no built-in MSBuild task handles `.tar.gz`).
     - Copy the shipped files into `$(_SlangStagedDir)`, renaming the Linux one.
     - Copy `include/slang.h`, `include/slang-deprecated.h`,
       `include/slang-image-format-defs.h` into `$(_SlangIncludeDir)` — the
       stable, version-independent path `generate-slang.rsp` refers to (same
       trick as `_KtxStagedHeaderDir`, `Ahjo.Vulkan.Ktx.Native.csproj:50-53`).
     - Copy `LICENSE` to `$(_SlangSrcDir)SLANG-LICENSE.txt`.
     - `<Error>` when `_SlangRid` is empty, naming `SkipSlangNativeFetch=true`
       as the escape hatch (mirrors `Ahjo.Vulkan.Ktx.Native.csproj:158-159`).
  2. `Regenerate` (`DependsOnTargets="FetchSlang;RunClangSharpSlangGenerator"`),
     not wired into `Build`.
  3. `RunClangSharpSlangGenerator`: `dotnet tool run ClangSharpPInvokeGenerator
     @"$(GeneratorResponseFile)"` from `$(RepositoryRoot)`.
  4. `PackSlangRuntimes` (`BeforeTargets="_GetPackageFiles;GenerateNuspec"`):
     emit `None … PackagePath="runtimes\<rid>\native\<file>"` for every
     `<rid>/<file>` pair that exists under `$(_SlangStagedRootDir)`, plus
     `SLANG-LICENSE.txt` at `PackagePath="\"`.

Add `.gitignore` entries for `native/slang/downloaded/` and
`native/slang/staged/` if the repo's existing pattern for `native/ktx/` does the
same; match whatever is already there.

### 1.5 Generate and commit

```bash
dotnet build src/Ahjo.Vulkan.Slang.Native -t:Regenerate
```

Expected: ~138 files under `Generated/`. Sanity checks before committing:

- `grep -c 'EntryPoint = "_Z' src/Ahjo.Vulkan.Slang.Native/Generated/*.cs` → 0.
  Any hit is a C++-mangled symbol and must be added to `--exclude`, not left in.
- `IComponentType.cs` contains `[VtblIndex(4)] … getLayout` dispatching through
  `lpVtbl[4]`.
- `SlangApi.cs` contains `spReflection_getGlobalParamsTypeLayout`,
  `spReflectionTypeLayout_getDescriptorSetCount` and
  `spReflectionEntryPoint_getStage`.
- `dotnet build src/Ahjo.Vulkan.Slang.Native` is clean under
  `TreatWarningsAsErrors`. If a generated file trips an analyzer, fix it in the
  `.rsp` — never with a `#pragma` and never by editing `Generated/`.

Add the project to `Ahjo.Vulkan.slnx` after the `Ahjo.Vulkan.Ktx.Native` entry.

### 1.6 `src/Ahjo.Vulkan.Slang.Native/CLAUDE.md` and `README.md`

`CLAUDE.md` mirrors `src/Ahjo.Vulkan.Ktx.Native/CLAUDE.md` and must state:
`Generated/` is never hand-edited; the `.rsp` plus `SlangVersion` are the knobs;
the shipped subset and why everything else is excluded; **and the two rules that
are specific to this binding** — never `--exclude` a virtual member (slot
shifting), and the reflection surface lives in `slang-deprecated.h`, guarded by
the drift test.

`README.md` (packed into the nupkg) follows
`src/Ahjo.Vulkan.Ktx.Native/README.md`: what the package is, the RID support
table with `*-arm64` / `osx-*` marked "no — no lane", and a **Licensing**
section stating Slang is Apache-2.0 WITH LLVM-exception (Khronos / NVIDIA) and
this package's binding code is MIT.

### 1.7 `tests/Ahjo.Vulkan.Slang.Native.Tests`

New xUnit v3 project modelled on
`tests/Ahjo.Vulkan.Ktx.Native.Tests/Ahjo.Vulkan.Ktx.Native.Tests.csproj`
(`IsTestProject`, `Microsoft.NET.Test.Sdk`, `xunit.v3`,
`xunit.runner.visualstudio`, one `ProjectReference` to
`Ahjo.Vulkan.Slang.Native`). Must acquire no Vulkan device — same contract as
the ktx suite (`tests/CLAUDE.md`).

`SlangSmokeTests.cs`:

1. `GlobalSession_Creates` — `slang_createGlobalSession(0, out)` returns `>= 0`
   and a non-null pointer; dispose via `release()`.
2. `BuildTag_MatchesPinnedVersion` — `getBuildTagString()` equals
   `SlangVersion` without the leading `v`. This is what catches a staged binary
   that did not get refreshed after a version bump.
3. `Compile_SimpleShader_ProducesSpirv` — source string with one
   `[shader("vertex")]` entry point; assert `getEntryPointCode` succeeds, the
   blob is a non-zero multiple of 4 bytes, and word 0 is `0x07230203`.
4. `Compile_BrokenShader_ProducesDiagnosticsAndNullModule` — assert
   `loadModuleFromSourceString` returns `null` **and** the diagnostics blob is
   non-empty and contains `"undefined identifier"`. (Verified: Slang signals
   this failure by returning `nullptr`, so a result-code-only check would pass
   on a broken compile.)
5. `Reflection_WalksGlobalScope` — compile the fixture from step 3 extended with
   a `ConstantBuffer`, a `Texture2D`, a `SamplerState` and a
   `[[vk::push_constant]]` block; assert `getDescriptorSetCount == 1`, four
   ranges of category `DESCRIPTOR_TABLE_SLOT` at index offsets 0..3 with the
   expected `SlangBindingType`s, and one range of category
   `PUSH_CONSTANT_BUFFER`.

`SlangExportDriftTests.cs` — the drift test (spec D6):

```csharp
private static readonly string[] RequiredExports = { /* the ~30 names the wrapper calls */ };

[Fact]
public void EveryRequiredExport_IsPresentInTheShippedBinary()
```

Implementation: `NativeLibrary.Load("slang", typeof(SlangApi).Assembly, null)`
then `NativeLibrary.TryGetExport` per name; collect misses and assert the
collection is empty with the missing names in the message. No reflection, no
`Assembly.GetTypes()` — AOT-clean by construction. Seed `RequiredExports` with:
`slang_createGlobalSession`, `spGetBuildTagString`,
`spReflection_GetParameterCount`, `spReflection_GetParameterByIndex`,
`spReflection_getEntryPointCount`, `spReflection_getEntryPointByIndex`,
`spReflection_getGlobalParamsTypeLayout`,
`spReflectionTypeLayout_getKind`,
`spReflectionTypeLayout_GetSize`,
`spReflectionTypeLayout_getAlignment`,
`spReflectionTypeLayout_GetElementTypeLayout`,
`spReflectionTypeLayout_GetParameterCategory`,
`spReflectionTypeLayout_getDescriptorSetCount`,
`spReflectionTypeLayout_getDescriptorSetSpaceOffset`,
`spReflectionTypeLayout_getDescriptorSetDescriptorRangeCount`,
`spReflectionTypeLayout_getDescriptorSetDescriptorRangeIndexOffset`,
`spReflectionTypeLayout_getDescriptorSetDescriptorRangeDescriptorCount`,
`spReflectionTypeLayout_getDescriptorSetDescriptorRangeType`,
`spReflectionTypeLayout_getDescriptorSetDescriptorRangeCategory`,
`spReflectionTypeLayout_GetType`,
`spReflectionType_GetKind`, `spReflectionType_GetElementCount`,
`spReflectionType_GetElementType`, `spReflectionType_GetScalarType`,
`spReflectionType_GetRowCount`, `spReflectionType_GetColumnCount`,
`spReflectionVariableLayout_GetVariable`,
`spReflectionVariableLayout_GetTypeLayout`,
`spReflectionVariableLayout_GetOffset`,
`spReflectionVariableLayout_GetSemanticName`,
`spReflectionVariableLayout_GetSemanticIndex`,
`spReflectionVariable_GetName`,
`spReflectionEntryPoint_getName`, `spReflectionEntryPoint_getStage`,
`spReflectionEntryPoint_getParameterCount`,
`spReflectionEntryPoint_getParameterByIndex`.
Trim to exactly what Phase 3 ends up calling when Phase 3 lands.

Register the project in `Ahjo.Vulkan.slnx` and in the table in
`tests/CLAUDE.md` ("Slang binding checks — must pass with **no** Vulkan
loader/ICD installed").

### 1.8 `.github/workflows/build-slang-native.yml`

Copy `build-ktx-native.yml` and change: matrix `win-x64`/`windows-latest` and
`linux-x64`/`ubuntu-latest`; cache path `native/slang/staged/${{ matrix.rid }}`
keyed on `hashFiles('Directory.Build.props',
'src/Ahjo.Vulkan.Slang.Native/Ahjo.Vulkan.Slang.Native.csproj',
'.github/workflows/build-slang-native.yml')`; build the project; run
`Ahjo.Vulkan.Slang.Native.Tests` **in the same job, before** the artifact
upload; upload `slang-native-<rid>`.

Provision no Vulkan loader and no ICD, and say so in a comment for the same
reason `build-ktx-native.yml:98-102` does. Do not set `AHJO_VULKAN_TIER`.

Wire it into `ci.yml` next to the `ktx-native` job (`ci.yml:399-401`) and into
`publish.yml` alongside the ktx artifact download + multi-RID pack, so the
binary a release attaches comes from the definition CI proved.

Add a `## slang-native lane` section to `.github/CLAUDE.md` in the same voice as
the `ktx-native` section: build-artifact check, not wrapper coverage, no ICD by
design, do not grow it.

### 1.9 Repo docs

- `README.md`: add `Ahjo.Vulkan.Slang.Native` to the package list (~line 25),
  add the `src/` bullet (~line 82) and the `tests/` bullet (~line 89), add the
  regen command to the block at ~line 104, and update the "one `v*` tag ships
  all four packages" sentence — it is six now (here and in
  `.github/CLAUDE.md:33`, `CLAUDE.md`'s intro line, and
  `Directory.Build.props:56-58`'s comment, which currently says "the two
  publishable projects are…" and is already stale).
- `docs/aot-notes.md`: note that the Slang binding dispatches through
  `delegate* unmanaged[MemberFunction]` vtable slots with no `ComWrappers` and
  no `[ComImport]`, and that `[VtblIndex]`/`[NativeTypeName]` are
  `[Conditional("DEBUG")]`.

---

## Phase 2 — `Ahjo.Vulkan.Slang`: the compiler API

### 2.1 Project

`src/Ahjo.Vulkan.Slang/Ahjo.Vulkan.Slang.csproj`: `IsPackable=true`,
`PackageId=Ahjo.Vulkan.Slang`, `ProjectReference` to `Ahjo.Vulkan` and
`Ahjo.Vulkan.Slang.Native`, MinVer + SourceLink + `README.md`, same metadata
shape as `src/Ahjo.Vulkan/Ahjo.Vulkan.csproj`. Namespace `Ahjo.Vulkan.Slang`.
Add to `Ahjo.Vulkan.slnx`.

### 2.2 Interop helper

`src/Ahjo.Vulkan.Slang/Internal/SlangUtf8.cs` — `internal static unsafe class`:

- `static string? ToString(sbyte* utf8)` — mirrors
  `src/Ahjo.Vulkan/Internal/Utf8.cs:11-12`.
- `static string ReadBlob(ISlangBlob* blob)` — `getBufferPointer()` +
  `getBufferSize()` → `string`, empty string for null.
- A `ref struct ScopedUtf8` holding either a `stackalloc`-backed or pooled
  buffer, exposing `sbyte* Ptr`, constructed from `ReadOnlySpan<byte>` or
  `string`. It **must** append an explicit `0` byte and the pointer must only be
  taken inside a `fixed` scope that covers the native call.

File-level comment: why this exists and why it does not violate invariant #1 —
Slang paths and entry-point names are runtime-variable, unlike Vulkan extension
names; `Utf8Name.FromLiteral` stays the tool for the constants
(`"spirv_1_5"u8` and friends), and the prohibition being honoured is
"never hand native code a GC-movable, unterminated pointer", not "never
allocate at setup time". Cross-reference
`src/Ahjo.Vulkan/Pipelines/GraphicsPipelineBuilder.cs:213-220` as the existing
precedent.

### 2.3 Public types

All in namespace `Ahjo.Vulkan.Slang`, one file each.

```csharp
public enum SlangOptimizationLevel { None, Default, High, Maximal }

public readonly record struct SlangSessionDescription
{
    public ReadOnlySpan<byte>          SpirvProfile { get; init; } // default "spirv_1_5"u8
    public SlangOptimizationLevel      Optimization { get; init; }
    public bool                        EmitSpirvDirectly { get; init; } // default true
    public string[]?                   SearchPaths { get; init; }
}

public readonly record struct SlangCompileRequest
{
    public string?                     Path { get; init; }        // file source
    public string?                     Source { get; init; }      // in-memory source
    public string?                     ModuleName { get; init; }
    public IReadOnlyList<string>?      EntryPoints { get; init; } // null = all [shader(...)] entry points
}

public readonly record struct SlangEntryPointInfo(string Name, ShaderStages Stage);

public sealed class SlangCompilationException : Exception
{
    public string Diagnostics { get; }
}

public sealed class SlangCompiler : IDisposable
{
    public static SlangCompiler Create();
    public SlangSession CreateSession(in SlangSessionDescription description);
    public string BuildTag { get; }
}

public sealed class SlangSession : IDisposable
{
    public SlangProgram Compile(in SlangCompileRequest request);
}

public sealed class SlangProgram : IDisposable
{
    public int                 EntryPointCount { get; }
    public SlangEntryPointInfo EntryPoint(int index);
    public ReadOnlySpan<uint>  Spirv(int entryPointIndex);
    public string?             Warnings { get; }
    // SlangReflection Reflection { get; }  <- Phase 3b
}
```

`SlangProgram` must hold the **linked** `IComponentType*` in a field an
`internal` member can hand to Phase 3b, and must expose no way to obtain a
program from anything other than a successful `link`. Spec D9 rule 1: reflection
that is not taken from the same linked component the SPIR-V came from is wrong,
and the type system is where that is cheapest to enforce.

Rules the implementation must follow:

1. `SlangCompiler.Create()` calls `slang_createGlobalSession(0, out)`;
   `Dispose()` calls `release()` on the global session and **does not** call
   `slang_shutdown()` — see **OPEN-3**.
2. `SlangSession.Compile` performs: `loadModuleFromSource{String,}` →
   `findAndCheckEntryPoint` per requested entry point (or
   `getDefinedEntryPointCount`/`getDefinedEntryPoint` when `EntryPoints` is
   null) → `createCompositeComponentType([module, ...entryPoints])` → `link`.
   The linked `IComponentType*` is what `SlangProgram` owns.
3. **Every** call taking an `ISlangBlob** outDiagnostics` passes one, reads it
   before inspecting the result, and `release()`s it. Failure — a negative
   `SlangResult` **or** a null returned `IModule*`/`IEntryPoint*` — throws
   `SlangCompilationException` whose `Message` is
   `$"Slang compilation failed: {firstLineOfDiagnostics}"` and whose
   `Diagnostics` is the full blob text. A non-empty blob on success goes to
   `SlangProgram.Warnings`. There must be no code path that returns an empty
   SPIR-V span on failure.
4. `Spirv(int)` calls `getEntryPointCode(index, 0, …)` once per index, caches
   the `ISlangBlob*` in the program, and returns
   `new ReadOnlySpan<uint>(blob->getBufferPointer(), (int)(size / 4))`. XML doc
   states the span is valid until `Dispose`, mirroring
   `src/Ahjo.Vulkan/Memory/SpirvBlob.cs:37-47`.
5. Target setup: `TargetDesc.structureSize = sizeof(TargetDesc)` and
   `SessionDesc.structureSize = sizeof(SessionDesc)` must be set explicitly —
   ClangSharp drops the C++ default member initialisers, and Slang reads those
   fields. `format = SLANG_SPIRV`, `profile = findProfile(SpirvProfile)`,
   `flags = SLANG_TARGET_FLAG_GENERATE_SPIRV_DIRECTLY`, and `Optimization` as a
   `CompilerOptionEntry { name = CompilerOptionName.Optimization, value = { kind
   = Int, intValue0 = (int)level } }`.
6. Every wrapper type is `sealed` and holds its native pointer in a private
   field with an `ObjectDisposedException` guard, matching the wrapper's
   existing handle discipline.

### 2.4 `SlangProgram` → `Device.CreateShaderModule`

No new API on `Device`. The sample/test call site is
`device.CreateShaderModule(program.Spirv(i))`, using the existing
`CreateShaderModule(ReadOnlySpan<uint>)` overload. If that overload does not
exist in the shape needed, **stop** — adding it is a change to
`src/Ahjo.Vulkan/` and is outside this plan.

### 2.5 Tests — `tests/Ahjo.Vulkan.Slang.Tests`

xUnit v3, `ProjectReference` to `Ahjo.Vulkan.Slang`. Runs in the Windows
`build-test` job. Cases:

1. `Create_ExposesPinnedBuildTag`.
2. `Compile_FromSourceString_OneEntryPoint_ProducesSpirv` — first word
   `0x07230203`, length multiple of 4.
3. `Compile_FromFile_ProducesSpirv` — a `.slang` fixture copied to output.
4. `Compile_AllEntryPoints_WhenEntryPointsIsNull` — a two-entry-point source
   yields `EntryPointCount == 2` with `Stage` `Vertex` and `Fragment`.
5. `Compile_SyntaxError_ThrowsWithCompilerText` — asserts
   `SlangCompilationException.Diagnostics` contains `"error[E30015]"` and
   `"undefined identifier"`, and that `Message` is not empty. **This is the
   acceptance criterion "never a silent empty blob".**
6. `Compile_UndefinedEntryPoint_Throws` — `findAndCheckEntryPoint` failure path.
7. `Warnings_SurfaceOnSuccess` — a source that produces a Slang warning yields
   non-null `Warnings` and a valid blob.
8. `OptimizationLevels_AllSucceed` — `[Theory]` over every
   `SlangOptimizationLevel`. **This is the OPEN-1 decision procedure**: if any
   level fails with `failed to load downstream compiler 'spirv-opt'`, stop and
   report rather than capping the enum or growing the package.
9. `TwoCompilers_InSequence_Work` — create, dispose, create, dispose. **This is
   the OPEN-3 decision procedure**; if it fails, stop and ask.
10. `Spirv_FeedsCreateShaderModule` — behind `TestGate.RequireDriver`, per
    `tests/CLAUDE.md`. Creates a `ShaderModule` from `program.Spirv(0)` and
    disposes it.

Register in `Ahjo.Vulkan.slnx` and in `tests/CLAUDE.md`'s table.

### 2.6 AOT coverage

`samples/AotSmoke/AotSmoke.csproj`: add a `ProjectReference` to
`Ahjo.Vulkan.Slang`, and in `Program.cs` compile the existing triangle shader's
Slang equivalent (a new `samples/AotSmoke/Shaders/triangle.slang`, added
alongside — **not** replacing — the linked GLSL sources) and feed the result to
the existing `CreateShaderModule` call. Do **not** remove the `CompileShaders`
`glslc` target; that is the follow-up issue's job.

Publish locally and confirm zero trim/AOT warnings:

```bash
dotnet publish samples/AotSmoke/AotSmoke.csproj -c Release -r win-x64 -p:IlcUseEnvironmentalTools=true
```

If ILC reports anything, it is a design problem, not a suppression candidate.

### 2.7 Docs

`src/Ahjo.Vulkan.Slang/README.md` (packed), a `CLAUDE.md` for the project
stating the setup-time allocation posture explicitly (invariant #3 does not
apply here, and no benchmark should be added), and `README.md` +
`.github/CLAUDE.md` package-count updates.

No `docs/benchmarks.md` change and no new benchmark class: nothing in this
phase is on a per-frame path (spec §Problem).

### 2.8 Composition surface (spec D8) — added by the 2026-08-01 revision

Phase 2 must expose the pieces Phase 3a composes, because they are the same
native objects `Compile` already creates internally. Add to
`src/Ahjo.Vulkan.Slang/`:

```csharp
public sealed class SlangModule : IDisposable          // wraps IModule*
{
    public string Name { get; }
    public int    DefinedEntryPointCount { get; }      // IModule::getDefinedEntryPointCount
    public SlangEntryPoint DefinedEntryPoint(int index);
    public SlangEntryPoint FindEntryPoint(string name, ShaderStages stage);
}

public sealed class SlangEntryPoint : IDisposable      // wraps IEntryPoint*
{
    public string       Name  { get; }
    public ShaderStages Stage { get; }
}
```

on `SlangSession`:

```csharp
public SlangModule LoadModule(string moduleName);
public SlangModule LoadModuleFromSource(string moduleName, string path, ReadOnlySpan<byte> source);
public SlangModule LoadModuleFromSource(string moduleName, string path, string source);
```

Rules:

1. `LoadModuleFromSource` calls `loadModuleFromSourceString(name, path, source, &diag)`.
   A `null` return is a failure even when no result code says so (§2.3 rule 3);
   the diagnostics blob is read first and carried in
   `SlangCompilationException.Diagnostics`.
2. `FindEntryPoint` calls `findAndCheckEntryPoint(name, stage, &out, &diag)`.
   `ShaderStages` → `SlangStage` is a total switch; unmapped stages throw
   `NotSupportedException` rather than passing `SLANG_STAGE_NONE`.
3. `SlangEntryPoint.Stage` is the stage the caller passed to `FindEntryPoint`,
   or `spReflectionEntryPoint_getStage` for `DefinedEntryPoint`. Do **not** try
   to read it from `spReflectionVariableLayout_getStage` — that returns
   `SLANG_STAGE_NONE` for parameters (spec E13).
4. Modules loaded from a string are registered in the session under
   `moduleName`, so a later module's `import <moduleName>;` resolves with no file
   system present. Verified; a test asserts it (§2.5 case 11).
5. `SlangSession.Compile` (§2.3 rule 2) is re-expressed in terms of these types
   so there is exactly one code path that loads a module and one that finds an
   entry point.

Tests to add to §2.5:

11. `LoadModuleFromSource_TwoModules_SecondImportsFirst` — module `a` declares a
    `public` struct and function; module `b` does `import a;` and uses them;
    assert both loads succeed with a null diagnostics blob.
12. `FindEntryPoint_WrongStage_ThrowsWithCompilerText` — ask for a
    `[shader("fragment")]` entry point as `ShaderStages.Vertex`; assert
    `SlangCompilationException` with non-empty `Diagnostics`.
13. `DefinedEntryPoints_Enumerate` — a module with two `[shader(...)]` entry
    points reports `DefinedEntryPointCount == 2` with the expected names and
    stages.

---

## Phase 3a — composition: build and link a program from N components

Spec D8/D9. Nothing in this phase touches `src/Ahjo.Vulkan/`, and nothing in it
reflects.

### 3a.1 `src/Ahjo.Vulkan.Slang/SlangProgramBuilder.cs`

```csharp
public sealed class SlangProgramBuilder
{
    public SlangProgramBuilder Add(SlangModule module);
    public SlangProgramBuilder Add(SlangEntryPoint entryPoint);
    public SlangProgramBuilder AddTypeConformance(string concreteType, string interfaceType);
    public SlangProgram        Link();
}
```

obtained from `SlangSession.CreateProgram()`. Implementation rules:

1. The builder accumulates `IComponentType*` in **caller `Add` order** into a
   `List<nint>` (setup-time allocation is fine, spec §Problem). `Link()` copies
   them into a `stackalloc IComponentType*[n]` — or an `ArrayPool` rental above a
   threshold — and calls
   `session->createCompositeComponentType(comps, n, &composite, &diag)` then
   `composite->link(&linked, &diag)`.
2. **The ordering contract is the XML doc's first sentence**, worded to the
   effect of: *the order components are added is the order Slang assigns
   descriptor bindings, descriptor spaces and entry-point indices; adding the
   same components in a different order produces a different, equally valid,
   incompatible layout.* Spec E12 is the measurement; do not soften this to
   "order may matter".
3. `Link()` on an empty builder throws `InvalidOperationException`. `Link()`
   twice on the same builder is allowed and returns independent `SlangProgram`s —
   the builder holds no linked state.
4. `AddTypeConformance(concrete, iface)`:
   - `spReflection_FindTypeByName` on the *composite's* layout for each name; a
     null return throws `ArgumentException` naming the type that was not found.
     Resolving a type name needs a composite, so the builder defers conformance
     resolution to the start of `Link()`: build a composite from the
     modules/entry points, resolve the names against `composite->getLayout(0, …)`,
     then build a second composite that appends the `ITypeConformance*`
     components, and link that. Comment that ordering — it is not obvious, and it
     is why conformances resolve lazily rather than at `AddTypeConformance` time.
   - `session->createTypeConformanceComponentType(concrete, iface, &conf, -1, &diag)`;
     `conformanceIdOverride = -1` means "let Slang assign the dispatch ID"
     (`slang.h:4620-4623`). Do not expose the override in Phase 3a.
5. **Do not add a `Specialize` method.** Spec D9 / OPEN-4:
   `IComponentType::specialize` segfaults inside Slang for the interface-typed
   `ParameterBlock` case. Leave a comment in this file saying so, so the omission
   reads as a decision rather than an oversight, and so a later phase adds the
   D9 rule 3 pre-flight guard rather than the bare call.
6. `SlangProgram.EntryPoint(int)` is backed by `spReflection_getEntryPointByIndex`
   order, and its XML doc states that `Spirv(i)` and `EntryPoint(i)` use the same
   index (verified, spec E12).

### 3a.2 Tests — added to `tests/Ahjo.Vulkan.Slang.Tests`

1. `Compose_ThreeModulesTwoEntryPoints_Links` — `common` (a `public`
   `ParameterBlock<CameraData>` plus a helper function), `geometry`
   (`import common;` + `[shader("vertex")]`), `material` (`import common;` +
   `[shader("fragment")]`); assert `Link()` succeeds and `EntryPointCount == 2`
   with stages `Vertex`/`Fragment` in add order.
2. `Compose_EntryPointIndex_MatchesSpirv` — `Spirv(0)` and `Spirv(1)` are both
   valid SPIR-V (magic `0x07230203`) and are **different lengths**, guarding
   against both indices returning the same blob.
3. `Compose_OrderIsObservable` — link the same components in two different orders
   and assert `EntryPoint(0).Name` differs. This asserts rule 2's documented
   contract rather than assuming it.
4. `Compose_TypeConformance_Links` — a module with `interface ISurface`, two
   implementations and a `ParameterBlock<ISurface>` global. Without a
   conformance, `Spirv(...)` throws `SlangCompilationException` whose
   `Diagnostics` contains `"no type conformances found"`; with
   `AddTypeConformance("Glossy", "ISurface")` it produces valid SPIR-V.
   **This is the acceptance criterion for D9's "conformance, not `specialize`".**
5. `Compose_UnknownConformanceType_Throws` — `AddTypeConformance("Nope", "ISurface")`
   throws `ArgumentException` naming `Nope`.
6. `Compose_Empty_Throws`.

### 3a.3 Docs

`src/Ahjo.Vulkan.Slang/README.md`: a "composing a program" section whose first
paragraph is the ordering contract and whose second says that specialization of
interface-typed parameter blocks goes through `AddTypeConformance`.

---

## Phase 3b — reflection → the existing description types

Requires 3a. Spec D5/D10.

### 3b.1 `src/Ahjo.Vulkan.Slang/SlangReflection.cs`

```csharp
public enum SlangStageAttribution { ProgramStageUnion, PerEntryPointUsage }

public sealed class SlangReflection
{
    public int  DescriptorSetCount { get; }
    public uint SetIndex(int i);
    public ReadOnlySpan<DescriptorBinding> Bindings(int i);
    public bool TryGetSet(uint setIndex, out ReadOnlySpan<DescriptorBinding> bindings);
    public uint SetLayoutSlotCount { get; }
    public ReadOnlySpan<PushConstantRange> PushConstantRanges { get; }
    public int  EntryPointCount { get; }
    public ReadOnlySpan<VertexAttributeDescription> VertexAttributes(int entryPointIndex);
}
```

on `SlangProgram`:

```csharp
public SlangReflection Reflection { get; }                    // == GetReflection(ProgramStageUnion)
public SlangReflection GetReflection(SlangStageAttribution mode);
```

Populated eagerly into arrays in the constructor (setup-time; the spans are views
over those arrays). Built from `linked->getLayout(0, &diagnostics)` on the
**linked** component the `SlangProgram` owns — there is no constructor that takes
anything else (spec D9 rule 1). A null layout throws
`SlangCompilationException` carrying the diagnostics text.

### 3b.2 The walk

An `internal` recursive method, exactly spec D5's pseudocode. Given
`SlangReflectionTypeLayout* structTl` and `uint absoluteSet`:

**Step 1 — this scope's descriptor sets.** For `s` in
`0 .. spReflectionTypeLayout_getDescriptorSetCount(structTl)`:

- `uint vkSet = absoluteSet + (uint)spReflectionTypeLayout_getDescriptorSetSpaceOffset(structTl, s);`
  — **use `getDescriptorSetSpaceOffset`, never the loop index `s`.** Spec E10:
  `[[vk::binding(7, 2)]]` yields `s == 1` for set 2.
- For `r` in `0 .. …getDescriptorSetDescriptorRangeCount(structTl, s)`:
  - `category = …getDescriptorSetDescriptorRangeCategory(structTl, s, r)`
  - `SLANG_PARAMETER_CATEGORY_PUSH_CONSTANT_BUFFER` → step 4; everything else →

    ```csharp
    new DescriptorBinding {
        Slot   = (uint)…getDescriptorSetDescriptorRangeIndexOffset(structTl, s, r),
        Count  = (uint)…getDescriptorSetDescriptorRangeDescriptorCount(structTl, s, r),
        Type   = MapBindingType(…getDescriptorSetDescriptorRangeType(structTl, s, r)),
        Stages = /* step 5 */,
    }
    ```

  - `SLANG_UNBOUNDED_SIZE` (`~(size_t)0`) and `SLANG_UNKNOWN_SIZE`
    (`SLANG_UNBOUNDED_SIZE - 1`), `slang.h:2416-2417`, are documented returns of
    the index-offset and descriptor-count calls (`slang-deprecated.h:637-657`).
    Throw `NotSupportedException` naming the set and range index rather than
    casting them to `uint`. Silently emitting `Count = 4294967295` is a driver
    crash, not a validation error.

**Step 2 — the block's implicit uniform buffer.** When `structTl` is a
`ParameterBlock` *element* (i.e. a recursive call, not the global-scope entry) and
`spReflectionTypeLayout_GetSize(structTl, SLANG_PARAMETER_CATEGORY_UNIFORM) > 0`,
emit
`new DescriptorBinding { Slot = 0, Type = VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, Count = 1, Stages = … }`
into `absoluteSet`. Spec E11: Slang allocates it, SPIR-V binds it, reflection
never lists it, and the listed ranges already start at 1 to leave room.
**Do not apply this to the global scope** — E11 shows the global implicit
constant buffer *is* listed, and applying it there double-counts binding 0.

**Step 3 — recurse into `ParameterBlock`s.** For `i` in
`0 .. spReflectionTypeLayout_getSubObjectRangeCount(structTl)`:

- `br = …getSubObjectRangeBindingRangeIndex(structTl, i)`
- skip unless `…getBindingRangeType(structTl, br) == SLANG_BINDING_TYPE_PARAMETER_BLOCK`
  — the sub-object range list also contains constant buffers, raw buffers and
  push-constant buffers, which step 1 already handled
- `blockTl  = …getBindingRangeLeafTypeLayout(structTl, br)`
- `offsetVar = …getSubObjectRangeOffset(structTl, i)`
- `childSet = absoluteSet + (uint)spReflectionVariableLayout_GetOffset(offsetVar, SLANG_PARAMETER_CATEGORY_SUB_ELEMENT_REGISTER_SPACE)`
- recurse on `spReflectionTypeLayout_GetElementTypeLayout(blockTl)` with `childSet`

**Never use `spReflectionTypeLayout_getSubObjectRangeSpaceOffset`.** It returns 0
for every sub-object range, including blocks that land in spaces 1 and 2
(spec E10). Put that sentence in a comment at the call site so nobody "fixes" the
code back to it.

**Step 4 — push constants.** Each `PUSH_CONSTANT_BUFFER`-category range found in
step 1 contributes one
`PushConstantRange { Stages = <program union>, Offset = 0, Size = (uint)spReflectionTypeLayout_GetSize(GetElementTypeLayout(paramTypeLayout), UNIFORM) }`,
where `paramTypeLayout` is located by scanning `spReflection_GetParameterByIndex`
for the parameter whose `spReflectionTypeLayout_GetParameterCategory` is
`PUSH_CONSTANT_BUFFER`. **If more than one such parameter exists, throw**

```
NotSupportedException(
  $"Slang program declares {n} push-constant blocks ('{a}', '{b}'…); byte offsets " +
  $"for multiple blocks are not derivable from Slang reflection (issue #166, OPEN-5).")
```

Spec E17: the only offset reflection exposes for the multi-block case is a buffer
index (0, 1), not the byte offset `VkPushConstantRange.offset` needs.

**Step 5 — `Stages`.**

- `ProgramStageUnion`: OR of every `spReflectionEntryPoint_getStage` mapped to
  `ShaderStages`. Computed once.
- `PerEntryPointUsage`: for each entry point `e`, call
  `linked->getEntryPointMetadata(e, 0, &md, &diag)` (read the blob first; a
  failure throws `SlangCompilationException`), then per binding
  `md->isParameterLocationUsed(SLANG_PARAMETER_CATEGORY_DESCRIPTOR_TABLE_SLOT, vkSet, slot, &used)`.
  `Stages` is the OR of the stages that reported `true`; **if no entry point
  reports `true`, fall back to the program union** — `ShaderStages.None` is not a
  usable `VkDescriptorSetLayoutBinding.stageFlags`. `md->release()` in a
  `finally`.
- `PushConstantRange.Stages` is the program union in **both** modes.
  `isParameterLocationUsed` reports push constants as unused even when they are
  (spec E13); a comment at that line says so.

**Step 6 — ordering and the public shape.** Sets sorted ascending by `vkSet`;
bindings within a set ascending by `Slot`. `SetLayoutSlotCount` is
`max(vkSet) + 1`, or `0` when there are no sets. `TryGetSet` is a linear scan over
`DescriptorSetCount` — setup-time, and the set count is single digits.

### 3b.3 `MapBindingType`

`internal static VkDescriptorType MapBindingType(SlangBindingType)` — a total
switch, mutable flag masked off with `SLANG_BINDING_TYPE_BASE_MASK` and
re-applied where it changes the Vulkan type:

| `SlangBindingType` | `VkDescriptorType` |
|---|---|
| `SAMPLER` | `SAMPLER` |
| `TEXTURE` | `SAMPLED_IMAGE` |
| `TEXTURE \| MUTABLE_FLAG` | `STORAGE_IMAGE` |
| `CONSTANT_BUFFER` | `UNIFORM_BUFFER` |
| `TYPED_BUFFER` | `UNIFORM_TEXEL_BUFFER` |
| `TYPED_BUFFER \| MUTABLE_FLAG` | `STORAGE_TEXEL_BUFFER` |
| `RAW_BUFFER`, `RAW_BUFFER \| MUTABLE_FLAG` | `STORAGE_BUFFER` |
| `COMBINED_TEXTURE_SAMPLER` | `COMBINED_IMAGE_SAMPLER` |
| `INPUT_RENDER_TARGET` | `INPUT_ATTACHMENT` |
| `INLINE_UNIFORM_DATA` | `INLINE_UNIFORM_BLOCK` |
| `RAY_TRACING_ACCELERATION_STRUCTURE` | `ACCELERATION_STRUCTURE_KHR` |
| anything else | `throw new NotSupportedException($"Slang binding type {t} has no VkDescriptorType mapping.")` |

Never fall through to a default `VkDescriptorType` — a wrong descriptor type is a
validation error the caller cannot diagnose.

Two changes from the pre-revision table:

- `SLANG_BINDING_TYPE_PARAMETER_BLOCK` is **removed** from the `UNIFORM_BUFFER`
  row. It never reaches this switch — §3b.2 step 3 filters it and recurses. If it
  does reach here, that is a bug in the walk: throw with a message saying so
  rather than mapping it, because mapping it would produce a phantom binding in
  the parent set on top of the real one §3b.2 step 2 synthesizes in the child.
- The synthesized binding from §3b.2 step 2 does not go through this switch at
  all; it is `VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER` by construction.

### 3b.4 Vertex attributes

For entry point `e` **only when `spReflectionEntryPoint_getStage(ep)` maps to
`ShaderStages.Vertex`** — a fragment stage's struct input is `VARYING_INPUT` too
and would otherwise produce phantom attributes.

For each `p = spReflectionEntryPoint_getParameterByIndex(ep, i)` with
`tl = spReflectionVariableLayout_GetTypeLayout(p)`:

1. **Skip unless
   `spReflectionTypeLayout_GetParameterCategory(tl) == SLANG_PARAMETER_CATEGORY_VARYING_INPUT`.**
   Spec E16: `SV_InstanceID`, `SV_VertexID`, `SV_IsFrontFace` and `SV_Position`
   all report `SLANG_PARAMETER_CATEGORY_NONE`, and the pre-revision walk would
   have emitted a phantom attribute at location 0 colliding with the real
   `POSITION`.
2. If `spReflectionTypeLayout_getKind(tl) == SLANG_TYPE_KIND_STRUCT`, recurse one
   level via `GetFieldCount`/`GetFieldByIndex` (`slang-deprecated.h:538-539`),
   applying rule 1 to each field and accumulating
   `Location = parentOffset + fieldOffset`, where each offset is
   `spReflectionVariableLayout_GetOffset(v, SLANG_PARAMETER_CATEGORY_VARYING_INPUT)`.
   Verified against SPIR-V `Location` decorations for a four-field `VSIn`
   (spec E16).
3. `Format` from the type: `SLANG_TYPE_KIND_VECTOR` → element count + scalar type;
   `SLANG_TYPE_KIND_SCALAR` → scalar type. **`SLANG_TYPE_KIND_MATRIX` throws**
   `NotSupportedException` naming the field and citing OPEN-6.
4. `Binding` and `Offset` stay at their defaults; the XML doc says the caller
   fills them (spec D5, unchanged).

### 3b.5 The remaining documented gap

Only one survives the revision. `DescriptorBinding.Stages` is now derivable
(§3b.2 step 5), so the pre-revision §3.3 first bullet is **deleted**. What stays:

- `VertexAttributes` returns values with `Binding` and `Offset` at their defaults.
  XML doc, verbatim intent: *a shader states its input locations and formats but
  not how the application packs its vertex buffers, so `Binding` and `Offset`
  must be filled by the caller and there is deliberately no
  `VertexInputDescription` factory here.*
- `PushConstantRange.Stages` is the program union even in `PerEntryPointUsage`
  mode. XML doc says why (spec E13).

No `VertexBindingDescription` is produced. Do not add one.

### 3b.6 The sparse-set contract (spec D10)

XML doc on `SetLayoutSlotCount`, verbatim intent: *a Slang program's descriptor
set indices need not be contiguous. `PipelineLayoutDescription.SetLayouts` is
positional, so allocate `SetLayoutSlotCount` entries and fill any index
`TryGetSet` returns `false` for with a single reusable empty
`DescriptorSetLayout` (a `DescriptorSetLayoutDescription` with no bindings).*
No API is added to `src/Ahjo.Vulkan/`; if a step appears to need one, stop.

### 3b.7 Tests — added to `tests/Ahjo.Vulkan.Slang.Tests`

1. `Reflection_ConstantBufferTextureSampler_ProducesBindings` — asserts
   `DescriptorSetCount == 1`, `SetIndex(0) == 0`, and bindings at slots 0/1/2
   with types `UNIFORM_BUFFER`, `SAMPLED_IMAGE`, `SAMPLER`, each `Count == 1`.
2. `Reflection_RWStructuredBuffer_MapsToStorageBuffer` — slot 3,
   `STORAGE_BUFFER`.
3. `Reflection_TextureArray_ProducesCount` — `Texture2D maps[4]` yields
   `Count == 4`.
4. `Reflection_PushConstant_ProducesRange` — one range, `Offset == 0`,
   `Size == 16` for a `float4` block; equal to what
   `PushConstantRange.For<Vector4>(stages)` produces.
5. `Reflection_PushConstant_IsNotAlsoADescriptorBinding` — the
   `PUSH_CONSTANT_BUFFER` range must not appear in `Bindings(0)`. Regression
   guard for §3b.2 step 1's category filter.
6. `Reflection_Stages_IsUnionOfEntryPointStages` — under
   `SlangStageAttribution.ProgramStageUnion`, a vert+frag program yields
   `Vertex | Fragment` on every binding.
7. `Reflection_VertexAttributes_LocationsAndFormats` — `float3 : POSITION` at
   location 0 → `VK_FORMAT_R32G32B32_SFLOAT`; `float2 : TEXCOORD0` at location 1
   → `VK_FORMAT_R32G32_SFLOAT`; both with `Binding == 0` and `Offset == 0` (the
   documented defaults).

The pre-revision case 8 (`Reflection_ParameterBlock_ThrowsNotSupported`) is
**deleted** — that behaviour no longer exists. Added by the revision:

8. `Reflection_TwoParameterBlocks_LandInSetsOneAndTwo` — global scope with a
   `ConstantBuffer`, a `Texture2D` and a `SamplerState`, plus `ParameterBlock<A>`
   and `ParameterBlock<B>`; assert `DescriptorSetCount == 3`,
   `SetIndex(0..2) == 0, 1, 2`, and each set's bindings. **This is the acceptance
   criterion for spec OPEN-2's resolution.**
9. `Reflection_BlockWithOrdinaryData_HasUniformBufferAtSlotZero` — a block whose
   element has `float4 factors; float roughness;` yields, in its set, a
   `UNIFORM_BUFFER` at `Slot 0` plus the declared resources at slots 1..n; a
   block with no ordinary data starts at slot 0 with its first resource.
   Spec E11 — the regression guard for the one binding Slang does not report.
10. `Reflection_NestedParameterBlock_AccumulatesSetIndex` —
    `ParameterBlock<Outer>` at set 3 containing `ParameterBlock<Inner>` yields a
    set 4 (spec E10).
11. `Reflection_NoGlobalDescriptors_FirstBlockIsSetZero` — a program whose global
    scope declares only `ParameterBlock`s puts the first at set **0**, not 1
    (spec E10). Guards against a hardcoded `+1`.
12. `Reflection_ExplicitVkBinding_ReportsSparseSets` — `[[vk::binding(3,0)]]` and
    `[[vk::binding(7,2)]]` yield `DescriptorSetCount == 2`, `SetIndex(0) == 0`,
    `SetIndex(1) == 2`, `SetLayoutSlotCount == 3`, and
    `TryGetSet(1, out _) == false` (spec D10).
13. `Reflection_ComposedProgram_DiffersFromPerModule` — reflect module `material`
    alone, then the three-module composite; assert at least one binding index
    differs. Spec E12 — the guard against anyone "optimizing" reflection back
    onto a single module.
14. `Reflection_PerEntryPointUsage_NarrowsStages` — the composed fixture: under
    `PerEntryPointUsage` the vertex-only binding reports `ShaderStages.Vertex`,
    the fragment-only bindings report `ShaderStages.Fragment`, and the binding
    both stages reach reports `Vertex | Fragment`; under `ProgramStageUnion` all
    three report `Vertex | Fragment`.
15. `Reflection_PushConstantStages_StayUnion_InBothModes` — asserts the E13
    caveat rather than leaving it to a comment.
16. `Reflection_SystemValueInputs_AreNotVertexAttributes` — a vertex entry point
    taking `VSIn vin, uint iid : SV_InstanceID, uint vid : SV_VertexID` yields
    attributes only for `vin`'s fields, at locations 0,1,2 — no attribute at a
    location claimed by a system value.
17. `Reflection_StructVertexInput_AccumulatesLocations` —
    `VSIn { float3 pos : POSITION; float2 uv : TEXCOORD0; float4 tangent : TANGENT; }`
    yields locations 0,1,2 with formats `R32G32B32_SFLOAT`, `R32G32_SFLOAT`,
    `R32G32B32A32_SFLOAT`.
18. `Reflection_MatrixVertexInput_ThrowsNotSupported` — OPEN-6's guard.
19. `Reflection_TwoPushConstantBlocks_ThrowsNotSupported` — OPEN-5's guard; two
    modules each with `[[vk::push_constant]]`.
20. `Reflection_ConformanceLinkedInterfaceBlock_ReportsUniformBufferOnly` — a
    conformance-linked `ParameterBlock<ISurface>` reports exactly one
    `UNIFORM_BUFFER` at slot 0 of its set, matching the SPIR-V (spec E14).
21. `Reflection_BuildsAWorkingPipelineLayout` — behind `TestGate.RequireDriver`,
    upgraded from the pre-revision case: build `SetLayoutSlotCount` descriptor
    set layouts, filling gaps with an empty one, feed them plus
    `PushConstantRanges` to `Device.CreatePipelineLayout`, assert a non-null
    handle. **This is the acceptance criterion "reflect into descriptions that
    build a working `PipelineLayout`", now over a composed multi-set program.**

### 3b.8 Drift-test additions

Extend `RequiredExports` in
`tests/Ahjo.Vulkan.Slang.Native.Tests/SlangExportDriftTests.cs` (§1.7) with the
names 3b calls that the seed list lacks. All 13 were verified present in
`libslang-compiler.so.0.2026.14.1`:

`spReflectionTypeLayout_getSubObjectRangeCount`,
`spReflectionTypeLayout_getSubObjectRangeBindingRangeIndex`,
`spReflectionTypeLayout_getSubObjectRangeOffset`,
`spReflectionTypeLayout_getBindingRangeType`,
`spReflectionTypeLayout_getBindingRangeCount`,
`spReflectionTypeLayout_getBindingRangeLeafTypeLayout`,
`spReflectionTypeLayout_GetFieldCount`,
`spReflectionTypeLayout_GetFieldByIndex`,
`spReflectionTypeLayout_GetCategoryCount`,
`spReflectionVariableLayout_GetSpace`,
`spReflection_FindTypeByName`,
`spReflection_getGlobalConstantBufferBinding`,
`spReflection_getGlobalConstantBufferSize`.

`spReflectionTypeLayout_getSubObjectRangeSpaceOffset` is deliberately **not**
added — 3b must not call it (spec E10).

Note for the implementer: `IMetadata::isParameterLocationUsed`,
`IComponentType::getEntryPointMetadata`,
`ISession::createTypeConformanceComponentType` and
`ISession::createCompositeComponentType` are **vtable** members, not flat exports,
so they cannot go in an export-name drift test. They are covered by §3a.2 case 4
and §3b.7 case 14 executing them.

### 3b.9 Docs

- `src/Ahjo.Vulkan.Slang/README.md`: a "reflection-driven layouts" section ending
  with (a) the sparse-set recipe from §3b.6, (b) the `Binding`/`Offset` gap, and
  (c) the two stage-attribution modes and what each costs.
- `docs/migration-vortice-to-ahjo.md`: only if it already documents a shader
  compilation story; if it does not, skip.

---

## OPEN items — stop and ask

- **OPEN-1** — whether `slang-glslang` ships. Decision procedure: Phase 2 step
  2.5 test 8. Cost if yes: `slang-glslang.dll` +2 411 328 bytes compressed
  (win-x64), `libslang-glslang-2026.14.1.so` +3 632 358 bytes compressed
  (linux-x64), on top of a ~24.6 MB compressed baseline. The Linux file name is
  version-embedded and is `dlopen`ed by that exact name, so it must ship
  unrenamed. Do not add it, and do not silently cap the optimization enum,
  without a human decision.
- **OPEN-2** — **RESOLVED 2026-08-01, nothing to ask.** The set index is
  `spReflectionVariableLayout_GetOffset(param, SLANG_PARAMETER_CATEGORY_SUB_ELEMENT_REGISTER_SPACE)`,
  accumulated down the nesting chain from a global-scope base of 0; verified
  against SPIR-V `OpDecorate DescriptorSet` for two-block, nested-block,
  no-global-descriptor and explicit-`[[vk::binding]]` fixtures (spec E10).
  `getSubObjectRangeSpaceOffset` was the wrong function. Implemented in §3b.2;
  §3b.7 case 8 is the acceptance test. The old §3.4 guard is deleted — do not
  reintroduce a `NotSupportedException` on `ParameterBlock<T>`.
- **OPEN-3** — `SlangCompiler` lifetime and `slang_shutdown()`. Phase 2 releases
  the global session and does not call `slang_shutdown`; step 2.5 test 9 is the
  probe. If it fails, stop.
- **OPEN-4** — the `specialize()` segfault. `IComponentType::specialize` on a
  component whose global scope holds an interface-typed `ParameterBlock`,
  followed by `getTargetCode` or by `getEntryPointCode` for the consuming entry
  point, crashes inside Slang's type-legalization pass (spec E14, with a stack
  trace; reproduced 3/3 on `v2026.14.1` linux-x64). Phase 3a therefore exposes
  `AddTypeConformance` and **no** `Specialize` (§3a.1 rule 5). Two things need a
  human call before anyone adds `Specialize`: whether to file the repro upstream
  and block on it, and whether the crash reproduces on `win-x64` — it was
  measured on Linux only. **Do not add a `Specialize` method to close a gap.**
- **OPEN-5** — more than one push-constant block. Two modules each declaring
  `[[vk::push_constant]]` compose and link, and reflection reports two
  `PUSH_CONSTANT` ranges, but the only offset it exposes is a buffer *index*
  (0, 1), not the byte offset `VkPushConstantRange.offset` needs (spec E17).
  §3b.2 step 4 throws naming both parameters. Do not guess a byte offset; if a
  consumer needs two blocks, stop and report.
- **OPEN-6** — `MATRIX`-kind vertex inputs. A `float4x4` input occupies
  `GetSize(typeLayout, VARYING_INPUT)` = 4 consecutive locations and SPIR-V
  decorates it at the base location, but the per-location scalar count depends on
  `SessionDesc.defaultMatrixLayoutMode` and only column-major was probed
  (spec E16). §3b.4 rule 3 throws naming the field; §3b.7 case 18 is the guard.
  Settling it means probing both layout modes against the SPIR-V type of each
  per-location input variable — a separate investigation, not an improvisation.
