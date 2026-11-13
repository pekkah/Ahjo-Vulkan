# Issue #256: version-stamp staged native inputs and binaries (Slang, VMA, KTX, NGX)

Paired with [../plans/2026-11-13-issue-256-native-staging-version-stamp.md](../plans/2026-11-13-issue-256-native-staging-version-stamp.md).

Scope note: #256 is about Slang. At approval the user resolved OPEN-1 as
"widen this PR to every package" and OPEN-2 as "a plain build re-stages
tracked headers". Part A (Slang) is the approved design and is unchanged.
Parts B–D extend the same rule to VMA, KTX and NGX. Each sibling gate was
first checked for whether it actually goes stale on a pin bump (E6–E9).

## Problem

Several native projects decide whether to fetch, stage or build by checking
whether a file **exists**. Where that file's name and path carry no version,
the check passes after a pin bump, and the old content is reused silently.

1. **Slang staged runtime binaries.** `_SlangStageNeeded`
   (`Ahjo.Vulkan.Slang.Native.csproj:97`, "`csproj`" below in Part A) is true only
   when `staged/<rid>/<primary>` or `staged/<rid>/<glslang>` is missing. On
   win-x64 those names are `slang.dll` and `slang-glslang.dll`
   (`csproj:84`, `:93`), and neither carries a version. After a `SlangVersion`
   bump, an existing `native/slang/staged/win-x64/` therefore satisfies the
   check. `DownloadSlangArchive`, `ExtractSlangArchive` and
   `StageSlangBinaries` (`csproj:216`, `:235`, `:290`) all skip, and every
   consumer gets the old DLLs through the `CopyToOutputDirectory` items
   (`csproj:137-153`).
2. **Slang staged headers.** `StageSlangHeaders` runs only when
   `native/slang/include/slang.h` is missing (`csproj:392`). Those headers are
   committed, so on any clone the file is always there. After a bump,
   `-t:Regenerate` (`csproj:187`) silently regenerates `Generated/` from the
   **old** headers.
3. **VMA, KTX and NGX** have the same defect at the gates listed in E6–E8.
   - VMA's header gate is already documented as a manual-deletion trap
     (`.claude/skills/regen-bindings/SKILL.md:24`).
   - KTX's gate comment claims the binary cost "is paid once per KtxVersion
     bump" (`Ktx.Native.csproj:143-144`). That is only true in CI, where a
     bump changes the cache key. Locally the binary is never rebuilt.
   - NGX's own docs tell the developer to re-run `setup-ngx.ps1` "after a pin
     bump" (`src/Ahjo.Vulkan.Ngx.Native/CLAUDE.md:8`). Nothing checks that
     they did.

The regen skill tells you to work around Slang and VMA by deleting files by
hand (`SKILL.md:22`, `:24`). The Slang bump PRs did exactly that:
- #236 found the trap and documented it in its "Docs: record the Slang
  header/binary staging trap" commit.
- The issue body says #254 had to delete the files by hand too.
- #270's body records "binaries re-staged from v2026.22".

"Silently" needs one qualification. Two tests compare the loaded binary with
the pin, so their own suites fail loudly on stale binaries:
- Slang: `BuildTag_MatchesPinnedVersion`
  (`tests/Ahjo.Vulkan.Slang.Native.Tests/SlangSmokeTests.cs:116`).
- NGX: `VersionApi_MatchesTheGeneratedBindings`
  (`tests/Ahjo.Vulkan.Ngx.Native.Tests/NgxSmokeTests.cs:31-44`). This one only
  catches a bump that also changes `NVSDK_NGX_VERSION_API`.

Nothing else catches stale binaries. Nothing at all catches stale headers,
and VMA and KTX have no binary-vs-pin test.

## Evidence

### E1. Slang: audit of every existence gate in the csproj

| Gate | Location | Version-dependent? | Affected? |
|---|---|---|---|
| `_SlangStageNeeded` (win-x64) | `csproj:97` | yes | **Yes.** `slang.dll` and `slang-glslang.dll` are unversioned (`csproj:84`, `:93`). |
| `_SlangStageNeeded` (linux-x64) | `csproj:97` | yes | **Only by accident.** `libslang.so` is unversioned (`csproj:85`). The check still trips after a bump, but only because `libslang-glslang-<ver>.so` embeds the version (`csproj:94`). The old `libslang-glslang-<old>.so` is never removed. |
| `StageSlangHeaders` | `csproj:392` | yes | **Yes**, on every RID. |
| Download / extract gating | `csproj:216`, `:235` | n/a | **Yes, latent.** These targets run only for `_SlangStageNeeded` or `_SlangToolNeeded`, never for headers. If headers needed staging on their own, `StageSlangHeaders` would read whatever an earlier extraction left in `downloaded/<rid>/include/`, or hit its Error (`csproj:400`) if that is empty. |
| Extraction scratch `downloaded/<rid>/` | `csproj:62`, `:236-247` | yes | **Yes, latent.** The path has no version, and `Unzip` and `tar -x` both overlay it. If a release drops a file, the previous version's copy is still there. The `Exists`-filtered header copy (`csproj:397`) and the shipped-file copies (`csproj:292-314`) would then stage the old file without complaint. |
| Archive download | `csproj:66-69`, `:219-230` | yes | **No.** The file name embeds the version, and the SHA-256 check runs on every fetch. |
| Build-time slangc `tool/<rid>/` | `csproj:106`, `:114-119`, `:377-379` | yes | **No.** Already stamped (`slangc.version`, #171/#172 spec A2). Like `staged/`, though, it overlays: on Linux the old `libslang-compiler.so.0.<old>` stays behind. |
| Pack includes | `csproj:436`, `:443` | no | **No.** These conditions are optional per-RID includes, not cache checks. |
| Shader build stamp | `tools/msbuild/Ahjo.Vulkan.SlangShaders.targets:112` | yes | **No.** The stamp already includes `$(SlangVersion)`. |

### E2. Slang: where the stamps have to live for CI

- `ci.yml:144-148` and `build-slang-native.yml:85-90` cache exactly
  `native/slang/staged/<rid>` and `native/slang/tool/<rid>`. The key hashes
  `Directory.Build.props`, the csproj and the lane workflow. CI never sees a
  stale binary, because a bump changes the key. This bug is local-dev only.
- A runtime stamp **outside** the cached paths would come back missing after
  every cache hit, and each CI run would re-download the ~77 MB archive. So
  the stamp has to sit inside `staged/<rid>/`.
- Inside `staged/<rid>/` the stamp travels in the uploaded artifact
  (`build-slang-native.yml:126`), and `publish.yml:215` copies it back. It is
  **not** packed, because `_SlangPackFile` names its files
  (`csproj:424-431`). It is not copied to consumers either, because the `None`
  items name each file (`csproj:138-177`).
- `native/slang/include/` is tracked and is in no cache path. An untracked
  header stamp would be missing on every CI checkout, so the header stamp has
  to be **committed**. `git check-ignore` confirms
  `native/slang/include/slang.version` is not ignored.
- This change edits the csproj, which changes the cache key. CI therefore
  migrates through one cold miss.

### E3. Headers carry no release version to compare against

`slang.h` contains only `SLANG_API_VERSION 0` (`native/slang/include/slang.h:5964`)
and the language-version enum. There is no release tag in it to parse.

### E4. Staged Slang files keep the archive's timestamps, which matters for `bin/`

In the main checkout, `staged/win-x64/slang-compiler.dll` has mtime
`2026-11-10 19:45:36`. That is the release's archive entry time, not the
staging time, and the copy in `tests/…/bin/Release/net11.0/` has the same
mtime. The consumer items use `PreserveNewest` (`csproj:139`), which copies
only when the source is newer than the destination. So re-staging an **older**
pin leaves the newer DLL in every `bin/`. This is derived from MSBuild's
`_CopyOutOfDateSourceItemsToOutputDirectory` Inputs/Outputs semantics and the
observed mtimes; the downgrade case itself has not been reproduced.

### E5. The pattern to reuse already exists

`_SlangToolNeeded` (`csproj:114`, `:117-119`) uses two property groups:

- The first sets it to `true` when the file or the stamp is missing.
- The second is guarded so that `ReadAllText` only runs on a stamp that
  exists, and compares the trimmed text with `$(_SlangVersionNumber)`.

`StageSlangCompiler` writes the stamp last (`csproj:377-379`). A stage that
fails partway therefore leaves no stamp, and the next build retries.

### E6. VMA (`src/Ahjo.Vulkan.Vma.Native/Ahjo.Vulkan.Vma.Native.csproj`)

| Gate | Location | Goes stale on a `VmaVersion` bump? |
|---|---|---|
| `FetchVma` | `:96-97` | **Yes.** It is gated on `native/vma/include/vk_mem_alloc.h`, which is tracked (`git ls-files`), so the gate is always satisfied on a clone. `SKILL.md:24` already confirms it from bump experience. |
| Header copy inside `FetchVma` | `:116-128` | **Yes, latent.** It globs `VulkanMemoryAllocator-*\include\vk_mem_alloc.h` over every extracted tree, so with two trees present two files race into one destination. `SKILL.md:24` asks you to keep only one tree by hand. `_VmaExtractDir` / `_VmaHeadersDir` (`:49-50`) were meant to pin the exact tree. They are dead (no reference outside `:49-50`) and also wrong: they use `$(VmaVersion)` (`v3.5.0`), but GitHub's archive strips the `v` (`:112-115`). |
| Zip download | `:100-105` | **No.** The file name embeds `$(VmaVersion)`. |
| `BuildVmaForHost` | `:158-161` | **Yes.** It is gated on `!Exists(build/<rid>/vma.dll)`, and that path is unversioned (`:52-54`). After a `VmaVersion` bump the header changes but cmake never runs again. The binary is also compiled against the Vulkan headers (`:169`), so a `VulkanHeadersVersion` bump leaves it stale the same way. Edits to the tracked `native/vma/src/vma.cpp` / `CMakeLists.txt` are missed too, but they are not version-dependent (see Residual risk). |

How VMA is built and cached in CI:
- `build-vma-native.yml` caches the cmake output, which on Linux is exactly
  `_VmaStagedBinary` (`:49`, `:53`, `:74`). That lane's only `dotnet` call
  passes `SkipVmaNativeBuild=true` (`:93`), and it then drives cmake itself
  (`:98-120`). So `BuildVmaForHost` never sees the restored binary.
- `ci.yml:162` (Windows) and the `vma-linux` lane's `dotnet test` (`ci.yml:458`)
  build Vma.Native without `SkipVmaNativeBuild`, but nothing in those jobs
  caches or downloads `native/vma/build/`. `ci.yml:421-426` caches only
  `native/vma/downloaded`. Those jobs build from scratch either way.

Why mtimes can't drive VMA's rebuild (measured shape, not assumed):
- `FetchVma` stages the header with `Unzip` + `Copy`, which keep the
  archive's timestamps (as Slang's do, E4). A freshly re-staged
  `vk_mem_alloc.h` can therefore be **older** than an existing `vma.dll` and
  its cmake objects.
- `CopyGeneratedHeaders` (`Ahjo.Vulkan.Native.csproj:85-114`) re-copies
  `native/include/vulkan/*.h` with `SkipUnchangedFiles`. That resets the
  tracked headers to the Vulkan-Headers archive's mtimes whenever it runs.
- Inside an existing build dir, cmake's own dependency check is also
  mtime-based, so after a header re-stage it can decide nothing changed.

### E7. KTX (`src/Ahjo.Vulkan.Ktx.Native/Ahjo.Vulkan.Ktx.Native.csproj`)

| Gate | Location | Goes stale on a `KtxVersion` bump? |
|---|---|---|
| `CloneKtx` | `:108` | **Yes.** It is gated on `downloaded/KTX-Software/CMakeLists.txt` (`:48`), and that path is unversioned. After a bump, `git checkout $(KtxVersion)` (`:117`) never runs again, so every later step reads the old source. |
| `StageKtxHeaders` | `:121` | **Yes.** It is gated on the tracked `native/ktx/include/ktx.h` (`git ls-files`: `ktx.h` and `KHR/khr_df.h`). Even if it ran, it would copy from the stale clone. |
| `BuildKtxForHost` | `:153-156` | **Yes.** It is gated on `!Exists(staged/<rid>/ktx.dll)`, and that path is unversioned (`:65`). The comment at `:143-144` assumes a bump triggers a rebuild, but nothing does locally. |

How KTX is cached in CI, and why that matters here:
- `build-ktx-native.yml:78-82` caches **only the single binary file**
  `native/ktx/staged/<rid>/<lib-file>`. The key hashes the props, the csproj
  and the workflow.
- Because the cache restores that binary, an mtime-based check would see
  fresh checkout files as newer than it and rebuild on every hit. So KTX needs
  a stamp, not Inputs/Outputs.
- The stamp then has to be added to the cache `path:`. Otherwise every hit
  restores the binary without its stamp and triggers a ~170 MB clone plus a
  cmake build.
- `ci.yml:162` and `publish.yml:308` pass `SkipKtxNativeBuild=true` and are
  unaffected.

### E8. NGX (`src/Ahjo.Vulkan.Ngx.Native/Ahjo.Vulkan.Ngx.Native.csproj`, `tools/setup-ngx.ps1`)

| Gate | Location | Goes stale on an `NgxVersion` bump? |
|---|---|---|
| `RunClangSharpNgxGenerator`'s Error on missing headers | `:128-130` | **No, excluded.** It is not a staging gate. Headers are staged only by `setup-ngx.ps1`, which always overwrites them (`Copy-Item -Force`, `setup-ngx.ps1:251`). A bump is made by running that script with `-UpdatePins` (`setup-ngx.ps1:50-61`), which also re-stages the tracked headers. |
| `Exists($(_NgxStaticLib))` | `:95`, `:164`, `:207` | **Yes.** The static library under `native/ngx/staged/<rid>/` is unversioned (`:62-63`). It is staged only by an explicit `setup-ngx.ps1` run, and per #214 that must stay explicit (`:58-61`). A developer who pulls a bump keeps the old library and the build links it without comment. `BuildNgxForHost`'s Inputs (`:166`) include the library, but `Copy-Item` preserves the download's mtime. A re-run that copies from an older cached download under `downloaded/<Version>/` (`setup-ngx.ps1:212`, `:226`) therefore may not trigger a relink. |
| Script downloads | `setup-ngx.ps1:212` | **No.** The cache directory is versioned (`downloaded/<Version>/`), and every file is hash-checked against the committed `pins.sha256`. |

CI: `build-ngx-native.yml:85-90` caches `native/ngx/staged/<rid>` (the whole
directory) keyed on the props, `pins.sha256` and `setup-ngx.ps1`. It also runs
`setup-ngx.ps1` on every run (`:109`). A stamp the script writes into
`staged/<rid>/` is therefore always present in CI. `ci.yml:162` and `publish.yml`
pass `SkipNgxNativeBuild=true`.

### E9. Gates checked and excluded

| Gate | Why it does not go stale |
|---|---|
| Vulkan-Headers `FetchVulkanHeaders` (`src/Ahjo.Vulkan.Native/Ahjo.Vulkan.Native.csproj:55-56`) | The extract dir embeds `$(VulkanHeadersVersion)` (`:36`), and `CopyGeneratedHeaders` copies with no condition. |
| `vk.xml` / `VkStructureType.cs` checks (`Ahjo.Vulkan.Native.csproj:155`, `:158`, `:187`) | These are `Error`s on missing inputs, not staging gates. |
| NGX headers (`Ngx.Native.csproj:129`) | See E8. |
| Every `PackXRuntimes` `Exists` (Slang `:436`, `:443`, VMA `:213`, KTX `:210`, NGX `:236`, `:243`) | These are optional per-RID pack includes, not caches. |
| `ci.yml:64-67` Vulkan-Headers cache, `ci.yml:74-77` loader cache | Their keys already contain the version. |
| `samples/HelloDlaa/HelloDlaa.csproj:60`, `:64`; `samples/HelloFrameGen/HelloFrameGen.csproj:67-84` | These are optional copies of consumer-supplied DLSS DLLs (#214), not pinned staging. |
| `tools/msbuild/Ahjo.Vulkan.SlangShaders.targets:105`, `:161` | `Error`s, not gates. |

Scope of the sweep: `Exists(` / `Test-Path` over every `src/*/*.csproj`,
`tests/`, `samples/`, `tools/` (`.targets`, `.ps1`), `Directory.Build.*` and
`Directory.Packages.props`. Every hit is listed in E1, E6–E9.

## Decision

**One rule, applied per package: every output that depends on a pinned
version is checked against that version, not just for existence. A re-stage
replaces the output instead of overlaying it.** The way it is checked
follows the shape of each package's fetch/build and its CI cache:

- **Fetched or unpacked content** (archives, clones, upstream headers,
  upstream binaries, and binaries built from fetched inputs) gets a
  **stamp file**: `<name>.version` inside the directory it describes. It
  holds the pin with any leading `v` trimmed, using the `_SlangToolNeeded`
  two-group pattern (E5).
  - A stamp beside **tracked** content is **committed**.
  - A stamp beside **gitignored** content is gitignored and must sit inside
    whatever path CI caches.
  - It is written last, so a failed stage leaves no stamp.
  - A cmake-built binary whose stamp is stale is rebuilt in a **cleared**
    build directory, because cmake's mtime checks can't be trusted across a
    re-stage (E6).
- **A version in the path, with no stamp,** is used where nothing else
  depends on the path being stable. That is the KTX clone only. It is the
  Vulkan-Headers precedent (`Ahjo.Vulkan.Native.csproj:36`).

| Package | Output | Mechanism | Stamp file | Tracked? |
|---|---|---|---|---|
| Slang | `staged/<rid>/` | stamp | `native/slang/staged/<rid>/slang.version` | no (cached) |
| Slang | `include/` | stamp | `native/slang/include/slang.version` | **yes** |
| Slang | `tool/<rid>/` | stamp (exists) | `slangc.version` | no (cached) |
| VMA | `include/vk_mem_alloc.h` | stamp | `native/vma/include/vma.version` | **yes** |
| VMA | `build/<rid>/vma.dll` | stamp (VMA **and** Vulkan-Headers pins) | `native/vma/build/<rid>/vma.version` | no (not cached by any lane that reads it) |
| KTX | `downloaded/KTX-Software-<tag>/` | versioned path | — | no |
| KTX | `include/` | stamp | `native/ktx/include/ktx.version` | **yes** |
| KTX | `staged/<rid>/<lib>` | stamp | `native/ktx/staged/<rid>/ktx.version` | no (**added to cache path**) |
| NGX | `staged/<rid>/` (static lib) | stamp written by `setup-ngx.ps1`, **checked** by the build | `native/ngx/staged/<rid>/ngx.version` | no (cached) |

Per OPEN-2's resolution, a plain build re-stages tracked headers when their
stamp is stale. Slang does it through `FetchSlang`. VMA and KTX each get a
`BeforeTargets="AssignTargetPaths"` hook, so headers re-stage even when the
binary stamp matches: VMA through a new `RestageVmaHeader` target that
depends on `FetchVma` (B1), KTX through `FetchKtx` itself (C4). VMA's hook is
a separate target because the `vma-native` lane calls `-t:FetchVma`
explicitly with `SkipVmaNativeBuild=true` and must still stage. NGX is the
exception: its headers are re-staged by the script, not the build (D).

### A. Slang (approved; unchanged)

1. **Runtime stamp** `native/slang/staged/<rid>/slang.version`, holding
   `$(_SlangVersionNumber)`.
   - `_SlangStageNeeded` is true when either shipped file is missing, the
     stamp is missing, or the stamp differs from the pin.
   - This applies to both RIDs.
   - `StageSlangBinaries` writes the stamp last.
2. **Header stamp** `native/slang/include/slang.version`, **committed**.
   - `_SlangHeadersNeeded` replaces the bare `!Exists(slang.h)` at
     `csproj:392`.
   - `StageSlangHeaders` writes the stamp and logs that `Generated/` is now
     behind the headers.
   - It stays in `FetchSlang`.
3. **Download and extract also run for headers**, through
   `_SlangArchiveNeeded` (stage OR tool OR headers).
4. **Replace, don't overlay.**
   - `ExtractSlangArchive`, `StageSlangBinaries` and `StageSlangCompiler`
     clear `downloaded/<rid>/`, `staged/<rid>/` and `tool/<rid>/`
     respectively before writing.
   - Headers are not cleared.
5. **`Touch` the staged runtime files after copying** (E4).

### B. VMA

1. **Header stamp** `native/vma/include/vma.version`, **committed**, holding
   `$(VmaVersion.TrimStart('v'))`.
   - New `_VmaHeaderNeeded`, two-group shape.
   - `FetchVma`'s condition becomes `'$(_VmaHeaderNeeded)' == 'true'`.
   - The stamp is written after the copy.
   - New `RestageVmaHeader` (`BeforeTargets="AssignTargetPaths"`,
     `DependsOnTargets="FetchVma"`, condition
     `'$(SkipVmaNativeBuild)' != 'true' and '$(_VmaHeaderNeeded)' == 'true'`)
     re-stages a stale header in a plain build when the binary stamp matches.
     MSBuild runs `FetchVma` once per build, so it never double-runs with
     `BuildVmaForHost`'s dependency. Publish passes `SkipVmaNativeBuild=true`
     and is unaffected.
2. **Pin the extracted tree.**
   - Fix `_VmaExtractDir` (`:49`) to
     `VulkanMemoryAllocator-$(VmaVersion.TrimStart('v'))\`.
   - The header item at `:117` becomes `$(_VmaHeadersDir)vk_mem_alloc.h`
     instead of the `VulkanMemoryAllocator-*` glob.
   - Old extracted trees can then sit beside the current one harmlessly, and
     the "keep only one tree" instruction at `SKILL.md:24` goes.
3. **Binary stamp** `native/vma/build/<rid>/vma.version`, holding
   `$(_VmaVersionNumber);$(VulkanHeadersVersion)`, e.g.
   `3.5.0;vulkan-sdk-1.4.363.0`.
   - It covers both pins the binary is compiled from.
   - New `_VmaBinaryNeeded`, two-group shape.
   - `BuildVmaForHost`'s `!Exists('$(_VmaStagedBinary)')` becomes
     `'$(_VmaBinaryNeeded)' == 'true'`.
   - Before configuring, the target clears `$(_VmaBuildDir)`. cmake's mtime
     check could otherwise keep stale objects (E6).
   - The stamp is written after the normalising copy (`:181-185`).
   - A fresh build gives the binary a fresh mtime, so `PreserveNewest`
     refreshes `bin/` without a `Touch`.

### C. KTX

1. **Versioned clone** `_KtxSourceDir` =
   `$(_KtxDownloadedRoot)KTX-Software-$(KtxVersion)\`.
   - `CloneKtx` keeps its existence condition, which is now version-correct.
   - It clears `$(_KtxDownloadedRoot)` before cloning, so earlier clones
     (~170 MB each) don't pile up.
2. **Header stamp** `native/ktx/include/ktx.version`, **committed**, holding
   `$(KtxVersion.TrimStart('v'))`.
   - New `_KtxHeadersNeeded`, two-group shape.
   - `StageKtxHeaders`' condition becomes `'$(_KtxHeadersNeeded)' == 'true'`.
   - The stamp is written after both copies.
3. **Binary stamp** `native/ktx/staged/<rid>/ktx.version`, holding the same
   value.
   - New `_KtxBinaryNeeded`: true when the binary or stamp is missing, or
     they differ.
   - `BuildKtxForHost`'s `!Exists` becomes `'$(_KtxBinaryNeeded)' == 'true'`.
   - Before cmake runs, the target clears `$(_KtxBuildDir)`, because
     `CMakeCache.txt` records the old source directory and cmake refuses a
     different `-S`. It also clears `staged/<rid>/`.
   - `StageKtxBinary` writes the stamp last.
   - No `Touch` is needed, because a fresh cmake build gives the binary a
     fresh mtime.
4. **Headers re-stage on their own stamp.** `FetchKtx` gains
   `BeforeTargets="AssignTargetPaths"` and the condition
   `'$(SkipKtxNativeBuild)' != 'true' and ('$(_KtxHeadersNeeded)' == 'true' or '$(_KtxBinaryNeeded)' == 'true')`.
   - Today it runs only as `BuildKtxForHost`'s dependency, so a stale header
     stamp beside a current binary would never re-stage in a plain build. That
     would break OPEN-2's resolution.
   - `BuildKtxForHost` still lists `FetchKtx` in `DependsOnTargets`. MSBuild
     runs a target once per build, so it is not repeated.
   - On CI, `ci.yml:162` passes `SkipKtxNativeBuild=true`, and in the
     `ktx-native` lane the committed stamp matches. So CI never clones because
     of this hook unless a bump commit forgets its stamp.
5. **CI cache path** in `build-ktx-native.yml:81` gains
   `native/ktx/staged/<rid>/ktx.version`. The upload `path:` (`:115`) stays the
   binary alone. Publish packs with `SkipKtxNativeBuild=true` and never reads
   the stamp.

### D. NGX

1. `setup-ngx.ps1` writes `native/ngx/staged/<rid>/ngx.version` holding
   `$Bare` (`setup-ngx.ps1:97`).
   - It writes one stamp per platform it staged (`$wantWin` / `$wantLinux`).
   - It writes only after the verification `throw` (`:255-258`) has not fired
     and after the `-UpdatePins` block (`:260-277`).
   - The stamp vouches for the **client library** the build links. It does
     not vouch for the feature DLLs under `rel/` and `dev/`. Those are
     consumer-supplied (#214), the build never reads them, and after a
     `-SkipFeatureDll` run an older unversioned `rel/nvngx_dlss.dll` can sit
     beside a new stamp. The script's help says so.
2. The csproj adds `_NgxSdkStamp` and `_NgxSdkStale`. `_NgxSdkStale` is true
   when `_NgxStaticLib` exists and the stamp is missing or differs from
   `$(NgxVersion.TrimStart('v'))`, using the two-group shape.
3. When `_NgxSdkStale` is true, a new target `ErrorNgxSdkStale`
   (`BeforeTargets="AssignTargetPaths"`, same RID / `SkipNgxNativeBuild`
   guards as `BuildNgxForHost`) **fails the build** and says to re-run
   `./tools/setup-ngx.ps1`. See **OPEN-3**. The build never fetches the SDK
   itself (#214).
4. `$(_NgxSdkStamp)` is added to `BuildNgxForHost`'s `Inputs`. The script
   rewrites the stamp on every run, so every re-stage forces a relink even
   when `Copy-Item` kept an old mtime on the library (E8).

### Shared

- **Drop the manual steps from the regen skill.**
  - Replace `SKILL.md:22` (Slang) and `:24` (VMA) with one paragraph: every
    pin bump re-stages its headers and binaries automatically, and the bump
    commit includes the package's committed `*.version` stamp next to its
    headers.
  - Add to the NGX table row: after an `NgxVersion` bump, the build fails
    until `setup-ngx.ps1` is re-run.
  - Regenerate the Codex mirror with `python tools/sync-codex.py`.
- Generated bindings under every `src/*/Generated/` are untouched; they are
  still derived only through `-t:Regenerate`.
- Incremental builds: when stamps match, every gated target skips as before.
  The added cost is a `File.ReadAllText` at evaluation time per stamp, on
  files of about 10 bytes.

### Why not the alternatives

- **A version in each staged directory path** (e.g. `staged/<ver>/<rid>/`).
  The staged paths are contracts across CI cache paths, artifact uploads,
  publish's copy steps and the pack targets (Slang: `ci.yml:146`,
  `build-slang-native.yml:88`, `:126`, `publish.yml:212-215`, `csproj:435`; KTX
  and VMA have the same shape at `publish.yml:165-192`). Versioning them
  would push pins into YAML. The KTX clone is the one place where the path is
  internal, so it is versioned there.
- **Reading the version back from the binary.** Slang's `staged/` has no
  executable, and loading a native DLL during MSBuild needs a task assembly.
  The SHA-256 / pins check already proves the bytes.
- **Parsing a version out of headers.** `slang.h` has none (E3). The KTX and
  NGX headers were not checked for one, because a single stamp convention
  beats per-package header parsers.
- **Inputs/Outputs, the NGX shim's mechanism** (`Ngx.Native.csproj:148-160`).
  It fits NGX because NGX builds from sources in the repo. It does not fit:
  - Slang, whose outputs carry upstream archive mtimes (E4) and are
    cache-restored.
  - KTX, whose binary is cache-restored (E7), so every hit would rebuild.
  - VMA, even though no cache restores its output. Its re-staged header keeps
    an archive mtime, `CopyGeneratedHeaders` resets the Vulkan headers'
    mtimes, and the Inputs check would also force `EnsureVulkanHeaders` to run
    on every build (E6).
  - Tracked headers, whose real input is a property, not a file.

  Keying on `Directory.Build.props` would re-stage every package on any pin
  bump.
- **Untracked or cached header stamps.** CI would re-fetch and rewrite
  tracked headers on every run (E2). This applies equally to VMA and KTX
  headers.
- **Stamping the KTX clone instead of versioning its path.** It would need a
  stamp, a clear-before-clone and a re-checkout. The versioned path needs
  none of these, and nothing outside the csproj's own targets references the
  path. The only other hits for `KTX-Software` are prose docs and a test
  fixture NOTICE.
- **NGX: auto-run `setup-ngx.ps1` from the build.** That is forbidden by
  #214: acquiring the SDK is an explicit act (`Ngx.Native.csproj:58-61`).
- **NGX: hash the staged library against `pins.sha256` instead of a stamp.**
  `pins.sha256` is committed and already the record, so this would need no
  script change. It would also hash a multi-MB `.lib` on every build and parse
  `pins.sha256` in MSBuild. A stamp costs one tiny file read.
- **`CopyToOutputDirectory="Always"` instead of `Touch`** (Slang). That copies
  ~33 MB into every consumer on every build.

### Residual risk

- **One-time cost on the first build after this lands, per developer:**
  - Slang: one re-stage (download).
  - KTX: one clone plus a full cmake rebuild, because no binary stamp exists
    yet (minutes).
  - NGX: anyone with a staged SDK hits the new Error once and re-runs
    `setup-ngx.ps1` (OPEN-3).
  - VMA: one cmake rebuild (seconds), because no binary stamp exists yet.
  - CI takes one cold cache miss per lane whose key hashes an edited file
    (Slang, KTX, NGX). The VMA lane's key does not include its csproj.
- **Edits to VMA's `vma.cpp` / `CMakeLists.txt`** are still not picked up
  locally, because existence plus a version stamp ignores source edits. This
  is the NGX shim's old defect (`Ngx.Native.csproj:148-154`), but it is not
  version-dependent and is out of scope here. CI's VMA key covers them. It
  would be a good follow-up.
- **The NGX shim's Inputs omit `VulkanHeadersVersion`'s headers.** This is
  also not an existence gate. The shim uses Vulkan types only by pointer.
- **KTX cmake feature flags** (`KtxCMakeFeatureFlags`) are not in the stamp.
  Changing them locally still needs a manual delete. They are not a version,
  and CI's key covers them.
- **The NGX shim's Inputs omit the NGX headers.** This is not a version gate,
  because a header change arrives with a pin bump, and the stamp/Error path
  then forces a re-stage and relink. It is recorded here and not fixed.
- **A bump commit that forgets its committed stamp** fails nothing outright.
  Every build and CI run re-stages the tracked headers, byte-identical, until
  the stamp is committed. The skill line is the guard.
- Stale `bin/` copies from **before** this change persist until the next
  re-stage or clean. `BuildTag_MatchesPinnedVersion` catches that for Slang.

## Links

- Resolves #256.
- Must land consistently with the #171/#172 design
  (`docs/design/specs/2026-11-06-issue-171-172-slang-shader-build-design.md`,
  A2 and "Pre-existing gap, not fixed"). The stamps reuse its
  `slangc.version` shape and naming. This design adds clearing to its
  `StageSlangCompiler`, and the #172 E8 ordering facts are what make that
  safe.
- Keeps #166's OPEN-1 guarantee: `_SlangStageNeeded` still requires
  `slang-glslang`.
- Keeps #214's rule that NGX acquisition is explicit (D3 errors; it never
  fetches).

## OPEN

- **OPEN-1 (resolved):** widen to all packages.
- **OPEN-2 (resolved):** a plain build re-stages tracked headers.
- **OPEN-3: what does the NGX build do with a stale or unstamped SDK?**
  - Recommendation: **fail the build** (D3). The developer has opted into
    DLSS by staging the SDK. Linking a different version than the committed
    headers and bindings is the defect, and a skip would turn
    `Ahjo.Vulkan.Ngx.Native.Tests` into green-on-nothing locally.
  - Cost: every developer who already has a staged SDK hits the Error once,
    because no stamp exists yet, and must re-run the script.
  - Alternative: treat a stale SDK like an absent one, with a high-importance
    `Message` and no shim build. That is the `WarnNgxSdkMissing` path
    (`:205-210`). It is softer and never blocks, but it is silent in exactly
    the way this issue is about.
  - `Warning` is not an option either way, because of
    `TreatWarningsAsErrors` (`:196-198`).
