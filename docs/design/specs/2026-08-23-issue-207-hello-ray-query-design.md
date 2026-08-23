# HelloRayQuery — proving the acceleration-structure surface end to end

Issue: [#207](https://github.com/pekkah/Ahjo-Vulkan/issues/207). Written 2026-08-23.

## Problem

#202 (PR #204) landed the `VK_KHR_acceleration_structure` surface and #206 (PR #211) closed its last untested arm. Every piece has a test. Nothing puts the chain together, and three claims the wrapper makes are structurally untestable by the suite as it stands:

1. **That the pieces compose.** `AccelerationStructureTests` builds a BLAS (`tests/Ahjo.Vulkan.Tests/AccelerationStructureTests.cs:566`), builds a TLAS over one instance (`:673`), and the wrapper offers an AS descriptor write (`src/Ahjo.Vulkan/Pools/DescriptorWrite.cs:177`) — but never in one sequence, and never with anything reading the result.

2. **That the shader side works at all.** `VK_KHR_ray_query` defines **zero entry points**, so there is no wrapper API to unit-test: traversal lives entirely in the shader (`RayQuery<>` / `TraceRayInline`). Nothing in this repository has compiled or executed such a shader. The Tier-3 tests and the benchmarks all enable `VK_KHR_ray_query` and the `rayQuery` feature (`AccelerationStructureTests.cs:1052`, `tests/Ahjo.Vulkan.Benchmarks/AccelerationStructureBenchmarks.cs:93`) purely to satisfy the AS extension's dependency chain — the feature is switched on and then never used.

3. **That the documented barrier recipe is right.** #202's docs steer callers to `Stage.AccelerationStructureBuild` + `Access.AccelerationStructureRead`. Every existing use of that recipe is build→build (a compacted-size query, or a later build reading the BLAS). The recipe a real consumer needs — build→**shader read**, with `Stage.ComputeShader` + `Access.ShaderRead` on the consuming side — is written down nowhere and executed nowhere.

## Evidence

Two unknowns were probed on this host before choosing an approach. Both changed the design.

### The shipped Slang compiles ray query, with a caveat

Compiling a `RayQuery<>` compute shader through `Ahjo.Vulkan.Slang` (`SlangCompiler.Create()` → `CreateSession(default)` → `Compile`) succeeds: 1 entry point, 469 SPIR-V words. It also always emits a warning:

```
warning[E41012]: profile implicitly upgraded
  entry point 'computeMain' uses additional capabilities that are not part of the
  specified profile 'spirv_1_5'. The profile setting is automatically updated to
  include these capabilities: 'spvRayQueryKHR'
```

The warning is unavoidable through the current wrapper surface. `SlangSessionDescription.SpirvProfile` (`src/Ahjo.Vulkan.Slang/SlangSessionDescription.cs:40`) takes a bare profile name handed to `IGlobalSession::findProfile`; there is no capability member. Probed alternatives:

| `SpirvProfile` | Result |
|---|---|
| default (`spirv_1_5`) | compiles, E41012 |
| `spirv_1_6` | compiles, E41012 |
| `spirv_1_4` | compiles, E41012 |
| `spirv_1_5+spvRayQueryKHR` | **throws** — `SlangCompilationException: unknown SPIR-V profile` (`src/Ahjo.Vulkan.Slang/SlangCompiler.cs:97`) |

So Slang auto-upgrades and emits correct SPIR-V, and there is no way through the current surface to say "I meant that".

**Decided at the approval gate: fix it rather than work around it.** `SlangSessionDescription` gains a `Capabilities` member (`Utf8Name[]?`, matching `SpirvProfile`'s form for the same invariant-#1 reason), resolved through `IGlobalSession::findCapability` and passed as `CompilerOptionName.Capability` target options alongside the existing `Optimization` entry. Resolving the *name* to an id in the wrapper is what lets an unrecognised capability fail at `CreateSession` with the name in the message, instead of being silently ignored and resurfacing as the E41012 the caller thought they had turned off.

Measured after implementing it: with `spvRayQueryKHR` declared the compile is warning-free and the SPIR-V is **byte-identical** to the inferred one — the capability moves the decision from Slang's inference to the caller's description, it does not change what is emitted. That equality is asserted by `SlangCompilerTests.Compile_RayQuery_WithCapability_IsWarningFreeAndEmitsTheSameSpirv`.

### The validation layer cannot see whether a build is *correct*

While closing #206 the layer was probed on `vkCmdBuildAccelerationStructuresKHR`: it validates `primitiveCount` (via the destination size check) and is **blind to `primitiveOffset`** — offsets of 1, 4 (both illegal; the AABB rule is a multiple of 8) and 9999 (past the end of a 72-byte buffer) all recorded without a single message. This is the concrete reason a sample is worth more than another test here: only something that *traverses* the structure can tell a correct build from a well-formed garbage one.

## Decision

**A headless compute sample, `samples/HelloRayQuery`, that ray-queries a two-triangle BLAS through a TLAS and writes the traversal result to a PNG.**

Shape, following `HeadlessTriangle` / `HeadlessExport`:

1. Pick an RT-capable device; print a skip line and exit **0** when none exists — the `HeadlessExport` precedent (`samples/HeadlessExport/Program.cs:55`).
2. Build a BLAS over two triangles; barrier; read its device address.
3. Write one `VkAccelerationStructureInstanceKHR`; build the TLAS over it; barrier.
4. Compile `Shaders/rayquery.slang` at run time with `Ahjo.Vulkan.Slang`.
5. Push-descriptor the TLAS (binding 0) and a storage image (binding 1); dispatch.
6. Barrier; copy the image to a host buffer; write `rayquery.png`.

### The output is the assertion

The image is not decoration. Each thread casts a ray along `+Z` from its pixel's position in the XY plane. Pixels inside the two triangles report a hit and are shaded by barycentric coordinates; pixels outside report a miss. **The PNG is therefore a picture of the geometry that was actually built.** A wrong `stride`, a wrong `primitiveOffset`, a mis-set instance transform or a missing barrier produces a visibly wrong image rather than a clean run — which is precisely the failure class the layer was just shown to be blind to.

The sample checks this itself rather than leaving it to the eye: it reads back a pixel known to be inside the geometry and one known to be outside, and exits non-zero if either disagrees. A sample that renders garbage and still exits 0 proves nothing.

### Why the alternatives were rejected

- **A ray-query *test* instead of a sample.** It needs the same shader, the same run-time compile and the same RT gate, and would then be a test that can never run in CI (`.github/CLAUDE.md`: software rasterizers are not honest coverage) — all the cost of the sample with none of the "here is how a consumer writes this" value. The issue asks for a sample; composition is the point.
- **A ray-tracing-*pipeline* sample** (`VK_KHR_ray_tracing_pipeline`, raygen/miss/closest-hit shaders, a shader binding table). Much larger, and it would exercise surface the wrapper does not have. Ray query needs no new wrapper API at all, which is exactly why it is the right first consumer.
- **A windowed sample.** Adds a swapchain and a present loop to a sample whose subject is traversal, and cannot be run unattended.
- **Precompiling the shader with `glslc`,** the way `HeadlessTriangle` does. `glslc` is GLSL; ray query there needs `GL_EXT_ray_query`, and the repo is already moving off glslc — `samples/AotSmoke/AotSmoke.csproj:53-60` calls the leftover target debt. Compiling at run time also covers `Ahjo.Vulkan.Slang` on a path that matters.
- **Leaving E41012 in place and having the sample explain it.** This was the spec's original choice — a sample should not be the reason a published package grows a member. Overridden at the approval gate: the warning is a real gap in `Ahjo.Vulkan.Slang`, every ray-query consumer hits it, and shipping a sample that documents a wart is worse than removing the wart. The two changes land together in one PR rather than stacked, so the sample's clean compile is the capability member's proof.

### CI

The sample **builds** in CI and joins the solution, so a wrapper change that breaks it breaks the build. It is not **run** in CI: it needs an RT-capable device, and `.github/CLAUDE.md` is explicit that software rasterizers are not honest coverage. Runtime gating follows `HeadlessExport` — a clear skip line and exit 0. The development host is RT-capable, so it is runnable in practice, and the PR carries the PNG it produced.

## Relationships

- Depends on #202 (PR #204) for the AS surface and #206 (PR #211) for the AABB coverage.
- Benefits from #205 (PR #208): the sample's barrier spans are `stackalloc`, which is only legal since `PipelineBarrier`'s parameters became `scoped`.
- Sidesteps #209: the sample is compute + `PushDescriptorSet`, not `BeginRendering`, so it does not hit the per-frame `ColorAttachment[]` allocation.
- Produces a follow-up: `SlangSessionDescription` has no way to declare target capabilities, so every ray-query compile warns.

## Found during implementation

**Slang emits the entry point as `main`, not `computeMain`.** The Slang-side name is `computeMain`; SPIR-V keeps the GLSL-style default regardless of the source-language name, so `ComputePipelineBuilder.WithEntryPoint("computeMain"u8)` fails with `VUID-VkPipelineShaderStageCreateInfo-pName-00707` ("The only entry point found was `main`"). The builder's default is already `main`, so the fix is to not call `WithEntryPoint` at all. Recorded in the sample at the call site, since it is the kind of thing that costs an hour once per person.

**`PhysicalDeviceInfo.SupportsExtension` has no `Utf8Name` overload,** only `ReadOnlySpan<byte>`, so the picker cannot use the `VulkanExtensions.Khr*` constants and falls back to `"…"u8` literals. Same bytes, same invariant, slightly worse ergonomics. Not fixed here — noted so it is a known gap rather than a puzzle.

## Open items

None. Both unknowns — does the shipped Slang compile ray query, and can the profile be set explicitly — were resolved by probe before this spec was written, and the capability gap they exposed is fixed in this change rather than deferred.
