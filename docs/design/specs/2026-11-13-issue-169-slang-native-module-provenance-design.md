# Slang test guards: assert where the shipped native modules were loaded from

**Issue:** [#169](https://github.com/pekkah/Ahjo-Vulkan/issues/169): *Tests: the Vulkan SDK's slang-glslang.dll shadows ours on PATH, defeating the Optimization staging guard on Windows*
**Widens to:** the same shadowing of `slang.dll` and `slang-compiler.dll`, which this spec measured and which #169 does not mention (see Evidence E3).
**Lands consistently with:** [#166](https://github.com/pekkah/Ahjo-Vulkan/issues/166) OPEN-1 (`docs/design/specs/2026-08-01-issue-166-slang-support-design.md`: "ship `slang-glslang`"); [#256](https://github.com/pekkah/Ahjo-Vulkan/issues/256) (stale staged DLLs after a `SlangVersion` bump). This spec checks **where** a module was loaded from, not **which version** it is. #256 owns the version question (see "What this does not prove").
**Upstream:** searched shader-slang/slang issues for the `slang-glslang` PATH fallback / wrong-copy load (four queries, 2026-11-13). There was no relevant hit; the only result was an unrelated 2022 issue (#2116). The search order is Windows' and the shadowing copy is the SDK's own. Nothing upstream is broken, so there is nothing to wait for.
**Companion (same PR):** `2026-11-13-issue-169-native-pack-completeness-design.md` covers the packed half.
**Date:** 2026-11-13

## Problem

`Optimization_ChangesTheEmittedSpirv` (`tests/Ahjo.Vulkan.Slang.Tests/SlangCompilerTests.cs:300-313`) and the four `OptimizationLevels_ReachTheDownstreamCompiler` cases (`SlangCompilerTests.cs:245-276`) guard the staging of `slang-glslang`. Without that library, every `SlangOptimizationLevel` above `None` silently does nothing. Both tests observe Slang's *behaviour*: word counts, and the absence of `failed to load downstream compiler 'spirv-opt'`. Neither test checks *which binary* produced that behaviour.

On Windows, Slang loads `slang-glslang` by bare name. After the application directory, the loader's search reaches `PATH`. The Vulkan SDK installer puts `C:\VulkanSDK\<ver>\Bin` on `PATH`, and that directory ships its own `slang-glslang.dll`. When ours is missing, which is exactly the regression the guard exists to catch, the SDK's copy loads and the behaviour tests stay green. `src/Ahjo.Vulkan.Slang.Native/CLAUDE.md:38-40` documents this and defers the fix to #169.

The same search order applies to the other two win-x64 files the package ships (`slang.dll`, `slang-compiler.dll`; `src/Ahjo.Vulkan.Slang.Native/Ahjo.Vulkan.Slang.Native.csproj:137-153`). The SDK ships both of those as well (E3).

This is a **verification** defect, not a shipping one. A consumer's nupkg puts our copies in `runtimes/win-x64/native/`, so they land in the application directory, which the loader searches before `PATH` (`Ahjo.Vulkan.Slang.Native.csproj:424-438`). A wrong-version copy is reachable only when ours is absent.

## Evidence

All measurements were taken on 2026-11-13 in this worktree at the current pin `SlangVersion` = `v2026.22` (`Directory.Build.props:46`), win-x64, Debug build of `tests/Ahjo.Vulkan.Slang.Tests`. The machine has two SDKs installed. `C:\VulkanSDK\1.4.363.0\Bin` is on `PATH` (`VULKAN_SDK=C:\VulkanSDK\1.4.363.0`), and `C:\VulkanSDK\1.4.341.1` is installed but not on `PATH`.

### E1: the #169 shadowing still reproduces on the current pin, with both SDKs

The issue measured this on `v2026.14.1` with SDK 1.4.341.1. Re-run on `v2026.22`, filtering `-method "*Optimization*"` (5 tests):

| our `slang-glslang.dll` | SDK `Bin` on `PATH` | word counts None/Default/High/Maximal | diagnostic | result |
|---|---|---|---|---|
| present | 1.4.363.0 | 317 / 313 / 245 / 245 | none | 5/5 pass |
| **deleted** | 1.4.363.0 | 317 / 313 / 245 / 245 | none | **5/5 pass** |
| **deleted** | 1.4.341.1 | 317 / 313 / 245 / 245 | none | **5/5 pass** |
| **deleted** | none | 317 / 317 / 317 / 317 | `error[E00100]: failed to load downstream compiler 'spirv-opt'` / `note[E99996]: failed to load dynamic library 'slang-glslang'` | **4 of 5 fail** (`Default`/`High`/`Maximal` cases + `Optimization_ChangesTheEmittedSpirv`; `None` passes, correctly). Full suite in this state: 199 total, 4 failed. |

The word counts at the pin match the issue's `v2026.14.1` numbers exactly. On win-x64 at `v2026.22` the diagnostic names the library `'slang-glslang'` with no version. The text quoted at `SlangCompilerTests.cs:237-238` (`'slang-glslang-2026.14.1'`) carries a version. The neighbouring `Optimization_ChangesTheEmittedSpirv` remarks (`:292-293`) say its measurement was taken on linux-x64, and the linux-x64 file is versioned (`Ahjo.Vulkan.Slang.Native.csproj:94`), so it is most likely the linux spelling. The Windows spelling at `v2026.14.1` was not re-measured.

### E2: the copies differ, and size no longer tells them apart

| copy | bytes |
|---|---|
| ours, `native/slang/staged/win-x64/slang-glslang.dll` (v2026.22) | 6 262 784 |
| `C:\VulkanSDK\1.4.341.1\Bin\slang-glslang.dll` | 9 840 568 |
| `C:\VulkanSDK\1.4.363.0\Bin\slang-glslang.dll` | 5 954 488 |

The issue's table had the SDK copy as the bigger one. With SDK 1.4.363.0 it is the smaller one. Any check based on size or "looks like ours" would be guessing.

### E3: `slang.dll` and `slang-compiler.dll` are shadowable too, and one of them is invisible today

`C:\VulkanSDK\1.4.363.0\Bin` contains `slang.dll`, `slang-compiler.dll` and `slang-glslang.dll`, among others. The full `Ahjo.Vulkan.Slang.Tests` suite (199 tests) was run with each of our files deleted in turn, with SDK 1.4.363.0 on `PATH`:

| deleted from test output | what loaded instead | suite result |
|---|---|---|
| `slang-compiler.dll` | `C:\VulkanSDK\1.4.363.0\Bin\slang-compiler.DLL`, build tag `2026.17-25-gc7954ebd1` | 198/199. **Only** `Create_ExposesPinnedBuildTag` (`SlangCompilerTests.cs:20-28`) fails |
| `slang.dll` | `C:\VulkanSDK\1.4.363.0\Bin\slang.DLL` (76 216 bytes; ours is 159 744). It forwards to *our* `slang-compiler.dll`, so the tag stays `2026.22` | **199/199 pass**. Nothing notices. |

For `slang-compiler.dll`, the only guard is a coincidence: the SDK's Slang happens to be a different version from our pin. An SDK that bundles the same Slang release as the pin would turn that row green too.

A missing `slang.dll` is caught by nothing in this suite. On a consumer machine without the SDK it would fail with `DllNotFoundException` on the first call. That is the failure the forwarder note at `Ahjo.Vulkan.Slang.Native.csproj:130-135` warns about.

The `tests/Ahjo.Vulkan.Slang.Native.Tests` suite is fed the same three files by the same `None` items (`Ahjo.Vulkan.Slang.Native.csproj:137-153`). It has the same blind spot for `slang.dll`, and it has no optimizing compile at all.

### E4: the loaded module path is observable and discriminating, within a window

These results come from a scratch console app that references `src/Ahjo.Vulkan.Slang` and enumerates `Process.GetCurrentProcess().Modules` for names starting with `slang`. Each run did a `Maximal` compile of a redundant vertex shader and fetched `Spirv(0)`.

- **Our files present:** all three modules resolve to the application directory.
- **Our file deleted, SDK on `PATH`:** that module, and only that module, resolves to `C:\VulkanSDK\<ver>\Bin\…`.
- **`slang-glslang.dll` deleted, no SDK on `PATH`:** that module is not in the process at all.
- **Lifetime:** `slang-glslang.dll` is loaded lazily, during code generation. In the wrapper that is `SlangProgram.Spirv` → `getEntryPointCode` (`src/Ahjo.Vulkan.Slang/SlangProgram.cs:187-200`), not `SlangCompiler.Create` or `SlangSession.Compile`. It stays loaded after the program and the session are disposed. It is **unloaded when the `SlangCompiler` is disposed**, because that is when the global session is released (`src/Ahjo.Vulkan.Slang/SlangCompiler.cs:203-212`).

So a provenance assertion has to run after `Spirv()` at an optimizing level and before the compiler's `Dispose`. The existing helper `CompileRedundantVertexWordCount` (`SlangCompilerTests.cs:315-326`) disposes its compiler before returning, so the check cannot hang off it.

Windows' documented DLL search order resolves a bare-name load against a module already loaded with the same base name, wherever that module came from. If so, at most one `slang-glslang.dll` is in the process at a time, and parallel tests holding their own compilers only keep that same module loaded longer. That is **documented behaviour, not measured here**. Every probe in this spec ran a single compiler.

Module names come back with OS casing (`slang.DLL`, `slang-compiler.DLL`, `slang-glslang.dll`), so names must be compared case-insensitively.

### E5: CI is not affected today; developer machines are

`ci.yml:69-121` provisions only the Silk.NET Khronos loader and the SwiftShader ICD into `build/vulkan/win-x64` and prepends that directory to `PATH` (`ci.yml:119-121`). It installs no Vulkan SDK, so no `slang*.dll` reaches `PATH` from the workflow. The contents of the hosted runner image were not audited, so treat "CI is honest today" as the workflow's statement, not a measured fact.

The machines where #169 bites are developer machines with the Vulkan SDK installed. Those are also the only machines where this suite's driver-gated test gets a real driver (`tests/CLAUDE.md`, tier `none` in CI).

### E6: the prescribed test, prototyped end to end

The exact shape in the plan's Step 1 was compiled in a scratch xUnit project outside the repo. It used the gate, the two `Assert.True` messages, the `string?` → non-nullable flow through `Assert.True(path is not null, …)`, and the lookup helpers. The project referenced the worktree's `src/Ahjo.Vulkan.Slang`, `xunit.v3` 4.0.3 (`Directory.Packages.props:15`), `TreatWarningsAsErrors=true`, `AnalysisLevel=latest` and `Nullable=enable` (`Directory.Build.props:4-10`). It **builds clean**, with the xUnit analyzers and nullable flow both satisfied. Run alone:

| state | result |
|---|---|
| all three present, SDK on `PATH` | pass; three paths under the output directory |
| `slang-glslang.dll` aside, SDK on `PATH` | fail: `slang-glslang.dll was loaded from C:\VulkanSDK\1.4.363.0\Bin\slang-glslang.dll, not from the application directory …` |
| `slang-compiler.dll` aside, SDK on `PATH` | fail: `… loaded from C:\VulkanSDK\1.4.363.0\Bin\slang-compiler.DLL …` |
| `slang.dll` aside, SDK on `PATH` | fail: `… loaded from C:\VulkanSDK\1.4.363.0\Bin\slang.DLL …` |
| `slang-glslang.dll` aside, no SDK on `PATH` | fail: `slang-glslang.dll is not loaded after a Maximal compile …` |

The prototype was **not** run inside the real 199-test suite, so behaviour under xUnit's parallel class execution is still unmeasured (plan, risk notes).

## Decision: assert the loaded path of every shipped win-x64 Slang module

Add one test to `Ahjo.Vulkan.Slang.Tests`. It runs an optimizing compile, fetches the SPIR-V, and then, while the `SlangCompiler` is still alive, requires each of `slang.dll`, `slang-compiler.dll` and `slang-glslang.dll` to meet two conditions:

1. it is **loaded in the process**, and
2. it was loaded **from the application directory** (`AppContext.BaseDirectory`).

The lookup is by name: `kernel32!GetModuleHandleW` + `GetModuleFileNameW`, declared with source-generated `[LibraryImport]` (AOT- and trim-clean, no reflection). The first implementation enumerated `System.Diagnostics.Process.GetCurrentProcess().Modules`, as originally designed. Inside the real parallel suite it failed 2 of 10 full runs with `Win32Exception: The operation completed successfully` from `NtProcessManager.GetModules`. That is `EnumProcessModules` racing other test classes loading and unloading `slang-glslang`. So the pre-agreed fallback below was taken (implementation, 2026-11-13). After the switch, 12 of 12 full Debug runs passed, and the five negative cases still fail as designed. The test makes no assumption about `PATH`, because it checks the fact the behaviour tests depend on directly. On a machine with or without an SDK, deleting any of the three files makes it fail. The failure message names the file and the directory it actually came from, or says the file was never loaded.

The behaviour tests stay as they are. They answer a different question ("does `Optimization` change the output?"), and they are still the guard on Linux. Their remarks gain a pointer to the provenance test so nobody reads a green run of them on Windows as staging proof.

The test is **win-x64 only**, gated with `TestGate.RequirePlatform(OperatingSystem.IsWindows(), …)` (a `[gate:platform]` skip, never a bare `Assert.Skip`, per `tests/CLAUDE.md`). The defect is a property of the Windows `LoadLibrary` search order. The linux-x64 equivalents (`libslang.so`, `libslang-glslang-<ver>.so`) were not measured here. `tests/CLAUDE.md` and issue #32 establish that the wrapper suite has no Linux CI lane, so a Linux branch would be code that nothing ever runs. Linux developer runs report a classified skip, which is the honest result.

### Why not the alternatives

- **Assert the staged set exists in `Ahjo.Vulkan.Slang.Native.Tests` (the issue's second option).** A file being present proves it was copied, not that Slang asks for it by that name or loads it. The name Slang requests is upstream's choice, and it already differs by platform (versioned on Linux, `Ahjo.Vulkan.Slang.Native.csproj:93-94`; bare `'slang-glslang'` on win-x64 at the pin, E1). If that name ever stops matching the file we ship, an existence check stays green, while the provenance test fails because the module never loads. It would also guard a different directory from the one the behaviour tests run in. `_SlangStageNeeded` already checks existence in `native/slang/staged/`; the gap is the test output.
- **Restrict the test process's DLL search path** (`SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_APPLICATION_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32)` from a module initializer). This changes how every DLL in the process loads. CI deliberately reaches `vulkan-1.dll` through `PATH` (`ci.yml:119-121`) for the driver-gated `Spirv_FeedsCreateShaderModule` (`SlangShaderModuleTests.cs:15`). It also depends on running before the first Slang load. And it turns the failure into an indirect symptom (a word count, or `DllNotFoundException`) instead of naming the file. This option was **not measured**; it is rejected on those structural grounds.
- **Scrub `PATH` in CI or the test runner.** CI already has no SDK on `PATH` (E5), so CI gains nothing. Developer machines, the only place the defect occurs, do not go through the workflow. Inside an in-process xUnit v3 runner, `PATH` cannot be scrubbed early enough for every way of launching the suite (`dotnet test`, IDE, running the `.exe` directly).
- **Compare size or hash of the loaded module.** E2 shows size does not discriminate. A hash duplicates the archive SHA-256 pin, and #256 tracks stale-copy detection anyway. Location is the property that actually separates "ours" from "someone else's".
- **Fold the check into `Optimization_ChangesTheEmittedSpirv`.** That test's helper disposes the compiler before returning (E4), and its subject is the optimizer's effect. A separate test keeps "did we load our binaries" and "does the setting do anything" as two distinct failures, each with its own message.
- **P/Invoke `GetModuleHandleW` + `GetModuleFileNameW` instead of `Process.Modules`.** That would work (`AllowUnsafeBlocks` is already on, `Directory.Build.props:9`), but it adds a hand-written native declaration to a test that the BCL already covers. It stays available as the fallback if `Process.Modules` ever proves flaky (see the plan's risk notes). **Taken:** `Process.Modules` flaked under the parallel suite (see the Decision above), so this is now the implementation.

### What this does not prove

- **Version.** A stale `slang-glslang.dll` in the output directory, left over from before a `SlangVersion` bump, passes this test. That is [#256](https://github.com/pekkah/Ahjo-Vulkan/issues/256), whose version-stamp fix is the right layer for it. `Create_ExposesPinnedBuildTag` still covers `slang-compiler.dll`'s version.
- **The nupkg.** The test guards the copy-to-output items (`Ahjo.Vulkan.Slang.Native.csproj:137-153`), not the pack list (`_SlangPackFile`, `:424-438`). Those are separate item lists. The **packed** half of #169 is a separate decision in the same PR: `2026-11-13-issue-169-native-pack-completeness-design.md` makes pack fail on a missing declared file and verifies the built nupkg on every PR and before every push, for all four native packages.
- **`Ahjo.Vulkan.Slang.Native.Tests`.** That suite is fed by the same items, so a staging regression shows up in both outputs and this one guard catches it. The native suite's own lane (`build-slang-native.yml`) provisions no SDK.

## Invariants honored

- **Native AOT / trim:** nothing changes under `src/`. The `[LibraryImport]` declarations are source-generated (no runtime marshalling stubs, no reflection), and the test project is not AOT-published.
- **Zero per-frame allocation:** not applicable. This is test-only code, and `src/Ahjo.Vulkan.Slang` has no per-frame path (`tests/CLAUDE.md`). No benchmark is needed.
- **Generated code:** untouched.
- **`TreatWarningsAsErrors`:** the prescribed test shape compiles clean under the repo settings and xUnit analyzers (E6). No suppressions.
- **CI reality:** the test runs in the existing `build-test` Windows step (`ci.yml:182-183`). There is no new lane and no Linux wrapper coverage.
