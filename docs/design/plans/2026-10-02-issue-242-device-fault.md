Paired with [../specs/2026-10-02-issue-242-device-fault-design.md](../specs/2026-10-02-issue-242-device-fault-design.md). Read it first; this plan only says *how*.

# Implementation plan — issue #242: `VK_EXT_device_fault` + `VK_KHR_device_fault` readout

Branch: `issue-242-device-fault` (from `main`).

This is managed-surface work only. Nothing under `src/*/Generated/`, `native/`
or `tools/*.rsp` changes, and there is no regen. Every native symbol already
exists. The commands are at `Vk.cs:2962` (EXT) and `Vk.cs:2246` / `:2249`
(KHR). These `[DllImport]`s are **never called**, because they cannot bind.
The structs are `VkDeviceFaultCountsEXT`, `VkDeviceFaultInfoEXT`,
`VkDeviceFaultInfoKHR`, `VkDeviceFaultDebugInfoKHR`,
`VkDeviceFaultAddressInfoKHR` and `VkDeviceFaultVendorInfoKHR`. The enums are
`VkDeviceFaultAddressTypeKHR` and `VkDeviceFaultFlagBitsKHR`. The feature
structs are `VkPhysicalDeviceFaultFeaturesEXT` and `…KHR`.

Out of scope:
- KHR healthy-device polling;
- `VK_KHR_shader_abort` chaining on `VkDeviceFaultDebugInfoKHR`;
- `VkPhysicalDeviceFaultPropertiesKHR` queries.

## Step 0 — Verification record (done; copy into the PR description)

Verified on 2026-10-02 against the **installed SDK 1.4.363.0**, which is now the only
registered `VK_LAYER_KHRONOS_validation` on the host. Quote the VUIDs verbatim in the XML docs.

- **VUIDs**, from `C:\VulkanSDK\1.4.363.0\share\vulkan\registry\validusage.json`
  (`"api version": "1.4.363"`):
  - **EXT**: `VUID-vkGetDeviceFaultInfoEXT-device-07336` (must be lost),
    `-pFaultCounts-07337` / `-07338` / `-07339`,
    `VUID-VkDeviceFaultCountsEXT-sType-sType`,
    `VUID-VkDeviceFaultInfoEXT-sType-sType`, and both `-pNext-pNext`.
  - **KHR reports**: only `VUID-vkGetDeviceFaultReportsKHR-device-parameter`,
    `-pFaultCounts-parameter`, `-pFaultInfo-parameter`, and
    `-pFaultCounts-arraylength` (a non-null `pFaultInfo` requires
    `*pFaultCounts > 0`). There is **no lost-state VU**.
    `VUID-VkDeviceFaultInfoKHR-sType-sType` / `-pNext-pNext` apply to each
    element.
  - **KHR debug info**: `VUID-vkGetDeviceFaultDebugInfoKHR-device-12383` (must
    be lost), `-pDebugInfo-parameter`,
    `VUID-VkDeviceFaultDebugInfoKHR-sType-sType`, and `-pNext-pNext` (null or
    `VkDeviceFaultShaderAbortMessageInfoKHR`).
- **Registry**: `C:\VulkanSDK\1.4.363.0\share\vulkan\registry\vk.xml` is
  byte-identical, apart from line endings, to the pinned
  `native/include/vulkan/vk.xml`. EXT is at `:27444`, KHR at `:30952`, and the
  KHR commands at `:18889` / `:18896`.
- **Spec prose**, which `validusage.json` does not carry, from
  `KhronosGroup/Vulkan-Docs@e4e53e4` (`chapters/debugging.adoc`, *Device
  Fault Diagnosis*; `proposals/VK_KHR_device_fault.adoc`):
  - the reports call blocks up to `timeout`;
  - it returns `VK_TIMEOUT` when no fault is available, even at timeout 0;
  - it returns each report exactly once;
  - EXT and KHR debug-info report a vendor binary size of zero when the
    vendor-binary feature is off;
  - EXT counts are identical across calls;
  - `addressPrecision` is a power of two.
- **Validation layer behavior**, from `KhronosGroup/Vulkan-ValidationLayers@vulkan-sdk-1.4.363.0`:
  - `VkPhysicalDeviceFaultFeaturesKHR` is a known `VkDeviceCreateInfo` pNext
    struct (`layers/vulkan/generated/stateless_validation_helper.cpp:1319`, `:9379`);
  - `PreCallValidateGetDeviceFaultReportsKHR` (`:20959-20978`) checks
    extension enablement and every element's sType/pNext;
  - `PreCallValidateGetDeviceFaultDebugInfoKHR` (`:20980-21000`);
  - `CoreChecks` enforce 07336 and 12383 from the layer's own
    `is_device_lost` (`layers/core_checks/cc_device.cpp:1083-1101`).

## Step 1 — `src/Ahjo.Vulkan/Internal/DeviceExtensionNames.cs`

Add after `CmdCopyAccelerationStructure` (`:50`):

```csharp
public static ReadOnlySpan<byte> ExtDeviceFault          => "VK_EXT_device_fault"u8;
public static ReadOnlySpan<byte> GetDeviceFaultInfo      => "vkGetDeviceFaultInfoEXT"u8;

public static ReadOnlySpan<byte> KhrDeviceFault          => "VK_KHR_device_fault"u8;
public static ReadOnlySpan<byte> GetDeviceFaultReports   => "vkGetDeviceFaultReportsKHR"u8;
public static ReadOnlySpan<byte> GetDeviceFaultDebugInfo => "vkGetDeviceFaultDebugInfoKHR"u8;
```

Extend the `<remarks>` "Not every name here gates something" paragraph
(`:19-30`):
- `ExtDeviceFault` gates `vkGetDeviceFaultInfoEXT`, which is legal only on a
  lost device.
- `KhrDeviceFault` gates `vkGetDeviceFaultReportsKHR`, which is legal any time
  but called by the wrapper only after loss, and `vkGetDeviceFaultDebugInfoKHR`,
  which is legal only on a lost device.

## Step 2 — `src/Ahjo.Vulkan/Rendering/VulkanExtensions.cs`

Add after `KhrDeferredHostOperations` (`:140-141`):

```csharp
public static Utf8Name ExtDeviceFault => Utf8Name.FromLiteral(DeviceExtensionNames.ExtDeviceFault);
public static Utf8Name KhrDeviceFault => Utf8Name.FromLiteral(DeviceExtensionNames.KhrDeviceFault);
```

**`ExtDeviceFault` docs.**
- Summary: "VK_EXT_device_fault — device-level. Enables `Device.TryGetDeviceFault`
  after device loss (used when `KhrDeviceFault` is not also enabled)."
- Remarks, the recipe: add the name to `DeviceDescription.Extensions`, then
  push `VkPhysicalDeviceFaultFeaturesEXT` from `ConfigureFeatures` with
  `deviceFault = 1`, and `deviceFaultVendorBinary = 1` for a crash dump. The
  extension alone is not enough, and the wrapper cannot check the feature.

**`KhrDeviceFault` docs.**
- Summary: "VK_KHR_device_fault — device-level. Promotion of
  `ExtDeviceFault`; when enabled, `Device.TryGetDeviceFault` reads through it
  in preference to EXT."
- Remarks:
  - The recipe uses **`VkPhysicalDeviceFaultFeaturesKHR`**, a different sType
    from the EXT struct.
  - **Validation layer floor: 1.4.363.** It validates both KHR commands and
    the KHR feature struct. Older layers (1.4.341 is one) do not know the
    extension, and report the KHR feature struct as
    `VUID-VkDeviceCreateInfo-pNext-pNext` ("unknown VkStructureType").
  - `deviceFaultReportMasked` / `deviceFaultDeviceLostOnMasked` are the
    caller's to set. The wrapper's read is post-loss only; healthy-device
    polling is not wrapped.

## Step 3 — `src/Ahjo.Vulkan/Internal/DeviceFunctionTable.cs`

- **3a. Fields.** Add a new section after `CmdCopyAccelerationStructure`
  (`:196-198`), before `// ---- Pipeline barriers (sync2) ----`:

  ```csharp
  // ---- Device fault (VK_EXT_device_fault / VK_KHR_device_fault) ----

  /// <summary><c>vkGetDeviceFaultInfoEXT</c>. Null unless VK_EXT_device_fault
  /// was enabled. Legal only on a lost device (VUID-…-07336).</summary>
  public readonly delegate* unmanaged[Stdcall]<
      VkDevice_T*, VkDeviceFaultCountsEXT*, VkDeviceFaultInfoEXT*, VkResult> GetDeviceFaultInfo;

  /// <summary><c>vkGetDeviceFaultReportsKHR</c>. Null unless VK_KHR_device_fault
  /// was enabled. Legal at any time, blocks up to its timeout, and drains:
  /// each report is returned exactly once.</summary>
  public readonly delegate* unmanaged[Stdcall]<
      VkDevice_T*, ulong, uint*, VkDeviceFaultInfoKHR*, VkResult> GetDeviceFaultReports;

  /// <summary><c>vkGetDeviceFaultDebugInfoKHR</c>. Null unless VK_KHR_device_fault
  /// was enabled. Legal only on a lost device (VUID-…-12383).</summary>
  public readonly delegate* unmanaged[Stdcall]<
      VkDevice_T*, VkDeviceFaultDebugInfoKHR*, VkResult> GetDeviceFaultDebugInfo;
  ```
- **3b. Null-init.** Add all three after the acceleration-structure
  null-inits (`:306-312`).
- **3c. Two gated blocks.** Add them after the acceleration-structure block
  (`:532`): one `if (IsExtensionEnabled(enabledExtensions, DeviceExtensionNames.ExtDeviceFault))`
  resolving `GetDeviceFaultInfo`, and one for `KhrDeviceFault` resolving
  `GetDeviceFaultReports` and `GetDeviceFaultDebugInfo`. Each resolves through
  `ResolveExtensionRequired(Utf8Name.FromLiteral(DeviceExtensionNames.<Cmd>), DeviceExtensionNames.<Ext>)`.
- **3d. Charter comment.** In the third `<item>` (`:31-53`), add after the
  ray-query sentence: "`VK_EXT_device_fault` and `VK_KHR_device_fault` are
  the third and fourth blocks (issue #242). Two of their three pointers (EXT
  info, KHR debug-info) are legal only on a lost device; KHR reports is legal
  at any time but destructive. The wrapper calls all three only after
  `Device.IsLost`."

## Step 4 — `src/Ahjo.Vulkan/Internal/Utf8.cs`

- Add `public static string FromBounded(ReadOnlySpan<sbyte> buffer)`, with
  `using System.Text;`. It decodes up to the first NUL, or the whole span when
  there is none, via `Encoding.UTF8.GetString`. Invalid sequences become
  U+FFFD and nothing throws. An empty result is `string.Empty`.
- Update the class `<summary>` (`:5-8`) to say "debug-utils trampoline and
  the device-fault decode".
- Do not touch `Instance.NameSlice` (`Lifecycle/Instance.cs:409-419`).

## Step 5 — New public types under `src/Ahjo.Vulkan/Lifecycle/`

Use one file per type. Shapes are exactly as in spec §C.

- **5a. `DeviceFaultApi.cs`.** `public enum DeviceFaultApi { None = 0, Ext = 1, Khr = 2 }`.
  The docs say this is the path a read takes, and that KHR wins when both are
  enabled.
- **5b. `DeviceFaultFlags.cs`.** `[Flags] public enum DeviceFaultFlags : uint`
  with `None = 0`, `DeviceLost = 0x1`, `MemoryAddress = 0x2`,
  `InstructionAddress = 0x4`, `Vendor = 0x8`, `WatchdogTimeout = 0x10` and
  `Overflow = 0x20`. Each member's doc paraphrases its
  `VK_DEVICE_FAULT_FLAG_*_KHR` text from debugging.adoc. The type remarks say:
  "Always `None` on an EXT-sourced entry — EXT reports carry no flags; the
  wrapper does not synthesize them."
- **5c. `DeviceFaultAddressType.cs`.** Underlying `int`, members `None = 0`
  through `InstructionPointerFault = 6`, copied from
  `VkDeviceFaultAddressTypeKHR`.
- **5d. `DeviceFaultAddressInfo.cs`.**
  `public readonly record struct DeviceFaultAddressInfo(DeviceFaultAddressType AddressType, ulong ReportedAddress, ulong AddressPrecision);`
  The docs give the lower/upper bound formula from debugging.adoc and say
  `AddressPrecision` is a power of two.
- **5e. `DeviceFaultVendorInfo.cs`.**
  `public readonly record struct DeviceFaultVendorInfo(string Description, ulong VendorFaultCode, ulong VendorFaultData);`
- **5f. `DeviceFaultEntry.cs`.** `public sealed class DeviceFaultEntry` with
  `init` properties `Description` (default `string.Empty`), `Flags`,
  `GroupId`, `AddressInfos` (default `[]`) and `VendorInfos` (default `[]`).
  The remarks carry the mapping table from spec §C, including "KHR members
  whose flag bit is clear are omitted".
- **5g. `DeviceFaultReport.cs`.** `public sealed class DeviceFaultReport` with
  `init` properties `Api`, `Entries` (default `[]`), `VendorBinary` (default
  `[]`) and `IsIncomplete`. The remarks say:
  - It is publicly constructible, so formatter tests can hand-build one.
  - EXT always yields exactly one entry. KHR yields zero or more, in order of
    occurrence.
  - `VendorBinary` is opaque, starts with the 56-byte
    `VkDeviceFaultVendorBinaryHeaderVersionOneKHR`, and is empty unless the
    driver produced one (normally this needs `deviceFaultVendorBinary`).
  - `IsIncomplete`: the driver had more than was returned. This covers EXT or
    debug-info `VK_INCOMPLETE`, a KHR cap reached while more was queued, and a
    KHR round error after a partial drain.
  - No `ToString` / formatter is provided.

## Step 6 — `src/Ahjo.Vulkan/Internal/DeviceFaultReader.cs` (new)

```csharp
internal readonly unsafe struct DeviceFaultEntryPoints
{
    public readonly delegate* unmanaged[Stdcall]<VkDevice_T*, VkDeviceFaultCountsEXT*, VkDeviceFaultInfoEXT*, VkResult> Ext;
    public readonly delegate* unmanaged[Stdcall]<VkDevice_T*, ulong, uint*, VkDeviceFaultInfoKHR*, VkResult> KhrReports;
    public readonly delegate* unmanaged[Stdcall]<VkDevice_T*, VkDeviceFaultDebugInfoKHR*, VkResult> KhrDebugInfo;
    public DeviceFaultEntryPoints(/* the three */);
    public DeviceFaultApi Api => KhrReports != null && KhrDebugInfo != null ? DeviceFaultApi.Khr
                               : Ext != null ? DeviceFaultApi.Ext : DeviceFaultApi.None;
}

internal static unsafe class DeviceFaultReader
{
    internal const int  MaxKhrEntries        = 1024;
    internal const int  MaxKhrRounds         = 8;
    internal const uint MaxAddressInfos      = 4096;              // 96 KiB
    internal const uint MaxVendorInfos       = 4096;              // ~1.1 MiB
    internal const uint MaxVendorBinaryBytes = 64u * 1024 * 1024; // EXT and KHR

    internal static bool TryRead(in DeviceFaultEntryPoints eps, VkDevice_T* device, ulong timeoutNs,
                                 [NotNullWhen(true)] out DeviceFaultReport? report);

    internal static VkResult ReadExt(/*eps.Ext*/ fn, VkDevice_T* device, out DeviceFaultReport? report);

    internal static VkResult ReadKhrEntries(/*eps.KhrReports*/ fn, VkDevice_T* device, ulong timeoutNs,
                                            out DeviceFaultEntry[] entries, out bool incomplete);

    internal static VkResult ReadKhrDebugInfo(/*eps.KhrDebugInfo*/ fn, VkDevice_T* device,
                                              out byte[] binary, out bool incomplete);
}
```

Rules for the whole file:

- **No `vk…(` call spellings.** Calls go through `fn(…)` only.
- **No `ThrowIfFailed` / `ThrowIfErrored`.** Results are branched on with
  `(int)r < 0`.
- **Every native struct gets an explicit `sType`, and `pNext = null`.** This
  includes every `VkDeviceFaultInfoKHR` array element.
- **Descriptions decode via `Utf8.FromBounded(MemoryMarshal.CreateReadOnlySpan(ref x.description.e0, 256))`.**
  Put a comment next to the `256` naming `VK_MAX_DESCRIPTION_SIZE`.
- **Every driver-reported size is clamped to its cap, and the clamped value
  is written back** into the size field before the fill call. That covers
  the EXT address and vendor counts and both binary sizes. A clamp also sets
  `IsIncomplete` / `incomplete`, even if the driver answers `VK_SUCCESS`.

**`ReadExt`** follows spec §D "EXT reader" exactly:
1. Count call.
2. Clamp the three sizes to `MaxAddressInfos` / `MaxVendorInfos` /
   `MaxVendorBinaryBytes`, write all three back into `counts`
   (VUIDs 07337/07338/07339), and allocate at the clamped sizes.
3. Make the fill call, **always**, with null pointers for zero counts.
4. Clamp the returned counts to the allocated lengths and trim.
5. Build the report: `Api = Ext`, one `DeviceFaultEntry` (`Flags = None`,
   `GroupId = 0`), `VendorBinary` = the trimmed bytes, and
   `IsIncomplete = r == VK_INCOMPLETE || capped`.

There is no retry. A negative result returns `r` with `report = null`.

**`ReadKhrEntries`** follows spec §D "KHR entries":
1. The loop counter `round` starts at 0. Round 0's count call passes
   `timeoutNs`; every other call passes `0`.
2. Each round:
   - count call `fn(device, t, &count, null)`;
   - if the result is `VK_TIMEOUT` or `count == 0`, stop;
   - request `n = Math.Min(count, MaxKhrEntries - collected)`;
   - allocate `new VkDeviceFaultInfoKHR[n]` and set each element's
     sType/pNext;
   - fill call `fn(device, 0, &n, p)`;
   - if the fill returned `VK_TIMEOUT`, append nothing and stop (the written
     count is not trusted);
   - append `Math.Min(written, requested)` translated entries;
   - continue only when the fill returned `VK_INCOMPLETE`,
     `collected < MaxKhrEntries` and `round + 1 < MaxKhrRounds`.
3. Set `incomplete = true` when the loop stopped on a cap while the last fill
   was `VK_INCOMPLETE`.
4. Translate each entry:
   - `Flags = (DeviceFaultFlags)e.flags`;
   - `GroupId = e.groupId`;
   - `Description` decoded;
   - `AddressInfos`: `faultAddressInfo` if `MemoryAddress` is set, then
     `instructionAddressInfo` if `InstructionAddress` is set;
   - `VendorInfos`: `[vendorInfo]` if `Vendor` is set.

   Members whose bit is clear are ignored whatever their bytes contain.
5. On a negative result in any round, stop and return that result.
   `entries` holds whatever was collected (possibly empty) and is never null.
   Otherwise return the last non-negative result.

**`ReadKhrDebugInfo`**:
1. Size query. A negative result returns it with `binary = []`.
2. If the size is 0, return `VK_SUCCESS` with `binary = []` and **no fill
   call**.
3. Otherwise clamp the size to `MaxVendorBinaryBytes`, allocate, write back
   the clamped size, and fill.
4. A negative result returns it with `binary = []`.
5. On `VK_SUCCESS` or `VK_INCOMPLETE`, trim to the written size and set
   `incomplete = r == VK_INCOMPLETE || capped`.

**`TryRead`** dispatches on `eps.Api`. Use exactly these sink messages,
written with `AhjoDiagnostics.Write(DiagnosticSeverity.Warning, "Device", …)`.

- **`None`**: return `false` (unreachable from `Device`, which checks first).
- **`Ext`**: call `ReadExt`.
  - On a negative result, warn
    `$"Device.TryGetDeviceFault: vkGetDeviceFaultInfoEXT returned {r}; no device-fault report is available."`
    and return `false`.
  - Otherwise return `true`.
- **`Khr`**: call `ReadKhrEntries`.
  - On a negative result with **zero** entries, warn
    `$"Device.TryGetDeviceFault: vkGetDeviceFaultReportsKHR returned {r}; no device-fault report is available."`
    and return `false`.
  - On a negative result with entries, warn
    `$"Device.TryGetDeviceFault: vkGetDeviceFaultReportsKHR returned {r} after {n} entries were drained; returning a partial report."`
    and mark the report incomplete.
  - Then call `ReadKhrDebugInfo`. On a negative result, warn
    `$"Device.TryGetDeviceFault: vkGetDeviceFaultDebugInfoKHR returned {r}; the report carries no vendor binary."`
    and keep the entries.
  - Return `true` with `Api = Khr`, `Entries`, `VendorBinary`, and
    `IsIncomplete` = the OR of all incomplete sources.

The class `<summary>` states three things:
- it exists so every protocol is testable against fake function pointers;
- it allocates by design, once, after loss, off every hot path;
- `TryRead` must only be reached through `Device.TryGetDeviceFault`'s
  `IsLost` gate. `ReadKhrEntries` alone is legal on a healthy device, which
  the hardware test relies on.

## Step 7 — `src/Ahjo.Vulkan/Lifecycle/Device.cs`

Add `using System.Diagnostics.CodeAnalysis;`. Add the field
`private readonly object _faultReadLock = new();` beside `_allocatorLock`
(`:44`). Insert after `PruneDeadLocked` (`:142-149`):

```csharp
public static readonly TimeSpan DefaultDeviceFaultTimeout = TimeSpan.FromMilliseconds(100);

public DeviceFaultApi DeviceFaultApi => FaultEntryPoints.Api;
public bool IsDeviceFaultEnabled     => DeviceFaultApi != DeviceFaultApi.None;

public bool TryGetDeviceFault([NotNullWhen(true)] out DeviceFaultReport? report)
    => TryGetDeviceFault(DefaultDeviceFaultTimeout, out report);

public bool TryGetDeviceFault(TimeSpan timeout, [NotNullWhen(true)] out DeviceFaultReport? report)
{
    ObjectDisposedException.ThrowIf(_disposed, this);
    ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);  // rejects InfiniteTimeSpan
    ulong timeoutNs = timeout.ToVulkanTimeout();
    if (timeoutNs == ulong.MaxValue)   // saturated (~292 years and up, TimeSpan.MaxValue): Vulkan's infinite wait
        throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "…");
    report = null;
    var eps = FaultEntryPoints;
    if (eps.Api == DeviceFaultApi.None) return false;
    if (!IsLost) return false;   // VUID-…-07336 / VUID-…-12383; keeps the draining KHR call off healthy devices
    lock (_faultReadLock)
        return DeviceFaultReader.TryRead(in eps, Handle, timeoutNs, out report);
}

private DeviceFaultEntryPoints FaultEntryPoints =>
    new(Functions.GetDeviceFaultInfo, Functions.GetDeviceFaultReports, Functions.GetDeviceFaultDebugInfo);
```

If CS8762 fires on the `return` inside `lock`, use
`bool ok = …; return ok;`. That is mechanical, not a design change.

XML docs:

- **`DefaultDeviceFaultTimeout`.** It bounds only KHR's first, possibly
  blocking, count call. It is a judgment value (spec *Uncertainty*).
- **`DeviceFaultApi` / `IsDeviceFaultEnabled`.** Enabled, not supported.
  Extension-only, not feature: adapt the wording of
  `MeshShaderSupport.PartialGuardNote`. KHR wins when both are enabled.
- **`TryGetDeviceFault`.**
  - `<returns>`: `false` when neither extension is enabled, when the device
    is not lost, or when the read failed with nothing to report (reported
    via `AhjoDiagnostics.Sink`, `Warning`, source `"Device"`).
  - `<exception>`: `ObjectDisposedException`, and
    `ArgumentOutOfRangeException` for a negative or infinite `timeout`, or
    one that converts to `UINT64_MAX` nanoseconds. Both checks precede the
    extension and `IsLost` gates.
    ("an unbounded wait after device loss is refused; see #120").
  - `<remarks>`:
    - Call it after observing `DeviceLost`. List the `MarkLost` sources in
      prose.
    - **Repeat calls:** EXT returns the same report; KHR returns no further
      entries (they are drained exactly once) but the same `VendorBinary`.
      Nothing is cached.
    - **Thread safety:** wrapper callers are serialized. Calling the raw KHR
      command elsewhere in the process splits the queue.
    - **Multi-device caveat** (`IsLost` is over-broad; the EXT call and the
      KHR debug-info call can then hit a healthy sibling).
    - Allocates once, after loss.
    - Healthy-device KHR polling is not wrapped.

## Step 8 — `tests/Ahjo.Vulkan.Tests/ShadowEnumDriftTests.cs`

Add after `AccelerationStructureCopyMode_MatchesNative` (`:240`):

- `DeviceFaultAddressType_MatchesNative()`: seven `Assert.Equal((int)VkDeviceFaultAddressTypeKHR.…_KHR, (int)DeviceFaultAddressType.…)`.
- `DeviceFaultFlags_MatchesNative()`: six `Assert.Equal((uint)VkDeviceFaultFlagBitsKHR.VK_DEVICE_FAULT_FLAG_…_KHR, (uint)DeviceFaultFlags.…)`.

## Step 9 — Tests

### 9a. `tests/Ahjo.Vulkan.Tests/Utf8Tests.cs` (extend; driverless) — 5 cases

1. `FromBounded_StopsAtFirstNul`: `"abc\0def"` gives `"abc"`.
2. `FromBounded_NoNul_UsesWholeBuffer`: 256 × `'A'` gives a 256-char string.
3. `FromBounded_MultiByte`: `"Störung ✓"` round-trips.
4. `FromBounded_InvalidUtf8_DoesNotThrow`: the result contains `'�'`.
5. `FromBounded_Empty`: a leading NUL gives `string.Empty`.

### 9b. `tests/Ahjo.Vulkan.Tests/DeviceFaultReaderTests.cs` (new; driverless; **no `TestGate`**) — 35 cases

`public sealed unsafe class DeviceFaultReaderTests`. All three fakes and every
scenario live in this one class. The suite is serial (`xunit.runner.json`:
`parallelizeTestCollections: false`, `maxParallelThreads: 1`), so static
scenario fields are safe. Reset them in a private `Arrange…` helper at the
start of each test.

All fakes are
`[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])] private static VkResult …`.
They are passed as `&FakeExt` / `&FakeReports` / `&FakeDebugInfo`, and the
device pointer is `(VkDevice_T*)0x1`, which is never dereferenced. **No fake
ever writes more elements or bytes than the caller passed.** An override
changes only the count *reported back*.

- **`FakeExt`**:
  - Scenario statics: available counts, first/second results, reported-count
    override, description bytes.
  - Recorders: call count, sType/pNext per call, sizes passed on fill,
    pointer nullness.
  - Deterministic content: address `i` gets type `i % 7`, `0x1000_0000 + i*0x100`
    and precision 64; vendor `i` gets `"vendor-{i}"`, `0xC0DE+i` and
    `0xDA7A+i`; binary byte `j` is `(byte)j`.
- **`FakeReports`**:
  - Scenario: a static queue of prepared `VkDeviceFaultInfoKHR` values and a
    per-call result script, e.g. `[VK_SUCCESS(count), VK_INCOMPLETE(fill), …]`.
  - Recorders: the timeout passed on each call; the requested `*pFaultCounts`
    on fills; sType/pNext of **every** element on fills.
  - It dequeues what it writes, modelling the destructive semantics. Count
    calls report the queue length, or `VK_TIMEOUT` with count 0 when the
    queue is empty.
  - A script entry may also **cap the number written** on that fill call
    (e.g. write 3 of 5 requested and return `VK_INCOMPLETE`). Test 16 needs
    this.
- **`FakeDebugInfo`**: scenario is the binary length and the result per call.
  It records sType/pNext and the size passed.

**EXT (13):**

1. `Ext_ZeroCounts_StillMakesFillCall_AndPassesNullPointers`.
2. `Ext_Populated_MapsToExactlyOneEntry`. Asserts every field, `Flags == None`,
   `GroupId == 0` and `Api == Ext`.
3. `Ext_SetsSTypeAndNullPNext_OnBothStructs`.
4. `Ext_Description_Unterminated256Bytes_IsBounded`.
5. `Ext_VendorDescription_MultiByteUtf8_RoundTrips`.
6. `Ext_Incomplete_KeepsPartialData_AndFlagsIt`.
7. `Ext_FirstCallError_ReturnsError_NoFillCall`.
8. `Ext_SecondCallError_ReturnsError_NoReport`.
9. `Ext_ReturnedCountsLargerThanAllocated_AreClamped`.
10. `Ext_PassesAllocatedSizes_OnFillCall`.
10a. `Ext_CountsAboveCap_AreCapped_PassedAsCap_AndIncomplete`. The fake
     reports 1,000,000 address and vendor infos. The fill is passed exactly
     `MaxAddressInfos` / `MaxVendorInfos`, the conforming fake answers
     `VK_INCOMPLETE`, and the report is capped and `IsIncomplete`.
10b. `Ext_BinarySizeAboveCap_IsCapped_PassedAsCap_AndIncomplete`. A 1 TiB
     reported size; the fill is passed `MaxVendorBinaryBytes`.
10c. `Ext_CapExceeded_DriverAnswersSuccess_StillIncomplete`. A
     non-conforming fake answers `VK_SUCCESS` to a clamped fill;
     `IsIncomplete` is still set.

**KHR entries (11):**

11. `Khr_TimeoutOnCount_NoFillCall_ZeroEntries`. The result is `VK_TIMEOUT`
    and `entries` is non-null and empty.
12. `Khr_FlagsSelectMembers`. Four queued entries with flags `MemoryAddress`,
    `InstructionAddress`, `MemoryAddress|InstructionAddress|Vendor` and
    `DeviceLost`. The `AddressInfos` lengths are 1 / 1 / 2 / 0 (in
    fault-then-instruction order), and `VendorInfos` has 1 only on the third.
13. `Khr_FlagsClear_GarbageInMembersIsIgnored`. `flags = 0` with non-zero
    bytes in all three members gives empty arrays.
14. `Khr_EveryElement_HasSTypeAndNullPNext`.
15. `Khr_CallerTimeoutOnFirstCountCall_ZeroOnAllOthers`. Use 5 ms, i.e.
    `5_000_000` ns.
16. `Khr_Incomplete_DrainsInSecondRound`. Five queued; the first fill is
    scripted to return 3 + `VK_INCOMPLETE`. The result is all 5 entries in
    order and `incomplete == false`.
17. `Khr_EntryCap_StopsRequesting_AndFlagsIncomplete`. With 1500 queued, the
    total requested is exactly `MaxKhrEntries`, 476 remain in the fake queue,
    and `incomplete == true`.
18. `Khr_RoundCap_StopsAfterMaxRounds`. The fake writes 1 entry and returns
    `VK_INCOMPLETE` forever. The result is exactly `MaxKhrRounds` fills and
    `incomplete == true`.
19. `Khr_FirstCountError_ReturnsError_NoEntries`.
20. `Khr_SecondRoundError_KeepsFirstRoundEntries`.
20a. `Khr_FillTimeout_TranslatesNothing_StopsDraining`. The fill returns
     `VK_TIMEOUT` and leaves `*pFaultCounts` at the requested 3. The result
     is `VK_TIMEOUT`, zero entries, one fill, and no further round. The fake
     gets a `NoWrite` script step for this.

**KHR debug info (5):**

21. `KhrDebug_SizeThenFill_WritesBackSize_SetsSTypeNullPNext`.
22. `KhrDebug_SizeZero_NoFillCall_EmptyBinary`.
23. `KhrDebug_Incomplete_KeepsBytes_AndFlagsIt`.
24. `KhrDebug_Error_ReturnsError_EmptyBinary` (`VK_ERROR_NOT_ENOUGH_SPACE_KHR`).
24a. `KhrDebug_SizeAboveCap_IsCapped_PassedAsCap_AndIncomplete`. A
     `uint.MaxValue` reported size; the fill is passed `MaxVendorBinaryBytes`
     and the result is `VK_INCOMPLETE` plus `incomplete`.

**`TryRead` composition (6).** Capture the sink with the
`DeviceLossTests.cs:162-170` try/finally pattern.

25. `TryRead_BothSupplied_UsesKhrOnly`. EXT fake call count is 0 and
    `Api == Khr`.
26. `TryRead_ExtOnly_ReturnsOneEntry_ApiExt`.
27. `TryRead_Khr_CombinesEntriesAndBinary`.
28. `TryRead_ErrorWithNothingCollected_ReturnsFalse_OneWarning`. Exercise it
    for both the EXT and the KHR first-call error, using exactly the Step 6
    message shapes.
29. `TryRead_KhrPartialDrainError_ReturnsTrue_IncompleteAndWarning`.
30. `TryRead_KhrDebugInfoError_KeepsEntries_EmptyBinary_Warning`.

The caps are unit-tested directly (10a-10c, 24a). The fakes are
conforming: a `VK_SUCCESS`-scripted fill that was passed less than is
available answers `VK_INCOMPLETE`. The two binary-cap cases allocate the
64 MiB cap once each.

### 9c. `tests/Ahjo.Vulkan.Tests/DeviceFaultTests.cs` (new; device-level) — 13 cases

Copy these private helpers from `MeshShaderTests.cs` and adapt them:
`CreateValidatedInstance` (`:889`), `AssertNoValidationErrors` (`:901`) and
`CreateGraphicsDevice` (`:908`).

Add `TryCreateFaultDevice(Instance, bool ext, bool khr, bool pushFeatures, out uint family)`,
modelled on `TryCreateMeshDevice` (`:950`):
- the picker requires `SupportsExtension` for each requested extension;
- `Extensions` lists the requested names;
- when `pushFeatures` is set, the configurer pushes the matching
  `VkPhysicalDeviceFaultFeaturesEXT` / `…KHR` with **`deviceFault = 1` only**,
  because requesting `deviceFaultVendorBinary` risks `FEATURE_NOT_PRESENT`;
- `EXTENSION_NOT_PRESENT` / `FEATURE_NOT_PRESENT` return `null`.

Validation-tier KHR tests (10-12 below) gate on a layer of at least
**1.4.363**, through the Step 9d overload. On an older layer they **skip**
with a `[gate:validation]` reason naming the found version; they do not fail.
An older layer reporting an unknown sType is a gap in the oracle, not a
wrapper defect. Every validation-tier test must record **zero** errors
(`AssertNoValidationErrors`). There is no allowlist.

**`[gate:driver]` (4):**

1. `NoExtension_ApiNone_TryGetFalse_EvenAfterMarkLost`. This `MarkLost` is
   safe: there is no pointer to call.
2. `Disposed_TryGetDeviceFault_Throws`.
3. `NegativeOrInfiniteTimeout_Throws`. Checks both
   `TimeSpan.FromMilliseconds(-1)` and `Timeout.InfiniteTimeSpan`.
3a. `UnboundedTimeout_MaxValueOrOverflowing_Throws`. `TimeSpan.MaxValue` and
    `TimeSpan.FromTicks(long.MaxValue / 100 + 1)` throw. `FromTicks(long.MaxValue / 100)`
    and 100 years are accepted (no extension, so `false`). This runs on a
    plain device because the argument checks precede the extension and
    `IsLost` gates.

**`[gate:feature]`.** Each case calls
`TestGate.RequireDeviceFeature(device is not null, "No GPU exposes <ext> with the deviceFault feature.")`.
There are 5:

All feature-gated devices are created with `pushFeatures: true`.

4. `ExtOnly_ResolvesEntryPoint_ApiExt`.
5. `KhrOnly_ResolvesBothEntryPoints_ApiKhr`.
6. `Both_ApiKhr`.
7. `HealthyDevice_TryGetReturnsFalse`. Checks both the EXT-only and the KHR
   device. Comment: if the gate were missing, the read would run and return
   `true`.
8. `Khr_RealDriver_ReportsCallOnHealthyDevice_TimesOut`. Calls
   `DeviceFaultReader.ReadKhrEntries(device.Functions.GetDeviceFaultReports, device.Handle, 0, …)`
   **directly**. Asserts `(int)r >= 0`, expects `VK_TIMEOUT`, and asserts
   zero entries. Comment: legal per the spec (no lost-state VU for
   reports); never do this with `ReadExt` or `ReadKhrDebugInfo`.

**`[gate:validation]` + `[gate:feature]` (4).** All four use `AssertNoValidationErrors`.

9. `ExtOnly_CreatesCleanly_UnderValidation`, with the plain
   `TestGate.RequireValidationLayer()`.
10. `Both_CreateCleanly_UnderValidation`: both extensions and both feature
    structs. `TestGate.RequireValidationLayer(MinKhrLayer, …)`.
11. `Khr_CreatesCleanly_UnderValidation`: `RequireValidationLayer(MinKhrLayer, …)`.
12. `Khr_RealDriverReportsCall_UnderValidation_Clean`. Case 8 under a
    validated instance on the 4070 Ti, with
    `RequireValidationLayer(MinKhrLayer, …)`. The 1.4.363 layer checks
    extension enablement, each element's sType/pNext and
    `-pFaultCounts-arraylength` on this call, so a clean run proves those
    rules hold for the wrapper's structs.

`MinKhrLayer` is a `private const uint` holding the packed 1.4.363:
`(1u << 22) | (4u << 12) | 363u`, with a comment pointing at
`Directory.Build.props:18`.

**Forbidden.** Put this at the top of the class `<remarks>`: no test calls
`MarkLost()` → `TryGetDeviceFault()`, `ReadExt`, or `ReadKhrDebugInfo` on an
extension-enabled healthy device (VUID-07336 / VUID-12383). Under validation
the 1.4.363 layer would flag it: it tracks loss from real
`VK_ERROR_DEVICE_LOST` returns, and knows nothing of the wrapper's
`MarkLost` seam. Add the tiering
note in the voice of `MeshShaderTests.cs:14-23`: CI has neither extension, so
`[gate:feature]` skips are expected.

### 9d. `tests/Shared/VulkanEnvironment.cs` + `tests/Shared/TestGate.cs` — a layer-version gate

This is additive; no existing gate changes behavior.

- **`VulkanEnvironment`.** Extend `ProbeValidationLayer` (`:252-283`) to also
  capture the matching entry's `VkLayerProperties.specVersion`. Use a second
  `Lazy`, or a tuple in the existing one. Expose it as
  `public static uint ValidationLayerSpecVersion` (0 when there is no layer).
  Keep the probe's never-throw guarantee (`:249-251`).
- **`TestGate`.** Add
  `public static void RequireValidationLayer(uint minimumSpecVersion, string reason)`.
  - It first applies the existing `RequireValidationLayer()` check.
  - It then calls `Assert.SkipWhen(VulkanEnvironment.ValidationLayerSpecVersion < minimumSpecVersion, …)`.
  - The skip message is `$"[gate:validation] {reason} Requires VK_LAYER_KHRONOS_validation >= {Fmt(minimumSpecVersion)}; found {Fmt(found)}."`,
    where `Fmt` prints `major.minor.patch`.

  The `[gate:validation]` prefix keeps CI's unclassified-skip check satisfied.
- **No change to `VulkanTierContractTests`.** A host that declares
  `validation` with an old layer still observes `validation`. The PR's quoted
  run must show the KHR validation tests *ran*.

## Step 10 — Docs (no benchmarks)

- `src/Ahjo.Vulkan/README.md:109-112`: add `ExtDeviceFault` and
  `KhrDeviceFault` to the `VulkanExtensions` constants list.
- **No benchmark step.** Nothing in `Recording/`, `Sync/`, `Pools/` or
  `Memory/` changes, and `FrameRing` is untouched. The code allocates by
  design, once, after loss (spec §G). State this in the PR for
  `bench-coverage-checker`.
- `docs/aot-notes.md`: no change.

## Step 11 — Verify

- `dotnet build Ahjo.Vulkan.slnx` should be clean (warnings are errors).
- `dotnet test tests/Ahjo.Vulkan.Tests`, then
  `AHJO_VULKAN_TIER=validation dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~DeviceFault|FullyQualifiedName~Utf8Tests|FullyQualifiedName~ShadowEnumDrift|FullyQualifiedName~ResultPolicyGuard"`
  on the RTX 4070 Ti, and on the AMD iGPU if reachable as a separate GPU.
  Quote the contract test's `declared=… observed=…` line, the
  validation-layer version (`VulkanEnvironment.ValidationLayerSpecVersion`;
  it must be ≥ 1.4.363, so that cases 10-12 ran rather than skipped), the
  `DeviceFaultTests` pass/skip counts, and the outcome of case 8 (the actual
  `VkResult`).
- `ResultPolicyGuardTests` must stay green.
- Optional: the AOT smoke publish
  (`dotnet publish samples/AotSmoke/AotSmoke.csproj -c Release -r win-x64 -p:IlcUseEnvironmentalTools=true`)
  to prove no trim warning appeared.
- Reviewers:
  - `vulkan-validation-reviewer`: lost-state gating of all three commands,
    sType on array elements, pointer/count pairing, the timeout-0 fills;
  - `bench-coverage-checker`: should conclude no hot path is touched.

## Step 12 — PR and release

- PR title: `Device: add VK_EXT_device_fault + VK_KHR_device_fault readout`.
  Body: `Closes #242`.
- **Name the public-API delta for Ahjo**:
  - `IsDeviceFaultEnabled` instead of the requested `SupportsDeviceFault`,
    plus `DeviceFaultApi`;
  - the report is `Entries[]`-based; the issue's `Description` /
    `AddressInfos` / `VendorInfos` moved onto `DeviceFaultEntry`; EXT
    always yields one entry;
  - `IsIncomplete`;
  - the `TimeSpan` overload and `DefaultDeviceFaultTimeout`;
  - **the validation layer floor**: a "no validation message" capability
    fact that pushes `VkPhysicalDeviceFaultFeaturesKHR` needs SDK/layer
    1.4.363 or newer. On 1.4.341 it reports an unknown sType.
- Include the Step 0 record.
- **Releasing is a separate step after merge.** One `v*` tag ships all eight
  packages via `gh release create`, which fires `publish.yml`. It is not part
  of this plan.

## Open items

None.
