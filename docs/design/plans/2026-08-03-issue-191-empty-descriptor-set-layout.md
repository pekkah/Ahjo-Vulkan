Paired with `../specs/2026-08-03-issue-191-empty-descriptor-set-layout-design.md`

# Plan — issue #191: accept an empty `Bindings` span

Branch `issue-191-empty-descriptor-set-layout` (already checked out). Nine
steps: four code edits plus one comment-only touch, four documentation edits,
then tests and verification. Do them in order — step 4's Slang change is only
correct once step 1 has landed.

**Scope note.** The spec's original OPEN-1 — relaxing `DescriptorSetPool`'s
`poolSizes` guard — was resolved **INCLUDE** by the maintainer, against the
spec's recommendation. Step 3 is therefore a real code change, not a no-op. Read
spec §E11 before starting it. Two things in there are load-bearing and will look
like obvious improvements if you skip it: do **not** suppress auto-grow for a
budget-less pool (§E11.3 shows why that is wrong, not merely unnecessary), and do
**not** "harmonize" `FrameRing`'s empty-span opt-out with the relaxed guard
(§E11.5 — a behaviour change on a per-frame path, explicitly out of scope).

---

## 1. `src/Ahjo.Vulkan/Lifecycle/Device.cs` — delete the guard

**Delete lines 198-199 in their entirety:**

```csharp
if (desc.Bindings.IsEmpty)
    throw new ArgumentException("DescriptorSetLayoutDescription.Bindings must contain at least one entry.");
```

Nothing replaces them. `ValidateVariableDescriptorCountOrdering(desc.Bindings)`
at `:201` becomes the first statement of the method body. Do not add a
`Bindings.IsEmpty` early-return either — the method must reach
`vkCreateDescriptorSetLayout` with `bindingCount = 0` and return a real handle.

**Rewrite the XML block at `:184-195`.** The `<exception cref="ArgumentException">`
tag currently reads "`desc` has no bindings, or a binding carrying …". Drop the
first clause so it names only the #192 ordering violation:

```
/// <exception cref="ArgumentException">
/// A binding carrying <see cref="DescriptorBindingFlags.VariableDescriptorCount"/>
/// is not the one with the highest binding number in the set
/// (VUID-VkDescriptorSetLayoutBindingFlagsCreateInfo-pBindingFlags-03004).
/// </exception>
```

**Add a `<remarks>` paragraph** to the same block, stating in the wrapper's usual
voice, and covering all four points:

- An empty `Bindings` span is legal and produces a layout with zero bindings.
  Vulkan contemplates it —
  `VUID-VkDescriptorSetLayoutCreateInfo-pBindings-parameter` excuses `pBindings`
  "if `bindingCount` is not 0", and there is no `bindingCount-arraylength` VUID.
  Measured: `VK_SUCCESS`, validation layer silent (issue #183 §E7, NVIDIA
  RTX 4070 Ti, layer 1.4.341).
- It is the layout Vulkan wants for an unpopulated set index in a sparse-set
  program, and for a set whose every binding is a zero-length resource array
  (issue #191, issue #183).
- **Contrast with `DescriptorBinding.Count == 0`**, which is issue #119's
  sentinel for a zeroed span element and is still normalized to `1` at `:218`.
  An empty span is zero bindings; a one-element span of `default(DescriptorBinding)`
  is one binding of one descriptor. Say this explicitly — the two are adjacent
  and mean opposite things.
- Such a layout cannot carry a `DescriptorTemplate<T>`; see step 2.

Leave the `Count == 0 ? 1u` normalization at `:218` and its comment at `:214-217`
alone.

## 2. `src/Ahjo.Vulkan/Pools/DescriptorTemplate.cs` — keep the guard, fix the message

`DescriptorTemplateBuilder.BuildEntries` at `:157-158` **stays**. Vulkan itself
forbids a zero-entry template. Replace only the message so it reads as a Vulkan
rule rather than a wrapper preference:

```csharp
if (bindings.IsEmpty)
    throw new ArgumentException(
        "DescriptorTemplate<T> requires at least one binding: Vulkan requires "
        + "descriptorUpdateEntryCount > 0 "
        + "(VUID-VkDescriptorUpdateTemplateCreateInfo-descriptorUpdateEntryCount-arraylength). "
        + "A descriptor set layout with zero bindings is legal (issue #191) but has nothing "
        + "for a template to update.",
        nameof(bindings));
```

Add a one-line code comment above it saying this guard is **not** the symmetric
twin of the one deleted in step 1 — it is a real VUID — so nobody removes it by
analogy. Change nothing else in this file; `Count == 0 ? 1u` at `:167` and the
strict-size check at `:189-193` stay as they are.

## 3. `src/Ahjo.Vulkan/Pools/DescriptorSetPool.cs` — relax the `poolSizes` guard

Resolved INCLUDE by the maintainer (spec D4 + *Overruled*). Evidence: spec §E11.
Four edits here, two of them comments, plus one comment-only touch on `FrameRing`.

**3a. Delete the guard at `:138-139`:**

```csharp
if (poolSizes.IsEmpty)
    throw new ArgumentException("poolSizes must contain at least one entry.", nameof(poolSizes));
```

Keep `ArgumentNullException.ThrowIfNull(device)` (`:136`) and
`ArgumentOutOfRangeException.ThrowIfZero(maxSets, nameof(maxSets))` (`:137`) —
the latter is `VUID-VkDescriptorPoolCreateInfo-descriptorPoolOverallocation-09227`
and is guarded by `Pool_Ctor_ZeroMaxSets_Throws`
(`tests/Ahjo.Vulkan.Tests/DescriptorSetPoolTests.cs:211-227`). Nothing else in the
constructor changes: the per-type loop at `:148-161` runs zero times and leaves
`_maxPerTypeDescriptorTotal` at its `0` seed, which is correct (spec §E11.2).

**3b. Comment the two newly reachable states**, so neither reads as an oversight
later:

- Append to the existing comment block above the per-type loop (`:140-147`): an
  empty `poolSizes` leaves this at `0`, a value no non-empty template can produce
  (`VUID-VkDescriptorPoolSize-descriptorCount-00302` requires every entry's
  `descriptorCount > 0`), and the `Acquire` guard is *correct* at `0` rather than
  merely tolerant — it then rejects every variable count ≥ 1, which is the right
  answer for a pool holding no descriptors.
- Above `CreatePool`'s `fixed` (`:403`): `fixed` over a zero-length array yields
  `null`, so an empty template emits `poolSizeCount = 0, pPoolSizes = null` — the
  shape `VUID-VkDescriptorPoolCreateInfo-pPoolSizes-parameter` excuses.

**3c. Extend the pre-flight message at `:267-273` with an empty-template branch.**
Keep the existing text verbatim for a non-empty template — it is #182's and tests
may assert on it. When `_poolSizes.Length == 0`, say instead (exact wording yours;
these facts required): the pool was created with **no** `poolSizes`, so it holds
no descriptors of any type and can serve only descriptor set layouts with zero
bindings; a variable-descriptor count of *N* cannot be satisfied by any sub-pool
built from an empty template; pass a `poolSizes` template if the layout has
bindings.

Two constraints on how, both load-bearing:

- **Do not change the comparison at `:266`** and do not touch the
  "NECESSARY-condition guard, and it must stay one" comment at `:256-265`.
- **Build the message inside the throwing branch**, never before the comparison.
  `Acquire` is a benchmarked hot path (`docs/benchmarks.md:89-91`); a string or an
  interpolation hoisted above the `if` is a per-call allocation, and step 9's
  benchmark run is what will catch it.

**3d. Document the contract on the `poolSizes` `<param>` (`:106-119`).** Add a
paragraph: an **empty** span creates a pool with no descriptor budget
(`poolSizeCount = 0`, legal Vulkan). Such a pool can allocate `maxSets` descriptor
sets whose layouts have **zero bindings** — issue #191's sparse-set hole — and
nothing else. The wrapper **cannot check** that the layout passed to `Acquire` has
no bindings, because bindings are not readable back from a
`VkDescriptorSetLayout` handle; on a driver that enforces per-type pool
accounting, allocating a layout that *does* have bindings fails with
`VK_ERROR_OUT_OF_POOL_MEMORY` after one wasted chained sub-pool (issue #187).
Write it in the register this file already uses for exactly this class of fact —
the "**The pool cannot check this**" remark at `:226-235`.

**Do not** add a `_budgetless` field, do not branch `CreatePool`, and do not
suppress auto-grow for an empty template (spec §E11.3).

**3e. `src/Ahjo.Vulkan/Pools/FrameRing.cs` — comments only, no behaviour change.**
`descriptorPoolSizes.IsEmpty` is FrameRing's own opt-out sentinel for "this slot
gets no descriptor pool" (`:252-254`), and `:57-63` rejects empty `poolSizes`
together with a non-zero `descriptorMaxSets` with *"pass both or neither"*. Both
stay exactly as they are. Add a comment at each site recording that the sentinel
is **FrameRing's own**, deliberately independent of `DescriptorSetPool`'s
now-relaxed guard, and that redefining it to mean "give every slot a budget-less
pool" would be a behaviour change on a per-frame path (spec §E11.5). No
executable code changes in this file.

## 4. `src/Ahjo.Vulkan.Slang/SlangVulkanMapping.cs` — retire the #183 workaround

Four edits in this file.

**4a. `MapBindings(this ReadOnlySpan<SlangDescriptorBinding>)` (`:310-333`).**
Delete the refusal:

```csharp
if (bindings.Length != 0 && kept == 0)
{
    throw new NotSupportedException(EmptySetMessage(bindings));
}
```

`int kept = CountMappable(bindings);` stays — it still sizes `result`. With the
refusal gone the method returns `new DescriptorBinding[0]` for an all-zero-count
set, which is the `return []` the docs promised.

**4b. `MapBindings(this ReadOnlySpan<SlangDescriptorBinding>, SlangUnboundedCapacity)`
(`:379-408`).** Delete the identical block at `:387-390`. Keep the
`ArgumentNullException.ThrowIfNull(capacity)` at `:383`.

**4c. Delete `EmptySetMessage` (`:492-518`) entirely**, and with it the now-unused
`using System.Text;` at line 2 — `StringBuilder` is used nowhere else in the file
(verified: the only occurrence is `:500`). `TreatWarningsAsErrors=true` +
`AnalysisLevel=latest` will otherwise fail the build on the unused import.
`using System;` at line 1 stays (`NotSupportedException`, `ArgumentException`).

**4d. XML docs on both `MapBindings` overloads.** In each, replace the
"When *every* binding of a non-empty set is zero-count …" paragraph
(`:296-302` and `:363-369`) with a paragraph saying the result may be **empty**,
that this is the correct layout for a set whose every binding is a zero-length
array, and that `Device.CreateDescriptorSetLayout` accepts an empty `Bindings`
span (issue #191). Then narrow the `<exception cref="NotSupportedException">`
tags at `:304-309` and `:375-378` — drop the "or every binding of a non-empty set
declares zero descriptors" clause from both; what remains is the unmappable-Slang-type
case and (for the parameterless overload) the unsized-binding case.

**Do not touch** `MapBinding(binding)` (`:180-202`) or
`MapBinding(binding, descriptorCount)` (`:234-263`), their `ZeroCountMessage`
(`:448-456`) or `ZeroCountCapacityMessage` (`:464-471`). Those refusals are #183
§D3/D4 and are unaffected: a method returning one `DescriptorBinding` still has
no value meaning "nothing". Their XML at `:166-173` mentions that
`MapBindings` "can express it, and omits the binding" — still true, no edit
needed.

## 5. `src/Ahjo.Vulkan.Slang/SlangReflection.cs` — the loop now has an else branch

Rewrite the `<remarks>` on `SetLayoutSlotCount` (`:172-200`):

- Extend the code sample at `:177-186` with the branch that was impossible:

```
/// for (uint set = 0; set &lt; reflection.SetLayoutSlotCount; set++)
/// {
///     layouts[set] = reflection.TryGetSet(set, out ReadOnlySpan&lt;SlangDescriptorBinding&gt; bindings)
///         ? device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription { Bindings = bindings.MapBindings() })
///         : device.CreateDescriptorSetLayout(default);   // the hole: a layout with zero bindings
/// }
```

- Replace the "**A `false` from `TryGetSet` has no answer in the wrapper today.**"
  paragraph (`:187-196`) with one saying a `false` is filled by a layout with
  zero bindings, which `Device.CreateDescriptorSetLayout` produces from an empty
  `Bindings` span (issue #191). Keep the closing sentence about not renumbering
  sets to be dense (`:197-199`) verbatim.

Also update the `TryGetSet` remark at `:247-250` — it already says "a gap the
caller fills with an empty descriptor set layout", which becomes true rather than
aspirational; make sure it does not still read as a forward reference to
something unavailable.

## 6. `src/Ahjo.Vulkan.Slang/CLAUDE.md` — three edits

**6a. Delete the "### Known gap: no zero-binding descriptor set layout" section
(`:324-343`) in full.** It is four paragraphs whose entire subject is the defect
this change removes. Do not replace it with a "this used to be a gap" note in
the same place; the fact belongs where a reader needs it, which is rule 11 and
step 5's XML.

**6b. Rule 11 (`:275-301`).** Two changes:
- The clause at `:295-301` ("Emitting `descriptorCount = 0` is *not* an option
  here anyway … **do not 'fix' that guard**") stays **verbatim** — it is about
  #119's `Count` sentinel, a different number (spec §E10), and is still binding.
- Append one sentence to the rule recording that a set whose every binding is
  zero-count now maps to an **empty** `DescriptorBinding[]`, which
  `Device.CreateDescriptorSetLayout` accepts (issue #191), and that the three
  entry points still agree: `MapBindings` omits, `MapBinding` refuses,
  `MapBinding(binding, count)` refuses.

**6c. Fix the stale parenthetical at `:313-317`.** The sentence describes
`Device.CreateDescriptorSetLayout` as the method "which validates only that the
`Bindings` span is non-empty" — already wrong since #192, doubly so now. Keep the
sentence's point (a duplicate `(set, slot)` reaches Vulkan unchecked by the
wrapper, #180's OPEN-1) and correct the parenthetical to say what the method
actually validates: the variable-descriptor-count ordering
(VUID-…-pBindingFlags-03004) and nothing about slot uniqueness.

## 7. `src/Ahjo.Vulkan.Slang/README.md` — retire the "Open gap" blockquote

Delete the `> **Open gap.** …` blockquote at `:598-605` and replace it with a
plain paragraph in the "Set indices are set numbers, not positions" section
stating that a hole is filled with a zero-binding layout:
`device.CreateDescriptorSetLayout(default)`. Show the two-branch loop from step 5
if the surrounding prose reads better with it. The recipe block at `:166-172`
needs no change — `MapBindings()` returning an empty array flows through it
unmodified, which is the point.

## 8. Tests

Windows-only suite (issue #32). Every device-touching test gates on
`TestGate.RequireDriver()`; the validation assertions additionally condition on
`VulkanDriverProbe.HasValidationLayer` in the wrapper suite (the pattern at
`tests/Ahjo.Vulkan.Tests/MemoryAliasingTests.cs:112-124`) or
`VulkanEnvironment.HasValidationLayer` in the Slang suite (the pattern at
`tests/Ahjo.Vulkan.Slang.Tests/SlangReflectionTests.cs:1727-1744`).

### 8a. `tests/Ahjo.Vulkan.Tests/PipelineLayoutTests.cs` — three new tests

Use the existing private `CreateGraphicsDevice(instance)` helper at `:239`.

| test | shape | assertions |
|---|---|---|
| `DescriptorSetLayout_EmptyBindings_CreatesZeroBindingLayout` | `new DescriptorSetLayoutDescription { Bindings = [] }` | handle is non-null; `Dispose` does not throw |
| `DescriptorSetLayout_DefaultDescription_CreatesZeroBindingLayout` | `device.CreateDescriptorSetLayout(default)` | handle is non-null — the #119 valid-by-default convention applied to this description |
| `PipelineLayout_SparseSets_FillsTheHoleWithAnEmptyLayout` | three layouts: set 0 = one `UNIFORM_BUFFER` binding, set 1 = **empty**, set 2 = one `SAMPLED_IMAGE` binding; then `CreatePipelineLayout` with `SetLayouts = [l0, l1, l2]` | all three handles non-null and **distinct**; `pipelineLayout.IsNull` is false; when validating, the error sink is empty (message must dump the collected strings, like `SlangReflectionTests.cs:1819-1822`) |

The third test is the acceptance criterion for the issue's shape 1 and needs no
Slang. Assert the middle handle is non-null explicitly — an empty layout is a
*real* layout, not `VK_NULL_HANDLE`, and a reader will wonder.

**Do not** add a test asserting `DescriptorTemplate<T>` still refuses an empty
binding span unless one is missing; check
`tests/Ahjo.Vulkan.Tests/DescriptorTemplateTests.cs` first and add
`DescriptorTemplate_EmptyBindings_Throws` (asserting the message contains
`descriptorUpdateEntryCount`) only if no equivalent exists.

### 8b. `tests/Ahjo.Vulkan.Slang.Tests/SlangReflectionTests.cs` — rewrite two, add two

- **`MapBindings_EverythingZeroCount_ThrowsNamingTheGap` (`:463-482`)** →
  rename to `MapBindings_EverythingZeroCount_ReturnsEmpty`. Keep the fixture
  (`ShaderFixtures.ReflectionZeroLengthArrayOnly`) and the two reflection
  assertions at `:470-472` — reflection still reports the binding, that is its
  job. Replace the `Assert.Throws<NotSupportedException>` with
  `Assert.Empty(((ReadOnlySpan<SlangDescriptorBinding>)copy).MapBindings())`.
  Rewrite the doc comment: the gap is closed, and the empty array is the layout
  that matches SPIR-V decorating nothing.
- **`MapBindings_ZeroCountInItsOwnSet_StillMapsTheOtherSet` (`:488-518`)** —
  keep the first half unchanged (live set still maps to two bindings at slots 0
  and 1). Replace the second half's `Assert.Throws` (`:506-517`) with
  `Assert.Empty(...)` on the dead set. The comment at `:512-515` explaining why
  the assertion discriminates needs rewriting: the discriminator is now that the
  dead set maps to **zero** bindings rather than to one binding of count 0 or
  count 1.
- **New: `MapBindings_WithResolver_EverythingZeroCount_ReturnsEmptyWithoutAsking`.**
  Same fixture as the first, through the `SlangUnboundedCapacity` overload; assert
  the result is empty **and** the resolver's call counter is 0. Mirrors the
  existing `MapBindings_WithResolver_OmitsZeroCountWithoutAskingTheResolver`
  (`:433-455`) and covers the second refusal site deleted in step 4b, which
  otherwise has no test.
- **New, driver-gated: `Reflection_SparseSets_BuildsAPipelineLayoutWithAHole`.**
  The end-to-end acceptance test. Fixture `ShaderFixtures.ReflectionSparseSets`
  (`ShaderFixtures.cs:515-524`): sets 0 and 2 populated, set 1 a hole,
  `SetLayoutSlotCount == 3` — already asserted by
  `Reflection_ExplicitVkBinding_ReportsSparseSets` (`:752-776`). Model the device
  setup on `Reflection_BuildsAWorkingPipelineLayout` (`:1713-1824`) but
  **without** the `ConfigureFeatures` / `shaderDrawParameters` block at
  `:1766-1779` — this fixture is fragment-only and declares no `SV_VertexID`, so
  that capability is not emitted. Run the exact two-branch loop from step 5,
  build the `PipelineLayout`, load `program.Spirv(0)` as a `ShaderModule`, and
  assert the validation error sink is empty. Dispose the layouts in a `finally`,
  as `:1809-1815` does.

### 8c. `tests/Ahjo.Vulkan.Slang.Tests/ShaderFixtures.cs` — fixture doc comment

The `<remarks>` on `ReflectionZeroLengthArrayOnly` (`:1010-1022`) claims
"`Device.CreateDescriptorSetLayout` cannot make one — it rejects an empty
`Bindings` span". Rewrite that sentence; keep the measured facts (one set, one
binding `(0,0) gTex Fixed(0)`, SPIR-V decorates nothing) and the last sentence
explaining why the fixture is deliberately not a row in
`Reflection_CoversEverySetAndBinding_TheSpirvDecorates`.

### 8d. `tests/Ahjo.Vulkan.Tests/DescriptorSetPoolTests.cs` — four new tests

They need a **zero-binding** raw layout, so add a private helper beside
`CreateUniformBufferLayout` (`:257-275`):

```csharp
private static VkDescriptorSetLayout_T* CreateEmptyLayout(Device device)
```

— the same body with `bindingCount = 0, pBindings = null`. Raw API, matching this
file's existing convention; do **not** route it through
`Device.CreateDescriptorSetLayout`, so this suite keeps testing the pool rather
than step 1.

| test | shape | assertions |
|---|---|---|
| `Pool_Ctor_EmptyPoolSizes_Succeeds` | `new DescriptorSetPool(device, maxSets: 4, [])` | no throw; `PoolCount == 1`. **This test is also the measurement**: spec §E11.1 is registry-derived, and this is where `vkCreateDescriptorPool` with `poolSizeCount = 0` first meets a real driver. If it fails, **stop and report** — do not work around it. |
| `Pool_EmptyPoolSizes_AcquireZeroBindingLayout_RoundTrips` | budget-less pool + `CreateEmptyLayout` | `Acquire` non-null; `Release`; `Acquire` again returns the **same** `Handle` (the free-list bucket works for a zero-binding layout); `Reset` then `Acquire` still succeeds; validation sink empty |
| `Pool_EmptyPoolSizes_AcquireBeyondMaxSets_Grows` | `maxSets: 1`, `poolSizes: []`, two acquires of the zero-binding layout | both non-null and distinct; `PoolCount == 2`. This is the auto-grow interaction the scope call asked about, and it is testable *here* precisely because `maxSets` is the one budget this repo's driver enforces (#187, spec §E11.3). Model on `Pool_AcquireBeyondMaxSets_GrowsAndSucceeds` (`:101-130`). |
| `Pool_EmptyPoolSizes_AcquireVariableCount_Throws` | budget-less pool, `Acquire(layout, 4)` | `ArgumentOutOfRangeException`; message mentions that the pool was created with no `poolSizes` (step 3c's branch) |

**Do not write a test asserting that a budget-less pool refuses a layout that
*has* bindings.** Spec §E11.3: this repo's driver enforces `maxSets` only and will
very likely **succeed**, so such a test would assert a spec rule this hardware
does not implement. That gap is a documented limitation (spec Risk 4), not a
coverage hole to be filled with a wrong test.

### 8e. Red-ability

Each new or rewritten test must be shown red by a named mutation before the PR.
Predicted mutations — **verify each; correct the plan's prediction if measurement
disagrees, and say so in the PR** (two of #183's predictions were wrong):

| test | mutation that must make it red |
|---|---|
| the three in 8a | restore step 1's guard |
| `MapBindings_EverythingZeroCount_ReturnsEmpty` | restore step 4a's refusal |
| `MapBindings_WithResolver_EverythingZeroCount_…` | restore step 4b's refusal (this is the one that discriminates 4a from 4b) |
| `MapBindings_ZeroCountInItsOwnSet_StillMapsTheOtherSet` | restore step 4a's refusal |
| `Reflection_SparseSets_BuildsAPipelineLayoutWithAHole` | restore step 1's guard |
| `Pool_Ctor_EmptyPoolSizes_Succeeds` | restore step 3a's guard |
| `Pool_EmptyPoolSizes_AcquireZeroBindingLayout_RoundTrips` | restore step 3a's guard |
| `Pool_EmptyPoolSizes_AcquireBeyondMaxSets_Grows` | construct the pool with `growOnExhaustion: false` — proves the assertion is about growth, not about `maxSets` being ignored by the driver |
| `Pool_EmptyPoolSizes_AcquireVariableCount_Throws` | change `:266` to `if (_poolSizes.Length != 0 && variableDescriptorCount > _maxPerTypeDescriptorTotal)` — exempting the empty template is the plausible wrong implementation, and this is the test that discriminates against it |

## 9. Benchmarks and verification

**A benchmark run *is* required for `DescriptorSetPool`**, because step 3c edits
`Acquire`, which is a benchmarked hot path:

```bash
dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter "*DescriptorSetPool*"
```

Compare against `docs/benchmarks.md:89-91` (`AcquireReleaseReset_Cycle` 39.62 ns,
`…_VariableCount_Cycle` 88.23 ns, `…_TwoCounts_Cycle` 221.69 ns). The `Allocated`
column **must** stay `-` and the means must stay within noise — the edit is inside
an already-throwing branch, so the success path should execute the same
instructions. If either moves, the message was built before the comparison rather
than inside the throw (step 3c); fix that rather than re-baselining. Update
`docs/benchmarks.md` **only** if a mean genuinely moved and the cause is
understood; a re-measurement within noise is left as captured, per the note
already on the #182 row.

**No run is needed for the rest, and the PR body should say why**, because the
diff also touches `Pools/DescriptorTemplate.cs` and `bench-coverage-checker` will
ask: the only change there is the text of an exception thrown from
`DescriptorTemplateBuilder.BuildEntries`, a setup-time one-shot
(`DescriptorTemplate.cs:80-84`). `DescriptorTemplate<T>.Update` (`:65-71`) is
untouched, so the `PushDescriptors.*` row at `docs/benchmarks.md:88` keeps its
meaning. `Device.CreateDescriptorSetLayout` appears in benchmarks only inside
`[GlobalSetup]` (4 sites, spec §E4).

**Verification to run and quote in the PR:**

```bash
dotnet build Ahjo.Vulkan.slnx                                   # 0 warnings (TreatWarningsAsErrors)
AHJO_VULKAN_TIER=validation dotnet test tests/Ahjo.Vulkan.Tests
AHJO_VULKAN_TIER=validation dotnet test tests/Ahjo.Vulkan.Slang.Tests
```

Quote `VulkanTierContractTests`' `declared=… observed=…` line — the validation
assertions in 8a and 8b are meaningless without it, and "N passed" without a tier
is indistinguishable from N skips (`tests/CLAUDE.md`). Also report the counts of
passed/skipped, as `f0c6e9b`'s commit message does.

Run `vulkan-validation-reviewer` before the PR (the diff touches `Pools/` and
`Recording/`-adjacent public API).

Commit style: `Device: accept an empty descriptor set layout (#191)`, body
naming the retired #183 workaround and the pool relaxation, closing with
`Closes #191`. The body must also state, in one sentence, that this **widens
#187's leaked-sub-pool residue by a new route and does not close it** (spec
Risk 4) — that is the one honest caveat of this change, and it should not have to
be discovered from the diff.

---

## OPEN

**None.** The spec's original OPEN-1 was resolved **INCLUDE** by the maintainer,
against the spec's recommendation, and is now step 3.

Two items are *scoped out* rather than open — do not read them as invitations:

- **#187's sub-pool rollback.** The ~5-line fix that would close both routes into
  the leaked-sub-pool residue. Its own issue, its own PR, untestable on this
  driver (spec §E11.3).
- **`FrameRing`'s empty-span opt-out.** Comments only (step 3e); redefining it is
  a behaviour change on a per-frame path.

One item must **stop the work and go back to the maintainer** rather than be
routed around: if `Pool_Ctor_EmptyPoolSizes_Succeeds` (step 8d) fails — i.e.
`vkCreateDescriptorPool` rejects `poolSizeCount = 0` on the test box — that
contradicts spec §E11.1, which is registry-derived rather than measured. Report
it; do not add a workaround, and do not delete the test.
