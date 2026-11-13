# Ahjo.Vulkan.Native — generated bindings

`Generated/` is ClangSharp output from `vulkan.h` at the pinned `VulkanHeadersVersion` in `Directory.Build.props`. **Never hand-edit anything under `Generated/`** — it is overwritten wholesale on the next regen.

To change the bindings: edit `tools/generate.rsp` (and/or bump `VulkanHeadersVersion`), then:

```bash
dotnet build src/Ahjo.Vulkan.Native -t:Regenerate
```

The `/regen-bindings` skill walks the full procedure, including verification. Hand-written code (partial classes, helpers) lives outside `Generated/` and is fair game.
