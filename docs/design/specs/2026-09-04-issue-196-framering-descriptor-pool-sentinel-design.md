# Issue #196 — `FrameRing`'s descriptor-pool switch is `descriptorMaxSets`, not an empty span

Paired plan: `../plans/2026-09-04-issue-196-framering-descriptor-pool-sentinel.md`

Follow-up to #191, which relaxed `DescriptorSetPool`'s `poolSizes` guard and
documented the resulting collision in its spec §E11.5 rather than resolving it
(`docs/design/specs/2026-08-03-issue-191-empty-descriptor-set-layout-design.md:402-426`).
This is the issue that decides it.

---

## Problem

An empty `ReadOnlySpan<VkDescriptorPoolSize>` means two different things at two
adjacent layers of the same subsystem.

**At `DescriptorSetPool`** (since #191, commit `4811ab8`), an empty `poolSizes`
means *"a pool with no per-type budget"* — legal Vulkan (`poolSizeCount = 0`),
and the correct pool for a zero-binding descriptor set layout. The contract is
written out at `src/Ahjo.Vulkan/Pools/DescriptorSetPool.cs:121-138`, the
constructor computes `_maxPerTypeDescriptorTotal = 0` for it deliberately
(`:170-181`), `CreatePool` emits `poolSizeCount = 0, pPoolSizes = null`
(`:424-445`), and `ThrowVariableCountExceedsBudget` has a dedicated message
branch for it (`:513-518`).

**At `FrameRing`**, an empty `descriptorPoolSizes` is an *opt-out sentinel*:
this slot gets **no descriptor pool at all**.

```csharp
// src/Ahjo.Vulkan/Pools/FrameRing.cs:264-266
descSets  = descriptorPoolSizes.IsEmpty
    ? null
    : new DescriptorSetPool(device, descriptorMaxSets, descriptorPoolSizes);
```

And the constructor actively rejects the one spelling that would express
"give every slot a budget-less pool":

```csharp
// src/Ahjo.Vulkan/Pools/FrameRing.cs:66-69
if (descriptorPoolSizes.IsEmpty && descriptorMaxSets != 0)
    throw new ArgumentException(
        "descriptorMaxSets is set but descriptorPoolSizes is empty — pass both or neither.",
        nameof(descriptorPoolSizes));
```

Nothing is broken. `FrameRing` branches on its sentinel before the relaxed
`DescriptorSetPool` guard is reachable, so no shipped behaviour depends on the
disagreement. #191 pinned both sites with comments (`FrameRing.cs:57-62` and
`:258-263`) that say, in as many words, *"do not harmonize the two"* — a holding
position, taken because redefining a sentinel on a per-frame path did not belong
in that PR.

The cost of the holding position is that two files in the same directory now
teach opposite lessons about the same value, and every future reader of either
must hold both. That is what this issue is for.

---

## Evidence

### E1 — the collision, at today's line numbers

The issue text cites `FrameRing.cs:57-63` and `:252-254`; the file has moved
since. Current anchors:

| what | where |
|---|---|
| class doc: *"when the ring is configured with descriptor-pool sizes"* | `FrameRing.cs:11-13` |
| ctor doc: *"Pass `descriptorPoolSizes` + a non-zero `descriptorMaxSets`"* | `FrameRing.cs:40-45` |
| ctor signature (both params optional, span defaulted) | `FrameRing.cs:47-53` |
| #191 pinning comment (*"do not 'harmonize' the two"*) | `FrameRing.cs:57-62` |
| guard **G1**: non-empty sizes + `maxSets == 0` → `ArgumentOutOfRangeException` | `FrameRing.cs:63-65` |
| guard **G2**: empty sizes + `maxSets != 0` → `ArgumentException` | `FrameRing.cs:66-69` |
| `Slot` ctor forwarding | `FrameRing.cs:79`, `:230-235` |
| #191 pinning comment, second site | `FrameRing.cs:258-263` |
| **the sentinel branch** | `FrameRing.cs:264-266` |
| `FrameContext.DescriptorSets` doc: *"when the ring was constructed without descriptor-pool sizes"* | `Rendering/FrameContext.cs:38-46` |

### E2 — consumer audit: `new FrameRing(...)`

32 construction sites repo-wide. **Four** pass descriptor arguments, all in one
test file; **zero** in `samples/`, **zero** in `tests/Ahjo.Vulkan.Benchmarks/`.

| site | descriptor args |
|---|---|
| `tests/Ahjo.Vulkan.Tests/FrameRingTests.cs:156-158` | `sizes`, `descriptorMaxSets: 0` — asserts G1 throws |
| `tests/Ahjo.Vulkan.Tests/FrameRingTests.cs:160-162` | `default`, `descriptorMaxSets: 4` — asserts G2 throws |
| `tests/Ahjo.Vulkan.Tests/FrameRingTests.cs:179-180` | `sizes`, `descriptorMaxSets: 8` |
| `tests/Ahjo.Vulkan.Tests/FrameRingTests.cs:237-238` | `sizes`, `descriptorMaxSets: 4` |
| the other 28 (`samples/HelloCube:323`, `HelloDlaa:325`, `HelloTriangle:91`, `HelloVmaWindowed:255`, `FrameRingBenchmarks.cs:43`, `DeviceLossTests.cs:141`, `SwapchainTests.cs:279`, `WindowedValidationTests.cs:66`, and 20 more in `FrameRingTests.cs`) | none — three positional args, both descriptor params defaulted |

Two of the four are the guards' own tests. **Two** call sites in the entire
repository actually build a ring with per-slot descriptor pools, and both are
tests. There is no sample and no benchmark exercising the feature at all.

### E3 — consumer audit: `new DescriptorSetPool(...)`

24 sites today, up from the 18 that #191's spec counted (`git grep -c` at
`4811ab8^` gives 18; #191's own plan added 4 empty-template tests, and #197 plus
the push-descriptor benchmark added 2 more). The split:

| location | sites | template |
|---|---|---|
| `src/Ahjo.Vulkan/Pools/FrameRing.cs:266` | 1 | whatever the ring was handed — never empty, because G2 rejects it |
| `tests/Ahjo.Vulkan.Tests/DescriptorSetPoolTests.cs` | 12 | 8 literal templates, **4 literal `[]`** (`:246, :282, :323, :354`) |
| `tests/Ahjo.Vulkan.Tests/DescriptorSetPoolVariableCountTests.cs` | 4 | literal templates |
| `tests/Ahjo.Vulkan.Benchmarks/` (`BindDescriptorSets:110`, `DescriptorSetPool:76`, `DescriptorSetPoolVariableCount:96`, `PushDescriptors:130`) | 4 | literal templates |
| `DescriptorTemplateTests:122`, `DescriptorWriteTests:193`, `PipelineLayoutTests:272` | 3 | literal templates |
| `samples/` | **0** | — |

The issue's "only `src/` caller, 18 sites repo-wide" claim is confirmed as to the
first half and stale as to the second: it is 24 now, and the four empty templates
all live in `DescriptorSetPoolTests`, i.e. the budget-less pool is exercised only
through the direct constructor, never through `FrameRing`.

### E4 — the sentinel is the only one of its kind in the wrapper

Surveying every `.IsEmpty` in `src/Ahjo.Vulkan/` outside `Generated/` (39 hits),
they fall into three shapes:

1. **Reject** — `PhysicalDevice.cs:762-763` (`Queues` must be non-empty),
   `Device.cs:352-353` (SPIR-V blob), `DescriptorTemplate.cs:160`.
2. **No-op early return** — `CommandRecorder.cs:248, 372, 451, 1199, 1503, 1664,
   1723, 1757, 1790, 2065, 2116, 2140`, `DescriptorSetExtensions.cs:30`,
   `QueryPool.cs:145, 182`, `PipelineCache.cs:123`.
3. **Substitute a default / skip an optional argument** —
   `GraphicsPipelineBuilder.cs:668` (empty dynamic states ⇒ viewport+scissor),
   `CommandRecorder.cs:266, 397`, `Instance.cs:445`.

`FrameRing.cs:264-266` is in none of them. It is the **only** `.IsEmpty` in the
wrapper that decides *whether a resource object is created at all* — the only one
whose answer is a different object graph rather than a different argument. It is
the odd one out by construction, not just by #191's accident.

### E5 — a nullable / "was it passed?" span spelling is not available

`ReadOnlySpan<T>` cannot be `null`, and the obvious workaround — distinguish
`default` from a genuinely empty span via `Unsafe.IsNullRef` — is
caller-dependent in a way no API can document. Measured on this repo's target
framework (`net10.0`, `LangVersion` preview; scratch console app, `dotnet run -c
Release`):

```
default:                 IsEmpty=True Length=0 nullRef=True
[] literal:              IsEmpty=True Length=0 nullRef=True
ReadOnlySpan<int>.Empty: IsEmpty=True Length=0 nullRef=True
new int[0]:              IsEmpty=True Length=0 nullRef=False
Array.Empty<int>():      IsEmpty=True Length=0 nullRef=False
arr.AsSpan(1, 0):        IsEmpty=True Length=0 nullRef=False
```

So `[]` and `Array.Empty<VkDescriptorPoolSize>()` — which every C# programmer
reads as the same value — land on opposite sides of a null-ref test. Any design
that reads meaning into "null span vs. empty span" is a design whose behaviour
depends on which of two identical-looking literals the caller typed. That rules
out the nullable-span option in the issue's question 3 on evidence, not taste.

An optional parameter also cannot carry a non-`default` span default: `= default`
is the only legal default for `ReadOnlySpan<T>`, which is why the current
signature spells it that way (`FrameRing.cs:52`).

### E6 — `descriptorMaxSets == 0` *is* an unambiguous switch

Unlike an empty template, zero is not a legal value for anything downstream:

- `VUID-VkDescriptorPoolCreateInfo-descriptorPoolOverallocation-09227`
  (`native/downloaded/Vulkan-Headers-vulkan-sdk-1.4.341.0/registry/validusage.json`):
  *"If the `descriptorPoolOverallocation` feature is not enabled, or flags does
  not have `VK_DESCRIPTOR_POOL_CREATE_ALLOW_OVERALLOCATION_SETS_BIT_NV` set,
  `maxSets` must be greater than 0."*
- The wrapper never sets that NV bit: `_poolFlags` is either
  `UPDATE_AFTER_BIND_BIT` or `0`, and there is no raw-flags parameter
  (`DescriptorSetPool.cs:196-199`).
- `DescriptorSetPool` therefore rejects `maxSets == 0` unconditionally at `:157`
  (`ArgumentOutOfRangeException.ThrowIfZero`).

So within this wrapper a `descriptorMaxSets` of 0 can never describe a real pool,
at either layer, ever. It is a sentinel that cannot collide with a meaning —
exactly the property the empty span lost in #191.

### E7 — the capability being argued over is worth approximately nothing

A budget-less pool serves *only* layouts with zero bindings
(`DescriptorSetPool.cs:126-132`). For a `FrameRing` slot that would mean: this
slot allocates zero-binding descriptor sets every frame and no other kind. Two
findings:

1. **The zero-binding *set* is rarely needed at all.** #191's motivating shape is
   the sparse-set hole — a program binding sets 0 and 2 needs a *layout* handle
   at index 1 for `vkCreatePipelineLayout`. It does not need a *set*:
   `CommandRecorder.BindDescriptorSets` takes an arbitrary `firstSet`
   (`Recording/CommandRecorder.cs:241-247`), so binding 0 and 2 is two calls, not
   one call with a filler in the middle. Nothing in `src/`, `samples/` or
   `tests/` allocates a set against a zero-binding layout on a per-frame path.
2. **When it *is* needed, a one-entry template is functionally identical.** A
   pool built from `[{UNIFORM_BUFFER, 1}]` allocates exactly the same
   zero-binding sets, up to the same `maxSets`, with the same auto-grow
   behaviour — zero-binding sets consume no per-type budget, so the budget is
   simply never touched. The only observable difference is
   `_maxPerTypeDescriptorTotal` (1 vs. 0), which feeds one guard
   (`DescriptorSetPool.cs:295`) on the `Acquire(layout, variableCount)` overload
   — and a zero-binding layout has no variable-count binding, so that overload is
   never the one in play.

The delta between "capability present" and "capability absent" is therefore one
`VkDescriptorPoolSize` of pool memory per slot.

### E8 — allowing it would amplify #187 from a setup-time wart to a per-frame one

`DescriptorSetPool.Reset` resets every chained sub-pool but never removes one
(`DescriptorSetPool.cs:394-407`); only `Dispose` destroys them (`:409-422`). The
auto-grow retry chains a fresh sub-pool on exhaustion and does not roll it back
when the retry also fails (`:316-330`) — that is #187, still open.

`DescriptorSetPool`'s own docs already say what a *misused* budget-less pool does
on a driver that enforces per-type accounting: it fails with
`VK_ERROR_OUT_OF_POOL_MEMORY` *"having chained one wasted sub-pool per failed
call"* (`:132-137`). At the direct-constructor layer that is a setup-time or
scene-time misuse. Hang the same pool off a `FrameRing` slot and the identical
misuse leaks one `VkDescriptorPool` **per failed `Acquire` per frame**, and —
because `Reset` walks `_pools` — adds one `vkResetDescriptorPool` call to every
subsequent `BeginFrame`. A per-frame path that gets monotonically slower is a
worse failure mode than a setup-time one, and it is unreachable on this repo's
only driver (#187: RTX 4070 Ti enforces `maxSets` only), so no test here would
catch it.

### E9 — #191's "explicit spelling is ceremony" reasoning does **not** transfer

#191 rejected an opt-in flag for `CreateDescriptorSetLayout` because *the one
call site with data-dependent emptiness would have to set the flag
unconditionally*: `SlangReflection.cs:177-186` writes
`Bindings = bindings.MapBindings()` and cannot know in advance whether the result
is empty
(`docs/design/specs/2026-08-03-issue-191-empty-descriptor-set-layout-design.md:479-487`).

Checked against `FrameRing`: **no call site has data-dependent
`descriptorPoolSizes`.** All four sites that pass the parameter (E2) pass a
literal array declared three lines above; the other 28 pass nothing. There is no
caller that computes a template whose emptiness it cannot predict, so #191's
objection has no target here. The reasoning does not apply — which is why this
spec is free to move the switch, and #191 was not.

---

## Decision

**`descriptorMaxSets` becomes `FrameRing`'s descriptor-pool switch.
`descriptorPoolSizes.IsEmpty` stops meaning anything at the `FrameRing` layer.
The budget-less per-slot pool stays refused — deliberately, with a stated reason,
not as a side effect of a sentinel.**

Answering the issue's three questions in order:

| Q | answer |
|---|---|
| **Q1** — should `FrameRing` be able to express "budget-less pool per slot"? | **No.** E7: the capability is worth one `VkDescriptorPoolSize` of pool memory, has no call site anywhere in the repo, and its only workload (per-frame zero-binding sets) does not exist. E8: allowing it moves #187's un-rolled-back sub-pool from a setup-time wart to an unbounded per-frame leak on drivers we cannot test. A caller who genuinely wants one constructs `DescriptorSetPool` directly — nothing in `DescriptorSetPool` changes. |
| **Q2** — if yes, what spells it? | Moot, but recorded: it would be `descriptorPoolSizes: [], descriptorMaxSets: N`, and under D1–D2 that is exactly one guard-deletion away (see *How to reverse this*). |
| **Q3** — should the opt-out sentinel become explicit? | **Yes — but onto an existing parameter, not a new type.** The switch moves to `descriptorMaxSets == 0`, which E6 shows cannot collide with any meaning at any layer. No `FrameRingDescriptorMode` enum, no nullable span (E5 shows it is not expressible), no second constructor. |

| # | decision |
|---|---|
| **D1** | The `Slot` constructor branches on `descriptorMaxSets == 0`, not on `descriptorPoolSizes.IsEmpty` (`FrameRing.cs:264-266`). This is the whole substance: after it, exactly one expression in the type decides whether a pool exists, and it is not an emptiness test. |
| **D2** | Both argument guards stay, with **identical trigger conditions** and rewritten messages. G1 (`:63-65`) keeps `ArgumentOutOfRangeException(nameof(descriptorMaxSets))`; G2 (`:66-69`) keeps `ArgumentException(nameof(descriptorPoolSizes))` but its message stops saying *"pass both or neither"* and starts saying *why* a budget-less ring pool is refused, naming #191 and #187. |
| **D3** | **No behaviour change.** Every input that succeeds today succeeds identically; every input that throws today throws the same exception type with the same `ParamName`. G2 is what makes D1 safe: it guarantees `descriptorPoolSizes` is non-empty whenever the `Slot` constructor reaches `new DescriptorSetPool(...)`, so the empty-template state of `DescriptorSetPool` is now **unreachable from `FrameRing` by construction** rather than by sentinel. |
| **D4** | **Both #191 pinning comments are deleted** (`FrameRing.cs:57-62`, `:258-263`). They exist to defend a sentinel that no longer exists; a comment saying "do not harmonize the two" is false once the two no longer disagree. What replaces them is one short comment at each site explaining the *switch* and the *refusal* respectively. |
| **D5** | **Signature unchanged** — same parameters, same order, same defaults, same types. Source- and binary-compatible for all 32 call sites (E2); no call-site edits outside the tests that assert the guards. |
| **D6** | Docs follow the switch: `FrameRing.cs:11-13`, `:40-45`, and `FrameContext.cs:38-46` stop saying "configured with descriptor-pool sizes" and say "configured with a non-zero `descriptorMaxSets`". |
| **D7** | **`DescriptorSetPool.cs` is not touched at all** — not the guards, not `CreatePool`, not the `Acquire` pre-flight, and specifically **not the auto-grow retry at `:316-330`**, which is #187's territory and is being worked on a separate branch. Not the docs either: #187's own plan step 5 rewrites the `poolSizes` `<param>` paragraph at `:121-138` (`docs/design/plans/2026-09-04-issue-187-descriptor-pool-retry-rollback.md:145-159`), which is the exact paragraph a cross-reference from here would have landed in. See OPEN-2 — this PR touches zero lines of that file. |

### How to reverse this (recorded on purpose)

If a real workload for a budget-less per-slot pool appears, the reversal is
mechanical and does not undo D1: delete guard G2 (`FrameRing.cs:66-69`) and its
test. Because D1 already put the switch on `descriptorMaxSets`,
`descriptorPoolSizes: [], descriptorMaxSets: N` then means "budget-less pool per
slot" with no further change and no ambiguity — the spelling the issue's Q2 asked
about becomes available for free. That is the point of doing D1 even while
answering Q1 "no": the refusal becomes a one-line policy rather than a structural
impossibility.

### Why not the alternatives

- **Do nothing; keep the sentinel and strengthen the two comments.** Rejected:
  the comments already say the strongest thing they can ("do not harmonize"), and
  they still leave two files in one directory teaching opposite meanings for one
  value — which is the entire content of #196. Strengthening prose cannot remove
  a semantic collision.
- **Harmonize by *allowing* the budget-less pool (delete G2 as well).** Rejected
  on E7 + E8: the capability is worth one pool-size entry, has no consumer, and
  turns #187's un-rolled-back sub-pool into a per-frame leak with a growing
  per-frame `Reset` cost on drivers this repo cannot test. Recorded above as the
  cheap reversal if that judgement turns out wrong.
- **A `FrameRingDescriptorMode { None, PerSlotPool }` enum parameter.** Rejected:
  it is a second spelling of a question `descriptorMaxSets == 0` already answers
  unambiguously (E6), it adds a public type to a shipped package for a two-state
  flag, and its `default` (`None`) is redundant with the existing
  `descriptorMaxSets = 0` default — precisely the "is `Empty` different from
  `default`?" confusion #191's D2 refused to introduce.
- **A nullable span shape (`ReadOnlySpan<T>?`, or `default`-vs-`[]` detection).**
  Rejected on measurement, not preference: `ReadOnlySpan<T>?` is not a legal
  type; `Unsafe.IsNullRef` detection makes `[]` and `Array.Empty<T>()` behave
  differently (E5); and `= default` is the only legal optional-parameter default
  for a span, so the "not passed" state cannot be spelled distinctly anyway.
- **A separate factory (`FrameRing.WithDescriptorPools(...)`) or a second
  constructor overload.** Rejected: two constructors for one object whose only
  difference is whether a `uint` is zero, on a type with 32 construction sites
  that would then have two shapes to choose between; and the overload would still
  need G1/G2's consistency checks, so nothing is actually simplified.
- **Reorder the parameters so `descriptorMaxSets` precedes
  `descriptorPoolSizes`, reading as "switch, then budget".** Rejected: a silent
  source break for any positional caller and a binary break for all of them, in
  exchange for reading order. D5 keeps the signature frozen.
- **Move `FrameRing`'s inputs into a `FrameRingDescription` ref struct** (the
  #119 valid-by-default convention, as `DeviceDescription` and
  `DescriptorSetLayoutDescription` do). Rejected as out of scope and
  one-decision-per-spec: it is a source break at all 32 sites and a redesign of a
  constructor whose other four parameters this issue has no opinion about. If
  wanted, it is its own issue.

### What this does not change

- `DescriptorSetPool`'s relaxed guard, its empty-template contract (`:121-138`),
  its `_maxPerTypeDescriptorTotal == 0` reasoning (`:170-181`), the
  `ThrowVariableCountExceedsBudget` empty branch (`:513-518`), and the four
  `new DescriptorSetPool(device, maxSets, [])` tests. #191 stands entirely.
- The auto-grow retry (`:316-330`) and everything #187 will touch.
- The per-frame path: `BeginFrame` → `Slot.WaitAndReset` →
  `DescriptorSets?.Reset()` (`FrameRing.cs:349-366`) is byte-for-byte the same.
  D1 changes a constructor-time branch condition only; no per-frame branch is
  added, no allocation is introduced, and no `FrameRingBenchmarks` number should
  move.
- Native AOT: no reflection, no dynamic codegen, no new type. The diff is a
  changed `if` condition, string literals, and XML docs.

---

## Risks

1. **The decision is a judgement call about a capability, and judgement calls in
   this file family have been overruled before** (#191's OPEN-1 was resolved
   against the architect's recommendation, recorded at
   `docs/design/specs/2026-08-03-issue-191-empty-descriptor-set-layout-design.md:444-472`).
   Mitigated by writing the reversal recipe into the spec: reversing costs one
   guard deletion and one test.
2. **E8's strength depends on #187 staying open, and #187 is actively being
   planned.** Its paired spec and plan already exist in this tree
   (`docs/design/specs/2026-09-04-issue-187-descriptor-pool-retry-rollback-design.md`,
   `…/plans/2026-09-04-issue-187-descriptor-pool-retry-rollback.md`) and add a
   rollback that destroys the failed retry's sub-pool before throwing. Once that
   lands, a misused budget-less ring pool would leak nothing — it would merely
   create-and-destroy a `VkDescriptorPool` per failed `Acquire` per frame. That is
   still a per-frame native-call storm on a hot path, and E7's
   functional-equivalence argument (the one-entry template is free) is unaffected,
   so the decision holds either way; but the *weight* of E8 shifts from "leak" to
   "churn". Recorded as OPEN-1. E8 above describes the code as it stands today and
   should be read as of this date.
3. **Low value per unit of churn.** This is a coherence fix on a low-priority
   issue with no behaviour change; a reviewer may reasonably ask what it buys.
   The answer is E4: it removes the wrapper's only "emptiness decides whether an
   object exists" branch, and it makes the empty-template state of
   `DescriptorSetPool` unreachable from `FrameRing` structurally rather than by
   convention. The diff is deliberately small.
4. **Weak test assertions today.**
   `FrameRingTests.DescriptorSets_Mismatched_Args_Throw` (`:145-163`) asserts
   only exception *types*, which pins less of the contract than it looks like it
   does: both guards throw from the same constructor on adjacent lines, so the
   type alone does not say *which* argument was rejected or why. (An earlier
   draft of this risk claimed the G2 assertion would still pass if the two
   guards swapped, reasoning from `ArgumentOutOfRangeException` deriving from
   `ArgumentException`. That is wrong for xUnit — `Assert.Throws<T>` matches the
   exact type and only `Assert.ThrowsAny<T>` accepts a derived one, so a swap
   would in fact fail today.) The plan still tightens both assertions to check
   `ParamName`, which is what names the specific guard and survives a later
   switch to `ThrowsAny<T>`.

---

## Cross-links

- **Resolves #196.**
- **#191**
  (`docs/design/specs/2026-08-03-issue-191-empty-descriptor-set-layout-design.md`)
  — created the collision and documented it at §E11.5 (`:402-426`); D4 there
  explicitly left `FrameRing` comment-only. This spec discharges that deferral and
  deletes the comments it added. Nothing #191 decided is reopened.
- **#187** (open) — `DescriptorSetPool`'s auto-grow retry keeps a sub-pool that
  cannot satisfy the request. E8 uses it as an argument; D7 forbids touching it.
  **This change has no dependency on #187's outcome and must not be sequenced
  behind it.** See OPEN-1 and OPEN-2.
- **#60** (closed) — auto-grow on pool exhaustion; the growth path a budget-less
  pool's `maxSets` would rely on, and the feature #187 is a defect in.
- **#182**
  (`docs/design/specs/2026-08-03-issue-182-descriptor-pool-variable-count-design.md`)
  — the variable-count `Acquire` and the `_maxPerTypeDescriptorTotal` pre-flight
  guard that E7.2 reasons about.
- **#119**
  (`docs/design/specs/2026-06-12-issue-119-valid-by-default-descriptions-design.md`)
  — the valid-by-default convention the rejected `FrameRingDescription` and
  `FrameRingDescriptorMode` options were weighed against.
- **#32** — wrapper tests are Windows-only; the new tests are ordinary
  `TestGate.RequireDriver()` xUnit cases in the existing Windows lane and add no
  Linux assumption.

---

## OPEN items

**OPEN-1 — sequencing against #187.** With OPEN-2 resolved as recommended, the
two PRs share **no file at all**: #196 edits `Pools/FrameRing.cs`,
`Rendering/FrameContext.cs` and `tests/…/FrameRingTests.cs`; #187 edits
`Pools/DescriptorSetPool.cs` and `tests/…/DescriptorSetPoolTests.cs`. #187's own
plan says the same from its side
(`docs/design/plans/2026-09-04-issue-187-descriptor-pool-retry-rollback.md:15,344-346`).
The maintainer's call is only whether to serialise `Pools/` reviews anyway.
*Recommendation: land independently, in either order, both branched from `main`.*

**OPEN-2 — should this PR cross-reference #196 from `DescriptorSetPool.cs`?**
An earlier draft proposed one sentence on the `poolSizes` `<param>` (`:121-138`)
recording that `FrameRing` never passes an empty template, so a reader arriving
from #191's paragraph knows where the other layer stands. **#187's plan step 5
rewrites that exact paragraph**
(`…/plans/2026-09-04-issue-187-descriptor-pool-retry-rollback.md:145-159`), so
the sentence would be a guaranteed textual conflict on the one paragraph both
issues care about. *Recommendation: drop it from this PR — the plan carries no
step 5 — and let whichever PR lands second add the cross-reference, or file it as
a one-line docs follow-up.* Raised because it is the only place the two designs
would otherwise touch, and because a maintainer sequencing both may prefer to
fold the sentence into #187 directly.

**OPEN-3 — does #196 close as "capability refused", or does a follow-up get
filed?** D1 makes the budget-less spelling available at the cost of one guard
deletion, so a follow-up would be a one-line issue with no known consumer.
*Recommendation: close #196 with the decision, file nothing, and let the reversal
recipe in this spec be the record.* Flagged because it permanently answers the
issue's Q1, and that is a maintainer's call rather than an architect's.
