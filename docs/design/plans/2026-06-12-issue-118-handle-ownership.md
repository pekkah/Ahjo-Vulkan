# Implementation plan — explicit handle ownership (#118)

Paired with `../specs/2026-06-12-issue-118-handle-ownership-design.md`.
Option 3: `unmanaged` → `struct` on `IVulkanHandle<TSelf>`, `OwnsHandle`
interface member, `PipelineLayout` metadata on the struct, side tables deleted,
#102 §2 sync guards.

## Step 1 — Interface change

- `src/Ahjo.Vulkan/Internal/IVulkanHandle.cs`
  - Constraint `unmanaged` → `struct`.
  - Add `bool OwnsHandle { get; }`.
  - Rewrite remarks: drop the "one (or two) raw handles" bullet's implication
    of pointer-purity; add the ownership bullet (*owning iff `OwnsHandle`;
    `FromRaw` and `default` are borrowed: no-op `Dispose`, device-bound
    members throw*) and a note that one managed reference field is permitted
    (metadata rides the handle; setup-time allocation only).
- `src/Ahjo.Vulkan/Diagnostics/DebugMarker.cs` `ObjectName.Set<T>` —
  constraint `unmanaged, IVulkanHandle<T>` → `struct, IVulkanHandle<T>`.

## Step 2 — `OwnsHandle` on all fifteen struct implementers

Implemented as the existing null-owner check; `Dispose` guards switch to
`if (!OwnsHandle) return;` (same behavior, now reading the contract member):

| Type | `OwnsHandle` |
|---|---|
| `Buffer`, `Image` (Resources/) | `!Owner.IsNull` |
| `ImageView`, `Sampler` (Resources/), `ShaderModule`, `DescriptorSetLayout`, `PipelineLayout`, `PipelineCache`, `GraphicsPipeline`, `ComputePipeline` (Pipelines/) | `DeviceHandle != null` |
| `Surface` (Rendering/) | `InstanceHandle != null` |
| `Fence`, `TimelineSemaphore`, `BinarySemaphore` (Sync/), `DescriptorSet` (Pipelines/) | `false` (pool-owned; struct never destroys) |

## Step 3 — `PipelineLayout` metadata rides the struct

- `src/Ahjo.Vulkan/Pipelines/PipelineLayout.cs`
  - Add `internal sealed class LayoutMetadata { PushConstantRange[] PushRanges; nint[] SetLayouts; }`
    (file-scoped next to the struct) and an
    `internal readonly LayoutMetadata? Metadata` field; extend the internal
    ctor; `FromRaw` passes `null`.
  - Delete `s_pushRanges`, `s_setLayouts`, `s_metadataLock`,
    `RegisterPushRanges`, `RegisterSetLayouts`, `TryGetPushRanges`,
    `TryGetSetLayouts`, `UnregisterMetadata`, and the `UnregisterMetadata`
    call in `Dispose`. Rewrite the side-table comment block to describe the
    on-struct metadata.
- `src/Ahjo.Vulkan/Lifecycle/Device.cs` `CreatePipelineLayout` — stop calling
  `Register*`; build `LayoutMetadata` and pass it to the ctor.
- `src/Ahjo.Vulkan/Recording/CommandRecorder.cs`
  `AssertSetsMatchLayout` / `AssertPushRangeFits` — read
  `layout.Metadata?.SetLayouts` / `.PushRanges` instead of `TryGet*`.

## Step 4 — #102 §2: borrowed sync handles fail loudly

- `src/Ahjo.Vulkan/Sync/Fence.cs` — `IsSignaled`, `Wait`, `Reset`: leading
  `DeviceHandle == null` → `InvalidOperationException` ("requires an owning
  device; a FromRaw-constructed (borrowed) fence has none." — match the
  PipelineLayout/DescriptorSetLayout message shape).
- `src/Ahjo.Vulkan/Sync/TimelineSemaphore.cs` — same on `Value`, `Signal`,
  `WaitFor`.

## Step 5 — Tests (`tests/Ahjo.Vulkan.Tests/HandleConventionsTests.cs`)

- Generic helpers now constrain `struct, IVulkanHandle<T>`.
- `AssertBorrowContract<T>()`: `T.FromRaw(sentinel).OwnsHandle == false`,
  `default(T).OwnsHandle == false`; one `[Fact]` enumerating all fifteen
  types. Extend `FromRawHandles_Dispose_IsNoOp` to all eleven `IDisposable`
  types (add `PipelineCache`, `GraphicsPipeline`, `ComputePipeline`,
  `Surface`).
- Owning-side: construct each type via its internal ctor with sentinel owner
  pointers (`InternalsVisibleTo` already in place), assert
  `OwnsHandle == true`. **Do not dispose these.**
- `PipelineLayout` metadata: created-layout copy shares the same
  `LayoutMetadata` reference; `FromRaw`/`default` → `null`; reflection probe
  (test-side only) asserts `typeof(PipelineLayout)` declares no static
  `Dictionary<,>` fields.
- #102: `Fence.FromRaw(x).Wait(0)` / `.IsSignaled` / `.Reset()`,
  `TimelineSemaphore.FromRaw(x).Value` / `.Signal(0)` / `.WaitFor(0, 0)` each
  throw `InvalidOperationException`.

## Step 6 — Benchmark + docs

- New `tests/Ahjo.Vulkan.Benchmarks/HandleOwnershipBenchmarks.cs`
  (`[MemoryDiagnoser]`, no driver needed): non-inlined pass/return of
  `PipelineLayout` through a call chain; `Metadata` read on owning + borrowed;
  `ObjectName`-style constrained-generic dispatch. All `Allocated = -`.
- `docs/benchmarks.md`: add the class to the no-driver filter examples and a
  baseline section (numbers from this container are fine for the allocation
  column; mark Mean as host-dependent like the rest).
- `docs/aot-notes.md:11` mentions the static-abstract dispatch — confirm
  wording still holds (it does; constraint isn't named there).

## Step 7 — Verify invariants

- `dotnet build Ahjo.Vulkan.slnx` clean (warnings = errors) + `dotnet test`
  (GPU-gated tests skip in the container; Windows CI runs them on
  SwiftShader).
- `dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter
  "*HandleOwnership*"` → `Allocated = -`.
- Driver-bound recording benchmarks: recapture on the Windows host before
  merge (Release paths don't touch metadata; expected unchanged).
- `vulkan-validation-reviewer` + `bench-coverage-checker` over the diff.

## Risk notes

- **Contract-level break:** consumer code using
  `where T : unmanaged, IVulkanHandle<T>`, handles in unmanaged structs, or
  stackalloc'd wrapper arrays stops compiling. No in-repo code does; pre-1.0.
- **`Fence.Wait` guard sits on a per-frame path** (`FrameRing`): one null
  compare before a host syscall; covered by `FrameRing`/`SyncPool` benchmarks
  if recaptured.
- **`DescriptorSet.OwnsHandle == false` while carrying a `Layout` pointer** is
  correct but worth the doc comment: the layout pointer is routing metadata,
  not ownership.
