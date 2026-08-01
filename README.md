# Ahjo.Vulkan

.NET bindings and a low-allocation C# wrapper for [Vulkan](https://www.vulkan.org/),
part of the [Ahjo](https://github.com/pekkah) game engine. Published on
NuGet as six packages:

- [**Ahjo.Vulkan**](https://www.nuget.org/packages/Ahjo.Vulkan) — the idiomatic
  C# wrapper. What most callers want. Vulkan + integrated
  [AMD VulkanMemoryAllocator](https://github.com/GPUOpen-LibrariesAndSDKs/VulkanMemoryAllocator)
  are coupled in one wrapper surface (`Allocator`, `AllocatedBuffer`,
  `AllocatedImage`, `MappedRegion` live here).
- [**Ahjo.Vulkan.Native**](https://www.nuget.org/packages/Ahjo.Vulkan.Native)
  — raw P/Invoke bindings against `vulkan.h`. Pulled in transitively by
  `Ahjo.Vulkan`; referenced directly only if you want to bypass the
  wrapper. The Vulkan loader is platform-supplied (Vulkan SDK / VulkanRT
  on Windows, `libvulkan1` on Linux) — no native binary ships in the
  nupkg.
- [**Ahjo.Vulkan.Vma.Native**](https://www.nuget.org/packages/Ahjo.Vulkan.Vma.Native)
  — raw P/Invoke bindings against `vk_mem_alloc.h`, plus prebuilt VMA
  SHARED library (`vma.{dll,so}`) for Windows + Linux (x64, arm64) under
  `runtimes/<rid>/native/`. Pulled in transitively by `Ahjo.Vulkan`.
  Shares the same `v*` tag versioning as the rest of the stack — all
  six packages release together. macOS RIDs aren't shipped today;
  the MoltenVK runtime path needs validation before adding them.
- [**Ahjo.Vulkan.Ktx.Native**](https://www.nuget.org/packages/Ahjo.Vulkan.Ktx.Native)
  — raw P/Invoke bindings against Khronos `libktx`'s `ktx.h` (KTX2
  container read + Basis Universal transcode), plus prebuilt
  `ktx.dll` / `libktx.so` for `win-x64` and `linux-x64` under
  `runtimes/<rid>/native/`. **Standalone**: it is not pulled in by
  `Ahjo.Vulkan`, and has no Vulkan dependency of its own — the library is
  built with `KTX_FEATURE_VK_UPLOAD=OFF` so the binding's contract ends at
  transcoded bytes plus a `vkFormat`, leaving image creation and barriers
  to the caller. Reference it directly if you need it.
- [**Ahjo.Vulkan.Slang.Native**](https://www.nuget.org/packages/Ahjo.Vulkan.Slang.Native)
  — raw P/Invoke bindings against the [Slang](https://github.com/shader-slang/slang)
  shader compiler's `slang.h` (compile Slang/HLSL to SPIR-V, plus shader
  reflection), plus prebuilt `slang.dll` + `slang-compiler.dll` /
  `libslang.so` for `win-x64` and `linux-x64` under
  `runtimes/<rid>/native/`. **Standalone**: it is not pulled in by
  `Ahjo.Vulkan` and has no Vulkan dependency of its own — a consumer
  shipping precompiled SPIR-V never pulls ~25 MB of compiler. The binaries
  are upstream's own release artifacts, pinned by tag *and* by SHA-256.
  Reference it directly if you need it.
- [**Ahjo.Vulkan.Slang**](https://www.nuget.org/packages/Ahjo.Vulkan.Slang)
  — the idiomatic wrapper over the Slang compiler: compile Slang/HLSL source
  to SPIR-V at run time and hand the words straight to
  `Device.CreateShaderModule`. Compiler diagnostics come back as exceptions
  carrying Slang's own text — there is no path that returns an empty blob on
  failure. **Standalone**: `Ahjo.Vulkan` does not pull it in, so a consumer
  shipping precompiled SPIR-V never pays for it. Reference it directly if you
  want to compile shaders at run time.

Project folders, csproj filenames, `AssemblyName`, `RootNamespace`, and
NuGet `PackageId` all use the dotted `Ahjo.Vulkan*` form — one canonical
spelling everywhere.

This sister project parallels [`ahjo-wgpu`](https://github.com/pekkah/wgpu-sharp)
and follows the same architecture: ClangSharp-generated P/Invokes,
ref-struct wrapper, `Directory.Build.props` central config, MinVer
version derivation, GitHub Actions for CI + tag-driven NuGet publish.

## Quick start

```csharp
using Ahjo.Vulkan;

ReadOnlySpan<Utf8Name> extensions = stackalloc Utf8Name[]
{
    Utf8Name.FromLiteral("VK_KHR_surface"u8),
    VulkanExtensions.KhrWin32Surface,
};

using var instance = Instance.Create(new InstanceDescription
{
    ApplicationName  = Utf8Name.FromLiteral("hello"u8),
    ApiVersion       = VulkanVersion.V1_4,
    EnableValidation = true,
    Extensions       = extensions,
    DebugCallback    = msg => Console.Error.WriteLine($"[{msg.Severity}] {msg.Message}"),
});
```

`"…"u8` literals live in the assembly's read-only data segment (process
lifetime, null-terminated, no GC pinning); `Utf8Name.FromLiteral` wraps the
pointer so a `params`/span list can carry them. **Never** round-trip an
extension name through `string` + `Encoding.UTF8.GetBytes` — the resulting
buffer is GC-movable and not null-terminated, so the pointer Vulkan sees
will dangle. `VulkanExtensions.KhrSurface` / `KhrSwapchain` / etc. expose
the names the wrapper actively wraps as ready-made `Utf8Name` values.

Full design rationale (instance lifecycle, validation wiring, callback
contract): [`docs/design/specs/2026-05-04-issue-06-instance-creation-design.md`](docs/design/specs/2026-05-04-issue-06-instance-creation-design.md).
Porting from Vortice.Vulkan: [`docs/migration-vortice-to-ahjo.md`](docs/migration-vortice-to-ahjo.md).
Other specs and plans live under [`docs/design/specs/`](docs/design/specs/) and [`docs/design/plans/`](docs/design/plans/).

## Layout

- `src/Ahjo.Vulkan.Native` — ClangSharp-generated P/Invokes against `vulkan.h`. Regenerated via `dotnet build -t:Regenerate`. Ships as `Ahjo.Vulkan.Native`.
- `src/Ahjo.Vulkan.Vma.Native` — ClangSharp-generated P/Invokes against `vk_mem_alloc.h`. Builds + ships `vma.{dll,so}` for every RID. Ships as `Ahjo.Vulkan.Vma.Native`.
- `src/Ahjo.Vulkan.Ktx.Native` — ClangSharp-generated P/Invokes against Khronos `ktx.h`. Builds + ships `ktx.dll` / `libktx.so` for `win-x64` + `linux-x64`. Ships as `Ahjo.Vulkan.Ktx.Native`.
- `src/Ahjo.Vulkan.Slang.Native` — ClangSharp-generated P/Invokes against the Slang compiler's `slang.h`. Stages + ships `slang.dll` + `slang-compiler.dll` / `libslang.so` for `win-x64` + `linux-x64` from the pinned, checksum-verified upstream release. Ships as `Ahjo.Vulkan.Slang.Native`.
- `src/Ahjo.Vulkan.Slang` — idiomatic wrapper over the Slang compiler: `SlangCompiler`/`SlangSession`/`SlangModule`/`SlangProgram`, diagnostics as exceptions, SPIR-V as a `ReadOnlySpan<uint>`. Ships as `Ahjo.Vulkan.Slang`.
- `src/Ahjo.Vulkan` — idiomatic wrapper covering both Vulkan and VMA (`Memory/Allocator.cs`, `Memory/AllocatedBuffer.cs`, …). Ships as `Ahjo.Vulkan`.
- `src/Ahjo.Vulkan.Utilities` — dep-free helpers usable from samples and tests. Not published.
- `native/vma/` — VMA impl translation unit (`src/vma.cpp`) + `CMakeLists.txt`. Source for the SHARED library packaged in `Ahjo.Vulkan.Vma.Native`.
- `native/ktx/` — staged libktx headers (`include/`, the generator input of record) + parse-time `stubs/`. Unlike VMA there is no translation unit of ours: the build drives KTX-Software's own CMake at the pinned tag.
- `native/slang/` — staged Slang headers (`include/`, the generator input of record) + parse-time `stubs/`. Nothing is built here at all: the binaries come from upstream's release archive, verified against a pinned SHA-256 before extraction.
- `tests/Ahjo.Vulkan.Native.Tests` — xUnit smoke suite over the raw bindings.
- `tests/Ahjo.Vulkan.Tests` — xUnit integration tests over the wrapper.
- `tests/Ahjo.Vulkan.Ktx.Native.Tests` — xUnit smoke suite that loads the packaged libktx and transcodes a pinned KTX2 fixture. Runs per RID in the job that builds the binary.
- `tests/Ahjo.Vulkan.Slang.Native.Tests` — xUnit smoke suite that loads the packaged Slang compiler, compiles a shader to SPIR-V, walks its reflection, and checks that every deprecated reflection export the stack depends on is still present in the binary. Runs per RID in the job that stages the binary.
- `tests/Ahjo.Vulkan.Slang.Tests` — xUnit suite over the Slang wrapper: compile from source and from file, diagnostics-as-exceptions, warnings on success, every optimization level, compiler lifetime, and one driver-gated test that feeds the result to `Device.CreateShaderModule`.
- `tests/Shared` — the declared Vulkan capability tier (`AHJO_VULKAN_TIER`) shared by the Vulkan-touching suites. Most of the wrapper suite needs a device, so what a green run actually covered depends on the tier the lane declared: see [`docs/ci-coverage.md`](docs/ci-coverage.md).

## Design principles

Aimed at games. **Low allocation, raw-pointer friendly, minimal ceremony.**
Typical .NET safety (SafeHandle, Task-based async, defensive null checks)
is not a goal — perf and zero per-frame allocations take precedence.

## Getting started

```bash
dotnet tool restore
dotnet build src/Ahjo.Vulkan.Native -t:Regenerate       # Vulkan headers + bindings
dotnet build src/Ahjo.Vulkan.Vma.Native -t:Regenerate   # VMA headers + bindings (also needs cmake on PATH)
dotnet build src/Ahjo.Vulkan.Ktx.Native -t:Regenerate   # libktx headers + bindings (needs git; cmake to build)
dotnet build src/Ahjo.Vulkan.Slang.Native -t:Regenerate # Slang headers + bindings (downloads the pinned release archive)
dotnet build Ahjo.Vulkan.slnx
dotnet test
```

Building the VMA `.Native` project locally invokes `cmake` to compile
`vma.{dll,so,dylib}` for the host RID. Pass `-p:SkipVmaNativeBuild=true`
to skip that step (e.g. when consuming a pre-staged binary in CI).

The KTX `.Native` project works the same way, with `-p:SkipKtxNativeBuild=true`
as its escape hatch. Its first build additionally makes a shallow, blobless,
sparse `git clone` of KTX-Software at the pinned tag (~170 MB, `tests/`
excluded) — a source archive is not an option there, because KTX derives its
version from `git describe` and fails to configure without a repository. The
staged binary at `native/ktx/staged/<rid>/` is the cache: once it exists,
neither the clone nor cmake runs again until the pinned version changes.

The Slang `.Native` project builds nothing, but its first build downloads the
~77 MB upstream release archive for the host RID and verifies its SHA-256
against `SlangWinX64Sha256` / `SlangLinuxX64Sha256` before extracting the two
or three files that actually ship. `-p:SkipSlangNativeFetch=true` is the escape
hatch. `native/slang/staged/<rid>/` is the cache, same as KTX's.

## Release tagging

All six packages — `Ahjo.Vulkan`, `Ahjo.Vulkan.Native`,
`Ahjo.Vulkan.Vma.Native`, `Ahjo.Vulkan.Ktx.Native`,
`Ahjo.Vulkan.Slang.Native` and `Ahjo.Vulkan.Slang` — share a single `v*` tag
and release together.
A `git tag v0.1.0 && git push origin v0.1.0` ships the whole stack.

The underlying VMA C++ library version (independent of the package
version) is pinned in `Directory.Build.props` as `VmaVersion`; bump it
deliberately and regenerate the bindings.

Requires the .NET 10 SDK (see `global.json`) and a system Vulkan loader.

## Status

Pre-1.0. Public API may shift between 0.x releases; pin your
`PackageReference` to an exact version.
