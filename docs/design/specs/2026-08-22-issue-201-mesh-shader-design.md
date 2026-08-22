# Mesh-shader raster surface — builder mesh path, `DrawMeshTasks*` commands, gated EXT entry-point loading

**Issue:** [#201](https://github.com/pekkah/Ahjo-Vulkan/issues/201) — *Raster: mesh-shader surface — pipeline builder mesh path, DrawMeshTasks* commands, EXT entry points*
**Must land consistently with:** [#202](https://github.com/pekkah/Ahjo-Vulkan/issues/202) (KHR acceleration structure + ray query — needs the same device-extension entry-point loading for a **larger, non-command** set), [#121](https://github.com/pekkah/Ahjo-Vulkan/issues/121) (per-device dispatch table), [#39](https://github.com/pekkah/Ahjo-Vulkan/issues/39) (push descriptors — the in-repo precedent for an optional command that may be absent), [#118](https://github.com/pekkah/Ahjo-Vulkan/issues/118) (handle ownership — no new handle types here, noted so nobody looks for one)
**Test strategy constrained by:** [#32](https://github.com/pekkah/Ahjo-Vulkan/issues/32) (wrapper suite is Windows-only; software rasterizers are not honest coverage), [#158](https://github.com/pekkah/Ahjo-Vulkan/issues/158) (`AHJO_VULKAN_TIER` / `TestGate` gate classification)
**Consumer:** Ahjo Lane L virtualized geometry (ADR-0028 decisions 6 and 11), acceptance-gated on `pekkah/ahjo#1002`. Not urgent — v1 cluster raster ships through `DrawIndexedIndirectCount`.
**Date:** 2026-08-22

## Problem

Everything about `VK_EXT_mesh_shader` that is *data* already works; everything
that is *API* is missing. The issue's three-part split is accurate, and the
audit below confirms each part.

**Already present, nothing to do:**

- Extension enabling — `DeviceDescription.Extensions` is a
  `ReadOnlySpan<Utf8Name>` (`Lifecycle/DeviceDescription.cs:15`) copied
  verbatim into `VkDeviceCreateInfo.ppEnabledExtensionNames`
  (`Lifecycle/PhysicalDevice.cs:181-183, 271-274`).
- Feature enabling — `DeviceDescription.ConfigureFeatures`
  (`Lifecycle/DeviceDescription.cs:24`) is handed the live `ChainBuilder`
  (`Lifecycle/PhysicalDevice.cs:255`), and
  `VkPhysicalDeviceMeshShaderFeaturesEXT` is chainable
  (`src/Ahjo.Vulkan.Native/Generated/Chains/VkPhysicalDeviceMeshShaderFeaturesEXT.Chain.g.cs`).
- Stage flags — `ShaderStages.Task = 0x40` / `ShaderStages.Mesh = 0x80`
  (`Pipelines/ShaderStages.cs:18-19`), with the `AllGraphics = 0x1F`
  exclusion already documented (`:20-29`).

**Missing, gap 1 — the builder has no mesh path.**
`GraphicsPipelineBuilder` carries exactly five module slots — `_vert`,
`_frag`, `_geom`, `_tessControl`, `_tessEval`
(`Pipelines/GraphicsPipelineBuilder.cs:42-46`) — and its `Build()`
unconditionally writes both `pVertexInputState = &vertexInput` and
`pInputAssemblyState = &inputAssembly` (`:605-606`). The file's own comment
records the gap: *"Mesh + task shaders are not wired through the builder
yet"* (`:489-495`).

**Missing, gap 2 — no recorder commands.** `CommandRecorder`'s
`// ---- Draw / Dispatch ----` block (`Recording/CommandRecorder.cs:487-583`)
has `Draw`, `DrawIndexed`, `DrawIndirect`, `DrawIndirectCount`,
`DrawIndexedIndirect`, `DrawIndexedIndirectCount`, `Dispatch`,
`DispatchIndirect` — and nothing mesh.

**Missing, gap 3 — no entry points.** `Internal/DeviceFunctionTable.cs`
carries 33 pointers, none of them mesh. Worse than "absent": the table has
**no mechanism at all** for a device extension. Its only extension entries
are the four `VK_EXT_debug_utils` pointers (`:170-198, 343-355`), and
`VK_EXT_debug_utils` is an **instance** extension — it never appears in
`DeviceDescription.Extensions`, so those four are resolved unconditionally
with plain `Resolve` and left null when absent. There is no code path in the
wrapper today that says "resolve this because the caller enabled that device
extension." `Device.Functions` is `internal` (`Lifecycle/Device.cs:26`), so a
consumer has no supported escape hatch either.

**Generated bindings are complete** — verified, and no `Generated/` change is
needed:

| Symbol | Location |
|---|---|
| `vkCmdDrawMeshTasksEXT` | `src/Ahjo.Vulkan.Native/Generated/Vk.cs:3205` |
| `vkCmdDrawMeshTasksIndirectEXT` | `Generated/Vk.cs:3208` |
| `vkCmdDrawMeshTasksIndirectCountEXT` | `Generated/Vk.cs:3211` |
| `VkDrawMeshTasksIndirectCommandEXT` (3 × `uint`, 12 bytes) | `Generated/VkDrawMeshTasksIndirectCommandEXT.cs` |
| `VkPhysicalDeviceMeshShaderFeaturesEXT` | `Generated/VkPhysicalDeviceMeshShaderFeaturesEXT.cs` |
| `VkPhysicalDeviceMeshShaderPropertiesEXT` | `Generated/VkPhysicalDeviceMeshShaderPropertiesEXT.cs` |
| `VK_SHADER_STAGE_TASK_BIT_EXT` / `_MESH_BIT_EXT` | `Generated/VkShaderStageFlagBits.cs:19-20` |

**This is wrapper-surface work only: no `tools/*.rsp` change, no regen.**

## Evidence

### The builder's state model, and what a task stage actually costs

The builder is a `ref struct` (`:36`) whose per-stage state is four parallel
groups, one entry per stage:

| Group | Lines | Entries today |
|---|---|---|
| module handle | `:42-46` | 5 |
| inline entry-point buffer (`[InlineArray(32)]`) | `:47-51` | 5, all initialised to `"main\0"` at `:122-131` |
| entry-point setter | `:182-186` | 5 |
| specialization triplet (`ptr`, `size`, `entries[]`) + setter | `:69-84`, `:194-211` | 5 |

`Build()` then `fixed`-pins each buffer and each spec array — fifteen `fixed`
statements at `:463-477` — and emits stages into a
`stackalloc VkPipelineShaderStageCreateInfo[MaxStages]` where
`MaxStages = 5` (`:37`, `:496`).

Adding **mesh alone** costs one entry in each group (+4 `fixed`). Adding
**task as well** costs a second entry in each group (+4 more `fixed`) and
**one** additional validation rule. It changes no structural property of the
builder:

- `MaxStages` stays **5**. The mesh path's maximum is task + mesh + fragment
  = 3; the classic path's is 5; the two are mutually exclusive. The existing
  comment at `:489-495` already worked this out and reached the same number.
- Stage emission becomes one `if (_mesh != null) { … } else { … existing … }`
  around `:498-506`. The classic branch is untouched.
- No new span field, so the aliasing hazard documented at `:20-33` does not
  grow.

There is no state-model complication. The answer to the issue's open question
is therefore **include the task stage** (see Decision A).

### Where a validation-layer error would actually come from

Audited against `native/downloaded/Vulkan-Headers-vulkan-sdk-1.4.341.0/registry/validusage.json`
(the tag pinned at `Directory.Build.props:18`) and `registry/vk.xml`.

**`pVertexInputState` / `pInputAssemblyState` are `optional="true"` and
`noautovalidity="true"`** (`vk.xml:1783-1784`). A programmatic scan of every
`VkGraphicsPipelineCreateInfo` VUID for the strings `must be NULL` /
`must not be NULL` returns **zero results**. The two rules that mention them
are conditional *requirements*, not prohibitions:

| VUID | Rule |
|---|---|
| `VUID-VkGraphicsPipelineCreateInfo-pStages-02097` | *If the pipeline requires vertex input state*… `pVertexInputState` must be a valid pointer |
| `VUID-VkGraphicsPipelineCreateInfo-dynamicPrimitiveTopologyUnrestricted-09031` | *If the pipeline requires vertex input state*… `pInputAssemblyState` must be a valid pointer |
| `VUID-VkGraphicsPipelineCreateInfo-pInputAssemblyState-09032` | *If* `pInputAssemblyState` *is not NULL* it must point at a valid struct |

**Finding, stated plainly because it contradicts the issue's framing:** a
mesh pipeline does not "require vertex input state", so the pointers are not
required — but passing a structurally valid pair is **not** a VU violation
either. The spec's member prose says they are *ignored* when the pipeline
includes a mesh shader stage. So nulling them is **correct and honest, not
mandatory**. The reason to null them anyway is that it is the only way to
make "this pipeline has no vertex input" true in the struct the driver sees,
and it lets the builder reject — rather than silently discard — a caller who
combined `WithVertexInput` / `WithTopology` with a mesh stage.

The rules that **are** hard errors, and which the builder must enforce or
structurally prevent:

| VUID | Rule | How this design satisfies it |
|---|---|---|
| `-stage-02096` | one of `pStages` must be `VERTEX` **or** `MESH` | ⇒ a **task-only** pipeline is invalid. Builder rejects `WithTaskStage` without `WithMeshStages`. |
| `-pStages-02095` | geometric stages must be **all** mesh-family (`TASK`/`MESH`) or **all** primitive-family (`VERTEX`/`TESC`/`TESE`/`GEOM`) | Builder rejects mesh + vertex, mesh + geometry, mesh + tessellation. |
| `-pDynamicStates-07065` | with a mesh shader: no `VK_DYNAMIC_STATE_PRIMITIVE_TOPOLOGY`, no `..._VERTEX_INPUT_BINDING_STRIDE` | Builder scans the effective dynamic-state list. |
| `-pDynamicStates-07066` | with a mesh shader: no `..._PRIMITIVE_RESTART_ENABLE`, no `..._PATCH_CONTROL_POINTS_EXT` | ditto |
| `-pDynamicStates-07067` | with a mesh shader: no `..._VERTEX_INPUT_EXT` | ditto |
| `VkPipelineShaderStageCreateInfo-stage-02091/-02092` | `MESH`/`TASK` stage requires the `meshShader`/`taskShader` **feature** | Documented; the wrapper cannot see the enabled feature chain after create. Driver + validation layer report it. |
| `-renderPass-07720` | mesh shader + non-zero `viewMask` requires `multiviewMeshShader` | Structurally unreachable: the builder never sets `VkPipelineRenderingCreateInfo.viewMask` (`:590-597`), so it is always 0. |
| `-pStages-09631`, `-TaskNV-07063`, `-PrimitiveId-06264`, `-None-02322` | SPIR-V-level rules (no `DrawIndex` builtin with task+mesh, no mixing NV/EXT execution models, `PrimitiveId` pairing, no `Xfb`) | Shader-authoring rules; documented, not enforceable from the builder. |

`pTessellationState` needs **no** change: it is already gated on
`_tessControl != null` (`:607`), and tessellation stages are rejected on the
mesh path by `-02095`, so it is guaranteed null there.

Everything else on `VkGraphicsPipelineCreateInfo` is unchanged for a mesh
pipeline: pre-rasterization state still includes viewport + rasterization,
fragment state still includes depth-stencil, fragment-output state still
includes multisample + color blend, and dynamic rendering
(`VkPipelineRenderingCreateInfo`) is orthogonal.

### Draw-command rules

| VUID | Rule |
|---|---|
| `vkCmdDrawMeshTasksEXT-TaskEXT-07322/23/24/25` | with a task stage: `groupCount{X,Y,Z}` ≤ `VkPhysicalDeviceMeshShaderPropertiesEXT::maxTaskWorkGroupCount[i]`, product ≤ `maxTaskWorkGroupTotalCount` |
| `-TaskEXT-07326/27/28/29` | without a task stage: same bounds against `maxMeshWorkGroupCount` / `maxMeshWorkGroupTotalCount` |
| `vkCmdDrawMeshTasksIndirectEXT-buffer-02709`, `-offset-02710` | indirect buffer needs `INDIRECT_BUFFER` usage; `offset` multiple of 4 |
| `-drawCount-02718` | `drawCount > 1` requires the `multiDrawIndirect` feature |
| `-drawCount-07088/89/90` | `stride` multiple of 4 and ≥ `sizeof(VkDrawMeshTasksIndirectCommandEXT)` (= 12); range must fit the buffer |
| `vkCmdDrawMeshTasksIndirectCountEXT-None-04445` | **requires the `drawIndirectCount` feature** — same rule the existing `DrawIndirectCount` doc already states (`Recording/CommandRecorder.cs:509-524`) |
| `-countBuffer-02715`, `-countBufferOffset-02716`, `-04129`, `-02717` | count buffer needs `INDIRECT_BUFFER`; offset multiple of 4; in bounds; count ≤ `maxDrawIndirectCount` |
| `-stride-07096`, `-maxDrawCount-07097` | stride multiple of 4 and ≥ 12; range must fit the buffer |

None of these is checkable from the recorder without state it does not have
(the bound pipeline's stages, the device's mesh properties, buffer sizes).
The existing `DrawIndirect` / `DrawIndirectCount` carry **zero** runtime
validation and document the requirements instead
(`Recording/CommandRecorder.cs:501-573`). The mesh forwards match that
policy.

**Gap worth recording:** the wrapper has no route to
`VkPhysicalDeviceMeshShaderPropertiesEXT`. `PhysicalDeviceInfo` exposes only
`Properties`, `Features`, `Features11`–`Features14`, `Memory`, the queue
families and the extension list (`Lifecycle/PhysicalDeviceInfo.cs:19-30`),
and `PhysicalDevice.Handle` is `internal` (`Lifecycle/PhysicalDevice.cs:23`).
A caller therefore cannot read `maxMeshWorkGroupCount` through the wrapper to
bound its own dispatch. That is a *properties-chain query* design — #202 will
want the same thing for acceleration-structure limits — and was deliberately
out of scope here (see "Why not the alternatives").

> **Superseded — the deferral was reversed before this branch shipped.** The
> gap is closed on this same branch by
> [`2026-08-22-issue-201-properties-chain-query-design.md`](2026-08-22-issue-201-properties-chain-query-design.md)
> (plan: [`../plans/2026-08-22-issue-201-properties-chain-query.md`](../plans/2026-08-22-issue-201-properties-chain-query.md)),
> which adds `PhysicalDevice.TryGetProperties<T>`, `PhysicalDevice.SupportsExtension`
> and the `MeshShaderLimits` projection read through
> `PhysicalDevice.TryGetMeshShaderLimits`. Shipping a `DrawMeshTasks*` surface
> whose own XML doc told the caller to obey a limit the wrapper gave them no
> way to read was judged not worth deferring past the branch that introduced
> it. The properties-chain design still holds everything this section rejected
> — it *is* the general mechanism, not a mesh-specific accessor — so the
> reasoning below is reversed on timing only, not on shape. The
> `Recording/CommandRecorder.cs` sentence quoted in this spec as "the wrapper
> has no accessor … today" no longer exists; it now points at
> `TryGetMeshShaderLimits`.

### How the wrapper reports "this command is not available today"

There is exactly one precedent, and it is a close match. Push descriptors
became core only in Vulkan 1.4; on the wrapper's 1.3 floor the pointer can
legitimately be null:

- `DeviceFunctionTable` resolves with a fallback and leaves null when both
  names fail (`Internal/DeviceFunctionTable.cs:237-253`).
- Both call sites test the pointer **unconditionally** — not behind
  `AhjoValidation.IsEnabled` — and call a `[DoesNotReturn]` helper
  (`Recording/CommandRecorder.cs:408-409`, `:438-439`).
- The helper throws a plain `InvalidOperationException` whose message names
  the extension and the exact call the caller must make
  (`:463-467`).

That "unconditional" detail is load-bearing: `AhjoValidation.Enabled`
defaults to **false** in Release (`Diagnostics/AhjoValidation.cs:56-61`), so a
guard routed through `AhjoValidation.Fail` would compile away the protection
in exactly the build where dispatching through a null pointer is an access
violation. `AhjoValidation` is for *misuse the driver might not catch*; a
null function pointer is *the wrapper about to crash*. The mesh commands must
use the `ThrowPushDescriptorUnsupported` shape, not the `AhjoValidation.Fail`
shape.

The second failure mode has its own precedent: `ResolveRequired` throws at
`Device` construction when `vkGetDeviceProcAddr` returns null for something
that must exist, with a message naming the entry point
(`Internal/DeviceFunctionTable.cs:361-383`).

### Why the entry points cannot ride the static `[DllImport]`s

`Generated/Vk.cs:3205-3211` does declare `[DllImport]`s for all three mesh
commands, so "just call `Vk.vkCmdDrawMeshTasksEXT`" looks available. It is
not a supported path, and the repo already says so:
`InstanceFunctionTable`'s charter is explicit — *"The loader does not export
extension functions through `vulkan-1.dll`; the only legal way to call them
is via the function pointer the loader hands back at runtime."*
(`Internal/InstanceFunctionTable.cs:6-10`). This is the reason #202's
**non-command** entry points (`vkCreateAccelerationStructureKHR`,
`vkDestroyAccelerationStructureKHR`,
`vkGetAccelerationStructureBuildSizesKHR`,
`vkGetAccelerationStructureDeviceAddressKHR`) must go through the device
table too, even though the table's current charter (`:9-31`) scopes itself to
hot-path `vkCmd*` and says cold-path calls stay on the static imports. That
charter is correct for **core** commands and wrong for **extension** ones;
this design widens it explicitly so #202 does not have to relitigate it.

### What #202 needs from the loading mechanism

From the issue text: an owning `AccelerationStructure` type, a
`CommandRecorder.BuildAccelerationStructures`, and *"the entry points loaded
in `Internal/DeviceFunctionTable` through `vkGetDeviceProcAddr`, resolved
only when the extensions are enabled, failing loudly rather than calling
null."* Its set spans **two** extensions
(`VK_KHR_acceleration_structure` + `VK_KHR_ray_query`, the former also
requiring `VK_KHR_deferred_host_operations`) and includes create/destroy/query
commands, not just `vkCmd*`. The mechanism this spec chooses must therefore
(a) key on more than one extension, (b) admit non-`vkCmd*` signatures, and
(c) be additive — a new `if` block, not a new mechanism.

### Test-gating precedents

- Capability skips go through `TestGate` only, with a `[gate:*]` class; CI
  fails the job on an unclassified skip (`tests/Shared/TestGate.cs:5-18`,
  `tests/CLAUDE.md`).
- The exact "device extension may be absent" pattern already exists:
  `ExportableResourceTests.TryCreateDeviceWith` attempts `CreateDevice` with
  the extension, catches `VulkanException` with
  `VK_ERROR_EXTENSION_NOT_PRESENT`, returns `null`, and the test skips via
  `TestGate.RequireDeviceFeature(device is not null, …)`
  (`tests/Ahjo.Vulkan.Tests/ExportableResourceTests.cs:74-81, 176-209`).
- `PhysicalDeviceInfo.SupportsExtension(ReadOnlySpan<byte>)`
  (`Lifecycle/PhysicalDeviceInfo.cs:68-80`) is the allocation-free
  pick-time capability check a picker uses.
- Builder-validation tests need a `Device` only to obtain the builder and
  never reach `vkCreateGraphicsPipelines`, because `Build()`'s guards run
  first (`Pipelines/GraphicsPipelineBuilder.cs:382-394`);
  `ShaderModule.FromRaw(nint)` (`Pipelines/ShaderModule.cs:29`) supplies a
  non-null handle with no driver involvement. That makes every new
  *rejection* test `[gate:driver]`-only — runnable on any host with an ICD,
  mesh-capable or not.
- Shader compilation is a `glslc` MSBuild target over
  `Shaders\*.vert;*.frag;*.comp` with `ContinueOnError="WarnAndContinue"`
  (`tests/Ahjo.Vulkan.Tests/Ahjo.Vulkan.Tests.csproj:44-58`); the benchmark
  project reuses the test project's sources through the same target
  (`tests/Ahjo.Vulkan.Benchmarks/Ahjo.Vulkan.Benchmarks.csproj:20-38`).
  Neither passes `--target-env`, so both default to `vulkan1.0` — a
  `GL_EXT_mesh_shader` source will **not** compile under that default (it
  needs SPIR-V 1.4, i.e. `--target-env=vulkan1.2` or newer).
  `triangle.frag` declares no inputs
  (`tests/Ahjo.Vulkan.Tests/Shaders/triangle.frag`), so it pairs with a mesh
  shader unchanged.

### Benchmark surface

`Recording/**` is on the zero-per-frame-allocation list
(`src/Ahjo.Vulkan/CLAUDE.md`). Honest accounting of what exists today:
`docs/benchmarks.md`'s table has **no `Draw` or `DrawIndirect` row** — the
recorder's canary is `CommandRecorder.RenderingPass100Cmds`, which uses
`SetViewport` as *"the cheapest representative recording call"*
(`tests/Ahjo.Vulkan.Benchmarks/CommandRecorderBenchmarks.cs:6-14`). So mesh
draw benchmarks would be **new** coverage, not restored coverage.

The precedent for a benchmark class that needs an optional device capability
is `DescriptorSetPoolVariableCountBenchmarks`: *"Deliberately a separate class
… this `Setup` requires an optional device feature … and a host without it
must not take the issue-114 canary down with it"*
(`tests/Ahjo.Vulkan.Benchmarks/DescriptorSetPoolVariableCountBenchmarks.cs:18-26`),
with the failure mode recorded in `docs/benchmarks.md:119-130`.

## Decision

Three changes plus one new mechanism, all wrapper-surface:

### A. The task (amplification) stage **is** in scope

Ship `WithMeshStages` **and** `WithTaskStage`, with entry-point and
specialization setters for both, symmetric with every existing stage.

Justification, from the state-model audit above: the hard part —
suppressing vertex-input/input-assembly and rejecting cross-family stage
mixes — is identical either way; the marginal cost of task is one more entry
in four parallel field groups, four more `fixed` statements, and one
validation line (`-stage-02096`: task requires mesh). `MaxStages` does not
move. Deferring it would leave the builder with a stage the wrapper's own
`ShaderStages.Task` flag already advertises but cannot use, and the follow-up
diff would re-touch every one of the same lines.

Ahjo's current cluster raster does not need task shaders — the issue says so.
This is scoped on cost, not on demand.

### B. Device-extension entry points: gate on the enabled list, fail loudly twice

`DeviceFunctionTable`'s constructor takes the enabled device-extension list
and resolves extension groups only when their extension is in it:

```csharp
internal DeviceFunctionTable(VkDevice_T* device, ReadOnlySpan<Utf8Name> enabledExtensions)
{
    …core resolves, unchanged…

    if (IsEnabled(enabledExtensions, DeviceExtensionNames.MeshShader))
    {
        CmdDrawMeshTasks = (…)ResolveExtensionRequired(
            Utf8Name.FromLiteral(DeviceExtensionNames.CmdDrawMeshTasks),
            DeviceExtensionNames.MeshShader);
        …two more…
    }
}
```

The list reaches the table because `vkCreateDevice` has already succeeded by
the time `Device`'s constructor runs (`Lifecycle/PhysicalDevice.cs:278`), so
membership in `desc.Extensions` **is** "enabled" — there is no
`vkGetEnabledDeviceExtensions` to ask, and this is the only authoritative
source. The span is threaded `PhysicalDevice.CreateDevice` → `Device` ctor →
table ctor and never stored (a `ReadOnlySpan` field in a `class` would not
compile, so the compiler enforces this).

Name comparison uses the established idiom
`MemoryMarshal.CreateReadOnlySpanFromNullTerminated((byte*)name.Ptr).SequenceEqual(target)`
— exactly what `Instance.IsExtensionSupported(Utf8Name)` does
(`Lifecycle/Instance.cs:524-527`). Setup-time, allocation-free, AOT-clean.

Two failure modes, both loud, each matching an existing precedent:

| Situation | Behaviour | Precedent |
|---|---|---|
| Extension **not enabled** | pointer stays `null`; the recorder's unconditional `if (fn == null) ThrowMeshShaderUnsupported();` throws `InvalidOperationException` naming `VK_EXT_mesh_shader`, `DeviceDescription.Extensions` and the `meshShader` feature | `ThrowPushDescriptorUnsupported` (`Recording/CommandRecorder.cs:463-467`) |
| Extension **enabled but `vkGetDeviceProcAddr` returns null** | throws `InvalidOperationException` at **`Device` construction**, naming both the entry point and the extension | `ThrowEntryPointMissing` (`Internal/DeviceFunctionTable.cs:377-383`) |

The second mode is why the design uses `ResolveExtensionRequired` inside the
`if`, not a bare `Resolve`: a driver that advertises the extension but does
not expose a command is a broken configuration, and the wrapper should say so
at device creation rather than at frame 4000.

The four `VK_EXT_debug_utils` pointers stay exactly as they are
(`:343-355`, plain `Resolve`). `VK_EXT_debug_utils` is an **instance**
extension; it can never appear in `DeviceDescription.Extensions`, so gating it
on that list would break `DebugMarker` on every device. The new mechanism is
for *device* extensions, and the XML doc must say so.

**Why this extends to #202 without a rewrite:** the acceleration-structure
set is a second `if` block keyed on
`IsEnabled(enabledExtensions, DeviceExtensionNames.AccelerationStructure)`,
adding create/destroy/build-sizes/device-address pointers alongside the
`vkCmd*` ones — no signature restriction exists, because the table already
holds a non-`vkCmd*` device-level pointer (`SetDebugUtilsObjectName`,
`:176-177`). This spec also widens the table's charter doc from "hot-path
`vkCmd*` only" to "hot-path `vkCmd*` **and every device-extension entry
point**", with the `InstanceFunctionTable:6-10` reason (the loader does not
export extension symbols) recorded in the comment so it is not re-argued.

Three new pointers:

```csharp
delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, uint, uint, uint, void>                                   CmdDrawMeshTasks;              // "vkCmdDrawMeshTasksEXT"u8
delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkBuffer_T*, ulong, uint, uint, void>                     CmdDrawMeshTasksIndirect;      // "vkCmdDrawMeshTasksIndirectEXT"u8
delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkBuffer_T*, ulong, VkBuffer_T*, ulong, uint, uint, void> CmdDrawMeshTasksIndirectCount; // "vkCmdDrawMeshTasksIndirectCountEXT"u8
```

Signatures are byte-identical in shape to `CmdDispatch` (`:99-100`),
`CmdDrawIndirect` (`:87-88`) and `CmdDrawIndirectCount` (`:90-91`) — the
issue's requested shape.

### C. Builder mesh path: null both, and reject what would be silently dropped

`Build()` computes `bool meshPath = _mesh != null;` and then:

```csharp
pVertexInputState   = meshPath ? null : &vertexInput,
pInputAssemblyState = meshPath ? null : &inputAssembly,
pTessellationState  = _tessControl != null ? &tessellation : null,   // unchanged
```

Nulling is the honest encoding, not a VU requirement — see the Evidence
finding. What *is* required is that the builder never quietly accept state
that a mesh pipeline discards, and never emit an illegal stage or dynamic-state
combination. Six new `InvalidOperationException` guards in the `Build()`
preamble, beside the five that live there today (`:382-394`):

1. `_task != null && _mesh == null` → task requires mesh (`-stage-02096`).
2. `meshPath && _vert != null` → `WithStages` and `WithMeshStages` are
   mutually exclusive (`-pStages-02095`).
3. `meshPath && (_geom != null || _tessControl != null || _tessEval != null)`
   → geometry/tessellation are primitive-family stages (`-pStages-02095`).
4. `meshPath && (!_vBindings.IsEmpty || !_vAttrs.IsEmpty)` →
   `WithVertexInput` has no effect on a mesh pipeline; a mesh shader reads
   its data through descriptors.
5. `meshPath && (_topologySet || _patchControlPoints != 0)` →
   `WithTopology` / `WithTessellation` have no effect. Requires one new
   `bool _topologySet` field, set by `WithTopology` (`:229-233`), because
   the existing `_topology` defaults to `TRIANGLE_LIST` (`:125`) and cannot
   otherwise be distinguished from "never called".
6. `meshPath` and the **effective** dynamic-state list (`:455` — the caller's
   override, or the viewport+scissor default) contains any of
   `VK_DYNAMIC_STATE_PRIMITIVE_TOPOLOGY`,
   `VK_DYNAMIC_STATE_VERTEX_INPUT_BINDING_STRIDE`,
   `VK_DYNAMIC_STATE_PRIMITIVE_RESTART_ENABLE`,
   `VK_DYNAMIC_STATE_PATCH_CONTROL_POINTS_EXT`,
   `VK_DYNAMIC_STATE_VERTEX_INPUT_EXT` → reject, naming the offending state
   (`-pDynamicStates-07065/07066/07067`). The default list can never trip
   this; only a `WithDynamicState` override can.

Guards 1–3 prevent driver-rejected pipelines. Guards 4–6 convert silent
state loss (4, 5) and a real VU violation (6) into a message at the call
site. All are setup-time; `Build()` already allocates nothing and continues
not to (guard 6 is a linear scan over a stack span).

The existing `!_stagesSet` message (`:382`) is reworded to name both
`WithStages` and `WithMeshStages`.

### D. Three recorder forwards

`Recording/CommandRecorder.cs`, appended to the `// ---- Draw / Dispatch ----`
block after `DrawIndexedIndirectCount` (`:563-572`):

```csharp
public void DrawMeshTasks(uint groupCountX, uint groupCountY = 1, uint groupCountZ = 1);
public void DrawMeshTasksIndirect(in Buffer buffer, ulong offset, uint drawCount, uint stride);
public void DrawMeshTasksIndirectCount(
    in Buffer buffer, ulong offset, in Buffer countBuffer, ulong countBufferOffset,
    uint maxDrawCount, uint stride);
```

`DrawMeshTasks` mirrors `Dispatch`'s defaulted Y/Z (`:574`) because it is a
workgroup dispatch, not a vertex count. The two indirect forms mirror
`DrawIndirect` (`:507`) and `DrawIndirectCount` (`:525-533`) parameter for
parameter. Each body is: read the pointer into a local, `if (fn == null)
ThrowMeshShaderUnsupported();`, call. One shared `[DoesNotReturn]` throw
helper. No spans, no marshalling, no allocation.

### E. `VulkanExtensions.ExtMeshShader`

`Rendering/VulkanExtensions.cs` gains
`ExtMeshShader => Utf8Name.FromLiteral(DeviceExtensionNames.MeshShader)`,
so callers write `Extensions = [VulkanExtensions.ExtMeshShader]` instead of
re-quoting the literal — the file's stated purpose (`:3-11`). The literal
itself lives once, in a new internal `Internal/DeviceExtensionNames.cs`
alongside the three entry-point names, mirroring `InstanceExtensionNames`
(`Internal/InstanceExtensionNames.cs:8-15`) and how `InstanceFunctionTable`
consumes it (`Internal/InstanceFunctionTable.cs:33`).

### Why not the alternatives

- **Mesh-only, defer the task stage** (the issue's own suggestion) —
  rejected: the state-model audit shows task costs one entry in four
  existing field groups and one validation line, changes no structural
  property, and does not move `MaxStages`; the follow-up diff would re-touch
  the same lines.
- **Mesh/task stages without specialization-constant support** — rejected:
  all five existing stages have it (`:194-211`); a mesh stage without it
  would be the sole exception, and mesh shaders are the stage where
  workgroup-size and max-vertices/primitives specialization is most useful.
- **A separate `MeshPipelineBuilder` type** — rejected: it would duplicate
  rasterization, multisample, depth-stencil, color-blend, dynamic-state,
  dynamic-rendering, layout and cache configuration verbatim, and every
  future addition to one would have to be mirrored in the other. The
  divergence is three stages and two null pointers.
- **Passing empty `pVertexInputState` / `pInputAssemblyState` on the mesh
  path** — rejected, but *not* because it is a VU violation (the audit shows
  it is not): it makes the struct claim vertex state the pipeline does not
  have, and it removes the builder's ability to reject a caller whose
  `WithVertexInput` would be silently discarded.
- **Silently ignoring `WithVertexInput` / `WithTopology` on a mesh
  pipeline** — rejected: the builder already chose "reject the mismatch here
  so the builder fails loud at Build instead of producing a pipeline whose
  state doesn't match what the caller asked for" for the color-blend count
  (`:395-409`). Same policy.
- **`AhjoValidation`-gated null-pointer guards on the draw commands** —
  rejected: `AhjoValidation.Enabled` is `false` in Release
  (`Diagnostics/AhjoValidation.cs:56-61`), so the guard would vanish in the
  build where the null dispatch is an access violation. The precedent
  (`Recording/CommandRecorder.cs:408-409`) is an unconditional check.
- **`AhjoValidationException` instead of `InvalidOperationException`** —
  rejected: every `AhjoValidation.Fail` call site in the repo is behind
  `IsEnabled`; using the type outside that gate would blur the one
  distinction the class exists to draw.
- **Resolving the EXT pointers unconditionally with plain `Resolve`, like the
  debug-utils entries** — rejected: the issue asks for enabled-gated
  resolution, and it is the correct answer. Loader behaviour for a device
  extension command that was not enabled is not something the wrapper should
  depend on; gating on the list the wrapper itself passed to `vkCreateDevice`
  is deterministic. It also gives the enabled-but-missing case a distinct,
  loud failure that a bare `Resolve` cannot express.
- **A `[Flags] enum` of known device extensions instead of per-group `if`s** —
  rejected as premature: with one extension it is bit-assignment bookkeeping
  for no gain, and #202 adds two or three more `if` blocks either way. If the
  count reaches double digits the refactor is local to the constructor.
- **Calling `Vk.vkCmdDrawMeshTasksEXT` (the generated `[DllImport]`)
  directly** — rejected: the loader does not export extension symbols
  (`Internal/InstanceFunctionTable.cs:6-10`); the `DllImport` would resolve
  or not depending on the host loader build.
- **Storing the enabled-extension list on `Device` / adding a public
  `Device.IsExtensionEnabled`** — rejected for this issue: no consumer needs
  it (the null-pointer throw already answers "can I mesh-draw?" and the
  caller decided the answer when it built the device), and it would allocate
  a copy per device. Explicitly *not* precluded — the ctor already receives
  the span, so adding it later is additive.
- **Exposing `VkPhysicalDeviceMeshShaderPropertiesEXT` (a properties-chain
  query on `PhysicalDevice`)** — rejected as a separate design: it is a
  general mechanism (`vkGetPhysicalDeviceProperties2` + caller-supplied
  chain) that #202 will want too for acceleration-structure limits, and
  bolting a mesh-specific accessor on now would be the wrong shape. Recorded
  above as a known limitation. **Reversed on timing, not on shape** — the
  separate design was written and implemented on this same branch
  (`../specs/2026-08-22-issue-201-properties-chain-query-design.md`). The
  "separate design, general mechanism" judgement held; what did not hold was
  deferring it past the branch that shipped the doc-only obligation. See the
  superseded note in Evidence.
- **Runtime bounds checks on `groupCount*` against the mesh properties** —
  rejected: the recorder cannot see the bound pipeline's stages (task vs
  mesh limits differ, `-07322` vs `-07326`) or the device properties, and
  `DrawIndirect`/`DrawIndirectCount` set the precedent of documenting rather
  than checking (`Recording/CommandRecorder.cs:501-573`).
- **`VK_NV_mesh_shader` support** — rejected: `vkCmdDrawMeshTasksNV`
  (`Generated/Vk.cs:2445`) has an incompatible model (`taskCount`/`firstTask`
  vs 3-D group counts) and its execution model cannot be mixed with EXT
  (`-TaskNV-07063`). No consumer asked; EXT is the cross-vendor one.
- **A `HelloMeshShader` sample** — rejected for this issue: samples run in
  CI's AOT smoke and headless lanes, and no CI host is guaranteed
  mesh-capable. Revisit when a host is.
- **Promoting the duplicated UTF-8 name comparison
  (`Instance.PointerStringEquals:473-481`,
  `PhysicalDeviceInfo.NameEquals:82`) into `Internal/Utf8.cs`** — rejected as
  out of scope: `MemoryMarshal.CreateReadOnlySpanFromNullTerminated` +
  `SequenceEqual` (`Instance.cs:524-527`) does the job in the new code
  without a third copy or a two-file refactor.

## Invariants honored

- **UTF-8 literals.** The extension name and all three entry-point names are
  `"…"u8` literals in `Internal/DeviceExtensionNames.cs`, reaching Vulkan
  through `Utf8Name.FromLiteral`. No `Encoding.UTF8.GetBytes` anywhere.
- **Native AOT.** Three more `delegate* unmanaged[Stdcall]` fields, two more
  `VkShaderModule_T*` fields, a span walk. No reflection, no generics over
  runtime types, no dynamic code.
- **Zero per-frame allocations.** The three recorder methods are a pointer
  load, a null test and a call — strictly thinner than `DrawIndirectCount`
  (`:525-533`). Extension resolution and every builder guard are setup-time.
- **Generated code untouched.** Everything native exists
  (`Generated/Vk.cs:3205-3211` and the structs listed in Problem). No rsp
  change, no regen.
- **`TreatWarningsAsErrors`.** No suppressions. The one API-compat note: the
  `Device` and `DeviceFunctionTable` constructors are `internal`
  (`Lifecycle/Device.cs:52`, `Internal/DeviceFunctionTable.cs:200`), so
  adding a parameter breaks no public surface; the only call sites are
  `Lifecycle/PhysicalDevice.cs:290` and `Lifecycle/Device.cs:55`.

## Test strategy (constrained by #32 and #158)

Three tiers, and the mesh-capable tier is the *smallest* of them by design —
most of the new behaviour is rejection logic that never touches a driver.

**Tier 1 — driver-free.** Nothing; every builder guard needs a `Device` to
obtain the builder.

**Tier 2 — `[gate:driver]` only, runs on any host with an ICD (mesh-capable
or not).** This is where the bulk of the coverage lives, because `Build()`'s
guards throw before `vkCreateGraphicsPipelines` and
`ShaderModule.FromRaw(0xDEADBEEF)` supplies non-null handles
(`Pipelines/ShaderModule.cs:29`):

- Each of the six new guards throws `InvalidOperationException`.
- The `!_stagesSet` message mentions `WithMeshStages`.
- `Device.Functions.CmdDrawMeshTasks == null` on a device created **without**
  `VK_EXT_mesh_shader` — the direct proof that gating works.
- `CommandRecorder.DrawMeshTasks*` on such a device throws
  `InvalidOperationException` naming `VK_EXT_mesh_shader`.

**Tier 3 — `[gate:feature]`, needs a driver that exposes
`VK_EXT_mesh_shader`.** Gated with the `ExportableResourceTests`
try-create-catch-`EXTENSION_NOT_PRESENT` pattern
(`tests/Ahjo.Vulkan.Tests/ExportableResourceTests.cs:176-209`):

- All three pointers resolve non-null.
- A mesh-only pipeline builds; a task+mesh pipeline builds.
- Under the validation layer, an end-to-end record + submit
  (`BeginRendering` → bind → `DrawMeshTasks(1,1,1)` → `EndRendering`)
  produces no validation errors — the `SplitBarrierTests` oracle shape.

**What CI will actually run.** Wrapper tests are Windows-only (#32). Whether
the Windows runner's ICD exposes `VK_EXT_mesh_shader` is **not known** and
must not be assumed: the lane declares `AHJO_VULKAN_TIER` and tier 3 will
report `[gate:feature]` skips on any host without it, which the coverage
summary classifies rather than failing on
(`tests/Shared/TestGate.cs:5-18`). Tier 3 is therefore expected to be
developer-machine coverage, quoted in the PR, not CI coverage — the same
honest position #32 established. Tiers 1–2 carry CI.

**Shader fixtures.** New `mesh_tri.mesh` (and `mesh_tri.task`) under
`tests/Ahjo.Vulkan.Tests/Shaders/`, reusing the existing `triangle.frag`
(input-free, `Shaders/triangle.frag`). They need a **second** MSBuild target
with `--target-env=vulkan1.3`; the existing target
(`Ahjo.Vulkan.Tests.csproj:44-58`) passes no `--target-env`, so its
`vulkan1.0` default cannot compile a `GL_EXT_mesh_shader` source. Existing
`.spv` outputs must stay byte-identical, so the existing target is not
modified.

## Benchmarks

New `tests/Ahjo.Vulkan.Benchmarks/MeshShaderBenchmarks.cs`, a **separate
class** on the `DescriptorSetPoolVariableCountBenchmarks` precedent
(`:18-26`) so a non-mesh host fails only this `[GlobalSetup]` and leaves
`CommandRecorder.RenderingPass100Cmds` (the #29 canary) intact. Two
`[MemoryDiagnoser]` benchmarks with `OperationsPerInvoke`, recorded inside a
`BeginRendering` scope with a real mesh pipeline bound — a bare `vkCmd*` draw
with no bound pipeline is not a shape the repo currently records, and
recording it is a VU violation even without submission:

- `DrawMeshTasks_1024`
- `DrawMeshTasksIndirectCount_1024` — Ahjo's actual shape.

Both `Allocated` cells must read `-`. `docs/benchmarks.md` gains the two rows
(`n/m` mean if the authoring host has no mesh GPU, the convention already used
for the `BindDescriptorSets.*` rows) and a Caveats bullet naming the new
`[GlobalSetup]` requirement, next to the existing
`DescriptorSetPoolVariableCountBenchmarks` bullet (`docs/benchmarks.md:119-130`).

The builder change is setup-time; `GraphicsPipelineBuilderBenchmarks.Build_AlphaBlend_Msaa4x_DynamicLineWidth`
(`tests/Ahjo.Vulkan.Benchmarks/GraphicsPipelineBuilderBenchmarks.cs:49-61`)
takes the classic path and its `Allocated` must stay `-`; re-running it is a
regression check, not a new row.

## Uncertainty, stated

- **The issue's "must OMIT both" is stronger than the spec.** The pinned
  `validusage.json` has no rule forbidding a non-null
  `pVertexInputState`/`pInputAssemblyState` on a mesh pipeline, and `vk.xml`
  marks both `optional="true"`. The design nulls them for honesty and for the
  reject-don't-discard guards, not to avoid a validation error. If a
  validation-layer build is later found to warn on it, that only strengthens
  the same decision.
- **VUID numbers** are read from
  `native/downloaded/Vulkan-Headers-vulkan-sdk-1.4.341.0/registry/validusage.json`
  at the tag pinned in `Directory.Build.props:18`; doc comments should quote
  the requirement and cite the number, per the #155 renumbering caveat.
- **CI mesh availability is unknown.** No probe exists in this repo for
  `VK_EXT_mesh_shader` on the `windows-latest` runner. The test plan assumes
  it is absent and is structured so that assumption costs nothing.
- **No in-repo consumer to audit.** The `DrawMeshTasksIndirectCount`-with-a-
  compute-written-count shape comes from the issue's description of Ahjo
  ADR-0028; there is no call site in `src/`, `samples/` or `tests/` to check
  it against. The signature mirrors the existing `DrawIndirectCount`, so a
  mismatch would be a naming question, not a redesign.
- **`--target-env=vulkan1.3` for the mesh fixtures** is the conservative
  choice (`GL_EXT_mesh_shader` needs SPIR-V 1.4, i.e. `vulkan1.2` or newer);
  it has not been executed against this repo's glslc, because the authoring
  session did not run a Vulkan-SDK build.
