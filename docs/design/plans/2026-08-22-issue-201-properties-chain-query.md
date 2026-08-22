Paired with [../specs/2026-08-22-issue-201-properties-chain-query-design.md](../specs/2026-08-22-issue-201-properties-chain-query-design.md) — read it first; this plan only says *how*.

# Implementation plan — physical-device properties-chain query

Managed wrapper surface only, all of it in `src/Ahjo.Vulkan/Lifecycle/` except
one XML-doc edit in `Recording/`. **No `tools/*.rsp` change, no
`/regen-bindings`, nothing under `src/*/Generated/` moves** — every generated
symbol this needs already exists (`src/Ahjo.Vulkan.Native/IChainable.cs:20-24`
and the 110 `Generated/Chains/*.Chain.g.cs` files implementing
`IChainable<VkPhysicalDeviceProperties2>`).

Branch: `issue-201-mesh-shader` (this rides on top of the uncommitted
mesh-shader work; all line numbers below are the current working-tree state).

Steps 1–2 are the gate. Steps 3–4 are the generic query. Step 5 is the mesh
projection. Step 6 is the one doc edit in `Recording/`. Steps 7–8 are tests
and docs.

---

## Step 1 — `Lifecycle/PhysicalDeviceInfo.cs`: widen one helper

Change the access modifier on `NameEquals` (`:82`) from `private` to
`internal`:

```csharp
internal static unsafe bool NameEquals(sbyte* name, ReadOnlySpan<byte> target)
```

Body unchanged. Add one sentence to its (currently absent) doc explaining that
`PhysicalDevice.SupportsExtension` shares it so the repo does not gain a third
copy of the NUL-terminated-name comparison already duplicated at
`Lifecycle/Instance.cs:473-481`.

Do **not** move it to `Internal/Utf8.cs` — #201's spec rejected that refactor
and this plan does not relitigate it.

## Step 2 — `Lifecycle/PhysicalDevice.cs`: `SupportsExtension`

Place immediately after `SupportsPresent` (`:74-81`) and before the
`CreateDevice` doc block. Two overloads; the `Utf8Name` one delegates.

```csharp
/// <summary>
/// True when this GPU advertises the named <b>device</b> extension. The
/// after-the-picker counterpart to
/// <see cref="PhysicalDeviceInfo.SupportsExtension"/>, which is only
/// reachable inside <see cref="Instance.PickPhysicalDevice"/> because
/// <see cref="PhysicalDeviceInfo"/> is a <c>ref struct</c>. Setup-time:
/// issues <c>vkEnumerateDeviceExtensionProperties</c> on every call and
/// caches nothing, the same policy as <see cref="GetMemoryLimits"/>.
/// Rents its scratch from <see cref="ArrayPool{T}.Shared"/> and returns it.
/// </summary>
public bool SupportsExtension(ReadOnlySpan<byte> utf8ExtensionName)

/// <inheritdoc cref="SupportsExtension(ReadOnlySpan{byte})"/>
public bool SupportsExtension(Utf8Name extension)
    => !extension.IsNull
       && SupportsExtension(
           MemoryMarshal.CreateReadOnlySpanFromNullTerminated((byte*)extension.Ptr));
```

Body of the span overload — copy the shape of
`Instance.IsExtensionSupported(ReadOnlySpan<byte>)`
(`Lifecycle/Instance.cs:530-563`) with two differences: the enumerate call is
the device one, and there is no `DllNotFoundException` catch (a
`PhysicalDevice` only exists if the loader loaded):

1. `if (utf8ExtensionName.IsEmpty) return false;`
2. `uint count = 0; Vk.vkEnumerateDeviceExtensionProperties(Handle, null, &count, null).ThrowIfErrored();`
   — the `pLayerName: null` form used at `Lifecycle/Instance.cs:319`.
3. `if (count == 0) return false;`
4. Rent `ArrayPool<VkExtensionProperties>.Shared.Rent((int)count)`, `try` /
   `finally { pool.Return(buf); }`.
5. `fixed (VkExtensionProperties* p = buf)` → second enumerate with
   `.ThrowIfErrored()`.
6. Linear scan calling `PhysicalDeviceInfo.NameEquals((sbyte*)&p[i].extensionName.e0, utf8ExtensionName)`
   — the same field access `PhysicalDeviceInfo.SupportsExtension` uses
   (`Lifecycle/PhysicalDeviceInfo.cs:76`).
7. `return false;`

`using System.Buffers;` must be added to the file (`PhysicalDevice.cs:1-3`
currently has `System.Runtime.CompilerServices`,
`System.Runtime.InteropServices`, `Ahjo.Vulkan.Native`).
`MemoryMarshal` is already available via `:2`.

## Step 3 — `Lifecycle/PhysicalDevice.cs`: a private `ReadApiVersion`

The version-gated overload needs the device's `apiVersion` **before** it
decides whether to chain the node. Add a private helper beside
`ValidateQueues` (`:345`):

```csharp
/// <summary>
/// Packed <c>VkPhysicalDeviceProperties.apiVersion</c>, read into a stack
/// struct. Deliberately the un-chained <c>vkGetPhysicalDeviceProperties</c>:
/// this is the call that decides whether a node is safe to put in a
/// <c>VkPhysicalDeviceProperties2</c> chain at all, so it cannot itself use
/// one.
/// </summary>
private uint ReadApiVersion()
{
    VkPhysicalDeviceProperties props;
    Vk.vkGetPhysicalDeviceProperties(Handle, &props);
    return props.apiVersion;
}
```

Same shape as `:133-134` / `:238-239`, which already read properties twice in
one method.

Do **not** add a public `PhysicalDevice.ApiVersion` — out of scope;
`PhysicalDeviceInfo.Properties.apiVersion` covers the pick-time case
(`Lifecycle/PhysicalDeviceInfo.cs:20`).

## Step 4 — `Lifecycle/PhysicalDevice.cs`: `TryGetProperties<T>`

Place after `GetMemoryLimits` (`:143`), before `CreateDevice` (`:145`), under
a `// ---- Chained property queries ----` comment.

### 4a. The three public overloads

```csharp
public bool TryGetProperties<T>(ReadOnlySpan<byte> utf8ExtensionName, out T properties)
    where T : unmanaged, IChainable<VkPhysicalDeviceProperties2>
{
    if (!SupportsExtension(utf8ExtensionName)) { properties = default; return false; }
    QueryChained(out properties);
    return true;
}

public bool TryGetProperties<T>(Utf8Name extension, out T properties)
    where T : unmanaged, IChainable<VkPhysicalDeviceProperties2>
{
    if (extension.IsNull) { properties = default; return false; }
    return TryGetProperties(
        MemoryMarshal.CreateReadOnlySpanFromNullTerminated((byte*)extension.Ptr),
        out properties);
}

public bool TryGetProperties<T>(VulkanVersion minimumApiVersion, out T properties)
    where T : unmanaged, IChainable<VkPhysicalDeviceProperties2>
{
    if (ReadApiVersion() < minimumApiVersion.Packed) { properties = default; return false; }
    QueryChained(out properties);
    return true;
}
```

The three overloads are unambiguous: `Utf8Name`
(`Lifecycle/Utf8Name.cs:14-42`) declares no conversions, and
`VulkanVersion`'s only operator is `VulkanVersion → uint`
(`Lifecycle/VulkanVersion.cs:38`), not the reverse.

### 4b. The private worker

```csharp
private void QueryChained<T>(out T properties)
    where T : unmanaged, IChainable<VkPhysicalDeviceProperties2>
{
    Span<byte> scratch = stackalloc byte[
        sizeof(VkPhysicalDeviceProperties2) + Unsafe.SizeOf<T>() + 16];

    var chain = ChainBuilder.For<VkPhysicalDeviceProperties2>(scratch);
    chain.Root();
    ref T node = ref chain.Push<T>();
    Vk.vkGetPhysicalDeviceProperties2(Handle, chain.Head);
    properties = node;
}
```

Notes the implementer must not "improve" away:

- The `+ 16` is two 8-byte absolute-address pads, one per node
  (`Memory/ChainBuilder.cs:138-146`). Both size terms are compile-time
  constants per instantiation; ILC folds them.
- No `scratch.Clear()` is needed — `ChainBuilder.Reserve` zeroes each slot
  (`Memory/ChainBuilder.cs:156`). `[SkipLocalsInit]` on the method is
  optional and matches the documented usage at `Memory/ChainBuilder.cs:37`;
  apply it or not, but do not add a redundant clear.
- Do **not** write `properties.pNext = null` afterwards. The node is the chain
  tail and `WriteHeader` already nulled it (`Memory/ChainBuilder.cs:163-169`);
  step 7 asserts this instead.
- `sType` comes from `T.SType` inside `Push<T>` (`Memory/ChainBuilder.cs:97`).
  The caller never supplies one and structurally cannot supply a wrong one.

### 4c. XML docs

One shared `<summary>` + `<remarks>` on the span overload, `<inheritdoc>` on
the other two, each with its own one-line `<param>` for the gate. The remarks
must state, in this order:

1. **What the gate means.** Returns `false` and leaves `properties` at
   `default` **without** issuing the chained query. The wrapper refuses to put
   an `sType` a driver may not recognise into a
   `vkGetPhysicalDeviceProperties2` chain, because real ICDs have been
   observed not to skip one cleanly — cite the SwiftShader note at
   `Lifecycle/Instance.cs:272-281`. A `false` result is "not supported", never
   "supported but zero".
2. **Which overload to use.** Extension-only struct ⇒ the name overloads.
   Core-promoted struct (`VkPhysicalDeviceVulkan11Properties` …
   `Vulkan14Properties`) ⇒ the `VulkanVersion` overload; there is no extension
   to name. A struct that is *both* (e.g. `VkPhysicalDeviceDriverProperties`,
   `VK_KHR_driver_properties` promoted to Vulkan 1.2) ⇒ the `VulkanVersion`
   overload, because a device supporting it through core promotion is not
   required to keep advertising the extension.
3. **Type safety.** The `IChainable<VkPhysicalDeviceProperties2>` constraint
   is generated from `vk.xml`'s `structextends`
   (`src/Ahjo.Vulkan.Native/IChainable.cs:3-19`), so chaining a struct Vulkan
   does not permit here is a compile error.
4. **Cost.** Setup-time. One or two native queries per call, no caching,
   same policy as `GetMemoryLimits` and `Device.TimestampPeriod`
   (`Lifecycle/Device.cs:657-666`). The `VulkanVersion` overload allocates
   nothing; the name overloads rent a pooled array through
   `SupportsExtension`.
5. **A worked example** — the mesh one from the spec's Decision C, plus a
   pointer to `TryGetMeshShaderLimits` for that specific struct.

## Step 5 — the mesh projection

### 5a. `Lifecycle/MeshShaderLimits.cs` (new file)

`Lifecycle/`, not `Recording/`: it is a device-capability record produced by
`PhysicalDevice`, and `Recording/` is the zero-per-frame-allocation directory
(`src/Ahjo.Vulkan/CLAUDE.md`) where a setup-time record type would misfile.
Say so in the file's `<remarks>`.

```csharp
namespace Ahjo.Vulkan;

/// <summary>
/// The <c>VkPhysicalDeviceMeshShaderPropertiesEXT</c> fields that bound a
/// <see cref="CommandRecorder.DrawMeshTasks"/> dispatch — the subset a caller
/// issuing mesh draws actually has to obey. Read with
/// <see cref="PhysicalDevice.TryGetMeshShaderLimits"/>.
/// </summary>
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
```

Doc requirements, modelled on `Memory/DeviceMemoryLimits.cs:8-13`:

- A `<remarks>` paragraph saying it is a narrow projection **on purpose**, and
  naming what is left out (`maxMeshOutputVertices`, `maxMeshOutputPrimitives`,
  `maxTaskWorkGroupSize` / `maxMeshWorkGroupSize`, the payload and shared-memory
  sizes, the four `prefers*` hints) with the reason: those are shader-authoring
  constants, not per-draw bounds, and are reachable through
  `PhysicalDevice.TryGetProperties<VkPhysicalDeviceMeshShaderPropertiesEXT>`.
- **The task-vs-mesh rule, on the type and again on the `Task*` members**: the
  `MaxTask*` limits apply when the bound pipeline has a task stage
  (`GraphicsPipelineBuilder.WithTaskStage`), the `MaxMesh*` limits when it does
  not. Cite `VUID-vkCmdDrawMeshTasksEXT-TaskEXT-07322`/`-07323`/`-07324`/`-07325`
  and `-07326`/`-07327`/`-07328`/`-07329`. This is the entire reason the type
  exists — do not shorten it.
- A note that the X/Y/Z members flatten the generated
  `[InlineArray(3)]` buffers `maxTaskWorkGroupCount` / `maxMeshWorkGroupCount`
  (`src/Ahjo.Vulkan.Native/Generated/VkPhysicalDeviceMeshShaderPropertiesEXT.cs:95-117`),
  index 0/1/2 in order.

### 5b. `Lifecycle/PhysicalDevice.cs`: `TryGetMeshShaderLimits`

Immediately after the `TryGetProperties` overloads.

```csharp
public bool TryGetMeshShaderLimits(out MeshShaderLimits limits)
{
    if (!TryGetProperties<VkPhysicalDeviceMeshShaderPropertiesEXT>(
            DeviceExtensionNames.MeshShader, out var p))
    {
        limits = default;
        return false;
    }

    limits = new MeshShaderLimits
    {
        MaxTaskWorkGroupCountX     = p.maxTaskWorkGroupCount[0],
        // …[1], [2], MaxTaskWorkGroupTotalCount = p.maxTaskWorkGroupTotalCount,
        //   MaxTaskWorkGroupInvocations = p.maxTaskWorkGroupInvocations,
        //   and the five mesh-prefixed equivalents…
    };
    return true;
}
```

Go through `DeviceExtensionNames.MeshShader`
(`Internal/DeviceExtensionNames.cs:22`) — do **not** re-quote
`"VK_EXT_mesh_shader"u8` here, per that file's stated purpose.

Doc must state: `false` means the GPU does not advertise `VK_EXT_mesh_shader`.
It does **not** mean the extension was enabled on any `Device` — this is a
physical-device query, and the limits are readable before `CreateDevice`
precisely so a caller can size its dispatch while choosing a GPU. Cross-link
`VulkanExtensions.ExtMeshShader` (`Rendering/VulkanExtensions.cs:61`).

## Step 6 — `Recording/CommandRecorder.cs`: replace the "no accessor" sentence

`:601-604` currently reads:

> *"The wrapper has no accessor for `VkPhysicalDeviceMeshShaderPropertiesEXT`
> today, so the caller must obtain those limits itself or stay well inside the
> guaranteed minimums."*

Replace with a sentence pointing at
`<see cref="PhysicalDevice.TryGetMeshShaderLimits"/>` and naming the two
member groups (`MeshShaderLimits.MaxTaskWorkGroupCountX` … /
`MaxMeshWorkGroupCountX` …) so the reader lands on the right one for their
pipeline. Keep the VUID citations at `:596-601` exactly as they are.

This is the **only** edit outside `Lifecycle/`. Do not touch
`DrawMeshTasksIndirect` / `DrawMeshTasksIndirectCount` docs (`:613-668`) —
their bounds are buffer-shape rules, not workgroup bounds. Do not add any
runtime check to the three forwards (`:606`, `:636`, `:669`); #201's spec
rejected that and this design does not change it.

Do not touch the two mesh-shader design docs under `docs/design/`.

## Step 7 — Tests

New file `tests/Ahjo.Vulkan.Tests/PhysicalDevicePropertiesTests.cs`. All skips
through `Ahjo.Vulkan.Testing.TestGate` (`tests/CLAUDE.md`). Reuse the
device-picking helpers from `tests/Ahjo.Vulkan.Tests/MeshShaderTests.cs`
(`CreateGraphicsDevice` at `:795-805`, `TryCreateMeshDevice` at `:827-882`) —
copy them into the new class rather than making them shared, matching how
`MeshShaderTests` itself copied from `GraphicsPipelineTests`.

### 7a. `[gate:driver]` — the mechanism, unconditionally

`TestGate.RequireDriver()` at the top of each. Obtain a `PhysicalDevice` via
`instance.PickPhysicalDevice(…)` returning `true` for the first candidate, and
capture `info.Properties.apiVersion` out of the picker for the version tests.

1. **`TryGetProperties_CoreVulkan11Properties_Succeeds`** —
   `gpu.TryGetProperties<VkPhysicalDeviceVulkan11Properties>(VulkanVersion.V1_2, out var p)`:
   - returns `true` (the wrapper floor is 1.3, `Lifecycle/PhysicalDevice.cs:149-154`).
     `V1_2` because that struct is defined by Vulkan 1.2 — the "11" is the
     feature set it aggregates, not its version;
   - `p.sType == VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_1_PROPERTIES`
     — proves the mechanism wrote `sType` from `T.SType`;
   - `p.pNext == null` — proves no pointer into dead stack escapes;
   - `p.subgroupSize != 0` **and** `(p.subgroupSize & (p.subgroupSize - 1)) == 0`
     (spec requires a power of two) **and** `p.maxMemoryAllocationSize != 0` —
     proves the *driver* filled the node rather than the struct simply coming
     back zeroed.
2. **`TryGetProperties_ExtensionGate_MatchesSupportsExtension`** —
   `bool ok = gpu.TryGetProperties<VkPhysicalDeviceMeshShaderPropertiesEXT>(VulkanExtensions.ExtMeshShader, out var mesh);`
   - `Assert.Equal(gpu.SupportsExtension(DeviceExtensionNames.MeshShader), ok);`
   - `if (!ok) Assert.Equal(default, mesh);`
   Green on a mesh host and on a non-mesh host; the assertion is the
   relationship, not the capability. **Do not gate this on `[gate:feature]`.**
3. **`TryGetProperties_VersionGate_MatchesDeviceApiVersion`** —
   `Assert.Equal(apiVersion >= VulkanVersion.V1_4.Packed,
   gpu.TryGetProperties<VkPhysicalDeviceVulkan14Properties>(VulkanVersion.V1_4, out _));`
   This is the `Lifecycle/Instance.cs:272-281` regression in assertion form:
   a 1.3 device must never see sType-55-class nodes.
4. **`TryGetProperties_NullUtf8Name_ReturnsFalse`** —
   `gpu.TryGetProperties<VkPhysicalDeviceMeshShaderPropertiesEXT>(default(Utf8Name), out var p)`
   is `false` and `p == default`.
5. **`SupportsExtension_AgreesWithPickerView`** — capture
   `info.SupportsExtension(DeviceExtensionNames.MeshShader)` inside the picker
   into a local, then assert `gpu.SupportsExtension(DeviceExtensionNames.MeshShader)`
   equals it. Plus `Assert.False(gpu.SupportsExtension("VK_EXT_this_does_not_exist"u8))`
   and `Assert.False(gpu.SupportsExtension(default(Utf8Name)))`.
6. **`TryGetMeshShaderLimits_WithoutExtension_ReturnsFalse`** — skip via
   `TestGate.RequireDeviceFeature(!gpu.SupportsExtension(DeviceExtensionNames.MeshShader), …)`
   so it only asserts on a host where the negative is real; assert `false` and
   `limits == default`.

### 7b. `[gate:feature]` — the mesh projection

Gate with `TryCreateMeshDevice`-style picking, then
`TestGate.RequireDeviceFeature(gpu is not null, "Device does not expose VK_EXT_mesh_shader.")`.
No `Device` is actually required — these are physical-device queries — so pick
the GPU with `PhysicalDeviceInfo.SupportsExtension` and skip the
`CreateDevice` call entirely.

7. **`TryGetMeshShaderLimits_MatchesRawProperties`** — read both
   `gpu.TryGetMeshShaderLimits(out var limits)` and
   `gpu.TryGetProperties<VkPhysicalDeviceMeshShaderPropertiesEXT>(VulkanExtensions.ExtMeshShader, out var raw)`,
   then assert all ten field equalities, including
   `limits.MaxMeshWorkGroupCountX == raw.maxMeshWorkGroupCount[0]` and the
   `[1]`/`[2]` pairs. This tests the wrapper's mapping, **not** the driver's
   numbers — no hard-coded spec minimum.
8. **`TryGetMeshShaderLimits_AllLimitsNonZero`** — every one of the ten fields
   `!= 0`. A conformant `VK_EXT_mesh_shader` implementation cannot report zero
   for any of them; this is the one place a driver value is asserted, and it
   is a floor of 1, not a spec constant.

Run: `dotnet test tests/Ahjo.Vulkan.Tests --filter "PhysicalDevicePropertiesTests"`.
Quote the skip classification in the PR: on a hosted runner 7b is expected to
report `[gate:feature]`, and that is the correct outcome, not something to fix.

## Step 8 — Docs

- **`docs/aot-notes.md:13`** — extend the "Static abstract dispatch via
  `IVulkanHandle<T>`" bullet (or add a sibling bullet) to name the second
  instance of the same pattern: `IChainable<TRoot>.SType` /
  `IChainRoot.RootSType`, consumed by `ChainBuilder.Push<T>` and now by
  `PhysicalDevice.TryGetProperties<T>`. Same conclusion (ILC devirtualizes;
  the `unmanaged` constraint forbids reference type arguments, so there is no
  shared-canonical path), same "no reflection, no runtime type lookup" wording.
  Keep it to two sentences.
- **`docs/benchmarks.md`** — **no change.** `Lifecycle/` is not on the
  hot-path list in `src/Ahjo.Vulkan/CLAUDE.md`, and neither
  `GetMemoryLimits` nor `Device.TimestampPeriod` has a row. Do not add one, and
  do not add a benchmark class.
- **`docs/ci-coverage.md`** — no change; no new gate *class*, only new uses of
  `[gate:driver]` and `[gate:feature]`.
- **`README.md`** — no change; it does not enumerate `PhysicalDevice` members.
- **`docs/design/specs/2026-08-22-issue-201-mesh-shader-design.md` and its
  plan** — **do not edit.** The deferral they record is resolved by this pair;
  the cross-link lives in this spec's header, not in theirs.

## Verification

```bash
dotnet build Ahjo.Vulkan.slnx
dotnet test tests/Ahjo.Vulkan.Tests
dotnet publish samples/AotSmoke/AotSmoke.csproj -c Release -r win-x64 -p:IlcUseEnvironmentalTools=true
```

The AOT publish is the one that matters here: it is the proof that a second
static-abstract-constrained generic is still ILC-clean. It needs an MSVC
environment (vcvars or a VS dev shell) per the root `CLAUDE.md`.

No benchmark run is required (see step 8). `vulkan-validation-reviewer` is
worth running because the diff adds a `vkGetPhysicalDeviceProperties2` chain —
the reviewer should confirm the three `VkPhysicalDeviceProperties2` VUIDs
(`-sType-sType`, `-pNext-pNext`, `-sType-unique`) are structurally satisfied.
`bench-coverage-checker` is **not** required: no `Recording/`, `Sync/`,
`Pools/` or `Memory/` file is touched.

## Open items

- **OPEN (step 4b):** `sizeof(VkPhysicalDeviceProperties2) + Unsafe.SizeOf<T>() + 16`
  has not been executed. If `ChainBuilder` throws
  `"Chain buffer too small for next node."` (`Memory/ChainBuilder.cs:172-173`)
  from `QueryChained`, raise the constant pad term and report the value that
  worked — do not switch to a heap buffer or a fixed 2048-byte budget without
  saying so.
- **OPEN (step 5a):** the `MeshShaderLimits` field set is exactly the
  `vkCmdDrawMeshTasksEXT` VUID set — ten fields. If, while writing the docs,
  the implementer finds a `DrawMeshTasks*` VUID naming a field not in the list,
  stop and report rather than adding the field silently.
- **OPEN (step 7b):** whether the CI Windows runner exposes
  `VK_EXT_mesh_shader` is unknown (unchanged from #201). Write the tests to
  skip cleanly; a run reporting every 7b test as `[gate:feature]` is the
  expected outcome and belongs in the PR body, not in a fix.
- **Deliberately excluded, do not add:** an ungated `GetProperties<T>`; a
  properties-query method on `PhysicalDeviceInfo`; a public
  `PhysicalDevice.ApiVersion`; caching of the extension list or of any
  properties struct on `PhysicalDevice`; an `AccelerationStructureLimits` type
  (that is #202's call, and the spec's Decision-C rule says it should not need
  one); any runtime bounds check inside `CommandRecorder.DrawMeshTasks*`; any
  promotion of the duplicated UTF-8 name-comparison helpers beyond the single
  `internal` flip in step 1; any `tools/*.rsp` or `Generated/` change. Each is
  rejected with a reason in the spec's "Why not the alternatives" — if one
  turns out to be necessary, stop and report rather than adding it.
