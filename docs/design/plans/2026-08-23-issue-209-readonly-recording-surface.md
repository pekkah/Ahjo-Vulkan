# A `readonly` recording surface — implementation plan

Paired with [../specs/2026-08-23-issue-209-readonly-recording-surface-design.md](../specs/2026-08-23-issue-209-readonly-recording-surface-design.md). Issue [#209](https://github.com/pekkah/Ahjo-Vulkan/issues/209).

No public signature changes. No `Generated/` changes. One `src/` file (`Recording/CommandRecorder.cs`), four test files (`ScopedSpanProbe`, `CommandRecorderTests`, `MeshShaderTests`, `WindowedValidationTests`), two benchmark files, seven samples, two docs.

Branch note: the working branch is named `issue-209-beginrendering-by-value`. The decision is *not* by value — rename the branch to `issue-209-readonly-recording-surface` before the first commit, or keep it and say so in the PR body.

## 1. `src/Ahjo.Vulkan/Recording/CommandRecorder.cs` — mark the non-mutating surface `readonly`

Add the `readonly` modifier to **every instance member except** the constructor (`:40`), `End()` (`:75`) and `Dispose()` (`:97`). Nothing else in the file mutates `_ended` or `_retired` — grep confirms assignments only at `:44, :45, :79, :105, :111`.

Concretely, in declaration order:

**Properties** — these must be done, or every `readonly` method that reads them fails the build with CS8656 under `TreatWarningsAsErrors`:

```csharp
public readonly bool IsNull => Handle == null;
private readonly ref readonly DeviceFunctionTable Fns => ref _pool.Device.Functions;
public readonly nint RawHandle => (nint)Handle;
```

Note the doubled `readonly` on `Fns` — the first is the member modifier, the second is part of `ref readonly` return. That is the correct spelling, not a typo to "fix".

**Instance methods**, all of which become `public readonly void …` / `private readonly void …`:

`BeginLabel` (:126), `EndLabel` (:151), `InsertLabel` (:165), `LabelScope` (:194), `SetViewport` (:209), `SetScissor` (:215), both `BindPipeline` overloads (:223, :226), `BindDescriptorSets` (:229), `PushConstants<T>` (:297), `BindVertexBuffers` (:355), `BindIndexBuffer` (:394), `PushDescriptors<T>` (:405), `PushDescriptorSet` (:433), `Draw` (:525), `DrawIndexed` (:528), `DrawIndirect` (:543), `DrawIndirectCount` (:561), `DrawIndexedIndirect` (:579), `DrawIndexedIndirectCount` (:599), `DrawMeshTasks` (:638), `DrawMeshTasksIndirect` (:678), `DrawMeshTasksIndirectCount` (:721), `Dispatch` (:734), `DispatchIndirect` (:742), all three `PipelineBarrier` overloads (:764, :778, :782), `SetEvent` (:824), `WaitEvent` (:860), `ResetEvent` (:889), `RecordDependency` (:915, private), `ResetQueryPool` (:1013), `WriteTimestamp` (:1057), `BuildAccelerationStructures` (:1173), `WriteAccelerationStructuresProperties` (:1482), `CopyAccelerationStructure` (:1615), both `CopyBuffer` overloads (:1650, :1680), `CopyBufferToImage` (:1705), `CopyImageToBuffer` (:1739), `CopyImage` (:1773), `GenerateMips` (:1851), `BlitImage` (:2047), `FillBuffer` (:2089), both `ClearColorImage` overloads (:2098, :2111), both `ClearDepthStencilImage` overloads (:2122, :2143), `BeginRendering` (:2182), `EndRendering` (:2223).

`static` members (`AssertSetsMatchLayout`, `AssertPushRangeFits`, `ThrowPushDescriptorUnsupported`, `ThrowMeshShaderUnsupported`, `ThrowAccelerationStructureUnsupported`, `FlushPush`, `RecordBuilds`, `ValidateBuildSlices`, `AssertBuildsValid`, `FlushWriteProperties`, `InferDepthStencilAspect`, `RentForOverflow`) take no `readonly` — it is not legal on `static`.

**Do not change any signature.** `BeginRendering` keeps `in RenderingInfo info`. The `scoped` modifiers PR #208 added all stay exactly as they are.

Update the type's XML doc (`:14-31`) with a short paragraph on why the surface is `readonly` — one sentence naming the mechanism (a writable `ref`-to-`ref struct` receiver forces caller-wide safe-context on every ref-struct argument; `ref readonly` does not) and one naming the consequence (a stack-backed `RenderingInfo` reaches `BeginRendering` without the caller writing `scoped`). Reference issue #209.

Also update `BeginRendering`'s own doc (`:2180-2182`, currently undocumented) with a two-line `<remarks>` noting that `ColorAttachments` may be a `stackalloc` or a collection-expression span, and that `RenderingInfo` is consumed synchronously.

**Build after this step alone** (`dotnet build Ahjo.Vulkan.slnx`). Expect zero errors. If CS8656 appears, the cause is a `readonly` member calling a member that was missed above — mark that member, do not suppress.

## 2. `tests/Ahjo.Vulkan.Tests/ScopedSpanProbe.cs` — extend the compile-time guard

The probe is the regression guard for this change. Two edits:

**(a)** Add a `BeginRendering` case to `Probe` (the existing `internal static void Probe(ref CommandRecorder rec, …)`), or as a new sibling `internal static void ProbeRendering(ref CommandRecorder rec, in ImageView view)`:

```csharp
Span<ColorAttachment> color = stackalloc ColorAttachment[1];
var info = new RenderingInfo { LayerCount = 1, ColorAttachments = color };
rec.BeginRendering(in info);
rec.EndRendering();
```

Verified in the spec's repro that this shape compiles with a `readonly` member and a `ref CommandRecorder` receiver, and fails with CS8350 without it. That is the whole point of the case.

**(b)** Rewrite the class doc comment. It currently claims the probe guards `scoped` on span parameters. After step 1 a non-`scoped` span parameter would also compile here, so the claim would be false. State instead: the probe guards that **a stack span reaches every recording entry point**, which now rests on two things — `readonly` on the member (issue #209) and `scoped` on by-value span parameters (issue #205). Keep both named, keep the `params ReadOnlySpan<T>` note.

## 3. Convert the thirteen `ColorAttachment[]` sites

Mechanical: `ColorAttachment[] color = [ … ];` → `ReadOnlySpan<ColorAttachment> color = [ … ];`. Leave everything else at the site alone — the `new RenderingInfo { … }` temporaries and the `rec.BeginRendering(new RenderingInfo { … })` inline form both keep working.

Samples (all inside the render loop):

- `samples/HelloTriangle/Program.cs:150`
- `samples/HelloCube/Program.cs:399`
- `samples/HelloVma/Program.cs:479`
- `samples/HelloVmaWindowed/Program.cs:331`
- `samples/HeadlessTriangle/Program.cs:95`
- `samples/HeadlessExport/Program.cs:118`
- `samples/AotSmoke/Program.cs:182`

Tests:

- `tests/Ahjo.Vulkan.Tests/CommandRecorderTests.cs:308, 435, 578, 714`
- `tests/Ahjo.Vulkan.Tests/MeshShaderTests.cs:807` (multi-line initializer — same conversion)
- `tests/Ahjo.Vulkan.Tests/WindowedValidationTests.cs:102`

A collection-expression span is a reusable `InlineArray` local, not a `localloc`, so it is safe in a loop body — this is why the samples get collection expressions rather than `stackalloc`. Do **not** convert any of these to `stackalloc`: a `stackalloc` inside a `while (running)` loop grows the frame every iteration.

`samples/HelloRayQuery` has no `BeginRendering` call and is untouched.

## 4. Benchmarks — drop the `scoped` incantation so the benchmark proves the fix

- `tests/Ahjo.Vulkan.Benchmarks/CommandRecorderBenchmarks.cs`: change `using scoped var rec = _cmdPool.Begin();` (`:143`) to `using var rec = _cmdPool.Begin();`, and delete the now-wrong comment at `:140-142`, replacing it with one line: the recording surface is `readonly` (#209), so a method-local stack span carried inside `RenderingInfo` flows in without `scoped`.
- `tests/Ahjo.Vulkan.Benchmarks/MeshShaderBenchmarks.cs`: same change at `:235, :278, :314` (`using (scoped var rec = _cmdPool.Begin())` → `using (var rec = _cmdPool.Begin())`), and trim the `scoped`-rationale sentence from the comment block immediately above each (the block above `:235` spans `:226-234`).

Leave `rec.BeginRendering(in info)` as `in` at all four sites — it stays valid and it keeps the benchmark exercising the shipped signature.

Do **not** change `Span<ColorAttachment> color = stackalloc …` in the benchmarks; those are method-local, not in a loop, and changing them would perturb a measured row for no reason.

The other seven `scoped var rec` sites — `AccelerationStructureBenchmarks.cs:362, :413`, `CommandRecorderBenchmarks.cs:168, :191`, `PipelineBarrierBenchmarks.cs:133, :177`, `PushDescriptorsBenchmarks.cs:197, :240` — are **out of scope**. None carries a `RenderingInfo`; their `scoped` is now redundant too, but every one of them sits inside a measured row and touching them buys nothing this issue needs.

## 5. Tests to add

In `tests/Ahjo.Vulkan.Tests/CommandRecorderTests.cs` (Windows-only wrapper suite — issue #32; do not design for a Linux lane):

1. **`BeginRendering_StackBackedColorAttachments_IsZeroAllocation`** — the behavioural assertion that makes step 1 worth having. Follows the `MeshShaderTests.MeshPipeline_Build_IsZeroAllocation` pattern: warm up once, then measure `GC.GetAllocatedBytesForCurrentThread()` across ≥128 iterations of `using var rec = pool.Begin(); ReadOnlySpan<ColorAttachment> color = [ … ]; rec.BeginRendering(new RenderingInfo { … }); rec.EndRendering(); rec.End();` plus `pool.ResetForFrame()`, and assert a zero delta. Record-only; never submitted.
2. **`BeginRendering_MultipleColorAttachments_StackBacked`** — a 2-attachment `ReadOnlySpan<ColorAttachment> color = [a, b]` against a 2-color-format pipeline, recorded and submitted, asserting no validation errors. Exercises the `count > 0` / `stackalloc VkRenderingAttachmentInfo[8]` branch (`CommandRecorder.cs:2188-2192`) with the new call shape.
3. **`BeginRendering_WithDepthAttachment_StackBacked`** — one color attachment as a collection-expression span plus a `DepthAttachment`, so the `pDepth` branch (`:2205`) is covered under the new shape. `CommandRecorderTests.cs:578` region already builds a depth image; reuse that fixture rather than adding one.

Cases 2 and 3 are behavioural (submit under `AHJO_VULKAN_TIER=validation`); case 1 is the allocation canary. The compile-time guard lives in `ScopedSpanProbe` (step 2), not here.

No new test file. No test that asserts on a compiler diagnostic — the probe covers that by construction.

## 6. Benchmarks to run

`readonly` on a member is metadata only; the JIT emits the same code, and no allocation is added or removed inside the wrapper. The measurable change is at the *call site*, where a heap array disappears. So: re-run, do not re-baseline blindly.

```bash
dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter "*CommandRecorder*|*MeshShader.DrawMeshTasks*|*PipelineBarrier*"
```

Expectation: `Allocated` stays `-` on every row (they were already `-` — the benchmarks used the `scoped` workaround), and Means land inside the existing spread. **If a Mean moves outside the recorded spread, stop and report** — that would mean `readonly` changed codegen, which contradicts the premise, and the plan should not be finished on a guess. Follow `docs/benchmarks.md`'s minimum-of-N discipline for the `MeshShader.*` rows.

Also run the `bench-coverage-checker` and `vulkan-validation-reviewer` agents before the PR — the diff touches `Recording/`.

## 7. Docs

- **`src/Ahjo.Vulkan/CLAUDE.md`** — the paragraph beginning "**Span parameters on `CommandRecorder` must be `scoped`.**" is now incomplete and its stated rationale is partly wrong. Rewrite it as two rules under one heading:
  1. *Recording members must be `readonly`.* A non-`readonly` member of a mutable `ref struct` passes `this` as a writable `ref`-to-`ref struct`, which forces caller-wide safe-context on every ref-struct argument — including anything passed by `in`, where `scoped` cannot help. `End()` and `Dispose()` are the only exceptions; they mutate `_ended`/`_retired`.
  2. *By-value span parameters must be `scoped`.* Unchanged from #205, but restate it as covering the by-value case specifically.
  Keep the `ScopedSpanProbe` sentence, updated to say what the probe now proves.
- **`docs/benchmarks.md`** — update the `CommandRecorder.RenderingPass100Cmds` row (`:89`) and the `MeshShader.DrawMeshTasks*` rows (`:92-94`) only if the re-run moves a number. Add one sentence to the notes section recording that the `scoped var rec` workaround the rows used to carry was removed by #209 and that the rows now measure the shape a consumer actually writes.
- **`README.md`** — no change; it carries no `BeginRendering` snippet (checked).
- **`docs/migration-vortice-to-ahjo.md`** — no change; it carries no `BeginRendering` snippet (checked).

## 8. Verification

```bash
dotnet build Ahjo.Vulkan.slnx          # must be clean; TreatWarningsAsErrors catches CS8656
dotnet test                            # Windows wrapper suite
dotnet publish samples/AotSmoke/AotSmoke.csproj -c Release -r win-x64 -p:IlcUseEnvironmentalTools=true
```

`AotSmoke` matters here specifically: its `ColorAttachment[]` becomes an `InlineArray`-backed collection expression, and the AOT publish is what proves that shape is trim- and ILC-clean. Run the produced exe.

Run each windowed sample (`HelloTriangle`, `HelloCube`, `HelloVmaWindowed`) briefly and confirm the image is unchanged; run `HeadlessTriangle` / `HeadlessExport` and diff their PNG output against a pre-change run. A wrong span lifetime here would show as garbage attachments, not as a build error.

## Open items

**OPEN — scope of the `readonly` sweep.** The plan marks all ~50 recording members `readonly` (fix the whole class) rather than only `BeginRendering`. That is a larger, if entirely mechanical, diff and it changes metadata on the whole public recording surface. If the reviewer would rather see `BeginRendering` + the three properties alone in this PR, say so before step 1 — the design is unaffected either way, only the blast radius changes. Stop and ask; do not decide this in the implementation.

**OPEN — the `GraphicsPipelineBuilder` sibling.** `WithVertexInput` / `WithColorBlend` are constrained by the same C# rule and are *not* fixed here (spec: different failure — CS8347 on the returned builder — and setup-time, so no per-frame invariant is at stake). `samples/HelloVma/Program.cs:440,444` keeps its heap arrays. If a follow-up issue is wanted for the builder path, that is a human call; the spec's position is that no follow-up is warranted.
