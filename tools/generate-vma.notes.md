# `generate-vma.rsp` notes

ClangSharp's response-file parser doesn't support `#` comments, so context
that would normally live in the file is captured here.

## Path

`--file native/vma/include/vk_mem_alloc.h` — the header is staged to a
version-independent path by `Ahjo.Vulkan.Vma.Native.csproj`'s `FetchVma`
target. The version itself lives in `Directory.Build.props` (`VmaVersion`).
Bumping the tag does not require touching this response file.

## Why `--language cpp`

`vk_mem_alloc.h` carries `#ifdef __cplusplus` guards around the C API.
Parsing as `cpp` exposes the C ABI we care about plus VMA's enum
storage attributes; switching to `c` drops methods VMA only declares
inside the `extern "C"` block.

## Why route every `Vk*` back to `Ahjo.Vulkan.Native`

Without `--remap`, ClangSharp would emit parallel `VkBuffer`,
`VkResult`, … definitions inside `Ahjo.Vulkan.Vma.Native`. Code mixing
the two packages would then have to choose one set or hand-cast at
every boundary. The remaps point each VMA-referenced Vulkan symbol at
the existing definition in `Ahjo.Vulkan.Native`.

If a regen pass produces a duplicate `Vk*` under
`src/Ahjo.Vulkan.Vma.Native/Generated/`, add a remap for it here and
regenerate. The list grows slowly — VMA touches roughly 30 Vulkan
types.
