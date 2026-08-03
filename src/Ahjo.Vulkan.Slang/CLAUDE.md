# Ahjo.Vulkan.Slang — the compiler wrapper

The idiomatic layer over `Ahjo.Vulkan.Slang.Native`: `SlangCompiler` →
`SlangSession` → `SlangModule` / `SlangEntryPoint` → `SlangProgram` →
`SlangReflection`. Reflection produces `Slang*` description types that describe
what the *shader* declared; `SlangVulkanMapping` is the one place that converts
them into `Ahjo.Vulkan`'s `Pipelines/` types. It references `Ahjo.Vulkan` for
its vocabulary — `ShaderStages`, and those `Pipelines/` types — and nothing in
`src/Ahjo.Vulkan/` references it back.

Design record: `docs/design/specs/2026-08-01-issue-166-slang-support-design.md`
and its paired plan.

## Allocation posture — invariant #3 does not apply here

Compilation is **setup-time**. Nothing in this project is reachable from a frame
loop, and the zero-per-frame-allocation invariant covers `Recording/`, `Sync/`,
`Pools/` and `Memory/` in the wrapper. Allocating a `string[]`, a
`StringBuilder` or a pooled buffer while compiling a shader is correct here.

**Do not add a benchmark for this project**, and do not add a row to
`docs/benchmarks.md`. A benchmark whose subject is not on a hot path teaches a
future reader that it is one.

## The three rules that are easy to get wrong

**1. Diagnostics are read before anything else, on every call that offers
them.** `SlangUtf8.TakeDiagnostics` is the only way a blob is consumed: it reads
and releases in one step and clears the caller's pointer. A failure throws
`SlangCompilationException` carrying the text; a non-empty blob on a
*successful* call is a warning set and reaches `Warnings`. There must be no path
that returns an empty SPIR-V span instead of throwing — that is the acceptance
criterion issue #166 exists for, and `Compile_SyntaxError_ThrowsWithCompilerText`
is the guard.

Two Slang behaviours make this less obvious than it sounds, both measured on
`v2026.14.1`:

- `loadModule*` signals failure by returning `nullptr`, with no result code on
  the call at all. Check the pointer, not just an `rc`.
- `getEntryPointCode` can return `SLANG_OK` *and* an `error[…]` line in the
  diagnostics blob. That is not hypothetical: with `slang-glslang` absent it did
  exactly that for every optimization level above `None`
  (`failed to load downstream compiler 'spirv-opt'`) while handing back valid,
  completely unoptimized SPIR-V. `Ahjo.Vulkan.Slang.Native` ships that library
  now (issue #166, OPEN-1, resolved), so the diagnostic should no longer appear
  — `OptimizationLevels_ReachTheDownstreamCompiler` asserts it does not. Keep
  reading the blob regardless: a result code is not the whole answer here.

**2. `IModule*` is borrowed, `IEntryPoint*` is owned.** `ISession::loadModule*`
hands back a pointer the *session* owns — releasing it without an `addRef` first
corrupts the allocator's heap when the session is torn down (reproduced: `free():
corrupted unsorted chunks`). `SlangModule` therefore `addRef`s on construction
and releases exactly that reference on `Dispose`. `findAndCheckEntryPoint` and
`getDefinedEntryPoint` *do* add a reference per call, so `SlangEntryPoint` owns
its pointer outright. Do not "simplify" these into the same shape.

**3. UTF-8 in two categories, one rule each (invariant #1).** Compile-time
constants — the SPIR-V profile name, the default module name — are
`"…"u8` literals wrapped in `Utf8Name`, which is why
`SlangSessionDescription.SpirvProfile` is a `Utf8Name` and not a `string`.
Runtime-variable strings — paths, module names, entry-point names — go through
`SlangUtf8.ScopedUtf8`, which copies into a `stackalloc` scratch (or a pooled
rental when it does not fit), appends an explicit `0`, and exposes only a
`ReadOnlySpan<byte>` so the pointer can only be produced inside a `fixed` block
that covers the native call. `NativeUtf8Array` exists for the one case that
cannot be expressed with nested `fixed` statements: the session's search-path
array.

Never hand Slang a pointer into a `byte[]` that is neither pinned nor
null-terminated.

## Boundaries

- **Nothing here edits `src/Ahjo.Vulkan/`.** `SlangVulkanMapping` maps *onto* the
  existing `Pipelines/` description types; if a step seems to need a new one,
  that is a design question, not an edit. Keep the mapping confined to that one
  file — reflection returning `Slang*` types is what makes the Vulkan
  interpretation replaceable, and scattering `VkDescriptorType` decisions back
  into the reflection walk undoes it.
- **There is one composition path, and it is `SlangProgramBuilder`.** The
  session's convenience `Compile` composes `[module, ep₀, ep₁, …]` through the
  builder (plus any `SlangCompileRequest.TypeConformances`) and carries the
  module's warnings in through the internal `Link(string?)` overload. It used to
  own a second, identical composition; a second implementation of "the component
  order is the layout" can only drift from the first. Keep it that way.
- The integration point with the wrapper is
  `Device.CreateShaderModule(ReadOnlySpan<uint>)` and nothing else. No new
  `Device` overload was added and none should be.
- `SlangProgram` can only be constructed from a successful
  `IComponentType::link`. Composition changes the layout — the same module
  reflected alone and inside a composite reports different sets and bindings —
  so the SPIR-V a caller fetches and the layout a caller reads must come from
  the same linked object. The type system is the cheapest place to enforce that;
  do not add a constructor that takes anything else.
- **Do not add a `Specialize` method.** `IComponentType::specialize` on a
  component whose global scope holds an interface-typed `ParameterBlock`
  segfaults inside Slang's type-legalization pass (reproduced 3/3, spec E14 has
  the stack trace). Interface conformance goes through
  `createTypeConformanceComponentType` instead. An API whose failure mode is
  SIGSEGV cannot ship behind a `try`.

  **This is a decision, not a deferral.** `SlangProgramBuilder` ships
  `AddTypeConformance` and no `Specialize`, and the omission is load-bearing
  rather than an oversight — the reasoning, in full:

  1. `specialize()` and the following `link()` both return **success** on the
     crashing shape. The process dies later, in `getTargetCode` /
     `getEntryPointCode` for the entry point that consumes the block, inside
     `Slang::legalizeTypes`. There is no result code to check and no exception
     to catch, so no amount of wrapper discipline turns it into a diagnostic.
  2. `createTypeConformanceComponentType` was verified on the *same* shader to
     compose, link and emit valid SPIR-V — `Compose_TypeConformance_Links` is
     that test. So the capability is not missing; only the crashing route to it
     is.
  3. The interface-typed `ParameterBlock` case is
     specialization-**invariant** by design (spec E14): its reflected layout is
     byte-identical before and after, because what the block binds is one
     existential value buffer. `specialize()` would buy nothing here even if it
     worked.
  4. `specialize()` *does* work for the `type_param` form, which is a different
     Slang mechanism and genuinely does change the layout. If a later phase
     wants it, it needs spec D9 rule 3's pre-flight guard — walk the
     unspecialized composite's layout and throw `NotSupportedException` when
     any global parameter's type-layout tree contains a
     `SLANG_TYPE_KIND_INTERFACE` node — and not the bare call.

  **The crash is `linux-x64` only.** Probed 3/3 on `v2026.14.1` / win-x64 with
  the identical sequence: `specialize`, `link`, `getEntryPointCode` and
  `getTargetCode` all return `SLANG_OK` with empty diagnostics and the entry
  point emits 1 460 bytes of SPIR-V. That does **not** make `Specialize`
  shippable — this package ships both RIDs, and an API that SIGSEGVs on one of
  them cannot have a per-platform contract — but it is what the upstream bug
  report should say, and it means point 4's guard is required rather than
  conditional. Spec OPEN-4(b); the report is tracked as #170 and its upstream
  number belongs here once filed.

## Reflection — eleven rules Slang does not hand you

Every one of these was measured against `OpDecorate DescriptorSet` / `Binding`
in the SPIR-V Slang emitted, not read off a header, and every one of them looks
like a simplification opportunity to someone who has only read the header. The
tests in `tests/Ahjo.Vulkan.Slang.Tests/SlangReflectionTests.cs` assert against
the emitted SPIR-V for exactly that reason — reflection agreeing with itself
proves nothing.

1. **The set index is an accumulated offset, not a lookup.**
   `setOf(block) = setOf(enclosing scope) + GetOffset(blockVarLayout, SUB_ELEMENT_REGISTER_SPACE)`,
   with `setOf(global scope) = 0`.
   `spReflectionTypeLayout_getSubObjectRangeSpaceOffset` is **the wrong
   function** — it returns `0` for every sub-object range, including blocks that
   demonstrably land in spaces 1 and 2. Do not "fix" the walk back onto it; the
   call site carries a comment saying so.
2. **The global scope does not necessarily own set 0.** A program whose global
   scope declares only `ParameterBlock`s — the natural shape once a material
   system has put everything in a block — puts its first block at set 0. A
   hardcoded "globals own 0, blocks start at 1" is off by one everywhere.
3. **A `ParameterBlock` whose element carries ordinary data silently owns
   binding 0.** Slang allocates an implicit uniform buffer there, shifts every
   listed range up by one, and reports no descriptor range for it. Derived from
   `GetSize(elementTypeLayout, UNIFORM) > 0`. **The global scope does not share
   this asymmetry** — its implicit constant buffer *is* listed, so applying the
   rule there double-counts binding 0. Getting this wrong shifts every binding
   in every material.
4. **Set indices can be sparse, and the loop index is not the set number.**
   `[[vk::binding(7, 2)]]` is reported at loop index 1 for set 2; the number is
   `getDescriptorSetSpaceOffset`.

5. **An unbounded array is reported, not refused.** `SLANG_UNBOUNDED_SIZE` (-1)
   and `SLANG_UNKNOWN_SIZE` (-2) become `SlangDescriptorCount`, which has no
   readable number unless the kind is `Fixed`; the capacity decision belongs to
   `SlangVulkanMapping.MapBinding(binding, descriptorCount)`. The index-offset
   and space-offset sentinels still throw from the walk, because those leave no
   binding number to report. `-1` is measured
   (`Reflection_UnboundedArray_ReportsBindingInsteadOfThrowing`); `-2` is mapped
   from the header and untested — the unspecialized `type_param` form, the
   obvious candidate, reports **zero** descriptor sets rather than a sentinel
   count, so there is no fixture for it.

6. **The binding-range pass is how a `(set, slot)` gets back to what declared
   it, and it is route 1 of a measured ladder.**
   `getBindingRangeDescriptorSetIndex` + `getBindingRangeFirstDescriptorRangeIndex`,
   then the *same* `getDescriptorSetSpaceOffset` /
   `getDescriptorSetDescriptorRangeIndexOffset` calls the walk uses, produce keys
   that match the SPIR-V-verified bindings exactly — including the sparse case,
   where `[[vk::binding(7, 2)]]` joins to set 2 rather than to its loop index.
   Unlike `getSubObjectRangeSpaceOffset` (rule 1) these are **not** the wrong
   functions; `Reflection_BindingNames_MatchTheSpirvVariableNames` is the oracle
   and compares each reported name against the module's `OpName` at that very
   `(set, binding)`. The pass is additive: it never modifies the descriptor walk,
   so a key it cannot produce costs a name rather than a binding. Three binding
   types are skipped and each skip is load-bearing — `PARAMETER_BLOCK` (the walk
   recurses into it), `PUSH_CONSTANT` (it joins to slot 0 of the enclosing set,
   which is somebody else's constant buffer), and `EXISTENTIAL_VALUE` (it joins
   to the block's own synthesized binding and has a null leaf variable).

7. **`spReflectionTypeLayout_getBindingRangeImageFormat` is not a total
   function.** Called on the `SLANG_BINDING_TYPE_EXISTENTIAL_VALUE` range that a
   `ParameterBlock<ISurface>`'s element scope reports, it takes the process down
   with `0xC0000005` — no result code, no exception, just a dead test host.
   Every *other* call on that same range returns normally, so nothing about the
   range looks dangerous until this one is made. Do not assume the rest of this
   call family is total either — the member walk checks
   `SLANG_TYPE_KIND_STRUCT` before `GetFieldCount` for the same reason.

   **`SlangReflection.ImageFormatOf` guards it in two layers, and only the
   second one is the crash guard.** The kind test (texture / typed buffer, the
   only declarations `[[vk::image_format]]` applies to) is *narrowing*; behind
   it, a null check on
   `spReflectionTypeLayout_getBindingRangeLeafVariable` refuses the call for
   exactly the condition upstream dereferences. That accessor reads the same
   field and is safe by construction — same prologue, then a plain `convert` —
   and is measured returning null on the crashing range. So widening the kind
   test is no longer fatal, which it was before;
   `ImageFormat_ExistentialRange_IsUnknownRatherThanFatal` pins that by passing
   `SLANG_BINDING_TYPE_TEXTURE` for the existential range on purpose, and
   removing the null check makes it crash the run rather than fail (verified).

   **Root cause, traced (#181).** `slang-reflection-api.cpp`'s
   `getBindingRangeImageFormat` null-checks the type layout and bounds-checks the
   index, then dereferences `BindingRangeInfo::leafVariable` — a raw
   `VarDeclBase*` — with no null check. That field is **null** for an
   `EXISTENTIAL_VALUE` range, which describes a synthesized value buffer rather
   than a declared variable. So **the hazard is a null leaf variable, not the
   binding-type kind as such**: that is the condition to reason about if this
   guard is ever revisited, and it is why a kind-based predicate must stay
   narrow rather than enumerate the kinds someone has happened to measure.
   **Reproduces on win-x64 (`0xC0000005`) *and* linux-x64 (`SIGSEGV`)** — unlike
   #170, this one is not platform-specific, which the unconditional dereference
   predicts. Standalone C repro and the drafted upstream report:
   `docs/upstream/slang-getbindingrangeimageformat-crash.md` and `.cpp`. Tracked
   as #181; the upstream number belongs here once filed.

   **Upstream already fixes this incidentally, in unmerged PR
   [#11344](https://github.com/shader-slang/slang/pull/11344)** — it routes both
   format getters through a helper that null-checks the variable first, which is
   the same guard by the same reasoning. Our null check therefore mirrors the
   fix rather than working around it, but only the null-safety half: #11344 also
   reads a format off the texture *type*, which `v2026.14.1` has no notion of.
   Both layers come out when a Slang carrying that fix is **pinned** — not when
   it merges — and the repro in `docs/upstream/` is how you decide.

8. **Member offsets come from `SLANG_PARAMETER_CATEGORY_UNIFORM`, and the test
   asserts them against `OpMemberDecorate … Offset`.** A default or wrong
   category produces offsets that look plausible and are silently wrong — the
   failure issue #175 exists to prevent, and the one that survived a 97-test
   suite. `BufferLayout_MaterialBlock_OffsetsMatchTheEmittedSpirv` reads the
   emitted module's member decorations and compares; do not change the category
   without moving that assertion with it. `BufferLayout_WideningAMember_Changes…`
   is the other half: two fixtures differing only in `float2` → `float4`, whose
   layouts must differ. It cannot be made green by editing a constant, which is
   the whole point of keeping a near-duplicate shader in the fixture file.

9. **Slang's descriptor-set view loses the space of a global `ConstantBuffer<T>`
   that carries an explicit `[[vk::binding(n, space)]]`.** Measured on
   `v2026.14.1` / win-x64: the range is emitted into the record for space 0 with
   its binding index intact, so both the walk and the binding-range join key it
   to the wrong set — and, when something else already owns that key, silently
   rename it. `CollectSpaceCorrections` repairs the space from the declaring
   field's `spReflectionVariableLayout_GetSpace(field, DESCRIPTOR_TABLE_SLOT)`,
   which agreed with `OpDecorate DescriptorSet` in all 19 shapes probed and was
   a no-op in the 13 Slang already gets right. **The correction is deliberately
   inert whenever Slang agrees with itself**, so it retires when upstream fixes
   the view — do not "simplify" it into an unconditional override, and do not
   call `GetFieldCount` on a scope whose kind is not `SLANG_TYPE_KIND_STRUCT`
   (rule 7's family is not total; a conformance-linked
   `ParameterBlock<ISurface>`'s element scope is `SLANG_TYPE_KIND_INTERFACE`).
   Issue #180.

10. **The global scope is not always a struct.** Measured on `v2026.14.1` /
    win-x64: as soon as a module declares loose ordinary data at file scope
    (`float4 gTint;`), `spReflection_getGlobalParamsTypeLayout` returns a
    **`SLANG_TYPE_KIND_CONSTANT_BUFFER`** wrapper whose element is the real
    struct scope. **The wrapper's descriptor-set records are unusable** — they
    list the element's ranges *plus* the implicit buffer, with no constant offset
    between the two index spaces (`+1` for one record and `+0` for another in the
    same module), and they report the implicit buffer's index offset as `0` where
    SPIR-V decorates `1`. `UnwrapGlobalScope` therefore discards them, walks the
    element, and synthesizes the implicit buffer from
    `spReflection_getGlobalParamsVarLayout`'s `GetSpace`/`GetOffset` under
    `DESCRIPTOR_TABLE_SLOT`, which matched `OpDecorate` in every shape probed.
    `spReflection_getGlobalConstantBufferBinding` does **not** work — it returns
    `0` and is wrong wherever the implicit buffer is not at slot 0. **Test for
    `== CONSTANT_BUFFER`, never for `!= STRUCT`**: rule 7's call family is not
    total and issue #181 is still open, so an unmeasured kind must fall through
    to the ordinary path rather than into a `GetFieldCount`. Widening it to
    `!= STRUCT` leaves the suite green — the narrow test is the one that is
    *safe*, not the one that is *necessary*, and that is exactly why it must not
    be "simplified". Note also that the implicit buffer takes **slot 0 and pushes
    the resources up** unless something is explicitly bound there, which is why
    `ReflectionLooseGlobalsWithExplicitSpace` (where `[[vk::binding(0, 0)]]`
    claims slot 0) is the fixture that can tell a reported offset from a
    hard-coded one. Issue #180.

11. **A zero-length resource array is a real descriptor range with a count of
    literally zero, and the binding number is reserved.** Measured on
    `v2026.14.1` / win-x64: `Texture2D gTex[0];` compiles, and
    `getDescriptorSetDescriptorRangeCount` returns `0` — not a sentinel, so
    `Fixed(0)` is a legitimate report and reflection keeps making it. The slot is
    consumed exactly as a four-element array's would be (delete the declaration
    and every following resource moves down one), and the emitted SPIR-V
    decorates **no variable** at it; nothing can index it (`error[E30029]`
    statically, `error[E99997]` dynamically). It is reachable without anyone
    typing `[0]` — `gMaps[NUM_MAPS]` with `NUM_MAPS = 0` — and a
    struct-of-resources array yields one zero-count range **per member**.
    **So it is not a Vulkan descriptor binding, and `SlangVulkanMapping` says so
    at every entry point**: `MapBindings` omits it (the result is therefore not
    positionally aligned with `reflection.Bindings(i)` — key on `Slot`),
    `MapBinding` refuses it, and `MapBinding(binding, count)` refuses to size it.
    The three must keep agreeing: emitting `descriptorCount = 0` and omitting the
    binding are both legal Vulkan and are **not compatible with each other** — a
    set allocated from one layout fails
    `VUID-vkCmdBindDescriptorSets-pDescriptorSets-00358` against a pipeline
    layout built from the other (measured, NVIDIA + validation layer). Emitting
    `descriptorCount = 0` is *not* an option here anyway:
    `DescriptorBinding.Count == 0` is `Ahjo.Vulkan`'s sentinel for a zeroed span
    element and is rewritten to `1` (`src/Ahjo.Vulkan/Lifecycle/Device.cs`, issue
    #119) — **do not "fix" that guard**, and do not move this rule into
    `SlangReflection`: refusing a program there would make two perfectly usable
    bindings unreachable because of one nobody can reference, which is the
    failure #176 removed. Issue #183.
    A set whose *every* binding is zero-count therefore maps to an **empty**
    `DescriptorBinding[]`, which `Device.CreateDescriptorSetLayout` accepts as a
    layout with zero bindings (issue #191) — the three entry points still agree:
    `MapBindings` omits, `MapBinding` refuses, `MapBinding(binding, count)`
    refuses.

Two further constraints, both about refusing rather than guessing: more than one
`[[vk::push_constant]]` block throws from **reflection** (it exposes a buffer
*index*, not the byte offset `VkPushConstantRange.Offset` needs), and a
matrix-typed vertex input throws from **`MapVertexAttribute`** (per-location
component count depends on the session's matrix layout mode, and only
column-major was probed). The split is deliberate: reflection can report a
matrix perfectly well, and only the `VkFormat` mapping has to give up on it. Do
not replace either throw with a plausible value — both produce a pipeline that
builds and then mis-binds.

A *third* refusal was considered for #180 and deliberately not added (that
issue's OPEN-1): `Group` does not check for two bindings sharing a `(set, slot)`,
so should one ever survive rule 9's correction it still reaches
`Device.CreateDescriptorSetLayout` — which validates only the
variable-descriptor-count ordering
(VUID-VkDescriptorSetLayoutBindingFlagsCreateInfo-pBindingFlags-03004) and
nothing about slot uniqueness — silently.

Stage attribution is opt-in (`SlangStageAttribution.PerEntryPointUsage`) because
it costs a codegen per entry point. Push-constant stages stay the program union
in **both** modes: `isParameterLocationUsed` reports a push constant as unused
even for a stage whose SPIR-V provably reads it.
