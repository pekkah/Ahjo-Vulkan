# Ahjo.Vulkan.Vma.Native

Raw P/Invoke bindings for [AMD VulkanMemoryAllocator](https://github.com/GPUOpen-LibrariesAndSDKs/VulkanMemoryAllocator),
generated from `vk_mem_alloc.h` via [ClangSharp](https://github.com/dotnet/ClangSharp).
1:1 with the C ABI.

VMA is a C++ header-only library; this package ships a prebuilt SHARED
library compiled from a single `VMA_IMPLEMENTATION` translation unit, for
every supported RID under `runtimes/<rid>/native/`. No separate per-platform
package to reference, no compile step on the consumer side.

## Most people want `Ahjo.Vulkan.Vma` instead

This package exposes the raw C ABI. If you want ergonomics — `Allocator`,
`AllocatedBuffer`, `MappedRegion` — install
[`Ahjo.Vulkan.Vma`](https://www.nuget.org/packages/Ahjo.Vulkan.Vma).

## Install

```shell
dotnet add package Ahjo.Vulkan.Vma.Native
```

## Bundled platforms

| RID           | Binary           |
|---------------|------------------|
| `win-x64`     | `vma.dll`        |
| `win-arm64`   | `vma.dll`        |
| `linux-x64`   | `libvma.so`      |
| `linux-arm64` | `libvma.so`      |

macOS RIDs (`osx-x64`, `osx-arm64`) are not currently shipped — the
MoltenVK runtime path needs validation before adding them.

TFM: `net10.0`. Native ABI tracks the pinned VMA release version.

## Runtime requirements

VMA loads Vulkan entry points dynamically through
`vkGetInstanceProcAddr` / `vkGetDeviceProcAddr` — supplied by the caller
via `VmaVulkanFunctions`. You still need the Vulkan loader installed at
runtime (provided by the `Ahjo.Vulkan.Native` dependency).

## License

MIT. © Pekka Heikura. VulkanMemoryAllocator itself is licensed MIT.
