# Test fixture provenance

## color_grid_basis.ktx2

- **Source:** [KhronosGroup/KTX-Software](https://github.com/KhronosGroup/KTX-Software),
  `tests/testimages/color_grid_basis.ktx2`
- **Tag:** `v4.4.2` — the same tag `KtxVersion` in `Directory.Build.props` pins the
  library to, so the fixture and the transcoder that reads it move together
- **License:** Apache-2.0, © The Khronos Group Inc.
- **SHA-256:** `284ee77af748b9e640186916a6754fd07cab4a75bb99f92c87242b3e97efde1b`
- **Size:** 71 120 bytes
- **Shape:** 1024×1024, one level, one face, `vkFormat` = `VK_FORMAT_UNDEFINED`,
  BasisLZ supercompression

Copied out of upstream rather than referenced: KTX-Software keeps its test images in
Git LFS, and the shallow blobless checkout the build fetches deliberately excludes
`tests/`, so nothing in this repository's build graph can produce this file.

The bytes are pinned. Regenerating or substituting the fixture invalidates the exact
`dataSize` assertions in `KtxSmokeTests` — those numbers are derived from the 1024×1024
dimensions above, not measured from a run.
