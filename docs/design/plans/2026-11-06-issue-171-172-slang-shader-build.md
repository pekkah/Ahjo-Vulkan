Paired with [../specs/2026-11-06-issue-171-172-slang-shader-build-design.md](../specs/2026-11-06-issue-171-172-slang-shader-build-design.md). Read it first; this plan only says *how*.

# Implementation plan: issues #171 + #172 — pinned-Slang shader build step and sample migration

- **Branch:** `issue-171-172-slang-shaders`, from `main`.
- **PR:** one PR, with `Closes #171` and `Closes #172`.
- **Commit style:** `Build: …` for Part A and `Samples: …` for Part B.

**Order of work.**

1. Step 0 (baseline capture).
2. Part A (A1–A10), ending at a green checkpoint.
3. Part B (B1–B5).
4. Shared tail (T1–T3).

Part B must not start before Part A's checkpoint passes.

**Stop and ask, do not improvise, if any of these happen:**

- **OPEN-1 is unresolved when you reach Step A8.** Do A8 as written below;
  that is the recommended answer. Its alternative is in the spec.
- **Any measured fact the spec relies on fails to reproduce.** The facts are:
  - capabilities (E3);
  - matrix decompiles (E4);
  - mesh errors (E5);
  - `LocalSizeId` (E6);
  - `ReferenceOutputAssembly`/`Private` behaviour (E8).
- **A headless PNG differs from its Step 0 baseline.**
- **The `v2026.21` bump has merged to `main` while you work.** Rebase. The
  design depends on no specific version. Re-run A10's checks on the new pin and
  report if any behaviour changed.

**Unchanged and binding:**

- Nothing under `src/*/Generated/`, `tools/*.rsp` or `src/Ahjo.Vulkan*/**/*.cs`
  changes.
- No public API changes.
- No `#pragma` and no `NoWarn`.

---

## Step 0 — capture the glslc baseline, before anything changes

1. On a box with the Vulkan SDK installed (glslc is on PATH), start from `main`
   and run:
   `dotnet build Ahjo.Vulkan.slnx -c Release -p:SkipKtxNativeBuild=true -p:SkipNgxNativeBuild=true`
2. Run these three and copy the PNGs to a scratch directory outside the repo:
   - `samples/HeadlessTriangle` → `triangle.png`
   - `samples/HeadlessExport` → `export.png`
   - `samples/HelloVma` → `hellovma.png`
3. Copy these glslc-built `.spv` files from the sample output directories:
   - `cube.vert.spv` (HelloCube)
   - `vma.vert.spv` (HelloVmaWindowed)
4. Record `spirv-cross --version 450 --vulkan-semantics` output for both `.spv`
   files. B4 compares against these.

Commit nothing in this step.

---

## Part A — #171

### A1. Stage `slangc` into a non-shipped tool directory

**File:** `src/Ahjo.Vulkan.Slang.Native/Ahjo.Vulkan.Slang.Native.csproj`

**1. Properties.** Add these to the property group at `:41-98`, after
`_SlangStageNeeded`:

- `_SlangToolDir` = `$(_SlangSrcDir)tool\$(_SlangRid)\`
- `_SlangcFile`:
  - `slangc.exe` when `win-x64`;
  - `slangc` when `linux-x64`.
- `_SlangToolStamp` = `$(_SlangToolDir)slangc.version`
- `_SlangToolNeeded` = `true` when `_SlangRid != ''` and any of these holds:
  - `!Exists('$(_SlangToolDir)$(_SlangcFile)')`;
  - `!Exists('$(_SlangToolStamp)')`;
  - `$([System.IO.File]::ReadAllText('$(_SlangToolStamp)').Trim()) != '$(_SlangVersionNumber)'`.

  Guard the `ReadAllText` with `Exists(...)` in the same condition, ordered so
  it is never evaluated on a missing file. If MSBuild does not short-circuit
  it, split the condition into two `PropertyGroup` elements.

**2. Download and extract conditions.** Widen the conditions on
`DownloadSlangArchive` (`:193-195`) and `ExtractSlangArchive` (`:212-214`)
from `'$(_SlangStageNeeded)' == 'true'` to
`('$(_SlangStageNeeded)' == 'true' or '$(_SlangToolNeeded)' == 'true')`.

**3. Staging targets.** The staged binaries and the tool are now refreshed
independently.

- Add `StageSlangCompiler` to `FetchSlang`'s `DependsOnTargets` (`:179`),
  after `StageSlangBinaries`.
- Keep `StageSlangBinaries`' own condition on `_SlangStageNeeded` only.

**4. New target `StageSlangCompiler`.**

- `DependsOnTargets="ExtractSlangArchive"`.
- `Condition="'$(SkipSlangNativeFetch)' != 'true' and '$(_SlangToolNeeded)' == 'true'"`.
- Body:
  - **Copy, win-x64:** copy into `$(_SlangToolDir)` from
    `$(_SlangExtractDir)bin\`:
    - `slangc.exe`
    - `slang.dll`
    - `slang-compiler.dll`
    - `slang-glslang.dll`
  - **Copy, linux-x64:** copy into `$(_SlangToolDir)` with no rename:
    - `$(_SlangExtractDir)bin/slangc`
    - `$(_SlangExtractDir)lib/libslang-compiler.so.0.$(_SlangVersionNumber)`
    - `$(_SlangExtractDir)lib/$(_SlangGlslangFile)`

    Then `Exec` `chmod +x "<tooldir>slangc"`.
  - **Version check:** run
    `Exec "$(_SlangToolDir)$(_SlangcFile)" -v` with `ConsoleToMSBuild="true"`
    and `IgnoreExitCode="true"`. Capture `ExitCode` and `ConsoleOutput`.
  - **Error:** if the exit code is non-zero or the trimmed output is not equal
    to `$(_SlangVersionNumber)`, fail with:
    `The staged slangc at $(_SlangToolDir) reports '<output>' but SlangVersion is $(SlangVersion). Delete $(_SlangToolDir) and rebuild; if it recurs the release layout changed — re-check StageSlangCompiler against the archive.`
  - **Stamp:** `WriteLinesToFile` `$(_SlangToolStamp)` with
    `$(_SlangVersionNumber)`, `Overwrite="true"`.
  - **Message**, high importance:
    `Staged slangc $(SlangVersion) for $(_SlangRid): $(_SlangToolDir)`.

**5. Comment updates in the same file.**

- `:249-262` ("NOT shipped"): replace the `bin/` bullet's "We call the library,
  not slangc" with this text: slangc is **staged as a build tool** into
  `native/slang/tool/<rid>/` by `StageSlangCompiler`. It is never packed and
  never copied to consumers. The samples and tests compile their shaders with it
  (#171).
- Explain on `_SlangToolNeeded` why the stamp exists: Windows file names carry
  no version.

**6. Do not change** `_SlangPackFile` (`:346-375`) or the `None` copy items
(`:116-157`).

**File:** `.gitignore`

Next to `native/slang/staged/` (`:281`), add `native/slang/tool/` with a
one-line comment: "build-time slangc, staged from the verified archive; never
shipped".

### A2. The shared targets file

**New file:** `tools/msbuild/Ahjo.Vulkan.SlangShaders.targets`

**Header comment.** Point at the spec, and summarize:

- the item contract (A3);
- why `Exec` and not a task (spec Part A, alternatives);
- the matrix-flag split (E4);
- the error codes.

**Properties.** Define each only if not already set.

- `SlangShaderProfile` = `spirv_1_5`. Add a comment that it must match
  `SlangCompiler.cs:90`.
- `_AhjoSlangRid`. Use the same two conditions as `Ahjo.Vulkan.Slang.Native.csproj:42-43`,
  with a comment that says "keep in step with".
- `_AhjoSlangcPath` = `$(NativeDir)slang\tool\$(_AhjoSlangRid)\slangc.exe` or
  `…/slangc`, by RID.
- `_AhjoSlangShaderDir` = `$(IntermediateOutputPath)Shaders\`.
- `_AhjoSlangStamp` = `$(IntermediateOutputPath)ahjo-slang-shaders.stamp`.

**Evaluation-time items.** Place these at the end of the file, so they see the
project's `SlangShader` items.

- `ItemDefinitionGroup` for `SlangShader`: `EntryPoint` = `main`.
- An `ItemGroup` with condition
  `'@(SlangShader)' != '' and '$(MSBuildProjectName)' != 'Ahjo.Vulkan.Slang.Native'`:

  ```xml
  <ProjectReference Include="$(MSBuildThisFileDirectory)..\..\src\Ahjo.Vulkan.Slang.Native\Ahjo.Vulkan.Slang.Native.csproj"
                    ReferenceOutputAssembly="false" Private="false" />
  ```

  Comment it with E8: without `Private="false"`, 32 MB of Slang DLLs land in
  every sample. If the item condition cannot see `@(SlangShader)` at evaluation
  (it should — items are evaluated in order), **stop and report**. Do not move
  the reference into each csproj.

**Targets.**

- Every target carries this condition:
  `Condition="'$(DesignTimeBuild)' != 'true' and '@(SlangShader)' != ''"`.
- Only the entry target has a hook.

1. **`_AhjoSlangShaderPrepare`.**
   - Build item `_AhjoSlangShader` from `@(SlangShader)` with this metadata:
     - `Language`: `Slang` when `%(Extension)` is `.slang` (case-insensitive),
       otherwise `GLSL`.
     - `OutputName`: keep the user value. If it is empty and the language is
       GLSL, use `%(Filename)%(Extension).spv`.
     - `OutputFile` = `$(_AhjoSlangShaderDir)%(OutputName)`.
     - `LanguageArgs`: `-matrix-layout-row-major` for GLSL, empty for Slang.
   - Errors, all with `File="%(FullPath)"`:
     - **`AHJOSLANG003`** when a Slang item has an empty `OutputName`:
       `SlangShader '%(Identity)' is a .slang file; set OutputName (one SlangShader item per entry point, e.g. OutputName="name.vert.spv").`
     - **`AHJOSLANG004`** when two items share an `OutputName`. Detect this by
       comparing `@(_AhjoSlangShader->'%(OutputName)'->Distinct()->Count())` to
       the item count. Message:
       `Two SlangShader items write Shaders\<name>; give each entry point a distinct OutputName.`
       Listing the offending names with a batched message is acceptable.
     - **`AHJOSLANG002`** when `!Exists('$(_AhjoSlangcPath)')`:
       `This project compiles shaders with the pinned slangc ($(SlangVersion)), which was not found at $(_AhjoSlangcPath). It is staged by src/Ahjo.Vulkan.Slang.Native (target StageSlangCompiler) for win-x64 and linux-x64 only; SkipSlangNativeFetch=true also suppresses it.`
       Do **not** set `File` on this one.
   - `MakeDir` `$(_AhjoSlangShaderDir)`.
   - `WriteLinesToFile` `$(_AhjoSlangStamp)` with `WriteOnlyWhenDifferent="true"`
     and `Overwrite="true"`. Lines:
     - `$(SlangVersion)`
     - `$(SlangShaderProfile)`
     - `-warnings-as-errors all -diagnostic-color never`
     - `glsl:-matrix-layout-row-major`
   - Add the stamp to `FileWrites`.

2. **`_AhjoSlangShaderCompile`.**
   - `DependsOnTargets="_AhjoSlangShaderPrepare"`.
   - Target batching per output:
     - `Inputs="@(_AhjoSlangShader);$(_AhjoSlangcPath);$(_AhjoSlangStamp)"`
     - `Outputs="%(_AhjoSlangShader.OutputFile)"`
   - Body:
     - `Delete` `%(OutputFile)`.
     - `Exec` with these parameters:
       - `Command`:
         `"$(_AhjoSlangcPath)" "%(FullPath)" -target spirv -profile $(SlangShaderProfile) -entry %(EntryPoint) %(LanguageArgs) -warnings-as-errors all -diagnostic-color never -o "%(OutputFile)"`
       - `IgnoreExitCode="true"`, `ConsoleToMSBuild="true"`
       - `StandardOutputImportance="low"`, `StandardErrorImportance="low"`
       - `EchoOff="false"`
       - Capture `ExitCode` into `_AhjoSlangRc` and `ConsoleOutput` into
         `_AhjoSlangOut`.
     - `Error` when `'$(_AhjoSlangRc)' != '0'`, with `File="%(FullPath)"`,
       `Code="AHJOSLANG001"` and
       `Text="slangc ($(SlangVersion)) failed for entry point '%(EntryPoint)' (exit $(_AhjoSlangRc)):%0A@(_AhjoSlangOut, '%0A')"`.
     - `Error` when the exit code is 0 but `!Exists('%(OutputFile)')`, with code
       `AHJOSLANG001` and text `slangc reported success but wrote no output`.

3. **`AhjoSlangShaders`.**
   - `BeforeTargets="AssignTargetPaths"`.
   - `DependsOnTargets="_AhjoSlangShaderCompile"`.
   - Body: one `ItemGroup` containing:

     ```xml
     <None Include="@(_AhjoSlangShader->'%(OutputFile)')"
           Link="Shaders\%(OutputName)" CopyToOutputDirectory="PreserveNewest"
           CopyToPublishDirectory="PreserveNewest" Visible="false" />
     <FileWrites Include="@(_AhjoSlangShader->'%(OutputFile)')" />
     ```

     Metadata on the transformed item comes from the source item. If `Link`
     does not carry through the transform, add the items by batching over
     `_AhjoSlangShader` instead.

**New file:** `Directory.Build.targets` (repo root)

```xml
<Project>
  <!-- Shared shader build step (#171). Inert in any project without SlangShader items. -->
  <Import Project="$(MSBuildThisFileDirectory)tools\msbuild\Ahjo.Vulkan.SlangShaders.targets" />
</Project>
```

### A3. Translate the mesh fixtures (forced by E5)

**New file:** `tests/Ahjo.Vulkan.Tests/Shaders/mesh_tri.slang`

- Header comment: carry over both GLSL headers' intent. It is the same triangle
  as `triangle.vert`; `uScale` is a real spec constant for
  `WithMeshSpecialization`; the task stage adds no amplification. Add one line
  saying it is Slang because Slang's GLSL frontend rejects `GL_EXT_mesh_shader`
  (#171).
- Shape, as measured in spec E5:
  - `[vk::constant_id(0)] const float uScale = 1.0;`
  - `kPositions` = `(-0.5,-0.5)`, `(0.5,-0.5)`, `(0.0,0.5)`.
  - `meshMain`:
    `[shader("mesh")] [outputtopology("triangle")] [numthreads(1,1,1)]`
    with `out vertices MeshVertex verts[3]` and `out indices uint3 tris[1]`.
    It calls `SetMeshOutputCounts(3, 1)`.
  - `taskMain`: `[shader("amplification")] [numthreads(1,1,1)]`. It calls
    `DispatchMesh(1, 1, 1, payload)` with a one-`uint` payload struct.

**Delete:** `tests/Ahjo.Vulkan.Tests/Shaders/mesh_tri.mesh` and
`tests/Ahjo.Vulkan.Tests/Shaders/mesh_tri.task`.

**Update comments:** `MeshShaderTests.cs`, wherever it names the `.mesh`/`.task`
sources, and `MeshShaderBenchmarks.cs:73-77`.

### A4. Add the matrix-layout guard fixture

**New file:** `tests/Ahjo.Vulkan.Tests/Shaders/matrix_push.vert`

- GLSL 450.
- A `layout(push_constant) uniform Pc { mat4 mvp; } pc;` and one
  `layout(location=0) in vec3 inPosition;`.
- `gl_Position = pc.mvp * vec4(inPosition, 1.0);`
- Header comment: this file exists only so `CompiledShaderTests` can assert that
  GLSL keeps glslc's column-major layout through Slang (spec E4). No pipeline
  uses it.

The `Shaders\*.vert` glob in A5 picks it up.

### A5. Replace the eleven glslc targets

**Every csproj below:** delete the listed `_ShaderSource` / `_MeshShaderSource`
item groups, the `CompileShaders` / `CompileMeshShaders` targets and their
comments. Then add the `SlangShader` items shown.

**For this step only, the samples keep their GLSL.** Part B converts them.

| File | Delete | Add |
|---|---|---|
| `samples/HeadlessExport/HeadlessExport.csproj` | `:15-32` | `<SlangShader Include="Shaders\*.vert;Shaders\*.frag" />` |
| `samples/HeadlessTriangle/HeadlessTriangle.csproj` | `:15-33` | same |
| `samples/HelloCube/HelloCube.csproj` | `_ShaderSource` at `:24` and `:28-37` (keep `Textures` `None`) | same |
| `samples/HelloTriangle/HelloTriangle.csproj` | `:22-35` | same |
| `samples/HelloVma/HelloVma.csproj` | `:15-33` | same |
| `samples/HelloVmaWindowed/HelloVmaWindowed.csproj` | `:20-33` | same |
| `samples/AotSmoke/AotSmoke.csproj` | `:53-89` (GLSL link, `CompileShaders`, `CopyShadersToPublish`) | nothing |
| `tests/Ahjo.Vulkan.Tests/Ahjo.Vulkan.Tests.csproj` | `:37-92` | see below |
| `tests/Ahjo.Vulkan.Benchmarks/Ahjo.Vulkan.Benchmarks.csproj` | `:25-76` | see below |

**Tests** — add:

```xml
<SlangShader Include="Shaders\*.vert;Shaders\*.frag;Shaders\*.comp" />
<SlangShader Include="Shaders\mesh_tri.slang" EntryPoint="meshMain" OutputName="mesh_tri.mesh.spv" />
<SlangShader Include="Shaders\mesh_tri.slang" EntryPoint="taskMain" OutputName="mesh_tri.task.spv" />
```

**Benchmarks** — add these, all pointing at `..\Ahjo.Vulkan.Tests\Shaders\`:

- `triangle.vert`, `triangle.frag` and `spec_fill.comp` as plain items;
- the two `mesh_tri.slang` items, identical to the tests'.

**Comments to keep:**

- Benchmarks: the comment that it reuses the test project's sources.
- Tests: a two-line comment saying:
  - the sources are compiled by the shared step (`tools/msbuild/Ahjo.Vulkan.SlangShaders.targets`);
  - the GLSL stays GLSL on purpose (spec A5: translating `gl_VertexIndex`
    fixtures adds `DrawParameters`).

### A6. The `maintenance4` enables (E6)

**`tests/Ahjo.Vulkan.Tests/SpecializationInfoTests.cs`**

- Change: `CreateGraphicsDevice` (`:281-303`). Add `ConfigureFeatures` setting
  `f13.maintenance4 = 1`, using the 5-parameter lambda shape from
  `AotSmoke/Program.cs:323-332`. Discard every parameter except `f13`.
- Comment:
  `spec_fill.comp's spec-constant workgroup size compiles to OpExecutionModeId LocalSizeId under Slang (spec E6); VUID-RuntimeSpirv-LocalSizeId-06434 requires maintenance4, which is mandatory in Vulkan 1.3.`

**`tests/Ahjo.Vulkan.Benchmarks/SpecializationInfoBenchmarks.cs:56`**

- Same change, on the `CreateDevice` call there.
- The comment points at the test's comment.
- This is setup-time code. The benchmarked method is unchanged.

### A7. `CompiledShaderTests` — the driverless proof

**New file:** `tests/Ahjo.Vulkan.Tests/CompiledShaderTests.cs`

- Namespace `Ahjo.Vulkan.Tests`.
- No `TestGate` call anywhere. A missing file is a failure.

**Private nested helper `SpirvFacts`.** `static SpirvFacts Read(string path)`
walks the instruction stream:

- word 0 must be `0x07230203`;
- the stream starts at word 5;
- each instruction's opcode is `w & 0xFFFF` and its word count is `w >> 16`;
- a zero word count is a parse failure.

It collects:

| Field | Source |
|---|---|
| `Capabilities` (`HashSet<uint>`) | `OpCapability` = 17, operand 1 |
| `EntryPoints` (`List<(uint Model, string Name)>`) | `OpEntryPoint` = 15; model is operand 1; name is the null-terminated UTF-8 literal starting at word 3 |
| `MemberDecorations` (`HashSet<uint>`) | `OpMemberDecorate` = 72, operand 3 |
| `ExecutionModeIds` (`HashSet<uint>`) | `OpExecutionModeId` = 331, operand 2 |

Path helper: `Path.Combine(AppContext.BaseDirectory, "Shaders", name)`.

**Constants:**

| Name | Value |
|---|---|
| Capability `Shader` | 1 |
| Capability `DrawParameters` | 4427 |
| Capability `MeshShadingEXT` | 5283 |
| Execution model `TaskEXT` | 5364 |
| Execution model `MeshEXT` | 5365 |
| Decoration `RowMajor` | 4 |
| Decoration `ColMajor` | 5 |
| Execution mode `LocalSizeId` | 38 |

**Cases:**

1. **`EveryFixture_IsCompiledSpirv`** — a `[Theory]` over:
   - `triangle.vert.spv`, `triangle.frag.spv`, `near_plane_tri.vert.spv`
   - `fill.comp.spv`, `spec_fill.comp.spv`
   - `mesh_tri.mesh.spv`, `mesh_tri.task.spv`
   - `matrix_push.vert.spv`

   Asserts: the file exists, its length is a non-zero multiple of 4, the magic
   matches, and there is exactly one entry point, named `"main"`.
2. **`GlslVertexIndexFixtures_DeclareNoDrawParameters`** — over
   `triangle.vert.spv` and `near_plane_tri.vert.spv`. Assert `Capabilities`
   equals `{1}`.
3. **`GlslMatrix_KeepsGlslcColumnMajorLayout`** — on `matrix_push.vert.spv`.
   Assert it contains `ColMajor` and does not contain `RowMajor`. Failure
   message:
   `"GLSL compiled through Slang must keep glslc's column-major meaning; check -matrix-layout-row-major on GLSL items in tools/msbuild/Ahjo.Vulkan.SlangShaders.targets (spec E4)."`
4. **`MeshFixtures_AreMeshShadingModules`** — `mesh_tri.mesh.spv` has model
   5365 and capability 5283; `mesh_tri.task.spv` has model 5364 and capability
   5283.
5. **`SpecFill_UsesLocalSizeId_WhichIsWhyItsDevicesEnableMaintenance4`** — on
   `spec_fill.comp.spv`, `ExecutionModeIds` contains 38. Header comment: if this
   ever fails because a Slang bump stopped emitting `LocalSizeId`, the
   `maintenance4` enables from A6 may be removable. Do not delete this test to
   make a bump green.

### A8. Retire `[gate:spirv]` (OPEN-1, decided: error — the user, 2026-11-06)

1. **`tests/Shared/TestGate.cs:57-61`:** delete `RequireSpirv`. Update the class
   remarks if they list classes; they do not today.
2. **Delete all 57 `TestGate.RequireSpirv(...)` lines** in:
   - `CommandRecorderTests.cs` (14)
   - `GraphicsPipelineTests.cs` (18)
   - `MeshShaderTests.cs` (15)
   - `ComputePipelineTests.cs` (3)
   - `DepthClampTests.cs` (2)
   - `ShaderModuleTests.cs` (2)
   - `DescriptorTemplateTests.cs` (1)
   - `DescriptorWriteTests.cs` (1)
   - `SpecializationInfoTests.cs` (1)

   After deleting, `grep -rn RequireSpirv` over the repo, excluding
   `docs/design/`, must return nothing.
3. **`ShaderModuleTests.cs:105-110`:** replace the comment with:
   `The shared SlangShader step (tools/msbuild/Ahjo.Vulkan.SlangShaders.targets) compiles Shaders/triangle.{vert,frag} with the pinned slangc and fails the build if it cannot, so the .spv is always there.`
4. **Benchmarks:** keep each `FileNotFoundException` guard, but replace the
   message tail with:
   `"…is produced by the SlangShader build step; rebuild the benchmark project and check its build log for AHJOSLANG errors."`
   - `GraphicsPipelineBuilderBenchmarks.cs:37`
   - `MeshShaderBenchmarks.cs:73-84` (also fix the comment, which names
     `ContinueOnError`)
   - `SpecializationInfoBenchmarks.cs:38-39`

**If OPEN-1 is answered "skip" instead, follow this alternative:**

- keep `RequireSpirv`;
- change its reason to `[gate:spirv] Compiled shader missing: {spvPath} (the pinned slangc is unavailable on this host).`;
- keep the 57 lines;
- in A2, make `AHJOSLANG002` a `Message Importance="high"` plus an early return.

### A9. CI

**`.github/workflows/ci.yml`**

1. **`:128-139` cache step.**
   - Rename it to `Cache staged Slang binaries + build-time slangc`.
   - Set `path` to a two-line block:
     - `native/slang/staged/win-x64`
     - `native/slang/tool/win-x64`
   - Leave `key` unchanged.
   - Extend the comment: the samples and tests now compile their shaders with
     the pinned `slangc` (#171), and the path list must stay identical to
     `build-slang-native.yml`'s, because the entries share a key.
2. **Coverage summary.**
   - `:238`: remove `spirv = -1;` from `$needs`.
   - `:258`: remove `+ $counts["spirv"]`.
   - `:263`: remove the `"spirv"` arm.
3. **New proof line in the summary.** After `$failed` is computed (`:233`):
   - compute `$shaderTests` = the results whose `testName` matches the
     **unanchored** regex `CompiledShaderTests\.`. Do not anchor it: the trx
     writer's `testName` shape (fully qualified or not) was not verified, and
     this check fails closed;
   - compute `$shaderExecuted` = the count of those whose `outcome` is
     `Passed` or `Failed`.
   - Append to `$md`, before `### Skips by class`:
     `| SPIR-V fixtures | compiled at build time by the pinned slangc; CompiledShaderTests executed $shaderExecuted |`
     (inside the existing table).
   - Fail the step with
     `::error::CompiledShaderTests did not execute, so nothing proves the build compiled the SPIR-V fixtures (#171).`
     when `$shaderExecuted -eq 0`. Set `$bad = $true` in the block at `:305-317`.
4. **`:327-339` AOT comment.** No change is needed. The publish command stays
   the same.

**`.github/workflows/build-slang-native.yml:77-82`**

- Set `path` to two lines:
  - `native/slang/staged/${{ matrix.rid }}`
  - `native/slang/tool/${{ matrix.rid }}`
- Add a comment: the tool directory is not uploaded, `path:` at `:118` is
  unchanged, and the directory is cached only so that a hit does not
  re-download.
- **Do not add tests to this lane.**

### A10. Part A checkpoint

Run everything below on a Windows box. Run it once with the Vulkan SDK
**removed from `PATH`**, which proves that nothing reaches glslc or the SDK's
Slang.

1. **Clean clone build.** Run
   `dotnet build Ahjo.Vulkan.slnx -c Release -p:SkipKtxNativeBuild=true -p:SkipNgxNativeBuild=true`
   with `native/slang/tool` absent. Expect:
   - 0 warnings and 0 errors;
   - the log shows `Staged slangc v2026.xx` once;
   - `samples/*/bin/**/Shaders/*.spv` exist for all 7 build-time projects;
   - **no** `slang*.dll` in the outputs of HeadlessTriangle, HelloCube,
     HelloTriangle, HelloVma, HelloVmaWindowed or HeadlessExport (E8).
2. **Incremental build.** Rebuild with no changes. With `-v:d`, the log shows
   `_AhjoSlangShaderCompile` skipped as up to date.
   - `touch` one source: only that output recompiles.
   - `-p:SlangShaderProfile=spirv_1_6` once: all outputs recompile through the
     stamp. Afterwards rebuild without it.
3. **Failure path.** Temporarily break `tests/Ahjo.Vulkan.Tests/Shaders/fill.comp`
   with an undefined identifier. Expect:
   - the build **fails** with `fill.comp : error AHJOSLANG001` and Slang's text;
   - no `fill.comp.spv` in `obj/` or `bin/`.

   Revert the change. Then add an implicit-conversion warning (`int x = 3.7;`)
   and confirm it also fails. Revert.
4. **Design-time build.**
   `dotnet build tests/Ahjo.Vulkan.Tests -p:DesignTimeBuild=true -v:d` logs no
   `slangc` invocation.
5. **Tests.** `dotnet test --project tests/Ahjo.Vulkan.Tests -c Release`, with the CI
   flags `--report-xunit-trx --report-xunit-trx-filename wrapper.trx --results-directory TestResults`:
   - all `CompiledShaderTests` pass;
   - zero skips carry `[gate:spirv]`.

   Then run the edited `Vulkan coverage summary` script from A9 locally in
   `pwsh`, with `AHJO_VULKAN_TIER` unset, against that `TestResults/wrapper.trx`.
   It must print a non-zero `CompiledShaderTests executed` count and exit 0. If
   the count is 0, the regex does not match the trx's `testName`. **Stop and
   report the actual `testName` value.** Do not loosen the check further.
6. **Hardware, validation tier.** On a hardware box, run the same command with
   `AHJO_VULKAN_TIER=validation`.
   - Quote the contract line.
   - The baseline is #162's: 474 passed, 10 skipped (9 platform, 1 feature).
     Expect at least that number of passes and the same skip classes.
   - `SpecializationInfoTests`, `MeshShaderTests` and `DepthClampTests` must
     show zero validation errors.
   - Read back `VkPhysicalDeviceVulkan13Features.maintenance4` on the device
     with `TryGetFeatures` and report it, to confirm E6's "mandatory".
7. **Benchmark dry run.**
   `dotnet run -c Release --project tests/Ahjo.Vulkan.Benchmarks -- --filter *GraphicsPipelineBuilder* *MeshShader* *SpecializationInfo* --job dry`
   completes. No baseline capture: no hot path changed.
8. **AOT.** `dotnet publish samples/AotSmoke/AotSmoke.csproj -c Release -r win-x64 -p:IlcUseEnvironmentalTools=true`:
   - is clean;
   - its `obj/` has no `ahjo-slang-shaders.stamp`;
   - the exe runs.
9. **Publish.** `dotnet publish samples/HeadlessTriangle -c Release -r win-x64`
   produces `publish/Shaders/triangle.vert.spv` and `triangle.frag.spv`.
10. **Linux.** If a linux-x64 box is available, run step 1 for
    `tests/Ahjo.Vulkan.Tests` only. It proves the Linux tool directory loads.
    If no box is available, say so in the PR. Linux is not a CI lane for this
    step (#32).

Commit Part A, for example: `Build: compile shaders with the pinned slangc via a shared SlangShader step`.

---

## Part B — #172

### B1. Translate the six samples

**Rules for every file:**

- Keep the GLSL file's header comment intent.
- `vertexMain` / `fragmentMain` with `[shader("vertex")]` / `[shader("fragment")]`.
- Put `[[vk::location(n)]]` on every vertex input and every varying. Use the
  same numbers as the GLSL.
- `[[vk::binding(slot, set)]]` on every resource.
- Matrices are declared `row_major float4x4` and multiplied as `mul(v, m)`.
  Comment it as in `HelloDlaa/Shaders/cube.slang:9-10` (spec E4).
- No `[[vk::builtin]]`.

**Translations:**

| New file | Source | Content |
|---|---|---|
| `samples/HeadlessTriangle/Shaders/triangle.slang` | — | Same body as `samples/AotSmoke/Shaders/triangle.slang:11-44`. Header: the triangle `HeadlessTriangle` renders and the source `AotSmoke` mirrors. Note that `SV_VertexID` declares `DrawParameters`, which is why `Program.cs` enables `shaderDrawParameters` (#168). |
| `samples/HeadlessExport/Shaders/triangle.slang` | — | Same as HeadlessTriangle's; the header names `HeadlessExport`. |
| `samples/HelloTriangle/Shaders/triangle.slang` | `HelloTriangle/Shaders/triangle.vert`/`.frag` | Same body. Positions and colours as in its GLSL (identical values). |
| `samples/HelloCube/Shaders/cube.slang` | `cube.vert`/`cube.frag` | `struct PushConstants { row_major float4x4 mvp; }; [[vk::push_constant]] ConstantBuffer<PushConstants> pc;` and `[[vk::binding(0, 0)]] Sampler2D uTexture;`. Inputs at locations 0/1/2 (`float3`, `float2`, `float3`). Varyings at 0/1. Vertex: `mul(float4(p, 1.0), pc.mvp)`. Fragment: `uTexture.Sample(uv).rgb * lerp(float3(1.0), tint, 0.25)`. Keep the tint comment. |
| `samples/HelloVma/Shaders/vma.slang` | `vma.vert`/`vma.frag` | `struct Tint { float4 tint; }; [[vk::binding(0, 0)]] ConstantBuffer<Tint> u;`. Inputs at locations 0/1 (`float2`, `float3`). Varying at 0. Fragment returns `float4(c * u.tint.rgb, u.tint.a)`. |
| `samples/HelloVmaWindowed/Shaders/vma.slang` | its `vma.vert`/`vma.frag` | `struct Frame { row_major float4x4 transform; float4 tint; }; [[vk::binding(0, 0)]] ConstantBuffer<Frame> u;`. Vertex: `mul(float4(p, 0.0, 1.0), u.transform)`. Fragment as in HelloVma. |

**Delete** the 12 GLSL files.

**Change each csproj's `SlangShader` items** (from A5) to two items each:

```xml
<SlangShader Include="Shaders\<stem>.slang" EntryPoint="vertexMain"   OutputName="<stem>.vert.spv" />
<SlangShader Include="Shaders\<stem>.slang" EntryPoint="fragmentMain" OutputName="<stem>.frag.spv" />
```

The stems are `triangle`, `triangle`, `triangle`, `cube`, `vma` and `vma`.
`Program.cs` paths are unchanged.

### B2. `shaderDrawParameters` in the three `SV_VertexID` samples

Add `ConfigureFeatures` to each device creation below. The lambda has the
5-parameter shape with discards. It contains
`ref VkPhysicalDeviceVulkan11Features f11 = ref chain.Push<VkPhysicalDeviceVulkan11Features>(); f11.shaderDrawParameters = 1;`
Copy the comment from `samples/AotSmoke/Program.cs:312-322` and adapt it: name
`triangle.slang`'s `vertexMain`, #168, and `Slang/README.md`'s section
"`SV_VertexID` needs `shaderDrawParameters`".

- `samples/HeadlessTriangle/Program.cs:173-176`
- `samples/HeadlessExport/Program.cs:218`. Read the existing description first;
  if it already has `ConfigureFeatures`, add to its body. Do not push a second
  `VkPhysicalDeviceVulkan11Features`.
- `samples/HelloTriangle/Program.cs:396`. Same check as HeadlessExport.

**HelloCube, HelloVma and HelloVmaWindowed get no feature change.** B4 step 1
proves that they need none.

### B3. Comment-only updates for the run-time samples

| File | Change |
|---|---|
| `samples/AotSmoke/Shaders/triangle.slang:1-4` | Point the "Slang equivalent of …" sentence at `../../HeadlessTriangle/Shaders/triangle.slang`. Drop "which this sample also links". |
| `samples/AotSmoke/Program.cs:49-50` | Change to "2 stays 'no usable shader bytes'." Drop the glslc clause. |
| `samples/HelloRayQuery/RayQueryPipeline.cs:11-14` | Change to: "Compiled at run time … ray query in GLSL needs `GL_EXT_ray_query`; the other samples precompile through the pinned-Slang build step (#171), and this one deliberately exercises `Ahjo.Vulkan.Slang` at run time." |
| `samples/HelloRayQuery/HelloRayQuery.csproj:17-21` | Same message: "No SlangShader items: compiled at run time by Ahjo.Vulkan.Slang on purpose." |
| `samples/HelloDlaa/HelloDlaa.csproj:23-26` | Same message as HelloRayQuery's csproj. |

### B4. Part B acceptance

Run on a hardware Windows box with the Vulkan SDK installed. The SDK is
needed only for the validation layer and `spirv-dis` / `spirv-cross`.

1. **Capabilities.** For each sample's `bin/**/Shaders/*.vert.spv`, run
   `spirv-dis … | findstr OpCapability`. Expected:
   - `DrawParameters` + `Shader` for HeadlessTriangle, HeadlessExport and
     HelloTriangle;
   - `Shader` only for HelloCube, HelloVma and HelloVmaWindowed.

   **Any other result stops the work.**
2. **Matrix equivalence.** For HelloCube's `cube.vert.spv` and
   HelloVmaWindowed's `vma.vert.spv`, the `spirv-cross` decompile shows a plain
   (column-major) `mat4` and `M * v`, identical in form to the Step 0
   baseline. Variable names may differ.
3. **Validation.** Run each sample and confirm zero validation messages, with no
   `VUID` text and no callback errors:
   - Headless:
     `$env:VK_INSTANCE_LAYERS="VK_LAYER_KHRONOS_validation"; dotnet run -c Release --project samples/HeadlessTriangle`
     and the same for HeadlessExport. Clear the variable afterwards.
   - `dotnet run -c Release --project samples/HelloVma`.
   - Windowed, each with `-- --frames 120`:
     - HelloTriangle
     - HelloCube
     - HelloVmaWindowed
4. **Negative control.** Temporarily delete HeadlessTriangle's
   `ConfigureFeatures` block, run step 3's headless command, and confirm
   `VUID-VkShaderModuleCreateInfo-pCode-08740` is printed. Restore the block.
   Quote the line in the PR.
5. **PNG diff.** Compare `triangle.png`, `export.png` and `hellovma.png`
   pixel by pixel against Step 0, for example by comparing the decoded RGBA
   bytes with a throwaway script outside the repo. **They must be identical.**
   If they are not, stop and report the differing pixel count and the max
   delta.
6. **Windowed visual check.** HelloCube, HelloTriangle and HelloVmaWindowed look
   as they did on `main`:
   - the cube tumbles and is textured, not skewed;
   - the triangle has the same colours;
   - the VMA triangle rotates with the tint.

Commit Part B, for example: `Samples: migrate the six GLSL samples to Slang`.

---

## T1. Docs

| File | Change |
|---|---|
| `src/Ahjo.Vulkan.Slang/README.md:765-768` | Replace the "MSBuild task" paragraph. This repository's samples and tests now compile shaders at build time with the pinned `slangc` through a repo-internal MSBuild step (#171). The step is **not** part of the package; consumers who want build-time compilation should run `slangc` the same way or use `SlangCompiler` at run time. |
| `src/Ahjo.Vulkan.Slang.Native/CLAUDE.md`, "The shipped subset" | Add a paragraph on `native/slang/tool/<rid>/`: what it holds per RID (A1), that it is never packed, why Linux needs `libslang-compiler.so.0.<ver>` by its soname (spec E1), the version stamp, and that `_SlangToolNeeded` is the tool's equivalent of `_SlangStageNeeded`. |
| `native/CLAUDE.md` | Add a `slang/tool/<rid>/` bullet after `slang/staged/<rid>/`. |
| `docs/ci-coverage.md` | **`:52`**: delete the `[gate:spirv]` row. **`:216-218`**: replace the "Provision `glslc`" follow-up with a "Resolved by #171" note: SPIR-V fixtures are compiled by the pinned `slangc` or the build fails, and `CompiledShaderTests` runs driverless, so the coverage summary's SPIR-V line is proof at tier `none`. |
| `tests/CLAUDE.md` | Remove `Spirv` from the `TestGate.Require{…}` list. Add a bullet that `CompiledShaderTests` is the driverless SPIR-V proof and must not be gated. |
| `.github/CLAUDE.md`, `slang-native` section | One sentence: the lane also caches `native/slang/tool/<rid>/` so that a hit does not re-download; it does not upload or test it. |
| `tools/CLAUDE.md` | One bullet for `msbuild/Ahjo.Vulkan.SlangShaders.targets`: the shared shader step, its item contract and its error codes, pointing at the spec. |
| Root `CLAUDE.md` | No change. It carries no glslc text. |

Do **not** edit the issue-166 spec. Its "Follow-ups" section is a historical
record.

**Follow-up, not to fix here (filed as #256):** the staged *runtime* DLLs on
Windows are not refreshed after a `SlangVersion` bump, because file names are
version-free and only existence is checked (`csproj:97`). The tool's stamp in
A1 is the pattern to copy. Put #256 in the PR body.

## T2. Final verification

1. Rerun A10 steps 1, 5 and 8 on the final tree.
2. `git grep -n -i "glslc" -- ':!docs/design'` returns exactly one line:
   `src/Ahjo.Vulkan/Pipelines/ComputePipelineBuilder.cs:14` ("glslc / DXC
   default"). It is a general statement about SPIR-V entry-point naming in
   wrapper code, which this plan does not touch. **Leave it.** Report any other
   match.
3. `git grep -n "WarnAndContinue"` returns nothing.

## T3. PR

- Title: `Build + Samples: compile shaders with the pinned slangc; migrate samples to Slang`.
- Body:
  - `Closes #171`, `Closes #172`.
  - Link the spec.
  - State the breaking dev-host change from OPEN-1's answer.
  - Paste the A10.6 contract line, the B4 negative-control VUID line, the PNG
    result and the capability table from B4.1.
- Reviewers: `vulkan-validation-reviewer`, because of the `ConfigureFeatures`
  and `maintenance4` changes, and `bench-coverage-checker`, because the
  benchmark setup changed.

## Implementation notes (review round 1)

Added after implementation. These are not plan steps.

- **Push-constant layout (spec E10).** `fill.comp` and `matrix_push.vert`
  now use `layout(push_constant, std430)`.
  - New fixture `push_std430.vert`.
  - New `CompiledShaderTests` cases: `GlslPushConstants_KeepGlslcStd430Offsets`
    and `NoCompiledModule_HasAStd140PushConstantBlock`. The second is
    name-based and was verified to fail when `std430` is removed from
    `fill.comp`.
- **Workgroup-size default (spec E11).** `spec_fill.comp` declares
  `layout(constant_id = 0) const int kLocalSizeX = 1;`. New case:
  `SpecFill_WorkgroupSizeSpecConstant_DefaultsToOne`.
- **Device pickers.** The `PickPhysicalDevice` predicates in HeadlessTriangle,
  HeadlessExport, HelloTriangle and AotSmoke skip devices with
  `Features11.shaderDrawParameters == 0`, the feature their `ConfigureFeatures`
  enables.
- **Docs.** `tools/CLAUDE.md` records both GLSL source rules.
  `tests/CLAUDE.md` and the targets header point to it.
