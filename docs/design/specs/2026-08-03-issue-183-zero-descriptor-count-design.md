# A reflected descriptor count of zero is laundered into a 1-descriptor binding

**Issue:** #183
**Paired plan:** `../plans/2026-08-03-issue-183-zero-descriptor-count.md`
**Lands consistently with:**
`2026-08-02-issue-175-177-slang-reflection-completeness-design.md` (#176 moved the
capacity decision out of reflection and into the mapper — this decision stays on
that side of the line) and
`2026-08-03-issue-180-constantbuffer-space-design.md` (the space correction is
measured to survive on a zero-count binding; see §E5).
**Measured on:** Slang `v2026.14.1` (`Directory.Build.props:46`), win-x64.
Vulkan measurements on NVIDIA GeForce RTX 4070 Ti, driver 610.47,
`VK_LAYER_KHRONOS_validation` 1.4.341, with a positive control proving the layer
was live (§E7). Validation-usage text quoted from
`C:\VulkanSDK\1.4.341.1\share\vulkan\registry\validusage.json`
(`api version 1.4.341`).
**Scope:** one decision — what `SlangVulkanMapping`'s four binding entry points
do with `SlangDescriptorCount.Fixed(0)`. Nothing in `src/Ahjo.Vulkan/` changes.

**The issue's suggested fix is refused.** The issue proposes a `> 0` guard in
`SlangReflection`'s classification switch. §E1–§E3 show why that is wrong, and
the maintainer has decided the fix belongs in `SlangVulkanMapping`. This spec
decides only which shape it takes there.

---

## Problem

A descriptor count of zero that reflection legitimately reports becomes a
1-descriptor binding in the emitted layout:

1. `SlangReflection.cs:526-536` classifies `>= 0 and <= uint.MaxValue` as
   `SlangDescriptorCount.Fixed`, so `Fixed(0)` is representable and — §E1 —
   reachable from a shader that compiles cleanly.
2. `SlangVulkanMapping.MapBinding` (`SlangVulkanMapping.cs:170-184`) passes it
   through: `Count = binding.Count.Value` with no zero case.
3. `Device.CreateDescriptorSetLayout` rewrites it —
   `descriptorCount = b.Count == 0 ? 1u : b.Count`
   (`src/Ahjo.Vulkan/Lifecycle/Device.cs:210`), with the same normalization in
   `src/Ahjo.Vulkan/Pools/DescriptorTemplate.cs:167`.

The result is a `VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE` descriptor at set 0 binding 0
that the shader never declared: surplus in the caller's pool budget, one wasted
`DescriptorWrite` slot in any `DescriptorTemplate<T>` built from the same array
(`DescriptorTemplate.cs:167-183`), and — measured, §E8 — a set layout that is
**not** interchangeable with a correctly built one for the same SPIR-V.

The `Count == 0 ? 1` normalization in `Ahjo.Vulkan` is not the defect. It is
`DescriptorBinding`'s valid-by-default guard for a zeroed span element
(`src/Ahjo.Vulkan/Pipelines/DescriptorBinding.cs:14-26`, issue #119) and stays.
The defect is that `SlangVulkanMapping` hands it a `Count` of `0` that means
"no descriptors" where the guard reads "somebody forgot to construct me".

---

## Evidence

Every number below was produced by a scratchpad probe against the current
branch (`issue-183-zero-descriptor-count`, stacked on #180/#181), reading
reflection's own output and the `OpDecorate DescriptorSet` / `Binding` pairs in
the SPIR-V Slang emitted for the same program. The probes have been deleted.

### E1 — Slang reports a literal zero, not a sentinel

```
Texture2D gTex[0]; SamplerState gSampler; Texture2D gReal;

  reflect (0,0) 'gTex'     TEXTURE Fixed(0)
  reflect (0,1) 'gSampler' SAMPLER Fixed(1)
  reflect (0,2) 'gReal'    TEXTURE Fixed(1)
  spirv   fragmentMain:    (0,1) gSampler | (0,2) gReal
```

`spReflectionTypeLayout_getDescriptorSetDescriptorRangeCount`
(`SlangReflection.cs:499`) returns `0` — distinct from the two documented
sentinels, which arrive as `-1` / `-2` and already have their own kinds
(`SlangDescriptorCount.cs:15-34`). The program compiles, links and emits SPIR-V
for its entry point; nothing in the pipeline complains.

Confirmed to produce the same `Fixed(0)` for `Texture2D`, `StructuredBuffer`,
`SamplerState`, `ConstantBuffer` and `RWTexture2D` element types (inherited from
the investigation; re-measured here for `Texture2D` and `SamplerState`).

### E2 — reachable without anyone typing `[0]`

```
static const int N = 0;
Texture2D gTex[N];   →  reflect (0,0) 'gTex' TEXTURE Fixed(0)   (identical to E1)
```

This is `Texture2D gMaps[NUM_MAPS];` with `NUM_MAPS = 0` — generated or
parameterized shader code, not a typo. It is the reason this is worth a fix
rather than a note.

### E3 — the binding number is reserved, and no legal shader can use it

Three-way control, same module minus one line:

| module | reflection | SPIR-V decorates |
|---|---|---|
| `gTex[0]; gSampler; gReal;` | `(0,0) gTex Fixed(0)`, `(0,1) gSampler`, `(0,2) gReal` | `(0,1)`, `(0,2)` |
| array deleted | `(0,0) gSampler`, `(0,1) gReal` | `(0,0)`, `(0,1)` |
| `gTex[4]` instead | `(0,0) gTex Fixed(4)`, `(0,1) gSampler`, `(0,2) gReal` | `(0,1)`, `(0,2)` |

So the zero-length array **consumes a binding number exactly as a four-element
one does**, and the survivors keep the slots Slang gave them. Dropping the
binding therefore renumbers nothing — it leaves a hole at the reserved number,
which is legal (§E7).

Nothing can reference the reserved slot. Re-measured on `v2026.14.1`:

- `gTex[0].Sample(...)` → `error[E30029]: array index out of bounds`
- `gTex[gPush.i].Sample(...)` →
  `error[E99997]: … unimplemented: Unhandled global inst in spirv-emit`

Both fail at code generation, after reflection has already reported the binding.

### E4 — one declaration can reserve several slots, and a `ParameterBlock` is not immune

```
struct Bundle { Texture2D tex; SamplerState samp; };
Bundle gBundles[0]; Texture2D gReal; SamplerState gSampler;

  reflect (0,0) 'tex' TEXTURE Fixed(0) | (0,1) 'samp' SAMPLER Fixed(0)
          (0,2) 'gReal' Fixed(1)       | (0,3) 'gSampler' Fixed(1)
  spirv   (0,2) gReal | (0,3) gSampler
```

```
struct Mats { Texture2D maps[0]; Texture2D real; SamplerState samp; float4 tint; };
ParameterBlock<Mats> gBlock;

  reflect (0,0) 'gBlock' CONSTANT_BUFFER Fixed(1)   ← the implicit uniform buffer
          (0,1) 'maps' TEXTURE Fixed(0)
          (0,2) 'real' TEXTURE Fixed(1)
          (0,3) 'samp' SAMPLER Fixed(1)
  spirv   (0,0) gBlock | (0,2) gBlock.real | (0,3) gBlock.samp
```

A struct-of-resources array of length zero yields **one zero-count range per
resource member**, and inside a block the implicit-uniform-buffer shift
(`CLAUDE.md` rule 3) is applied on top. Any fix has to handle several zero-count
bindings in one set, in any position.

### E5 — #180's space correction survives

```
[[vk::binding(3, 1)]] Texture2D gTex[0]; Texture2D gReal; SamplerState gSampler;

  reflect (0,0) 'gReal' Fixed(1) | (0,1) 'gSampler' Fixed(1) | (1,3) 'gTex' Fixed(0)
  spirv   (0,0) gReal | (0,1) gSampler
```

The zero-count binding is keyed to set **1**, its declared space, so
`CollectSpaceCorrections` (`SlangReflection.cs:788-876`) is unaffected by a zero
count. No interaction to design around.

### E6 — current end-to-end behaviour

`ReadOnlySpan<SlangDescriptorBinding>.MapBindings()` on E1's set:

```
MAPPED set 0: slot=0 count=0 VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE
            | slot=1 count=1 VK_DESCRIPTOR_TYPE_SAMPLER
            | slot=2 count=1 VK_DESCRIPTOR_TYPE_SAMPLED_IMAGE
```

`Device.cs:210` then turns `count=0` into `1`. The issue's report reproduces
exactly.

### E7 — `descriptorCount = 0` is legal Vulkan, and so is a hole

Measured against a live driver with the validation layer enabled:

| call | result | validation |
|---|---|---|
| `vkCreateDescriptorSetLayout` with bindings `{0: SAMPLED_IMAGE ×0, 1: SAMPLER ×1, 2: SAMPLED_IMAGE ×1}` | `VK_SUCCESS` | silent |
| the same minus binding 0 (numbers 1, 2 — non-contiguous) | `VK_SUCCESS` | silent |
| `vkCreateDescriptorSetLayout` with `bindingCount = 0` | `VK_SUCCESS` | silent |
| **positive control:** two bindings numbered `0` | `VK_SUCCESS` | **error**, quoted below |

The control proves the layer was live and its silence elsewhere is evidence:

> `vkCreateDescriptorSetLayout(): pCreateInfo->pBindings[1].binding is duplicated at pBindings[0].binding.`
> The Vulkan spec states: *If the perStageDescriptorSet feature is not enabled, or flags does not contain
> `VK_DESCRIPTOR_SET_LAYOUT_CREATE_PER_STAGE_BIT_NV`, then the `VkDescriptorSetLayoutBinding::binding` members
> of the elements of the `pBindings` array must each have different values*
> (`VUID-VkDescriptorSetLayoutCreateInfo-binding-00279`)

Verified verbatim in `validusage.json`
(`VkDescriptorSetLayoutCreateInfo/core`) — and note what it requires: binding
numbers must be **distinct**, not contiguous. A hole is legal.

That `descriptorCount = 0` is anticipated by the spec rather than merely
tolerated is verified from two further VUIDs, quoted from the same file:

- `VUID-VkDescriptorSetLayoutBinding-descriptorCount-09465` —
  *If `descriptorCount` is not `0`, `stageFlags` must be `VK_SHADER_STAGE_ALL`
  or a valid combination of other `VkShaderStageFlagBits` values*
- `VUID-VkDescriptorSetLayoutBinding-descriptorType-00282` —
  *If `descriptorType` is `VK_DESCRIPTOR_TYPE_SAMPLER` or
  `VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER`, and `descriptorCount` is not `0`
  and `pImmutableSamplers` is not `NULL`, `pImmutableSamplers` must be a valid
  pointer to an array of `descriptorCount` valid `VkSampler` handles*

A count of zero switches off the requirements that would apply to a real
descriptor. It is a defined state, not an accident.

The descriptor **pool** side is the other way round —
`VUID-VkDescriptorPoolSize-descriptorCount-00302`, verified verbatim:
*`descriptorCount` must be greater than 0*. Anything deriving pool sizes from a
binding list has to filter zero-count entries regardless of what the layout
does.

### E8 — the two candidate layouts are not interchangeable

Set allocated from the layout **without** binding 0, bound against a pipeline
layout built from the layout **with** binding 0 (`descriptorCount = 0`):

> `vkCmdBindDescriptorSets(): pDescriptorSets[0] … is not compatible with the corresponding VkPipelineLayout
> … due to: Binding 0 for VkDescriptorSetLayout … from pipeline layout has a descriptorCount of 0 but binding 0
> for VkDescriptorSetLayout …, trying to bind, has a descriptorCount of 1 (descriptorCount value of zero likely
> means there is descriptor for binding 0).`
> The Vulkan spec states: *Each element of `pDescriptorSets` that is not `VK_NULL_HANDLE` must have been
> allocated with a `VkDescriptorSetLayout` that matches (is the same as, or identically defined as) the
> `VkDescriptorSetLayout` at set n in layout, where n is the sum of `firstSet` and the index into
> `pDescriptorSets`* (`VUID-vkCmdBindDescriptorSets-pDescriptorSets-00358`)

VUID text verified verbatim in `validusage.json` (`vkCmdBindDescriptorSets/core`).
The same set bound against a pipeline layout built from its own layout produced
no message.

**This is the fact that forces the entry points to agree.** "Omit it" and "emit
it with count 0" are both legal, and they are not compatible with each other at
bind time. A mapper whose single-binding path and span path disagree hands a
caller two layouts that build and then fail at `vkCmdBindDescriptorSets`.

### E9 — a whole set can be nothing but zero-count bindings

```
Texture2D gTex[0];                        → 1 set, 1 binding, all Fixed(0); SPIR-V decorates nothing
[[vk::binding(0, 1)]] Texture2D gTex[0];  → set 0: gReal, gSampler; set 1: (1,0) gTex Fixed(0)
  + Texture2D gReal; SamplerState gSampler;   SPIR-V decorates only (0,0), (0,1)
```

Vulkan's answer for such a set is a layout with zero bindings — measured legal
in §E7. `Ahjo.Vulkan` cannot express one:
`Device.CreateDescriptorSetLayout` rejects an empty `Bindings` span outright
(`src/Ahjo.Vulkan/Lifecycle/Device.cs:192-193`). That is the gap already
documented for the sparse-set case in `src/Ahjo.Vulkan.Slang/CLAUDE.md:296-308`
and `SlangReflection.cs:187-196`; a zero-length array reaches it by a second
route.

### E10 — the secondary sources, measured

| expression | `Count` |
|---|---|
| `default(SlangDescriptorBinding)` | `Fixed(0)`, `Name` is **null** |
| `new SlangDescriptorBinding()` | `Fixed(1)` (`SlangDescriptorBinding.cs:94-98`) |
| `default(SlangDescriptorCount)` | `Fixed(0)` (`SlangDescriptorCount.cs:51-54`) |
| `SlangDescriptorCount.Unbounded.TryGetValue(out v)` | returns `false`, writes `v = 0` |

So a zeroed span element and a caller who ignored `TryGetValue`'s `bool` both
land on the same value reflection produces for a zero-length array. The mapper
cannot tell them apart and — §D5 — must not try.

A hand-built `Fixed(0)` binding today maps to `slot=4 count=0` and is refused by
the capacity overload with a message that reads oddly for zero:
*"already has a descriptor count from reflection (0). Supplying one here would
override what the shader declares"* (`SlangVulkanMapping.cs:220-224`).

### E11 — consumer audit

Every caller of the four entry points, repo-wide (`src/`, `samples/`, `tests/`,
`docs/`):

| call site | what it does |
|---|---|
| `src/Ahjo.Vulkan.Slang/README.md:173` | `reflection.Bindings(i).MapBindings()` → `CreateDescriptorSetLayout` (the documented recipe) |
| `src/Ahjo.Vulkan.Slang/README.md:276` | `MapBindings(resolver)` (the bindless recipe) |
| `tests/…/SlangReflectionTests.cs:199` | `MapBinding()` refusal on an unbounded binding |
| `tests/…/SlangReflectionTests.cs:214` | `MapBinding(1024)` |
| `tests/…/SlangReflectionTests.cs:233` | `MapBinding(64)` on a fixed binding — the `ArgumentException` |
| `tests/…/SlangReflectionTests.cs:254, 265` | `MapBindings(capacity)` |
| `tests/…/SlangReflectionTests.cs:1510` | driver-gated: `MapBindings()` → layout → `PipelineLayout` |

**No sample and nothing in `src/` outside the Slang package calls them**, and
`src/Ahjo.Vulkan/` does not reference `Ahjo.Vulkan.Slang` at all
(`src/Ahjo.Vulkan.Slang/CLAUDE.md:6-9`). The blast radius of a behaviour change
here is the README recipes, the seven test call sites, and downstream consumers
(Logos). None of the existing fixtures declares a zero-length array, so no
existing test changes colour.

---

## Decision

**`Fixed(0)` is not a Vulkan descriptor binding, and every entry point of
`SlangVulkanMapping` says so in the way its own return type allows.**

The shader declared a zero-length resource array. Slang reserved a binding
number for it (§E3) and emitted no variable (§E1). The layout that matches that
SPIR-V is the one **without** the binding: a hole at the reserved number, legal
under `VUID-VkDescriptorSetLayoutCreateInfo-binding-00279` (§E7), with every
other binding at the slot Slang gave it.

| entry point | behaviour on `Fixed(0)` |
|---|---|
| **D1** `MapBindings(span)` (`:260`) | **omits** it; result is shorter than the input |
| **D2** `MapBindings(span, capacity)` (`:293`) | **omits** it; the resolver is not asked (it never was — the binding is `Fixed`) |
| **D3** `MapBinding(binding)` (`:170`) | **refuses** with `NotSupportedException`, naming the binding and pointing at the batch call |
| **D4** `MapBinding(binding, count)` (`:214`) | **refuses**, keeping today's `ArgumentException` type with a zero-specific message |
| **D5** provenance | reflected, `default(SlangDescriptorBinding)` and hand-built `Fixed(0)` are treated identically |
| **D6** every binding of the set is `Fixed(0)` | both `MapBindings` overloads throw `NotSupportedException` naming the gap (§E9) |

**D1 + D3 are the same answer, not a conflict.** `MapBindings` returns an array
and can express "this binding produces nothing" by not producing an element.
`MapBinding` returns one `DescriptorBinding` and has no value that means
nothing — so it refuses and says what the batch call does. A caller who maps one
binding at a time is stopped and told to skip it; a caller who maps a span gets
the layout directly. Both arrive at the same layout, which is what §E8 makes
non-negotiable. This is the shape the file already has for the unbounded case:
`MapBinding()` refuses what `MapBindings(span, capacity)` handles
(`SlangVulkanMapping.cs:156-165`).

**D4 rejects candidate (b).** Letting a caller size a `Fixed(0)` binding
through the capacity overload would put descriptors in the layout that *no
shader code can reference* (§E3) — the issue's own defect, opt-in and larger.
The capacity overload exists to supply "information the shader does not state"
(`SlangVulkanMapping.cs:190-194`); a zero-length array is the shader stating a
count, and the count is zero. The message becomes zero-specific because the
current one (§E10) tells the caller reflection reported `0` as though that were
their mistake.

**D6 refuses rather than returning an empty array** because the alternative is
`Ahjo.Vulkan` throwing `"DescriptorSetLayoutDescription.Bindings must contain at
least one entry."` (`Device.cs:192-193`) two frames later, from a different
package, naming neither the set nor the zero-length array. Same failure, worse
message.

**That comparison is against the alternative design, not against shipped
behaviour, and the difference matters.** Today such a set maps to one binding
that `Device.cs:210` normalizes to `descriptorCount = 1`, so a complete
`PipelineLayout` *can* be built — wastefully, and layout-incompatible with the
emitted SPIR-V, but built. After D6 that program cannot be turned into a
`PipelineLayout` through this API at all. So for the all-zero-count set
specifically this is a **capability regression**, traded for not handing a driver
a layout the shader does not match. It is bounded to a set whose every binding is
a zero-length array — a shape with no legal use, since no index into such an
array compiles (§E4) — and it retires entirely the day OPEN-1 is answered yes.
Recorded because "same failure, worse message" would otherwise read as though
nothing were lost.

It is deliberately **not** papered over with a synthesized binding —
`src/Ahjo.Vulkan.Slang/CLAUDE.md:296-308` already forbids exactly that. The
refusal is scoped to "input non-empty, output empty" so an empty input span
keeps today's behaviour, and it retires the day `Ahjo.Vulkan` accepts an empty
span (OPEN-1).

**Visibility: nothing new.** No diagnostic, no `out int omitted`, no second
overload. The omitted binding is not lost — `reflection.Bindings(i)` still
reports it with its slot, name, type and `Fixed(0)` count, which is reflection's
job and where a caller already looks; the length difference between input and
output is observable; and `SlangDescriptorCount` gains one property,
`IsZero`, so a caller who builds `DescriptorBinding`s by hand (a route the
README explicitly invites, `README.md:183-189`) can name the case instead of
rediscovering it. Adding a channel to the mapper would duplicate information the
layer below already carries.

**The cost, stated:** `MapBindings`' result is no longer positionally aligned
with `reflection.Bindings(i)`. A caller correlating the two by index — nothing in
this repo does (§E11) — must key on `Slot`. That is documented on both
overloads rather than avoided, because the alternative is a layout with a
descriptor in it that the shader never declared.

### Why not the alternatives

- **(a) alone — `MapBinding(binding)` refuses, `MapBindings` unchanged.**
  Leaves the documented recipe (`README.md:173`) producing the defective layout;
  the refusal fires only on the path almost nobody takes.
- **(b) — relax `MapBinding(binding, count)` so a caller can size a `Fixed(0)`
  binding.** Reserves descriptors for a binding no shader code can index
  (`E30029` / `E99997`, §E3): the issue's defect made deliberate and unbounded.
- **(c) alone — `MapBindings` omits, `MapBinding` unchanged.** The two paths then
  build layouts that are incompatible at `vkCmdBindDescriptorSets` (§E8), and the
  omission is silent in a package whose recent history is three defects that were
  silent (#175 offsets, #180 renames, #183 itself).
- **(d) — emit `VkDescriptorSetLayoutBinding{descriptorCount = 0}`.** Legal
  Vulkan and measured to work (§E7), and rejected for a concrete reason rather
  than taste: **it is not expressible through `Ahjo.Vulkan`.**
  `DescriptorBinding.Count == 0` is the sentinel for "zeroed span element, make
  it 1" (`DescriptorBinding.cs:14-26`, `Device.cs:210`,
  `DescriptorTemplate.cs:167`), so a mapper that emitted `Count = 0` would have
  its intent overwritten by the wrapper — the current bug, unchanged. Emitting it
  would require `DescriptorBinding` to distinguish "zero on purpose" from
  "zeroed", i.e. an `Ahjo.Vulkan` type change that the valid-by-default rule
  (#119) exists to prevent and that the maintainer has excluded. It would also
  hand `DescriptorTemplate<T>` a binding whose 24 bytes of `T` the layout does
  not want (`DescriptorTemplate.cs:167-183`) and any pool-size derivation a
  `descriptorCount` of `0` that
  `VUID-VkDescriptorPoolSize-descriptorCount-00302` forbids.
- **The issue's own suggestion — a `> 0` guard in `SlangReflection`'s
  classification switch.** A cleanly compiling shader with two perfectly usable
  bindings (§E1) would become entirely unreflectable because of one binding
  nobody can reference — the failure mode #176 removed for the unbounded
  sentinel, reintroduced for a *less* exotic shape (§E2). Reflection reports what
  the shader declared, and the shader did declare a zero-length array.
- **Normalize in `Ahjo.Vulkan` (`Count == 0 ? 1`).** Correctly ruled out by the
  issue: that guard is load-bearing for `default(DescriptorBinding)` (#119) and
  belongs to the wrapper's own type.
- **Fix it in `SlangDescriptorCount`'s construction — forbid `Fixed(0)`.**
  Would make `default(SlangDescriptorCount)` unconstructible-by-value or a lie
  (§E10), and would push the same refusal into reflection by the back door.

---

## Consequences and cross-links

- **Resolves** #183.
- **Must land consistently with** #176 (`2026-08-02-issue-175-177-…-design.md`):
  reflection reports, the mapper decides. This decision adds a third case to the
  mapper's vocabulary and none to reflection's.
- **Does not disturb** #180 (§E5) or #181 (no new call into the binding-range
  family).
- **Does not touch** #119's normalization, `src/Ahjo.Vulkan/`, `Generated/`, or
  any hot path. **No benchmark and no `docs/benchmarks.md` row** —
  `src/Ahjo.Vulkan.Slang/CLAUDE.md:14-23` forbids both for this project, and
  nothing here is reachable from a frame loop.
- **Lands as rule 11** in `src/Ahjo.Vulkan.Slang/CLAUDE.md` (currently ten rules,
  heading at line 138; rule 9 came from #180, rule 10 from #180 Group D).
- **Test-suite lesson from #179** applies: each new test must have a named
  mutation that turns it red, and the plan states what colour every other new
  test goes under that mutation. `Reflection_CoversEverySetAndBinding_TheSpirvDecorates`
  (`SlangReflectionTests.cs:74`) is deliberately one-way (declared ⊇ used) and
  **cannot** discriminate here: it passes whether the zero-count binding is
  reported or not.

---

## OPEN items

**OPEN-1 — should `Device.CreateDescriptorSetLayout` accept an empty `Bindings`
span?** Vulkan does (`bindingCount = 0` → `VK_SUCCESS`, silent, §E7), and it is
the correct layout both for an unpopulated set index (`CLAUDE.md:296-308`) and
for a set whose every binding is a zero-length array (§E9). That is a decision
about `Ahjo.Vulkan`'s own validity guard, is not this issue's, and is **not**
assumed by anything above — D6 is a message-quality decision that becomes a
`return []` if OPEN-1 is ever answered yes. Recommend filing it as its own issue
rather than folding it in here. **The implementer must not change
`src/Ahjo.Vulkan/` for it.**

**OPEN-2 — is `IsZero` the right name on `SlangDescriptorCount`?** It sits beside
`IsUnbounded` (`SlangDescriptorCount.cs:96`), where "is" reads as "which kind is
this". `IsZero` means "kind is `Fixed` **and** the value is 0"; `IsEmpty` was
rejected as ambiguous with "has no value" (which is what `Unbounded`/`Unknown`
are). If the maintainer prefers a different spelling, it is a rename with no
behavioural consequence — say so before implementation rather than after.
