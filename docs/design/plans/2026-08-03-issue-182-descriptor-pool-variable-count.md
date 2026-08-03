Paired with [../specs/2026-08-03-issue-182-descriptor-pool-variable-count-design.md](../specs/2026-08-03-issue-182-descriptor-pool-variable-count-design.md).

# Plan — issue #182: variable-descriptor-count allocation through `DescriptorSetPool`

Branch: `issue-182-variable-descriptor-count`. No `Generated/` file changes;
no `/regen-bindings` run
(`VkDescriptorSetVariableDescriptorCountAllocateInfo` is already generated with
its `IChainable<VkDescriptorSetAllocateInfo>` partial — spec E1).

---

## Step 1 — `DescriptorSet` carries the allocated variable count

**File:** `src/Ahjo.Vulkan/Pipelines/DescriptorSet.cs`

Add one field immediately after `Layout` (currently line 27) and extend the
internal constructor:

```csharp
    // The variable-descriptor count this set was allocated with — the value
    // that went into VkDescriptorSetVariableDescriptorCountAllocateInfo::
    // pDescriptorCounts, or 0 when no chain was emitted. Carried for the same
    // reason as Layout: DescriptorSetPool.Release has to route the set back to
    // the free-list bucket it came from, and a set physically holds the count
    // it was allocated with (the driver checks every write against it —
    // VUID-VkWriteDescriptorSet-dstArrayElement-00321). 0 for FromRaw-
    // constructed instances, which is indistinguishable from a genuine zero;
    // that ambiguity is why this stays internal.
    internal readonly uint VariableDescriptorCount;

    internal DescriptorSet(
        VkDescriptorSet_T*       handle,
        VkDescriptorSetLayout_T* layout                  = null,
        uint                     variableDescriptorCount = 0)
    {
        Handle                  = handle;
        Layout                  = layout;
        VariableDescriptorCount = variableDescriptorCount;
    }
```

`FromRaw` (line 36) is unchanged — it already uses the single-argument form.

Update the type's `<remarks>` (lines 12-17) to mention that the struct is two
pointers plus the allocated variable count.

---

## Step 2 — `DescriptorSetPool`: composite free-list key, budget guard, second `Acquire`

**File:** `src/Ahjo.Vulkan/Pools/DescriptorSetPool.cs`

### 2a. Nested key type

Add at the bottom of the class, next to `IsExhaustion`:

```csharp
    /// <summary>
    /// Free-list bucket identity: a recycled set physically holds the
    /// variable-descriptor count it was allocated with, so a set acquired
    /// with count 4 must never be handed to a caller asking for count 64.
    /// A <c>readonly record struct</c> implements <see cref="IEquatable{T}"/>,
    /// so <c>EqualityComparer&lt;IdleKey&gt;.Default</c> devirtualizes and
    /// never boxes — the dictionary lookup stays allocation-free.
    /// </summary>
    private readonly record struct IdleKey(nint Layout, uint VariableDescriptorCount);
```

### 2b. Field changes

- `_idle` (line 67) becomes `private readonly Dictionary<IdleKey, Stack<nint>> _idle = new();`
  Keep the existing `:64-66` comment and extend it: different layouts *and*
  different variable counts are different binding shapes.
- Add `private readonly uint _maxPerTypeDescriptorTotal;`, computed in the
  constructor as the maximum **per-descriptor-type total** over `poolSizes`
  (sum every entry sharing a `type`, then take the largest of those sums),
  before `_poolSizes = poolSizes.ToArray();` is stored (loop the incoming
  span; an `O(n²)` double loop over a handful of setup-time entries is fine
  and keeps the computation allocation-free). Accumulate in `ulong` and clamp
  to `uint.MaxValue` so an absurd template cannot wrap into a small bound.

  > **Corrected during implementation (2026-08-03).** This step originally said
  > `_maxPoolSizeDescriptorCount`, "computed as the maximum `descriptorCount`
  > over `poolSizes`" — a max over *entries*. That is wrong:
  > `vkCreateDescriptorPool` sums duplicate same-type entries, and only
  > `VK_DESCRIPTOR_TYPE_MUTABLE_EXT` restricts repeats
  > (`VUID-VkDescriptorPoolCreateInfo-pPoolSizes-04787` is the sole VUID on
  > repeated `pPoolSizes` entries in validusage 1.4.341), so duplicates of
  > ordinary types are legal and additive. The old rule made `Acquire` throw on
  > requests a legal template can serve. See the matching note on spec D5.

### 2c. `Acquire` overloads

Replace the single `Acquire` (line 127) with:

```csharp
    public DescriptorSet Acquire(VkDescriptorSetLayout_T* layout)
        => Acquire(layout, variableDescriptorCount: 0);

    public DescriptorSet Acquire(VkDescriptorSetLayout_T* layout, uint variableDescriptorCount)
```

The two-argument body, in this exact guard order:

1. `ObjectDisposedException.ThrowIf(_disposed, this);`
2. `if (layout == null) throw new ArgumentNullException(nameof(layout));`
3. budget guard:

```csharp
        if (variableDescriptorCount > _maxPerTypeDescriptorTotal)
            throw new ArgumentOutOfRangeException(
                nameof(variableDescriptorCount), variableDescriptorCount,
                $"This pool's poolSizes template holds at most " +
                $"{_maxPerTypeDescriptorTotal} descriptors of any single descriptor type, so no " +
                $"sub-pool built from it can satisfy a variable-descriptor count of " +
                $"{variableDescriptorCount}. Size poolSizes for the sum, over the live sets, of " +
                $"all descriptors of that type.");
```

The comment on this guard must say it is a **necessary** condition and why it
can never be tightened into a per-type check: the pool holds a raw
`VkDescriptorSetLayout_T*` and Vulkan exposes no way to read the variable
binding's descriptor type back from it.

4. `var key = new IdleKey((nint)layout, variableDescriptorCount);` then the
   existing idle-pop, returning
   `new DescriptorSet((VkDescriptorSet_T*)stack.Pop(), layout, variableDescriptorCount)`.
5. The existing `_allHandles.EnsureCapacity` / allocate / grow-retry flow,
   threading `variableDescriptorCount` into both
   `AllocateFromCurrentPool` calls and into both `new DescriptorSet(...)`
   constructions.

Guard order matters: a disposed pool must report `ObjectDisposedException`,
not a budget complaint (`DescriptorSetPoolTests.cs:246` asserts the former).

### 2d. `AllocateFromCurrentPool`

New signature:

```csharp
    private VkDescriptorSet_T* AllocateFromCurrentPool(
        VkDescriptorSetLayout_T* layout,
        uint                     variableDescriptorCount,
        out VkResult             result)
```

Body: build the existing `VkDescriptorSetAllocateInfo`, then — **only when
`variableDescriptorCount != 0`** — chain a stack-local:

```csharp
        uint count = variableDescriptorCount;
        var vc = new VkDescriptorSetVariableDescriptorCountAllocateInfo
        {
            sType              = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_SET_VARIABLE_DESCRIPTOR_COUNT_ALLOCATE_INFO,
            descriptorSetCount = 1,     // must equal ai.descriptorSetCount — VUID-…-descriptorSetCount-03045
            pDescriptorCounts  = &count,
        };
        if (variableDescriptorCount != 0) ai.pNext = &vc;
```

Both locals live in the same stack frame as the `vkAllocateDescriptorSets`
call, so no pinning is needed and nothing reaches the managed heap.

The `!= 0` condition is load-bearing, not an optimization: chaining a zero
suppresses
`WARNING-CoreValidation-AllocateDescriptorSets-VariableDescriptorCount`, which
is the only diagnostic a caller who forgot the count ever gets (spec M1, M8).
Put that sentence in a code comment.

### 2e. `Release`

Line 200's key computation becomes:

```csharp
        var key = new IdleKey(
            set.Layout != null ? (nint)set.Layout : (nint)layout,
            set.VariableDescriptorCount);
```

The `AhjoValidation` block at `:181-193` is unchanged.

### 2f. `Reset` — no code change, comment change

`Reset` (line 217) already enumerates `_idle` and clears each `Stack` without
touching dictionary structure; that stays correct with the composite key. Amend
the comment at `:224-229` so it says "the layout-pointer + count keys stay
valid across resets".

### 2g. XML documentation on this file

- **Class `<remarks>`, auto-grow paragraph (`:43-54`)** — append: a variable
  count makes the requested shape a function of the `Acquire` argument rather
  than of the layout alone, so the sub-pool template must be sized for the sum
  of the variable counts of the live sets of that type; the pre-flight guard in
  `Acquire` rejects only the counts *no* template entry could hold.
- **Constructor `<param name="poolSizes">`** — add one; today the parameter has
  no `<param>` tag. State the sizing rule above.
- **Two-argument `Acquire` `<summary>` + `<remarks>`** must state, in this
  order:
  - chains `VkDescriptorSetVariableDescriptorCountAllocateInfo` with
    `descriptorSetCount = 1`;
  - the count applies to the one binding carrying
    `DescriptorBindingFlags.VariableDescriptorCount`, which Vulkan requires to
    be the highest binding number in the set
    (`VUID-VkDescriptorSetLayoutBindingFlagsCreateInfo-pBindingFlags-03004`);
  - it must not exceed that binding's declared `descriptorCount`
    (`VUID-VkDescriptorSetAllocateInfo-pSetLayouts-09380`);
  - the device must have enabled `descriptorBindingVariableDescriptorCount` —
    **the pool cannot check this**; the layout would already have been rejected
    at `vkCreateDescriptorSetLayout` by
    `VUID-VkDescriptorSetLayoutBindingFlagsCreateInfo-descriptorBindingVariableDescriptorCount-03014`,
    and `VK_LAYER_KHRONOS_validation` is the oracle (drivers may accept it
    silently);
  - passing 0 emits no chain and is exactly the one-argument overload;
  - sets acquired with different counts occupy different free-list buckets, so
    a caller who varies the count without bound grows the pool's bookkeeping —
    a variable-count set is a long-lived heap table, not per-frame scratch.
- **One-argument `Acquire`** — add: for a layout whose highest binding carries
  `VariableDescriptorCount`, this allocates that binding with an effective
  count of **zero**; every write then fails
  (`VUID-VkWriteDescriptorSet-dstBinding-00316`,
  `VUID-VkWriteDescriptorSet-dstArrayElement-00321`). The wrapper cannot detect
  this — Vulkan exposes no way to read a layout's binding flags back from the
  handle. Use the two-argument overload.

Copy every VUID identifier from the spec's E8 list verbatim. Do not invent
one, and do not paraphrase a VUID number from memory.

---

## Step 3 — Doc-comment fixes made stale by Step 1

**File:** `src/Ahjo.Vulkan/Pools/DescriptorSetExtensions.cs` — the class
`<summary>` at lines 8-11 says the struct is "one pointer + one layout
pointer". Change to "two pointers plus the allocated variable-descriptor
count"; the rest of the sentence (why `Update` lives on an extension) stands.

**File:** `src/Ahjo.Vulkan/Pipelines/DescriptorBindingFlags.cs` — on
`VariableDescriptorCount` (line 16), add a `<summary>` naming
`DescriptorSetPool.Acquire(layout, variableDescriptorCount)` as the allocation
path and `descriptorBindingVariableDescriptorCount` as the required feature.

**File:** `src/Ahjo.Vulkan/Pipelines/DescriptorBinding.cs` — the `<remarks>` at
lines 11-13 tells callers to use the flag "with a large `Count` for bindless
arrays". Add: `Count` is then the *maximum*; the per-set count is chosen at
`DescriptorSetPool.Acquire`.

---

## Step 4 — Probe property for the new gate

**File:** `tests/Ahjo.Vulkan.Tests/VulkanDriverProbe.cs`

Add a third property on the existing `_features12` cache, immediately after
`SupportsBindlessSampledImage` (line 112). Do **not** add a second probe or a
second instance:

```csharp
    /// <summary>
    /// <see langword="true"/> when the first enumerated GPU advertises the bits
    /// needed to allocate a variable-descriptor-count storage-buffer heap:
    /// <c>descriptorBindingPartiallyBound</c>,
    /// <c>descriptorBindingVariableDescriptorCount</c> and
    /// <c>descriptorBindingStorageBufferUpdateAfterBind</c>. Issue #182's
    /// allocation tests gate on this.
    /// </summary>
    public static bool SupportsBindlessVariableCountStorageBuffer
    {
        get
        {
            var f12 = _features12.Value;
            return f12.descriptorBindingPartiallyBound != 0
                && f12.descriptorBindingVariableDescriptorCount != 0
                && f12.descriptorBindingStorageBufferUpdateAfterBind != 0;
        }
    }
```

---

## Step 5 — Tests

**New file:** `tests/Ahjo.Vulkan.Tests/DescriptorSetPoolVariableCountTests.cs`,
`public sealed unsafe class DescriptorSetPoolVariableCountTests`.

### Shared fixture (private helpers in the new file)

- `CreateVariableCountDevice(Instance)` — copy the shape of
  `PipelineLayoutTests.cs:266-296` (`CreateBindlessGraphicsDevice`), but set
  `f12.descriptorBindingPartiallyBound = 1;`
  `f12.descriptorBindingVariableDescriptorCount = 1;`
  `f12.descriptorBindingStorageBufferUpdateAfterBind = 1;`.
- `CreateVariableCountLayout(Device, uint declaredCount)` — returns a
  `DescriptorSetLayout` from `Device.CreateDescriptorSetLayout` with
  `UpdateAfterBindPool = true` and one binding:
  `Slot = 0`, `Type = VK_DESCRIPTOR_TYPE_STORAGE_BUFFER`, `Count = declaredCount`,
  `Stages = ShaderStages.Compute`,
  `BindingFlags = PartiallyBound | UpdateAfterBind | VariableDescriptorCount`.
  Binding 0 is the only binding, so it is trivially the highest
  (`VUID-…-pBindingFlags-03004`), and `STORAGE_BUFFER` is not one of the two
  types `VUID-…-pBindingFlags-03015` forbids.
- `CreatePool(Device, uint budget, uint maxSets)` — `new DescriptorSetPool(device,
  maxSets, [ { type = STORAGE_BUFFER, descriptorCount = budget } ],
  updateAfterBind: true)` (`VUID-VkDescriptorSetAllocateInfo-pSetLayouts-03044`).

Every test starts with `TestGate.RequireDriver();` followed by
`TestGate.RequireDeviceFeature(VulkanDriverProbe.SupportsBindlessVariableCountStorageBuffer,
"Device does not advertise descriptorBindingPartiallyBound + descriptorBindingVariableDescriptorCount + descriptorBindingStorageBufferUpdateAfterBind; this variable-descriptor-count test requires all three.")`
except where noted. Never a bare `Assert.Skip` (`tests/CLAUDE.md`).

### T1 — `Acquire_WithVariableCount_WriteInsideTheCount_PassesValidation`

Gates: `RequireDriver`, `RequireValidationLayer`, `RequireDeviceFeature`.
Follow `CommandRecorderTests.cs:741-760` for the capture: create the instance
with `EnableValidation = true` and a `DebugCallback` that appends
`ERROR_BIT_EXT` messages to a `List<DebugMessage>` under a lock.

Layout declares 32; pool budget 256, `maxSets: 4`. `pool.Acquire(layout.Handle, 8)`.
Create one storage buffer via `device.Allocator.CreateBuffer` exactly as
`DescriptorWriteTests.cs:194-196`. `set.Update(device, [DescriptorWrite.Buffer(
binding: 0, arrayElement: 7, VK_DESCRIPTOR_TYPE_STORAGE_BUFFER, in info)])`.
Assert the captured error list is empty, with the messages joined into the
failure text.

- **Mutation that turns it red:** in Step 2d, delete the
  `if (variableDescriptorCount != 0) ai.pNext = &vc;` line.
- **Colour under that mutation: RED** — the effective count becomes 0 and the
  write raises `VUID-VkWriteDescriptorSet-dstBinding-00316` *and*
  `VUID-VkWriteDescriptorSet-dstArrayElement-00321` (spec M1/M2, measured on
  the NVIDIA host with the layer attached).

### T2 — `Acquire_WithoutVariableCount_OnVariableLayout_WriteFailsValidation`

This is T1's negative control and the anti-#179 guard: it proves the fixture
can distinguish a working allocation from a broken one, so T1's green carries
information.

Same gates and capture. Same layout and pool. `pool.Acquire(layout.Handle)`
(one argument). Same write, but at `arrayElement: 0`. Assert
`Assert.Contains(captured, m => m.MessageIdName == "VUID-VkWriteDescriptorSet-dstBinding-00316")`.

- **Mutation that turns it red:** make the one-argument overload delegate with a
  non-zero count, or drop the `!= 0` condition so a zero count still chains.
- **Colour under either mutation: RED** — the expected message disappears
  (chaining a zero suppresses it; a non-zero count makes the write legal).
  Measured: spec M8.

### T3 — `Acquire_SameLayout_DifferentCounts_DoNotShareTheFreeList`

Gates: `RequireDriver`, `RequireDeviceFeature`. No validation layer needed.

Layout declares 32; pool budget 256, `maxSets: 8`.

```
a = Acquire(layout, 4);   Release(layout, a);
b = Acquire(layout, 8);   Assert.True(a.Handle != b.Handle);
                          Assert.Equal(2, pool.AllocatedCount);
Release(layout, b);
c = Acquire(layout, 4);   Assert.True(a.Handle == c.Handle);
                          Assert.Equal(2, pool.AllocatedCount);
```

- **Mutation that turns it red:** revert `_idle` to `Dictionary<nint, Stack<nint>>`
  keyed on the layout pointer alone.
- **Colour under that mutation: RED** — `b` pops `a`'s handle, so
  `a.Handle != b.Handle` fails and `AllocatedCount` is 1, not 2.

### T4 — `Acquire_CountExceedingLargestPerTypeTotal_Throws`

Gates: `RequireDriver`, `RequireDeviceFeature`.

Pool budget 64, `maxSets: 4`; layout declares 128. `Acquire(layout.Handle, 65)`
must throw `ArgumentOutOfRangeException`; assert the message contains `"64"`.
Then assert `Acquire(layout.Handle, 64)` succeeds — the boundary is inclusive.

- **Mutation that turns it red:** delete the guard from Step 2c.
- **Colour under that mutation: RED** — no exception is thrown. Measured: this
  host's driver returns `VK_SUCCESS` for every per-type over-subscription
  tried (spec M6), so the guard is the *only* thing that fails here and the
  test is genuinely sensitive to its removal.

### T4b — `Acquire_CountAboveASingleEntryButWithinThePerTypeTotal_Succeeds`

*(Added during implementation alongside the D5 correction — T4 alone cannot
tell the per-type-total rule from the max-over-entries rule.)*

Gates: `RequireDriver`, `RequireDeviceFeature`.

Layout declares 256. Pool built from **two** `STORAGE_BUFFER` entries of 64
each (total 128), `maxSets: 4`, `updateAfterBind: true`.
`Acquire(layout.Handle, 100)` must **succeed** — above either individual entry,
within their sum. Then `Acquire(layout.Handle, 129)` must throw, with `"128"`
in the message.

- **Mutation that turns it red:** compute the guard bound as the maximum
  `descriptorCount` over entries instead of the maximum per-type total.
- **Colour under that mutation: RED** — the count-100 `Acquire` throws
  `ArgumentOutOfRangeException` ("holds at most 64 descriptors of any single
  descriptor type"). T4 stays green under the same mutation, since its
  single-entry template makes both rules agree — which is exactly why this
  case has to exist.

### T5 — `Acquire_ZeroCount_SharesTheFreeListWithTheOneArgumentOverload`

Gates: `RequireDriver` only (a plain uniform-buffer layout is fine — no
optional feature). Reuse `DescriptorSetPoolTests`' plain
`CreateUniformBufferLayout` shape (`DescriptorSetPoolTests.cs:257-275`) rather
than the variable-count fixture.

```
x = Acquire(layout, 0);  Release(layout, x);
y = Acquire(layout);     Assert.True(x.Handle == y.Handle);
                         Assert.Equal(1, pool.AllocatedCount);
```

- **Mutation that turns it red:** make the one-argument overload use a distinct
  sentinel key (e.g. `uint.MaxValue`) instead of 0.
- **Colour under that mutation: RED** — `y` allocates a second set.

### T6 — `Acquire_VariableCount_AfterReset_ReallocatesFresh`

Gates: `RequireDriver`, `RequireDeviceFeature`.

`Acquire(layout, 8)`; `Release(layout, set)`; `Reset()`;
`Assert.Equal(0, pool.AllocatedCount)`; `Acquire(layout, 8)`;
`Assert.False(fresh.IsNull)`; `Assert.Equal(1, pool.AllocatedCount)`.

- **Mutation that turns it red:** make `Reset` skip the
  `entry.Value.Clear()` loop over `_idle`.
- **Colour under that mutation: RED** — the second `Acquire` pops a
  reset-invalidated handle from the retained stack and `AllocatedCount` stays
  0, so the final assertion fails. This is the #114 interaction re-pinned under
  the composite key.

### T7 — `Acquire_VariableCount_Guards`

Gates: `RequireDriver` only.

- `Assert.Throws<ArgumentNullException>(() => pool.Acquire(null, 4))`.
- Dispose the pool, then
  `Assert.Throws<ObjectDisposedException>(() => pool.Acquire(layout, 999_999))`
  — a count that would also trip the budget guard, pinning the guard *order*.

- **Mutation that turns it red:** move the budget guard above the
  `ObjectDisposedException.ThrowIf`.
- **Colour under that mutation: RED** — the second assertion sees
  `ArgumentOutOfRangeException`.

### Regression check on the existing suite

`DescriptorSetPoolTests`, `DescriptorWriteTests`, `DescriptorTemplateTests`,
`FrameRingTests`, `PipelineLayoutTests` all call the one-argument overload
(spec E2, 16 call sites) and must stay green untouched. If any needs editing,
**stop** — Step 2c changed observable behaviour it should not have.

### How to run and what to quote

```
AHJO_VULKAN_TIER=validation dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~DescriptorSetPool"
```

Per `tests/CLAUDE.md`, quote `VulkanTierContractTests`' `declared=… observed=…`
line in the PR: T1/T2's only oracle is the validation layer, so a run without a
tier declaration is indistinguishable from two skips.

---

## Step 6 — Benchmarks (required; `Pools/` is a hot path)

**New file:** `tests/Ahjo.Vulkan.Benchmarks/DescriptorSetPoolVariableCountBenchmarks.cs`

A **separate class** from `DescriptorSetPoolBenchmarks`, deliberately: its
`[GlobalSetup]` requires an optional device feature, and a host without it must
not take the existing #114 canary down with it.

Model the class on `DescriptorSetPoolBenchmarks.cs:19-108` — `[MemoryDiagnoser]`,
`private const int CallsPerInvoke = 1000`, `[Benchmark(OperationsPerInvoke = CallsPerInvoke)]`.

`[GlobalSetup]`:

- Device via `gpu.CreateDevice(... ConfigureFeatures = static (…, ref VkPhysicalDeviceVulkan12Features f12, …) => f12.descriptorBindingVariableDescriptorCount = 1;)` —
  **only that bit**. Spec M9 measured this exact minimal shape working end to
  end with zero validation messages.
- Layout: one binding, `Slot = 0`,
  `Type = VK_DESCRIPTOR_TYPE_STORAGE_BUFFER`, `Count = 4096`,
  `Stages = ShaderStages.Compute`,
  `BindingFlags = DescriptorBindingFlags.VariableDescriptorCount`.
  `UpdateAfterBindPool = false`.
- Pool: `new DescriptorSetPool(device, maxSets: 64, [{ STORAGE_BUFFER, 4096 }])`
  — default `updateAfterBind: false`, matching the layout.
- Warm-up loop, mirroring `DescriptorSetPoolBenchmarks.cs:78-87`: run two full
  `Acquire → Release → Reset` cycles for **each** count the benchmarks use
  (256 and 512), so both dictionary entries and both `Stack<nint>` backing
  arrays exist before measurement.

Two benchmarks:

1. `AcquireReleaseReset_VariableCount_Cycle` — `Acquire(_layoutHandle, 256)` →
   `Release` → `Reset`, `CallsPerInvoke` times. Pins the single-count steady
   state at 0 B/op: proves the chain is stack-only and the `IdleKey` lookup
   does not box.
2. `AcquireReleaseReset_TwoCounts_Cycle` — per iteration, `Acquire(_layoutHandle, 256)`
   / `Release`, then `Acquire(_layoutHandle, 512)` / `Release`, then one
   `Reset`. Pins the *bounded set of counts* case at 0 B/op — this is the one
   the composite key could regress in exactly the #114 shape (a fresh
   `Stack<nint>` per bucket per frame).

`[GlobalCleanup]` disposes pool, layout, device, instance in that order, as the
existing class does.

Run:

```
dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter "*DescriptorSetPool*"
```

**Acceptance:** the `Allocated` column reads `-` for both new rows *and* for the
existing `AcquireReleaseReset_Cycle`. A non-`-` value on benchmark 2 means the
`IdleKey` buckets are being rebuilt per cycle — do not paper over it by
narrowing the benchmark to one count; report back.

Also re-measure the existing `DescriptorSetPool.AcquireReleaseReset_Cycle` row:
`DescriptorSet` grew by one `uint` and `_idle`'s key changed, so its Mean may
move. Update `docs/benchmarks.md:89` only if it moved by more than the doc's own
20% noise threshold; the `Allocated` cell must stay `-` either way.

---

## Step 7 — Docs

### 7a. `docs/benchmarks.md`

- Two rows after line 89, in the same table, with measured Means and `-` in
  **Allocated**. The row label is `<ClassName minus "Benchmarks">.<Method>`,
  which every existing row follows and which is usable verbatim as a
  `--filter` argument — so these are **`DescriptorSetPoolVariableCount.…`**,
  not `DescriptorSetPool.…`; the methods live in
  `DescriptorSetPoolVariableCountBenchmarks`. Match the table's right-aligned
  Mean/Allocated padding (12- and 11-char cells).

  | `DescriptorSetPoolVariableCount.AcquireReleaseReset_VariableCount_Cycle` | … | - | #182 canary: `Acquire(layout, count)` chains `VkDescriptorSetVariableDescriptorCountAllocateInfo` from the stack; the `(layout, count)` free-list key must not box. Mean not comparable to the row above — different layout, pool template and per-set descriptor count. |
  | `DescriptorSetPoolVariableCount.AcquireReleaseReset_TwoCounts_Cycle` | … | - | #182 canary: two distinct counts per cycle — the bounded-count case must reuse both retained `Stack`s, not rebuild them (the #114 shape, one key deeper). Mean not comparable to `DescriptorSetPool.AcquireReleaseReset_Cycle` — different layout, pool template and per-set descriptor count. |

  > **Corrected during implementation (2026-08-03).** These two rows were
  > originally specified as `DescriptorSetPool.…`, which names the wrong
  > class: `DescriptorSetPoolBenchmarks` has one method, and the label would
  > not work as a `--filter`. The `<ClassName minus "Benchmarks">.<Method>`
  > convention is followed by 9/9 pre-existing rows.

- The existing `DescriptorSetPool.AcquireReleaseReset_Cycle` row's Notes cell
  says "retains the per-layout idle `Stack`s" and goes stale with this change —
  the free-list is now per-bucket. Update it to "per-bucket `(layout, count)`
  idle `Stack`s", and record the re-measurement even when the Mean does not
  move (the #155 caveat sets that precedent: silence leaves the next reader
  unable to tell a checked row from an untouched one).

- The file→benchmark mapping table in
  `.claude/agents/bench-coverage-checker.md` needs `Pools/DescriptorSetPool.cs`
  to list **both** benchmark classes (with the reason the variable-count one is
  split out — its `[GlobalSetup]` needs an optional device feature), and a new
  `Pipelines/DescriptorSet.cs` row, which is missing entirely despite being a
  hot-path type.

- Extend the **Driver dependency** caveat: the new class additionally needs a
  device advertising `descriptorBindingVariableDescriptorCount` and fails at
  `[GlobalSetup]` without it; the rest of `DescriptorSetPool.*` does not.

### 7b. `src/Ahjo.Vulkan.Slang/README.md`

**Delete lines 391-400 outright** — the whole blockquote beginning "**This one
needs more than the flag, and `DescriptorSetPool` cannot give it to you yet.**".
Replace with prose in the same voice as the surrounding numbered items (which
end at line 389 and resume at line 402):

- Keep the feature requirement and its VUID:
  `descriptorBindingVariableDescriptorCount` must be enabled, or
  `vkCreateDescriptorSetLayout` rejects the layout with
  `VUID-VkDescriptorSetLayoutBindingFlagsCreateInfo-descriptorBindingVariableDescriptorCount-03014`
  — i.e. the flag fails at *layout* creation, before any pool is involved.
- Replace the workaround with the recipe, matching the file's existing snippet
  style:

  ```csharp
  DescriptorSet set = pool.Acquire(layout.Handle, variableDescriptorCount: 1024);
  ```

  and the sentence that the count states how many descriptors *this set* holds
  in that binding, must not exceed the layout's declared `Count`
  (`VUID-VkDescriptorSetAllocateInfo-pSetLayouts-09380`), and is what the
  driver checks every write against.
- Keep one warning sentence, because the wrapper cannot detect it: allocating
  such a layout through `pool.Acquire(layout.Handle)` — the one-argument
  overload — gives that binding an effective count of **zero** and every write
  fails.
- Do **not** mention pool sizing here; that belongs to the wrapper's own XML
  doc (Step 2g).

`src/Ahjo.Vulkan.Slang/CLAUDE.md`'s boundary rule forbids *editing*
`src/Ahjo.Vulkan/`, not documenting its API from this README — line 383 already
names the `DescriptorSetPool` constructor. Nothing else in that file constrains
this edit.

### 7c. `docs/migration-vortice-to-ahjo.md`

In the "Bindless: `UpdateAfterBind | PartiallyBound`" section (lines 181-206),
after the layout snippet, add a short paragraph: a set whose highest binding
carries `VariableDescriptorCount` is allocated with
`pool.Acquire(layout.Handle, variableDescriptorCount: n)`, and the pool's
`poolSizes` budget is consumed at `n` per set rather than at the layout's
declared maximum. One or two sentences — this file is a migration map, not a
reference.

---

## Step 8 — Verification gate before the PR

1. `dotnet build Ahjo.Vulkan.slnx` — clean. `TreatWarningsAsErrors=true`; if the
   `record struct` or the new overload produces an analyzer diagnostic, fix the
   code, do not `#pragma`-suppress (repo invariant 5).
2. `AHJO_VULKAN_TIER=validation dotnet test tests/Ahjo.Vulkan.Tests` — full
   suite, and quote the tier contract line.
3. `dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter "*DescriptorSetPool*"` —
   three rows, all `Allocated = -`.
4. AOT smoke:
   `dotnet publish samples/AotSmoke/AotSmoke.csproj -c Release -r win-x64 -p:IlcUseEnvironmentalTools=true`.
   Nothing here should threaten it (`Dictionary` with a struct key is already
   used by `HandleRegistry`), but the `EqualityComparer<IdleKey>.Default`
   instantiation is new and worth one publish.
5. Run `vulkan-validation-reviewer` (the diff touches `Pools/`) and
   `bench-coverage-checker` (hot-path diff) before opening the PR.
6. PR body: `Closes #182`.

---

## OPEN items

**OPEN-1 — Should `DescriptorSet.VariableDescriptorCount` be public?**
Specified as `internal` (spec D2) because a `FromRaw` set reports 0,
indistinguishable from a genuine zero — the same ambiguity that keeps `Layout`
internal. Logos may well want to read a heap set's capacity back. If the
implementer or reviewer thinks it should ship public in this PR, **stop and
ask**; do not decide it while implementing.

**OPEN-2 — Follow-up issues to file, not to implement here.** Both are named in
the spec and neither belongs in this PR. Confirm with the human whether to file
them now:
  (a) `Device.CreateDescriptorSetLayout` has no guard for
  `VUID-VkDescriptorSetLayoutBindingFlagsCreateInfo-pBindingFlags-03004` (the
  variable binding must be the highest binding number) —
  `Lifecycle/Device.cs:199-215` writes `pBindingFlags` with no ordering check.
  (b) The auto-grow retry keeps a sub-pool that provably cannot satisfy the
  request (spec D6/E4). Deliberately not fixed here: on this repo's only
  driver the failing-retry path is unreachable (spec M6), so the fix would ship
  untested.
