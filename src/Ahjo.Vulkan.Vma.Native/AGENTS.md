# Ahjo.Vulkan.Vma.Native — generated bindings + native binary

`Generated/` is ClangSharp output from `vk_mem_alloc.h` at the pinned `VmaVersion` in `Directory.Build.props`. **Never hand-edit anything under `Generated/`** — it is overwritten wholesale on the next regen.

To change the bindings: edit `tools/generate-vma.rsp` (notes in `tools/generate-vma.notes.md`), bump `VmaVersion` if needed, then:

```bash
dotnet build src/Ahjo.Vulkan.Vma.Native -t:Regenerate   # needs cmake on PATH
```

The package ships prebuilt `vma.dll` / `libvma.so` built from `native/vma/`. `-p:SkipVmaNativeBuild=true` skips the cmake step when consuming a pre-staged binary. The binary rebuilds, in a cleared build dir, whenever `VmaVersion` or `VulkanHeadersVersion` changes (`native/vma/build/<rid>/vma.version` records both); the staged header carries a committed `native/vma/include/vma.version` (#256). Source edits to `native/vma/src/vma.cpp` / `CMakeLists.txt` are not version changes — delete `native/vma/build/<rid>/` by hand to pick them up locally. A shipped RID's binary missing from `native/vma/build/<rid>/` fails `dotnet pack` with `AHJOPACK001`, and `ci.yml`'s `native-pack` job verifies the built nupkg on every PR (#169, `tools/AGENTS.md`).

Linux binaries are exercised by the `vma-linux` CI lane (lavapipe, allocation-only) before they reach NuGet — issue #144 shipped a `libvma.so` that SIGSEGVed on first use because nothing ever executed it. Don't remove or bypass that lane; see `.github/AGENTS.md`.
