# Ahjo.Vulkan.Slang.Native — generated bindings + pinned native binaries

`Generated/` is ClangSharp output from `slang.h` + `slang-deprecated.h` at the pinned `SlangVersion` in `Directory.Build.props`. **Never hand-edit anything under `Generated/`** — it is overwritten wholesale on the next regen.

To change the bindings: edit `tools/generate-slang.rsp`, bump `SlangVersion` if needed, then:

```bash
dotnet build src/Ahjo.Vulkan.Slang.Native -t:Regenerate   # needs network
```

## The version pin is two pins

Unlike VMA and libktx, Slang is **not built from source** — its build pulls LLVM and a full downstream-compiler tree, and the artifact we would produce is the one upstream already publishes and tests. This project consumes the official per-RID release archive instead, so `SlangVersion` alone does not describe the input: `SlangWinX64Sha256` and `SlangLinuxX64Sha256` pin the SHA-256 of each archive and are verified *before* extraction. **A `SlangVersion` bump means re-recording both hashes and running a regen.** The checksum error message prints expected and actual, so a stale hash is self-diagnosing.

## Two rules specific to this binding

**1. Never `--exclude` a virtual member name in `generate-slang.rsp`.** ClangSharp removes the vtable slot and silently shifts every later index — the helper method's body keeps calling the old literal, so you get a wrong-function call at runtime with no diagnostic. This was reproduced: excluding `ISlangFileSystemExt::enumeratePathContents` moved `getOSPathKind` from index 11 to 10 while its body still called `lpVtbl[10]`. The three names that *are* excluded (`spReflection_GetSession`, `slang_getEmbeddedCoreModule`, `getDefaultValueBlob`) are all non-virtual, and each would otherwise produce a C++-mangled `EntryPoint` — a binding whose only possible outcome is `EntryPointNotFoundException`. The `implicit-vtbls` shape was chosen over `explicit-vtbls` precisely so a regen's `git diff` shows slot-index changes as literal changes in the call expression.

**2. The entire reflection surface lives in `slang-deprecated.h`, whose own banner says the declarations will be dropped over time.** That is a deliberate, eyes-open dependency: Slang's recommended C++ reflection API is a header-only shim that calls exactly these symbols, so there is nothing else to bind. The mitigation is `SlangExportDriftTests` in `tests/Ahjo.Vulkan.Slang.Native.Tests`, which resolves every export the wrapper depends on out of the shipped binary by name. A `SlangVersion` bump that drops one fails that test with the missing name in the message. Do not delete or weaken it, and keep its list in step with what the wrapper actually calls.

Related: `--traverse` must name `slang-deprecated.h`, not just `--file slang.h`. ClangSharp only emits declarations from traversed files, so dropping it makes all 173 flat `spReflection_*` exports vanish and turns `ShaderReflection` / `TypeLayoutReflection` into empty structs — a change that compiles.

## The shipped subset

| RID | Files |
| --- | --- |
| `win-x64` | `slang.dll` (a ~158 KB forwarder) **and** `slang-compiler.dll` — ship only one and the first call is a `DllNotFoundException` |
| `linux-x64` | `libslang.so`, a **renamed copy** of `lib/libslang-compiler.so.0.<version>`; in the archive that name is a symlink, and a nupkg cannot carry symlinks |

Everything else in the ~77 MB archive is left out on purpose, and `StageSlangBinaries` in the csproj records why per file: `slang-llvm` (152 MB, CPU targets only), `slang-glsl-module` (GLSL *input* only), `libgfx` / `libslang-rt` (running shaders, not emitting them), the `slang-standard-module-*` tree (the core module is embedded — a full compile was verified with none of it present), and `bin/` + `share/doc/`. `slang-glslang` (which provides `spirv-opt`) is **deferred, not rejected** — see OPEN-1 in `docs/design/specs/2026-08-01-issue-166-slang-support-design.md`. If it ever ships, the Linux file name embeds the version and is `dlopen`ed by that exact name, so it must ship **unrenamed**.

`native/slang/include/` (the three parsed headers) and `native/slang/stubs/` (parse-time shims for `<inttypes.h>` and `<features.h>`) are committed: they are the generator input of record. `native/slang/downloaded/` and `native/slang/staged/` are not.

## The lane

Slang produces bytes and has no Vulkan surface at all, so this project references **nothing** — not `Ahjo.Vulkan.Native`, not `Ahjo.Vulkan`. The `slang-native` CI lane provisions no Vulkan loader and no ICD, on purpose: if a test in this suite suddenly needs one, something got linked in that the package's contract says is not there. It builds and **runs the tests in the same job, before uploading the artifact** — see `.github/CLAUDE.md`. The staged binaries under `native/slang/staged/<rid>/` are both the CI cache key and the release artifact.
