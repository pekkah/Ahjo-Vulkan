Paired with [../specs/2026-08-03-issue-183-zero-descriptor-count-design.md](../specs/2026-08-03-issue-183-zero-descriptor-count-design.md).

# Plan — a zero-length resource array is not a descriptor binding (#183)

Branch: `issue-183-zero-descriptor-count`, stacked on the unpushed work of #180
and #181.

Files this touches, and no others:

- `src/Ahjo.Vulkan.Slang/SlangDescriptorCount.cs`
- `src/Ahjo.Vulkan.Slang/SlangDescriptorBinding.cs` (one XML doc paragraph)
- `src/Ahjo.Vulkan.Slang/SlangVulkanMapping.cs`
- `src/Ahjo.Vulkan.Slang/CLAUDE.md`
- `src/Ahjo.Vulkan.Slang/README.md`
- `tests/Ahjo.Vulkan.Slang.Tests/ShaderFixtures.cs`
- `tests/Ahjo.Vulkan.Slang.Tests/SlangReflectionTests.cs`

**Nothing in `src/Ahjo.Vulkan/`, `src/*/Generated/`, `native/`, `samples/` or
`docs/benchmarks.md` changes.** `SlangReflection.cs` is **not** edited — the
classification switch at `SlangReflection.cs:526-536` stays exactly as it is;
that is the spec's decision, not an oversight.

**No benchmark and no `docs/benchmarks.md` row.**
`src/Ahjo.Vulkan.Slang/CLAUDE.md:14-23` forbids both for this project: nothing
here is reachable from a frame loop. Invariants #2 (Native AOT — plain fields,
arrays, `StringBuilder`; no reflection), #4 (nothing generated is edited) and #5
(`TreatWarningsAsErrors`) do apply.

---

## 1. `SlangDescriptorCount.IsZero` — name the case

`src/Ahjo.Vulkan.Slang/SlangDescriptorCount.cs`, immediately after `IsUnbounded`
(currently line 96):

```csharp
/// <summary>
/// <see langword="true"/> when Slang stated a count and that count is
/// <c>0</c> — a zero-length resource array (<c>Texture2D gTex[0]</c>, or
/// <c>gTex[N]</c> with <c>N = 0</c>).
/// </summary>
/// <remarks>
/// Slang reserves a binding number for such a declaration and emits no
/// SPIR-V variable for it, and no shader code can index it, so it is not a
/// Vulkan descriptor binding: <c>SlangVulkanMapping.MapBindings</c> omits it
/// from the layout and <c>MapBinding</c> refuses it. Measured on
/// <c>v2026.14.1</c> / win-x64 — issue #183.
/// <para><c>default(SlangDescriptorCount)</c> satisfies this, which is why
/// <see cref="SlangDescriptorBinding"/>'s parameterless constructor supplies
/// <see cref="Fixed(uint)">Fixed(1)</see> instead (issue #119).</para>
/// </remarks>
public bool IsZero => Kind == SlangDescriptorCountKind.Fixed && _value == 0;
```

Also extend the `<remarks>` on the type (currently `:50-54`) so the sentence
"`default(SlangDescriptorCount)` is `Fixed(0)` — no descriptors" points at
`IsZero` and at the mapper's behaviour.

**OPEN-2 applies to the name.** If the maintainer prefers a different spelling,
change it here and in every use below before implementing.

## 2. `MapBinding(SlangDescriptorBinding)` refuses `Fixed(0)`

`src/Ahjo.Vulkan.Slang/SlangVulkanMapping.cs:170-184`. Signature unchanged. Add,
**before** the existing `Kind != Fixed` guard and therefore before
`MapBindingType` is ever called:

```csharp
if (binding.Count.IsZero)
{
    throw new NotSupportedException(ZeroCountMessage(binding));
}
```

Ordering is deliberate: a binding that produces no descriptor never reaches a
layout, so refusing it for an unmappable Slang *type* would be noise about a
value nobody would have emitted.

Message (`ZeroCountMessage`, new `private static string` beside
`UnsizedBindingMessage` at `:330`):

```
Descriptor binding {Slot} ('{Name}') declares zero descriptors: it is a zero-length resource array
(Texture2D gTex[0], or gTex[N] with N = 0). Slang reserves the binding number for it and emits no SPIR-V
variable, and no shader code can index it, so there is no VkDescriptorSetLayoutBinding to build — mapping it
would put a descriptor in the layout that the shader never declared. MapBindings(span) omits such bindings
from the layout it returns; skip this one, or test for it yourself with SlangDescriptorCount.IsZero.
```

`Name` is interpolated directly. It is `null` on a `default(SlangDescriptorBinding)`
and interpolates as empty — the same behaviour `UnsizedBindingMessage` already
has; do not add a null check.

Update the `<remarks>` and the `<exception cref="NotSupportedException">` tag on
the method to name the third refusal.

## 3. `MapBinding(SlangDescriptorBinding, uint)` gets a zero-specific message

`SlangVulkanMapping.cs:214-234`. Signature, exception **type** and `paramName`
are unchanged — it still throws `ArgumentException` with
`nameof(descriptorCount)`. Split the existing "already has a descriptor count"
branch so the zero case gets its own text:

```csharp
if (binding.Count.IsZero)
{
    throw new ArgumentException(ZeroCountCapacityMessage(binding, descriptorCount), nameof(descriptorCount));
}

if (binding.Count.Kind == SlangDescriptorCountKind.Fixed)
{
    // unchanged
}
```

Message (`ZeroCountCapacityMessage`, new `private static string`):

```
Descriptor binding {Slot} ('{Name}') declares zero descriptors — a zero-length resource array — so there is
nothing for this overload to size. Reserving {descriptorCount} descriptors here would put descriptors in the
layout that no shader code can index: a zero-length array cannot be indexed statically (error[E30029]) or
dynamically (error[E99997]). MapBindings(span) omits such bindings from the layout it returns.
```

`ArgumentOutOfRangeException.ThrowIfZero(descriptorCount)` at `:216` stays first,
so `MapBinding(zeroBinding, 0)` keeps reporting the caller's `0` rather than the
shader's.

## 4. `MapBindings(ReadOnlySpan<SlangDescriptorBinding>)` omits

`SlangVulkanMapping.cs:260-266`. Signature and return type unchanged
(`DescriptorBinding[]`). Two passes — count, then fill — so the array is exact
and no `List<T>` is needed:

```csharp
public static DescriptorBinding[] MapBindings(this ReadOnlySpan<SlangDescriptorBinding> bindings)
{
    int kept = CountMappable(bindings);

    if (bindings.Length != 0 && kept == 0)
    {
        throw new NotSupportedException(EmptySetMessage(bindings));
    }

    var result = new DescriptorBinding[kept];
    int n = 0;

    for (int i = 0; i < bindings.Length; i++)
    {
        if (bindings[i].Count.IsZero)
        {
            continue;
        }

        result[n++] = bindings[i].MapBinding();
    }

    return result;
}
```

`bindings.Length != 0` scopes the refusal to "input non-empty, output empty": an
empty input span keeps today's behaviour of returning an empty array.

New private helpers:

```csharp
private static int CountMappable(ReadOnlySpan<SlangDescriptorBinding> bindings);
private static string EmptySetMessage(ReadOnlySpan<SlangDescriptorBinding> bindings);
```

`EmptySetMessage` builds its binding list with a `StringBuilder` (a span cannot
be closed over, and setup-time allocation is fine here):

```
All {N} binding(s) of this descriptor set declare zero descriptors (binding 0 ('gTex'), binding 1 ('samp')),
so a layout for it would have no bindings at all. Vulkan's answer is a descriptor set layout with zero
bindings, but Device.CreateDescriptorSetLayout rejects an empty Bindings span, so there is nothing this call
can return — see the "no zero-binding descriptor set layout" note in src/Ahjo.Vulkan.Slang/CLAUDE.md. Give the
set a binding the shader can reference, or build the VkDescriptorSetLayout for it directly.
```

List item format: `binding {Slot} ('{Name}')`, joined with `", "`.

## 5. `MapBindings(ReadOnlySpan<SlangDescriptorBinding>, SlangUnboundedCapacity)` omits

`SlangVulkanMapping.cs:293-309`. Same shape: `ArgumentNullException.ThrowIfNull(capacity)`
first, then `CountMappable` / the `EmptySetMessage` refusal, then the fill loop
with the existing `Fixed` / not-`Fixed` branch inside, `continue`-ing on
`IsZero`. The resolver is never invoked for an omitted binding — it never was,
since a zero-count binding is `Fixed` — so `SlangUnboundedCapacity`'s contract
(`SlangUnboundedCapacity.cs:6-12`) is unchanged and that file is not edited.

## 6. XML docs on the four entry points

On both `MapBindings` overloads, add to `<remarks>`:

- zero-count bindings are omitted, and **why** (Slang reserves the number,
  emits no variable, nothing can index it);
- the resulting array is therefore **not positionally aligned** with the
  `reflection.Bindings(i)` span it came from — key on `Slot`, not on index;
- the `NotSupportedException` when every binding of the set is zero-count,
  with a pointer at the zero-binding-layout gap.

Add `<exception cref="NotSupportedException">` coverage for the new throw on
both. On `SlangDescriptorBinding.Count` (`SlangDescriptorBinding.cs:42-43`) add
one sentence naming `IsZero` and the omission.

## 7. Four fixtures in `tests/Ahjo.Vulkan.Slang.Tests/ShaderFixtures.cs`

Every number in the doc comments below is measured on `v2026.14.1` / win-x64.
Append after `ReflectionLooseGlobalsWithParameterBlock`.

**7a. `ReflectionZeroLengthArray`** — the issue's shape, with two controls in
the same set so slot reservation is observable.

```
Texture2D    gTex[0];
SamplerState gSampler;
Texture2D    gReal;

[shader("fragment")]
float4 fragmentMain(float2 uv : TEXCOORD0) : SV_Target
{
    return gReal.Sample(gSampler, uv);
}
```

Doc comment states: reflection reports `(0,0) gTex Fixed(0)`,
`(0,1) gSampler Fixed(1)`, `(0,2) gReal Fixed(1)`; the emitted SPIR-V decorates
only `(0,1)` and `(0,2)`. **Deleting the array entirely moves `gSampler` to 0 and
`gReal` to 1** — that is what makes `gTex[0]` a reserved slot rather than a
non-event, and it is why the two other resources are in the fixture.

**7b. `ReflectionZeroLengthArrayFromConstant`** — 7a character-for-character
except the first two lines:

```
static const int N = 0;
Texture2D        gTex[N];
```

Doc comment: this is `Texture2D gMaps[NUM_MAPS];` with `NUM_MAPS = 0` —
generated or parameterized shader code. Reflection reports the identical three
bindings, so the shape is reachable without anyone typing `[0]`. Kept as a
near-duplicate on purpose, for the same reason
`ReflectionMaterialBlockWidened` is (`ShaderFixtures.cs:648-662`).

**7c. `ReflectionZeroLengthArrayOnly`** — the degenerate set.

```
Texture2D gTex[0];

[shader("fragment")]
float4 fragmentMain() : SV_Target
{
    return float4(1.0, 0.0, 0.0, 1.0);
}
```

Doc comment: reflection reports one set with one binding, `(0,0) gTex Fixed(0)`;
the emitted SPIR-V decorates **nothing**. Vulkan's layout for this set has zero
bindings and `Device.CreateDescriptorSetLayout` cannot make one
(`src/Ahjo.Vulkan/Lifecycle/Device.cs:192-193`).

**7d. `ReflectionZeroLengthArrayInOwnSet`** — one dead set beside a live one.

```
[[vk::binding(0, 1)]] Texture2D gTex[0];

Texture2D    gReal;
SamplerState gSampler;

[shader("fragment")]
float4 fragmentMain(float2 uv : TEXCOORD0) : SV_Target
{
    return gReal.Sample(gSampler, uv);
}
```

Doc comment: reflection reports `(0,0) gReal`, `(0,1) gSampler`,
`(1,0) gTex Fixed(0)`; SPIR-V decorates `(0,0)` and `(0,1)`. The explicit space
also shows #180's `CollectSpaceCorrections` is unaffected by a zero count — the
binding is keyed to set 1, its declared space.

## 8. Tests in `tests/Ahjo.Vulkan.Slang.Tests/SlangReflectionTests.cs`

Place T1–T9 after `MapBindings_WithResolver_SizesEachArrayIndependently`
(`:238-269`), so the zero-count block sits beside the unbounded block it mirrors.

**T1 `Reflection_ZeroLengthArray_ReportsFixedZeroAndReservesTheSlot`**
(`ReflectionZeroLengthArray`)
- `bindings.Length == 3`.
- `bindings[0]`: `Slot == 0`, `Name == "gTex"`, `Count.Kind == Fixed`,
  `Count.Value == 0`, `Count.IsZero`.
- `bindings[1].Slot == 1`, `bindings[2].Slot == 2` — the slot is reserved.
- **SPIR-V oracle**: `SpirvDecorations.ReadDescriptorBindings(program.Spirv(0))`
  contains `(0,1)` and `(0,2)` and **does not contain any pair with
  `Binding == 0`**. This is the assertion the existing coverage theory cannot
  make (it is one-way, declared ⊇ used).

**T2 `Reflection_ZeroLengthArrayFromConstant_IsTheSameShape`**
(`ReflectionZeroLengthArrayFromConstant`) — the same three `Count`/`Slot`
assertions as T1. No SPIR-V assertion; T1 carries that.

**T3 `MapBindings_ZeroCountBinding_IsOmittedFromTheLayout`**
(`ReflectionZeroLengthArray`)
- `DescriptorBinding[] mapped = bindings.MapBindings();`
- `mapped.Length == 2`; `mapped[0].Slot == 1` (`VK_DESCRIPTOR_TYPE_SAMPLER`),
  `mapped[1].Slot == 2` (`VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE`).
- no element has `Count == 0`, and none has `Slot == 0`.

**T4 `MapBinding_ZeroCountBinding_Throws`** (`ReflectionZeroLengthArray`)
- `Assert.Throws<NotSupportedException>(() => bindings[0].MapBinding())`.
- message contains `"binding 0"`, `"gTex"`, `"zero descriptors"`, `"MapBindings"`
  (`StringComparison.Ordinal`, as the sibling tests do).

**T5 `MapBinding_WithCapacity_OnZeroCountBinding_Throws`**
(`ReflectionZeroLengthArray`)
- `Assert.Throws<ArgumentException>(() => bindings[0].MapBinding(64))`.
- message contains `"zero descriptors"` **and** `"E30029"`.
  Asserting on the text is load-bearing: the pre-existing generic branch throws
  the same exception type, so a type-only assertion cannot go red.

**T6 `MapBindings_WithResolver_OmitsZeroCountWithoutAskingTheResolver`**
(`ReflectionZeroLengthArray`)
- resolver increments `asked` and returns `1u`.
- `mapped.Length == 2`, slots 1 and 2, `asked == 0`.

**T7 `MapBindings_EverythingZeroCount_ThrowsNamingTheGap`**
(`ReflectionZeroLengthArrayOnly`)
- `TryGetSet(0, …)` is `true` with one binding (reflection still reports it).
- `Assert.Throws<NotSupportedException>(() => bindings.MapBindings())`.
- message contains `"gTex"`, `"zero bindings"` and `"CreateDescriptorSetLayout"`.

**T8 `MapBindings_ZeroCountInItsOwnSet_StillMapsTheOtherSet`**
(`ReflectionZeroLengthArrayInOwnSet`)
- set 0 maps to two bindings, slots 0 and 1.
- set 1 throws `NotSupportedException` from `MapBindings()`.
- This is the #176-consistency claim: one dead array does not make the program
  unmappable, only the set that consists entirely of dead arrays.

**T9 `MapBinding_HandBuiltZeroCount_BehavesLikeAReflectedOne`** — no shader, no
compiler.
- `new SlangDescriptorBinding { Slot = 4, Name = "hand", Type = SLANG_BINDING_TYPE_TEXTURE, Count = SlangDescriptorCount.Fixed(0), Stages = ShaderStages.Fragment }`
  → `MapBinding()` throws `NotSupportedException` whose message contains
  `"binding 4"` and `"hand"`.
- `Assert.True(default(SlangDescriptorBinding).Count.IsZero)` — a zeroed span
  element is a zero-count binding (spec §E10).
- `Assert.False(new SlangDescriptorBinding().Count.IsZero)` — the parameterless
  constructor still supplies `Fixed(1)` (`SlangDescriptorBinding.cs:94-98`,
  issue #119).

**T10 `MapBindings_ZeroLengthArray_BuildsALayoutValidationAccepts`** — driver +
validation gated, placed beside the existing device test (`:1450-1540`).
- `TestGate.RequireDriver();` then `TestGate.RequireValidationLayer();`
- `Instance.Create` with `EnableValidation = true` and a `DebugCallback` that
  appends every message to a `List<string>`.
- compile `ReflectionZeroLengthArray`, `MapBindings()` the one set, create the
  `DescriptorSetLayout` (binding numbers 1 and 2 — non-contiguous by
  construction), then a `PipelineLayout`, then
  `device.CreateShaderModule(program.Spirv(0))`.
- assert the collected error-severity list is empty.
- **This test is corroboration, not a discriminator.** It stays green under every
  mutation in §9 — a surplus `descriptorCount = 1` at binding 0 is a perfectly
  valid layout, which is exactly why the defect was invisible. Say so in its doc
  comment so nobody mistakes it for the guard.

**Theory rows.** Add `ReflectionZeroLengthArray`,
`ReflectionZeroLengthArrayFromConstant` and `ReflectionZeroLengthArrayInOwnSet`
to `Reflection_CoversEverySetAndBinding_TheSpirvDecorates` (`:54-74`) as
`xcheckZeroArray`, `xcheckZeroArrayConst`, `xcheckZeroArraySet1`. They are
**not** discriminating — the theory is one-way and passes whether the zero-count
binding is reported or not — and are added only so the fixtures are covered by
the suite's standing cross-check. **Do not add
`ReflectionZeroLengthArrayOnly`:** its SPIR-V decorates nothing, so the row would
iterate zero times and assert nothing at all.

## 9. Falsifiability — prove each new test can go red

Apply each mutation, run `dotnet test tests/Ahjo.Vulkan.Slang.Tests`, record the
failures, revert. The predicted colours are part of the deliverable: **a
mutation that does not produce exactly this column is a finding — stop and
report it, do not adjust the test to match.**

| | M1 omission deleted (both `MapBindings` map every binding) | M2 `IsZero` refusal deleted from `MapBinding(binding)` | M3 zero clause deleted from `MapBinding(binding, count)` | M4 all-zero refusal deleted (return `[]`) | M5 `SlangReflection.Walk` skips ranges with `count == 0` | M6 `IsZero` weakened to `Kind == Fixed` |
|---|---|---|---|---|---|---|
| **T1** reflection reports `Fixed(0)` | green | green | green | green | **RED** | green |
| **T2** the `N = 0` shape | green | green | green | green | **RED** | green |
| **T3** batch omits | **RED** | green | green | green | green | **RED** |
| **T4** single refuses | green | **RED** | green | green | green | green |
| **T5** capacity refuses | green | green | **RED** | green | green | green |
| **T6** resolver not asked | **RED** | green | green | green | green | **RED** |
| **T7** all-zero set refuses | **RED** | green | green | **RED** | **RED** | green |
| **T8** dead set beside a live one | **RED** | green | green | **RED** | **RED** | **RED** |
| **T9** hand-built + `default` | green | **RED** | green | green | green | **RED** |
| **T10** driver/validation | green | green | green | green | green | green |

The two columns that carry the design:

- **M5 is why T1 and T2 exist.** "Fix it in reflection" — the issue's own
  suggestion — leaves **T3 and T6 green**, because a walk that dropped the range
  and a mapper that omits the binding produce byte-identical
  `DescriptorBinding[]`. Only an assertion on reflection's own output can tell
  them apart, and #179 is the precedent for what happens when nothing can.
- **M2 is why T4 and T9 exist.** The batch path skips before it calls
  `MapBinding`, so every batch test stays green while the single-binding path
  silently ships `Count = 0` again.

T10 is green in every column, by construction. It proves the produced layout is
one a driver and the validation layer accept; it cannot prove it is the right
one.

## 10. Docs

**10a. `src/Ahjo.Vulkan.Slang/CLAUDE.md`.** The heading at line 138 reads
"Reflection — ten rules Slang does not hand you"; make it **eleven** and append
rule 11 after rule 10 (which ends at line 273). Text to write, in the file's
voice — measurement first, consequence second:

> 11. **A zero-length resource array is a real descriptor range with a count of
>     literally zero, and the binding number is reserved.** Measured on
>     `v2026.14.1` / win-x64: `Texture2D gTex[0];` compiles, and
>     `getDescriptorSetDescriptorRangeCount` returns `0` — not a sentinel, so
>     `Fixed(0)` is a legitimate report and reflection keeps making it. The slot
>     is consumed exactly as a four-element array's would be (delete the
>     declaration and every following resource moves down one), and the emitted
>     SPIR-V decorates **no variable** at it; nothing can index it
>     (`error[E30029]` statically, `error[E99997]` dynamically). It is reachable
>     without anyone typing `[0]` — `gMaps[NUM_MAPS]` with `NUM_MAPS = 0` — and a
>     struct-of-resources array yields one zero-count range **per member**.
>     **So it is not a Vulkan descriptor binding, and `SlangVulkanMapping` says
>     so at every entry point**: `MapBindings` omits it (the result is therefore
>     not positionally aligned with `reflection.Bindings(i)` — key on `Slot`),
>     `MapBinding` refuses it, and `MapBinding(binding, count)` refuses to size
>     it. The three must keep agreeing: emitting `descriptorCount = 0` and
>     omitting the binding are both legal Vulkan and are **not compatible with
>     each other** — a set allocated from one layout fails
>     `VUID-vkCmdBindDescriptorSets-pDescriptorSets-00358` against a pipeline
>     layout built from the other (measured, NVIDIA + validation layer). Emitting
>     `descriptorCount = 0` is *not* an option here anyway:
>     `DescriptorBinding.Count == 0` is `Ahjo.Vulkan`'s sentinel for a zeroed
>     span element and is rewritten to `1`
>     (`src/Ahjo.Vulkan/Lifecycle/Device.cs:210`, issue #119) — **do not "fix"
>     that guard**, and do not move this rule into `SlangReflection`: refusing a
>     program there would make two perfectly usable bindings unreachable because
>     of one nobody can reference, which is the failure #176 removed. Issue #183.

Also extend the "Known gap: no zero-binding descriptor set layout" section
(`:296-308`) with one sentence: a set whose every binding is a zero-length array
reaches the same gap by a second route, and `MapBindings` refuses it there with a
message naming the gap rather than letting `Device` report an empty span.

**10b. `src/Ahjo.Vulkan.Slang/README.md`.** New `###` subsection after the
unbounded-array recipe (which ends at `:288`, before "That array is not yet a
layout a driver will accept") titled **"A zero-length array is a binding that
isn't"**: three or four sentences plus a `SlangDescriptorCount.IsZero` snippet
for the hand-rolled path, the note that `MapBindings`' result is not positionally
aligned with the reflected span, and the `NotSupportedException` a set with
nothing but zero-length arrays raises. Keep the existing `MapBindings()` recipe
at `:173` as it is — it is still correct.

**No `docs/benchmarks.md` row, no benchmark project change.**

## 11. Verification

1. `dotnet build Ahjo.Vulkan.slnx` — zero warnings (`TreatWarningsAsErrors`).
2. `dotnet test tests/Ahjo.Vulkan.Slang.Tests` — record the pass count **before**
   the change and after; the delta must be 10 tests + 3 theory rows, with no
   pre-existing test changing colour (§E11 of the spec: no current fixture
   declares a zero-length array).
3. `dotnet test` repo-wide — unchanged pass/skip counts elsewhere.
4. §9's six mutations, each reverted, each column recorded in the PR body.
5. If a Vulkan device and `VK_LAYER_KHRONOS_validation` are present, run the
   suite with `AHJO_VULKAN_TIER=validation` and quote the contract test's
   `declared=… observed=…` line for T10; otherwise state that T10 skipped as
   `[gate:driver]` / `[gate:validation]` and that every other test in §9 ran.

---

## OPEN items

**OPEN-1 — `Device.CreateDescriptorSetLayout` and an empty `Bindings` span.**
Vulkan accepts `bindingCount = 0` (measured: `VK_SUCCESS`, validation silent).
`Ahjo.Vulkan` refuses it (`src/Ahjo.Vulkan/Lifecycle/Device.cs:192-193`), which
is the pre-existing gap in `src/Ahjo.Vulkan.Slang/CLAUDE.md:296-308` and the only
reason step 4 throws instead of returning `[]`. **Do not change
`src/Ahjo.Vulkan/` in this PR.** If the maintainer wants that gap closed, it is a
separate issue and step 4's refusal becomes a `return []` afterwards.

**OPEN-2 — the name `IsZero`** (spec §OPEN-2). A rename with no behavioural
consequence, but it appears in five files; settle it before step 1 rather than
after step 8.

**OPEN-3 — should `ReflectionZeroLengthArrayOnly` also get a driver-gated test**
proving the whole-set failure is the only thing that fails, i.e. that
`SetLayoutSlotCount`'s documented loop (`SlangReflection.cs:177-186`) cannot build
a `PipelineLayout` for it? Deliberately left out: it would assert the same gap
OPEN-1 covers, from a third place, and would skip in CI. Add it only if the
maintainer wants the gap pinned by a test rather than by a message.
