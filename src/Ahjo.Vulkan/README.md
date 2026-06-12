# Ahjo.Vulkan

Idiomatic C# wrapper over [Vulkan](https://www.vulkan.org/) with
integrated [AMD VulkanMemoryAllocator](https://github.com/GPUOpen-LibrariesAndSDKs/VulkanMemoryAllocator).
Built for games: `ref struct` command-buffer recorders, `readonly struct`
resource handles, zero heap allocations on per-frame paths. Buffer/image
creation pairs `VkBuffer`/`VkImage` with its VMA allocation handle in a
single type so you never juggle the two halves manually.

> **Status: pre-1.0.** The public surface may shift between 0.x releases
> as the wrapper fills in remaining Vulkan coverage. Tag your
> `PackageReference` to an exact version.

## Install

```shell
dotnet add package Ahjo.Vulkan
```

The Vulkan loader is platform-supplied — see
[`Ahjo.Vulkan.Native`](https://www.nuget.org/packages/Ahjo.Vulkan.Native)
for runtime requirements (Windows GPU drivers / `libvulkan1` on Linux /
MoltenVK on macOS). The VMA shared library ships with the transitive
[`Ahjo.Vulkan.Vma.Native`](https://www.nuget.org/packages/Ahjo.Vulkan.Vma.Native)
dependency — no extra setup.

## Platforms

Runs on Windows and Linux (x64, arm64) against a system Vulkan 1.4
loader. TFM: `net10.0`. macOS support (via MoltenVK) is on the roadmap
but not currently tested.

## Design principles

Games-first. Low allocation, raw-pointer friendly, minimal ceremony.

This is an **opinionated** wrapper — not a SafeHandle-shaped .NET port.
Same load-bearing idioms as the Ahjo Wgpu wrapper:

- **Struct handles.** `Buffer`, `Image`, `Pipeline`, etc. are
  `readonly struct`s holding one Vulkan handle. Copy-by-value, no
  finalizer, `default(T)` is a legal null handle, double-dispose is UB.
- **`ref struct` command recorders + span-parameter descriptors.**
  Recorders don't escape methods; spans live on method parameters, not
  descriptor fields, to keep escape-analysis happy.

## Quick start

```csharp
using Ahjo.Vulkan;

ReadOnlySpan<Utf8Name> extensions = stackalloc Utf8Name[]
{
    Utf8Name.FromLiteral("VK_KHR_surface"u8),
};

using var instance = Instance.Create(new InstanceDescription
{
    ApiVersion       = VulkanVersion.V1_4,
    EnableValidation = true,            // appends VK_LAYER_KHRONOS_validation + VK_EXT_debug_utils
    Extensions       = extensions,
    DebugCallback    = msg => Console.Error.WriteLine($"[{msg.Severity}] {msg.Message}"),
});
```

A `default(InstanceDescription)` is also legal — `ApiVersion` falls back to
`VulkanVersion.V1_4` and the wrapper uses a default callback that routes
through `AhjoDiagnostics.Sink` (stderr by default). Every diagnostic the
wrapper emits — debug-utils messages, dispose-time warnings, VMA leak
reports — flows through that one static sink; replace it once at startup to
capture everything in your engine's logger:

```csharp
AhjoDiagnostics.Sink = (severity, source, message) => myLogger.Log(severity, source, message);
```

## Layered design

- **Struct handles.** `Buffer`, `Image`, `Pipeline`, `Queue`, `CommandBuffer`,
  etc. are `readonly struct`s that satisfy `IVulkanHandle<TSelf>` (one or two
  raw `Vk*_T*` fields plus optional creation-time metadata, `default(T)` is a
  legal null handle, copy-by-value, no finalizer). Ownership is part of the
  contract: `OwnsHandle` reports whether `Dispose` destroys; `FromRaw` and
  `default` produce borrowed handles with a no-op `Dispose`. Disposal is
  deterministic at the call site; double-dispose is
  undefined behavior — the wrapper does not zero-on-release. `Instance` and
  `Device` are the exceptions: they are `sealed class`es with finalizers
  because they're once-per-process and worth backstopping.
- **`ChainBuilder<TRoot>`.** Stack-only bump allocator that lays out a Vulkan
  `pNext` chain into a caller-supplied `Span<byte>` (typically
  `stackalloc byte[256]`). Zero heap allocations. The generic constraint
  `T : IChainable<TRoot>` makes "this struct cannot extend that root" a
  compile error, not a runtime check. `SType` is read from a static-abstract
  member — call sites never pass it.
- **`VulkanException` + `VkResult.ThrowIfFailed()`.** Single-success-result
  APIs throw `VulkanException` on anything other than `VK_SUCCESS`; the
  exception carries the `VkResult` and the originating function name. Hot-
  path APIs that legitimately return non-success codes (`VK_INCOMPLETE`,
  `VK_SUBOPTIMAL_KHR`, `VK_TIMEOUT`, …) surface the `VkResult` directly so
  callers can branch without paying for a throw.
- **`Utf8Name.FromLiteral("…"u8)`.** All `const char*` parameters (extension
  names, layer names, debug labels, application names) flow through
  `Utf8Name`. Pass `"…"u8` literals only — they live in the assembly's read-
  only data segment for process lifetime, are null-terminated, and don't
  require GC pinning. Never round-trip a `string` through
  `Encoding.UTF8.GetBytes` for these slots: the resulting `byte[]` is GC-
  movable and not null-terminated, and the pointer Vulkan sees will dangle.
  See the `FromLiteral` XML doc for the full contract. The wrapper's
  `VulkanExtensions` static class exposes ready-made `Utf8Name` constants
  for the names it actively wraps (`KhrSurface`, `KhrWin32Surface`,
  `KhrSwapchain`).

Deeper rationale on each layer lives under
[`docs/superpowers/specs/`](https://github.com/pekkah/Ahjo-Vulkan/tree/main/docs/superpowers/specs)
in the source repo.

## Repository

Source, issues, samples: <https://github.com/pekkah/Ahjo-Vulkan>

## License

MIT. © Pekka Heikura.
