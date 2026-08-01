# Slang support: pinned shader compiler + reflection-driven pipeline layouts

**Issue:** [#166](https://github.com/pekkah/Ahjo-Vulkan/issues/166) — *Slang support: pinned shader compiler + reflection-driven pipeline layouts*
**Prevents the structural cause of:** [#162](https://github.com/pekkah/Ahjo-Vulkan/issues/162) (`glslc` absent from the runner; the toolchain is an unpinned `PATH` assumption)
**Lands consistently with:** [#119](https://github.com/pekkah/Ahjo-Vulkan/issues/119) (valid-by-default descriptions — reflection must *produce* those types, not fork them), [#32](https://github.com/pekkah/Ahjo-Vulkan/issues/32) / `.github/CLAUDE.md` (no RID ships without a lane that has executed it)
**Date:** 2026-08-01
**Revised:** 2026-08-01 — consumer requirement narrowed the design. The
downstream consumer is an Unreal-Engine-*Substrate*-style material system, which
means reflection must work over a **program composed at runtime from N modules +
N entry points**, and must handle **`ParameterBlock<T>` per material**. That
promoted the original **OPEN-2** (multi-descriptor-set reflection) from a
deferral to the load-bearing case; it is now settled empirically (E10) and
specified in D5. New evidence E10-E17, new decisions D8-D10, Phase 3 split.
Phase 1 and D1-D4, D6, D7 are unchanged.
**Revised:** 2026-08-01 (post-implementation) — **OPEN-1 is resolved by human
decision and shipped** (commit `f646463`): `slang-glslang` ships on both RIDs.
E6 and D3 now carry the measurement instead of the projection, including the
part this spec predicted wrongly. OPEN-3 is resolved in the plan's direction;
OPEN-4 is decided except for one sub-question. No other decision changed, and
nothing in D5/D8/D9/D10 or the reflection evidence is affected.

---

## Problem

Three defects. The first two are downstream of "the shader compiler is whatever
is on `PATH`"; the third is what the consumer requirement added.

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

**3. A material system's shader is not one file, and its layout is not one
descriptor set.** A Substrate-style material system assembles a program at
runtime out of a shared "common" module, a geometry module, and a per-material
module, plus the entry points it wants — `ISession::createCompositeComponentType`
over N components, then `IComponentType::link`, then `getLayout` on the *composed*
program (`slang.h:5341-5344`, `:5378-5386`). It gives each material its own descriptor set via
`ParameterBlock<T>`. A reflection API that only reflects a single `IModule` and
only enumerates descriptor space 0 does not serve that consumer at all: it
reports the wrong bindings (E12) and throws on the one parameter that matters
(E10). This is a gap in *this design*, not in the codebase, and it is what the
revision fixes.

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
| `lib/libslang-glslang-2026.14.1.so` | 10 055 776 | 3 632 358 | **Yes** — provides `spirv-opt`, without which `Optimization` is a silent no-op. See below. |
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

**On `slang-glslang`, this spec originally reported the evidence as mixed. It
is no longer mixed: the library ships (OPEN-1, commit `f646463`), and *how* the
question resolved is the part worth keeping.**

What was measured before implementation, and is still true as far as it goes:
`slangc test.slang -target spirv` fails without the library —
`error[E00100]: failed to load downstream compiler 'spirv-opt'` /
`note[E99996]: failed to load dynamic library 'slang-glslang-2026.14.1'` — and
succeeds with `-O0` (2 008-byte blob) or once the library is present
(1 704-byte blob). The **API path this design uses** (`getEntryPointCode` on a
linked composite) produced valid SPIR-V with only `libslang.so` present, at
default target flags and with an explicit `Optimization` target option. This
spec concluded from that: "it did not demand `spirv-opt` in any configuration
probed."

What the Phase 2 implementation run measured, one process per level on
`v2026.14.1` / linux-x64, on a deliberately redundant vertex shader (a
fixed-trip loop that folds, a dead local chain, two arithmetic identities):

| shipped set | `None` | `Default` | `High` | `Maximal` | downstream-compiler diagnostic |
|---|---|---|---|---|---|
| compiler only | 317 words | 317 | 317 | 317 | `error[E00100] … 'spirv-opt'` + `note[E99996] … 'slang-glslang-2026.14.1'` at every level above `None` |
| compiler + `slang-glslang` | 317 | 313 | 245 | 245 | none, at any level |

Verified from the other side by withholding the binary from the output
directory: 4 of the 5 optimization tests fail without it, 5 of 5 pass with it.

**"The API path does not demand `spirv-opt`" was true only in the sense that it
never fails, and that is the more dangerous reading.** Every level returned
`SLANG_OK` and a well-formed module; the missing optimizer surfaced as text in
the diagnostics blob of a *successful* call, which the wrapper routes to
`SlangProgram.Warnings` (D4). A caller asking for `Maximal` got `None`, quietly.
The failure mode is therefore strictly worse than the outright failure OPEN-1's
decision procedure was written to detect. **Carry this forward to the next
`SlangVersion` bump: an absent Slang downstream compiler does not fail the call,
it makes the option do nothing.** Any future "do we actually need this library"
question is answered by comparing emitted bytes across settings, never by
checking result codes.

Cost, measured rather than estimated:

| RID | File | Raw | Compressed |
|---|---|---|---|
| `win-x64` | `bin/slang-glslang.dll` | 6 173 184 | **2 417 901** in the pinned archive (this spec's pre-implementation figure of 2 411 328 was ~6 KB low) |
| `linux-x64` | `lib/libslang-glslang-2026.14.1.so` | 10 055 776 | 3 632 358 in the archive; **3 736 269** as deflated into the produced nupkg — a different deflate level, so both numbers are real and they measure different things |

Package total moves from ~24.6 MB to **~31 MB** across both RIDs: ~14 MB
`win-x64`, ~17 MB `linux-x64`. `src/Ahjo.Vulkan.Slang.Native/README.md` now
carries that as a per-RID column rather than a single number, because the two
RIDs are no longer close enough for one figure to be honest.

One deployment prediction held: on Linux the file ships **unrenamed**
(`libslang-glslang-2026.14.1.so`), because `libslang` `dlopen`s it by its
versioned name — unlike `libslang.so` itself, which is a renamed copy by
necessity. Renaming it is indistinguishable from not shipping it.

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

### Method note for E10-E18

Everything below was produced by extending the E2/E3 probe: the same
ClangSharp-generated binding, the same renamed `libslang.so`, on linux-x64.
Two things were added, and they are why these findings are assertions rather
than readings of a header:

1. **SPIR-V ground truth.** Every claim about a descriptor set/binding number is
   cross-checked against the emitted module by parsing `OpName` (opcode 5) and
   `OpDecorate` (opcode 71) with `DescriptorSet` (34) / `Binding` (33) out of
   `IComponentType::getTargetCode` and `getEntryPointCode`. Where the text below
   says "SPIR-V says set=1 binding=2", that is a decoration read out of the blob
   Slang produced, not an inference from reflection.
2. **Composition.** Programs are built as
   `loadModuleFromSourceString` xN -> `findAndCheckEntryPoint` xM ->
   `createCompositeComponentType` -> `link` -> `getLayout`, i.e. exactly the
   consumer's shape.

### E10. OPEN-2 is settled: the set index is `GetOffset(param, SUB_ELEMENT_REGISTER_SPACE)`

A program with a populated global scope plus **two** `ParameterBlock<T>`s
(`ConstantBuffer<Xform> gXform; Texture2D gAlbedo; SamplerState gSampler;`
`ParameterBlock<MatParams> gMaterial;` `ParameterBlock<LightParams> gLights;`
plus a push-constant block) reflects as:

| parameter | category | `GetBindingIndex` | `GetOffset(…, SUB_ELEMENT_REGISTER_SPACE)` | `getSubObjectRangeSpaceOffset` | **SPIR-V set** |
|---|---|---|---|---|---|
| `gXform` | `DESCRIPTOR_TABLE_SLOT` | 0 | 0 | – | 0 |
| `gAlbedo` | `DESCRIPTOR_TABLE_SLOT` | 1 | 0 | – | 0 |
| `gSampler` | `DESCRIPTOR_TABLE_SLOT` | 2 | 0 | – | 0 |
| `gMaterial` | `SUB_ELEMENT_REGISTER_SPACE` | 1 | **1** | 0 | **1** |
| `gLights` | `SUB_ELEMENT_REGISTER_SPACE` | 2 | **2** | 0 | **2** |

So the earlier confusion resolves cleanly:

- **`spReflectionTypeLayout_getSubObjectRangeSpaceOffset`
  (`slang-deprecated.h:678`) returned `0` because it is the wrong function for
  this.** It reported `0` for *all four* sub-object ranges in the fixture,
  including the two blocks that demonstrably land in spaces 1 and 2. It is not
  the set index and must not be used as one.
- **The set index is
  `spReflectionVariableLayout_GetOffset(param, SLANG_PARAMETER_CATEGORY_SUB_ELEMENT_REGISTER_SPACE)`**
  (`slang-deprecated.h:741`, category `slang.h:2297`). For a parameter whose
  category *is* `SUB_ELEMENT_REGISTER_SPACE` this is the same number
  `spReflectionParameter_GetBindingIndex` (`slang-deprecated.h:873`) returns —
  which is why `GetBindingIndex` read `1` while `getSubObjectRangeSpaceOffset`
  read `0`. `GetBindingIndex` was right; it just wasn't obviously a *space*.
- The same number is reachable a second way, and both agree:
  `spReflectionTypeLayout_getSubObjectRangeOffset(globals, i)`
  (`slang-deprecated.h:681`) returns a `VariableLayout` whose
  `GetOffset(SUB_ELEMENT_REGISTER_SPACE)` is 1 and 2 for the two blocks.

**It is an offset, not an absolute index, and that matters for nesting.** With
`struct Outer { ParameterBlock<Inner> inner; Texture2D o; float4 k; };
ParameterBlock<Outer> gNested;` at global-scope offset 3, the `inner` field's
own `GetOffset(SUB_ELEMENT_REGISTER_SPACE)` is `1` — relative to its parent —
and SPIR-V places `gNested.inner` at **set 4**. The derivation is therefore
recursive with accumulation:

```
setOf(block) = setOf(enclosing scope) + block.GetOffset(SUB_ELEMENT_REGISTER_SPACE)
setOf(global scope) = 0
```

Verified end to end: `gNested` -> set 3, `gNested.o` -> set 3 binding 1,
`gNested.inner` -> set 4, `gNested.inner.t` -> set 4 binding 1.

**"Global scope is always set 0" is false, and the formula above is what saves
it.** In a program whose global scope declares *no* descriptors at all (only
`ParameterBlock`s — the natural Substrate shape once everything is in a block),
`getDescriptorSetCount(globalParamsTypeLayout)` is `0`, the first block's
`GetOffset(SUB_ELEMENT_REGISTER_SPACE)` is `0`, and SPIR-V puts it at **set 0**;
the second lands at set 1. A hardcoded "global scope owns space 0, blocks start
at 1" would be wrong by one for every set in that program.

**Global-scope sets are not necessarily contiguous either.** With
`[[vk::binding(3, 0)]] Texture2D gTex;` and `[[vk::binding(7, 2)]] SamplerState
gSamp;`, the global params type layout reports **two** descriptor sets, and
`spReflectionTypeLayout_getDescriptorSetSpaceOffset` (`slang-deprecated.h:632`)
returns `0` for the first and **`2`** for the second — matching SPIR-V's
`set=0 binding=3` / `set=2 binding=7`. The loop index over
`getDescriptorSetCount` is *not* the Vulkan set number; `getDescriptorSetSpaceOffset`
is.

### E11. A `ParameterBlock<T>` with ordinary data silently owns binding 0, and reflection does not list it

| block element type | `GetSize(elem, UNIFORM)` | descriptor ranges reflection lists | SPIR-V bindings |
|---|---|---|---|
| `{ Texture2D maps[4]; SamplerState samp; float4 factors; float roughness; }` | **32** | `TEXTURE idx=1 count=4`, `SAMPLER idx=2` | `0 = gWith` (implicit UB), `1 = gWith.maps`, `2 = gWith.samp` |
| `{ StructuredBuffer<float4> buf; Texture2D tex; SamplerState samp; }` | **0** | `RAW_BUFFER idx=0`, `TEXTURE idx=1`, `SAMPLER idx=2` | `0,1,2` — no implicit UB |

When the block's element type has ordinary (uniform) data, Slang allocates an
implicit uniform buffer at **binding 0** of the block's space and shifts every
listed range up by one — but **emits no descriptor range for that buffer**. A
`DescriptorSetLayout` built only from the listed ranges is missing a binding and
the pipeline is invalid at bind time.

The rule is derivable and was verified on four separate blocks:
`spReflectionTypeLayout_GetSize(elementTypeLayout, SLANG_PARAMETER_CATEGORY_UNIFORM) > 0`
=> synthesize `VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER` at slot 0 of that set.

**The global scope does not have this problem.** Loose global uniforms
(`float4 gTint; float gScale;`) *are* listed: the global set's range list gains
`SLANG_BINDING_TYPE_CONSTANT_BUFFER idx=0`, `spReflection_getGlobalConstantBufferBinding`
(`slang-deprecated.h:998`) returns `0` and `…_getGlobalConstantBufferSize`
(`:1006`) returns `32`, and SPIR-V emits `set=0 binding=0 globalParams`. The
asymmetry is real and the wrapper must handle exactly one of the two cases, not
both.

Also from that fixture, a warning worth surfacing to users: Slang emits
`warning[E39019]: implicit global shader parameter` for each loose global — it
treats them as shader parameters, not variables. That text reaches
`SlangProgram.Warnings` under D4 unchanged.

### E12. Composition changes the layout; per-module reflection is wrong

Three modules — `common` (`ParameterBlock<CameraData> gCamera`), `geometry`
(`StructuredBuffer<InstanceData> gInstances`, `vertexMain`), `material`
(`ParameterBlock<SubstrateParams> gMaterial`, `ParameterBlock<LightParams> gLights`,
a push-constant block, `Texture2D gLut`, `SamplerState gLutSamp`, `fragmentMain`)
— reflected **alone** versus reflected through
`createCompositeComponentType([common, geometry, material, vsEP, fsEP])` + `link`:

| parameter | set/binding when its module is reflected alone | set/binding in the composed program |
|---|---|---|
| `gCamera` | set 0 | **set 1** |
| `gInstances` | set 0, binding 0 | set 0, binding 0 |
| `gMaterial` | set 1 | **set 2** |
| `gLights` | set 2 | **set 3** |
| `gLut` | set 0, binding 0 | set 0, **binding 1** |
| `gLutSamp` | set 0, binding 1 | set 0, **binding 2** |

SPIR-V for the composed program confirms every right-hand column value. Two
consequences, both load-bearing:

- **Reflection must be taken from the linked composite**, never from an
  `IModule`. `slang.h:5378-5386` says this in the header ("If this component type
  is combined into a composite, then the absolute offsets/bindings of parameters
  may not stay the same"); the table is the measurement.
- **Composite membership *and order* are part of the layout contract.** Re-running
  the identical five components in the order `[material, common, geometry, fsEP,
  vsEP]` produced: `gLut` binding 0, `gLutSamp` binding 1, `gInstances` binding 2,
  `gMaterial` set 1, `gLights` set 2, `gCamera` set 3 — a completely different
  assignment. Entry-point *indices* reorder with it too
  (`ep[0] = fragmentMain, ep[1] = vertexMain`). A wrapper that lets composition
  order vary between the reflect call and the codegen call produces a pipeline
  layout that does not match its own SPIR-V.
- Composing the entry points **without** naming their modules also links and
  still reports all seven global parameters (an entry point carries its module as
  a requirement, `slang.h:5337-5339`) — but with a *third* assignment again. So
  "just add the entry points" is not equivalent to "add the modules and the entry
  points"; the API has to make the component list explicit.

Two things composition does **not** change: `loadModuleFromSourceString` registers
the module under its name so a later `import common;` resolves with no file
system present (verified — all three modules loaded from strings), and
`getEntryPointCode(i)` indexes the same entry-point order that
`spReflection_getEntryPointByIndex(i)` does (verified by correlating each blob's
descriptor decorations with the entry point that uses them).

### E13. Entry-point stage attribution *is* recoverable under composition — via `IMetadata`, not via reflection

The E3 finding stands unchanged and is not composition-specific:
`spReflectionVariableLayout_getStage` (`slang-deprecated.h:860`) returns
`SLANG_STAGE_NONE` for every global descriptor parameter in every fixture
probed, single-module and composed alike. `spReflectionEntryPoint_getStage`
(`:908`) does return real stages (`SLANG_STAGE_VERTEX`, `SLANG_STAGE_PIXEL`),
also under composition and also after re-ordering.

What is new is a second source. `IComponentType::getEntryPointMetadata`
(`slang.h:5536-5540`) yields an `IMetadata` whose `isParameterLocationUsed`
(`slang.h:4715-4728`) answers, per entry point, whether a given
(category, space, index) is actually used:

```
composed program, per-entry-point query on SLANG_PARAMETER_CATEGORY_DESCRIPTOR_TABLE_SLOT
  vertexMain    : s0b0=USED  s0b1=no    s0b2=no    s1b0=USED  s2b*=no    s3b*=no
  fragmentMain  : s0b0=no    s0b1=USED  s0b2=USED  s1b0=USED  s2b0..3=USED  s3b0..1=USED
```

Cross-checked against per-entry-point SPIR-V from `getEntryPointCode`: the
`vertexMain` module contains decorations for exactly `set=0 binding=0` and
`set=1 binding=0`; the `fragmentMain` module contains exactly the other nine.
The two agree on every location, including `gCamera` (set 1) which both stages
use because it is reached through a helper in a third module. **So
`DescriptorBinding.Stages` is derivable after all** — E3's "not derivable" was
true of the reflection API and false of the compiled-artifact API.

Three caveats, all measured:

- **Push constants are not covered.** `isParameterLocationUsed` returned `false`
  for `(PUSH_CONSTANT_BUFFER, 0, 0)` on *both* entry points of a program whose
  fragment stage demonstrably reads `gPush.tint` and whose fragment SPIR-V
  contains a `PushConstant` variable. Sweeping `UNIFORM`, `CONSTANT_BUFFER`,
  `REGISTER_SPACE` and `DESCRIPTOR_TABLE_SLOT` over spaces 0-1 and indices 0-1
  found no category that reports the push constant as used. Push-constant stage
  attribution stays a union.
- **It costs a codegen.** `getEntryPointMetadata` carries the same preconditions
  as `getEntryPointCode` (`slang.h:5533-5534`: "Has the same requirements … as
  `getEntryPointCode()`"), i.e. fully specialized and fully linked, and it can
  fail with diagnostics. Computing precise stages therefore makes reflection a
  compiling operation that can throw.
- **It reports post-optimization usage.** A binding no entry point touches
  reports `false` everywhere, which would yield `Stages = None` — not a usable
  `VkDescriptorSetLayoutBinding`.

### E14. Specialization changes the reflected layout, so reflection must follow it

Two distinct Slang mechanisms, two different answers, both measured.

**Generic specialization parameters (`type_param`) — the layout changes.** For
`type_param TSurface : ISurface; ParameterBlock<TSurface> gSurface;`
(`getSpecializationParamCount()` = 1):

| program | `gSurface` element kind | ranges reflected for its set | SPIR-V |
|---|---|---|---|
| unspecialized, linked | `SLANG_TYPE_KIND_GENERIC_TYPE_PARAMETER` (`slang.h:2124`) | **none** (`getDescriptorSetCount(elem)` = 0) | n/a |
| `specialize(Glossy)` + link | `SLANG_TYPE_KIND_STRUCT` | `TEXTURE idx=1`, `SAMPLER idx=2` | set 1 bindings 0,1,2 |
| `specialize(Matte)` + link | `SLANG_TYPE_KIND_STRUCT` | `TEXTURE idx=1,2,3`, `SAMPLER idx=4` | set 1 bindings 0..4 |

Reflecting before specializing does not fail — it silently reports a descriptor
set with **zero** bindings where the compiled shader has three or five. This is
the strongest correctness statement in the revision: **reflect the specialized,
linked component or the layout is wrong.** The header states the same thing at
`slang.h:5370-5377`.

**Interface-typed parameter blocks — the layout is specialization-invariant, by
design.** For `ParameterBlock<ISurface> gSurface;`, the reflected layout is
byte-identical before specialization, after `createTypeConformanceComponentType`
linking, and after `specialize()`: category `MIXED`, element kind
`SLANG_TYPE_KIND_INTERFACE` (`slang.h:2125`), `getDescriptorSetCount(elem)` = 0,
`GetSize(elem, UNIFORM)` = 32. And that is *correct*: the SPIR-V from the
conformance-linked program contains exactly one descriptor for the block —
`set=1 binding=0 gSurface`, the existential value buffer — which is precisely
what E11's "elem uniform size > 0 => uniform buffer at binding 0" rule already
produces. `slang.h:5370-5377` documents this invariance explicitly.

**Two codegen findings fall out, and one of them is a native crash.**

- `getTargetCode` on an **unspecialized** program with an interface-typed block
  fails cleanly: `0x80004005` with
  `error[E50100]: no type conformances found … Code generation for current target
  requires at least one implementation type present in the linkage.`
- `IComponentType::specialize(Glossy)` + `link` on that same program **succeeds**,
  `getLayout` **succeeds**, and then `getTargetCode` — or `getEntryPointCode` for
  the entry point that consumes the block — **segfaults inside libslang**:

  ```
  Thread 1 received SIGSEGV
  #0  Slang::IRVarLayout::getTypeLayout()
  #1  Slang::declareVars(Slang::IRTypeLegalizationContext*, …)
  #5  Slang::legalizeInst(…)
  #7  Slang::legalizeTypes(…)
  #8  Slang::linkAndOptimizeIR(…)
  #11 Slang::TargetProgram::_createWholeProgramResult(…)
  #13 Slang::ComponentType::getTargetCode(long, ISlangBlob**, ISlangBlob**)
  ```

  Every frame is Slang's own type-legalization pass; frame 13 is the correct
  virtual entry, so this is not a binding defect. `getEntryPointCode(0)` for the
  *vertex* entry point (which does not touch the block) returns a valid 223-word
  module first; only the consuming entry point crashes. Reproduced 3/3 in a
  minimal fixture.
- The same `specialize()` call on the **`type_param`** form produced valid SPIR-V
  for both `Glossy` and `Matte` (558 and 650 words, decorations matching the
  reflected layout exactly). So the crash is specific to specializing a global
  whose type is an *interface-typed* `ParameterBlock`, not to `specialize()` as
  such.
- `ISession::createTypeConformanceComponentType` (`slang.h:4608-4631`) is the
  route that works for the interface form: composite of
  `[module, module, vsEP, fsEP, conformance]` linked and produced a valid
  1169-word module.

### E15. Reflection reports the declared union, not the used subset

A program declaring both `ParameterBlock<Glossy> gGlossyBlock` and
`ParameterBlock<Matte> gMatteBlock` reflects **both** (sets 1 and 2) regardless of
which fragment entry point is in the composite — while `getTargetCode` for the
`fragGlossy` variant emits only set 1 and for the `fragMatte` variant only set 2.
That is the right default for building a `VkPipelineLayout` (the layout must
describe what the program declares), and it is why the narrowing in E13 is a
separate, opt-in step rather than the default.

### E16. Vertex inputs under composition: system values and structs

`spReflectionEntryPoint_getParameterByIndex` on a composed program returns
system-value parameters alongside real varying inputs, and they are cleanly
distinguishable:

| entry-point parameter | `GetParameterCategory(typeLayout)` | `GetSize(tl, VARYING_INPUT)` | `GetOffset(p, VARYING_INPUT)` |
|---|---|---|---|
| `VSIn vin` (struct) | `VARYING_INPUT` | 7 | 0 |
| `uint iid : SV_InstanceID` | **`NONE`** | **0** | 0 |
| `uint vid : SV_VertexID` | **`NONE`** | **0** | 0 |
| `bool ff : SV_IsFrontFace` | **`NONE`** | **0** | 0 |

The plan's original walk would have emitted a bogus attribute at location 0 for
`SV_InstanceID`, colliding with the real `POSITION` at location 0. The filter is
`GetParameterCategory(typeLayout) == SLANG_PARAMETER_CATEGORY_VARYING_INPUT`
(`slang-deprecated.h:568`), verified on all four rows.

Struct-typed vertex inputs need one level of recursion, and locations accumulate:

```
VSIn { float3 pos : POSITION; float2 uv : TEXCOORD0; float4 tangent : TANGENT; float4x4 inst : INSTANCEXF; }
  pos      off(VARYING_INPUT)=0  size=1   -> SPIR-V Location 0
  uv       off=1  size=1                  -> Location 1
  tangent  off=2  size=1                  -> Location 2
  inst     off=3  size=4  kind=MATRIX     -> Location 3 (SPIR-V decorates the matrix at 3, consuming 3..6)
```

Location = parent offset + field offset, via `GetFieldCount`/`GetFieldByIndex`
(`slang-deprecated.h:538-539`). The fragment stage's struct input has a
`SV_POSITION` field with category `NONE` and the same filter excludes it. A
`MATRIX`-kind vertex input occupies `GetSize(tl, VARYING_INPUT)` consecutive
locations, but which scalar count each of those locations carries depends on
`SessionDesc.defaultMatrixLayoutMode`, and that was **not** probed across both
modes — see OPEN-6.

### E17. Two push-constant blocks compose, and the byte offsets are not derivable

Two modules each declaring `[[vk::push_constant]]` compose and link. The global
scope reports **two** `SLANG_BINDING_TYPE_PUSH_CONSTANT` ranges, at
`indexOffset` 0 and 1, and SPIR-V emits two distinct `PushConstant` variables
(`gPush1` used only by the vertex stage, `gPush2` only by the fragment stage).
But `spReflectionVariableLayout_GetOffset(p, PUSH_CONSTANT_BUFFER)` returned `0`
and `1` — a *buffer index*, not a byte offset, while the element uniform sizes
are 16 and 8. `VkPushConstantRange.offset` is a byte offset. Nothing probed
yields the byte offsets Vulkan wants for the two-block case, so D5 refuses it
rather than guessing (OPEN-5).

### E18. The C++ reflection shim in the pinned header does not compile

Incidental, but it retires the last doubt about D2. `slang.h:3020-3023` defines
`TypeLayoutReflection::getBindingRangeSpaceOffset` as a call to
`spReflectionTypeLayout_getBindingRangeSpaceOffset` — a function that is
**declared in no shipped header and exported by no shipped binary**
(`nm -D` over `libslang-compiler.so.0.2026.14.1` finds 258 `sp*` symbols, not
that one). Anyone using Slang's own recommended C++ shim for the reflection
surface D5 needs would hit a link error on that member. Binding the flat
`spReflection*` exports directly, as D2 decided, is not merely equivalent — it
is the only one of the two that builds at this tag.

Related: `slang-deprecated.h:685-696` wraps a further six
`spReflectionTypeLayout_getSubObjectRangeDescriptorRange*` declarations in
`#if 0`. They are not available and any design that wants them is blocked.

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
SPIR-V never pulls ~31 MB of compiler (~14 MB `win-x64` / ~17 MB
`linux-x64` — the figure grew with `slang-glslang`, OPEN-1). This raises the
repo to six projects
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

**Correction from implementation (`f646463`):** "the staged path is the cache
key" is too coarse to be safe. Keyed on the *primary* staged file alone, a
staged tree left over from before `slang-glslang` joined the shipped set
satisfies the check, and the build then silently produces a package whose
`Optimization` is still a no-op — the exact defect OPEN-1 existed to fix. The
cache token is the presence of **every** shipped file for the RID, not of one
representative file, and staging errors when any of them fails to appear.

Shipped subset:

| RID | Files | Compressed |
|---|---|---|
| `win-x64` | `slang.dll`, `slang-compiler.dll`, `slang-glslang.dll` | ~14 MB |
| `linux-x64` | `libslang.so` (renamed copy of `libslang-compiler.so.0.2026.14.1`), `libslang-glslang-2026.14.1.so` (**not** renamed) | ~17 MB |

Phase 1 originally shipped the first two rows without `slang-glslang`;
it was added by OPEN-1's resolution (`f646463`), and E6 carries the
measurement that drove it.

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
**Ship the compiler without `slang-glslang`** — was the Phase 1 position, now
**rejected on measurement**: the API path does not *require* `spirv-opt` only in
the sense that it never fails without it. Every `SlangOptimizationLevel` above
`None` returns `SLANG_OK` and byte-identical SPIR-V while reporting the missing
downstream compiler as a warning, so `Optimization` is a silent no-op (E6). The
2.4 MB / 3.7 MB is the price of the setting meaning anything, and a public
option that does nothing is worse than an absent one.
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

### D5. Reflection API — a composed program's *whole* binding surface, sparse sets included

`SlangReflection` still produces only the types in `src/Ahjo.Vulkan/Pipelines/`.
No new description type is introduced and nothing in `src/Ahjo.Vulkan/` is
modified. What changed is that it reflects a **linked composite** and covers
**every descriptor space the program declares**, not space 0.

```csharp
public sealed class SlangReflection
{
    // Descriptor sets, in ascending set index. The program's set indices are
    // NOT necessarily 0..N-1 and NOT necessarily contiguous (E10).
    public int  DescriptorSetCount { get; }                    // number of POPULATED sets
    public uint SetIndex(int i);                               // the Vulkan set number of the i-th
    public ReadOnlySpan<DescriptorBinding> Bindings(int i);    // its bindings, ascending Slot
    public bool TryGetSet(uint setIndex, out ReadOnlySpan<DescriptorBinding> bindings);

    // Highest declared set index + 1 — the length PipelineLayoutDescription.SetLayouts
    // must have, because that span is positional.
    public uint SetLayoutSlotCount { get; }

    public ReadOnlySpan<PushConstantRange> PushConstantRanges { get; }

    public int  EntryPointCount { get; }
    public SlangEntryPointInfo EntryPoint(int index);
    public ReadOnlySpan<VertexAttributeDescription> VertexAttributes(int entryPointIndex);
}
```

**Why `SetIndex(i)` exists rather than `DescriptorSet(int setIndex)`.**
`PipelineLayoutDescription.SetLayouts` is a positional
`ReadOnlySpan<DescriptorSetLayout>` (`PipelineLayoutDescription.cs:9`) consumed
by index in `Device.CreatePipelineLayout`
(`src/Ahjo.Vulkan/Lifecycle/Device.cs:505-507`). A Slang program can declare
sets 0 and 2 and not 1 (E10, the `[[vk::binding]]` case; also any Substrate
material that reserves a space). Collapsing "how many sets exist" and "what
their indices are" into one number is precisely how a caller ends up binding a
material's descriptor set at slot 1 of a layout whose SPIR-V expects slot 2.
`SetLayoutSlotCount` plus per-index lookup makes the gap explicit; the XML doc
says the caller fills gaps with one reusable empty `DescriptorSetLayout`.

**The walk**, verified end to end against SPIR-V (E10, E11):

```
Walk(structTypeLayout, absoluteSet):
  1. for s in 0 .. getDescriptorSetCount(structTypeLayout):
       vkSet = absoluteSet + getDescriptorSetSpaceOffset(structTypeLayout, s)
       for r in 0 .. getDescriptorSetDescriptorRangeCount(structTypeLayout, s):
         category PUSH_CONSTANT_BUFFER  -> a PushConstantRange
         otherwise                      -> DescriptorBinding into set vkSet:
             Slot  = getDescriptorSetDescriptorRangeIndexOffset(…, s, r)
             Count = getDescriptorSetDescriptorRangeDescriptorCount(…, s, r)
             Type  = MapBindingType(getDescriptorSetDescriptorRangeType(…, s, r))
  2. if structTypeLayout is a ParameterBlock element
       and GetSize(structTypeLayout, UNIFORM) > 0:
         emit UNIFORM_BUFFER at Slot 0 of absoluteSet          // E11 — reflection omits it
  3. for i in 0 .. getSubObjectRangeCount(structTypeLayout):
       br = getSubObjectRangeBindingRangeIndex(structTypeLayout, i)
       if getBindingRangeType(structTypeLayout, br) != SLANG_BINDING_TYPE_PARAMETER_BLOCK: continue
       blockTl = getBindingRangeLeafTypeLayout(structTypeLayout, br)
       childSet = absoluteSet + getSubObjectRangeOffset(structTypeLayout, i)
                                  .GetOffset(SUB_ELEMENT_REGISTER_SPACE)
       Walk(GetElementTypeLayout(blockTl), childSet)

entry: Walk(spReflection_getGlobalParamsTypeLayout(layout), 0)
```

Step 2 is deliberately not applied to the global scope: E11 shows Slang *does*
list the global implicit constant buffer as a `CONSTANT_BUFFER` range, so
applying it there would double-count binding 0.

**`DescriptorBinding.Stages` — the E3 gap is closed, with a switch.** Two modes,
because precision costs a compile (E13):

```csharp
public enum SlangStageAttribution { ProgramStageUnion, PerEntryPointUsage }
public SlangReflection GetReflection(SlangStageAttribution mode = SlangStageAttribution.ProgramStageUnion);
```

- `ProgramStageUnion` (default): every binding gets the union of the program's
  entry-point stages. Always valid, sometimes broader than necessary, never
  compiles anything, never throws.
- `PerEntryPointUsage`: one `getEntryPointMetadata` per entry point, then
  `isParameterLocationUsed(DESCRIPTOR_TABLE_SLOT, set, slot, …)` per binding per
  entry point; `Stages` is the OR of the stages that report `true`. A binding no
  entry point reports falls back to the union rather than to
  `ShaderStages.None` — `stageFlags = 0` is not a binding any stage can access.
  This mode compiles every entry point and therefore can throw
  `SlangCompilationException`; the XML doc says so.

`PushConstantRange.Stages` stays the program-stage union in **both** modes:
`isParameterLocationUsed` reports push constants as unused even when they
provably are (E13).

**Vertex attributes** are produced only for entry points whose stage is
`ShaderStages.Vertex`, and only from parameters (and struct fields, one level
down) whose type layout's parameter category is `VARYING_INPUT` — which excludes
`SV_InstanceID`, `SV_VertexID`, `SV_IsFrontFace` and `SV_Position` (E16).
`Location` accumulates parent offset + field offset. `MATRIX`-kind inputs throw
`NotSupportedException` naming the field (OPEN-6).

**One remaining mapping gap** (E3's second gap, unchanged by composition):
`VertexBindingDescription.{Slot, Stride, InputRate}` and
`VertexAttributeDescription.{Binding, Offset}` describe how the *application*
packs its vertex buffers, which the shader never states. `VertexAttributes`
returns values with `Binding`/`Offset` at their defaults and there is
deliberately no `VertexInputDescription` factory. Composition does not change
this — nothing in a composite adds information about the application's buffer
layout.

*Why not the alternatives.*
**Keep "space 0 only, throw on `ParameterBlock<T>`" and defer** — rejected: the
consumer's shader *is* the `ParameterBlock` case (§Problem 3); the deferral
would ship an API that throws on its only user.
**Use `spReflectionTypeLayout_getSubObjectRangeSpaceOffset` as the set index** —
rejected on measurement: it returns `0` for every sub-object range in a fixture
whose blocks are demonstrably at spaces 1 and 2 (E10).
**Treat the global scope as always occupying set 0 and start blocks at 1** —
rejected: false whenever the global scope has no descriptors (E10), which is the
natural Substrate shape.
**Use the loop index over `getDescriptorSetCount` as the Vulkan set number** —
rejected: `[[vk::binding(7, 2)]]` yields index 1 for set 2 (E10).
**Skip the synthesized `UNIFORM_BUFFER` at binding 0 of a block** — rejected: it
is what the SPIR-V binds, and omitting it produces a descriptor set layout that
does not match the shader (E11).
**Make `PerEntryPointUsage` the default** — rejected: it turns reflection into a
compile, gives it a failure mode it otherwise does not have, and a superset of
stages is always valid Vulkan. Precision is opt-in.
**A parallel `Slang*` description type set** — rejected, unchanged from the
original: reflection's value is that its output is the type
`Device.CreateDescriptorSetLayout` already takes.
**Deriving `Stages` by parsing the emitted SPIR-V ourselves** — rejected:
`IMetadata` answers the same question without a SPIR-V parser in this repo.

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

### D8. Composition — an explicit component list, in caller order

Phase 3a extends the Phase 2 compiler API rather than replacing it. `Compile`
(D4) stays as the one-module convenience path; composition gets first-class
types, because E12 shows the component list and its order are part of the layout
contract and therefore cannot be implicit.

```csharp
public sealed class SlangSession : IDisposable
{
    public SlangModule  LoadModule(string moduleName);                                   // via search paths
    public SlangModule  LoadModuleFromSource(string moduleName, string path, ReadOnlySpan<byte> source);
    public SlangProgram Compile(in SlangCompileRequest request);                         // unchanged (D4)
    public SlangProgramBuilder CreateProgram();
}

public sealed class SlangModule : IDisposable                 // wraps IModule
{
    public string Name { get; }
    public int    DefinedEntryPointCount { get; }
    public SlangEntryPoint DefinedEntryPoint(int index);
    public SlangEntryPoint FindEntryPoint(string name, ShaderStages stage);
}

public sealed class SlangEntryPoint : IDisposable             // wraps IEntryPoint
{
    public string       Name  { get; }
    public ShaderStages Stage { get; }
}

public sealed class SlangProgramBuilder                        // accumulates the component list
{
    public SlangProgramBuilder Add(SlangModule module);
    public SlangProgramBuilder Add(SlangEntryPoint entryPoint);
    public SlangProgramBuilder AddTypeConformance(string concreteType, string interfaceType);
    public SlangProgram Link();                                // composite -> link -> SlangProgram
}
```

`Add` order is the composite order is the binding-assignment order is the
entry-point index order (E12). The XML doc on `SlangProgramBuilder` states that
in one sentence, and `SlangProgram` exposes the linked component so the SPIR-V a
caller fetches and the reflection a caller reads come from the *same* linked
object — there is no path on which they can diverge.

*Why not the alternatives.*
**Extend `SlangCompileRequest` with a list of module names** — rejected: it hides
the ordering contract behind a record-struct field and gives the caller no
handle on an individual entry point, which `AddTypeConformance` and
per-entry-point SPIR-V both need.
**Auto-compose from the entry points alone** (`Add(entryPoint)` only, letting
Slang pull modules in as requirements) — rejected on measurement: it links, but
produces a *third* binding assignment different from either explicit ordering
(E12). Convenience that silently changes the layout is not convenience.
**Make `SlangProgramBuilder` a `ref struct`** — rejected: it holds refcounted
native components across statements and is setup-time; invariant #3 does not
apply here (§Problem).

### D9. Specialization — reflect after, conformance for interfaces, guard the crash

Scope discipline first: this spec designs **no permutation system and no
variant cache**. The issue puts runtime permutation workflows out of scope, and
nothing here caches, hashes or keys anything. What is in scope is the
correctness consequence E14 forces.

1. **Reflection is taken from the specialized, linked component.** Reflecting
   before specialization silently reports a descriptor set with zero bindings
   where the shader has five (E14). `SlangProgram.Reflection` is therefore only
   ever built from what `SlangProgramBuilder.Link()` returned, and
   `SlangProgramBuilder` has no "reflect the unlinked composite" path at all.
2. **Interface conformance is exposed; `IComponentType::specialize` is not, in
   Phase 3.** `AddTypeConformance` maps to
   `ISession::createTypeConformanceComponentType` (`slang.h:4626-4631`), which
   was verified to link and emit valid SPIR-V for the interface-typed
   `ParameterBlock` case. `specialize()` on that same case **segfaults the
   process** in Slang's type-legalization pass (E14, with a stack trace) — an API
   whose failure mode is SIGSEGV cannot ship behind a `try`.
3. **A pre-flight guard, because a crash must become an exception.** Should a
   later phase expose `Specialize`, it must first walk the *unspecialized*
   composite's layout and throw `NotSupportedException` naming the parameter if
   any global parameter's type-layout tree contains a node of kind
   `SLANG_TYPE_KIND_INTERFACE` (`slang.h:2125`). The guard is precise and
   testable: the crashing form reflects as element kind `INTERFACE`, the working
   `type_param` form reflects as `GENERIC_TYPE_PARAMETER` (`slang.h:2124`).
4. **The crash goes upstream.** A minimal repro exists; it should be filed
   against shader-slang/slang, and the issue number recorded next to the guard.

*Why not the alternatives.*
**Expose `Specialize` now and document "do not call it on interface-typed
blocks"** — rejected: documentation does not stop a SIGSEGV, and the consumer's
material system is exactly the code that would call it.
**Pin to an older or newer Slang to dodge the crash** — rejected without
evidence: only `v2026.14.1` was measured, and moving the pin to avoid an
unreported bug trades a known crash for an unknown set.
**Build a specialization/permutation API in this spec** — rejected: explicitly
out of scope in the issue, and it needs the compile-time story (D7's MSBuild
task) before it needs a runtime cache.

### D10. Sparse sets meet a positional API — state it, don't paper over it

`PipelineLayoutDescription.SetLayouts` is positional
(`PipelineLayoutDescription.cs:9`). A reflected program can leave set indices
unused (E10). `SlangReflection.SetLayoutSlotCount` is the length the caller must
allocate; `TryGetSet(i, …)` returns `false` for a gap, and the documented recipe
is one empty `DescriptorSetLayout` reused for every gap. No API is added to
`src/Ahjo.Vulkan/` for this — `DescriptorSetLayoutDescription` with an empty
`Bindings` span already produces exactly that layout.

*Why not the alternatives.*
**Add a `PipelineLayoutDescription` overload keyed by set index** — rejected:
it is a change to `src/Ahjo.Vulkan/` for one producer's convenience, and D5's
whole premise is that reflection adapts to the existing types.
**Renumber the reflected sets to be dense** — rejected outright: the set numbers
are baked into the SPIR-V. Renumbering would produce a layout that compiles and
then binds to the wrong slots at draw time, which is the exact bug class #166
exists to eliminate.

---

## Phases and what can ship independently

| Phase | Deliverable | Ships alone? |
|---|---|---|
| 1 | `Ahjo.Vulkan.Slang.Native`: `.rsp`, generated bindings, archive acquisition + staging + pack, `build-slang-native.yml`, native smoke + drift tests | **Yes.** A published raw-binding package, exactly like `Ahjo.Vulkan.Ktx.Native` was. **Unchanged by this revision.** |
| 2 | `Ahjo.Vulkan.Slang`: `SlangCompiler`/`SlangSession`/`SlangProgram`, diagnostics, `AotSmoke` coverage | **Yes.** Compile-to-SPIR-V is useful without reflection. |
| **3a** | **Composition (D8/D9):** `SlangModule`, `SlangEntryPoint`, `SlangProgramBuilder`, `AddTypeConformance`, ordering contract | **Yes.** Composing and linking an N-module program to SPIR-V is useful before any reflection exists, and it is what Phase 3b must reflect *over*. |
| **3b** | **Reflection (D5/D10):** the recursive `ParameterBlock` walk, sparse set indices, synthesized block uniform buffer, push constants, `SlangStageAttribution`, vertex attributes | Requires 3a. |

**Why Phase 3 splits.** The original Phase 3 was "walk space 0 and map four
range types". The revision adds a whole component-composition surface (three new
public types plus an ordering contract, D8), a recursive multi-space walk with
two derivations reflection does not expose directly (E10, E11), an opt-in
compiling mode (E13), a specialization posture with a native-crash guard (D9),
and a positional-API mismatch to document (D10). That is two reviewable diffs,
not one — and 3a is independently useful, which is the repo's own bar for a
phase boundary. 3a touches no `Pipelines/` type; 3b touches no `IComponentType`
plumbing. Splitting also keeps the `vulkan-validation-reviewer` pass on 3b
focused on binding correctness rather than on lifetime plumbing.

---

## OPEN

**OPEN-1 — ~~does `slang-glslang` ship?~~ — RESOLVED 2026-08-01 by human
decision; shipped in `f646463`.** Yes, on both RIDs, unrenamed on Linux. Without
it every `SlangOptimizationLevel` above `None` returns `SLANG_OK` and
byte-identical SPIR-V (317 words at all four levels) while reporting
`failed to load downstream compiler 'spirv-opt'` in the diagnostics blob of a
*successful* call; with it, 317 / 313 / 245 / 245 words at
`None` / `Default` / `High` / `Maximal` and no diagnostic at any level (E6).
Cost as measured: ~14 MB `win-x64` / ~17 MB `linux-x64`, package total
~24.6 MB → ~31 MB.

Two things this spec predicted wrongly, recorded because whoever bumps
`SlangVersion` next will face the same question:

- **The decision procedure was written to detect a failure that never
  happens.** It said: if any level *fails* with `failed to load downstream
  compiler 'spirv-opt'`, stop and confirm. No level ever failed. An absent Slang
  downstream compiler does not fail the call — it makes the option do nothing
  and puts the reason in text the caller has to go looking for. Compare emitted
  bytes across settings; do not check result codes.
- **The original test could not have caught it, and neither could its
  fixture.** "Valid SPIR-V at every level" passed for a whole phase over a
  setting that did nothing, and the trivial shader it compiled emitted identical
  words with or without the optimizer. Both were replaced:
  `Optimization_ChangesTheEmittedSpirv` requires `Maximal` to be strictly
  smaller than `None` on a deliberately redundant shader, and
  `OptimizationLevels_ReachTheDownstreamCompiler` requires the `spirv-opt`
  diagnostic to be absent. Withholding the binary now fails 4 of the 5
  optimization tests.

**OPEN-2 — ~~multi-descriptor-set reflection~~ — RESOLVED 2026-08-01.** Settled
empirically in E10 against SPIR-V ground truth: the set index is
`spReflectionVariableLayout_GetOffset(param, SLANG_PARAMETER_CATEGORY_SUB_ELEMENT_REGISTER_SPACE)`,
accumulated down the nesting chain from a global-scope base of 0.
`getSubObjectRangeSpaceOffset` was the wrong function and returns 0 for every
range. Specified in D5; no `NotSupportedException` guard for `ParameterBlock<T>`
remains, and none should be added.

**OPEN-3 — ~~`SlangCompiler` lifetime and `slang_shutdown`~~ — RESOLVED
2026-08-01 in the plan's direction.** `SlangCompiler.Dispose()` releases the
global session and does **not** call `slang_shutdown()`
(`src/Ahjo.Vulkan.Slang/SlangCompiler.cs:19`, `:160`); a second
`SlangCompiler.Create()` in the same process is legal, verified by
`TwoCompilers_InSequence_Work`
(`tests/Ahjo.Vulkan.Slang.Tests/SlangCompilerTests.cs:280`), which creates and
disposes two compilers in sequence and passes. `slang_shutdown` stays uncalled:
it is process-scoped (`slang.h:5860`), and a library has no standing to end the
process's use of Slang on behalf of its host.

**OPEN-4 — the `specialize()` segfault (E14) — DECIDED 2026-08-01, one
sub-question still open.** `IComponentType::specialize` on a component whose
global scope contains an interface-typed `ParameterBlock`, followed by any
codegen call, crashes inside Slang's type-legalization pass — reproduced 3/3
with a stack trace, on `v2026.14.1` linux-x64.

- **(a) Decided by a human: ship 3a without `Specialize`, exposing
  `AddTypeConformance`.** Do not block on an upstream fix.
  `SlangProgramBuilder` (`f646463`) has `AddTypeConformance` and no
  `Specialize`, and carries the reasoning in its XML doc so the omission reads
  as a decision. Filing the repro upstream stays a follow-up (see below), not a
  gate.
- **(b) Still genuinely open: does the crash reproduce on `win-x64`?** It was
  measured on Linux only and the Windows lane has no equivalent probe. Nothing
  in the shipped code depends on the answer — `Specialize` is not exposed on
  either RID — but D9 rule 3's pre-flight guard cannot be sized without it, so
  anyone proposing a `Specialize` API must answer this first.

**OPEN-5 — more than one push-constant block (E17).** Two modules each declaring
`[[vk::push_constant]]` compose and link, and reflection reports two
`PUSH_CONSTANT` ranges — but the only offset it exposes is a buffer *index*
(0, 1), not the byte offset `VkPushConstantRange.offset` needs. Phase 3b emits
one `PushConstantRange` for the single-block case and throws
`NotSupportedException` naming both parameters when it sees two. Do not guess a
byte offset. If the consumer needs two blocks, that is its own investigation.

**OPEN-6 — `MATRIX`-kind vertex inputs (E16).** A `float4x4` vertex input
occupies `GetSize(typeLayout, VARYING_INPUT)` = 4 consecutive locations and
SPIR-V decorates it at the base location, but which scalar count each of those
locations carries depends on `SessionDesc.defaultMatrixLayoutMode`, and only
column-major was probed. Phase 3b throws `NotSupportedException` naming the
field. Settling it means probing both layout modes and reading the SPIR-V type
of each per-location input variable.

---

## Follow-ups this spec does not do

- **Replace the samples'/tests' `glslc` `Exec` with a Slang MSBuild task**
  (relates to #162). Should become its own issue after Phase 2 — it is a build
  system change with its own failure modes (task host, incremental inputs,
  design-time builds) and it is what finally deletes the eight duplicated
  `_GlslcExe` blocks and lets `ci.yml:212` stop printing "NOT PROVEN".
- **File the `specialize()` segfault upstream** (E14, OPEN-4). A minimal repro
  exists: `ParameterBlock<ISurface>` + `IComponentType::specialize(Concrete)` +
  `link` + `getEntryPointCode` on the consuming entry point. It is not this
  spec's job to fix Slang, but it is this spec's job to record that D9 exists
  because of it, so a future `Specialize` API is not added by someone who only
  read the header.
- **Migrate the existing GLSL sample shaders to Slang.** Should become its own
  issue after that one. It is a content change with a per-sample visual diff to
  review, it is not all-or-nothing (Slang can consume GLSL), and doing it before
  the build task exists would mean hand-running `slangc` — trading one unpinned
  external invocation for another.
