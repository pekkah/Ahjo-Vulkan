Paired with ../specs/2026-09-04-issue-187-descriptor-pool-retry-rollback-design.md

# Plan — issue #187: roll the auto-grow retry's sub-pool back on a failed retry

> **Status: DEFERRED — design approved, implementation not scheduled.**
> Approved on 2026-09-04 alongside issue #196, then deliberately held back: #196
> shipped first and #187 was parked rather than implemented. Nothing in this
> document is in `main` — `DescriptorSetPool.Acquire` still leaves the failed
> retry's sub-pool chained. The agreed evidence bar for merging the eventual
> change is a **local mutation run** (force the retry to fail; confirm
> `PoolCount == 1` with the fix and `2` without, recorded in the PR body, nothing
> test-only shipped) rather than holding out for AMD/Intel hardware. Line numbers
> below were accurate at 2026-09-04 and drift with every edit to the file.


Branch: `issue-187-descriptor-pool-retry-rollback`. Nine steps. Steps 1-3 are the wrapper code,
4-5 the XML documentation, 6-7 the tests, 8-9 verification and wrap-up.

**Files this PR may touch — and no others:**

- `src/Ahjo.Vulkan/Pools/DescriptorSetPool.cs`
- `tests/Ahjo.Vulkan.Tests/DescriptorSetPoolTests.cs`
- `tests/Ahjo.Vulkan.Tests/DescriptorSetPoolVariableCountTests.cs`
- `docs/benchmarks.md` (only if step 8 measures a real change)

**Explicitly out of scope:** `src/Ahjo.Vulkan/Pools/FrameRing.cs` (issue #196 is in flight on a
separate branch — do not touch it, do not "harmonize" anything with it), anything under
`Generated/`, `native/` or `tools/`, and the `Acquire` pre-flight guard at `:295-296` (load-bearing:
#191's mutation testing found that removing it takes the process down with `0xC0000005` inside the
driver, not a bad `VkResult`).

Line numbers below are as of `main` at `60cd9f2`; they shift as you edit, so work top-down within a
file or re-locate by the quoted text.

---

## 1. `DescriptorSetPool.cs` — add the rollback helper

Add a private instance method next to the other private helpers, after `AllocateFromCurrentPool`
(`:482`) and before the `ThrowVariableCountExceedsBudget` doc block (`:484`):

```csharp
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void RollBackFailedGrowth()
```

Body — exactly three statements, in this order:

1. `nint doomed = _pools[^1];`
2. `_pools.RemoveAt(_pools.Count - 1);`
3. `Vk.vkDestroyDescriptorPool(_device.Handle, (VkDescriptorPool_T*)doomed, null);`

Unchain **before** destroy, not the reverse: the object's own state must never name a destroyed
handle, so a later `Reset`/`Dispose` cannot reach it. `List<T>.RemoveAt` at the last index does not
allocate and does not shrink capacity.

`NoInlining` is not decoration — `docs/benchmarks.md:162` and the standing instruction at
`DescriptorSetPool.cs:484-506` record a measured 43.82 → 38.01 ns swing on
`AcquireReleaseReset_Cycle` attributed to `Acquire`'s IL size and the one-arg forwarder's inlining.
Keep this body out of `Acquire`. Do **not** mark it `[DoesNotReturn]`; it returns, and the throw
stays where it is.

Doc comment to write (content, not literal text — match the file's density):

- Why it exists: the retry built a sub-pool solely to satisfy this one request and the request was
  not satisfied, so the sub-pool has served nothing and never will.
- Why destroying it is safe: **VUID-vkDestroyDescriptorPool-descriptorPool-00303** ("all submitted
  commands that refer to `descriptorPool` via any allocated descriptor sets must have completed
  execution") is vacuously satisfied — `vkAllocateDescriptorSets` frees any partially allocated sets
  and sets every `pDescriptorSets` entry to `VK_NULL_HANDLE` on failure, so no `VkDescriptorSet`
  derived from this pool exists in `_allHandles`, in `_idle`, or in the caller's hands.
  `pAllocator` is `null` on both `CreatePool` and here, per `-00304`/`-00305`.
- Why `NoInlining`: point at the `ThrowVariableCountExceedsBudget` doc block rather than repeating
  the argument, plus "do not fold it back inline" in the same words that block uses.
- The invariant it preserves: `PoolCount` is unchanged across a throwing `Acquire`, and
  `_pools.Count >= 1` still holds because this removes exactly the element the caller added two
  statements earlier.

`System.Runtime.CompilerServices` is already imported (`:2`); no new `using`.

## 2. `DescriptorSetPool.cs` — call it from the retry block

In `Acquire(VkDescriptorSetLayout_T*, uint)`, inside the existing
`if (_growOnExhaustion && IsExhaustion(result))` block (`:319-329`), after the
`if (raw != null) { … return …; }` early return and before the block's closing brace, add:

```csharp
            RollBackFailedGrowth();
```

The condition is "the retry produced no set" — i.e. plain fall-through, **not** a second
`IsExhaustion(result)` test. Any non-success from the retry leaves a sub-pool that served nothing.

Do not add a `try`/`catch` around `CreatePool()`: if it throws, `_pools.Add` never ran and there is
nothing chained (spec E2). Do not touch `_allHandles` or `_idle` on this path — neither has an entry
for a failed allocation.

Everything below the block stays byte-identical apart from step 3's comment: `result.ThrowIfFailed()`
remains the throw site, `VulkanException` remains the exception, `Function` remains `"Acquire"` via
`[CallerMemberName]`, and the surfaced `VkResult` remains the retry's.

## 3. `DescriptorSetPool.cs` — rewrite the two wrong comments

**This step is required on its own merits.** Both comments are factually wrong today, independently
of steps 1-2.

**3a. The retry's justifying comment (`:313-318`).** Replace it. Points the new text must make, all
four:

- The true half, kept: a fresh sub-pool restores the full `maxSets` budget and carries no
  fragmentation. That is what makes ordinary growth work (#60), and this repo's driver was measured
  enforcing `maxSets` (#191's mutation run), so it is the half with real coverage.
- The false half, deleted: a fresh sub-pool does **not** guarantee the requested binding shape fits.
- The two shapes that are unsatisfiable by construction and reach here anyway: (i) a variable count
  that exceeds the template's entry for the variable binding's own descriptor type while staying
  under the largest per-type total the guard above checks — the guard is necessary, never sufficient,
  because the type is not readable back from the handle (#182); (ii) any layout with real bindings
  against a pool created with an empty `poolSizes` template, through the plain `Acquire(layout)`
  overload (#191).
- Therefore the sub-pool this branch builds is rolled back on a failed retry rather than chained
  forever (#187).

Draft, to be reworded to taste but not shortened past those four points:

```csharp
        // Exhaustion is retry-able, but not always satisfiable. A fresh
        // sub-pool from the same template restores the full maxSets budget and
        // carries no fragmentation — that half is real, and it is what makes
        // ordinary growth work (#60). It does NOT guarantee the requested
        // binding shape fits. Two requests are unsatisfiable by construction
        // and still reach here: a variable count larger than the template's
        // entry for the variable binding's own descriptor type but under the
        // largest per-type total the guard above checks (the type is not
        // readable back from a VkDescriptorSetLayout handle, so that guard is
        // necessary and never sufficient — #182), and any layout with real
        // bindings against a pool created with an empty poolSizes template
        // (#191). Both fail the retry too, so the sub-pool this branch built is
        // rolled back rather than chained forever (#187).
```

**3b. The throw-site comment (`:331`).** *"Neither retry path produced a set — surface the original
failure"* is wrong whenever the growth branch ran: `:323` reassigned `result` through `out`. New text
must say which result is surfaced and why that is the useful one — on the growth leg it is the
retry's, from a brand-new sub-pool, which is strictly more diagnostic because it rules fragmentation
out as the explanation. On the other legs (growth off, or a non-exhaustion failure) it is still the
original.

## 4. `DescriptorSetPool.cs` — class-remarks **Auto-grow** paragraph (`:45-68`)

The sentence *"All sub-pools live until `Dispose`; `Reset` resets every one of them"* (`:50-51`) now
needs its exception. Amend to say that a sub-pool the auto-grow retry built for a request the retry
could not satisfy is destroyed before the throw, so a failed `Acquire` leaves `PoolCount` unchanged.
Leave the rest of the paragraph — the `growOnExhaustion: false` guidance and the variable-count
sizing rules (`:57-68`) — alone.

## 5. `DescriptorSetPool.cs` — `poolSizes` parameter doc (`:121-138`) and `Acquire`'s exception docs

**5a.** The empty-span paragraph currently ends: *"it fails with `VK_ERROR_OUT_OF_POOL_MEMORY`,
having chained one wasted sub-pool per failed call — the auto-grow retry builds it and nothing rolls
it back, so they accumulate until `Dispose` (issue #187, widened by this route, not closed by it)"*
(`:132-138`). This is the sentence the change falsifies. Rewrite so it:

- keeps the failure itself — on a driver that enforces per-type pool accounting, passing a layout
  that has bindings to a budget-less pool fails with `VK_ERROR_OUT_OF_POOL_MEMORY`;
- keeps the undiagnosability — the pool cannot check it, because a layout's bindings are not readable
  back from a `VkDescriptorSetLayout` handle;
- replaces the residue clause with: the failed auto-grow retry's sub-pool is destroyed before the
  throw (#187), so the failure is clean and `PoolCount` is unchanged;
- keeps the measurement — on a driver that enforces only `maxSets`, which is what this repo's
  hardware was measured doing, it may simply succeed.

**5b.** Add an `<exception cref="VulkanException">` block to `Acquire(VkDescriptorSetLayout_T*, uint)`
(after the existing `<exception cref="ArgumentOutOfRangeException">` at `:274-280`) stating: the
allocation failed and, where growth was enabled, the retry from a fresh sub-pool failed too; the
`VkResult` is the retry's; **`PoolCount` is unchanged** — a failed `Acquire` never leaves a sub-pool
behind.

**5c.** The `Acquire` summary (`:207-214`) says the pool "allocates a fresh sub-pool with the original
template and retries". Add the failure clause: and destroys it again if the retry also fails.

## 6. `tests/Ahjo.Vulkan.Tests/DescriptorSetPoolTests.cs` — the #191-route invariant test

Add after `Pool_EmptyPoolSizes_AcquireBeyondMaxSets_Grows` (ends `:343`):

```csharp
    [Fact]
    public void Pool_EmptyPoolSizes_AcquireLayoutWithBindings_LeavesNoResidualSubPool()
```

Fixture:

- `TestGate.RequireDriver();`
- `Instance.Create(default)` — **a plain instance, deliberately no validation callback.** The layer
  flags the over-subscription as an error and this test is about the wrapper's chain bookkeeping, not
  the layer's opinion. Model the body on `Pool_EmptyPoolSizes_AcquireBeyondMaxSets_Grows` (`:314-343`),
  not on `Pool_EmptyPoolSizes_AcquireZeroBindingLayout_RoundTrips`.
- `CreateGraphicsDevice(instance)`, `CreateUniformBufferLayout(device)` (the existing helper at
  `:410-435` — a layout with **real** bindings, which is the point), `try`/`finally` destroying the
  layout as every test in this file does.
- `using var pool = new DescriptorSetPool(device, maxSets: 4, []);` — budget-less.

Assertions:

```csharp
            bool threw = false;
            try { pool.Acquire(layout); }
            catch (VulkanException) { threw = true; }

            Assert.Equal(1, pool.PoolCount);
            Assert.Equal(threw ? 0 : 1, pool.AllocatedCount);
```

`Assert.Equal(1, pool.PoolCount)` is the whole test and it is driver-portable by construction:

- on a driver that enforces only `maxSets` (this repo's) the acquire succeeds, no exhaustion, no
  growth — the assertion holds on the no-growth leg;
- on a driver that enforces per-type accounting the acquire fails, growth runs, the retry fails, the
  rollback fires — the assertion holds only because of this change.

Write that into the XML doc on the test, in those terms, plus the plain statement: **on this repo's
hardware this test does not execute the rollback branch; it becomes rollback coverage on the first
AMD/Intel run, unedited.** Do not weaken it to `Assert.True(pool.PoolCount <= 2)` — the exact `1` is
the assertion the change buys.

## 7. `tests/Ahjo.Vulkan.Tests/DescriptorSetPoolVariableCountTests.cs` — the #182-route invariant test

Add:

```csharp
    [Fact]
    public void Acquire_CountWithinAnotherTypesTotal_LeavesNoResidualSubPool()
```

Gate it exactly as the file's other variable-count tests do — `TestGate.RequireDriver()` plus
`TestGate.RequireDeviceFeature(VulkanDriverProbe.SupportsBindlessVariableCountStorageBuffer,
FeatureGateReason)` (see `:173-202`). No validation instance, for the reason in step 6.

Fixture — this is the residual case from spec E3, constructed so the pre-flight guard **passes**.
Model the setup on `Acquire_CountAboveASingleEntryButWithinThePerTypeTotal_Succeeds` (`:203-228`),
which already builds its pool inline from a `ReadOnlySpan<VkDescriptorPoolSize>` rather than through
the single-entry `CreatePool` helper (`:404-415`) — do the same here and leave that helper and its
five call sites alone.

- `using var instance = Instance.Create(default);`, `using var device = CreateVariableCountDevice(instance);`
- `using var layout = CreateVariableCountLayout(device, declaredCount: 32);` — its variable binding is
  a storage buffer, and the layout is built with `UpdateAfterBindPool = true` (`:395-399`).
- pool, inline:

```csharp
        ReadOnlySpan<VkDescriptorPoolSize> sizes =
        [
            new VkDescriptorPoolSize { type = VkDescriptorType.VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER, descriptorCount = 64 },
            new VkDescriptorPoolSize { type = VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER, descriptorCount = 1 },
        ];
        using var pool = new DescriptorSetPool(device, maxSets: 4, sizes, updateAfterBind: true);
```

  `updateAfterBind: true` is mandatory, not stylistic — the layout is an update-after-bind layout and
  a flag mismatch is VUID-VkDescriptorSetAllocateInfo-pSetLayouts-03044 (undefined behaviour; the
  `CreatePool` helper carries the same note at `:402-403`).
- the call: `pool.Acquire(layout.Handle, variableDescriptorCount: 8)`. The variable binding is a
  storage buffer with a budget of 1, so the request is unsatisfiable by construction; 8 ≤ 64 is the
  largest per-type total, so the pre-flight guard lets it through. That gap **is** the test.

The guard firing instead would be a red test, not a silent pass — the `catch` below is
`VulkanException` only, so an `ArgumentOutOfRangeException` escapes and fails. Say so in the XML doc
so nobody later "fixes" the fixture by shrinking the uniform-buffer entry.

Then the same shape as step 6:

```csharp
        bool threw = false;
        try { pool.Acquire(layout.Handle, variableDescriptorCount: 8); }
        catch (VulkanException) { threw = true; }

        Assert.Equal(1, pool.PoolCount);
        Assert.Equal(threw ? 0 : 1, pool.AllocatedCount);
```

Same XML-doc honesty clause as step 6 about which leg runs on which driver.

**Do not add** a variable-count acquire against a *zero-binding* layout from a budget-less pool: #191
measured that combination taking the process down with `0xC0000005` inside the driver when the
pre-flight guard is bypassed. It is guarded, and it is not this issue's shape.

## 8. Mutation verification — run it, record it, revert it

The rollback branch cannot be entered on Windows + NVIDIA (spec E5), so this is the only way to
observe the new code executing before merge. **Nothing from this step is committed.**

In a scratch working copy, force the retry to fail by replacing the retry's allocation at `:323`
with:

```csharp
            raw = null; result = VkResult.VK_ERROR_OUT_OF_POOL_MEMORY;   // MUTATION — revert
```

Then run a temporary test built on the `Pool_GrowDisabled_AcquireBeyondBudget_Throws` fixture
(`:179-203`) but with growth **on**: `maxSets: 2`, `poolSizes` `[{UNIFORM_BUFFER, 2}]`, two acquires,
then a third. That third acquire drives the real path — genuine `maxSets` exhaustion from the driver
(the one budget it enforces), genuine `IsExhaustion`, genuine `CreatePool`, mutated retry failure,
rollback.

Record in the PR description:

| | expected |
|---|---|
| with the mutation, **with** step 1-2 | `VulkanException` thrown, `pool.PoolCount == 1` |
| with the mutation, **without** step 1-2 (stash steps 1-2) | `VulkanException` thrown, `pool.PoolCount == 2` |

The second row is what proves the assertion is sensitive to the fix rather than to the fixture.
Revert the mutation and delete the temporary test before committing; `git diff` must show no trace of
either.

**If either row does not reproduce, stop and report** — it means the branch is not reached the way
this plan claims and the design needs revisiting, not the test needs adjusting.

## 9. Build, existing tests, benchmark, review

- `dotnet build Ahjo.Vulkan.slnx` — must be clean. `TreatWarningsAsErrors=true`; no `#pragma`
  suppressions.
- `dotnet test` on Windows. The three growth tests are the over-fire regression guard and must stay
  green untouched: `Pool_AcquireBeyondMaxSets_GrowsAndSucceeds` (`:101-137`),
  `Pool_GrownChain_ResetThenReallocate_Succeeds` (`:139-177`),
  `Pool_EmptyPoolSizes_AcquireBeyondMaxSets_Grows` (`:314-343`, asserts `PoolCount == 2` exactly). If
  any of them goes red, the rollback is firing on a *successful* growth — that is the bug this change
  can introduce, and it is the one thing this hardware can catch.
  `Pool_GrowDisabled_AcquireBeyondBudget_Throws` (`:179-203`) must also stay green: with growth off
  the new code is unreachable and `PoolCount` stays 1 for the pre-existing reason.
- Also run once with `AHJO_VULKAN_TIER=validation` so the layer sees the two new tests' allocations
  and the suite's validation-gated tests run.
- **Benchmark.** `Pools/**` is a hot path (`src/Ahjo.Vulkan/CLAUDE.md:36`). Run
  `/run-bench` with `--filter "*DescriptorSetPool*"` in Release, capturing a **same-session
  baseline** from `main` — `docs/benchmarks.md` treats cross-session comparisons as invalid.
  Expectation: no change. `AcquireReleaseReset_Cycle` never enters the retry branch (it hits the idle
  free-list), and the new code is behind two conditions and a failed Vulkan call. The reason to
  measure anyway is the recorded IL-size sensitivity of the one-arg `Acquire` forwarder
  (`DescriptorSetPool.cs:484-506`), which step 1's `NoInlining` exists to protect.
  - `Allocated` must read `-` on every row. The success path gained nothing; if a row shows an
    allocation, stop.
  - Update `docs/benchmarks.md:162` **only if** the measurement moved outside the row's noise band.
    If it is within noise, follow the row's own precedent from #182 and #191 and leave the captured
    number alone — but note the re-measurement in the PR.
- Run the `vulkan-validation-reviewer` agent (diff touches `Pools/`) and the
  `bench-coverage-checker` agent.
- PR: `Pools: destroy the auto-grow retry's sub-pool when the retry also fails (#187)`, `Closes #187`.
  The description must carry, in plain words: **the rollback branch ships without automated coverage
  on this repo's hardware**, the mutation table from step 8 as the substitute evidence, and the note
  that the two new tests become real rollback coverage unedited on a per-type-enforcing driver.

---

## Deliberately left open

- **OPEN-1 — do not touch `FrameRing.cs`.** #196 is in flight on a separate branch deciding what
  `FrameRing`'s empty `descriptorPoolSizes` span means. Nothing in this plan depends on its outcome,
  and no file in this PR overlaps with it. If while working you find something in `FrameRing` that
  looks like it needs this change to be complete, **stop and report** rather than widening the diff —
  the influence runs #196 → #187, not the reverse.
- **OPEN-2 — merging code that cannot execute here.** Step 8 is the substitute for coverage. If the
  reviewer wants the branch actually executed on a per-type-enforcing driver (AMD or Intel) before
  merge, that is a hardware call this plan cannot make. Ask before merging if that is in doubt.
- **OPEN-3 — step 7's two-entry template on an update-after-bind pool is unverified here.** The
  template pairs `UNIFORM_BUFFER, 64` with `STORAGE_BUFFER, 1` in a pool created with
  `VK_DESCRIPTOR_POOL_CREATE_UPDATE_AFTER_BIND_BIT`. Nothing in the registry gates budgeting a type
  you never allocate, and `vkCreateDescriptorPool` has no device-limit VUID, so this should just
  work — but it is reasoning, not a measurement. If `vkCreateDescriptorPool` rejects it or the layers
  complain, **stop and report**; the fix is to pick a different filler type, not to change the
  storage-buffer entry, which is what makes the test the residual case.
