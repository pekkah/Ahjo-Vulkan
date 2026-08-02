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

## Reflection — four rules Slang does not hand you

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

Two further constraints, both about refusing rather than guessing: more than one
`[[vk::push_constant]]` block throws from **reflection** (it exposes a buffer
*index*, not the byte offset `VkPushConstantRange.Offset` needs), and a
matrix-typed vertex input throws from **`MapVertexAttribute`** (per-location
component count depends on the session's matrix layout mode, and only
column-major was probed). The split is deliberate: reflection can report a
matrix perfectly well, and only the `VkFormat` mapping has to give up on it. Do
not replace either throw with a plausible value — both produce a pipeline that
builds and then mis-binds.

Stage attribution is opt-in (`SlangStageAttribution.PerEntryPointUsage`) because
it costs a codegen per entry point. Push-constant stages stay the program union
in **both** modes: `isParameterLocationUsed` reports a push constant as unused
even for a stage whose SPIR-V provably reads it.

### Known gap: no zero-binding descriptor set layout

A reflected program may leave a set index unused (rule 4). Vulkan fills such a
hole with a descriptor set layout that has zero bindings, but
`Device.CreateDescriptorSetLayout` rejects an empty `Bindings` span
(`src/Ahjo.Vulkan/Lifecycle/Device.cs`), so a sparse program cannot currently be
turned into a complete `PipelineLayout` through this API.

`SlangReflection.SetLayoutSlotCount` and `TryGetSet` make the hole visible and
the XML doc names it. **Do not paper over it here** — synthesizing a stand-in
binding would put a descriptor in a layout the shader never declared. Closing it
is a decision about `Ahjo.Vulkan`'s own validity guard, not an edit this project
gets to make.
