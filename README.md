# Ahjo.Vulkan

.NET bindings and a low-allocation C# wrapper for [Vulkan](https://www.vulkan.org/),
part of the [Ahjo](https://github.com/pekkah) game engine. Published on
NuGet as two packages:

- [**Ahjo.Vulkan**](https://www.nuget.org/packages/Ahjo.Vulkan) — the idiomatic
  C# wrapper. What most callers want.
- [**Ahjo.Vulkan.Native**](https://www.nuget.org/packages/Ahjo.Vulkan.Native)
  — raw P/Invoke bindings against `vulkan.h`. Pulled in transitively by
  `Ahjo.Vulkan`; referenced directly only if you want to bypass the
  wrapper. The Vulkan loader is platform-supplied (Vulkan SDK / VulkanRT
  on Windows, `libvulkan1` on Linux, MoltenVK on macOS) — no native
  binary ships in the nupkg.
- [**Ahjo.Vulkan.Vma**](https://www.nuget.org/packages/Ahjo.Vulkan.Vma) +
  [**Ahjo.Vulkan.Vma.Native**](https://www.nuget.org/packages/Ahjo.Vulkan.Vma.Native)
  — optional integration of [AMD VulkanMemoryAllocator](https://github.com/GPUOpen-LibrariesAndSDKs/VulkanMemoryAllocator).
  VMA is C++ header-only, so the `.Native` package ships a prebuilt
  SHARED library (`vma.{dll,so,dylib}`) for every supported RID under
  `runtimes/<rid>/native/`. Versioned independently from core via the
  `vma-v*` tag prefix.

Project folders, csproj filenames, `AssemblyName`, `RootNamespace`, and
NuGet `PackageId` all use the dotted `Ahjo.Vulkan*` form — one canonical
spelling everywhere.

This sister project parallels [`ahjo-wgpu`](https://github.com/pekkah/wgpu-sharp)
and follows the same architecture: ClangSharp-generated P/Invokes,
ref-struct wrapper, `Directory.Build.props` central config, MinVer
version derivation, GitHub Actions for CI + tag-driven NuGet publish.

## Layout

- `src/Ahjo.Vulkan.Native` — ClangSharp-generated P/Invokes against `vulkan.h`. Regenerated via `dotnet build -t:Regenerate`. Ships as `Ahjo.Vulkan.Native`.
- `src/Ahjo.Vulkan` — idiomatic wrapper. Ships as `Ahjo.Vulkan`.
- `src/Ahjo.Vulkan.Vma.Native` — ClangSharp-generated P/Invokes against `vk_mem_alloc.h`. Builds + ships `vma.{dll,so,dylib}` for every RID. Ships as `Ahjo.Vulkan.Vma.Native`.
- `src/Ahjo.Vulkan.Vma` — idiomatic VMA wrapper (`Allocator`, `Allocation`, `AllocatedBuffer`, `MappedRegion`). Ships as `Ahjo.Vulkan.Vma`.
- `src/Ahjo.Vulkan.Utilities` — dep-free helpers usable from samples and tests. Not published.
- `native/vma/` — VMA impl translation unit (`src/vma.cpp`) + `CMakeLists.txt`. Source for the SHARED library packaged in `Ahjo.Vulkan.Vma.Native`.
- `tests/Ahjo.Vulkan.Native.Tests` — xUnit smoke suite over the raw bindings.
- `tests/Ahjo.Vulkan.Tests` — xUnit integration tests over the wrapper.

## Design principles

Aimed at games. **Low allocation, raw-pointer friendly, minimal ceremony.**
Typical .NET safety (SafeHandle, Task-based async, defensive null checks)
is not a goal — perf and zero per-frame allocations take precedence.

## Getting started

```bash
dotnet tool restore
dotnet build src/Ahjo.Vulkan.Native -t:Regenerate       # Vulkan headers + bindings
dotnet build src/Ahjo.Vulkan.Vma.Native -t:Regenerate   # VMA headers + bindings (also needs cmake on PATH)
dotnet build Ahjo.Vulkan.slnx
dotnet test
```

Building the VMA `.Native` project locally invokes `cmake` to compile
`vma.{dll,so,dylib}` for the host RID. Pass `-p:SkipVmaNativeBuild=true`
to skip that step (e.g. when consuming a pre-staged binary in CI).

## Release tagging

| Package set                                       | Tag prefix |
|---------------------------------------------------|------------|
| `Ahjo.Vulkan` + `Ahjo.Vulkan.Native`              | `v*`       |
| `Ahjo.Vulkan.Vma` + `Ahjo.Vulkan.Vma.Native`      | `vma-v*`   |

VMA is versioned independently because its release cadence and ABI
churn don't align with Vulkan-Headers'.

Requires the .NET 10 SDK (see `global.json`) and a system Vulkan loader.

## Status

Pre-1.0. Public API may shift between 0.x releases; pin your
`PackageReference` to an exact version.
