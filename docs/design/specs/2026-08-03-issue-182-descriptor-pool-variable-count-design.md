# Variable-descriptor-count allocation through `DescriptorSetPool`

**Issue:** [#182](https://github.com/pekkah/Ahjo-Vulkan/issues/182) — *Pools: DescriptorSetPool cannot allocate a variable-descriptor-count set, so bindless heaps are unreachable*
**Closes the arc opened by:** [#176](https://github.com/pekkah/Ahjo-Vulkan/issues/176) (reflection reports an unbounded array instead of refusing the program — shipped in 241541f)
**Lands consistently with:** [#114](https://github.com/pekkah/Ahjo-Vulkan/issues/114) (Reset keeps the per-layout idle `Stack`s — the allocation canary this must not regress), [#60](https://github.com/pekkah/Ahjo-Vulkan/issues/60) (auto-grow on pool exhaustion), [#118](https://github.com/pekkah/Ahjo-Vulkan/issues/118) (handle metadata rides the handle struct)
**Motivating consumer:** [#68](https://github.com/pekkah/Ahjo-Vulkan/issues/68) — Logos, whose set 0 is three unbounded arrays sized by its own heap
**Date:** 2026-08-03

## Problem

`DescriptorSetPool` has exactly one `Acquire` overload
(`Pools/DescriptorSetPool.cs:127`):

```csharp
public DescriptorSet Acquire(VkDescriptorSetLayout_T* layout)
```

*(The issue body writes the return type as `VkDescriptorSet_T*`; it is
`DescriptorSet`. Everything else in the issue's framing holds.)*

The allocate-info it builds has no `pNext`
(`Pools/DescriptorSetPool.cs:270-276`):

```csharp
var ai = new VkDescriptorSetAllocateInfo
{
    sType              = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO,
    descriptorPool     = current,
    descriptorSetCount = 1,
    pSetLayouts        = &layout,
};
```

So a layout built with `DescriptorBindingFlags.VariableDescriptorCount`
(`Pipelines/DescriptorBindingFlags.cs:16`, plumbed through
`Lifecycle/Device.cs:213`) can be created but not usefully allocated: the
variable binding's effective count is **zero**, and every descriptor write and
every shader access into the array is out of range. `DescriptorBinding`'s own
XML doc tells callers to use that flag for bindless arrays
(`Pipelines/DescriptorBinding.cs:11-13`) — the wrapper documents a path it does
not implement.

`src/Ahjo.Vulkan.Slang/README.md:391-400` is currently an honest blockquote
saying exactly this ("`DescriptorSetPool.Acquire` has no overload that chains
it … Allocate such a set yourself until the wrapper grows the overload").

## Evidence

### E1 — The struct and its chain contract are already generated

`VkDescriptorSetVariableDescriptorCountAllocateInfo` exists at
`src/Ahjo.Vulkan.Native/Generated/VkDescriptorSetVariableDescriptorCountAllocateInfo.cs:3-15`
with `descriptorSetCount` / `pDescriptorCounts`, and its chain partial at
`Generated/Chains/VkDescriptorSetVariableDescriptorCountAllocateInfo.Chain.g.cs:6`
declares `IChainable<VkDescriptorSetAllocateInfo>` with the right `sType`.
**No `/regen-bindings` run is required** for this work — the design touches
hand-written code only.

### E2 — Consumer audit: 18 `Acquire` call sites, none in `src/` or `samples/`

| Location | Count |
|---|---|
| `tests/Ahjo.Vulkan.Tests/DescriptorSetPoolTests.cs:32,55,57,81,87,120,154,164,196,197,198,246` | 12 |
| `tests/Ahjo.Vulkan.Tests/DescriptorTemplateTests.cs:126` | 1 |
| `tests/Ahjo.Vulkan.Tests/DescriptorWriteTests.cs:199` | 1 |
| `tests/Ahjo.Vulkan.Tests/FrameRingTests.cs:212` | 1 |
| `tests/Ahjo.Vulkan.Tests/PipelineLayoutTests.cs:150` | 1 |
| `tests/Ahjo.Vulkan.Benchmarks/DescriptorSetPoolBenchmarks.cs:84,104` | 2 |
| `src/`, `samples/` | **0** |

Every one passes a bare layout handle. Adding an overload therefore breaks no
call site and requires no migration; the existing overload's behaviour is what
all 18 depend on.

`FrameRing` constructs a `DescriptorSetPool` per in-flight slot
(`Pools/FrameRing.cs:204,262`), resets it every `BeginFrame`
(`FrameRing.cs:353`), and exposes it as `FrameContext.DescriptorSets` — but
never calls `Acquire` itself. Any overload added to the pool is automatically
reachable from the frame path.

### E3 — The free-list is keyed by layout alone, and that is now unsound

```csharp
private readonly Dictionary<nint, Stack<nint>> _idle = new();   // DescriptorSetPool.cs:67
```

with the comment at `:64-66` — "Different layouts allocate to different binding
shapes, so a set built for layout A can't be returned to a caller asking for
layout B." Under variable counts, **one layout no longer means one binding
shape**. `Acquire` pops any idle set for the layout (`:132-133`) and `Release`
pushes by the layout the set carries (`:200-203`). So
`Acquire(layout, 4)` → `Release` → `Acquire(layout, 64)` hands back a set whose
variable binding holds 4 descriptors, to a caller who believes it holds 64.

This is not theoretical — see M4 below, where the validation layer rejects the
write with the *allocated* count quoted back.

### E4 — The auto-grow comment becomes false

`DescriptorSetPool.cs:146-148`:

> Exhaustion is the only retry-able failure: a fresh sub-pool built from the
> same template **guarantees the requested binding shape fits**, and a
> brand-new pool can't already be fragmented.

That guarantee holds only while the requested shape is a function of the layout
alone. Once the count comes from a runtime argument, `Acquire(layout, 4096)`
against a pool template with `descriptorCount = 1024` for that type is
unsatisfiable by construction, and the retry at `:149-159` creates a fresh
sub-pool that also cannot satisfy it — one permanently chained
`VkDescriptorPool` per doomed call, on drivers that enforce pool accounting.

### E5 — `DescriptorSet` already carries routing metadata; one field is the precedent

`Pipelines/DescriptorSet.cs:27` holds `internal readonly
VkDescriptorSetLayout_T* Layout`, added (per its comment at `:21-26`) precisely
so `Release` "can assert it matches the layout the caller is claiming, instead
of trusting the caller and silently routing to the wrong layout-keyed
free-list." The variable count is the same category of fact about the same
object.

Growing the struct is safe: `CommandRecorder.BindDescriptorSets` unwraps
`ReadOnlySpan<DescriptorSet>` into `stackalloc nint[]` element-by-element
(`Recording/CommandRecorder.cs:241-243`), and there is no `sizeof`,
`MemoryMarshal.Cast`, or `stackalloc DescriptorSet` anywhere in `src/`,
`samples/` or `tests/` (grepped). `DescriptorSetExtensions.cs:8-11` documents
the struct as "one pointer + one layout pointer" and will need updating.

A struct key in a `Dictionary` is established practice here and AOT-clean:
`Diagnostics/HandleRegistry.cs:50-51` uses
`HashSet<(VkObjectType Type, ulong Handle)>`.

### E6 — Feature detection already exists; only the allocation path is missing

`tests/Ahjo.Vulkan.Tests/VulkanDriverProbe.cs:103-112` exposes
`SupportsBindlessSampledImage`, which requires
`descriptorBindingPartiallyBound` + `descriptorBindingVariableDescriptorCount`
+ `descriptorBindingSampledImageUpdateAfterBind` (`:107-110`).
`PipelineLayoutTests.cs:51-54` gates on it, and `:266-296`
(`CreateBindlessGraphicsDevice`) is a ready-made device factory that enables
the three bits. New tests reuse this, not a parallel probe.

The wrapper itself has no equivalent: `Device` exposes no enabled-feature
snapshot, and Vulkan offers no API to read a `VkDescriptorSetLayout`'s binding
flags back from the handle.

### E7 — Validation messages are already a test-visible oracle

`InstanceDescription.DebugCallback` is an `Action<DebugMessage>`
(`Lifecycle/InstanceDescription.cs:25`) and `DebugMessage` carries
`MessageIdName` (`Lifecycle/DebugMessage.cs:11-16`), so a test can assert on an
exact VUID string. `CommandRecorderTests.cs:741-791`
(`PushConstants_64ByteStruct_PassesValidation`) is the pattern: `TestGate.
RequireDriver()` + `TestGate.RequireValidationLayer()`, capture errors, assert
the list is empty. `MemoryAliasingTests.cs:110-124` is the softer variant.

### E8 — Verified VUIDs

Read from `C:\VulkanSDK\1.4.341.1\share\vulkan\registry\validusage.json`
(schema 2, api version **1.4.341**, commit `ac8223b3`, dated 2026-01-23).
Quoted verbatim, markup stripped:

- **`VUID-VkDescriptorSetVariableDescriptorCountAllocateInfo-descriptorSetCount-03045`**
  — "If descriptorSetCount is not zero, descriptorSetCount must equal
  VkDescriptorSetAllocateInfo::descriptorSetCount"
- **`VUID-VkDescriptorSetVariableDescriptorCountAllocateInfo-pDescriptorCounts-parameter`**
  — "If descriptorSetCount is not 0, pDescriptorCounts must be a valid pointer
  to an array of descriptorSetCount uint32_t values"
- **`VUID-VkDescriptorSetAllocateInfo-pSetLayouts-09380`** — "If pSetLayouts[i]
  was created with an element of pBindingFlags that includes
  VK_DESCRIPTOR_BINDING_VARIABLE_DESCRIPTOR_COUNT_BIT, and
  VkDescriptorSetVariableDescriptorCountAllocateInfo is included in the pNext
  chain, and
  VkDescriptorSetVariableDescriptorCountAllocateInfo::descriptorSetCount is not
  zero, then
  VkDescriptorSetVariableDescriptorCountAllocateInfo::pDescriptorCounts[i] must
  be less than or equal to VkDescriptorSetLayoutBinding::descriptorCount for
  the corresponding binding used to create pSetLayouts[i]"
- **`VUID-VkDescriptorSetAllocateInfo-pNext-pNext`** — "pNext must be NULL or a
  pointer to a valid instance of
  VkDescriptorSetVariableDescriptorCountAllocateInfo"
- **`VUID-VkDescriptorSetAllocateInfo-pSetLayouts-03044`** — "If any element of
  pSetLayouts was created with the
  VK_DESCRIPTOR_SET_LAYOUT_CREATE_UPDATE_AFTER_BIND_POOL_BIT bit set,
  descriptorPool must have been created with the
  VK_DESCRIPTOR_POOL_CREATE_UPDATE_AFTER_BIND_BIT flag set" *(already honoured
  and cited by the pool's `updateAfterBind` ctor param,
  `DescriptorSetPool.cs:88-89`)*
- **`VUID-VkDescriptorSetLayoutBindingFlagsCreateInfo-pBindingFlags-03004`** —
  "If an element of pBindingFlags includes
  VK_DESCRIPTOR_BINDING_VARIABLE_DESCRIPTOR_COUNT_BIT, then it must be the
  element with the highest binding number"
- **`VUID-VkDescriptorSetLayoutBindingFlagsCreateInfo-descriptorBindingVariableDescriptorCount-03014`**
  — "If
  VkPhysicalDeviceDescriptorIndexingFeatures::descriptorBindingVariableDescriptorCount
  is not enabled, all elements of pBindingFlags must not include
  VK_DESCRIPTOR_BINDING_VARIABLE_DESCRIPTOR_COUNT_BIT"
- **`VUID-VkDescriptorSetLayoutBindingFlagsCreateInfo-pBindingFlags-03015`** —
  "If an element of pBindingFlags includes
  VK_DESCRIPTOR_BINDING_VARIABLE_DESCRIPTOR_COUNT_BIT, that element's
  descriptorType must not be VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER_DYNAMIC or
  VK_DESCRIPTOR_TYPE_STORAGE_BUFFER_DYNAMIC"
- **`VUID-VkDescriptorSetLayoutBindingFlagsCreateInfo-flags-03003`** — "If
  VkDescriptorSetLayoutCreateInfo::flags includes
  VK_DESCRIPTOR_SET_LAYOUT_CREATE_PUSH_DESCRIPTOR_BIT, then all elements of
  pBindingFlags must not include VK_DESCRIPTOR_BINDING_UPDATE_AFTER_BIND_BIT,
  VK_DESCRIPTOR_BINDING_UPDATE_UNUSED_WHILE_PENDING_BIT, or
  VK_DESCRIPTOR_BINDING_VARIABLE_DESCRIPTOR_COUNT_BIT"

There is **no** VUID constraining `pDescriptorCounts[i]` when the binding does
*not* carry the flag: 09380 is conditioned on it. That absence is what makes
the "chain it unconditionally" alternative merely wasteful rather than invalid
— see M3.

Note that all four **layout**-side rules (03003, 03004, 03014, 03015) bind
`vkCreateDescriptorSetLayout`, i.e. `Device.CreateDescriptorSetLayout`
(`Lifecycle/Device.cs:190-249`). None of them is checkable from
`DescriptorSetPool`.

### E9 — Measurements against a real driver

Probed with a throwaway console app in the session scratchpad (deleted; not in
the repo) linked against `Ahjo.Vulkan.Native`, on
**NVIDIA GeForce RTX 4070 Ti, `DRIVER_ID_NVIDIA_PROPRIETARY`, driver
610.47.0.0, instance 1.4.341**, with `VK_LAYER_KHRONOS_validation` 1.4.341
installed and a debug-utils messenger attached. The layer's
`duplicate_message_limit` had to be raised
(`VK_LAYER_DUPLICATE_MESSAGE_LIMIT=1000`) before M6/M7 were observable at all —
the interesting warning is muted after ten repeats.

**M1 — Without the chain, the effective count is zero, and the layer says so.**
Allocating a `VARIABLE_DESCRIPTOR_COUNT | PARTIALLY_BOUND | UPDATE_AFTER_BIND`
layout with no `pNext` returns `VK_SUCCESS` and emits
`WARNING-CoreValidation-AllocateDescriptorSets-VariableDescriptorCount`:

> vkAllocateDescriptorSets(): pAllocateInfo->pSetLayouts[0] binding 0 was
> created with VK_DESCRIPTOR_BINDING_VARIABLE_DESCRIPTOR_COUNT_BIT but no
> VkDescriptorSetVariableDescriptorCountAllocateInfo was provided and the
> effective descriptorCount for the binding is now zero, not the value passed
> in VkDescriptorSetLayoutBinding::descriptorCount

**M2 — A write to element 0 of such a set is rejected twice over.**
`vkUpdateDescriptorSets` on the no-chain set, `dstArrayElement = 0`,
`descriptorCount = 1`:

> `VUID-VkWriteDescriptorSet-dstBinding-00316`: vkUpdateDescriptorSets():
> pDescriptorWrites[0].dstBinding (0) has
> VkDescriptorSetLayoutBinding::descriptorCount of zero in
> VkDescriptorSetLayout …. **(Did you forget to allocate with
> VkDescriptorSetVariableDescriptorCountAllocateInfo?)**

> `VUID-VkWriteDescriptorSet-dstArrayElement-00321`: … dstArrayElement (0) +
> descriptorCount (1) is larger than
> VkDescriptorSetVariableDescriptorCountAllocateInfo::pDescriptorCounts (0) for
> dstBinding (0) …

This is the issue's claim, measured rather than inferred.

**M3 — The allocated count, not the declared count, is what a write is checked
against.** A set allocated with `pDescriptorCounts = 2` from a layout declaring
8 rejects `dstArrayElement = 3`:

> `VUID-VkWriteDescriptorSet-dstArrayElement-00321`: … dstArrayElement (3) +
> descriptorCount (1) is larger than
> VkDescriptorSetVariableDescriptorCountAllocateInfo::pDescriptorCounts (2) …

The count is a per-`VkDescriptorSet` property that survives the wrapper's
`Release`-without-free recycling. **This is the direct proof that E3's
layout-only free-list key is a bug**, and it is the oracle a test can assert
on.

**M4 — Pool accounting charges the *variable* count.** The layer's
pool-capacity warning (`WARNING-VkDescriptorSetAllocateInfo-descriptorCount`)
reports the chained value:

- chain = 17, pool total = 16 → "Trying to allocate **17** … pSetLayouts[0]::pBindings[0].descriptorCount = 17 **(adjusted for VK_DESCRIPTOR_BINDING_VARIABLE_DESCRIPTOR_COUNT_BIT)**"
- chain = 2, pool total = 4, layout declares 1024 → **no warning at all**
- no chain, pool total = 16, layout declares 1024 → "Trying to allocate **1024**"

So the interaction the issue asks about is real and quantified: the pool's
`VkDescriptorPoolSize` budget is consumed at the rate the caller passes to
`Acquire`, and `poolSizes[type].descriptorCount` must cover the sum of the
counts of the live sets of that type — not `maxSets × layoutDeclaredCount`.

**M5 — The chain is ignored when the binding lacks the flag.** Same layer
oracle, on a layout declaring 16 with **no** binding flags, pool total 4:
"Trying to allocate **16**" with *and* without a chain saying 2. Consistent
with 09380's conditional wording (E8).

**M6 — This driver enforces `maxSets` only.** With `maxSets = 2` the third
allocation of a zero-descriptor set fails with `VK_ERROR_OUT_OF_POOL_MEMORY`.
But every per-type over-subscription succeeded: 17 from a 16-descriptor pool;
10 + 10 + 6 = 26 from a 16-descriptor pool; 4 + 4 from a 4-descriptor pool;
16 + 16 from a 4-descriptor pool with a plain (non-update-after-bind) pool and
a plain layout. The layer's own warning names this: *"While this might succeed
on some implementations, it will fail on others."*

**Consequence for testability, and it is a limiting one:** on the CI host's
driver, `vkAllocateDescriptorSets` cannot be made to fail for descriptor-budget
reasons. Any wrapper behaviour that only triggers on a budget failure is
therefore **not testable in this repo's lanes** (wrapper tests are
Windows-only, #32).

**M7 — Without the feature, the failure lands at layout creation, not at
allocation.** On a device created *without*
`descriptorBindingVariableDescriptorCount`, `vkCreateDescriptorSetLayout` for a
`VARIABLE_DESCRIPTOR_COUNT` binding produces
`VUID-VkDescriptorSetLayoutBindingFlagsCreateInfo-descriptorBindingVariableDescriptorCount-03014`
from the layer (the NVIDIA driver itself still returns `VK_SUCCESS`). The pool
is never reached. This decides question 2 below.

**M8 — Chaining a count of 0 is accepted**, and — importantly — it *suppresses*
M1's warning, because the layer's check is "was a chain provided", not "was a
non-zero count provided."

**M9 — The minimum viable fixture needs exactly one optional feature.** A
`STORAGE_BUFFER` binding declaring 4096 with `VARIABLE_DESCRIPTOR_COUNT` and
*nothing else* (no `PARTIALLY_BOUND`, no `UPDATE_AFTER_BIND`), on a plain
descriptor pool with no `UPDATE_AFTER_BIND_BIT`, on a device that enables only
`descriptorBindingVariableDescriptorCount`: layout creation, pool creation,
`Acquire(256)`, `Acquire(512)`, `vkResetDescriptorPool`, `Acquire(256)` all
returned `VK_SUCCESS` with **zero** validation messages. No VUID requires
`PARTIALLY_BOUND` alongside the variable-count flag (E8). This is the benchmark
fixture — it keeps the new benchmark class's host requirement to a single
feature bit.

## Decision

Add a second `Acquire` overload that chains the count, key the free-list on
`(layout, count)`, carry the count on `DescriptorSet`, and guard the one
pool-budget error the wrapper can detect without a driver.

### D1 — `Acquire(layout, uint variableDescriptorCount)`, not a pool-construction parameter

```csharp
public DescriptorSet Acquire(VkDescriptorSetLayout_T* layout);                        // unchanged
public DescriptorSet Acquire(VkDescriptorSetLayout_T* layout, uint variableDescriptorCount);
```

A single `uint` is the correct shape because Vulkan permits the flag on at most
one binding per set and requires it to be the highest binding number
(`VUID-…-pBindingFlags-03004`, E8) — unlike #176's bulk mapping case, where
three independently-sized arrays needed a resolver delegate.

The count belongs on `Acquire` because it is a per-`VkDescriptorSet` property:
M3 shows the driver checks writes against the value passed at *allocation*
time, and a pool serving one long-lived 4096-slot heap and one 256-slot heap
from the same budget is an ordinary thing to want.

**The chain is emitted if and only if `variableDescriptorCount != 0`.**
`Acquire(layout)` therefore produces a byte-identical `VkDescriptorSetAllocateInfo`
to today's, and — per M8 — a caller who forgets the count still gets the
layer's `WARNING-CoreValidation-AllocateDescriptorSets-VariableDescriptorCount`
diagnostic. Chaining a zero unconditionally would silence the one warning that
names the mistake.

### D2 — The free-list key becomes `(layout, variableDescriptorCount)`

```csharp
private readonly record struct IdleKey(nint Layout, uint VariableDescriptorCount);
private readonly Dictionary<IdleKey, Stack<nint>> _idle = new();
```

Forced by M3: a recycled set physically holds the count it was allocated with,
and the driver checks writes against it. Exact-match keying keeps `Acquire`
and `Release` O(1) and allocation-free (a `readonly record struct` implements
`IEquatable<T>`, so `EqualityComparer<T>.Default` devirtualizes and never
boxes; `HandleRegistry.cs:50-51` is the in-repo precedent for a struct key on
an AOT-clean path).

`Release` needs the count to route, so `DescriptorSet` gains
`internal readonly uint VariableDescriptorCount` alongside the existing
`Layout` field, for the reason `Layout` was added (E5). It stays **internal**,
not public: a `FromRaw` set would report 0, indistinguishable from a genuine
zero — the same ambiguity that keeps `Layout` internal
(`DescriptorSet.cs:21-26`). Promoting it is a cheap follow-up if Logos asks.

**Cost of the exact-match rule, stated plainly:** a caller who varies the count
every frame creates one dictionary entry and one `Stack<nint>` per distinct
count, which is the #114 allocation shape. That is a documented misuse, not a
supported pattern — a variable-count set is a long-lived heap table, and the
pool's own remarks already route per-frame descriptors to
`CommandRecorder.PushDescriptors` (`DescriptorSetPool.cs:30-33`). The
benchmark step below pins the supported case (a bounded set of counts) at
0 B/op.

### D3 — Document the feature bit; do not attempt to validate it

Per M7, `descriptorBindingVariableDescriptorCount` is enforced at
`vkCreateDescriptorSetLayout` by `VUID-…-descriptorBindingVariableDescriptorCount-03014`,
which is upstream of the pool: a caller who forgot the feature never gets a
layout worth allocating against. The pool additionally *cannot* check it —
`Acquire` receives a `VkDescriptorSetLayout_T*`, Vulkan exposes no way to read
a layout's binding flags back from the handle, and `Device` keeps no
enabled-feature snapshot (E6).

So: the new overload's XML doc names the feature, names 03014 as the
enforcement point, names `VK_LAYER_KHRONOS_validation` as the oracle, and
states the failure mode if it is missing. No runtime check. This mirrors how
the `updateAfterBind` ctor parameter documents
`VUID-VkDescriptorSetAllocateInfo-pSetLayouts-03044` (`DescriptorSetPool.cs:85-93`).

### D4 — The existing overload stays, delegates, and its trap is documented — because the wrapper cannot detect it

`Acquire(layout)` becomes `Acquire(layout, 0)`. Whether that is a caller error
is **undecidable at the pool**: nothing in the Vulkan API turns a
`VkDescriptorSetLayout` handle back into its binding flags, and the pool is
handed a raw pointer rather than a `DescriptorSetLayout` or the
`DescriptorBinding` span used to build it. All 18 existing call sites (E2)
pass layouts with no variable binding, for which zero is correct.

The mitigation is that the failure is loud *elsewhere*: M1 and M2 show the
validation layer flags both the allocation and the first write, and the write
message literally asks "Did you forget to allocate with
`VkDescriptorSetVariableDescriptorCountAllocateInfo`?". D1's
"chain only when non-zero" rule is what keeps that diagnostic alive. The
one-arg overload's doc gains a sentence pointing at the two-arg one.

### D5 — Reject counts no pool template could ever satisfy

`Acquire` throws `ArgumentOutOfRangeException` when
`variableDescriptorCount` exceeds the largest **per-descriptor-type total** in
the pool's own `poolSizes` template (cached as a single `uint` in the
constructor, so the check is one comparison on the hot path).

> **Corrected during implementation (2026-08-03).** This rule originally read
> "the largest `descriptorCount` in the pool's own `poolSizes` template" — a
> max over *entries*. That is wrong, and the guard as first written rejected
> requests a legal template can serve. `vkCreateDescriptorPool` **sums**
> duplicate same-type entries: "if multiple `VkDescriptorPoolSize` structures
> containing the same descriptor type appear in the `pPoolSizes` array then the
> pool will be created with enough storage for the total number of descriptors
> of each type." Evidence that duplicates are legal in the first place: in
> validusage 1.4.341 the only VUID constraining repeated `pPoolSizes` entries is
> `VUID-VkDescriptorPoolCreateInfo-pPoolSizes-04787`, which covers
> `VK_DESCRIPTOR_TYPE_MUTABLE_EXT` only. So a template of
> `[{STORAGE_BUFFER, 64}, {STORAGE_BUFFER, 64}]` creates a pool holding 128, and
> the max-over-entries rule would have thrown on `Acquire(layout, 100)`. The
> *decision* below — a setup-time pre-flight necessary-condition guard — is
> unchanged; only the arithmetic computing "what the template can serve" was
> wrong. `Acquire_CountAboveASingleEntryButWithinThePerTypeTotal_Succeeds`
> pins the corrected rule and is the case that distinguishes the two.

This is a *necessary*-condition guard, and it is sound without knowing the
binding's descriptor type: if no type in the template totals that large, then
whichever type the variable binding has, a sub-pool built from that template
cannot hold the request — and per E4, auto-grow will keep building sub-pools
that also cannot. It must stay a necessary condition and never become a
sufficient one: the pool receives a raw `VkDescriptorSetLayout_T*`, and Vulkan
exposes no way to read the variable binding's descriptor type back from it, so
a per-type check is not merely undesirable here but unimplementable. It is the only part of the pool-capacity interaction the
wrapper can check locally, deterministically, and before any Vulkan call, which
matters because M6 says the driver-side failure is **not reproducible on the CI
host at all**.

It does mean the wrapper now rejects something this NVIDIA driver would accept.
That is deliberate: the layer's own text for the same situation is "it will
fail on others", and the wrapper already fails early on the analogous
portability trap (`CreatePipelineLayout` throws on a push range exceeding
`maxPushConstantsSize` — `PipelineLayoutTests.cs:202-234`).

Guard order in `Acquire(layout, count)`: `ObjectDisposedException` →
`ArgumentNullException(layout)` → `ArgumentOutOfRangeException(count)`, so a
disposed pool reports disposal rather than a budget complaint.

### D6 — Leave the auto-grow retry alone, and say why

E4's doomed-retry (one chained sub-pool leaked per unsatisfiable call) is
narrowed by D5 to the residual case "the variable binding's own type entry is
too small, but some other entry in the template is larger". Rolling the fresh
sub-pool back on a failed retry would be a five-line change — and per M6 the
failing-retry path **cannot be exercised on this driver at all** (`maxSets ≥ 1`
means a brand-new sub-pool always satisfies one set, and per-type
over-subscription never fails). Shipping an untestable branch on a hot path is
worse than documenting the residue. Recorded here as a follow-up candidate, not
as work.

### Why not the alternatives

- **Count on pool construction (`new DescriptorSetPool(…, variableDescriptorCount: 4096)`).**
  Rejected: M3 makes the count a per-set property the driver enforces per-set,
  `FrameRing` builds its own pools (`FrameRing.cs:262`) so a caller could not
  set it there, one budget could no longer serve two heap capacities, and the
  pool would have to chain the struct on allocations for layouts that have no
  variable binding — harmless (M5) but unjustifiable.
- **Chain the struct on every allocation with count 0 as the default.**
  Rejected on M8: it suppresses
  `WARNING-CoreValidation-AllocateDescriptorSets-VariableDescriptorCount`, the
  single diagnostic that names the mistake D4 admits the wrapper cannot detect.
- **An options struct (`Acquire(layout, in DescriptorSetAllocation opts)`).**
  Rejected: `VkDescriptorSetAllocateInfo`'s only chainable extension is this
  one struct (`VUID-VkDescriptorSetAllocateInfo-pNext-pNext`, E8), so the
  options type would have exactly one field for the foreseeable future.
- **Keep the layout-only free-list key and forbid mixed counts per layout
  (throw when a layout is acquired with two different counts).** Rejected: it
  is strictly less capable (a heap and a smaller shadow heap over one layout is
  legitimate), needs a per-layout "first count seen" side table anyway, and
  the composite key costs the same.
- **Store the count in a side dictionary keyed by set handle instead of on the
  struct.** Rejected: an extra hash lookup on every `Release` to avoid 8 bytes
  on a struct that is never bulk-reinterpreted (E5), and #118 established that
  handle metadata rides the handle rather than a side table.
- **Record layout→binding-flags in a device-side registry at
  `CreateDescriptorSetLayout` time so `Acquire(layout)` can throw on a variable
  layout.** Tempting — it would make D4's trap detectable. Rejected: it covers
  only layouts built through the wrapper (raw and `FromRaw` handles are
  invisible), it introduces lifetime coupling (entries must die with the
  layout, and driver handle-value reuse then aliases stale entries — the exact
  hazard `HandleRegistry.cs:14-18` documents), and it makes `DescriptorSetPool`
  depend on `Device` state it does not own. The validation layer already
  reports this case with a better message (M2).
- **`AhjoValidation`-gated bounds check in `DescriptorSet.Update`
  (`arrayElement + count ≤ VariableDescriptorCount`).** Rejected: the wrapper
  does not know *which* binding is the variable one, so the check would either
  wrongly constrain the set's fixed bindings or be unimplementable. The layer's
  00321 covers it exactly (M3).
- **Change `Acquire` to take the wrapper's `DescriptorSetLayout` instead of the
  raw pointer.** Out of scope — a separate API decision affecting all 18 call
  sites and unrelated to the variable-count gap.
- **Add a `VUID-…-pBindingFlags-03004` guard (variable binding must be the
  highest binding number) to `Device.CreateDescriptorSetLayout`.** Genuinely
  missing (`Lifecycle/Device.cs:199-215` writes `pBindingFlags` with no
  ordering check), but it is a second, independent decision about the layout
  builder. One decision per spec — file it separately.

## Uncertainty recorded

- **The layer's two messages for the no-chain case disagree with each other.**
  M1 says "the effective descriptorCount for the binding is now zero"; the
  pool-capacity warning fired on the same call charges the layout's declared
  1024 (M4, third bullet). We design against the M1/M2/M3 reading — the
  effective count is zero — because that is the one the write-time checks
  enforce. Whether a strict driver charges 0 or 1024 to the pool for a
  no-chain allocation is **unknown from this box**, and D5's guard does not
  depend on the answer.
- **Every per-type accounting claim in M4 rests on the validation layer's
  arithmetic, not on a driver refusal**, because this driver never refuses
  (M6). A second driver (AMD or Intel) would strengthen it. This is stated
  rather than papered over.

## Consequences

- `src/Ahjo.Vulkan.Slang/README.md:391-400`'s blockquote is deleted and
  replaced with the pool recipe. Permitted by `src/Ahjo.Vulkan.Slang/CLAUDE.md`
  — that file forbids *editing* `src/Ahjo.Vulkan/`, and the README already
  documents `new DescriptorSetPool(device, maxSets, sizes, updateAfterBind:
  true)` at `README.md:383`.
- `docs/benchmarks.md` gains two rows; the existing
  `DescriptorSetPool.AcquireReleaseReset_Cycle` row (`docs/benchmarks.md:89`)
  is re-measured because `DescriptorSet` grew and `_idle`'s key changed.
- No `Generated/` file changes (E1). No new warnings to suppress. No
  reflection, no dynamic codegen — the new types are a `record struct` key and
  a stack-local `VkDescriptorSetVariableDescriptorCountAllocateInfo`.
