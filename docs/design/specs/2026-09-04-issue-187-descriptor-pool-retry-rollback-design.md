# Issue #187 — roll the auto-grow retry's sub-pool back when the retry also fails

Paired plan: `../plans/2026-09-04-issue-187-descriptor-pool-retry-rollback.md`

> **Status: DEFERRED — design approved, implementation not scheduled.**
> Approved on 2026-09-04 alongside issue #196, then deliberately held back: #196
> shipped first and #187 was parked rather than implemented. Nothing in this
> document is in `main` — `DescriptorSetPool.Acquire` still leaves the failed
> retry's sub-pool chained. The agreed evidence bar for merging the eventual
> change is a **local mutation run** (force the retry to fail; confirm
> `PoolCount == 1` with the fix and `2` without, recorded in the PR body, nothing
> test-only shipped) rather than holding out for AMD/Intel hardware. Line numbers
> below were accurate at 2026-09-04 and drift with every edit to the file.


## Problem

`DescriptorSetPool.Acquire` grows its sub-pool chain on descriptor-pool exhaustion and retries the
allocation exactly once (`src/Ahjo.Vulkan/Pools/DescriptorSetPool.cs:319-329`). When the retry also
fails, the freshly created `VkDescriptorPool` stays chained forever. The justifying comment
immediately above it (`:313-318`) is the reason nothing rolls it back:

> Exhaustion is the only retry-able failure: a fresh sub-pool built from the same template fits the
> requested binding shape, and a brand-new pool can't already be fragmented. The guard above is what
> keeps that true once the shape depends on a runtime count — it rejects the counts no template entry
> could hold, before any sub-pool is built for them.

Half of that is true and load-bearing: a brand-new sub-pool restores the full `maxSets` budget and
carries no fragmentation, which is what makes ordinary auto-grow work (#60). The other half — "fits
the requested binding shape" — is false on two routes that both exist in shipped code:

1. **#182's residual case.** `Acquire(layout, count)` makes the requested shape a function of a
   runtime argument. The pre-flight guard (`:295-296`) compares `count` against
   `_maxPerTypeDescriptorTotal`, the largest **per-type** total in the template, because the variable
   binding's descriptor *type* is not readable back from a `VkDescriptorSetLayout` handle
   (`:285-294`). So a template of `[{UNIFORM_BUFFER, 64}, {STORAGE_BUFFER, 1}]` passes
   `Acquire(layout, 8)` for a layout whose variable binding is a storage buffer: 8 ≤ 64. The
   allocation is unsatisfiable by construction, the retry rebuilds the same unsatisfiable template,
   and the sub-pool is chained.
2. **#191's route.** A pool constructed with an **empty** `poolSizes` span has no per-type budget at
   all. Any ordinary layout with real bindings then reaches the same doomed retry through the plain
   `Acquire(layout)` overload — no variable count, no mis-sizing, no guard to pass. The class's own
   `poolSizes` doc already states the consequence (`:132-138`): *"having chained one wasted sub-pool
   per failed call — the auto-grow retry builds it and nothing rolls it back, so they accumulate
   until `Dispose` (issue #187, widened by this route, not closed by it)."*

The residue is bounded per call (the retry is a single shot, not a loop) and unbounded across calls.
It is not cleared by `Reset`: `Reset` calls `vkResetDescriptorPool` on every chained sub-pool and
explicitly preserves the chain length (`:381-408`, "Sub-pool count is preserved"). Only `Dispose`
(`:410-422`) destroys them. So on a driver that enforces per-type pool accounting, a caller that
repeatedly makes a doomed `Acquire` — and catches the exception, which is the shape an engine's
"this material failed to bind, skip it" path takes — accumulates one `VkDescriptorPool` per call for
the lifetime of the pool object, across every frame and every `Reset`.

A second, smaller defect sits four lines below. `:331` reads *"Neither retry path produced a set —
surface the original failure"*, but `:323` passed `out result` and overwrote it. When the growth
branch ran, the caller sees the **retry's** `VkResult`, not the original. The comment is correct only
on the legs where growth did not run (`_growOnExhaustion == false`, or a non-exhaustion failure).

## Evidence

### E1 — the chain is a `List<nint>`, and "current" means "last"

`_pools` is `private readonly List<nint>` (`:73`) holding raw `VkDescriptorPool` handles as `nint`.
There is no linked list and no per-sub-pool bookkeeping object: no allocation counter, no "was
anything ever allocated from this one" flag, no per-sub-pool remaining budget. The three things that
read it are:

- `AllocateFromCurrentPool` (`:447-482`) — `var current = (VkDescriptorPool_T*)_pools[^1];`. "Current"
  is *always* the last element; there is no separate `_current` field.
- `Reset` (`:395-396`) — `vkResetDescriptorPool` over every element.
- `Dispose` (`:414-418`) — `vkDestroyDescriptorPool` over every non-zero element.

`PoolCount => _pools.Count` (`:98`) is the only public window onto it. The `VkDescriptorPool` handles
themselves are never exposed — there is no public or internal accessor that hands one out.

The constructor pre-grows and adds exactly one sub-pool (`:203-204`), so `_pools.Count >= 1` holds
from construction onward.

### E2 — what the retry block does, statement by statement

```
:319   if (_growOnExhaustion && IsExhaustion(result))
:321       _pools.EnsureCapacity(_pools.Count + 1);
:322       _pools.Add(CreatePool());
:323       raw = AllocateFromCurrentPool(layout, variableDescriptorCount, out result);
:324-328   if (raw != null) { _allHandles.Add((nint)raw); return …; }
:329   }
:332   result.ThrowIfFailed();
```

Two consequences worth stating because they constrain the fix:

- If `CreatePool()` itself throws (`vkCreateDescriptorPool` failing at `:442`), `_pools.Add` never
  runs and there is nothing to roll back — the `VulkanException` propagates with `_pools` unchanged.
  That leg is already correct and needs no `try`/`catch`.
- After `:322` succeeds, the only exit from the block that is not a `return` is falling through to
  `:332`, which always throws (`ThrowIfFailed` is total on non-`VK_SUCCESS`). So the fall-through
  path and the leak path are the same path.

`IsExhaustion` (`:529-531`) is `OUT_OF_POOL_MEMORY || FRAGMENTED_POOL`.

### E3 — route 1 is reachable through the public API, with a concrete fixture

The guard at `:295-296` is documented as a *necessary, never sufficient* condition, in the code
(`:285-294`), in the class remarks (`:64-68`) and in the `poolSizes` parameter doc (`:113-120`). The
existing test `Acquire_CountAboveASingleEntryButWithinThePerTypeTotal_Succeeds`
(`tests/Ahjo.Vulkan.Tests/DescriptorSetPoolVariableCountTests.cs:204`) already exercises the
"passes the guard" side of that with a same-type duplicate template. The residual case is the
different-type variant of the same shape and needs no new API to reach.

### E4 — route 2 is already documented as a defect in shipped XML docs

`:121-138` is the `poolSizes` doc paragraph added by #191. It states the failure and names #187 as
the open fix. `tests/Ahjo.Vulkan.Tests/DescriptorSetPoolTests.cs:256-343` covers the *intended* uses
of a budget-less pool (zero-binding layout round trip; `maxSets` growth); nothing covers the misuse,
deliberately — see E5.

### E5 — what this repo's hardware does, and what it therefore cannot show

Measured for #182 and re-stated in #191's spec: RTX 4070 Ti, NVIDIA 610.47.0.0, validation layer
1.4.341 enforces `maxSets` only. 17 descriptors from a 16-descriptor pool succeeded; 26 from 16
succeeded; 16+16 from 4 succeeded. `vkAllocateDescriptorSets` never returned
`VK_ERROR_OUT_OF_POOL_MEMORY` for a per-type over-subscription.

Two conclusions follow, and they point in opposite directions:

- **The `maxSets` half of the retry genuinely works here.** #191's mutation testing ran
  `Pool_EmptyPoolSizes_AcquireBeyondMaxSets_Grows` with `growOnExhaustion: false` and got
  `VK_ERROR_OUT_OF_POOL_MEMORY` — so `IsExhaustion` fires and growth runs on this box. The existing
  `Pool_AcquireBeyondMaxSets_GrowsAndSucceeds`
  (`tests/Ahjo.Vulkan.Tests/DescriptorSetPoolTests.cs:101-137`) can only pass if that is true.
- **The failing-retry branch cannot be entered here.** The constructor rejects `maxSets: 0`
  (`:157`), so a brand-new sub-pool always has room for the one set the retry asks for, and per-type
  over-subscription does not fail. There is no legal call — with or without a variable count, with or
  without an empty template — that makes the second `AllocateFromCurrentPool` return null on this
  driver. Confirmed by reading every failure mode: the remaining ones
  (`OUT_OF_HOST_MEMORY`, `OUT_OF_DEVICE_MEMORY`, `DEVICE_LOST`) are not deliberately reproducible in
  a test that shares a device with the rest of the suite.

**This is the load-bearing uncertainty in this spec, and it is not resolved by it.** Whether any
real driver returns `OUT_OF_POOL_MEMORY` for either route is spec-derived, not measured — no AMD or
Intel box was available. The Vulkan spec mandates the failure; this repo has never observed it.

### E6 — consumer audit: nothing in `src/` or `samples/` calls `Acquire`

Repo-wide search for `DescriptorSetPool.Acquire` call sites:

| Where | Count | Sites |
|---|---|---|
| `src/` | **0** | `FrameContext.DescriptorSets` (`src/Ahjo.Vulkan/Rendering/FrameContext.cs:46`) exposes the pool; nothing in the wrapper calls `Acquire` on it |
| `samples/` | **0** | no sample acquires a descriptor set from the pool |
| `tests/Ahjo.Vulkan.Tests/` | 17 | `DescriptorSetPoolTests.cs` (11), `DescriptorSetPoolVariableCountTests.cs` (6) |
| `tests/Ahjo.Vulkan.Benchmarks/` | 8 | `DescriptorSetPoolBenchmarks.cs`, `DescriptorSetPoolVariableCountBenchmarks.cs`, `BindDescriptorSetsBenchmarks.cs`, `PushDescriptorsBenchmarks.cs` |

`PoolCount` has exactly one definition (`:98`) and eight read sites, all in
`DescriptorSetPoolTests.cs` (`:115`, `:126-127`, `:155`, `:160`, `:199`, `:247`, `:324`, `:332`).
Nothing outside the test suite observes the chain length, so changing it *after a throw* has no
production blast radius.

### E7 — destroying the fresh sub-pool is safe, and the safety argument is spec-derived

`vkDestroyDescriptorPool` has one lifetime VUID —
**VUID-vkDestroyDescriptorPool-descriptorPool-00303**: *"All submitted commands that refer to
`descriptorPool` (via any allocated descriptor sets) must have completed execution"*
(`native/downloaded/Vulkan-Headers-vulkan-sdk-1.4.341.0/registry/validusage.json`). It is
**vacuously satisfied** for the sub-pool this branch created:

- The pool was created microseconds earlier at `:322` and the only allocation attempted from it is
  the one at `:323` that just failed.
- `vkAllocateDescriptorSets` is specified to free any partially allocated sets and set every entry of
  `pDescriptorSets` to `VK_NULL_HANDLE` when it fails, and `AllocateFromCurrentPool` returns `null`
  for any non-`VK_SUCCESS` result (`:481`) — so no `VkDescriptorSet` derived from it exists, in
  `_allHandles`, in `_idle`, or in the caller's hands.
- The two allocator VUIDs (`-00304`, `-00305`) are satisfied: `CreatePool` passes `null` for
  `pAllocator` (`:442`) and so does `Dispose` (`:417`).
- `descriptorPool` is externally-synchronized; the class is documented as not thread-safe for
  concurrent `Acquire` (`:10-13`).

`vkDestroyDescriptorPool` returns `void`, so the rollback cannot itself throw and shadow the
`VkResult` the caller needs.

### E8 — this is a benchmarked hot path with a standing inlining warning

`Pools/**` is listed as a zero-per-frame-allocation hot path (`src/Ahjo.Vulkan/CLAUDE.md:36`).
`docs/benchmarks.md:162` records `DescriptorSetPool.AcquireReleaseReset_Cycle` at **39.62 ns,
Allocated `-`**, with two later same-session re-measurements (38.67 ns for #182, 38.01 ns for #191).

That row also carries the repo's only standing "do not fold this back inline" instruction
(`DescriptorSetPool.cs:484-506`): the pre-flight guard's message was extracted into a
`[MethodImpl(MethodImplOptions.NoInlining)]` helper because the inline form measured 43.82 ns in the
same session, and the stated mechanism is **IL size** — *"the one-arg `Acquire` forwarder's inlining
is size-sensitive"*. Any code added to `Acquire`'s body, even on a cold branch, is subject to that
finding.

### E9 — the second inaccuracy is real, not stylistic

`result` is declared as the `out` of the first `AllocateFromCurrentPool` (`:306`) and reassigned by
the retry (`:323`). The comment at `:331` claims the original failure is surfaced. On the growth leg
it is not. The two codes can genuinely differ: `FRAGMENTED_POOL` from a used sub-pool followed by
`OUT_OF_POOL_MEMORY` from a fresh one is exactly the case the retry exists for.

### E10 — what the exception looks like today

`result.ThrowIfFailed()` (`:332`) reaches `ResultExtensions.Throw`
(`src/Ahjo.Vulkan/Internal/ResultExtensions.cs:96-117`). `OUT_OF_POOL_MEMORY` and
`FRAGMENTED_POOL` are not among the cached codes, so the caller gets a fresh
`VulkanException` with `Result` set and `Function == "Acquire"` from `[CallerMemberName]`, message
`"Acquire failed: VK_ERROR_OUT_OF_POOL_MEMORY"`. `Pool_GrowDisabled_AcquireBeyondBudget_Throws`
(`tests/Ahjo.Vulkan.Tests/DescriptorSetPoolTests.cs:179-203`) pins `VulkanException` as the type and
already asserts `PoolCount == 1` after it — the exact assertion shape this fix needs, one leg over.

## Decision

**Make the change.** On a failed retry, unchain and destroy the sub-pool the retry created, then
throw as before. Rewrite the two inaccurate comments and the XML docs that describe the residue.

The previous answer (#182 D6: "shipping an untestable branch on a hot path is worse than documenting
the residue") is re-weighed and reversed, on three pieces of new information:

- **#191 widened the entry condition from "programming error involving a runtime count" to "created
  a pool with `[]` and passed it a layout with bindings."** The second is not a mis-sizing; it is one
  argument away from the feature #191 shipped, and the class documents that it *cannot* diagnose it
  (a layout's bindings are not readable back from its handle, `:129-132`). #182's residual case is
  rare enough to argue about; #191's is not.
- **The residue survives `Reset`** (E1). #182's D6 was written before the budget-less route existed
  and did not weigh a per-frame `Reset` loop that never drops the accumulated pools.
- **The untestability is narrower than "an untestable branch on a hot path" suggests** (E7, E8). The
  branch's *entry* is what cannot be reached here; its *body* is three straight-line statements whose
  correctness argument is spec-derived, and it sits after a Vulkan call that already failed, so it
  is not on any measured path.

### D1 — the rollback, precisely

Inside the existing `if (_growOnExhaustion && IsExhaustion(result))` block, after the `if (raw !=
null) { … return …; }` early return, when control reaches the end of the block:

1. Capture the doomed handle: `nint doomed = _pools[^1];`
2. Unchain it **first**: `_pools.RemoveAt(_pools.Count - 1);`
3. Destroy it: `Vk.vkDestroyDescriptorPool(_device.Handle, (VkDescriptorPool_T*)doomed, null);`

Then fall through to the unchanged `result.ThrowIfFailed()`.

Unchain-before-destroy, not the reverse, so the object's own state never names a destroyed handle —
a later `Dispose` or `Reset` cannot reach it even if something between the two statements were ever
added. The condition is simply "the retry produced no set", not "the retry failed with an exhaustion
code": any non-success from `:323` leaves a sub-pool that served nothing.

Resulting invariants, all of which the plan asserts:

- **`PoolCount` is unchanged across a throwing `Acquire`.** Growth is observable only when it
  succeeded. This makes `PoolCount` mean "sub-pools that have served at least one allocation, plus
  the one the constructor made", which is the meaning the existing tests already assume.
- **`_pools.Count >= 1` still holds.** Rollback removes exactly the element added two statements
  earlier, and the constructor guarantees at least one before it.
- **No handle can be observed after rollback.** `_pools[^1]` reverts to the sub-pool that was current
  before growth; the destroyed handle was never exposed (E1) and no `VkDescriptorSet` was derived
  from it (E7). `_allHandles` and `_idle` are untouched on this path.
- **Nothing new allocates.** `List<T>.RemoveAt` at the last index does not allocate and does not
  shrink capacity, so a repeated doomed `Acquire` performs `EnsureCapacity` (no-op after the first),
  `Add`, `RemoveAt` with zero managed allocation. The success path is not touched at all.
- **`vkCreateDescriptorPool` + `vkDestroyDescriptorPool` per doomed call.** One extra driver round
  trip on a path that already made one and is about to throw.

### D2 — the caller still sees the same exception

No new exception type, no message wrapping, no new throw helper on this path. The caller gets
`VulkanException` with the retry's `VkResult` and `Function == "Acquire"`, exactly as today (E10).
Two reasons: `Pool_GrowDisabled_AcquireBeyondBudget_Throws` already pins that shape one leg over, and
a wrapped message would have to be built on a path that can be `OUT_OF_HOST_MEMORY`. The *diagnosis*
("your pool has no budget for this layout") belongs in the XML docs, which D3 rewrites, not in a
string built at failure time.

The behavioural change visible to a caller is therefore exactly one thing: `PoolCount` after the
throw. Documented, not silent.

### D3 — the comments and docs are rewritten regardless of D1

This part would be required even if D1 had gone the other way; it is not conditional on the code
change. Four sites:

- `:313-318` — the retry's justifying comment. Must keep the true half (fresh sub-pool ⇒ full
  `maxSets`, no fragmentation ⇒ ordinary growth works, #60), drop the false half, name both
  unsatisfiable routes and say that the sub-pool is rolled back rather than chained.
- `:331` — "surface the original failure" is wrong on the growth leg; say which result is surfaced.
- `:45-68` — the class remarks' **Auto-grow** paragraph. *"All sub-pools live until `Dispose`"* needs
  the exception: a sub-pool built for a request the retry could not satisfy is destroyed before the
  throw.
- `:121-138` — the `poolSizes` paragraph's *"nothing rolls it back, so they accumulate until
  `Dispose` (issue #187, widened by this route, not closed by it)"* is the sentence this change
  falsifies. It must still say the allocation fails on a per-type-enforcing driver — that part is
  unchanged and is the whole reason the paragraph exists — but say that it fails cleanly.

### D4 — extract the rollback into a `NoInlining` helper

`private void RollBackFailedGrowth()`, marked `[MethodImpl(MethodImplOptions.NoInlining)]`, called
from the one site. Not decoration: E8 records a measured 43.82 → 38.01 ns swing on this exact method
attributed to `Acquire`'s IL size and the one-arg forwarder's inlining. Three inline statements plus
a pointer cast is enough IL to be worth keeping out, and the file already carries a standing
instruction not to undo the equivalent extraction for the guard message. The helper is not
`[DoesNotReturn]` — it returns, and the throw stays at `:332`.

### D5 — test strategy: portable invariant tests here, mutation as the acceptance evidence

**The rollback body cannot be executed on Windows + NVIDIA** (E5). Say it plainly rather than
inventing a fixture that pretends otherwise. What is available:

1. **Two driver-portable invariant tests** that assert *"`PoolCount` is unchanged across an `Acquire`
   that may or may not throw"* — one per route. On this driver both take the no-exhaustion leg (the
   acquire succeeds, no growth, `PoolCount == 1`) and the assertion holds trivially. On a driver that
   enforces per-type accounting the same assertion becomes genuine rollback coverage without a single
   edit. They are written to accept either outcome and assert only the invariant that must hold on
   both, so they are green here and meaningful there.
2. **Mutation verification, run locally by the implementer and recorded, not shipped.** This is the
   repo's established practice — #191's spec is built on it. Temporarily force the retry's allocation
   to fail, run the existing `growOnExhaustion: true` exhaustion fixture, and confirm the branch
   behaves: `VulkanException` thrown, `PoolCount == 1`. Reverting the mutation and confirming the
   assertion flips to 2 proves the test is sensitive to the fix. The exact mutation is in the plan.
3. **Over-fire regression coverage, which already exists.** `Pool_AcquireBeyondMaxSets_GrowsAndSucceeds`
   (`:101-137`), `Pool_GrownChain_ResetThenReallocate_Succeeds` (`:139-177`) and
   `Pool_EmptyPoolSizes_AcquireBeyondMaxSets_Grows` (`:314-343`, asserting `PoolCount == 2` exactly)
   all fail if the rollback ever fires on a successful growth. That is real, running, Windows+NVIDIA
   coverage for the half of the change that *can* go wrong here.

**What this means for merging:** the three straight-line statements of the rollback body ship without
automated coverage on this repo's hardware, and the PR must say so. The mitigations are (2) as
one-off evidence, (3) as standing regression coverage, and the fact that the body's correctness
argument is a spec citation (E7) rather than an observation. Anyone who disagrees with merging
uncovered code should note that the alternative — the status quo — is *also* uncovered here, and is
wrong.

### D6 — no test seam

Rejected below, but recorded here as a decision because it is the obvious counter-proposal: no
injectable allocator delegate, no `internal static bool` failure switch, no `InternalsVisibleTo` hook
for forcing the retry to fail.

### Why not the alternatives

- **Re-affirm #182 D6 — document the residue, change nothing.** The honest baseline, and it was right
  when the only route needed a mis-sized runtime count. #191 turned the entry condition into "empty
  `poolSizes` + a layout with bindings", which is undiagnosable by construction and one argument away
  from a shipped feature; and the residue survives `Reset`, so the accumulation is for the lifetime of
  the pool object, not the frame.
- **A test seam that forces the retry to fail** (an `internal static bool`, or a delegate/function
  pointer for the allocation call). Rejected: it puts test-only mutable state into a class documented
  as externally-synchronized-per-instance while xUnit runs classes in parallel, and it buys coverage
  of three statements at the price of production surface that the AOT and zero-alloc invariants would
  then have to be re-argued around. The same information is obtainable from a local mutation run
  (D5.2) with zero shipped surface.
- **Suppress auto-grow when `_poolSizes.Length == 0`.** Wrong, and already rejected in #191's spec:
  `maxSets` is a budget-less pool's *legitimate* exhaustion mode and growth is the correct response,
  and `VkResult` cannot distinguish "out of `maxSets`" from "out of per-type budget".
- **Pre-flight-reject a layout that has bindings.** Impossible, and already rejected in #191's spec: a
  layout's bindings are not readable back from a `VkDescriptorSetLayout` handle. The file says so
  three times (`:129-132`, `:222-227`, `:252-254`).
- **Prune sub-pools that never served an allocation, in `Reset` or lazily.** Rejected: it needs a
  per-sub-pool "ever used" flag (E1 shows there is no per-sub-pool state at all today), it moves work
  onto `Reset`, which *is* a per-frame path, and it fixes the symptom late instead of not creating it.
  The rollback is strictly smaller and strictly earlier.
- **Loop the retry instead of rolling back.** Rejected: the failures in question are unsatisfiable by
  construction, so a loop turns a bounded residue into an unbounded one plus a hang.
- **Wrap the failure in a richer exception naming the empty template as the likely cause.** Rejected
  for this change: it would change `VulkanException.Function`/message on a path an existing test pins,
  it can fire on `OUT_OF_HOST_MEMORY` where allocating a message is the wrong move, and the guess
  ("your template is probably too small") is not derivable from the `VkResult`. The diagnosis belongs
  in the XML docs (D3).
- **Also roll back when `CreatePool()` throws.** Rejected as a non-problem: `_pools.Add` never runs on
  that leg (E2), so there is nothing chained. Adding a `try`/`catch` would be dead code on a hot
  method.

## Cross-links

- **Closes #187.** The rollback this issue names, plus the comment rewrite the issue identifies as
  independently required.
- **Closes the residue #191 documented but deliberately left open.**
  `docs/design/specs/2026-08-03-issue-191-empty-descriptor-set-layout-design.md:538-541, 584-590,
  644-646` names "roll the fresh sub-pool back when the retry also fails" as *correct*, ~5 lines, and
  explicitly out of scope for that PR. `docs/design/plans/2026-08-03-issue-191-…:454` repeats it as
  deferred work. This spec is that work.
- **Supersedes #182 D6.**
  `docs/design/specs/2026-08-03-issue-182-descriptor-pool-variable-count-design.md:466-477` decided
  "leave the auto-grow retry alone, and say why". That decision is reversed here, on the grounds in
  D1's preamble. The pre-flight guard D5 introduced is untouched and stays a necessary-not-sufficient
  condition — #191's mutation testing found that removing it takes the process down with `0xC0000005`
  inside the driver, so it is load-bearing well beyond message quality.
- **Must land consistently with #60** (auto-grow on pool exhaustion). The rollback does not weaken
  auto-grow: growth that succeeds is unaffected, and #60's three growth tests are the regression
  guard (D5.3).
- **Independent of #196** (FrameRing's empty-span sentinel), which is in flight on a separate branch.
  This change touches no file #196 touches. See the OPEN item below for the one direction of
  influence, which runs #196 → #187 and not the reverse.

## Open questions

- **OPEN-1 — #196 interaction is one-directional, and this spec does not resolve it.** If #196 later
  lets `FrameRing` construct a budget-less `DescriptorSetPool` per slot (`FrameRing.cs:57-69`,
  `:259-266`), route 2 lands on a genuine per-frame path where a doomed `Acquire` would run every
  frame — which strengthens the case for this change but does not alter its design. Nothing here
  depends on #196's outcome, and `FrameRing.cs` must not be touched by this PR. Flagged rather than
  resolved: if the sequencing means #196 should cite this fix, that is #196's call, not this one's.
- **OPEN-2 — no per-type-enforcing driver was available.** E5 states the limit. If the reviewer wants
  the rollback branch executed before merge rather than argued for, the only honest answers are an
  AMD/Intel box or the mutation run in D5.2. Confirm which is required.
