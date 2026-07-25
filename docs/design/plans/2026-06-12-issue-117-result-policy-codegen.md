# Issue #117 — Generate VkResult success-code policy from vk.xml — Implementation Plan

**Goal:** Derive the `ThrowIfFailed` single-success contract from `vk.xml`
instead of a doc-comment convention. Tier 1: a generator emits the set of
multi-success commands, a guard test fails the build on any
`vk(...).ThrowIfFailed()` against one, and the existing #97 violations are
fixed so the test lands green.

**Architecture:** Mirror the StructExtendsGen codegen path — an off-tree
console generator reading `vk.xml`, run by a `Regenerate*` MSBuild target,
emitting a committed `*.g.cs`. The table lives in the test project and is
consumed only by the guard test. A new `VkResult.ThrowIfErrored()` (throw only
on a negative/error result) is the fix primitive for the count→fill idiom.

**Tech stack:** .NET 10, C# 14, `System.Xml.Linq` (generator), xUnit v3 +
`[GeneratedRegex]` (guard test). No Vulkan driver needed — the guard test is
pure source scanning.

**Spec:** `docs/design/specs/2026-06-12-issue-117-result-policy-codegen-design.md`.
Read it before starting.

---

## File map

| Path | Kind | Purpose |
|---|---|---|
| `tools/Ahjo.Vulkan.ResultPolicyGen/Ahjo.Vulkan.ResultPolicyGen.csproj` | new | Off-tree generator project. |
| `tools/Ahjo.Vulkan.ResultPolicyGen/Program.cs` | new | Reads `vk.xml`, emits the multi-success table (name → success-set), resolves aliases. |
| `src/Ahjo.Vulkan.Native/Ahjo.Vulkan.Native.csproj` | edit | Add `RegenerateResultPolicy` target. |
| `tests/Ahjo.Vulkan.Tests/Generated/ResultPolicyData.g.cs` | generated | Committed `MultiSuccessCommands` table. |
| `src/Ahjo.Vulkan/Internal/ResultExtensions.cs` | edit | Add `ThrowIfErrored`; cross-reference the guard in `ThrowIfFailed`'s doc. |
| `src/Ahjo.Vulkan/Lifecycle/Instance.cs` | edit | 10 enumerate sites → `ThrowIfErrored`. |
| `src/Ahjo.Vulkan/Rendering/Swapchain.cs` | edit | 6 swapchain/surface sites → `ThrowIfErrored`. |
| `src/Ahjo.Vulkan/Pipelines/PipelineCache.cs` | edit | `Save` retry loop + `ThrowIfErrored`. |
| `tests/Ahjo.Vulkan.Tests/ResultPolicyGuardTests.cs` | new | Source-scanning guard + allowlist hygiene + table sanity. |
| `tests/Ahjo.Vulkan.Tests/ResultPolicyTests.cs` | edit | Behavioral tests for `ThrowIfErrored`. |

---

## Tasks

- [x] **1. Generator.** New `ResultPolicyGen` console project + `Program.cs`.
  Parse `<command>` elements, collect `successcodes` with length > 1, resolve
  aliases, emit a sorted committed table into the test project.
- [x] **2. Regenerate target.** `RegenerateResultPolicy` in the Native csproj,
  depending on `CopyGeneratedHeaders`, writing the table. Not wired into Build.
- [x] **3. Generate the table.** Run the target/tool to produce
  `ResultPolicyData.g.cs`.
- [x] **4. `ThrowIfErrored`.** Add the negative-result guard to
  `ResultExtensions`, with the zero-alloc success path and the cold throw
  helper; update `ThrowIfFailed`'s doc to point at the guard test.
- [x] **5. Fix #97 sites.** `Instance` (enumerate), `Swapchain`
  (images/formats/present-modes), `PipelineCache.Save` (retry loop).
- [x] **6. Guard test.** `ResultPolicyGuardTests`: scan, allowlist, hygiene,
  table-sanity facts.
- [x] **7. Behavioral tests.** `ThrowIfErrored` pass-through / throw / zero-alloc
  in `ResultPolicyTests`.
- [x] **8. Verify.** Build wrapper + tests clean (warnings-as-errors); run
  `ResultPolicyTests` + `ResultPolicyGuardTests` green; confirm the scanner
  fails on an injected violation (negative test) then reverts.

## Verification

- `dotnet build src/Ahjo.Vulkan` and the test project build clean under
  `TreatWarningsAsErrors`.
- `dotnet test --filter ResultPolicyTests|ResultPolicyGuardTests` — all green.
- Injected `vkEnumeratePhysicalDevices(...).ThrowIfFailed()` makes
  `NoMultiSuccessCommand_UsesPlainThrowIfFailed` fail naming the site
  (confirmed, then reverted).
- Driver-gated integration tests (`Instance`, `Swapchain`, `PipelineCache`) are
  unchanged in shape and run on Windows CI; the success path of the fixed sites
  is identical for `VK_SUCCESS`, only the `VK_INCOMPLETE` branch differs.

## Notes for future tiers

- **Tier 2:** relocate the generated map to a shipped `internal static class
  ResultPolicy` and add `[Conditional("DEBUG")]` asserts in `ResultExtensions`
  that the observed code is in the command's allowed set. The generator already
  emits the full success-set, so this is a move + assert.
- **Tier 3:** a shared `count → fill` retry helper once a signature that
  accommodates stackalloc / `ArrayPool` / fixed buffers without allocating is
  settled.
