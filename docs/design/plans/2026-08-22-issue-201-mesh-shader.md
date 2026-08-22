Paired with [../specs/2026-08-22-issue-201-mesh-shader-design.md](../specs/2026-08-22-issue-201-mesh-shader-design.md) — read it first; this plan only says *how*.

# Implementation plan — issue #201: mesh-shader raster surface

Managed-surface work only. No `tools/*.rsp` change, no regen, nothing under
`src/*/Generated/` moves. Every native symbol already exists
(`src/Ahjo.Vulkan.Native/Generated/Vk.cs:3205, 3208, 3211`).

Steps 1–3 are the loading mechanism (the part #202 will extend). Steps 4–5
are the recorder. Steps 6–8 are the builder. Steps 9–12 are fixtures, tests,
benchmarks and docs.

---

## Step 1 — `Internal/DeviceExtensionNames.cs` (new file)

Mirror `Internal/InstanceExtensionNames.cs:8-15` exactly — same namespace,
same `internal static class` shape, same "centralized so a typo can only be
made in one place" doc.

```csharp
namespace Ahjo.Vulkan;

internal static class DeviceExtensionNames
{
    public static ReadOnlySpan<byte> MeshShader => "VK_EXT_mesh_shader"u8;

    public static ReadOnlySpan<byte> CmdDrawMeshTasks              => "vkCmdDrawMeshTasksEXT"u8;
    public static ReadOnlySpan<byte> CmdDrawMeshTasksIndirect      => "vkCmdDrawMeshTasksIndirectEXT"u8;
    public static ReadOnlySpan<byte> CmdDrawMeshTasksIndirectCount => "vkCmdDrawMeshTasksIndirectCountEXT"u8;
}
```

Class doc must state that this is for **device** extensions and that
`InstanceExtensionNames` is the instance-level counterpart (the two are not
interchangeable — see step 3's `VK_EXT_debug_utils` note).

## Step 2 — `Rendering/VulkanExtensions.cs`

Add, after `KhrSwapchain` (`:49-51`):

```csharp
/// <summary>VK_EXT_mesh_shader — device-level. Enables
/// <see cref="GraphicsPipelineBuilder.WithMeshStages"/> /
/// <see cref="GraphicsPipelineBuilder.WithTaskStage"/> and the
/// <see cref="CommandRecorder.DrawMeshTasks"/> family. Pair it with the
/// <c>meshShader</c> (and, for a task stage, <c>taskShader</c>) feature via
/// <see cref="DeviceDescription.ConfigureFeatures"/> pushing
/// <c>VkPhysicalDeviceMeshShaderFeaturesEXT</c> — the extension alone is not
/// enough.</summary>
public static Utf8Name ExtMeshShader => Utf8Name.FromLiteral(DeviceExtensionNames.MeshShader);
```

Do **not** re-quote the literal here; go through `DeviceExtensionNames` (the
`InstanceFunctionTable.cs:33` pattern). Leave the existing
`KhrSwapchain` duplication with `KhronosExtensionNames.cs:19` alone — out of
scope.

## Step 3 — `Internal/DeviceFunctionTable.cs`

### 3a. Charter doc

Rewrite the second `<item>` of the class doc (`:22-27`). It currently reads
"Extension entry points — `VK_EXT_debug_utils`". Replace with two bullets
that draw the instance/device line explicitly:

- **Instance-extension entry points reached through `vkGetDeviceProcAddr`** —
  the four `VK_EXT_debug_utils` pointers. `VK_EXT_debug_utils` is enabled on
  the *instance*, never appears in `DeviceDescription.Extensions`, and so is
  resolved unconditionally; absent ⇒ null ⇒ the helper degrades to a no-op.
- **Device-extension entry points** — resolved **only** when the extension
  appears in the enabled list passed to `vkCreateDevice`. Two failure modes,
  both loud: not enabled ⇒ null pointer ⇒ the calling wrapper method throws
  a message naming the extension; enabled but unresolvable ⇒ throw here, at
  `Device` construction. This group is **not** limited to `vkCmd*`: the
  loader does not export extension symbols through `vulkan-1.dll`
  (`Internal/InstanceFunctionTable.cs:6-10`), so create/destroy/query
  entry points of a device extension belong here too, unlike core cold-path
  calls which stay on the static `[DllImport]`s. Name issue #202 as the next
  consumer of this group.

### 3b. Three fields

New `// ---- Mesh shading (VK_EXT_mesh_shader) ----` section placed after the
`// ---- Draw / dispatch ----` block (after `:103`):

```csharp
/// <summary><c>vkCmdDrawMeshTasksEXT</c>. Null when VK_EXT_mesh_shader
/// was not enabled on this device.</summary>
public readonly delegate* unmanaged[Stdcall]<
    VkCommandBuffer_T*, uint, uint, uint, void> CmdDrawMeshTasks;

/// <summary><c>vkCmdDrawMeshTasksIndirectEXT</c>. Null when
/// VK_EXT_mesh_shader was not enabled on this device.</summary>
public readonly delegate* unmanaged[Stdcall]<
    VkCommandBuffer_T*, VkBuffer_T*, ulong, uint, uint, void> CmdDrawMeshTasksIndirect;

/// <summary><c>vkCmdDrawMeshTasksIndirectCountEXT</c>. Null when
/// VK_EXT_mesh_shader was not enabled on this device.</summary>
public readonly delegate* unmanaged[Stdcall]<
    VkCommandBuffer_T*, VkBuffer_T*, ulong, VkBuffer_T*, ulong, uint, uint, void> CmdDrawMeshTasksIndirectCount;
```

### 3c. Constructor signature + gated resolve block

```csharp
public DeviceFunctionTable(VkDevice_T* device, ReadOnlySpan<Utf8Name> enabledExtensions)
```

At the end of the constructor, after the debug-utils block (`:355`):

```csharp
// Device-extension entry points. Gated on the list the wrapper itself
// passed to vkCreateDevice — vkCreateDevice has already succeeded, so
// membership in that list *is* "enabled", and Vulkan offers no query to
// ask the device after the fact.
if (IsExtensionEnabled(enabledExtensions, DeviceExtensionNames.MeshShader))
{
    CmdDrawMeshTasks =
        (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, uint, uint, uint, void>)
        ResolveExtensionRequired(
            Utf8Name.FromLiteral(DeviceExtensionNames.CmdDrawMeshTasks),
            DeviceExtensionNames.MeshShader);
    // …the other two, same shape…
}
```

Fields are `readonly`, so all three must be definitely assigned: either
initialise them to `null` before the `if`, or use an `else` that assigns
null — pick whichever the compiler accepts without a warning
(`TreatWarningsAsErrors`).

### 3d. Two private helpers

Add beside `ResolveWithFallback` (`:391-395`):

```csharp
/// <summary>
/// True when <paramref name="utf8Name"/> is in the device-extension list
/// the caller passed to <c>vkCreateDevice</c>. Setup-time, allocation-free;
/// the span-over-NUL-terminated-pointer idiom is the one
/// <c>Instance.IsExtensionSupported(Utf8Name)</c> uses
/// (<c>Lifecycle/Instance.cs:524-527</c>).
/// </summary>
private static bool IsExtensionEnabled(ReadOnlySpan<Utf8Name> enabled, ReadOnlySpan<byte> utf8Name)
{
    for (int i = 0; i < enabled.Length; i++)
    {
        if (enabled[i].IsNull) continue;
        if (MemoryMarshal.CreateReadOnlySpanFromNullTerminated((byte*)enabled[i].Ptr)
                         .SequenceEqual(utf8Name))
            return true;
    }
    return false;
}
```

```csharp
/// <summary>
/// Resolves an entry point belonging to a device extension the caller
/// enabled. A null result means the driver advertised the extension at
/// vkCreateDevice but does not expose the command — a broken loader/driver
/// configuration, reported here rather than as an access violation on a
/// later frame.
/// </summary>
private delegate* unmanaged[Stdcall]<void> ResolveExtensionRequired(
    Utf8Name entryPoint, ReadOnlySpan<byte> extension)
{
    var p = Resolve(entryPoint);
    if (p == null) ThrowExtensionEntryPointMissing(entryPoint, extension);
    return p;
}
```

Plus a `[DoesNotReturn]` throw helper modelled on `ThrowEntryPointMissing`
(`:377-383`):

> `vkGetDeviceProcAddr returned null for '<entryPoint>', which belongs to device extension '<extension>' — enabled at device creation via DeviceDescription.Extensions. The driver advertises the extension but does not expose the command; this indicates a loader or driver configuration the wrapper does not support.`

Decode `entryPoint` with `Marshal.PtrToStringUTF8((nint)entryPoint.Ptr)` and
`extension` with `System.Text.Encoding.UTF8.GetString(extension)` — cold path,
allocation is fine (`ThrowEntryPointMissing:381` does the same).

`MemoryMarshal` needs `using System.Runtime.InteropServices;` at the top of
the file (currently only `using Ahjo.Vulkan.Native;`), or fully qualify as
`:381` does. Either is acceptable; be consistent within the file.

### 3e. Thread the list through

- `src/Ahjo.Vulkan/Lifecycle/Device.cs:52` — ctor becomes
  `internal Device(VkDevice_T* handle, PhysicalDevice physicalDevice, Queue[] queues, ReadOnlySpan<Utf8Name> enabledExtensions)`;
  `:55` becomes `Functions = new DeviceFunctionTable(handle, enabledExtensions);`.
  **Do not store the span** — a `ReadOnlySpan` field in a `class` will not
  compile, which is the enforcement.
- `src/Ahjo.Vulkan/Lifecycle/PhysicalDevice.cs:290` — the sole call site:
  `var device = new Device(raw, physicalDevice: this, queues, desc.Extensions);`.
  It is inside the `try` whose `catch` calls `vkDestroyDevice` (`:287-308`),
  so a throw from `ResolveExtensionRequired` already destroys the device
  correctly — no change to that block.

Both constructors are `internal`, so no public API breaks.

## Step 4 — `Recording/CommandRecorder.cs`: three forwards

Append to the `// ---- Draw / Dispatch ----` block, after
`DrawIndexedIndirectCount` (`:563-572`) and before `Dispatch` (`:574`).

```csharp
public void DrawMeshTasks(uint groupCountX, uint groupCountY = 1, uint groupCountZ = 1)
{
    var fn = Fns.CmdDrawMeshTasks;
    if (fn == null) ThrowMeshShaderUnsupported();
    fn(Handle, groupCountX, groupCountY, groupCountZ);
}

public void DrawMeshTasksIndirect(in Buffer buffer, ulong offset, uint drawCount, uint stride)
{
    var fn = Fns.CmdDrawMeshTasksIndirect;
    if (fn == null) ThrowMeshShaderUnsupported();
    fn(Handle, buffer.Handle, offset, drawCount, stride);
}

public void DrawMeshTasksIndirectCount(
    in Buffer buffer,
    ulong     offset,
    in Buffer countBuffer,
    ulong     countBufferOffset,
    uint      maxDrawCount,
    uint      stride)
{
    var fn = Fns.CmdDrawMeshTasksIndirectCount;
    if (fn == null) ThrowMeshShaderUnsupported();
    fn(Handle, buffer.Handle, offset, countBuffer.Handle, countBufferOffset, maxDrawCount, stride);
}
```

The null test is **unconditional** — not behind `AhjoValidation.IsEnabled`
(spec, Decision B). Copy the exact shape of `PushDescriptors`
(`:402-413`).

XML docs, one per method, each stating the VUID-derived rules the wrapper
does not enforce (numbers from
`native/downloaded/Vulkan-Headers-vulkan-sdk-1.4.341.0/registry/validusage.json`):

- `DrawMeshTasks` — `vkCmdDrawMeshTasksEXT`; group counts are **workgroups**,
  not vertices (hence the `Dispatch`-shaped defaults). Bounds are
  `VkPhysicalDeviceMeshShaderPropertiesEXT::maxTaskWorkGroupCount[i]` /
  `maxTaskWorkGroupTotalCount` when the bound pipeline has a task stage
  (`-TaskEXT-07322/23/24/25`) and `maxMeshWorkGroupCount[i]` /
  `maxMeshWorkGroupTotalCount` when it does not (`-07326/27/28/29`). State
  that the wrapper has no accessor for that properties struct today.
- `DrawMeshTasksIndirect` — reads `drawCount`
  `VkDrawMeshTasksIndirectCommandEXT` structs (three `uint`s, 12 bytes;
  `Generated/VkDrawMeshTasksIndirectCommandEXT.cs`). Buffer needs
  `BufferUsage.IndirectBuffer` (`-buffer-02709`); `offset` multiple of 4
  (`-offset-02710`); `drawCount > 1` needs the `multiDrawIndirect` feature
  (`-drawCount-02718`) and a `stride` that is a multiple of 4 and ≥ 12
  (`-drawCount-07088`).
- `DrawMeshTasksIndirectCount` — effective count is
  `min(maxDrawCount, *countBuffer)`; both buffers need
  `BufferUsage.IndirectBuffer`; `countBufferOffset` multiple of 4
  (`-countBufferOffset-02716`); **requires the `drawIndirectCount` feature**
  (`-None-04445`) — reuse the wording already on `DrawIndirectCount`
  (`:509-524`), which points at
  `VkPhysicalDeviceVulkan12Features.drawIndirectCount` via
  `DeviceDescription.ConfigureFeatures`.

Every doc also notes: the bound pipeline must be a mesh pipeline built with
`GraphicsPipelineBuilder.WithMeshStages`, and the command must be recorded
inside a `BeginRendering`/`EndRendering` scope.

## Step 5 — `Recording/CommandRecorder.cs`: the throw helper

Beside `ThrowPushDescriptorUnsupported` (`:463-467`), same
`[System.Diagnostics.CodeAnalysis.DoesNotReturn]` + `private static`:

```csharp
[System.Diagnostics.CodeAnalysis.DoesNotReturn]
private static void ThrowMeshShaderUnsupported() =>
    throw new InvalidOperationException(
        "Mesh-shader draw commands are not available on this device. Enable VK_EXT_mesh_shader via " +
        "DeviceDescription.Extensions (VulkanExtensions.ExtMeshShader) and turn on the meshShader " +
        "(and, for a task stage, taskShader) feature by pushing VkPhysicalDeviceMeshShaderFeaturesEXT " +
        "from DeviceDescription.ConfigureFeatures, then re-create the Device.");
```

## Step 6 — `Pipelines/GraphicsPipelineBuilder.cs`: new state

All additions mirror the existing per-stage groups; keep the same field
ordering and alignment style.

1. Module handles, after `_tessEval` (`:46`):
   `private VkShaderModule_T* _task;` and `private VkShaderModule_T* _mesh;`
2. Entry-point buffers, after `_tessEvalEntry` (`:51`):
   `private EntryPointBuffer _taskEntry;` and `private EntryPointBuffer _meshEntry;`
3. In the ctor (`:122-131`), two more `InitMain(ref _taskEntry);` /
   `InitMain(ref _meshEntry);`.
4. Input-assembly section (`:57-58`): add `private bool _topologySet;` with a
   comment saying it exists only so the mesh path can reject an explicit
   `WithTopology` — `_topology` defaults to `TRIANGLE_LIST` (`:125`) and
   cannot otherwise be told apart from "never called".
5. Specialization slots, after `_tessEvalSpecEntries` (`:84`): the two
   triplets `_taskSpecDataPtr/_taskSpecDataSize/_taskSpecEntries` and
   `_meshSpecDataPtr/_meshSpecDataSize/_meshSpecEntries`.
6. `MaxStages` (`:37`) **stays 5** — update its neighbouring comment
   (`:489-495`) from "not wired through the builder yet" to a statement of
   the fact: classic max is 5 (vert+frag+geom+tessC+tessE), mesh max is 3
   (task+mesh+frag), and the two paths are mutually exclusive.

## Step 7 — `Pipelines/GraphicsPipelineBuilder.cs`: new `With*` methods

Place `WithMeshStages` / `WithTaskStage` immediately after
`WithTessellationStages` (`:175-180`); the entry-point and specialization
setters go at the end of their respective existing runs (`:186`, `:211`).

```csharp
/// <summary>
/// Selects the mesh-shading path: a mesh stage plus fragment, replacing the
/// vertex / tessellation / geometry front end. Mutually exclusive with
/// <see cref="WithStages"/>, <see cref="WithGeometryStage"/> and
/// <see cref="WithTessellationStages"/> — Vulkan requires every geometric
/// stage in a pipeline to come from one family or the other
/// (VUID-VkGraphicsPipelineCreateInfo-pStages-02095). Requires
/// VK_EXT_mesh_shader and the meshShader feature.
/// </summary>
public GraphicsPipelineBuilder WithMeshStages(in ShaderModule mesh, in ShaderModule fragment)
{
    _mesh      = mesh.Handle;
    _frag      = fragment.Handle;
    _stagesSet = true;
    return this;
}

/// <summary>
/// Adds a task (amplification) stage ahead of the mesh stage. Requires
/// <see cref="WithMeshStages"/> — a task-only pipeline is invalid
/// (VUID-VkGraphicsPipelineCreateInfo-stage-02096) — plus the taskShader
/// feature.
/// </summary>
public GraphicsPipelineBuilder WithTaskStage(in ShaderModule task)
{
    _task = task.Handle;
    return this;
}

public GraphicsPipelineBuilder WithMeshEntryPoint(ReadOnlySpan<byte> name) { CopyName(name, ref _meshEntry); return this; }
public GraphicsPipelineBuilder WithTaskEntryPoint(ReadOnlySpan<byte> name) { CopyName(name, ref _taskEntry); return this; }

public GraphicsPipelineBuilder WithMeshSpecialization<T>(SpecializationInfo<T> spec) where T : unmanaged
{ _meshSpecDataPtr = spec.DataPtr; _meshSpecDataSize = spec.DataSize; _meshSpecEntries = spec.Entries; return this; }

public GraphicsPipelineBuilder WithTaskSpecialization<T>(SpecializationInfo<T> spec) where T : unmanaged
{ _taskSpecDataPtr = spec.DataPtr; _taskSpecDataSize = spec.DataSize; _taskSpecEntries = spec.Entries; return this; }
```

Also set `_topologySet = true;` inside `WithTopology` (`:229-233`).

## Step 8 — `Pipelines/GraphicsPipelineBuilder.cs`: `Build()`

### 8a. Guards

Insert after the existing tessellation guards (`:394`), before the
color-blend count check (`:406`). Compute `bool meshPath = _mesh != null;`
first. All six throw `InvalidOperationException`; messages must name the
`With*` method the caller should drop and, where a VUID exists, cite it.

1. `_task != null && _mesh == null` →
   *"WithTaskStage requires WithMeshStages — a task shader amplifies a mesh
   shader and a task-only pipeline has no pre-rasterization stage
   (VUID-VkGraphicsPipelineCreateInfo-stage-02096)."*
2. `meshPath && _vert != null` →
   *"WithStages (vertex) and WithMeshStages are mutually exclusive; a
   pipeline's geometric stages must all come from the primitive-shading
   family or all from the mesh-shading family
   (VUID-VkGraphicsPipelineCreateInfo-pStages-02095). Pick one."*
3. `meshPath && (_geom != null || _tessControl != null || _tessEval != null)` →
   same VUID, message naming `WithGeometryStage` /
   `WithTessellationStages`.
4. `meshPath && (!_vBindings.IsEmpty || !_vAttrs.IsEmpty)` →
   *"WithVertexInput has no effect on a mesh pipeline — a mesh shader has no
   vertex-input stage and reads its data through descriptors or buffer
   device addresses. Drop WithVertexInput."*
5. `meshPath && (_topologySet || _patchControlPoints != 0)` →
   *"WithTopology / WithTessellation have no effect on a mesh pipeline — the
   mesh shader emits primitives directly. Drop them."* (Split into two
   guards with two messages if the implementer prefers; the wording must
   name whichever was actually set.)
6. `meshPath` and the effective dynamic-state list contains any forbidden
   state → name the offending state and the VUID. Compute the effective list
   the same way `:455` does (`_dynamicStates.IsEmpty ? default : _dynamicStates`
   — the default viewport+scissor pair can never trip this, so scanning
   `_dynamicStates` alone is sufficient and simpler). Forbidden set:

   | State | VUID |
   |---|---|
   | `VK_DYNAMIC_STATE_PRIMITIVE_TOPOLOGY` | `-pDynamicStates-07065` |
   | `VK_DYNAMIC_STATE_VERTEX_INPUT_BINDING_STRIDE` | `-pDynamicStates-07065` |
   | `VK_DYNAMIC_STATE_PRIMITIVE_RESTART_ENABLE` | `-pDynamicStates-07066` |
   | `VK_DYNAMIC_STATE_PATCH_CONTROL_POINTS_EXT` | `-pDynamicStates-07066` |
   | `VK_DYNAMIC_STATE_VERTEX_INPUT_EXT` | `-pDynamicStates-07067` |

   Plain `for` loop over the span; no LINQ, no allocation.

Also reword the `!_stagesSet` message (`:382`) to
*"GraphicsPipelineBuilder requires WithStages or WithMeshStages."*

### 8b. `fixed` chain

Add four statements to the chain at `:463-477`, keeping the existing
grouping (entry buffers first, then spec arrays):

```csharp
fixed (byte* pTaskEntry = &_taskEntry[0])
fixed (byte* pMeshEntry = &_meshEntry[0])
fixed (VkSpecializationMapEntry* pTaskSpecEntries = _taskSpecEntries)
fixed (VkSpecializationMapEntry* pMeshSpecEntries = _meshSpecEntries)
```

and two `hasTaskSpec` / `hasMeshSpec` locals beside `:458-462`, and two
`SpecInfo(...)` locals beside `:483-487`.

### 8c. Stage emission

Replace `:497-506` with a branch. Order within `pStages` is not significant;
use task → mesh → fragment for readability.

```csharp
uint stageCount = 0;
if (_mesh != null)
{
    if (_task != null)
        stages[stageCount++] = ShaderStage(VkShaderStageFlagBits.VK_SHADER_STAGE_TASK_BIT_EXT, _task, pTaskEntry, hasTaskSpec ? &taskSpec : null);
    stages[stageCount++] = ShaderStage(VkShaderStageFlagBits.VK_SHADER_STAGE_MESH_BIT_EXT,     _mesh, pMeshEntry, hasMeshSpec ? &meshSpec : null);
    stages[stageCount++] = ShaderStage(VkShaderStageFlagBits.VK_SHADER_STAGE_FRAGMENT_BIT,     _frag, pFragEntry, hasFragSpec ? &fragSpec : null);
}
else
{
    …existing vert / frag / geom / tess block, unchanged…
}
```

### 8d. Create-info

`:605-606` become:

```csharp
pVertexInputState   = _mesh != null ? null : &vertexInput,
pInputAssemblyState = _mesh != null ? null : &inputAssembly,
```

`pTessellationState` (`:607`) is **unchanged** — guard 3 already guarantees
`_tessControl == null` on the mesh path. Add a one-line comment saying so, so
nobody "fixes" it later.

Do **not** touch `pViewportState`, `pRasterizationState`,
`pMultisampleState`, `pDepthStencilState`, `pColorBlendState`,
`pDynamicState` or the `VkPipelineRenderingCreateInfo` chain — a mesh
pipeline needs all of them. Note in a comment that `viewMask` stays 0
(`:590-597`), which is what keeps
`VUID-VkGraphicsPipelineCreateInfo-renderPass-07720` unreachable without the
`multiviewMeshShader` feature.

Class-level `<remarks>` (`:12-34`) gains a paragraph naming the mesh path and
the "mesh and classic stages are mutually exclusive" rule.

## Step 9 — Shader fixtures

New files under `tests/Ahjo.Vulkan.Tests/Shaders/`:

- `mesh_tri.mesh` — `#version 450` + `#extension GL_EXT_mesh_shader : require`,
  `layout(local_size_x = 1) in;`,
  `layout(triangles, max_vertices = 3, max_primitives = 1) out;`, emitting the
  same hard-coded NDC triangle as `triangle.vert` so the two paths are
  visually comparable.
- `mesh_tri.task` — `#version 450` + the same extension,
  `layout(local_size_x = 1) in;`, body `EmitMeshTasksEXT(1, 1, 1);`.

`triangle.frag` is reused unchanged (it declares no inputs).

**csproj (`tests/Ahjo.Vulkan.Tests/Ahjo.Vulkan.Tests.csproj`)** — add a
*second* item group + target rather than editing the existing one at
`:44-58`, so the existing `.spv` outputs stay byte-identical:

```xml
<ItemGroup>
  <_MeshShaderSource Include="Shaders\*.mesh;Shaders\*.task" />
</ItemGroup>
<Target Name="CompileMeshShaders" AfterTargets="Build"
        Inputs="@(_MeshShaderSource)"
        Outputs="@(_MeshShaderSource->'$(OutputPath)Shaders\%(Filename)%(Extension).spv')">
  <!-- same _GlslcExe resolution as CompileShaders -->
  <Exec Command="&quot;$(_GlslcExe)&quot; --target-env=vulkan1.3 …"
        IgnoreExitCode="false" ContinueOnError="WarnAndContinue" />
</Target>
```

`--target-env=vulkan1.3` is required: `GL_EXT_mesh_shader` needs SPIR-V 1.4,
and the existing target passes no `--target-env` (so it defaults to
`vulkan1.0`). `ContinueOnError="WarnAndContinue"` keeps the no-SDK build
green, with `TestGate.RequireSpirv` handling the runtime skip.

Mirror the same item group + target in
`tests/Ahjo.Vulkan.Benchmarks/Ahjo.Vulkan.Benchmarks.csproj` (`:20-38`),
including `..\Ahjo.Vulkan.Tests\Shaders\mesh_tri.mesh` in `_MeshShaderSource`
— the benchmark project already reuses the test project's shader sources
rather than duplicating them.

## Step 10 — Tests

New file `tests/Ahjo.Vulkan.Tests/MeshShaderTests.cs`. Use `TestGate` for
every skip (`tests/CLAUDE.md`); copy `CreateGraphicsDevice` from
`GraphicsPipelineTests.cs` and the try-create-catch helper from
`ExportableResourceTests.cs:176-209`.

### 10a. Builder rejections — `[gate:driver]` only

These need a `Device` to obtain the builder and nothing else:
`Build()`'s guards run before any native call, and
`ShaderModule.FromRaw(unchecked((nint)0xDEADBEEF))` supplies non-null
handles. **No mesh-capable driver required.** One `[Fact]` each, all
asserting `InvalidOperationException`:

1. `WithTaskStage` without `WithMeshStages`.
2. `WithStages` + `WithMeshStages`.
3. `WithMeshStages` + `WithGeometryStage`.
4. `WithMeshStages` + `WithTessellationStages`.
5. `WithMeshStages` + `WithVertexInput` (non-empty bindings/attributes).
6. `WithMeshStages` + `WithTopology(...)`.
7. `WithMeshStages` + `WithTessellation(3)`.
8. `WithMeshStages` + `WithDynamicState([VK_DYNAMIC_STATE_PRIMITIVE_TOPOLOGY])`
   — plus a `[Theory]` over the other four forbidden states.
9. Empty builder `.Build()` message mentions `WithMeshStages` (extend or
   sit beside `GraphicsPipelineTests.Builder_MissingStages_Throws:37-46`).
10. **Negative control:** `WithMeshStages` +
    `WithDynamicState([VIEWPORT, SCISSOR])` does **not** throw a
    dynamic-state error (it will fail later for a missing layout /
    rendering — assert on the message, or supply those and let it reach the
    driver, in which case move the test to 10c).

### 10b. Gating — `[gate:driver]` only

- Create a device **without** `VK_EXT_mesh_shader`; assert
  `device.Functions.CmdDrawMeshTasks == null` (and the other two).
  `Device.Functions` is reachable from the test assembly via
  `InternalsVisibleTo` (`src/Ahjo.Vulkan/Ahjo.Vulkan.csproj:26`); the shape
  is `InstanceFunctionTableTests.cs:8-22`.
- On that same device, each of the three `CommandRecorder.DrawMeshTasks*`
  calls throws `InvalidOperationException` whose message contains
  `VK_EXT_mesh_shader`. Record into a `CommandBufferPool` scope; never
  submit.

### 10c. Mesh-capable — `[gate:feature]`

Gate with
`TestGate.RequireDeviceFeature(device is not null, "Device does not expose VK_EXT_mesh_shader.")`
after the try-create helper, and `TestGate.RequireSpirv` on the two new
`.spv` paths. The device must enable `VulkanExtensions.ExtMeshShader` **and**
push `VkPhysicalDeviceMeshShaderFeaturesEXT { meshShader = 1, taskShader = 1 }`
through `ConfigureFeatures` (chain-push shape:
`DescriptorSetPoolVariableCountBenchmarks.cs:59-71`).

- All three table pointers resolve non-null.
- A mesh-only pipeline (`WithMeshStages` + `WithDynamicRendering` +
  `WithLayout`) builds and disposes.
- A task+mesh pipeline builds and disposes.
- `WithMeshEntryPoint("main"u8)` / `WithMeshSpecialization<T>` round-trip
  into a successful build.
- End-to-end oracle, additionally `TestGate.RequireValidationLayer()`:
  create a color image + view, `BeginRendering` → `BindPipeline` →
  `SetViewport`/`SetScissor` → `DrawMeshTasks(1, 1, 1)` → `EndRendering` →
  submit → fence wait, asserting the validation layer reported no errors
  (the `SplitBarrierTests` capture pattern). This is the test that proves the
  nulled `pVertexInputState`/`pInputAssemblyState` and the stage set are
  actually accepted.

If the oracle test is run, capture it as
`AHJO_VULKAN_TIER=validation dotnet test tests/Ahjo.Vulkan.Tests` and quote
the contract test's `declared=… observed=…` line in the PR
(`tests/CLAUDE.md`).

**OPEN:** whether the mesh-capable tier can run in CI at all is unknown — no
probe exists for `VK_EXT_mesh_shader` on the `windows-latest` runner. Write
the tests to skip cleanly; if CI reports every 10c test as
`[gate:feature]`, that is the expected outcome and must be stated in the PR
body rather than "fixed".

## Step 11 — Benchmarks

New file `tests/Ahjo.Vulkan.Benchmarks/MeshShaderBenchmarks.cs`, a separate
`[MemoryDiagnoser]` class (the
`DescriptorSetPoolVariableCountBenchmarks.cs:18-26` rationale applies
verbatim — copy that `<remarks>` shape).

`[GlobalSetup]` builds the full per-frame shape, because a `vkCmdDraw*` with
no bound pipeline is not something this repo records: instance → device with
`VulkanExtensions.ExtMeshShader` + `VkPhysicalDeviceMeshShaderFeaturesEXT`
→ `mesh_tri.mesh` + `triangle.frag` shader modules → pipeline layout →
mesh pipeline → 64×64 color image + view → `CommandBufferPool` → an indirect
+ count buffer pair (`BufferUsage.IndirectBuffer`, `AutoPreferDevice`) →
one warm call of each benchmark method. Reuse the image/view/buffer setup
verbatim from `CommandRecorderBenchmarks.cs:62-105`.

Two benchmarks, both `OperationsPerInvoke = 1024`, both
`Begin → BeginRendering → BindPipeline → 1024 draws → EndRendering → End →
ResetForFrame`, never submitted:

- `DrawMeshTasks_1024`
- `DrawMeshTasksIndirectCount_1024` (`maxDrawCount: 1`, `stride: 12`)

`[GlobalCleanup]` disposes in reverse order.

Run with `/run-bench --filter "*MeshShader*"` plus a re-run of
`*GraphicsPipelineBuilder*` and `*CommandRecorder*` to prove the builder and
recorder edits moved nothing. Every `Allocated` cell must read `-`.

## Step 12 — Docs

`docs/benchmarks.md`:

- Two rows in the baseline table (`:72-100`) for the new benchmarks. Use
  `n/m` for the Mean if the authoring host has no mesh-capable GPU — the
  convention already used by the `BindDescriptorSets.*` rows — and say so in
  the Notes cell.
- Extend the Caveats "Driver dependency" bullet (`:119-130`):
  `MeshShaderBenchmarks` needs a device that exposes `VK_EXT_mesh_shader`
  and fails at `[GlobalSetup]` without it, which is why it is its own class
  and not two more methods on `CommandRecorderBenchmarks` (whose
  `RenderingPass100Cmds` is the #29 canary and must keep running on any host
  with an ICD).
- Add `*MeshShader*` to the filter example at `:27` if the implementer
  touches that line; optional.

No `README.md` change — it does not enumerate recorder commands. No
`docs/aot-notes.md` change — nothing new is reflection- or
trim-sensitive. No `docs/ci-coverage.md` change — no new gate *class* is
introduced, only new uses of `[gate:driver]` / `[gate:feature]`.

## Verification

```bash
dotnet build Ahjo.Vulkan.slnx
dotnet test tests/Ahjo.Vulkan.Tests
AHJO_VULKAN_TIER=validation dotnet test tests/Ahjo.Vulkan.Tests   # if the oracle test runs
dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter "*MeshShader*|*GraphicsPipelineBuilder*|*CommandRecorder*"
```

Then the two reviewer agents, both mandatory for this diff:
`vulkan-validation-reviewer` (touches `Recording/` + `Pipelines/`) and
`bench-coverage-checker` (new hot-path methods).

## Open items

- **OPEN (step 10c):** CI mesh-shader availability on `windows-latest` is
  unknown. Do not attempt to force it green; report the skip classification.
- **OPEN (step 11):** if the implementer's host has no mesh-capable GPU, the
  benchmark class cannot be captured. Land the class, record `n/m` means,
  and say so in the PR — do not delete the benchmark to get a clean table.
- **OPEN (step 9):** `--target-env=vulkan1.3` has not been executed against
  this repo's glslc. If glslc rejects the mesh source, try `vulkan1.2`
  (the SPIR-V 1.4 floor for `GL_EXT_mesh_shader`) before changing anything
  else, and report the working flag.
- **Deliberately excluded, do not add:** a properties-chain accessor for
  `VkPhysicalDeviceMeshShaderPropertiesEXT`, a public
  `Device.IsExtensionEnabled`, `VK_NV_mesh_shader`, a `HelloMeshShader`
  sample, and any promotion of the duplicated UTF-8 name-comparison helpers.
  Each is rejected with a reason in the spec's "Why not the alternatives";
  if one turns out to be necessary, stop and report rather than adding it.
