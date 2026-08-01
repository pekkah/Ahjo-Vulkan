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
precompiled SPIR-V never pulls ~25 MB of compiler.

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

Composing those components into one linked program — where the *order* of the
component list is part of the layout contract — is
`SlangProgramBuilder`, which lands with reflection. Until then, `Compile`
handles the single-module case.

Two things worth knowing about `FindEntryPoint`:

- Slang does **not** validate the stage you ask for against the function's
  `[shader("…")]` attribute. Measured on `v2026.14.1`: asking for a fragment
  entry point as `ShaderStages.Vertex` succeeds silently. The `Stage` you get
  back is the one you passed. Use `DefinedEntryPoint(i)` when you want the
  stage the shader declares.
- An unmapped `ShaderStages` value (a mask like `AllGraphics`, or a ray-tracing
  stage the wrapper has no member for) throws `NotSupportedException` rather
  than degrading to "no stage".

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

Shader **reflection** — turning a linked program's descriptor sets, push
constants and vertex inputs into `DescriptorBinding`,
`PushConstantRange` and `VertexAttributeDescription` — is the next phase of
[issue #166](https://github.com/pekkah/Ahjo-Vulkan/issues/166). So is the
MSBuild task that would replace the `glslc` invocations still scattered across
this repository's samples.

## Licensing

The Slang compiler binary is Apache-2.0 WITH LLVM-exception (the Khronos Group /
NVIDIA) and ships in `Ahjo.Vulkan.Slang.Native`, which this package depends on.
This package's own code is MIT.
