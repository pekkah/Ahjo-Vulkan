Paired with [../specs/2026-09-04-issue-219-hello-dlaa-design.md](../specs/2026-09-04-issue-219-hello-dlaa-design.md).

# Implementation plan — issue #219: `Swapchain.GetImage`, `samples/HelloDlaa`, consumer docs

**Branch:** `issue-216-ngx-native` (existing). **PR:** [#217](https://github.com/pekkah/Ahjo-Vulkan/pull/217) (existing). Do **not** create a new branch or PR.

**What this issue touches under `src/`:** exactly one file,
`src/Ahjo.Vulkan/Rendering/Swapchain.cs`, gaining exactly one public method
(spec D11). Nothing else in `src/` changes — not `Image`, not `HandleRegistry`,
not `ImageBlitRegion`, nothing in `Ahjo.Vulkan.Ngx`. If a step appears to need
more, stop and report it.

**What this issue does not touch:** `docs/migration-vortice-to-ahjo.md`. The
issue's upscaler-note item is **declined** by the repo owner, not deferred; the
spec's Scope boundary records the reason.

Conventions the whole plan assumes: `System.Numerics` row-vector matrices
(`v * M`), a y-**down** NDC via `proj.M22 *= -1f`, and DLSS pixel space with the
origin at the top-left, +Y down. Spec D2 is the derivation; do not re-derive it.

Steps 1 and 2 are the core-package change and come first, because step 10 calls
the API they add and because a reviewer should see the smallest, most scrutinized
part of the diff on its own.

---

## Step 1 — `Swapchain.GetImage(uint index)` (core public API)

**File:** `src/Ahjo.Vulkan/Rendering/Swapchain.cs`, immediately after
`GetImageHandle` (currently line 90). One method, no other edit to the file, no
new field, no change to `Recreate`.

```csharp
public Image GetImage(uint index)
```

Behaviour, exactly:

1. `ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, (uint)_images.Length);`
2. Return an `Image` built through `Image`'s existing internal constructor with:

   | Argument | Value |
   |---|---|
   | `handle` | `_images[index]` |
   | `allocation` | `null` |
   | `owner` | `default` |
   | `format` | `_format.format` |
   | `width` / `height` | `_extent.width` / `_extent.height` |
   | `depth` | `1` |
   | `mipLevels` | `1` |
   | `arrayLayers` | `1` (hardcoded at creation, `Swapchain.cs:521`) |
   | `usage` | `_imageUsage` |
   | `persistentMapped` | `null` |

   Constructed on demand — do **not** cache an `Image[]`; that would be a second
   array `Recreate` has to keep in sync, for eleven field assignments.

**Doc comment.** It carries four things, each of which a consumer will otherwise
get wrong:

- **The returned image is borrowed.** `OwnsHandle` is `false` because `owner` is
  `default`, so `Dispose()` is a **no-op**: it returns at
  `Resources/Image.cs:155` before `HandleRegistry.TrackDispose` and before
  `vmaDestroyImage`, and it can never hand a swapchain-owned `VkImage` to VMA.
  `using var image = swap.GetImage(i);` is harmless and pointless. Say both.
- **It is never registered with `HandleRegistry`.** `TrackCreate` returns on its
  first branch for a non-owning handle (`Diagnostics/HandleRegistry.cs:67-68`), so
  calling this once per frame costs two predictable branches and cannot produce a
  false double-dispose report.
- **Lifetime.** Valid only while this swapchain is alive and un-recreated;
  `Recreate` replaces the images and may change the extent and format. Do not
  cache it across a `Recreate` — the same contract `ImageViews` carries.
- **When to use which.** `GetImageHandle` returns the raw `nint` that
  `ImageBarrier.Image` (`Recording/ImageBarrier.cs:33`) takes in its
  object-initializer form. `GetImage` is what you want for
  `ImageBarrier.Transition`, for `ImageBlitRegion.WholeImage` /
  `BufferImageCopy.WholeImage`, and for anything that reads the extent, format or
  usage — cross-reference that `Image.FromRaw` reports `0×0` /
  `VK_FORMAT_UNDEFINED` / `ImageUsage.None` on purpose (#119,
  `Resources/Image.cs:78-85`) and that this method exists to supply what the
  swapchain genuinely knows.

**Do not** remove or change `GetImageHandle`; five call sites use it and it is the
right shape for them (spec D11, E4).

**Do not** migrate the five existing `GetImageHandle` barrier sites
(`HelloCube:599`, `HelloTriangle:227`, `HelloVmaWindowed:482`,
`SwapchainTests:308`, `WindowedValidationTests:159`) to the new API. Spec D11
records the reason; a mechanical follow-up can do it with a clean diff.

---

## Step 2 — tests for `GetImage`

**File:** `tests/Ahjo.Vulkan.Tests/SwapchainTests.cs` — extend it, do not add a
new file. Every new `[Fact]` opens with the two gates that file already uses at
`:20-21`:

```csharp
TestGate.RequirePlatform(IsWindows, "Surface tests are Win32-only for now.");
TestGate.RequireDriver();
```

These run on the CI runner, so everything below is real CI coverage.

`GetImage_Reports_The_Swapchains_Own_Facts`
- for every `i` in `[0, swap.ImageCount)`: `(ulong)swap.GetImage(i).RawHandle == (ulong)swap.GetImageHandle(i)`;
- `Format == swap.Format`, `Width == swap.Extent.width`, `Height == swap.Extent.height`,
  `Usage == swap.ImageUsage`;
- `Depth == 1`, `MipLevels == 1`, `ArrayLayers == 1`;
- `IsNull == false`.

`GetImage_Returns_A_Borrowed_Handle_That_Dispose_Does_Not_Destroy`
- `OwnsHandle == false`, `OwnsMemory == false`;
- with `AhjoValidation.Enabled = true` for the duration (restore it in a
  `finally`), call `Dispose()` **twice** on the same value and assert no
  `AhjoValidationException` is thrown, then assert the swapchain still presents —
  or, more cheaply and just as conclusively, that
  `swap.GetImage(0).RawHandle` is unchanged afterwards. This is the direct
  regression test for the `HandleRegistry` question (spec E7): a borrowed handle
  is never tracked, so a second dispose is not a double-dispose.

`GetImage_Makes_WholeImage_Regions_Cover_The_Swapchain`
- create any small VMA `Image` as the blit source;
- `ImageBlitRegion.WholeImage(in source, in swap.GetImage(0))` has
  `DstOffset1.x == (int)swap.Extent.width`, `DstOffset1.y == (int)swap.Extent.height`,
  `DstOffset1.z == 1`;
- assert the same shape is **wrong** for `Image.FromRaw(swap.GetImageHandle(0))`
  (`DstOffset1 == (0,0,0)`), so the test states the defect it fixes rather than
  only the fix. Comment it as the E4 regression.

`GetImage_Tracks_Recreate`
- capture extent and handles, `swap.Recreate(...)` at a different extent (the file
  already has a recreate test at `:55-101` to copy the resize dance from), then
  assert `GetImage(0)` reports the new extent and a handle matching the new
  `GetImageHandle(0)`.

`GetImage_Rejects_An_Out_Of_Range_Index`
- `Assert.Throws<ArgumentOutOfRangeException>(() => swap.GetImage(swap.ImageCount))`.

**Not coverable here, and say so in a comment on the class or the first new test:**
that a blit *into* a swapchain image executes correctly and layer-clean end to
end. That needs a present loop on real hardware and is what `HelloDlaa`'s hardware
run (step 14) supplies; CI has no NVIDIA GPU (#32).

---

## Step 3 — sample project scaffolding

**New file `samples/HelloDlaa/HelloDlaa.csproj`.** Model on
`samples/HelloRayQuery/HelloRayQuery.csproj` (Slang, no glslc target) plus
`samples/HelloVmaWindowed/HelloVmaWindowed.csproj` (SDL3 + linked `SdlWindow.cs`).

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <RootNamespace>Ahjo.Vulkan.Samples.HelloDlaa</RootNamespace>
  <AssemblyName>HelloDlaa</AssemblyName>
  <IsPackable>false</IsPackable>
</PropertyGroup>
```

Project references: `..\..\src\Ahjo.Vulkan\Ahjo.Vulkan.csproj`,
`..\..\src\Ahjo.Vulkan.Slang\Ahjo.Vulkan.Slang.csproj`,
`..\..\src\Ahjo.Vulkan.Ngx\Ahjo.Vulkan.Ngx.csproj`,
`..\..\src\Ahjo.Vulkan.Utilities\Ahjo.Vulkan.Utilities.csproj`.
Package reference: `ppy.SDL3-CS`.
Linked compile: `<Compile Include="..\..\tests\Ahjo.Vulkan.Tests\SdlWindow.cs" Link="SdlWindow.cs" />`
with the same "#87 / #88" comment the other two samples carry.
Content: `<None Include="Shaders\cube.slang" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" />`.

**Feature-DLL staging, three ordered candidates, every one `Condition`ed on
`Exists`** — a missing DLL must never fail or warn the build (spec E1;
`TreatWarningsAsErrors=true`):

```xml
<PropertyGroup>
  <_DlaaWindows Condition="$([MSBuild]::IsOSPlatform('Windows'))">true</_DlaaWindows>
</PropertyGroup>

<!-- 1. Explicit override: dotnet run -p:NvidiaDlssDll=C:\path\nvngx_dlss.dll -->
<ItemGroup Condition="'$(NvidiaDlssDll)' != '' and Exists('$(NvidiaDlssDll)')">
  <None Include="$(NvidiaDlssDll)" CopyToOutputDirectory="PreserveNewest" Visible="false" />
</ItemGroup>

<!-- 2. Beside this csproj (git-ignored, see step 3c). -->
<ItemGroup Condition="'$(NvidiaDlssDll)' == '' and '$(_DlaaWindows)' == 'true' and Exists('$(MSBuildProjectDirectory)\nvngx_dlss.dll')">
  <None Include="nvngx_dlss.dll" CopyToOutputDirectory="PreserveNewest" Visible="false" />
</ItemGroup>
<ItemGroup Condition="'$(NvidiaDlssDll)' == '' and '$(_DlaaWindows)' != 'true'">
  <None Include="libnvidia-ngx-dlss.so*" CopyToOutputDirectory="PreserveNewest" Visible="false" />
</ItemGroup>
```

Candidate 3 is **not** an MSBuild item: it is the run-time
`NgxDescription.DlssSearchPaths` fallback to `native/ngx/staged/<rid>/rel`
(step 10c). Carry an XML comment in the csproj saying so, and saying that no
target here ever downloads a feature DLL — the same posture
`Ahjo.Vulkan.Ngx.Native.csproj` states for the SDK.

**3b. `Ahjo.Vulkan.slnx`** — add
`<Project Path="samples/HelloDlaa/HelloDlaa.csproj" />` after the `HelloRayQuery`
line.

**3c. `.gitignore`** — append next to the existing NGX block (currently ending at
the `!native/ngx/src/ahjo_ngx.map` negation):

```
# Consumer-supplied DLSS feature library dropped beside samples/HelloDlaa for a
# local run. Never committed; see docs/ngx-notes.md.
samples/HelloDlaa/nvngx_dlss.dll
samples/HelloDlaa/libnvidia-ngx-dlss.so*
```

**Check:** `dotnet build Ahjo.Vulkan.slnx -c Release -p:SkipKtxNativeBuild=true -p:SkipNgxNativeBuild=true`
succeeds with no NGX SDK staged and emits no warning about the sample. This is
the CI command verbatim (`.github/workflows/ci.yml:153`).

---

## Step 4 — `samples/HelloDlaa/Shaders/cube.slang`

Two entry points in one file, compiled at run time (spec E2).

```hlsl
struct FrameUniforms
{
    row_major float4x4 jitteredMvp;    // model * view * projJittered
    row_major float4x4 currentMvp;     // model * view * proj   (UNJITTERED)
    row_major float4x4 previousMvp;    // last frame's, unjittered
    row_major float4x4 model;
    float2             renderExtent;   // (renderWidth, renderHeight)
    float2             pad;
};

[[vk::binding(0, 0)]] ConstantBuffer<FrameUniforms> frame;
[[vk::binding(1, 0)]] Sampler2D<float4> albedo;     // combined image sampler

struct VsIn  { float3 position : POSITION; float2 uv : TEXCOORD0; float3 normal : NORMAL; };
struct VsOut
{
    float4 position : SV_Position;   // JITTERED clip position — this is what rasterizes
    float4 curClip  : TEXCOORD0;     // unjittered current clip position
    float4 prevClip : TEXCOORD1;     // unjittered previous clip position
    float2 uv       : TEXCOORD2;
    float3 normalWs : TEXCOORD3;
};
struct PsOut { float4 color : SV_Target0; float2 motion : SV_Target1; };

[shader("vertex")]   VsOut vertexMain(VsIn input);
[shader("fragment")] PsOut fragmentMain(VsOut input);
```

`vertexMain` computes all three clip positions with `mul(float4(p, 1.0), m)` —
row-vector, matching `System.Numerics`. `row_major` on the declarations is what
makes the raw `Matrix4x4` bytes land as rows; if the cube renders as garbage on
the first run, that pairing is the first suspect and the fallback is
`Matrix4x4.Transpose` on the CPU with plain `float4x4` and `mul(m, v)`. This
failure is immediately visible, so it cannot ship silently.

`fragmentMain` must do exactly three things, in this order, with a comment on
each citing the guide section (spec D2):

1. Shade: `albedo.Sample(input.uv)` times a simple N·L term with a fixed light
   direction, then **apply the sRGB transfer function** before writing
   `SV_Target0` — the colour attachment is `R8G8B8A8_UNORM` and DLSS is running
   in LDR mode (guide §3.1.2, spec D4). Use the exact piecewise sRGB encode, not
   `pow(c, 1/2.2)`, and say why in a comment.
2. Motion vectors, verbatim from spec D2 step 4:
   ```
   float2 curUV  = input.curClip.xy  / input.curClip.w  * 0.5 + 0.5;
   float2 prevUV = input.prevClip.xy / input.prevClip.w * 0.5 + 0.5;
   output.motion = (prevUV - curUV) * frame.renderExtent;
   ```
   with a comment stating: previous − current (guide §3.6), render-resolution
   pixels (§3.6.1), +X right / +Y down which the `M22`-flipped projection already
   gives, **and that there is deliberately no negation anywhere**.
3. Alpha 1.0 on the colour output.

---

## Step 5 — `samples/HelloDlaa/DlaaOptions.cs`

```csharp
internal enum DlaaMode { Dlaa, Quality, Off, Bilinear }

internal readonly record struct DlaaOptions
{
    public DlaaMode Mode        { get; init; }   // default Dlaa
    public ulong    MaxFrames   { get; init; }   // --frames, default ulong.MaxValue; 240 when --capture is set and --frames is not
    public string?  CapturePath { get; init; }   // --capture
    public bool     RequireDlss { get; init; }   // --require-dlss
    public bool     Validation  { get; init; }   // true unless --no-validation

    public bool UsesDlss   => Mode is DlaaMode.Dlaa or DlaaMode.Quality;
    public bool UsesJitter => UsesDlss;

    public static bool TryParse(string[] args, out DlaaOptions options, out string? error);
}
```

`TryParse` rejects an unknown flag or an unknown `--mode` value with a message
naming the accepted set; `Program` prints it and returns exit code 2. No
third-party parser, no reflection.

---

## Step 6 — `samples/HelloDlaa/JitterSequence.cs`

```csharp
internal sealed class JitterSequence
{
    public JitterSequence(uint renderWidth, uint renderHeight, uint outputWidth, uint outputHeight);

    public int     PhaseCount { get; }          // guide §3.7.1.1
    public Vector2 Current    { get; }          // render pixels, each in [-0.5, +0.5]
    public void    Advance();                   // ++index, wraps at PhaseCount

    public static Matrix4x4 ApplyJitter(in Matrix4x4 viewProjection, Vector2 jitterPixels,
                                        uint renderWidth, uint renderHeight);
    internal static float Halton(int index, int radix);   // index is 1-based
}
```

- The table is built once in the constructor: `Halton(i + 1, 2) - 0.5f`,
  `Halton(i + 1, 3) - 0.5f` for `i` in `[0, PhaseCount)`. Allocation happens here,
  at setup, never in the loop.
- `PhaseCount = max(8, (int)ceil(8 * (outputWidth / (double)renderWidth) ^ 2))`
  (guide §3.7.1.1: 8 at DLAA, 18 at Quality's 1.5× ratio).
- `ApplyJitter` is spec D2 step 2 **verbatim** — build `Matrix4x4.Identity`, set
  `M41 = 2f * j.X / renderWidth` and `M42 = 2f * j.Y / renderHeight`, return
  `viewProjection * t`. Carry the doc comment explaining why this form and not the
  guide's `M[2][0] +=`: `Matrix4x4.CreatePerspectiveFieldOfView` is right-handed
  with `M34 = -1`, so `clip.w = -z` and the guide's edit shifts NDC by *minus* the
  amount added; post-multiplying by a clip-space translation has no dependence on
  the sign of `w`.
- `Current` is what goes into `DlssEvaluateInputs.JitterOffsetX/Y` unchanged.

---

## Step 7 — `samples/HelloDlaa/CubePipeline.cs`

Same shape as `samples/HelloRayQuery/RayQueryPipeline.cs`: an `IDisposable` that
owns the compiler, session, program, both shader modules, the descriptor-set
layout, the pipeline layout and the pipeline, with a `public bool Failed` the
caller turns into exit code 2.

```csharp
internal sealed class CubePipeline : IDisposable
{
    public CubePipeline(Device device, string shaderPath, VkFormat colorFormat,
                        VkFormat motionFormat, VkFormat depthFormat);
    public bool Failed { get; }
    public ref readonly GraphicsPipeline Pipeline { get; }
    public ref readonly PipelineLayout   Layout   { get; }
}
```

- `SlangCompiler.Create()` → `CreateSession(new SlangSessionDescription())` (no
  capability needed) → `Compile(new SlangCompileRequest { Path = shaderPath, EntryPoints = ["vertexMain", "fragmentMain"] })`.
  Print `Warnings` if non-empty rather than swallowing, as `RayQueryPipeline.cs:63-68` does.
- `device.CreateShaderModule(program.Spirv(0))` for the vertex stage,
  `Spirv(1)` for the fragment stage — the index order is the `EntryPoints` order
  (`SlangProgram.cs:150-156`). Do **not** call `WithEntryPoint`: Slang emits each
  entry point as `main` (`RayQueryPipeline.cs:104-108`).
- Descriptor set layout with `PushDescriptor = true` and two bindings:
  slot 0 `VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER` (`ShaderStages.Vertex | ShaderStages.Fragment`),
  slot 1 `VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER` (`ShaderStages.Fragment`).
- Vertex input: binding 0, stride `sizeof(CubeVertex)`; attributes
  `0 = R32G32B32_SFLOAT position`, `1 = R32G32_SFLOAT uv`,
  `2 = R32G32B32_SFLOAT normal`.
- `WithDynamicRendering([colorFormat, motionFormat], depthFormat)` — **two**
  colour formats, in attachment order.
- `WithColorBlend` with two `ColorBlendAttachment.Opaque` entries: the builder
  rejects a non-empty attachment span whose length does not match the declared
  colour-attachment count (`Pipelines/ColorBlendDescription.cs:5-11`). Omitting
  `WithColorBlend` entirely is also legal; do one or the other, not a
  one-element span.
- `WithDepthStencil(testEnable: true, writeEnable: true, VkCompareOp.VK_COMPARE_OP_LESS)`
  — standard depth, near 0 / far 1, matching `DepthInverted` staying clear.
- `WithRasterization` with back-face culling; the cube is closed.

---

## Step 8 — `samples/HelloDlaa/CubeScene.cs`

Owns everything that does not depend on the render extent, plus the one thing
that does (the sampler).

```csharp
internal sealed class CubeScene : IDisposable
{
    public CubeScene(Device device, uint queueFamily, uint framesInFlight);

    public ref readonly Buffer VertexBuffer { get; }
    public ref readonly Buffer IndexBuffer  { get; }
    public uint IndexCount { get; }

    public ref readonly ImageView TextureView { get; }
    public Sampler Sampler { get; }                       // current mip-biased sampler
    public void    SetMipLodBias(float bias);             // recreates the sampler; setup-time only

    public Buffer Uniforms(uint slot);                    // persistent-mapped, one per slot
    public void   WriteUniforms(uint slot, in FrameUniforms values);   // no allocation

    [StructLayout(LayoutKind.Sequential)]
    internal struct FrameUniforms
    {
        public Matrix4x4 JitteredMvp;
        public Matrix4x4 CurrentMvp;
        public Matrix4x4 PreviousMvp;
        public Matrix4x4 Model;
        public Vector2   RenderExtent;
        public Vector2   Pad;
    }
}
```

- **Geometry.** 24 vertices (four per face, so UVs and normals are per-face), 36
  indices, `VkIndexType.VK_INDEX_TYPE_UINT16`. Uploaded once with `StagingBatch`
  + `Flush(queue, pool)` — `samples/HelloVmaWindowed/Program.cs:118-140` is the
  pattern.
- **Texture.** 512×512 `VK_FORMAT_R8G8B8A8_UNORM`, `MipLevels = 10`, usage
  `Sampled | TransferSrc | TransferDst` (`TransferSrc` is required by
  `GenerateMips`). Generate the pattern into a `byte[]` at setup: a checkerboard
  whose cell size is 4 texels over one half and 32 over the other, plus
  single-texel grid lines every 64 texels in a contrasting colour. Values are
  written **already sRGB-encoded**, because the shader's encode (step 4) applies
  to the shaded result, not to the texture fetch — pick the constants so the
  pattern is high-contrast. Upload + `GenerateMips(finalLayout: SHADER_READ_ONLY_OPTIMAL)`;
  copy the recipe at `samples/HelloCube/Program.cs:510-560`.
- **Sampler.** `SamplerDescription { MipLodBias = bias, MaxAnisotropy = 1, … }`
  with linear min/mag/mip and repeat addressing. `SetMipLodBias` disposes the old
  sampler and creates a new one; it is only ever called from setup and from the
  resize path, never per frame.
- **Uniform ring.** `framesInFlight` host-visible buffers,
  `MemoryUsage.AutoPreferHost` with
  `AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped`, exactly
  `samples/HelloVmaWindowed/Program.cs:178-193`. `WriteUniforms` is
  `Uniforms(slot).AsSpan<FrameUniforms>()[0] = values;` followed by `Flush()`.

---

## Step 9 — `samples/HelloDlaa/FrameTargets.cs`

The per-slot DLSS-facing images and the barrier recipe (spec D5, D7). One
instance per frame slot; the owner is a small array in `Program`.

```csharp
internal sealed class FrameTargets : IDisposable
{
    public static FrameTargets Create(Device device, uint renderWidth, uint renderHeight,
                                      uint outputWidth, uint outputHeight);

    public ref readonly Image     Color            { get; }
    public ref readonly ImageView ColorView        { get; }   // attachment view
    public ref readonly ImageView MotionView       { get; }
    public ref readonly ImageView DepthView        { get; }
    public ref readonly Image     Presentation     { get; }

    public NgxImage NgxColor         { get; }
    public NgxImage NgxDepth         { get; }
    public NgxImage NgxMotionVectors { get; }
    public NgxImage NgxOutput        { get; }

    public void RecordPreRasterBarriers(ref CommandRecorder recorder);
    public void RecordPreEvaluateBarriers(ref CommandRecorder recorder);
    public void RecordPreBlitBarriers(ref CommandRecorder recorder);
    public void Dispose();
}
```

**Formats and usages — copy `tests/Ahjo.Vulkan.Ngx.Tests/DlssHardwareTests.cs:317-350`
and add what the sample needs on top:**

| Target | Format | Usage | Extent |
|---|---|---|---|
| Colour | `R8G8B8A8_UNORM` | `Sampled \| ColorAttachment \| TransferSrc` | render |
| Motion vectors | `R16G16_SFLOAT` | `Sampled \| ColorAttachment` | render |
| Depth | `D32_SFLOAT` | `Sampled \| DepthStencilAttachment` | render |
| Presentation | `R8G8B8A8_UNORM` | `Storage \| TransferSrc \| TransferDst` | output |

`TransferSrc` on colour is for the `off`/`bilinear` blit; `TransferDst` on the
presentation image is **not optional** — DLSS clears it itself with
`vkCmdClearColorImage` and the validation layer is what says so
(`VUID-vkCmdClearColorImage-image-00002`, driver 610.47, #218 D3). Put that
sentence in the code as a comment.

Every `CreateImage` uses
`new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice, Flags = AllocationFlags.DedicatedMemory }`
with a comment citing #214: full-screen targets that live for the session are
what `DedicatedMemory` is for.

**Views.** Attachment views come from `Image.CreateView`. The `NgxImage`s come
from `NgxImage.CreateView(device, in image, in viewDescription)` — the documented
default (`NgxImage.cs:117-122`); the sample deliberately creates its own views
rather than using `NgxImage.Wrap`, and says so in a comment, because `Wrap`'s
contract ("the description must be the one that created the view") is the one
thing the wrapper cannot verify. Depth uses
`Aspect = VK_IMAGE_ASPECT_DEPTH_BIT`; the rest use `COLOR`.

**Barriers.** Three methods, matching spec D7 steps 1, 5 and 7. The pre-evaluate
one is the load-bearing one and must reproduce `DlssHardwareTests.cs:372-400`,
including the output transition's destination scope of
`Stage.ComputeShader | Stage.AllTransfer` / `Access.ShaderWrite | Access.TransferWrite`
and the comment explaining that DLSS's own clear is a transfer-stage write.
Batch each set into one `Span<ImageBarrier>` + one `PipelineBarrier` call;
`stackalloc`/collection-expression spans only — no `T[]` (spec E12).

**Dispose order:** the four `NgxImage`s first (they own their views), then the
attachment views, then the images.

---

## Step 10 — `samples/HelloDlaa/Program.cs`

### 10a. Start-up, in this order (the order is the contract — `NgxSupport.cs:44-46`)

1. `DlaaOptions.TryParse`; on failure print and return 2.
2. `AhjoValidation.Enabled = options.Validation;` **before** any handle is created
   — the double-dispose registry only tracks handles created while it is on
   (`Diagnostics/AhjoValidation.cs:49-52`). Spec E13: this costs the loop nothing.
3. Build the `NgxDescription`:
   ```csharp
   new NgxDescription
   {
       ProjectId       = "5f3b2c41-9a6d-4b18-8e77-2c0d5a9b7e14",  // this sample's own, fixed
       EngineVersion   = "0.1.0-hellodlaa",
       // ApplicationDataPath deliberately unset: the wrapper materializes the
       // temp path, and a null reaching NGX access-violates (NgxDescription.cs:36-47).
       DlssSearchPaths = BuildSearchPaths(),
   }
   ```
4. `SdlWindow` (`"Ahjo.Vulkan — HelloDlaa (Esc to quit)"`, 1600×900, resizable).
5. `NgxSupport.TryGetInstanceExtensions(in description, out NgxExtensionSet? ngxInstanceExts)`.
   Wrap this call in `try { … } catch (DllNotFoundException) { … }` — on a clone
   with no NGX SDK staged, `ahjo_ngx` does not exist and the P/Invoke throws.
   That is the clean-skip path: print one line naming `./tools/setup-ngx.ps1` and
   return 0 (or 5 under `--require-dlss`).
6. Concatenate SDL's required instance extensions
   (`SdlWindow.GetRequiredVulkanInstanceExtensions()`) with
   `ngxInstanceExts.Names` into one `Utf8Name[]` built at setup, and create the
   `Instance` with `EnableValidation = options.Validation` and a **static**
   `DebugCallback` tallying into static counters
   (`HelloVmaWindowed/Program.cs:454-467` — static so the closure is captureless).
   Dispose `ngxInstanceExts` after `Instance.Create` returns.
7. Surface, then pick a physical device that supports graphics + present (copy
   `HelloVmaWindowed/Program.cs:CreatePresentDevice`).
8. `NgxSupport.TryGetDeviceExtensions(gpu, in description, out NgxExtensionSet? ngxDeviceExts)`.
9. Device extensions = `VulkanExtensions.KhrSwapchain` + `ngxDeviceExts.Names`
   + `VulkanExtensions.ExtMemoryBudget` **only when
   `gpu.SupportsExtension(VulkanExtensions.ExtMemoryBudget)`**; set
   `DeviceDescription.Allocator = new AllocatorDescription { EnableMemoryBudget = true }`
   under the same condition. Both, or neither — the wrapper fails the pairing
   check when the flag is set without the extension (#218 D11).
   Dispose `ngxDeviceExts` after `CreateDevice`.
10. `Swapchain` with
    `PreferredFormats = [B8G8R8A8_UNORM, R8G8B8A8_UNORM]` and
    `ImageUsage = ColorAttachment | TransferDst`. If `swap.Format` is an `_SRGB`
    format, print a warning line saying the presented image will be over-bright
    because the blit encodes a second time (spec D4).
11. `NgxContext.Create(device, in description)` when `options.UsesDlss`, inside
    `try/catch (NgxFeatureLibraryNotFoundException ex)` and
    `catch (NgxDriverTooOldException ex)`: print `ex.Message` **verbatim** — it
    already names the file and every searched directory — then return 0, or 5
    under `--require-dlss`. Do not reformat or summarize the message; it is the
    diagnosis the wrapper exists to produce.
12. `ngx.IsSuperSamplingAvailable` false → same skip path.

### 10b. Render-extent selection and the resources that depend on it

One method, called at start-up and again on every resize:

```csharp
private static void RebuildForExtent(/* device, ngx, options, swap extent, … */)
```

1. Output extent = `swap.Extent`.
2. Render extent: output extent for `dlaa`/`off`; for `quality`/`bilinear`,
   `ngx.GetOptimalSettings(outW, outH, DlssQualityMode.MaxQuality)` — and when
   `IsAvailable` is false, print that the mode is not offered at this resolution
   and fall back to `dlaa` (or return 5 under `--require-dlss`).
   In `bilinear` the settings are queried purely to pick a matching extent, so
   the control renders the same number of pixels as `quality`.
3. `JitterSequence` rebuilt for the new extents.
4. `scene.SetMipLodBias(options.UsesDlss ? MathF.Log2(renderW / (float)outW) - 1f : 0f)`
   — guide §3.5, unmodified, for every DLSS mode including DLAA where it is −1.0
   (spec D6, OPEN-1 resolved). The computed value is printed in step 8 below.
5. Dispose and recreate both `FrameTargets`.
6. `dlss?.Dispose()`, then when `options.UsesDlss` recreate through
   `queue.ImmediateSubmit(pool, (ref CommandRecorder r) => feature = ngx.CreateDlss(ref r, description))`
   — `ImmediateSubmit` submits **and waits**, which is what `CreateDlss` requires
   before the first `Evaluate` (`NgxContext.cs:336`, `DlssHardwareTests.cs:97-108`).
   The `DlssFeatureDescription` is spec D2's table:
   ```csharp
   new DlssFeatureDescription
   {
       RenderWidth  = renderW, RenderHeight = renderH,
       OutputWidth  = outW,    OutputHeight = outH,
       Mode         = options.Mode == DlaaMode.Quality ? DlssQualityMode.MaxQuality : DlssQualityMode.Dlaa,
       Flags        = DlssFeatureFlags.MotionVectorsLowRes | DlssFeatureFlags.AutoExposure,
   }
   ```
   `MotionVectorsJittered`, `DepthInverted` and `Hdr` are **not** set; write a
   comment on that line saying why for each (spec D2 step 5).
7. Set `resetNextFrame = true`.
8. Print one line: mode, render extent, output extent, phase count, mip bias.

### 10c. `BuildSearchPaths()`

Port `tests/Ahjo.Vulkan.Ngx.Tests/NgxTestEnvironment.cs:215-229` into the sample
(do not reference the test project): walk parents of `AppContext.BaseDirectory`
until one contains `native/ngx`, then return
`[native/ngx/staged/{win-x64|linux-x64}/rel]` if that directory exists, otherwise
`[]`. Comment that this is a developer-machine convenience for *this repository*
and is **not** the deployment model — a shipped application puts the DLL beside
its executable (`native/ngx/README.md:31-34`).

### 10d. The frame loop — allocation-free (spec E12)

Structure: `HelloVmaWindowed/Program.cs:236-420` (pump, resize check + recreate,
`ring.BeginFrame`, acquire, record, `fc.Submit`, `swap.Present`), with the record
body replaced by spec D7 steps 1–9.

Rules the implementer must hold, in order of how easy they are to violate:

- **No `T[]` inside the loop.** `Buffer[] vertexBuffers = [vertexBuffer];`
  (`HelloVmaWindowed/Program.cs:366`) is exactly the line not to copy — hoist the
  one-element `Buffer[]` to a setup local, or pass a `stackalloc`-backed
  `ReadOnlySpan<Buffer>`. `BindVertexBuffers` takes `scoped ReadOnlySpan<Buffer>`
  (`Recording/CommandRecorder.cs:367-370`).
- No lambdas, no LINQ, no `string` interpolation, no boxed enums in the loop.
  Per-frame `Console.Write` of any kind is out; status printing happens at
  start-up and on recreation only.
- `NgxLoggingLevel` stays `Off` — turning it on allocates per callback by design
  (`src/Ahjo.Vulkan.Ngx/CLAUDE.md`).
- `Image swapImage = swap.GetImage(imageIndex);` once per frame, reused by both
  swapchain barriers and the blit region. `GetImage` allocates nothing and is not
  tracked by the handle registry (spec D11) — but it is still one call, not four.
- Matrices: keep `previousUnjitteredMvp` in a loop-local and update it at the end
  of the frame. On the first frame after a rebuild, set it equal to the current
  one so the first frame's motion vectors are zero rather than garbage — and pair
  that with `Reset = true`.

The evaluate call:

```csharp
dlss.Evaluate(ref rec, new DlssEvaluateInputs
{
    Color = targets.NgxColor, Depth = targets.NgxDepth,
    MotionVectors = targets.NgxMotionVectors, Output = targets.NgxOutput,
    JitterOffsetX = jitter.Current.X, JitterOffsetY = jitter.Current.Y,
    RenderWidth = renderW, RenderHeight = renderH,
    Reset = resetNextFrame,
    // MotionVectorScaleX/Y stay at their 1f defaults: the vectors are already
    // in render pixels and already point at the previous frame (guide §3.6.3).
});
resetNextFrame = false;
```

Immediately after it, a comment: *no draw or dispatch follows, so there is
nothing to rebind — but `EvaluateFeature_C` clobbers the bound pipeline,
descriptor sets and dynamic state (guide §5.2.5), and a renderer that draws UI
after DLSS must rebind everything.*

The swapchain barriers use `ImageBarrier.Transition(in swapImage, …)`, and the
blit uses
`ImageBlitRegion.WholeImage(in targets.Presentation, in swapImage)` with
`VkFilter.VK_FILTER_NEAREST`. Comment that this is correct only because
`Swapchain.GetImage` carries the real extent — `Image.FromRaw` reports `0×0`
(`Resources/Image.cs:84-85`) and `WholeImage` over it would produce a degenerate
destination box that blits nothing (step 1, spec E4).

### 10e. `--capture`

On the frame where `frame + 1 == options.MaxFrames` and `CapturePath` is set:
after the presentation image has been written but before the swapchain blit, add
a `GENERAL → TRANSFER_SRC_OPTIMAL` barrier and `CopyImageToBuffer` into a
host-visible readback buffer created at setup
(`AllocationFlags.HostAccessRandom | AllocationFlags.Mapped`), using
`BufferImageCopy.WholeImage(in targets.Presentation)`. After the loop,
`device.WaitIdle()` then
`PngWriter.Write(options.CapturePath, readback.AsReadOnlySpan<byte>(), (int)outW, (int)outH)`.
`samples/HeadlessExport/Program.cs:145-185` is the recipe. The presentation image
is `R8G8B8A8_UNORM`, which is the byte order `PngWriter` expects.

### 10f. VRAM reporting (spec E11)

When the allocator was created with `EnableMemoryBudget`, print
`Allocator.GetHeapBudgets` (a `stackalloc MemoryHeapBudget[16]`) once before
`CreateDlss` and once after, plus `ngx.TryGetStats(out DlssStats stats)`. Label
the output so the point lands: VMA's `AllocationBytes` is unchanged while `Usage`
on the device-local heap has grown by roughly `stats.VramAllocatedBytes`, because
DLSS's history and scratch surfaces are allocated inside the driver where VMA
cannot see them. Also print `stats.OptLevel` and `stats.IsDevSnippetBranch` and
warn when they are not `40` / `false` — that pair is the deployed-a-`dev`-build
detector (#218 OPEN-3, `DlssHardwareTests.cs:168-174`).

### 10g. Exit codes

| Code | Meaning |
|---|---|
| 0 | ran to completion, or skipped cleanly (no shim / no DLSS / no feature DLL) without `--require-dlss` |
| 2 | bad command line, or the shader failed to compile / was missing |
| 3 | the validation layer reported at least one error |
| 5 | `--require-dlss` was passed and DLSS was unavailable |

Print the validation tally at the end unconditionally, as
`HelloVmaWindowed/Program.cs:431-434` does.

---

## Step 11 — `docs/ngx-notes.md` (new)

Write the eight sections of spec D12, in that order. Content requirements:

- **§2** must show the actual MSBuild item and the actual `DlssSearchPaths`
  usage, and must say `lib/<plat>/rel/` and **never** `dev/` with the reason
  (on-screen watermark).
- **§3** lists the obligations as the *application's*: notify NVIDIA before
  commercial release, RTX UI branding guidelines, reproduce the third-party
  notices from guide §9.5, never redistribute `dev/`. One sentence stating that
  `Ahjo.Vulkan.Ngx.Native` ships only the `ahjo_ngx` shim, with `NGX-LICENSE.txt`
  beside it, and no feature DLL (#214).
- **§4** is the renderer contract. Reproduce spec D2 — the jitter construction
  including the right-handed-projection sign warning, the motion-vector formula,
  the flag table. Then document the two general affordances of guide §3.6.3
  (`MotionVectorScaleX/Y` for vectors that are UV-space, or that point along the
  direction of motion) on their own merits: what they let a renderer avoid
  changing, and the cost — a convention the shader no longer shows. Do **not**
  motivate them by naming any particular engine. State the mip-bias formula,
  `Reset` on cuts and loads, `AutoExposure` versus an exposure image, and the
  layout contract with `Storage | TransferDst` on the output. End with
  "RenderDoc does not work with DLSS applications (guide §8.6) — take your own
  frame dump; `HelloDlaa --capture` shows how."
- Each hardware-found trap is labelled as such with the driver version
  (RTX 4070 Ti, driver 610.47): the `TransferDst`/`vkCmdClearColorImage`
  requirement, the null `ApplicationDataPath` access violation, and the two
  extension-list failure modes.
- **§5** shows the `AllocatorDescription.EnableMemoryBudget` +
  `VulkanExtensions.ExtMemoryBudget` pairing, `Allocator.GetHeapBudgets`, and
  `AllocationFlags.DedicatedMemory` on the DLSS-facing targets.
- **§6** explains the application-owned presentation image and the blit, points at
  `Swapchain.GetImage`, and states the two conditions a DLSS-direct-to-swapchain
  path needs (a surface advertising `STORAGE`, a non-sRGB swapchain format) as the
  consumer's own portability judgement (spec D10).
- **§7** documents the four modes, the DLL resolution order of step 3, and states
  plainly that CI builds this sample and never runs it, because there is no
  NVIDIA hardware in CI (#32).

---

## Step 12 — README and package-README links

- `README.md`, in the doc-links block at `:122-124`, add:
  `DLSS / DLAA consumer contract: docs/ngx-notes.md`.
- `README.md:148` (the `samples/` paragraph): add `HelloDlaa` to the windowed
  list and one sentence — a jittered spinning cube through DLAA or DLSS Quality
  with motion vectors and mip bias; needs an NVIDIA GPU and a consumer-supplied
  `nvngx_dlss.dll`, so CI builds it and never runs it.
- `src/Ahjo.Vulkan.Ngx/README.md`: one line under "This package does not contain
  DLSS" pointing at `docs/ngx-notes.md` for the full contract and at
  `samples/HelloDlaa` for a worked call site.

---

## Step 13 — build, tests, CI parity, review routing

```bash
dotnet build Ahjo.Vulkan.slnx -c Release --nologo -p:SkipKtxNativeBuild=true -p:SkipNgxNativeBuild=true
dotnet test tests/Ahjo.Vulkan.Tests/Ahjo.Vulkan.Tests.csproj -c Release --no-build
```

The build must be clean **with `native/ngx/staged/` temporarily renamed away**,
proving the CI path. Restore it afterwards and rebuild so the shim and the
sample's DLL copy land in `samples/HelloDlaa/bin/`.

The wrapper suite must show the step 2 cases running (not skipped) on this
machine; on the CI runner they run under the same two gates every other
`SwapchainTests` case uses.

**Review routing.** The diff now touches the core wrapper surface
(`Rendering/Swapchain.cs`), so `vulkan-validation-reviewer` applies to the PR and
should be run explicitly — the questions to put to it are the borrowed-handle
semantics of `GetImage` and the swapchain barrier/blit sequence of step 10d
(`UNDEFINED → TRANSFER_DST_OPTIMAL → PRESENT_SRC_KHR`), which is the repo's first
blit-to-swapchain.

No benchmark is added and no `bench-coverage-checker` mapping row changes:
`Swapchain.GetImage` is a setup/per-frame-once accessor that allocates nothing and
is not on a `Recording/`, `Sync/`, `Pools/` or `Memory/` hot path, and nothing
under `src/Ahjo.Vulkan.Ngx` changes, so `DlssEvaluateBenchmarks` still pins the
DLSS hot path. The sample's own loop is held to the zero-allocation rule by
inspection against spec E12 (step 10d) — say so in the PR description rather than
leaving a reviewer to wonder.

---

## Step 14 — local hardware verification (RTX 4070 Ti, driver 610.47)

CI cannot do any of this. Run all of it before the PR is updated, with
`VK_LAYER_KHRONOS_validation` installed; validation is on by default and **any
layer error fails the run with exit code 3** — that is the oracle that found the
`TransferDst` bug in Phase 2 (#218 D4).

**14a. DLAA, interactive.**
`dotnet run --project samples/HelloDlaa -c Release -- --mode dlaa --require-dlss`

Expect on screen: a slowly spinning textured cube, sharp, with the fine-checker
half of the texture **stable** — not crawling. Expect on stdout: one mode line
(`dlaa`, render == output, 8 phases, bias −1.00), the VRAM lines of step 10f with
a non-zero DLSS figure and `OptLevel 40`, and `Validation: 0 error(s)` at exit.

What each failure mode means, in the order to check it:
- *Fine detail shimmers and never resolves while the cube is stationary* — the
  jitter sign (spec OPEN-3). Flip the sign of `t.M41`/`t.M42` in
  `JitterSequence.ApplyJitter` **only** as a diagnosis; if that fixes it, spec D2
  step 2's derivation is wrong and must be corrected there and in
  `docs/ngx-notes.md`, not patched quietly in the sample.
- *Trails or smearing behind the moving silhouette* — motion vectors: direction
  (`prevUV - curUV`), space (multiply by render extent) or the
  `MotionVectorsLowRes` flag.
- *Ghosting only on the first frames* — `Reset` is not being set after feature
  creation.
- *Washed-out colours* — the swapchain fell back to an `_SRGB` format (the
  warning line of step 10a.10 will have printed).
- *A black window with no validation errors* — the swapchain blit did nothing.
  Check that step 10d used `swap.GetImage(...)` and not
  `Image.FromRaw(swap.GetImageHandle(...))`.

**14b. Mip bias at DLAA — record the answer (spec D6 / OPEN-1).**
In the same run, look specifically at the fine-cell half of the texture on faces
angled away from the camera. Write down whether −1.00 produces moiré or crawl
there. This observation is a deliverable: it goes into `docs/ngx-notes.md` §4 and
into the PR description. If it does moiré, **stop and report** — the agreed
follow-up is a `--mip-bias <float>` override with the formula as the default, not
a different formula.

**14c. Upscaling is real.**
`dotnet run --project samples/HelloDlaa -c Release -- --mode quality --require-dlss`

The mode line must show a render extent strictly smaller than the output extent
(≈1.5× linear at MaxQuality) and 18 phases. The window is still full size and the
cube's texture detail must be visibly better than 14d's.

**14d. The control.**
`--mode bilinear --frames 240 --capture bilinear.png` and
`--mode quality --frames 240 --capture quality.png`.

Both PNGs are output-resolution. `quality.png` must show reconstructed edges and
texture detail where `bilinear.png` is soft. Both are the same underlying render
resolution — that is what makes the comparison honest. Attach both to the PR.

**14e. Native baseline.** `--mode off --frames 240 --capture native.png` — the
reference for what DLAA is being judged against.

**14f. The missing-DLL diagnosis.** Rename
`native/ngx/staged/win-x64/rel/nvngx_dlss.dll` away, ensure no copy sits beside
the binary, and run `--mode dlaa`. Expect a single
`NgxFeatureLibraryNotFoundException` message naming `nvngx_dlss.dll` and every
searched directory, and exit code 0. Repeat with `--require-dlss` and expect
exit 5. Restore the file.

**14g. Resize.** Drag the window edge in both `dlaa` and `quality`. Expect one
recreation line per resize, no validation errors, and no smear on the frames
after the recreation (that is `Reset` working, spec OPEN-4).

**14h. Record the outcome.** Whatever 14a's jitter behaviour and 14b's bias
behaviour turn out to be, write one sentence about each into
`docs/ngx-notes.md` §4 — measured on this GPU and driver. Spec OPEN-3 exists
because the jitter sentence cannot be written before the run.

---

## Deliberately open — stop and ask rather than improvise

- **OPEN: the jitter sign.** Step 14a's first failure mode. If the derivation is
  wrong, the fix belongs in the spec and in `docs/ngx-notes.md`, not only in the
  code. (Spec OPEN-3 — the only OPEN still unresolved.)
- **OPEN: mip bias at DLAA.** Resolved as "ship the formula" (spec D6), but step
  14b's observation can reopen it. If −1.00 moirés, report rather than changing
  the formula.
- **OPEN: matrix layout.** Step 4 prescribes `row_major float4x4` + `mul(v, m)`.
  If the first run draws garbage, the fallback is a CPU `Matrix4x4.Transpose`
  with plain `float4x4` and `mul(m, v)` — report which one was needed so the spec
  and any future Slang sample record the answer.
- **Not open, and not to be widened:** `Swapchain.GetImage` is one method on one
  file (step 1). Do not add an `Image.FromRaw` metadata overload, do not add a
  `SwapchainImage` type, do not touch `Image`, `HandleRegistry` or
  `ImageBlitRegion`, and do not migrate the five existing `GetImageHandle` barrier
  sites. Spec D11 rejects each of those explicitly.
