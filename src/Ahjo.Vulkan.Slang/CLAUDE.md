# Ahjo.Vulkan.Slang — the compiler wrapper

The idiomatic layer over `Ahjo.Vulkan.Slang.Native`: `SlangCompiler` →
`SlangSession` → `SlangModule` / `SlangEntryPoint` → `SlangProgram`. It
references `Ahjo.Vulkan` for its vocabulary (`ShaderStages` today, the
`Pipelines/` description types when reflection lands) and nothing in
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

- **Nothing here edits `src/Ahjo.Vulkan/`.** Reflection maps *onto* the existing
  `Pipelines/` description types; if a step seems to need a new one, that is a
  design question, not an edit.
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

  Measured on `v2026.14.1` / linux-x64 only; `win-x64` has no equivalent probe.
