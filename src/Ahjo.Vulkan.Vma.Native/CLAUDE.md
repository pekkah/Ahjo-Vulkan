# Ahjo.Vulkan.Vma.Native — generated bindings + native binary

`Generated/` is ClangSharp output from `vk_mem_alloc.h` at the pinned `VmaVersion` in `Directory.Build.props`. **Never hand-edit anything under `Generated/`** — it is overwritten wholesale on the next regen.

To change the bindings: edit `tools/generate-vma.rsp` (notes in `tools/generate-vma.notes.md`), bump `VmaVersion` if needed, then:

```bash
dotnet build src/Ahjo.Vulkan.Vma.Native -t:Regenerate   # needs cmake on PATH
```

The package ships prebuilt `vma.dll` / `libvma.so` built from `native/vma/`. `-p:SkipVmaNativeBuild=true` skips the cmake step when consuming a pre-staged binary.

Linux binaries are exercised by the `vma-linux` CI lane (lavapipe, allocation-only) before they reach NuGet — issue #144 shipped a `libvma.so` that SIGSEGVed on first use because nothing ever executed it. Don't remove or bypass that lane; see `.github/CLAUDE.md`.
