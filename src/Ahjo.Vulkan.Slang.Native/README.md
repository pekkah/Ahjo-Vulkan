# Ahjo.Vulkan.Slang.Native

Raw P/Invoke bindings for the [Slang shader compiler](https://github.com/shader-slang/slang)
— compile Slang/HLSL to SPIR-V, plus the full shader reflection surface — with
native runtime binaries included for `win-x64` and `linux-x64`.

The managed surface is generated with
[ClangSharpPInvokeGenerator](https://github.com/dotnet/ClangSharp) from `slang.h`
at a pinned upstream release tag. Nothing is hand-written, so the bindings are a
mechanical function of that tag.

**Standalone.** This package has no Vulkan dependency and does not reference
`Ahjo.Vulkan.Native` or `Ahjo.Vulkan` — Slang takes shader text and produces
bytes. Reference it directly if you need it.

```csharp
using Ahjo.Vulkan.Slang.Native;

unsafe
{
    IGlobalSession* global;
    if (SlangApi.slang_createGlobalSession(0, &global) < 0) { /* handle */ }

    // Describe a SPIR-V target, create a session, load a module, find its entry
    // points, composite + link them, then getEntryPointCode() for the blob.
    // Every call that takes an ISlangBlob** outDiagnostics should be given one:
    // loadModuleFromSourceString signals failure by returning null, so a
    // result-code-only check will miss a broken compile.

    global->release();
}
```

Interface methods dispatch through `delegate* unmanaged[MemberFunction]` vtable
slots — no `ComWrappers`, no `[ComImport]`, no `Marshal`, no reflection — so the
binding is Native AOT clean.

For an idiomatic wrapper over this (session objects, diagnostics as exceptions,
reflection mapped onto `DescriptorBinding` / `PushConstantRange` /
`VertexAttributeDescription`), see `Ahjo.Vulkan.Slang`.

## What is and is not in the package

The shipped binaries are extracted from the official release archive for the
pinned tag, after its SHA-256 is verified against a value pinned in this
repository. Only the compiler and its SPIR-V optimizer ship:

| RID | Files | Compressed |
| --- | --- | --- |
| `win-x64` | `slang.dll` + `slang-compiler.dll` (the first is a small forwarder that loads the second; both are required) + `slang-glslang.dll` | ~14 MB |
| `linux-x64` | `libslang.so` + `libslang-glslang-<version>.so` | ~17 MB |

`slang-glslang` provides the `spirv-opt` downstream compiler. Without it the
compiler still emits valid SPIR-V — `SLANG_TARGET_FLAG_GENERATE_SPIRV_DIRECTLY`
is Slang's own default — but every optimization level above "none" quietly
reports `error[E00100]: failed to load downstream compiler 'spirv-opt'` and
produces identical output, so the setting does nothing. It ships for that
reason. The Linux file name embeds the version and is loaded by that exact name,
so it is not renamed.

Left out deliberately: `slang-llvm` (152 MB, for CPU and host-callable targets),
`slang-glsl-module` (GLSL *input* only), `libgfx` and `libslang-rt` (Slang's own
graphics layer and CPU runtime — for *running* shaders, not emitting them), the
`slang-standard-module-*` tree (the core module is embedded in the compiler
binary), and the `slangc` / `slangd` command-line tools. The compile-to-SPIR-V
path was verified end to end with only the files above present.

## Platform support

| RID | Shipped | Notes |
| --- | --- | --- |
| `win-x64` | yes | x86-64 |
| `linux-x64` | yes | glibc; staged and executed on `ubuntu-latest` |
| `*-arm64`, `osx-*` | no | no lane — see below |

Upstream publishes `windows-aarch64`, `linux-aarch64`, `macos-x86_64` and
`macos-aarch64` assets, and this package ships none of them. That is not an
oversight: a RID only ships once a CI lane has loaded its binary and compiled a
shader with it. Add the lane first, then the RID.

## Licensing

Slang is Apache-2.0 WITH LLVM-exception (the Khronos Group / NVIDIA); its license
text ships in this package as `SLANG-LICENSE.txt`. This package's own binding
code is MIT, like the rest of the Ahjo.Vulkan repository.
