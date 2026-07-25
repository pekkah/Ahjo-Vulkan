# Issue #117 — Generate VkResult success-code policy from vk.xml — Design

Status: design + tier-1 implemented
Date: 2026-06-12
Issue: https://github.com/pekkah/ahjo-vulkan/issues/117
Resolves the structural cause of: #97 (`ThrowIfFailed` treats `VK_INCOMPLETE` as failure)
Builds on: the existing vk.xml codegen path (StructExtendsGen, issue #29/#94/#95).

## 1. Goal

Stop enforcing the "`ThrowIfFailed` is only for single-success-code APIs"
contract by doc-comment convention. Derive it from `vk.xml`, which carries
`successcodes`/`errorcodes` per command, so a build artifact — not a human
reading the spec at the right moment — decides which entry points have
multi-code success sets.

The #97 bug class (throwing on `VK_INCOMPLETE` across enumerate / get-data /
surface-query sites) happened because nothing machine-checked which entry
points have more than one success code. `ResultExtensions.ThrowIfFailed`
throws on anything other than `VK_SUCCESS`; on a two-call `count → fill`
command the second call can legally return `VK_INCOMPLETE` (the set grew
between the two calls), and the wrapper turned that spec-defined success into
a `VulkanException`.

The codegen infrastructure already reads `vk.xml` (StructExtendsGen consumes
`structextends`), so this is an extension of an existing pattern, not new
machinery.

## 2. Tiers

The issue sketches three tiers, cheapest first. **Tier 1 alone kills the bug
class** and is what this change implements; tiers 2–3 are recorded here as the
intended evolution.

1. **Generated guard test (implemented).** A generator emits the set of
   multi-success-code commands from `vk.xml`; a unit test scans the wrapper
   sources for `vk(...).ThrowIfFailed()` and fails, naming any call site on a
   multi-success command. No runtime change to the success path of correct
   call sites.
2. **Generated policy table (future).** Promote the generated data from
   test-only to a shipped `internal static class ResultPolicy` mapping entry
   point → allowed success set, consumed by debug-only asserts in
   `ResultExtensions` (`[Conditional("DEBUG")]`, zero release-build cost). The
   tier-1 generator already emits the full name → success-set mapping, so this
   is a relocation + a debug assert, not new extraction.
3. **Shared two-call helper (future / partially in place).** A shared retry
   loop for the `count → fill → VK_INCOMPLETE` idiom so each site stops
   hand-rolling it. Tier 1 lands the building block — `ThrowIfErrored` (throw
   only on an error `VkResult`) — and uses an explicit retry loop where
   truncation matters (`PipelineCache.Save`). A fully generic helper is
   deferred because the call sites own heterogeneous buffers (stackalloc,
   `ArrayPool`, fixed pointers) that resist a single signature.

## 3. The success/error partition

Vulkan encodes the outcome class in the **sign** of `VkResult`: every error is
negative, every success/partial outcome (`VK_SUCCESS` = 0, `VK_NOT_READY` = 1,
`VK_TIMEOUT` = 2, `VK_EVENT_SET` = 3, `VK_INCOMPLETE` = 5,
`VK_SUBOPTIMAL_KHR`, …) is non-negative. This is the spec's own partition and
is the basis for the new guard helper.

Two predicates now exist on `VkResult`:

| Helper | Throws when | Use for |
|---|---|---|
| `ThrowIfFailed()` | `result != VK_SUCCESS` | commands whose only success code is `VK_SUCCESS` |
| `ThrowIfErrored()` | `(int)result < 0` | multi-success commands; caller branches on the returned partial codes |

`ThrowIfErrored` keeps the same zero-allocation success path and cold
`NoInlining` throw helper as `ThrowIfFailed`.

## 4. Components

### 4.1 Generator — `tools/Ahjo.Vulkan.ResultPolicyGen`

Off-tree console app (mirrors `Ahjo.Vulkan.StructExtendsGen`). Reads `vk.xml`,
collects every `<command>` whose `successcodes` attribute lists more than one
code, resolves `<command name="X" alias="Y"/>` aliases to their target's
success set, and emits a committed C# table:

```
tests/Ahjo.Vulkan.Tests/Generated/ResultPolicyData.g.cs
  internal static class ResultPolicyData
  {
      public static readonly IReadOnlyDictionary<string, string[]> MultiSuccessCommands = …;
  }
```

The map is `command name → full success-code set` (not just the name set) so
tier 2 can reuse it for the policy asserts without a re-extraction. Output is
sorted by command name for a stable diff.

### 4.2 Regenerate target

`RegenerateResultPolicy` in `Ahjo.Vulkan.Native.csproj`, alongside `Regenerate`
and `RegenerateChains`. Not wired into `Build` — regeneration is a deliberate
step after a Vulkan-Headers bump. Depends on `CopyGeneratedHeaders` (stages
`vk.xml`), runs the generator, writes the committed table.

### 4.3 Guard test — `ResultPolicyGuardTests`

Locates the wrapper source root via `[CallerFilePath]`, strips comments
(offset-preserving, so reported line numbers stay accurate), and for every
`vk…(` call attributes the statement it opens (up to the next `;`) to that
command. If the command is in `MultiSuccessCommands`, the statement contains
`.ThrowIfFailed(`, and the command is not allowlisted, it is a violation.

Three supporting tests keep the mechanism honest:

- `Allowlist_OnlyContainsMultiSuccessCommands` — no stale/typo allowlist keys.
- `Allowlist_HasNoDeadEntries` — every allowlist entry still matches a real
  suppressed call site (so a removed site forces removal of its suppression).
- `GeneratedTable_IsPopulatedAndCoversKnownCommands` — a regen that emptied the
  table can't make the scan pass vacuously.

### 4.4 Allowlist

A small, justified suppression set in the test (matching the repo's
"justified suppression with a comment" philosophy). Two entries today:
`vkCreateGraphicsPipelines` and `vkCreateComputePipelines`. These are
multi-success in `vk.xml` (`VK_SUCCESS, VK_PIPELINE_COMPILE_REQUIRED`) but
`VK_PIPELINE_COMPILE_REQUIRED` is only returned when
`VK_PIPELINE_CREATE_FAIL_ON_PIPELINE_COMPILE_REQUIRED_BIT` is set in
`flags`; neither pipeline builder ever sets `flags` (defaults to 0), so
`VK_SUCCESS` is the only reachable success code and the plain `ThrowIfFailed`
is correct. Each entry's justification is reviewed against a live call site by
`Allowlist_HasNoDeadEntries`.

## 5. Call-site fixes (the #97 sites)

Landing the guard test green requires the existing violations — exactly the
#97 sites — to be fixed first. All are `count → fill` or single get-data
queries:

| Site | Command | Fix |
|---|---|---|
| `Instance.PickPhysicalDevice` / `PopulateCacheAndFind` | `vkEnumeratePhysicalDevices` | `ThrowIfErrored` on both calls |
| `Instance.PickPhysicalDevice` | `vkEnumerateDeviceExtensionProperties` | `ThrowIfErrored` |
| `Instance.EnsureInstanceLayerPresent` | `vkEnumerateInstanceLayerProperties` | `ThrowIfErrored` |
| `Instance.EnsureInstanceExtensionPresent` | `vkEnumerateInstanceExtensionProperties` | `ThrowIfErrored` |
| `Swapchain.LoadImagesAndViews` | `vkGetSwapchainImagesKHR` | `ThrowIfErrored` |
| `Swapchain.NegotiateFormat` | `vkGetPhysicalDeviceSurfaceFormatsKHR` | `ThrowIfErrored` |
| `Swapchain.NegotiatePresentMode` | `vkGetPhysicalDeviceSurfacePresentModesKHR` | `ThrowIfErrored` |
| `PipelineCache.Save` | `vkGetPipelineCacheData` | retry loop + `ThrowIfErrored` |

For the enumerate/surface sites, `VK_INCOMPLETE` means "you got the count the
fill call reported; more may exist" — accepting the partial set is correct
(picking a device / negotiating a format still works on a subset, and the
race is display hot-plug / installer mid-enumeration). For `PipelineCache.Save`
the partial buffer would be persisted and poison the next run's cache load, so
that site re-queries the now-larger size in a loop instead of writing a
truncated blob — the standard Vulkan two-call retry idiom, which `Save`'s own
doc comment (per-thread caches + `Merge`) makes a realistic trigger.

## 6. Out of scope / non-goals

- **Tiers 2 and 3** beyond the building blocks above (§2).
- **Native AOT**: the generated table and guard live in the test project, not
  on any wrapper code path, so invariant #2 is untouched. `ThrowIfErrored` is
  plain branch-on-sign — no reflection, no dynamic code.
- **Hot-path allocation**: `ThrowIfErrored`'s success path is allocation-free
  (covered by `ResultPolicyTests`); `PipelineCache.Save` is setup-time, not
  per-frame.
- **A code-fix analyzer** that rewrites offending sites. The guard test fails
  the build with an actionable message; auto-fix is not warranted.

## 7. Risks

- **Indirect call sites.** The scanner attributes `.ThrowIfFailed(` to the
  `vk…(` call in the same statement. A two-statement form
  (`var r = vkFoo(...); … r.ThrowIfFailed();`) would not be attributed. No such
  form exists in the wrapper today (all multi-success uses are the chained
  form); if one is introduced it would be a gap, not a false pass on the
  chained sites. Noted for tier 2, which moves the check to runtime asserts and
  closes it.
- **Headers bump.** New multi-success commands enter the table on regen; if a
  future wrapper call uses one with `ThrowIfFailed`, the guard fails the build
  — which is the intended behavior.
