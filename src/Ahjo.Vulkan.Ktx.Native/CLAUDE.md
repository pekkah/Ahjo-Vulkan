# Ahjo.Vulkan.Ktx.Native — generated bindings + native binary

`Generated/` is ClangSharp output from Khronos `ktx.h` at the pinned `KtxVersion` in `Directory.Build.props`. **Never hand-edit anything under `Generated/`** — it is overwritten wholesale on the next regen.

To change the bindings: edit `tools/generate-ktx.rsp`, bump `KtxVersion` if needed, then:

```bash
dotnet build src/Ahjo.Vulkan.Ktx.Native -t:Regenerate   # needs git; cmake to build the binary
```

The libktx feature set is defined ONCE as `KtxCMakeFeatureFlags` in `Directory.Build.props` — both the local host build and every CI job use it, so flags can't drift. The contract is "read a KTX2 container and transcode Basis Universal": both GL and Vulkan uploaders are OFF, so the binary must not need a graphics API or ICD. The `ktx-native` CI lane builds + tests each RID's binary with no loader installed, on purpose — if a test suddenly needs one, something got linked in that shouldn't be. See `.github/CLAUDE.md`.

The staged binary under `native/ktx/staged/<rid>/` is both the CI cache key and the release artifact.
