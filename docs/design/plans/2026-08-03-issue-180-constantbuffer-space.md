Paired with [../specs/2026-08-03-issue-180-constantbuffer-space-design.md](../specs/2026-08-03-issue-180-constantbuffer-space-design.md).

# Plan — repair the descriptor set of a global `ConstantBuffer<T>` with an explicit `[[vk::binding(n, space)]]` (#180)

**Revised 2026-08-03 (twice).** **R1** — Groups A and C implemented; Group B
dropped by maintainer decision; Group D specified as new work. **R2** — Group D is
now implemented and green too (120/120 Slang suite, 532 passed / 10 skipped
repo-wide, 0 warnings); steps 11, 12e and 13b carried slot numbers measured against
*different* modules than the fixtures they specified, and are corrected below. See
the spec's §Correction record and §E11.

**Nothing here is outstanding work.** This plan is now a record of what was
executed. The R2 corrections are to its *rationale*, so a reader who returns to it
is not taught a wrong placement rule.

Step numbers are **not** renumbered: steps 1–8 remain so the reviewer can see what
was decided and what was executed. Do not re-do them.

| Group | Steps | State |
|---|---|---|
| **A** — the space correction | 1–3 | **DONE.** `CollectSpaceCorrections` + `SpaceOf` in `SlangReflection.cs`; both readers consume it. |
| **B** — duplicate-slot guard | 4 | **DROPPED by maintainer decision. Do not implement.** |
| **C** — fixtures, tests, docs | 5–8 | **DONE.** Four fixtures, four tests, two theory extensions, `CLAUDE.md` rule 9. 108/108 green; both mutations behaved as predicted (11 red on the correction mutation, exactly 2 on the span-rule mutation). |
| **D** — unwrap the wrapped global scope | 9–14 | **DONE.** `UnwrapGlobalScope` + `ImplicitGlobalParamsName`; four fixtures, five test items, `CLAUDE.md` rule 10. 120/120 Slang suite green. Mutation 13b produced exactly the designed asymmetry (12c red, 12d/12e green). |

Files Group D touched, and no others:

- `src/Ahjo.Vulkan.Slang/SlangReflection.cs`
- `src/Ahjo.Vulkan.Slang/CLAUDE.md`
- `tests/Ahjo.Vulkan.Slang.Tests/ShaderFixtures.cs`
- `tests/Ahjo.Vulkan.Slang.Tests/SlangReflectionTests.cs`

Nothing in `src/*/Generated/`, `native/`, `src/Ahjo.Vulkan/` or
`docs/benchmarks.md` changes. Every native symbol used below already exists in
`src/Ahjo.Vulkan.Slang.Native/Generated/` — each is cited at its step.

**No benchmark, no `docs/benchmarks.md` row.** `src/Ahjo.Vulkan.Slang/CLAUDE.md`
forbids both for this project: reflection is setup-time and invariant #3 does not
apply. Invariants #2 (Native AOT — direct calls, arrays and dictionaries only)
and #5 (`TreatWarningsAsErrors`) do.

**Step 13 was not skipped**, and it is what caught the slot numbers this plan had
wrong. A test that passes against a deliberately broken implementation is
worthless; #179 shipped one.

---

## Groups A–C — already implemented (steps 1–8, for reference only)

<details>
<summary>What was done, so a reviewer can check the tree against the decision</summary>

- **1–2.** `CollectSpaceCorrections(SlangReflectionTypeLayout*)` returning
  `Dictionary<(long Set, long Range), uint>?`, guarded on
  `getKind == SLANG_TYPE_KIND_STRUCT`, walking each field's binding-range span
  `[getFieldBindingRangeOffset(f), getFieldBindingRangeOffset(f+1))` and recording
  the field's `GetSpace(…, DESCRIPTOR_TABLE_SLOT)` only where it differs from the
  record's own space offset. Called at the top of `Walk`, scope-local, passed down
  — deliberately not in `WalkState`.
- **3.** `SpaceOf(corrections, s, r, spaceOffset)` consumed by step 1 (with `vkSet`
  moved inside the range loop) and by `CollectBindingRangeFacts`.
- **4.** **DROPPED.**
- **5–7.** Fixtures `ReflectionExplicitSpaceConstantBuffer`,
  `ReflectionExplicitSpaceMixed`, `ReflectionExplicitSpaceTwoConstantBuffers`,
  `ReflectionExplicitSpaceStructGlobal`; rows added to
  `Reflection_CoversEverySetAndBinding_TheSpirvDecorates` and
  `Reflection_BindingNames_MatchTheSpirvVariableNames`; four focused tests; both
  falsifiability mutations run and recorded.
- **8.** `src/Ahjo.Vulkan.Slang/CLAUDE.md` rule 9; `ReflectionBindlessArrays` doc
  comment rewritten to point at the new fixture.

</details>

---

## Group D — unwrap the constant-buffer-wrapped global scope

Spec §D3, §E7–E12. The trigger: once a module declares loose ordinary data at file
scope (`float4 gTint;`), `spReflection_getGlobalParamsTypeLayout` returns a
**`SLANG_TYPE_KIND_CONSTANT_BUFFER`** wrapper instead of a
`SLANG_TYPE_KIND_STRUCT`. Its descriptor-set records are unusable (spec §E8) and
Group A's kind guard short-circuits, so nothing is repaired.

**What breaks depends on the shape** (spec §E12, measured after implementation —
R1 generalized from one module and got this wrong): every binding comes back
**unnamed** in all cases, and on top of that an explicitly-bound module gets three
bindings at one slot and loses a set, while a module with a `ParameterBlock` loses
the block's binding entirely. A module whose resources are all auto-bound gets the
right slots and only loses the names.

### 9. New private helper in `SlangReflection.cs`: `UnwrapGlobalScope`

Add next to `CollectSpaceCorrections`:

```csharp
private static SlangReflectionTypeLayout* UnwrapGlobalScope(
    SlangProgramLayout* layout,
    SlangReflectionTypeLayout* globalScope,
    WalkState state)
```

Returns the scope `Walk` should actually be given. Body:

1. `if (SlangApi.spReflectionTypeLayout_getKind(globalScope) != SlangTypeKind.SLANG_TYPE_KIND_CONSTANT_BUFFER) return globalScope;`
   **Test for `== CONSTANT_BUFFER`, never for `!= STRUCT`.** Spec §E7 measured that
   the wrapper's kind is exactly `SLANG_TYPE_KIND_CONSTANT_BUFFER` and that an
   interface scope is *never* the global scope. A `!= STRUCT` test would admit
   kinds nobody has measured and would put the field/binding-range call family one
   step away from the `0xC0000005` in issue #181.
2. `SlangReflectionTypeLayout* element = SlangApi.spReflectionTypeLayout_GetElementTypeLayout(globalScope);`
   If `element == null`, throw `NotSupportedException`:
   > "Slang reports the global scope as a constant buffer but exposes no element
   > type layout for it, so the parameters inside it cannot be reflected at all.
   > Reflect a fully specialized program."
3. `SlangReflectionVariableLayout* globalVar = SlangApi.spReflection_getGlobalParamsVarLayout(layout);`
   If `globalVar == null`, throw `NotSupportedException` naming
   `spReflection_getGlobalParamsVarLayout` and stating that the implicit global
   constant buffer's descriptor location is not derivable without it.
4. Read the two quantities (spec §E9 — measured against `OpDecorate` in **14 of
   14** shapes, counting R2's re-measurement):

   ```csharp
   nuint rawSpace = SlangApi.spReflectionVariableLayout_GetSpace(
       globalVar, SlangParameterCategory.SLANG_PARAMETER_CATEGORY_DESCRIPTOR_TABLE_SLOT);
   nuint rawSlot = SlangApi.spReflectionVariableLayout_GetOffset(
       globalVar, SlangParameterCategory.SLANG_PARAMETER_CATEGORY_DESCRIPTOR_TABLE_SLOT);
   ```

   If either is `> uint.MaxValue`, **throw** `NotSupportedException` — not
   `continue`, not a fallback to 0. This follows the all-or-nothing posture the
   file already states for an index offset (`SlangReflection.cs:481-487`): a
   binding with no binding number has no layout to report. Message shape:
   > "The implicit global constant buffer reports descriptor space {rawSpace} and
   > binding {rawSlot}; one of these is Slang's sentinel for a value that depends
   > on unresolved generic parameters or link-time constants rather than a
   > descriptor location. Reflect a fully specialized program."
5. Add the binding and its buffer layout to `state`:

   ```csharp
   state.Pending.Add(new PendingBinding(
       (uint)rawSpace,
       new SlangDescriptorBinding
       {
           Slot = (uint)rawSlot,
           Name = ImplicitGlobalParamsName,
           Count = SlangDescriptorCount.Fixed(1),

           // By construction, like step 2's: there is no binding range for a
           // buffer Slang reports no descriptor range for.
           Type = SlangBindingType.SLANG_BINDING_TYPE_CONSTANT_BUFFER,
       }));

   state.BufferLayouts.Add(
       ((uint)rawSpace, (uint)rawSlot, BuildBufferLayout(element, ImplicitGlobalParamsName)));
   ```

   `Stages` is left unset exactly as step 2 leaves it; `ApplyStages` fills it in.
   `IsSpecializable` stays `false` — there is no binding range to ask (OPEN-4).
6. `return element;`

Add the name constant next to `MaxMemberDepth` (`SlangReflection.cs:53`):

```csharp
/// <summary>
/// The name reported for the implicit constant buffer Slang synthesizes around a
/// global scope that carries loose ordinary data.
/// </summary>
/// <remarks>
/// <b>Chosen here, not read from Slang.</b> Measured on <c>v2026.14.1</c> /
/// win-x64: <c>spReflectionVariableLayout_GetVariable</c> on the global params
/// var layout is <see langword="null"/> and both the wrapper's and the element's
/// type names are <see langword="null"/>, so Slang supplies no name at all — but
/// the emitted SPIR-V decorates the variable <c>OpName "globalParams"</c> in
/// every shape probed, so matching it is what lets a caller correlate the binding
/// with a disassembly. Unlike a set or a slot, this name is cosmetic: nothing a
/// driver sees depends on it.
/// </remarks>
private const string ImplicitGlobalParamsName = "globalParams";
```

`BuildBufferLayout` already drops the resource fields — spec §E9 measured
`gAlbedo` / `gXform` / `gMat` at `GetSize(UNIFORM) = 0`, which the existing
zero-size rule at `SlangReflection.cs:1101-1105` skips — so the layout comes out
as just the loose members (`gTint` at offset 0, size 16).

### 10. Call it from the constructor

In `SlangReflection`'s constructor, replace the current top-level `Walk` call
(`SlangReflection.cs:110-116`) with:

```csharp
// setOf(global scope) = 0. Not "the global scope owns set 0" — …
// (keep the existing comment verbatim, and add the sentence below)
Walk(
    UnwrapGlobalScope(layout, SlangApi.spReflection_getGlobalParamsTypeLayout(layout), state),
    absoluteSet: 0,
    isParameterBlockElement: false,
    scopeName: string.Empty,
    scopeIsSpecializable: false,
    state);
```

`isParameterBlockElement` stays **`false`**, which is the load-bearing part.
Passing `true` would make step 2 synthesize a second buffer at **slot 0** of the
set — spec §D3 measured that the global scope's implicit buffer goes at slot **1**
or **2**, not 0, and that the element's own indices are *not* shifted up the way a
block's are. Extend the existing step 2 comment at `SlangReflection.cs:526-529`
("The global scope does NOT share this asymmetry…") with one sentence recording
that a *wrapped* global scope is a third case again: its implicit buffer is listed
nowhere and is synthesized by `UnwrapGlobalScope` at the slot
`getGlobalParamsVarLayout` reports.

**Do not apply the unwrap in step 3.** Every `ParameterBlock` element scope
measured is `STRUCT` (or `INTERFACE` for the conformance-linked existential case),
never `CONSTANT_BUFFER`; widening it there would be designing against a shape
nobody has seen. Spec §D3's last rejected alternative.

### 11. Three new fixtures in `tests/Ahjo.Vulkan.Slang.Tests/ShaderFixtures.cs`

Same rule as the existing ones (`ShaderFixtures.cs:277-282`): every parameter must
be *read* by an entry point, or the optimizer removes it and the SPIR-V oracle has
nothing to assert against.

**`ReflectionLooseGlobalsWithExplicitSpace`** — the reviewer's module, the one
that motivates Group D:

```hlsl
struct Xform { float4x4 mvp; };

float4 gTint;

[[vk::binding(0, 0)]] Texture2D<float4>     gAlbedo;
[[vk::binding(0, 1)]] ConstantBuffer<Xform> gXform;

[shader("fragment")]
float4 fragmentMain() : SV_Target
{
    return gAlbedo.Load(int3(0, 0, 0)) * gXform.mvp[0] * gTint;
}
```

Doc comment: `float4 gTint;` is the trigger — it makes
`spReflection_getGlobalParamsTypeLayout` return a
`SLANG_TYPE_KIND_CONSTANT_BUFFER` wrapper instead of a struct. Measured SPIR-V:
`gAlbedo (0,0)`, `globalParams (0,1)`, `gXform (1,0)`. Before Group D reflection
reported three bindings all at slot 0 of set 0, all unnamed, and no set 1.

**`ReflectionLooseGlobalsOnly`** — the degenerate control:

```hlsl
float4 gTint;
float  gScale;

[shader("fragment")]
float4 fragmentMain() : SV_Target
{
    return gTint * gScale;
}
```

Doc comment: the wrapper with nothing else in it. SPIR-V puts `globalParams` at
`(0,0)`, which is what reflection reported *by accident* before Group D — so this
fixture's assertion is carried by the **buffer layout** (two members, `gTint` at 0
and `gScale` at 16), not by the set number. Step 13b depends on that asymmetry.

**`ReflectionLooseGlobalsNoExplicitBinding`** — proof that Group D is not about
`[[vk::binding]]`:

```hlsl
float4 gTint;

Texture2D    gAlbedo;
SamplerState gSampler;

[shader("fragment")]
float4 fragmentMain(float2 uv : TEXCOORD0) : SV_Target
{
    return gAlbedo.Sample(gSampler, uv) * gTint;
}
```

Doc comment: **no explicit binding anywhere.** Measured SPIR-V:
`globalParams (0,0)`, `gAlbedo (0,1)`, `gSampler (0,2)` — the implicit buffer takes
slot 0 and pushes the resources up, because nothing claims slot 0 (spec §E11).

**R2 correction.** R1 specified this fixture with `gAlbedo (0,0)`,
`gSampler (0,1)`, `globalParams (0,2)`. Those numbers were measured against a
*different* module — one carrying `[[vk::binding(0,0)]]` and `[[vk::binding(1,0)]]`
— and the attributes were dropped when this fixture's text was written, which moves
the implicit buffer from slot 2 to slot 0. The implementation measured the fixture
correctly and the shipped doc comment is right; this is the plan catching up.

R1's claim that reflection "put the implicit buffer at `(0,0)`, duplicating the
texture's slot" is wrong for this module too: the pre-Group-D walk produced
`(0,0)`, `(0,1)`, `(0,2)` — **the slots were already correct here** — while every
binding came back **unnamed** (spec §E12). The fixture still makes the point it
exists for, that the wrapper defect is independent of `[[vk::binding]]`; the defect
it exhibits is missing names, not colliding slots.

### 12. Tests in `tests/Ahjo.Vulkan.Slang.Tests/SlangReflectionTests.cs`

**12a. Extend the SPIR-V cross-check theory.** Three rows on
`Reflection_CoversEverySetAndBinding_TheSpirvDecorates`
(`SlangReflectionTests.cs:54-66`, plus Group C's four):

```csharp
[InlineData("xcheckLooseSpace", ShaderFixtures.ReflectionLooseGlobalsWithExplicitSpace)]
[InlineData("xcheckLooseOnly", ShaderFixtures.ReflectionLooseGlobalsOnly)]
[InlineData("xcheckLoosePlain", ShaderFixtures.ReflectionLooseGlobalsNoExplicitBinding)]
```

This is the existing oracle and the primary red-able assertion: today
`ReflectionLooseGlobalsWithExplicitSpace` reports no set 1 while SPIR-V binds
`gXform` there.

**12b. Extend the name oracle.** Add the same three rows to
`Reflection_BindingNames_MatchTheSpirvVariableNames`
(`SlangReflectionTests.cs:699-703`). All bindings here are global-scope
declarations, so the leaf-vs-path qualification exclusion documented at
`:694-698` does not apply. **This is the test that pins
`ImplicitGlobalParamsName`** — SPIR-V's `OpName` for the implicit buffer is
`globalParams` — and it is also what catches today's empty names on every binding
in these modules.

**12c. New `Reflection_LooseGlobalUniforms_ReportTheImplicitBufferAndTheRest`.**
Against `ReflectionLooseGlobalsWithExplicitSpace`:

- `DescriptorSetCount == 2`, `SetIndex(0) == 0`, `SetIndex(1) == 1`,
  `SetLayoutSlotCount == 2u`.
- `TryGetSet(0, …)` → **exactly two** bindings: slot 0
  `SLANG_BINDING_TYPE_TEXTURE` named `gAlbedo`, slot 1
  `SLANG_BINDING_TYPE_CONSTANT_BUFFER` named `globalParams`. Assert the length —
  that is what fails on today's three-at-slot-0.
- `TryGetSet(1, …)` → exactly one binding, slot 0,
  `SLANG_BINDING_TYPE_CONSTANT_BUFFER`, named `gXform`. This is the assertion that
  proves Group A's correction resumes working once the scope is unwrapped.
- `TryGetBufferLayout(0, 1, out SlangBufferLayout? loose)` is `true`, with exactly
  one member named `gTint` at `Offset == 0`.
- `TryGetBufferLayout(1, 0, out SlangBufferLayout? xform)` is `true`, with a member
  named `mvp`.
- `TryGetBufferLayout(0, 0, out _)` is `false` — the texture's slot owns no buffer.
- Close with `AssertReflectionCoversSpirv(reflected.Program, reflection, _output)`.

**12d. New `Reflection_LooseGlobalUniformsOnly_ReportOneBufferWithBothMembers`.**
Against `ReflectionLooseGlobalsOnly`: `DescriptorSetCount == 1`,
`SetIndex(0) == 0`, one binding at slot 0 named `globalParams` of type
`SLANG_BINDING_TYPE_CONSTANT_BUFFER`; `TryGetBufferLayout(0, 0, …)` yields
`Size == 32` and two members, `gTint` at offset 0 and `gScale` at offset 16. **The
member offsets are the assertion here, not the set number** — see step 13b.

**12e. New `Reflection_LooseGlobalUniforms_DoNotNeedAnExplicitBinding`.**
Against `ReflectionLooseGlobalsNoExplicitBinding`: one set, three bindings —
`(0,0)` `CONSTANT_BUFFER` `globalParams`, `(0,1)` `TEXTURE` `gAlbedo`, `(0,2)`
`SAMPLER` `gSampler`.

**R2 correction — the discriminator is the names, not the slots.** R1 said "the
slot-2 assertion is the point: it fails today with the implicit buffer reported at
slot 0". Wrong twice over: the implicit buffer *belongs* at slot 0 in this module
(spec §E11), and the pre-Group-D walk already produced the correct triple of slots
(spec §E12). What it did not produce was any name — the wrapper exposes one binding
range with a `null` leaf variable, so every binding came back with an empty `Name`.
Therefore:

- 12e goes **red under mutation 13a** (unwrap removed) on the **name** assertions,
  and on its 12b row against the SPIR-V `OpName` oracle.
- 12e stays **green under mutation 13b** (slot hard-coded to 0), because 0 is the
  correct slot here — the same as 12d.

The test is worth keeping: it is the only fixture proving the wrapper defect reaches
a module with no `[[vk::binding]]` at all. But it must assert the names, and its doc
comment must say that the names are what carry it. The implementer kept it on
exactly those grounds.

**12f. Stage attribution — measure, do not assume.** Extend 12c with a
`GetReflection(SlangStageAttribution.PerEntryPointUsage)` and assert only that the
`globalParams` binding's `Stages` is **not** `ShaderStages.None`. Then **write the
observed value into the test's doc comment and the PR description.** Spec
§Bounded uncertainties records that `IMetadata::isParameterLocationUsed`
(`SlangReflection.cs:915-919`) was never measured against a synthesized binding;
the fallback at `:943` bounds the failure mode to "wider than necessary", never
illegal, so a wide answer is acceptable and a narrow one is a bonus — but it must
be recorded rather than guessed. Do the same for Group A's corrected set if that
was not already recorded.

### 13. Falsifiability — prove each new test can go red

Do this before opening the PR and put the results in the PR description.

**13a. Mutate the unwrap away.** Make `UnwrapGlobalScope` return `globalScope`
unconditionally (delete the `if` body, keep the method). Run
`dotnet test tests/Ahjo.Vulkan.Slang.Tests`. Expected red, and **record which
tests fail and on which assertion**: the three 12a rows, the three 12b rows, and
12c / 12d / 12e. If any of those nine passes, the test is not testing what it
claims — strengthen it before reverting.

Revert.

**13b. Mutate the synthesized slot.** Keep the unwrap but hard-code `Slot = 0`
instead of `(uint)rawSlot`. Expected — **corrected in R2, and this is what the
implementer observed**: **12c goes red; 12d and 12e stay green.**

Only `ReflectionLooseGlobalsWithExplicitSpace` puts the implicit buffer anywhere
other than slot 0, because it is the only fixture whose `[[vk::binding(0,0)]]`
claims slot 0 and forces the buffer to 1 (spec §E11). R1 predicted 12e would go red
here; it does not, and that prediction rested on the same wrong slot numbers step 11
carried.

The single-fixture sensitivity is the point rather than a weakness: it proves 12c
asserts the offset Slang reports rather than a constant, and it is why
`ReflectionLooseGlobalsWithExplicitSpace` cannot be dropped from the suite. If 12d
or 12e also goes red, something else broke; stop and report rather than adjusting a
test to match.

Revert.

**13c. Mutate the kind test.** Change
`== SlangTypeKind.SLANG_TYPE_KIND_CONSTANT_BUFFER` to
`!= SlangTypeKind.SLANG_TYPE_KIND_STRUCT`. Expected: **still green**, because spec
§E7 measured that no other kind occurs as a global scope. Record that it is green
— then revert anyway. The narrower test is the one that is *safe*, not the one
that is *necessary*, and step 9.1's comment must say so: a future Slang that
returns some third kind must fall through to today's path rather than into a
`GetFieldCount` on a non-struct (issue #181).

**13d. Confirm the interface scope is still excluded.**
`Reflection_ConformanceLinkedInterfaceBlock_ReportsUniformBufferOnly` must be green
throughout 13a–13c and in the final state. Quote its result in the PR description;
it is the test standing between this change and #181's access violation.

### 14. Docs

**14a. `src/Ahjo.Vulkan.Slang/CLAUDE.md`** — add **rule 10** after the rule 9 that
Group C added:

> **10. The global scope is not always a struct.** Measured on `v2026.14.1` /
> win-x64: as soon as a module declares loose ordinary data at file scope
> (`float4 gTint;`), `spReflection_getGlobalParamsTypeLayout` returns a
> **`SLANG_TYPE_KIND_CONSTANT_BUFFER`** wrapper whose element is the real struct
> scope. **The wrapper's descriptor-set records are unusable** — they list the
> element's ranges *plus* the implicit buffer, with no constant offset between the
> two index spaces (`+1` for one record and `+0` for another in the same module),
> and they report the implicit buffer's index offset as a constant `0`, which is
> right only by coincidence. `UnwrapGlobalScope` therefore discards them, walks the
> element, and synthesizes the implicit buffer from
> `spReflection_getGlobalParamsVarLayout`'s `GetSpace`/`GetOffset` under
> `DESCRIPTOR_TABLE_SLOT`, which matched `OpDecorate` in **14 of 14** shapes.
> `spReflection_getGlobalConstantBufferBinding` does **not** work — also a constant
> `0`. **Where the buffer lands** (measured, descriptive — do not encode it): *the
> implicit buffer is allocated first, at the lowest binding in space 0 that no
> explicit `[[vk::binding]]` has claimed, and the automatically-assigned resources
> are allocated after it.* So it is at slot 0 in a module with no explicit bindings
> — pushing the resources to 1 and 2 — and at slot 1 when `[[vk::binding(0,0)]]`
> pins something there. Declaration order is irrelevant. **Read the slot, never
> derive it**: upstream #11860 shows this allocator is being changed. **Test for
> `== CONSTANT_BUFFER`, never for `!= STRUCT`**: rule 7's call family is not total
> and issue #181 is still open, so an unmeasured kind must fall through to the
> ordinary path rather than into a `GetFieldCount`. Issue #180.

**14b.** No `docs/benchmarks.md` change, no benchmark. Forbidden for this project.

---

## Verification

```bash
dotnet build Ahjo.Vulkan.slnx
dotnet test tests/Ahjo.Vulkan.Slang.Tests
dotnet test                                   # the whole suite; nothing else should move
```

The full `Ahjo.Vulkan.Slang.Tests` suite must be green, not just the new tests.
Three existing tests carry particular weight:

- `Reflection_ConformanceLinkedInterfaceBlock_ReportsUniformBufferOnly` — the
  interface-scope guard (step 13d, issue #181).
- `Reflection_ExplicitVkBinding_ReportsSparseSets` — the `(2, 7)` control that
  Group A must stay inert against.
- Group C's four tests — Group D must not disturb the already-shipped P1 repair.

Wrapper tests are Windows-only (issue #32); nothing here needs a driver.

---

## OPEN items

- **OPEN-1 — the duplicate-slot guard.** **Closed: dropped by maintainer
  decision.** Do not reintroduce it; a reviewer who wants it back needs a new
  decision, not a re-reading of the spec. Recorded here only because the first
  draft proposed it.
- **OPEN-2 — upstream reporting.** The spec establishes that neither P1 nor P2 is
  filed upstream, that the closest relative (`shader-slang/slang#10959`) is already
  fixed in our pin, and that no newer pin exists. Filing them upstream is
  explicitly out of scope for this work; if the project wants that, it is a
  separate issue.
- **OPEN-3 — combined-shape fixtures.** Loose globals *plus* a `ParameterBlock`,
  and loose globals *plus* a push constant, were both simulated green (spec §E10)
  but are not in step 11. **Recommendation: skip them** — the three fixtures above
  already carry every assertion, and this suite's own rule is that a fixture should
  test one thing. If the implementer or a reviewer wants one, add the
  `ParameterBlock` combination rather than the push-constant one: it is the case
  where a second implicit buffer exists and the slot-0-vs-slot-N asymmetry could
  regress. **Stop and ask rather than adding both.**
- **OPEN-4 — `IsSpecializable` for the synthesized binding.** Step 9 sets it to
  `false` because there is no binding range to ask `isBindingRangeSpecializable`,
  whereas step 2 threads the declaring block's own flag down and the global scope
  has no equivalent. This is almost certainly right — a program's loose globals are
  not an existential — but it is asserted, not measured. If a reviewer wants it
  measured, the shape to probe is loose globals inside a module that also has an
  interface-typed parameter.
