---
name: regen-bindings
description: Regenerate the ClangSharp bindings for one or more of the three Native projects (Vulkan, VMA, libktx) safely — version pin bump, regen target, diff sanity check, build + native tests. Use when the user asks to "regen bindings", "bump Vulkan headers / VMA / KTX", or when an rsp/codegen change needs to be applied.
---

# regen-bindings

Generated code is generated — `src/*/Generated/`, `native/downloaded/`, `native/ktx/downloaded/` are codegen output and hand-edits there are overwritten and lost. The editable inputs are the `tools/*.rsp` files, the codegen tools (`tools/Ahjo.Vulkan.StructExtendsGen/`, `tools/Ahjo.Vulkan.ResultPolicyGen/`), and the version pins in `Directory.Build.props`.

## Which target, which prerequisites

| Target | Pin in `Directory.Build.props` | rsp | Extra prerequisites |
|---|---|---|---|
| `dotnet build src/Ahjo.Vulkan.Native -t:Regenerate` | `VulkanHeadersVersion` | `tools/generate.rsp` | network (tarball fetch) |
| `dotnet build src/Ahjo.Vulkan.Vma.Native -t:Regenerate` | `VmaVersion` | `tools/generate-vma.rsp` (+ `generate-vma.notes.md`) | cmake on PATH |
| `dotnet build src/Ahjo.Vulkan.Ktx.Native -t:Regenerate` | `KtxVersion` | `tools/generate-ktx.rsp` | git; cmake to build the binary |

Only regenerate the project(s) whose pin or rsp actually changed.

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
   dotnet test                                       # full sweep if wrapper-visible API moved
   ```
   New upstream API often surfaces as analyzer warnings in the wrapper — `TreatWarningsAsErrors` means those are build breaks to fix properly, not suppress.
5. **Commit** the pin + rsp + regenerated output together, style `<area>: <imperative>` (e.g. `Native: bump Vulkan-Headers to 1.4.350`).

## What not to do

- Never hand-edit `Generated/` to fix a regen problem — fix the rsp or the codegen tool and regen again.
- Don't mix a regen with unrelated wrapper changes in one commit; the mechanical churn buries the real diff.
- Don't bump two pins in one go unless asked — independent cadences, independent commits.
