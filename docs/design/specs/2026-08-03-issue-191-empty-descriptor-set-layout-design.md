# Issue #191 — an empty `Bindings` span is a legal descriptor set layout

Paired plan: `../plans/2026-08-03-issue-191-empty-descriptor-set-layout.md`

Resolves #191, which is verbatim **OPEN-1 of the #183 spec**
(`2026-08-03-issue-183-zero-descriptor-count-design.md:442-451`). That spec
answered "not this issue's decision, file it separately"; this is that file.

---

## Problem

`Device.CreateDescriptorSetLayout` refuses a construct Vulkan accepts:

```csharp
// src/Ahjo.Vulkan/Lifecycle/Device.cs:198-199
if (desc.Bindings.IsEmpty)
    throw new ArgumentException("DescriptorSetLayoutDescription.Bindings must contain at least one entry.");
```

Vulkan has no such rule. `bindingCount = 0` returns `VK_SUCCESS` and the
validation layer says nothing (§E3). The zero-binding layout is not a degenerate
value to be defended against — it is the *only correct* layout for two shapes the
wrapper already produces descriptions for:

1. **An unpopulated set index in a sparse-set program.** A program binding sets
   0 and 2 needs a layout handle at index 1 for `vkCreatePipelineLayout`, and
   Vulkan's answer is a layout with no bindings.
   `SlangReflection.SetLayoutSlotCount` exists precisely to size that positional
   span (`src/Ahjo.Vulkan.Slang/SlangReflection.cs:167-201`), and its own XML doc
   says the hole "has no answer in the wrapper today"
   (`SlangReflection.cs:187-196`).
2. **A set whose every binding is a zero-length resource array.** #183
   established that Slang reports `Texture2D gTex[0]` as a real range with count
   `0`, that the emitted SPIR-V decorates nothing for it, and that the layout
   matching that SPIR-V therefore omits it
   (`src/Ahjo.Vulkan.Slang/CLAUDE.md:275-301`). When the whole set is such
   arrays, the matching layout has no bindings at all.

The cost today is concrete and already shipped:
`SlangVulkanMapping.MapBindings` **refuses** shape 2 rather than returning an
empty array (`src/Ahjo.Vulkan.Slang/SlangVulkanMapping.cs:314-317, 387-390`),
which the #183 spec recorded as a bounded **capability regression** (its §D6:
"after D6 that program cannot be turned into a `PipelineLayout` through this API
at all … it retires entirely the day OPEN-1 is answered yes"). Shape 1 has never
been expressible at all.

---

## Evidence

### E1 — the guard's provenance: original, uncommented, untested

`git log -L 190,205:src/Ahjo.Vulkan/Lifecycle/Device.cs` returns exactly two
commits:

| commit | what it did to these lines |
|---|---|
| `526f149` "Add DescriptorSetLayout + PipelineLayout + ShaderStages (issue 22)" | introduced the method **with the guard already in it**, no comment, no cited VUID |
| `1027c0a` "Device: reject a mis-ordered variable-descriptor-count binding (#192)" | added `ValidateVariableDescriptorCountOrdering` *below* the guard and added the `<exception>` doc that mentions "has no bindings" |

The guard has **no comment explaining it**, unlike every other validity check in
the same file (compare `Device.cs:214-217`, `Device.cs:256-267`,
`Device.cs:566-571`, each of which names its reason or its VUID). It has **no
test**: `grep -rn "must contain at least one entry"` across `tests/` returns
nothing, and no test in `tests/Ahjo.Vulkan.Tests/` asserts an `ArgumentException`
from `CreateDescriptorSetLayout` on an empty span. So it is a valid-by-default
reflex from the wrapper's first week, not an encoded constraint — which is the
issue's question 1, answered by reading rather than assumed.

### E2 — the method's own body needs nothing from non-emptiness

Line by line, with `desc.Bindings.Length == 0`:

| line | code | behaviour when empty |
|---|---|---|
| `Device.cs:201` | `ValidateVariableDescriptorCountOrdering(desc.Bindings)` | both loops (`:272-276`, `:278-290`) iterate zero times; `highestSlot` stays `0`; cannot throw |
| `Device.cs:203-205` | `stackalloc VkDescriptorSetLayoutBinding[0]`, `stackalloc uint[0]` | legal C#; both produce empty spans |
| `Device.cs:207-223` | the fill loop, including the `Count == 0 ? 1u` normalization at `:218` | iterates zero times; `anyFlagsSet` stays `false` |
| `Device.cs:226-227` | `fixed (… pBindings = nativeBindings)` | `Span<T>.GetPinnableReference()` returns a null ref for a zero-length span, so `pBindings == null` — which is exactly what §E3's VUID permits |
| `Device.cs:238` | `bindingCount = (uint)nativeBindings.Length` | `0` |
| `Device.cs:242-252` | the `VkDescriptorSetLayoutBindingFlagsCreateInfo` chain | not chained, because `anyFlagsSet` is `false` |

There is no arithmetic on the length, no division, no "first element" access.
Deleting the guard leaves a body that is already correct for the empty case.

The two `DescriptorSetLayoutDescription` flags are also vacuous when empty: every
`VkDescriptorSetLayoutCreateInfo` VUID that constrains `PUSH_DESCRIPTOR_BIT` or
`UPDATE_AFTER_BIND_POOL_BIT` quantifies over *elements of* `pBindings`
(`-flags-00280`, `-flags-02208`, `-flags-04591`, `-flags-08000`, `-flags-09464`,
verified in `validusage.json`), so an empty array satisfies all of them.

### E3 — Vulkan's answer, from the registry and from a driver

Quoted verbatim from
`C:/VulkanSDK/1.4.341.1/share/vulkan/registry/validusage.json`,
`VkDescriptorSetLayoutCreateInfo`:

> `VUID-VkDescriptorSetLayoutCreateInfo-pBindings-parameter` — *If `bindingCount`
> is **not 0**, `pBindings` must be a valid pointer to an array of
> `bindingCount` valid `VkDescriptorSetLayoutBinding` structures*

The conditional is the evidence: the registry contemplates `bindingCount == 0`
and excuses `pBindings` in that case. There is **no**
`VUID-VkDescriptorSetLayoutCreateInfo-bindingCount-arraylength` — contrast
`VUID-VkDescriptorUpdateTemplateCreateInfo-descriptorUpdateEntryCount-arraylength`
(§E5) and `VUID-VkDescriptorSetAllocateInfo-descriptorSetCount-arraylength`
(§E11.1), which are exactly that VUID for other structs and do exist.

The live half is already on record in #183's spec §E7 — measured on an NVIDIA
RTX 4070 Ti with `VK_LAYER_KHRONOS_validation` 1.4.341, with a positive control
in the same table proving the layer was live:

| call | result | validation |
|---|---|---|
| `vkCreateDescriptorSetLayout` with `bindingCount = 0` | `VK_SUCCESS` | silent |
| **positive control:** two bindings numbered `0` | `VK_SUCCESS` | **error** (`VUID-VkDescriptorSetLayoutCreateInfo-binding-00279`) |

### E4 — consumer audit of `Device.CreateDescriptorSetLayout`

Every call site repo-wide (`src/`, `samples/`, `tests/`): **21**, of which 3 are
samples, 18 are tests/benchmarks, and **0** are in `src/` outside the declaring
method.

| where | count | passes an empty span? |
|---|---|---|
| `samples/HelloCube/Program.cs:240`, `samples/HelloVma/Program.cs:420`, `samples/HelloVmaWindowed/Program.cs:203` | 3 | no — all literal collection expressions with ≥1 element |
| `tests/Ahjo.Vulkan.Tests/*` (CommandRecorder, ComputePipeline, DescriptorTemplate, DescriptorWrite, FrameRing, PipelineLayout ×4, SpecializationInfo, ValidByDefault, DescriptorSetPoolVariableCount) | 13 | no |
| `tests/Ahjo.Vulkan.Benchmarks/*` (DescriptorSetPool, DescriptorSetPoolVariableCount, PushDescriptors, SpecializationInfo) | 4 | no — all in `[GlobalSetup]`, none per-iteration |
| `tests/Ahjo.Vulkan.Slang.Tests/SlangReflectionTests.cs:1790, 1907` | 2 | no (today it cannot) |

**No call site loses a diagnostic.** Every one passes a literal, non-empty
collection expression; not one computes a length that could come out zero. The
only call site where emptiness is *data-dependent* is the reflection loop
documented at `SlangReflection.cs:177-186` and `src/Ahjo.Vulkan.Slang/README.md:166-172`
— and that is the call site the guard is *blocking*, not protecting.

`Device.CreateDescriptorSetLayout` is a setup-time API and appears in no
benchmark's `[Benchmark]` body — only in `[GlobalSetup]` (four occurrences
above). It is not on a hot path listed in `src/Ahjo.Vulkan/CLAUDE.md`.

### E5 — `DescriptorTemplate` carries a guard of the *same shape* for the *opposite* reason

`DescriptorTemplateBuilder.BuildEntries` refuses an empty binding span
(`src/Ahjo.Vulkan/Pools/DescriptorTemplate.cs:157-158`):

```csharp
if (bindings.IsEmpty)
    throw new ArgumentException("DescriptorTemplate<T> requires at least one binding.", nameof(bindings));
```

This one **is** load-bearing, and Vulkan says so:

> `VUID-VkDescriptorUpdateTemplateCreateInfo-descriptorUpdateEntryCount-arraylength`
> — *`descriptorUpdateEntryCount` must be greater than 0*

So `DescriptorSetLayout.CreateUpdateTemplate<T>` /
`PipelineLayout.CreatePushDescriptorTemplate<T>` on a zero-binding layout is a
VUID violation, not a wrapper preference. A second, independent check would also
catch it: `Unsafe.SizeOf<T>()` is ≥ 1 for any C# `unmanaged` struct, so the
strict-size assertion at `DescriptorTemplate.cs:189-193` (`runningOffset != structSize`,
i.e. `0 != ≥1`) would throw anyway — with a confusing message about `T`'s
padding. The early guard is therefore the right diagnostic and stays; only its
*message* is wrong, because it reads as a wrapper rule when it is a Vulkan one.

No other assumption in that file breaks: `stackalloc …[0]` at `:97` / `:125`,
`descriptorUpdateEntryCount = 0`, and the `Count == 0 ? 1u` normalization at
`:167` are all reached zero times or trivially. There is no division and no
per-binding aggregate.

### E6 — `DescriptorSetPool` carries no zero-binding assumption at all

Full read of `src/Ahjo.Vulkan/Pools/DescriptorSetPool.cs` (469 lines). Nothing in
it inspects a layout's bindings — it is handed a raw
`VkDescriptorSetLayout_T*` and says so repeatedly (`:186-198`, `:256-265`:
"Vulkan exposes no way to read a layout's binding flags … back from the handle").
Specifically:

| candidate hazard | finding |
|---|---|
| division by a binding count | none exists; no division anywhere in the file |
| `_maxPerTypeDescriptorTotal` (`:148-161`) | computed from the caller's `poolSizes` only, `O(n²)` over that span, in the constructor. Independent of any layout. |
| the `Acquire` budget guard (`:266-273`) | compares `variableDescriptorCount` against that total. For a zero-binding layout the caller passes `0` (there is no variable binding), `0 > n` is false, so it never fires. |
| free-list key `IdleKey(layout, count)` (`:275-277`, `:350-355`, `:468`) | a pointer plus a `uint`; a zero-binding layout is just another pointer |
| `AllocateFromCurrentPool` (`:419-454`) | `descriptorSetCount = 1`, `pSetLayouts = &layout`; the `pNext` chain is suppressed for count `0` (`:449`) — exactly what a zero-binding layout wants |
| `Reset` / `Release` / `Dispose` | handle bookkeeping only |

So `vkAllocateDescriptorSets` against a zero-binding layout consumes one of
`maxSets` and no per-type budget, and every wrapper path around it already
behaves. **One friction point exists and it is a different guard:** the
constructor rejects an empty `poolSizes` span (`:138-139`), which Vulkan does not
require. That guard is relaxed in this change too — **§E11** audits it, **D4**
decides it, and the *Overruled* note under the decision records that the
maintainer, not this spec, made that scope call.

### E7 — the recording and pipeline-layout paths are handle-only

- `Device.CreatePipelineLayout` (`Device.cs:545-613`) reads
  `desc.SetLayouts[i].Handle` and nothing else; a zero-binding layout handle is
  an ordinary non-null handle.
- `CommandRecorder.BindDescriptorSets` (`Recording/CommandRecorder.cs:226-253`)
  and its debug assertion `AssertSetsMatchLayout` (`:255-284`) compare
  `nint` handles. `declared[slot] != (nint)sets[i].Layout` behaves identically for
  an empty layout.
- `PipelineLayoutMetadata.SetLayouts` (`Pipelines/PipelineLayout.cs:18-22`,
  filled at `Device.cs:604-611`) is a `nint[]` of handles.

Nothing downstream needs to know that a layout has bindings.

### E8 — the Slang coupling, exactly as shipped

The refusal this issue retires lives in three places, all introduced by `f0c6e9b`
(#183):

| location | what it does |
|---|---|
| `SlangVulkanMapping.cs:312-317` (`MapBindings(span)`) | `int kept = CountMappable(bindings); if (bindings.Length != 0 && kept == 0) throw new NotSupportedException(EmptySetMessage(bindings));` |
| `SlangVulkanMapping.cs:385-390` (`MapBindings(span, capacity)`) | the identical pair, after the `capacity` null check |
| `SlangVulkanMapping.cs:496-518` (`EmptySetMessage`) | builds the message, whose text literally names this defect: *"Vulkan's answer is a descriptor set layout with zero bindings, but `Device.CreateDescriptorSetLayout` rejects an empty `Bindings` span, so there is nothing this call can return"* |

And the coupling is stated in four documents, each of which promises this exact
retirement:

- `src/Ahjo.Vulkan.Slang/CLAUDE.md:324-343` — the "Known gap: no zero-binding
  descriptor set layout" section, ending *"That refusal is scoped to 'input
  non-empty, output empty' and becomes a `return []` the day `Ahjo.Vulkan`
  accepts an empty span."*
- `SlangVulkanMapping.cs:296-302` and `:363-369` — the XML `<remarks>` paragraph
  on both `MapBindings` overloads, plus the `<exception>` tags at `:304-309` and
  `:375-378`.
- `src/Ahjo.Vulkan.Slang/README.md:598-605` — the "> **Open gap.**" blockquote.
- `src/Ahjo.Vulkan.Slang/SlangReflection.cs:187-196` — the `SetLayoutSlotCount`
  remark, *"A `false` from `TryGetSet` has no answer in the wrapper today."*

Two tests assert the refusal and both must be rewritten, not deleted — the shapes
they cover stay interesting, only the expected outcome changes:
`MapBindings_EverythingZeroCount_ThrowsNamingTheGap`
(`tests/Ahjo.Vulkan.Slang.Tests/SlangReflectionTests.cs:463-482`) and
`MapBindings_ZeroCountInItsOwnSet_StillMapsTheOtherSet` (`:488-518`, whose
second half asserts the same throw). The fixture doc comment at
`tests/Ahjo.Vulkan.Slang.Tests/ShaderFixtures.cs:1010-1022` repeats the claim.

The singular overloads are **not** coupled to this issue.
`MapBinding(binding)` (`:180-202`) and `MapBinding(binding, count)` (`:234-263`)
refuse a `Fixed(0)` binding because a method returning one `DescriptorBinding`
has no value meaning "nothing" (#183 §D3/D4). That reasoning is untouched by an
empty *set* becoming expressible.

### E9 — doc drift found while auditing

`src/Ahjo.Vulkan.Slang/CLAUDE.md:313-317` describes
`Device.CreateDescriptorSetLayout` as the method "which validates only that the
`Bindings` span is non-empty". That was already stale when `1027c0a` (#192)
added the variable-descriptor-count ordering check, and this change makes it
doubly wrong. The sentence's actual point — that a duplicate `(set, slot)` pair
surviving #180's correction reaches Vulkan unchecked by the wrapper — is still
true and worth keeping; the parenthetical needs correcting.

### E10 — two different zeros, not to be conflated

#119 and #191 are about different numbers and this spec does not touch the first:

| | #119's zero | #191's zero |
|---|---|---|
| the value | `DescriptorBinding.Count == 0` | `DescriptorSetLayoutDescription.Bindings.Length == 0` |
| what it means | a **sentinel**: a `default(DescriptorBinding)` array element bypassed the `Count = 1` field initializer (`Pipelines/DescriptorBinding.cs:31, 39`) | a **real count**: this set has no bindings |
| wrapper behaviour | normalized to `1` at `Device.cs:218` and `DescriptorTemplate.cs:167`; guarded by `ValidByDefaultDescriptionTests.CreateDescriptorSetLayout_DefaultBindingElement_NormalizesCount` (`tests/Ahjo.Vulkan.Tests/ValidByDefaultDescriptionTests.cs:163-188`) | rejected outright today |
| this spec | **untouched** — `src/Ahjo.Vulkan.Slang/CLAUDE.md:295-301` explicitly says "do not 'fix' that guard" | changed |

A consequence worth stating plainly: after this change `Bindings = []` produces a
layout with zero bindings, while `Bindings = new DescriptorBinding[1]` (one
zeroed element) still produces a layout with **one** binding of one descriptor.
The two look adjacent and mean opposite things, and that asymmetry is deliberate
in both directions.

### E11 — the budget-less pool, audited

Gathered after the maintainer resolved this spec's original OPEN-1 as **INCLUDE**
(see *Overruled*, below). Five questions; the second and the fifth are the ones
that changed the shape of the answer.

**E11.1 — is `poolSizeCount = 0` legal?** Yes, checked rather than assumed. The
complete VUID set for `VkDescriptorPoolCreateInfo` in `validusage.json` (13
entries) contains **no** `poolSizeCount-arraylength` VUID. The only entry that
mentions the array is conditional:

> `VUID-VkDescriptorPoolCreateInfo-pPoolSizes-parameter` — *If `poolSizeCount` is
> **not 0**, `pPoolSizes` must be a valid pointer to an array of `poolSizeCount`
> valid `VkDescriptorPoolSize` structures*

The contrast is the evidence, and it is a real contrast: the same file *does*
carry `VUID-VkDescriptorSetAllocateInfo-descriptorSetCount-arraylength` and
`VUID-VkDescriptorUpdateTemplateCreateInfo-descriptorUpdateEntryCount-arraylength`
("must be greater than 0"), so the registry spells arraylength rules out when it
means them. `maxSets` **must** be greater than 0
(`VUID-VkDescriptorPoolCreateInfo-descriptorPoolOverallocation-09227`), which the
wrapper already enforces at `DescriptorSetPool.cs:137` and which this change does
not touch.

Wrapper mechanics line up: `_poolSizes = poolSizes.ToArray()` (`:166`) yields a
zero-length array, and `fixed (VkDescriptorPoolSize* pSizes = _poolSizes)`
(`:403`) over a zero-length array is *specified* to produce `null`, so
`CreatePool` emits `poolSizeCount = 0, pPoolSizes = null` — exactly the shape the
VUID above excuses. Same mechanism as the layout case in §E2.

**E11.2 — `_maxPerTypeDescriptorTotal` becomes `0`, and that is a genuinely new
state.** The constructor's per-type loop (`:148-161`) iterates `poolSizes.Length`
times, so an empty template leaves the field at its `0` seed (`:148`). **That
value is unreachable today:** `VUID-VkDescriptorPoolSize-descriptorCount-00302`
requires every entry's `descriptorCount` to be greater than 0, so any legal
non-empty template yields a total of at least 1. The relaxation therefore hands
#182's pre-flight guard a value it has never seen — the sharpest question in this
audit.

The guard (`:266-273`) is `if (variableDescriptorCount > _maxPerTypeDescriptorTotal) throw`.
With the total at `0`:

| call | comparison | outcome | correct? |
|---|---|---|---|
| `Acquire(layout)` → `Acquire(layout, 0)` (`:200-201`) | `0 > 0` → false | proceeds to allocate | **yes** — a zero-binding layout needs no descriptors of any type |
| `Acquire(layout, n)`, `n ≥ 1` | `n > 0` → true | `ArgumentOutOfRangeException` | **yes** — a pool with no budget holds zero descriptors of every type, so no variable count ≥ 1 is satisfiable by any sub-pool built from this template |

So the guard does not misbehave, and it does not merely tolerate the new state —
it **degrades to exactly the right answer**. It stays what its comment at
`:256-265` insists it must stay, a necessary-condition test; the empty template is
simply the strongest necessary condition it can express. The comparison needs no
change.

Its *message* does. It ends *"Size `poolSizes` for the sum, over the live sets, of
all descriptors of that type"*, which is odd advice for a pool created with no
`poolSizes` at all. D4 adds an empty-template branch.

**E11.3 — the auto-grow retry, which is where this could have gone wrong.** The
retry (`:296-306`) is a single shot, not a loop: one
`if (_growOnExhaustion && IsExhaustion(result))` block that builds one sub-pool
and re-attempts once. Two cases, and they diverge:

*The layout has zero bindings — the intended use.* The only way such an
allocation can fail is `maxSets` exhaustion, and a fresh sub-pool restores the
full `maxSets`. So the retry's justifying comment (`:290-295`, "a fresh sub-pool
built from the same template fits the requested binding shape") is **true** for a
budget-less pool: the requested shape is "no descriptors", which any sub-pool
fits. Sound, and — see below — measurable here.

*The layout has real bindings — the misuse.* Per spec the allocation fails with
`VK_ERROR_OUT_OF_POOL_MEMORY`, the retry builds a second budget-less sub-pool,
that fails identically, and `result.ThrowIfFailed()` (`:309`) surfaces the
failure — after one permanently chained `VkDescriptorPool` that can never serve
anyone. **That is precisely the residue #187 already documents**, reached by a new
route: #187 needs a mis-sized variable count through `Acquire(layout, count)`,
whereas a budget-less pool reaches it through the plain `Acquire(layout)` overload
with any ordinary layout. So this change **widens an already-known, already-filed
defect** rather than creating one. It is bounded — one wasted sub-pool per doomed
call, then a throw; no loop, no corruption, no silent wrong result.

**The misuse half cannot be tested here, and must not be.** #187 measured that
this repo's only driver (RTX 4070 Ti, NVIDIA 610.47.0.0, validation layer 1.4.341)
"enforces `maxSets` only" and "never returned `VK_ERROR_OUT_OF_POOL_MEMORY` for a
per-type over-subscription" — 17 descriptors allocated from a 16-descriptor pool
succeeded, 26 from 16 succeeded. On this box a real layout allocated from a
budget-less pool will therefore most likely just **succeed**, and a test asserting
the spec-mandated failure would be red for the wrong reason.
**Everything in the preceding two paragraphs about the misuse case is
spec-derived, not measured.** Stated plainly because the difference is the point.

**The intended half is fully testable here**, and for a reason worth naming: a
budget-less pool's *only* exhaustion mode is `maxSets`, which is the one mode this
driver does enforce. The suite already proves the mechanism —
`Pool_AcquireBeyondMaxSets_GrowsAndSucceeds`
(`tests/Ahjo.Vulkan.Tests/DescriptorSetPoolTests.cs:101-130`) passes, and it can
only pass if `IsExhaustion(result)` was true and growth ran; its sibling
`Pool_GrowDisabled_AcquireBeyondBudget_Throws` (`:179-202`) proves the same
`VkResult` surfaces when growth is off.

Three shapes were considered for containing the widened residue; two are not
merely unattractive but *wrong*:

- *Suppress auto-grow when `_poolSizes.Length == 0`.* **Wrong.** A budget-less
  pool's legitimate exhaustion mode is `maxSets` and growth is the correct
  response to it; `VkResult` cannot distinguish the two exhaustion causes, so this
  would break the intended use to mitigate the unintended one.
- *Pre-flight-reject a layout that has bindings.* **Impossible.** The pool holds a
  raw `VkDescriptorSetLayout_T*` and Vulkan exposes no way to read a layout's
  bindings back from a handle — the file says so twice (`:186-198`, `:256-265`).
- *Roll the fresh sub-pool back when the retry also fails.* **Correct**, about five
  lines, and it would close both routes — but it is #187's fix for a defect that
  predates this issue, is equally untestable on this driver, and belongs in its own
  PR.

What is left is documentation, which is the posture this file already takes for
every fact it cannot check from a handle (`:186-198`, `:214-235`, `:256-265`).

**E11.4 — a budget-less pool is useful for exactly one thing, and the docs should
say so.** Holding zero descriptors of every type, the only layout it can serve on
a conformant driver is one with zero bindings. That is the feature, not a
limitation to be papered over: #191's sparse-set hole needs a *layout*, and a
caller who additionally wants a *set* for that hole — to bind sets 0..2 in one
`vkCmdBindDescriptorSets` instead of two calls around the gap — now gets one from
a pool that costs no descriptor budget at all. D4 scopes the change to that and
does not pretend the wrapper can enforce it.

**E11.5 — `FrameRing` already gives an empty `poolSizes` span a *different*
meaning.** The only `DescriptorSetPool` constructor call in `src/` is
`Pools/FrameRing.cs:252-254`, and it never passes an empty span:

```csharp
descSets = descriptorPoolSizes.IsEmpty
    ? null
    : new DescriptorSetPool(device, descriptorMaxSets, descriptorPoolSizes);
```

`descriptorPoolSizes.IsEmpty` is FrameRing's own opt-out sentinel for "this slot
gets **no** descriptor pool". Its argument guards go further — `FrameRing.cs:57-63`
rejects empty `poolSizes` together with a non-zero `descriptorMaxSets` with *"pass
both or neither"*, which is exactly the combination that would spell "give every
slot a budget-less pool".

Two consequences. First, **no existing behaviour changes**: FrameRing branches
before the relaxed guard could ever be reached, and the other 17 constructor call
sites are tests and benchmarks that all pass a literal one- or two-entry template
(18 sites total, counted repo-wide). Second, the two layers now disagree about
what an empty span *means* — a legitimate budget-less pool at the
`DescriptorSetPool` layer, "no pool" at the `FrameRing` layer. That is a coherence
wart, not a defect, and D4 resolves it conservatively: FrameRing keeps its
sentinel and gains a comment pinning it, because redefining it is a behaviour
change on a per-frame path for a use case nobody has asked for.

---

## Decision

**Delete the guard. An empty `Bindings` span produces a zero-binding
`VkDescriptorSetLayout`, spelled the ordinary way, with no opt-in.**

| # | decision |
|---|---|
| **D1** | Remove `Device.cs:198-199`. Nothing replaces it — §E2 shows the body is already correct for the empty case. Update the `<exception>` doc at `:190-195` to drop "has no bindings", and add a `<remarks>` paragraph stating that an empty span is legal, is the layout Vulkan wants for an unpopulated set index, and cites `VUID-VkDescriptorSetLayoutCreateInfo-pBindings-parameter`. |
| **D2** | **No explicit spelling and no opt-in flag** (the issue's question 3). `new DescriptorSetLayoutDescription { Bindings = [] }` and `default` both work. Reasoning below. |
| **D3** | `DescriptorTemplateBuilder.BuildEntries`'s empty guard **stays** (§E5), with its message rewritten to cite `VUID-VkDescriptorUpdateTemplateCreateInfo-descriptorUpdateEntryCount-arraylength` and to say that a zero-binding layout is legal but cannot carry an update template. Otherwise the first thing a reader will do after D1 is "fix" this one too. |
| **D4** | `DescriptorSetPool`'s "poolSizes must contain at least one entry" guard (`:138-139`) is **also removed** (§E11), with three supporting edits and no logic change elsewhere: a contract paragraph on the `poolSizes` `<param>` scoping a budget-less pool to zero-binding layouts (§E11.4), comments pinning the two newly-reachable states (§E11.1, §E11.2), and an empty-template branch in the `Acquire` pre-flight message (§E11.2). The comparison at `:266`, the auto-grow retry and `CreatePool` are untouched. `FrameRing` is **comment-only** — its empty-span opt-out stays (§E11.5). |
| **D5** | The #183 workaround retires: both `MapBindings` overloads drop the `bindings.Length != 0 && kept == 0` refusal and return the (possibly empty) array. `EmptySetMessage` is deleted. `CountMappable` stays — it still sizes the result. Both `MapBinding` singular overloads keep their `Fixed(0)` refusals unchanged (§E8). |
| **D6** | Four documents and the `SlangReflection` XML lose the gap; `CLAUDE.md:313-317`'s stale parenthetical is corrected (§E9). |

### Overruled: OPEN-1 resolved INCLUDE, against this spec's recommendation

The first draft recommended deferring the `DescriptorSetPool` relaxation to a
follow-up issue. The maintainer made the opposite scope call, and D4 implements
it. Both sides are recorded, because a reader six months from now should be able
to see that this was a judgement call and what the judgement rested on — not a
record rewritten to look unanimous.

*For deferring (this spec's original position):* the two changes are independent
— nothing in #191's layout story needs a budget-less pool, since a hole needs a
*layout* and a caller wanting a *set* can pass a one-entry template — and folding
a second API's guard into the PR widens the review surface of a change whose whole
value is that it is small. The auto-grow interaction at
`DescriptorSetPool.cs:296-306` had not been audited at the time the
recommendation was made, and recommending a change whose risk is unmeasured is
worse than deferring it.

*For including (the call taken):* the two guards are the same mistake in the same
file family — a wrapper-invented "at least one entry" rule with no VUID behind it
— and the audit that justifies deleting one is the audit that justifies deleting
the other. Splitting them ships a wrapper that accepts a zero-binding layout and
then cannot allocate a set for it without a fictitious pool budget, which is half
a feature. §E11 was written to discharge the evidence objection and does: the one
genuinely new state (`_maxPerTypeDescriptorTotal == 0`) is sound by construction
(§E11.2), the intended growth path is measurable on this driver (§E11.3), and the
one unsound path is a documented pre-existing defect (#187) reached by a new
route rather than a new defect (§E11.3). Nothing in the audit argues against
implementing it; the residual risk is recorded as Risk 4 rather than hidden.

### Why D2 — no `Empty`, no `AllowNoBindings`

The issue asks whether an empty layout should be reachable only through an
explicit spelling, so a mis-sized array cannot produce one by accident. The
answer is no, for three reasons in descending weight:

1. **The guard would be defeated exactly where the accident could occur.** The
   one call site in this repo where emptiness is data-dependent is the reflection
   loop (`SlangReflection.cs:177-186`): the caller writes
   `Bindings = bindings.MapBindings()`, and after D5 that array is empty for an
   all-zero-count set. Under an opt-in flag, that loop must set the flag
   unconditionally — because it cannot know in advance — at which point the flag
   catches nothing and is pure ceremony. Under a separate `Empty` spelling, the
   loop grows a `mapped.Length == 0 ? … : …` conditional, which is a new
   easy-to-forget branch in the one place we are trying to make simple.
2. **The accident has a loud, existing backstop one call later.** A layout that
   is empty by mistake is a layout that does not declare a binding the shader
   uses, which the validation layer reports at *pipeline creation* under
   `VUID-Vk{Graphics,Compute}PipelineCreateInfo-layout-07988` / `-07990`
   ("*If a resource variable is declared in a shader … the corresponding
   descriptor set in `layout` must match the shader stage / the descriptor
   type*", quoted from `validusage.json`). The wrapper's own rule is to add a
   check when it can name the mistake *better* than the layer at the call site
   that made it (`Device.cs:259-267`) — here it cannot: the wrapper has no idea
   what the shader declares, and refusing a legal construct to approximate a
   check the layer performs properly is the trade #191 exists to undo. *Bounded
   claim:* the VUID text is quoted from the registry; the layer's exact message
   for the specifically-empty layout was not measured, and the plan's step 8
   measures it rather than asserting it here.
3. **It would fight the repo's own convention.** #119 established that a
   description struct's `default`/`new()` is valid and means the obvious thing
   (`docs/design/specs/2026-06-12-issue-119-valid-by-default-descriptions-design.md`).
   `DescriptorSetLayoutDescription` has no other member with an opt-in gate, and
   no other description type in `Pipelines/` carries an `Empty`. A second
   spelling for one value invites "is `Empty` different from `default`?" forever.

### Why not the alternatives

- **Keep the guard; let Slang keep refusing.** Rejected: it preserves a
  capability regression (#183 §D6) and leaves shape 1 permanently
  inexpressible, for a check with no VUID, no comment, no test and no call site
  that benefits (§E1, §E4).
- **Keep the guard; add `Device.CreateEmptyDescriptorSetLayout()`.** Rejected:
  two wrapper methods for one Vulkan call, and the reflection loop still has to
  branch on `mapped.Length`.
- **Add `DescriptorSetLayoutDescription.AllowNoBindings`.** Rejected: see D2.1 —
  the primary consumer must set it unconditionally, so it protects nothing while
  costing every future reader a question.
- **Add a static `DescriptorSetLayoutDescription.Empty` as required spelling.**
  Rejected for the same reason; as *optional* sugar it is redundant with
  `default` and conflicts with the #119 convention (D2.3).
- **Allow empty, but `AhjoValidation.Fail`/warn in debug builds.** Rejected: an
  empty layout is *correct* in the sparse-set case, which is a normal shape, so
  the diagnostic would fire on working code — the definition of noise.
- **Leave `DescriptorSetPool`'s `poolSizes` guard alone** (this spec's original
  recommendation). Overruled on scope; §E11 supplies the evidence the
  recommendation said was missing, and that evidence does not argue against the
  change.
- **Relax the pool guard but suppress auto-grow for a budget-less pool.**
  Rejected as *wrong*, not merely unattractive — §E11.3: `maxSets` exhaustion is a
  budget-less pool's legitimate growth trigger and `VkResult` cannot distinguish
  it from descriptor exhaustion.
- **Relax the pool guard and pre-flight-reject layouts that have bindings.**
  Rejected as impossible: a layout's bindings are not readable back from a
  `VkDescriptorSetLayout` handle (`DescriptorSetPool.cs:256-265`).
- **Relax the pool guard and roll back the failed retry's sub-pool.** Correct and
  about five lines, but it is #187's documented fix for a defect that predates
  this issue and is equally untestable on this repo's driver — its own PR.
- **Add a second `DescriptorSetPool` constructor overload for the budget-less
  case.** Rejected: the D2 argument again — a second spelling for
  `poolSizes: []`, with the same "is it different?" tax and no accident prevented.
- **Redefine `FrameRing`'s empty-`descriptorPoolSizes` opt-out to match.**
  Rejected — §E11.5: a behaviour change on a per-frame path, for a use case
  nobody has asked for.
- **Also revisit `DescriptorBinding.Count == 0`.** Rejected: it is #119's
  sentinel, a different question about a different number (§E10), and
  `src/Ahjo.Vulkan.Slang/CLAUDE.md:295-301` forbids touching it.

### What this does not change

- No behavioural change for any existing call site. §E4: no
  `CreateDescriptorSetLayout` caller passes an empty span. §E11.5: the only
  `DescriptorSetPool` constructor call in `src/` (`FrameRing.cs:252-254`) branches
  on `IsEmpty` before it could reach the relaxed guard, and the other 17 sites all
  pass a literal template.
- No hot-path *logic* change. `CreateDescriptorSetLayout` and
  `DescriptorTemplateBuilder` are setup-time. `DescriptorSetPool.Acquire`
  **is** benchmarked (`docs/benchmarks.md:89-91`), and D4 touches it — but only
  inside the already-throwing branch of a guard whose comparison is unchanged, so
  the success path executes the same instructions. That is a claim to *verify*,
  not assert: the plan requires a `*DescriptorSetPool*` benchmark run, and the
  empty-template message must be built inside the throw, never before the
  comparison.
- No AOT surface, no new `const char*`, no allocation added on any path.
- No new public API member — D2 is precisely the decision not to add one.

---

## Risks

1. **A caller who mis-sizes an array now gets a layout instead of an exception.**
   Accepted, with the backstop in D2.2. Blast radius: the layout builds, the
   pipeline creation that uses it fails under the validation layer. This is
   strictly the same class of error as passing the *wrong* binding, which the
   wrapper has never caught either.
2. **Someone later "simplifies" `DescriptorTemplate`'s guard away by symmetry.**
   Mitigated by D3's message rewrite, which puts the VUID in the exception text
   itself.
3. **The empty layout is created but never bound, and a shader in that set
   silently reads garbage.** Not a new risk: identical to declaring the wrong
   descriptor type, and covered by the layer.
4. **The pool relaxation widens #187's residue.** A budget-less pool asked for a
   layout that *has* bindings leaks one chained `VkDescriptorPool` before throwing,
   on a driver that enforces per-type accounting (§E11.3). Pre-existing defect,
   new route, bounded to one sub-pool per doomed call, and **not observable on this
   repo's driver**. Mitigated by documentation only, because the two runtime
   mitigations are respectively wrong and impossible. Closing it is #187's
   rollback, in its own PR. This is the one thing the audit found that makes the
   scope call riskier than it looked.
5. **`FrameRing` and `DescriptorSetPool` now read an empty `poolSizes` span
   differently** (§E11.5). No behaviour change today; the risk is a future reader
   "harmonizing" them into one. Contained by comments at `FrameRing.cs:57-63` and
   `:252-254` — in the code, not only in this spec, because that is where the
   reader will be.

---

## Cross-links

- **Resolves** #191. Answers **OPEN-1** of
  `docs/design/specs/2026-08-03-issue-183-zero-descriptor-count-design.md:442-451`
  in the affirmative, retiring that spec's §D6 capability regression.
- **Must land consistently with** #183 (`f0c6e9b`): `MapBindings` omits
  zero-count bindings and the singular `MapBinding` overloads refuse them —
  unchanged. Only the whole-set refusal goes.
- **Must land consistently with** #192 (`1027c0a`): the variable-descriptor-count
  ordering check stays and is vacuous on an empty span (§E2).
- **Does not touch** #119's `Count == 0 → 1` normalization or its test (§E10).
- **Unblocks** the sparse-set story `SlangReflection.SetLayoutSlotCount` /
  `TryGetSet` were built for (#166, #180) — a program with a hole becomes a
  complete `PipelineLayout` for the first time.
- **Touches but does not resolve** #180's OPEN-1 (duplicate `(set, slot)` is
  still unchecked); only the stale description of what
  `CreateDescriptorSetLayout` validates is corrected (§E9).
- **Widens, and does not close, the residue documented by #187** (auto-grow keeps
  a sub-pool that cannot satisfy the request) — a budget-less pool reaches it
  through the plain `Acquire(layout)` overload rather than through a mis-sized
  variable count (§E11.3). #187's rollback fix stays its own work.
- **Must land consistently with** #182 (`5a30908`): the `Acquire(layout, count)`
  pre-flight guard keeps its comparison and its necessary-condition contract;
  only its message gains an empty-template branch (§E11.2).
- **Interacts with** #60 (auto-grow on pool exhaustion): a budget-less pool's only
  exhaustion mode is `maxSets` — the mode #60's growth path was built for, and the
  one this repo's driver actually enforces (§E11.3).

---

## OPEN items

**None.** The original OPEN-1 — whether to relax `DescriptorSetPool`'s
`poolSizes` guard here — was resolved **INCLUDE** by the maintainer, against this
spec's recommendation. See D4 for the decision, *Overruled* for both sides of the
argument, and §E11 for the evidence gathered to support implementing it. One
correction the audit forced on the original framing, recorded because it was
wrong in the deferral rationale: the auto-grow path does **not** "loop building
sub-pools" — the retry is a single shot (§E11.3), so the failure is one wasted
sub-pool and a throw, not an unbounded leak.

Two things are deliberately *scoped out* rather than open, so the implementer does
not read them as invitations:

- **#187's sub-pool rollback** — the five-line fix that would close both routes
  into the leaked-sub-pool residue. Its own issue, its own PR, untestable on this
  driver (§E11.3).
- **`FrameRing`'s empty-span opt-out** — stays exactly as it is; comment only
  (§E11.5).

One thing the implementer must **stop and report** rather than route around: if
`vkCreateDescriptorPool` with `poolSizeCount = 0` fails on the test box, that
contradicts §E11.1, which is registry-derived rather than measured. The plan's
step 8e constructor test is where it would surface, and it is a finding for the
maintainer, not a bug to be worked around.
