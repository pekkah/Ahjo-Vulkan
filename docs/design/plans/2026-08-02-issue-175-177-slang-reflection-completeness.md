Paired with [../specs/2026-08-02-issue-175-177-slang-reflection-completeness-design.md](../specs/2026-08-02-issue-175-177-slang-reflection-completeness-design.md).

# Plan — Slang reflection completeness (#176, #177, #175)

Four commit groups: **A = #176** (blocker), **B = #177**, **C = #175's
completeness sweep**, **D = #175's buffer layouts**. Each builds and tests green
on its own before the next starts.

**Recommended as two PRs** — `A + B` (`Closes #176`, `Closes #177`) then `C + D`
(`Closes #175`) — so the blocker's review does not wait on the completeness
work. One PR with four commit groups is fine too; the boundaries are identical.

Nothing here edits `src/*/Generated/`, `native/`, or anything outside
`src/Ahjo.Vulkan.Slang/` and its two test projects. Every native symbol used
already exists in the generated binding — cited per step.

**Breaking changes are sanctioned.** Do not preserve a signature or a field's
meaning for compatibility's sake; the spec's §Compatibility lists what the
release note must say. Update in-repo call sites and move on.

**No benchmark work, on purpose.** `src/Ahjo.Vulkan.Slang/CLAUDE.md` forbids
adding one for this project and forbids a `docs/benchmarks.md` row: nothing here
is on a per-frame path, and a benchmark whose subject is not a hot path teaches
a future reader that it is one. Invariant #3 does not apply. Invariants #2
(Native AOT: no reflection, no dynamic codegen — everything below is direct
calls, arrays and strings) and #5 (`TreatWarningsAsErrors`) do.

---

## Group A — #176: report the sentinel, refuse in the mapper

### A1. New file `src/Ahjo.Vulkan.Slang/SlangDescriptorCount.cs`

Both the enum and the option struct, one file:

```csharp
namespace Ahjo.Vulkan.Slang;

public enum SlangDescriptorCountKind { Fixed, Unbounded, Unknown }

public readonly record struct SlangDescriptorCount
{
    public static SlangDescriptorCount Fixed(uint count);
    public static SlangDescriptorCount Unbounded { get; }
    public static SlangDescriptorCount Unknown { get; }

    public SlangDescriptorCountKind Kind { get; }
    public uint Value { get; }                    // throws when Kind != Fixed
    public bool TryGetValue(out uint count);
    public bool IsUnbounded => Kind == SlangDescriptorCountKind.Unbounded;
}
```

- Store `uint _value` + `SlangDescriptorCountKind Kind`; the factories are the
  only way to construct a non-default value, so no incoherent state exists.
- `default(SlangDescriptorCount)` is `Fixed(0)` — document it as "no descriptors",
  and note that `SlangDescriptorBinding()`'s initializer supplies `Fixed(1)` so
  the valid-by-default rule (#119) still holds for the type callers actually
  construct.
- `Value`'s `InvalidOperationException` message: "This binding's descriptor count
  is {Kind}: Slang reports no count for it. Use TryGetValue, or supply a
  capacity through SlangVulkanMapping.MapBinding(binding, descriptorCount)."
- Enum XML docs name the sentinels and their values: `Unbounded` =
  `SLANG_UNBOUNDED_SIZE` (`slang.h:2361`, reaches C# as `-1`), `Unknown` =
  `SLANG_UNKNOWN_SIZE` (`:2362`, `-2`). If step A6 finds no fixture for
  `Unknown`, add: "No fixture in this repository produces this; it is mapped
  from the documented sentinel value and is not covered by a test."

### A2. `src/Ahjo.Vulkan.Slang/SlangDescriptorBinding.cs`

`Count` changes type: `public SlangDescriptorCount Count { get; init; }`. The
parameterless constructor becomes `Count = SlangDescriptorCount.Fixed(1);`.

Type-level XML doc: "`Count` is an option, not a number. An unbounded (bindless)
array has no descriptor count Slang can state, and no `uint` is safe to put here
— `0` is normalized to `1` by the descriptor-set-layout build path and
`uint.MaxValue` crashes the driver. Read it with `Count.TryGetValue`, or map the
binding with `SlangVulkanMapping.MapBinding(binding, descriptorCount)`."

(`Name`, `ImageFormat` and `IsSpecializable` are added in group C — do not add
them here.)

### A3. `src/Ahjo.Vulkan.Slang/SlangReflection.cs` — classify instead of throwing

Replace the count check at `:322-329`. The surrounding code — the `slot` check
at `:314-320`, the `bindingType` read at `:331-332`, the `pending.Add` at
`:334-341` — is unchanged except that the binding now carries a
`SlangDescriptorCount`:

```
count == -1                  -> SlangDescriptorCount.Unbounded
count == -2                  -> SlangDescriptorCount.Unknown
0 <= count <= uint.MaxValue  -> SlangDescriptorCount.Fixed((uint)count)
otherwise                    -> keep throwing NotSupportedException
```

Surviving throw's message:

> "Descriptor range {r} of descriptor set {vkSet} reports descriptor count
> {count}, which is neither a descriptor count nor one of Slang's documented
> sentinels (`SLANG_UNBOUNDED_SIZE` = -1, `SLANG_UNKNOWN_SIZE` = -2). Casting it
> to a `uint` would hand `vkCreateDescriptorSetLayout` a nonsense
> `descriptorCount`."

Keep the comment block at `:309-313` (it explains the driver crash) and extend
it: the sentinels are now classified rather than refused, and the two remaining
sentinel throws in this file — index offset (`:314-320`) and sub-object space
offset (`:404-410`) — stay all-or-nothing because a program with no binding
number and no set number has no layout to report at all (spec E1).

Update the class-level XML remarks (`:12-33`) with one paragraph: an unbounded
binding is reported, not refused; the capacity decision lives in
`SlangVulkanMapping`.

Step 2's synthesized binding (`:356-371`) becomes
`Count = SlangDescriptorCount.Fixed(1)`. Do not otherwise touch the sub-object
recursion, `BuildPushConstantRanges`, `ApplyStages` or `Group`.

### A4. `src/Ahjo.Vulkan.Slang/SlangVulkanMapping.cs` — the refusal and the capacity

New file `src/Ahjo.Vulkan.Slang/SlangUnboundedCapacity.cs`:

```csharp
/// <summary>Supplies the descriptor capacity for a binding reflection could not size.</summary>
public delegate uint SlangUnboundedCapacity(SlangDescriptorBinding binding);
```

In `SlangVulkanMapping`:

1. `MapBinding(this SlangDescriptorBinding binding)` — signature unchanged
   (`:159-168`). Throws when `binding.Count.Kind != Fixed`:

   > `NotSupportedException` for `Unbounded`: "Descriptor binding {Slot}
   > ('{Name}') is an unbounded (bindless) array: Slang reports no descriptor
   > count for it. Reflection cannot choose your heap's capacity. Call
   > `MapBinding(binding, descriptorCount)` with the capacity you reserve, and
   > set `DescriptorBindingFlags.VariableDescriptorCount` yourself on the one
   > binding of the set whose count actually varies — Vulkan allows it on at
   > most one binding per set."
   >
   > For `Unknown`, the same shape with the first sentence replaced by
   > "…reports a descriptor count that depends on unresolved generic parameters
   > or link-time constants. Reflect a fully specialized program, or call
   > `MapBinding(binding, descriptorCount)`."

   (`{Name}` is empty until group C lands; write the message with it from the
   start and let group C fill it.)

2. New `MapBinding(this SlangDescriptorBinding binding, uint descriptorCount)`:
   - `ArgumentOutOfRangeException.ThrowIfZero(descriptorCount)`.
   - `ArgumentException` when `Count.Kind == Fixed`: "Descriptor binding {Slot}
     already has a descriptor count from reflection ({Count.Value}). Supplying
     one here would override what the shader declares; use `MapBinding()`."
   - Otherwise the same `DescriptorBinding` as (1) with `Count = descriptorCount`
     and `BindingFlags` left at `DescriptorBindingFlags.None` (spec D1/E2).

3. `MapBindings(this ReadOnlySpan<SlangDescriptorBinding>)` — unchanged
   (`:192-198`); it now propagates (1)'s `NotSupportedException`.

4. New `MapBindings(this ReadOnlySpan<SlangDescriptorBinding> bindings,
   SlangUnboundedCapacity capacity)`:
   - `ArgumentNullException.ThrowIfNull(capacity)`.
   - Per element: `Count.Kind == Fixed` → `MapBinding()`; otherwise
     `MapBinding(capacity(binding))`. The resolver is **not** called for `Fixed`
     bindings; the XML doc says so, so a caller's resolver may assume it is only
     asked about bindings it must size.

XML docs on both new members carry the parallel explicitly: "the mapper is where
information the shader does not state gets supplied — the same reason
`MapVertexAttribute` takes `binding` and `offset`."

Update `MapBinding`'s existing body to read `binding.Count.Value` (`:165`).

### A5. Tests — `tests/Ahjo.Vulkan.Slang.Tests/`

Update the two existing `Count` readers first: `SlangReflectionTests.cs:110-120`
(texture array) becomes `Assert.Equal(4u, binding.Count.Value)`.

New fixture in `ShaderFixtures.cs`, `ReflectionBindlessArrays`: three unbounded
arrays in set 0 plus one ordinary set and a push-constant block, so the test can
prove the *rest* of the program survives:

```hlsl
struct Push  { float4 tint; uint index; };
struct Xform { float4x4 mvp; };

[[vk::binding(0, 0)]] Texture2D                gTextures[];
[[vk::binding(1, 0)]] SamplerState             gSamplers[];
[[vk::binding(2, 0)]] StructuredBuffer<float4> gBuffers[];

[[vk::binding(0, 1)]] ConstantBuffer<Xform> gXform;
[[vk::push_constant]] ConstantBuffer<Push>  gPush;

[shader("vertex")]   float4 vertexMain(float3 position : POSITION) : SV_Position { … }
[shader("fragment")] float4 fragmentMain(float2 uv : TEXCOORD0) : SV_Target { … }
```

Every parameter must be *read* by an entry point — the fixture-file rule at
`ShaderFixtures.cs:246-251`; an unread global survives reflection but not
codegen, and `AssertReflectionCoversSpirv` would then assert against an emptied
module. Index the arrays with `gPush.index` so nothing folds away.

Cases in `SlangReflectionTests.cs`:

1. `Reflection_UnboundedArray_ReportsBindingInsteadOfThrowing` — constructing the
   reflection does not throw; set 0 has three bindings; each has
   `Count.Kind == Unbounded` and `Count.IsUnbounded`; `Count.TryGetValue` is
   `false`; `Count.Value` throws `InvalidOperationException`.
2. `Reflection_UnboundedArray_DoesNotHideTheRestOfTheProgram` — the ordinary set
   is present with its `ConstantBuffer` binding, `PushConstantRanges.Length == 1`,
   `VertexAttributes(0)` non-empty. This is the issue's actual complaint.
3. `MapBinding_UnboundedBinding_Throws` — `NotSupportedException`, message
   contains the slot number and `MapBinding`.
4. `MapBinding_WithCapacity_ProducesCountAndNoFlags` — `MapBinding(1024)` gives
   `Count == 1024` and `BindingFlags == DescriptorBindingFlags.None`. Assert the
   flags explicitly; spec E2's argument is that we must *not* set them.
5. `MapBinding_WithCapacity_OnFixedBinding_Throws` — `ArgumentException`.
6. `MapBindings_WithResolver_SizesEachArrayIndependently` — resolver returns
   `slot == 0 ? 512u : slot == 1 ? 16u : 64u`; assert the three counts, and
   assert the resolver was **not** invoked for the fixed bindings (count
   invocations in the lambda).
7. Extend the `Reflection_CoversEverySetAndBinding_TheSpirvDecorates` theory
   (`SlangReflectionTests.cs:54-73`) with the new fixture.

### A6. Sentinel-value verification (measurement, not a guess)

Test A5.1 *is* the verification that `-1` reaches C# and maps to `Unbounded` — if
the fixture produces `-2` or another value, the test fails with the actual value
and A3's mapping is wrong. Fix the mapping to what was measured and record the
measured value in a comment next to it, in this file's existing style.

For `Unknown`: attempt one fixture (a `ParameterBlock<T>` with an unbound generic
type parameter, linked without `specialize`). If it produces `-2` for a
descriptor *count*, add the equivalent of A5.1 for it. If it instead trips the
index-offset or space-offset throw first (`SlangReflection.cs:314-320`,
`:404-410`), that is the expected outcome — do **not** relax those throws to
reach it; delete the fixture and leave `Unknown` documented as untested per A1.

### A7. Docs for group A

- `src/Ahjo.Vulkan.Slang/README.md` — move the unbounded-array case out of "Two
  things reflection refuses rather than guesses" (`:313-328`) into a new
  subsection under "Reflection-driven layouts": "Bindless arrays: reflection
  reports, you supply the capacity", with the `MapBindings(resolver)` snippet and
  the one-variable-count-binding-per-set note.
- `src/Ahjo.Vulkan.Slang/CLAUDE.md` — add a rule: "**An unbounded array is
  reported, not refused.** `SLANG_UNBOUNDED_SIZE` (-1) and `SLANG_UNKNOWN_SIZE`
  (-2) become `SlangDescriptorCount`, which has no readable number unless the
  kind is `Fixed`; the capacity decision belongs to
  `SlangVulkanMapping.MapBinding(binding, descriptorCount)`. The index-offset and
  space-offset sentinels still throw from the walk, because those leave no
  binding number to report."
- Release note bullets per the spec's §Compatibility.

---

## Group B — #177: conformances on the request, and a refusal that explains itself

### B1. New file `src/Ahjo.Vulkan.Slang/SlangTypeConformance.cs`

```csharp
public readonly record struct SlangTypeConformance(string ConcreteType, string InterfaceType);
```

XML remarks: point at `SlangProgramBuilder.AddTypeConformance` for the semantics
(do not restate them), and record why this is a named type rather than a
`(string, string)` tuple — Slang's `conformanceIdOverride`
(`SlangProgramBuilder.cs:247-249`, always `-1` today) is a member this type will
want once someone needs deterministic dispatch IDs, and a `ValueTuple` cannot
grow one. Do **not** expose the override now.

### B2. `src/Ahjo.Vulkan.Slang/SlangCompileRequest.cs`

Add after `EntryPoints`:

```csharp
public IReadOnlyList<SlangTypeConformance>? TypeConformances { get; init; }
```

XML doc: "Implementations to make available to interface-typed parameters.
`null` (the default) is correct for any shader without an `interface`-typed
parameter. Without at least one conformance, a program with a
`ParameterBlock<ISomeInterface>` **links successfully and then fails at
`SlangProgram.Spirv`** with `error[E50100]: no type conformances found`. Names
are resolved when the program is linked, so a misspelling throws
`ArgumentException` from `Compile`."

Extend the type's `<remarks>` (`:7-18`) — the paragraph that sends multi-module
programs to `SlangProgramBuilder` — with a sentence saying conformances are the
one builder feature the request does carry.

### B3. `src/Ahjo.Vulkan.Slang/SlangProgramBuilder.cs` — one internal overload

Add `internal SlangProgram Link(string? carriedWarnings)`; move the current body
of `Link()` into it, threading `carriedWarnings` into `LinkDirect` /
`LinkWithConformances` so it reaches `SlangProgram.JoinDiagnostics` at `:219`
and `:339`. Public `Link()` becomes `=> Link(carriedWarnings: null);`. The
diagnostics join already tolerates nulls (`SlangProgram.cs:219-250`).

### B4. `src/Ahjo.Vulkan.Slang/SlangSession.cs` — route `Compile` through the builder

In `Compile` (`:141-173`), after `SelectEntryPoints`, replace the call to the
private `Link` with:

```csharp
SlangProgramBuilder builder = CreateProgram().Add(module);

for (int i = 0; i < entryPoints.Length; i++) builder.Add(entryPoints[i]);

if (request.TypeConformances is { Count: > 0 } conformances)
{
    for (int i = 0; i < conformances.Count; i++)
        builder.AddTypeConformance(conformances[i].ConcreteType, conformances[i].InterfaceType);
}

return builder.Link(module.Warnings);
```

Then **delete the private `SlangSession.Link` (`:343-385`)** — it is now dead,
and spec E4 establishes it composes the identical list in the identical order.
Component order must stay `[module, ep₀, ep₁, …]`; the existing composition and
reflection tests are the regression check. **If any set or binding number moves,
stop** — that means the two paths were not equivalent and this deduplication is
wrong.

`Compile`'s `<exception>` docs gain `ArgumentException` for an unresolvable
conformance type name (thrown from `SlangProgramBuilder.FindType`, `:277-284`).

### B5. `src/Ahjo.Vulkan.Slang/SlangProgram.cs` — make the late failure actionable

In `Spirv` (`:147-188`), at the `rc < 0 || code == null` throw (`:163-168`), when
`text.Contains("E50100", StringComparison.Ordinal)` use the three-argument
`SlangCompilationException(message, diagnostics, innerException: null)`
(`SlangCompilationException.cs:41-43`) with:

> "Slang compilation failed: error[E50100]: no type conformances found. Entry
> point {index} dispatches through an interface-typed parameter, so at least one
> implementation must be in the linkage. Add one with
> `SlangCompileRequest.TypeConformances` or
> `SlangProgramBuilder.AddTypeConformance(concreteType, interfaceType)`. This
> shape links successfully — the failure can only appear here."

`Diagnostics` stays the verbatim blob (`SlangCompilationException.cs:44-48`);
only `Message` is enriched. Every other failure path keeps the two-argument
constructor.

Add to `SlangProgram`'s type-level `<remarks>` and to `Spirv`'s: "A linked
program is not necessarily a compilable one. A program whose global scope holds
an interface-typed parameter links, reflects and reports its entry points
correctly, and refuses only here."

### B6. Measurement — does `getSpecializationParamCount()` discriminate?

Print `program.LinkedComponent->getSpecializationParamCount()`
(`Generated/IComponentType.cs:54`) for four shapes:

| # | shape | fixture |
|---|---|---|
| 1 | concrete control | `ReflectionTwoBlocks` |
| 2 | existential, no conformance | `InterfaceSurfaceModule` (`ShaderFixtures.cs:201-226`) |
| 3 | existential, with conformance | same, `.AddTypeConformance("Glossy", "ISurface")` |
| 4 | interface declared, dispatched statically | new one-off fixture: `ISurface` + `Glossy`, entry point calls `Glossy.shade` directly, no `ParameterBlock<ISurface>` |

- **Row 2 `> 0` and rows 1 and 4 `== 0`:** ship
  `public int SpecializationParameterCount { get; }` on `SlangProgram`, read once
  in the constructor beside `ReadEntryPoints`, with the four measured values in
  its XML doc and an explicit sentence that it does **not** predict whether code
  generation will succeed (row 3 shows a conformance does not consume it). Add a
  test asserting rows 1, 2 and 4.
- **Otherwise:** do not ship it. Record the measured values in the PR
  description and as a finding in the spec's E5.

**OPEN:** if the numbers land in neither branch cleanly — e.g. row 2 `> 0` but so
is row 1 — stop and report them rather than picking an interpretation.

### B7. Tests for group B

Alongside the existing `Compose_TypeConformance_Links`:

1. `Compile_WithTypeConformance_ProducesSpirv` — `session.Compile(new
   SlangCompileRequest { Source = InterfaceSurfaceModule, ModuleName = "surface",
   TypeConformances = [new("Glossy", "ISurface")] })`, then `Spirv(0)` starts
   with `ShaderFixtures.SpirvMagic` and is non-empty. This is the capability the
   issue says is unreachable today.
2. `Compile_WithoutTypeConformance_LinksThenFailsAtSpirv` — the reporter's own
   assertion, now ours: `EntryPointCount == 1`, reflection succeeds, `Spirv(0)`
   throws `SlangCompilationException` whose `Message` contains both `E50100`
   **and** `AddTypeConformance`, and whose `Diagnostics` contains `E50100`. The
   message assertion is the regression guard on B5.
3. `Compile_WithUnknownConformanceType_ThrowsArgumentException` — misspelled
   concrete type; message names it.
4. `Compile_WithTwoConformances_LinksBoth` — `Glossy` and `Matte`
   (`ShaderFixtures.cs:207-217`) in one request.
5. Regression pin for B4: `Compile_EntryPointOrder_IsRequestOrder` plus a
   reflected-set assertion, added **before** the change so there is a
   before/after.

### B8. Docs for group B

- `README.md:108-134` — `TypeConformances` becomes the first example in
  "Specializing an interface-typed `ParameterBlock`", the builder form stays
  below it for multi-module programs, and "at `Spirv(...)`, not at `Link()`" gets
  its own paragraph explaining that this is deliberate: reflection stays
  available for a program that cannot yet generate code.
- `src/Ahjo.Vulkan.Slang/CLAUDE.md` — under "Boundaries": the session's
  convenience `Compile` composes through `SlangProgramBuilder`; there is one
  composition path and it must stay that way.

---

## Group C — #175, part one: the completeness sweep and the binding-range pass

This group introduces the pass group D depends on, and consumes it with simple,
independently verifiable facts. **If the pass is broken, it is discovered here.**

### C1. The binding-range pass — `src/Ahjo.Vulkan.Slang/SlangReflection.cs`

A new private method, called from `Walk` for each scope (global and each
`ParameterBlock` element), which does **not** modify the existing descriptor
walk:

```csharp
private static void CollectBindingRangeFacts(
    SlangReflectionTypeLayout* structTypeLayout,
    uint absoluteSet,
    Dictionary<(uint Set, uint Slot), BindingFacts> into)
```

where `BindingFacts` is a private record struct
`(string Name, SlangImageFormat ImageFormat, bool IsSpecializable, nint LeafTypeLayout)`.
For each `br` in `0 .. spReflectionTypeLayout_getBindingRangeCount`
(`SlangApi.cs:453`):

- `s = getBindingRangeDescriptorSetIndex(structTypeLayout, br)` (`:485`),
  `r = getBindingRangeFirstDescriptorRangeIndex(structTypeLayout, br)` (`:489`);
  skip when either is negative.
- `set = absoluteSet + getDescriptorSetSpaceOffset(structTypeLayout, s)` and
  `slot = getDescriptorSetDescriptorRangeIndexOffset(structTypeLayout, s, r)` —
  the same two calls the verified walk uses (`SlangReflection.cs:279`, `:306`),
  so the keys are computed exactly the way the bindings were.
- `Name` from `getBindingRangeLeafVariable` (`:470`) → `spReflectionVariable_GetName`
  (`:538`), `""` when null.
- `ImageFormat` from `getBindingRangeImageFormat` (`:473`).
- `IsSpecializable` from `isBindingRangeSpecializable` (`:460`) `!= 0`.
- `LeafTypeLayout` from `getBindingRangeLeafTypeLayout` (`:467`) — unused in
  group C, consumed by D.
- Skip `SLANG_BINDING_TYPE_PARAMETER_BLOCK` ranges (the walk recurses into those)
  and push-constant ranges (handled separately).

`Group` (`SlangReflection.cs:598-648`) then stamps each pending binding from the
dictionary before slicing.

**Measurement, first thing in this group** (spec E8): dump `(br, s, r, set, slot,
name)` for `ReflectionGlobals` and `ReflectionTwoBlocks` and compare against the
bindings the existing walk reports.

- **Route 1 works** (keys line up, names are the declared parameter names):
  proceed as written.
- **Route 1 fails** (e.g. `s`/`r` come back `0` for everything, the way
  `getSubObjectRangeSpaceOffset` does): switch to **route 2** — walk
  `spReflection_GetParameterCount` / `GetParameterByIndex`
  (`SlangReflection.cs:436-444`) and key on
  `spReflectionVariableLayout_GetSpace(param, DESCRIPTOR_TABLE_SLOT)`
  (`SlangApi.cs:585`) + `GetOffset(param, DESCRIPTOR_TABLE_SLOT)`, recursing into
  block elements the same way `Walk` does. Measure it the same way.
- **Both fail:** **OPEN** — stop and report. The last-resort scope (names only
  for push-constant blocks and `ParameterBlock` uniform buffers, no `ImageFormat`,
  no `IsSpecializable`, and group D reduced to E7's two free sources) removes the
  most classic UBO shape from #175 and is a call for a human.

Record which route was used, and the measured evidence, in a comment at the top
of the pass — the same way the `getSubObjectRangeSpaceOffset` warning is
recorded today (`SlangReflection.cs:394-400`).

### C2. `SlangDescriptorBinding` — `Name`, `ImageFormat`, `IsSpecializable`

Add to `src/Ahjo.Vulkan.Slang/SlangDescriptorBinding.cs`:

```csharp
public string Name { get; init; }                  // "" when Slang reports none
public SlangImageFormat ImageFormat { get; init; } // SLANG_IMAGE_FORMAT_UNKNOWN when unspecified
public bool IsSpecializable { get; init; }
```

- The synthesized `ParameterBlock` uniform buffer (`SlangReflection.cs:356-371`)
  takes the **block parameter's** name. `Walk` gains a `string scopeName`
  parameter, supplied at the recursion site (`:412-417`) from
  `NameOf(offsetVariable)` (`:391-392`) and `""` for the global-scope entry
  (`:75-80`). Group D needs the same thread.
- `ImageFormat` XML doc: reflection reports Slang's own enum; there is
  deliberately no `VkFormat` mapping yet, because no Vulkan call at
  layout-creation time needs one.
- `IsSpecializable` ships only if C6.3 shows it discriminates. If it is `false`
  everywhere including the existential fixture, **drop the member** and record
  the measurement — a constant field reads as information and is not.

Fill `{Name}` into A4's `MapBinding` exception messages now that it exists.

### C3. `SlangPushConstantRange.Name`

`src/Ahjo.Vulkan.Slang/SlangPushConstantRange.cs` gains
`public string Name { get; init; }`. `BuildPushConstantRanges` already computes
it as `firstName` (`SlangReflection.cs:458`) and discards it at `:506` — pass it
through.

### C4. Vertex attribute semantics

`src/Ahjo.Vulkan.Slang/SlangVertexAttributeDescription.cs` gains:

```csharp
public string SemanticName { get; init; }   // "POSITION", "TEXCOORD" — "" when none
public uint SemanticIndex { get; init; }
```

Filled in `BuildVertexAttribute` (`SlangReflection.cs:722-759`) — but the
semantic lives on the *variable layout*, not the type layout, so
`BuildVertexAttribute` needs the `SlangReflectionVariableLayout*` passed in
alongside the type layout it already takes; both call sites (`:700`, `:705`)
already have it. Calls:
`spReflectionVariableLayout_GetSemanticName` (`SlangApi.cs:592`) and
`GetSemanticIndex` (`:596`) — both already in the drift guard's list
(`SlangExportDriftTests.cs:103-104`).

XML doc: this is what a vertex-buffer binder matches on; `Name` is the field's
name in the shader struct and is not what the application's mesh format keys by.

### C5. `SlangEntryPointInfo` — thread group size, and the name override question

`src/Ahjo.Vulkan.Slang/SlangEntryPointInfo.cs` becomes:

```csharp
public readonly record struct SlangEntryPointInfo(
    string Name,
    ShaderStages Stage,
    uint ThreadGroupSizeX,
    uint ThreadGroupSizeY,
    uint ThreadGroupSizeZ);
```

Filled from `spReflectionEntryPoint_getComputeThreadGroupSize(entryPoint, 3,
outSizeAlongAxis)` (`SlangApi.cs:766`) in both places entry points are read
(`SlangReflection.cs:58-67` and `SlangProgram.ReadEntryPoints`, `:252-276`) —
or better, extract the shared read into one internal helper so there is one code
path. Document that non-compute stages report `1,1,1` (verify; if Slang reports
`0,0,0` for them, document what it actually returns rather than normalizing).

**Measurement for the name override.** Extend `SpirvDecorations` with an
`OpEntryPoint` (opcode 15) name reader and assert, for the existing fixtures,
that `EntryPoint(i).Name` equals the emitted entry-point name. Then check
whether `spReflectionEntryPoint_getNameOverride` (`SlangApi.cs:750`) ever
differs from `getName` for those fixtures.

- **It can differ:** add `public string? NameOverride { get; init; }` with an XML
  doc saying that when non-null it is the name emitted into the SPIR-V and
  therefore the one `VkPipelineShaderStageCreateInfo.pName` must use, and add a
  fixture that produces one.
- **It never differs in any fixture:** do not add the member; keep the
  `OpEntryPoint`-name test (it is worth having regardless) and record the finding.

### C6. Tests for group C

1. `Reflection_BindingNames_MatchTheSpirvVariableNames` — for `ReflectionGlobals`
   and `ReflectionTwoBlocks`, every reported `(set, slot)` binding's `Name`
   matches the `OpName` the existing `SpirvDecorations.ReadDescriptorBindings`
   already returns for that `(set, binding)` (`SpirvDecorations.cs:44-85`). This
   is the SPIR-V oracle for C1's join, and the cheapest possible test of it.
2. `Reflection_ParameterBlockUniformBuffer_TakesTheBlockName` — for
   `ReflectionBlockOrdinaryData` (`ShaderFixtures.cs:337-362`), the synthesized
   binding 0 of `gWith`'s set is named `gWith`.
3. `Reflection_InterfaceTypedBlock_IsSpecializable` — `InterfaceSurfaceModule`
   linked with a conformance: the block's binding reports `IsSpecializable ==
   true` while `ReflectionTwoBlocks`' bindings report `false`. **This is the
   measurement gate for C2's third member.**
4. `Reflection_StorageImageFormat_IsReported` — a fixture with
   `[[vk::image_format("rgba8")]] RWTexture2D<float4> gOut;` reports the matching
   `SlangImageFormat`; an unannotated one reports
   `SLANG_IMAGE_FORMAT_UNKNOWN`.
5. `Reflection_PushConstantRange_HasBlockName` — `ReflectionGlobals` reports
   `Name == "gPush"`.
6. `Reflection_VertexAttributes_CarrySemantics` — `ReflectionSystemValueInputs`
   (`ShaderFixtures.cs:453-466`) reports `POSITION`/0, `TEXCOORD`/0, `TANGENT`/0
   and no entry for the two system values.
7. `Reflection_ComputeEntryPoint_ReportsThreadGroupSize` — a new
   `[shader("compute")] [numthreads(8,4,1)]` fixture reports `8,4,1`; a vertex
   entry point reports whatever C5's measurement found, asserted explicitly.
8. `Reflection_EntryPointName_MatchesOpEntryPoint` — per C5.

### C7. Drift guard

Add to `RequiredExports` (`SlangExportDriftTests.cs:40-115`), each under a
comment naming its caller, and **only the ones the final implementation calls**:

```
"spReflectionTypeLayout_getBindingRangeCount",
"spReflectionTypeLayout_getBindingRangeDescriptorSetIndex",
"spReflectionTypeLayout_getBindingRangeFirstDescriptorRangeIndex",
"spReflectionTypeLayout_getBindingRangeLeafVariable",
"spReflectionTypeLayout_getBindingRangeImageFormat",
"spReflectionTypeLayout_isBindingRangeSpecializable",
"spReflectionEntryPoint_getComputeThreadGroupSize",
"spReflectionEntryPoint_getNameOverride",          // only if C5 ships it
```

(If route 2 was used, swap the first three for
`spReflectionVariableLayout_GetSpace`.)

### C8. Docs for group C

- `README.md`, "Reflection-driven layouts" — one short subsection: what each
  reported name is (binding name, push-constant block name, vertex semantic) and
  which one a caller should key on.
- `src/Ahjo.Vulkan.Slang/CLAUDE.md` — record the measured route for the
  binding-range join next to the existing `getSubObjectRangeSpaceOffset` warning,
  in the same "this is the function that works, that one does not" form.

---

## Group D — #175, part two: buffer member layouts and `ToJson()`

### D1. New file `src/Ahjo.Vulkan.Slang/SlangBufferMember.cs`

The full member set from the spec's D4 — `Name`, `ParentIndex`, `Offset`,
`Size`, `Stride`, `Alignment`, `IsUnsized`, `TypeName`, `Kind`, `ScalarType`,
`ComponentCount`, `RowCount`, `ColumnCount`, `MatrixLayout`, `MatrixStride`,
`ElementCount`, `ElementStride`.

XML docs must state, per member: `Name` is the dotted path from the buffer root
(`"Params.UvScale"`); `Offset`/`Size`/`Stride`/`Alignment` are **bytes** from the
`SLANG_PARAMETER_CATEGORY_UNIFORM` category, i.e. the layout Slang baked into the
emitted SPIR-V for this program's target; `ParentIndex` is an index into the
owning `SlangBufferLayout.Members`, `-1` at the root; `IsUnsized` means Slang
returned a size or element-count sentinel (`slang.h:2361-2362`) and `Size` /
`ElementCount` are `0`. Note the shared vocabulary with
`SlangVertexAttributeDescription` (`SlangVertexAttributeDescription.cs:8-18`).

### D2. New file `src/Ahjo.Vulkan.Slang/SlangBufferLayout.cs`

```csharp
public sealed class SlangBufferLayout
{
    internal SlangBufferLayout(string name, uint size, SlangBufferMember[] members);

    public string Name { get; }
    public uint Size { get; }
    public ReadOnlySpan<SlangBufferMember> Members { get; }
    public bool TryGetMember(string path, out SlangBufferMember member);   // ordinal, linear
}
```

`TryGetMember` is a linear `StringComparison.Ordinal` scan — setup-time, member
counts are single to low double digits, the same reasoning as
`SlangReflection.TryGetSet` (`SlangReflection.cs:182`).

Type-level remarks state the four rules a caller will otherwise get wrong:

1. **Pre-order, flattened, dotted paths, struct nodes retained.** Filter
   `Kind != SLANG_TYPE_KIND_STRUCT` for leaves only.
2. **The list is losslessly a tree.** Parent is `Members[ParentIndex]` (`-1` =
   root); children are the entries whose `ParentIndex` is this one's index, and
   pre-order makes them contiguous and in declaration order. Give the two-line
   reconstruction in the doc — it is what makes one representation sufficient.
3. **Arrays are leaves.** Element `i` is at `Offset + i * ElementStride`;
   per-element paths are not generated. For an array of structs, the element's
   own members are not described — use `ToJson()` until the follow-up lands.
4. **Resources are not members.** A field with zero UNIFORM size occupies no
   bytes and is omitted.

### D3. The member walk — `SlangReflection.cs`

```csharp
private static SlangBufferLayout BuildBufferLayout(
    SlangReflectionTypeLayout* structTypeLayout, string name)
```

- Buffer size: `spReflectionTypeLayout_GetSize(structTypeLayout, UNIFORM)`;
  classify sentinels the way A3 does and throw `NotSupportedException` for a
  value that is neither a sentinel nor `<= uint.MaxValue` — same shape as the
  push-constant size guard (`SlangReflection.cs:496-501`).
- Recursive `AppendMembers(typeLayout, pathPrefix, parentIndex, baseOffset, List<SlangBufferMember>)`:
  - `spReflectionTypeLayout_GetFieldCount` / `_GetFieldByIndex` (`SlangApi.cs:397`,
    `:400`) — the calls the vertex-input walk already makes
    (`SlangReflection.cs:684-688`).
  - `fieldTypeLayout = spReflectionVariableLayout_GetTypeLayout(field)`;
    `size = GetSize(fieldTypeLayout, UNIFORM)`; **skip when `size == 0`** (rule 4);
    set `IsUnsized` and `Size = 0` when it is a sentinel.
  - `offset = baseOffset + (uint)spReflectionVariableLayout_GetOffset(field, UNIFORM)`
    — offsets accumulate exactly as vertex locations do (`:697-698`).
  - `name = pathPrefix.Length == 0 ? NameOf(field) : pathPrefix + "." + NameOf(field)`
    (`NameOf` exists, `:772-776`).
  - `Stride` from `GetStride(fieldTypeLayout, UNIFORM)` (`SlangApi.cs:389`);
    `Alignment` from `getAlignment(fieldTypeLayout, UNIFORM)` (`:393`);
    `TypeName` from `spReflectionType_GetName(GetType(fieldTypeLayout))` (`:368`).
  - Kind/scalar/component/row/column exactly as `BuildVertexAttribute` derives
    them (`:722-759`).
  - Matrices: `MatrixLayout` from `GetMatrixLayoutMode(fieldTypeLayout)` (`:433`).
    **`MatrixStride` measurement:** check whether
    `GetElementTypeLayout(matrixTypeLayout)` yields the row/column vector's
    layout; if it does, `MatrixStride = GetStride(that, UNIFORM)`; if it does not,
    leave `0` and say so in the XML doc. **Do not derive it from
    `Size / RowCount`.**
  - Arrays: `ElementCount` from `spReflectionType_GetElementCount` (`:337`,
    sentinel → `IsUnsized`, `0`), `ElementStride` from
    `GetElementStride(fieldTypeLayout, UNIFORM)` (`:411`). Do **not** recurse.
  - Emit the member, then recurse **only** for `Kind == SLANG_TYPE_KIND_STRUCT`,
    passing this member's index as `parentIndex`, its name as the prefix and its
    offset as the new base.
- `const int MaxMemberDepth = 16` with a `NotSupportedException` naming the path,
  so a pathological layout cannot stack-overflow.

Storage: a `(uint Set, uint Slot)[] _bufferLayoutKeys` + `SlangBufferLayout[]
_bufferLayouts` + `SlangBufferLayout? _pushConstantLayout`, all built eagerly in
the constructor.

```csharp
public bool TryGetBufferLayout(uint set, uint slot, [NotNullWhen(true)] out SlangBufferLayout? layout);
public bool TryGetPushConstantLayout([NotNullWhen(true)] out SlangBufferLayout? layout);
```

Linear lookup; `[NotNullWhen(true)]` keeps nullable analysis green under
`TreatWarningsAsErrors`.

### D4. Wiring the three sources

- **(a) Push-constant block.** In `BuildPushConstantRanges`, where `found` and
  `firstName` are resolved and the element type layout is already taken
  (`:492-494`), build
  `BuildBufferLayout(GetElementTypeLayout(found), firstName)`. Do not re-walk the
  parameter list.
- **(b) `ParameterBlock<T>` implicit uniform buffer.** In step 2
  (`:356-371`), build `BuildBufferLayout(structTypeLayout, scopeName)` keyed
  `(absoluteSet, 0)` — `scopeName` is the parameter C2 already threads through
  `Walk`.
- **(c) Everything else.** From group C's `BindingFacts.LeafTypeLayout`: for each
  entry whose `GetSize(GetElementTypeLayout(leaf), UNIFORM) > 0`, build a layout
  from that element type layout, keyed by the same `(set, slot)` the facts were
  keyed by. No new native calls — group C already proved the join.

### D5. `ToJson()`

`SlangReflection` gains `private readonly SlangProgram _program;` (set in the
constructor) and:

```csharp
public string ToJson();
```

Lazy + cached in a `private string? _json`. Implementation:
`GetLayout(_program.LinkedComponent)` — reusing the existing private helper
(`:761-770`), which throws `ObjectDisposedException` through `LinkedComponent`
if the program is gone — then `spReflection_ToJson(layout, request: null, &blob)`
(`SlangApi.cs:800`; the header's own shim passes `nullptr`, `slang.h:3833-3835`),
`SlangUtf8.ToString` over the buffer, `release()` the blob. A negative result or
a null blob throws `SlangCompilationException("spReflection_ToJson", "")`.

Storing the program changes **no ownership or disposal semantics** — the
reflection does not own or dispose it; say so in the field's comment so a future
reader does not "fix" it.

XML doc: "Slang's own JSON dump of this program's layout. **For diagnostics.**
The schema is Slang's, is not stable across versions, and is not something this
package parses or promises. Use the typed surface for anything a program depends
on; this is the escape hatch for what the typed surface does not cover yet — for
example the members of an array-of-struct element."

If the call fails at runtime, that is a **finding to report**, not a reason to
have skipped it: record the result code and the diagnostics blob in the PR
description before deciding anything.

### D6. Tests for group D

New fixtures in `ShaderFixtures.cs`, both entry-point-read like every other one:

- `ReflectionMaterialBlock` — the issue's own shape:
  ```hlsl
  struct MaterialParams { float3 BaseColor; float Roughness; float Metallic; float2 UvScale; uint Flags; };
  struct MaterialBlock  { MaterialParams Params; float4x4 Transform; Texture2D<float4> BaseColorMap; SamplerState Sampler; };
  ParameterBlock<MaterialBlock> gMaterial;
  ```
  (the `float4x4` is there so the matrix-layout and matrix-stride members have a
  subject).
- `ReflectionMaterialBlockWidened` — byte-identical except `float4 UvScale`. Its
  doc comment says what it is for: the fault-sweep mutation from issue #175, kept
  as a fixture so the mutation cannot become unobservable again.

Extend `SpirvDecorations.cs` with a member-offset reader in the existing style
(walk by word count, pick opcodes):

```csharp
public static List<(string StructName, string MemberName, uint Index, uint Offset)>
    ReadMemberOffsets(ReadOnlySpan<uint> words);
```

Opcodes `OpMemberName` = 6, `OpMemberDecorate` = 72, decoration `Offset` = 35;
reuse `OpName` = 5 for the struct's own name. Match by name when `OpMemberName`
is present. **Pre-committed fallback if Slang emits none for this fixture:**
return `MemberName = ""` and assert the ordered *offset sequence* of the struct
whose member count matches the reflected leaf count — the oracle is still the
SPIR-V, which is the point. Note which applied in a comment.

Cases:

1. `BufferLayout_MaterialBlock_OffsetsMatchTheEmittedSpirv` — the load-bearing
   one. Reflected leaf members of `gMaterial`'s set, slot 0, versus
   `OpMemberDecorate … Offset`, in order. A wrong parameter category fails here.
2. `BufferLayout_MaterialBlock_HasGoldenSizeAndOffsets` — literal expected values
   for `Size` and each leaf. **Record them from the first green run rather than
   computing them by hand**, with the Slang version in a comment, in the style of
   `ShaderFixtures.cs:84-90`.
3. `BufferLayout_WideningAMember_ChangesSizeAndSubsequentOffsets` — compile both
   material fixtures; assert the two `Size`s differ **and** that `Params.Flags`'
   offset differs. The fault-sweep survivor turned into a test; it cannot be made
   green by editing a constant.
4. `BufferLayout_KeysMatchTheReportedBindings` — for every populated set and
   binding, a successful `TryGetBufferLayout(set, slot)` corresponds to a binding
   the descriptor walk reported with a buffer-ish `SlangBindingType`; and
   `ReflectionGlobals`' `ConstantBuffer<Xform>` resolves to a layout with an
   `mvp` member. This is D4(c)'s check.
5. `BufferLayout_ResourceFieldsAreNotMembers` — `BaseColorMap` and `Sampler` are
   absent.
6. `BufferLayout_NestedStruct_IsLosslesslyATree` — `Params` present with
   `Kind == STRUCT`; its five fields immediately follow, all named `Params.*`,
   all with `ParentIndex` equal to `Params`' index; `Params.ParentIndex == -1`;
   `TryGetMember("Params.UvScale", out _)` is `true` and
   `TryGetMember("UvScale", out _)` is `false`. Reconstruct the tree from
   `ParentIndex` in the test and assert it matches the declared nesting — the
   spec's sufficiency claim, asserted rather than argued.
7. `BufferLayout_Matrix_ReportsLayoutMode` — `Transform` reports
   `SLANG_MATRIX_LAYOUT_ROW_MAJOR` or `COLUMN_MAJOR` (assert whichever was
   measured, with the session's default named in a comment), `RowCount == 4`,
   `ColumnCount == 4`, and `MatrixStride` per D3's measurement.
8. `BufferLayout_PushConstantBlock_HasMembers` — `ReflectionGlobals`:
   `TryGetPushConstantLayout` yields `Size == 16`, one member `tint` at offset 0,
   size 16, `Kind == VECTOR`, `ComponentCount == 4`. (16 is the measured value
   from the issue-166 spec's E3 probe.)
9. `BufferLayout_StrideAndAlignment_AreReported` — assert `Alignment` on the
   `float4x4` and on a scalar; golden values recorded from the run.
10. `Reflection_ToJson_ContainsDeclaredParameters` — non-empty, contains
    `"gMaterial"`; and a second assertion that calling it twice returns the same
    instance (the cache).
11. Add both new fixtures to the
    `Reflection_CoversEverySetAndBinding_TheSpirvDecorates` theory
    (`SlangReflectionTests.cs:54-73`).

### D7. Drift guard

Add, each with a comment naming its caller:

```
"spReflectionTypeLayout_GetStride",
"spReflectionTypeLayout_GetElementStride",
"spReflectionTypeLayout_GetMatrixLayoutMode",
"spReflectionType_GetName",
"spReflection_ToJson",
```

(`spReflectionTypeLayout_getAlignment`, `GetFieldCount`, `GetFieldByIndex`,
`GetElementTypeLayout` and `spReflectionType_GetElementCount` are already
listed.)

### D8. Docs for group D

- `README.md` — new subsection under "Reflection-driven layouts": "What is inside
  a buffer", with a `TryGetBufferLayout` + `TryGetMember` snippet filling a UBO
  by name, the four rules from D2 in one sentence each, and the tree
  reconstruction in two lines. Add `ToJson()` as the documented escape hatch.
  Update "What this package does not do yet" (`:350-360`) with the remaining
  gaps: array-of-struct element paths, user attributes, member defaults.
- `src/Ahjo.Vulkan.Slang/CLAUDE.md` — one rule: "**Member offsets come from
  `SLANG_PARAMETER_CATEGORY_UNIFORM`, and the test asserts them against
  `OpMemberDecorate … Offset`.** A default or wrong category produces offsets
  that look plausible and are silently wrong — the failure issue #175 exists to
  prevent. Do not change the category without moving that assertion with it."

---

## Verification

```bash
dotnet build Ahjo.Vulkan.slnx
dotnet test tests/Ahjo.Vulkan.Slang.Tests/Ahjo.Vulkan.Slang.Tests.csproj
dotnet test tests/Ahjo.Vulkan.Slang.Native.Tests/Ahjo.Vulkan.Slang.Native.Tests.csproj
```

Plus, because public API on the compile path changed and invariant #2 is proven
rather than asserted:

```bash
dotnet publish samples/AotSmoke/AotSmoke.csproj -c Release -r win-x64 -p:IlcUseEnvironmentalTools=true
```

`samples/AotSmoke/Program.cs:67-73` should root the new surface so ILC sees it —
add one line printing `reflection.TryGetPushConstantLayout(out _)` and one
reading a binding's `Name`. Keep it to two lines; the sample's job is the
publish, not the demo.

Reviewers before the PR: `vulkan-validation-reviewer` (group A touches
`VkDescriptorSetLayoutBinding.descriptorCount` and the bindless flags argument)
and `bench-coverage-checker` (expected outcome: nothing to add — this project is
excluded from benchmarks by `src/Ahjo.Vulkan.Slang/CLAUDE.md`; say so in the PR
rather than leaving it unanswered).

---

## OPEN items — stop and ask

- **OPEN-1 — the binding-range join, if both routes fail** (C1). Route 1
  (binding ranges) then route 2 (the global parameter list) are both measured
  before anything is dropped. If neither produces keys that line up with the
  SPIR-V-verified bindings, the remaining scope — no binding names, no image
  formats, no specializability, and group D reduced to push-constant blocks and
  `ParameterBlock` uniform buffers — removes the most classic UBO shape from
  #175. That is a scope call for a human, not an implementer decision.
- **OPEN-2 — `SpecializationParameterCount`, on an ambiguous measurement** (B6).
  Ship it on a clean discrimination; report the numbers rather than interpreting
  them if it is not clean.
- **OPEN-3 — `Compile`'s reroute changing a layout** (B4). The deduplication is
  justified by the two paths composing an identical component list. If any
  existing test's set or binding number moves, the premise is wrong: stop, do not
  update the expectations.

Measured-and-decided, **not** open — the implementer proceeds without asking:
`SlangDescriptorCountKind.Unknown` shipping untested if no fixture produces `-2`
(A6); `MatrixStride` shipping as `0` if `GetElementTypeLayout` on a matrix does
not yield the row/column layout (D3); `IsSpecializable` and `NameOverride` being
dropped if they are constant across every fixture (C2, C5); `ToJson()` failing at
runtime being written up as a finding (D5).
