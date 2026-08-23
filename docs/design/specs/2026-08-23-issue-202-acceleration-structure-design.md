# Acceleration-structure surface — owning `AccelerationStructure`, CSR-batched builds, BLAS compaction over the #199 query pool

- Issue: #202 — "Ray tracing: acceleration-structure surface (KHR acceleration structure + ray query)"
- Plan: [`../plans/2026-08-23-issue-202-acceleration-structure.md`](../plans/2026-08-23-issue-202-acceleration-structure.md)
- Lands on top of: [#201 mesh shader](2026-08-22-issue-201-mesh-shader-design.md) (the gated device-extension mechanism), [#201 properties chain](2026-08-22-issue-201-properties-chain-query-design.md) (the limits read), [#198 timestamp query pool](2026-08-16-issue-198-timestamp-query-pool-design.md) (the query-pool surface compaction rides on)

Scope settled by the maintainer before this spec was written, and treated as
input rather than an open question:

- **BLAS compaction is in scope** — the compacted-size query type,
  `vkCmdWriteAccelerationStructuresPropertiesKHR`, and
  `vkCmdCopyAccelerationStructureKHR` with
  `VK_COPY_ACCELERATION_STRUCTURE_MODE_COMPACT_KHR`.
- **Ray query only** — no `VK_KHR_ray_tracing_pipeline`, no shader binding
  tables, no `VK_NV_cluster_acceleration_structure`.

## Problem

The wrapper has no acceleration-structure surface. A grep of
`src/Ahjo.Vulkan` for `AccelerationStructure` hits exactly **one** file:

- `Memory/BufferUsage.cs:24` — `AccelerationStructureBuildInputReadOnly`
- `Memory/BufferUsage.cs:25` — `AccelerationStructureStorage`

Two enum members. There is no handle type, no create/destroy path, no build
recording, no device-address accessor, and no entry-point loading. A consumer
that wants ray query today has to drop to `Ahjo.Vulkan.Native` for the whole
subsystem, which defeats the point of the wrapper and — because the loader does
not export device-extension symbols through `vulkan-1`
(`Internal/InstanceFunctionTable.cs:6-10`, restated in the `DeviceFunctionTable`
charter at `Internal/DeviceFunctionTable.cs:36-45`) — would have it depend on
`[DllImport]` symbols that resolve or not depending on the host loader build.

Everything native already exists; this is wrapper surface only. The generated
bindings carry every entry point the design needs:

| Entry point | `Generated/Vk.cs` |
|---|---|
| `vkCreateAccelerationStructureKHR` | `:3137` |
| `vkDestroyAccelerationStructureKHR` | `:3140` |
| `vkCmdBuildAccelerationStructuresKHR` | `:3143` |
| `vkCmdCopyAccelerationStructureKHR` | `:3164` |
| `vkGetAccelerationStructureDeviceAddressKHR` | `:3174` |
| `vkCmdWriteAccelerationStructuresPropertiesKHR` | `:3177` |
| `vkGetAccelerationStructureBuildSizesKHR` | `:3183` |

and the struct/enum shapes: `VkAccelerationStructureCreateInfoKHR`,
`VkAccelerationStructureBuildGeometryInfoKHR`,
`VkAccelerationStructureGeometryKHR` (+ the `Triangles`/`Aabbs`/`Instances`
data structs and their `VkAccelerationStructureGeometryDataKHR` union),
`VkAccelerationStructureBuildRangeInfoKHR`,
`VkAccelerationStructureBuildSizesInfoKHR`,
`VkAccelerationStructureInstanceKHR`,
`VkWriteDescriptorSetAccelerationStructureKHR`,
`VkPhysicalDeviceAccelerationStructurePropertiesKHR` (already
`IChainable<VkPhysicalDeviceProperties2>` at
`Generated/Chains/VkPhysicalDeviceAccelerationStructurePropertiesKHR.Chain.g.cs:6`),
`VkPhysicalDeviceAccelerationStructureFeaturesKHR`,
`VkPhysicalDeviceRayQueryFeaturesKHR`.

What blocks on this, per the issue: ADR-0027's reference path tracer (CPU-only,
records "no acceleration-structure surface in the wrapper" as one of three
reasons a GPU tier is deferred) and ADR-0028 (Louhi). Neither is urgent; both
have fallbacks that ship today.

## Evidence

### 1. `VK_KHR_ray_query` defines zero entry points

`grep -c "RayQuery\|rayQuery" src/Ahjo.Vulkan.Native/Generated/Vk.cs` returns
**0**. Ray query is a pure SPIR-V capability: it adds `OpRayQuery*`
instructions and the `VkPhysicalDeviceRayQueryFeaturesKHR` feature struct (its
own generated file), and no commands at all.

`VK_KHR_deferred_host_operations` is a hard *device-creation* dependency of
`VK_KHR_acceleration_structure`, but this design calls no deferred entry point:
`vkCmdBuildAccelerationStructuresKHR` and `vkCmdCopyAccelerationStructureKHR`
are the command-buffer forms, which take no `VkDeferredOperationKHR`; the host
forms (`vkBuildAccelerationStructuresKHR` `:3149`,
`vkCopyAccelerationStructureKHR` `:3152`) are out of scope.

**Consequence for the design:** despite the issue's plural ("resolved only when
the *extensions* are enabled"), there is exactly **one** extension to gate
entry-point resolution on — `VK_KHR_acceleration_structure`. The other two
extensions are things the caller must pass to `vkCreateDevice`; the wrapper's
only job for them is to supply the UTF-8 names.

### 2. The #201 mechanism is reusable verbatim, and was designed to be

`DeviceFunctionTable` already takes the enabled device-extension list
(`Internal/DeviceFunctionTable.cs:234`), nulls extension pointers up front
(`:239-242`), resolves a gated block (`:402-419`), and has the two failure modes
this issue asks for:

- not enabled ⇒ null pointer ⇒ the calling wrapper method throws naming the
  extension (`Recording/CommandRecorder.cs:470-473`, `:608-612`);
- enabled but unresolvable ⇒ `ResolveExtensionRequired` (`:487-493`) throws at
  `Device` construction via `ThrowExtensionEntryPointMissing` (`:496-504`).

The charter comment names this issue explicitly: *"`VK_EXT_mesh_shader` is the
first member; issue #202 … is the next consumer and adds a second `if` block of
the same shape"* (`:40-45`), and it already states that the group is **not**
limited to `vkCmd*` — *"create/destroy/query entry points of a device extension
belong here too"* (`:36-40`). Four of this design's seven pointers are
device-level rather than command-level, so that sentence is load-bearing.

`Device`'s constructor is `internal` and takes the span
(`Lifecycle/Device.cs:52-58`); the sole `new Device(...)` call site is
`Lifecycle/PhysicalDevice.cs:583`. No signature change is needed anywhere.

### 3. The #199 query-pool surface: readback fits, creation does not

Compaction needs a query pool of type
`VK_QUERY_TYPE_ACCELERATION_STRUCTURE_COMPACTED_SIZE_KHR`
(`Generated/VkQueryType.cs:8`). Auditing what #198/#199 shipped
(`Sync/QueryPool.cs`, 224 lines):

**Fits, unchanged.**

- `TryGetResults(uint, Span<ulong>)` (`:109`),
  `TryGetResults(uint, Span<QueryResult>)` (`:146`) and
  `GetResults(uint, Span<ulong>)` (`:181`) issue `vkGetQueryPoolResults` with
  `VK_QUERY_RESULT_64_BIT` and a stride of 8 (or 16 with availability). A
  compacted-size result is exactly one `uint64_t` — the same shape a timestamp
  is. Nothing in those three methods is timestamp-specific in *behaviour*.
- The reset discipline is identical: a compacted-size query must be reset by a
  submitted `CommandRecorder.ResetQueryPool`
  (`Recording/CommandRecorder.cs:983`) before it is written, exactly as a
  timestamp must.
- The bounds guard `AssertRangeInBounds` (`Sync/QueryPool.cs:213-224`) and the
  borrowed-handle guard `ThrowIfBorrowed` (`:201-208`) are type-agnostic.

**Does not fit.**

- `Device.CreateQueryPool(uint queryCount)` (`Lifecycle/Device.cs:583-598`)
  hard-codes `queryType = VkQueryType.VK_QUERY_TYPE_TIMESTAMP` (`:592`). There
  is no way to mint any other pool.
- `QueryPool` stores `_queryCount` but **not** the query type (`:44-46`), so
  nothing can stop `WriteTimestamp` on a compacted-size pool or vice versa —
  both are validation errors the wrapper is otherwise positioned to catch, given
  it already null-checks the pool and range-checks the index
  (`Recording/CommandRecorder.cs:1027-1042`).
- The *prose* is timestamp-specific throughout (ticks, `TimestampValidBits`,
  `Device.TimestampPeriod` — `Sync/QueryPool.cs:6-45`, `:100-107`).

**Consumer audit for widening `CreateQueryPool`.** Call sites of
`Device.CreateQueryPool`:
`tests/Ahjo.Vulkan.Benchmarks/TimestampQueryBenchmarks.cs:48` and 12 calls in
`tests/Ahjo.Vulkan.Tests/TimestampQueryTests.cs` (`:40`, `:54`, `:67`, `:104`,
`:131`, `:161`, `:232`, `:269`, `:296`, …). **Zero** in `src/` and **zero** in
`samples/`. All pass a bare `uint`; adding a `CreateQueryPool(QueryType, uint)`
overload leaves every one of them compiling and meaning what it means today.

### 4. `Stage` and `Access` have no acceleration-structure bits

`Recording/Stage.cs` is 37 lines and stops at `VertexAttributeInput`;
`Recording/Access.cs` is 33 lines and stops at `ShaderStorageWrite`. Neither
carries an acceleration-structure bit. The native values exist only as sync2
constants, not enum members:

- `Generated/Vk.cs:727` — `VK_PIPELINE_STAGE_2_ACCELERATION_STRUCTURE_BUILD_BIT_KHR = 0x02000000`
- `Generated/Vk.cs:763` — `VK_PIPELINE_STAGE_2_ACCELERATION_STRUCTURE_COPY_BIT_KHR = 0x10000000`
- `Generated/Vk.cs:967` — `VK_ACCESS_2_ACCELERATION_STRUCTURE_READ_BIT_KHR = 0x00200000`
- `Generated/Vk.cs:970` — `VK_ACCESS_2_ACCELERATION_STRUCTURE_WRITE_BIT_KHR = 0x00400000`

Without these four the surface is not merely inconvenient, it is
**unsynchronizable**: a caller cannot order a build against a traversal, a build
against its compacted-size query, or a compaction copy against use of the copy's
destination. Every one of those is a required barrier. `Stage` and `Access` are
both already `: ulong`, so the additions are pure members.

There is currently **no** drift test for `Stage`/`Access` — the eight
`*_MatchesNative` facts in `tests/Ahjo.Vulkan.Tests/ShadowEnumDriftTests.cs`
cover `BufferUsage` (`:27`), `ImageUsage` (`:45`), `AllocationFlags` (`:60`),
`MemoryUsage` (`:79`), `ShaderStages` (`:89`), `DescriptorBindingFlags` (`:104`),
`EventCreateFlags` (`:113`) and `MemoryProperties` (`:120`) and stop there.

### 5. Descriptor writes cannot express a TLAS binding — the gap that makes ray query unreachable

This is the finding the issue's framing misses. The issue says *"Ray query
itself is a shader-side capability, so nothing on the recorder needs to change
for the traversal — only for building."* That is true of the **recorder** and
false of the **descriptor set**: a ray-query shader reads the TLAS through a
`VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR` binding, and writing that
descriptor requires chaining `VkWriteDescriptorSetAccelerationStructureKHR`
(`Generated/VkWriteDescriptorSetAccelerationStructureKHR.cs`) into
`VkWriteDescriptorSet.pNext`, with `pBufferInfo`/`pImageInfo` left null.

What already works:

- `DescriptorBinding.Type` is a raw `VkDescriptorType`
  (`Pipelines/DescriptorBinding.cs:30`), so an AS binding is declarable today.
- `DescriptorSetPool` takes raw `ReadOnlySpan<VkDescriptorPoolSize>`
  (`Pools/DescriptorSetPool.cs:152`), so an AS pool budget is expressible today.

What does not:

- `DescriptorWrite.Kind` has exactly two members — `Buffer = 0`, `Image = 1`
  (`Pools/DescriptorWrite.cs:40-44`) — and the struct carries only
  `_buffer`/`_image` payloads (`:50-51`).
- `DescriptorWriteBuilder.BuildWrites` (`Pools/DescriptorWriteBuilder.cs:31-70`)
  sets `pBufferInfo`/`pImageInfo` and **never** writes `pNext`.

There are exactly **two** call sites of `BuildWrites`:
`Pools/DescriptorSetExtensions.cs:59` (in `FlushUpdate`) and
`Recording/CommandRecorder.cs:487` (in `FlushPush`). Both carve their
`VkWriteDescriptorSet` scratch the same way — `stackalloc` at `≤ 8` writes,
`ArrayPool<VkWriteDescriptorSet>` beyond (`Pools/DescriptorSetExtensions.cs:16`,
`:33-48`; `Recording/CommandRecorder.cs:441-459`).

So the AS descriptor write is a three-file change with two call sites, and
without it nothing in this design can be *used* by a ray-query shader.

### 6. The properties-chain query already lands the scratch-alignment read

`minAccelerationStructureScratchOffsetAlignment` is the last field of
`VkPhysicalDeviceAccelerationStructurePropertiesKHR`
(`Generated/VkPhysicalDeviceAccelerationStructurePropertiesKHR.cs:31-32`).
`PhysicalDevice.TryGetProperties<T>(Utf8Name, out T)`
(`Lifecycle/PhysicalDevice.cs:305-317`) reads exactly this shape, and its own
doc comment already names this struct as an intended consumer
(`Lifecycle/PhysicalDevice.cs:246-248`). The chainable is generated
(`Generated/Chains/VkPhysicalDeviceAccelerationStructurePropertiesKHR.Chain.g.cs:6`),
so no rsp or codegen change is needed.

`MeshShaderLimits` (`Lifecycle/MeshShaderLimits.cs`) + `TryGetMeshShaderLimits`
(`Lifecycle/PhysicalDevice.cs:382-405`) is the precedent for a narrow typed
projection over such a struct: *"the subset a caller … actually has to obey"*,
with the raw struct reachable in one line for everything else.

### 7. Handle conventions, and the one place acceleration structures break them

`IVulkanHandle<TSelf>` (`Internal/IVulkanHandle.cs:57-77`) requires
`readonly struct`, `default(T)`-is-null, `FromRaw` producing a *borrowed* handle
with `OwnsHandle == false`, and no finalizers. `Event` (`Sync/Event.cs:36-77`)
and `QueryPool` (`Sync/QueryPool.cs:44-83`) are the two caller-owned,
`Device.Create*`-minted precedents, and both destroy through a **static**
`[DllImport]`: `Vk.vkDestroyEvent` (`Sync/Event.cs:76`), `Vk.vkDestroyQueryPool`
(`Sync/QueryPool.cs:82`).

That last part does not carry over. `vkDestroyAccelerationStructureKHR` belongs
to a device extension, so per the charter
(`Internal/DeviceFunctionTable.cs:36-40`) it must be reached through
`vkGetDeviceProcAddr` — and a `readonly struct` handle cannot reach
`Device.Functions` from `Dispose()` without holding something.

Three more conventions that constrain the shape:

- `Buffer.GetDeviceAddress(Device device)` (`Resources/Buffer.cs:104-112`) takes
  the device as a parameter rather than storing it. That is the precedent for
  the device-address accessor #202 asks for.
- `Buffer` deliberately does **not** implement equality: *"adding equality would
  imply that two structs sharing the same handle are 'the same buffer,' which is
  misleading on a copy-by-value type"* (`Resources/Buffer.cs:22-26`). This
  matters because a `record struct` that *contains* a handle would synthesize an
  `Equals` routed through `EqualityComparer<T>.Default` → `ValueType.Equals`,
  i.e. runtime field-walking, on a type nobody compares.
- `HandleConventionsTests.BorrowContract_HoldsForEveryHandleType`
  (`tests/Ahjo.Vulkan.Tests/HandleConventionsTests.cs:59-80+`) enumerates every
  handle type explicitly, *"so adding a handle type forces a conscious entry"*.

### 8. Hot-path and benchmark surface

`Recording/**` is on the zero-per-frame-allocation list
(`src/Ahjo.Vulkan/CLAUDE.md`). A dynamic TLAS is rebuilt (or updated) **every
frame** for any scene with moving objects, so `BuildAccelerationStructures` is a
genuine per-frame path — unlike BLAS builds, which are load-time. The
compacted-size query and the compaction copy are load-time.

The translation precedent is `CommandRecorder.PushDescriptorSet`
(`Recording/CommandRecorder.cs:441-459`): `const int StackThreshold = 8`,
`stackalloc` below it, `ArrayPool<T>.Shared.Rent/Return` in a `try/finally`
above it, with the caller's span pinned across the native call and the native
structs pointing into it (`Pools/DescriptorWriteBuilder.cs:41-49`).

The "exact mirror so the conversion is a pointer cast, not a copy" trick is also
established: `BufferDescriptorWrite`/`ImageDescriptorWrite` mirror
`VkDescriptorBufferInfo`/`VkDescriptorImageInfo` for exactly that reason
(`Pools/DescriptorWrite.cs:31-35`).

Benchmark precedent for a class that needs an optional device capability:
`MeshShaderBenchmarks`
(`tests/Ahjo.Vulkan.Benchmarks/MeshShaderBenchmarks.cs:17-31`) — *"a host
without them must not take the issue-29 canary down with it"* — with its rows
and the driver-dependency caveat recorded in `docs/benchmarks.md:92-96` and
`:165-200`.

### 9. Test-gating reality

Wrapper tests are Windows-only (#32). The hosted `windows-latest` runner has no
GPU and no ray-tracing-capable ICD, so **every** test that needs a real
acceleration structure will report `[gate:feature]` in CI and only run on the
maintainer's host. `TestGate` (`tests/Shared/TestGate.cs`) is the only
sanctioned skip and CI fails on an unclassified one; the
create-device-and-catch-`VK_ERROR_EXTENSION_NOT_PRESENT` probe is already
written for exactly this shape
(`tests/Ahjo.Vulkan.Tests/ExportableResourceTests.cs:74-81`, `:176-209`).

That pushes the design toward putting as much behaviour as possible in
**driver-free or driver-only** territory: argument validation, layout mirrors,
enum drift, and the "extension not enabled ⇒ throws naming it" path all run on
any host with an ICD.

### 10. What a cluster-AS follow-up would and would not reuse

The issue asks that the KHR surface not be designed into a corner for
`VK_NV_cluster_acceleration_structure`. Reading the generated bindings rather
than speculating:

- `vkGetClusterAccelerationStructureBuildSizesNV` (`Generated/Vk.cs:3083`) fills
  the **same** `VkAccelerationStructureBuildSizesInfoKHR`. So the sizes type and
  the caller-owned-scratch model carry over unchanged.
- Cluster ASes are ordinary `VkAccelerationStructureKHR` objects living in
  ordinary buffers, so the handle type, the buffer-ownership model and the
  device-address accessor carry over unchanged.
- `vkCmdBuildClusterAccelerationStructureIndirectNV` (`Generated/Vk.cs:3086`)
  takes `VkClusterAccelerationStructureCommandsInfoNV` and is **indirect-only** —
  its inputs are device addresses of GPU-resident command arrays. It shares
  *nothing* with `VkAccelerationStructureGeometryKHR` /
  `VkAccelerationStructureBuildRangeInfoKHR`.

**Conclusion, stated plainly:** the geometry-description types designed here
will not be reused by a cluster path *no matter how they are shaped*, so shaping
them for a hypothetical cluster future buys nothing. What must stay clean is the
handle, the ownership model and the sizes type — and those are shared by
construction. The one decision that *would* have to be rebuilt is recorded in
"Uncertainty" below.

## Decision

Eight pieces, one coherent surface. Nothing here requires a codegen change.

### A. One gated entry-point block, keyed on `VK_KHR_acceleration_structure`

`DeviceFunctionTable` gains seven pointers, nulled with the mesh trio at
`:239-242` and resolved in a second `if` block after `:419`:

```csharp
if (IsExtensionEnabled(enabledExtensions, DeviceExtensionNames.AccelerationStructure))
{
    CreateAccelerationStructure              = …; // vkCreateAccelerationStructureKHR
    DestroyAccelerationStructure             = …; // vkDestroyAccelerationStructureKHR
    GetAccelerationStructureBuildSizes       = …; // vkGetAccelerationStructureBuildSizesKHR
    GetAccelerationStructureDeviceAddress    = …; // vkGetAccelerationStructureDeviceAddressKHR
    CmdBuildAccelerationStructures           = …; // vkCmdBuildAccelerationStructuresKHR
    CmdWriteAccelerationStructuresProperties = …; // vkCmdWriteAccelerationStructuresPropertiesKHR
    CmdCopyAccelerationStructure             = …; // vkCmdCopyAccelerationStructureKHR
}
```

all through
`ResolveExtensionRequired(name, DeviceExtensionNames.AccelerationStructure)`.
Names live in `Internal/DeviceExtensionNames.cs` as `"…"u8` literals, alongside
the three extension names (`VK_KHR_acceleration_structure`, `VK_KHR_ray_query`,
`VK_KHR_deferred_host_operations`), which `Rendering/VulkanExtensions.cs`
re-exposes as `Utf8Name`s (`KhrAccelerationStructure`, `KhrRayQuery`,
`KhrDeferredHostOperations`).

Not enabled ⇒ pointers stay null ⇒ every public entry point in this design
throws an `InvalidOperationException` carrying
`AccelerationStructureSupport.EnableInstructions` (a `const string`, the
`Internal/MeshShaderSupport.cs` shape) that names all three extensions and the
`accelerationStructure` / `rayQuery` / `bufferDeviceAddress` features. The
`PartialGuardNote` counterpart records that a non-null pointer proves the
*extension* only — Vulkan exposes no post-`vkCreateDevice` feature query, the
same limitation `MeshShaderSupport.PartialGuardNote` documents.

### B. `AccelerationStructure` — caller-owned handle that does **not** own its buffer

`Resources/AccelerationStructure.cs`, next to `Buffer` and `Image`:

```csharp
public readonly unsafe struct AccelerationStructure
    : IVulkanHandle<AccelerationStructure>, IDisposable
{
    public   readonly VkAccelerationStructureKHR_T* Handle;
    internal readonly VkDevice_T*                   DeviceHandle;
    private  readonly delegate* unmanaged[Stdcall]<
        VkDevice_T*, VkAccelerationStructureKHR_T*, VkAllocationCallbacks*, void> _destroy;
    private  readonly ulong                         _size;

    public static VkObjectType ObjectType
        => VkObjectType.VK_OBJECT_TYPE_ACCELERATION_STRUCTURE_KHR;
    public static AccelerationStructure FromRaw(nint handle); // borrowed: device/destroy null, size 0
    public ulong RawHandle  { get; }
    public bool  IsNull     { get; }
    public bool  OwnsHandle => DeviceHandle != null;
    public ulong Size       => _size;   // 0 on a borrowed handle == unknown
    public ulong GetDeviceAddress(Device device);
    public void  Dispose();
}
```

The stored `_destroy` function pointer is the answer to Evidence §7: the handle
must dispatch an *extension* destroy from `Dispose()`, and copying the one
pointer at creation keeps the struct **unmanaged** — which matters, because
`CommandRecorder.WriteAccelerationStructuresProperties` takes a
`ReadOnlySpan<AccelerationStructure>` and callers will `stackalloc` those. A
managed `Device` field would forfeit that. `_size` follows the
`QueryPool.QueryCount` convention: 0 means *unknown*, never *empty* (a
zero-sized acceleration structure cannot be created).

`GetDeviceAddress(Device)` mirrors `Buffer.GetDeviceAddress(Device)` rather than
storing a second pointer — this is the accessor an instance descriptor needs.

### C. Creation, build sizes, and where the scratch alignment comes from

**Creation** — an explicit-parameter method on `Device`, no description struct
(see "why not the alternatives"):

```csharp
public AccelerationStructure CreateAccelerationStructure(
    AccelerationStructureType type, in Buffer buffer, ulong offset, ulong size);
```

Guards, all unconditional (setup-time, `CreateQueryPool`'s precedent at
`Lifecycle/Device.cs:585-587`):

| Condition | Throw |
|---|---|
| `Functions.CreateAccelerationStructure == null` | `InvalidOperationException` + `EnableInstructions` |
| `buffer.IsNull` | `ArgumentException` |
| `size == 0` | `ArgumentOutOfRangeException` |
| `offset % 256 != 0` | `ArgumentException` (`VUID-VkAccelerationStructureCreateInfoKHR-offset-03734`) |
| `buffer.Size != 0 && offset + size > buffer.Size` | `ArgumentOutOfRangeException` |
| `buffer.Usage != None && !buffer.Usage.HasFlag(AccelerationStructureStorage)` | `ArgumentException` (`VUID-VkAccelerationStructureCreateInfoKHR-buffer-03614`) |

The last two are checkable **only** because `Buffer` caches `Size` and `Usage`
(`Resources/Buffer.cs:32-33`); borrowed buffers report 0/`None`, which the
guards read as *unknown* and skip, mirroring `QueryPool.AssertRangeInBounds`.

**Build sizes** — on `Device`, mapping `vkGetAccelerationStructureBuildSizesKHR`
with `buildType = VK_ACCELERATION_STRUCTURE_BUILD_TYPE_DEVICE_KHR`:

```csharp
public AccelerationStructureBuildSizes GetAccelerationStructureBuildSizes(
    AccelerationStructureType                   type,
    AccelerationStructureBuildFlags             flags,
    ReadOnlySpan<AccelerationStructureGeometry> geometries,
    ReadOnlySpan<uint>                          maxPrimitiveCounts);
```

returning `Memory/AccelerationStructureBuildSizes.cs`:

```csharp
public readonly record struct AccelerationStructureBuildSizes
{
    public ulong AccelerationStructureSize { get; init; } // → the backing buffer range
    public ulong BuildScratchSize          { get; init; } // → scratch for Mode.Build
    public ulong UpdateScratchSize         { get; init; } // → scratch for Mode.Update
}
```

No `mode`, no `source`, no `destination` parameter: the spec has
`vkGetAccelerationStructureBuildSizesKHR` ignore `srcAccelerationStructure`,
`dstAccelerationStructure` and `scratchData`, and both scratch sizes come back
from one call. It lives in `Memory/` because it *is* a memory-requirements
record, next to `MemoryRequirements`.

**Scratch alignment** — surfaced as a narrow projection on `PhysicalDevice`, the
`MeshShaderLimits` shape:

```csharp
public bool TryGetAccelerationStructureLimits(out AccelerationStructureLimits limits);

// Lifecycle/AccelerationStructureLimits.cs
public readonly record struct AccelerationStructureLimits
{
    public uint  MinScratchOffsetAlignment { get; init; } // the one every build must obey
    public ulong MaxGeometryCount          { get; init; }
    public ulong MaxInstanceCount          { get; init; }
    public ulong MaxPrimitiveCount         { get; init; }
    public uint  MaxPerStageDescriptorAccelerationStructures { get; init; }
    public uint  MaxDescriptorSetAccelerationStructures      { get; init; }
}
```

gated on `VulkanExtensions.KhrAccelerationStructure` through the existing
`TryGetProperties<T>(Utf8Name, out T)`. Readable **before** `CreateDevice`, so a
picker can reject a GPU on `MaxInstanceCount`, exactly as
`TryGetMeshShaderLimits` is.

The alignment is therefore *not* folded into `AccelerationStructureBuildSizes`:
it is a device constant, not a per-build result, and folding it would make the
size query issue a second (chained) native query on a path a per-frame TLAS
rebuild can take. Instead, the XML doc on `BuildScratchSize`/`UpdateScratchSize`
and on `AccelerationStructureBuild.ScratchAddress` both point at
`MinScratchOffsetAlignment` and state the rule: **the scratch device address
passed to a build must be a multiple of `MinScratchOffsetAlignment`**, and the
scratch buffer must be created with
`BufferUsage.StorageBuffer | BufferUsage.ShaderDeviceAddress`.

### D. Builds: one plural method over a CSR span triple

```csharp
public void BuildAccelerationStructures(
    ReadOnlySpan<AccelerationStructureBuild>      builds,
    ReadOnlySpan<AccelerationStructureGeometry>   geometries,
    ReadOnlySpan<AccelerationStructureBuildRange> ranges);
```

`AccelerationStructureBuild` (in `Recording/`) carries the per-build state plus a
**`(FirstGeometry, GeometryCount)` slice into the two flat spans** — the
compressed-sparse-row encoding:

```csharp
public readonly struct AccelerationStructureBuild
{
    public AccelerationStructureType       Type           { get; init; }
    public AccelerationStructureBuildFlags Flags          { get; init; }
    public AccelerationStructureBuildMode  Mode           { get; init; } // Build | Update
    public AccelerationStructure           Source         { get; init; } // Mode.Update only
    public AccelerationStructure           Destination    { get; init; }
    public ulong                           ScratchAddress { get; init; }
    public uint                            FirstGeometry  { get; init; }
    public uint                            GeometryCount  { get; init; }
}
```

`geometries[i]` and `ranges[i]` correspond one-to-one, which is what Vulkan
requires (`ppBuildRangeInfos[b]` is an array of `geometryCount` range structs),
so **one** slice indexes both. This is the whole reason for the CSR shape: it
batches N builds with no span-of-spans (illegal — `ReadOnlySpan<T>` cannot have a
`ref struct` `T`), no allocation, and no wrapper-owned scratch object. The caller
owns all three spans; every one can be a `stackalloc`, a pooled array, or a
long-lived field.

`AccelerationStructureGeometry` (in `Recording/`) is a flat tagged record with
static factories, all addresses being `VkDeviceAddress` values the caller got
from `Buffer.GetDeviceAddress`:

```csharp
public static AccelerationStructureGeometry Triangles(
    ulong vertexAddress, VkFormat vertexFormat, ulong vertexStride, uint maxVertex,
    ulong indexAddress = 0, VkIndexType indexType = VkIndexType.VK_INDEX_TYPE_NONE_KHR,
    ulong transformAddress = 0, GeometryFlags flags = GeometryFlags.Opaque);

public static AccelerationStructureGeometry Aabbs(
    ulong address, ulong stride, GeometryFlags flags = GeometryFlags.Opaque);

public static AccelerationStructureGeometry Instances(
    ulong address, bool arrayOfPointers = false, GeometryFlags flags = GeometryFlags.None);
```

`VkFormat` / `VkIndexType` appear in the public signature because the wrapper
already does that elsewhere (`GraphicsPipelineBuilder.WithDynamicRendering` takes
`ReadOnlySpan<VkFormat>`); re-shadowing 200 formats is not on the table.

`AccelerationStructureBuildRange` (in `Recording/`) is an **exact layout mirror**
of `VkAccelerationStructureBuildRangeInfoKHR` — four `uint`s in order
`PrimitiveCount, PrimitiveOffset, FirstVertex, TransformOffset` — so the recorder
casts the caller's span in place rather than copying it, the
`BufferDescriptorWrite` trick (`Pools/DescriptorWrite.cs:31-35`). A test pins the
size and each field offset.

**Translation and allocation.** An internal
`Internal/AccelerationStructureBuildTranslator` (shared by the recorder and by
`Device.GetAccelerationStructureBuildSizes`, the `DescriptorWriteBuilder`
relationship) fills, per call:

- `VkAccelerationStructureBuildGeometryInfoKHR[builds.Length]`
- `VkAccelerationStructureGeometryKHR[geometries.Length]`
- `VkAccelerationStructureBuildRangeInfoKHR*[builds.Length]` — pointers into the
  caller's pinned `ranges` span, no copy

`stackalloc` when `builds.Length ≤ 8 && geometries.Length ≤ 16` (the per-frame
TLAS case is 1 and 1, so it always stackallocs: ~2.2 KB worst case on the stack
path), `ArrayPool<T>.Shared` rentals in `try/finally` otherwise — the `FlushPush`
shape at `Recording/CommandRecorder.cs:441-459`. Zero GC allocation on both
paths.

**Validation-gated guards** (behind `AhjoValidation.IsEnabled`, since this is the
per-frame path — `Recording/CommandRecorder.cs:985` precedent):
`ranges.Length == geometries.Length`; every build's slice inside `geometries`;
`Destination` non-null; `Source` non-null iff `Mode.Update`; a top-level build
having exactly one `Instances` geometry and a bottom-level build having none.
That last pair is not decoration — `AccelerationStructureType` keeps native
numbering (`TopLevel = 0`), so `default` is `TopLevel` and a caller who forgets
`Type = BottomLevel` would otherwise silently build the wrong thing.

The unconditional guard is the null-pointer one:
`if (fn == null) ThrowAccelerationStructureUnsupported();`, matching
`DrawMeshTasks` (`:610-612`) and **not** behind `AhjoValidation`, because
`AhjoValidation.IsEnabled` is false in Release, which is the build where a null
dispatch is an access violation.

### E. Compaction, composed onto the #199 query pool

```csharp
public enum QueryType                          // Sync/QueryType.cs
{
    Unknown                            = 0,           // borrowed/default pool: type not known
    Timestamp                          = 2,           // VK_QUERY_TYPE_TIMESTAMP
    AccelerationStructureCompactedSize = 1000150000,  // VK_QUERY_TYPE_..._COMPACTED_SIZE_KHR
}

public QueryPool Device.CreateQueryPool(QueryType type, uint queryCount); // new overload
public QueryPool Device.CreateQueryPool(uint queryCount);                 // unchanged ⇒ Timestamp
public QueryType QueryPool.Type { get; }                                  // Unknown when borrowed

public void CommandRecorder.WriteAccelerationStructuresProperties(
    ReadOnlySpan<AccelerationStructure> structures, in QueryPool pool, uint firstQuery);

public void CommandRecorder.CopyAccelerationStructure(
    in AccelerationStructure source, in AccelerationStructure destination,
    AccelerationStructureCopyMode mode);   // Clone = 0 | Compact = 1
```

`QueryType.Unknown = 0` is legitimate rather than a fudge: `VkQueryType` 0 is
`VK_QUERY_TYPE_OCCLUSION`, which this wrapper does not create, so 0 is free to
carry the "borrowed handle, type unknown" meaning that `QueryPool.QueryCount`'s 0
already carries for size. The other two members keep native values, so the cast
to `VkQueryType` is free and a drift test can pin both.

`WriteAccelerationStructuresProperties` takes **no** `queryType` parameter — it
reads `pool.Type`, which removes by construction the mismatch between the pool's
type and the command's `queryType` that Vulkan forbids. It throws unconditionally
on a borrowed pool (type unknown, nothing valid to pass), the
`QueryPool.ThrowIfBorrowed` precedent (`Sync/QueryPool.cs:201-208`), and
validation-gated when `pool.Type` is not an acceleration-structure query type or
the range overruns `pool.QueryCount`. The handle array is a `stackalloc nint[n]`
for `n ≤ 8` / `ArrayPool<nint>` beyond, cast to `VkAccelerationStructureKHR_T**`.

`Device.CreateQueryPool(QueryType.AccelerationStructureCompactedSize, n)` throws
when `Functions.CmdWriteAccelerationStructuresProperties == null` — the pool
would be useless, and creating it is itself a valid-usage violation without the
`accelerationStructure` feature.

**The compaction flow, which the XML docs spell out end to end:**

1. Build the BLAS with `AccelerationStructureBuildFlags.AllowCompaction`.
2. Barrier `Stage.AccelerationStructureBuild`/`Access.AccelerationStructureWrite`
   → `Stage.AccelerationStructureBuild`/`Access.AccelerationStructureRead`.
3. `ResetQueryPool` (a compacted-size query must be reset before it is written,
   exactly as a timestamp must), then
   `WriteAccelerationStructuresProperties(blases, pool, 0)`.
4. Submit; wait on the fence.
5. `pool.GetResults(0, sizes)` — each value is the compacted size **in bytes**.
6. Allocate a new buffer of that size, `CreateAccelerationStructure` over it,
   `CopyAccelerationStructure(old, new, AccelerationStructureCopyMode.Compact)`,
   submit, wait.
7. Only now dispose the original acceleration structure and free its buffer.

Everything in that list except steps 5–7 is recorded through methods this design
adds; step 5 and the fence wait are #199's surface unchanged.

### F. Four sync2 bits

`Recording/Stage.cs` gains `AccelerationStructureBuild = 0x02000000` and
`AccelerationStructureCopy = 0x10000000`; `Recording/Access.cs` gains
`AccelerationStructureRead = 0x00200000` and
`AccelerationStructureWrite = 0x00400000`. Two new drift facts pin all four
against the `Vk.VK_*` constants (`Generated/Vk.cs:727`, `:763`, `:967`, `:970`).

`Stage.RayTracingShader` (`0x00200000`, `Generated/Vk.cs:730`) is deliberately
**not** added: it is the ray-tracing-*pipeline* stage, and a ray-query traversal
executes in whatever shader stage runs it, so a ray-query consumer barriers
against `Stage.ComputeShader` / `Stage.FragmentShader` with
`Access.AccelerationStructureRead`. Adding the bit would invite the wrong
barrier.

### G. TLAS descriptor writes

`DescriptorWrite` gains a third `Kind` and a factory; the payload is the
acceleration-structure handle stored inline:

```csharp
public static DescriptorWrite AccelerationStructure(
    uint binding, uint arrayElement, in AccelerationStructure structure);
```

No `VkDescriptorType` parameter — `VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR`
is the only type this write can have, unlike the buffer and image factories which
cover several.

`DescriptorWriteBuilder.BuildWrites` takes an additional
`Span<VkWriteDescriptorSetAccelerationStructureKHR> chains` of the same length
and, for an AS write, fills `chains[i]` with `accelerationStructureCount = 1` and
`pAccelerationStructures` pointing at the handle field *inside the caller's
pinned `writes` span* — the `Unsafe.AsPointer(ref Unsafe.AsRef(in w._buffer))`
idiom already at `Pools/DescriptorWriteBuilder.cs:47-48` — then sets
`dst[i].pNext = &chains[i]` with both info pointers null. Both call sites
(`Pools/DescriptorSetExtensions.cs:33-48`, `Recording/CommandRecorder.cs:441-459`)
carve the `chains` span alongside the `VkWriteDescriptorSet` span, by the same
`≤ 8 ? stackalloc : ArrayPool` rule.

This is the one part of the design that touches an existing benchmarked hot path,
so `PushDescriptors.*` is re-measured (see Benchmarks). It is also the one part a
reviewer could reasonably ask to split out — see **OPEN-1**.

### H. Ownership and lifetime — the contract

This is the part with the most ways to be silently wrong, so it is stated as
rules, each with the reason it is a rule.

**H1. The acceleration structure does not own its backing buffer. Ever.**
`Device.CreateAccelerationStructure` takes `in Buffer buffer, ulong offset,
ulong size` and stores neither the buffer nor the allocator. Three reasons:
Vulkan's own model binds an acceleration structure to a *range* of a
caller-supplied buffer, and suballocating hundreds of BLASes into a handful of
large buffers is the standard (and, at Louhi scale, mandatory) pattern —
one-buffer-per-AS would waste memory against the 256-byte offset alignment and
multiply VMA allocations; `Buffer` is a VMA-backed handle already owned by an
`Allocator` (`Resources/Buffer.cs:29-33`), so an AS that owned it would be
inventing a second owner; and the repo's handle contract is copy-by-value with
deterministic `Dispose` and no finalizers (`Internal/IVulkanHandle.cs:38-42`),
which cannot express shared ownership of a sub-range. **The caller must keep the
buffer alive strictly longer than the acceleration structure, and must not let a
second acceleration structure or any other resource alias the same range.**

**H2. Scratch is caller-owned, always, and is passed as an address, not a
handle.** `AccelerationStructureBuild.ScratchAddress` is a `VkDeviceAddress`
because that is what `VkAccelerationStructureBuildGeometryInfoKHR::scratchData`
is. The recorder never allocates, sizes, suballocates or recycles scratch: how
scratch is reused across builds and across frames is a frame-graph decision the
wrapper cannot see. Caller obligations: size it from
`AccelerationStructureBuildSizes.BuildScratchSize` (or `UpdateScratchSize` for
`Mode.Update`); align the address to
`AccelerationStructureLimits.MinScratchOffsetAlignment`; create the buffer with
`StorageBuffer | ShaderDeviceAddress`; **give every build in one
`BuildAccelerationStructures` call a non-overlapping scratch range**, because
builds within one call may execute concurrently; and keep the scratch alive and
untouched by anything else until the build completes on the GPU.

**H3. Build inputs are invisible to the wrapper.** Vertex, index, transform,
AABB and instance data all reach Vulkan as `ulong` device addresses. The wrapper
cannot check them, cannot keep them alive, and cannot tell you that you passed an
address from a buffer you already disposed. They must be alive, resident and
unmodified from submission until the build completes.

**H4. What must outlive an in-flight build.** Destination AS **and its buffer**;
source AS **and its buffer** for `Mode.Update`; the scratch range; every input
buffer behind an address in the geometry span; and, for compaction, the query
pool.

**H5. Dispose while a build is in flight is undefined behavior, and the wrapper
does not stop you.** `AccelerationStructure.Dispose()` calls
`vkDestroyAccelerationStructureKHR` unconditionally for an owning handle, the
same policy `Event.Dispose` (`Sync/Event.cs:67-77`) and `QueryPool.Dispose`
(`Sync/QueryPool.cs:74-83`) already state — *"do not dispose while a submission
that references it is still pending"*. There is no submission tracking anywhere
in this repo and adding one for this type alone would be a new lifetime model.
`VK_LAYER_KHRONOS_validation` catches destroy-in-use for the handle; the
`AHJO_VULKAN_TIER=validation` lane is where that gets caught.

**H6. The hazard validation cannot catch: dangling BLAS addresses inside a
TLAS.** A TLAS's instance entries carry `accelerationStructureReference`, a plain
`uint64` device address obtained from `AccelerationStructure.GetDeviceAddress`.
Once written into the instance buffer it is **just a number** — no layer, no
driver and no tool can tell that the BLAS behind it was destroyed. Destroying or
recreating a BLAS while a TLAS references it produces traversal reads of freed
memory with no diagnostic at all. The rule, stated in the XML docs on
`GetDeviceAddress` and on `AccelerationStructureGeometry.Instances`: **every BLAS
must outlive every TLAS built over it, and a TLAS must be fully rebuilt
(`Mode.Build`, not `Mode.Update`) after any referenced BLAS is destroyed,
recreated, or compacted** — compaction moves the BLAS to a new buffer and
therefore changes its device address.

**H7. Compaction ordering.** The source acceleration structure and its buffer
stay alive until the compaction copy has completed on the GPU; only then may
either be disposed. The compacted destination has a different device address
(H6).

### Why not the alternatives

- **Defer BLAS compaction to a later cut** (the issue's own open question) —
  overridden by the maintainer before this spec, and the audit agrees it is
  cheap: readback, reset and bounds-checking are #199's code unchanged, and the
  marginal surface is one enum, one `CreateQueryPool` overload, one `QueryPool`
  property and two recorder forwards.
- **A dedicated `CompactedSizeQueryPool` type instead of widening `QueryPool`** —
  rejected: it would duplicate three readback methods, the borrowed-handle guard
  and the bounds guard verbatim to change one field of `VkQueryPoolCreateInfo`,
  and every future query type would clone them again.
- **Passing `queryType` to `WriteAccelerationStructuresProperties`** — rejected:
  the pool already knows its type, and a parameter creates a mismatch Vulkan
  forbids and the wrapper would then have to check.
- **A stateful, reusable `AccelerationStructureBuildBatch` class owning pinned
  native arrays** — rejected: it achieves the same zero-allocation result as the
  CSR span triple while adding a lifetime-owning type to a repo whose recording
  surface is deliberately span-in/no-state. Kept on record as the escape hatch if
  the translation ever shows up in a profile.
- **Spans of spans (`ReadOnlySpan<AccelerationStructureBuildInfo>` where the info
  carries geometry/range spans)** — rejected because it does not compile: a type
  with `ReadOnlySpan<T>` fields is a `ref struct`, and `ReadOnlySpan<T>` cannot
  be instantiated over one.
- **Exposing `ReadOnlySpan<VkAccelerationStructureBuildGeometryInfoKHR>` directly
  and doing no translation** — rejected: it hands the caller the `sType`
  boilerplate, the `pGeometries`-vs-`ppGeometries` choice, the
  `VkDeviceOrHostAddressKHR` unions and the `ppBuildRangeInfos`
  double-indirection — i.e. exactly what the wrapper exists to absorb — while
  saving a memcpy of ~96 bytes per geometry on a path that is one geometry deep
  per frame.
- **Also shipping a singular `BuildAccelerationStructure` convenience** —
  rejected for the first cut: it would either duplicate the CSR fields with
  different meanings or silently override `FirstGeometry`/`GeometryCount`, and
  the single-build call is `stackalloc AccelerationStructureBuild[1]`. Purely
  additive later.
- **An `AccelerationStructureDescription` record struct for
  `CreateAccelerationStructure`** — rejected: it would contain a `Buffer`, so the
  synthesized record equality would route through
  `EqualityComparer<Buffer>.Default` → `ValueType.Equals` field-walking on a type
  that deliberately has no equality (`Resources/Buffer.cs:22-26`). Four explicit
  parameters have none of that and read no worse.
- **Storing `Type` on the `AccelerationStructure` handle so builds can be
  cross-checked against the destination** — rejected on a numbering collision:
  `VK_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL_KHR` is 0, so a zeroed field could
  not distinguish "top level" from "borrowed handle, unknown", and renumbering
  the shadow away from native values would break the repo-wide shadow-enum
  convention and its drift tests for one guard. The TLAS-geometry-kind guard in
  §D catches the same class of mistake without the field.
- **Storing a managed `Device` reference on the handle instead of the destroy
  function pointer** — rejected: it would make `AccelerationStructure` a managed
  type, which forfeits `stackalloc AccelerationStructure[n]` in
  `WriteAccelerationStructuresProperties` and puts a GC reference into a handle
  the contract describes as raw pointers plus creation-time metadata
  (`Internal/IVulkanHandle.cs:14-21`).
- **Making the acceleration structure allocate and own its own buffer** —
  rejected in H1; the short version is that it forces one VMA allocation per BLAS
  and invents a second owner for memory the `Allocator` already owns.
- **Letting the recorder allocate scratch from a wrapper-owned pool** — rejected:
  scratch reuse is a frame-graph decision, the wrapper has no frame boundary, and
  a hidden allocation on the per-frame recording path is exactly what
  `Recording/**`'s zero-allocation rule forbids.
- **A wrapper `AccelerationStructureInstance` struct mirroring
  `VkAccelerationStructureInstanceKHR`** — rejected: that struct is 64 bytes of
  bitfields (`Generated/VkAccelerationStructureInstanceKHR.cs`, two packed
  `uint`s with generated accessors), and hand-copying a bitfield layout is
  precisely the drift class `ShadowEnumDriftTests` exists to prevent. Callers
  write instance data using the generated struct, which the wrapper already does
  for `VkFormat`, `VkDescriptorType` and the properties-chain query. Same
  reasoning for `VkAabbPositionsKHR`.
- **Adding `Stage.RayTracingShader`** — rejected in §F: it is the RT-pipeline
  stage, and offering it to a ray-query consumer invites a barrier that
  synchronizes nothing they run.
- **Reflection- or attribute-driven geometry translation** — never on the table;
  AOT (`src/Ahjo.Vulkan/CLAUDE.md`).
- **Host builds (`vkBuildAccelerationStructuresKHR`) and deferred host
  operations** — out of scope: they need the
  `accelerationStructureHostCommands` feature (rare on desktop drivers) and a
  `VkDeferredOperationKHR` lifetime model of their own. Additive later; nothing
  here precludes them.
- **Serialization / deserialization
  (`vkCmdCopyAccelerationStructureToMemoryKHR`,
  `vkCmdCopyMemoryToAccelerationStructureKHR`,
  `VK_QUERY_TYPE_ACCELERATION_STRUCTURE_SERIALIZATION_SIZE_KHR`)** — out of
  scope: that is an AS-disk-cache feature with its own versioning-compatibility
  handshake (`vkGetDeviceAccelerationStructureCompatibilityKHR`,
  `Generated/Vk.cs:3180`). The `QueryType` enum and
  `AccelerationStructureCopyMode` both extend additively.
- **Indirect builds (`vkCmdBuildAccelerationStructuresIndirectKHR`,
  `Generated/Vk.cs:3146`)** — out of scope: they need the
  `accelerationStructureIndirectBuild` feature and a GPU-resident
  `maxPrimitiveCounts` array. Noted because a cluster follow-up is indirect-only
  (Evidence §10), so this is the piece a cluster path would build *next to*, not
  *on top of*.
- **A `HelloRayQuery` sample** — rejected for this issue, the #201 precedent: no
  CI host is ray-tracing-capable, and samples run in the AOT-smoke and headless
  lanes.

## Invariants honored

- **UTF-8 literals.** Three extension names and seven entry-point names are
  `"…"u8` in `Internal/DeviceExtensionNames.cs`, reaching Vulkan through
  `Utf8Name.FromLiteral`. No `Encoding.UTF8.GetBytes` anywhere.
- **Native AOT.** Seven more `delegate* unmanaged[Stdcall]` fields, one stored
  function pointer on a struct, flat struct translation, one more generic
  instantiation of the existing `TryGetProperties<T>` (a static-abstract
  constrained generic ILC already compiles for
  `VkPhysicalDeviceMeshShaderPropertiesEXT`). No reflection, no dynamic code, no
  new trim surface. The `record struct`-containing-a-handle trap is avoided by
  design (see "why not").
- **Zero per-frame allocations.** `BuildAccelerationStructures` stackallocs at
  the sizes a per-frame TLAS rebuild uses and rents from `ArrayPool` only for the
  load-time batch case; `WriteAccelerationStructuresProperties` and
  `CopyAccelerationStructure` are load-time but written to the same rule;
  `GetAccelerationStructureBuildSizes` and the limits read are setup-time and
  stack-only. The descriptor-write change adds a stack span to a path already
  measured at 0 B/op, which the benchmark re-checks.
- **Generated code untouched.** Every entry point, struct, union, enum and
  chainable this design needs already exists (Problem, Evidence §1, §6). No rsp
  change, no regen.
- **`TreatWarningsAsErrors`.** No suppressions. `DeviceFunctionTable` and
  `Device` constructors are `internal`, so no public signature moves;
  `DescriptorWriteBuilder` is `internal`, so its signature change is confined to
  its two call sites; `Device.CreateQueryPool(uint)` keeps its meaning and every
  existing call site.

## Test strategy (constrained by #32 and CI having no RT-capable GPU)

**Tier 1 — driver-free.** Layout mirror (`AccelerationStructureBuildRange` size
and four field offsets against `VkAccelerationStructureBuildRangeInfoKHR`);
`QueryType` and the four new `Stage`/`Access` bits against their native
constants; `AccelerationStructureGeometry` factories producing the expected
kind/flags/addresses; `AccelerationStructure.FromRaw`/`default` reporting
`OwnsHandle == false`, `Size == 0` and a no-op `Dispose`; the new
`HandleConventionsTests.BorrowContract_HoldsForEveryHandleType` entry.

**Tier 2 — `[gate:driver]` only** (any host with an ICD, RT-capable or not). The
whole extension-not-enabled surface: `CreateAccelerationStructure`,
`GetAccelerationStructureBuildSizes`, `BuildAccelerationStructures`,
`WriteAccelerationStructuresProperties`, `CopyAccelerationStructure` and
`CreateQueryPool(AccelerationStructureCompactedSize, …)` each throwing an
`InvalidOperationException` naming `VK_KHR_acceleration_structure` on a device
created without it. Plus every argument guard from §C, which runs before any
native call (`Buffer.FromRaw` supplies a non-null handle with no driver
involvement, the `ShaderModule.FromRaw` trick from #201). Plus
`DescriptorWrite.AccelerationStructure` producing a write whose native form has a
non-null `pNext`, null `pBufferInfo`/`pImageInfo` and
`descriptorType == VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR`, and
`TryGetAccelerationStructureLimits` returning `false` (not throwing) on a GPU
that does not advertise the extension.

**Tier 3 — `[gate:feature]`, maintainer host only.** Build a BLAS from a
triangle, read its device address, build a TLAS over one instance, and — the full
compaction round trip — build with `AllowCompaction`, query the compacted size,
assert it is non-zero and `≤` the original
`AccelerationStructureBuildSizes.AccelerationStructureSize`, compact into a new
structure, and confirm the compacted structure's device address differs from the
original's (the H6 rule, made executable). A CI run reporting all of Tier 3 as
`[gate:feature]` is the expected outcome, not a gap to fix.

## Benchmarks

Two obligations, both on `Recording/`:

1. **New coverage — `AccelerationStructureBenchmarks`** (its own class, the
   `MeshShaderBenchmarks` precedent, so a host without RT cannot take the #29
   canary down): `BuildAccelerationStructures` recording the one-build /
   one-geometry TLAS-rebuild shape N times into one command buffer, expecting `-`
   in `Allocated`. This is the per-frame path and the only new hot-path method.
2. **Regression check — `PushDescriptors.*`** must still read `-` and stay in its
   recorded band after the `chains` span is added to `BuildWrites` and both call
   sites (`docs/benchmarks.md`). This is the only existing measured path the
   design touches.

`docs/benchmarks.md` gets the new rows plus the driver-dependency caveat the mesh
rows already carry.

## Uncertainty, stated

- ~~**VUID numbers are recalled, not verified.**~~ **Resolved 2026-08-23.**
  Every VUID cited in this spec and in the plan was checked against
  `native/downloaded/Vulkan-Headers-vulkan-sdk-1.4.341.0/registry/validusage.json`
  (api version 1.4.341) during the plan's Step 0. All were correct as written;
  no guard is contradicted by the registry. The one refinement is that
  "top level ⇒ exactly one `Instances` geometry" is two VUIDs rather than one
  (`VUID-VkAccelerationStructureBuildGeometryInfoKHR-type-03789` for the kind,
  `-type-03790` for the count). The full verification table is in the plan's
  Step 0.
- **The stack thresholds (8 builds / 16 geometries) are reasoned, not
  measured.** They cover the per-frame TLAS case (1/1) with three orders of
  magnitude of headroom and put load-time BLAS batches on `ArrayPool`. If a
  consumer turns up that batches ~64 builds per frame, the numbers should move
  with a measurement behind them.
- **The one decision a cluster follow-up would have to rebuild** is the
  assumption that a build's inputs are CPU-side geometry descriptors:
  `VK_NV_cluster_acceleration_structure` builds are indirect-only, driven from
  GPU-resident command buffers. That is not a corner this design paints itself
  into — an indirect build path is a *sibling* method
  (`vkCmdBuildAccelerationStructuresIndirectKHR` has the same relationship) that
  reuses the handle, the sizes type and the ownership model unchanged. Recorded
  because the issue asked.
- **Whether `arrayOfPointers = true` instance data is worth supporting** is not
  something the audit can answer — no consumer exists. It is a `bool` on one
  factory, so it ships; if it turns out nobody uses it, removing it is a one-line
  deprecation.

## Cross-links

- **Resolves** #202.
- **Builds directly on** #201 (`DeviceFunctionTable` gated block, the
  `PhysicalDevice.TryGetProperties<T>` limits read) and #198/#199 (`QueryPool`,
  `ResetQueryPool`, the readback methods).
- **Must land consistently with** the handle-ownership decision in #118
  (`Internal/IVulkanHandle.cs`) — `AccelerationStructure` is the next handle type
  and takes an explicit entry in
  `HandleConventionsTests.BorrowContract_HoldsForEveryHandleType`.
- **Constrained by** #32 (wrapper tests are Windows-only; no RT-capable CI host)
  and #158 (`AHJO_VULKAN_TIER`, the validation lane that catches the
  destroy-in-use case H5 leaves to the layer).
- **Does not preclude, and is deliberately shaped not to block**: ray-tracing
  pipelines + SBTs, host builds, indirect builds, AS serialization, and
  `VK_NV_cluster_acceleration_structure` (Evidence §10).
