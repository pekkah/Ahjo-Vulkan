# native/ — pinned upstream sources and staged binaries

- `downloaded/` — pinned Vulkan-Headers + VMA tarball cache. **Generated/downloaded content; never hand-edit.** Refreshed by the `-t:Regenerate` targets.
- `ktx/downloaded/` — shallow sparse KTX-Software checkout at the pinned tag. Same rule.
- `ktx/staged/<rid>/` — the built libktx binary per RID; both the CI cache key and the release artifact.
- `vma/` — the VMA implementation translation unit + `CMakeLists.txt` (hand-maintained, edits allowed).
- `include/`, `stubs/` — hand-maintained build support.

Version pins live in `Directory.Build.props`. To move a pin, use `/regen-bindings`.
