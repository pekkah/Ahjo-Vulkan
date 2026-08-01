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
implementation is in the linkage — Slang refuses with
`error[E50100]: no type conformances found` at `Spirv(...)`, not at `Link()`.
Declare the implementation with `AddTypeConformance`:

```csharp
using var program = session.CreateProgram()
    .Add(module)
    .Add(entryPoint)
    .AddTypeConformance("Glossy", "ISurface")
    .Link();
```

Type names are resolved when `Link()` runs, not when `AddTypeConformance` is
called — resolving a name needs a composite to resolve it against — so an
unknown type name throws `ArgumentException` from `Link()`.

There is deliberately **no `Specialize` method**. `IComponentType::specialize`
on a component whose global scope holds an interface-typed `ParameterBlock`
segfaults inside Slang's own type-legalization pass: `specialize` and `link`
both return success and the crash lands in the subsequent code-generation call.
Reproduced 3/3 on `v2026.14.1`. `AddTypeConformance` is the route that works for
that shape, and an API whose failure mode is a process crash cannot ship behind
a `try`.

## Reflection-driven layouts

A linked program knows its own binding surface, and `SlangReflection` hands it
back as the description types `Ahjo.Vulkan` already takes — there is no parallel
`Slang*` type set to convert from.

```csharp
SlangReflection reflection = program.Reflection;

for (int i = 0; i < reflection.DescriptorSetCount; i++)
{
    uint set = reflection.SetIndex(i);            // the Vulkan set number
    using var layout = device.CreateDescriptorSetLayout(
        new DescriptorSetLayoutDescription { Bindings = reflection.Bindings(i) });
}

using var pipelineLayout = device.CreatePipelineLayout(new PipelineLayoutDescription
{
    SetLayouts         = layouts,
    PushConstantRanges = reflection.PushConstantRanges,
});
```

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

`reflection.VertexAttributes(entryPointIndex)` gives `Location` and `Format` for
each real varying input of a vertex entry point, struct-typed inputs recursed
one level with locations accumulating. System values (`SV_VertexID`,
`SV_InstanceID`, `SV_IsFrontFace`, `SV_Position`) are excluded — without that
filter an `SV_InstanceID` emits a phantom attribute at location 0 that collides
with the real `POSITION`.

**`Binding` and `Offset` are left at their defaults and the caller must fill
them.** A shader states its input locations and formats but never how the
application packs its vertex buffers, so those two fields and every field of
`VertexBindingDescription` are information reflection does not have. There is
deliberately no `VertexInputDescription` factory here, and composition does not
change that.

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

## Licensing

The Slang compiler binary is Apache-2.0 WITH LLVM-exception (the Khronos Group /
NVIDIA) and ships in `Ahjo.Vulkan.Slang.Native`, which this package depends on.
This package's own code is MIT.
