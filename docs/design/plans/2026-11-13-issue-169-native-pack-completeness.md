Paired with [../specs/2026-11-13-issue-169-native-pack-completeness-design.md](../specs/2026-11-13-issue-169-native-pack-completeness-design.md). Read it first; this plan only says *how*.

# Implementation plan: issue #169 (packed half), native pack completeness

- **Branch:** `issue-169-slang-glslang-path-shadowing`, the same branch and PR as `2026-11-13-issue-169-slang-native-module-provenance.md`. Execute that plan first; it is test-only and independent.
- **Scope:** `tools/msbuild/`, two new `tools/` scripts, the four native csprojs, `Directory.Build.targets`, `ci.yml`, `publish.yml`, docs. No runtime code, no `Generated/`, no test project changes.
- **Commits** (suggested, one PR): `Packaging: fail pack on a missing native runtime file`, then `CI: verify native nupkg contents on PRs and before push`.

## Step 1: Shared target `tools/msbuild/Ahjo.Vulkan.NativePack.targets` (new)

One target, `AhjoPackNativeFiles`:

```xml
<Target Name="AhjoPackNativeFiles"
        BeforeTargets="_GetPackageFiles;GenerateNuspec"
        DependsOnTargets="$(AhjoNativePackDependsOn)"
        Condition="'$(IsPackable)' == 'true' and '$(AhjoNativePackDependsOn)' != ''">
```

Body, in this order (the shape was prototyped; spec E5):

1. `<Error Code="AHJOPACK002" Condition="'@(AhjoNativePackFile)' == ''" Text="$(PackageId): $(AhjoNativePackDependsOn) declared no AhjoNativePackFile items." />`
2. `<Error Code="AHJOPACK001" Condition="'$(AhjoAllowPartialNativePack)' != 'true' and !Exists('%(AhjoNativePackFile.Identity)')" Text="$(PackageId): %(AhjoNativePackFile.PackagePath) is in the pack list but its source %(AhjoNativePackFile.FullPath) does not exist. Refusing to pack an incomplete package; stage every RID (CI: tools/stage-native-artifacts.sh) or pass -p:AhjoAllowPartialNativePack=true for a local host-RID-only pack." />`. This batches per item, one error per missing file.
   **Superseded during implementation:** a batched `<Error>` stops at the first failing batch, so only the first missing file was named. The target now collects `_AhjoMissingNativePackFile` (the `AhjoNativePackFile` items whose source does not exist), then raises one `AHJOPACK001` listing `@(_AhjoMissingNativePackFile->'%(PackagePath) (from %(FullPath))', '; ')`. See the spec, Layer 1.
3. `<None Include="@(AhjoNativePackFile)" Condition="Exists('%(AhjoNativePackFile.Identity)')" Pack="true" Visible="false" />`. `PackagePath` flows from the item metadata. The `Exists` filter is kept, so the opt-out still packs what is there.
4. A manifest of the **full declared list, not filtered by `Exists`**: `<_AhjoNativeManifestLine Include="@(AhjoNativePackFile->'%(PackagePath)'->Replace('\','/'))" />`, then `<WriteLinesToFile File="$([MSBuild]::EnsureTrailingSlash('$(PackageOutputPath)'))$(PackageId).native-files.txt" Lines="@(_AhjoNativeManifestLine)" Overwrite="true" />`. `EnsureTrailingSlash` is **required**: `-o out` yields `PackageOutputPath=out` (measured, spec E5). The name deliberately carries **no** `$(PackageVersion)`: MinVer timing relative to this target is unmeasured, and the verifier locates the nupkg itself.

`AhjoNativePackFile` item contract, written as a header comment in the file: the `Include` is the source file, and `PackagePath` is the **full** entry path including the file name (`runtimes\<rid>\native\<file>`, or `<LICENSE>.txt` at the root). Header comment also cites both specs.

Import it from `Directory.Build.targets`, next to the existing line 3: `<Import Project="$(MSBuildThisFileDirectory)tools\msbuild\Ahjo.Vulkan.NativePack.targets" />`, with a one-line comment `<!-- Native pack completeness (#169). Inert unless a project sets AhjoNativePackDependsOn. -->`.

## Step 2: The four native csprojs declare instead of include

The change has the same shape in each csproj:
- Add `<AhjoNativePackDependsOn>Pack<Xxx>Runtimes</AhjoNativePackDependsOn>` to the project's main `PropertyGroup`.
- On the existing `Pack<Xxx>Runtimes` target, **remove** `BeforeTargets="_GetPackageFiles;GenerateNuspec"`. The shared target now pulls it in through `DependsOnTargets`. Keep `Condition="'$(IsPackable)' == 'true'"`.
- Keep the existing list item group byte-for-byte (`_SlangPackFile`, `_VmaPackRid`, `_KtxPackRid`, `_NgxPackRid`). Replace the `None … Condition="Exists(…)"` group with `AhjoNativePackFile` items carrying the same source path and the same `PackagePath`. Licences become `AhjoNativePackFile` items with `PackagePath` = the file name. The `\` root path used today is not a full entry path, and the manifest needs one.
- Keep the `<Message Importance="high" …>`.
- Rewrite the "Local pack only includes the host's RID — that's intentional" comment: pack is now strict, local host-RID-only pack needs `-p:AhjoAllowPartialNativePack=true`, and see the #169 pack spec.

| csproj | target (line) | `AhjoNativePackFile` Include → PackagePath |
|---|---|---|
| `src/Ahjo.Vulkan.Slang.Native/Ahjo.Vulkan.Slang.Native.csproj` | `PackSlangRuntimes` (`:422`) | `$(_SlangStagedRootDir)%(_SlangPackFile.Rid)\%(_SlangPackFile.Identity)` → `runtimes\%(_SlangPackFile.Rid)\native\%(_SlangPackFile.Identity)`; `$(_SlangSrcDir)SLANG-LICENSE.txt` → `SLANG-LICENSE.txt` |
| `src/Ahjo.Vulkan.Vma.Native/Ahjo.Vulkan.Vma.Native.csproj` | `PackVmaRuntimes` (`:199`) | `$(_VmaBuildRootDir)%(_VmaPackRid.Identity)\%(_VmaPackRid.LibFile)` → `runtimes\%(_VmaPackRid.Identity)\native\%(_VmaPackRid.LibFile)` |
| `src/Ahjo.Vulkan.Ktx.Native/Ahjo.Vulkan.Ktx.Native.csproj` | `PackKtxRuntimes` (`:202`) | `$(_KtxStagedRootDir)%(_KtxPackRid.Identity)\%(_KtxPackRid.LibFile)` → `runtimes\%(_KtxPackRid.Identity)\native\%(_KtxPackRid.LibFile)` |
| `src/Ahjo.Vulkan.Ngx.Native/Ahjo.Vulkan.Ngx.Native.csproj` | `PackNgxRuntimes` (`:228`) | `$(_NgxBuildRootDir)%(_NgxPackRid.Identity)\%(_NgxPackRid.LibFile)` → `runtimes\%(_NgxPackRid.Identity)\native\%(_NgxPackRid.LibFile)`; `$(_NgxSrcDir)NGX-LICENSE.txt` → `NGX-LICENSE.txt` |

The KTX target's comment says the binaries live under `native/ktx/lib/<rid>/`, but the code uses `_KtxStagedRootDir` = `native/ktx/staged/` (`Ahjo.Vulkan.Ktx.Native.csproj:64`). Fix the comment while you are there.

**Check:** for each package, the set of `runtimes/**` + licence entries in a fully staged pack must be **identical** to today's. This is a refactor of how entries are included, not of which ones. Step 5 verifies it.

## Step 3: `tools/stage-native-artifacts.sh` (new, POSIX sh)

The script is `tools/stage-native-artifacts.sh <download-root> <vma|ktx|slang|ngx>`. It replaces the bodies of the four `publish.yml` "Stage … binaries for pack" steps (`:161`, `:185`, `:208`, `:236`).

| kind | artifact dir glob under `<download-root>` | destination |
|---|---|---|
| `vma` | `vma-native-*` | `native/vma/build/<rid>/` |
| `ktx` | `ktx-native-*` | `native/ktx/staged/<rid>/` |
| `slang` | `slang-native-*` | `native/slang/staged/<rid>/` |
| `ngx` | `ngx-native-*` | `native/ngx/build/<rid>/` |

For each matching directory, derive `<rid>` by stripping the `<kind>-native-` prefix, run `mkdir -p` on the destination, and `cp -rv "$dir"/. "$dest/"`. `-r` for all four kinds; today only NGX needs it, and it is harmless for the others. The script holds **no RID list**. It must exit non-zero with a message if `<kind>` is unknown, and if **no** directory matched (that means the download step got nothing, which differs from a single RID going missing; a missing RID is Layer 1's job). `set -eu`. It runs from the repo root, the workflows' working directory.

Mark it executable (`git update-index --chmod=+x`).

## Step 4: `tools/verify-native-nupkgs.py` (new)

The script is `python3 tools/verify-native-nupkgs.py <artifacts-dir> <PackageId> [<PackageId>...]`, standard library only. Behaviour (prototyped, spec E5):

For each `PackageId`:
- the manifest is `<dir>/<PackageId>.native-files.txt`. If it is missing → fail with `"{id}: no {id}.native-files.txt in {dir} (did its pack target run?)"`.
- the nupkg is the single file in `<dir>` matching `^<escaped PackageId>\.\d.*\.nupkg$`. The `\.\d` keeps `Ahjo.Vulkan.Slang` from matching `Ahjo.Vulkan.Slang.Native…`, and `.snupkg` does not match. If not exactly one → fail with `"{id}: expected exactly one {id}.<version>.nupkg in {dir}, found {n}"`.
- Manifest has no `runtimes/` line → fail.
- For each manifest line not in the zip's `namelist()` → fail with `"{id}: {entry} is declared in the pack list but is not in {nupkg}"`.
- For each zip entry starting with `runtimes/` that is not in the manifest → fail with `"{id}: {entry} is in {nupkg} but not in the pack list"` (this is the NGX feature-DLL guard).
- Print `"{id}: {n} declared, checked against {nupkg}"` on success.

Each failure is printed as `::error::<text>`. Exit 1 if any failure, else 0. Run with `python3 -I` in workflows. Python is preinstalled on `ubuntu-latest`, and the repo already has Python tooling (`tools/sync-codex.py`).

## Step 5: `publish.yml`

- Replace the four "Stage … binaries for pack" `run:` bodies with `sh tools/stage-native-artifacts.sh "${{ github.workspace }}/<kind>-artifacts" <kind>`. Keep each step's name, `if:` and the comments above it, trimmed of the loop description.
- No flag changes on the pack steps. Strict is the default.
- New step **after** "Pack (NGX wrapper)" and **before** "List artifacts" (`:376`), named `Verify native nupkg contents`. It builds the ID list from the `needs.build-*.result == 'success'` conditions already used by the pack steps (`Ahjo.Vulkan.Vma.Native`, `Ahjo.Vulkan.Ktx.Native`, `Ahjo.Vulkan.Slang.Native`, `Ahjo.Vulkan.Ngx.Native`). If the list is non-empty it runs `python3 -I tools/verify-native-nupkgs.py "${{ github.workspace }}/artifacts" <ids>`. Comment: this is the last gate before the NuGet push, see #169.
- The `<PackageId>.native-files.txt` manifests land in `artifacts/`. Check that the upload glob `*.*nupkg` (`:383`), the push glob `*.nupkg` and the release-attach globs do **not** match them. They don't as written. Do not widen those globs.

## Step 6: `ci.yml`, PR-time pack check

Add two jobs after `ngx-native` (`:514-516`):

```yaml
  vma-native:
    name: VMA native
    uses: ./.github/workflows/build-vma-native.yml

  native-pack:
    name: Native pack completeness
    runs-on: ubuntu-latest
    needs: [vma-native, ktx-native, slang-native, ngx-native]
```

`native-pack` steps:
1. Checkout with `fetch-depth: 0`, mirroring `publish.yml`, because MinVer on a shallow clone was not measured.
2. Setup .NET, identical to `publish.yml`'s.
3. Four `actions/download-artifact@v8` steps, patterns `vma-native-*`, `ktx-native-*`, `slang-native-*` and `ngx-native-*`, into `${{ runner.temp }}/<kind>-artifacts`.
4. `sh tools/stage-native-artifacts.sh "${{ runner.temp }}/<kind>-artifacts" <kind>` for each kind.
5. Pack the four native projects into `${{ runner.temp }}/nupkgs`, with the same Skip flags `publish.yml` uses: `-p:SkipVmaNativeBuild=true` / `-p:SkipKtxNativeBuild=true` / `-p:SkipSlangNativeFetch=true` / `-p:SkipNgxNativeBuild=true`, and `-c Release`. VMA is packed **without** `--no-build` here, because no core build precedes it. If that fails for a reason unrelated to this change, stop and report; don't improvise a core build.
6. `python3 -I tools/verify-native-nupkgs.py "${{ runner.temp }}/nupkgs" Ahjo.Vulkan.Vma.Native Ahjo.Vulkan.Ktx.Native Ahjo.Vulkan.Slang.Native Ahjo.Vulkan.Ngx.Native`.

Header comment on both jobs. `vma-native` is the same reusable workflow publish calls, now also proven on PRs; it runs no tests. `native-pack` packs and inspects and runs **no test suite**, so it is not a test lane (`.github/CLAUDE.md`). No `AHJO_VULKAN_TIER`, no artifact upload.

Update `build-vma-native.yml:9`'s header line from "invoked by publish.yml before pack" to name both callers.

## Step 7: Tests (negative cases, quoted in the PR)

CI can only show these green, so run the red cases locally and quote one line each in the PR description:

| # | action | expected |
|---|---|---|
| 1 | `dotnet pack src/Ahjo.Vulkan.Slang.Native -c Release -p:SkipSlangNativeFetch=true -o <tmp>` with only win-x64 staged | **exit 1**, `AHJOPACK001` naming `runtimes\linux-x64\native\libslang.so` and `…libslang-glslang-2026.22.so` |
| 2 | same, `slang-glslang.dll` moved aside in `native/slang/staged/win-x64/` | exit 1, `AHJOPACK001` naming `runtimes\win-x64\native\slang-glslang.dll` (restore the file afterwards) |
| 3 | row 1 plus `-p:AhjoAllowPartialNativePack=true` | exit 0; `Ahjo.Vulkan.Slang.Native.native-files.txt` lists all 6 declared entries, unfiltered (prototype-measured shape, spec E5) |
| 4 | `verify-native-nupkgs.py <tmp> Ahjo.Vulkan.Slang.Native` on the row 3 output | **exit 1**, names the two linux-x64 entries |
| 5 | verifier with an ID that was not packed (e.g. `Ahjo.Vulkan.Ktx.Native` in row 3's dir) | exit 1, "did its pack target run?" |
| 6 | `sh tools/stage-native-artifacts.sh <empty-dir> slang` | non-zero, "no … matched" |
| 7 | the PR's own `native-pack` job | green, with four "N declared, checked against …" lines. Quote them |

Also confirm the **Step 2 equality check**. Take the `native-pack` job's nupkgs, or a `workflow_dispatch` publish dry run (`release=false`) and download its `nuget-packages` artifact, and compare each native package's `runtimes/**` + licence entries with the latest published preview on nuget.org. They must be identical.

After any local pack, revert build side effects. `native/slang/SLANG-LICENSE.txt` gets CRLF churn on a Windows build: `git checkout -- native/slang/SLANG-LICENSE.txt`.

## Step 8: Docs

1. `.github/CLAUDE.md`: new section `## native-pack job — the nupkg is opened before anything ships`. It covers what the job does, that it runs no tests (not a lane, don't grow it), that `vma-native` is now called by both workflows, the two layers, the `AhjoAllowPartialNativePack` opt-out ("never set it in a workflow"), and that the same verifier gates the publish push.
2. `docs/ci-coverage.md`: add `vma-native` and `native-pack` rows to the lane table near `:76-78`, with tier `—` and reason "packaging, not Vulkan coverage".
3. `tools/CLAUDE.md`: a paragraph after the `SlangShaders.targets` one, describing `msbuild/Ahjo.Vulkan.NativePack.targets` (contract, `AHJOPACK001`/`002`, opt-out, manifest), `stage-native-artifacts.sh` and `verify-native-nupkgs.py`.
4. `src/Ahjo.Vulkan.Slang.Native/CLAUDE.md`: in "The shipped subset", one sentence saying a missing shipped file now fails pack (`AHJOPACK001`) and the nupkg is verified on PRs. Do the same for the other three native packages' `CLAUDE.md` where they describe their shipped set (`src/Ahjo.Vulkan.{Vma,Ktx,Ngx}.Native/CLAUDE.md`). If one doesn't describe it, skip it rather than inventing a section.
5. `python tools/sync-codex.py`, then `--check`. Commit the mirrors.

There is no benchmark step: no runtime code changes.

## Step 9: Verify invariants

- `git diff --stat main` touches only the files named in Steps 1–8, plus the provenance plan's files.
- Normal `dotnet build` of every project is unaffected. `AhjoPackNativeFiles` hangs off pack-only targets: run a Release `dotnet build` of the solution and confirm no `AHJOPACK` diagnostics appear.
- No `src/` runtime code, so the AOT smoke is unaffected.

## Risk notes

- **`DependsOnTargets` + removed `BeforeTargets`.** If the shared target's condition is false (for example `IsPackable` unset), the per-package target no longer runs at all. That is intended, because it only declares items. The verifier's "no manifest" failure is the backstop.
- **MinVer in `native-pack`.** The version string only names the files. The verifier globs by ID plus a digit, so any version passes. If MinVer emits a warning that fails the pack under `TreatWarningsAsErrors`, report it; don't add `NoWarn`.
- **Cold VMA build on PRs** is unmeasured (spec E4: 6–16 s was cache hits). If it is materially slower, report it; the trade-off is OPEN-A below.

## OPEN items

- **OPEN-A: VMA on every PR.** The plan adds `vma-native` to `ci.yml` so VMA's nupkg is checked before merge, like the other three. Cached cost is measured at 6–16 s per job. Cold cost is unmeasured. It adds 4 matrix jobs, including a `ubuntu-24.04-arm` runner. The alternative is to leave VMA publish-time only: its nupkg would still be verified, but only on the `push: main` run that also pushes the preview. The architect's default is **add it** (fix the whole class, cheap when cached). The user can veto it before implementation.
  **If vetoed**, remove exactly these and nothing else: the `vma-native` job in Step 6; `vma-native` from `native-pack`'s `needs`; the `vma-native-*` download step and the `vma` staging call in `native-pack`; the VMA pack command in `native-pack`; `Ahjo.Vulkan.Vma.Native` from `native-pack`'s verifier call; the `build-vma-native.yml:9` header edit; and the `vma-native` row in `docs/ci-coverage.md`. Steps 1, 2 (including the VMA csproj), 3, 4 and 5 stay unchanged, so publish still fails pack and verifies the VMA nupkg before the push.
