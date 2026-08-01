# Slang support: pinned shader compiler + reflection-driven pipeline layouts

**Issue:** [#166](https://github.com/pekkah/Ahjo-Vulkan/issues/166) — *Slang support: pinned shader compiler + reflection-driven pipeline layouts*
**Prevents the structural cause of:** [#162](https://github.com/pekkah/Ahjo-Vulkan/issues/162) (`glslc` absent from the runner; the toolchain is an unpinned `PATH` assumption)
**Lands consistently with:** [#119](https://github.com/pekkah/Ahjo-Vulkan/issues/119) (valid-by-default descriptions — reflection must *produce* those types, not fork them), [#32](https://github.com/pekkah/Ahjo-Vulkan/issues/32) / `.github/CLAUDE.md` (no RID ships without a lane that has executed it)
**Date:** 2026-08-01

---

## Problem

Two defects, both downstream of "the shader compiler is whatever is on `PATH`".

**1. The shader toolchain is the only unpinned native dependency in the repo.**
Every other native input is version-pinned in `Directory.Build.props`
(`VulkanHeadersVersion` :18, `VmaVersion` :24, `KtxVersion` :28) and built or
staged from that pin. The shader compiler is not. Seven projects carry an
identical copy of the same fallback —
`samples/HelloCube/HelloCube.csproj:30-31`,
`samples/HeadlessTriangle/HeadlessTriangle.csproj:26-27`,
`samples/HelloVma/HelloVma.csproj:26-27`,
`samples/HelloVmaWindowed/`, `samples/HelloTriangle/`,
`samples/AotSmoke/AotSmoke.csproj:42-43`,
`tests/Ahjo.Vulkan.Tests/Ahjo.Vulkan.Tests.csproj:53-54`,
`tests/Ahjo.Vulkan.Benchmarks/Ahjo.Vulkan.Benchmarks.csproj:33-34` — and each
invokes it under `ContinueOnError="WarnAndContinue"`
(`samples/AotSmoke/AotSmoke.csproj:50`), so an absent compiler degrades to a
warning and a missing `.spv`. `ci.yml:212` still prints
`"spirv" { "NOT PROVEN — glslc absent (follow-up)" }`: the repo currently
reports, in its own coverage summary, that a whole gate class is unproven
because of this.

**2. Layout information is duplicated by hand and nothing reconciles the two
copies.** A shader declares its descriptor sets, bindings, push-constant ranges
and vertex inputs; a consumer then restates all of it in C# as
`DescriptorBinding` (`src/Ahjo.Vulkan/Pipelines/DescriptorBinding.cs:22-35`),
`DescriptorSetLayoutDescription`
(`src/Ahjo.Vulkan/Pipelines/DescriptorSetLayoutDescription.cs:9-29`),
`PushConstantRange` (`src/Ahjo.Vulkan/Pipelines/PushConstantRange.cs:19-41`)
and `VertexInputDescription`
(`src/Ahjo.Vulkan/Pipelines/VertexInputDescription.cs:8-12`). A slot typo is a
validation-layer error at draw time at best. This is the class of bug the repo
invests heavily in preventing elsewhere (#119's valid-by-default descriptions,
the `vulkan-validation-reviewer` agent); reflection closes it at the source.

**Allocation posture, stated up front so a reviewer does not flag it:**
compilation and reflection are **setup-time**. Invariant #3 (zero per-frame
allocations on `Recording/`, `Sync/`, `Pools/`, `Memory/`) **does not apply** to
anything in this design — nothing here is reachable from a frame loop, and no
benchmark in `tests/Ahjo.Vulkan.Benchmarks/` covers or should cover it.
Invariants #1 (UTF-8 `const char*`), #2 (Native AOT), #4 (generated code stays
generated) and #5 (`TreatWarningsAsErrors`) all apply in full.

---

## Evidence

All Slang line numbers are against `include/slang.h` and
`include/slang-deprecated.h` at tag `v2026.14.1`. All binding, size, symbol and
runtime facts below were produced by running the tools, not read off the header.

### E1. ClangSharp generates a working, compiling binding over `slang.h`

`ClangSharpPInvokeGenerator 21.1.8.3` (`.config/dotnet-tools.json:5`) was run
against `slang.h` with `--language c++` — the mode
`tools/generate-vma.rsp:12-13` already uses. Result: **138 files, 6 833 lines,
0 errors, 0 warnings** compiling under `net10.0` / `LangVersion 14.0` /
`AllowUnsafeBlocks`, after five configuration fixes (E4). For comparison, the
whole `Ahjo.Vulkan.Vma.Native` generated tree exists on the same basis.

The generated dispatch is exactly the AOT-clean shape the issue hoped for:

```csharp
[VtblIndex(4)]
public ShaderReflection* getLayout(long targetIndex = 0, ISlangBlob** outDiagnostics = null)
    => ((delegate* unmanaged[MemberFunction]<IComponentType*, long, ISlangBlob**, ShaderReflection*>)(lpVtbl[4]))(
           (IComponentType*)Unsafe.AsPointer(ref this), targetIndex, outDiagnostics);
```

No `ComWrappers`, no `[ComImport]`, no `Marshal`, no reflection. The
`[VtblIndex]`/`[NativeTypeName]` attributes ClangSharp emits are
`[Conditional("DEBUG")]` (see the existing
`src/Ahjo.Vulkan.Ktx.Native/Generated/NativeTypeNameAttribute.cs` shape). C++
default arguments survive into the C# helper (`targetIndex = 0`).

### E2. The generated binding was executed end-to-end against the shipped binary

A scratch console app over the generated files, with only
`libslang-compiler.so.0.2026.14.1` renamed to `libslang.so` in its output
directory, ran on Linux x64:

```
createGlobalSession = 0x00000000
buildTag = 2026.14.1
createSession = 0x00000000
loadModuleFromSourceString -> module != null
findAndCheckEntryPoint(vertexMain, SLANG_STAGE_VERTEX)   = 0x00000000
findAndCheckEntryPoint(fragmentMain, SLANG_STAGE_FRAGMENT) = 0x00000000
createCompositeComponentType(3 components) = 0x00000000
link  = 0x00000000
getEntryPointCode = 0x00000000
SPIR-V bytes=1192 magic=0x07230203 version=0x00010500
```

So: vtable dispatch through `delegate* unmanaged[MemberFunction]` works under
the Itanium ABI; the compile path works with **one** native file present; and
the emitted blob is a well-formed SPIR-V 1.5 module ready for
`Device.CreateShaderModule(ReadOnlySpan<uint>)`
(`src/Ahjo.Vulkan/Memory/SpirvBlob.cs:48-55` is the existing ingress and takes
the same `ReadOnlySpan<uint>` shape).

### E3. Reflection was walked through the flat `spReflection_*` exports

Same probe, continued against the linked component's `getLayout(0, …)`:

```
program params = 6, entryPoints = 2
descriptor sets = 1
  set[0] space=0 ranges=5
    range[0] SLANG_BINDING_TYPE_CONSTANT_BUFFER      cat=DESCRIPTOR_TABLE_SLOT  indexOffset=0 count=1
    range[1] SLANG_BINDING_TYPE_TEXTURE             cat=DESCRIPTOR_TABLE_SLOT  indexOffset=1 count=1
    range[2] SLANG_BINDING_TYPE_SAMPLER             cat=DESCRIPTOR_TABLE_SLOT  indexOffset=2 count=1
    range[3] SLANG_BINDING_TYPE_MUTABLE_RAW_BUFFER  cat=DESCRIPTOR_TABLE_SLOT  indexOffset=3 count=1
    range[4] SLANG_BINDING_TYPE_PUSH_CONSTANT       cat=PUSH_CONSTANT_BUFFER   indexOffset=0 count=1
PUSH gPush elemUniformSize=16 align=16
entryPoint[0] vertexMain stage=SLANG_STAGE_VERTEX params=2
   in[0] position sem=POSITION0 loc=0 kind=VECTOR elems=3 scalar=FLOAT32 stage=SLANG_STAGE_VERTEX
   in[1] uv       sem=TEXCOORD0 loc=1 kind=VECTOR elems=2 scalar=FLOAT32 stage=SLANG_STAGE_VERTEX
entryPoint[1] fragmentMain stage=SLANG_STAGE_PIXEL params=1
```

Four things fall out, all load-bearing for §Decision:

- The descriptor-range walk over
  `spReflection_getGlobalParamsTypeLayout` →
  `spReflectionTypeLayout_getDescriptorSet{Count,SpaceOffset,DescriptorRangeCount,DescriptorRangeIndexOffset,DescriptorRangeDescriptorCount,DescriptorRangeType}`
  (`slang-deprecated.h:632-680` in generated form) yields everything
  `DescriptorBinding.{Slot, Type, Count}` needs.
- **Push constants arrive as a descriptor range**, type
  `SLANG_BINDING_TYPE_PUSH_CONSTANT`, category `PUSH_CONSTANT_BUFFER`. They must
  be filtered out of the `DescriptorBinding` stream and routed to
  `PushConstantRange`; the byte size comes from
  `spReflectionTypeLayout_GetSize(GetElementTypeLayout(tl), UNIFORM)` = 16 for a
  `float4` block, matching what `PushConstantRange.For<T>` would compute
  (`PushConstantRange.cs:33-40`).
- `spReflectionVariableLayout_getStage` returns **`SLANG_STAGE_NONE` for every
  global descriptor parameter** (all five, above). It only returns a real stage
  for entry-point *varying* parameters. `spReflection_ToJson` confirms the same
  from the other side: each entry point's `bindings` array lists the entire
  global scope (`vertexMain -> [gXform, gAlbedo, gSampler, gPush]`,
  `fragmentMain -> [gXform, gAlbedo, gSampler, gPush]`), never a narrowed
  per-stage subset. **`DescriptorBinding.Stages` is not derivable from Slang
  reflection.** This is a finding about the mapping, not a defect in
  `DescriptorBinding`.
- Vertex inputs give `Location` (the `VARYING_INPUT` offset), the semantic name
  and index, and enough type information (`kind=VECTOR elems=3
  scalar=FLOAT32` → `VK_FORMAT_R32G32B32_SFLOAT`) to derive
  `VertexAttributeDescription.Format`. They give **nothing** for
  `VertexAttributeDescription.Binding` / `.Offset`
  (`VertexAttributeDescription.cs:11-14`) or for
  `VertexBindingDescription.{Slot, Stride, InputRate}`
  (`VertexBindingDescription.cs:11-13`) — those describe how the *application*
  packs its vertex buffers, which the shader does not state.

### E4. Raw ClangSharp output over `slang.h` does **not** compile; five fixes, each verified

Each was reproduced and then verified fixed:

| Symptom | Cause | Fix (goes in the `.rsp`) |
|---|---|---|
| `CS0616: 'NativeTypeNameAttribute' is not an attribute class` ×2032 | `struct Attribute` (`slang.h:2402`) shadows `System.Attribute` inside the generated namespace | `--remap Attribute=SlangUserAttributeInfo` |
| `CS0266: cannot convert uint to int` ×29 | nested `enum class Kind` (`slang.h:2446`, `slang.h:3857`) emitted without an underlying type while its members come from `uint`-backed `SlangTypeKind` / `SlangDeclKind` | `--with-type Kind=uint` |
| `CS0117: CallingConvention does not contain MemberFunction` | `slang::VariableReflection::getDefaultValueBlob` (`slang.h:3228`) is a **C++ member function exported under a mangled name**; ClangSharp emits `[DllImport(…, CallingConvention.MemberFunction, EntryPoint="_ZN5slang18VariableReflection19getDefaultValueBlobEPP10ISlangBlob")]` | `--exclude getDefaultValueBlob` |
| `CS1503: [Cdecl] vs [MemberFunction]` ×5 | ClangSharp propagates the enclosing member-function callconv onto nested C-callback parameter types (`FileSystemContentsCallBack` `slang.h:1679`, `SlangDiagnosticCallback` `slang.h:1992`, `slang::VMExtFunction` `slang.h:5947`, `slang::VMPrintFunc` `slang.h:5948`) | `--remap FileSystemContentsCallBack=nint SlangDiagnosticCallback=nint slang::VMExtFunction=nint slang::VMPrintFunc=nint` — namespace qualification is **required** for the two `slang::` ones; the bare names are silently ignored |
| flat `spReflection_*` exports missing entirely; `ShaderReflection`/`TypeLayoutReflection` emitted as empty structs | `slang.h:2379` `#include`s `slang-deprecated.h`, but ClangSharp only emits declarations from files named in `--file`/`--traverse` | `--traverse slang.h slang-deprecated.h slang-image-format-defs.h` |

**A sixth finding is a trap rather than a fix.** `--exclude <memberName>` on a
virtual member **deletes the vtable slot and shifts every later index**.
Verified: with `--exclude enumeratePathContents`, `ISlangFileSystemExt.getOSPathKind`
moved from vtbl index 11 to index 10 while `getOSPathKind()`'s body still called
`lpVtbl[10]` — a silent wrong-function call at runtime. **No `.rsp` for Slang may
ever `--exclude` a virtual member name.** This is why the callback fixes above
are remaps, not excludes.

### E5. Exactly one bound symbol does not exist in the shipped binary

`nm -D --defined-only libslang-compiler.so.0.2026.14.1` yields 258 `sp*`
symbols; the generated `SlangApi` class declares 259 `sp*` `DllImport`s. The
difference is exactly one:

```
bound but NOT exported: spReflection_GetSession
```

Cause: `spReflection_GetSession` is declared at `slang-deprecated.h:1070`,
**outside** the file's `extern "C"` block (which closes at `:1066`), so it has
C++ linkage. It *is* in the binary — as
`_Z23spReflection_GetSessionP18SlangProgramLayout` — and ClangSharp bakes that
Itanium-mangled name into `EntryPoint=` because we parse against a Linux
target. On Windows the same declaration mangles differently, so the binding
would resolve on Linux and throw `EntryPointNotFoundException` on Windows. A
sweep for every mangled-name `DllImport` in the generated tree found exactly
two (`spReflection_GetSession`, `slang_getEmbeddedCoreModule` at `slang.h:5852`)
plus the already-excluded `getDefaultValueBlob`. All three must be excluded, on
the same reasoning `tools/generate-ktx.rsp:66-68` uses for `ktxLoadOpenGL`: *a
binding whose only possible outcome is `EntryPointNotFoundException` is a trap,
not a feature.*

### E6. What the 77 MB release archive actually contains, and what is required

`slang-2026.14.1-linux-x86_64.tar.gz`, `Content-Length: 77478212`,
`sha256 21f2d7847385a770e569fb61b1507a7794d742d97850bce0432bff0032ca005f`.
Extracted contents by size:

| File | Raw | Deflate | Required for compile→SPIR-V? |
|---|---|---|---|
| `lib/libslang-llvm.so` | 152 111 720 | — | **No.** CPU/host-callable targets only. |
| `lib/libslang-compiler.so.0.2026.14.1` (`libslang.so` → symlink) | 30 598 248 | 13 101 543 | **Yes.** This is the compiler. |
| `lib/libslang-glslang-2026.14.1.so` | 10 055 776 | 3 632 358 | Provides `spirv-opt`; see below. |
| `lib/libslang-glsl-module-2026.14.1.so` | 1 334 808 | — | No. GLSL *input* only. |
| `lib/libgfx.so`, `lib/libslang-rt.so` | 1 069 288 / 320 096 | — | No. Slang's own gfx layer and CPU runtime. |
| `lib/slang-standard-module-*/` (incl. `neural.slang-module`, 5.5 MB) | ~6 MB | — | No. The core module is embedded — E2 compiled with none present. |
| `bin/`, `share/doc/` (~4 MB of markdown) | — | — | No. |

Windows (`slang-2026.14.1-windows-x86_64.zip`,
`sha256 5ed0a59d650a0af0aca45d5db4e083b3d8fb5cea05748747dd95dfbe9c580658`) has a
different shape: `bin/slang.dll` is **158 720 bytes**, imports only
`KERNEL32.dll`, and exports the 173 `spReflection*` names — a forwarder that
loads `bin/slang-compiler.dll` (25 334 784) at runtime. Both files are required
on Windows; on Linux `libslang.so` is a *symlink* to the compiler, and a nupkg
cannot carry symlinks (`Ahjo.Vulkan.Ktx.Native.csproj:55-61` documents that
exact constraint for libktx's SOVERSION chain). Verified: a **plain rename** of
`libslang-compiler.so.0.2026.14.1` to `libslang.so` loads and runs (E2 was
re-run against a renamed copy, not a symlink).

**On `slang-glslang`, the evidence is mixed and is reported as such.**
`slangc test.slang -target spirv` fails without it —
`error[E00100]: failed to load downstream compiler 'spirv-opt'` /
`note[E99996]: failed to load dynamic library 'slang-glslang-2026.14.1'` — and
succeeds with `-O0` (2 008-byte blob) or once the library is present
(1 704-byte blob). The **API path this design uses** (`getEntryPointCode` on a
linked composite) produced valid SPIR-V with only `libslang.so` present, both at
default target flags and with an explicit `Optimization` target option — it did
not demand `spirv-opt` in any configuration probed. That is not the same as
proving no configuration demands it. Cost if we ship it anyway: **+3 632 358
bytes (linux) / +2 411 328 bytes (win) compressed**, against a ~24.6 MB
compressed baseline for the core-only subset.

### E7. Direct SPIR-V emission is Slang's own default

`SLANG_TARGET_FLAG_GENERATE_SPIRV_DIRECTLY = 1 << 10` and
`kDefaultTargetFlags = SLANG_TARGET_FLAG_GENERATE_SPIRV_DIRECTLY` (`slang.h`,
reproduced verbatim in generated `SlangApi.cs`). The GLSL round-trip through
`slang-glslang` is opt-in, not the path we would be on.

### E8. The repo already has both patterns this needs

- **Standalone native package, no `Ahjo.Vulkan.Native` reference.**
  `Ahjo.Vulkan.Ktx.Native.csproj:10-21` states the rule explicitly: libktx is
  built with no Vulkan dependency, so the project does not reference
  `Ahjo.Vulkan.Native`. Slang is the same shape — it produces bytes, it does not
  touch a `VkDevice`. Contrast `Ahjo.Vulkan.Vma.Native`, whose
  `tools/generate-vma.rsp:40-70` remaps 30 Vulkan types because VMA *is* a
  Vulkan consumer.
- **Host-RID staging + multi-RID pack.** `Ahjo.Vulkan.Ktx.Native.csproj:79-85`
  (Content+Link propagation through `ProjectReference`),
  `:154-192` (host build + stage), `:203-219` (`PackKtxRuntimes` →
  `runtimes/<rid>/native/`). `build-ktx-native.yml:36-112` is the lane shape:
  build + **test in the same job** before uploading the artifact, because #144
  shipped a `.so` that SIGSEGVed on its first call.
- **Runtime-variable UTF-8 names.** The wrapper does not only use
  `Utf8Name.FromLiteral`. `GraphicsPipelineBuilder.CopyName`
  (`GraphicsPipelineBuilder.cs:213-220`) copies a caller-supplied
  `ReadOnlySpan<byte>` into an inline fixed-size buffer and null-terminates by
  clearing first; `InitMain` (`:140-147`) writes `"main\0"` by hand. That is the
  established pattern for a name that is not a compile-time literal, and
  `Utf8Name`'s doc comment (`Lifecycle/Utf8Name.cs:29-33`) is explicit that
  `FromLiteral` is only for `"…"u8`.

### E9. Naming hazard

`--methodClassName Slang` would put a type `Slang` in namespace
`Ahjo.Vulkan.Slang.Native`. Code inside namespace `Ahjo.Vulkan.Slang` resolving
the identifier `Slang` walks outward and binds the **namespace**
`Ahjo.Vulkan.Slang` before it ever considers an imported type, so
`Slang.slang_createGlobalSession(…)` would not compile. The three existing
packages (`Vk`, `Vma`, `Ktx`) never hit this because no namespace shares their
method-class name.

---

## Decision

### D1. Package layout — confirm the libktx pattern, two new projects

```
src/Ahjo.Vulkan.Slang.Native/   ClangSharp bindings + pinned native binaries  (publishable)
src/Ahjo.Vulkan.Slang/          compile + reflect -> existing description types (publishable)
```

`Ahjo.Vulkan.Slang.Native` references **nothing** — not `Ahjo.Vulkan.Native`
(Slang has no Vulkan surface at all; E8) and not `Ahjo.Vulkan`.
`Ahjo.Vulkan.Slang` references `Ahjo.Vulkan` + `Ahjo.Vulkan.Slang.Native`.
`Ahjo.Vulkan` itself gains no dependency, so a consumer shipping precompiled
SPIR-V never pulls ~25 MB of compiler. This raises the repo to six projects
and six published packages under the single `v*` tag (`README.md:124`).

*Why not the alternatives.*
**A hard `ProjectReference` from `Ahjo.Vulkan` (the VMA pattern)** — rejected:
VMA earns that because every allocation goes through it; a shader compiler is
used at asset-build time by some consumers and never by others.
**One combined `Ahjo.Vulkan.Slang` package with the bindings internal** —
rejected: it breaks the repo's own separation (raw bindings are a published,
separately versioned artifact in all three existing cases) and would hide the
binding surface from `Ahjo.Vulkan.Slang.Native.Tests`, which is where the
drift test has to live.

### D2. Binding strategy — ClangSharp, `implicit-vtbls` + `generate-callconv-member-function`

ClangSharp, driven by a new `tools/generate-slang.rsp`, with the six
configuration decisions of E4/E5 and `--methodClassName SlangApi` (E9). The
exact flag set is in the plan; the load-bearing choices are
`--language c++`, `--traverse` over `slang.h` **and** `slang-deprecated.h`,
`implicit-vtbls`, `generate-vtbl-index-attribute`,
`generate-callconv-member-function`, and the two mangled-symbol exclusions.

`generate-callconv-member-function` (emitting
`delegate* unmanaged[MemberFunction]`) is chosen over the default
`[Thiscall]`: `CallConvMemberFunction` is the CLR's declared modelling of a C++
instance method, it is what TerraFX's ClangSharp-generated COM bindings use, and
it is what made the callback parameter types agree (E4 row 4). On x64 — the only
architecture either shipping RID has — `SLANG_MCALL` expands to `__stdcall` on
Windows (`slang.h:218-226`) and to nothing elsewhere, and x64 has exactly one
calling convention, so the Linux-targeted parse is ABI-identical on both RIDs.
This is the same argument `tools/generate-ktx.rsp:30-38` already records.

`implicit-vtbls` (`void** lpVtbl` + an inline cast per call) over
`explicit-vtbls` (a named `Vtbl` struct of function-pointer fields, the shape of
`src/Ahjo.Vulkan.Ktx.Native/Generated/ktxTexture_vtbl.cs`): both compile and
both are AOT-clean, but with implicit vtbls the slot index is a literal in the
call expression annotated by `[VtblIndex(n)]`, so a `git diff` of a regen shows
index changes directly. With explicit vtbls the index is positional in a struct
and a reordered upstream method is an invisible field reorder. Given E4's
demonstration that slot drift is the failure mode with teeth here, the shape
that makes the index reviewable wins.

*Why not the alternatives.*
**Hand-written bindings** — rejected on measurement. The premise ("~10 free
functions plus a handful of vtables") does not survive contact: reaching
`IComponentType::getLayout` means declaring every preceding slot of
`IGlobalSession` (30 virtuals), `ISession` (24), `IComponentType` (19) and
`IModule` (12) in exact upstream order, and reflection is 173 flat exports, not
a handful. That is ~260 hand-transcribed signatures whose only validation is
"nobody miscounted", against an `.rsp` that a regen re-derives from the pinned
header. E4's slot-shift experiment is precisely the bug class a hand count
produces, and it produces it silently.
**`ComWrappers` / `[ComImport]`** — rejected: violates invariant #2 outright
and Slang explicitly does not require COM (`slang.h:1471-1474`).
**Binding only `slang-deprecated.h` and skipping the vtables** — rejected:
`IComponentType::getLayout` (`slang.h:5331`) is the only way to obtain a
`ProgramLayout*` from a linked program, and it is a virtual.
**Using Slang's C++ header-only reflection shim via C++/CLI or a shim DLL** —
rejected: adds a hand-written native translation unit to build and ship per RID,
which is strictly more surface than calling the same exports directly, and the
repo already builds exactly one such TU (VMA's) only because VMA is header-only.

**Recorded as a deliberate, eyes-open dependency:** the entire reflection
surface lives in a header whose banner says "New code should not use any of
these declarations, and the Slang API will drop these declarations over time"
(`slang-deprecated.h:5-11`). There is no alternative — Slang's own recommended
C++ API is a header-only shim that calls exactly these symbols
(`slang.h:2732`, `:3309`, `:3655`). Mitigation is a drift test (D6), not
avoidance.

### D3. Native acquisition — consume the pinned official release archive, checksum-verified

`SlangVersion` pins `v2026.14.1` in `Directory.Build.props` alongside the other
three. An MSBuild target downloads the per-RID release asset, verifies its
SHA-256 against a value pinned in the same file, extracts **only** the required
files, and stages them under `native/slang/staged/<rid>/`. The staged path is
the cache key, the `ProjectReference` copy source and the pack input — the same
three-way role `Ahjo.Vulkan.Ktx.Native.csproj:55-65` gives its staged binary.

Shipped subset (Phase 1):

| RID | Files | Compressed |
|---|---|---|
| `win-x64` | `slang.dll`, `slang-compiler.dll` | ~11.5 MB |
| `linux-x64` | `libslang.so` (renamed copy of `libslang-compiler.so.0.2026.14.1`) | ~13.1 MB |

RIDs: `win-x64` + `linux-x64` only, matching the two lanes we are willing to
run. Slang publishes `windows-aarch64`, `linux-aarch64`, `macos-x86_64` and
`macos-aarch64` (all verified present at the tag), and all four stay unshipped
under the rule in `Ahjo.Vulkan.Ktx.Native.csproj:33-38` and
`.github/CLAUDE.md`: add the lane first, then the RID.

*Why not the alternatives.*
**Build from source (the `BuildKtxForHost` pattern)** — rejected: Slang's build
pulls LLVM and a full downstream-compiler tree; a cold CI build is tens of
minutes against libktx's low single digits, and the artifact we would produce is
the artifact Khronos already publishes and tests. The pattern the repo actually
cares about is "the binary that ships is the binary a lane executed", and D6's
lane delivers that regardless of who compiled it.
**Ship all six RIDs from the archive since they already exist** — rejected: no
lane, no ship. That rule exists because #144 shipped an unexecuted `.so`.
**Take a dependency on an existing NuGet Slang package** — rejected: the
official distribution channel is the GitHub release; a third-party repackage
adds a supply-chain hop we would then have to pin anyway.
**Ship `slang-glslang` in Phase 1** — deferred, not rejected; see **OPEN-1**.
The API path did not require it in any probed configuration (E6), so Phase 1
ships without it and the lane's optimization-level matrix decides.
**Ship `slang-llvm`** — rejected outright: 152 MB for CPU targets this wrapper
will never request.

### D4. Compiler API — session objects, spans of UTF-8, exceptions carrying the compiler's text

Three types in `src/Ahjo.Vulkan.Slang/`, all `sealed class : IDisposable`,
all setup-time:

```csharp
public sealed class SlangCompiler : IDisposable          // wraps IGlobalSession
{
    public static SlangCompiler Create();
    public SlangSession CreateSession(in SlangSessionDescription desc);
}

public sealed class SlangSession : IDisposable           // wraps ISession
{
    public SlangProgram Compile(in SlangCompileRequest request);
}

public sealed class SlangProgram : IDisposable           // wraps the linked IComponentType
{
    public int                EntryPointCount { get; }
    public SlangEntryPointInfo EntryPoint(int index);     // name + ShaderStages
    public ReadOnlySpan<uint> Spirv(int entryPointIndex); // valid until Dispose
    public SlangReflection    Reflection { get; }
    public string?            Warnings { get; }
}
```

`Spirv` hands back a span over Slang-owned blob memory the `SlangProgram` holds
a reference on — interchangeable with `SpirvBlob.Words`
(`SpirvBlob.cs:48-55`) at the `Device.CreateShaderModule(ReadOnlySpan<uint>)`
call site, and with the same "valid inside the `using` scope" contract
`SpirvBlob`'s remarks already state.

**Diagnostics.** Every Slang call that can produce an `ISlangBlob** outDiagnostics`
gets its blob read before anything else. A failing `SlangResult` (or a null
`IModule*`/`IEntryPoint*` — E2 confirms `loadModuleFromSourceString` signals
failure by returning `nullptr`, not only by a result code) throws
`SlangCompilationException` carrying the blob text verbatim. Slang's text is
already good enough to surface unchanged:

```
error[E30015]: undefined identifier
 --> bad.slang:1:21
  |
1 | float4 f() { return notAThing; }
  |                     ^^^^^^^^^ undefined identifier 'notAThing'.
```

A non-empty blob on a *successful* call is a warning set and lands in
`SlangProgram.Warnings`. There is no path on which an empty blob is returned
silently — that is the acceptance criterion the issue asks for, and it is
enforced by tests, not by convention.

**UTF-8 (invariant #1).** Slang takes `const char*` for module names, source
paths, entry-point names and profile names. Two categories, two rules:

- *Compile-time constants* (profile names such as `"spirv_1_5"u8`, target option
  names, the default module name) use `Utf8Name.FromLiteral` exactly as the
  invariant requires.
- *Runtime-variable strings* (file paths, entry-point names) are accepted as
  `ReadOnlySpan<byte>` **and** as `string`, and in both cases the wrapper
  encodes into an explicitly null-terminated, `fixed`-pinned buffer whose
  pointer is created and discarded inside the single native call. The
  prohibition in `src/Ahjo.Vulkan/CLAUDE.md` is against handing Slang a
  GC-movable, unterminated `byte[]` pointer — not against allocating at setup
  time. `GraphicsPipelineBuilder.CopyName` (`:213-220`) is the existing
  precedent for the span form. Every such site carries a comment saying why the
  literal rule does not apply.

*Why not the alternatives.*
**Return a nullable/`TryCompile` result instead of throwing** — rejected: the
issue's acceptance list requires exceptions, and a compile failure at setup time
is not a recoverable condition a caller branches on.
**Reuse `SpirvBlob` as the return type** — rejected: `SpirvBlob` owns an
`ArrayPool` rental (`SpirvBlob.cs:85`); a Slang blob is native memory owned by a
refcounted `ISlangBlob`. Forcing one into the other means a copy for no benefit.
The shared contract is the `ReadOnlySpan<uint>` at the call site, and that is
preserved.

### D5. Reflection API — produces the existing description types, and states where it cannot

`SlangReflection` produces the types in `src/Ahjo.Vulkan/Pipelines/`. No new
description type is introduced, and nothing in `src/Ahjo.Vulkan/` is modified.

```csharp
public sealed class SlangReflection
{
    public int  DescriptorSetCount { get; }
    public ReadOnlySpan<DescriptorBinding> DescriptorSet(int setIndex);
    public ReadOnlySpan<PushConstantRange> PushConstantRanges { get; }
    public ReadOnlySpan<VertexAttributeDescription> VertexAttributes(int entryPointIndex);
}
```

Mapping, per E3:

- `SlangBindingType` → `VkDescriptorType`, a total switch over the ten cases the
  SPIR-V target can emit (`SAMPLER`, `TEXTURE`, `CONSTANT_BUFFER`,
  `TYPED_BUFFER`, `RAW_BUFFER`, `COMBINED_TEXTURE_SAMPLER`,
  `INPUT_RENDER_TARGET`, `INLINE_UNIFORM_DATA`,
  `RAY_TRACING_ACCELERATION_STRUCTURE`, each optionally `| MUTABLE_FLAG`).
  Unmapped cases throw rather than guess.
- `DescriptorBinding.Slot` ← `getDescriptorSetDescriptorRangeIndexOffset`;
  `.Count` ← `…DescriptorRangeDescriptorCount` (verified: a `Texture2D maps[4]`
  inside a `ParameterBlock` reflects as `count=4`).
- `PUSH_CONSTANT` ranges are filtered out of the binding stream into
  `PushConstantRange { Offset = 0, Size = elementUniformSize }`.
- `VertexAttributeDescription` is populated with `Location` and `Format` only.

**Two mapping gaps, stated rather than papered over:**

1. **`DescriptorBinding.Stages` has no source in Slang reflection** (E3). The
   wrapper sets it to the union of the stages of the entry points in the
   compiled program, and the XML doc says so in one sentence. That is correct
   (a superset of actual usage is always valid for `VkDescriptorSetLayout`) and
   it is coarser than a hand-written layout. A caller who wants tighter
   visibility overrides the field — the type is a `readonly record struct` with
   `init` accessors, so `binding with { Stages = ShaderStages.Fragment }` is the
   documented escape hatch.
2. **`VertexInputDescription` cannot be produced at all** (E3).
   `VertexBindingDescription.{Slot, Stride, InputRate}` and
   `VertexAttributeDescription.{Binding, Offset}` describe the application's
   buffer packing, which the shader never states. The reflection API therefore
   returns `VertexAttributeDescription` values with `Binding`/`Offset` left at
   their defaults and does **not** offer a `VertexInputDescription` factory. The
   XML doc says the caller must fill those two fields. This is a genuine finding
   about the shape of the request in #166, not a shortfall in
   `VertexInputDescription` — and it is exactly why no parallel description type
   is being invented: a `SlangVertexInput` that carried only half the fields
   would be a worse `VertexAttributeDescription`, not a better one.

**Scope of set enumeration.** The flat
`spReflectionTypeLayout_getDescriptorSet*` walk over the global-params type
layout returns the parameters in **space 0 only**. Slang's mechanism for
additional descriptor sets is `ParameterBlock<T>`, which appears in the global
walk as a sub-object range and requires recursing
`getSubObjectRangeBindingRangeIndex` → `getBindingRangeLeafTypeLayout` →
`GetElementTypeLayout` → `getDescriptorSetCount`. That recursion was verified to
produce the block's own set (`ranges=2: TEXTURE count=4, SAMPLER count=1`), but
`getSubObjectRangeSpaceOffset` reported `0` for it while
`spReflectionParameter_GetBindingIndex` on the block reported `1` — i.e. the set
*index* derivation is not settled. See **OPEN-2**.

*Why not the alternatives.*
**A parallel `Slang*` description type set** — rejected, and explicitly out of
bounds per the issue: reflection's whole value is that its output is the type
`Device.CreateDescriptorSetLayout` already takes.
**Changing `DescriptorBinding`/`VertexAttributeDescription` to make them
reflectable** (e.g. making `Stages` nullable) — rejected: it would regress #119's
valid-by-default contract for every hand-written caller to accommodate one
producer.
**Deriving `Stages` from the SPIR-V binary instead** — rejected for Phase 3: it
means a SPIR-V parser in this repo, which is a separate design with its own
issue.

### D6. Proving it — one native lane, one drift test

`build-slang-native.yml`, modelled line-for-line on `build-ktx-native.yml`:
`win-x64` on `windows-latest` and `linux-x64` on `ubuntu-latest`, each job
staging its RID's binaries and **running `Ahjo.Vulkan.Slang.Native.Tests` before
uploading the artifact**. It provisions no Vulkan loader and no ICD, on purpose
and for the same reason the `ktx-native` lane does not: Slang produces bytes and
must not need a graphics API. `AHJO_VULKAN_TIER` stays unset (`none`) — the lane
touches no Vulkan surface. It is a build-artifact check, not wrapper coverage,
and must not grow into one (`.github/CLAUDE.md`).

The **drift test** is a `string[]` of the ~30 `spReflection_*` /
`spReflectionTypeLayout_*` / `spReflectionEntryPoint_*` names the wrapper
actually calls, resolved through
`NativeLibrary.Load` + `NativeLibrary.TryGetExport`. No reflection, no
`Assembly.GetTypes()`, AOT-clean by construction. A `SlangVersion` bump that
drops any of them fails that one test with the missing name in the message —
which is the loud failure the issue asks for, and the reason the deprecated-header
dependency in D2 is acceptable.

Wrapper-level tests (`Ahjo.Vulkan.Slang.Tests`) run in the Windows `build-test`
job only. They need no Vulkan device for compile + reflect; the one test that
feeds a Slang blob to `Device.CreateShaderModule` goes behind
`TestGate.RequireDriver` like every other device-dependent test
(`tests/CLAUDE.md`). Linux wrapper lanes stay closed (#32).

### D7. Runtime API now, MSBuild task later

This spec delivers the **runtime** API only. An MSBuild task replacing the
`glslc` `Exec` is strictly downstream of it — the task would host the same
`SlangCompiler` — and building the task first would leave reflection unbuilt,
which is half the point of #166. Doing both at once triples the surface under
review in one PR.

---

## Phases and what can ship independently

| Phase | Deliverable | Ships alone? |
|---|---|---|
| 1 | `Ahjo.Vulkan.Slang.Native`: `.rsp`, generated bindings, archive acquisition + staging + pack, `build-slang-native.yml`, native smoke + drift tests | **Yes.** A published raw-binding package, exactly like `Ahjo.Vulkan.Ktx.Native` was. |
| 2 | `Ahjo.Vulkan.Slang`: `SlangCompiler`/`SlangSession`/`SlangProgram`, diagnostics, `AotSmoke` coverage | **Yes.** Compile-to-SPIR-V is useful without reflection. |
| 3 | `SlangReflection` → `DescriptorBinding` / `PushConstantRange` / `VertexAttributeDescription` | Requires Phase 2. |

---

## OPEN

**OPEN-1 — does `slang-glslang` ship?** E6 shows the API path did not require
`spirv-opt` in any probed configuration, while `slangc`'s default pipeline does.
Phase 1 ships core-only. Phase 2's test matrix compiles at every
`SlangOptimizationLevel`; if any level fails with
`failed to load downstream compiler 'spirv-opt'`, the answer is to add
`slang-glslang.dll` / `libslang-glslang-2026.14.1.so` (+2.4 MB / +3.6 MB
compressed) and the implementer should stop and confirm rather than silently
growing the package or silently capping the optimization level.

**OPEN-2 — multi-descriptor-set reflection.** Phase 3 as specified enumerates
space 0. `ParameterBlock<T>` sub-object recursion was verified to reach the
block's bindings, but the set-index derivation is not settled (E3/D5). The
implementer should implement space 0, add an `[Fact(Skip=…)]`-free failing-loud
guard (`SlangReflection` throws `NotSupportedException` naming the parameter
when it encounters a `SUB_ELEMENT_REGISTER_SPACE` parameter), and raise a
follow-up issue rather than guessing the mapping.

**OPEN-3 — `SlangCompiler` lifetime and `slang_shutdown`.** `slang.h:5860`
documents `slang_shutdown()` as callable only after every Slang object is
released, and Slang's global session is process-scoped. Whether
`SlangCompiler.Dispose()` should call it (and therefore whether a second
`SlangCompiler.Create()` in the same process is legal) is not settled by the
header. Phase 2 should release the global session and **not** call
`slang_shutdown`, and add a test that creates/disposes two compilers in
sequence; if that test fails, stop and ask.

---

## Follow-ups this spec does not do

- **Replace the samples'/tests' `glslc` `Exec` with a Slang MSBuild task**
  (relates to #162). Should become its own issue after Phase 2 — it is a build
  system change with its own failure modes (task host, incremental inputs,
  design-time builds) and it is what finally deletes the eight duplicated
  `_GlslcExe` blocks and lets `ci.yml:212` stop printing "NOT PROVEN".
- **Migrate the existing GLSL sample shaders to Slang.** Should become its own
  issue after that one. It is a content change with a per-sample visual diff to
  review, it is not all-or-nothing (Slang can consume GLSL), and doing it before
  the build task exists would mean hand-running `slangc` — trading one unpinned
  external invocation for another.
