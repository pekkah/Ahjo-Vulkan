# Slang reflection completeness: unbounded arrays, buffer contents, and type conformances

**Issues:** #176 (priority:high, the blocker), #175, #177 — designed as **one arc**.
**Lands on:** `v0.10.0`'s Slang surface, immediately after #173 split the `Slang*`
reflection types from `SlangVulkanMapping`.
**Paired plan:** `../plans/2026-08-02-issue-175-177-slang-reflection-completeness.md`
**Revised:** 2026-08-02, after review — breaking changes are sanctioned (§Compatibility),
and completeness is the goal rather than a tight arc. Two decisions were re-derived
without the compatibility constraint; one changed (D1), one did not (D2's refusal
placement). Three pre-committed scope fallbacks were promoted to plan-A work, and a
completeness sweep (D3) was added.

## Why one spec for three issues

All three were filed by the same downstream consumer (Ahjo/Logos) against the
same four files, and all three are the same defect wearing three hats:
**reflection refuses, or stays silent, where it should report.** They also
share one organizing principle, which is #173's:

> Reflection reports what the shader says. The mapper is where a Vulkan-shaped
> decision gets made. Code generation is where a missing input finally refuses.

Each issue gets its own numbered decision below (D1 = #176, D2 = #177,
D3 + D4 = #175) and its own commit group in the plan, so each closes
independently. Splitting them into three specs would have restated the same
principle three times and hidden the one thing they collectively establish:
which of the three layers a refusal belongs in.

## Compatibility

**This is a 0.x preview package and breaking changes are acceptable.** Consumers
recompile. Nothing in this spec is shaped to preserve a signature, a struct
layout or a field's meaning, and where an earlier draft was, it has been
re-derived (see D1). What the release note needs to say:

- `SlangDescriptorBinding.Count` changes type from `uint` to
  `SlangDescriptorCount`. Reads become `binding.Count.Value` (throws for an
  unbounded binding) or `binding.Count.TryGetValue(out uint n)`. **Every existing
  read is a compile error, deliberately** — the meaning of the field changed, and
  a compile error is the only migration signal that cannot be missed.
- `SlangDescriptorBinding`, `SlangPushConstantRange`,
  `SlangVertexAttributeDescription`, `SlangEntryPointInfo` and
  `SlangCompileRequest` all gain members; struct layouts change.
- `SlangReflection` no longer throws when a program declares an unbounded
  descriptor array. Code that catches `NotSupportedException` around
  `program.Reflection` to detect bindless shaders must move that check to
  `SlangVulkanMapping.MapBinding`.
- `SlangSession.Compile` composes through `SlangProgramBuilder` internally. The
  component order, and therefore the reflected layout, is unchanged; this is
  asserted by the existing composition tests.

---

## Problem

### P1 (#176) — one unbounded array makes the whole program unreflectable

`SlangReflection`'s constructor is total and eager: it walks every descriptor
set, push-constant range and vertex input in one pass
(`src/Ahjo.Vulkan.Slang/SlangReflection.cs:43-88`). Inside that walk,

```csharp
// SlangReflection.cs:322-329
if (count < 0 || count > uint.MaxValue)
{
    throw new NotSupportedException(
        $"Descriptor range {r} of descriptor set {vkSet} reports descriptor count {count}. That is "
        + "Slang's sentinel for an unbounded array, or for a count that depends on unresolved "
        + "generic parameters or link-time constants. …");
}
```

Because the throw is in the constructor, `SlangProgram.Reflection`
(`SlangProgram.cs:76`) and `GetReflection` (`SlangProgram.cs:99-111`) both fail
outright. A program with one bindless set and three ordinary ones yields *no*
reflection: not the other sets, not the push-constant ranges, not the vertex
attributes. The reporter's `engine.bindless` module puts every pass in this
bucket.

The *reasoning* in that message is correct and is not being reversed. Reflection
cannot know the capacity. What is wrong is the **blast radius** — an
all-or-nothing refusal for a per-binding fact — and the **layer**: the count
only becomes a problem when someone builds a `VkDescriptorSetLayoutBinding` out
of it, which happens in `SlangVulkanMapping.MapBinding`
(`SlangVulkanMapping.cs:159-168`), not in the walk.

### P2 (#177) — `SlangCompileRequest` cannot name a conformance, and the failure is late

`SlangCompileRequest` has four members — `Path`, `Source`, `ModuleName`,
`EntryPoints` (`SlangCompileRequest.cs:19-52`). None of them can express a type
conformance, so `SlangSession.Compile` (`SlangSession.cs:141-173`) cannot
produce SPIR-V for any shader with an existential (`interface`-typed) parameter.
The capability exists one layer down —
`SlangProgramBuilder.AddTypeConformance(concrete, interface)`
(`SlangProgramBuilder.cs:108-116`), whose own XML doc already names the error
you get without it — but reaching it means abandoning the convenience path for
`CreateProgram()` + `LoadModule` + `FindEntryPoint` + `AddTypeConformance` +
`Link`.

Second half: without a conformance the program **links clean**. `Compile()`
returns, `EntryPointCount` is 1, `EntryPoint(0)` reports the right name and
stage, and reflection succeeds. The refusal arrives only from
`SlangProgram.Spirv` (`SlangProgram.cs:163-168`) as
`error[E50100]: no type conformances found`, wrapped in a
`SlangCompilationException` whose message is Slang's first diagnostic line
(`SlangCompilationException.cs:50-63`) and which says nothing about
`AddTypeConformance`.

### P3 (#175) — reflection describes where a resource is bound, never what is inside it

The whole public reflection surface is bindings, ranges, entry points and vertex
attributes (`SlangReflection.cs:94-244`). `SlangDescriptorBinding` is
`{Slot, Type, Count, Stages}` (`SlangDescriptorBinding.cs:8-19`);
`SlangPushConstantRange` is `{Stages, Offset, Size}`
(`SlangPushConstantRange.cs:6-11`). Nothing carries a member name, a member
offset, a member size or a buffer size — **and nothing carries the name of the
resource itself** (§E10).

The reporter's measurement is the sharpest statement of the gap: widening
`float2 UvScale` to `float4` inside a material uniform buffer changes that
buffer's size and every subsequent member offset, and **no assertion in a
97-test reflection suite moves**. The change is structurally unobservable
through this API, which is why it was recorded as a deliberate fault-sweep
survivor.

The interop is not the obstacle. The field walk already exists in this file for
struct-typed vertex inputs (`SlangReflection.cs:684-701`), using
`spReflectionTypeLayout_GetFieldCount`,
`spReflectionTypeLayout_GetFieldByIndex`,
`spReflectionVariableLayout_GetOffset`, `spReflectionVariableLayout_GetVariable`
and `spReflectionVariable_GetName` — all five already in the drift guard's
required-exports list (`tests/Ahjo.Vulkan.Slang.Native.Tests/SlangExportDriftTests.cs:89-108`).
What is missing is a public way to reach a walk the package already performs.

---

## Evidence

### E1. The two sentinels are distinguishable, and the header says which is which

`native/slang/downloaded/win-x64/include/slang.h:2361-2362`:

```c
#define SLANG_UNBOUNDED_SIZE (~size_t(0))
#define SLANG_UNKNOWN_SIZE   (SLANG_UNBOUNDED_SIZE - 1)
```

Read through the generated `long`-returning binding these reach C# as `-1` and
`-2`. The header's own doc comments separate them cleanly, and — importantly for
D4 — the same pair is documented on the *size* and *stride* calls the member
walk will make:

| call | documented sentinels | slang.h |
|---|---|---|
| `getDescriptorSetDescriptorRangeDescriptorCount` | `SLANG_UNBOUNDED_SIZE` **and** `SLANG_UNKNOWN_SIZE` | `3060-3072` |
| `getDescriptorSetDescriptorRangeIndexOffset` | `SLANG_UNKNOWN_SIZE` **only** | `3046-3058` |
| `getSubObjectRangeSpaceOffset` | `SLANG_UNKNOWN_SIZE` **only** | `3104-3115` |
| `spReflectionTypeLayout_GetSize` | both | `2724-2733` |
| `spReflectionTypeLayout_GetStride` | both | `2736-2745` |
| `spReflectionTypeLayout_GetElementStride` | both | `2844-2852` |

So #176's secondary ask ("split the two causes, if cheap") is cheap: it is a
`-1` vs `-2` comparison on a value the walk already reads
(`SlangReflection.cs:307`). It also confirms the *other* two refusals in the
walk (`SlangReflection.cs:314-320`, `404-410`) are correctly all-or-nothing —
an unresolved *index offset* or *space offset* means there is no binding number
to emit at all, whereas an unresolved *count* leaves a perfectly usable
`(set, slot, type)`. And it means a buffer **member** can be unsized too (a
trailing runtime-sized array), which D4 has to represent rather than truncate.

**Uncertainty, stated:** the table above is read off the pinned header, not
measured. This repo's Slang rules were all established by measurement against
emitted SPIR-V precisely because the header has misled before —
`getSubObjectRangeSpaceOffset` looks correct in the header and returns `0` for
every sub-object range (`src/Ahjo.Vulkan.Slang/CLAUDE.md`, rule 1). The plan
therefore pins the `-1` → `Unbounded` mapping with a fixture assertion rather
than trusting the doc comment.

### E2. `uint` cannot safely carry "no count", because `Ahjo.Vulkan` launders zero

`src/Ahjo.Vulkan/Pipelines/DescriptorBinding.cs` carries
`DescriptorBindingFlags BindingFlags` alongside `Count`, and
`DescriptorBindingFlags` (`src/Ahjo.Vulkan/Pipelines/DescriptorBindingFlags.cs`)
has `PartiallyBound = 0x4` and `VariableDescriptorCount = 0x8`. The target type
can express a bindless binding; nothing new is needed in `Ahjo.Vulkan`.

Two facts from that file decide D1:

- Its remarks state that the layout/template build paths keep a
  `Count == 0 ? 1` normalization for zeroed span elements. So **`Count = 0` is
  not a loud poison value**: a caller who copies `Count` across without checking
  a flag gets a silently valid 1-descriptor binding. `uint.MaxValue` is worse —
  it is the value the current comment says crashes
  `vkCreateDescriptorSetLayout` (`SlangReflection.cs:309-313`). There is no
  `uint` that is safe to put in `Count` for an unbounded binding: every value is
  either a plausible count, a laundered 1, or a driver crash.
- `VariableDescriptorCount` may be carried by **at most one binding per set**
  (it must be the binding with the highest binding number). The reporter's set 0
  is *three* unbounded arrays, so a mapper that set the flag automatically would
  produce an invalid layout for the exact shape the issue is about.

### E3. The in-repo blast radius of the mapping API is two call sites

Grepped across `src/`, `samples/` and `tests/` (excluding `obj/`, `bin/` and
`SlangVulkanMapping.cs` itself):

| API | in-repo call sites |
|---|---|
| `MapBindings(ReadOnlySpan<SlangDescriptorBinding>)` | 1 — `tests/Ahjo.Vulkan.Slang.Tests/SlangReflectionTests.cs:636` |
| `MapPushConstantRanges(...)` | 1 — `SlangReflectionTests.cs:642` |
| `MapBinding`, `MapVertexAttribute`, `MapBindingType`, `MapScalarFormat` | **0** |

Direct reads of `SlangDescriptorBinding.Count` in-repo: `SlangReflectionTests.cs`
(the texture-array case, `:110-120`) and `SlangVulkanMapping.MapBinding`
(`:165`). `samples/AotSmoke/Program.cs:67-73` reflects but never maps and never
reads `Count`. So D1's type change costs three edits in this repository; the
real audience is downstream, which is why it is worth getting right rather than
cheap.

### E4. A conformance-aware `Compile` can reuse the builder rather than duplicate it

`SlangSession.Compile` (`SlangSession.cs:141-173`) ends in a private
`Link(module, entryPoints, carriedWarnings)` (`SlangSession.cs:343-385`) that
composes `[module, ep₀, ep₁, …]` and links. `SlangProgramBuilder.Link()`
(`SlangProgramBuilder.cs:132-171`) composes the same list in the same order,
and — when conformances are present — composes twice, resolving type names
against the first composite's layout before appending the conformance
components (`SlangProgramBuilder.cs:192-237`). The component order is the only
thing that matters for layout equivalence (`SlangProgramBuilder.cs:8-23`), and
it is identical. So routing `Compile` through the builder is a deduplication,
not a behaviour change — with one thing to preserve: `Compile` currently carries
`module.Warnings` into the program, which the builder's public `Link()` has no
parameter for.

### E5. Slang exposes a specialization-parameter count, but not the predicate E50100 tests

`IComponentType::getSpecializationParamCount()` is declared at `slang.h:5336`
and generated at
`src/Ahjo.Vulkan.Slang.Native/Generated/IComponentType.cs:54`, so it is bound
and reachable on the linked component the wrapper already holds
(`SlangProgram.cs:78-79`). `spReflectionTypeLayout_isBindingRangeSpecializable`
is bound too (`SlangApi.cs:460`).

But neither answers the question a caller wants answered. `E50100` is not "this
program is unspecialized" — it is "this program needs dynamic dispatch and no
type conformance component was composed in". The `ParameterBlock<IBsdf>` shape
is specialization-**invariant** by design: its reflected layout is byte-identical
before and after (issue-166 spec, E14), and adding a conformance does not
consume a specialization parameter. Whether `getSpecializationParamCount()`
happens to be a usable *proxy* is measurable but unmeasured; the plan measures
it, ships it when the measurement is clean, and D2 does not depend on the answer.

### E6. `spReflection_ToJson` is bound, and the header calls it with a null request

`SlangApi.cs:800` generates

```csharp
int spReflection_ToJson(SlangProgramLayout* reflection, ICompileRequest* request, ISlangBlob** outBlob);
```

and the header's own C++ shim passes `nullptr` for `request`
(`slang.h:3833-3835`). It was also executed during the issue-166 investigation —
that spec quotes its output when establishing that per-entry-point binding lists
are not narrowed (issue-166 spec, E3). It is **not** in the drift guard's
required-exports list (`SlangExportDriftTests.cs:40-115`), so shipping it needs
that list extended.

### E7. The reflection walk already holds two of the three type layouts a member walk needs

- **Push-constant blocks.** `BuildPushConstantRanges` already resolves the
  declaring parameter's type layout *and its name* and takes the block size from
  `GetSize(GetElementTypeLayout(found), UNIFORM)`
  (`SlangReflection.cs:436-507`). The member walk starts from the same pointer;
  the name is computed at `:458` and then discarded.
- **`ParameterBlock<T>` implicit uniform buffers.** Step 2 of the walk already
  tests `GetSize(structTypeLayout, UNIFORM) > 0` and synthesizes the binding-0
  uniform buffer from it (`SlangReflection.cs:356-371`). `structTypeLayout` *is*
  the struct whose members are wanted, and `(absoluteSet, 0)` is its key.
- **Explicit `ConstantBuffer<T>` / structured buffers.** These are the gap. The
  walk sees them only as descriptor ranges `(s, r)`, which carry a type
  (`SlangBindingType`) but no leaf type layout and no name.

### E8. Three routes from a descriptor range back to a type layout, in order of preference

The join E7 needs is the single unmeasured native hypothesis in this arc, so it
gets a ladder rather than a single bet:

1. **Binding ranges.** `getBindingRangeCount` (`SlangApi.cs:453`),
   `getBindingRangeDescriptorSetIndex` (`:485`),
   `getBindingRangeFirstDescriptorRangeIndex` (`:489`),
   `getBindingRangeLeafTypeLayout` (`:467`, already used by the ParameterBlock
   recursion at `SlangReflection.cs:389-390`), `getBindingRangeLeafVariable`
   (`:470`), `getBindingRangeImageFormat` (`:473`),
   `isBindingRangeSpecializable` (`:460`). This route yields *everything* D3 and
   D4 want in one pass — layout, name, image format, specializability.
2. **The global parameter list.** `spReflection_GetParameterCount` /
   `GetParameterByIndex` (already used at `SlangReflection.cs:436-444`), then
   `spReflectionVariableLayout_GetSpace(param, DESCRIPTOR_TABLE_SLOT)`
   (`SlangApi.cs:585`) and `GetOffset(param, DESCRIPTOR_TABLE_SLOT)` for the
   key. Covers global-scope parameters; block interiors need the same recursion
   the walk already performs.
3. **Neither.** Ship only what E7's first two bullets give for free
   (push-constant blocks and `ParameterBlock` uniform buffers), and report.

Route 1 is bound but has never been executed in this repository, and it is the
same *kind* of call as `getSubObjectRangeSpaceOffset`, which looks correct in
the header and returns `0` for every sub-object range. Route 3 is a genuine last
resort after both measurements fail, not a scope-management device.

### E9. The existing test suite already has the right oracle, and it does not reach members

`tests/Ahjo.Vulkan.Slang.Tests/SpirvDecorations.cs` reads `OpName` (5),
`OpVariable` (59) and `OpDecorate` (71), extracting `DescriptorSet` (34),
`Binding` (33) and `Location` (30) — the ground truth every load-bearing
reflection test asserts against, because "reflection agreeing with itself proves
nothing". It reads no `OpMemberDecorate` (72) and no `OpMemberName` (6), which
is exactly why the fault-sweep survived: the suite has no member-offset oracle
at all.

### E10. What else Slang answers and this package currently drops

Audited the generated `SlangApi` against the wrapper's public surface. Facts
Slang exposes, that a material system or an inspector needs, and that nothing in
`Ahjo.Vulkan.Slang` reports today:

| fact | bound at | why it matters | in this arc? |
|---|---|---|---|
| the **name** of a descriptor binding | `SlangApi.cs:470` + `:538` | `SlangDescriptorBinding` is `{Slot, Type, Count, Stages}` — a material system binding `gAlbedo` by name has #175's problem one level up | **yes**, D3 |
| the **name** of the push-constant block | already computed and discarded, `SlangReflection.cs:458` | same | **yes**, D3 |
| vertex input **semantic name + index** | `SlangApi.cs:592`, `:596` — already in the drift list | a vertex-buffer binder matches `POSITION` / `TEXCOORD0`, not the field's C-side name | **yes**, D3 |
| compute **thread-group size** | `SlangApi.cs:766` | a compute dispatch cannot compute its group count without it | **yes**, D3 |
| storage-image **format** | `SlangApi.cs:473` | `[[vk::image_format]]` on a `RWTexture2D` — a caller must match the view's format | **yes**, D3 (reflection side only) |
| binding **specializability** | `SlangApi.cs:460` | tells a caller which binding is the interface-typed one — the open-world case #177 is about | **yes**, D3, measured |
| entry-point **name override** | `SlangApi.cs:750` | if it can differ from `getName`, `VkPipelineShaderStageCreateInfo.pName` must use it | **yes**, D3, measured |
| type **alignment / stride / matrix layout** | `SlangApi.cs:393`, `:389`, `:433` | a caller writing a UBO by hand needs them; matrix layout decides row/column order | **yes**, D4 |
| **user attributes** on variables and types | `SlangApi.cs:291-310` (name, arg count, int/float/string args) | `[Attribute]` on a shader struct field is how a material editor gets ranges and display names | **no** — a design of its own; §Follow-ups |
| member **default values** | `SlangApi.cs:557-565` | material defaults | **no** — cannot express a `float3` default through the bound int/float getters; §Follow-ups |
| `spReflectionType_GetFullName` | `SlangApi.cs:372` | specialized generic type names | **no** — `GetName` first; §Follow-ups if it proves insufficient |

### E11. Where these tests run, and which known hazards they touch

`Ahjo.Vulkan.Slang.Tests` is wrapper coverage and runs on `windows-latest`
only (`.github/workflows/ci.yml:41`, `:166-167`); `Ahjo.Vulkan.Slang.Native.Tests`
runs on both RIDs in `build-slang-native.yml:111-112`. Consequences for the
known hazards:

- **#170 (`specialize()` SIGSEGV, linux-x64 only).** Untouched. Nothing in this
  arc calls `IComponentType::specialize`; #177's conformance work goes through
  `createTypeConformanceComponentType` (`SlangProgramBuilder.cs:239-261`), which
  the issue-166 work verified composes, links and emits valid SPIR-V for exactly
  the interface-typed-`ParameterBlock` shape that crashes `specialize`. The new
  existential fixtures run only in the Windows lane, and even on Linux they
  would take the safe route. The `Specialize`-is-absent decision
  (`src/Ahjo.Vulkan.Slang/CLAUDE.md`) stands unchanged.
- **#169 (Vulkan SDK's `slang-glslang.dll` shadowing ours on Windows `PATH`).**
  Low risk but worth naming: it changes which downstream optimizer runs.
  Everything this arc asserts — uniform member offsets, buffer sizes,
  descriptor counts — is decided by the Slang front end and emitted as
  `OpMemberDecorate Offset` regardless of optimization level, so a shadowed
  glslang cannot move these numbers. If it does, that is a finding worth
  reporting, not a test to relax.

---

## Decision

The arc's single organizing sentence, applied four times:

| layer | what it does | decision |
|---|---|---|
| `SlangReflection` | reports every fact Slang states — including "this count is a sentinel", "this binding is called `gAlbedo`", "this buffer has these members at these offsets" | D1 (report), D3 (report more), D4 (report deeper) |
| `SlangVulkanMapping` | refuses, or takes the missing input as a parameter, when a Vulkan value cannot be derived | D1 |
| `SlangProgram.Spirv` | refuses where Slang refuses, and explains itself | D2 |

### D1 (#176) — the count becomes an option type; the mapper refuses or takes a capacity

**Re-derived without the compatibility constraint, and it changed.** The earlier
draft kept `Count` as a `uint` that reads `0` for an unbounded binding, with a
sibling `CountKind` enum — a shape chosen partly so existing reads would keep
compiling. E2 kills it on merits: **no `uint` value is safe in that field.**
`0` is laundered into `1` by `Ahjo.Vulkan`'s own layout path, `uint.MaxValue` is
the driver crash the current code exists to prevent, and any other value is a
plausible-looking lie. A caller who forgets to check a sibling flag must not get
a number that works.

```csharp
public enum SlangDescriptorCountKind
{
    Fixed,      // Slang stated a descriptor count
    Unbounded,  // SLANG_UNBOUNDED_SIZE — an unsized (bindless) array
    Unknown,    // SLANG_UNKNOWN_SIZE — depends on unresolved generics or link-time constants
}

public readonly record struct SlangDescriptorCount
{
    public static SlangDescriptorCount Fixed(uint count);
    public static SlangDescriptorCount Unbounded { get; }
    public static SlangDescriptorCount Unknown { get; }

    public SlangDescriptorCountKind Kind { get; }
    public uint Value { get; }                       // InvalidOperationException when Kind != Fixed
    public bool TryGetValue(out uint count);
    public bool IsUnbounded => Kind == SlangDescriptorCountKind.Unbounded;
}

public readonly record struct SlangDescriptorBinding
{
    public uint Slot { get; init; }
    public string Name { get; init; }                // D3
    public SlangBindingType Type { get; init; }
    public SlangDescriptorCount Count { get; init; }
    public ShaderStages Stages { get; init; }
    // + ImageFormat, IsSpecializable — D3

    public SlangDescriptorBinding() { Count = SlangDescriptorCount.Fixed(1); }
}
```

One member rather than two, so there is **no incoherent state to construct** —
a `uint? Count` plus a `CountKind` enum can disagree, and a hand-built value
that says `Fixed` with a null count would be a bug the type system permits. And
`Count.Value` throwing `InvalidOperationException` is the property that makes an
unbounded binding *impossible to misread*: the only ways to read a number out
of it are one that throws and one that forces a branch.

The walk stops throwing on the count (`SlangReflection.cs:322-329` is deleted)
and classifies instead: `-1` → `Unbounded`, `-2` → `Unknown`, `0 … uint.MaxValue`
→ `Fixed`, anything else keeps throwing (a value that is neither a documented
sentinel nor a representable count is a binding we do not understand, and
guessing is how the driver crash at `SlangReflection.cs:309-313` happens). The
two *other* refusals in the walk — index offset and space offset — stay exactly
as they are, per E1: those leave no binding number to report at all.

**Mapping.** `SlangVulkanMapping` gains one overload each:

```csharp
public static DescriptorBinding MapBinding(this SlangDescriptorBinding binding);
public static DescriptorBinding MapBinding(this SlangDescriptorBinding binding, uint descriptorCount);

public delegate uint SlangUnboundedCapacity(SlangDescriptorBinding binding);
public static DescriptorBinding[] MapBindings(this ReadOnlySpan<SlangDescriptorBinding> bindings);
public static DescriptorBinding[] MapBindings(this ReadOnlySpan<SlangDescriptorBinding> bindings, SlangUnboundedCapacity capacity);
```

This mirrors `MapVertexAttribute(binding = 0, offset = 0)`
(`SlangVulkanMapping.cs:51`): the mapper takes the input the shader does not
state. An **overload** rather than an optional parameter — that choice survives
the compat re-derivation on its own merits, because an optional
`descriptorCount = 0` would need `0` to mean "unset", colliding with a
legitimately supplied zero. A **resolver** rather than one `uint` for the bulk
overload because the reporter's set 0 is three unbounded arrays whose capacities
their heap picks independently. `MapBinding(descriptorCount)` on a `Fixed`
binding throws `ArgumentException`; the bulk overload never asks the resolver
about one, so a caller's resolver may assume it is only ever asked about
bindings it must size.

**`DescriptorBindingFlags` is not set automatically** (E2): at most one binding
per set may carry `VariableDescriptorCount`, and the motivating set has three
unbounded arrays. The mapper produces `Count = descriptorCount` with
`BindingFlags` at its default and the XML doc tells the caller to add
`VariableDescriptorCount` / `PartiallyBound` themselves.

#### Why not the alternatives

- **`uint Count` = 0 plus a `CountKind` flag** (the previous draft, and what the
  issue reporter floated) — rejected on E2: `Ahjo.Vulkan` normalizes `Count == 0`
  to `1`, so an ignored flag produces a silently valid 1-descriptor binding.
  The reporter's instinct — that the sentinel must leave `Count` — is right; the
  option type takes it one step further so there is no readable number at all.
- **`uint? Count` plus a separate `CountKind`** — rejected: two members encoding
  one fact, with constructible states where they disagree.
- **`Count = uint.MaxValue`** — rejected: the exact value
  `SlangReflection.cs:309-313` exists to keep away from a driver.
- **Keep the throw, make reflection lazy or partial** — rejected: it turns an
  eagerly-computed type (`SlangReflection.cs:29-32`) into one with per-property
  failure modes, and the caller still cannot get the bindless set they want.
- **`MapBinding` auto-sets `VariableDescriptorCount`** — rejected on E2.
- **One `uint` capacity for the bulk mapper** — rejected: cannot size three
  arrays independently.

### D2 (#177) — conformances on the request; the late failure stays late but explains itself

**(a) `SlangCompileRequest.TypeConformances`.**

```csharp
public readonly record struct SlangTypeConformance(string ConcreteType, string InterfaceType);

public readonly record struct SlangCompileRequest
{
    …
    public IReadOnlyList<SlangTypeConformance>? TypeConformances { get; init; }
}
```

`SlangSession.Compile` forwards each entry to
`SlangProgramBuilder.AddTypeConformance`, and — per E4 — is **rerouted through
the builder entirely**, deleting the duplicate composition in
`SlangSession.Link` (`SlangSession.cs:343-385`). The builder gains an
`internal SlangProgram Link(string? carriedWarnings)` so the module's warnings
still reach the program. One composition path, same order, so the existing
composition and reflection tests are the regression check.

A named `SlangTypeConformance` rather than a `(string, string)` tuple: XML docs,
and Slang's `conformanceIdOverride` (`SlangProgramBuilder.cs:247-249`, always
`-1` today) is a member this type will want once someone needs deterministic
dispatch IDs. It is deliberately **not** exposed now — shipping a knob this
package has never exercised would be completeness theatre, not completeness.

**(b) The late failure.** No new throw. `Link()` keeps returning a program for
an existential shader with no conformance, and `Spirv(i)` keeps being where it
fails. What changes:

- When `getEntryPointCode` fails and the diagnostics text contains `E50100`,
  `SlangProgram.Spirv` throws a `SlangCompilationException` whose **message**
  appends one sentence naming the fix (`SlangCompileRequest.TypeConformances`
  and `SlangProgramBuilder.AddTypeConformance`). `Diagnostics` stays verbatim —
  that contract is explicit (`SlangCompilationException.cs:44-48`).
- `SlangProgram` gains `public int SpecializationParameterCount { get; }`, a
  cached passthrough of `IComponentType::getSpecializationParamCount()`
  (E5) — **shipped if and only if** the plan's measurement shows it discriminates
  (non-zero for the existential fixture, zero for the concrete control and for
  an interface that is only dispatched statically). It is a report, not a
  predicate: its XML doc records the four measured values and states that it
  does not predict whether code generation will succeed.
- `SlangProgram`'s and `Spirv`'s XML docs state that a linked program is not
  necessarily a compilable one, and name this as the case where that is true.
  The README's "Specializing an interface-typed `ParameterBlock`" section
  (`src/Ahjo.Vulkan.Slang/README.md:108-134`) gains the `TypeConformances` form.

**Why the throw does not move to `Link()`** — unchanged by the wider scope, and
worth restating because a wider scope makes the temptation stronger. This is the
arc's consistency test. #176's entire complaint is *"do not refuse the whole
program for one thing you cannot resolve"*. A `Link()` that refuses an entire
program because a conformance was not supplied is the same defect with a
different subject — and strictly worse, because the predicate is a guess: E5
shows Slang exposes no "is this program codegen-ready" query, and
`getSpecializationParamCount()` answers a different question. A program with an
interface-typed parameter that is only ever dispatched statically would be
refused for nothing. Completeness is a reason to report more facts, never a
reason to act on a fact that does not mean what you want it to mean.

#### Why not the alternatives

- **Throw from `Link()` when conformances are absent and specialization params
  exist** — rejected, above: unproven predicate, contradicts D1.
- **An opt-out (`AllowUnresolvedConformances()`) to escape such a throw** —
  rejected: surface added solely to undo a refusal we decided not to add.
- **Generate SPIR-V eagerly at `Link()` so failures are early** — rejected: it
  turns linking into N code generations, the exact cost
  `SlangStageAttribution.PerEntryPointUsage` is opt-in to avoid
  (`SlangStageAttribution.cs:31-40`).
- **`IReadOnlyList<(string, string)>`** — rejected: cannot grow a third member.
- **`TypeConformances` on `SlangSessionDescription`** — rejected: a conformance
  is a property of one composed program, not of a session's module namespace.

### D3 (#175, part one) — the completeness sweep: report the facts already on the table

E10's audit found six facts Slang answers, that a material system or an
inspector needs, and that this package currently drops. All six are one call
each, none needs a new concept, and five of them ride the same binding-range
pass D4 needs anyway (E8 route 1):

```csharp
public readonly record struct SlangDescriptorBinding
{
    public uint Slot { get; init; }
    public string Name { get; init; }                 // "" when Slang reports none
    public SlangBindingType Type { get; init; }
    public SlangDescriptorCount Count { get; init; }
    public ShaderStages Stages { get; init; }
    public SlangImageFormat ImageFormat { get; init; } // SLANG_IMAGE_FORMAT_UNKNOWN when unspecified
    public bool IsSpecializable { get; init; }         // this binding's type is existential/generic
}

public readonly record struct SlangPushConstantRange
{
    public string Name { get; init; }                 // the [[vk::push_constant]] block's parameter name
    public ShaderStages Stages { get; init; }
    public uint Offset { get; init; }
    public uint Size { get; init; }
}

public readonly record struct SlangVertexAttributeDescription
{
    …                                                 // unchanged members
    public string SemanticName { get; init; }         // "POSITION", "TEXCOORD" — "" when none
    public uint SemanticIndex { get; init; }
}

public readonly record struct SlangEntryPointInfo(
    string Name,
    ShaderStages Stage,
    uint ThreadGroupSizeX,                            // 1,1,1 for non-compute stages
    uint ThreadGroupSizeY,
    uint ThreadGroupSizeZ);
```

`Name` on a binding is the one that matters most: it is #175's complaint one
level up. A material system that can ask "what are the members of the buffer at
set 2 binding 0" but not "which binding is `gAlbedo`" still has to hard-code
slot numbers. The synthesized `ParameterBlock` uniform buffer at binding 0
(`SlangReflection.cs:356-371`) has no Slang-reported name of its own, so it
takes the block parameter's name — which the walk must thread down anyway for
D4's buffer layouts.

`ImageFormat` is reported as Slang's own `SlangImageFormat`. Mapping it to
`VkFormat` is a `SlangVulkanMapping` addition and is **not** in this arc: no
Vulkan call at layout-creation time needs it, so the mapping table would be
surface without a consumer. Reflection reports; the mapper can grow the table
when something needs it (§Follow-ups).

`IsSpecializable` and the entry-point name override are both **measured before
they ship**: the first must be `true` for the existential fixture's block and
`false` for concrete bindings; the second ships only if `getNameOverride` can
actually differ from `getName`, verified against the `OpEntryPoint` name in the
emitted SPIR-V. A field that is always `false`, or always equal to another
field, is noise.

#### Why not the alternatives

- **Leave names out and let callers use slot numbers** — rejected: it is the
  same defect #175 reports, one level up, and the data is in a pass we are
  already writing.
- **Map `SlangImageFormat` to `VkFormat` here** — rejected for now: a ~40-entry
  table with no in-arc consumer. Reflection-side only.
- **Ship `IsSpecializable` / `NameOverride` unmeasured** — rejected: a field that
  is constant is worse than no field, because it reads as information.
- **Also expose user attributes and member defaults** (E10's last three rows) —
  rejected *for this arc*: attributes are a sub-surface with their own argument
  model (`spReflectionUserAttribute_GetArgumentValue{Int,Float,String}`) and
  deserve their own decision; the bound int/float default getters cannot express
  a `float3` default, so shipping them would describe defaults incorrectly.
  Named in §Follow-ups with the exact entry points.

### D4 (#175, part two) — a flattened, pre-order member list per buffer, losslessly a tree

**A buffer layout is a Slang fact, not a Vulkan fact.** This is the #173
question the issue raises implicitly, and it decides where the API lives. The
offsets that matter are the ones baked into the emitted SPIR-V for this
program's target, and they are what Slang reports under
`SLANG_PARAMETER_CATEGORY_UNIFORM`. There is no Vulkan type to map onto: no
`Ahjo.Vulkan` description type describes a buffer's interior, and Vulkan never
sees member names. **`SlangVulkanMapping` gains nothing for #175.**

```csharp
public readonly record struct SlangBufferMember
{
    public string Name { get; init; }                  // dotted path from the buffer root: "Params.UvScale"
    public int ParentIndex { get; init; }              // index of the enclosing struct member, -1 at the root
    public uint Offset { get; init; }                  // bytes from the start of the buffer (UNIFORM)
    public uint Size { get; init; }                    // bytes (UNIFORM)
    public uint Stride { get; init; }                  // bytes including trailing padding (UNIFORM)
    public uint Alignment { get; init; }               // bytes (UNIFORM)
    public bool IsUnsized { get; init; }               // Slang reported a size/count sentinel (E1)
    public string TypeName { get; init; }              // "MaterialParams", "float4" — "" when unnamed
    public SlangTypeKind Kind { get; init; }
    public SlangScalarType ScalarType { get; init; }
    public uint ComponentCount { get; init; }
    public uint RowCount { get; init; }
    public uint ColumnCount { get; init; }
    public SlangMatrixLayoutMode MatrixLayout { get; init; }  // matrices only
    public uint MatrixStride { get; init; }            // matrices only; 0 when not derivable — see below
    public uint ElementCount { get; init; }            // arrays only; 0 when unsized
    public uint ElementStride { get; init; }           // arrays only
}

public sealed class SlangBufferLayout
{
    public string Name { get; }                              // declaring parameter's name, "" when none
    public uint Size { get; }                                // the buffer's UNIFORM byte size
    public ReadOnlySpan<SlangBufferMember> Members { get; }  // pre-order, declaration order
    public bool TryGetMember(string path, out SlangBufferMember member);
}

// on SlangReflection:
public bool TryGetBufferLayout(uint set, uint slot, out SlangBufferLayout layout);
public bool TryGetPushConstantLayout(out SlangBufferLayout layout);
public string ToJson();
```

The type vocabulary is deliberately the same as
`SlangVertexAttributeDescription` (`SlangVertexAttributeDescription.cs:8-18`) —
`Kind` / `ScalarType` / `ComponentCount` / `RowCount` / `ColumnCount` — so the
package has one way of describing a type, not two.

**Everything a hand-written UBO writer needs is present.** `Offset` and `Size`
alone are not enough: `Stride` (`spReflectionTypeLayout_GetStride`,
`SlangApi.cs:389`) is what separates a member's bytes from its padded footprint,
`Alignment` (`:393`) is what a caller checks its own struct against, and
`MatrixLayout` (`spReflectionTypeLayout_GetMatrixLayoutMode`, `:433`) decides
whether a `float4x4` is written row- or column-major — the difference between a
correct transform and a transposed one, with no other symptom.

`MatrixStride` has **no dedicated getter in the bound surface**. The plan
measures whether `GetElementTypeLayout` on a matrix type layout yields the
row/column vector's layout, whose `GetStride(UNIFORM)` would be it; if it does,
the field carries a measured value, and if it does not, the field ships as `0`
with an XML doc saying it is not derivable and that `Size`, `RowCount`,
`ColumnCount` and `MatrixLayout` are what a caller has. Deriving it by dividing
`Size` by `RowCount` would be a guess, and a guess about matrix strides is
exactly the silent-wrong-bytes failure this issue exists to remove.

**Flattened, pre-order, dotted paths, struct nodes retained — and provably a
tree.** The list is a depth-first pre-order flattening of the element struct in
declaration order. A `struct`-typed member appears as its own entry
(`Kind == SLANG_TYPE_KIND_STRUCT`, with its own offset, size and alignment)
immediately followed by its fields, whose `Name` is the parent path plus `"."`
plus the field name.

The single representation is **sufficient, not merely cheaper**, and
`ParentIndex` is what makes that true rather than an argument about string
parsing:

- *Tree from list, O(n):* every member's parent is `Members[ParentIndex]`, or the
  buffer root when `ParentIndex < 0`; a member's children are the entries whose
  `ParentIndex` is its own index, and pre-order guarantees they are contiguous
  and in declaration order. Depth is a parent-chain walk. No string parsing, no
  ambiguity, no reliance on `.` being absent from identifiers.
- *Leaves from tree:* filter `Kind != SLANG_TYPE_KIND_STRUCT` and read `Name`.

So both consumers the issue names are served exactly, and the "two
representations can only diverge" objection to shipping a parallel nested type
stands: a second representation would carry no information this one lacks.

**Arrays are leaves.** An array-typed member reports `ElementCount` and
`ElementStride` and is not expanded; `Lights[0].Color` is not reachable through
the typed surface in this phase. Expanding N elements multiplies the member list
by N for offsets a caller computes as `Offset + i * ElementStride`, and
recursing into an array's element struct would introduce members whose `Offset`
is element-relative while every other member's is buffer-relative — a mixed
offset base is precisely the silent-wrong-offset trap #175 is about. The
completeness ladder for this case is explicit: the typed surface covers scalar,
vector, matrix and struct leaves; an array reports count, stride and element
type; anything beyond that is `ToJson()` until a follow-up designs
element-relative layouts properly.

**Resources are not members.** A field whose UNIFORM size is `0` — a
`Texture2D`, a `SamplerState`, a struct of those — is skipped. They occupy no
bytes in the buffer, and listing them at offset 0 with size 0 invites a caller
to write there.

**The category is `SLANG_PARAMETER_CATEGORY_UNIFORM`, everywhere, and a test
pins it.** Offsets come from
`spReflectionVariableLayout_GetOffset(field, UNIFORM)` and accumulate down the
struct exactly as vertex-input locations already do
(`SlangReflection.cs:697-698`). Getting this category wrong is the
silent-wrong-offsets failure the issue exists to prevent, so the test asserts
against `OpMemberDecorate … Offset` read out of the emitted SPIR-V, not against
reflection agreeing with itself (E9).

**Which buffers get a layout: all of them.** Push-constant blocks and
`ParameterBlock<T>` implicit uniform buffers come free (E7); standalone
`ConstantBuffer<T>`, the global scope's implicit constant buffer and structured
buffer elements come from E8's join, pursued down all three rungs of the ladder
before anything is dropped. The join is implemented as a **separate additive
pass**, not by modifying the SPIR-V-verified binding walk, so a failure there
cannot regress what already works.

**`ToJson()` ships**, as the complement #175 asks for. `spReflection_ToJson` is
bound (E6) and the header calls it with a null request. Its XML doc states that
the format is Slang's, is unstable across versions, and is for diagnostics
rather than production parsing — a complement to the typed surface, never a
substitute for it. It is **lazy and cached**: `SlangReflection` keeps a managed
reference to its `SlangProgram` so the layout pointer can be re-obtained on
demand, which costs nothing when nobody asks and avoids serializing every
program's whole layout into a string at construction. That reference changes no
ownership or disposal semantics — the reflection still does not own the program,
and calling `ToJson()` after the program is disposed throws
`ObjectDisposedException` from the existing `LinkedComponent` guard
(`SlangProgram.cs:78-79`).

Everything else is computed eagerly in the constructor, like the rest of
`SlangReflection` (`SlangReflection.cs:29-32`). The cost is a few strings and
arrays per buffer at setup time; invariant #3 does not apply to this project
(`src/Ahjo.Vulkan.Slang/CLAUDE.md`), and invariant #2 holds trivially — arrays,
strings and direct calls, no reflection, no dynamic codegen.

#### Why not the alternatives

- **A nested `SlangBufferMember.Members` tree** — rejected: the primary consumer
  wants leaf paths, and getting them from a tree means writing the walk this API
  exists to remove.
- **Both a tree and a flat list** — rejected, and now demonstrably rather than
  merely on cost grounds: `ParentIndex` + pre-order makes the tree recoverable
  in O(n), so a second representation would carry no extra information and could
  only diverge.
- **Leaves only, no struct nodes** — rejected: the struct nodes are what make the
  tree recoverable and what give an inspector a `Params` row with its own size.
- **Dotted paths as the only parent link** — rejected as *sufficient*: it works
  for HLSL identifiers but makes reconstruction O(n²) string work and depends on
  `.` never appearing in a name. `ParentIndex` costs 4 bytes and removes the
  argument.
- **Expanding array elements, or recursing into array element structs** —
  rejected: N× entries for derivable offsets, or a mixed offset base.
- **Deriving `MatrixStride` from `Size / RowCount`** — rejected: a guess about
  the exact quantity whose wrongness has no symptom until the transform is
  transposed.
- **`ToJson()` as a substitute for the typed surface** — rejected: a JSON string
  cannot be asserted per-member-offset without a JSON parser in the suite, so it
  would not close the fault-sweep survivor. Accepted as a complement.
- **Eager `ToJson()` in the constructor** — rejected once the program reference
  made lazy possible: serializing every layout for the few callers who ask is
  waste, and the earlier draft only preferred eager because it was avoiding
  holding the reference.
- **Hanging `SlangBufferLayout?` off `SlangDescriptorBinding`** — rejected: a
  reference type inside the value struct that feeds the mapper, for a fact most
  callers of that path never read.
- **Reporting resource fields as zero-size members** — rejected: an entry at
  offset 0 with size 0 reads as writable and is not.
- **Putting the member walk behind `SlangVulkanMapping`** — rejected on the #173
  split: there is no Vulkan decision here.
- **Parsing the emitted SPIR-V for member offsets inside the wrapper** —
  rejected: it duplicates what Slang computed, and it is what the consumer is
  doing today and filed the issue to stop doing.

---

## Sequencing

Four commit groups, in issue order, with #175's work split so the one unmeasured
native hypothesis is tested by a small consumer before a large one depends on it:

1. **A = #176.** The blocker, the smallest, and the one that changes
   `SlangDescriptorBinding.Count`'s type. First, so B, C and D are additive on
   top of the final shape.
2. **B = #177.** A disjoint file set (`SlangCompileRequest`, `SlangSession`,
   `SlangProgramBuilder`, `SlangProgram`), reviewable without the reflection
   walk in your head.
3. **C = #175's completeness sweep (D3).** Introduces the binding-range pass
   (E8 route 1) and consumes it for names, image formats and specializability —
   simple consumers whose test is "does this name match the `OpName` in the
   SPIR-V". If the join is broken, it is found here, cheaply.
4. **D = #175's buffer layouts (D4) + `ToJson()`.** The largest surface, built
   on a pass group C has already proven.

**Recommended PR split.** A+B is a small, self-contained PR that unblocks the
consumer immediately (#176 is the blocker and #177 is four files); C+D is
larger, carries the join measurement, and adds the new `OpMemberDecorate` test
oracle. Two PRs — `Closes #176, Closes #177` then `Closes #175` — keep the
blocker's review from waiting on the completeness work. One PR with four commit
groups is acceptable if the reviewer prefers it; the group boundaries are the
same either way.

---

## Cross-links

- **Resolves:** #176, #177, #175.
- **Must land consistently with:** #173 (the reflection/mapping split — D1 is
  its clearest application; D4 concludes buffer layouts are a *reflection* fact
  and adds nothing to the mapper), and the issue-166 pair
  (`docs/design/specs/2026-08-01-issue-166-slang-support-design.md`, D5 and D9)
  whose reflection walk and no-`Specialize` decision this arc extends without
  contradicting.
- **Does not touch:** #170 (`specialize()` SIGSEGV on linux-x64) — E11. #169
  (`slang-glslang.dll` shadowing) — E11, low risk, named.
- **Leaves open (issue-166 OPENs, unchanged):** OPEN-5 (two push-constant blocks
  still throw from reflection; D4's push-constant layout is reachable only for
  the single-block case reflection already supports) and OPEN-6 (matrix vertex
  inputs still throw from `MapVertexAttribute` — note that a matrix *buffer
  member* is fine, because a member reports bytes rather than locations).
- **Prevents:** a repeat of the fault-sweep survivor, once the widened-twin test
  exists.

## Follow-ups this spec does not do

- **User attributes on buffer members and types** — the biggest remaining hole
  for a material *editor* (`[Attribute]` carries UI ranges and display names).
  Fully bound already: `spReflectionVariable_GetUserAttributeCount` /
  `GetUserAttribute` / `FindUserAttributeByName` (`SlangApi.cs:548-554`) and
  `spReflectionUserAttribute_GetName` / `GetArgumentCount` / `GetArgumentType` /
  `GetArgumentValue{Int,Float,String}` (`:291-310`). Its own decision: the
  argument model needs designing, not appending.
- **Member default values** (`spReflectionVariable_HasDefaultValue`,
  `GetDefaultValueInt`, `GetDefaultValueFloat`, `SlangApi.cs:557-565`) — the
  bound getters cannot express a vector default, so shipping them would describe
  defaults incorrectly.
- **Array-of-struct member paths** (`Lights[0].Color`) — needs a decision about
  element-relative offsets. `ToJson()` is the escape hatch until then.
- **`SlangImageFormat` → `VkFormat` in `SlangVulkanMapping`** — add when
  something needs it.
- **`spReflectionType_GetFullName`** (`SlangApi.cs:372`) for specialized generic
  type names, if `GetName` proves insufficient for `TypeName`.
- **A typed "fill this buffer by name" writer** — the natural layer above D4.
- **Distinguishing `SLANG_UNKNOWN_SIZE` in a test.** If no fixture can produce
  `-2` for a descriptor count, `SlangDescriptorCountKind.Unknown` ships mapped
  but unexercised, and its XML doc says so.
