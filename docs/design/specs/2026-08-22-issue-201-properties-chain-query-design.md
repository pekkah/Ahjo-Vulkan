# Physical-device properties-chain query — a generic `TryGetProperties<T>` on `PhysicalDevice`

**Driving issue:** none of its own. This is the deferral recorded in
[#201](https://github.com/pekkah/Ahjo-Vulkan/issues/201)'s spec
(`docs/design/specs/2026-08-22-issue-201-mesh-shader-design.md`, Evidence
§"Gap worth recording" and the "Why not the alternatives" bullet on
`VkPhysicalDeviceMeshShaderPropertiesEXT`). It lands on the same branch,
`issue-201-mesh-shader`.
**Must land consistently with:** [#202](https://github.com/pekkah/Ahjo-Vulkan/issues/202)
(KHR acceleration structure + ray query — needs
`VkPhysicalDeviceAccelerationStructurePropertiesKHR::minAccelerationStructureScratchOffsetAlignment`
before it can allocate scratch correctly), [#53](https://github.com/pekkah/Ahjo-Vulkan/issues/53)
(the duplicate-`sType` chain rule that shaped `ConfigureFeatures`),
[#07](https://github.com/pekkah/Ahjo-Vulkan/issues/7) (`PhysicalDeviceInfo`
as a picker-scoped `ref struct`).
**Test strategy constrained by:** [#32](https://github.com/pekkah/Ahjo-Vulkan/issues/32)
(wrapper suite is Windows-only), [#158](https://github.com/pekkah/Ahjo-Vulkan/issues/158)
(`TestGate` / `[gate:*]` classification).
**Date:** 2026-08-22
**Naming note:** the paired files are named without an `issue-NN` segment
because there is no issue; everything else follows `docs/design/CLAUDE.md`.

## Problem

A caller cannot read *any* `VkPhysicalDeviceProperties2` `pNext` extension
struct through the wrapper. Three concrete consequences, in descending order
of severity:

**1. `DrawMeshTasks` has no bound to check against.** The mesh-shader surface
now in the working tree forwards three commands
(`Recording/CommandRecorder.cs:606, 636, 669`) whose group counts are bounded
by `VkPhysicalDeviceMeshShaderPropertiesEXT::maxTaskWorkGroupCount[i]` /
`maxTaskWorkGroupTotalCount` (with a task stage,
`VUID-vkCmdDrawMeshTasksEXT-TaskEXT-07322`…`-07325`) or
`maxMeshWorkGroupCount[i]` / `maxMeshWorkGroupTotalCount` (without,
`-07326`…`-07329`). The recorder's own XML doc states the gap in prose:

> *"The wrapper has no accessor for `VkPhysicalDeviceMeshShaderPropertiesEXT`
> today, so the caller must obtain those limits itself or stay well inside the
> guaranteed minimums."* — `Recording/CommandRecorder.cs:602-604`

There is no supported way for the caller to "obtain those limits itself":
`PhysicalDevice.Handle` is `internal` (`Lifecycle/PhysicalDevice.cs:23`), so a
consumer cannot call `vkGetPhysicalDeviceProperties2` directly either. The
only remaining options are hard-coding a constant or guessing — exceeding the
limit is a VUID violation with undefined behaviour, not a soft warning.

**2. #202 cannot allocate acceleration-structure scratch.**
`VkPhysicalDeviceAccelerationStructurePropertiesKHR::minAccelerationStructureScratchOffsetAlignment`
(`src/Ahjo.Vulkan.Native/Generated/VkPhysicalDeviceAccelerationStructurePropertiesKHR.cs:31`)
is not optional knowledge — a scratch buffer at the wrong alignment is a build
failure, and there is no defensible default.

**3. Nothing in the wrapper reads a chained property at all.** A repo-wide
grep for `vkGetPhysicalDeviceProperties2` outside `Generated/` returns
**exactly one** call site — `Lifecycle/Instance.cs:269`, whose chain is
commented `// 2a. Properties chain (root only).` (`:265`). The wrapper builds
a `ChainBuilder<VkPhysicalDeviceProperties2>`, calls `Root()`, and pushes
nothing.

The asymmetry with the features side is the actual defect.
`DeviceDescription.ConfigureFeatures` (`Lifecycle/DeviceDescription.cs:24`)
hands the caller a live `ChainBuilder<VkDeviceCreateInfo>`
(`Lifecycle/PhysicalDevice.cs:255`), so *writing* an arbitrary extension
feature struct into a chain is a first-class, documented operation — the
mesh-shader tests use it verbatim
(`tests/Ahjo.Vulkan.Tests/MeshShaderTests.cs:864-873`). *Reading* an arbitrary
extension property struct has no equivalent.

## Evidence

### The generated support already exists — no codegen change

This is the load-bearing finding and it removes the largest cost this design
could have had.

`tools/Ahjo.Vulkan.StructExtendsGen` already models `vk.xml`'s `structextends`
attribute as a pair of interfaces in `src/Ahjo.Vulkan.Native/IChainable.cs`:

```csharp
public interface IChainable<TRoot> where TRoot : unmanaged
{
    static abstract VkStructureType SType { get; }     // IChainable.cs:23
}
public interface IChainRoot
{
    static abstract VkStructureType RootSType { get; } // IChainable.cs:34
}
```

and emits one partial-struct file per relationship into
`src/Ahjo.Vulkan.Native/Generated/Chains/` — **1062 files**. The three that
matter here already exist and already carry the `sType`:

| Struct | Generated file | Interface |
|---|---|---|
| `VkPhysicalDeviceProperties2` | `Chains/VkPhysicalDeviceProperties2.Root.g.cs` | `IChainRoot`, `RootSType => VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_PROPERTIES_2` |
| `VkPhysicalDeviceMeshShaderPropertiesEXT` | `Chains/VkPhysicalDeviceMeshShaderPropertiesEXT.Chain.g.cs` | `IChainable<VkPhysicalDeviceProperties2>`, `SType => …MESH_SHADER_PROPERTIES_EXT` |
| `VkPhysicalDeviceAccelerationStructurePropertiesKHR` | `Chains/VkPhysicalDeviceAccelerationStructurePropertiesKHR.Chain.g.cs` | `IChainable<VkPhysicalDeviceProperties2>`, `SType => …ACCELERATION_STRUCTURE_PROPERTIES_KHR` |

A count of the generated files implementing `IChainable<VkPhysicalDeviceProperties2>`
returns **110**. Every one of `VkPhysicalDeviceVulkan11Properties`,
`…Vulkan12Properties`, `…Vulkan13Properties`, `…Vulkan14Properties`,
`…SubgroupProperties` and `…DriverProperties` is among them.

**Consequence:** the question the task poses in part B — "how does the caller
supply `sType` without reflection?" — is already answered by generated code.
`T.SType` is a static-abstract read on a value-type generic parameter. **No
`tools/*.rsp` change, no `/regen-bindings`, nothing under `Generated/` moves.**

### The static-abstract mechanism is already proven AOT-clean

`ChainBuilder<TRoot>.Push<T>()` does exactly the thing this design needs:

```csharp
public ref T Push<T>() where T : unmanaged, IChainable<TRoot>
{
    …
    WriteHeader(offset, T.SType);   // Memory/ChainBuilder.cs:97
```

and `samples/AotSmoke` — published with `PublishAot=true` in CI per
`src/Ahjo.Vulkan/CLAUDE.md` — instantiates it
(`chain.Push<VkPhysicalDeviceVulkan11Features>()`, `samples/AotSmoke:330`).
`docs/aot-notes.md:13` already records the general finding for
`IVulkanHandle<T>`: *"The JIT (and ILC) devirtualizes and inlines through the
constrained generic — no runtime type lookup, no reflection."* The `unmanaged`
constraint additionally forbids reference type arguments, so there is no
`__Canon` shared-code path and every instantiation is exact.

### Reading a chain is not symmetric with writing one, and the difference is a hazard

`ConfigureFeatures` works as a callback because the chain is **consumed
immediately** after the callback returns (`Lifecycle/PhysicalDevice.cs:255`
→ `:267` validate → `:278` `vkCreateDevice`). A properties read is
three-phase: push node → call `vkGetPhysicalDeviceProperties2` → read node
back. A single configurer delegate cannot express phase three, because the
scratch that backs the chain dies with the wrapper method's frame.

More importantly, the read chain carries a hazard the write chain also has,
and the repo has already been burned by it. `Instance.PickPhysicalDevice`
carries a ten-line comment explaining why the 1.4 features struct is gated out
of the **read-back** chain on a sub-1.4 device:

> *"SwiftShader (and other 1.3-only ICDs) log `"UNSUPPORTED: curExtension->sType: 55"`
> — the `VkPhysicalDeviceVulkan14Features` sType — when the struct sits in the
> read-back chain, and the cumulative state damage manifests as later SIGSEGVs
> in unrelated entry points."* — `Lifecycle/Instance.cs:272-281`

The same gate is repeated on the create path with the same reasoning
(`Lifecycle/PhysicalDevice.cs:228-246`). So: **the wrapper's own operating
experience says an unrecognized `sType` in a `vkGetPhysicalDevice*2` chain is
not safely ignored by real ICDs**, even though the spec says implementations
must skip what they do not recognise. A properties-query API that lets a
caller put an arbitrary node in front of any driver would reintroduce exactly
that bug class.

The validation rules themselves impose no obstacle. Scanning
`native/downloaded/Vulkan-Headers-vulkan-sdk-1.4.341.0/registry/validusage.json`
(the tag pinned at `Directory.Build.props:18`), `VkPhysicalDeviceProperties2`
carries three VUIDs total:

| VUID | Rule | Satisfied by |
|---|---|---|
| `-sType-sType` | root `sType` must be `…PROPERTIES_2` | `ChainBuilder.Root()` writes `TRoot.RootSType` (`ChainBuilder.cs:76`) |
| `-pNext-pNext` | each node must be one of the listed structextends targets | the `IChainable<VkPhysicalDeviceProperties2>` constraint, at compile time |
| `-sType-unique` | no duplicate `sType` in the chain | a one-node chain, structurally |

`vkGetPhysicalDeviceProperties2` itself adds only the two handle/pointer
parameter VUIDs. There is **no** VUID forbidding a node whose extension the
device does not support — which is precisely why the wrapper, not the
validation layer, has to be the one that refuses.

### The in-repo precedent for a narrow typed accessor

`PhysicalDevice.GetMemoryLimits()` (`Lifecycle/PhysicalDevice.cs:131-143`)
returns a five-field `DeviceMemoryLimits` record struct
(`Memory/DeviceMemoryLimits.cs:14`) whose doc states the rationale verbatim:

> *"A narrow accessor rather than the whole limits struct on purpose: the full
> `VkPhysicalDeviceLimits` is reachable only through `PhysicalDeviceInfo`,
> which is a `ref struct` that cannot escape the device-picker callback. These
> five are the ones a memory allocator needs after the device exists."*
> — `Memory/DeviceMemoryLimits.cs:8-13`

That is the same problem statement this spec opens with, one level down the
chain. Its sibling precedent is `Device.TimestampPeriod`
(`Lifecycle/Device.cs:667`), documented as *"Read on demand from the physical
device into a stack struct — zero-alloc, no caching … Typically read once at
setup."* Both establish: read on demand, no cache, narrow projection.

### Where the capability answer lives today

`PhysicalDeviceInfo.SupportsExtension(ReadOnlySpan<byte>)`
(`Lifecycle/PhysicalDeviceInfo.cs:68-80`) is the allocation-free device
extension check — but `PhysicalDeviceInfo` is a `ref struct`
(`:11`) that cannot escape the picker callback (`Lifecycle/Instance.cs:218-223`).
`PhysicalDevice` itself has **no** extension check: its public surface is
`RawHandle`, `IsNull`, `ObjectType`, `GetFormatProperties`,
`SupportsOptimalTilingFeature`, `SupportsPresent`, `GetMemoryLimits`,
`CreateDevice` (`Lifecycle/PhysicalDevice.cs:32-145`). The instance-level
counterpart `Instance.IsExtensionSupported` exists in both `Utf8Name` and
`ReadOnlySpan<byte>` forms (`Lifecycle/Instance.cs:524-563`) and rents from
`ArrayPool<VkExtensionProperties>.Shared` (`:546-560`).

So the gate this design needs does not exist on the type that needs it, and
adding it is a strictly additive four-line method with two established
precedents to copy.

The name-comparison helper is already duplicated twice —
`Instance.PointerStringEquals` (`Lifecycle/Instance.cs:473-481`) and
`PhysicalDeviceInfo.NameEquals` (`Lifecycle/PhysicalDeviceInfo.cs:82-89`),
byte-for-byte the same loop. #201's spec explicitly rejected promoting them
into `Internal/Utf8.cs` as out of scope. This design does not add a third
copy: `PhysicalDeviceInfo.NameEquals` becomes `internal static` (one
visibility keyword, no public surface change) and `PhysicalDevice` calls it.

### Consumer audit — who would call this, and what the call site looks like

A grep of `src/`, `samples/` and `tests/` for a properties-chain read returns
**zero** existing consumers, because the capability does not exist. The
prospective ones:

| Consumer | Struct | Fields | Status |
|---|---|---|---|
| `CommandRecorder.DrawMeshTasks*` callers (Ahjo Lane L) | `VkPhysicalDeviceMeshShaderPropertiesEXT` | `maxMeshWorkGroupCount[3]`, `maxMeshWorkGroupTotalCount`, `maxMeshWorkGroupInvocations` + the task equivalents | the gap this spec closes |
| #202 scratch allocation | `VkPhysicalDeviceAccelerationStructurePropertiesKHR` | `minAccelerationStructureScratchOffsetAlignment` (one `uint`), `maxGeometryCount`/`maxInstanceCount`/`maxPrimitiveCount` (three `ulong`) | next |
| `tests/Ahjo.Vulkan.Tests` unconditional coverage | `VkPhysicalDeviceVulkan11Properties` | `subgroupSize`, `maxMemoryAllocationSize` | new |

The mesh struct's array limits are `[InlineArray(3)]` buffers
(`Generated/VkPhysicalDeviceMeshShaderPropertiesEXT.cs:95-117`), so
`props.maxMeshWorkGroupCount[0]` indexes directly — the raw struct is *usable*,
not painful. What is genuinely error-prone about it is that it carries **two
parallel limit sets** (task-prefixed and mesh-prefixed, 28 fields total) and
which one applies depends on whether the bound pipeline has a task stage. The
acceleration-structure struct has no such ambiguity: it is nine flat scalars
(`Generated/VkPhysicalDeviceAccelerationStructurePropertiesKHR.cs:10-31`).

### Hot-path classification

`src/Ahjo.Vulkan/CLAUDE.md` lists the zero-per-frame-allocation directories as
`Recording/**`, `Sync/**`, `Pools/**`, `Memory/**`. **`Lifecycle/**` is not on
that list**, and everything this design adds lives in `Lifecycle/`. The
comparable existing methods say the same thing about themselves:
`GetMemoryLimits` and `TimestampPeriod` are documented as setup-time reads,
and `docs/benchmarks.md` has no row for either.

## Decision

Four parts. Everything is managed wrapper surface in `Lifecycle/`; no
`tools/*.rsp` change, no regen, nothing under `Generated/` moves.

### A. A generic, gated `TryGetProperties<T>` on `PhysicalDevice`

Two `Try` overload families, both of which **refuse to put a node in front of
a driver that cannot be expected to recognise it**. There is deliberately no
ungated `GetProperties<T>`.

```csharp
// Lifecycle/PhysicalDevice.cs
public bool TryGetProperties<T>(ReadOnlySpan<byte> utf8ExtensionName, out T properties)
    where T : unmanaged, IChainable<VkPhysicalDeviceProperties2>;

public bool TryGetProperties<T>(Utf8Name extension, out T properties)
    where T : unmanaged, IChainable<VkPhysicalDeviceProperties2>;

public bool TryGetProperties<T>(VulkanVersion minimumApiVersion, out T properties)
    where T : unmanaged, IChainable<VkPhysicalDeviceProperties2>;

public bool SupportsExtension(ReadOnlySpan<byte> utf8ExtensionName);
public bool SupportsExtension(Utf8Name extension);
```

Semantics, identical across the three:

- Gate fails ⇒ return `false`, `properties = default`, **no native call with
  the node in the chain**.
- Gate passes ⇒ build a two-node `ChainBuilder<VkPhysicalDeviceProperties2>`
  (`Root()` then `Push<T>()`), call `vkGetPhysicalDeviceProperties2`, copy the
  node out by value, return `true`.

The `sType` question answers itself: `ChainBuilder.Push<T>()` writes
`T.SType` (`Memory/ChainBuilder.cs:97`) from the generated static abstract.
The caller never passes an `sType`, cannot pass a wrong one, and cannot chain
a struct `vk.xml` does not permit on `VkPhysicalDeviceProperties2` — that is a
compile error, not a runtime check.

Two overload families rather than one because the two gates answer different
questions and neither subsumes the other:

- **Extension gate** — for extension-only structs
  (`VkPhysicalDeviceMeshShaderPropertiesEXT`,
  `VkPhysicalDeviceAccelerationStructurePropertiesKHR`). Truth source is
  `vkEnumerateDeviceExtensionProperties`.
- **Version gate** — for core-promoted structs
  (`VkPhysicalDeviceVulkan11Properties` … `Vulkan14Properties`), which have no
  extension name to name, and where the SwiftShader failure at
  `Instance.cs:272-281` is *literally* a version mismatch. Truth source is
  `VkPhysicalDeviceProperties.apiVersion`, read with a plain
  `vkGetPhysicalDeviceProperties` before the chained call — the same
  read-twice shape `CreateDevice` already uses (`:147-148` and `:238-239`).

**Sharp edge, stated rather than papered over:** a struct from an extension
that was later promoted to core (e.g. `VkPhysicalDeviceDriverProperties`,
`VK_KHR_driver_properties` → Vulkan 1.2) is reachable through *either* gate,
and on a device that supports it via core promotion the extension may or may
not still be advertised. Callers of such structs should use the version
overload. The XML docs must say this.

Scratch is a `stackalloc` sized from the two compile-time-known sizes:

```csharp
Span<byte> scratch = stackalloc byte[sizeof(VkPhysicalDeviceProperties2) + Unsafe.SizeOf<T>() + 16];
```

(the `+16` covers ChainBuilder's two 8-byte absolute-address pads,
`Memory/ChainBuilder.cs:138-146`). ILC constant-folds both terms per
instantiation. No `ArrayPool`, no heap, no arbitrary byte budget to keep in
sync with a growing registry.

The returned node's `pNext` is null by construction: `ChainBuilder.WriteHeader`
sets it (`ChainBuilder.cs:163-169`) and the node is the chain tail, so nothing
overwrites it. The copy-out therefore cannot hand the caller a pointer into
dead stack. This is asserted by a test rather than defended by a redundant
store.

### B. `PhysicalDevice.SupportsExtension` as the gate's truth source

Mirrors `PhysicalDeviceInfo.SupportsExtension` in name and behaviour
(`Lifecycle/PhysicalDeviceInfo.cs:68-80`) and `Instance.IsExtensionSupported`
in implementation (`Lifecycle/Instance.cs:530-563`): enumerate with
`vkEnumerateDeviceExtensionProperties(Handle, null, …)`, rent the buffer from
`ArrayPool<VkExtensionProperties>.Shared`, linear-scan, return the array.

This is useful in its own right — today a caller who has a `PhysicalDevice`
(from `PickPhysicalDevice`, or from `Device.PhysicalDevice`,
`Lifecycle/Device.cs:27`) has no way to ask "does this GPU advertise
`VK_EXT_mesh_shader`?" outside the picker callback.

**Not allocation-free**, and that is fine: it rents and returns a pooled
array, exactly as `Instance.IsExtensionSupported` does, and `Lifecycle/` is not
on the hot-path list in `src/Ahjo.Vulkan/CLAUDE.md`. The version-gated
overload of `TryGetProperties` is allocation-free outright (stack only).

### C. `MeshShaderLimits` — a typed accessor, and a rule for when to add one

Ship both: the generic mechanism *and* a narrow typed projection for mesh.

```csharp
// Lifecycle/MeshShaderLimits.cs
public readonly record struct MeshShaderLimits
{
    public uint MaxTaskWorkGroupCountX { get; init; }
    public uint MaxTaskWorkGroupCountY { get; init; }
    public uint MaxTaskWorkGroupCountZ { get; init; }
    public uint MaxTaskWorkGroupTotalCount { get; init; }
    public uint MaxTaskWorkGroupInvocations { get; init; }
    public uint MaxMeshWorkGroupCountX { get; init; }
    public uint MaxMeshWorkGroupCountY { get; init; }
    public uint MaxMeshWorkGroupCountZ { get; init; }
    public uint MaxMeshWorkGroupTotalCount { get; init; }
    public uint MaxMeshWorkGroupInvocations { get; init; }
}

// Lifecycle/PhysicalDevice.cs
public bool TryGetMeshShaderLimits(out MeshShaderLimits limits);
```

Rationale, and the general rule this establishes:

> **Ship a typed projection when the raw properties struct's *interpretation*
> is ambiguous; use the generic when it is not.**

`VkPhysicalDeviceMeshShaderPropertiesEXT` qualifies: 28 fields, two parallel
limit sets, and picking the wrong one is a silent VUID violation because
`maxTaskWorkGroupCount` and `maxMeshWorkGroupCount` differ on real hardware and
the choice depends on the *bound pipeline's stages*, which the recorder
deliberately does not track (#201's Decision, "Runtime bounds checks on
`groupCount*`"). Flattening the `[InlineArray]` buffers into X/Y/Z members
makes the applicable triple nameable at the call site. Field set = exactly what
`VUID-vkCmdDrawMeshTasksEXT-TaskEXT-07322`…`-07329` name; the same "narrow on
purpose" doc paragraph as `DeviceMemoryLimits` (`Memory/DeviceMemoryLimits.cs:8-13`).

Deliberately excluded from the projection: `maxMeshOutputVertices`,
`maxMeshOutputPrimitives`, `maxTaskWorkGroupSize` / `maxMeshWorkGroupSize`,
the payload/shared-memory sizes and the four `prefers*` hints. Those are
shader-authoring constants (they bound the `layout(…) out` declaration and the
`local_size_*`), not per-draw bounds, and a caller who needs them can read the
raw struct through the generic in one line. Additive later.

`VkPhysicalDeviceAccelerationStructurePropertiesKHR` does **not** qualify: nine
flat scalars, no ambiguity. #202 uses the generic and ships no
`AccelerationStructureLimits` type.

The caller-facing shape, both ways:

```csharp
// typed
if (gpu.TryGetMeshShaderLimits(out var mesh))
    groupCountX = Math.Min(clusterCount, mesh.MaxMeshWorkGroupCountX);

// generic — same device, raw struct
if (gpu.TryGetProperties<VkPhysicalDeviceMeshShaderPropertiesEXT>(
        VulkanExtensions.ExtMeshShader, out var raw))
    uint maxVerts = raw.maxMeshOutputVertices;
```

`Recording/CommandRecorder.cs:602-604` — the "no accessor today" sentence — is
replaced with a `<see cref="PhysicalDevice.TryGetMeshShaderLimits"/>` pointer.
That is the only edit outside `Lifecycle/`.

### D. How #202 plugs in, unchanged

Verbatim, with no new mechanism:

```csharp
if (gpu.TryGetProperties<VkPhysicalDeviceAccelerationStructurePropertiesKHR>(
        VulkanExtensions.KhrAccelerationStructure, out var asProps))
{
    ulong scratchAlign = asProps.minAccelerationStructureScratchOffsetAlignment;
    // round the scratch buffer offset up to scratchAlign before building
}
```

What #202 must add and what it must not:

- **Must add:** `VulkanExtensions.KhrAccelerationStructure` (and
  `KhrRayQuery`, `KhrDeferredHostOperations`) plus the matching
  `DeviceExtensionNames` literals — the same one-property-per-extension
  addition #201 made for `ExtMeshShader`
  (`Rendering/VulkanExtensions.cs:53-61`, `Internal/DeviceExtensionNames.cs:22`).
- **Must not add:** any properties-query mechanism, any `sType` plumbing, any
  `AccelerationStructureLimits` projection (see the rule in Decision C), any
  change to `TryGetProperties`.
- **Free:** `VkPhysicalDeviceAccelerationStructurePropertiesKHR` already
  implements `IChainable<VkPhysicalDeviceProperties2>`
  (`Generated/Chains/VkPhysicalDeviceAccelerationStructurePropertiesKHR.Chain.g.cs`),
  so #202's call site compiles the day this lands.

This is the same "additive, a new call not a new mechanism" test #201 applied
to `DeviceFunctionTable`'s extension gating, and it passes for the same reason:
the mechanism is keyed on data (an extension name / a version), not on a
per-extension code path.

### Why not the alternatives

- **A configurer delegate mirroring `ConfigureFeatures`
  (`PhysicalDevicePropertyChainConfigurer(ref ChainBuilder<VkPhysicalDeviceProperties2>)`)** —
  rejected: `ConfigureFeatures` works because the chain is consumed inside the
  same method (`Lifecycle/PhysicalDevice.cs:255` → `:278`). A read is
  push → query → *read back*, and the third phase happens after the callback's
  `ref` returns are dead. Making it work needs either two delegates (configure,
  then read) or a callback that receives the filled chain — both strictly worse
  to call than `TryGetProperties<T>` for the single-struct case, which is what
  both known consumers need. Consistency with `ConfigureFeatures` is real but
  it is consistency with a shape whose precondition does not hold here.
- **An ungated `GetProperties<T>()` alongside the `Try` overloads** —
  rejected: the failure mode is silent zeros. A caller who queries mesh
  properties on a non-mesh device gets `maxMeshWorkGroupCount = 0` and clamps
  its dispatch to nothing, and the wrapper additionally exposes every driver to
  the unrecognized-`sType` class of bug the repo already hit
  (`Lifecycle/Instance.cs:272-281`). The version-gated overload covers the only
  legitimate use of an ungated call (core structs) while still gating.
- **`TryGetProperties<T>(out T)` with the extension name derived from `T`
  through generated metadata** — rejected, and this is the one that would have
  cost a codegen change. `StructExtendsGen` would have to emit a
  `static abstract ReadOnlySpan<byte> ExtensionName` (and, for promoted
  structs, a *core version* as well, because `vk.xml` associates
  `VkPhysicalDeviceDriverProperties` with both `VK_KHR_driver_properties` and
  Vulkan 1.2). That is a new interface on 110+ generated files, a regen, and a
  new `Generated/` surface to keep stable — to remove one argument from two call
  sites in the whole repo. The mismatch risk it would eliminate is already
  eliminated for the mesh case by the typed accessor in Decision C, which
  hard-codes the pairing once.
- **Widening `PhysicalDeviceInfo` with a properties-query method** — rejected
  as redundant: `PhysicalDeviceInfo.Device` is a `PhysicalDevice`
  (`Lifecycle/PhysicalDeviceInfo.cs:19`), so a picker already writes
  `info.Device.TryGetProperties<…>(…)`. Duplicating the surface on a `ref
  struct` that already carries `SupportsExtension` buys nothing and doubles the
  XML docs.
- **Extending the picker's existing properties chain
  (`Lifecycle/Instance.cs:265-270`) with the structs the wrapper cares about** —
  rejected: it would push extension `sType`s at every GPU on the host including
  ones that do not support them (the `Instance.cs:272-281` bug), it would need
  a per-extension gate for each struct pushed, and the picker's chain is a
  `ref struct` view that dies at callback return — the exact reason
  `GetMemoryLimits` exists (`Memory/DeviceMemoryLimits.cs:8-13`).
- **Caching the extension list (or the properties) on `PhysicalDevice`** —
  rejected for this design, not precluded later. `Device.TimestampPeriod` is
  documented as *"Read on demand from the physical device into a stack struct —
  zero-alloc, no caching"* (`Lifecycle/Device.cs:657-666`) and
  `GetMemoryLimits` does the same; a cache would add a lifetime and a
  thread-safety question to a class the `Instance` already caches one of per
  handle, for a setup-time call.
- **`GetProperties<T>` returning `T` by value instead of `bool` + `out`** —
  rejected: there is no in-band way to express "the device does not support
  this", and throwing would make the common "probe then branch" shape a
  try/catch. `Try*` + `out` is the repo idiom
  (`QueryPool.TryGetResults`, `Lifecycle/Device.cs:602`).
- **Putting the query on `Device` rather than `PhysicalDevice`** — rejected:
  properties are a property of the *physical* device and are readable before
  any device exists, which is exactly when a picker needs them.
  `Device.PhysicalDevice` is public (`Lifecycle/Device.cs:27`), so the
  post-device case costs one dot.
- **Runtime bounds checking inside `CommandRecorder.DrawMeshTasks*` now that
  the limits are readable** — rejected, unchanged from #201's Decision: the
  recorder cannot see the bound pipeline's stages, so it cannot tell `-07322`
  from `-07326`, and reading properties per draw would put a native call on a
  `Recording/` hot path. The limits are for the caller to apply at setup.
- **Shipping `AccelerationStructureLimits` now, ahead of #202** — rejected: no
  consumer, and the Decision-C rule says a nine-scalar unambiguous struct does
  not earn a projection anyway.
- **Promoting the duplicated `PointerStringEquals` / `NameEquals` helpers into
  `Internal/Utf8.cs`** — rejected, consistent with #201's identical rejection.
  This design makes `PhysicalDeviceInfo.NameEquals` `internal static` and calls
  it; that is one keyword, not a refactor, and it adds no third copy.

## Invariants honored

- **UTF-8 literals.** The only `const char*` this design touches is an
  extension name, taken as `ReadOnlySpan<byte>` (from
  `DeviceExtensionNames.MeshShader`, `Internal/DeviceExtensionNames.cs:22`) or
  `Utf8Name` (from `VulkanExtensions.ExtMeshShader`,
  `Rendering/VulkanExtensions.cs:61`). No `Encoding.UTF8.GetBytes` anywhere;
  the comparison walks the driver-returned `VkExtensionProperties.extensionName`
  against the literal, never the other way.
- **Native AOT.** One generic method with an `unmanaged` +
  `IChainable<VkPhysicalDeviceProperties2>` constraint, resolving `T.SType` as
  a static-abstract on a value type — the pattern `docs/aot-notes.md:13`
  already blesses and `ChainBuilder.Push<T>` already ships
  (`Memory/ChainBuilder.cs:90-105`), exercised under `PublishAot` by
  `samples/AotSmoke`. No reflection, no `MakeGenericMethod`, no runtime type
  map, no trim-unsafe surface.
- **Zero per-frame allocations.** Not applicable — `Lifecycle/` is not on the
  hot-path list in `src/Ahjo.Vulkan/CLAUDE.md` (`Recording/`, `Sync/`,
  `Pools/`, `Memory/`), and both existing analogues (`GetMemoryLimits`,
  `Device.TimestampPeriod`) are documented setup-time reads. Stated so
  `bench-coverage-checker` does not ask for a benchmark. Accounting anyway:
  the version-gated overload allocates nothing (one `stackalloc`); the
  extension-gated overloads and `SupportsExtension` rent and return a pooled
  `VkExtensionProperties[]`, matching `Instance.IsExtensionSupported`
  (`Lifecycle/Instance.cs:546-560`); `MeshShaderLimits` is a `readonly record
  struct`, so the projection does not box.
- **Generated code untouched.** The design consumes
  `Generated/Chains/*.g.cs` and `IChainable.cs` exactly as emitted. **No
  `tools/*.rsp` change, no `/regen-bindings`.**
- **`TreatWarningsAsErrors`.** No suppressions. One `internal`-visibility flip
  on an existing private helper; no public API is removed or changed, only
  added to.

## Test strategy (constrained by #32 and #158)

The design deliberately admits a properties struct **every** conformant device
must expose, so the general mechanism gets unconditional coverage on any host
with an ICD.

**Tier 1 — `[gate:driver]` only. Covers the whole mechanism.**
`VkPhysicalDeviceVulkan11Properties` is core Vulkan **1.2** — the "11" names
the feature set it aggregates, not the version that defines it; `vulkan_core.h`
declares it between `#define VK_VERSION_1_2` and `#define VK_VERSION_1_3` — and
the wrapper's floor is 1.3 (`Lifecycle/PhysicalDevice.cs:149-154`), so it is
guaranteed. Gate it on `V1_2`, never on `V1_1`: a 1.1 physical device would
pass a `V1_1` gate and take sType 50 into a read-back chain its ICD never
learned, which is the `UNSUPPORTED: curExtension->sType` class the gate exists
to prevent.

- `TryGetProperties<VkPhysicalDeviceVulkan11Properties>(VulkanVersion.V1_2, …)`
  returns `true`; the returned `sType` is
  `VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_1_PROPERTIES` (proves the
  mechanism wrote it), `pNext` is null (proves no dangling stack pointer
  escapes), `subgroupSize` is a non-zero power of two and
  `maxMemoryAllocationSize` is non-zero (proves the *driver* filled it, not
  just that the struct came back zeroed).
- Extension gate consistency, also unconditional:
  `TryGetProperties<VkPhysicalDeviceMeshShaderPropertiesEXT>(VulkanExtensions.ExtMeshShader, out var p)`
  returns exactly `gpu.SupportsExtension(DeviceExtensionNames.MeshShader)`,
  and when it returns `false`, `p` equals `default`. Green on a mesh-capable
  host and on a host without one — the assertion is the *relationship*.
- Version gate consistency: `TryGetProperties<VkPhysicalDeviceVulkan14Properties>(VulkanVersion.V1_4, out _)`
  returns exactly `apiVersion >= VulkanVersion.V1_4.Packed`, read from the
  picker's `PhysicalDeviceInfo.Properties`. This is the SwiftShader regression
  test (`Lifecycle/Instance.cs:272-281`) in assertion form.
- `SupportsExtension` agrees with `PhysicalDeviceInfo.SupportsExtension` for
  the same name on the same GPU, and returns `false` for a name no device has.
- `TryGetMeshShaderLimits` on a non-mesh device returns `false` with
  `limits == default`.

**Tier 2 — `[gate:feature]`, needs a driver exposing `VK_EXT_mesh_shader`.**
Gated through the try-create helper the mesh work already added
(`tests/Ahjo.Vulkan.Tests/MeshShaderTests.cs:827-882`):

- `TryGetMeshShaderLimits` returns `true` and every one of its ten fields is
  non-zero.
- The projection is exact: each `MeshShaderLimits` field equals the
  corresponding field of a raw
  `TryGetProperties<VkPhysicalDeviceMeshShaderPropertiesEXT>` read on the same
  GPU. This tests the wrapper's mapping, not the driver's numbers — no
  hard-coded spec minimum to get wrong.

**What CI runs.** Tier 1 is the coverage that matters and it runs on every
Windows lane with an ICD (#32). Tier 2 is expected to report
`[gate:feature]` skips on the hosted runner, the same honest position #201
took; the classification is what CI checks (`tests/Shared/TestGate.cs:5-18`).

No new gate *class* is introduced, so `docs/ci-coverage.md` needs no change.

## Benchmarks

**None, deliberately.** `Lifecycle/` is not among the zero-per-frame-allocation
directories in `src/Ahjo.Vulkan/CLAUDE.md`, the two closest existing accessors
(`PhysicalDevice.GetMemoryLimits`, `Device.TimestampPeriod`) have no rows in
`docs/benchmarks.md`, and every method here issues a native driver query — the
wrong thing to have on a per-frame path regardless of its allocation profile.
`docs/benchmarks.md` is not modified.

## Uncertainty, stated

- **"Drivers must skip unrecognized `pNext` nodes" is spec-true and
  field-false.** The design gates because of the repo's own observation
  (`Lifecycle/Instance.cs:272-281`), which was on a software rasterizer
  (SwiftShader) and on a *features* chain. Whether a modern hardware ICD would
  actually misbehave on an unrecognized *properties* node has not been
  measured here. The gate costs one extension enumeration and removes the
  question; if measurement later shows it is unnecessary, the ungated path is
  additive.
- **No consumer to audit.** Both prospective consumers (Ahjo Lane L, #202) are
  out of this repo or unwritten. The chosen signature shape is inferred from
  the VUID field sets and from `Try*`/`out` precedent, not from a call site.
  A mismatch would be a naming question, not a redesign.
- **The `MeshShaderLimits` field set is a judgement call.** It is exactly the
  fields the `vkCmdDrawMeshTasksEXT` VUIDs name. A consumer authoring mesh
  shaders dynamically would also want `maxMeshOutputVertices` /
  `maxMeshOutputPrimitives`; they are reachable through the generic in the
  meantime and adding them to the projection is additive.
- **The stack-buffer sizing is arithmetic, not measured.**
  `sizeof(VkPhysicalDeviceProperties2) + Unsafe.SizeOf<T>() + 16` is derived
  from `ChainBuilder`'s two 8-byte absolute-address pads
  (`Memory/ChainBuilder.cs:138-146`); it has not been executed. If
  `ChainBuilder` ever throws `"Chain buffer too small for next node."`
  (`ChainBuilder.cs:172-173`) from this path, the pad term is the thing to
  raise — not the design.
- **CI mesh availability is unknown**, unchanged from #201. Tier 2 is written
  to skip cleanly and is expected to.
