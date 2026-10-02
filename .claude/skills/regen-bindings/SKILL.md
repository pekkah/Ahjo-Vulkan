---
name: regen-bindings
description: Regenerate the ClangSharp bindings for one or more of the five Native projects (Vulkan, VMA, libktx, Slang, NGX) safely — version pin bump, regen target, diff sanity check, build + native tests. Use when the user asks to "regen bindings", "bump Vulkan headers / VMA / KTX / Slang / NGX", or when an rsp/codegen change needs to be applied.
---

# regen-bindings

Generated code is generated — `src/*/Generated/` (including `src/Ahjo.Vulkan.Ngx.Native/Generated/`), `native/downloaded/`, `native/ktx/downloaded/` are codegen output and hand-edits there are overwritten and lost. The editable inputs are the `tools/*.rsp` files, the codegen tools (`tools/Ahjo.Vulkan.StructExtendsGen/`, `tools/Ahjo.Vulkan.ResultPolicyGen/`), and the version pins in `Directory.Build.props`.

## Which target, which prerequisites

| Target | Pin in `Directory.Build.props` | rsp | Extra prerequisites |
|---|---|---|---|
| `dotnet build src/Ahjo.Vulkan.Native -t:Regenerate` | `VulkanHeadersVersion` | `tools/generate.rsp` | network (tarball fetch) |
| `dotnet build src/Ahjo.Vulkan.Vma.Native -t:Regenerate` | `VmaVersion` | `tools/generate-vma.rsp` (+ `generate-vma.notes.md`) | cmake on PATH |
| `dotnet build src/Ahjo.Vulkan.Ktx.Native -t:Regenerate` | `KtxVersion` | `tools/generate-ktx.rsp` | git; cmake to build the binary |
| `dotnet build src/Ahjo.Vulkan.Slang.Native -t:Regenerate` | `SlangVersion` **+ `SlangWinX64Sha256` + `SlangLinuxX64Sha256`** | `tools/generate-slang.rsp` | network (release archive); a bump needs **both** hashes re-recorded |
| `dotnet build src/Ahjo.Vulkan.Ngx.Native -t:Regenerate` | `NgxVersion` | `tools/generate-ngx.rsp` | `./tools/setup-ngx.ps1` must have staged `native/ngx/include/` at least once. No network for the regen itself — the headers are committed. cmake + a C++ toolchain only if you also want the shim built |

Only regenerate the project(s) whose pin or rsp actually changed.

**Slang gotcha:** `StageSlangHeaders` only copies headers out of the archive when `native/slang/include/slang.h` is *missing*, so after a `SlangVersion` bump a plain `-t:Regenerate` silently regenerates from the **old** committed headers. Delete `native/slang/include/*.h` first. Likewise, an existing `native/slang/staged/win-x64/` is not re-staged (the Windows file names carry no version) — delete it so local tests load the new binaries.

**VMA gotcha:** same shape. `FetchVma` only runs when `native/vma/include/vk_mem_alloc.h` is *missing*, and that committed header is what both the regen **and the native cmake build (local and CI)** compile. After a `VmaVersion` bump, delete it before regenerating; otherwise the bindings stay on the old API and CI ships an old-version binary under the new pin. Confirm with `grep VMA_VERSION native/vma/include/vk_mem_alloc.h`. Keep only one `VulkanMemoryAllocator-*` tree under `native/vma/downloaded/`, because the header copy globs over all of them.

## Procedure

1. **Edit the input**, not the output: bump the pin in `Directory.Build.props` and/or edit the rsp. Pins are pinned deliberately — all packages ship under a single `v*` tag, so a header bump is a release-visible decision; confirm the user wants it if they only asked vaguely.
2. **Run the regen target(s)** from the table above.
3. **Sanity-check the diff.** `git status` + skim `git diff --stat` for `Generated/`. Expect mechanical churn consistent with the upstream changelog. Red flags: whole files disappearing, the diff touching hand-written (non-`Generated/`) files, or a tiny diff after a major version bump.
4. **Build + test:**
   ```bash
   dotnet build Ahjo.Vulkan.slnx
   dotnet test tests/Ahjo.Vulkan.Native.Tests        # Vulkan regen
   dotnet test tests/Ahjo.Vulkan.Vma.Native.Tests    # VMA regen
   dotnet test tests/Ahjo.Vulkan.Ktx.Native.Tests    # KTX regen (must pass with NO Vulkan loader)
   dotnet test tests/Ahjo.Vulkan.Slang.Native.Tests  # Slang regen (must pass with NO Vulkan loader)
   dotnet test tests/Ahjo.Vulkan.Ngx.Native.Tests    # NGX regen — needs the shim built, else it skips
   dotnet test                                       # full sweep if wrapper-visible API moved
   ```
   New upstream API often surfaces as analyzer warnings in the wrapper — `TreatWarningsAsErrors` means those are build breaks to fix properly, not suppress.
5. **Commit** the pin + rsp + regenerated output together, style `<area>: <imperative>` (e.g. `Native: bump Vulkan-Headers to 1.4.350`).

## What not to do

- Never hand-edit `Generated/` to fix a regen problem — fix the rsp or the codegen tool and regen again.
- After an **NGX** regen, check `grep -r wchar_t src/Ahjo.Vulkan.Ngx.Native/Generated` returns nothing. A wide type in that tree is a Windows-only encoding bug waiting to ship; the fix is `--exclude`-ing the struct that carries it, never `--remap wchar_t`. See `src/Ahjo.Vulkan.Ngx.Native/CLAUDE.md`.
- Don't mix a regen with unrelated wrapper changes in one commit; the mechanical churn buries the real diff.
- Don't bump two pins in one go unless asked — independent cadences, independent commits.
