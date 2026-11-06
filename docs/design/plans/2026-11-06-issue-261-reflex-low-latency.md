Paired with [../specs/2026-11-06-issue-261-reflex-low-latency-design.md](../specs/2026-11-06-issue-261-reflex-low-latency-design.md)

# Implementation plan — issue #261: Reflex Low Latency over `VK_NV_low_latency2` + PCL Stats

One PR, branch `issue-261-reflex-low-latency` (already branched from `main`),
`Closes #261` (spec D10).

**How to use this plan**

- Spec references look like "spec D3" or "spec E6".
- Signatures are given exactly. Method bodies are the implementer's.
- An **OPEN** item means stop and ask. Do not improvise.
- Repo rules that apply to every step:
  - Every `const char*` is a `"…"u8` literal or `Utf8Name.FromLiteral`.
  - No `#pragma` suppressions.
  - Partial-class files are named `Type.Aspect.cs`.
  - Nothing under `Generated/` is edited. All bindings used here already exist.

---

## 1. Extension names

**`src/Ahjo.Vulkan/Internal/DeviceExtensionNames.cs`** — add these, and extend
the class remarks with one paragraph saying `NvLowLatency2` gates the four
entry points below and `KhrPresentId` gates none:

```csharp
public static ReadOnlySpan<byte> NvLowLatency2         => "VK_NV_low_latency2"u8;
public static ReadOnlySpan<byte> SetLatencySleepMode   => "vkSetLatencySleepModeNV"u8;
public static ReadOnlySpan<byte> LatencySleep          => "vkLatencySleepNV"u8;
public static ReadOnlySpan<byte> SetLatencyMarker      => "vkSetLatencyMarkerNV"u8;
public static ReadOnlySpan<byte> GetLatencyTimings     => "vkGetLatencyTimingsNV"u8;
public static ReadOnlySpan<byte> KhrPresentId          => "VK_KHR_present_id"u8;
```

**`src/Ahjo.Vulkan/Rendering/VulkanExtensions.cs`** — add:

- `public static Utf8Name NvLowLatency2 => Utf8Name.FromLiteral(DeviceExtensionNames.NvLowLatency2);`
- `public static Utf8Name KhrPresentId => Utf8Name.FromLiteral(DeviceExtensionNames.KhrPresentId);`

Give them remarks in the same style as `KhrAccelerationStructure`. The
`NvLowLatency2` remarks hold "the full enable recipe, once" (spec D2):

- put both names in `DeviceDescription.Extensions`, because present_id is
  LL2's creation dependency (`vk.xml:29889`);
- `timelineSemaphore` is already default-on;
- push `VkPhysicalDevicePresentIdFeaturesKHR { presentId = 1 }` from
  `ConfigureFeatures` **only** if non-zero present ids will be passed
  (VUID-VkPresentInfoKHR-pNext-06235);
- check support first with `PhysicalDevice.SupportsExtension(VulkanExtensions.NvLowLatency2)`;
- then set `SwapchainDescription.LowLatency = device.IsNvLowLatency2Enabled`.

Link `docs/reflex-notes.md`.

## 2. `DeviceFunctionTable` — the gated LL2 block

**`src/Ahjo.Vulkan/Internal/DeviceFunctionTable.cs`**

- Add four fields under a `// ---- Low latency (VK_NV_low_latency2) ----`
  header. Each gets a `<summary>` saying it is null unless the extension was
  enabled:

```csharp
public readonly delegate* unmanaged[Stdcall]<VkDevice_T*, VkSwapchainKHR_T*, VkLatencySleepModeInfoNV*, VkResult> SetLatencySleepMode;
public readonly delegate* unmanaged[Stdcall]<VkDevice_T*, VkSwapchainKHR_T*, VkLatencySleepInfoNV*, VkResult>     LatencySleep;
public readonly delegate* unmanaged[Stdcall]<VkDevice_T*, VkSwapchainKHR_T*, VkSetLatencyMarkerInfoNV*, void>     SetLatencyMarker;
public readonly delegate* unmanaged[Stdcall]<VkDevice_T*, VkSwapchainKHR_T*, VkGetLatencyMarkerInfoNV*, void>     GetLatencyTimings;
```

- Null-initialize all four alongside the existing device-extension fields
  (`:328-342`).
- Add a fifth gated block after the KHR device-fault block (`:579-591`):
  `if (IsExtensionEnabled(enabledExtensions, DeviceExtensionNames.NvLowLatency2))`,
  with four `ResolveExtensionRequired` calls, each passing
  `DeviceExtensionNames.NvLowLatency2` as the extension.
- Extend the type's `<summary>` bullet (`:31-60`) with one sentence:
  "`VK_NV_low_latency2` is the fifth block (issue #261); all four of its
  pointers are swapchain-scoped device-level calls, none is `vkCmd*`."
- Do **not** resolve `vkQueueNotifyOutOfBandNV` (spec D3, why-not).

## 3. `Device` — two capability flags

**`src/Ahjo.Vulkan/Lifecycle/Device.cs`**

- `public bool IsNvLowLatency2Enabled => Functions.LatencySleep != null;`
  Its doc says it is true iff `VK_NV_low_latency2` was enabled at creation and
  resolved.
- `public bool IsPresentIdEnabled { get; }`, set in the constructor next to
  `MemoryBudgetExtensionEnabled` (`:89-92`):
  `PhysicalDevice.ContainsExtension(enabledExtensions, DeviceExtensionNames.KhrPresentId)`.
  Its doc says the wrapper cannot see whether the `presentId` feature was
  pushed (no post-create feature query), and that the validation layer is the
  oracle for that.

## 4. Public latency types — `src/Ahjo.Vulkan/Latency/` (new folder)

Namespace `Ahjo.Vulkan`, one type per file:

- **`LatencyMarker.cs`**:
  `public enum LatencyMarker : uint { SimulationStart = 0, SimulationEnd = 1, RenderSubmitStart = 2, RenderSubmitEnd = 3, PresentStart = 4, PresentEnd = 5, TriggerFlash = 7, OutOfBandPresentStart = 10, OutOfBandPresentEnd = 11 }`.
  Its doc states that the numbering is `VkLatencyMarkerNV` and differs from
  `PclMarker` from 8 upward. It also explains why InputSample and the two OOB
  render-submit values are absent (spec D3).
- **`PclMarker.cs`**:
  `public enum PclMarker : uint { SimulationStart = 0, SimulationEnd = 1, RenderSubmitStart = 2, RenderSubmitEnd = 3, PresentStart = 4, PresentEnd = 5, TriggerFlash = 7, PcLatencyPing = 8, OutOfBandPresentStart = 11, OutOfBandPresentEnd = 12 }`.
  Its doc cites Streamline `include/sl_pcl.h:62-74` (MIT) as the numbering
  source.
- **`LatencySleepMode.cs`**:
  `public readonly record struct LatencySleepMode(bool LowLatency, bool Boost, uint MinimumIntervalUs)`
  with three static properties: `Off => default`,
  `On => new(true, false, 0)` and `OnPlusBoost => new(true, true, 0)`. Its doc
  says:
  - NVIDIA requires `LowLatency` whenever `Boost` is set (Reflex l. 428), but
    the spec permits `Boost` alone (wsi l. 9179-9181), so the wrapper passes it
    through;
  - `MinimumIntervalUs` is a frame-rate cap, with 0 meaning none.
- **`LatencyTimings.cs`**:
  `[StructLayout(LayoutKind.Sequential)] public readonly struct LatencyTimings`,
  whose single private field is `VkLatencyTimingsFrameReportNV _native`. It has
  read-only `ulong` properties `FrameId` (← `presentID`), `InputSampleTimeUs`,
  `SimulationStartTimeUs`, `SimulationEndTimeUs`, `RenderSubmitStartTimeUs`,
  `RenderSubmitEndTimeUs`, `PresentStartTimeUs`, `PresentEndTimeUs`,
  `DriverStartTimeUs`, `DriverEndTimeUs`, `OsRenderQueueStartTimeUs`,
  `OsRenderQueueEndTimeUs`, `GpuRenderStartTimeUs` and `GpuRenderEndTimeUs`.
  Its doc says the layout is identical to the native struct by construction,
  which is what makes `GetLatencyTimings` copy-free.
- **`PclStatsOptions.cs`**:
  `public struct PclStatsOptions { public uint PingThreadId; public PclPingVirtualKey PingVirtualKey; }`.
  The default is the registered-message mode. Its doc gives the three modes and
  their precedence, from pclstats.h:63-88: thread id, then virtual key, then
  message. It also says the virtual-key mode collides with the "no F13" QA item
  (Reflex l. 1029).
- **`PclPingVirtualKey.cs`**:
  `public enum PclPingVirtualKey : ushort { None = 0, F13 = 0x7C, F14 = 0x7D, F15 = 0x7E }`.

**`src/Ahjo.Vulkan/Internal/LatencyMarkerMap.cs`** (new):
`internal static class LatencyMarkerMap { public static PclMarker ToPcl(LatencyMarker m); public static VkLatencyMarkerNV ToVk(LatencyMarker m); public static bool ForwardsToPcl(LatencyMarker m, bool lowLatencySwapchain); }`.
`ForwardsToPcl` returns `false` exactly when `lowLatencySwapchain` is true and
`m` is `OutOfBandPresentStart` or `OutOfBandPresentEnd`; otherwise `true`. The
reason is spec D7/E6: the driver writes those PCL markers itself.
`ToPcl` is an exhaustive `switch` per the spec D7 table. Its default arm throws
`ArgumentOutOfRangeException`, which is unreachable for defined values. `ToVk`
is `(VkLatencyMarkerNV)(uint)m`, and its doc points to the drift test in step 11.

## 5. `SwapchainDescription` + `Swapchain` lifecycle changes

**`src/Ahjo.Vulkan/Rendering/SwapchainDescription.cs`** — add
`public bool LowLatency;`. Its doc says:

- `true` chains `VkSwapchainLatencyCreateInfoNV { latencyModeEnable = VK_TRUE }`;
- the device must have `VK_NV_low_latency2` enabled;
- the value is fixed for the swapchain's lifetime, and `Recreate` must pass the
  same value;
- the default `false` gives the pre-#261 create, byte for byte.

**`src/Ahjo.Vulkan/Rendering/Swapchain.cs`**

- Add fields: `private readonly bool _lowLatency;`,
  `private LatencySleepMode _sleepMode;`,
  `private readonly TimelineSemaphore _sleepSemaphore;`,
  `private ulong _sleepValue;` and `private ulong _lastPresentId;`.
  Either keep them in `Swapchain.cs` or declare them in `Swapchain.Latency.cs`.
  The class is already partial-capable, so add `partial` to the declaration at
  `:31`.
- **Constructor** (`:225-239`). Before `CreateOrRecreate`:
  - if `desc.LowLatency && !device.IsNvLowLatency2Enabled`, throw
    `InvalidOperationException("SwapchainDescription.LowLatency is true but the device was created without VK_NV_low_latency2. Add VulkanExtensions.NvLowLatency2 and VulkanExtensions.KhrPresentId to DeviceDescription.Extensions (check PhysicalDevice.SupportsExtension first), or set LowLatency = false.")`;
  - set `_lowLatency = desc.LowLatency`;
  - if it is set, `_sleepSemaphore = _semaphorePool.AcquireTimeline()`.
- **`Recreate`** (`:320`). Right after the Surface check (`:323-326`): if
  `desc.LowLatency != _lowLatency`, throw
  `ArgumentException("Recreate must use the same LowLatency value this Swapchain was constructed with (" + _lowLatency + ").", nameof(desc))`.
  Extend the `<summary>` to say so.
- **`CreateOrRecreate`** (`:761-829`):
  - Declare a stack local
    `VkSwapchainLatencyCreateInfoNV latencyCi = new() { sType = VK_STRUCTURE_TYPE_SWAPCHAIN_LATENCY_CREATE_INFO_NV, latencyModeEnable = 1 };`
  - Set `ci.pNext = _lowLatency ? &latencyCi : null`.
  - After `_handle = raw` and `LoadImagesAndViews()`, set `_lastPresentId = 0`.
  - If `_lowLatency`, call `ApplySleepMode()` from step 6 to re-apply the
    stored mode. Its comment says the spec is silent on whether mode survives
    `oldSwapchain`, so the re-apply is defensive (spec D3).
  - The state machine and the #111 retire logic are untouched.
- **`Dispose`**: no change. The pool owns and destroys `_sleepSemaphore`
  (`:730`).

## 6. `src/Ahjo.Vulkan/Rendering/Swapchain.Latency.cs` (new, `public sealed unsafe partial class Swapchain`)

The members, with doc comments citing the spec sections. Every member reads
`_handle` exactly once into a local.

```csharp
public bool             IsLowLatencyEnabled => _lowLatency;
public LatencySleepMode LatencySleepMode    => _sleepMode;
public void      SetLatencySleepMode(in LatencySleepMode mode);
public WaitState LatencySleep(TimeSpan timeout);
public void      SetLatencyMarker(LatencyMarker marker, ulong frameId);
public int       GetLatencyTimings(scoped Span<LatencyTimings> destination);
public int       LatencyTimingCapacity { get; }
private void     ApplySleepMode();
```

Behaviour:

- **`SetLatencySleepMode`**
  - `ObjectDisposedException.ThrowIf`.
  - If `!_lowLatency`, throw
    `InvalidOperationException("SetLatencySleepMode requires a Swapchain created with SwapchainDescription.LowLatency = true. Hide Reflex controls when Device.IsNvLowLatency2Enabled is false.")`.
  - Store `_sleepMode = mode`, then `ApplySleepMode()`.
- **`ApplySleepMode`**: if `_handle == null` (minimized), return; the next
  create re-applies. Otherwise build `VkLatencySleepModeInfoNV` from
  `_sleepMode` and call it through `_device.Functions.SetLatencySleepMode(...).ThrowIfFailed()`.
- **`LatencySleep`**
  - If `!_lowLatency || _handle == null`, return `WaitState.Signaled` (spec D3,
    per-frame degrade).
  - Otherwise `ulong v = ++_sleepValue;` and build
    `VkLatencySleepInfoNV { signalSemaphore = _sleepSemaphore.Handle, value = v }`.
  - Call `Functions.LatencySleep(...).ThrowIfFailed()`, then
    `return _sleepSemaphore.WaitFor(v, timeout);`.
  - Doc: call once per *rendered* frame, before input sampling (spec D5/E5);
    pass a bounded timeout, for example 1 s, so a driver that never signals
    cannot hang the loop; on `Timeout`, continue the frame.
- **`SetLatencyMarker`**
  - `ArgumentOutOfRangeException.ThrowIfZero(frameId)`.
  - If `_lowLatency && h != null`, call `Functions.SetLatencyMarker` with
    `VkSetLatencyMarkerInfoNV { presentID = frameId, marker = LatencyMarkerMap.ToVk(marker) }`.
  - Then, if `LatencyMarkerMap.ForwardsToPcl(marker, _lowLatency)`, call
    `PclStats.Marker(LatencyMarkerMap.ToPcl(marker), frameId)`. Added after the
    live ETW capture (spec E6/D7); this is the one implementer change it
    requires.
  - Doc: carries the full D5 sequence (single and FG); the threading rule from
    spec D3; PRESENT markers go on the presenting thread.
- **`GetLatencyTimings`**
  - If `!_lowLatency || h == null || destination.IsEmpty`, return 0.
  - `fixed` the span and cast it to `VkLatencyTimingsFrameReportNV*`. For each
    element set `sType = VK_STRUCTURE_TYPE_LATENCY_TIMINGS_FRAME_REPORT_NV` and
    `pNext = null`.
  - Build `VkGetLatencyMarkerInfoNV { timingCount = (uint)destination.Length, pTimings = p }`,
    make the call, and return `(int)info.timingCount`.
  - Doc: oldest first (wsi l. 9384-9386).
- **`LatencyTimingCapacity`**: the NULL-`pTimings` query (wsi l. 9380-9381);
  0 when not opted in or there is no handle.
- None of the per-frame members may allocate. The message strings above are
  only on throw paths.

## 7. Present-id overloads — `src/Ahjo.Vulkan/Rendering/Swapchain.cs`

- Extract the body of `Present(Queue, uint, in BinarySemaphore)` (`:691-712`)
  into `private AcquireResult PresentCore(Queue queue, uint imageIndex, VkSemaphore_T* waitRaw, ulong presentId)`.
  When `presentId == 0`, `info.pNext` is `null`, exactly as today. Otherwise a
  stack `ulong id = presentId;` and
  `VkPresentIdKHR pid = { sType = VK_STRUCTURE_TYPE_PRESENT_ID_KHR, swapchainCount = 1, pPresentIds = &id }`,
  with `info.pNext = &pid`. Record `_lastPresentId = presentId` (if non-zero)
  **unconditionally, right after `vkQueuePresentKHR` returns and before
  mapping the result**. VUID-04999 is about ids *passed*, whatever the result.
  The result mapping stays `MapPresentationResult`.
- The existing `Present(Queue, uint, in BinarySemaphore)` keeps its guards and
  becomes `return PresentCore(queue, imageIndex, waitSemaphore.Handle, 0);`.
  The existing `Present(Queue, uint)` is unchanged and still forwards.
- New `public AcquireResult Present(Queue queue, uint imageIndex, ulong presentId)`:
  the same guards as `Present(Queue, uint)` (`:653-655`), then
  `ValidatePresentId(presentId)`, then
  `PresentCore(queue, imageIndex, _renderingDone[imageIndex].Handle, presentId)`.
- New `public AcquireResult Present(Queue queue, uint imageIndex, in BinarySemaphore waitSemaphore, ulong presentId)`:
  the same guards, then `ValidatePresentId(presentId)`, then `PresentCore`.
- `private void ValidatePresentId(ulong presentId)`. If `presentId == 0`,
  return. If `!_device.IsPresentIdEnabled`, throw
  `InvalidOperationException("A non-zero presentId requires VK_KHR_present_id in DeviceDescription.Extensions and VkPhysicalDevicePresentIdFeaturesKHR.presentId pushed from ConfigureFeatures.")`.
  If `presentId <= _lastPresentId`, throw
  `ArgumentOutOfRangeException(nameof(presentId), presentId, $"presentId must be greater than the last non-zero presentId ({_lastPresentId}) presented on this VkSwapchainKHR (VUID-VkPresentIdKHR-presentIds-04999). The counter resets when Recreate creates a new swapchain.")`.
  The interpolation is on the throw path only.
- Docs on both new overloads say:
  - `Present(q, i, default)` is ambiguous (CS0121), so write `0UL` or
    `default(BinarySemaphore)` (spec D4);
  - KHR present ids are **not** Reflex frame ids (spec D4/E4);
  - a frame-generation frame needs two distinct ids;
  - the result set is that of the semaphore overload (cref it).

## 8. PCL Stats — ETW provider

### 8a. `src/Ahjo.Vulkan/Internal/PclStatsEtw.cs` (new, `internal static unsafe class PclStatsEtw`)

- **Interop.** Blittable `[DllImport]`s with `ExactSpelling = true`, and no
  `SetLastError`:
  - advapi32: `uint EventRegister(Guid* providerId, delegate* unmanaged[Stdcall]<Guid*, uint, byte, ulong, ulong, void*, void*, void> callback, void* context, ulong* regHandle)`,
    `uint EventUnregister(ulong regHandle)`,
    `uint EventSetInformation(ulong regHandle, int informationClass, void* information, uint length)`
    and `uint EventWriteTransfer(ulong regHandle, EventDescriptor* descriptor, Guid* activityId, Guid* relatedActivityId, uint userDataCount, EventDataDescriptor* userData)`;
  - user32: `uint RegisterWindowMessageW(char* name)`,
    `nint GetForegroundWindow()`,
    `uint GetWindowThreadProcessId(nint hwnd, uint* processId)`,
    `int PostMessageW(nint hwnd, uint msg, nuint wParam, nint lParam)` and
    `int PostThreadMessageW(uint threadId, uint msg, nuint wParam, nint lParam)`.
- **Structs.**
  - `EventDescriptor`: `ushort Id; byte Version; byte Channel; byte Level; byte Opcode; ushort Task; ulong Keyword;`,
    16 bytes. One static instance has `Channel = 11, Level = 5`, everything
    else 0 (spec E6).
  - `EventDataDescriptor`: `ulong Ptr; uint Size; byte Type; byte Reserved1; ushort Reserved2;`,
    16 bytes. `Type` is 2 for provider traits, 1 for event metadata, 0 for
    payload (tld.cs:451-459).
- **Constants.**
  - Provider GUID `0d216f06-82a6-4d49-bc4f-8f38ae56efab`, as a
    `static readonly Guid`. Assigning it is setup-time.
  - Window-message name `"PC_Latency_Stats_Ping"`.
  - Control codes: 0 disable, 1 enable, 2 capture state.
  - `EventProviderSetTraits = 2`.
- **Metadata blobs.** Each is a `static ReadOnlySpan<byte> X => [ … ];`
  RVA constant, hand-encoded:
  - provider traits: `UInt16 LE total size` + `"PCLStatsTraceLoggingProvider\0"`;
  - events: `UInt16 LE total size` + tag byte `0x00` + `"<EventName>\0"` +
    per field `"<FieldName>\0"` + `InType byte`, with UInt32 = `8` and
    UInt64 = `10`.

  Seven event blobs: `PCLStatsInit` (no fields),
  `PCLStatsEvent {Marker:8, FrameID:10}`, `PCLStatsInput` ×3
  (`IdThread:8`, `VirtualKey:8`, `MsgId:8`), `PCLStatsFlags {Flags:8}` and
  `PCLStatsShutdown`. Each blob gets a comment giving the decoded form. A test
  (step 11) rebuilds every blob from a builder and compares bytes, so hand
  encoding errors cannot ship.
- **`Write(ReadOnlySpan<byte> eventMetadata, void* payload0, uint size0, void* payload1, uint size1)`**:
  - the internal one-call path, with no allocation;
  - `stackalloc EventDataDescriptor[4]`;
  - `fixed` both blobs (RVA, no pinning cost);
  - `userDataCount = 2 + number of non-null payloads`.
  - Returns nothing: an ETW write failure is never the frame's problem.

### 8b. `src/Ahjo.Vulkan/Latency/PclStats.cs` (new, `public static unsafe class PclStats`)

The state is `static` fields: `s_handle` (ulong), `s_enabled` (volatile bool),
`s_pendingPing` (int, Interlocked), `s_pingMessageId` (uint), `s_options`,
`s_pingThread` (Thread?), `s_quit` (ManualResetEvent?) and `s_initialized`
(bool). They are guarded by a `static readonly Lock s_lock` for
`Initialize`/`Shutdown` only.

- `public static bool IsInitialized`, `IsProviderEnabled => s_enabled`,
  `uint PingMessageId`.
- **`public static void Initialize(in PclStatsOptions options)`**
  1. If not Windows, return.
  2. Under the lock: if already initialized, throw
     `InvalidOperationException("PclStats is already initialized; call Shutdown first.")`.
  3. `RegisterWindowMessageW`.
  4. `EventRegister` with `&EnableCallback`. A non-zero status means: do not
     throw, leave `IsInitialized` false, and report through
     `AhjoDiagnostics.Sink(DiagnosticSeverity.Warning, "PclStats", $"EventRegister failed (0x{status:X8}); PCL Stats markers are disabled.")`
     (`src/Ahjo.Vulkan/Diagnostics/AhjoDiagnostics.cs:59`). This is a setup
     path, so the interpolation is fine.
     Telemetry must not crash a game.
  5. `EventSetInformation(EventProviderSetTraits, traits)`.
  6. Write `PCLStatsInit`.
  7. Start the ping thread: `IsBackground = true`, `Name = "Ahjo PCL Stats ping"`.
- **`public static void Shutdown()`**
  1. If not initialized, return.
  2. Signal quit, then `Join(TimeSpan.FromSeconds(1))`.
  3. Write `PCLStatsShutdown`.
  4. `EventUnregister`, dispose the event, clear the state.
- **`public static void Marker(PclMarker marker, ulong frameId)`**
  1. `ArgumentOutOfRangeException.ThrowIfZero(frameId)` first (spec D3).
  2. If `!s_initialized`, return.
  3. If `marker == PclMarker.SimulationStart` and
     `Interlocked.Exchange(ref s_pendingPing, 0) == 1`, then if `s_enabled`,
     first write `PCLStatsEvent(PcLatencyPing, frameId)` and increment the
     test counter. The flag is consumed even when nobody is listening, so it
     cannot go stale.
  4. If `!s_enabled`, return.
  5. Write `PCLStatsEvent(marker, frameId)` with a `uint m` and a `ulong f`
     stack payload.
- **`public static bool IsPingMessage(uint message) => message != 0 && message == s_pingMessageId;`**
- **`public static void NotifyPingReceived() => Volatile.Write(ref s_pendingPing, 1);`**
- **`[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])] private static void EnableCallback(Guid* sourceId, uint controlCode, byte level, ulong matchAny, ulong matchAll, void* filter, void* context)`**:
  - 1 → `s_enabled = true`;
  - 0 → `s_enabled = false`;
  - 2 → write `PCLStatsFlags {Flags = 0}` if `s_handle != 0`. The callback can
    fire synchronously inside `EventRegister`, before the handle is stored.
  - It must not throw. Wrap the body in `try`/`catch` that swallows, since an
    exception across `UnmanagedCallersOnly` terminates the process.
- **Ping thread body** (pclstats.h:53-97 semantics, spec D6):
  - loop `while (!s_quit.WaitOne(100 + Random.Shared.Next(200)))`;
  - `if (!s_enabled) continue;`;
  - if `PingThreadId != 0`: `PostThreadMessageW` + `PCLStatsInput{IdThread}`;
  - else get `GetForegroundWindow`. If it is non-zero and
    `GetWindowThreadProcessId` returns this process (`Environment.ProcessId`):
    - with `PingVirtualKey != None`, post `WM_KEYDOWN` (`0x0100`, lParam `1`)
      and `WM_KEYUP` (`0x0101`, lParam `unchecked((nint)0xC0000001)`), plus
      `PCLStatsInput{VirtualKey}`;
    - otherwise `PostMessageW(hwnd, s_pingMessageId, 0, 0)` +
      `PCLStatsInput{MsgId}`.
  - No allocation inside the loop.
- **Test/benchmark seam**:
  `internal static void SetProviderEnabledForTesting(bool enabled) => s_enabled = enabled;`.
  Also `internal static int PingMarkersWrittenForTesting`, incremented in step 3
  of `Marker`. Both are reachable through the existing `InternalsVisibleTo`
  (`Ahjo.Vulkan.csproj`).
- **Class `<remarks>`**:
  - one process-wide provider (pclstats.h globals);
  - Windows-only, no-op elsewhere;
  - thread-safe for `Marker` from any thread;
  - why this is not `EventSource` (spec E8);
  - the provenance of every constant (spec E6/D8).

## 9. `tests/Ahjo.Vulkan.Tests/SdlWindow.cs` — PCL ping hook + left click (shared with samples)

- `public void EnablePclPingHook()`. On Windows it calls
  `SDL_SetWindowsMessageHook(&PclHook, null)`. `PclHook` is an
  `[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]` static that
  reads the `MSG*`'s `message`. If `PclStats.IsPingMessage(message)` it calls
  `PclStats.NotifyPingReceived()`. It returns SDL's "let SDL continue
  processing" value.
- **OPEN-5:** before going further, confirm on hardware that the hook sees the
  registered message posted by the ping thread to the SDL window (log a counter
  for 10 s with PresentMon or `capturePclEtw.bat` running), and confirm the
  return-value semantics of the ppy.SDL3-CS 2026.1002.1 signature. **If it does
  not see the message, stop and ask.** Do not switch to the thread-id or F13
  mode yourself.
- `public bool ConsumeLeftClick()`. Set it in `PumpEvents` on
  `SDL_EVENT_MOUSE_BUTTON_DOWN` with button 1. It has the same shape as
  `ConsumeResize`.

## 10. `samples/HelloCube` — the non-FG hardware host (spec D9)

**`samples/HelloCube/Program.cs`**

- **Arguments**, extending the existing loop at `:106-108`:
  - `--reflex off|on|boost`, default `on`;
  - `--present fifo|immediate`, default `fifo`;
  - `--present-id`;
  - `--latency-report N`, default 0 = off;
  - `--skip-sleep`: a **diagnostic only**, for the OPEN-7 control. The frame
    loop does not call `swap.LatencySleep`. Print
    `"Reflex: sleep skipped (diagnostic; not a valid Reflex integration)"`.
  - A bad value prints usage and exits 2.
- **Device.** If `gpu.SupportsExtension(VulkanExtensions.NvLowLatency2)`, add
  `NvLowLatency2` and `KhrPresentId` to the extension list. With
  `--present-id`, also push `VkPhysicalDevicePresentIdFeaturesKHR { presentId = 1 }`
  from `ConfigureFeatures`. Print
  `"Reflex: available (VK_NV_low_latency2)"` or
  `"Reflex: unavailable on this GPU — PCL markers only"`.
- **Swapchain.** Set `LowLatency = device.IsNvLowLatency2Enabled` in both
  descriptions (`:151`, `:736`), and set `PreferredPresentMode` from
  `--present`. When enabled, call `swap.SetLatencySleepMode(...)` once after
  construction (`off` → `Off`, `on` → `On`, `boost` → `OnPlusBoost`).
- **PCL.** Call `PclStats.Initialize(new PclStatsOptions())` before the loop,
  `window.EnablePclPingHook()`, and `PclStats.Shutdown()` in the teardown
  `finally`.
- **Loop.** Use `ulong frameId` starting at 1, incremented once per loop
  iteration. Order, per spec D5 single-present:
  1. `swap.LatencySleep(TimeSpan.FromSeconds(1))` at the top, before
     `window.PumpEvents()` (input).
  2. `SimulationStart`, then pump and update. If `window.ConsumeLeftClick()`,
     emit `TriggerFlash`.
  3. `SimulationEnd`.
  4. `RenderSubmitStart` before `ring.BeginFrame()`.
  5. `PresentStart`, then `swap.Present(queue, imageIndex, nextPresentId++)`
     with `--present-id`, else the existing call (`:510`).
  6. `PresentEnd`, then `RenderSubmitEnd`.
- **On abandoned frames** (acquire `OutOfDate` etc.), still emit `SimulationEnd`,
  `RenderSubmitEnd` for the frame id that started. Do not reuse the id.
- **`--latency-report N`**: every N frames, call
  `swap.GetLatencyTimings(stackalloc LatencyTimings[64])`. Print the mean of
  `GpuRenderEndTimeUs - SimulationStartTimeUs` over entries whose `FrameId != 0`,
  plus the entry count. This uses a stack span and does not allocate in steady
  state. Keep the `Console.WriteLine` interpolation to the report frame.
- Keep everything else in the sample as is.

## 11. Tests — `tests/Ahjo.Vulkan.Tests/`

**CI-runnable (no driver):**

- **`LatencyMarkerMapTests.cs`**
  - `ToPcl_MatchesSpecTable`: a theory over all 9 `LatencyMarker` values
    against spec D7's table.
  - `ForwardsToPcl_SuppressesOnlyOobPresentOnLowLatency`: a theory over all
    9 markers × {true, false}. It is `false` only for
    (`OutOfBandPresentStart`, true) and (`OutOfBandPresentEnd`, true).
  - `ToVk_EqualsVkLatencyMarkerNV`: each value equals the named
    `VkLatencyMarkerNV` constant, for example
    `LatencyMarker.OutOfBandPresentStart` ==
    `(uint)VK_LATENCY_MARKER_OUT_OF_BAND_PRESENT_START_NV`.
  - `PclMarker_ValuesPinnedToSlPcl`: literal asserts 0,1,2,3,4,5,7,8,11,12.
  - `Enum_IsExhaustive`: `Enum.GetValues<LatencyMarker>().Length == 9`, which
    catches a value added without a map arm.
- **`LatencySleepModeTests.cs`**: the three presets have the exact field
  values; `default == Off`.
- **`LatencyTimingsLayoutTests.cs`**:
  `sizeof(LatencyTimings) == sizeof(VkLatencyTimingsFrameReportNV)`, and a
  round-trip through `MemoryMarshal.Cast` maps each property to the right field
  (write distinct values into a native struct, read them back through the
  wrapper).
- **`PclStatsTests.cs`**, Windows-gated with
  `TestGate.RequirePlatform(IsWindows, …)`, and `[Collection]`-serialized
  because the state is static:
  - `MetadataBlobs_MatchBuilder`: a test-local builder encodes each of the
    seven events from (name, fields[]) and the provider traits from the name,
    and the result equals `PclStatsEtw`'s blob bytes.
  - `Initialize_Marker_Shutdown_Succeeds`: `IsInitialized` flips true, then
    false; `PingMessageId != 0`.
  - `Initialize_Twice_Throws`.
  - `Marker_BeforeInitialize_IsNoOp`.
  - `Marker_ZeroFrameId_Throws`.
  - `PendingPing_IsEmittedBeforeSimulationStart_Once`: use the seam to force
    enabled, call `NotifyPingReceived`, then `Marker(SimulationStart, 5)` twice.
    `PingMarkersWrittenForTesting` grows by exactly 1.
  - `Marker_IsAllocationFree`: force enabled, warm up, then
    `GC.GetAllocatedBytesForCurrentThread()` delta over 10 000 `Marker` calls
    is 0.
  - `IsPingMessage_ZeroIsFalse`.
- **`PclStatsEtwRoundTripTests.cs`** — the real oracle.
  - Add the test-only package `Microsoft.Diagnostics.Tracing.TraceEvent` (the
    latest stable on nuget.org at implementation time) to
    `Directory.Packages.props` and `Ahjo.Vulkan.Tests.csproj`.
  - Gate with
    `TestGate.RequirePlatform(IsWindows && IsElevated, "ETW real-time sessions need elevation.")`,
    where
    `IsElevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)`
    is a private static in the test class.
  - Start a `TraceEventSession` enabling `0d216f06-82a6-4d49-bc4f-8f38ae56efab`
    at verbose level, call `PclStats.Initialize`, emit `SimulationStart` /
    `OutOfBandPresentStart` with frame id 42, then `Shutdown`.
  - Assert through `source.Dynamic.All`: event names `PCLStatsInit`,
    `PCLStatsEvent` ×2 and `PCLStatsShutdown`; `PayloadByName("Marker")` is 0
    and 11; `PayloadByName("FrameID")` is 42; provider name
    `PCLStatsTraceLoggingProvider`.

**Hardware (RTX 4070 Ti).**
- Gate with `TestGate.RequireDriver()` + `Win32Window`, as in
  `SwapchainTests.cs:17-23`.
- Add `TestGate.RequireDeviceFeature(gpu.SupportsExtension(VulkanExtensions.NvLowLatency2), "VK_NV_low_latency2 not advertised.")`
  wherever LL2 is needed.
- Put them in **`SwapchainLowLatencyTests.cs`**:
  - `LowLatency_OnDeviceWithoutExtension_Throws`: no LL2 in the list,
    `LowLatency = true` → `InvalidOperationException` naming
    `VK_NV_low_latency2`. Runs anywhere with a driver.
  - `NotOptedIn_PerFrameCallsDegrade`: `LatencySleep` returns `Signaled` in
    under 1 ms; `SetLatencyMarker` does not throw; `GetLatencyTimings` returns
    0; `SetLatencySleepMode` throws `InvalidOperationException`.
  - `OptedIn_SleepMarkersTimings`:
    - `SetLatencySleepMode(On)`;
    - 120 frames of sleep + the six single-present markers + an acquire / a
      barrier-only submit / a present;
    - then `GetLatencyTimings(stackalloc LatencyTimings[64])` returns > 0;
    - every non-zero `FrameId` is one of the sent ids;
    - `SimulationStartTimeUs <= SimulationEndTimeUs` on populated entries;
    - `LatencyTimingCapacity >= 1`.
  - `Recreate_KeepsOptInAndMode`: set `OnPlusBoost`, `Recreate` at a new size →
    `IsLowLatencyEnabled`, `LatencySleepMode == OnPlusBoost`, and the next
    `LatencySleep` returns `Signaled`.
  - `Recreate_WithDifferentLowLatency_Throws` → `ArgumentException`.
  - `FrameIdZero_Throws`.
  - `PresentId_StrictlyIncreasing`: with present_id plus the feature pushed,
    ids 1 and 2 succeed; 2 again → `ArgumentOutOfRangeException`; after
    `Recreate`, 1 succeeds again.
  - `PresentId_WithoutExtension_Throws` → `InvalidOperationException`.
  - `PresentId_Zero_IsPlainPresent`: no extension needed.
- **`WindowedValidationTests.cs`** — add
  `FrameGenerationMarkerSequence_IsValidationClean`. It runs the spec D5
  two-present sequence (two acquires, two submits, two presents with OOB pairs
  and KHR ids `2N`/`2N+1`) for 30 frames under `VK_LAYER_KHRONOS_validation`,
  with zero errors. It needs `RequireValidationLayer()` plus the hardware gates.
- **`ScopedSpanProbe.cs`** — add a call to
  `swap.GetLatencyTimings(stackalloc LatencyTimings[4])`, so dropping `scoped`
  breaks the build (`src/Ahjo.Vulkan/CLAUDE.md`, rule 2).

## 12. Benchmarks — `tests/Ahjo.Vulkan.Benchmarks/`

- **`PclStatsBenchmarks.cs`** (`[MemoryDiagnoser]`, any Windows host). Setup
  calls `Initialize`; Cleanup calls `Shutdown`.
  - `Marker_NoListener`: provider disabled, `OperationsPerInvoke = 64`.
  - `Marker_ListenerForced`: `SetProviderEnabledForTesting(true)`, measuring
    the `EventWriteTransfer` path, `OperationsPerInvoke = 64`.
  - `SimulationStart_WithPendingPing`: `NotifyPingReceived` + `Marker`.
- **`LowLatencyBenchmarks.cs`** (`[MemoryDiagnoser]`).
  - Link `..\Ahjo.Vulkan.Tests\Win32Window.cs` into the benchmarks csproj.
    First check that it uses no xunit or `TestGate` types. If it does, stop
    and ask rather than copying it.
  - **Setup throws** if LL2 is unavailable, with the same rationale as
    `DlssFrameGenerationEvaluateBenchmarks` (`:25-27`).
  - Setup creates the device with LL2 + present_id + the feature, then a
    `LowLatency` swapchain with `PreferredPresentMode = IMMEDIATE` (falling back
    to FIFO is acceptable; record which), mode `On`, and a `FrameRing`.
  - Rows:
    - `SetLatencyMarker_FgFrame`: the ten D5 markers for one frame id,
      `OperationsPerInvoke = 10`.
    - `GetLatencyTimings_64`: a stack span of 64.
    - `Frame_SleepMarkersPresentWithId`: `LatencySleep` + six markers +
      `BeginFrame` + acquire + barrier-only record + submit +
      `Present(queue, i, id++)`.
- Every row must read `Allocated: -`. Run through `/run-bench` (`-c Release`).

## 13. AOT smoke — `samples/AotSmoke/Program.cs`

Add, before the existing render round-trip, a Windows-only block:

```
PclStats.Initialize(new PclStatsOptions());
PclStats.Marker(PclMarker.SimulationStart, 1);
PclStats.Marker(PclMarker.SimulationEnd, 1);
PclStats.Shutdown();
```

It prints `"PCL Stats: ok"`. This makes ILC compile the ETW path (spec D9). Run
the local publish command from the root `CLAUDE.md` and confirm there are zero
ILC/trim warnings.

## 14. Hardware run (RTX 4070 Ti) — record in `docs/reflex-notes.md` §7

Record, with the date:

- the driver version and `VK_NV_low_latency2` revision from `vulkaninfoSDK`;
- whether the run was windowed or borderless;
- the refresh rate.

Then:

1. **PC latency**, from PresentMon's PC-latency column (PCL) and from
   `--latency-report` sim→GPU-end. Run HelloCube for 60 s each:
   - `--present immediate --reflex off`
   - `--present immediate --reflex on`
   - `--present immediate --reflex boost`
   - `--present fifo --reflex on`

   Report the mean and p99 PCL and the FPS.
2. **OPEN-7 control** (spec OPEN-7). The no-timeout and about-one-refresh
   results are already measured. Measure FPS and the mean `LatencySleep`
   duration, 60 s each, under `--present immediate`, in both windowed and
   borderless fullscreen:
   - `--reflex off`
   - `--reflex off --skip-sleep`
   - `--reflex on`

   Record the NVIDIA Control Panel settings for the process: Max Frame Rate,
   Vertical sync, Low Latency Mode, Vulkan/OpenGL present method.
   - If `--skip-sleep` FPS is within 4% of `--reflex off`, record "closed:
     pacing not caused by the sleep".
   - Otherwise **stop and report both numbers**. The spec D3 sleep rule stays
     as written until a human decides.
3. **OPEN-4**: run `--reflex on --latency-report 120` with and without
   `--present-id`. Record whether `GetLatencyTimings` returns populated
   entries in both. **If they populate only with `--present-id`, stop and
   report.** That would invalidate spec D5 (OPEN-4).
4. **Reflex Test Utility** (`Reflex_Verification/` from NVIDIA-RTX/REFLEX @
   `c2522cee`, Reflex guide §4.2). Run the Reflex On and On + Boost passes. Record
   pass or warnings verbatim, and the PCL / I>S / S>Q values. Check that the
   Verification HUD keeps updating markers with `--reflex off` (Reflex l. 1025).
   **OPEN-2: if warnings point at the integration path rather than at marker
   placement, stop and report.** FG-on runs belong to #253 PR C 19i.
5. **Other IHV.** Run HelloCube on the AMD iGPU with
   `VK_LOADER_DRIVERS_SELECT=*amd*` (Khronos loader driver filter). It must
   print "Reflex: unavailable…", and PresentMon must show non-zero PCL. That
   covers "PCL is not 0.0 on other IHV hardware" (Reflex l. 1030).
6. Record **OPEN-5**'s answer from step 9.

## 15. Docs

- **`docs/reflex-notes.md`** (new). It is the consumer contract, in the
  `docs/ngx-notes.md` style: every claim cited or measured.
  - §1 What ships: low latency on `Swapchain`, `PclStats`, and present ids.
  - §2 Enable recipe, from step 1.
  - §3 The frame-loop contract: the single-present and FG sequences from spec
    D5, verbatim; one frame id per rendered frame, ≥ 1, shared with
    `BackbufferFrameId`; sleep once per rendered frame; PRESENT markers on the
    presenting thread; KHR present ids are not frame ids.
  - §4 PCL integration: Initialize/Shutdown, the ping hook and SDL3 example,
    TriggerFlash on left click, all vendors, and why not F13.
  - §5 NVIDIA's obligations, quoted:
    - default On (Reflex l. 1017);
    - cannot be disabled with FG on (UI l. 149-150);
    - Boost needs LowLatency (Reflex l. 428);
    - markers always sent (l. 1025);
    - Reflex UI hidden on other IHVs (l. 1031);
    - the QA checklist pointer.
  - §6 Licensing facts and OPEN-1, stated, not settled.
  - §7 Measured, from step 14.
  - §8 Not provided: out-of-band queues, explicit submission attribution
    (ignored on revision 2), `VkLatencySurfaceCapabilitiesNV`,
    `vkWaitForPresentKHR`, and `VK_AMD_anti_lag`.
- **`README.md`** and **`src/Ahjo.Vulkan/README.md`**: one feature line each,
  linking `docs/reflex-notes.md`.
- **`docs/benchmarks.md`**: add the six rows from step 12 to the baseline table,
  with the host and the minimum-of-N discipline the file already uses.
  `LowLatencyBenchmarks` rows get the driver-dependency note (NVIDIA RTX, LL2).
- **`docs/aot-notes.md`**: one entry saying PCL Stats is raw `advapi32`
  TraceLogging with an `[UnmanagedCallersOnly]` callback, deliberately not
  `EventSource`, because `EventSourceSupport=false` under PublishAot (spec E8).

## 16. Review and PR

- Run the `vulkan-validation-reviewer` agent; the diff touches `Rendering/`
  present and create paths. Run `bench-coverage-checker`; step 12 must cover
  every new per-frame member: `LatencySleep`, `SetLatencyMarker`,
  `GetLatencyTimings`, `Present(…, ulong)` and `PclStats.Marker`.
- `dotnet build -c Release` with zero warnings, plus the full
  `Ahjo.Vulkan.Tests` run on the 4070 Ti.
- PR title `Rendering: NVIDIA Reflex low latency (VK_NV_low_latency2) + PCL Stats`.
  The body:
  - says `Closes #261`;
  - links the spec;
  - lists the step-14 measurements;
  - states that this unblocks #253 PR C;
  - names the API additions (no breaks: the new field defaults false and the
    new overloads are additive);
  - lists OPEN-1 for the human.
