Paired with [../specs/2026-09-03-issue-218-ngx-wrapper-design.md](../specs/2026-09-03-issue-218-ngx-wrapper-design.md).

# Implementation plan — issue #218, `Ahjo.Vulkan.Ngx`

Eighteen steps. Steps 1–2 touch `src/Ahjo.Vulkan` and are the only ones that do;
steps 3–13 build the new package; 14–15 are tests and benchmarks; 16–18 are CI,
docs and verification.

**Branch:** this lands on the existing `issue-216-ngx-native` branch and ships in
PR #217 — no new branch, no new PR (#218's own instruction).

**Two approval gates before you start.** Steps 1 and 2 are gated on **OPEN-2** and
**OPEN-1** in the spec. If either was declined at the approval gate, take the
fallback named in the step and adjust the dependent steps as indicated. If you reach
either step without knowing the answer, **stop and ask**.

**Local hardware is required to finish.** Step 15 and part of step 14 only execute on
the RTX 4070 Ti with `nvngx_dlss.dll` present. CI cannot run them; the PR must quote
the local run, the way #217 quoted `nm -D` and `dumpbin`.

**Linux:** step 16's build matrix already exists from Phase 1 and this phase adds no
native code, so no WSL rebuild is needed — but do re-run
`dotnet build Ahjo.Vulkan.slnx` on WSL once at step 18, because the new project joins
the solution.

---

## 1. `src/Ahjo.Vulkan/Lifecycle/PhysicalDevice.cs` — make `Instance` public

**Gated on OPEN-2.** If declined, skip this step and apply the fallback noted in
steps 8 and 9.

At `PhysicalDevice.cs:25`, change

```csharp
    internal readonly Instance            Instance;
```

to `public readonly Instance Instance;` and add a doc comment: the instance this
physical device was enumerated from; needed by satellite packages
(`Ahjo.Vulkan.Ngx`) that must hand `VkInstance` + `VkPhysicalDevice` + `VkDevice` to
a third-party API, and reachable today only through `Instance.RawHandle`.

**Verify:** `dotnet build src/Ahjo.Vulkan -c Release` is clean, and
`grep -rn "physicalDevice.Instance" src/` still resolves at every existing site.

## 2. `src/Ahjo.Vulkan` — `AllocatorDescription`, the memory-budget flag, and the query that reads it

**Gated on OPEN-1** for sub-step 2e only. If declined, do 2a–2d and skip 2e; drop the
two budget tests in step 14 and the `GetHeapBudgets` mention from step 17's docs.

### 2a. `Memory/AllocatorDescription.cs` (new)

```csharp
public readonly record struct AllocatorDescription
{
    public bool EnableMemoryBudget { get; init; }
}
```

Doc comment: allocator-level options, distinct from the per-allocation
`AllocationDescription`. `EnableMemoryBudget` sets
`VMA_ALLOCATOR_CREATE_EXT_MEMORY_BUDGET_BIT` **and requires the caller to also list
`VulkanExtensions.ExtMemoryBudget` in `DeviceDescription.Extensions`** — VMA's other
prerequisite, `VK_KHR_get_physical_device_properties2`, is core from Vulkan 1.1 and
the wrapper requires 1.3+. Why you would want it: DLSS allocates its history and
scratch inside the driver, invisible to VMA, so `GetHeapBudgets` under-reports
without it (#214). Default is `false`; this is not a default change.

### 2b. `Memory/Allocator.cs` — the `Create` overload

Keep `public static Allocator Create(Device device)` (`Allocator.cs:57`) as
`=> Create(device, default);` and add

```csharp
public static Allocator Create(Device device, in AllocatorDescription description)
```

with the existing body, changing only line 111 to OR in the budget bit when
`description.EnableMemoryBudget`:

```csharp
ci.flags = (uint)VmaAllocatorCreateFlagBits.VMA_ALLOCATOR_CREATE_BUFFER_DEVICE_ADDRESS_BIT
         | (description.EnableMemoryBudget
              ? (uint)VmaAllocatorCreateFlagBits.VMA_ALLOCATOR_CREATE_EXT_MEMORY_BUDGET_BIT
              : 0u);
```

Leave the 1.2 api-version clamp and its comment (`Allocator.cs:88-102`) untouched.

### 2c. `Lifecycle/DeviceDescription.cs` — carry it

Add, after `Extensions`:

```csharp
    /// <summary>Allocator options applied when <see cref="Device.Allocator"/> is
    /// first created. Default = <c>default</c>, which is byte-identical to the
    /// pre-#218 behaviour.</summary>
    public AllocatorDescription Allocator;
```

### 2d. `Lifecycle/PhysicalDevice.cs` + `Lifecycle/Device.cs` — plumb and validate

- `PhysicalDevice.CreateDevice`, at the `new Device(...)` call
  (`PhysicalDevice.cs:642`), passes `desc.Allocator` as a new final constructor
  parameter.
- `Device`'s constructor (`Device.cs:52-56`) takes
  `AllocatorDescription allocatorDescription` and stores it in a new
  `private readonly AllocatorDescription _allocatorDescription;`.
- `Device.Allocator`'s lazy body (`Device.cs:151`) becomes
  `Ahjo.Vulkan.Allocator.Create(this, in _allocatorDescription);`.
- In `CreateDevice`, **before** `vkCreateDevice`, add the pairing check:

  ```csharp
  if (AhjoValidation.IsEnabled && desc.Allocator.EnableMemoryBudget && !ContainsExtension(desc.Extensions, "VK_EXT_memory_budget"u8))
      AhjoValidation.Fail("PhysicalDevice.CreateDevice",
          "AllocatorDescription.EnableMemoryBudget is set but VK_EXT_memory_budget is not in DeviceDescription.Extensions. " +
          "VMA needs the device extension enabled at vkCreateDevice time; add VulkanExtensions.ExtMemoryBudget to the list.");
  ```

  `ContainsExtension` is a `private static bool` local helper comparing each
  `Utf8Name.Ptr` byte-wise against the literal (there is no existing helper of that
  shape — write one, keep it private, no allocation).

### 2e. `Memory/Allocator.cs` — `GetHeapBudgets` (OPEN-1)

- Add `internal readonly uint HeapCount;` to `Allocator` (third field, after
  `Loader`), set in `Create` from
  `vkGetPhysicalDeviceMemoryProperties(device.PhysicalDevice.Handle, &memProps)` →
  `memProps.memoryHeapCount`. Thread it through the existing
  `internal Allocator(VmaAllocator_T*, nint)` constructor as a third parameter.
- New `Memory/MemoryHeapBudget.cs`:

  ```csharp
  public readonly record struct MemoryHeapBudget
  {
      public uint  HeapIndex       { get; init; }
      public uint  BlockCount      { get; init; }
      public uint  AllocationCount { get; init; }
      public ulong BlockBytes      { get; init; }
      public ulong AllocationBytes { get; init; }
      public ulong Usage           { get; init; }
      public ulong Budget          { get; init; }
  }
  ```

  Doc: `Usage`/`Budget` are meaningful only when the allocator was created with
  `AllocatorDescription.EnableMemoryBudget`; without it VMA estimates them from its
  own bookkeeping and they exclude everything allocated outside VMA — including
  DLSS's driver-side history and scratch.
- New method:

  ```csharp
  public int GetHeapBudgets(Span<MemoryHeapBudget> destination)
  ```

  `stackalloc VmaBudget[16]` (`VK_MAX_MEMORY_HEAPS`), one `vmaGetHeapBudgets`
  (`src/Ahjo.Vulkan.Vma.Native/Generated/Vma.cs:33`), project the first `HeapCount`
  entries, return `(int)HeapCount`. Throw `ArgumentException` when
  `destination.Length < HeapCount`, naming both numbers. No managed allocation.

### 2f. `Rendering/VulkanExtensions.cs` — the name

```csharp
/// <summary>VK_EXT_memory_budget — device-level. Pair with
/// <see cref="AllocatorDescription.EnableMemoryBudget"/> so
/// <see cref="Allocator.GetHeapBudgets"/> reports the driver's real heap usage
/// rather than VMA's own bookkeeping. Needed to see allocations VMA never made —
/// notably DLSS's internal history and scratch (issue #214).</summary>
public static Utf8Name ExtMemoryBudget => Utf8Name.FromLiteral("VK_EXT_memory_budget"u8);
```

Add a `<seealso>` from `Memory/AllocationFlags.cs`'s `DedicatedMemory` noting that
full-screen DLSS-facing targets are the canonical case for it (#214).

**Verify:** `dotnet build Ahjo.Vulkan.slnx -c Release` clean;
`dotnet test tests/Ahjo.Vulkan.Tests` green (existing `Device.Allocator` behaviour is
unchanged because `default(AllocatorDescription).EnableMemoryBudget == false`).

## 3. `src/Ahjo.Vulkan.Ngx` — the project

New folder with `Ahjo.Vulkan.Ngx.csproj`, `README.md`, `CLAUDE.md`. Model the csproj
on `src/Ahjo.Vulkan.Slang/Ahjo.Vulkan.Slang.csproj` verbatim in shape:

```xml
<PropertyGroup>
  <RootNamespace>Ahjo.Vulkan.Ngx</RootNamespace>
  <AssemblyName>Ahjo.Vulkan.Ngx</AssemblyName>
  <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
</PropertyGroup>
<PropertyGroup>
  <IsPackable>true</IsPackable>
  <PackageId>Ahjo.Vulkan.Ngx</PackageId>
  <Title>Ahjo.Vulkan.Ngx</Title>
  <Description>NVIDIA DLSS Super Resolution and DLAA for Ahjo.Vulkan, over the pinned NGX SDK ($(NgxVersion)). The DLSS feature DLL (nvngx_dlss.dll / libnvidia-ngx-dlss.so) is NOT included and must be supplied by the application from NVIDIA; see the package README for the licence obligations.</Description>
  <PackageTags>vulkan;dlss;dlaa;ngx;nvidia;upscaling;antialiasing</PackageTags>
  <MinVerTagPrefix>v</MinVerTagPrefix>
  <MinVerDefaultPreReleaseIdentifiers>alpha.0</MinVerDefaultPreReleaseIdentifiers>
</PropertyGroup>
<ItemGroup>
  <ProjectReference Include="..\Ahjo.Vulkan\Ahjo.Vulkan.csproj" />
  <ProjectReference Include="..\Ahjo.Vulkan.Ngx.Native\Ahjo.Vulkan.Ngx.Native.csproj" />
  <ProjectReference Include="..\Ahjo.Vulkan.Native\Ahjo.Vulkan.Native.csproj" />
  <PackageReference Include="MinVer" PrivateAssets="all" />
  <PackageReference Include="Microsoft.SourceLink.GitHub" PrivateAssets="all" />
  <None Include="README.md" Pack="true" PackagePath="\" />
  <InternalsVisibleTo Include="Ahjo.Vulkan.Ngx.Tests" />
  <InternalsVisibleTo Include="Ahjo.Vulkan.Benchmarks" />
</ItemGroup>
```

The comment block above the package properties says what the Slang one says, adapted:
this package ships **no native files** — the shim rides in `Ahjo.Vulkan.Ngx.Native`
and the feature DLL is the consumer's (fixed decision, #214). `Ahjo.Vulkan` gains no
dependency in the other direction.

Also in this step:

- `Ahjo.Vulkan.slnx` — add `<Project Path="src/Ahjo.Vulkan.Ngx/Ahjo.Vulkan.Ngx.csproj" />`
  after the `Ahjo.Vulkan.Ngx.Native` line, and
  `tests/Ahjo.Vulkan.Ngx.Tests/Ahjo.Vulkan.Ngx.Tests.csproj` after the native test
  project.
- `Directory.Build.props:97-100` — the package-list comment goes from seven to
  **eight**, adding `src/Ahjo.Vulkan.Ngx (Ahjo.Vulkan.Ngx)`.
- `README.md` — add the eighth row to the package table, with the
  consumer-supplies-the-DLL note.
- `src/Ahjo.Vulkan.Ngx/CLAUDE.md` — the scoped memory file. Contents: this package
  wraps `Ahjo.Vulkan.Ngx.Native`; **never** hand-edit `Generated/` in the native
  project; `Evaluate` is a per-frame hot path and carries the zero-allocation rule
  even though it does not live under `Recording/`; the three invariants and which
  are enforced how (spec D2/D3/D4); NGX is not thread-safe; DLSS's VRAM is invisible
  to VMA.

**Verify:** `dotnet build Ahjo.Vulkan.slnx -c Release` clean with an empty project.

## 4. `src/Ahjo.Vulkan.Ngx/Internal/` — five internal helpers

All `internal static`, all AOT-clean, no reflection.

### 4a. `Internal/NgxLoader.cs`

```csharp
internal static unsafe class NgxLoader
{
    internal static nint Load();                       // vulkan-1.dll / libvulkan.so.1
    internal static void* GetExport(nint loader, ReadOnlySpan<byte> utf8Name);
}
```

`Load` mirrors `Allocator.LoadVulkanLoader` (`src/Ahjo.Vulkan/Memory/Allocator.cs:342-364`)
with the candidate list narrowed to the two RIDs NGX ships for
(`["vulkan-1.dll", "vulkan-1"]` / `["libvulkan.so.1", "libvulkan.so"]`), throwing
`NgxException` naming the loader when none loads. Copy the comment explaining *why*
the loader is re-loaded rather than reusing the `[DllImport]` statics — CS8757 —
and cite `Allocator.cs:342-364` as the sibling.

### 4b. `Internal/NgxValidation.cs`

```csharp
internal static class NgxValidation
{
    internal static bool IsEnabled { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => AhjoValidation.Enabled; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [DoesNotReturn]
    internal static void Fail(string source, string message)
    {
        AhjoDiagnostics.Sink(DiagnosticSeverity.Error, source, message);
        throw new AhjoValidationException(message);
    }
}
```

The comment states spec D10/E4: this is `AhjoValidation.Fail`
(`src/Ahjo.Vulkan/Diagnostics/AhjoValidation.cs:94-99`) re-expressed against the
public surface, because `Fail` and `AhjoDiagnostics.Write` are `internal` and this is
a separately published package — **do not** add an `InternalsVisibleTo` to
`Ahjo.Vulkan` for it.

### 4c. `Internal/NgxUtf8.cs`

```csharp
internal unsafe ref struct NgxUtf8Block
{
    internal NgxUtf8Block(int byteCapacity, int stringCapacity);
    internal sbyte* Add(string? value);      // returns null for null; NUL-terminates
    internal sbyte** AddArray(IReadOnlyList<string>? values, out uint count);
    internal void Dispose();                 // frees the block
}

internal static class NgxUtf8
{
    internal static string? ToString(sbyte* utf8);   // Marshal.PtrToStringUTF8
}
```

One `NativeMemory.Alloc` block, bump-allocated, freed on `Dispose`. **Every `Add`
writes the terminating NUL explicitly** — the exact bug PR #217 fixed on the shim
side (`"…"u8.ToArray()` copies only `Length`). Encoding is
`Encoding.UTF8.GetBytes(ReadOnlySpan<char>, Span<byte>)` into the block, never a
`byte[]` whose address is taken. Setup-time only; nothing on the evaluate path uses
it.

### 4d. `Internal/NgxParameterNames.cs`

```csharp
internal static class NgxParameterNames
{
    internal static readonly Utf8Name Color = Utf8Name.FromLiteral(NgxApi.NVSDK_NGX_Parameter_Color);
    // …
}
```

One `static readonly Utf8Name` per name the wrapper uses. Exactly these, and nothing
else (each maps to a `NgxApi.NVSDK_NGX_Parameter_*` property):

*Create:* `CreationNodeMask`, `VisibilityNodeMask`, `Width`, `Height`, `OutWidth`,
`OutHeight`, `PerfQualityValue`, `DLSS_Feature_Create_Flags`,
`DLSS_Enable_Output_Subrects`, and the six
`DLSS_Hint_Render_Preset_{DLAA,Quality,Balanced,Performance,UltraPerformance,UltraQuality}`.

*Evaluate:* `Color`, `Output`, `Depth`, `MotionVectors`, `ExposureTexture`,
`DLSS_Input_Bias_Current_Color_Mask`, `Jitter_Offset_X`, `Jitter_Offset_Y`, `Reset`,
`MV_Scale_X`, `MV_Scale_Y`, `DLSS_Render_Subrect_Dimensions_Width`,
`DLSS_Render_Subrect_Dimensions_Height`, `DLSS_Pre_Exposure`, `DLSS_Exposure_Scale`,
`DLSS_Input_Color_Subrect_Base_X/Y`, `DLSS_Input_Depth_Subrect_Base_X/Y`,
`DLSS_Input_MV_SubrectBase_X/Y`, `DLSS_Input_Bias_Current_Color_SubrectBase_X/Y`,
`DLSS_Output_Subrect_Base_X/Y`.

*Capability / settings / stats:* `SuperSampling_Available`,
`SuperSampling_NeedsUpdatedDriver`, `SuperSampling_FeatureInitResult`,
`SuperSampling_MinDriverVersionMajor`, `SuperSampling_MinDriverVersionMinor`,
`DLSSOptimalSettingsCallback`, `DLSSGetStatsCallback`, `RTXValue`, `Sharpness`,
`SizeInBytes`, `FreeMemOnReleaseFeature`,
`DLSS_Get_Dynamic_{Min,Max}_Render_{Width,Height}`.

Comment: spec E7 — these are RVA-backed `"…"u8` literals, so `FromLiteral` is free
and the resulting pointers are process-lifetime; the evaluate path reads a static
field instead of deriving a pointer per call. **Do not** add
`NVSDK_NGX_EParameter_*` names here: they were excluded from the bindings on purpose
(#216 E7).

### 4e. `Internal/NgxResult.cs`

```csharp
internal static unsafe class NgxResult
{
    internal static bool Succeeded(NVSDK_NGX_Result result) => result == NVSDK_NGX_Result.NVSDK_NGX_Result_Success;
    internal static string Describe(NVSDK_NGX_Result result);         // stackalloc byte[128] + ahjo_ngx_result_to_utf8
    internal static void ThrowIfFailed(NVSDK_NGX_Result result, string operation);
}
```

`Describe` calls `NgxApi.ahjo_ngx_result_to_utf8(result, buffer, 128)`; when the
return value exceeds 128 it retries once with the requested size (the shim returns
the required byte count — #216 D2). `ThrowIfFailed` throws
`NgxException(result, $"{operation} failed: {result} (0x{(uint)result:X8}) — {Describe(result)}")`.

## 5. Shadow enums + drift-test targets

Five files in the package root, each with hand-copied values and a doc comment
naming the generated enum it shadows.

- `DlssQualityMode.cs` — `uint`-backed: `MaxPerformance = 0`, `Balanced = 1`,
  `MaxQuality = 2`, `UltraPerformance = 3`, `UltraQuality = 4`, `Dlaa = 5`.
- `DlssFeatureFlags.cs` — `[Flags] int`-backed (the native enum is untyped, so it is
  `int`): `None = 0`, `Hdr = 1 << 0`, `MotionVectorsLowRes = 1 << 1`,
  `MotionVectorsJittered = 1 << 2`, `DepthInverted = 1 << 3`, `AutoExposure = 1 << 6`,
  `AlphaUpscaling = 1 << 7`. **Omit** `DoSharpening` (deprecated), `IsInvalid` and
  both `Reserved_*`, with a comment saying so.
- `DlssPreset.cs` — `uint`-backed: `Default = 0`, `E = 5`, `F = 6`, `G = 7`,
  `J = 10`, `K = 11`, `L = 12`, `M = 13`, `N = 14`, `O = 15`. Omit
  `H_Reserved`/`I_Reserved`. Doc comment carries #214's guidance: `K` is the
  transformer default for DLAA/Quality/Balanced, `L`/`M` for UltraPerf/Perf, `J`
  trades flicker for less ghosting, `E`/`F` are the deprecated CNN presets.
- `NgxLoggingLevel.cs` — `Off = 0`, `On = 1`, `Verbose = 2`.
- `NgxFeatureSupport.cs` — `[Flags] uint` shadow of
  `NVSDK_NGX_Feature_Support_Result`: `Supported = 0`, `CheckNotPresent = 1`,
  `DriverVersionUnsupported = 2`, `AdapterUnsupported = 4`,
  `OsVersionBelowMinimum = 8`, `NotImplemented = 16`.

## 6. `NgxDescription.cs` (+ validation)

```csharp
public readonly record struct NgxDescription
{
    public string  ProjectId          { get; init; }   // GUID-shaped; NGX validates it
    public string  EngineVersion      { get; init; }
    public string? ApplicationDataPath{ get; init; }   // null => process temp path chosen by NGX
    public IReadOnlyList<string>? DlssSearchPaths { get; init; }
    public NgxLoggingLevel LoggingLevel { get; init; }        // default Off
    public bool DisableOtherLoggingSinks { get; init; }
}
```

Plus `internal void Validate()` (called by `NgxSupport.*` and `NgxContext.Create`,
setup-time so allocation is fine — **not** gated on `AhjoValidation`, because a bad
project id reaches a `strlen` + GUID parse inside NGX):

- `ProjectId` null/whitespace → `ArgumentException` naming the property and saying it
  must be a GUID-like string (guide §5.2.1); `Guid.TryParse` failing → the same
  exception with the offending value quoted.
- `EngineVersion` null/whitespace → `ArgumentException`.
- Any null or whitespace entry in `DlssSearchPaths` → `ArgumentException` naming the
  index.

Also `internal AhjoNgxInitInfo ToNative(ref NgxUtf8Block block)`, filling:
`StructSize = (uint)sizeof(AhjoNgxInitInfo)`,
`IdentifierType = NVSDK_NGX_Application_Identifier_Type_Project_Id`,
`ApplicationId = 0`, `ProjectId`/`EngineVersion`/`ApplicationDataPath` from the block,
`EngineType = NVSDK_NGX_ENGINE_TYPE_CUSTOM`, the search-path array from the block,
`LogCallback = &NgxContext.LogThunk` (or `null` when `LoggingLevel == Off`),
`MinimumLoggingLevel = (NVSDK_NGX_Logging_Level)LoggingLevel`,
`DisableOtherLoggingSinks = (byte)(DisableOtherLoggingSinks ? 1 : 0)`.

## 7. `NgxExtensionSet.cs`, `NgxSupport.cs`, `DlssRequirements.cs`

### 7a. `NgxExtensionSet`

```csharp
public sealed unsafe class NgxExtensionSet : IDisposable
{
    internal static NgxExtensionSet FromProperties(ReadOnlySpan<VkExtensionProperties> properties);
    public ReadOnlySpan<Utf8Name> Names { get; }
    public int Count { get; }
    public void Dispose();          // idempotent
}
```

`FromProperties` copies each `extensionName` (a `char[256]` inline array) up to its
first NUL into one `NativeMemory.Alloc` block, writing an explicit terminator, and
builds a `Utf8Name[]` of pointers into that block. `Dispose` frees the block and
clears the array; a second `Dispose` is a no-op. Doc comment cites spec E8: NGX's
returned array has no documented lifetime and `Utf8Name`
(`src/Ahjo.Vulkan/Lifecycle/Utf8Name.cs:28-35`) requires stable non-movable storage,
so aliasing it would be unsound — hence the copy.

`FromProperties` is `internal` on purpose: it is the seam step 14 drives with a
fabricated array so CI can prove the termination contract with no NGX and no driver.

### 7b. `DlssRequirements`

```csharp
public readonly record struct DlssRequirements
{
    public bool              IsSupported          { get; init; }
    public NgxFeatureSupport Reason               { get; init; }
    public uint              MinimumArchitecture  { get; init; }
    public string            MinimumOsVersion     { get; init; }
}
```

Projected from `NVSDK_NGX_FeatureRequirement`; `MinOSVersion` is a `char[255]` inline
array decoded as UTF-8 up to its first NUL.

### 7c. `NgxSupport`

```csharp
public static class NgxSupport
{
    public static bool TryGetInstanceExtensions(in NgxDescription description, out NgxExtensionSet extensions);
    public static bool TryGetDeviceExtensions(PhysicalDevice physicalDevice, in NgxDescription description, out NgxExtensionSet extensions);
    public static bool IsSuperSamplingSupported(PhysicalDevice physicalDevice, in NgxDescription description);
    public static bool TryGetSuperSamplingRequirements(PhysicalDevice physicalDevice, in NgxDescription description, out DlssRequirements requirements);
}
```

Each builds an `NgxUtf8Block` + `AhjoNgxInitInfo` from the description, calls the
matching `ahjo_ngx_*_utf8` export
(`NgxApi.cs:87`, `:90`, `:84`) with
`NVSDK_NGX_Feature.NVSDK_NGX_Feature_SuperSampling`, disposes the block, and returns
`false` (with `extensions = null` / `requirements = default`) on any non-`Success`
rather than throwing — these are capability queries a settings screen runs. The
Vulkan handles come from `physicalDevice.Instance.RawHandle` and
`physicalDevice.RawHandle`, cast to `VkInstance_T*` / `VkPhysicalDevice_T*`.

**If OPEN-2 was declined**, the three device-level methods take
`(Instance instance, PhysicalDevice physicalDevice, …)` instead, with a doc comment
requiring the instance to be the one that enumerated the physical device.

Doc comments record the two facts #216 measured so nobody re-derives them:
`TryGetInstanceExtensions` is a pre-instance static query that answers identically
with and without an NVIDIA driver (#216 OPEN-1, resolved); the other three need a
live `VkInstance` and are **not** callable on the driverless `ngx-native` lane
(#216 finding 4).

## 8. `NgxContext.cs`

```csharp
public sealed unsafe class NgxContext : IDisposable
{
    public static NgxContext Create(Device device, in NgxDescription description);
    public bool IsSuperSamplingAvailable { get; }
    public DlssOptimalSettings GetOptimalSettings(uint outputWidth, uint outputHeight, DlssQualityMode mode);
    public bool TryGetStats(out DlssStats stats);
    public DlssFeature CreateDlss(ref CommandRecorder recorder, in DlssFeatureDescription description);
    public void Dispose();
}
```

State: `Device _device`, `nint _loader`, `NVSDK_NGX_Parameter* _capabilityParameters`,
the availability triple cached at creation, `int _busy` (the re-entrancy guard),
`bool _disposed`.

`Create` in order:

1. `description.Validate()`.
2. `nint loader = NgxLoader.Load();` — kept for the context's lifetime, freed in
   `Dispose`; the `Allocator.Loader` pattern (`Memory/Allocator.cs:36-40`).
3. Resolve `vkGetInstanceProcAddr` / `vkGetDeviceProcAddr` with
   `NgxLoader.GetExport` and cast each to the generated
   `delegate* unmanaged[Cdecl]<…>` parameter type of `ahjo_ngx_vulkan_init_utf8`
   (`NgxApi.cs:81`). Comment: on the two RIDs NGX ships for, both x86-64, the
   `Cdecl` typing (from the fixed Linux parse target) and Vulkan's `VKAPI_PTR` are
   the same ABI, and the pointer arrives as an `nint` so no delegate-type conversion
   occurs (spec E12).
4. Build the `NgxUtf8Block` + `AhjoNgxInitInfo`, call `ahjo_ngx_vulkan_init_utf8`
   with `device.PhysicalDevice.Instance.RawHandle`, `device.PhysicalDevice.RawHandle`
   and `device.RawHandle` cast to the three handle pointer types, dispose the block
   (spec E5: the shim copies and retains everything on the init path —
   `native/ngx/src/ahjo_ngx.cpp:707-760`).
5. On non-`Success`, map through step 8b and throw.
6. `NVSDK_NGX_VULKAN_GetCapabilityParameters(&_capabilityParameters)` —
   `ThrowIfFailed`.
7. Read the availability triple with
   `NVSDK_NGX_Parameter_GetI/GetUI`: `SuperSampling.Available`,
   `SuperSampling.NeedsUpdatedDriver`, `SuperSampling.FeatureInitResult`,
   `SuperSampling.MinDriverVersionMajor`, `SuperSampling.MinDriverVersionMinor`.
   Apply step 8b again. `IsSuperSamplingAvailable` is the cached result.

**8b — the diagnosis mapping** (a `private static` helper used from both places):

- `FeatureInitResult == NVSDK_NGX_Result_FAIL_FeatureNotFound`, or `Init` returned
  it → `NgxFeatureLibraryNotFoundException`. Message, verbatim shape:

  ```
  DLSS is unavailable: the NVIDIA feature library was not found.
  Expected file: nvngx_dlss.dll            (linux: libnvidia-ngx-dlss.so.<version>)
  Searched:
    <AppContext.BaseDirectory>
    <each NgxDescription.DlssSearchPaths entry, one per line>
  This library is NOT shipped by Ahjo.Vulkan.Ngx — the application supplies it from
  NVIDIA's DLSS SDK (https://github.com/NVIDIA/DLSS, lib/<plat>/rel/). See
  docs/ngx-notes.md.
  ```

  (The docs page lands in Phase 3; the link is written now so the message does not
  need a second edit.)
- `NeedsUpdatedDriver != 0` → `NgxDriverTooOldException`, message naming
  `MinDriverVersionMajor.MinDriverVersionMinor`.
- Any other non-`Success` → `NgxException` via `NgxResult.ThrowIfFailed`.

**8c — the log thunk**:

```csharp
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
private static void LogThunk(sbyte* message, NVSDK_NGX_Logging_Level level, NVSDK_NGX_Feature feature)
```

Body: `try { AhjoDiagnostics.Sink(map(level), "NGX", $"[NGX {feature}] {NgxUtf8.ToString(message)}"); } catch { }`.
The `catch { }` and its comment are copied from
`src/Ahjo.Vulkan/Lifecycle/Instance.cs:614-617` — never throw across the
unmanaged-to-managed boundary.

**8d — the re-entrancy guard**: a `private ref struct Busy` (or a private
`EnterExclusive`/`ExitExclusive` pair) used by `GetOptimalSettings`, `TryGetStats`,
`CreateDlss` and `DlssFeature.Evaluate`. Body:

```csharp
if (!NgxValidation.IsEnabled) return;                       // one predictable branch when off
if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
    NgxValidation.Fail("NgxContext", "The NGX API is not thread safe (DLSS Programming Guide §5.2.4) …");
```

released in a `finally`. Comment cites the guide and spec E10 (the capability map is
mutable shared state).

**Dispose** order: `DestroyParameters(_capabilityParameters)` →
`NVSDK_NGX_VULKAN_Shutdown1(device)` → `NativeLibrary.Free(_loader)`. Idempotent via
`_disposed`. Doc comment: the caller must dispose every `DlssFeature` first.

**If OPEN-2 was declined**, `Create` takes `(Instance instance, Device device, in NgxDescription)`.

## 9. `DlssOptimalSettings.cs`, `DlssStats.cs`, and the two capability-map queries

### 9a. Types

```csharp
public readonly record struct DlssOptimalSettings
{
    public bool IsAvailable     { get; init; }
    public uint RenderWidth     { get; init; }
    public uint RenderHeight    { get; init; }
    public uint MinRenderWidth  { get; init; }
    public uint MinRenderHeight { get; init; }
    public uint MaxRenderWidth  { get; init; }
    public uint MaxRenderHeight { get; init; }
}

public readonly record struct DlssStats
{
    public ulong VramAllocatedBytes { get; init; }
}
```

Six dimensions, not four — spec D8 records the deviation from #218's wording and
why. `DlssStats` ships one field; **OPEN-3** governs whether `OptLevel` /
`IsDevSnippetBranch` may be added, and they may not be added on a guess.

### 9b. `NgxContext.GetOptimalSettings`

A managed transcription of `NGX_DLSS_GET_OPTIMAL_SETTINGS`
(`native/ngx/include/nvsdk_ngx_helpers.h:64-113`) against `_capabilityParameters`:

1. `NVSDK_NGX_Parameter_GetVoidPointer(_capabilityParameters, NgxParameterNames.DLSSOptimalSettingsCallback.Ptr, &callback)`.
   Null → throw `NgxException(FAIL_OutOfDate, …)` carrying the header's own two
   causes (out-of-date feature DLL; an `AllocateParameters` map was used instead of
   the capability map).
2. `SetUI(Width, outputWidth)`, `SetUI(Height, outputHeight)`,
   `SetI(PerfQualityValue, (int)mode)`, `SetI(RTXValue, 0)` — the last one because
   "some older DLSS dlls still expect this value to be set"
   (`nvsdk_ngx_helpers.h:89`).
3. Invoke via
   `((delegate* unmanaged[Cdecl]<NVSDK_NGX_Parameter*, NVSDK_NGX_Result>)callback)(_capabilityParameters)`;
   `ThrowIfFailed`.
4. `GetUI(OutWidth)`, `GetUI(OutHeight)`; seed min and max from that pair **before**
   overwriting them from `DLSS.Get.Dynamic.{Min,Max}.Render.{Width,Height}` — the
   helper does exactly this for older feature DLLs that leave those keys unset.
5. `GetF(Sharpness, …)` is **not** read (deprecated).
6. `RenderWidth == 0 || RenderHeight == 0` → return
   `new DlssOptimalSettings { IsAvailable = false }` with every dimension zero.
   Otherwise `IsAvailable = true`.

Wrap the whole body in the step-8d guard.

### 9c. `NgxContext.TryGetStats`

`GetVoidPointer(DLSSGetStatsCallback)` → null returns `false`; otherwise invoke and
`GetULL(SizeInBytes, &bytes)`, returning `true`. Comment cites spec E9: `OptLevel`
and `IsDevSnippetBranch` are unreachable because
`NGX_DLSS_GET_STATS_2` reads them through the excluded `NVSDK_NGX_EParameter_*` hash
aliases (`nvsdk_ngx_helpers.h:42-43`, #216 E7/D7); see OPEN-3 before adding them.

## 10. `NgxImage.cs`

```csharp
public readonly unsafe struct NgxImage : IDisposable
{
    public static NgxImage CreateView(Device device, in Image image, in ImageViewDescription view);
    public static NgxImage Wrap(in Image image, in ImageView view, in ImageViewDescription viewDescription);

    public bool IsNull   { get; }
    public bool OwnsView { get; }
    public void Dispose();
}
```

Fields (all `readonly`, `internal` where the wrapper needs them):
`VkImage_T* Image`, `VkImageView_T* View`, `VkImageSubresourceRange Range`,
`VkFormat Format`, `uint Width`, `uint Height`, `ImageUsage Usage`,
`VkDevice_T* _ownedViewDevice`.

Both factories funnel into one private constructor that:

- builds `Range` from `viewDescription` with the **same** field mapping
  `Image.CreateView` uses (`src/Ahjo.Vulkan/Resources/Image.cs:127-134`);
- **resolves the sentinels**: `LevelCount == VK_REMAINING_MIP_LEVELS` becomes
  `image.MipLevels - BaseMipLevel`; `LayerCount == VK_REMAINING_ARRAY_LAYERS` becomes
  `image.ArrayLayers - BaseArrayLayer`. Comment: spec OPEN-4 — how the feature DLL
  consumes the range is undocumented, a concrete count is equivalent for every
  Vulkan use, and `Image.FromRaw` reports 1/1 which is right for a swapchain image
  (`Resources/Image.cs:84-85`);
- takes `Format` from `viewDescription.Format`, falling back to `image.Format` on
  `VK_FORMAT_UNDEFINED`, matching `Image.CreateView` (`:139`);
- takes `Width`/`Height`/`Usage` off the `Image`.

`CreateView` calls `image.CreateView(device, view)` and records the device so
`Dispose` destroys the view; `Wrap` leaves `_ownedViewDevice` null and `Dispose` is a
no-op — the `OwnsHandle` split `Image`/`ImageView` already use
(`Resources/Image.cs:97`, `Resources/ImageView.cs:39`).

`internal NVSDK_NGX_Resource_VK ToNative(bool readWrite)` builds the resource:
`Type = NVSDK_NGX_RESOURCE_VK_TYPE_VK_IMAGEVIEW`, the six `ImageViewInfo` fields, and
`ReadWrite = readWrite` — **`true`/`false`, not `1`/`0`**; Phase 1 measured the field
as C# `bool` (`Generated/NVSDK_NGX_Resource_VK.cs:11-12`, #216 E11).

Class-level doc: this type exists so the ImageView/Image/SubresourceRange triple
cannot disagree (spec D2), and `ReadWrite` is deliberately not a member (spec D3).
`Wrap`'s doc states the one unenforceable contract: the description must be the one
that created the view, because nothing can recover a `VkImageView`'s range.

**Not a `record struct`** — it carries pointers (spec E16).

## 11. `DlssFeatureDescription.cs`, `DlssEvaluateInputs.cs`, `DlssSubrects.cs`

```csharp
public readonly record struct DlssFeatureDescription
{
    public uint RenderWidth  { get; init; }
    public uint RenderHeight { get; init; }
    public uint OutputWidth  { get; init; }
    public uint OutputHeight { get; init; }
    public DlssQualityMode  Mode  { get; init; }
    public DlssFeatureFlags Flags { get; init; }
    public DlssPreset       Preset{ get; init; }
    public bool EnableOutputSubrects  { get; init; }
    public bool FreeMemoryOnRelease   { get; init; }   // default false = NGX's own behaviour
}

public readonly record struct DlssSubrects
{
    public uint ColorBaseX { get; init; }  public uint ColorBaseY { get; init; }
    public uint DepthBaseX { get; init; }  public uint DepthBaseY { get; init; }
    public uint MotionVectorsBaseX { get; init; }  public uint MotionVectorsBaseY { get; init; }
    public uint BiasCurrentColorBaseX { get; init; }  public uint BiasCurrentColorBaseY { get; init; }
    public uint OutputBaseX { get; init; }  public uint OutputBaseY { get; init; }
}

public readonly struct DlssEvaluateInputs           // NOT a record struct — carries pointers (E16)
{
    public NgxImage Color                { get; init; }
    public NgxImage Depth                { get; init; }
    public NgxImage MotionVectors        { get; init; }
    public NgxImage Output               { get; init; }
    public NgxImage ExposureTexture      { get; init; }   // optional; default(NgxImage) = none
    public NgxImage BiasCurrentColorMask { get; init; }   // optional
    public float JitterOffsetX { get; init; }
    public float JitterOffsetY { get; init; }
    public uint  RenderWidth   { get; init; }
    public uint  RenderHeight  { get; init; }
    public bool  Reset         { get; init; }
    public float MotionVectorScaleX { get; init; } = 1f;
    public float MotionVectorScaleY { get; init; } = 1f;
    public float PreExposure        { get; init; } = 1f;
    public float ExposureScale      { get; init; } = 1f;
    public DlssSubrects Subrects    { get; init; }

    public DlssEvaluateInputs() { }        // CS8983 — runs the field initializers
}
```

The explicit parameterless constructor is the `ImageViewDescription` pattern
(`Memory/ImageViewDescription.cs:69`, #119). `JitterOffsetX/Y` are documented as
**render-pixel space** (`nvsdk_ngx_helpers_vk.h:69`).

`DlssEvaluateInputs`'s class doc carries the layout contract (spec D4) in full:
inputs in a shader-read layout, output in `VK_IMAGE_LAYOUT_GENERAL`, evaluate
recorded **outside** any `BeginRendering` scope, DLSS restores the states before
returning (guide §3.4), and — explicitly — that the wrapper **cannot check this**
because layout is not tracked on `Image` by design (`Resources/Image.cs:19-24`,
issue #17).

## 12. `DlssFeature.cs` and `NgxContext.CreateDlss`

### 12a. `CreateDlss`

```csharp
public DlssFeature CreateDlss(ref CommandRecorder recorder, in DlssFeatureDescription description)
```

Under the step-8d guard. Body transcribes `NGX_VULKAN_CREATE_DLSS_EXT1`
(`nvsdk_ngx_helpers_vk.h:113-135`):

1. Validate (always, setup-time): render and output extents non-zero; output ≥ 32×32;
   render ≤ output. Throw `ArgumentException` naming the field.
2. `NVSDK_NGX_VULKAN_AllocateParameters(&parameters)` — `ThrowIfFailed`. (Only ever
   from an initialized context, which satisfies #216 OPEN-2 by construction — see
   spec OPEN-5.)
3. `SetUI(CreationNodeMask, 1)`, `SetUI(VisibilityNodeMask, 1)`, `SetUI(Width, …)`,
   `SetUI(Height, …)`, `SetUI(OutWidth, …)`, `SetUI(OutHeight, …)`,
   `SetI(PerfQualityValue, (int)Mode)`, `SetI(DLSS_Feature_Create_Flags, (int)Flags)`,
   `SetI(DLSS_Enable_Output_Subrects, EnableOutputSubrects ? 1 : 0)`.
4. When `Preset != DlssPreset.Default`, `SetUI` the **one** hint key matching
   `description.Mode` (`DLAA`→`…Preset_DLAA`, `MaxQuality`→`…Preset_Quality`,
   `Balanced`→`…Preset_Balanced`, `MaxPerformance`→`…Preset_Performance`,
   `UltraPerformance`→`…Preset_UltraPerformance`,
   `UltraQuality`→`…Preset_UltraQuality`).
5. `NVSDK_NGX_VULKAN_CreateFeature1(device, (VkCommandBuffer_T*)recorder.RawHandle, NVSDK_NGX_Feature_SuperSampling, parameters, &handle)`
   — `ThrowIfFailed`; on failure `DestroyParameters` first.
6. Query the feature's dynamic range once with `GetUI` on the four
   `DLSS_Get_Dynamic_*` keys and store it on the feature for the evaluate-time
   range check.

Doc comment: **the recorder must be submitted and completed before the first
`Evaluate`** — `CreateFeature1` records initialization work into it.

### 12b. `DlssFeature`

```csharp
public sealed unsafe class DlssFeature : IDisposable
{
    public void Evaluate(ref CommandRecorder recorder, in DlssEvaluateInputs inputs);
    public uint RenderWidth  { get; }  public uint RenderHeight { get; }
    public uint OutputWidth  { get; }  public uint OutputHeight { get; }
    public uint MinRenderWidth { get; }  public uint MinRenderHeight { get; }
    public uint MaxRenderWidth { get; }  public uint MaxRenderHeight { get; }
    public void Dispose();
}
```

`Evaluate`, in order — **this is the hot path; nothing in it may allocate**:

1. Enter the context's step-8d guard.
2. Under `NgxValidation.IsEnabled` only, run the checks of spec D3/D9 (see step 12c).
3. Build up to six `NVSDK_NGX_Resource_VK` **stack locals** via
   `NgxImage.ToNative(readWrite:)` — `false` for Color/Depth/MotionVectors/Exposure/
   BiasCurrentColorMask, `true` for Output. Optional slots that are
   `default(NgxImage)` are not built and their parameter is set to `null`.
4. `SetVoidPointer` for each of the six (`&color`, `&output`, `&depth`, `&mv`,
   `&exposure`-or-null, `&bias`-or-null).
5. `SetF(Jitter_Offset_X/Y)`, `SetI(Reset, inputs.Reset ? 1 : 0)`,
   `SetF(MV_Scale_X/Y)` (falling back to `1f` when the caller wrote `0f`, the
   helper's own behaviour at `nvsdk_ngx_helpers_vk.h:177-178`),
   `SetUI(DLSS_Render_Subrect_Dimensions_Width/Height)`,
   `SetF(DLSS_Pre_Exposure)` and `SetF(DLSS_Exposure_Scale)` (same `0f → 1f`
   fallback, `:224-225`), and the ten `*_Subrect_Base_{X,Y}` keys from
   `inputs.Subrects`.
6. `NVSDK_NGX_VULKAN_EvaluateFeature_C((VkCommandBuffer_T*)recorder.RawHandle, _handle, _parameters, null)`
   — `ThrowIfFailed("DLSS evaluate")`.
7. Exit the guard in a `finally`.

**Do not** write the research-only slots (`GBuffer.*`, `MotionVectors3D`,
`IsParticleMask`, `AnimatedTextureMask`, `DepthHighRes`, `Position.ViewSpace`,
`FrameTimeDeltaInMsec`, `RayTracingHitDistance`, `MotionVectorsReflection`,
`TonemapperType`, `TransparencyMask`) or `Sharpness` — spec D9 records the omission
and why. **Do not** split `Evaluate` into a prepare step: the map retains raw
pointers to these stack locals (spec E6).

`Evaluate`'s doc comment must contain, verbatim in substance:

> **Rebind after evaluate.** `EvaluateFeature_C` clobbers the command buffer's bound
> pipeline, descriptor sets and dynamic state (DLSS Programming Guide §5.2.5).
> `CommandRecorder` caches no bound state, so there is nothing for the wrapper to
> invalidate — but the *caller* must rebind everything before the next draw or
> dispatch.

plus the layout contract cross-reference and "the NGX API is not thread safe".

`Dispose`: `SetI(FreeMemOnReleaseFeature, 1)` when the description asked for it, then
`NVSDK_NGX_VULKAN_ReleaseFeature(_handle)`, then
`NVSDK_NGX_VULKAN_DestroyParameters(_parameters)`. Idempotent.

### 12c. The validation block (`internal` seam for tests and benchmarks)

Factor the checks into
`internal void ValidateInputs(in DlssEvaluateInputs inputs)` so step 14 can drive
them without a device:

- `Color`/`Depth`/`MotionVectors`/`Output` non-null → else
  `NgxValidation.Fail` naming the slot.
- `Output.Width >= 32 && Output.Height >= 32` (guide §3.3).
- `inputs.RenderWidth`/`RenderHeight` inside `[MinRender*, MaxRender*]` — message
  quotes all three numbers.
- Usage bits (spec D3): `Output.Usage` has `ImageUsage.Storage`; each present input
  has `ImageUsage.Sampled`. **`ImageUsage.None` is skipped, not failed** — it is the
  `Image.FromRaw` "unknown" state (`Resources/Image.cs:84-85`, spec E3) — and the
  comment says so, because the obvious reading of the check is wrong.

Also factor the map population of steps 3–5 into
`internal void PackEvaluateParameters(in DlssEvaluateInputs inputs)`… **no**: that
would leave dangling pointers in the map (spec E6). Instead expose
`internal void EvaluateCore(VkCommandBuffer_T* commandBuffer, in DlssEvaluateInputs inputs, bool invokeNgx)`
— one method, one frame, with the final `EvaluateFeature_C` call skipped when
`invokeNgx` is `false`. That is the seam `PackParameters_16` in step 15 measures, and
it keeps the resource structs and the native call in the same frame in both modes.

## 13. `NgxException.cs`

```csharp
public class NgxException : Exception
{
    public NVSDK_NGX_Result Result { get; }
    public NgxException(NVSDK_NGX_Result result, string message);
}
public sealed class NgxFeatureLibraryNotFoundException : NgxException { … }
public sealed class NgxDriverTooOldException : NgxException
{
    public uint MinimumDriverVersionMajor { get; }
    public uint MinimumDriverVersionMinor { get; }
}
```

Doc: distinct from `AhjoValidationException` (wrapper misuse) and
`VulkanException` (a `VkResult`) — this is a `NVSDK_NGX_Result` from NGX.

## 14. `tests/Ahjo.Vulkan.Ngx.Tests`

New xunit.v3 project modelled on `tests/Ahjo.Vulkan.Slang.Tests`: references
`Ahjo.Vulkan`, `Ahjo.Vulkan.Ngx`, `Ahjo.Vulkan.Ngx.Native`, and links
`<Compile Include="..\Shared\*.cs" LinkBase="Shared" />` (the same clause at
`Ahjo.Vulkan.Slang.Tests.csproj:29`). Add it to `Ahjo.Vulkan.slnx` and to the table
at the top of `tests/CLAUDE.md`.

`NgxTestEnvironment.cs` (internal, `Lazy<bool>`-backed, mirroring
`tests/Ahjo.Vulkan.Ngx.Native.Tests/NgxShimFixture.cs`):

- `ShimPresent` — `NativeLibrary.TryLoad("ahjo_ngx", …)` succeeds.
- `IsDlssAvailable` — `ShimPresent`, plus a `Device` can be created, plus
  `NgxSupport.IsSuperSamplingSupported` returns `true`.

Gates, using existing `TestGate` classes so no `ci.yml` edit is needed (spec E14):
`TestGate.RequirePlatform(NgxTestEnvironment.ShimPresent, "ahjo_ngx shim not staged — DLSS is opt-in; run ./tools/setup-ngx.ps1.")`
and
`TestGate.RequireDeviceFeature(NgxTestEnvironment.IsDlssAvailable, "No NVIDIA GPU with a DLSS-capable driver and nvngx_dlss.dll on this host.")`.

### Files and cases

**`NgxShadowEnumDriftTests.cs`** — no device, no shim. One `[Fact]` per enum,
member-by-member `Assert.Equal` against the generated enum, the
`ShadowEnumDriftTests` shape (`tests/Ahjo.Vulkan.Tests/ShadowEnumDriftTests.cs:24-56`),
plus one `[Fact]` per enum pinning the **count** of shadowed members so a pin bump
that adds a member is a decision, not a silent gap. Cover `DlssQualityMode`,
`DlssFeatureFlags`, `DlssPreset`, `NgxLoggingLevel`, `NgxFeatureSupport`.

**`NgxDescriptionTests.cs`** — no device, no shim. `Validate` throws
`ArgumentException` for: null `ProjectId`, whitespace `ProjectId`, `"not-a-guid"`,
null `EngineVersion`, a null entry in `DlssSearchPaths` (message names the index).
A well-formed description validates without throwing.

**`NgxExtensionSetTests.cs`** — no device, no shim; drives the `FromProperties`
internal seam with a fabricated `VkExtensionProperties[]`:

- two names round-trip byte-for-byte through `Names`;
- each `Utf8Name.Ptr` addresses a NUL-terminated copy — read forward from the pointer
  and assert the terminator sits exactly at `name.Length` (the direct regression test
  for the class of bug PR #217 fixed on the shim side);
- a 255-character name (the `char[256]` field's maximum) survives;
- `Count == Names.Length`; `Dispose` twice does not throw.

**`DlssOptimalSettingsTests.cs`** — no device: the projection reports
`IsAvailable = false` and all-zero dimensions for a 0×0 answer, and `true` with the
six values for a non-zero one (over the internal projection seam).

**`DlssValidationTests.cs`** — no device, `AhjoValidation.Enabled = true` inside the
test with restore in a `finally`. Drives `DlssFeature.ValidateInputs` against
`default`-handle `NgxImage`s: a missing required slot, a sub-32×32 output, a render
size outside `[Min, Max]`, an output image whose `Usage` lacks `Storage`, an input
whose `Usage` lacks `Sampled` — each asserting the message names the offending slot;
and one case proving `ImageUsage.None` is **skipped** rather than failed.

**`NgxDevicePlumbingTests.cs`** — `TestGate.RequireDriver()`:

- An `NgxExtensionSet` built from names the host actually advertises
  (`vkEnumerateDeviceExtensionProperties`) reaches `vkCreateDevice` through
  `DeviceDescription.Extensions` and the device is created. This proves the
  pointer/termination contract against a real loader with no NGX involved.
- `AllocatorDescription.EnableMemoryBudget = true` **without**
  `VulkanExtensions.ExtMemoryBudget` in `Extensions` throws
  `AhjoValidationException` from `CreateDevice`. *(OPEN-1/step 2d.)*
- With the extension present (`TestGate.RequireDeviceFeature` on
  `physicalDevice.SupportsExtension`), `device.Allocator.GetHeapBudgets(span)`
  returns `> 0` and every returned `HeapIndex` is `< returned count`. *(OPEN-1/step 2e.)*

**`NgxSupportTests.cs`** — `TestGate.RequirePlatform(ShimPresent, …)`:
`NgxSupport.TryGetInstanceExtensions` returns `true` with `Count >= 1` and every name
non-empty. Comment notes that the native-level version of this assertion already runs
in the `ngx-native` lane
(`tests/Ahjo.Vulkan.Ngx.Native.Tests/NgxSmokeTests.cs:193`); this one covers the
wrapper's copy path over it.

**`DlssHardwareTests.cs`** — `TestGate.RequireDeviceFeature(IsDlssAvailable, …)`.
These are the only proof of the real path and run **only** on the dev machine:

1. `NgxContext.Create` succeeds and `IsSuperSamplingAvailable` is `true`.
2. `GetOptimalSettings(3840, 2160, mode)` for all six modes: `Dlaa` returns
   `RenderWidth == 3840 && RenderHeight == 2160`; every available mode returns
   `Min <= Render <= Max`; an unavailable mode returns `IsAvailable == false` with
   zero dimensions rather than a 0×0 render target.
3. End-to-end: allocate colour (`Sampled|ColorAttachment`), depth
   (`Sampled|DepthStencilAttachment`), motion vectors (`R16G16_SFLOAT`,
   `Sampled|ColorAttachment`) and output (`Storage|TransferSrc`) images; wrap each in
   an `NgxImage.CreateView`; `CreateDlss` on an immediate-submit recorder; wait;
   transition the images; `Evaluate`; submit; `vkQueueWaitIdle`; dispose in order.
   Assert no exception and no validation-layer error (run this suite with
   `AHJO_VULKAN_TIER=validation` locally and quote the contract test's
   `declared=… observed=…` line in the PR).
4. `TryGetStats` returns `true` with `VramAllocatedBytes > 0` after a feature exists.
5. Missing-DLL diagnosis: with `DlssSearchPaths` pointed at an empty temp directory
   and the feature DLL moved aside, `Create` throws
   `NgxFeatureLibraryNotFoundException` whose message contains the expected file name
   and that directory. *(If moving the DLL aside is impractical, skip this case with
   `TestGate.Unsupported` and say so in the PR — do not fake it.)*
6. **OPEN-3 probe, optional:** read `Snippet.OptLevel` / `Snippet.IsDevBranch` off the
   stats map and record what you observe in the PR. Only if they return plausible
   values may `DlssStats` gain the two fields, with the measurement written into the
   spec as an amendment. Otherwise leave `DlssStats` at one field.

## 15. `tests/Ahjo.Vulkan.Benchmarks/DlssEvaluateBenchmarks.cs`

Add `<ProjectReference Include="..\..\src\Ahjo.Vulkan.Ngx\Ahjo.Vulkan.Ngx.csproj" />`
to the benchmark csproj.

New class — **its own**, for the reason `MeshShaderBenchmarks` and
`DescriptorSetPoolVariableCountBenchmarks` are their own
(`MeshShaderBenchmarks.cs:18-31`, `.claude/agents/bench-coverage-checker.md:46`):

```csharp
[MemoryDiagnoser]
public unsafe class DlssEvaluateBenchmarks
{
    private const int EvaluatesPerInvoke = 16;

    [GlobalSetup] public void Setup();       // THROWS with an actionable message when unavailable
    [Benchmark(OperationsPerInvoke = EvaluatesPerInvoke)] public void Evaluate_16();
    [Benchmark(OperationsPerInvoke = EvaluatesPerInvoke)] public void PackParameters_16();
    [GlobalCleanup] public void Cleanup();
}
```

- `Setup` builds instance → NVIDIA device → `NgxContext` → images/views →
  `DlssFeature` at 2560×1440 → 3840×2160 DLAA, and **throws**
  `InvalidOperationException` naming what is missing (no NVIDIA GPU / no DLSS-capable
  driver / `nvngx_dlss.dll` not beside the benchmark binary) rather than skipping —
  BenchmarkDotNet has no skip, and a silent zero is worse than a loud failure
  (`MeshShaderBenchmarks.cs:124-127`).
- `Evaluate_16` records 16 `Evaluate` calls into one command buffer per invoke and
  never submits. 16, not the `*_1024` used elsewhere, because one DLSS evaluate
  records many dispatches; say so in a comment.
- `PackParameters_16` calls `EvaluateCore(cb, in inputs, invokeNgx: false)` 16 times —
  the managed side only: parameter-map population plus resource-struct fill, which is
  exactly what #218 asks to measure.
- **Dispose the recorder before `CommandBufferPool.ResetForFrame`** in both methods.
  This is the #188/#199 ordering that `docs/benchmarks.md:109` documents; getting it
  backwards makes the pool ping-pong two buffers and the numbers bimodal.

Also in this step:

- `docs/benchmarks.md` — two rows in the table with `Allocated: -`, the measured
  Mean, and a caveat naming the host (RTX 4070 Ti, driver 610.47) and the
  driver-dependency note the mesh rows carry (`docs/benchmarks.md:101`). Add
  `|*DlssEvaluate*` to the driver-bound filter example at `docs/benchmarks.md:27`.
  **Minimum of 5 runs**, per this file's own discipline.
- `.claude/agents/bench-coverage-checker.md` — a mapping row:
  `src/Ahjo.Vulkan.Ngx/DlssFeature.cs` → `DlssEvaluateBenchmarks.cs`, with the note
  that this class is host-gated and must never be folded into
  `CommandRecorderBenchmarks`.

## 16. CI and publish

- `.github/workflows/ci.yml`, `build-test` job: add a
  `Test — Ahjo.Vulkan.Ngx.Tests` step after the Slang wrapper step
  (`ci.yml:171-173`), same shape, **no trx logger** — the coverage summary reads
  `wrapper.trx` only (spec E14) and this suite does not join it. Comment: the NGX SDK
  is not staged on this lane, so the shim-dependent and NVIDIA-dependent tests skip
  through `TestGate`; what CI proves here is enum drift, description validation, the
  extension-copy contract and the SwiftShader device plumbing.
- Do **not** touch the `ngx-native` lane (`ci.yml:470-472`) or
  `build-ngx-native.yml`. That lane's contract is "no loader, no ICD"
  (`tests/CLAUDE.md`) and `.github/CLAUDE.md` forbids growing it into wrapper
  coverage.
- `.github/workflows/publish.yml`: add a `Pack (NGX wrapper)` step after
  `Pack (NGX native)` (`publish.yml:350-357`), modelled on `Pack (Slang wrapper)`
  (`:331-341`), gated on the same `needs.build-ngx.result == 'success'` and carrying
  `-p:SkipVmaNativeBuild=true -p:SkipNgxNativeBuild=true`. Update the
  `include_ngx` input description (`:55`) to say it packs both NGX packages.

## 17. Docs

- `docs/ci-coverage.md` — a row for `Ahjo.Vulkan.Ngx.Tests` stating precisely what CI
  proves (enum drift, description validation, extension-copy termination, SwiftShader
  device plumbing) and what it cannot (any NGX call needing a `VkInstance`, and every
  DLSS evaluate), pointing at the local-hardware requirement. Also add the
  `slang-native` row noted as missing in #216 E14 **only if it is still missing** —
  check first; #217 may have added it.
- `README.md` — eighth package row (step 3) plus one sentence in the feature list:
  DLSS Super Resolution and DLAA through `Ahjo.Vulkan.Ngx`, with the
  consumer-supplies-the-DLL note.
- `src/Ahjo.Vulkan/CLAUDE.md` — one line under the hot-path list noting that
  `Ahjo.Vulkan.Ngx`'s `DlssFeature.Evaluate` carries the same zero-per-frame-allocation
  rule despite living outside `Recording/`.
- `tests/CLAUDE.md` — the `Ahjo.Vulkan.Ngx.Tests` table row and a bullet describing
  its gate split (`[gate:platform]` for the opt-in shim, `[gate:feature]` for the
  NVIDIA GPU).

`docs/ngx-notes.md` and `samples/HelloDlaa` are **Phase 3 and out of scope** — do not
start them. The exception message in step 8b links to `docs/ngx-notes.md`
deliberately, ahead of the file existing.

## 18. Verification

Run and record each; the PR body carries the table, the #217 shape.

| Check | Expectation |
|---|---|
| `dotnet build Ahjo.Vulkan.slnx -c Release` | 0 warnings, 0 errors (`TreatWarningsAsErrors`, `AnalysisLevel=latest`) |
| `dotnet test` (full sweep, Windows, no NGX SDK staged) | green; every NGX-suite skip carries `[gate:platform]` or `[gate:feature]` |
| `dotnet test` (Windows, NGX SDK staged, RTX 4070 Ti, `nvngx_dlss.dll` present) | the `DlssHardwareTests` group **executes** — quote the counts |
| `AHJO_VULKAN_TIER=validation dotnet test tests/Ahjo.Vulkan.Ngx.Tests` | quote the contract test's `declared=… observed=…` line; no validation-layer error during the end-to-end evaluate |
| `dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter "*DlssEvaluate*"` | both rows `Allocated: -`, minimum of 5 runs |
| `dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter "*CommandRecorder*|*PipelineBarrier*"` | unchanged vs `docs/benchmarks.md` — proves step 2's `Ahjo.Vulkan` edits moved nothing |
| `dotnet pack src/Ahjo.Vulkan.Ngx/Ahjo.Vulkan.Ngx.csproj -c Release` | contains **no** native files: 0 hits for `nvngx`, `dlss.dll`, `ahjo_ngx`, `.so`, `.lib` |
| `dotnet publish samples/AotSmoke -c Release -r win-x64 -p:PublishAot=true` | clean — the new package must not break the AOT lane even though the sample does not reference it |
| `dotnet build Ahjo.Vulkan.slnx` on WSL | clean (the new project joins the solution) |
| `vulkan-validation-reviewer` on the diff | run it; the three invariants of spec D2/D3/D4 are what it will look for |
| `bench-coverage-checker` on the diff | run it; `Memory/Allocator.cs` and `Lifecycle/Device.cs` are touched, so it will ask about the existing canaries — the row above is the answer |

**Report back rather than improvising** if: OPEN-1 or OPEN-2 is unanswered when you
reach step 1 or 2; the OPEN-3 probe in step 14 case 6 is inconclusive; the
`Evaluate_16` benchmark reports a non-`-` `Allocated` you cannot attribute; or
`ValidateInputs`'s `ImageUsage.None` carve-out turns out to hide a real failure on
hardware.
