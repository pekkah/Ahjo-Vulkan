Paired with `../specs/2026-09-04-issue-196-framering-descriptor-pool-sentinel-design.md`.

# Plan — issue #196: make `descriptorMaxSets` `FrameRing`'s descriptor-pool switch

**Scope.** Two files of production code (`Pools/FrameRing.cs`,
`Rendering/FrameContext.cs`) and one test file (`tests/…/FrameRingTests.cs`).
**No behaviour change** — every input that succeeds today succeeds identically;
every input that throws today throws the same exception type with the same
`ParamName`. **`Pools/DescriptorSetPool.cs` is not touched: zero lines.**

**Constraints this plan operates under** (spec §Decision, §What this does not change):

- `FrameRing`'s constructor is setup-time; `FrameRing` itself is on the per-frame
  path (`FrameRingBenchmarks`, `docs/benchmarks.md:152`). **No per-frame
  allocation and no new per-frame branch.** Step 2 changes a *constructor-time*
  branch condition only; `BeginFrame` → `Slot.WaitAndReset` →
  `DescriptorSets?.Reset()` (`FrameRing.cs:349-366`) is untouched.
- Native AOT clean: no reflection, no dynamic codegen, no new public type.
- `TreatWarningsAsErrors=true`: no suppressions anywhere in this diff.
- **Public API surface, but the signature does not change** — same parameters,
  order, types and defaults (`FrameRing.cs:47-53`). Source- and
  binary-compatible for all 32 `new FrameRing(...)` sites; the only call sites
  edited are the guard tests in `FrameRingTests.cs`.
- **Do not touch `DescriptorSetPool.cs` at all** — not the retry logic
  (`:316-330`), not the guards, not the docs. #187 is being worked on a separate
  branch and a separate PR, and its plan step 5 rewrites the `poolSizes`
  `<param>` paragraph at `:121-138`
  (`docs/design/plans/2026-09-04-issue-187-descriptor-pool-retry-rollback.md:145-159`)
  — the one paragraph a cross-reference from here would have landed in. Spec
  OPEN-2 records why the cross-reference is deferred instead. With that, the two
  PRs share no file.
- Wrapper tests are Windows-only (#32). All new tests are ordinary
  `TestGate.RequireDriver()` xUnit cases in the existing suite; none require a
  Linux lane, a second driver, or a software ICD.

---

## 1. `src/Ahjo.Vulkan/Pools/FrameRing.cs` — constructor guards (`:57-69`)

**Delete** the #191 pinning comment at `:57-62` in full (the six lines beginning
`// "Pass both or neither" is FrameRing's OWN contract` and ending `— do not
"harmonize" the two.`). It defends a sentinel that step 2 removes.

**Replace it** with a comment stating the switch, then keep both guards with
**unchanged conditions** and new messages:

```csharp
// descriptorMaxSets is this ring's descriptor-pool switch: 0 means "no
// per-slot pools". Zero is never a legal maxSets for a real pool
// (VUID-VkDescriptorPoolCreateInfo-descriptorPoolOverallocation-09227; the
// wrapper never enables the NV overallocation flag, and DescriptorSetPool
// rejects maxSets == 0 outright), so the value cannot collide with any other
// meaning. descriptorPoolSizes is only the template — its emptiness decides
// nothing here. The two guards keep the pair consistent.
if (!descriptorPoolSizes.IsEmpty && descriptorMaxSets == 0)
    throw new ArgumentOutOfRangeException(nameof(descriptorMaxSets),
        "descriptorMaxSets must be > 0 when descriptorPoolSizes is non-empty — it is the " +
        "switch that gives every slot its own DescriptorSetPool.");
if (descriptorPoolSizes.IsEmpty && descriptorMaxSets != 0)
    throw new ArgumentException(
        "descriptorPoolSizes must be non-empty when descriptorMaxSets is > 0. An empty template is " +
        "legal at DescriptorSetPool — a pool with no per-type budget (issue #191) — but it can serve " +
        "only descriptor set layouts with zero bindings, which is not a per-frame workload, and a " +
        "misuse from a ring slot chains a leaked sub-pool per failed Acquire per frame on a driver " +
        "that enforces per-type pool accounting (issue #187). Pass a template sized for the " +
        "descriptors this ring's layouts declare, or construct DescriptorSetPool directly.",
        nameof(descriptorPoolSizes));
```

Exception types and `ParamName`s are deliberately identical to today's
(`ArgumentOutOfRangeException` / `nameof(descriptorMaxSets)`;
`ArgumentException` / `nameof(descriptorPoolSizes)`). Only the message text and
the surrounding comment change.

## 2. `src/Ahjo.Vulkan/Pools/FrameRing.cs` — the `Slot` constructor branch (`:258-266`)

This is the substance of the change.

**Delete** the second #191 pinning comment at `:258-263` in full (the six lines
beginning `// FrameRing's own opt-out sentinel:` and ending `matching comment on
the constructor's argument guards.`).

**Replace the branch condition** — `descriptorPoolSizes.IsEmpty` becomes
`descriptorMaxSets == 0`:

```csharp
// descriptorMaxSets is the switch (see the ring constructor's guards): 0
// means this slot gets no descriptor pool. The template's emptiness is not
// consulted — the constructor has already guaranteed it is non-empty
// whenever we reach the `new`, so DescriptorSetPool's empty-poolSizes state
// (issue #191, a pool with no per-type budget) is unreachable from FrameRing
// by construction rather than by convention.
descSets  = descriptorMaxSets == 0
    ? null
    : new DescriptorSetPool(device, descriptorMaxSets, descriptorPoolSizes);
```

Nothing else in `Slot` moves: the `try`/`finally` rollback (`:277-294`),
`WaitAndReset`'s `DescriptorSets?.Reset()` (`:365`), and `Dispose`'s
`DescriptorSets?.Dispose()` (`:436`) all stay exactly as they are.

## 3. `src/Ahjo.Vulkan/Pools/FrameRing.cs` — XML docs

**3a. Class summary (`:11-13`).** Change

> `and — when the ring is configured with descriptor-pool sizes — a per-slot <see cref="DescriptorSetPool"/> reset alongside the command pool.`

to say `and — when the ring is constructed with a non-zero
<c>descriptorMaxSets</c> — a per-slot <see cref="DescriptorSetPool"/> reset
alongside the command pool.`

**3b. Constructor summary (`:37-46`).** Rewrite the second sentence so the
switch, not the template, is the subject. Required content, in this order:

1. `descriptorMaxSets` is the switch: pass a non-zero value to give every slot
   its own `DescriptorSetPool`; leave it 0 (the default) for a ring with no
   descriptor pools, in which case `FrameContext.DescriptorSets` is `null`.
2. `descriptorPoolSizes` is the per-slot template and must be non-empty whenever
   the switch is on — one sentence, pointing at the `<exception>` below for why.
3. Keep the existing sentence about the pool being reset alongside the command
   pool, so sets from `FrameContext.DescriptorSets` are valid for exactly one
   frame.

**3c. Add `<exception>` docs to the constructor** (it has none today):

- `<exception cref="ArgumentOutOfRangeException">` — `framesInFlight` is 0, or
  `descriptorPoolSizes` is non-empty while `descriptorMaxSets` is 0.
- `<exception cref="ArgumentException">` — `descriptorMaxSets` is non-zero while
  `descriptorPoolSizes` is empty. One clause naming the reason: a budget-less
  pool (legal at `DescriptorSetPool` since #191) serves only zero-binding
  layouts and is not a per-frame shape; construct `DescriptorSetPool` directly
  if that is genuinely wanted.

The `<see cref="FrameRing(Device,uint,uint,ulong,ReadOnlySpan{VkDescriptorPoolSize},uint)"/>`
cref at `:24` is unaffected — the signature does not change (spec D5).

## 4. `src/Ahjo.Vulkan/Rendering/FrameContext.cs` — `DescriptorSets` doc (`:38-46`)

Change the opening clause

> `Per-slot descriptor-set pool, or <c>null</c> when the ring was constructed without descriptor-pool sizes.`

to

> `Per-slot descriptor-set pool, or <c>null</c> when the ring was constructed with <c>descriptorMaxSets: 0</c> (the default).`

The rest of the paragraph — `BeginFrame` calls `Reset` on rotation, so a
`DescriptorSet` acquired here is valid for exactly one frame — is unchanged.

## 5. `src/Ahjo.Vulkan/Pools/DescriptorSetPool.cs` — not touched

Deliberately empty step, kept numbered so the omission is visible rather than
forgotten. An earlier draft added one sentence to the `poolSizes` `<param>`
(`:121-138`) noting that `FrameRing` never passes an empty template. **#187's
plan rewrites that same paragraph** (its step 5), so the sentence is a
guaranteed conflict on the one paragraph both issues care about. It is deferred
— see spec OPEN-2. The implementer changes **zero lines** of this file.

## 6. Tests — `tests/Ahjo.Vulkan.Tests/FrameRingTests.cs`

All cases follow the file's existing shape: `TestGate.RequireDriver()`,
`Instance.Create(default)`, `CreateGraphicsDevice(instance, out uint family)`.
None needs `RequireHardwareDriver` (no submits).

**6a. Rename and tighten `DescriptorSets_Default_NullWhenNotConfigured`
(`:130-143`)** → `DescriptorSets_Null_When_MaxSets_Is_Zero`. Body unchanged
(`Assert.Null(frame.DescriptorSets)`), plus a second ring in the same test
constructed with `descriptorPoolSizes: default, descriptorMaxSets: 0` written
explicitly, asserting `Assert.Null` again — pinning that the *switch*, not the
template, is what produced `null`.

**6b. Replace `DescriptorSets_Mismatched_Args_Throw` (`:144-163`)** with two
tests that assert `ParamName`, not just the type (spec Risk 4 — today's
assertions would pass even if the guards swapped, because
`ArgumentOutOfRangeException` derives from `ArgumentException`):

- `DescriptorSets_Template_Without_MaxSets_Throws` — `descriptorPoolSizes: sizes,
  descriptorMaxSets: 0`. Assert `ArgumentOutOfRangeException` and
  `ex.ParamName == "descriptorMaxSets"`.
- `DescriptorSets_MaxSets_Without_Template_Throws` — `descriptorPoolSizes:
  default, descriptorMaxSets: 4`. Assert `ArgumentException`, `ex.ParamName ==
  "descriptorPoolSizes"`, and `Assert.IsType<ArgumentException>(ex)` so a
  derived type (e.g. a future `ArgumentOutOfRangeException`) fails the test
  rather than passing by inheritance.

**6c. New: `DescriptorSets_EmptyArray_Rejected_Like_Default`.** Same as 6b's
second case but passing `Array.Empty<VkDescriptorPoolSize>()` instead of
`default`. Pins that the guard tests `IsEmpty`, not the span's null-ref state —
spec §E5 measured that `[]`/`default` and `Array.Empty<T>()` differ under
`Unsafe.IsNullRef`, and this test makes it impossible for a future "clever"
refactor to reintroduce that distinction silently. Assert the same
`ArgumentException` + `ParamName`.

**6d. New: `DescriptorSets_Pool_Created_When_MaxSets_NonZero`.** Ring with a
one-entry `UNIFORM_BUFFER` template and `descriptorMaxSets: 4`; assert
`frame.DescriptorSets` is not `null` and that `PoolCount == 1` /
`AllocatedCount == 0` on a fresh slot. This is the positive half of the switch;
today no test asserts the pool's *existence* independently of the reset/per-slot
behaviour.

**6e. Unchanged:** `DescriptorSets_Pool_ResetsBetweenFrames` (`:165-224`) and
`DescriptorSets_Pool_IsPerSlot` (`:225-249`) must pass **with no edits**. That is
the regression proof for spec D3 — if either needs touching, stop and report.

**6f. Unchanged:** every test in `tests/Ahjo.Vulkan.Tests/DescriptorSetPoolTests.cs`,
including the four `new DescriptorSetPool(device, maxSets, [])` cases at `:246,
:282, :323, :354`. #191's behaviour is not in scope; if any of them changes,
something in step 5 went beyond documentation.

## 7. Benchmarks

`FrameRing` is a hot path (`docs/benchmarks.md:152`,
`tests/Ahjo.Vulkan.Benchmarks/FrameRingBenchmarks.cs`). The diff touches only a
constructor-time branch, so the expectation is **no measurable change**:

```bash
dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter "*FrameRing*"
```

- Expected: `Frame_Begin_Submit_Wait` `Allocated` stays `-`, mean within the
  run-to-run noise band recorded in `docs/benchmarks.md:241` (driver-dependent
  row).
- **No `docs/benchmarks.md` edit is expected.** If the mean moves outside noise
  or `Allocated` becomes non-`-`, stop and report rather than updating the table
  — this change cannot legitimately do either.
- `FrameRingBenchmarks.cs:43` constructs the ring with no descriptor arguments,
  so it exercises the `descriptorMaxSets == 0` side of the new branch. No new
  benchmark is warranted: the descriptor-pool side of `FrameRing` has no
  benchmark today and this change does not add a hot-path cost that would need
  one (`DescriptorSetPoolBenchmarks` already covers `Acquire`/`Release`/`Reset`).

## 8. Docs

- No `README.md`, `src/Ahjo.Vulkan/README.md` or `src/Ahjo.Vulkan/CLAUDE.md`
  edits: neither mentions `descriptorPoolSizes`, and the only `FrameRing`
  reference in the scoped CLAUDE.md is the `Pools/**` bullet at `:36`.
- No `docs/benchmarks.md` edit (step 7).
- No new design doc. This plan and its spec are the record; #191's spec §E11.5
  stays as written (it is a historical record of the collision, not a live
  contract).

## 9. Verification

```bash
dotnet build Ahjo.Vulkan.slnx
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~FrameRing"
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~DescriptorSetPool"
dotnet test
```

Then, before the PR (per `src/Ahjo.Vulkan/CLAUDE.md:53` — the diff touches
`Pools/`):

- `vulkan-validation-reviewer` on the diff.
- `bench-coverage-checker` on the diff (expected finding: constructor-time only,
  existing `FrameRing` coverage sufficient).

Commit style: `Pools: make descriptorMaxSets FrameRing's descriptor-pool switch
(#196)`. PR body references `Closes #196` and links this plan and its spec.

---

## Deliberately open

- **OPEN-2 (step 5)** — resolved in this plan as "omit": `DescriptorSetPool.cs`
  is not touched, because #187's plan rewrites the paragraph the cross-reference
  would have joined. If the maintainer instead wants the sentence in this PR,
  they say so at approval and the implementer adds it to `:121-138` as spec
  OPEN-2 describes — and expects a conflict with the #187 branch. Nothing else
  in the plan depends on the answer.
- **OPEN-1 / OPEN-3 (spec)** — sequencing against #187, and whether #196 closes
  as "capability refused" with no follow-up filed. Both are maintainer calls made
  at approval time, not during implementation; the implementer needs no answer to
  either to execute steps 1–4 and 6–9.
- **If any step reveals that a `FrameRing` consumer outside this repo depends on
  `descriptorPoolSizes.IsEmpty` selecting the no-pool path** — i.e. someone
  passing `descriptorMaxSets` non-zero with an empty template and expecting
  `null` — stop and report. Spec §E2 found no such caller in `src/`, `samples/`
  or `tests/`, and today that combination throws, so the case should not exist;
  but it is the one way this "no behaviour change" claim could be wrong.
