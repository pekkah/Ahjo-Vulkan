# Native packages: refuse to pack, and refuse to ship, a nupkg missing a declared runtime file

**Issue:** [#169](https://github.com/pekkah/Ahjo-Vulkan/issues/169), the "or packed" half: *"the Optimization tests are green" does not prove `slang-glslang` was staged **or packed**.*
**Companion spec (same PR, separate decision):** `2026-11-13-issue-169-slang-native-module-provenance-design.md` closes the **staged** half (which binary the test process loads). This spec closes the **packed** half. They are two independent mechanisms (a test vs. pack-time MSBuild plus a CI step), so per `docs/design/CLAUDE.md` each gets its own pair. The user decided they ship in one PR.
**Widens to:** every package that ships native files. `Ahjo.Vulkan.Vma.Native`, `Ahjo.Vulkan.Ktx.Native` and `Ahjo.Vulkan.Ngx.Native` have the same gap (E2). The user's direction is to fix the whole class.
**Lands consistently with:** `.github/CLAUDE.md` ("defined ONCE … called by both `ci.yml` and `publish.yml`"; native lanes are build-artifact checks, don't grow them); [#166](https://github.com/pekkah/Ahjo-Vulkan/issues/166) OPEN-1 (`slang-glslang` ships); [#214](https://github.com/pekkah/Ahjo-Vulkan/issues/214) (the NGX feature DLL must never be packed); [#144](https://github.com/pekkah/Ahjo-Vulkan/issues/144) (a broken native binary reached NuGet because nothing checked it; this is the packaging-side analogue).
**Date:** 2026-11-13

## Problem

Each native package declares the files it ships in a pack list inside a `Pack*Runtimes` target. Each target then turns that list into `None` items **conditioned on `Exists()`**. A declared file that is not on disk is dropped without a message, and `dotnet pack` exits 0:

| package | target | list | silent drop |
|---|---|---|---|
| `Ahjo.Vulkan.Slang.Native` | `PackSlangRuntimes` (`Ahjo.Vulkan.Slang.Native.csproj:422`) | `_SlangPackFile` (`:424-431`), 3 win-x64 + 2 linux-x64 | `:435-436`; licence `:442-443` |
| `Ahjo.Vulkan.Vma.Native` | `PackVmaRuntimes` (`Ahjo.Vulkan.Vma.Native.csproj:199`) | `_VmaPackRid` (`:201-204`), 4 RIDs | `:212-213` |
| `Ahjo.Vulkan.Ktx.Native` | `PackKtxRuntimes` (`Ahjo.Vulkan.Ktx.Native.csproj:202`) | `_KtxPackRid` (`:204-205`), 2 RIDs | `:209-210` |
| `Ahjo.Vulkan.Ngx.Native` | `PackNgxRuntimes` (`Ahjo.Vulkan.Ngx.Native.csproj:228`) | `_NgxPackRid` (`:230-231`), 2 RIDs | `:235-236`; licence `:242-243` |

The comment above each target calls this intentional: "Local pack only includes the host's RID" (for example `Ahjo.Vulkan.Vma.Native.csproj:196-197`). That suits a local experiment. For release it is a hole: nothing between "a per-RID artifact is missing or incomplete" and "`dotnet nuget push`" notices. The publish job's staging loops skip a missing artifact directory by design (`if [ -d "$src_dir" ]` at `publish.yml:167`, `:191`, `:214`, `:242`), and no step looks inside a built nupkg (`publish.yml:376-384` lists the artifacts and uploads them; that is all).

There is a second, quieter failure mode. All four targets hook `BeforeTargets="_GetPackageFiles;GenerateNuspec"`, and `_GetPackageFiles` is a **private** NuGet SDK target. If an SDK update renamed it, every `Pack*Runtimes` target would stop running. All four packages would then ship with no native files at all, still with exit 0. A check placed only inside those targets would stop running along with them.

## Evidence

### E1: measured on Slang, the package #169 is about

Packing `src/Ahjo.Vulkan.Slang.Native` in this worktree with `-p:SkipSlangNativeFetch=true` (the flag `publish.yml:324` uses):

| staged state | `dotnet pack` exit | `runtimes/` in nupkg |
|---|---|---|
| only `native/slang/staged/win-x64/` (no linux-x64 directory) | **0**, no warning | the 3 win-x64 files only; **the whole linux-x64 RID is absent** |
| win-x64 with `slang-glslang.dll` moved aside | **0**, no warning | `slang.dll`, `slang-compiler.dll`; **`slang-glslang.dll` absent** |

The second row is exactly the #166 OPEN-1 regression (Optimization silently a no-op for consumers), and it can be pushed to NuGet with a green run.

### E2: VMA, KTX and NGX have the same gap, read from the csprojs and not measured

Their targets use the identical `Condition="Exists(…)"` shape (table above). They were not packed here, because their native binaries are not staged in this worktree. The claim rests on the code being the same shape as the Slang target measured in E1, not on a run. Two extras:

- NGX's licence (`NGX-LICENSE.txt`, committed: `git ls-files native/ngx/NGX-LICENSE.txt`) and Slang's (`native/slang/SLANG-LICENSE.txt`, committed) are dropped by the same pattern. Shipping a third-party binary without its licence is a redistribution defect of the same class.
- NGX has the opposite obligation too: the feature DLL must **never** be packed (#214, `Ahjo.Vulkan.Ngx.Native.csproj:225-226`). Today nothing checks the nupkg for extra files either.

`Ahjo.Vulkan.Native`, `Ahjo.Vulkan`, `Ahjo.Vulkan.Slang` and `Ahjo.Vulkan.Ngx` declare no `Pack*Runtimes` target (`grep` across `src/*/*.csproj`). The class is exactly these four packages.

### E3: who packs, and where

The only `dotnet pack` invocations in the repo are the eight in `publish.yml` (`:269-374`, found by grep across `*.yml`, `*.md`, `*.ps1`, `*.py`, `*.sh`, excluding `docs/design/`). No skill or doc tells a developer to pack locally. `ci.yml` never packs.

`publish.yml` runs on `push: main`, `release: published` and `workflow_dispatch` (`:28-62`), so a pack defect is discovered after merge at the earliest. On `push: main`, that is the run that pushes a preview to NuGet.

### E4: the per-RID artifacts already exist on every PR run, except VMA's

`ci.yml` runs on `pull_request` (`:3-7`) and calls `build-ktx-native.yml` (`:470-472`), `build-slang-native.yml` (`:489-491`) and `build-ngx-native.yml` (`:514-516`). Each uploads `<pkg>-native-<rid>` artifacts (`build-ktx-native.yml:112-117`, `build-slang-native.yml:123-128`, `build-ngx-native.yml:153-158`), the same artifacts `publish.yml` downloads and stages. `build-vma-native.yml` is called only by `publish.yml` (`:84-87`; its header, `build-vma-native.yml:9`, says so). The `vma-linux` lane in `ci.yml:392` is a test lane and uploads nothing.

Cost of adding VMA to PRs: on the last successful `publish.yml` run on `main` (run 43405684961, 2026-11-13), the four `Build vma (<rid>)` jobs took 6–16 s each. Those were **cache hits** (`build-vma-native.yml:71-78` skips cmake on a hit). A cold build was not measured.

### E5: the mechanism, prototyped (on Windows only)

A scratch project outside the repo (`TreatWarningsAsErrors=true`) imported a shared `.targets` file shaped **exactly** as the plan's Step 1. It declared two runtime files and one licence. The verifier prototype is shaped as the plan's Step 4.

| state | result |
|---|---|
| one declared file missing, default | `dotnet pack` **exit 1**, `error AHJOPACK001: Probe.Native: runtimes\linux-x64\native\liba.so is in the pack list but its source … does not exist …` |
| same, `-p:AhjoAllowPartialNativePack=true` | exit 0, partial nupkg. The manifest `Probe.Native.native-files.txt` still lists **all 3** declared entries |
| verifier on that partial output | **exit 1**: `runtimes/linux-x64/native/liba.so is declared in the pack list but is not in Probe.Native.1.2.3.nupkg` |
| all present, `dotnet build -c Release` then `dotnet pack -c Release --no-build` (the shape `publish.yml:289-293` uses for VMA) | exit 0; manifest beside the nupkg; verifier exit 0 |
| verifier asked for an ID that was never packed | exit 1, `… (did its pack target run?)` |

Two pitfalls were measured, and the design avoids both:
- `-o out` sets `PackageOutputPath` to `out` with **no trailing slash**, so the manifest path uses `$([MSBuild]::EnsureTrailingSlash('$(PackageOutputPath)'))`.
- The manifest name carries **no version** (`<PackageId>.native-files.txt`). The repo's version comes from MinVer (`Directory.Packages.props:22`), and whether `$(PackageVersion)` is final by the time this target runs was not measured. The verifier finds the single `<PackageId>.<digit>….nupkg` itself, so MinVer timing cannot split the manifest from its nupkg.

The verifier was also run on the real E1 Slang nupkg (missing `slang-glslang.dll` and linux-x64) against a hand-written 6-line manifest. It exits 1 and names all three missing entries.

**Not measured:** every run above was on Windows, while both real callers (`publish.yml`'s publish job, and the new `native-pack` job) run on `ubuntu-latest`. The PR's own `native-pack` run is the first Linux execution. Packing the real VMA/KTX/NGX projects through the new target, and VMA packed *without* `--no-build` (the PR job's shape), are also first exercised there.

## Decision: strict-by-default pack lists, plus a post-pack nupkg check that runs on every PR and before every push

Two layers, both fed by **the same per-package list that already exists**, so neither can drift from what the package declares.

**Layer 1: pack refuses to produce an incomplete package (MSBuild).** A new shared file `tools/msbuild/Ahjo.Vulkan.NativePack.targets` is imported by the root `Directory.Build.targets`, next to `Ahjo.Vulkan.SlangShaders.targets` (`Directory.Build.targets:3`). It is inert unless a project sets `AhjoNativePackDependsOn`. Each of the four native csprojs keeps its own list (that is where the knowledge lives). Its `Pack*Runtimes` target now only **declares** `AhjoNativePackFile` items: source path plus full `PackagePath`, licence included. The shared target then does three things:
- **Error** `AHJOPACK001`, once, naming the package and **every** declared file that does not exist (its nupkg path and missing source), with nothing written. The missing items are collected first and reported by a single `<Error>`. A batched per-item `<Error>` stops at the first failing batch (measured during implementation: only the first of two missing files was named). `ContinueOnError="ErrorAndContinue"` names them all but still writes the nupkg and manifest (scratch probe). This is on by default. `-p:AhjoAllowPartialNativePack=true` is the explicit local opt-out.
- Error `AHJOPACK002` if the project declared no items at all.
- Turns the items into the `None`/`Pack` items, and writes `<PackageId>.native-files.txt` (the **full declared** list, unfiltered, forward slashes) beside the nupkg.

**Layer 2: the built nupkg is opened and compared (CI).** A new script, `tools/verify-native-nupkgs.py <artifacts-dir> <PackageId>...`, checks each named package:
- its manifest exists (no manifest means the pack target never ran, which covers the private-hook failure), and exactly one `<PackageId>.<version>.nupkg` sits beside it;
- every manifest line is an entry in the nupkg;
- every `runtimes/**` entry in the nupkg is in the manifest. The NGX feature DLL can never slip in.

The workflow names the package IDs, but the files come only from the csproj.

**Where it runs:**
- **`publish.yml`**, after the last pack and before upload and push. Strict pack is the default, so the existing pack steps need no flag. Then the verifier runs over every native package that was packed. This blocks the `push: main` preview and the release push.
- **`ci.yml`, on every PR**, in a new job `native-pack`. It runs on `ubuntu-latest` (same as the publish job) after the native lanes, downloads their artifacts, stages them, packs the four native packages into a temp directory and runs the verifier. It **runs no test suite** and provisions no Vulkan. It is a packaging check, not a lane that grows into wrapper coverage (`.github/CLAUDE.md`). To cover VMA on PRs, `ci.yml` also calls `build-vma-native.yml`, as a new job `vma-native`. This follows the "defined once, called by both" rule, and it is cheap on a cache hit (E4).
- **One staging definition.** The four inline "Stage … binaries for pack" loops move from `publish.yml` into `tools/stage-native-artifacts.sh <download-root> <vma|ktx|slang|ngx>`, and both workflows call it. The RID is derived from each downloaded `<pkg>-native-<rid>` directory name, so the script holds no RID list of its own.

The push stays in `publish.yml`: its own comment (`publish.yml:393-400`) records that trusted publishing breaks if it moves.

### Why not the alternatives

- **Post-pack check with its own hand-written expected-file list.** That would be a second copy of each pack list, and drifting from the lists is exactly what the user ruled out. The manifest is emitted from the list itself.
- **Layer 1 only (MSBuild error, no nupkg inspection).** It never looks at the artifact that ships. It also goes silent along with its own target if `_GetPackageFiles` (private) is renamed. Layer 2's "no manifest" failure is what catches that case.
- **Layer 2 only (keep the silent `Exists()` drop, verify afterwards).** It would catch the defect, but `dotnet pack` would still report success on a package it knows is incomplete. The error belongs where the knowledge is, and the message can name the missing source path, which the nupkg cannot.
- **Strict only when a CI property is set (opt-in).** E3 shows `publish.yml` is the only pack caller today. Opt-in means every future caller must remember the flag, and forgetting it reproduces this bug. Strict by default plus an explicit local opt-out fails safe. The "local pack includes only the host RID" behaviour survives behind the opt-out. Breaking the default local pack is acceptable (Ahjo is the only consumer, and no doc or skill tells anyone to pack locally, per E3).
- **Run the check only in `publish.yml`.** That catches the defect only after merge, on the run that pushes the preview. The artifacts for three of the four packages already exist on every PR (E4), so a PR-time job costs a download plus four packs.
- **Restructure `publish.yml` around a reusable pack workflow** that both workflows call. That is a larger change to the job that holds the OIDC exchange and the eight-pack ordering. A shared staging script plus the shared verifier gives the same single definition for the parts that can drift.
- **Make the staging script fail on a missing RID directory.** It would need its own RID list, which is a third copy. Layer 1 already fails, naming the file.

### What this does not cover

- **Whether the packed binaries work.** That is each native lane's smoke suite, and it is unchanged.
- **Version freshness** of staged files (#256).
- **The wrapper packages** (`Ahjo.Vulkan.Slang`, `Ahjo.Vulkan.Ngx`): they ship no native files (`publish.yml:333`; the NGX wrapper comment above `:371`).

## Invariants honored

- **AOT / trim:** MSBuild and CI only. No runtime code changes, and the AOT smoke is unaffected.
- **Generated code:** untouched.
- **`TreatWarningsAsErrors`:** the shared target emits errors, not warnings. The prototype built clean with the setting on.
- **CI reality / lane rules:** no new test lane, no Linux wrapper coverage, no tier declaration. `native-pack` runs no tests. `vma-native` is the existing reusable workflow, called from a second place as `.github/CLAUDE.md` prescribes for the other three.
