# Shaders built by the pinned Slang: one shared MSBuild step (#171) and the sample migration to Slang (#172)

- Issues: #171 — "Build: replace the eight duplicated glslc Exec blocks with a Slang MSBuild task"; #172 — "Samples: migrate the GLSL sample shaders to Slang"
- Plan: [`../plans/2026-11-06-issue-171-172-slang-shader-build.md`](../plans/2026-11-06-issue-171-172-slang-shader-build.md)
- Builds on: [#166 Slang support](2026-08-01-issue-166-slang-support-design.md) (D3 pinned + hashed archive, D7 "MSBuild task later", E19 `SV_VertexID` → `DrawParameters`, *Follow-ups this spec does not do*)
- Must land consistently with: #168 (decided: the wrapper does **not** default `shaderDrawParameters`; each sample opts in through `ConfigureFeatures`), #162 (closed as superseded by #171), #32 / `.github/CLAUDE.md` (wrapper tests are Windows-only), the in-flight Slang bump to `v2026.21` (separate branch; this design reads `$(SlangVersion)` and depends on nothing version-specific)
- Date: 2026-11-06

**One PR, two parts.** The maintainer asked for both issues in a single PR. Part A
(#171) is the build step. Part B (#172) is the content migration. Part B depends
on Part A. Part A does not depend on Part B, except for one forced translation:
Slang's GLSL frontend cannot compile the mesh fixtures, so those move to Slang in
Part A (E5). Each part has its own decision and alternatives. They share one
evidence section, because every measurement below was taken with the pinned
compiler and applies to both parts.

**Method.** Every compiler measurement below was taken with the pinned archive.
`slang-2026.19-windows-x86_64.zip` has SHA-256 `fc922f21…6f8f33`, which matches
`Directory.Build.props:47`. It was extracted to a scratch directory. The
version-sensitive measurements (E3–E6) were repeated on the in-flight pin
`v2026.21`, whose archive hash `4b6f509e…86b8de` matches that branch's
`Directory.Build.props`, and gave identical results. SPIR-V was inspected with
`spirv-dis` / `spirv-cross` / `spirv-val` from Vulkan SDK 1.4.363.0. Those three
are **verification tools only** and appear nowhere in the design.

---

## Problem

**1. The shader compiler is the only unpinned native input, and its failure is
a warning.** #171 listed 8 projects. The current count is **9 projects with 11
`glslc` targets**:

| Project | Targets | Lines |
|---|---|---|
| `samples/AotSmoke/AotSmoke.csproj` | `CompileShaders` (+ `CopyShadersToPublish`) | 66-75, 84-89 |
| `samples/HeadlessExport/HeadlessExport.csproj` (missing from #171's list) | `CompileShaders` | 23-32 |
| `samples/HeadlessTriangle/HeadlessTriangle.csproj` | `CompileShaders` | 24-33 |
| `samples/HelloCube/HelloCube.csproj` | `CompileShaders` | 28-37 |
| `samples/HelloTriangle/HelloTriangle.csproj` | `CompileShaders` | 26-35 |
| `samples/HelloVma/HelloVma.csproj` | `CompileShaders` | 24-33 |
| `samples/HelloVmaWindowed/HelloVmaWindowed.csproj` | `CompileShaders` | 24-33 |
| `tests/Ahjo.Vulkan.Tests/Ahjo.Vulkan.Tests.csproj` | `CompileShaders`, `CompileMeshShaders` | 51-60, 83-92 |
| `tests/Ahjo.Vulkan.Benchmarks/Ahjo.Vulkan.Benchmarks.csproj` | `CompileShaders`, `CompileMeshShaders` | 35-44, 67-76 |

`HelloDlaa.csproj:23-26` and `HelloRayQuery.csproj:17-21` mention glslc in
comments only. Every one of the 11 targets resolves `$(VULKAN_SDK)\Bin\glslc.exe`
or a bare `glslc` on `PATH`. Every one runs with
`ContinueOnError="WarnAndContinue"`. So an absent compiler produces `MSB3073`
warnings and a build with no `.spv` files that still reports green. Every other
native input is pinned in `Directory.Build.props` (`:18`, `:23`, `:28`,
`:46-48`, `:66`).

**2. CI cannot see the result.** `ci.yml:263` prints
`"spirv" { "NOT PROVEN — glslc absent (follow-up)" }`. The `[gate:spirv]` class
is `TestGate.RequireSpirv` (`tests/Shared/TestGate.cs:57-61`). It skips when a
`.spv` file is missing. There are **57 call sites in 8 files**, all in
`tests/Ahjo.Vulkan.Tests`:

| File | Calls |
|---|---|
| `CommandRecorderTests.cs` | 14 |
| `GraphicsPipelineTests.cs` | 18 |
| `MeshShaderTests.cs` | 15 |
| `ComputePipelineTests.cs` | 3 |
| `DepthClampTests.cs` | 2 |
| `ShaderModuleTests.cs` | 2 |
| `DescriptorTemplateTests.cs` | 1 |
| `DescriptorWriteTests.cs` | 1 |
| `SpecializationInfoTests.cs` | 1 |

This count comes from a grep for `RequireSpirv`. The `LSP` `findReferences` call
returned nothing in this worktree, which was not indexed. #171 and #162 said
"~36"; that figure is stale.

Every one of these call sites comes after `TestGate.RequireDriver()`. The
Windows lane declares `AHJO_VULKAN_TIER=none` (`ci.yml:45`). So the driver gate
always fires first, and the `spirv` count reads 0 whatever the toolchain does.
#162 documented this masking. A design that only changes the summary text proves
nothing; it needs a test that runs **without** a driver.

**3. The samples are GLSL, and the repo's shader language is now Slang.**
These GLSL sources are migrated by #172:

| Sample | Sources |
|---|---|
| HeadlessTriangle | `triangle.vert`, `triangle.frag` |
| HeadlessExport | `triangle.vert`, `triangle.frag` |
| HelloTriangle | `triangle.vert`, `triangle.frag` |
| HelloCube | `cube.vert`, `cube.frag` |
| HelloVma | `vma.vert`, `vma.frag` |
| HelloVmaWindowed | `vma.vert`, `vma.frag` |

That is 6 samples and 12 files. Three samples are already Slang and compile at
run time: AotSmoke, HelloDlaa and HelloRayQuery.

---

## Evidence

### E1. The pinned archive already contains a working `slangc`. The staged set deliberately does not.

`bin/slangc.exe` is 276 480 bytes in the 2026.19 win-x64 archive. On win-x64,
`slangc` runs from a directory that contains only `slangc.exe`, `slang.dll`,
`slang-compiler.dll` and `slang-glslang.dll`. The test ran with `PATH` reduced to
`System32`, so the Vulkan SDK's own `slang-glslang.dll` could not be picked up
(the hijack recorded at `src/Ahjo.Vulkan.Slang.Native/CLAUDE.md:36`).
`slang-glsl-module.dll` is **not** needed for GLSL input: the output was
byte-identical to a full-archive run (`cmp`). `slangc -v` prints `2026.19`, the
pin's `_SlangVersionNumber`.

`StageSlangBinaries` excludes `bin/` on purpose
(`Ahjo.Vulkan.Slang.Native.csproj:261-262`: "We call the library, not slangc").
The staged directory is the shipped artifact and the CI cache key
(`ci.yml:135-139`, `build-slang-native.yml:77-82`). On a cache hit,
`native/slang/downloaded/` does not exist at all.

On linux-x64, measured on the **2026.14.1** extraction (the only Linux one on
the box), `bin/slangc` has the following dynamic section:

- `NEEDED`: `libslang-compiler.so.0.<ver>`
- `RUNPATH`: `$ORIGIN/../lib:$ORIGIN`

The staged copy of that library is renamed to `libslang.so`
(`csproj:281-284`), so `slangc` placed beside the staged set would not load. A
directory holding `slangc`, `libslang-compiler.so.0.<ver>` and
`libslang-glslang-<ver>.so` satisfies the `$ORIGIN` entry.

### E2. A failed compile can be made an MSBuild error, with the compiler's text.

Measured in a scratch MSBuild project with `dotnet msbuild` (SDK
11.0.100-rc.3):

- `slangc` exits **-1** on an error. Through `Exec`, the
  `IgnoreExitCode="true"` + `ConsoleToMSBuild="true"` + `<Error>` pattern
  produced `bad.task : error AHJOSLANG001: …` followed by the full
  `error[E30015] … --> bad.task:11:5` text, and the build failed.
- No output file was written for the failed input.
- Slang's default diagnostic format (`error[E…]:` then ` --> file:line:col`) does
  **not** match MSBuild's canonical-error regex. That is why the design reports
  through `<Error>` rather than relying on `Exec`'s parser.
- `-diagnostic-format vs`, which would match the regex, exists in 2026.21 and
  **not** in 2026.19. Depending on it would tie the design to the bump.
- `-warnings-as-errors all` turns a warning (`E30081`) into a failing exit.
  `-profile spirv_1_0` emits `warning[E50011] SPIR-V version too old`, so the
  profile must be pinned explicitly to a level the warning accepts.

### E3. GLSL through Slang keeps `gl_VertexIndex`'s capability set. The Slang translation does not.

| Input | Capabilities |
|---|---|
| `tests/…/Shaders/triangle.vert` via Slang's GLSL frontend | `Shader` |
| `tests/…/Shaders/near_plane_tri.vert` via Slang's GLSL frontend | `Shader` |
| `samples/AotSmoke/Shaders/triangle.slang` (`SV_VertexID`) | `Shader`, **`DrawParameters`** |

This is #166 E19, reproduced on both 2026.19 and 2026.21. The remaining
translated samples were measured too. `cube.slang`, `vma.slang` and HelloVmaWindowed's
`vma.slang` use vertex **inputs**, not `SV_VertexID`, and declare `Shader` only.
So `DrawParameters` appears in exactly the three triangle samples: HeadlessTriangle,
HeadlessExport and HelloTriangle.

### E4. Matrix layout differs silently between Slang's two frontends.

Decompiling `cube.vert` with `spirv-cross` gives:

| Compile | Decompiled |
|---|---|
| glslc (today) | `mat4 mvp;` `pc.mvp * v` |
| Slang, GLSL input, default | `layout(row_major) mat4 mvp;` `pc.mvp * v` — **transposed**, so it does not match glslc |
| Slang, GLSL input, `-matrix-layout-row-major` | `mat4 mvp;` `pc.mvp * v` — matches glslc |
| Slang, `.slang` `float4x4` + `mul(M, v)`, default | `layout(row_major) mat4;` `v * M` — matches glslc |
| Slang, `.slang` `float4x4` + `mul(M, v)`, `-matrix-layout-row-major` | `mat4;` `v * M` — **transposed** |
| Slang, `.slang` `row_major float4x4` + `mul(v, M)`, default | `mat4 mvp;` `pc.mvp * v` — matches glslc, the same text as the glslc decompile |

Slang's flag naming is inverted relative to GLSL. So one repo-wide flag
transposes one of the two languages, and neither validation nor `spirv-val`
notices: the module is valid, it just draws wrong.

The run-time path (`SlangCompiler`) sets no matrix layout (grep shows no
`MatrixLayout` setter in `src/Ahjo.Vulkan.Slang/`). The repo's existing Slang
convention is explicit in the source instead: `samples/HelloDlaa/Shaders/cube.slang:9-23,60-65`
declares `row_major float4x4` and writes `mul(v, m)` "to match
System.Numerics". The last row above shows that this convention also reproduces
glslc's semantics exactly, with no compiler flag.

### E5. Slang's GLSL frontend cannot compile the mesh fixtures.

`mesh_tri.mesh` fails with `error[E31217]: unrecognized GLSL layout qualifier`
on `layout(triangles, max_vertices = 3, max_primitives = 1) out;`.
`mesh_tri.task` fails with `error[E30015]: undefined identifier 'EmitMeshTasksEXT'`.
Both fail on 2026.19 and on 2026.21.

A Slang translation compiles cleanly under `-warnings-as-errors all`. It uses
`[outputtopology("triangle")] [numthreads(1,1,1)]`,
`out vertices` / `out indices`, `SetMeshOutputCounts`, and `DispatchMesh` with a
payload. Its output:

- declares `MeshShadingEXT` + `Shader` and `SPV_EXT_mesh_shader`;
- uses plain `OpExecutionMode LocalSize 1 1 1`. That is the property the
  `--target-env=vulkan1.2` comment at `Ahjo.Vulkan.Tests.csproj:63-78` was
  protecting, so it holds without that flag;
- keeps `[vk::constant_id(0)] uScale` as `SpecId 0`;
- names both entry points `"main"`;
- passes `spirv-val --target-env vulkan1.3`.

So the mesh fixtures **must** be translated, and that translation belongs to
Part A.

### E6. Slang emits `LocalSizeId` for a spec-constant workgroup size, at every profile.

`spec_fill.comp` (`layout(local_size_x_id = 0) in;`) compiles to
`OpExecutionModeId %main LocalSizeId %29 %int_1 %int_1` at every profile tried
(`spirv_1_0`, `1_3`, `1_5`, `1_6`, `glsl_450`). It does so on 2026.19 and
2026.21. The spec constant's default is `OpSpecConstant %int 0`, where glslang
emits 1.

`VUID-RuntimeSpirv-LocalSizeId-06434` makes `LocalSizeId` conditional on
`maintenance4`. That is the same constraint the mesh comment worked around. The
wrapper's default feature set does not set `maintenance4`
(`src/Ahjo.Vulkan/Lifecycle/PhysicalDevice.cs:788-826`). The `f13` block sets
only `synchronization2` and `dynamicRendering`.

`maintenance4` is a Vulkan 1.3 feature listed as required for 1.3 conformance in
the spec's *Feature Requirements* chapter. This is quoted from the spec text and
was **not re-measured** here. The plan has a step that reads it back on the
verification device.

Both consumers of `spec_fill.comp` specialize id 0 before building:

- `SpecializationInfoTests.cs:196-198` uses 32 and 8;
- `SpecializationInfoBenchmarks.cs:59-64` uses 64.

So the default of 0 is never used.

### E7. The other GLSL fixtures compile unchanged.

The following compile through Slang's GLSL frontend with exit 0 on **both** 2026.19 and 2026.21. Each was run with the exact
final argument string,
`-target spirv -profile spirv_1_5 -entry main -matrix-layout-row-major -warnings-as-errors all -diagnostic-color never`:

- `triangle.vert`, `triangle.frag`, `near_plane_tri.vert` (`tests/Ahjo.Vulkan.Tests/Shaders/`);
- `fill.comp`, `spec_fill.comp`;
- `cube.vert/.frag`, both samples' `vma.vert/.frag`;
- a `matrix_push.vert` stand-in. It emits `ColMajor`.

The test fixtures were also checked from the four-file directory. Each declares
`Shader` as its only capability.

Every Slang source the plan prescribes compiles clean under
`-profile spirv_1_5 -warnings-as-errors all -diagnostic-color never` on both
versions:

- `cube` and both `vma` translations, in the `row_major` + `mul(v, m)` style;
- `mesh_tri` `meshMain` and `taskMain`.

HelloVmaWindowed's `row_major` + `mul(v, m)` vertex shader decompiles to
`mat4 transform; u.transform * v`, which is glslc's form.

Every module names its entry point `"main"`, which matches
`GraphicsPipelineBuilder`'s `InitMain` default (`GraphicsPipelineBuilder.cs:171`).
So `-fvk-use-entrypoint-name` is **not** wanted. No shader in the repo uses
`#include` or `import`; the sources listed in Problem 3 and above are the whole
inventory.

### E8. MSBuild wiring facts, measured.

These were measured in a scratch two-project build on the repo's SDK band:

- A `ProjectReference` with `ReferenceOutputAssembly="false"` **still copies**
  the referenced project's `CopyToOutputDirectory` items into the referencing
  project's output.
- Adding `Private="false"` stops that copy.
- In both cases, the referenced project's `BeforeTargets="AssignTargetPaths"`
  target runs **before** the referencing project builds.

So a `ReferenceOutputAssembly="false" Private="false"` reference to
`Ahjo.Vulkan.Slang.Native` does two things. It orders `FetchSlang` ahead of the
consumer through the normal project graph, which is race-free under `-m`. And it
does not copy `slang-compiler.dll` (26 MB) into every sample.

### E9. The repo already has every pattern this needs.

- **Hash-verified staging with existence-as-cache:** `FetchSlang` and
  `_SlangStageNeeded` (`csproj:97`, `177-306`).
- **A target that writes a file and registers it in `FileWrites`:**
  `WritePinnedSlangVersion` (`tests/Ahjo.Vulkan.Slang.Tests/Ahjo.Vulkan.Slang.Tests.csproj:41-65`).
- **`ConfigureFeatures` pushing `VkPhysicalDeviceVulkan11Features`:**
  `samples/AotSmoke/Program.cs:312-333` and `src/Ahjo.Vulkan.Slang/README.md:530-575`.
  `f13` is handed out by ref, so `maintenance4` needs no `chain.Push`.

### E10. Slang's GLSL frontend lays push constants out std140 unless told std430.

Added during implementation review. Measured on 2026.19 with the
GLSL-item argument string from E7, on a push block
`layout(push_constant) uniform Pc { float arr[2]; float c; } pc;`, where the
two layouts disagree:

| Compile | `arr` ArrayStride | `c` Offset |
|---|---|---|
| glslc | 4 | 8 |
| Slang, default | 16 | 32 |
| Slang, `-fvk-use-gl-layout` | 16 | 32 |
| Slang, `layout(push_constant, std430)` | 4 | 8 |

`slangc -h` lists no std430 or push-constant layout option.
`-fvk-use-scalar-layout` gives scalar layout, which is not std430. Slang names
the block's struct `…_std140` or `…_std430`.

**Source rule.** Every GLSL push-constant block is written
`layout(push_constant, std430)`. `fill.comp` and `matrix_push.vert` carry it.
`CompiledShaderTests` checks two things:
- `push_std430.vert`'s offsets;
- that no compiled module has a `_std140` push-constant struct.

### E11. A bare `local_size_x_id` defaults to 0 under Slang.

Added during implementation review, measured on 2026.19 on `spec_fill.comp`.
glslc emits `OpSpecConstant %uint 1` for the same source.

| Source form | Slang output |
|---|---|
| `layout(local_size_x_id = 0) in;` | `OpSpecConstant %int 0`, `SpecId 0`, `LocalSizeId` |
| `layout(local_size_x_id = 0, local_size_x = 1) in;` | spec constant dropped; fixed `LocalSize 1 1 1` |
| `layout(local_size_x = 1, local_size_x_id = 0) in;` | `OpSpecConstant %int 0` (default unchanged) |
| `layout(constant_id = 0) const uint k = 1u;` + `layout(local_size_x_id = 0) in;` | `OpSpecConstant %uint 1`; fails `spirv-val` (uint constituent in the `ivec3` workgroup-size `OpCompositeConstruct`) |
| `layout(constant_id = 0) const int k = 1;` + `layout(local_size_x_id = 0) in;` | `OpSpecConstant %int 1`, `SpecId 0`, `LocalSizeId`; passes `spirv-val --target-env vulkan1.3`; glslc also accepts it |

**Source rule.** A `local_size_x_id` workgroup size declares its default with
an `int` `constant_id` constant. `spec_fill.comp` does this, and
`CompiledShaderTests` pins the default at 1.

---

## Part A — #171: one shared shader step that runs the pinned `slangc`

### Decision

**A1. Mechanism: `Exec` the pinned `slangc` from one shared `.targets` file.**

- The targets file is `tools/msbuild/Ahjo.Vulkan.SlangShaders.targets`.
- A new repo-root `Directory.Build.targets` imports it.
- The step does nothing in any project that declares no `SlangShader` items.
- `tools/` is used because `.gitignore` ignores `[Bb]uild/`.

**A2. Binary: a non-shipped tool directory, `native/slang/tool/<rid>/`, staged
from the same hash-verified archive.**

- `Ahjo.Vulkan.Slang.Native.csproj` gains a `StageSlangCompiler` target in the
  `FetchSlang` chain.
- It copies:
  - win-x64: `bin/slangc.exe`, `slang.dll`, `slang-compiler.dll`,
    `slang-glslang.dll`;
  - linux-x64: `bin/slangc`, `lib/libslang-compiler.so.0.<ver>`,
    `lib/libslang-glslang-<ver>.so`, flat, with no rename; then `chmod +x`.
- It runs `slangc -v` and fails unless the output equals
  `$(_SlangVersionNumber)`.
- It writes `slangc.version` with that value.

`_SlangToolNeeded` is true when `slangc` is missing, when the stamp is missing,
or when the stamp differs from the pin. The stamp is what makes a `SlangVersion`
bump re-stage the tool on Windows, where the file names carry no version.
Download and extract run when either `_SlangStageNeeded` or `_SlangToolNeeded`
is true.

The shipped set, the pack list (`csproj:346-375`) and the uploaded artifact
(`native/slang/staged/<rid>/`) are **unchanged**. `staged/` is what ships;
`tool/` is what builds.

- **Version independence.** Paths derive from `_SlangVersionNumber`, and the
  checks are against `$(SlangVersion)`. No flag used here is 2026.21-only (E2).
- **Ordering.** The shared targets add
  `<ProjectReference Include="…Ahjo.Vulkan.Slang.Native.csproj" ReferenceOutputAssembly="false" Private="false" />`
  to any project with `SlangShader` items (E8).
- **Pre-existing gap, not fixed.** The *staged runtime* DLLs on Windows have the
  same "stale after a bump" gap, and it predates this design. It is recorded
  here, and the plan names it as a follow-up, filed as #256.

**A3. Item contract.**

```xml
<!-- GLSL: entry 'main', output %(Filename)%(Extension).spv — every existing .spv name is kept -->
<SlangShader Include="Shaders\*.vert;Shaders\*.frag;Shaders\*.comp" />
<!-- Slang: one item per entry point; OutputName is required -->
<SlangShader Include="Shaders\triangle.slang" EntryPoint="vertexMain"   OutputName="triangle.vert.spv" />
<SlangShader Include="Shaders\triangle.slang" EntryPoint="fragmentMain" OutputName="triangle.frag.spv" />
```

**The language is inferred from the extension.** `.slang` is Slang; any other
extension is GLSL.

- **Arguments for every item:**
  `-target spirv -profile $(SlangShaderProfile) -entry %(EntryPoint) -warnings-as-errors all -diagnostic-color never -o <out> <src>`.
  `SlangShaderProfile` defaults to `spirv_1_5`, which matches `SlangCompiler.cs:90`.
  So build-time and run-time compiles of the same `.slang` use the same
  profile. Other session defaults, such as the optimization level, were **not**
  compared, and the design does not claim byte-identical output.
- **GLSL items only:** `-matrix-layout-row-major` (E4), which restores glslc's
  column-major meaning.
- **Slang items:** no matrix flag. They follow HelloDlaa's
  `row_major float4x4` + `mul(v, m)` source convention.

**A4. Build behaviour.**

- **Where outputs go.** Output is written to
  `$(IntermediateOutputPath)Shaders\<OutputName>`. It is registered as
  `None` with `Link="Shaders\<OutputName>"`, `CopyToOutputDirectory` and
  `CopyToPublishDirectory` set to `PreserveNewest`, plus `FileWrites`. As a
  result:
  - the binary-relative `Shaders/<name>.spv` paths in about 30 C# call sites
    are unchanged;
  - `dotnet publish` carries the shaders;
  - AotSmoke's `CopyShadersToPublish` hack is no longer needed.
- **Incremental builds.** The compile target batches per output with
  `Inputs` = source + `slangc` + a stamp. The stamp holds `$(SlangVersion)`,
  the profile and the flag set, and is written with `WriteOnlyWhenDifferent`.
  The output is deleted before each compile, so a failure cannot leave a stale
  file that satisfies the up-to-date check.
- **Failure is an error.**

  | Code | When |
  |---|---|
  | `AHJOSLANG001` | the compiler fails; the message is the compiler's own text, attributed to the source file (E2) |
  | `AHJOSLANG002` | `slangc` is missing (unsupported host RID, or `SkipSlangNativeFetch=true`) — an error, per OPEN-1 (decided) |
  | `AHJOSLANG003` | a `.slang` item has no `OutputName` |
  | `AHJOSLANG004` | two items share an `OutputName` |

  Nothing uses `ContinueOnError`.
- **Design-time builds.** Every target in the file is conditioned on
  `'$(DesignTimeBuild)' != 'true'`. IDE load never runs `slangc`.
- **Hook.** `BeforeTargets="AssignTargetPaths"`, the same hook `FetchSlang` uses.
  It runs after `ResolveProjectReferences`, so the staged tool exists, and before
  copy-to-output is computed.

**A5. Test and benchmark fixtures stay GLSL and compile through the new step.
The two mesh fixtures are the exception.**

- `mesh_tri.mesh` and `mesh_tri.task` become one `mesh_tri.slang` with
  `meshMain` and `taskMain`. The output names are unchanged:
  `mesh_tri.mesh.spv` and `mesh_tri.task.spv` (E5).
- `SpecializationInfoTests`' device and `SpecializationInfoBenchmarks`' device
  enable `f13.maintenance4 = 1` through `ConfigureFeatures` (E6).

**Why GLSL stays.** These are fixtures for `Ahjo.Vulkan`, not for a shader
language. Translating `triangle.vert` and `near_plane_tri.vert` would add
`DrawParameters` (E3). That would force `shaderDrawParameters` into every device
helper in the suite, including the validation-asserting ones
(`DepthClampTests.cs:56`), and gain nothing in wrapper coverage.

**A6. "Proven on CI" means a driverless test, not a relabelled row.**

- **The test.** New `tests/Ahjo.Vulkan.Tests/CompiledShaderTests.cs` with no
  `TestGate`. It reads the build's own output and asserts:
  - every fixture exists and carries the SPIR-V magic;
  - every entry point is named `main`;
  - the GLSL vertex fixtures declare no `DrawParameters`;
  - the mesh fixtures declare `MeshShadingEXT`;
  - `spec_fill` uses `LocalSizeId`, which pins *why* the `maintenance4` enable
    exists;
  - a new GLSL fixture `matrix_push.vert` carries `ColMajor` and no
    `RowMajor`, which guards E4's flag.

  It runs at tier `none`, so it executes on CI today.
- **`TestGate.RequireSpirv`.** Under OPEN-1's decided answer, the build now
  guarantees the `.spv` files or fails, so `RequireSpirv` and its 57 call sites
  are deleted.
- **`ci.yml`.** The coverage summary drops the `spirv` class from `$needs`
  (`:238`), from `$gap` (`:258`) and from the meaning switch (`:263`). It gains a
  **SPIR-V fixtures** line that counts `CompiledShaderTests` results in
  `wrapper.trx` and **fails the step if that count is 0**. The `build-test`
  cache (`:135-139`) adds `native/slang/tool/win-x64`, and
  `build-slang-native.yml:77-82` adds `native/slang/tool/${{ matrix.rid }}`.
  The two cache entries share a key, so their path lists must stay identical;
  otherwise the slang-native lane re-downloads 77 MB on every cache hit.
- **Docs.** `docs/ci-coverage.md:52,217` and `tests/CLAUDE.md` follow.

**A7. AOT.** The step runs only at MSBuild time and puts no code in any
assembly, so invariant #2 does not apply to it. AotSmoke loses its dead GLSL
block (`AotSmoke.csproj:53-89`). That block existed only because #166 left the
glslc deletion to this issue: the comment at `:53-60` says so, and `Program.cs`
no longer reads those `.spv` files. AotSmoke keeps compiling `triangle.slang` at
run time, because that is the ILC proof for `Ahjo.Vulkan.Slang`.

So the AOT lane does **not** exercise the build step. Nothing about the step
could break under AOT, so that is correct. Two things still have to hold:

- **`AotSmoke` publish stays clean with the root `Directory.Build.targets`
  imported.** It has no `SlangShader` items, so no reference is added and no
  target does anything.
- **A non-AOT `dotnet publish -r win-x64` of a build-time-shader sample carries
  `Shaders/*.spv`.** This is a plan verification step.

### Why not the alternatives (Part A)

- **An in-process MSBuild task assembly hosting `SlangCompiler`** (#166 D7's
  original sketch):
  - MSBuild worker nodes are reused, so the task DLL and the 26 MB
    `slang-compiler.dll` stay loaded and locked across builds.
  - Visual Studio's MSBuild runs on .NET Framework and cannot load a `net10.0`
    task in-process.
  - A task host process (`TaskHostFactory`) fixes both, at the cost of a task
    project bootstrapped inside the same build.
  - The in-proc DLL search would also reach the Vulkan SDK's
    `slang-glslang.dll` through `PATH`
    (`Slang.Native/CLAUDE.md:36`).
- **A small console tool project that references `Ahjo.Vulkan.Slang` and is
  run through `Exec`:** it avoids the locking. But the wrapper exposes no
  GLSL source-language or matrix-layout option. A grep of
  `src/Ahjo.Vulkan.Slang/` for `Glsl|SourceLanguage|MatrixLayout` finds only
  `SlangSessionDescription`'s emit-via-glslang switch (`:21-58`) and
  reflection's read-back of a member's layout, so A3 would first need public API changes made
  only to serve a build tool. It would also add a build-graph edge from 9
  projects to a tool project. `slangc` is upstream's own driver of the same
  hash-verified library.
- **Running `slangc` from `native/slang/downloaded/<rid>/bin/`:**
  - that directory does not exist on a CI cache hit (E1);
  - it is shared across versions (the box holds 2026.14.1, 2026.19 and 2026.21
    archives side by side);
  - on Linux it relies on the archive's symlinks.
- **Putting `slangc` inside `staged/<rid>/`:** on Linux it cannot load beside
  the renamed `libslang.so` (E1). Fixing that would put a second 30 MB copy into
  the shipped artifact.
- **Keeping `glslc` and installing the SDK on the runner (#162's ask):** that is
  still unpinned, which is what #171 exists to remove. #162 was closed for that
  reason.
- **One global matrix-layout flag:** E4 shows that any single flag transposes
  one language.
- **Translating every test fixture to Slang:** rejected in A5.
- **`-diagnostic-format vs` for native MSBuild error parsing:** it exists only
  from 2026.21 (E2). It is a candidate once the pin is ≥ 2026.21, as a separate
  change.
- **Keeping `RequireSpirv` as a hard assert:** it would leave 57 lines that
  guard a state the build can no longer produce.

---

## Part B — #172: migrate the six GLSL samples to Slang

### Decision

**B1. Translate, then compile at build time through Part A.** Each sample gets
one `.slang` file with `vertexMain` and `fragmentMain`. It keeps its
`<stem>.vert.spv` / `<stem>.frag.spv` output names through `OutputName`, so
`Program.cs` file paths do not change. The GLSL files are deleted.

Translations follow the repo's Slang conventions:

- `[shader(...)]` entry attributes;
- `[[vk::location(n)]]` on every varying, to keep GLSL's explicit locations;
- `[[vk::binding(slot, set)]]`;
- `[[vk::push_constant]] ConstantBuffer<T>`;
- `Sampler2D` for a combined image sampler;
- `row_major float4x4` with `mul(v, m)`, as in HelloDlaa (E4).

**B2. `shaderDrawParameters` exactly where the module declares
`DrawParameters`.**

- **HeadlessTriangle, HeadlessExport and HelloTriangle** use `SV_VertexID`
  (E3). Each adds the #168 `ConfigureFeatures` block that pushes
  `VkPhysicalDeviceVulkan11Features { shaderDrawParameters = 1 }`. The text
  matches `Slang/README.md:545-561`, and the comment cites #168.
- **HelloCube, HelloVma and HelloVmaWindowed** declare `Shader` only and get
  **no** feature change.

The #168 note says to revisit the wrapper default "if enough samples end up
repeating the same block". Three samples, plus AotSmoke, is that signal. It is
**not** acted on here: #168 is a recorded human decision, and the
`TryGetFeatures` gate it would need is #246's territory.

**Decided (the user, 2026-11-06):** `shaderDrawParameters` stays per-sample.
The #168 decision is kept and the wrapper default does not change.

**B3. Acceptance.**

- **Validation run.** Every migrated sample is run once with
  `VK_LAYER_KHRONOS_validation` and must produce zero validation messages. How
  each sample gets the layer:
  - HelloTriangle, HelloCube, HelloVma and HelloVmaWindowed already set
    `EnableValidation = true` with a `DebugCallback`;
  - HeadlessTriangle and HeadlessExport use `Instance.Create(default)`. They
    are run with `VK_INSTANCE_LAYERS=VK_LAYER_KHRONOS_validation`. The sample
    code is **not** changed for this.
- **Negative control.** One run of HeadlessTriangle with its `ConfigureFeatures`
  block removed must show `VUID-VkShaderModuleCreateInfo-pCode-08740`. That
  proves the validation run can see the defect it guards.
- **Visual diff.**
  - Headless PNGs (`triangle.png`, `export.png`, `hellovma.png`) are captured
    with today's glslc build **before** the change, then compared. They are
    expected to be pixel-identical.
  - The windowed matrix samples (HelloCube, HelloVmaWindowed) get a
    `spirv-cross` decompile comparison of the vertex shader against the glslc
    output, plus a visual check with `--frames`.

**B4. What stays at run time.** HelloRayQuery, HelloDlaa and AotSmoke keep
compiling at run time. They are the samples that demonstrate the run-time
`Ahjo.Vulkan.Slang` path. Their csproj comments are updated only to stop
describing glslc as the alternative.

### Why not the alternatives (Part B)

- **Keep the samples as GLSL compiled by Slang:** that satisfies #171 but not
  #172's goal. The samples should show the repo's shader language.
- **Avoid `SV_VertexID` with `[[vk::builtin("VertexIndex")]]`:** this was
  rejected on #168. It is a Vulkan-only escape hatch in portable source.
- **Enable `shaderDrawParameters` in all six samples uniformly:** it would
  require an optional feature in three samples whose modules do not use it,
  which turns a working device into `VK_ERROR_FEATURE_NOT_PRESENT` on hardware
  that lacks it.
- **Move the migrated samples to run-time compilation like AotSmoke:** #172 was
  blocked on #171 precisely so that the samples would use the build step. Run
  time is already demonstrated three times.
- **One SPIR-V module holding both entry points:** it would force
  `WithVertexEntryPoint` / `WithFragmentEntryPoint` and file-name changes in
  every `Program.cs`, for no behavioural gain.

---

## Interaction and blast radius

- **Files outside `samples/` and `tests/`:**
  - `Directory.Build.targets` (new);
  - `tools/msbuild/Ahjo.Vulkan.SlangShaders.targets` (new);
  - `src/Ahjo.Vulkan.Slang.Native/Ahjo.Vulkan.Slang.Native.csproj`
    (`StageSlangCompiler`, `_SlangToolNeeded`);
  - `.gitignore` (`native/slang/tool/`);
  - `.github/workflows/ci.yml`;
  - `.github/workflows/build-slang-native.yml`;
  - docs.
- **Nothing changes in** `src/Ahjo.Vulkan*/` C# code, `Generated/` or any
  `.rsp`. No public API changes. No hot path is touched, so benchmarks need a
  build plus a dry run, not a baseline capture.
- **Merge order with the `v2026.21` bump.** That branch edits a comment in
  `Ahjo.Vulkan.Slang.Native.csproj:88-92` and three pins. Nothing here depends
  on which pin lands. Whichever merges second rebases; the conflict is
  textual only.

## OPEN (resolved)

**OPEN-1 — Unsupported host or `-p:SkipSlangNativeFetch=true`: error, or skip
and keep `[gate:spirv]`?**

**Decided: error (`AHJOSLANG002`)** — the user, 2026-11-06. A project that
declares `SlangShader` items fails the build when the pinned slangc is
unavailable, and `TestGate.RequireSpirv` and its call sites are deleted (A6).
The "skip" alternative below is kept as the record of what was not chosen.

**Recommended: error (`AHJOSLANG002`).** The issue asks for "failure that is an
error". `ValidateSlangHost` already errors on non-win/linux-x64 hosts for any
project that reaches `Ahjo.Vulkan.Slang.Native`, which is every Slang consumer
today. `publish.yml:320-345` passes `SkipSlangNativeFetch=true` only to the two
`src/` Slang projects, which declare no `SlangShader` items, so publishing is
unaffected.

**The cost:** a macOS or arm64 developer can no longer build the samples or
`Ahjo.Vulkan.Tests`. With glslc on `PATH` they could before.

**If the answer is "skip":**

- `AHJOSLANG002` becomes a high-importance message;
- `TestGate.RequireSpirv` and its 57 call sites stay, with the reason text
  changed to "pinned slangc unavailable on this host";
- `ci.yml` keeps the `spirv` class, but adds it to the `:311` fail loop
  unconditionally, because CI is win-x64 and must never skip on it;
- the plan's Step A8 changes accordingly.
