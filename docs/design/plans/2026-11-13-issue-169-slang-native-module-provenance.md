Paired with [../specs/2026-11-13-issue-169-slang-native-module-provenance-design.md](../specs/2026-11-13-issue-169-slang-native-module-provenance-design.md). Read it first; this plan only says *how*.

# Implementation plan: issue #169, Slang native module provenance test

- **Branch:** `issue-169-slang-glslang-path-shadowing`, from `main`.
- **Scope (this plan):** `tests/Ahjo.Vulkan.Slang.Tests/` + docs. **Nothing under `src/`** changes in this plan. The companion pack plan, in the same PR, edits the four native csprojs' pack targets and CI. No `Generated/`, no csproj staging logic, no workflow edits.
- **PR:** one PR for both #169 plans (this one, then `2026-11-13-issue-169-native-pack-completeness.md`). Title `Tests + Packaging: prove the shipped native files are the ones loaded and packed`, body `Closes #169`. Name the widening to `slang.dll` / `slang-compiler.dll` (spec E3) and to the VMA/KTX/NGX packages (pack spec).

## Step 1: New test class `SlangShippedModuleTests`

New file `tests/Ahjo.Vulkan.Slang.Tests/SlangShippedModuleTests.cs`, namespace `Ahjo.Vulkan.Slang.Tests`, `public sealed class SlangShippedModuleTests`. It is a separate class, not part of `SlangCompilerTests`, because its subject is deployment rather than the compiler API.

Members:

```csharp
// The win-x64 shipped set. Must match the None items in
// src/Ahjo.Vulkan.Slang.Native/Ahjo.Vulkan.Slang.Native.csproj (win-x64 ItemGroup)
// and _SlangPackFile there.
private static readonly string[] WindowsShippedModules = ["slang.dll", "slang-compiler.dll", "slang-glslang.dll"];

[Fact]
public void ShippedNativeModules_LoadFromTheApplicationDirectory();

private static string? FindLoadedModulePath(string fileName);
private static bool IsInApplicationDirectory(string modulePath);
```

`ShippedNativeModules_LoadFromTheApplicationDirectory`, in this order:

1. `TestGate.RequirePlatform(OperatingSystem.IsWindows(), "Module provenance is asserted for the win-x64 shipped set; the linux-x64 load path is unmeasured (#169).");`
2. Open `using var compiler = SlangCompiler.Create();`, then `using SlangSession session = compiler.CreateSession(new SlangSessionDescription { Optimization = SlangOptimizationLevel.Maximal });`, then `using SlangProgram program = session.Compile(new SlangCompileRequest { ModuleName = "provenance", Source = ShaderFixtures.RedundantVertex });`. Use **`using` declarations** so that everything stays alive until the method returns. Spec E4: `slang-glslang` unloads when the compiler is disposed.
3. Call `program.Spirv(0)` and assert the result is non-empty. This call is what loads `slang-glslang` (spec E4, `SlangProgram.cs:187-200`). It **must** come before any lookup. Do not put the lookup and the `Spirv` call inside one interpolated string: an interpolated string evaluates its holes left to right, so the lookup would run first.
4. For each name in `WindowsShippedModules`:
   - `string? path = FindLoadedModulePath(name);`
   - `Assert.True(path is not null, …)` with this message:
     `$"{name} is not loaded after a Maximal compile. Slang loads it by exactly this name, so either it is missing from the test output with no other copy reachable, or Slang now asks for a different file name than the one we ship (src/Ahjo.Vulkan.Slang.Native/Ahjo.Vulkan.Slang.Native.csproj)."`
   - `Assert.True(IsInApplicationDirectory(path), …)` with this message:
     `$"{name} was loaded from {path}, not from the application directory {AppContext.BaseDirectory}. Ours is missing from the test output and the loader found another copy on PATH; a Vulkan SDK's Bin ships all three. Check the shipped set in src/Ahjo.Vulkan.Slang.Native/Ahjo.Vulkan.Slang.Native.csproj (#169)."`
   - Write `$"{name} -> {path}"` to `ITestOutputHelper`. Take it through the constructor, the same way `SlangCompilerTests.cs:16-18` does, so a passing run still shows the evidence.
   - Assert each module in turn; the first failure stops the test, which is acceptable because the message names the file.

`FindLoadedModulePath(string fileName)`: `using Process self = Process.GetCurrentProcess();`, iterate `self.Modules` as `ProcessModule`, and return `module.FileName` for the first entry where `string.Equals(module.ModuleName, fileName, StringComparison.OrdinalIgnoreCase)`. Otherwise return `null`. Names come back as `slang.DLL` (spec E4), so the comparison has to be case-insensitive.
**Superseded during implementation:** this enumeration flaked in the parallel suite (2/10 full runs, `Win32Exception` from `EnumProcessModules`), so the risk notes' fallback replaced it. `FindLoadedModulePath` now calls `GetModuleHandleW(fileName)` (null when it returns 0) and then `GetModuleFileNameW` into a 32 767-char buffer. Both are source-generated `[LibraryImport]` from `kernel32.dll` on a `partial` class. The loader matches base names case-insensitively itself.

`IsInApplicationDirectory(string modulePath)`: compares **directory equality**, not a prefix match:
`Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetDirectoryName(modulePath)!))` against `Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory))`, using `StringComparison.OrdinalIgnoreCase`. The test only runs on Windows, so the comparison is Windows-only too. A prefix match would wrongly accept a subdirectory of the output.

XML docs on the class and the test must cover these points, citing the spec: why this is a location check and not a behaviour check; that a green `Optimization` test on a machine with the SDK proves nothing about staging; the lifetime window from E4; and that version is out of scope (#256).

No reflection. The class must not be `unsafe`. The original "no P/Invoke" rule was lifted when the fallback was taken (above), and the only P/Invokes are the two source-generated `kernel32` imports.

## Step 2: Point the behaviour guards at the provenance test

`tests/Ahjo.Vulkan.Slang.Tests/SlangCompilerTests.cs`. The assertions do not change.

- `Optimization_ChangesTheEmittedSpirv` remarks (`:278-299`): add one `<para>`. It should say that on Windows the loader falls back to `PATH`, a Vulkan SDK there ships its own `slang-glslang.dll`, and so this test can pass with ours deleted. `SlangShippedModuleTests.ShippedNativeModules_LoadFromTheApplicationDirectory` is the staging guard. This test checks only that the setting has an effect.
- `OptimizationLevels_ReachTheDownstreamCompiler` remarks (`:229-244`): add one sentence saying that on win-x64 at `v2026.22` the second line reads `failed to load dynamic library 'slang-glslang'`, with no version (spec E1). Do **not** call the quoted versioned text "the linux spelling" as fact; the spec records that as likely, not measured. Also add the same pointer to the provenance test.
- Do **not** edit the `Assert.True` message at `:308-311`. It already points at the shipped set.

## Step 3: Tests (verification of the new test itself)

Run on a Windows machine with a Vulkan SDK `Bin` on `PATH`. If the implementer's machine has none, stop and say so. That configuration is the reason this change exists, and a run without it proves nothing new.

Build once: `dotnet build tests/Ahjo.Vulkan.Slang.Tests -c Debug`. Then run `tests/Ahjo.Vulkan.Slang.Tests/bin/Debug/net11.0/Ahjo.Vulkan.Slang.Tests.exe -class Ahjo.Vulkan.Slang.Tests.SlangShippedModuleTests -showliveoutput` in each of these states, restoring every file afterwards.

| # | state | expected |
|---|---|---|
| 1 | all three present, SDK on `PATH` | pass; output shows three paths under the output dir |
| 2 | `slang-glslang.dll` moved aside, SDK on `PATH` | **fail**, "loaded from C:\VulkanSDK\…\Bin\slang-glslang.dll" |
| 3 | `slang-glslang.dll` moved aside, SDK removed from `PATH` for the run | **fail**, "is not loaded after a Maximal compile" |
| 4 | `slang-compiler.dll` moved aside, SDK on `PATH` | **fail**, "loaded from …\Bin\slang-compiler.DLL" |
| 5 | `slang.dll` moved aside, SDK on `PATH` | **fail**, "loaded from …\Bin\slang.DLL"; this is the case nothing caught before (spec E3) |

Then run the full suite in state 1 (`dotnet test --project tests/Ahjo.Vulkan.Slang.Tests -c Release`) and confirm the total grows by exactly one and nothing is skipped on Windows.

Quote rows 1–5 (one line each, including the path from the failure message) in the PR description. A local run is the only evidence this test can fail, because CI has no SDK (spec E5).

**Restore check:** before committing, `ls tests/Ahjo.Vulkan.Slang.Tests/bin/*/net11.0/slang*.dll` must list all three files, and no `*.aside` may remain.

## Step 4: Docs

1. `src/Ahjo.Vulkan.Slang.Native/CLAUDE.md:38-40`. Rewrite the "Reproducing the 'it can still fail' check…" paragraph and the paragraph after it:
   - Current figures: ours 6 262 784 at `v2026.22`; SDK 1.4.341.1 9 840 568; SDK 1.4.363.0 5 954 488. Say outright that size does not tell the copies apart.
   - All three win-x64 files are shadowable. A missing `slang.dll` was previously invisible (199/199 green), and a missing `slang-compiler.dll` was caught only by the build-tag mismatch.
   - Replace "Making the guard say so itself is #169" with: `SlangShippedModuleTests.ShippedNativeModules_LoadFromTheApplicationDirectory` is the guard. It fails on any machine, SDK or not, and the `Optimization` tests are behaviour checks only.
   - Keep the "costs nothing at run time for a consumer" reasoning. Replace the "check the staged set directly" advice with a pointer to the pack check (`AHJOPACK001` + `tools/verify-native-nupkgs.py`, from the companion plan).
   - At `:32`, label the `6 173 184 raw` figure `(v2026.14.1)`. It is the historical OPEN-1 measurement, not the current size.
2. `tests/CLAUDE.md`, the `Ahjo.Vulkan.Slang.Tests/` table row: append `, and that the three shipped win-x64 Slang modules are loaded from the test output directory rather than a Vulkan SDK on PATH (#169)`.
3. Run `python tools/sync-codex.py`, then `python tools/sync-codex.py --check`. Both `CLAUDE.md` edits have `AGENTS.md` mirrors (`src/Ahjo.Vulkan.Slang.Native/AGENTS.md`, `tests/AGENTS.md`). Commit the regenerated mirrors in the same commit.

There is no benchmark step. The change is test-only, and `src/Ahjo.Vulkan.Slang` has no per-frame path (`tests/CLAUDE.md`).

## Step 5: Verify invariants

- For this plan's commit(s), the diff touches only `tests/Ahjo.Vulkan.Slang.Tests/SlangShippedModuleTests.cs` (new), `tests/Ahjo.Vulkan.Slang.Tests/SlangCompilerTests.cs`, the two `CLAUDE.md` files, their two `AGENTS.md` mirrors, and the #169 specs and plans. The companion plan lists its own files.
- A Release build is warning-free (`TreatWarningsAsErrors`). No `#pragma`, and no `<NoWarn>` added.
- No `src/` change, so the AOT smoke is unaffected. No need to run it.
- Building on Windows rewrites `native/slang/SLANG-LICENSE.txt` with CRLF line endings. Staging copies it out of the archive, and the diff is line-ending churn only. Seen during this design: `git checkout -- native/slang/SLANG-LICENSE.txt` before committing. Do not commit it.

## Risk notes

- **`Process.Modules` racing with parallel tests.** xUnit v3 runs test classes in parallel. Other classes create and dispose `SlangCompiler`s, which loads and unloads `slang-glslang` while this test enumerates. This test holds its own compiler, so the module it asserts on cannot unload mid-test. `Process.Modules` on Windows enumerates via `EnumProcessModules`. Whether the BCL retries when the module list changes mid-enumeration was **not verified** for this design, and the prototype (spec E6) ran alone. If the test ever flakes with a `Win32Exception` from `Modules`, do **not** add a retry loop or collection-disable attribute on your own. Report it. The pre-agreed fallback is to replace `FindLoadedModulePath` with a `[LibraryImport]` of `kernel32!GetModuleHandleW` + `GetModuleFileNameW` (spec, "why not the alternatives"). That is a lookup by name with no enumeration.
- **8.3 short names / junctions in the output path.** `GetFullPath` does not expand short names. If a developer's checkout path makes `AppContext.BaseDirectory` and the loader-reported path differ only in short-vs-long form, the test would fail falsely. This was not observed: the scratch run under a deep `%TEMP%` path matched. Report it if seen; don't paper over it.

## OPEN items

None. The former OPEN-1 (the packed half of #169) was decided by the user: **widen**. It is designed in `../specs/2026-11-13-issue-169-native-pack-completeness-design.md` and planned in `2026-11-13-issue-169-native-pack-completeness.md`, and it ships in this PR.
