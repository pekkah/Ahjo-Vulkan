Paired with [../specs/2026-11-13-issue-256-native-staging-version-stamp-design.md](../specs/2026-11-13-issue-256-native-staging-version-stamp-design.md).

# Plan: issue #256: version-stamp staged native inputs and binaries (Slang, VMA, KTX, NGX)

This plan changes MSBuild, `tools/setup-ngx.ps1`, one workflow cache path, workflow comments, docs
and the regen skill. It touches no C#, no `Generated/` file and no hot path, so it has no benchmark
step.

How to work through it:

- It is ordered **by package**. Finish and verify each Part before starting the next.
- Commit each Part separately (`<area>: <imperative>`), all on one branch and one PR. Do not stack
  PRs.
- OPEN-1 (widen) and OPEN-2 (a plain build re-stages headers) are resolved.
- **OPEN-3** (NGX stale SDK: Error or skip) must be answered before Part D. This plan assumes
  **Error**. If the answer is "skip", stop and ask before D2.
- All line numbers refer to `main` at `a66d5a1`.

**Shared convention.** Every stamp check uses the `_SlangToolNeeded` shape
(`Ahjo.Vulkan.Slang.Native.csproj:114-119`):

1. In the main group, set `_XNeeded` to `true` when the content or the stamp is missing.
2. In a separate `PropertyGroup` with condition `'$(_XNeeded)' != 'true'` (plus the project's RID
   guard), compare `$([System.IO.File]::ReadAllText(`<stamp>`).Trim())` with the trimmed pin and
   set `_XNeeded` to `true` if they differ.

Stamps are written with `<WriteLinesToFile File="…" Lines="…" Overwrite="true" />` as the **last**
write of the target that produced the content. The stamp value is always the pin with any leading
`v` trimmed.

---

## Part A: Slang (approved design; unchanged)

### A1. Stamp properties in `src/Ahjo.Vulkan.Slang.Native/Ahjo.Vulkan.Slang.Native.csproj`

1. Below `_SlangStagedGlslang` (`:95`), add
   `<_SlangStageStamp>$(_SlangStagedDir)slang.version</_SlangStageStamp>`.
2. `_SlangStageNeeded` (`:97`): add `or !Exists('$(_SlangStageStamp)')` inside the parentheses.
3. Below `_SlangIncludeDir` (`:55`), add
   `<_SlangHeaderStamp>$(_SlangIncludeDir)slang.version</_SlangHeaderStamp>`.
4. After `_SlangToolNeeded` (`:114`), add
   `<_SlangHeadersNeeded Condition="'$(_SlangRid)' != '' and (!Exists('$(_SlangIncludeDir)slang.h') or !Exists('$(_SlangHeaderStamp)'))">true</_SlangHeadersNeeded>`.
5. After the existing stamp group (`:117-119`), add two groups with the shared shape:
   - one for `_SlangStageNeeded` against `$(_SlangStageStamp)`;
   - one for `_SlangHeadersNeeded` against `$(_SlangHeaderStamp)`.

   Both compare with `$(_SlangVersionNumber)`.
6. Add a final group with no condition containing
   `<_SlangArchiveNeeded Condition="'$(_SlangStageNeeded)' == 'true' or '$(_SlangToolNeeded)' == 'true' or '$(_SlangHeadersNeeded)' == 'true'">true</_SlangArchiveNeeded>`.
7. Rewrite the comment at `:77-83`. It must say:
   - existence **plus** the stamp is the cache;
   - why the stamp exists: Windows names are unversioned, and Linux was only refreshed because the
     glslang file name happens to carry the version;
   - that the stamp lives inside `staged/<rid>/` so a CI cache hit restores it (spec E2);
   - the #166 OPEN-1 reason for checking both files, which stays.
8. Shorten the comment at `:108-113` so it refers to the shared pattern instead of repeating it.

### A2. Targets in the same csproj

1. `DownloadSlangArchive` (`:216`) and `ExtractSlangArchive` (`:235`): replace
   `('$(_SlangStageNeeded)' == 'true' or '$(_SlangToolNeeded)' == 'true')` with
   `'$(_SlangArchiveNeeded)' == 'true'`.
2. `ExtractSlangArchive`: add `<RemoveDir Directories="$(_SlangExtractDir)" />` before its `MakeDir`
   (`:236`).
3. `StageSlangBinaries` (`:288-330`):
   - At the top, add `<RemoveDir Directories="$(_SlangStagedDir)" />` and then
     `<MakeDir Directories="$(_SlangStagedDir)" />`.
   - Leave the `Copy` tasks as they are.
   - After the two `Error`s (`:316-320`), add an item `_SlangStagedShipped` with the staged
     destination files, then `<Touch Files="@(_SlangStagedShipped)" />`. The files are:
     - win-x64: `$(_SlangStagedDir)slang.dll`, `$(_SlangStagedDir)slang-compiler.dll`,
       `$(_SlangStagedDir)slang-glslang.dll`
     - linux-x64: `$(_SlangStagedPrimary)`, `$(_SlangStagedGlslang)`
   - Leave the `SLANG-LICENSE.txt` copy (`:325-327`) where it is.
   - **After** that copy, immediately before the `Message` (`:329`), write `$(_SlangStageStamp)`
     with `$(_SlangVersionNumber)`.
   - Add a sentence to the target comment explaining why the `Touch` is there: `PreserveNewest`
     compares mtimes, and staged files keep the archive's entry times (spec E4).
   - **Why `RemoveDir` is safe here.** The flags are fixed at evaluation time. `FetchSlang` runs
     once per build of this project, and consumers reach it through a `ProjectReference` (#171/#172
     spec E8). Verification A4.9 checks this under `-m`.
4. `StageSlangCompiler` (`:343-382`): at the top, add `<RemoveDir Directories="$(_SlangToolDir)" />`
   and `<MakeDir Directories="$(_SlangToolDir)" />`. Do not touch these files.
5. `StageSlangHeaders` (`:390-404`):
   - Set its condition to
     `'$(SkipSlangNativeFetch)' != 'true' and '$(_SlangHeadersNeeded)' == 'true'`.
   - After the `Copy`, write `$(_SlangHeaderStamp)`.
   - Then add
     `<Message Importance="high" Text="Staged Slang $(SlangVersion) headers into $(_SlangIncludeDir). Generated/ is derived from them by -t:Regenerate; commit the headers, slang.version and the regenerated output together." />`.
   - Do not add a `RemoveDir`; the directory is tracked.
   - Add a comment line explaining why the stamp is committed (spec E2).
6. Leave `PackSlangRuntimes` and the consumer `None` items unchanged.

### A3. Bootstrap the committed header stamp

1. Run `dotnet build src/Ahjo.Vulkan.Slang.Native/Ahjo.Vulkan.Slang.Native.csproj -c Release`.
2. `git status --porcelain native/slang` must show only `?? native/slang/include/slang.version`,
   with the content `2026.22`.
   - If any `.h` file is modified, **stop and report**.
   - `SLANG-LICENSE.txt` must show no diff.

### A4. Verify Slang

Run every case on Windows with
`dotnet build src/Ahjo.Vulkan.Slang.Native/Ahjo.Vulkan.Slang.Native.csproj -c Release -v:n`
unless the case says otherwise. Record each outcome in the PR body.

1. **No-op build.** Build twice. The second run must print no "Staged …" messages and must show no
   `DownloadFile` or `Unzip` activity.
2. **Stale runtime stamp.**
   - Write `0.0` into `staged/win-x64/slang.version` and add a junk file `stale.txt` in the same
     directory.
   - Build. The directory must re-stage, `stale.txt` must be gone, and the stamp must read `2026.22`.
   - The tool and header targets must skip.
3. **Missing runtime stamp.** Delete the stamp and build. The directory must re-stage.
4. **Stale header stamp only.**
   - Write `0.0` into `native/slang/include/slang.version`.
   - Delete `native/slang/downloaded/win-x64/`.
   - Build. Download and extract must run, and the headers must re-stage.
   - `git status native/slang` must be clean afterwards.
5. **Real bump and back.**
   - At the current pin, run `dotnet test --project tests/Ahjo.Vulkan.Slang.Native.Tests -c Release`
     once, so that `bin/` holds 2026.22.
   - Set the v2026.21 pins from `8b109a7`:
     - `SlangVersion` = `v2026.21`
     - `SlangWinX64Sha256` = `4b6f509e73397175ed36a341beb7ad15f4fff5feaf5f6d95fecf896c0d86b8de`
     - `SlangLinuxX64Sha256` = `691f9341e5bec099eec97ee30a83bcbef541cee7fe3b4de68a821d42f9506c2f`
   - Test again. Expect:
     - `BuildTag_MatchesPinnedVersion` passes against 2026.21. This leg is the downgrade proof for
       `Touch`.
     - `git diff 8b109a7 -- native/slang/include/*.h` is empty.
     - The header stamp reads `2026.21`.
   - Restore the props and test again. It must pass against 2026.22, and `git status` must be clean
     apart from this PR's changes.
   - Keep the Vulkan SDK `Bin` off `PATH` for this case (as PR #236 did).
6. **Fetch skipped.** Delete the runtime stamp and build with `-p:SkipSlangNativeFetch=true`. It
   must succeed with no fetch. Rebuild normally afterwards.
7. **Pack.** Run `dotnet pack src/Ahjo.Vulkan.Slang.Native -c Release -o <scratch>`.
   `runtimes/win-x64/native/` must contain exactly the three DLLs, and there must be no
   `slang.version` anywhere in the package.
8. **Suite.** Run `dotnet build Ahjo.Vulkan.slnx -c Release`, then
   `dotnet test tests/Ahjo.Vulkan.Slang.Tests -c Release`. Both must be green.
9. **`-m` stages once.**
   - Write `0.0` into both `staged/win-x64/slang.version` and `tool/win-x64/slangc.version`.
   - Run `dotnet build Ahjo.Vulkan.slnx -c Release -m -v:n`.
   - "Staged Slang v2026.22 for win-x64" and "Staged slangc v2026.22 for win-x64" must each appear
     **exactly once**, and the shader steps must pass.
   - If either message appears twice, **stop and report**.
10. **Linux (CI).** The `slang-native` linux-x64 job will miss the cache, because the key changed.
    Its "List staged output" step must show `libslang.so`, `libslang-glslang-2026.22.so` and
    `slang.version`, and nothing else.

Commit: `Slang.Native: re-stage binaries and headers on SlangVersion change`. The commit includes
`native/slang/include/slang.version`.

---

## Part B: VMA (`src/Ahjo.Vulkan.Vma.Native/Ahjo.Vulkan.Vma.Native.csproj`)

### B1. Properties

1. Fix `_VmaExtractDir` (`:49`) to
   `$(_VmaDownloadedRoot)VulkanMemoryAllocator-$(VmaVersion.TrimStart('v'))\`. `_VmaHeadersDir`
   (`:50`) follows from it.
2. Below `_VmaStagedHeaderDir` (`:60`), add:
   - `<_VmaVersionNumber>$(VmaVersion.TrimStart('v'))</_VmaVersionNumber>`
   - `<_VmaHeaderStamp>$(_VmaStagedHeaderDir)vma.version</_VmaHeaderStamp>`
   - `<_VmaBinaryStamp>$(_VmaBuildDir)vma.version</_VmaBinaryStamp>`
   - `<_VmaBinaryKey>$(_VmaVersionNumber);$(VulkanHeadersVersion)</_VmaBinaryKey>`
   - `<_VmaHeaderNeeded Condition="!Exists('$(_VmaStagedHeaderDir)vk_mem_alloc.h') or !Exists('$(_VmaHeaderStamp)')">true</_VmaHeaderNeeded>`
   - `<_VmaBinaryNeeded Condition="'$(_VmaLibFile)' != '' and (!Exists('$(_VmaStagedBinary)') or !Exists('$(_VmaBinaryStamp)'))">true</_VmaBinaryNeeded>`
3. After that group, add two shared-shape stamp groups:
   - `_VmaHeaderNeeded`, compared with `$(_VmaVersionNumber)`;
   - `_VmaBinaryNeeded`, compared with `$(_VmaBinaryKey)`.

### B2. `FetchVma` (`:96-129`)

1. Set the condition to `'$(_VmaHeaderNeeded)' == 'true'`.
2. Change the `_VmaExtractedHeader` include (`:117`) to `$(_VmaHeadersDir)vk_mem_alloc.h`.
   - Rewrite the comment at `:112-115`: the tree name is now computed (GitHub strips the `v`, hence
     the `TrimStart`) instead of globbed, so stray older trees no longer matter.
3. Change the `Error` condition (`:120-122`) to `!Exists('$(_VmaHeadersDir)vk_mem_alloc.h')`, with
   text naming `$(_VmaHeadersDir)`.
4. After the `Copy` (`:125-128`), write `$(_VmaHeaderStamp)` with `$(_VmaVersionNumber)`, then add
   `<Message Importance="high" Text="Staged VMA $(VmaVersion) header into $(_VmaStagedHeaderDir); commit it with vma.version and the regenerated bindings." />`.

### B3. `BuildVmaForHost` (`:149-188`)

1. Set the condition to
   `'$(_VmaLibFile)' != '' and '$(_VmaBinaryNeeded)' == 'true' and '$(SkipVmaNativeBuild)' != 'true'`.
2. Before the `MakeDir` (`:166`), add `<RemoveDir Directories="$(_VmaBuildDir)" />`.
3. After the normalising `Copy` (`:181-185`), write `$(_VmaBinaryStamp)` with `$(_VmaBinaryKey)`.
   It must come before the `Message`.
4. Extend the target comment (`:149-157`) with three points:
   - the stamp covers both pins the binary is compiled from;
   - the build dir is cleared because staged headers keep archive mtimes, so cmake's own check is
     not trustworthy (spec E6);
   - CI never reads the stamp. The `vma-native` lane builds with `SkipVmaNativeBuild=true`, and the
     other jobs start from an empty `build/`.

### B4. Bootstrap

Run `dotnet build src/Ahjo.Vulkan.Vma.Native -t:FetchVma`. `git status --porcelain native/vma` must
show only `?? native/vma/include/vma.version`, with the content `3.5.0`. If `vk_mem_alloc.h`
changed, **stop and report**.

### B5. Verify VMA (Windows; needs cmake)

1. **First build.** With no binary stamp yet, the first build must clear `build/win-x64/`, run
   cmake and write `vma.version` containing `3.5.0;vulkan-sdk-1.4.363.0`. A second build must skip
   both `BuildVmaForHost` and `FetchVma` on their conditions. Neither `cmake` nor
   `EnsureVulkanHeaders` may appear in that log.
2. **Stale binary stamp.** Write `0.0` into `build/win-x64/vma.version` and build. Expect a
   rebuild in a cleared directory. The header must be untouched.
3. **Stale header stamp.**
   - Write `0.0` into `native/vma/include/vma.version` and create a decoy tree
     `native/vma/downloaded/VulkanMemoryAllocator-0.0.0/include/vk_mem_alloc.h` containing junk.
   - Build. The header must re-stage from the 3.5.0 tree, not the decoy, and
     `git status native/vma` must be clean.
   - The binary stamp still matches, so the binary must **not** rebuild.
   - Delete the decoy afterwards.
4. **Real bump and back.**
   - The previous pin is `v3.4.0`, introduced in `5bf8e71` and replaced in `b16e3af`.
   - Set `VmaVersion` to `v3.4.0` and build. `git diff 5bf8e71 -- native/vma/include/vk_mem_alloc.h`
     must be empty. Expect:
     - `include/vma.version` reads `3.4.0`;
     - `build/win-x64/vma.version` reads `3.4.0;vulkan-sdk-1.4.363.0`;
     - `vma.dll` rebuilds.

     Do **not** run the VMA tests at this pin. The committed bindings are 3.5.0's.
   - Restore the pin and build again. Expect the header back at 3.5.0, a rebuild, and `git status`
     clean.
   - Then run `dotnet test tests/Ahjo.Vulkan.Vma.Native.Tests -c Release`. It must be green.
5. **Native build skipped.** Build with `-p:SkipVmaNativeBuild=true`. It must succeed without
   running cmake.
6. **CI.** No workflow change. `build-vma-native.yml:93` runs `-t:FetchVma`, which is now a no-op
   because the committed stamp matches.

Commit: `Vma.Native: re-stage header and rebuild binary on pin change`.

---

## Part C: KTX (`src/Ahjo.Vulkan.Ktx.Native/Ahjo.Vulkan.Ktx.Native.csproj`)

### C1. Properties

1. `_KtxSourceDir` (`:48`): set it to `$(_KtxDownloadedRoot)KTX-Software-$(KtxVersion)\`.
2. Add these properties:
   - `<_KtxVersionNumber>$(KtxVersion.TrimStart('v'))</_KtxVersionNumber>`
   - `<_KtxHeaderStamp>$(_KtxStagedHeaderDir)ktx.version</_KtxHeaderStamp>`
   - `<_KtxStagedDir>$(_KtxStagedRootDir)$(_KtxRid)\</_KtxStagedDir>`
   - `<_KtxBinaryStamp>$(_KtxStagedDir)ktx.version</_KtxBinaryStamp>`
   - `<_KtxHeadersNeeded Condition="!Exists('$(_KtxStagedHeaderDir)ktx.h') or !Exists('$(_KtxHeaderStamp)')">true</_KtxHeadersNeeded>`
   - `<_KtxBinaryNeeded Condition="'$(_KtxRid)' != '' and (!Exists('$(_KtxStagedBinary)') or !Exists('$(_KtxBinaryStamp)'))">true</_KtxBinaryNeeded>`
3. Add two shared-shape stamp groups, one for `_KtxHeadersNeeded` and one for `_KtxBinaryNeeded`.
   Each compares with `$(_KtxVersionNumber)`.

### C2. Targets

1. `CloneKtx` (`:108-119`):
   - Keep the condition. It is now on the versioned path.
   - Before the existing `MakeDir`, add `<RemoveDir Directories="$(_KtxDownloadedRoot)" />`.
   - Add a comment explaining that a bump clones into a new directory and that clearing drops
     earlier ~170 MB clones.
   - If `RemoveDir` fails on git's read-only pack files under `.git/objects`, **stop and report**.
     Do not add an `Exec rm -rf`. That shape needs a decision.
2. `FetchKtx` (`:106`): add `BeforeTargets="AssignTargetPaths"` and
   `Condition="'$(SkipKtxNativeBuild)' != 'true' and ('$(_KtxHeadersNeeded)' == 'true' or '$(_KtxBinaryNeeded)' == 'true')"`.
   Keep `DependsOnTargets="CloneKtx;StageKtxHeaders"`. Comment: headers re-stage in a plain build
   on their own stamp (OPEN-2), and CI never triggers it while the committed stamp matches.
3. `StageKtxHeaders` (`:121-132`):
   - Set the condition to `'$(_KtxHeadersNeeded)' == 'true'`.
   - After both copies, write `$(_KtxHeaderStamp)`, then add a high-importance `Message`. Use the
     same text as Slang's header message, with KTX names.
4. `BuildKtxForHost` (`:153-169`):
   - Set the condition to
     `'$(_KtxLibFile)' != '' and '$(_KtxBinaryNeeded)' == 'true' and '$(SkipKtxNativeBuild)' != 'true'`.
   - Before the `MakeDir` (`:160`), add `<RemoveDir Directories="$(_KtxBuildDir);$(_KtxStagedDir)" />`.
     `CMakeCache.txt` pins the old `-S` and cmake rejects a different one.
   - Rewrite the comment at `:141-147`. The stamp, not existence, is now the cache, and "once per
     KtxVersion bump" is true locally now too.
5. `StageKtxBinary` (`:177-191`): after the `Copy`, write `$(_KtxBinaryStamp)`. It must come before
   the `Message`.

### C3. CI: `.github/workflows/build-ktx-native.yml`

1. Change the cache `path:` (`:81`) to a block listing both files:
   ```yaml
   path: |
     native/ktx/staged/${{ matrix.rid }}/${{ matrix.lib-file }}
     native/ktx/staged/${{ matrix.rid }}/ktx.version
   ```
2. Update the comment at `:68-73`: the binary **and its stamp** are the cache, and without the stamp
   a hit would rebuild.
3. Leave the upload `path:` (`:115`) and the key unchanged.

### C4. Bootstrap

Run `dotnet build src/Ahjo.Vulkan.Ktx.Native -t:FetchKtx`. This clones into `KTX-Software-v4.4.2`
and re-stages the headers.

- `git status --porcelain native/ktx` must show only `?? native/ktx/include/ktx.version`, with the
  content `4.4.2`.
- If `ktx.h` or `KHR/khr_df.h` changed, **stop and report**.

### C5. Verify KTX (Windows; needs git and cmake)

1. **First build.** Build `src/Ahjo.Vulkan.Ktx.Native`. With no binary stamp yet, it must rebuild
   once and write `staged/win-x64/ktx.version`. A second build must be a no-op: no clone, no cmake.
2. **Stale binary stamp.** Write `0.0` into `staged/win-x64/ktx.version` and build. Expect a clean
   cmake configure with no CMakeCache source-mismatch error, then a build and a re-stamp. The
   clone and the headers must be untouched.
3. **Bump and back.**
   - `KtxVersion` has never changed (`7019f3e` introduced `v4.4.2`), so there is no known-good
     earlier pin with matching committed headers. Instead, simulate a bump without committing:
     - Set `KtxVersion` to `v4.4.0` and run `-t:FetchKtx` only. Expect a new
       `KTX-Software-v4.4.0` clone, the `v4.4.2` clone gone, re-staged headers, and a header stamp
       reading `4.4.0`.
     - Do **not** run the KTX tests on this pin. The generated bindings are 4.4.2's.
   - Restore the pin and run a **plain** `dotnet build src/Ahjo.Vulkan.Ktx.Native`. Expect:
     - the `FetchKtx` hook fires on the stale header stamp;
     - a `KTX-Software-v4.4.2` clone again;
     - headers and stamp back to 4.4.2, with `git status native/ktx` clean;
     - **no** cmake run, because the binary stamp still reads 4.4.2.

     This leg proves C2.2.
   - Then run `dotnet test tests/Ahjo.Vulkan.Ktx.Native.Tests -c Release`.
   - If the tag `v4.4.0` does not exist upstream, use any earlier `v4.*` tag. Name it in the PR
     body.
4. **Native build skipped.** Build with `-p:SkipKtxNativeBuild=true`. Nothing native may run.
5. **CI.** The `ktx-native` jobs miss the cache once, because the csproj and the workflow changed.
   - On a re-run of the same commit, the cache must **hit**, and the log must show no clone and no
     cmake.
   - That re-run proves the stamp is in the cache path.

Commit: `Ktx.Native: rebuild and re-stage on KtxVersion change`.

---

## Part D: NGX (requires OPEN-3 answered)

### D1. `tools/setup-ngx.ps1`

1. After the `-UpdatePins` block (`:260-277`) and before the summary `Write-Host ""` (`:279`),
   write the stamps:
   - When `$wantWin` is set, write `$Bare` to `native/ngx/staged/win-x64/ngx.version`.
   - When `$wantLinux` is set, write `$Bare` to `native/ngx/staged/linux-x64/ngx.version`.
   - Use `Set-Content -LiteralPath … -Value $Bare -Encoding utf8NoBOM` and create the directory if
     it is missing.
   - Add `ngx.version` to the "Staged under native/ngx/" list (`$staged`).
2. Leave the `throw` on verification failures (`:255-258`) in place before the stamp write. A failed
   run must leave the old stamp, or none.
3. In the comment-based help (`:9-12`), add two lines:
   - the script stamps each staged platform with the tag, and the build refuses a stale stamp;
   - the stamp vouches for the client library only, and feature DLLs from an earlier run may remain
     after `-SkipFeatureDll`.

### D2. `src/Ahjo.Vulkan.Ngx.Native/Ahjo.Vulkan.Ngx.Native.csproj`

1. Below `_NgxStaticLib` (`:62-63`), add:
   - `<_NgxSdkStamp>$(_NgxStagedDir)ngx.version</_NgxSdkStamp>`
   - `<_NgxSdkStale Condition="'$(_NgxRid)' != '' and Exists('$(_NgxStaticLib)') and !Exists('$(_NgxSdkStamp)')">true</_NgxSdkStale>`
2. Add the shared-shape stamp group. Its condition is
   `'$(_NgxRid)' != '' and Exists('$(_NgxStaticLib)') and '$(_NgxSdkStale)' != 'true'`, and it
   compares with `$(NgxVersion.TrimStart('v'))`.
3. Add a new target, `ErrorNgxSdkStale`:
   - `BeforeTargets="AssignTargetPaths"`
   - `Condition="'$(_NgxRid)' != '' and '$(SkipNgxNativeBuild)' != 'true' and '$(_NgxSdkStale)' == 'true'"`
   - It contains one `<Error>` with this text:
     `The NGX SDK staged at $(_NgxStagedDir) is not $(NgxVersion) (ngx.version is missing or different). Re-run ./tools/setup-ngx.ps1 (add -SkipFeatureDll if you only build the shim). The build never fetches the SDK itself (#214).`
   - Add a comment explaining why this is an Error and not a skip (OPEN-3), and why it is not a
     `Warning`.
4. `BuildNgxForHost` (`:162-167`):
   - Append `;$(_NgxSdkStamp)` to `Inputs`.
   - Add `and '$(_NgxSdkStale)' != 'true'` to the condition, so a stale SDK never links.
   - Extend the comment at `:156-160` with one sentence on why the stamp is an input: `Copy-Item`
     keeps a cached download's old mtime (spec E8).
5. Leave the `None` item (`:95-101`) and `WarnNgxSdkMissing` (`:205-210`) unchanged.

### D3. CI

No workflow change. The `ngx-native` cache key hashes `tools/setup-ngx.ps1`, so the cache misses
once. The lane runs the script on every run (`build-ngx-native.yml:109`), so the stamp is always
present.

### D4. Verify NGX (Windows; needs the SDK staged)

1. **Not staged.** With no SDK staged (move `native/ngx/staged/win-x64` aside), build. Expect only
   the `WarnNgxSdkMissing` message and success.
2. **Unstamped.** Restore the staged directory without `ngx.version` and build. The build must fail
   with the D2.3 text.
3. **Fresh stamp.**
   - Run `./tools/setup-ngx.ps1 -SkipFeatureDll`. `ngx.version` must hold `310.9.16`.
   - Build. The shim must relink, because the stamp is an input.
   - Run `dotnet test tests/Ahjo.Vulkan.Ngx.Native.Tests -c Release` with `AHJO_NGX_REQUIRE_SHIM=1`.
     It must be green.
4. **Stale stamp.** Write `0.0` into `ngx.version` and build. The build must fail. Re-run the script,
   then build; it must succeed.
5. **Native build skipped.** Build with `-p:SkipNgxNativeBuild=true` and a stale stamp. It must
   succeed.

Commit: `Ngx.Native: refuse a staged SDK that does not match NgxVersion`.

---

## Part E: Docs, skill and Codex mirror

1. **`.claude/skills/regen-bindings/SKILL.md`**
   - Replace `:22` (Slang gotcha) and `:24` (VMA gotcha) with one paragraph. It says that a pin bump
     re-stages each package's headers and binaries automatically (version stamps, #256), and that
     the bump commit must include the committed stamp next to the headers:
     - `native/slang/include/slang.version`
     - `native/vma/include/vma.version`
     - `native/ktx/include/ktx.version`

     If the stamp is forgotten, every build re-stages until it is committed. Keep the
     `grep VMA_VERSION native/vma/include/vk_mem_alloc.h` sanity check.
   - NGX row (`:18`): add "after an `NgxVersion` bump the build fails until `./tools/setup-ngx.ps1`
     is re-run".
2. **`src/Ahjo.Vulkan.Slang.Native/CLAUDE.md`**
   - `:34`: the staged tree is reused only when both files exist **and** `slang.version` equals the
     pin. Re-staging clears the directory and touches the files.
   - `:36`: all three outputs now use the same stamp rule.
   - `:42`: `include/slang.version` is committed.
3. **`src/Ahjo.Vulkan.Vma.Native/CLAUDE.md:11`**: add that the binary rebuilds, in a cleared
   build dir, when `VmaVersion` or `VulkanHeadersVersion` changes (`build/<rid>/vma.version`), and
   that the header carries a committed `vma.version`. Source edits to `vma.cpp` / `CMakeLists.txt`
   still need a manual delete of `build/<rid>/`.
4. **`src/Ahjo.Vulkan.Ktx.Native/CLAUDE.md:13`**: the staged binary **and** `ktx.version` are the CI
   cache, and the headers carry a committed `ktx.version`.
5. **`src/Ahjo.Vulkan.Ngx.Native/CLAUDE.md`**
   - `:8`: change "only needed once, or after a pin bump" so it notes that the build now enforces
     this.
   - `:59`: add one sentence on `ErrorNgxSdkStale`.
6. **`native/CLAUDE.md`**
   - `:4`: change it to "`ktx/downloaded/KTX-Software-<tag>/` … earlier clones are removed on a
     bump".
   - `:5`, `:7` and `:9`: mention the stamps.
   - `:12`: add that `ngx/staged/<rid>/ngx.version` is written by the script.
7. **`.github/workflows/build-slang-native.yml`**
   - `:69-70`: change it to "every fetch target is conditioned on the staged files and their
     `slang.version` stamp".
   - `:80`: change it to "… when either directory is missing or stamped with a different version".
8. **`.github/CLAUDE.md`**
   - `:29`: change "the staged binary … is both the cache key and the artifact" to "the staged
     binary and its `ktx.version` stamp are the cache; the binary alone is the artifact".
   - `:37`: add that the artifact also carries `slang.version`, which is never packed.
9. Run `python tools/sync-codex.py`, then `python tools/sync-codex.py --check`, which must exit 0.
   Commit `.agents/skills/regen-bindings/SKILL.md` and every regenerated `AGENTS.md`.

Commit: `Docs: document version-stamped native staging; sync Codex mirror`.

## Part F: PR

- One PR from this branch, with `Closes #256`.
- The body records the outcomes of A4, B5, C5 and D4, and the OPEN-3 answer.
- It names the breaking local-workflow change: NGX users must re-run `setup-ngx.ps1` once, and KTX
  rebuilds once per developer.
- Before marking the PR ready, check that every lane is green: `slang-native`, `ktx-native`,
  `ngx-native`, the VMA lanes and `build-test`. The C5.5 cache-hit re-run needs a second run of the
  `ktx-native` job.
