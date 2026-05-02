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

This sister project parallels [`ahjo-wgpu`](https://github.com/pekkah/wgpu-sharp)
and follows the same architecture: ClangSharp-generated P/Invokes,
ref-struct wrapper, `Directory.Build.props` central config, MinVer
version derivation, GitHub Actions for CI + tag-driven NuGet publish.

## Layout

- `src/AhjoVulkan.Native` — ClangSharp-generated P/Invokes against `vulkan.h`. Regenerated via `dotnet build -t:Regenerate`. Ships as `Ahjo.Vulkan.Native`.
- `src/AhjoVulkan` — idiomatic wrapper. Ships as `Ahjo.Vulkan`.
- `src/AhjoVulkan.Utilities` — dep-free helpers usable from samples and tests. Not published.
- `tests/AhjoVulkan.Native.Tests` — xUnit smoke suite over the raw bindings.
- `tests/AhjoVulkan.Tests` — xUnit integration tests over the wrapper.

## Design principles

Aimed at games. **Low allocation, raw-pointer friendly, minimal ceremony.**
Typical .NET safety (SafeHandle, Task-based async, defensive null checks)
is not a goal — perf and zero per-frame allocations take precedence.

## Getting started

```bash
dotnet tool restore
dotnet build src/AhjoVulkan.Native -t:Regenerate   # downloads Vulkan-Headers + generates bindings
dotnet build AhjoVulkan.slnx
dotnet test
```

Requires the .NET 10 SDK (see `global.json`) and a system Vulkan loader.

## Status

Pre-1.0. Public API may shift between 0.x releases; pin your
`PackageReference` to an exact version.
