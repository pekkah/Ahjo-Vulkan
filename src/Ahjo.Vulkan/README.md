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

## Repository

Source, issues, samples: <https://github.com/pekkah/ahjo-vulkan>

## License

MIT. © Pekka Heikura.
