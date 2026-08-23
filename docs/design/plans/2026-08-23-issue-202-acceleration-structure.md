Paired with [`../specs/2026-08-23-issue-202-acceleration-structure-design.md`](../specs/2026-08-23-issue-202-acceleration-structure-design.md).

# Implementation plan — issue #202: acceleration-structure surface (KHR AS + ray query)

Branch: `issue-202-acceleration-structure` (cut from `main` at `a5ca697`).

Nothing under `src/*/Generated/`, `native/` or `tools/*.rsp` is touched. Every
entry point, struct, union and chainable already exists.

Read the spec's §H (ownership and lifetime) before writing any XML doc — the
doc comments are where that contract lives, and they are not optional
decoration.

## Step 0 — VUID verification pass (do this first, it feeds every message)

Every VUID number in the spec and in this plan was written from recall, not
from the registry. Before writing any error string, verify each against the
current Vulkan spec / `validusage.json` and correct it here and in the spec:

- `VUID-VkAccelerationStructureCreateInfoKHR-offset-03734` (offset multiple of 256)
- `VUID-VkAccelerationStructureCreateInfoKHR-buffer-03614` (buffer usage bit)
- `VUID-VkAccelerationStructureCreateInfoKHR-offset-03616` (offset + size ≤ buffer size)
- the `vkCmdBuildAccelerationStructuresKHR` scratch-alignment VU (scratch address
  multiple of `minAccelerationStructureScratchOffsetAlignment`)
- the `VkAccelerationStructureBuildGeometryInfoKHR` VUs for "top level ⇒
  `geometryCount == 1` and `geometryType == INSTANCES`" and "bottom level ⇒ not
  `INSTANCES`"
- `vkCmdWriteAccelerationStructuresPropertiesKHR`: queries must be unavailable
  (reset), and the structures must have been built with `ALLOW_COMPACTION` for
  the compacted-size query type
- `vkCmdCopyAccelerationStructureKHR` / `VkCopyAccelerationStructureInfoKHR`:
  `mode` must be `COMPACT` or `CLONE`; `COMPACT` requires the source built with
  `ALLOW_COMPACTION`
- `vkDestroyAccelerationStructureKHR`: all submitted commands referencing it must
  have completed

**If a number cannot be confirmed, write the rule in prose and drop the number.**
A wrong VUID in a message is worse than none.

### Step 0 result — verified 2026-08-23 against `native/downloaded/Vulkan-Headers-vulkan-sdk-1.4.341.0/registry/validusage.json` (api version 1.4.341)

Every recalled number is **correct as written**. No guard is contradicted; no
number needed changing. The registry text for each:

| Rule | VUID | Registry wording |
|---|---|---|
| offset multiple of 256 | `VUID-VkAccelerationStructureCreateInfoKHR-offset-03734` | "`offset` must be a multiple of `256` bytes" |
| buffer usage bit | `VUID-VkAccelerationStructureCreateInfoKHR-buffer-03614` | "`buffer` must have been created with the `VK_BUFFER_USAGE_ACCELERATION_STRUCTURE_STORAGE_BIT_KHR` usage flag set" |
| offset + size ≤ buffer size | `VUID-VkAccelerationStructureCreateInfoKHR-offset-03616` | "The sum of `offset` and `size` must be less than or equal to the size of `buffer`" |
| scratch address alignment | `VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03710` | "its `scratchData.deviceAddress` member must be a multiple of `VkPhysicalDeviceAccelerationStructurePropertiesKHR::minAccelerationStructureScratchOffsetAlignment`" |
| top level ⇒ geometry is `Instances` | `VUID-VkAccelerationStructureBuildGeometryInfoKHR-type-03789` | "If `type` is `..._TOP_LEVEL_KHR`, the `geometryType` member … must be `VK_GEOMETRY_TYPE_INSTANCES_KHR`" |
| top level ⇒ exactly one geometry | `VUID-VkAccelerationStructureBuildGeometryInfoKHR-type-03790` | "If `type` is `..._TOP_LEVEL_KHR`, `geometryCount` must be 1" |
| bottom level ⇒ not `Instances` | `VUID-VkAccelerationStructureBuildGeometryInfoKHR-type-03791` | "If `type` is `..._BOTTOM_LEVEL_KHR` the `geometryType` member … must not be `VK_GEOMETRY_TYPE_INSTANCES_KHR`" |
| `Mode.Update` ⇒ source non-null | `VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-04630` | "if its `mode` member is `..._UPDATE_KHR`, its `srcAccelerationStructure` member must not be `VK_NULL_HANDLE`" |
| `Mode.Update` ⇒ source built with `AllowUpdate` | `VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03667` | "…must have previously been constructed with `VK_BUILD_ACCELERATION_STRUCTURE_ALLOW_UPDATE_BIT_KHR`" |
| scratch ranges non-overlapping across builds | `VUID-vkCmdBuildAccelerationStructuresKHR-scratchData-03704` | "…must not overlap the memory backing the `scratchData` member of any other element of `pInfos`" |
| build outside a rendering scope | `VUID-vkCmdBuildAccelerationStructuresKHR-renderpass` | "This command must only be called outside of a render pass instance" |
| build needs a compute-capable pool | `VUID-vkCmdBuildAccelerationStructuresKHR-commandBuffer-cmdpool` | "…must support `VK_QUEUE_COMPUTE_BIT` operations" |
| queries must be unavailable (reset) | `VUID-vkCmdWriteAccelerationStructuresPropertiesKHR-queryPool-02494` | "The queries identified by `queryPool` and `firstQuery` must be unavailable" |
| pool type matches `queryType` | `VUID-vkCmdWriteAccelerationStructuresPropertiesKHR-queryPool-02493` | "`queryPool` must have been created with a `queryType` matching `queryType`" |
| `firstQuery` + count ≤ pool size | `VUID-vkCmdWriteAccelerationStructuresPropertiesKHR-query-04880` | "The sum of `firstQuery` plus `accelerationStructureCount` must be less than or equal to the number of queries in `queryPool`" |
| compacted-size query ⇒ built with `AllowCompaction` | `VUID-vkCmdWriteAccelerationStructuresPropertiesKHR-accelerationStructures-03431` | "All acceleration structures … must have been built with `VK_BUILD_ACCELERATION_STRUCTURE_ALLOW_COMPACTION_BIT_KHR` if `queryType` is `VK_QUERY_TYPE_ACCELERATION_STRUCTURE_COMPACTED_SIZE_KHR`" |
| non-empty structure array | `VUID-vkCmdWriteAccelerationStructuresPropertiesKHR-accelerationStructureCount-arraylength` | "`accelerationStructureCount` must be greater than 0" |
| copy mode is `Compact` or `Clone` | `VUID-VkCopyAccelerationStructureInfoKHR-mode-03410` | "`mode` must be `VK_COPY_ACCELERATION_STRUCTURE_MODE_COMPACT_KHR` or `VK_COPY_ACCELERATION_STRUCTURE_MODE_CLONE_KHR`" |
| `Compact` ⇒ source built with `AllowCompaction` | `VUID-VkCopyAccelerationStructureInfoKHR-src-03411` | "If `mode` is `..._COMPACT_KHR`, `src` must have been constructed with `VK_BUILD_ACCELERATION_STRUCTURE_ALLOW_COMPACTION_BIT_KHR` in the build" |
| destroy only after completion | `VUID-vkDestroyAccelerationStructureKHR-accelerationStructure-02442` | "All submitted commands that refer to `accelerationStructure` must have completed execution" |

One refinement, not a contradiction: the "top level ⇒ exactly one `Instances`
geometry" pairing is **two** VUIDs in the registry (`-type-03789` for the kind
and `-type-03790` for the count), not one. The guard messages cite whichever
applies.

## Step 1 — `Internal/DeviceExtensionNames.cs`

Add, after the mesh entries:

```csharp
public static ReadOnlySpan<byte> AccelerationStructure  => "VK_KHR_acceleration_structure"u8;
public static ReadOnlySpan<byte> RayQuery               => "VK_KHR_ray_query"u8;
public static ReadOnlySpan<byte> DeferredHostOperations => "VK_KHR_deferred_host_operations"u8;

public static ReadOnlySpan<byte> CreateAccelerationStructure              => "vkCreateAccelerationStructureKHR"u8;
public static ReadOnlySpan<byte> DestroyAccelerationStructure             => "vkDestroyAccelerationStructureKHR"u8;
public static ReadOnlySpan<byte> GetAccelerationStructureBuildSizes       => "vkGetAccelerationStructureBuildSizesKHR"u8;
public static ReadOnlySpan<byte> GetAccelerationStructureDeviceAddress    => "vkGetAccelerationStructureDeviceAddressKHR"u8;
public static ReadOnlySpan<byte> CmdBuildAccelerationStructures           => "vkCmdBuildAccelerationStructuresKHR"u8;
public static ReadOnlySpan<byte> CmdWriteAccelerationStructuresProperties => "vkCmdWriteAccelerationStructuresPropertiesKHR"u8;
public static ReadOnlySpan<byte> CmdCopyAccelerationStructure             => "vkCmdCopyAccelerationStructureKHR"u8;
```

Extend the class doc with a sentence noting that `RayQuery` and
`DeferredHostOperations` gate **nothing** — ray query defines no entry points,
and the wrapper calls no deferred-host-operation command — they exist so
`VulkanExtensions` can hand callers the names `vkCreateDevice` requires.

## Step 2 — `Rendering/VulkanExtensions.cs`

Three new `Utf8Name` properties in the mesh-shader style:

```csharp
public static Utf8Name KhrAccelerationStructure  => Utf8Name.FromLiteral(DeviceExtensionNames.AccelerationStructure);
public static Utf8Name KhrRayQuery               => Utf8Name.FromLiteral(DeviceExtensionNames.RayQuery);
public static Utf8Name KhrDeferredHostOperations => Utf8Name.FromLiteral(DeviceExtensionNames.DeferredHostOperations);
```

XML docs must state the full enable recipe once: all three extensions in
`DeviceDescription.Extensions`, plus `VkPhysicalDeviceAccelerationStructureFeaturesKHR.accelerationStructure`,
`VkPhysicalDeviceRayQueryFeaturesKHR.rayQuery` and Vulkan 1.2's
`bufferDeviceAddress` pushed from `DeviceDescription.ConfigureFeatures`, and
that `KhrDeferredHostOperations` is required by `KhrAccelerationStructure` even
though the wrapper calls no deferred command.

## Step 3 — `Internal/AccelerationStructureSupport.cs` (new file)

`internal static class AccelerationStructureSupport`, modelled exactly on
`Internal/MeshShaderSupport.cs`:

- `public const string EnableInstructions` — names the three extensions
  (`VulkanExtensions.KhrAccelerationStructure` /
  `KhrDeferredHostOperations` / `KhrRayQuery`), the three features, and
  "then re-create the Device".
- `public const string PartialGuardNote` — a non-null entry point proves the
  *extension* was enabled and nothing more; Vulkan exposes no post-device
  feature query, so a device that enabled the extension without the
  `accelerationStructure` feature still reaches the driver.

`const` so the concatenations at call sites fold at compile time.

## Step 4 — `Internal/DeviceFunctionTable.cs`

### 4a. Charter doc

Amend the third bullet (`:36-45`): `VK_EXT_mesh_shader` was the first member,
`VK_KHR_acceleration_structure` is the second, and note that the AS block is the
first to carry **device-level** (create/destroy/query) entry points, which the
bullet already anticipated.

### 4b. Seven fields

In a new `// ---- Acceleration structures (VK_KHR_acceleration_structure) ----`
section, each with a `<summary>` saying "Null when VK_KHR_acceleration_structure
was not enabled on this device":

```csharp
public readonly delegate* unmanaged[Stdcall]<
    VkDevice_T*, VkAccelerationStructureCreateInfoKHR*, VkAllocationCallbacks*,
    VkAccelerationStructureKHR_T**, VkResult> CreateAccelerationStructure;

public readonly delegate* unmanaged[Stdcall]<
    VkDevice_T*, VkAccelerationStructureKHR_T*, VkAllocationCallbacks*, void>
    DestroyAccelerationStructure;

public readonly delegate* unmanaged[Stdcall]<
    VkDevice_T*, VkAccelerationStructureBuildTypeKHR,
    VkAccelerationStructureBuildGeometryInfoKHR*, uint*,
    VkAccelerationStructureBuildSizesInfoKHR*, void> GetAccelerationStructureBuildSizes;

public readonly delegate* unmanaged[Stdcall]<
    VkDevice_T*, VkAccelerationStructureDeviceAddressInfoKHR*, ulong>
    GetAccelerationStructureDeviceAddress;

public readonly delegate* unmanaged[Stdcall]<
    VkCommandBuffer_T*, uint, VkAccelerationStructureBuildGeometryInfoKHR*,
    VkAccelerationStructureBuildRangeInfoKHR**, void> CmdBuildAccelerationStructures;

public readonly delegate* unmanaged[Stdcall]<
    VkCommandBuffer_T*, uint, VkAccelerationStructureKHR_T**, VkQueryType,
    VkQueryPool_T*, uint, void> CmdWriteAccelerationStructuresProperties;

public readonly delegate* unmanaged[Stdcall]<
    VkCommandBuffer_T*, VkCopyAccelerationStructureInfoKHR*, void>
    CmdCopyAccelerationStructure;
```

Cross-check each signature against `Generated/Vk.cs:3137`, `:3140`, `:3183`,
`:3174`, `:3143`, `:3177`, `:3164` — parameter-for-parameter, including the
`VkAllocationCallbacks*` the wrapper always passes as `null`.

### 4c. Null-initialize

Extend the definite-assignment block at `:239-242` with all seven `= null;`.

### 4d. Gated resolve block

After the mesh `if` block (ends `:419`), add:

```csharp
if (IsExtensionEnabled(enabledExtensions, DeviceExtensionNames.AccelerationStructure))
{
    // seven ResolveExtensionRequired(
    //     Utf8Name.FromLiteral(DeviceExtensionNames.X),
    //     DeviceExtensionNames.AccelerationStructure)
    // casts, in the field order above
}
```

No new helper methods: `IsExtensionEnabled`, `ResolveExtensionRequired` and
`ThrowExtensionEntryPointMissing` are reused unchanged.

## Step 5 — Shadow enums and sync2 bits

### 5a. `Resources/AccelerationStructureType.cs` (new)

```csharp
public enum AccelerationStructureType
{
    TopLevel    = 0,   // VK_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL_KHR
    BottomLevel = 1,   // VK_ACCELERATION_STRUCTURE_TYPE_BOTTOM_LEVEL_KHR
    Generic     = 2,   // VK_ACCELERATION_STRUCTURE_TYPE_GENERIC_KHR
}
```

Doc must call out the footgun explicitly: `TopLevel` is 0, so a
default-initialized `AccelerationStructureBuild` builds a TLAS; a BLAS build must
set `Type` explicitly, and the recorder's geometry-kind guard (Step 9) exists to
catch the omission.

### 5b. `Recording/AccelerationStructureBuildFlags.cs` (new)

`[Flags] public enum AccelerationStructureBuildFlags : uint` with
`None = 0`, `AllowUpdate = 0x1`, `AllowCompaction = 0x2`, `PreferFastTrace = 0x4`,
`PreferFastBuild = 0x8`, `LowMemory = 0x10`. Values from
`Generated/VkBuildAccelerationStructureFlagBitsKHR.cs`. Doc: `AllowUpdate` is
required before any `Mode.Update`; `AllowCompaction` is required before the
compacted-size query and the compaction copy; `PreferFastTrace` and
`PreferFastBuild` are mutually exclusive in practice.

### 5c. `Recording/AccelerationStructureBuildMode.cs` (new)

`Build = 0`, `Update = 1` (`VkBuildAccelerationStructureModeKHR`).

### 5d. `Recording/GeometryFlags.cs` (new)

`[Flags] public enum GeometryFlags : uint` — `None = 0`, `Opaque = 0x1`,
`NoDuplicateAnyHitInvocation = 0x2` (`Generated/VkGeometryFlagBitsKHR.cs`). Doc
that `NoDuplicateAnyHitInvocation` is inert for ray query (no any-hit shader) and
is listed for completeness.

### 5e. `Recording/AccelerationStructureCopyMode.cs` (new)

`Clone = 0`, `Compact = 1` (`VkCopyAccelerationStructureModeKHR`). `Serialize` /
`Deserialize` are deliberately absent — spec "why not the alternatives".

### 5f. `Recording/Stage.cs` — two members

```csharp
AccelerationStructureBuild = 0x02000000,   // VK_PIPELINE_STAGE_2_ACCELERATION_STRUCTURE_BUILD_BIT_KHR
AccelerationStructureCopy  = 0x10000000,   // VK_PIPELINE_STAGE_2_ACCELERATION_STRUCTURE_COPY_BIT_KHR
```

Do **not** add `RayTracingShader` (spec §F). Extend the enum's summary to say
that a ray-query traversal is synchronized against the shader stage that runs it
(`ComputeShader` / `FragmentShader`), not against an RT-pipeline stage.

### 5g. `Recording/Access.cs` — two members

```csharp
AccelerationStructureRead  = 0x00200000,   // VK_ACCESS_2_ACCELERATION_STRUCTURE_READ_BIT_KHR
AccelerationStructureWrite = 0x00400000,   // VK_ACCESS_2_ACCELERATION_STRUCTURE_WRITE_BIT_KHR
```

## Step 6 — `Resources/AccelerationStructure.cs` (new file)

Exact shape in spec §B. Points the implementer must not improvise:

- `HandleRegistry.TrackCreate(this)` in the internal constructor and
  `HandleRegistry.TrackDispose(this)` in `Dispose` before the destroy call — the
  `Event`/`QueryPool` shape (`Sync/Event.cs:44-46`, `:73-77`).
- `Dispose`: `if (Handle == null) return; if (!OwnsHandle) return;` then
  `_destroy(DeviceHandle, Handle, null);`. Add an assert-free comment that
  `_destroy` is non-null exactly when `DeviceHandle` is, because both come from
  the same construction path.
- `GetDeviceAddress(Device device)`:
  ```csharp
  var fn = device.Functions.GetAccelerationStructureDeviceAddress;
  if (fn == null) throw new InvalidOperationException(
      "AccelerationStructure.GetDeviceAddress is not available on this device. "
      + AccelerationStructureSupport.EnableInstructions);
  var info = new VkAccelerationStructureDeviceAddressInfoKHR
  {
      sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_DEVICE_ADDRESS_INFO_KHR,
      accelerationStructure = Handle,
  };
  return fn(device.Handle, &info);
  ```
  plus `ArgumentNullException.ThrowIfNull(device)` and a borrowed-handle throw
  (a `FromRaw` handle has no owning device, but the *passed* device is what
  dispatches, so the borrowed check here is only that `Handle != null`).
- XML docs carry spec §H1 (buffer not owned, must outlive), §H5 (dispose in
  flight is UB) and — on `GetDeviceAddress` — §H6 in full: the returned value is
  a bare number once written into a TLAS instance buffer, so every BLAS must
  outlive every TLAS over it and compaction changes the address.

## Step 7 — `Memory/AccelerationStructureBuildSizes.cs` and `Lifecycle/AccelerationStructureLimits.cs` (new files)

Both `readonly record struct`, members exactly as in spec §C. Docs:

- `BuildScratchSize` / `UpdateScratchSize`: the scratch buffer needs
  `BufferUsage.StorageBuffer | BufferUsage.ShaderDeviceAddress`, and the address
  handed to a build must be a multiple of
  `AccelerationStructureLimits.MinScratchOffsetAlignment` — cross-`see cref` both
  ways.
- `AccelerationStructureSize`: the size to allocate in the backing buffer, at an
  offset that is a multiple of 256.
- `AccelerationStructureLimits`: the "narrow on purpose" paragraph in
  `MeshShaderLimits`' voice, naming
  `PhysicalDevice.TryGetProperties<VkPhysicalDeviceAccelerationStructurePropertiesKHR>`
  as the escape hatch for the two update-after-bind members left out.

## Step 8 — Geometry and build description types (new files under `Recording/`)

### 8a. `Recording/AccelerationStructureBuildRange.cs`

```csharp
public readonly record struct AccelerationStructureBuildRange
{
    public uint PrimitiveCount  { get; init; }
    public uint PrimitiveOffset { get; init; }
    public uint FirstVertex     { get; init; }
    public uint TransformOffset { get; init; }

    public static AccelerationStructureBuildRange Of(uint primitiveCount, ...);
}
```

Field **declaration order is load-bearing** — say so in the doc, in
`QueryResult`'s voice (`Sync/QueryResult.cs:6-25`): the struct is cast in place
to `VkAccelerationStructureBuildRangeInfoKHR`, so a reorder would silently
scramble every build. No `ToNative()`; a test pins size and offsets (Step 13a).

### 8b. `Recording/AccelerationStructureGeometry.cs`

`public readonly struct AccelerationStructureGeometry` (**not** a record — it is
compared by nobody and record equality on a struct of primitives adds nothing).
Public read-only properties: `Kind` (`GeometryKind` — a nested-or-sibling enum
`Triangles = 0`, `Aabbs = 1`, `Instances = 2`, native values from
`VkGeometryTypeKHR`), `Flags`, `Address`, `Stride`, `IndexAddress`,
`TransformAddress`, `MaxVertex`, `VertexFormat`, `IndexType`, `ArrayOfPointers`.

Doc must state per-kind field meaning in a table: for `Triangles`, `Address` is
`vertexData` and `Stride` is `vertexStride`; for `Aabbs`, `Address` is `data` and
`Stride` is the AABB stride (must be a multiple of 8, and each element is a
`VkAabbPositionsKHR`); for `Instances`, `Address` is the instance-array address
and `Stride`/`MaxVertex`/`IndexType`/`TransformAddress` are unused.

Three static factories with the signatures in spec §D. `Instances` doc carries
§H6 and states that each element is a `VkAccelerationStructureInstanceKHR` from
`Ahjo.Vulkan.Native` (the wrapper deliberately does not mirror that bitfield
struct — spec "why not the alternatives"), that the array address must be
16-byte aligned, and that `accelerationStructureReference` is
`AccelerationStructure.GetDeviceAddress(device)`.

An `internal void WriteNative(out VkAccelerationStructureGeometryKHR dst)` (or an
equivalent internal method on the translator) does the union fill; it is the only
place `VkAccelerationStructureGeometryDataKHR` is touched.

### 8c. `Recording/AccelerationStructureBuild.cs`

`public readonly struct AccelerationStructureBuild` with the eight init
properties in spec §D. Doc:

- the CSR contract in one paragraph: `FirstGeometry`/`GeometryCount` slice the
  `geometries` **and** `ranges` spans passed to
  `CommandRecorder.BuildAccelerationStructures`, and the two spans are indexed
  identically because Vulkan pairs one range per geometry;
- `ScratchAddress` alignment + non-overlap-across-builds-in-one-call (§H2);
- `Source` required iff `Mode.Update`, and the source must have been built with
  `AccelerationStructureBuildFlags.AllowUpdate`;
- a five-line usage example showing the single-build TLAS rebuild with
  `stackalloc AccelerationStructureBuild[1]`.

## Step 9 — `Internal/AccelerationStructureBuildTranslator.cs` (new file)

`internal static unsafe class` in `DescriptorWriteBuilder`'s voice — its doc
states that the caller MUST keep `builds`, `geometries` and `ranges` pinned and
addressable for the duration of the native call, because the native structs point
into them.

```csharp
internal static void BuildGeometryInfos(
    ReadOnlySpan<AccelerationStructureBuild>            builds,
    ReadOnlySpan<AccelerationStructureGeometry>         geometries,
    VkAccelerationStructureBuildRangeInfoKHR*           pRanges,   // pinned caller span, cast in place
    Span<VkAccelerationStructureGeometryKHR>            nativeGeometries,
    Span<VkAccelerationStructureBuildGeometryInfoKHR>   infos,
    VkAccelerationStructureBuildRangeInfoKHR**          ppRanges);
```

Per build `b`: fill `infos[b]` with `sType`, `type = (VkAccelerationStructureTypeKHR)b.Type`,
`flags = (uint)b.Flags`, `mode = (VkBuildAccelerationStructureModeKHR)b.Mode`,
`srcAccelerationStructure = b.Source.Handle`,
`dstAccelerationStructure = b.Destination.Handle`,
`geometryCount = b.GeometryCount`,
`pGeometries = &nativeGeometries[b.FirstGeometry]`, `ppGeometries = null`,
`scratchData.deviceAddress = b.ScratchAddress`; and
`ppRanges[b] = pRanges + b.FirstGeometry`.

Fill `nativeGeometries[i]` from `geometries[i]` once, up front, over the whole
span — not per build — so overlapping slices cost nothing.

A second, smaller entry point serves `Device.GetAccelerationStructureBuildSizes`:
one `VkAccelerationStructureBuildGeometryInfoKHR` with `geometryCount` =
`geometries.Length`, `pGeometries` = the translated array, `src`/`dst`/`scratch`
left zero (ignored by the size query).

## Step 10 — `Lifecycle/Device.cs`

### 10a. `CreateAccelerationStructure`

```csharp
public AccelerationStructure CreateAccelerationStructure(
    AccelerationStructureType type, in Buffer buffer, ulong offset, ulong size)
```

Guard table exactly as spec §C, all unconditional, each message naming the rule
and (after Step 0) its VUID. Then:

```csharp
var ci = new VkAccelerationStructureCreateInfoKHR
{
    sType       = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_CREATE_INFO_KHR,
    createFlags = 0,
    buffer      = buffer.Handle,
    offset      = offset,
    size        = size,
    type        = (VkAccelerationStructureTypeKHR)type,
    deviceAddress = 0,
};
VkAccelerationStructureKHR_T* raw = null;
Functions.CreateAccelerationStructure(Handle, &ci, null, &raw).ThrowIfFailed();
return new AccelerationStructure(raw, Handle, Functions.DestroyAccelerationStructure, size);
```

`createFlags = 0` and `deviceAddress = 0` get a comment: capture/replay
(`VK_ACCELERATION_STRUCTURE_CREATE_DEVICE_ADDRESS_CAPTURE_REPLAY_BIT_KHR`) is out
of scope, and `deviceAddress` is only meaningful with that flag.

XML doc: §H1 in full (buffer is caller-owned and must outlive the structure; the
range must not be aliased), plus the 256-byte offset rule and the
`AccelerationStructureStorage` usage requirement.

### 10b. `GetAccelerationStructureBuildSizes`

Signature in spec §C. Body: null-pointer guard →
`ArgumentException` when `maxPrimitiveCounts.Length != geometries.Length` →
translate geometries (stackalloc ≤ 16, else `ArrayPool`) → one
`VkAccelerationStructureBuildSizesInfoKHR` with its `sType` set (**the driver
requires it**; a zeroed `sType` is the classic silent failure here) → call with
`VkAccelerationStructureBuildTypeKHR.VK_ACCELERATION_STRUCTURE_BUILD_TYPE_DEVICE_KHR`
→ project into `AccelerationStructureBuildSizes`.

Doc: `src`/`dst`/`scratchData` are ignored by this query, which is why the
signature has no destination; `maxPrimitiveCounts[i]` is the upper bound on
`ranges[i].PrimitiveCount` for the build this sizes.

### 10c. `CreateQueryPool(QueryType type, uint queryCount)`

New overload; move the existing body into it and make
`CreateQueryPool(uint queryCount)` a one-line forward with
`QueryType.Timestamp`, so there is one create path.

Additional guards in the typed overload:

- `type == QueryType.Unknown` → `ArgumentException`
  ("QueryType.Unknown is the borrowed-handle sentinel, not a creatable type").
- `type == QueryType.AccelerationStructureCompactedSize &&
  Functions.CmdWriteAccelerationStructuresProperties == null` →
  `InvalidOperationException` + `AccelerationStructureSupport.EnableInstructions`.

Return `new QueryPool(raw, Handle, queryCount, type)`.

## Step 11 — `Sync/QueryType.cs` (new) and `Sync/QueryPool.cs`

### 11a. `Sync/QueryType.cs`

Three members exactly as spec §E, each with the native name in the doc, plus a
class-level paragraph explaining why `Unknown = 0` is safe (occlusion queries are
not created by this wrapper) and that adding members is additive.

### 11b. `Sync/QueryPool.cs`

- Constructor gains `QueryType type`; store `_type`.
- `FromRaw` passes `QueryType.Unknown`.
- New `public QueryType Type => _type;` with the "Unknown on a borrowed handle
  means *unknown*, never *timestamp*" paragraph, mirroring `QueryCount`'s.
- Class doc: generalize the opening sentence from "A timestamp-typed
  `VkQueryPool`" to "A typed `VkQueryPool`", keep the timestamp guidance as the
  `QueryType.Timestamp` case, and add a short `QueryType.AccelerationStructureCompactedSize`
  paragraph: each result is a size in bytes, the same reset-before-use rule
  applies, and it is written by
  `CommandRecorder.WriteAccelerationStructuresProperties`.
- The three readback methods are unchanged; adjust only the doc lines that assume
  ticks so they read "raw ticks for a timestamp pool".

## Step 12 — `Recording/CommandRecorder.cs`

### 12a. Throw helper

Next to `ThrowMeshShaderUnsupported` (`:470`):

```csharp
[DoesNotReturn]
private static void ThrowAccelerationStructureUnsupported(string what) =>
    throw new InvalidOperationException(
        $"{what} is not available on this device. "
        + AccelerationStructureSupport.EnableInstructions);
```

### 12b. `BuildAccelerationStructures`

Signature in spec §D. Order of operations:

1. `var fn = Fns.CmdBuildAccelerationStructures; if (fn == null) ThrowAccelerationStructureUnsupported("BuildAccelerationStructures");`
   — unconditional, **not** behind `AhjoValidation`.
2. `if (builds.IsEmpty) return;` (an empty batch is a no-op, the `CopyBuffer`
   empty-span precedent).
3. `if (AhjoValidation.IsEnabled) AssertBuildsValid(builds, geometries, ranges);`
   — a private static helper containing: `ranges.Length == geometries.Length`;
   for each build, `(ulong)FirstGeometry + GeometryCount <= geometries.Length`
   (widen before adding, the `ResetQueryPool` precedent at `:993`),
   `GeometryCount > 0`, `!Destination.IsNull`,
   `Mode == Update ? !Source.IsNull : Source.IsNull`, and the type/kind pairing
   (`Type == TopLevel` ⇒ exactly one geometry and it is `Instances`;
   `Type == BottomLevel` ⇒ no `Instances` geometry). Every message names the
   member and, where Step 0 confirmed one, the VUID; the TLAS message explicitly
   mentions that `AccelerationStructureType.TopLevel` is the default value.
4. Carve scratch: `builds.Length <= 8 && geometries.Length <= 16` ⇒ three
   `stackalloc`s (`VkAccelerationStructureBuildGeometryInfoKHR`,
   `VkAccelerationStructureGeometryKHR`, `nint` for the range pointers); else
   three `ArrayPool<T>.Shared.Rent` in nested `try/finally`, returning in reverse
   order. Factor the post-carve work into a private static
   `RecordBuilds(fn, cb, builds, geometries, ranges, infos, nativeGeometries, ppRanges)`
   so the two paths share one body — the `FlushPush` split at `:441-459`.
5. In `RecordBuilds`: `fixed` over `geometries`, `builds` and `ranges`; cast the
   pinned `ranges` pointer to `VkAccelerationStructureBuildRangeInfoKHR*`; call
   the translator; then
   `fn(Handle, (uint)builds.Length, pInfos, ppRanges);`.

Define the two thresholds as `private const int BuildStackThreshold = 8;` /
`GeometryStackThreshold = 16;` with a comment recording the spec's reasoning (the
per-frame TLAS shape is 1/1; BLAS batches are load-time) and that the numbers are
reasoned, not measured.

XML doc: what the command does, the CSR contract, the scratch rules (§H2), the
"must be outside a rendering scope" rule, the required queue capability
(compute), and the barrier a caller needs before traversal
(`Stage.AccelerationStructureBuild`/`Access.AccelerationStructureWrite` →
the consuming shader stage/`Access.AccelerationStructureRead`).

### 12c. `WriteAccelerationStructuresProperties`

```csharp
public void WriteAccelerationStructuresProperties(
    ReadOnlySpan<AccelerationStructure> structures, in QueryPool pool, uint firstQuery)
```

1. null-pointer guard on `Fns.CmdWriteAccelerationStructuresProperties`
   (unconditional).
2. `if (structures.IsEmpty) return;`
3. Unconditional throw when `pool.IsNull` **or** `pool.Type == QueryType.Unknown`
   — the second names the borrowed-pool case and says the wrapper has no valid
   `queryType` to pass (the `QueryPool.ThrowIfBorrowed` voice).
4. `AhjoValidation`-gated: `pool.Type == QueryType.AccelerationStructureCompactedSize`;
   `(ulong)firstQuery + structures.Length <= pool.QueryCount` when `QueryCount != 0`;
   no `structures[i].IsNull`.
5. Handle array: `stackalloc nint[n]` for `n <= 8`, else `ArrayPool<nint>`; fill
   with `(nint)structures[i].Handle`; `fixed` and cast to
   `VkAccelerationStructureKHR_T**`.
6. `fn(Handle, (uint)structures.Length, pHandles, (VkQueryType)pool.Type, pool.Handle, firstQuery);`

Doc: the queries must have been reset by a **submitted** `ResetQueryPool`; every
structure must have been built with `AllowCompaction`; a barrier is required
between the build and this command; and the seven-step compaction flow from spec
§E goes here as the worked example (this is the method a reader lands on).

### 12d. `CopyAccelerationStructure`

```csharp
public void CopyAccelerationStructure(
    in AccelerationStructure source, in AccelerationStructure destination,
    AccelerationStructureCopyMode mode)
```

Null-pointer guard; `AhjoValidation`-gated null-handle checks on both structures;
build `VkCopyAccelerationStructureInfoKHR { sType, src, dst, mode = (VkCopyAccelerationStructureModeKHR)mode }`
and call. Doc: for `Compact`, the destination must have been created with the
size read back from the compacted-size query and the source must have been built
with `AllowCompaction`; the source and its buffer must stay alive until the copy
completes (§H7); the destination's device address differs from the source's
(§H6).

### 12e. Class doc

Extend the "Recording surface" paragraph (`:23-29`) with the three new commands.

## Step 13 — Descriptor writes for the TLAS binding

**See OPEN-1 before starting this step.**

### 13a. `Pools/DescriptorWrite.cs`

- `Kind.AccelerationStructure = 2`.
- Make the struct `unsafe` and add
  `internal readonly VkAccelerationStructureKHR_T* _accelerationStructure;`
  (keep it last so the existing `[StructLayout(Sequential)]` payload offsets
  don't shift meaning).
- Factory:
  ```csharp
  public static DescriptorWrite AccelerationStructure(
      uint binding, uint arrayElement, in AccelerationStructure structure);
  ```
  with `_type = VkDescriptorType.VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR`
  fixed, and a doc explaining why there is no `type` parameter.
- Class doc gains a paragraph on the `pNext` chaining and on the caller's
  obligation that the referenced structure outlives the descriptor set's use.

### 13b. `Pools/DescriptorWriteBuilder.cs`

`BuildWrites` gains a fourth parameter
`Span<VkWriteDescriptorSetAccelerationStructureKHR> chains` (same length as
`dst`). For `Kind.AccelerationStructure`:

```csharp
chains[i] = new VkWriteDescriptorSetAccelerationStructureKHR
{
    sType = VkStructureType.VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET_ACCELERATION_STRUCTURE_KHR,
    accelerationStructureCount = 1,
    pAccelerationStructures = (VkAccelerationStructureKHR_T**)
        Unsafe.AsPointer(ref Unsafe.AsRef(in w._accelerationStructure)),
};
dst[i].pNext       = Unsafe.AsPointer(ref chains[i]);
dst[i].pBufferInfo = null;
dst[i].pImageInfo  = null;
```

Extend the method's remarks: the caller must now pin **both** `writes` and
`chains` for the duration of the native call.

### 13c. Both call sites

`Pools/DescriptorSetExtensions.cs:33-48` and
`Recording/CommandRecorder.cs:441-459`: carve a `chains` span alongside the
`VkWriteDescriptorSet` span using the identical `≤ 8 ? stackalloc : ArrayPool`
rule and the same `try/finally` return discipline, and `fixed`-pin it in
`FlushUpdate` / `FlushPush` before calling `BuildWrites`.

## Step 14 — `Lifecycle/PhysicalDevice.cs`

Add `TryGetAccelerationStructureLimits(out AccelerationStructureLimits limits)`
immediately after `TryGetMeshShaderLimits`, implemented as:

```csharp
if (!TryGetProperties<VkPhysicalDeviceAccelerationStructurePropertiesKHR>(
        VulkanExtensions.KhrAccelerationStructure, out var raw))
{ limits = default; return false; }
limits = new AccelerationStructureLimits { … };
return true;
```

Doc in `TryGetMeshShaderLimits`' voice: `false` means the **physical device** does
not advertise the extension, not that any `Device` enabled it; readable before
`CreateDevice`; setup-time, nothing cached, three native queries on the passing
path. Also update the `TryGetProperties` example block
(`Lifecycle/PhysicalDevice.cs:274-280`) to point at this projection the way it
points at `TryGetMeshShaderLimits`.

## Step 15 — Tests

New file `tests/Ahjo.Vulkan.Tests/AccelerationStructureTests.cs`, plus edits to
two existing files. Every skip goes through `TestGate`.

### 15a. Driver-free (no gate)

- `AccelerationStructureBuildRange` matches `VkAccelerationStructureBuildRangeInfoKHR`:
  `Unsafe.SizeOf<T>()` equal for both, and each of the four field offsets equal
  via `Marshal.OffsetOf`/pointer arithmetic. Message must say a reorder silently
  scrambles builds.
- `QueryType` values equal `VkQueryType.VK_QUERY_TYPE_TIMESTAMP` and
  `..._ACCELERATION_STRUCTURE_COMPACTED_SIZE_KHR`; `Unknown` is 0 — extend
  `ShadowEnumDriftTests` with `QueryType_MatchesNative`.
- `ShadowEnumDriftTests.Stage_AccelerationStructureBits_MatchNative` and
  `Access_AccelerationStructureBits_MatchNative` against `Vk.VK_PIPELINE_STAGE_2_*`
  / `Vk.VK_ACCESS_2_*` (`Generated/Vk.cs:727`, `:763`, `:967`, `:970`).
- `AccelerationStructureType`, `AccelerationStructureBuildFlags`,
  `AccelerationStructureBuildMode`, `GeometryFlags`,
  `AccelerationStructureCopyMode` against their native enums — one more
  `*_MatchesNative` fact each, or one combined fact per enum in the existing
  file's style.
- `AccelerationStructureGeometry.Triangles/Aabbs/Instances` set the expected
  `Kind`, `Flags`, `Address`, `Stride` and per-kind unused members.
- `AccelerationStructure` borrow contract: `FromRaw(0xDEADBEEF)` and `default`
  report `OwnsHandle == false`, `Size == 0`, and `Dispose()` is a no-op that
  does not dispatch. Add the explicit entry to
  `HandleConventionsTests.BorrowContract_HoldsForEveryHandleType` and bump the
  count in its comment ("seventeen …").

### 15b. `[gate:driver]` only

Create a device **without** `VK_KHR_acceleration_structure` (the plain
`CreateGraphicsDevice` helper the mesh tests already use) and assert each of the
following throws `InvalidOperationException` whose message contains
`VK_KHR_acceleration_structure`:

`CreateAccelerationStructure`, `GetAccelerationStructureBuildSizes`,
`CreateQueryPool(QueryType.AccelerationStructureCompactedSize, 1)`,
`BuildAccelerationStructures`, `WriteAccelerationStructuresProperties`,
`CopyAccelerationStructure`, and `AccelerationStructure.GetDeviceAddress`.

Also `[gate:driver]`:

- Every `CreateAccelerationStructure` argument guard, using `Buffer.FromRaw` for
  a non-null borrowed buffer where the guard is not usage/size dependent, and a
  real VMA buffer where it is (unaligned offset, offset+size past the end,
  missing `AccelerationStructureStorage`). These must run **before** the
  null-pointer extension guard is reached — order the guards so the
  device-independent misuse keeps the more actionable message, and pin that
  ordering with a test, exactly as #201 did for the mesh builder.
- `CreateQueryPool(QueryType.Unknown, 1)` throws `ArgumentException`.
- `TryGetAccelerationStructureLimits` returns `false` (does not throw) on a GPU
  without the extension, and `default` limits.
- `DescriptorWrite.AccelerationStructure` round-trip: build one through
  `DescriptorSet.Update` on a real device with an AS-typed layout binding — if
  that needs the extension, instead assert at the `BuildWrites` level through an
  internal-visible test that the produced `VkWriteDescriptorSet` has non-null
  `pNext`, null `pBufferInfo`/`pImageInfo` and the AS descriptor type.

### 15c. `[gate:feature]` — RT-capable host only

Probe with the `ExportableResourceTests.TryCreateDeviceWith` shape: attempt
`CreateDevice` with the three extensions plus the feature chain, catch
`VulkanException` carrying `VK_ERROR_EXTENSION_NOT_PRESENT`, and
`TestGate.RequireDeviceFeature(device is not null, "…VK_KHR_acceleration_structure + VK_KHR_ray_query…")`.

- **BLAS round trip.** One triangle in a device-address buffer →
  `GetAccelerationStructureBuildSizes` returns non-zero
  `AccelerationStructureSize` and `BuildScratchSize` → allocate backing + scratch
  → `CreateAccelerationStructure` → `BuildAccelerationStructures` with one build
  and one geometry → submit → `GetDeviceAddress` is non-zero.
- **TLAS over one instance.** Write one `VkAccelerationStructureInstanceKHR` with
  `accelerationStructureReference` = the BLAS address → build a TLAS with an
  `Instances` geometry → submit and wait without validation errors.
- **Compaction round trip.** Rebuild the BLAS with `AllowCompaction`; barrier;
  `ResetQueryPool` + `WriteAccelerationStructuresProperties`; submit; wait;
  `GetResults` gives a non-zero size `≤` the original
  `AccelerationStructureSize`; create the compacted structure over a new buffer;
  `CopyAccelerationStructure(..., Compact)`; submit; assert the compacted
  structure's `GetDeviceAddress` differs from the original's.
- **Zero-allocation assertion** on `BuildAccelerationStructures`, the
  `MeshShaderTests.MeshPipeline_Build_IsZeroAllocation` shape:
  `GC.GetAllocatedBytesForCurrentThread()` delta over 128 recordings of the
  one-build/one-geometry shape is 0.

## Step 16 — Benchmarks

New `tests/Ahjo.Vulkan.Benchmarks/AccelerationStructureBenchmarks.cs`, its own
class for the `MeshShaderBenchmarks` reason (an RT-less host must not take the
issue-29 canary down). `[MemoryDiagnoser]`.

- `[GlobalSetup]`: device with the three extensions + features, a triangle vertex
  buffer, a BLAS built once, an instance buffer, a TLAS backing buffer, a scratch
  buffer, a `CommandBufferPool`.
- `BuildTlas_1024`: `OperationsPerInvoke`-style loop recording
  `BuildAccelerationStructures` with one build / one geometry into one command
  buffer, never submitted. Expect `-` in `Allocated`.
- Follow the #188/#199 recorder-disposal ordering: dispose the recorder **before**
  `ResetForFrame`, or the pool ping-pongs two buffers (`docs/benchmarks.md:98`).

Re-run `PushDescriptors.*` and `CommandRecorder.RenderingPass100Cmds` after
Step 13 and confirm both still read `-`.

Update `docs/benchmarks.md`: the new row(s), the `--filter` line at `:27`, and
the driver-dependency caveat paragraph naming the RT requirement.

## Step 17 — Docs

- `docs/benchmarks.md` — Step 16.
- `docs/aot-notes.md` — one line in the inventory noting the AS entry points join
  the mesh block as `delegate* unmanaged` dispatch with no new trim surface, if
  the existing file lists the mesh block that way.
- `src/Ahjo.Vulkan/README.md` — add acceleration structures to the surface
  overview if it enumerates subsystems.
- `.claude/agents/bench-coverage-checker.md` — add the new benchmark class to the
  coverage map, as #199 and #201 both did.

## Verification

```bash
dotnet build Ahjo.Vulkan.slnx                 # TreatWarningsAsErrors: must be clean
dotnet test                                   # includes the new gated tests
dotnet publish samples/AotSmoke/AotSmoke.csproj -c Release -r win-x64 -p:IlcUseEnvironmentalTools=true
dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- \
  --filter "*AccelerationStructure*|*PushDescriptors*|*CommandRecorder*"
```

Then run the `vulkan-validation-reviewer` and `bench-coverage-checker` agents —
the diff touches `Recording/`, `Sync/`, `Pools/`, `Resources/` and `Memory/`.

Run the test suite once with `AHJO_VULKAN_TIER=validation` on the RT-capable
host: the Tier-3 tests are the only place the layer can check the build,
compaction and barrier sequences, and spec §H5 explicitly delegates
destroy-in-use detection to it.

## Open items

- **OPEN-1 — Step 13 (TLAS descriptor writes) may want to be its own issue.**
  The audit found that without it a ray-query shader cannot bind the TLAS, so the
  rest of this surface is unusable end to end — which is why it is in this plan.
  But it is the only part that touches an existing benchmarked hot path
  (`PushDescriptors`, `DescriptorSet.Update`) and it is a self-contained decision
  about the descriptor surface rather than about acceleration structures. **Stop
  and ask** whether to (a) implement it here, or (b) drop Step 13, file it as a
  follow-up issue, and ship #202 with a documented "TLAS binding lands in
  #NNN" note. Do not decide this unilaterally.
- **OPEN-2 — Step 0's VUID verification may contradict a guard.** If the registry
  says a rule this plan encodes as a hard guard is not actually a valid-usage
  requirement (most likely candidates: the 256-byte offset multiple, and the
  "top level ⇒ exactly one `Instances` geometry" pairing), report back rather
  than silently keeping or dropping the guard.
- **OPEN-3 — no RT-capable CI host.** Every Tier-3 test will skip on
  `windows-latest`. If the maintainer's host is unavailable when this lands,
  say so in the PR; do not weaken the tests to make them run.
