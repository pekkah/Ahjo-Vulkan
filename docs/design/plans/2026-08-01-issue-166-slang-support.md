Paired with [../specs/2026-08-01-issue-166-slang-support-design.md](../specs/2026-08-01-issue-166-slang-support-design.md).

# Implementation plan — issue #166, Slang support

Three phases. **Phase 1 and Phase 2 each ship independently**; Phase 3 requires
Phase 2. Do not start a phase before the previous one is merged and its lane is
green.

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
    // SlangReflection Reflection { get; }  <- Phase 3
}
```

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

---

## Phase 3 — reflection → the existing description types

### 3.1 `src/Ahjo.Vulkan.Slang/SlangReflection.cs`

```csharp
public sealed class SlangReflection
{
    public int DescriptorSetCount { get; }
    public ReadOnlySpan<DescriptorBinding>          DescriptorSet(int setIndex);
    public ReadOnlySpan<PushConstantRange>          PushConstantRanges { get; }
    public ReadOnlySpan<VertexAttributeDescription> VertexAttributes(int entryPointIndex);
}
```

Populated eagerly into arrays in the constructor (setup-time; the spans are
views over those arrays). `SlangProgram.Reflection` lazily constructs one from
`linked->getLayout(0, &diagnostics)`, throwing `SlangCompilationException` on a
null layout with the diagnostics text.

Walk, verified against `v2026.14.1`:

1. `globals = spReflection_getGlobalParamsTypeLayout(layout)`.
2. For each `s` in `0..spReflectionTypeLayout_getDescriptorSetCount(globals)`:
   for each `r` in `0..…getDescriptorSetDescriptorRangeCount(globals, s)`:
   - `category = …getDescriptorSetDescriptorRangeCategory(globals, s, r)`
   - `PUSH_CONSTANT_BUFFER` → a `PushConstantRange`; everything else → a
     `DescriptorBinding` with
     `Slot = (uint)…getDescriptorSetDescriptorRangeIndexOffset(globals, s, r)`,
     `Count = (uint)…getDescriptorSetDescriptorRangeDescriptorCount(globals, s, r)`,
     `Type = MapBindingType(…getDescriptorSetDescriptorRangeType(globals, s, r))`,
     `Stages = <program stage union>`.
3. Push-constant size: locate the `PUSH_CONSTANT_BUFFER` parameter via
   `spReflection_GetParameterByIndex` + `spReflectionTypeLayout_GetParameterCategory`,
   then
   `spReflectionTypeLayout_GetSize(spReflectionTypeLayout_GetElementTypeLayout(tl), UNIFORM)`.
   Verified: a `struct { float4 tint; }` block yields size 16, alignment 16.
   Emit `new PushConstantRange { Stages = <union>, Offset = 0, Size = (uint)size }`.
4. Vertex attributes: for entry point `e`, for each
   `spReflectionEntryPoint_getParameterByIndex`, take
   `Location = (uint)spReflectionVariableLayout_GetOffset(p, VARYING_INPUT)` and
   derive `Format` from the parameter's type layout
   (`spReflectionTypeLayout_GetType` → `spReflectionType_GetKind` /
   `GetElementCount` / `GetElementType` / `GetScalarType`). Struct-typed
   varying parameters (a fragment stage taking the vertex output struct) are
   skipped — they are not vertex-buffer inputs.

### 3.2 `MapBindingType`

`internal static VkDescriptorType MapBindingType(SlangBindingType)` — a total
switch, mutable flag masked off with `SLANG_BINDING_TYPE_BASE_MASK` and
re-applied where it changes the Vulkan type:

| `SlangBindingType` | `VkDescriptorType` |
|---|---|
| `SAMPLER` | `SAMPLER` |
| `TEXTURE` | `SAMPLED_IMAGE` |
| `TEXTURE \| MUTABLE_FLAG` | `STORAGE_IMAGE` |
| `CONSTANT_BUFFER`, `PARAMETER_BLOCK` | `UNIFORM_BUFFER` |
| `TYPED_BUFFER` | `UNIFORM_TEXEL_BUFFER` |
| `TYPED_BUFFER \| MUTABLE_FLAG` | `STORAGE_TEXEL_BUFFER` |
| `RAW_BUFFER`, `RAW_BUFFER \| MUTABLE_FLAG` | `STORAGE_BUFFER` |
| `COMBINED_TEXTURE_SAMPLER` | `COMBINED_IMAGE_SAMPLER` |
| `INPUT_RENDER_TARGET` | `INPUT_ATTACHMENT` |
| `INLINE_UNIFORM_DATA` | `INLINE_UNIFORM_BLOCK` |
| `RAY_TRACING_ACCELERATION_STRUCTURE` | `ACCELERATION_STRUCTURE_KHR` |
| anything else | `throw new NotSupportedException($"Slang binding type {t} has no VkDescriptorType mapping.")` |

Never fall through to a default `VkDescriptorType` — a wrong descriptor type is
a validation error the caller cannot diagnose.

### 3.3 The two documented gaps

- `DescriptorBinding.Stages` is set to the union of the stages of the program's
  entry points. XML doc on `DescriptorSet(int)`, verbatim intent: *Slang
  reflection does not attribute a global parameter to a stage
  (`spReflectionVariableLayout_getStage` returns `SLANG_STAGE_NONE` for global
  descriptor parameters), so this is the union of the compiled program's entry
  point stages — always valid, sometimes broader than necessary. Narrow it with
  `binding with { Stages = … }`.*
- `VertexAttributes` returns values with `Binding` and `Offset` at their
  defaults. XML doc, verbatim intent: *a shader states its input locations and
  formats but not how the application packs its vertex buffers, so `Binding` and
  `Offset` must be filled by the caller and there is deliberately no
  `VertexInputDescription` factory here.*

No `VertexBindingDescription` is produced. Do not add one.

### 3.4 Multi-set guard (OPEN-2)

When the global walk encounters a parameter whose category is
`SLANG_PARAMETER_CATEGORY_SUB_ELEMENT_REGISTER_SPACE` (that is, a
`ParameterBlock<T>`), throw
`NotSupportedException($"Slang parameter '{name}' uses a ParameterBlock; multi-descriptor-set reflection is not implemented (issue #166, OPEN-2).")`.
Do not guess the set index. Raise the follow-up issue rather than improvising.

### 3.5 Tests — added to `tests/Ahjo.Vulkan.Slang.Tests`

1. `Reflection_ConstantBufferTextureSampler_ProducesBindings` — asserts
   `DescriptorSetCount == 1` and bindings at slots 0/1/2 with types
   `UNIFORM_BUFFER`, `SAMPLED_IMAGE`, `SAMPLER`, each `Count == 1`.
2. `Reflection_RWStructuredBuffer_MapsToStorageBuffer` — slot 3,
   `STORAGE_BUFFER`.
3. `Reflection_TextureArray_ProducesCount` — `Texture2D maps[4]` yields
   `Count == 4`. (Verified: the descriptor-range count carries the array size.)
4. `Reflection_PushConstant_ProducesRange` — one range,
   `Offset == 0`, `Size == 16` for a `float4` block; equal to what
   `PushConstantRange.For<Vector4>(stages)` produces.
5. `Reflection_PushConstant_IsNotAlsoADescriptorBinding` — the
   `PUSH_CONSTANT_BUFFER` range must not appear in `DescriptorSet(0)`. This is
   the regression guard for the filter in 3.1 step 2.
6. `Reflection_Stages_IsUnionOfEntryPointStages` — a vert+frag program yields
   `Vertex | Fragment` on every binding, and the XML-documented behaviour is
   asserted rather than assumed.
7. `Reflection_VertexAttributes_LocationsAndFormats` — `float3 : POSITION` at
   location 0 → `VK_FORMAT_R32G32B32_SFLOAT`; `float2 : TEXCOORD0` at location 1
   → `VK_FORMAT_R32G32_SFLOAT`; both with `Binding == 0` and `Offset == 0`
   (the documented defaults).
8. `Reflection_ParameterBlock_ThrowsNotSupported` — the OPEN-2 guard.
9. `Reflection_BuildsAWorkingPipelineLayout` — behind `TestGate.RequireDriver`:
   feed `DescriptorSet(0)` into `DescriptorSetLayoutDescription.Bindings`, call
   `Device.CreateDescriptorSetLayout`, then `Device.CreatePipelineLayout` with
   `PushConstantRanges`, and assert a non-null handle. **This is the acceptance
   criterion "reflect into descriptions that build a working PipelineLayout".**

### 3.6 Docs

- `src/Ahjo.Vulkan.Slang/README.md`: a short "reflection-driven layouts" section
  ending with the two gaps stated plainly, so a consumer meets them in the
  README rather than in a validation error.
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
- **OPEN-2** — multi-descriptor-set (`ParameterBlock<T>`) reflection. Phase 3
  implements space 0 and throws on the rest (step 3.4). The recursion
  `getSubObjectRangeBindingRangeIndex` → `getBindingRangeLeafTypeLayout` →
  `GetElementTypeLayout` → `getDescriptorSetCount` was verified to reach the
  block's bindings, but `getSubObjectRangeSpaceOffset` returned `0` while
  `spReflectionParameter_GetBindingIndex` on the block returned `1`, so the set
  index derivation is unsettled. File a follow-up issue; do not guess.
- **OPEN-3** — `SlangCompiler` lifetime and `slang_shutdown()`. Phase 2 releases
  the global session and does not call `slang_shutdown`; step 2.5 test 9 is the
  probe. If it fails, stop.
