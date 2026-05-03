# Ahjo.Vulkan.Native

Raw P/Invoke bindings for [Vulkan](https://www.vulkan.org/), generated from
the canonical [Khronos Vulkan-Headers](https://github.com/KhronosGroup/Vulkan-Headers)
via [ClangSharp](https://github.com/dotnet/ClangSharp). 1:1 with the C ABI.

The Vulkan **loader** is platform-supplied — this package does not bundle a
loader binary. A `DllImportResolver` registered at module-load maps the
canonical `vulkan-1` library name to the right per-OS soname so
`DllImport` resolves cleanly without consumer setup.

## Most people want `Ahjo.Vulkan` instead

This package exposes the raw C ABI: function pointers, `unsafe`
`Vk*Info` structs, manual handle release, p-next chains. Useful if you
want to write your own wrapper on top, or fine-tune at the ABI layer.

If you just want to use Vulkan from C#, install
[`Ahjo.Vulkan`](https://www.nuget.org/packages/Ahjo.Vulkan) — it takes a
dependency on this package and layers an idiomatic `ref struct`-based
API on top.

## Install

```shell
dotnet add package Ahjo.Vulkan.Native
```

## Runtime requirements

| Platform | Loader source                                                 |
|----------|---------------------------------------------------------------|
| Windows  | GPU drivers ship `vulkan-1.dll`, or install the Vulkan SDK / VulkanRT |
| Linux    | `libvulkan1` (Debian/Ubuntu) or distro equivalent             |

The loader resolver code already understands MoltenVK / `libvulkan.dylib`
on macOS, but macOS isn't a tested target yet — revisit when surface
bindings + a Mac sample land.

TFM: `net10.0`. Native ABI tracks the pinned Vulkan-Headers release
version; bumps are minor-version bumps here.

## Status

Pre-1.0. The managed surface is generated and stable per Vulkan-Headers
release.

## Repository

Source, issues, generator response file
([`tools/generate.rsp`](https://github.com/pekkah/ahjo-vulkan/blob/main/tools/generate.rsp)),
regeneration instructions: <https://github.com/pekkah/ahjo-vulkan>

## License

MIT. © Pekka Heikura. Vulkan-Headers themselves are licensed Apache-2.0.
