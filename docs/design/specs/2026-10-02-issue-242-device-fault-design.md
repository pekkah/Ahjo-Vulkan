# Device-fault readout — `VK_EXT_device_fault` and `VK_KHR_device_fault` through `Device.TryGetDeviceFault`

- Issue: #242 — "Device: managed VK_EXT_device_fault readout (Ahjo #1749)"
- Consumer request: pekkah/ahjo#1749 (item 379: write the device-fault report on `DeviceLost`)
- Plan: [`../plans/2026-10-02-issue-242-device-fault.md`](../plans/2026-10-02-issue-242-device-fault.md)
- Lands on top of: [#120 device loss](2026-06-12-issue-120-device-loss-design.md) (`Device.IsLost`, the gate this read keys on), [#201 mesh shader](2026-08-22-issue-201-mesh-shader-design.md) and [#202 acceleration structure](2026-08-23-issue-202-acceleration-structure-design.md) (the gated device-extension block in `DeviceFunctionTable`)

**Revision note (same day).** The first draft wrapped EXT only. The maintainer
asked for **both** extensions: the dev host has an RTX 4070 Ti (EXT rev 2 + KHR
rev 1) next to an AMD iGPU (EXT only). This document replaces that draft. The
reason EXT-only was proposed (the then-installed 1.4.341 validation layer
did not know KHR) is gone. The host now runs SDK **1.4.363.0**, whose layer
validates both extensions (§4), so both paths run under validation armed.

**Revision note (SDK bump).** §2, §4, B, F, *Test strategy*, *Uncertainty*
and the plan's Step 0 are re-verified against the installed 1.4.363 SDK. The
earlier 1.4.341 layer-gap allowlist is removed.

## Problem

When a frame dies with `VK_ERROR_DEVICE_LOST`, the wrapper can tell the caller
*that* the device is lost (`Lifecycle/Device.cs:107`, `Device.IsLost`) but not
*why*. Vulkan has two answers to "why":

- `VK_EXT_device_fault`. After loss, `vkGetDeviceFaultInfoEXT` returns one
  description, arrays of faulting GPU virtual addresses and vendor fault
  codes, and optionally a vendor binary crash dump.
- `VK_KHR_device_fault`, its promotion. `vkGetDeviceFaultReportsKHR` returns a
  *queue* of per-fault entries, and `vkGetDeviceFaultDebugInfoKHR` returns the
  binary.

The Ahjo engine wants the report in the exception it throws on `DeviceLost`
and in a file next to the run's pipeline cache.

The wrapper has no door to either:

- The only bindings are the generated `[DllImport("vulkan-1")]`s:
  `src/Ahjo.Vulkan.Native/Generated/Vk.cs:2962` (EXT), and `:2246` / `:2249`
  (KHR). None can bind. The loader does not export extension entry points
  through `vulkan-1` (`Internal/InstanceFunctionTable.cs:6-9`, restated for
  device extensions at `Internal/DeviceFunctionTable.cs:37-40`). The only legal
  call goes through a `vkGetDeviceProcAddr` pointer.
- `Internal/DeviceFunctionTable.cs` resolves exactly two device extensions
  today: `VK_EXT_mesh_shader` (`:471-488`) and `VK_KHR_acceleration_structure`
  (`:495-532`). Nothing resolves any device-fault command.
- `Rendering/VulkanExtensions.cs` (the public name surface, 16 entries,
  `:12-142`) has neither extension name.
- The translation is not trivial enough to push onto every consumer.
  - EXT is a count/fill protocol over three arrays, with `VK_INCOMPLETE`,
    `char[256]` UTF-8 fields a driver is not forced to NUL-terminate, and a
    `ulong` binary size.
  - KHR is a *destructive* blocking queue, with `VK_TIMEOUT` and
    `VK_INCOMPLETE` meaning "more entries are waiting", plus a second
    lost-only call for the binary.

## Evidence

### 1. What the pinned headers generate (Vulkan-Headers 1.4.363, `Directory.Build.props:18`)

**EXT** — `VK_EXT_device_fault`, spec revision 2, `promotedto="VK_KHR_device_fault"`
(`native/include/vulkan/vk.xml:27444-27470`):

| Generated type | File | Notes |
|---|---|---|
| `vkGetDeviceFaultInfoEXT(VkDevice_T*, VkDeviceFaultCountsEXT*, VkDeviceFaultInfoEXT*)` | `Vk.cs:2962` | success `VK_SUCCESS, VK_INCOMPLETE`; errors `OUT_OF_HOST_MEMORY, UNKNOWN, VALIDATION_FAILED` (`vk.xml:18882`) |
| `VkDeviceFaultCountsEXT` | `Generated/VkDeviceFaultCountsEXT.cs` | `uint addressInfoCount`, `uint vendorInfoCount`, `ulong vendorBinarySize` |
| `VkDeviceFaultInfoEXT` | `Generated/VkDeviceFaultInfoEXT.cs` | `description` is `[InlineArray(256)] sbyte`; `VkDeviceFaultAddressInfoKHR* pAddressInfos`, `VkDeviceFaultVendorInfoKHR* pVendorInfos`, `void* pVendorBinaryData` |
| `VkPhysicalDeviceFaultFeaturesEXT` | `Generated/VkPhysicalDeviceFaultFeaturesEXT.cs` | `deviceFault`, `deviceFaultVendorBinary`; `IChainable<VkDeviceCreateInfo>` (`Chains/VkPhysicalDeviceFaultFeaturesEXT.Chain.g.cs:6`) |

**KHR** — `VK_KHR_device_fault`, spec revision 1 (`vk.xml:30952-30976`):

| Generated type | File | Notes |
|---|---|---|
| `vkGetDeviceFaultReportsKHR(VkDevice_T*, ulong timeout, uint* pFaultCounts, VkDeviceFaultInfoKHR*)` | `Vk.cs:2246` | success `VK_SUCCESS, VK_INCOMPLETE, VK_TIMEOUT`; errors as EXT (`vk.xml:18888`) |
| `vkGetDeviceFaultDebugInfoKHR(VkDevice_T*, VkDeviceFaultDebugInfoKHR*)` | `Vk.cs:2249` | success `VK_SUCCESS, VK_INCOMPLETE`; errors add `VK_ERROR_NOT_ENOUGH_SPACE_KHR` (`vk.xml:18895`) |
| `VkDeviceFaultInfoKHR` | `Generated/VkDeviceFaultInfoKHR.cs` | `uint flags`, `ulong groupId`, `description[256]`, **one** `faultAddressInfo`, **one** `instructionAddressInfo`, **one** `vendorInfo` |
| `VkDeviceFaultDebugInfoKHR` | `Generated/VkDeviceFaultDebugInfoKHR.cs` | `uint vendorBinarySize`, `void* pVendorBinaryData`; extended only by `VkDeviceFaultShaderAbortMessageInfoKHR` (`vk.xml:12062`, belongs to `VK_KHR_shader_abort`) |
| `VkDeviceFaultFlagBitsKHR` | `Generated/VkDeviceFaultFlagBitsKHR.cs` | `DEVICE_LOST 0x1`, `MEMORY_ADDRESS 0x2`, `INSTRUCTION_ADDRESS 0x4`, `VENDOR 0x8`, `WATCHDOG_TIMEOUT 0x10`, `OVERFLOW 0x20` |
| `VkPhysicalDeviceFaultFeaturesKHR` | `Generated/VkPhysicalDeviceFaultFeaturesKHR.cs` | `deviceFault`, `deviceFaultVendorBinary`, `deviceFaultReportMasked`, `deviceFaultDeviceLostOnMasked` |
| `VkPhysicalDeviceFaultPropertiesKHR` | `Generated/VkPhysicalDeviceFaultPropertiesKHR.cs` | `maxDeviceFaultCount` |

**Shared.** Both extensions use the same element structs. The `*EXT` element
names are registry aliases of the `*KHR` ones (`vk.xml:9986, 9992, 1036`), and
ClangSharp emits only the alias target. So `Generated/` holds a single
`VkDeviceFaultAddressInfoKHR`, a single `VkDeviceFaultVendorInfoKHR` (with its
own nested 256-`sbyte` `_description_e__FixedBuffer`), and a single
`VkDeviceFaultAddressTypeKHR` (values 0-6). EXT's arrays and KHR's per-entry
members point at exactly these types.

**Not shared.** The two feature structs are distinct sTypes.
`VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FAULT_FEATURES_EXT = 1000341000`
(`Generated/VkStructureType.cs:750`) and `…_FEATURES_KHR = 1000573000` (`:1124`).
Each extension needs its own feature struct.

The generated parameterless constructors set `sType`
(`Chains/VkDeviceFaultCountsEXT.Self.g.cs`, `…InfoEXT.Self.g.cs`,
`…InfoKHR.Self.g.cs`, `…DebugInfoKHR.Root.g.cs`). `default(T)`, a
`new T[n]` array, and a `stackalloc` skip them.

### 2. Spec text: what each command requires and promises

Sources:

- **VUIDs** come from the installed SDK's
  `C:\VulkanSDK\1.4.363.0\share\vulkan\registry\validusage.json`
  (`"api version": "1.4.363"`, generated 2026-09-18), queried 2026-10-02.
  The SDK's `vk.xml` in the same folder is byte-identical, apart from line
  endings, to the pinned `native/include/vulkan/vk.xml`, so the SDK and the
  generated bindings describe the same API.
- **Spec prose**, which `validusage.json` does not carry, comes from
  `KhronosGroup/Vulkan-Docs@e4e53e4`, fetched 2026-10-02:
  `chapters/debugging.adoc` ("Device Fault Diagnosis"),
  `chapters/features.adoc:8250-8313`, and `proposals/VK_KHR_device_fault.adoc`.
  Both extensions there are at the header revisions (EXT 2, KHR 1), and every
  VUID cited below is present in the 1.4.363 `validusage.json`.

| Command | Lost-state VU | Count/fill semantics | Repeatability |
|---|---|---|---|
| `vkGetDeviceFaultInfoEXT` | **`VUID-vkGetDeviceFaultInfoEXT-device-07336`: must be lost** (validusage.json) | Count call with `pFaultInfo == NULL`. The fill call passes sizes in and gets written counts back. `VK_INCOMPLETE` when a passed size is too small. VUIDs 07337-07339: a non-zero count with a non-null pointer must cover that many elements. | "subsequent calls … must return identical values" for the counts and the outputs. **Non-destructive.** |
| `vkGetDeviceFaultReportsKHR` | **None.** validusage.json 1.4.363 lists only `-device-parameter`, `-pFaultCounts-parameter`, `-pFaultInfo-parameter`, and `-pFaultCounts-arraylength` (a non-null `pFaultInfo` requires `*pFaultCounts > 0`). Proposal: "It may be called at any time, without the requirement for a VK_ERROR_DEVICE_LOST condition" | Count call with `pFaultInfo == NULL`, then fill. `VK_INCOMPLETE` "if pFaultCount is less than the number of fault reports available". `VK_TIMEOUT` when no fault occurred within `timeout`, "even … if timeout was zero". | **"Each individual fault report is returned exactly once."** Parallel calls each get "a unique set of reports". **Destructive.** |
| `vkGetDeviceFaultDebugInfoKHR` | **`VUID-vkGetDeviceFaultDebugInfoKHR-device-12383`: must be lost** (validusage.json 1.4.363) | Size query with `pVendorBinaryData == NULL`, then fill. The size is overwritten with the bytes written. | "subsequent calls … must return identical binary values". Non-destructive. |

Further spec facts the design relies on, all from `debugging.adoc`:

- **EXT vendor binary.** "If the vendor-specific crash dumps feature is not
  enabled, then implementations must set `pFaultCounts->vendorBinarySize` to
  zero and must not modify `pFaultInfo->pVendorBinaryData`." The KHR
  debug-info call has the same sentence for its own struct.
- **Blocking.** If no report is available, `vkGetDeviceFaultReportsKHR`
  "will block and wait until a fault occurs … or until the timeout has
  expired".
- **Flags gate members.** `VK_DEVICE_FAULT_FLAG_MEMORY_ADDRESS_KHR` /
  `_INSTRUCTION_ADDRESS_KHR` / `_VENDOR_KHR` say which of `faultAddressInfo` /
  `instructionAddressInfo` / `vendorInfo` "have been populated". An unset bit
  means the member is not meaningful.
- **Loss is terminal.** `VK_DEVICE_FAULT_FLAG_DEVICE_LOST_KHR`: "No subsequent
  entries will be returned for this device." After loss, the KHR queue is
  therefore finite.
- **Address precision.** `addressPrecision` "is a power of two value". The
  range is `reportedAddress & ~(precision-1)` through
  `reportedAddress | (precision-1)`.
- **Masked faults.** `deviceFaultReportMasked` lets KHR report faults that
  did *not* lose the device (`features.adoc:8290-8295`).

No device-fault command has a VUID requiring its `deviceFault` feature. Only
the extension gates the call.

### 3. Hardware

From triage on the issue: the AMD iGPU lists `VK_EXT_device_fault` rev 2 only,
and the RTX 4070 Ti lists EXT rev 2 and `VK_KHR_device_fault` rev 1. EXT is
the only path on the iGPU.

### 4. What the installed validation layer does with both extensions

The registered layer is SDK **1.4.363.0** (`C:\VulkanSDK\1.4.363.0\Bin\VkLayer_khronos_validation.json`,
`"api_version": "1.4.363"`). It is the only registered
`VK_LAYER_KHRONOS_validation`, and `vulkaninfo` reports instance 1.4.363. The
earlier 1.4.341.1 SDK is still on disk, but its layer is no longer registered.

What it checks, read from `KhronosGroup/Vulkan-ValidationLayers` at tag `vulkan-sdk-1.4.363.0`:

| Check | Location | Effect for this design |
|---|---|---|
| `VkPhysicalDeviceFaultFeaturesKHR` is a known `VkDeviceCreateInfo` pNext struct | `layers/vulkan/generated/stateless_validation_helper.cpp:1319` (member `Bool32` checks), `:9379` (allowed-pNext list) | Pushing the KHR feature struct is **clean**. The 1.4.341 "unknown VkStructureType" error no longer applies. |
| `PreCallValidateGetDeviceFaultReportsKHR` | `stateless_validation_helper.cpp:20959-20978` | Errors if `VK_KHR_device_fault` was not enabled. Validates `VUID-VkDeviceFaultInfoKHR-sType-sType` on **every** array element, `-pNext-pNext` per element, and `-pFaultCounts-arraylength`. No lost-state check. |
| `PreCallValidateGetDeviceFaultDebugInfoKHR` | `stateless_validation_helper.cpp:20980-21000` | sType, plus pNext limited to `VkDeviceFaultShaderAbortMessageInfoKHR` |
| `CoreChecks::PreCallValidateGetDeviceFaultInfoEXT` | `layers/core_checks/cc_device.cpp:1083-1091` | `LogError("VUID-vkGetDeviceFaultInfoEXT-device-07336", …)` when `!is_device_lost` |
| `CoreChecks::PreCallValidateGetDeviceFaultDebugInfoKHR` | `cc_device.cpp:1093-1101` | `LogError("VUID-vkGetDeviceFaultDebugInfoKHR-device-12383", …)` when `!is_device_lost` |

Two consequences:

- **Both lost-state rules are enforced by the layer.** The layer tracks
  `is_device_lost` from real `VK_ERROR_DEVICE_LOST` returns. It knows nothing
  about the wrapper's `Device.MarkLost()` test seam. A test that marks the
  wrapper lost and then reads would therefore trip 07336 or 12383, and that
  is exactly what the forbidden-test rule prevents (*Test strategy*).
- **Every KHR element sType is checked.** A `new VkDeviceFaultInfoKHR[n]`
  left at sType 0 is a validation error. The explicit per-element sType
  (D, plan Step 6) is therefore under a real oracle.

### 5. The gated-entry-point mechanism is reusable as-is

`DeviceFunctionTable` already has the shape this needs:

- extension fields are null-initialized (`:302-312`);
- each block is gated on `IsExtensionEnabled(enabledExtensions, DeviceExtensionNames.X)`
  (`:471`, `:495`; implementation at `:580-591`);
- each command is resolved with `ResolveExtensionRequired` (`:600-606`),
  which throws a message naming the extension when the driver advertises it
  but returns no pointer (`:609-615`);
- the charter comment (`:31-53`) already admits non-`vkCmd*` device-level
  commands, as the acceleration-structure create/destroy/query pointers
  showed (`:44-50`).

`VulkanExtensions` routes names through `DeviceExtensionNames` literals (`:69`,
`:118-119`). `Lifecycle/KhronosExtensionNames.cs` has one entry (`KhrSwapchain`,
`:19`). Across `src/`, `samples/` and `tests/` the only match is its own
declaration (`:16`), so it does not get the new names.

### 6. How `IsLost` gets set — the gate this read depends on

`Device._lost` is a set-once `volatile bool` (`Lifecycle/Device.cs:45-50`).
`MarkLost()` (`:109`) is called from:

| Site | Context | Precision |
|---|---|---|
| `Internal/ResultExtensions.cs:107-108` → `Device.NotifyDeviceLossObserved()` (`Device.cs:116-128`) | every `ThrowIfFailed` / `ThrowIfErrored` failure with `VK_ERROR_DEVICE_LOST` (`vkQueueSubmit2`, `vkDeviceWaitIdle`, …) | **marks every live device** |
| `Sync/Fence.cs:87` (status), `:118` (wait) | `Owner?.MarkLost()` | exact |
| `Sync/TimelineSemaphore.cs:104` | `Owner?.MarkLost()` | exact |
| `Rendering/Swapchain.cs:590` | acquire/present `VK_ERROR_DEVICE_LOST` arm | exact |

`Device.cs:98-106` records the cost of the first row. In a multi-device
process, a healthy sibling is marked too.

#120 also set the post-loss blocking policy: "Post-loss waits return
immediately … some drivers historically stall infinite waits after a TDR"
(`Sync/Fence.cs:110-114`). `TimeSpan.ToVulkanTimeout()` (`Sync/WaitState.cs:48-55`)
maps negative spans to `ulong.MaxValue`, which means wait forever.

### 7. UTF-8 decode helpers that exist today

- `Internal/Utf8.cs:11-12` `Utf8.ToString(sbyte*)` is `Marshal.PtrToStringUTF8`.
  It is **unbounded**, so it is wrong for a `char[256]` that a driver may fill
  without a NUL.
- `Lifecycle/Instance.cs:409-419` `NameSlice` is the bounded idiom: a span over
  the inline array (`:414-415`), sliced at the first NUL. It is private and
  returns bytes.

The three `description` fields (EXT info, KHR info, vendor info) are distinct
nested types, so a shared helper has to take a span.

### 8. Result policy

`ResultPolicyData.g.cs:53-55` lists all three commands as multi-success. The
guard (`tests/Ahjo.Vulkan.Tests/ResultPolicyGuardTests.cs:51-90`) scans for
`vk…(` spellings. A call through a function-pointer local is not scanned, so
the readers must branch on the result sign themselves.

### 9. Consumers today

None in `src/`, `samples/` or `tests/`. A search for `DeviceFault` outside
`Generated/` folders returns nothing. The single external consumer is Ahjo's
`AhjoAppRunner` on `DeviceLost`, plus two Ahjo facts:

- a capability fact on the RTX 4070 Ti with validation armed and "no
  validation message". With the 1.4.363 layer this holds for either feature
  struct (§4), provided the host's layer is 1.4.363 or newer;
- a formatter fact over a **hand-built** report, so the report type must be
  constructible outside this assembly.

### 10. Hot-path surface

None is touched. `FrameRing`'s loss handling (`Pools/FrameRing.cs:422-431`)
only consults `IsLost`. The new code lives in `Lifecycle/` and `Internal/`.

## Decision

**Both extensions, one public read.**

- `DeviceFunctionTable` gets two gated blocks:
  - `VK_EXT_device_fault` → `vkGetDeviceFaultInfoEXT`;
  - `VK_KHR_device_fault` → `vkGetDeviceFaultReportsKHR` and `vkGetDeviceFaultDebugInfoKHR`.
- `Device` exposes:
  - `DeviceFaultApi` (`None` / `Ext` / `Khr`), which says which path a read will use;
  - `IsDeviceFaultEnabled`;
  - `TryGetDeviceFault(out DeviceFaultReport?)`;
  - `TryGetDeviceFault(TimeSpan timeout, out DeviceFaultReport?)`.
- **KHR is used whenever it is enabled; EXT otherwise.**
- The report is one object holding **N entries**. EXT always produces exactly
  one entry, and KHR produces one entry per drained fault. The report also
  carries a single vendor binary and an `IsIncomplete` flag.
- Both protocols, the selection, and the translation live in an internal
  static `DeviceFaultReader` that takes function pointers, so all of it is
  testable against fake `[UnmanagedCallersOnly]` functions.

### A. Entry points and names

- `DeviceExtensionNames`:
  - `ExtDeviceFault => "VK_EXT_device_fault"u8`, `GetDeviceFaultInfo => "vkGetDeviceFaultInfoEXT"u8`;
  - `KhrDeviceFault => "VK_KHR_device_fault"u8`, `GetDeviceFaultReports => "vkGetDeviceFaultReportsKHR"u8`, `GetDeviceFaultDebugInfo => "vkGetDeviceFaultDebugInfoKHR"u8`.
- `DeviceFunctionTable`:
  - fields `GetDeviceFaultInfo`, `GetDeviceFaultReports` and `GetDeviceFaultDebugInfo`;
  - each null-initialized;
  - one `if (IsExtensionEnabled(…))` block per extension, resolving through
    `ResolveExtensionRequired` (the #201 and #202 shape).

  The charter comment records the gating of each. EXT info and KHR debug-info
  are legal only on a lost device. KHR reports is legal at any time, but the
  wrapper calls it only after loss.
- `VulkanExtensions.ExtDeviceFault` and `VulkanExtensions.KhrDeviceFault`.

### B. Selection when both are enabled: KHR

`TryGetDeviceFault` reads through KHR when both KHR pointers are non-null, and
through EXT otherwise. The reasons:

1. EXT is `promotedto="VK_KHR_device_fault"` (`vk.xml:27444`). KHR is the
   successor the registry points callers at.
2. KHR carries strictly more information per fault. It adds `flags` (device
   lost, watchdog timeout, overflow), `groupId` (several entries from one
   fault), and a per-entry description. EXT's single report maps into the
   same entry shape (C); the reverse mapping would lose information.
3. The caller already chooses the path by what it enables. A device that
   enables only EXT reads EXT. No override parameter is needed, and none is
   added.

`Device.DeviceFaultApi` returns the path a read *will* take: `Khr`, `Ext`, or
`None`. `IsDeviceFaultEnabled` is `DeviceFaultApi != None`. Both mean **the
extension was enabled at `vkCreateDevice`**, not that the `deviceFault`
feature was turned on. Vulkan offers no post-create query, and no command
has a feature VUID (§2). This is the extension-only guard of
`MeshShaderSupport.PartialGuardNote` (`Internal/MeshShaderSupport.cs:35-39`).

**Naming.** The property is `IsDeviceFaultEnabled`, not the issue's
`SupportsDeviceFault`. In the wrapper, "supports" means *advertised*
(`PhysicalDevice.SupportsExtension`, `Lifecycle/PhysicalDevice.cs:121`), and
`Device.cs:31-37` says "support is not the same question" as enabled. The
`Is…` prefix matches `IsLost` (`Device.cs:107`).

### C. Managed shape

All types go in `Lifecycle/`:

```csharp
public sealed class DeviceFaultReport
{
    public DeviceFaultApi     Api          { get; init; }
    public DeviceFaultEntry[] Entries      { get; init; } = [];
    public byte[]             VendorBinary { get; init; } = [];
    public bool               IsIncomplete { get; init; }
}

public sealed class DeviceFaultEntry
{
    public string                   Description  { get; init; } = string.Empty;
    public DeviceFaultFlags         Flags        { get; init; }
    public ulong                    GroupId      { get; init; }
    public DeviceFaultAddressInfo[] AddressInfos { get; init; } = [];
    public DeviceFaultVendorInfo[]  VendorInfos  { get; init; } = [];
}

public readonly record struct DeviceFaultAddressInfo(
    DeviceFaultAddressType AddressType, ulong ReportedAddress, ulong AddressPrecision);
public readonly record struct DeviceFaultVendorInfo(
    string Description, ulong VendorFaultCode, ulong VendorFaultData);

public enum DeviceFaultApi { None = 0, Ext = 1, Khr = 2 }
[Flags] public enum DeviceFaultFlags : uint { None = 0, DeviceLost = 0x1, MemoryAddress = 0x2,
    InstructionAddress = 0x4, Vendor = 0x8, WatchdogTimeout = 0x10, Overflow = 0x20 }
public enum DeviceFaultAddressType { None = 0, ReadInvalid = 1, WriteInvalid = 2,
    ExecuteInvalid = 3, InstructionPointerUnknown = 4, InstructionPointerInvalid = 5,
    InstructionPointerFault = 6 }
```

**Mapping.**

| Source | `Entries` | Entry fields |
|---|---|---|
| EXT | exactly **one** | `Description` from `VkDeviceFaultInfoEXT.description`; `AddressInfos` and `VendorInfos` from the two arrays; `Flags = None`; `GroupId = 0` |
| KHR | one per drained `VkDeviceFaultInfoKHR`, in order of occurrence | `Description`, `Flags`, `GroupId` copied. `AddressInfos` = `[faultAddressInfo]` if `MemoryAddress` is set, then `[instructionAddressInfo]` if `InstructionAddress` is set (0, 1 or 2 elements). `VendorInfos` = `[vendorInfo]` if `Vendor` is set. |

- **Flags decide, not field contents.** A KHR member whose flag bit is clear
  is dropped even when its bytes are non-zero. Flattening the two KHR address
  members into one array loses nothing, because `AddressType` already says
  whether an element is a memory access (`Read/Write/ExecuteInvalid`) or an
  instruction pointer (`InstructionPointer*`).
- **EXT entries get no synthesized flags.** EXT has no flags, and inventing
  `DeviceLost` (or address/vendor bits) would be fabricating driver output.
  `Report.Api` is how a formatter knows which source it is looking at.
- **`VendorBinary` sits at report level.** EXT returns one binary per read,
  and KHR's `vkGetDeviceFaultDebugInfoKHR` returns one per device. It is
  opaque bytes beginning with `VkDeviceFaultVendorBinaryHeaderVersionOneKHR`
  (56 bytes, debugging.adoc). Parsing it is for vendor tools.
- **Two `sealed class`es, two `readonly record struct`s.** The aggregate and
  the entry hold arrays, where record equality would be reference equality.
  A class also lets `[NotNullWhen(true)] out DeviceFaultReport?` mean "no
  report". Public `init` setters with non-null defaults let Ahjo hand-build
  reports. The two leaf types are flat value data, with `DebugMessage`
  (`Lifecycle/DebugMessage.cs:11`) as precedent for a record struct holding a
  `string`.
- **Shadow enums.** `DeviceFaultFlags` and `DeviceFaultAddressType` are pinned
  by `ShadowEnumDriftTests` (#122) against the `_KHR` members, the only ones
  that exist as enum types. Unknown future values pass through numerically.
- **No formatter.** No `ToString` is provided; Ahjo owns presentation.

### D. The read

`Device.TryGetDeviceFault(TimeSpan timeout, out DeviceFaultReport? report)`:

1. `ObjectDisposedException.ThrowIf(_disposed, this)`. Precedent:
   `Memory/StagingBatch.cs:98`. Calling into a destroyed `VkDevice` is
   undefined behavior.
2. Validate the timeout. Throw `ArgumentOutOfRangeException` when
   `timeout < TimeSpan.Zero`, which includes `Timeout.InfiniteTimeSpan`, **and
   when `timeout.ToVulkanTimeout()` is `ulong.MaxValue`**. `ToVulkanTimeout`
   saturates any span whose nanosecond count overflows (about 292 years and
   up, `TimeSpan.MaxValue` included) to `UINT64_MAX`, which Vulkan treats as
   an infinite wait. This deliberately does **not** reuse `ToVulkanTimeout`'s
   negative-is-infinite mapping, or its saturation: an unbounded block after
   loss is exactly what #120 removed (`Fence.cs:110-114`). Both checks run
   before the extension and `IsLost` gates, so they throw on any device.
3. `report = null`. If `DeviceFaultApi == None`, return `false`.
4. If `!IsLost`, return `false`. This is the gate for VUID-07336 (EXT) and
   VUID-12383 (KHR debug info). It also keeps the destructive KHR reports call
   off healthy devices (E).
5. Under `lock (_faultReadLock)`, return
   `DeviceFaultReader.TryRead(in pointers, Handle, timeoutNs, out report)`,
   passing the value converted in step 2, which is now known finite. The lock exists because concurrent KHR readers would each get
   a disjoint subset of entries (§2). One lock on a cold path, the
   `_allocatorLock` shape (`Device.cs:44`), makes a read see everything not
   already drained.

`TryGetDeviceFault(out report)` forwards with **`DefaultDeviceFaultTimeout = 100 ms`**,
exposed as `public static readonly TimeSpan` on `Device` so the documentation
and callers can name it. EXT ignores the timeout. For KHR it bounds only the
first count call, the one that may block when no entry has been posted yet.
All later calls pass 0. The *Uncertainty* section explains why the default is
100 ms.

`DeviceFaultReader.TryRead` selects KHR when both KHR pointers are non-null,
otherwise EXT, and returns `false` with an `AhjoDiagnostics.Write(DiagnosticSeverity.Warning, "Device", …)`
naming the command and `VkResult` on any error that leaves nothing to report.
It never throws for driver results and never uses `ThrowIfFailed` /
`ThrowIfErrored` (§8). This is the `Device.Dispose` shape
(`Device.cs:1054-1057`): a forensic read on a crash path must not raise a
second exception over the one the caller is handling.

**EXT reader** (`ReadExt`):

1. Count call `fn(device, &counts, null)`. `counts` has `sType` set
   explicitly and `pNext = null`. A negative result ends the read.
2. Clamp each returned size to its cap: `MaxAddressInfos = 4096` (96 KiB),
   `MaxVendorInfos = 4096` (about 1.1 MiB) and `MaxVendorBinaryBytes = 64 MiB`.
   Allocate the three arrays at the clamped sizes, and write the clamped
   sizes **back into `counts`**, because VUIDs 07337, 07338 and 07339 tie each
   pointer to the count passed. The caps exist because the counts come from a
   driver on a crash path. A garbage count would otherwise turn into an
   out-of-memory or overflow throw, out of a method that must not throw. A
   conforming driver answers a clamped fill with `VK_INCOMPLETE`.
3. Make the fill call, **always**, even when every count is 0, because
   `description` arrives only here. A zero count gets a null pointer, which
   `fixed` over an empty array yields.
4. A negative result ends the read. On `VK_SUCCESS` or `VK_INCOMPLETE`, clamp
   each returned count to the allocated length and trim.
   `IsIncomplete = (r == VK_INCOMPLETE) || (a cap was applied in step 2)`.
   The second term covers a driver that answers `VK_SUCCESS` to a clamped
   fill.
5. **No retry.** Counts are identical across calls by spec (§2), so a retry
   cannot return more.

**KHR entries** (`ReadKhrEntries`, a drain loop):

1. Round 1 makes a count call `fn(device, timeoutNs, &count, null)`. Later
   rounds pass `0`.
2. If the result is negative, the round errors. If it is `VK_TIMEOUT` or
   `count == 0`, stop draining.
3. Request `n = min(count, MaxEntries - collected)`, where `MaxEntries = 1024`.
   Allocate `new VkDeviceFaultInfoKHR[n]` and set **every element's**
   `sType = VK_STRUCTURE_TYPE_DEVICE_FAULT_INFO_KHR`, `pNext = null`, because
   a `new T[n]` does not run the generated ctor.
4. Fill call `fn(device, 0, &n, p)`. If it is negative, the round errors.
   If it is `VK_TIMEOUT`, append nothing and stop draining: the written count
   is not trusted then, because a driver need not write `*pFaultCounts` when
   nothing was posted, and trusting the requested count would emit blank
   entries. Otherwise append `min(written, requested)` entries.
5. If the fill returned `VK_INCOMPLETE` (more entries are queued) and
   `collected < MaxEntries` and `round < MaxRounds` (`MaxRounds = 8`), go
   back to step 1. Otherwise stop. Reaching a cap while the driver still
   reports more sets `IsIncomplete`.

   Entries past the cap are **never requested, so they stay queued** in the
   driver. Nothing is drained and discarded.
6. **A round error after at least one entry was collected** does not discard
   them, because they are already gone from the driver queue. The report is
   returned with what was collected, `IsIncomplete = true`, and a sink
   warning. An error with nothing collected makes `TryRead` return `false`.

`VK_INCOMPLETE` therefore means the opposite on the two paths. EXT counts are
stable, so a retry is pointless. KHR is a queue, so a bounded drain is
correct. After a device-lost entry, "No subsequent entries will be returned"
(§2), so on a lost device the drain terminates on its own. The caps exist for
a driver that does not honor that.

**KHR vendor binary** (`ReadKhrDebugInfo`), always attempted after the entries
because the device is lost by the gate:

1. Size query with `sType` set, `pNext = null`, `vendorBinarySize = 0`,
   `pVendorBinaryData = null`.
2. Allocate `min(size, MaxVendorBinaryBytes)` bytes (the same 64 MiB cap
   as EXT) and write the clamped size back.
3. Fill. `VK_INCOMPLETE` keeps the written bytes and sets `IsIncomplete`, and
   so does a clamp in step 2.
4. **A debug-info error (`VK_ERROR_NOT_ENOUGH_SPACE_KHR` or any negative
   result) does not fail the report.** The entries are kept, `VendorBinary`
   is empty, and a sink warning is written.
5. `pNext` stays null. `VkDeviceFaultShaderAbortMessageInfoKHR` belongs to
   `VK_KHR_shader_abort`, which this issue does not wrap.

**Repeat calls differ by path, and the XML doc says so.** A second
`TryGetDeviceFault` on EXT returns the same report. On KHR it returns an empty
`Entries` (already drained) and the same `VendorBinary`. Neither path caches.

### E. KHR on a healthy device: deliberately not exposed

`vkGetDeviceFaultReportsKHR` is legal on a healthy device. With
`deviceFaultReportMasked` it reports faults the driver recovered from. That is
a different feature: a background fault-catcher thread or telemetry. It is
not part of the crash-path readout this issue asks for, and it would compete
with it, because every entry is returned exactly once. A poller that drains
masked faults would leave the post-loss read without the entries that
preceded the loss.

`TryGetDeviceFault` therefore gates both paths on `IsLost`. A healthy-device
poller is a follow-up that must decide how it shares the queue with this read
(see *Cross-links*). Two consequences of the gating:

- **(a)** For KHR, the multi-device `IsLost` false positive (§6) puts only the
  debug-info call at risk of a VUID violation. The reports call is legal
  either way.
- **(b)** Because the reports call is legal on a healthy device, the KHR
  protocol gets a **real-driver test**. The internal reader is called directly
  on a healthy RTX 4070 Ti with timeout 0 (*Test strategy*).

### F. Feature enablement stays with the caller

`DeviceDescription` and `DeviceFeatureChainConfigurer`
(`Lifecycle/DeviceFeatureChainConfigurer.cs:97-102`) need no change. The
caller pushes `VkPhysicalDeviceFaultFeaturesEXT` and/or
`VkPhysicalDeviceFaultFeaturesKHR` from `ConfigureFeatures` and sets
`deviceFault` (plus `deviceFaultVendorBinary` for a crash dump). This is the
recipe on `VulkanExtensions.ExtMeshShader` (`VulkanExtensions.cs:61-68`).

The wrapper does **not** track `deviceFaultVendorBinary`. `VendorBinary` is
whatever the size protocol reports, and both commands are required to report
0 when the feature is off (§2). Even if a driver ignored that, passing a
correctly sized buffer satisfies VUID-07339 and its KHR equivalent.

The `VulkanExtensions.KhrDeviceFault` remarks name the layer floor: a
validation layer older than 1.4.363 may not know the KHR feature struct. The
1.4.341 layer reports it as `VUID-VkDeviceCreateInfo-pNext-pNext` (unknown
sType). The wrapper does not suppress this.

### G. Allocation and AOT posture

`TryGetDeviceFault` **allocates by design**: arrays, strings, the report, and
the entries. It runs once, after the device is lost. That is outside the
per-frame invariant (`src/Ahjo.Vulkan/CLAUDE.md`, "Setup-time allocations …
are fine"), and `bench-coverage-checker` should not flag it. No hot directory
is touched, and neither is `FrameRing` (§10). The three new
`DeviceFunctionTable` fields have the same shape as the ten mesh/AS fields
already there. No benchmark is added.

AOT: `delegate* unmanaged` only, with no reflection and no dynamic code. The
test fakes are `[UnmanagedCallersOnly]` statics.

### Why not the alternatives

- **EXT only.** This was the first draft's choice. It is rejected because the
  maintainer asked for both and the RTX 4070 Ti exposes KHR. The draft's
  reason (no validation oracle for KHR on the then-installed 1.4.341 layer)
  disappeared with the 1.4.363 SDK (§4).
- **EXT preferred when both are enabled.** That would read the less
  informative, superseded path on hardware that has the better one. A caller
  who wants EXT behavior enables only EXT.
- **An override parameter to force the flavor.** Enabling the extension *is*
  the override. A second switch adds a state that cannot be validated (KHR
  forced on an EXT-only device).
- **Separate public methods per flavor (`TryGetDeviceFaultExt` / `…Khr`).**
  This pushes the protocol choice onto every consumer, and Ahjo would write
  the same selection code the wrapper can write once.
- **Report = EXT shape plus KHR-only extras.** EXT's one-report model cannot
  hold N KHR entries with per-entry flags and descriptions without discarding
  or concatenating them. The N-entry shape holds EXT exactly (one entry).
- **Synthesize `DeviceLost` (or address/vendor) flags on the EXT entry.** That
  fabricates driver output. `Api` already tells the formatter what it has.
- **Size the KHR array in one call from `VkPhysicalDeviceFaultPropertiesKHR.maxDeviceFaultCount`.**
  It adds a properties query and allocates the maximum every time.
  Count-then-fill is the documented pattern (proposal *Examples*), and the
  drain loop covers late arrivals.
- **KHR healthy-device polling through `TryGetDeviceFault`.** Rejected in E:
  it is a different feature, and the destructive semantics would make it
  compete with the crash read.
- **Accept `Timeout.InfiniteTimeSpan`, or a span that saturates to
  `UINT64_MAX`.** An unbounded block after loss is the #120 hang
  (`Fence.cs:110-114`). The caller can pass any finite span below about 292
  years.
- **Clamp the EXT counts and both binary sizes only to `Array.MaxLength`.**
  That stops an overflow, but a garbage count can still make the crash-path
  read throw `OutOfMemoryException`. Fixed caps far above any real report keep
  the read non-throwing and cost nothing in practice.
- **Default timeout 0.** It cannot hang, but it misses an entry the driver
  posts slightly after `VK_ERROR_DEVICE_LOST` surfaces, which is the
  proposal's "faults provoked asynchronously … not triggered until after the
  queue submit had returned". 100 ms is invisible on a crash path. Callers
  who want 0 pass it.
- **Return `false` when a KHR round errors after entries were collected.**
  Those entries are already drained from the driver, so `false` would destroy
  the only copy.
- **Fail the whole KHR report when the debug-info call fails.** Same
  reasoning: the entries are already drained, and the binary is optional
  extra detail.
- **Unbounded KHR drain loop.** A driver that keeps reporting `VK_INCOMPLETE`
  would spin forever. The caps leave the remainder queued rather than
  discarding it.
- **Read `deviceFaultVendorBinary` back from the create chain.** It is cheap
  (`PhysicalDevice.CreateDevice` already walks the chain at
  `Lifecycle/PhysicalDevice.cs:647`), but it would be the first per-feature
  read-back in a wrapper whose guards are deliberately extension-only, and
  the size protocol already gives the answer (F).
- **Query `deviceFaultVendorBinary` support via `vkGetPhysicalDeviceFeatures2`.**
  That answers "supported", not "enabled" (`Device.cs:31-37`).
- **Throw on a failed read.** A second exception would replace the caller's
  own `DeviceLost` handling.
- **Return `false` on `VK_INCOMPLETE`.** That throws away the only report a
  lost device will give. Partial data plus `IsIncomplete` is strictly more
  useful.
- **No lock; document disjoint subsets for concurrent callers.** That is
  correct but surprising. The lock costs nothing on a cold path.
- **Call the generated `[DllImport]`s.** They cannot bind (Problem).
- **Put the protocols inline in `Device`.** The success paths would then be
  testable only on a truly lost device, which no test can produce portably.
- **`SupportsDeviceFault` as the property name.** See B.
- **Add the names to `KhronosExtensionNames`.** That class is vestigial (§5).

## Invariants honored

1. **UTF-8.** Extension and command names are `"…"u8` literals in
   `DeviceExtensionNames`, passed through `Utf8Name.FromLiteral`. Decoding
   (driver to managed) goes through a bounded span helper.
2. **AOT.** See G.
3. **Zero per-frame allocations.** This is not a per-frame path (G).
4. **Generated code untouched.** No `tools/*.rsp` change and no regen.
5. **Warnings-as-errors.** No suppression is needed.

## Test strategy

Calling EXT info or KHR debug-info on a healthy device violates a VUID, and
real device loss cannot be produced portably. Coverage is split four ways.

**1. Driverless, fake-backed (`DeviceFaultReaderTests`).** The tests drive
`DeviceFaultReader` against `[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]`
statics, the `InstanceCreateTests.cs:138-148` pattern: one fake for EXT info,
one for KHR reports, one for KHR debug-info. Scenario state lives in static
fields of the one class. That is safe because the suite runs serially
(`tests/Ahjo.Vulkan.Tests/xunit.runner.json`: `parallelizeTestCollections: false`,
`maxParallelThreads: 1`). No `TestGate` is needed. The cases cover:

- both protocols;
- selection (both fakes supplied, so only KHR may run);
- flag-driven flattening;
- every sType/pNext;
- timeout routing (caller timeout on the first count call, 0 afterwards);
- the drain loop and its caps;
- a `VK_TIMEOUT` fill that leaves the count unwritten (no blank entries);
- the EXT count and binary caps and the KHR binary cap (a garbage size is
  passed as the cap and the report is incomplete);
- partial-drain errors;
- debug-info failure isolation;
- bounded and multi-byte UTF-8.

**2. Device-level, `[gate:driver]` / `[gate:feature]` (`DeviceFaultTests`).**

- No extension: `DeviceFaultApi.None` and `false`, including after `MarkLost`.
  That is safe because there is no pointer to call.
- Disposed device: `ObjectDisposedException`.
- Negative or infinite timeout: `ArgumentOutOfRangeException`.
- A timeout that saturates to `UINT64_MAX` (`TimeSpan.MaxValue`, and the
  first span past the nanosecond overflow): `ArgumentOutOfRangeException`. A
  large but representable span is accepted. The argument checks run before
  the extension and `IsLost` gates, so a healthy device with no device-fault
  extension is enough.
- EXT-only device: `Ext`.
- KHR-only device: `Khr`.
- Both enabled: `Khr`.
- A healthy device with either extension enabled: `TryGetDeviceFault` returns
  `false`. If the `IsLost` gate were missing, the read would proceed and
  return `true`, so this is a real oracle.

**3. Real-driver KHR protocol test.** This is the one place a device-fault
command actually reaches hardware. On a KHR-capable GPU (the 4070 Ti) with no
`deviceFaultReportMasked`, the test calls `DeviceFaultReader.ReadKhrEntries`
**directly** on the healthy device with timeout 0. It asserts a non-negative
result, expected `VK_TIMEOUT`, and zero entries. This is legal (§2: no
lost-state VU), and it proves the resolved pointer is callable with the
wrapper's struct layout. Under validation (4), the layer also checks
extension enablement, each element's sType and pNext, and the
`-pFaultCounts-arraylength` rule on the same call. It consumes nothing,
because a healthy device without masked reporting has no entries.

**4. Validation-armed tests (`[gate:validation]`). All must record zero
errors.**

- EXT-only device with `VkPhysicalDeviceFaultFeaturesEXT.deviceFault = 1`.
  This is the wrapper half of Ahjo's capability fact.
- KHR device with `VkPhysicalDeviceFaultFeaturesKHR.deviceFault = 1`.
- Both extensions with both feature structs.
- The real-driver reports call (3), under the validated instance on the
  4070 Ti.

**Minimum layer: 1.4.363.** That is the pinned header version
(`Directory.Build.props:18`) and the first layer verified to know
`VK_KHR_device_fault` (§4). The KHR validation tests **skip** on an older
layer and do not fail. They skip through a new `TestGate.RequireValidationLayer(minimumSpecVersion, reason)`
overload that reports `[gate:validation]`, using the layer's
`VkLayerProperties.specVersion` captured by the existing probe
(`tests/Shared/VulkanEnvironment.cs:252-283`). The reason for skipping: an
older layer reporting an unknown sType is a gap in the oracle, not a wrapper
defect. Turning it red would blame the wrapper for the host. The
classification is still honest, because the skip reason names the layer
version, and the PR's quoted run must show these tests *ran*, not skipped.
The EXT validation test needs no minimum and keeps the plain
`RequireValidationLayer()`. EXT is old enough that even the previous 1.4.341
layer's registry already carried it
(`C:\VulkanSDK\1.4.341.1\share\vulkan\registry\vk.xml:25811`).

**Explicitly forbidden tests.** No test calls any of the following on an
extension-enabled healthy device:

- `MarkLost()` followed by `TryGetDeviceFault()`;
- `ReadExt`;
- `ReadKhrDebugInfo`.

Each of these reaches a command whose valid usage requires a lost device
(VUID-07336, VUID-12383) on hardware that is not lost. They are the obvious
tests to write by mistake.

**Hardware-only, untested by this PR.** No portable test can produce a real
device loss, so these are not covered:

- the contents of a real report after loss, on either path;
- whether the KHR device-lost entry is posted before `VK_ERROR_DEVICE_LOST`
  surfaces;
- the KHR vendor binary;
- EXT's native call;
- masked faults.

**CI.** Wrapper tests are Windows-only (#32), and the hosted runner has
neither extension, so `[gate:feature]` skips are the expected CI outcome, as
with the mesh tier (`MeshShaderTests.cs:14-23`). The PR quotes a local
`AHJO_VULKAN_TIER=validation` run on the RTX 4070 Ti (EXT, KHR and both) and,
where available, the AMD iGPU (EXT).

## Uncertainty, stated

- **The 100 ms default timeout is a judgment, not a measurement.** Nothing in
  the spec says whether a driver posts the device-lost KHR entry before or
  after `VK_ERROR_DEVICE_LOST` reaches the application. The number only has
  to be bounded (no #120 hang), invisible on a crash path, and long enough
  for an asynchronously posted entry. It is a public, named value, so
  changing it after real captures is a one-line, documented change.
- **Driver behavior of the KHR reports call on a healthy 4070 Ti**, whether
  it answers `VK_TIMEOUT` as the spec says, is confirmed only when test 3
  runs.
- **Multi-device false positive.** In a process with more than one live
  `Device`, a context-free loss marks every device (§6). The EXT read and the
  KHR debug-info call could then reach a healthy sibling. This is accepted
  under the one-device target shape and documented on the method. A real fix
  is a change to #120's flag.
- **Concurrent KHR readers outside the wrapper** (a consumer calling the raw
  command on another thread) would split the queue with this read. The lock
  covers only wrapper callers, and the XML doc says so.

## Cross-links

- **Resolves** #242 (the wrapper half of pekkah/ahjo#1749). Releasing (`gh release create v*`, which ships all eight packages) is a separate step after merge.
- **Builds on** #120 (`Device.IsLost` / `MarkLost`, the diagnostics sink, the post-loss no-unbounded-wait policy) and on the #201 and #202 gated device-extension block.
- **Must land consistently with** #117 (result policy: `VK_INCOMPLETE` / `VK_TIMEOUT` are success codes, never `ThrowIfFailed`) and #122 (shadow-enum drift tests).
- **Constrained by** #32 (Windows-only wrapper tests) and #158 (`AHJO_VULKAN_TIER`, `[gate:*]` classification).
- **Follow-ups to file:**
  - KHR healthy-device fault polling (`deviceFaultReportMasked`), which must decide how a poller shares the destructive queue with this read (E).
  - `VK_KHR_shader_abort` messages via the `VkDeviceFaultDebugInfoKHR` chain.
