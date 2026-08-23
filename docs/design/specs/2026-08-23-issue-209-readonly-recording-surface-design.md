# A `readonly` recording surface — removing the per-frame `ColorAttachment[]`

Issue: [#209](https://github.com/pekkah/Ahjo-Vulkan/issues/209). Written 2026-08-23.

## Problem

Thirteen call sites in this repository allocate a `ColorAttachment[]` on the heap purely to satisfy the C# ref-safety rules, seven of them **inside a render loop**:

| Site | Per-frame? |
|---|---|
| `samples/HelloTriangle/Program.cs:150` | yes |
| `samples/HelloCube/Program.cs:399` | yes |
| `samples/HelloVma/Program.cs:479` | yes |
| `samples/HelloVmaWindowed/Program.cs:331` | yes |
| `samples/HeadlessTriangle/Program.cs:95` | yes (loop of 1) |
| `samples/HeadlessExport/Program.cs:118` | yes (loop of 1) |
| `samples/AotSmoke/Program.cs:182` | yes (loop of 1) |
| `tests/Ahjo.Vulkan.Tests/CommandRecorderTests.cs:308, 435, 578, 714` | no |
| `tests/Ahjo.Vulkan.Tests/MeshShaderTests.cs:807` | no |
| `tests/Ahjo.Vulkan.Tests/WindowedValidationTests.cs:102` | no |

`src/Ahjo.Vulkan/CLAUDE.md` states zero per-frame allocations on `Recording/**` as a hard invariant. The samples are the code consumers copy from, so the wart propagates.

The issue proposes changing `CommandRecorder.BeginRendering(in RenderingInfo)` (`src/Ahjo.Vulkan/Recording/CommandRecorder.cs:2182`) to take `RenderingInfo` **by value**, on the diagnosis that `scoped` cannot narrow the value-safe-context of an `in` ref-struct parameter.

**The diagnosis is half right and the proposed fix does not work.** Both were checked with a compiled repro rather than reasoned about (see Evidence). By-value fails at the same call sites with the same `CS8350`.

## Evidence

### The repro

A scratch project (`net10.0`, `LangVersion=14.0`, same as `Directory.Build.props:4-5`) reproducing the exact shape — a mutable `ref struct` recorder with two `bool` state fields and a `ref readonly` function-table property, a `ref struct Info` holding a `ReadOnlySpan<T>`, `using var rec = Pool.Begin()` inside a loop, a collection-expression span. Results:

| Variant | Compiles? |
|---|---|
| `void Begin(in Info)`, plain `var rec` | **no** — CS8350 / CS8352 |
| `void Begin(scoped in Info)`, plain `var rec` | **no** — CS8350 |
| **`void Begin(Info)` by value, plain `var rec`** | **no** — CS8350 |
| `void Begin(Info)` by value, inline `new Info { … }` temp | **no** — CS8352 on `'Attachments = color'` |
| `void Begin(in Info)`, `scoped var rec` | yes |
| **`readonly void Begin(in Info)`, plain `var rec`** | **yes** |
| `readonly void Begin(Info)` by value, plain `var rec` | yes |
| `Begin(in Info)` on a `readonly ref struct` receiver | yes |
| `readonly` member, receiver is a `ref Rec` **parameter** (the `ScopedSpanProbe` shape) | yes |
| extension method `this scoped ref Rec` | **no** — CS8350 |

### What is actually blocking the call

Not the parameter. **The receiver.**

`BeginRendering` is a non-`readonly` instance member of a mutable `ref struct`, so `this` is passed as a writable `ref` to a `ref struct`. Under "method arguments must match", a writable ref-to-ref-struct argument forces every other ref-struct argument in the same invocation to have caller-wide safe-context — the callee could, in principle, store a narrow-scoped value into the wide-scoped receiver. That constraint fires regardless of how `info` is passed, which is why `in`, `scoped in` and by-value all fail identically.

`readonly` on the member changes `this` to `ref readonly`. A read-only receiver cannot be assigned to, the constraint no longer applies, and a stack-backed span flows in unchanged. This is a *soundness-preserving* relaxation, not a suppression: the compiler still enforces that a `readonly` member writes nothing.

This also explains, retroactively and correctly, why #205 / PR #208 worked for `DescriptorWrite[]` and `MemoryBarrier[]`: `scoped` on a **by-value** ref-struct parameter excludes it from the calculation entirely. There is no equivalent escape for a parameter passed by `in`, because `scoped` on a by-ref parameter narrows only the ref-safe-context.

### The workaround that already exists in-tree

`tests/Ahjo.Vulkan.Benchmarks/CommandRecorderBenchmarks.cs:143` and `tests/Ahjo.Vulkan.Benchmarks/MeshShaderBenchmarks.cs:235, 278, 314` already pass a `stackalloc`-backed `RenderingInfo` to `BeginRendering(in info)`, by declaring the recorder local `scoped`:

```csharp
using scoped var rec = _cmdPool.Begin();
rec.BeginRendering(in info);
```

with a comment at `CommandRecorderBenchmarks.cs:140-142` explaining exactly this. So the zero-allocation path is reachable today — but only by a caller who knows to write `scoped` on a local, and the benchmark authors had to discover it. That is the ergonomic defect, and it is library-side.

Confirmed as a side finding: a `scoped` recorder local can still be handed to `Queue.Submit2(ref CommandRecorder, in Fence)` (`src/Ahjo.Vulkan/Lifecycle/Queue.cs:106`) and to `ref CommandRecorder` helpers like `RecordSwapchainBarrier` (`samples/HelloTriangle/Program.cs:144`), so the call-site workaround is viable — it is rejected below on ergonomics, not on feasibility.

### Which members can be `readonly`

`CommandRecorder` has exactly two mutable fields, `_ended` and `_retired` (`CommandRecorder.cs:37-38`), assigned only in the constructor (`:44-45`), `End()` (`:79`) and `Dispose()` (`:105, :111`). Every other instance member — around 50 public recording entry points plus the private `RecordDependency` (`:915`) and the properties `IsNull` (`:48`), `Fns` (`:56`) and `RawHandle` (`:68`) — mutates nothing. `CommandRecorder` cannot become a `readonly ref struct` because of `End`/`Dispose`, but every recording member individually can.

One consequence must be handled: calling a non-`readonly` member from a `readonly` member emits **CS8656** ("results in an implicit copy of `this`"), which `TreatWarningsAsErrors=true` turns into a build failure. Reproduced. `Fns`, `IsNull` and `RawHandle` therefore have to be marked `readonly` in the same change.

### The sibling audit — is anything else broken the same way?

Every `ref struct` in `src/Ahjo.Vulkan/` was checked for the same shape (a ref-struct-typed argument passed to a member of a ref-struct receiver):

- **`GraphicsPipelineBuilder.WithVertexInput(in VertexInputDescription)`** (`src/Ahjo.Vulkan/Pipelines/GraphicsPipelineBuilder.cs:344`) and **`WithColorBlend(in ColorBlendDescription)`** (`:430`) have the same shape and *are* constrained — which is why `samples/HelloVma/Program.cs:440,444` declares `VertexBindingDescription[]` / `VertexAttributeDescription[]` as heap arrays. Verified in the repro: `readonly` does **not** fix these, because the builder methods *return* the builder, so the narrowed safe-context propagates to the assignment target and fails with CS8347 instead. They are also strictly setup-time — the pipeline is built once — so no invariant is violated. **Out of scope, deliberately, and not a follow-up issue**: the fix there is a different one (a `scoped` builder local at the call site), for a path with no per-frame obligation.
- **`AccelerationStructureBuild`** (`src/Ahjo.Vulkan/Recording/AccelerationStructureBuild.cs:55`) is a plain `readonly struct`, not a `ref struct` — the CSR shape documented at `:11-25` exists precisely to avoid spans-in-structs. `BuildAccelerationStructures` (`CommandRecorder.cs:1173`) takes three `scoped ReadOnlySpan<T>` and is already stack-friendly.
- **`ChainBuilder<TRoot>`** (`src/Ahjo.Vulkan/Memory/ChainBuilder.cs:46`) takes no ref-struct arguments; `Push<T>()` returns `ref T`. Unaffected.
- `Instance.Create(scoped in InstanceDescription)` (`src/Ahjo.Vulkan/Lifecycle/Instance.cs:60`), `PhysicalDevice.CreateDevice(in DeviceDescription)` (`:497`), `Device.CreatePipelineLayout` / `CreateDescriptorSetLayout` (`Device.cs:880, :225`), `new Swapchain(Device, in SwapchainDescription)` (`Swapchain.cs:119`) and `Swapchain.Recreate` (`:196`) are all **static methods or members of classes** — no ref-struct receiver, therefore no constraint. Confirmed in the repro. These already accept stack-backed descriptions today; the `scoped` on `Instance.Create` is redundant but harmless.

So `BeginRendering` is the **only** per-frame API with this defect, and marking the recording surface `readonly` closes the entire class for `Recording/` permanently — including any future recording method that takes a ref-struct description.

### Cost of the by-value alternative, for the record

Hand-computed x64 layout of `RenderingInfo` (`src/Ahjo.Vulkan/Recording/RenderingInfo.cs:10-18`): `VkRect2D` 16 + `LayerCount` 4 + `ViewMask` 4 + `ReadOnlySpan<ColorAttachment>` 16 + two `DepthAttachment?` at ~48 each (`DepthAttachment` is `ImageView` = two pointers, plus four 4-byte fields, plus the `Nullable` flag and padding) ≈ **136 bytes**. Not measured — treat as an estimate. A by-value signature would copy that on every `BeginRendering`; small against a ~3.2 µs recorded pass (`docs/benchmarks.md:89`), but it is a pure cost with no benefit once the receiver is `readonly`.

## Decision

**Mark every non-mutating instance member of `CommandRecorder` `readonly`. Keep `BeginRendering(in RenderingInfo)` exactly as it is.** Then convert the thirteen `ColorAttachment[]` sites to `ReadOnlySpan<ColorAttachment> color = [ … ]`.

Why this and not the issue's framing:

- It is the **only** option in evidence that actually compiles the sample shape without a call-site incantation.
- It costs **zero** API change: no signature moves, no source break, no binary break for callers. The house `in`-for-ref-struct-descriptions pattern survives intact, so there is no deviation to document and no inconsistency with `Instance.Create` / `CreateDevice` / `CreatePipelineLayout` / `Swapchain`.
- It fixes the whole class rather than one method, per the repo's standing preference: the constraint is gone from all ~50 recording entry points at once, so the next recording API that takes a `ref struct` description is immune by construction.
- `readonly` on the recording surface is independently correct documentation — recording a command genuinely does not mutate the recorder.

The one honest cost: `ScopedSpanProbe` (`tests/Ahjo.Vulkan.Tests/ScopedSpanProbe.cs`) currently guards `scoped` on span parameters by construction. Once receivers are `readonly`, a non-`scoped` span parameter also compiles there, so the probe stops being a guard for `scoped` specifically and becomes a guard for the property that actually matters — *a stack span reaches every recording entry point*. `scoped` stays on the parameters: it is accurate, it documents that the recorder captures nothing, and it is what keeps the by-value span path working if a member ever has to become mutating. The probe's doc comment must be updated to say what it now proves, and a `BeginRendering` case added — that case fails to compile the moment `readonly` is dropped, which is the regression this change needs guarded.

### Why not the alternatives

- **Option 1 — by value for `BeginRendering` only.** Does not compile. Verified: `readonly void Begin(Info)` works, `void Begin(Info)` does not, so any credit belongs to `readonly`, not to by-value. It would also break four in-tree call sites that pass `in` explicitly (`CommandRecorderBenchmarks.cs:144`, `MeshShaderBenchmarks.cs:237, 280, 316`), contradicting the issue's "source-compatible for every call site" claim.
- **Option 2 — by value for every ref-struct description type.** Fixes nothing that `readonly` does not fix better, and actively breaks things: `Instance.Create` / `CreateDevice` / `CreatePipelineLayout` / `CreateDescriptorSetLayout` / `Swapchain` were never constrained (class or static receivers), 34 in-tree call sites pass `in` explicitly and would break (3 `new Swapchain(device, in …)`, 8 `CreateDevice(in …)`, 16 in `SwapchainTests.cs`, 2 `Instance.Create(in …)`, 2 `CreateDescriptorSetLayout(in …)`, 3 in `WindowedValidationTests.cs`), and the `GraphicsPipelineBuilder` pair it would appear to target is blocked by CS8347 on the *return*, not by the parameter.
- **Option 3 — keep `in`, accept the allocation, comment the call sites.** Ships a documented violation of a stated hard invariant in the exemplar code, when a zero-API-cost fix exists.
- **Option 3b (not in the issue) — leave the library alone and write `using scoped var rec` at every call site.** This works today and is what the benchmarks do. Rejected because it puts the burden on every consumer of a library whose selling point is that the low-allocation path is the default one; the Logos engine would carry the same incantation at every render-pass site forever.
- **Make `CommandRecorder` a `readonly ref struct`.** Impossible: `End()` and `Dispose()` mutate `_ended` / `_retired` (`CommandRecorder.cs:79, :105, :111`), and that state is load-bearing for the pool's outstanding-buffer tracking.
- **An extension method with a `this scoped ref CommandRecorder` receiver.** Verified not to compile — `scoped` on the receiver ref does not narrow the receiver *value*, so the constraint survives. It would also move a core API out of the type.
- **Fix `GraphicsPipelineBuilder` in the same change.** Deliberately excluded: different failure (CS8347 on the returned builder), different remedy, and setup-time, so no invariant is at stake. Widening here would double the diff for no invariant gain.

## Cross-links

- Resolves [#209](https://github.com/pekkah/Ahjo-Vulkan/issues/209), and corrects its stated diagnosis and proposed fix.
- Completes [#205](https://github.com/pekkah/Ahjo-Vulkan/issues/205) / PR [#208](https://github.com/pekkah/Ahjo-Vulkan/pull/208), which fixed the by-value-span half of the same underlying constraint. The `scoped` modifiers PR #208 added stay.
- Lands consistently with `docs/design/specs/2026-08-23-issue-202-acceleration-structure-design.md`, whose CSR span design sidesteps the same rule and stays correct unchanged.
- Touches the hot path behind `CommandRecorder.RenderingPass100Cmds` and the three `MeshShader.DrawMeshTasks*` rows (`docs/benchmarks.md:89, 92-94`).
- The invariant this defends is `src/Ahjo.Vulkan/CLAUDE.md`, "Zero per-frame allocations on hot paths"; that file's `scoped`-modifier paragraph needs the `readonly` rule added alongside it.
