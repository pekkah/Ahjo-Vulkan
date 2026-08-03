# Ahjo.Vulkan.Slang

Compile [Slang](https://github.com/shader-slang/slang)/HLSL shader source to
SPIR-V at run time, with a pinned, checksum-verified compiler and the compiler's
own diagnostics surfaced as exceptions. The words come back as a
`ReadOnlySpan<uint>` that `Device.CreateShaderModule` already takes.

```csharp
using Ahjo.Vulkan;
using Ahjo.Vulkan.Slang;

using var compiler = SlangCompiler.Create();
using var session  = compiler.CreateSession(default);   // SPIR-V 1.5, direct emission

using var program = session.Compile(new SlangCompileRequest
{
    Path = "shaders/triangle.slang",
    // EntryPoints = null  ->  every [shader("…")] entry point, in declaration order
});

for (int i = 0; i < program.EntryPointCount; i++)
{
    SlangEntryPointInfo info = program.EntryPoint(i);
    using ShaderModule module = device.CreateShaderModule(program.Spirv(i));
    // info.Name, info.Stage  ->  the stage flags a pipeline builder wants
}
```

`Ahjo.Vulkan` itself takes no dependency on this package, so a consumer shipping
precompiled SPIR-V never pulls ~31 MB of compiler.

## Diagnostics are not optional

Every call that can produce compiler text gets its blob read before anything
else, and there is **no code path that returns an empty SPIR-V blob on
failure** — that is the whole point.

- A failure throws `SlangCompilationException`. `Diagnostics` carries Slang's
  text verbatim; `Message` is its first line.
- A success that produced text anyway — warnings — lands in
  `SlangProgram.Warnings` (and `SlangModule.Warnings` for a module you loaded
  yourself).

```
error[E30015]: undefined identifier
 --> bad.slang:1:21
  |
1 | float4 f() { return notAThing; }
  |                     ^^^^^^^^^ undefined identifier 'notAThing'.
```

Note that `loadModuleFromSourceString` signals failure by returning `null`
rather than by a result code, so a result-code-only check would sail past a
broken compile. This wrapper checks both.

## Composing a program from several modules

A material system's shader is not one file. Load each module under its own name
and the session's registry makes `import` work with no file system present:

```csharp
using var common   = session.LoadModuleFromSource("common",   "common.slang",   commonSource);
using var material = session.LoadModuleFromSource("material", "material.slang", materialSource);
// material's source can `import common;`

using var entryPoint = material.FindEntryPoint("fragmentMain", ShaderStages.Fragment);
```

Two things worth knowing about `FindEntryPoint`:

- Slang does **not** validate the stage you ask for against the function's
  `[shader("…")]` attribute. Measured on `v2026.14.1`: asking for a fragment
  entry point as `ShaderStages.Vertex` returns success and hands back the
  fragment entry point labelled `Vertex`. This wrapper reads the declared stage
  back and throws `SlangCompilationException` on a disagreement. A function
  with *no* attribute has no declared stage, so the stage you pass is what
  finds it and what it reports — that is the case the parameter exists for.
- An unmapped `ShaderStages` value (a mask like `AllGraphics`, or a ray-tracing
  stage the wrapper has no member for) throws `NotSupportedException` rather
  than degrading to "no stage".

### The component list is ordered, and the order is the layout

**The order components are added is the order Slang assigns descriptor
bindings, descriptor spaces and entry-point indices. Adding the same components
in a different order produces a different, equally valid, incompatible layout.**
That is a measurement: the same five components composed as
`[common, geometry, material, vs, fs]` and as
`[material, common, geometry, fs, vs]` give every parameter different set and
binding numbers and swap the entry-point indices, and the emitted SPIR-V is
decorated to match whichever one you asked for. Composing only the entry points
also links — an entry point carries its module as a requirement — and produces a
third assignment again. So the list is explicit:

```csharp
using var program = session.CreateProgram()
    .Add(common)            // modules first, in the order you want their
    .Add(geometry)          // parameters laid out
    .Add(material)
    .Add(vertexEntryPoint)  // entry-point order is Spirv(i) / EntryPoint(i) order
    .Add(fragmentEntryPoint)
    .Link();
```

`Link()` may be called more than once and returns independent programs; the
builder keeps no linked state.

### Specializing an interface-typed `ParameterBlock`

A `ParameterBlock<ISomeInterface>` cannot generate code until at least one
implementation is in the linkage. Name them on the request:

```csharp
using var program = session.Compile(new SlangCompileRequest
{
    Source           = surfaceSource,
    ModuleName       = "surface",
    TypeConformances = [new SlangTypeConformance("Glossy", "ISurface")],
});
```

For a program composed from several modules, the same declaration lives on the
builder:

```csharp
using var program = session.CreateProgram()
    .Add(module)
    .Add(entryPoint)
    .AddTypeConformance("Glossy", "ISurface")
    .Link();
```

Type names are resolved when the program is linked, not when the conformance is
declared — resolving a name needs a composite to resolve it against — so an
unknown type name throws `ArgumentException` from `Link()` (or from `Compile`,
which composes through the builder).

**The refusal lands at `Spirv(...)`, not at `Link()`, and that is deliberate.**
Without a conformance this shape links cleanly: `EntryPointCount` is right,
`EntryPoint(0)` reports the right name and stage, and reflection reports the
whole binding surface. Only code generation fails, with
`error[E50100]: no type conformances found` — and this package rewrites that
exception's `Message` to name `SlangCompileRequest.TypeConformances` and
`AddTypeConformance` (`Diagnostics` stays Slang's text, verbatim). Refusing at
`Link()` instead would throw away a reflection that is correct and useful, on
the strength of a predicate Slang does not expose:
`SlangProgram.SpecializationParameterCount` reports `1` for this shape both
*before and after* the conformance that makes it compile, so it is a report, not
a test for "will this generate code".

There is deliberately **no `Specialize` method**. `IComponentType::specialize`
on a component whose global scope holds an interface-typed `ParameterBlock`
segfaults inside Slang's own type-legalization pass: `specialize` and `link`
both return success and the crash lands in the subsequent code-generation call.
Reproduced 3/3 on `v2026.14.1`. `AddTypeConformance` is the route that works for
that shape, and an API whose failure mode is a process crash cannot ship behind
a `try`.

## Reflection-driven layouts

A linked program knows its own binding surface, and `SlangReflection` hands it
back as `Slang*` types that describe what the *shader* declared. `SlangVulkanMapping`
converts them to the description types `Ahjo.Vulkan` takes, with one extension
method per shape.

```csharp
SlangReflection reflection = program.Reflection;

for (int i = 0; i < reflection.DescriptorSetCount; i++)
{
    uint set = reflection.SetIndex(i);            // the Vulkan set number
    using var layout = device.CreateDescriptorSetLayout(
        new DescriptorSetLayoutDescription { Bindings = reflection.Bindings(i).MapBindings() });
}

using var pipelineLayout = device.CreatePipelineLayout(new PipelineLayoutDescription
{
    SetLayouts         = layouts,
    PushConstantRanges = reflection.PushConstantRanges.MapPushConstantRanges(),
});
```

The indirection is deliberate. Reflection reports what the shader declared;
`VkDescriptorType` is one interpretation of that, and a binding Slang reports as
a mutable texture is a `STORAGE_IMAGE` only because this mapping says so. Keeping
the two apart means a caller who disagrees can read `SlangDescriptorBinding` and
build the `DescriptorBinding` themselves, rather than being handed a decision
already baked in. `MapBindingType` and `MapScalarFormat` are public for the same
reason.

Reflection is only ever taken from a **linked** program, and that is enforced by
the type system rather than by documentation. Both alternatives are silently
wrong rather than loudly wrong: a module reflected on its own reports different
sets and binding numbers than the same module inside a composite, and an
unspecialized generic parameter block reports a descriptor set with *zero*
bindings where the compiled shader has five.

`ParameterBlock<T>` is fully supported and is the point: each block is its own
descriptor space, nesting accumulates, and a block whose element carries
ordinary data gets the implicit uniform buffer Slang puts at binding 0 of its
space but never lists as a descriptor range.

### Three names, and which one to key on

Reflection reports a name for each of three different things, and they are not
interchangeable:

| what | where | what it is |
|---|---|---|
| `SlangDescriptorBinding.Name` | every binding | the declared parameter name — `gAlbedo`, `gMaterial`. **This is what a material system keys on** instead of hard-coding slot numbers. |
| `SlangPushConstantRange.Name` | the push-constant range | the `[[vk::push_constant]]` block parameter's name. Vulkan never sees it; it is how you tell which block a layout describes. |
| `SlangVertexAttributeDescription.SemanticName` + `SemanticIndex` | vertex inputs | **what a vertex-buffer binder matches on.** `Name` on the same type is the field's name in the shader struct and has nothing to do with your mesh format. |

Two things about those to get right up front. A resource declared *inside* a
`ParameterBlock` is named by its field — reflection says `maps`, the emitted
SPIR-V says `gWith.maps` — and the block's implicit uniform buffer at binding 0
has no name of its own, so it takes the block parameter's. And Slang splits
`TEXCOORD0` into `SemanticName = "TEXCOORD"` and `SemanticIndex = 0`, so a
binder comparing against the literal `"TEXCOORD0"` never matches.

Bindings also carry `ImageFormat` — Slang's own `SlangImageFormat`, for a
`[[vk::image_format("rgba8")]] RWTexture2D<float4>`, and
`SLANG_IMAGE_FORMAT_unknown` for everything else. There is deliberately no
`VkFormat` mapping: no Vulkan call at layout-creation time takes one, and the
value matters when you create the `VkImageView` you write the binding with.

> **`_unknown` is a device requirement, not a benign default.** A storage image
> the shader reads or writes without a declared format needs the capability for
> it. Below Vulkan 1.3 without `VK_KHR_format_feature_flags2`, an
> `Unknown`-format storage image must be `NonReadable` / `NonWritable` unless
> `shaderStorageImageReadWithoutFormat` /
> `shaderStorageImageWriteWithoutFormat` is enabled
> (`VUID-RuntimeSpirv-apiVersion-07955` / `-07954`). From 1.3 on the check moves
> to draw/dispatch time and lands on the bound view's format features —
> `VK_FORMAT_FEATURE_2_STORAGE_READ_WITHOUT_FORMAT_BIT` /
> `..._WRITE_WITHOUT_FORMAT_BIT`
> (`VUID-vkCmdDispatch-OpTypeImage-07028` / `-07027`). Plain
> `RWTexture2D<float4> gPlain;` is exactly this case. Conversely, a
> *non*-`unknown` value obliges you the other way: the `VkImageView` bound there
> must have a matching format.

`SlangEntryPointInfo` carries the compute thread-group size —
`ThreadGroupSizeX/Y/Z`, `1, 1, 1` for every non-compute stage — which is what a
dispatch needs to turn a work item count into a group count.

> **`SlangEntryPointInfo.Name` is not `VkPipelineShaderStageCreateInfo.pName`.**
> Slang names every emitted entry point `main` regardless of the stage or the
> Slang function's name, while reflection reports the Slang name
> (`vertexMain`). Passing the reflected name as `pName` produces
> `VUID-VkPipelineShaderStageCreateInfo-pName-00707` and a non-null handle.
> `spReflectionEntryPoint_getNameOverride` does not help — measured on
> `v2026.14.1`, it returns exactly what `getName` returns — which is why no
> member here carries it.

### Bindless arrays: reflection reports, you supply the capacity

`Texture2D gTextures[];` has no descriptor count — the shader does not state
one, so neither does reflection. `SlangDescriptorBinding.Count` is therefore an
option (`SlangDescriptorCount`), not a `uint`:

```csharp
foreach (var binding in reflection.Bindings(i))
{
    if (binding.Count.TryGetValue(out uint count))    // Fixed
        …
    else if (binding.Count.IsUnbounded)               // SLANG_UNBOUNDED_SIZE
        …
}
```

`MapBinding()` refuses such a binding with `NotSupportedException`, because a
`VkDescriptorSetLayoutBinding.descriptorCount` has to come from somewhere and
reflection cannot pick your heap's capacity. Supply it:

```csharp
var bindings = reflection.Bindings(i).MapBindings(static binding => binding.Slot switch
{
    0 => 4096u,        // gTextures
    1 => 32u,          // gSamplers
    _ => 1024u,
});
```

The resolver is asked **only** about bindings reflection could not size, so it
never has to second-guess a count the shader did state. `MapBinding(binding,
descriptorCount)` is the single-binding form, and it throws `ArgumentException`
if reflection *did* report a count — overriding what the shader declares is not
something an overload should do quietly.

**That array is not yet a layout a driver will accept.** A capacity like 4096
with `BindingFlags = None` is measured against the **base** per-stage descriptor
limits, not the update-after-bind ones — and the base limits are small: the
Vulkan-guaranteed minimum for `maxPerStageDescriptorSampledImages` is **16**.
The six-figure numbers usually quoted for bindless are
`maxPerStageDescriptorUpdateAfterBindSampledImages`, and a binding only gets
measured against those once it is flagged `UpdateAfterBind` and its layout is
created with `UpdateAfterBindPool = true`. Miss that and
`vkCreatePipelineLayout` rejects the layout.

So a bindless heap needs three things this package cannot do for you.

**1. Device features.** Slang emits `OpCapability RuntimeDescriptorArray` and
`OpTypeRuntimeArray` for `Texture2D gTextures[]`, and a declared capability
whose requirement is not met is rejected when the module is created
(`VUID-VkShaderModuleCreateInfo-pCode-08740`) — so `runtimeDescriptorArray` has
to be on. These are Vulkan 1.2 core features, which the configurer hands out by
`ref`:

```csharp
using var device = gpu.CreateDevice(new DeviceDescription
{
    Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],

    ConfigureFeatures = static (
        ref ChainBuilder<VkDeviceCreateInfo> _,
        ref VkPhysicalDeviceFeatures2        _,
        ref VkPhysicalDeviceVulkan12Features f12,
        ref VkPhysicalDeviceVulkan13Features _,
        ref VkPhysicalDeviceVulkan14Features _) =>
    {
        f12.runtimeDescriptorArray                       = 1;
        f12.descriptorBindingPartiallyBound              = 1;

        // Update-after-bind is enabled per descriptor TYPE, not once. The
        // three unbounded arrays above are a SAMPLED_IMAGE, a SAMPLER and —
        // because StructuredBuffer<float4> maps to it — a STORAGE_BUFFER, so
        // flagging all three UpdateAfterBind needs both of these bits.
        f12.descriptorBindingSampledImageUpdateAfterBind  = 1;
        f12.descriptorBindingStorageBufferUpdateAfterBind = 1;

        // NOT needed by the fixture above, and enabling what you do not need is
        // its own failure — see the VK_ERROR_FEATURE_NOT_PRESENT note below.
        // `gTextures[gPush.index]` is dynamically uniform: a push constant is
        // the same value for every invocation in the draw, so Slang emits no
        // ShaderNonUniform capability and no `OpDecorate … NonUniform` for it.
        // Enable the matching bit only when the index comes from a
        // per-invocation source — an interpolated varying, a buffer read, a
        // material ID — *and* the shader wraps it in
        // `NonUniformResourceIndex(...)`, which is what makes Slang emit the
        // capability and the decoration:
        //
        // f12.shaderSampledImageArrayNonUniformIndexing = 1;
    },
});
```

**The per-type point is the generalizable one.** Miss
`descriptorBindingStorageBufferUpdateAfterBind` and `vkCreateDescriptorSetLayout`
rejects the very layout step 2 builds —
`VUID-VkDescriptorSetLayoutBindingFlagsCreateInfo-descriptorBindingStorageBufferUpdateAfterBind-03008`.
Whichever descriptor types your heap holds, enable the matching
`descriptorBinding<Type>UpdateAfterBind` for each of them.

Check the bits first with `vkGetPhysicalDeviceFeatures2`; they are optional, and
`vkCreateDevice` fails with `VK_ERROR_FEATURE_NOT_PRESENT` on a device that does
not advertise one you asked for.

**2. Binding flags and an update-after-bind layout.** The mapper leaves
`BindingFlags` at `None`, so stamp them on the heap bindings yourself:

```csharp
for (int b = 0; b < bindings.Length; b++)
{
    if (reflection.Bindings(i)[b].Count.IsUnbounded)
        bindings[b] = bindings[b] with
        {
            BindingFlags = DescriptorBindingFlags.UpdateAfterBind
                         | DescriptorBindingFlags.PartiallyBound,
        };
}

using var layout = device.CreateDescriptorSetLayout(new DescriptorSetLayoutDescription
{
    Bindings            = bindings,
    UpdateAfterBindPool = true,      // required by the UpdateAfterBind flag above
});
```

`PartiallyBound` is not decoration either: without it, every one of those 4096
descriptors must have been written before a draw that could index them, and
core validation reports the first one that was not. Allocate the set from a pool
created with `updateAfterBind: true` to match
(`new DescriptorSetPool(device, maxSets, sizes, updateAfterBind: true)`).

**3. `VariableDescriptorCount`, if you want the count to shrink per set.**
Vulkan allows `VariableDescriptorCount` on at most one binding per set — the one
with the highest binding number — and a set with three unbounded arrays is the
shape this exists for. Add `VariableDescriptorCount` / `PartiallyBound` to the
binding whose count actually varies.

This one needs more than the flag. It additionally requires
`descriptorBindingVariableDescriptorCount` enabled on the device — and without
it the problem lands at *layout* creation, before any pool is involved: the flag
violates
`VUID-VkDescriptorSetLayoutBindingFlagsCreateInfo-descriptorBindingVariableDescriptorCount-03014`
at `vkCreateDescriptorSetLayout`. Expect the validation layer to report it, not
the driver to refuse — a VUID violation is undefined behaviour, and hardware
drivers have been measured returning `VK_SUCCESS` here.

The count itself is stated at allocation time, and the pool has an overload for
it:

```csharp
DescriptorSet set = pool.Acquire(layout.Handle, variableDescriptorCount: 1024);
```

That count says how many descriptors *this set* holds in that binding. It must
not exceed the layout's declared `Count`
(`VUID-VkDescriptorSetAllocateInfo-pSetLayouts-09380`), and it is the number the
driver checks every write against — not the declared maximum.

Allocating such a layout through the one-argument `pool.Acquire(layout.Handle)`
gives that binding an effective count of **zero**, and every write to it fails.
The wrapper cannot warn you: Vulkan exposes no way to read a layout's binding
flags back from the handle, so `Acquire` cannot tell a variable-count layout
from an ordinary one. `VK_LAYER_KHRONOS_validation` does report it, and its
message asks the question directly.

There is one thing reflection still refuses outright, and it is not the count: a
descriptor range whose *index offset*, or a scope whose *space offset*, is a
sentinel throws `NotSupportedException` from reflection. A binding with no
binding number and a scope with no set number leave no layout to report at all,
so there is nothing to hand back.

### What is inside a buffer

A binding says *where* a resource lives. `TryGetBufferLayout` says what is in
it — the member names, byte offsets and sizes Slang baked into the SPIR-V it
emitted for this program's target.

```csharp
if (reflection.TryGetBufferLayout(set: 2, slot: 0, out SlangBufferLayout? material))
{
    Console.WriteLine($"{material.Name} is {material.Size} bytes");

    if (material.TryGetMember("Params.UvScale", out SlangBufferMember uvScale))
        WriteFloat2(bytes[(int)uvScale.Offset..], scale);
}

// Push constants are not descriptor bindings and have their own accessor.
if (reflection.TryGetPushConstantLayout(out SlangBufferLayout? push))
    …
```

Every buffer-shaped binding has one: a standalone `ConstantBuffer<T>`, the
global scope's implicit constant buffer, a `ParameterBlock`'s implicit uniform
buffer, and a structured buffer's element. A texture or a sampler reports
`false`.

Four rules the API will otherwise surprise you with:

1. **`Members` is a flattened pre-order walk with dotted paths, and struct nodes
   are kept.** `Params` appears as its own entry with its own offset and size,
   immediately followed by `Params.BaseColor`, `Params.Roughness`, … Filter
   `Kind != SLANG_TYPE_KIND_STRUCT` for the leaves.
2. **The list is losslessly a tree.** A member's parent is
   `Members[ParentIndex]` (`-1` at the root), and its children are the entries
   whose `ParentIndex` is its own index — contiguous and in declaration order,
   because the walk is pre-order. So the tree costs O(n) and no string parsing:
   ```csharp
   var children = new List<int>[layout.Members.Length];
   for (int i = 0; i < layout.Members.Length; i++) children[i] = [];
   for (int i = 0; i < layout.Members.Length; i++)
       if (layout.Members[i].ParentIndex >= 0) children[layout.Members[i].ParentIndex].Add(i);
   ```
   That is why there is one representation here and not two.
3. **Arrays are leaves.** An array member reports `ElementCount` and
   `ElementStride`; element `i` is at `Offset + i * ElementStride`. Paths like
   `Lights[0].Color` are not generated and an array-of-struct element's own
   members are not described — use `ToJson()` until the follow-up lands.
4. **Resources are not members.** A field with zero uniform size — a
   `Texture2D`, a `SamplerState` — occupies no bytes and is omitted rather than
   listed at offset 0 with size 0.

`Offset` and `Size` alone are not enough to fill a buffer by hand. `Stride` is
the padded footprint (a `float3` is 12 bytes of data in 16), `Alignment` is what
you check your own struct against, and `MatrixLayout` decides whether a
`float4x4` is written row- or column-major — the difference between a correct
transform and a transposed one, with no other symptom. `MatrixStride` is derived
from the row/column vector's layout, because Slang exposes no getter for it;
it is `0` rather than a guess when that derivation yields nothing.

**`ToJson()` is the escape hatch, for diagnostics.** It is Slang's own dump of
the whole program layout, lazily produced and cached. The schema is Slang's, is
not stable across versions, and is not something this package parses or
promises — use the typed surface for anything a program depends on, and this for
what the typed surface does not cover yet.

### `SV_VertexID` needs `shaderDrawParameters` enabled on the device

**If any vertex entry point takes `SV_VertexID`, enable
`shaderDrawParameters` when you create the device.** Slang emits the SPIR-V
`DrawParameters` capability for it — HLSL's `SV_VertexID` excludes the base
vertex where Vulkan's `VertexIndex` includes it, so the module computes
`VertexIndex - BaseVertex`, and `BaseVertex` requires that capability. The
same GLSL written with `gl_VertexIndex` requires nothing.

This is deliberately **not** enabled by default. `shaderDrawParameters` is an
*optional* Vulkan 1.1 feature and is still optional in 1.4, so switching it on
unconditionally would make `vkCreateDevice` fail with
`VK_ERROR_FEATURE_NOT_PRESENT` on a conformant device that does not advertise
it — a worse outcome than the diagnostic below. Enable it yourself:

```csharp
using var device = gpu.CreateDevice(new DeviceDescription
{
    Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],

    ConfigureFeatures = static (
        ref ChainBuilder<VkDeviceCreateInfo> chain,
        ref VkPhysicalDeviceFeatures2        _,
        ref VkPhysicalDeviceVulkan12Features _,
        ref VkPhysicalDeviceVulkan13Features _,
        ref VkPhysicalDeviceVulkan14Features _) =>
    {
        ref var f11 = ref chain.Push<VkPhysicalDeviceVulkan11Features>();
        f11.shaderDrawParameters = 1;
    },
});
```

`VkPhysicalDeviceVulkan11Features` is not one of the four structs the
configurer hands out by `ref`, but it is `IChainable<VkDeviceCreateInfo>`, so
`chain.Push<T>()` reaches it. The wrapper never pushes that struct itself, so
there is no duplicate-`sType` conflict.

**Forget it and nothing obviously breaks**, which is the reason this section
exists: `vkCreateShaderModule` still returns a usable handle and the shader
runs. Only the validation layer says anything, once:

```
vkCreateShaderModule(): SPIR-V Capability DrawParameters was declared, but one of
the following requirements is required
(VkPhysicalDeviceVulkan11Features::shaderDrawParameters OR VK_KHR_shader_draw_parameters).
VUID-VkShaderModuleCreateInfo-pCode-08740
```

Check support first with `vkGetPhysicalDeviceFeatures2` if you target hardware
that might lack it; on desktop it is effectively universal (the Vulkan Roadmap
2024 profile requires it).

### Set indices are set numbers, not positions

**A program's descriptor set indices need not start at 0 and need not be
contiguous.** `[[vk::binding(7, 2)]]` puts a sampler in set 2 whether or not set
1 exists, and a material system that reserves a space does the same.
`PipelineLayoutDescription.SetLayouts` is positional, so:

- `SetLayoutSlotCount` is the length that span must have — the highest declared
  set index plus one, not the number of populated sets;
- `TryGetSet(i, out bindings)` returns `false` for an index the program declares
  nothing in.

The reflected set numbers are baked into the emitted SPIR-V. Renumbering them to
be dense produces a pipeline layout that builds and then binds to the wrong
slots at draw time.

> **Open gap.** Vulkan fills a hole in a pipeline layout with a descriptor set
> layout that has zero bindings, but `Device.CreateDescriptorSetLayout` rejects
> an empty `Bindings` span, so there is currently no way to obtain one through
> this API. A reflected program that leaves a set index unused therefore cannot
> be turned into a complete `PipelineLayout` yet. Closing that is a decision in
> `Ahjo.Vulkan` itself; this package deliberately does not work around it with
> an invented binding.

### Stage flags: two modes, and one of them compiles

```csharp
SlangReflection precise = program.GetReflection(SlangStageAttribution.PerEntryPointUsage);
```

Slang's *reflection* API cannot say which stages use a binding —
`spReflectionVariableLayout_getStage` returns "none" for every global descriptor
parameter, and the JSON dump lists the whole global scope under every entry
point. The question is only answerable from the compiled artifact, through
`IComponentType::getEntryPointMetadata`.

| Mode | What `DescriptorBinding.Stages` gets | Cost |
|---|---|---|
| `ProgramStageUnion` (default, `program.Reflection`) | the union of the program's entry-point stages | none; cannot throw |
| `PerEntryPointUsage` | only the stages that actually read the binding | one code generation per entry point; can throw `SlangCompilationException` |

A superset of stages is always legal Vulkan, which is why precision is opt-in
rather than default. Under `PerEntryPointUsage`, a binding no entry point reads
falls back to the union — usage is reported post-optimization, and
`stageFlags = 0` is not a descriptor any stage could access.

**Push-constant ranges keep the union in both modes.**
`isParameterLocationUsed` reports a push constant as unused even for a stage
whose SPIR-V provably contains a `PushConstant` variable reading it, under every
parameter category and space swept, so there is no narrowing to be had.

### Vertex attributes, and the half this cannot fill in

`reflection.VertexAttributes(entryPointIndex)` gives the `Location` and the
declared type — `ScalarType`, `ComponentCount`, `Kind` — for each real varying
input of a vertex entry point, struct-typed inputs recursed one level with
locations accumulating. System values (`SV_VertexID`, `SV_InstanceID`,
`SV_IsFrontFace`, `SV_Position`) are excluded — without that filter an
`SV_InstanceID` emits a phantom attribute at location 0 that collides with the
real `POSITION`.

`MapVertexAttribute` turns one into a `VertexAttributeDescription`, resolving the
`VkFormat` from `(ScalarType, ComponentCount)`:

```csharp
foreach (var attr in reflection.VertexAttributes(entryPointIndex))
    attributes.Add(attr.MapVertexAttribute(binding: 0, offset: strideSoFar));
```

**`binding` and `offset` are parameters because reflection cannot know them.** A
shader states its input locations and formats but never how the application packs
its vertex buffers, so those two and every field of `VertexBindingDescription`
are information reflection does not have. They default to `0`, which is right for
a single tightly-packed buffer and wrong for anything else. There is deliberately
no `VertexInputDescription` factory here, and composition does not change that.

Note that the two refusals — a matrix-typed vertex input, and a
`(ScalarType, ComponentCount)` pair with no `VkFormat` — now throw
`NotSupportedException` from `MapVertexAttribute` rather than from reflection.
Reflection reports the matrix; only the mapping to Vulkan has to give up on it.

### Two things reflection refuses rather than guesses

- **More than one `[[vk::push_constant]]` block.** Two of them compose and link,
  and reflection reports two push-constant ranges — but the only offset it
  exposes is a push-constant *buffer index* (0, 1), not the byte offset
  `VkPushConstantRange.Offset` needs. `NotSupportedException`, naming both
  blocks.
- **A matrix-typed vertex input.** It occupies several consecutive locations and
  SPIR-V decorates it at the base one, but the per-location component count
  depends on the session's default matrix layout mode and only column-major has
  been verified against the emitted SPIR-V. `NotSupportedException`, naming the
  field.

Both are cases where a plausible guess produces a pipeline that builds and then
mis-binds, which is exactly the class of bug reflection exists to remove.

## Lifetimes

`SlangProgram.Spirv(i)` is a view over Slang-owned native memory the program
holds a reference on. It is valid **until the program is disposed** — the same
contract `SpirvBlob.Words` states. Copy it if it has to outlive the program.

Dispose in reverse order of creation: programs and modules, then sessions, then
the compiler. `SlangCompiler.Dispose` releases the global session and does not
call `slang_shutdown()`, so creating a second compiler in the same process
works.

Everything here is **setup-time**. Compiling allocates, and that is fine — the
repository's zero-per-frame-allocation invariant covers command recording,
synchronization, pools and memory, none of which this package touches.

## Native AOT

Clean, and proven rather than asserted: `samples/AotSmoke` compiles its triangle
with this package at startup and is published with `PublishAot=true` in CI, so
any reflection or dynamic-codegen creep on the compile path fails the publish.

## What this package does not do yet

The MSBuild task that would replace the `glslc` invocations still scattered
across this repository's samples — a follow-up to
[issue #166](https://github.com/pekkah/Ahjo-Vulkan/issues/166), and what finally
lets the CI coverage summary stop printing "NOT PROVEN" for SPIR-V gates.

There is also no permutation or variant-cache API, and no `Specialize` — see
"Specializing an interface-typed `ParameterBlock`" above for why the latter is a
decision rather than an omission.

On the reflection side, three gaps remain, each named with the entry points that
would close it:

- **Array-of-struct member paths** (`Lights[0].Color`). Needs a decision about
  element-relative offsets rather than an append; `ToJson()` covers it today.
- **User attributes on members and types** —
  `spReflectionVariable_GetUserAttributeCount` / `GetUserAttribute` and
  `spReflectionUserAttribute_GetArgumentValue{Int,Float,String}`. `[Attribute]`
  is how a material *editor* gets UI ranges and display names, and its argument
  model needs designing.
- **Member default values** (`spReflectionVariable_HasDefaultValue`,
  `GetDefaultValueInt`, `GetDefaultValueFloat`). The bound getters cannot
  express a `float3` default, so shipping them would describe defaults
  incorrectly.

`SlangImageFormat` also has no `VkFormat` mapping in `SlangVulkanMapping` yet —
a ~40-entry table with no consumer at layout-creation time.

## Licensing

The Slang compiler binary is Apache-2.0 WITH LLVM-exception (the Khronos Group /
NVIDIA) and ships in `Ahjo.Vulkan.Slang.Native`, which this package depends on.
This package's own code is MIT.
