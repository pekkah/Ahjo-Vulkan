# HelloRayQuery — implementation plan

Paired with [../specs/2026-08-23-issue-207-hello-ray-query-design.md](../specs/2026-08-23-issue-207-hello-ray-query-design.md). Issue [#207](https://github.com/pekkah/Ahjo-Vulkan/issues/207).

Two parts, landing in one PR (never stacked — see the no-stacked-PRs rule):

- **A**: `SlangSessionDescription.Capabilities`, so a ray-query compile can be warning-free. Approved as a widening at the approval gate; the original spec had deferred it.
- **B**: the `samples/HelloRayQuery` sample, which is A's first consumer.

Nothing under `src/Ahjo.Vulkan/` changes.

## 0. `SlangSessionDescription.Capabilities`

`src/Ahjo.Vulkan.Slang/SlangSessionDescription.cs` — new member:

```csharp
public Utf8Name[]? Capabilities { get; init; }
```

`Utf8Name[]`, not `string[]`: capability names are compile-time constants, same reasoning as `SpirvProfile`. Null or empty means the profile alone, so `default(SlangSessionDescription)` is unchanged.

`src/Ahjo.Vulkan.Slang/SlangCompiler.cs`, in `CreateSession` — the single `CompilerOptionEntry optimization` local becomes a `stackalloc Span<CompilerOptionEntry>` of `1 + Capabilities.Length`. Entry 0 stays `Optimization`; each capability resolves through `global->findCapability(name.Ptr)` and rides as `CompilerOptionName.Capability` with `CompilerOptionValueKind.Int` and the resolved id.

- A null entry → `ArgumentException`.
- `SLANG_CAPABILITY_UNKNOWN` → `SlangCompilationException` naming the capability, mirroring the unknown-profile throw immediately above it. Passing the raw string through instead would let a typo be silently ignored and resurface as the E41012 the caller thought they had turned off.
- `target.compilerOptionEntries` must be assigned inside a `fixed` over the span, and `createSession` called there — Slang copies the array during the call.

Tests in `tests/Ahjo.Vulkan.Slang.Tests/`: a `RayQueryCompute` fixture shader in `ShaderFixtures.cs`, then four cases in `SlangCompilerTests.cs` — without the capability warns E41012; with it is warning-free **and emits byte-identical SPIR-V** (the assertion that this declares intent rather than changing output); an unknown capability throws with the name in the message; an empty array behaves like the default.

## 1. Project skeleton

New `samples/HelloRayQuery/HelloRayQuery.csproj`, modelled on `samples/AotSmoke/AotSmoke.csproj` (the only sample that already references the Slang wrapper):

- `OutputType=Exe`, `RootNamespace=Ahjo.Vulkan.Samples.HelloRayQuery`, `AssemblyName=HelloRayQuery`, `IsPackable=false`.
- `ProjectReference`: `Ahjo.Vulkan`, `Ahjo.Vulkan.Slang`, `Ahjo.Vulkan.Utilities` (for `PngWriter`).
- `None Include="Shaders\rayquery.slang"` with `CopyToOutputDirectory="PreserveNewest"`. **No glslc target** — nothing here is GLSL.

Register the project in `Ahjo.Vulkan.slnx` next to the other samples.

## 2. The shader — `samples/HelloRayQuery/Shaders/rayquery.slang`

One compute entry point, `computeMain`, `[numthreads(8, 8, 1)]`.

```
[[vk::binding(0, 0)]] RaytracingAccelerationStructure scene;
[[vk::binding(1, 0)]] RWTexture2D<float4> output;
```

Per thread:

- Map `SV_DispatchThreadID.xy` to `[-1, 1]` in XY (`(tid + 0.5) / imageSize * 2 - 1`, Y flipped so the PNG is right way up).
- `RayDesc` with `Origin = float3(x, y, -1)`, `Direction = float3(0, 0, 1)`, `TMin = 0.001`, `TMax = 10`.
- `RayQuery<RAY_FLAG_FORCE_OPAQUE> q; q.TraceRayInline(scene, RAY_FLAG_NONE, 0xFF, ray); q.Proceed();`
- On `q.CommittedStatus() == COMMITTED_TRIANGLE_HIT`, shade from `q.CommittedTriangleBarycentrics()` and `q.CommittedPrimitiveIndex()` so the two triangles are visually distinct; otherwise write the background colour.
- Write `output[tid.xy]`.

Header comment states why the shader exists (the wrapper has no ray-query API to test; this is the only executable form of the feature) and why it is Slang rather than GLSL.

## 3. The program — `samples/HelloRayQuery/Program.cs`

`internal static unsafe class Program`, `Main(string[] args)` taking an optional output path, defaulting to `rayquery.png` next to the executable. Exit codes: `0` success or clean skip, `2` no usable shader, `3` the rendered image failed its own check.

### 3a. Device selection and the skip path

`Instance.Create` with `ApiVersion = V1_4` and validation **on permanently** — this sample exists to demonstrate a correct barrier and descriptor recipe, so the layer's verdict is part of its output, not a debugging aid. The debug callback counts errors; a non-zero count fails the run.

Then `PickPhysicalDevice` screening on the three extensions and a graphics+compute queue family — the same predicate as `AccelerationStructureBenchmarks.Setup` (`tests/Ahjo.Vulkan.Benchmarks/AccelerationStructureBenchmarks.cs:90-106`), but with `"…"u8` literals rather than the internal `DeviceExtensionNames`, since a sample only gets the public surface and `PhysicalDeviceInfo.SupportsExtension` takes a `ReadOnlySpan<byte>`. Catch `VK_ERROR_INITIALIZATION_FAILED` from the picker and `VK_ERROR_FEATURE_NOT_PRESENT` / `VK_ERROR_EXTENSION_NOT_PRESENT` from `CreateDevice`, returning null for each: no device, skip line, `return 0`.

`CreateDevice` enables the three extensions and, via `ConfigureFeatures`, `bufferDeviceAddress` + `VkPhysicalDeviceAccelerationStructureFeaturesKHR.accelerationStructure` + `VkPhysicalDeviceRayQueryFeaturesKHR.rayQuery`.

Push descriptors need `VK_KHR_push_descriptor` or Vulkan 1.4. Every RT-capable device in practice has both, so the sample uses them unconditionally rather than carrying a descriptor-pool fallback that would never execute and would double the descriptor code a reader has to follow.

### 3b. Geometry and the acceleration structures

- Vertex buffer: two triangles, 6 vertices, `VK_FORMAT_R32G32B32_SFLOAT`, stride 12. Place them at `z = 0` and inside `[-1, 1]` XY, with a clear gap between them so the miss region is unambiguous. Usage `AccelerationStructureBuildInputReadOnly | ShaderDeviceAddress`, host-visible + mapped, `Flush()` after writing.
- `AccelerationStructureGeometry.Triangles(vertexAddress, VK_FORMAT_R32G32B32_SFLOAT, vertexStride: 12, maxVertex: 5)`.
- Size with `GetAccelerationStructureBuildSizes(BottomLevel, PreferFastTrace, geos, maxCounts: [2])`; allocate backing (`AccelerationStructureStorage | ShaderDeviceAddress`) and scratch (`StorageBuffer | ShaderDeviceAddress`); assert the scratch address is a multiple of `AccelerationStructureLimits.MinScratchOffsetAlignment` the way `BlasFixture` does.
- `CreateAccelerationStructure(BottomLevel, …)`, build with `AccelerationStructureBuildRange.Of(2)`.
- Instance buffer: one `VkAccelerationStructureInstanceKHR`, identity 3×4 transform, `mask = 0xFF`, `accelerationStructureReference = blas.GetDeviceAddress(device)`.
- TLAS sized over `AccelerationStructureGeometry.Instances(instanceAddress)` with `maxCounts: [1]`, built with `Of(1)`.

Both builds go in **one** command buffer with a barrier between them and a barrier after — all `stackalloc`/collection-expression spans, which is legal since #205.

### 3c. The barriers — the recipe the spec says is undocumented

Three, all `MemoryBarrier`:

1. BLAS build → TLAS build: `Stage.AccelerationStructureBuild` / `Access.AccelerationStructureWrite` → `Stage.AccelerationStructureBuild` / `Access.AccelerationStructureRead`.
2. TLAS build → **shader read**: `Stage.AccelerationStructureBuild` / `Access.AccelerationStructureWrite` → `Stage.ComputeShader` / `Access.AccelerationStructureRead`. This is the one nothing in the repo has executed; comment it as such and cite it.
3. Storage-image writes → transfer read, before the copy-to-buffer: an `ImageBarrier` `GENERAL` → `TRANSFER_SRC_OPTIMAL`, `Stage.ComputeShader`/`Access.ShaderStorageWrite` → `Stage.Copy`/`Access.TransferRead`.

Do **not** use `Stage.AccelerationStructureCopy` anywhere: #202's docs warn it needs `VK_KHR_ray_tracing_maintenance1`, which this sample does not enable.

### 3d. Shader compile

`SlangCompiler.Create()` → `CreateSession` **with `Capabilities = [Utf8Name.FromLiteral("spvRayQueryKHR"u8)]`** (step 0) → `Compile(new SlangCompileRequest { Path = <Shaders/rayquery.slang> })`. Wrap in `try/catch (SlangCompilationException)` → print `ex.Diagnostics`, `return 2` — the `AotSmoke` pattern (`samples/AotSmoke/Program.cs:45-52`).

`program.Warnings` should now be empty. Print it if it is not: a warning is information, not noise.

**Do not call `WithEntryPoint`.** Slang names the entry point `computeMain` on the Slang side but emits it into SPIR-V as `main`; asking for `"computeMain"u8` fails with `VUID-VkPipelineShaderStageCreateInfo-pName-00707`. The builder already defaults to `main`. Comment this at the call site.

`ShaderModule` from `program.Spirv(0)`, `ComputePipelineBuilder` with entry point `"computeMain"u8`.

### 3e. Descriptors, dispatch, readback

- Storage image: `VK_FORMAT_R8G8B8A8_UNORM`, 512×512, usage `Storage | TransferSrc`, transitioned `UNDEFINED` → `GENERAL` before the dispatch.
- Set layout: binding 0 `VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR`, binding 1 `VK_DESCRIPTOR_TYPE_STORAGE_IMAGE`, both `ShaderStages.Compute`, built with `DescriptorSetLayoutDescription.PushDescriptor`.
- `DescriptorWrite.AccelerationStructure(binding: 0, arrayElement: 0, in tlas)` and `DescriptorWrite.Image(binding: 1, …, StorageImage, GENERAL)`, pushed as a `ReadOnlySpan<DescriptorWrite>` collection expression.
- `Dispatch(512/8, 512/8, 1)`.
- Barrier, `CopyImageToBuffer` into a host-visible readback buffer, submit, `Fence.Wait`.

### 3f. Self-check and PNG

Read the mapped readback buffer as `ReadOnlySpan<byte>`.

- Pick one pixel at the centroid of triangle 0 and one in the known-empty corner. Assert hit and miss respectively; on mismatch print both pixel values with their coordinates and `return 3`.
- `PngWriter.Write(path, 512, 512, pixels)` (match the existing signature in `src/Ahjo.Vulkan.Utilities/PngWriter.cs`).
- Print the output path, the hit/miss check result and the device name.

## 4. Documentation

- `README.md`: add `HelloRayQuery` to the samples list with a one-line description and the RT-device caveat.
- `samples/` — if a sample index exists, add the row; otherwise the README entry is enough.
- No `docs/benchmarks.md` change: nothing here is a hot path and no benchmark is added.

## 5. Verification

1. `dotnet build Ahjo.Vulkan.slnx` — 0 warnings (`TreatWarningsAsErrors`).
2. `dotnet run --project samples/HelloRayQuery` on this host — must exit 0, print the E41012 note, pass its own hit/miss check and write `rayquery.png`. Attach the PNG to the PR.
3. Re-run under the validation layer (`EnableValidation = true` in the sample's `InstanceDescription`, kept on permanently since this is a correctness demo, not a perf one) and confirm zero errors. Quote the result in the PR.
4. `AHJO_VULKAN_TIER=validation dotnet test Ahjo.Vulkan.slnx` — unchanged pass count; the sample must not perturb the suite.

## Deliberately out of scope

- Any `src/Ahjo.Vulkan/` change. If the sample cannot be written without one, **stop and report** rather than growing the wrapper from inside a sample. (Step 0 touches `Ahjo.Vulkan.Slang` only, and only because the approval gate said to.)
- A `Utf8Name` overload of `PhysicalDeviceInfo.SupportsExtension`. The picker uses `"…"u8` literals instead; same bytes, same invariant.
- A ray-tracing-pipeline (SBT) sample.
- Running the sample in CI.
