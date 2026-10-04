Paired with [../specs/2026-10-04-issue-246-physical-device-features-design.md](../specs/2026-10-04-issue-246-physical-device-features-design.md)

# Plan — issue #246: `PhysicalDevice.TryGetFeatures<T>`

Branch: `issue-246-physical-device-features` (already checked out, branched
from `main`). Nothing under `Generated/`, `tools/*.rsp` or `native/` changes.
No benchmark is added (spec, Evidence §8).

## Step 1 — `src/Ahjo.Vulkan/Lifecycle/PhysicalDevice.cs`: the query

**1a. Rename the private properties helper.** `QueryChained<T>` (`:388-399`)
becomes `QueryChainedProperties<T>`; update its two callers (`:310`, `:371`).
Body and XML doc unchanged except the `<summary>` names the properties root.

**1b. Add the private features helper**, directly after it:

```csharp
private void QueryChainedFeatures<T>(out T features)
    where T : unmanaged, IChainable<VkPhysicalDeviceFeatures2>
```

Same body shape as `QueryChainedProperties<T>`: `stackalloc byte[sizeof(VkPhysicalDeviceFeatures2) + Unsafe.SizeOf<T>() + 16]`,
`ChainBuilder.For<VkPhysicalDeviceFeatures2>(scratch)`, `Root()`,
`ref T node = ref chain.Push<T>()`, `Vk.vkGetPhysicalDeviceFeatures2(Handle, chain.Head)`,
`features = node`. Same `<remarks>` as the properties helper (sizing
arithmetic, no `Clear()` needed), and the same "private because calling it
ungated is the hazard" summary.

**1c. Add the three public overloads** in a new section
`// ---- Chained feature queries ----` placed after
`TryGetAccelerationStructureLimits` (ends `:511`) and before `CreateDevice`
(`:513`):

```csharp
public bool TryGetFeatures<T>(ReadOnlySpan<byte> utf8ExtensionName, out T features)
    where T : unmanaged, IChainable<VkPhysicalDeviceFeatures2>;
public bool TryGetFeatures<T>(Utf8Name extension, out T features)
    where T : unmanaged, IChainable<VkPhysicalDeviceFeatures2>;
public bool TryGetFeatures<T>(VulkanVersion minimumApiVersion, out T features)
    where T : unmanaged, IChainable<VkPhysicalDeviceFeatures2>;
```

Bodies mirror `TryGetProperties<T>` (`:301-373`) line for line:
span overload → `SupportsExtension(name)` gate, else `features = default; return false;`;
`Utf8Name` overload → null check, then
`MemoryMarshal.CreateReadOnlySpanFromNullTerminated` into the span overload;
version overload → `ReadApiVersion() < minimumApiVersion.Packed` gate. Gate
passes → `QueryChainedFeatures(out features); return true;`.

**1d. XML docs.** The span overload carries the full doc; the other two
`<inheritdoc cref="TryGetFeatures{T}(ReadOnlySpan{byte}, out T)"/>` plus their
own `<param>`s, as the properties overloads do. Content:

- `<summary>`: reads one `VkPhysicalDeviceFeatures2` `pNext` struct — what
  this GPU **supports** — but only when it advertises the named extension.
- `<typeparam>`: the `IChainable<VkPhysicalDeviceFeatures2>` constraint is
  generated from `vk.xml` `structextends`; `sType` is written from `T.SType`.
- `<remarks>` paragraphs, in order:
  1. **Supported, not enabled.** This is the pre-`vkCreateDevice` question.
     Use it to decide which bits to set in `DeviceDescription.ConfigureFeatures`
     — a bit the device does not report fails `vkCreateDevice` with
     `VK_ERROR_FEATURE_NOT_PRESENT`. It says nothing about what a created
     `Device` enabled.
  2. **What the gate means.** Same text as `TryGetProperties`'s, but note
     the `Instance.PickPhysicalDevice` SwiftShader failure was on a
     *features* read-back chain — this exact root.
  3. **Which overload.** Extension-only structs take a name. Core-promoted
     structs take a version — and `VkPhysicalDeviceVulkan11Features` is Vulkan
     **1.2** (the "11" names the feature set), `…Vulkan12Features` 1.2,
     `…Vulkan13Features` 1.3, `…Vulkan14Features` 1.4. A struct that is *both* extension-owned and core-promoted (e.g.
     `VkPhysicalDeviceShaderDrawParametersFeatures`, `VK_KHR_shader_draw_parameters`
     → Vulkan 1.1) takes the version overload, because a device supporting it
     through promotion need not keep advertising the extension. Copy the
     properties doc's wording (`PhysicalDevice.cs:268-272`).
  4. **Query the struct you will push.** `VK_EXT_device_fault` and
     `VK_KHR_device_fault` have separate feature structs with separate
     `sType`s; enable a bit in the one the query returned it from. Do not
     assume one adapter's two structs agree.
  5. **Cost.** Copy the `TryGetProperties` cost paragraph with
     `vkGetPhysicalDeviceFeatures2` substituted. Setup-time, not cached.
  6. **`pNext` is null** on the returned struct (tail node).
  7. `<code>` example — the #246 recipe:

     ```csharp
     bool vendorBinary =
         gpu.TryGetFeatures<VkPhysicalDeviceFaultFeaturesEXT>(VulkanExtensions.ExtDeviceFault, out var fault)
         && fault.deviceFaultVendorBinary != 0;
     // …later, in ConfigureFeatures (captures vendorBinary):
     ref var f = ref chain.Push<VkPhysicalDeviceFaultFeaturesEXT>();
     f.deviceFault             = 1;
     f.deviceFaultVendorBinary = vendorBinary ? 1u : 0u;
     ```

     Plus one sentence: copy the bits you want, do not assign the whole
     queried struct into the pushed slot (it would enable every supported
     bit, and the habit breaks once the slot is not the chain tail).

## Step 2 — `src/Ahjo.Vulkan/Rendering/VulkanExtensions.cs`: fix the stale sentence

- `ExtDeviceFault` remarks (`:148-156`). Keep the enable recipe. Replace
  *"The extension alone is not enough, and the wrapper cannot check the
  feature — Vulkan exposes no post-`vkCreateDevice` feature query."* with:
  the extension alone is not enough; `deviceFault` is guaranteed wherever the
  extension is advertised, `deviceFaultVendorBinary` is optional — read it
  first with
  `PhysicalDevice.TryGetFeatures<VkPhysicalDeviceFaultFeaturesEXT>(ExtDeviceFault, out …)`
  and set it only when reported. Keep: what the wrapper cannot see is the
  **enabled** state after `vkCreateDevice`, which is why
  `Device.DeviceFaultApi` is extension-only.
- `KhrDeviceFault` remarks (`:165-172`). Same rewrite with
  `VkPhysicalDeviceFaultFeaturesKHR` / `KhrDeviceFault`, plus: query the KHR
  struct, not the EXT one — they are separate structs. The
  `deviceFaultReportMasked` sentence (`:178-181`) gains: support is readable
  through the same query.
- `<see cref>` targets: `PhysicalDevice.TryGetFeatures{T}(Utf8Name, out T)`.

## Step 3 — `src/Ahjo.Vulkan/Memory/ChainBuilder.cs:43`

Replace `// hand chain.Head to vkGetPhysicalDeviceFeatures2 (or device.GetFeatures(...))`
with a comment that the chain head goes to the Vulkan call, and that to
*read* one features struct use `PhysicalDevice.TryGetFeatures<T>`. Doc-comment
change only.

## Step 4 — `src/Ahjo.Vulkan/Lifecycle/DeviceFeatureChainConfigurer.cs`

Add a third `<para>` to `<remarks>` (after `:63`, before `</remarks>`): an optional extension
feature bit should be set only when the GPU reports it. Query it with
`PhysicalDevice.TryGetFeatures<T>` **before** `CreateDevice`, because the
configurer does not receive the `PhysicalDevice`; capture the result (a
closure is fine at setup time) or pick between static configurers. No
signature change.

## Step 5 — Tests

### 5a. New `tests/Ahjo.Vulkan.Tests/PhysicalDeviceFeaturesTests.cs`

`public sealed unsafe class PhysicalDeviceFeaturesTests(ITestOutputHelper output)`,
class `<remarks>` laid out like `PhysicalDevicePropertiesTests` (which tiers
run where; `[gate:feature]` skips on CI are expected).

Helpers (`internal static` so 5b can reuse them):

- `internal readonly record struct AdapterSnapshot(PhysicalDevice Gpu, string Name, uint ApiVersion, uint GraphicsFamily, bool HasGraphics, bool HasExtFault, bool HasKhrFault, VkPhysicalDeviceVulkan11Features F11, VkPhysicalDeviceVulkan12Features F12, VkPhysicalDeviceVulkan13Features F13, VkPhysicalDeviceVulkan14Features F14)`.
- `internal static List<AdapterSnapshot> WalkAdapters(Instance instance)`:
  an all-`false` `PickPhysicalDevice` walk (the Ahjo `SnapshotAdapters`
  pattern). Copy every feature struct **by value inside the callback** (the
  view's refs die at return). Catch `VulkanException` with
  `VK_ERROR_INITIALIZATION_FAILED` only `when (list.Count > 0)`. `F14` is
  `default` when `ApiVersion < V1_4`.
- `AssertBodyEqual<T>(in T expected, in T actual) where T : unmanaged`:
  byte compare from offset `sizeof(VkBaseOutStructure)` (16) to the end. The
  picker's copy has a non-null `pNext`; the query's is null. Its doc must say why
  trailing padding is safe to compare: both sides are zeroed before the driver
  writes (`ChainBuilder.Reserve` clears each slot, `Memory/ChainBuilder.cs:156`;
  the picker clears its scratch, `Lifecycle/Instance.cs:282`).
- `AssertAllZero<T>` — copy from `PhysicalDevicePropertiesTests.cs`.
- `CreateValidatedInstance` / `AssertNoValidationErrors` — copy from
  `DeviceFaultTests.cs:326-343`.

Cases:

| # | Name | Gate | Asserts |
|---|---|---|---|
| 1 | `TryGetFeatures_CoreStructs_MatchPickerView` | `[gate:driver]` | Every adapter: `TryGetFeatures<VkPhysicalDeviceVulkan11Features>(V1_2)` and `<…12Features>(V1_2)` → `== ApiVersion >= V1_2`; `<…13Features>(V1_3)` → `== ApiVersion >= V1_3` (every adapter on this host is 1.4, so all three return `true` here). On `true`: `sType == T.SType`, `pNext == null`, `AssertBodyEqual` against the snapshot. This is the "driver actually filled it" oracle. |
| 2 | `TryGetFeatures_Vulkan14_VersionGate_MatchesDeviceApiVersion` | `[gate:driver]` | Every adapter: result `== ApiVersion >= V1_4`; on `true`, `AssertBodyEqual` against `F14`; on `false`, `AssertAllZero`. |
| 3 | `TryGetFeatures_DeviceFaultStructs_GateMatchesSupportsExtension` | `[gate:driver]` | Every adapter, both structs, both overloads (`Utf8Name` and `"…"u8`): result `== gpu.SupportsExtension(…)`; on `false` `AssertAllZero`; on `true` `sType == T.SType`, `pNext == null`. `output.WriteLine` one line per adapter: name, EXT `deviceFault`/`deviceFaultVendorBinary`, KHR all four bits or "not advertised". |
| 4 | `TryGetFeatures_NullOrEmptyName_ReturnsFalse` | `[gate:driver]` | `default(Utf8Name)` and `ReadOnlySpan<byte>.Empty` → `false`, all-zero. |
| 5 | `TryGetFeatures_UnadvertisedName_ReturnsFalse` | `[gate:driver]` | `"VK_EXT_this_does_not_exist"u8` with `VkPhysicalDeviceFaultFeaturesEXT` → `false`, all-zero. |
| 6 | `DeviceFault_RequiredFeature_ReportedWhereverAdvertised` | `[gate:feature]` | `TestGate.RequireDeviceFeature(any adapter has EXT or KHR, …)`. Every EXT adapter: `deviceFault == 1` (`vk.xml:27467`). Every KHR adapter: `deviceFault == 1` (`vk.xml:30973`). |
| 7a | `TryGetFeatures_CoreAndExt_UnderValidation_Clean` | `[gate:validation]` | Plain `TestGate.RequireValidationLayer()` (EXT needs no floor). Validated instance; every adapter: case 1's three core queries and the EXT fault-struct query. `AssertNoValidationErrors`. Log the layer version. |
| 7b | `TryGetFeatures_Khr_UnderValidation_Clean` | `[gate:validation]` | `TestGate.RequireValidationLayer(DeviceFaultTests.MinKhrLayer, "The KHR device-fault feature struct needs a layer that knows VK_KHR_device_fault.")`. Validated instance; every adapter: the KHR fault-struct query (gated out on adapters without the extension). `AssertNoValidationErrors`. |

### 5b. `tests/Ahjo.Vulkan.Tests/DeviceFaultTests.cs`

- `private const uint MinKhrLayer` (`:41`) → `internal const uint MinKhrLayer`.
- New helper `private static Device CreateFaultDeviceOn(PhysicalDevice gpu, uint family, bool khr, uint vendorBinary)`:
  `Extensions = [khr ? VulkanExtensions.KhrDeviceFault : VulkanExtensions.ExtDeviceFault]`;
  a capturing `ConfigureFeatures` lambda pushing the matching struct with
  `deviceFault = 1`, `deviceFaultVendorBinary = vendorBinary`. It does **not**
  catch `VK_ERROR_FEATURE_NOT_PRESENT` — that exception is the failure this
  test exists to rule out.
- New cases:

| # | Name | Gate | Asserts |
|---|---|---|---|
| 8 | `ExtVendorBinary_EnabledExactlyWhenReported_CreatesDevice` | `[gate:feature]` | `WalkAdapters`; `RequireDeviceFeature(any adapter with HasExtFault && HasGraphics, ExtSkipReason)`. For each such adapter: query EXT, create with `vendorBinary = queried`, assert `DeviceFaultApi.Ext`, dispose. `output.WriteLine($"{name}: deviceFaultVendorBinary={v}")`. This is #246's "done when" in test form. |
| 9 | `ExtVendorBinary_EnabledExactlyWhenReported_UnderValidation_Clean` | `[gate:validation]` + `[gate:feature]` | Case 8 under `CreateValidatedInstance`, plain `RequireValidationLayer()` (EXT needs no floor). Zero errors. |
| 10 | `KhrVendorBinary_EnabledExactlyWhenReported_UnderValidation_Clean` | `[gate:validation]` (`MinKhrLayer`) + `[gate:feature]` | Same for every KHR adapter with `khr: true`. `DeviceFaultApi.Khr`. Zero errors. |

- Update the `TryCreateFaultDevice` doc (`:367-379`): `deviceFaultVendorBinary`
  is still not requested **here**, because this helper screens by extension
  only; cases 8-10 request it after `PhysicalDevice.TryGetFeatures<T>`.
- Update the class `<remarks>` tier paragraph to mention cases 8-10.

**Forbidden (state it in both classes' remarks):** no test enables
`deviceFaultVendorBinary` on an adapter that reports 0. That would test the
driver, and a failing `vkCreateDevice` pollutes validation captures.

## Step 6 — Docs (no benchmarks)

- `docs/aot-notes.md:14`: "by `PhysicalDevice.TryGetProperties<T>`" →
  "by `PhysicalDevice.TryGetProperties<T>` and `TryGetFeatures<T>`".
- `docs/benchmarks.md:308` paragraph: add `PhysicalDevice.TryGetFeatures<T>`
  to the list. Its query counts match `TryGetProperties` with
  `vkGetPhysicalDeviceFeatures2` substituted: version overload two native
  calls and no allocation, name overloads three calls and one pooled rent.
- `.claude/agents/bench-coverage-checker.md:73`: add `TryGetFeatures<T>` to
  the `Lifecycle/PhysicalDevice.cs` member list and
  `vkGetPhysicalDeviceFeatures2` to the "issues a native …" clause.
- READMEs: no change. Neither `README.md` nor `src/Ahjo.Vulkan/README.md`
  lists `PhysicalDevice` queries (no hit for `TryGetProperties` in either).
  The repo has no CHANGELOG; release notes come from the GitHub release.
- `docs/ci-coverage.md`: no change. No new `[gate:*]` class is introduced;
  every new case uses `driver`, `feature` or `validation`.

## Step 7 — Verify

- `dotnet build Ahjo.Vulkan.slnx`: clean, warnings are errors.
- `dotnet test tests/Ahjo.Vulkan.Tests`.
- `AHJO_VULKAN_TIER=validation dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~PhysicalDeviceFeatures|FullyQualifiedName~DeviceFault|FullyQualifiedName~PhysicalDeviceProperties"`
  on this host. Quote:
  - the contract test's `declared=… observed=…` line;
  - the validation layer version (must be ≥ 1.4.363, so cases 7b and 10 ran);
  - case 3's per-adapter lines. They must match the spec's Evidence §4 table:
    RTX 4070 Ti EXT `1/1`, KHR `1/1/0/0`; AMD EXT `1/0`, KHR not advertised;
  - case 8's lines: 4070 Ti `deviceFaultVendorBinary=1`, AMD `=0`. Both
    branches must have run.
- If case 7b records an error on the AMD iGPU, stop and report it. The gate
  is supposed to keep the KHR struct away from that adapter.
- Optional AOT smoke:
  `dotnet publish samples/AotSmoke/AotSmoke.csproj -c Release -r win-x64 -p:IlcUseEnvironmentalTools=true`.
- Reviewers: `vulkan-validation-reviewer` (gate correctness, sType/pNext on
  the read chain, the version gates per struct); `bench-coverage-checker`
  (should conclude no hot path is touched; Step 6 rows).

## Step 8 — PR and release

- Commit / PR title: `PhysicalDevice: add TryGetFeatures<T> pre-create feature query`.
  Body: `Closes #246`. Say it unblocks #244 (`deviceFaultReportMasked`
  gate) and #172 (`shaderDrawParameters` gate) without resolving either,
  and that no default changes.
- **Name the delta from the issue's suggested shape for Ahjo:**
  - `TryGetFeatures<T>` (`bool` + `out`), not `GetFeatures<T>()`;
  - no `PhysicalDeviceInfo.DeviceFaultFeatures` and no
    `SupportsDeviceFault*` bools. Inside the picker, call
    `info.Device.TryGetFeatures<…>(…)`;
  - EXT and KHR are queried separately: query the struct you push;
  - Ahjo's static configurer switch (`RhiDevice.cs:256-262`) needs either a
    third axis or one capturing lambda. That is Ahjo's choice.
  - Include the Step 1d snippet as the Ahjo recipe.
- Quote the Step 7 run.
- **Not breaking.** Additive public API only; the private rename is
  invisible.
- Releasing is a separate step after merge: one `v*` tag ships all eight
  packages via `gh release create`. See OPEN-1.

## Open items

- **OPEN-1 — release version and timing.** #246's "done when" requires a
  tagged release. The last tag is `v0.15.0`, and this change is additive.
  The maintainer decides the version (presumably `v0.16.0`) and whether it
  ships alone or with other merged work. Not part of the implementer's
  steps.
