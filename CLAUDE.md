# Ahjo.Vulkan — Claude Project Memory

.NET 10 / C# 14 Vulkan bindings + low-allocation wrapper, aimed at the [Logos game engine](https://github.com/pekkah/logos). Three publishable NuGet packages live in this repo; see `README.md` for the consumer-facing overview.

## Load-bearing invariants

Violating any of these will either break CI or cause a silent runtime bug. Treat them as non-negotiable unless the user explicitly asks otherwise.

### 1. UTF-8 string literals for Vulkan `const char*`

Vulkan APIs that take `const char*` (extension names, layer names, application name, debug labels) require a UTF-8, null-terminated, non-GC-movable pointer. The convention is:

```csharp
Utf8Name.FromLiteral("VK_KHR_surface"u8)
```

`"…"u8` literals live in the assembly's read-only data segment — process lifetime, null-terminated, no GC pinning. **Never** round-trip through `string` + `Encoding.UTF8.GetBytes(...)`: the resulting `byte[]` is GC-movable and not null-terminated, so the pointer Vulkan sees will dangle.

`VulkanExtensions.KhrSurface` / `KhrSwapchain` / etc. expose the names the wrapper actively wraps as ready-made `Utf8Name` values — prefer those over re-quoting the literal at each call site.

### 2. Native AOT must stay clean

`samples/AotSmoke/` is published with `PublishAot=true` in CI and the produced exe runs the full render→PNG round-trip. Trim warnings, ILC errors, or runtime trim-related crashes will fail the build.

Forbidden on any code path reachable from the wrapper:
- `Type.MakeGenericType`, `MethodInfo.MakeGenericMethod`
- Reflection-based discovery (`Assembly.GetTypes()`, attribute scans)
- Dynamic code generation (`System.Reflection.Emit`, `DynamicMethod`, expression trees compiled at runtime)
- Anything that triggers `RequiresUnreferencedCodeAttribute` / `RequiresDynamicCodeAttribute`

See `docs/aot-notes.md` for the full inventory of patterns and the trim-attribute approach.

### 3. Zero per-frame allocations on hot paths

Stated explicitly in `README.md`: "Low allocation, raw-pointer friendly, minimal ceremony… perf and zero per-frame allocations take precedence." This is a hard constraint on:

- `src/Ahjo.Vulkan/Recording/**` — every command-recording call
- `src/Ahjo.Vulkan/Sync/**` — fence/semaphore operations
- `src/Ahjo.Vulkan/Pools/**` — `FrameRing`, `CommandBufferPool`, descriptor pools
- `src/Ahjo.Vulkan/Memory/**` — `StagingUploader`, `MappedRegion`, `ChainBuilder`
- Any other API expected to run inside a per-frame loop

`tests/Ahjo.Vulkan.Benchmarks/` has a `[MemoryDiagnoser]` benchmark per hot-path subsystem and `docs/benchmarks.md` records the baseline (every `Allocated` cell should read `-`). When changing a hot path, run the matching benchmark or use the `bench-coverage-checker` agent to confirm coverage hasn't slipped.

Setup-time allocations (constructors, builder finalization, one-shot config) are fine. The constraint is per-frame, not lifetime.

### 4. Generated code is generated — never hand-edit

These directories are output of the codegen tools and get overwritten on the next regen:

- `src/Ahjo.Vulkan.Native/Generated/` — ClangSharp P/Invokes from `vulkan.h`
- `src/Ahjo.Vulkan.Vma.Native/Generated/` — ClangSharp P/Invokes from `vk_mem_alloc.h`
- `native/downloaded/` — pinned Vulkan-Headers + VMA tarball cache

To change the bindings, edit the `*.rsp` files under `tools/`, bump the version in `Directory.Build.props` if needed, then regenerate:

```bash
dotnet build src/Ahjo.Vulkan.Native -t:Regenerate       # Vulkan
dotnet build src/Ahjo.Vulkan.Vma.Native -t:Regenerate   # VMA (also needs cmake on PATH)
```

Both packages ship under a single `v*` tag, so bump deliberately — see `Directory.Build.props` for the pinned `VulkanHeadersVersion` and `VmaVersion`.

### 5. `TreatWarningsAsErrors=true`

Set repo-wide in `Directory.Build.props` with `AnalysisLevel=latest`. Analyzer warnings break the build. Don't suppress diagnostics with `#pragma warning disable` to make code green — fix the underlying issue or move the suppression into a justified, file-scoped attribute with a comment.

## Project shape (quick reference)

```
src/
  Ahjo.Vulkan/                 idiomatic wrapper (Memory/, Recording/, Sync/, Pools/, Pipelines/, Resources/, …)
  Ahjo.Vulkan.Native/          ClangSharp P/Invokes against vulkan.h
  Ahjo.Vulkan.Vma.Native/      ClangSharp P/Invokes against vk_mem_alloc.h + prebuilt vma.{dll,so}
  Ahjo.Vulkan.Utilities/       dep-free helpers for samples/tests (not published)

native/vma/                    VMA impl translation unit + CMakeLists.txt
samples/                       HelloTriangle, HelloCube, HelloVma, HelloVmaWindowed, HeadlessTriangle, AotSmoke
tests/Ahjo.Vulkan.Tests/       xUnit integration suite over the wrapper
tests/Ahjo.Vulkan.Native.Tests xUnit smoke suite over raw bindings
tests/Ahjo.Vulkan.Benchmarks/  BenchmarkDotNet — zero-allocation regression canary
tools/                         StructExtendsGen + generate-vma.rsp + generate.rsp (codegen config)
docs/superpowers/              spec-driven design docs (specs/ + plans/, paired per issue)
```

## Common commands

```bash
# Restore + build + test
dotnet tool restore
dotnet build Ahjo.Vulkan.slnx
dotnet test

# Regenerate bindings (after a Directory.Build.props version bump)
dotnet build src/Ahjo.Vulkan.Native -t:Regenerate
dotnet build src/Ahjo.Vulkan.Vma.Native -t:Regenerate

# Skip VMA native build (e.g. when consuming a pre-staged binary)
dotnet build Ahjo.Vulkan.slnx -p:SkipVmaNativeBuild=true

# Run benchmarks (filter syntax matches benchmark class names)
dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter "*ChainBuilder*"

# AOT smoke locally (Windows; needs MSVC env via vcvars or VS dev shell)
dotnet publish samples/AotSmoke/AotSmoke.csproj -c Release -r win-x64 -p:IlcUseEnvironmentalTools=true
```

## CI

The **wrapper test suite** runs on `windows-latest` only. Linux is parked for it — issue #32 established that SwiftShader on Linux SIGSEGVs mid-suite across every loader+build combination tested, and gating driver-dependent tests behind a software rasterizer isn't honest coverage. When a self-hosted Linux runner with real Vulkan drivers becomes available, the Linux job can come back.

Windows CI provisions the Khronos Vulkan loader + Silk.NET-packaged SwiftShader ICD and routes the loader at it via `VK_DRIVER_FILES`.

The one exception is the `vma-linux` lane (both Linux RIDs, Mesa lavapipe), which runs `Ahjo.Vulkan.Vma.Native.Tests` and nothing else. It is a **build-artifact check, not wrapper coverage**, and does not reopen the issue-32 decision: `Ahjo.Vulkan.Vma.Native` publishes Linux binaries, so something has to execute one before it reaches NuGet. Issue #144 shipped a `libvma.so` that SIGSEGVed on the first `vmaCreateAllocator` precisely because nothing ever did. Allocation-only work is both what lavapipe handles reliably and what actually broke, which is why the lane stops there — don't grow it into a general Linux test lane. It sets `AHJO_REQUIRE_VULKAN_DEVICE=1`, which turns the suite's driverless skip into a hard failure so a broken ICD install can't report green while executing nothing.

`publish.yml` ships preview packages on `push:main` (MinVer-derived pre-release version) and stable packages on `release:published` events. Tag with `v0.x.y` → create a GitHub release → all three packages publish under that single tag.

## Spec-driven workflow

Non-trivial design work goes through `docs/superpowers/`:

- `docs/superpowers/specs/YYYY-MM-DD-issue-NN-<topic>-design.md` — design spec, "what and why"
- `docs/superpowers/plans/YYYY-MM-DD-issue-NN-<topic>.md` — implementation plan, "how"

Specs paired with issues (currently issues #06 instance creation, #07 physical device, #08 device creation). When the user asks for a new spec, follow that naming convention.

## Commit + PR style

Recent commits use `<area>: <imperative>` — examples from the log: `CI: enable auto-publish`, `Packaging: add LICENSE + Source Link`, `Versioning: ship all three packages under one v* tag`. Match that shape.

## Custom agents in this repo

- `.claude/agents/vulkan-validation-reviewer.md` — reviews diffs for the bugs `VK_LAYER_KHRONOS_validation` catches at runtime (image layouts, sync2 masks, queue ownership, descriptor lifetime, fence/semaphore signaling, VMA lifetime, pNext validity, UTF-8 string lifetime).
- `.claude/agents/bench-coverage-checker.md` — given a diff touching hot-path code, checks whether a matching benchmark in `tests/Ahjo.Vulkan.Benchmarks/` was updated and flags allocation smells.

Both kick in automatically on relevant diffs; invoke explicitly when reviewing a PR.

## What lives in `~/.claude/projects/.../memory/` (auto-memory)

Long-running project context that's likely to drift (issue numbers, design decisions in flight, user-specific preferences) lives in auto-memory, not in this file. This `CLAUDE.md` is the **stable** layer — invariants that hold across sessions and that anyone working in the repo should follow.
