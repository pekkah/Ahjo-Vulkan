# native/ — pinned upstream sources and staged binaries

- `downloaded/` — pinned Vulkan-Headers + VMA tarball cache. **Generated/downloaded content; never hand-edit.** Refreshed by the `-t:Regenerate` targets.
- `ktx/downloaded/` — shallow sparse KTX-Software checkout at the pinned tag. Same rule.
- `ktx/staged/<rid>/` — the built libktx binary per RID; both the CI cache key and the release artifact.
- `slang/downloaded/` — the pinned Slang release archive plus its extraction scratch. Same rule; the archive's SHA-256 is verified against `Directory.Build.props` before anything is extracted.
- `slang/staged/<rid>/` — the shipped Slang binaries per RID, copied out of that archive (on Linux, renamed — the archive's `libslang.so` is a symlink and a nupkg cannot carry one). Both the CI cache key and the release artifact.
- `slang/include/` — the three headers `tools/generate-slang.rsp` parses. Staged from the archive but **committed**: they are the generator input of record, so a version bump shows the API diff before the generated output is re-derived from it.
- `slang/SLANG-LICENSE.txt` — Slang's Apache-2.0 WITH LLVM-exception text, copied out of the archive and packed into the nupkg. Committed for the same reason: the publish job packs from downloaded artifacts and never runs the fetch.
- `vma/` — the VMA implementation translation unit + `CMakeLists.txt` (hand-maintained, edits allowed).
- `ngx/` — the pinned NVIDIA DLSS (NGX) SDK input, fetched per-file by `tools/setup-ngx.ps1` at the `NgxVersion` tag and verified against `ngx/pins.sha256`. `ngx/include/`, `ngx/NGX-LICENSE.txt` and `ngx/pins.sha256` are committed; `ngx/downloaded/`, `ngx/staged/` (static client libs + the `nvngx_dlss` feature DLLs) and `ngx/doc/` are not. The feature DLL is consumer-supplied by decision (#214): never commit it, never pack it, never stage it under a `lib/` path (the `.gitignore` un-ignores `native/**/lib/**/*.dll`). See `ngx/README.md`.
- `include/`, `stubs/`, `ktx/stubs/`, `slang/stubs/` — hand-maintained build support. The `stubs/` trees are parse-time shims that let libclang read the upstream headers without a system C toolchain, so generated output is a function of the version pin and not of who ran the regen.

Version pins live in `Directory.Build.props`. To move a pin, use `/regen-bindings`. Slang's pin is two values — the tag **and** both archive checksums.
