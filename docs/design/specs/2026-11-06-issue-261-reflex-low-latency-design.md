# `Ahjo.Vulkan` — NVIDIA Reflex Low Latency over native `VK_NV_low_latency2`, plus PC Latency Stats over direct ETW TraceLogging

Issue #261. Gates #253 PR C (`samples/HelloFrameGen`, #253 plan steps 17b and
19i) and pekkah/ahjo#2783. Must land consistently with #253 (spec D5 two-present
loop, D6, F1, F3, OPEN-6), #120/#222 (`SwapchainState` and the shared
`MapPresentationResult` path), #111 (retired swapchains), #121
(`DeviceFunctionTable`), #209/#205 (stack spans reach every per-frame entry
point), and #32 (wrapper tests are Windows-only).

Implementation plan: `../plans/2026-11-06-issue-261-reflex-low-latency.md`.

Sources cited below, with the revision read:

- **Vulkan spec**: `KhronosGroup/Vulkan-Docs` `main` @ `fa30d4e3`,
  `chapters/VK_KHR_surface/wsi.adoc` ("wsi l. N") and
  `appendices/VK_NV_low_latency{,2}.adoc`. The older text is quoted from tag
  `v1.4.340` ("wsi@340 l. N"). The pinned registry is
  `native/include/vulkan/vk.xml` (Vulkan-Headers `vulkan-sdk-1.4.363.0`,
  `Directory.Build.props`), which carries `VK_NV_LOW_LATENCY_2_SPEC_VERSION` 3
  (`vk.xml:29891`).
- **NVIDIA-RTX/REFLEX** `main` @ `c2522cee` (2026-09-01):
  `Reflex_Stats/pclstats.h` ("pclstats.h:N"), `license.txt` ("licence l. N"),
  `Reflex_Vulkan/inc/NvLowLatencyVk.h`, and the *NVIDIA Reflex SDK Integration
  Guide* v1.6 PDF extracted with `pdftotext -layout` ("Reflex l. N", the same
  extraction #253 F1 cites).
- **NVIDIA-RTX/Streamline** tag `v2.14.16` (MIT, `license.txt`):
  `source/plugins/sl.reflex/reflexEntry.cpp`, `source/platforms/sl.chi/vulkan.cpp`,
  `include/sl_pcl.h`, `source/plugins/sl.pcl/pclImpl.h`.
- **GameTechDev/PresentMon** `main` @ `362f2d93` (MIT): `PresentData/ETW/Nvidia_PCL.h`,
  `PresentData/PresentMonTraceConsumer.cpp`, `PresentData/PresentMonTraceSession.cpp`.
  PresentMon is the PCL consumer #253 D9 already measures with.
- **dotnet/runtime** `release/10.0`: `src/coreclr/nativeaot/BuildIntegration/Microsoft.NETCore.Native.targets`,
  `System.Private.CoreLib/.../Tracing/EventSource.cs` and `TraceLogging/TraceLoggingEventSource.cs`.
- **microsoft/tracelogging** `main`: `etw/cs/traceloggingdynamic/TraceLoggingDynamic.cs`
  ("tld.cs:N"), Microsoft's C# implementation of TraceLogging over `EventWriteTransfer`.
- **This machine**: `vulkaninfoSDK` (SDK 1.4.363.0), 2026-11-06. RTX 4070 Ti,
  driver 616.92, plus an AMD Radeon iGPU (driver 2.0.353).

---

## Problem

**P1. Nothing in the wrapper reaches `VK_NV_low_latency2`.** The five entry
points are bound as static `[DllImport]`s (`src/Ahjo.Vulkan.Native/Generated/Vk.cs:3308-3320`),
but the loader does not export extension commands through `vulkan-1.dll`, so
calling them throws `EntryPointNotFoundException`. The repo's rule is that
device-extension entry points resolve through `vkGetDeviceProcAddr` in a gated
`DeviceFunctionTable` block (`src/Ahjo.Vulkan/Internal/DeviceFunctionTable.cs:31-60`).
That table has four such blocks (mesh shader `:501`, acceleration structure
`:525`, EXT and KHR device fault `:566`, `:579`) and none for low latency.
`DeviceExtensionNames` has no low-latency or present-id name
(`src/Ahjo.Vulkan/Internal/DeviceExtensionNames.cs:44-71`).

**P2. A swapchain cannot opt in, and would lose its opt-in on resize.**
`VK_NV_low_latency2` is swapchain-scoped. `VkSwapchainLatencyCreateInfoNV` must
be chained into `VkSwapchainCreateInfoKHR` (wsi l. 9632-9648), and
`vkSetLatencySleepModeNV` sets state "on `swapchain`" (wsi l. 9134-9156).
`Swapchain.CreateOrRecreate` builds `VkSwapchainCreateInfoKHR` with no `pNext`
(`src/Ahjo.Vulkan/Rendering/Swapchain.cs:803-821`). `SwapchainDescription` has
no field for the opt-in (`src/Ahjo.Vulkan/Rendering/SwapchainDescription.cs:22-87`).
Every `Recreate` creates a new `VkSwapchainKHR` (`Swapchain.cs:409-414`). If the
latency create-info and sleep mode are not re-applied there, a window resize
turns Reflex off without anyone asking. NVIDIA's rule is that the mode flag is
"the only way" Reflex gets disabled (Reflex l. 393-394), and Reflex must not be
user-disableable while FG is on (#253 F1, UI l. 149-150).

**P3. A present id cannot be attached.** `Swapchain.Present(Queue, uint, in BinarySemaphore)`
builds `VkPresentInfoKHR` with no `pNext` (`Swapchain.cs:701-709`), so
`VkPresentIdKHR` cannot be chained. The issue asks for an additive path that
leaves the existing overloads unchanged.

**P4. Nothing says how markers map onto frame generation's two presents.**
#253's D5 loop presents twice per rendered frame (generated `i0`, then real
`i1`). #253 plan step 17b leaves this explicitly to this spec: "how present
markers and present ids map onto *two* presents per rendered frame … If it does
not, stop and ask."

**P5. PC Latency (PCL) Stats does not exist.** NVIDIA's guidelines want PCL
markers "always enabled on all vendor GPUs" (UI l. 142-143, #253 F1). PCL is an
ETW TraceLogging provider that "does not interact with NvAPI, DirectX, or Vulkan
in any way" (Reflex l. 182-184). Nothing in `src/` writes ETW, and the
obvious .NET route, `EventSource`, has three disqualifying properties under
Native AOT (E8).

---

## Evidence

### E1. The bindings are complete and match the pinned spec revision

All nine structs, both enums and the five commands are generated
(`Vk.cs:3308-3320`; `vk.xml:10673-10736`, `:14446-14463`). The registry entry
depends on `(VK_VERSION_1_2 or VK_KHR_timeline_semaphore) + (VK_KHR_present_id or VK_KHR_present_id2)`
and declares `nofeatures="true"` (`vk.xml:29889`). So enabling it needs a
present-id extension in the enabled list, and no feature struct of its own. The
wrapper enables `timelineSemaphore` by default (`DeviceFeatureChainConfigurer.cs:55-58`).

`VkLatencyMarkerNV` numbering (`vk.xml:14446-14459`): SIMULATION_START 0 …
PRESENT_END 5, INPUT_SAMPLE 6, TRIGGER_FLASH 7, OUT_OF_BAND_RENDERSUBMIT_START 8,
…_END 9, OUT_OF_BAND_PRESENT_START 10, …_END 11.

### E2. The hardware: driver 616.92 advertises `VK_NV_low_latency2` revision **2**, not 3

Measured with `vulkaninfoSDK` on the RTX 4070 Ti:

| Extension / feature | Value |
|---|---|
| `VK_NV_low_latency2` | revision **2** |
| `VK_NV_low_latency` (legacy) | revision 2 |
| `VK_KHR_present_id`, `VK_KHR_present_id2`, `VK_KHR_present_wait`, `VK_KHR_present_wait2` | revision 1 each |
| `presentId`, `presentWait` features | true |
| `VK_EXT_present_timing` | revision 3 |
| AMD Radeon iGPU on the same host | **no** `VK_NV_low_latency2`; has `VK_AMD_anti_lag` revision 1 |

Revision 3 (2026-04-20) "clarif[ies] frame attribution behavior … consistent
with implementations advertising older revisions, with one exception: explicit
submission attribution via `VkLatencySubmissionPresentIdNV` is only respected by
implementations advertising revision 3 or higher" (`VK_NV_low_latency2.adoc:44-52`;
wsi l. 9507-9515). **So on this driver `VkLatencySubmissionPresentIdNV` is
ignored. Implicit attribution is the only attribution there is.** The spec also
notes that "most applications do not need to pass" it (wsi l. 9517-9519), and
Streamline's own tagging path is dormant: "`areLatencyIdsEnabled()` always
returns false" (`sl.chi/vulkan.cpp:523-531`).

### E3. NVIDIA designates the native extension as the path for new code; the DLL is the legacy one

- Khronos, authored by NVIDIA: "`VK_NV_low_latency` provides legacy support for
  applications that make use of the NVIDIA Reflex SDK whereas new
  implementations should use the `VK_NV_low_latency2` extension"
  (`VK_NV_low_latency2.adoc:32-34`). The legacy commands that mirror
  `NvLowLatencyVk.dll`'s device-only signatures (`vkSetLatencySleepModeLegacyNV`,
  `vkLatencySleepLegacyNV`, `vk.xml:19187-19221`) "are only intended for use by
  compatibility layers, applications should not call these methods directly"
  (`VK_NV_low_latency.adoc:20-23`).
- Streamline creates the native backend first and falls back to the DLL only
  when it is unavailable *and* compiled with `SL_WITH_NVLLVK`:
  `m_reflex = CreateVkNvLowLatency2(...)`, then `#ifdef SL_WITH_NVLLVK if (!m_reflex) m_reflex = CreateNvLowLatencyVk(...)`
  (`sl.chi/vulkan.cpp:1179-1184`). It calls LL2 "the modern Reflex path"
  (`reflexEntry.cpp:289`) and gates it on Turing or newer (`:294-317`).
- `NvLowLatencyVk.h` is dated "Sept 23, 2020 … Version 1.0" and marked
  "PROPRIETARY and CONFIDENTIAL … provided solely under the terms … of an NVIDIA
  software license agreement and / or non-disclosure agreement"
  (`NvLowLatencyVk.h:9-11`, `:37-39`). Its markers have no OOB-present
  attribution semantics for frame generation (E5).
- The Reflex SDK licence supplement makes the SDK "licensed for you to develop
  applications only for use in systems with NVIDIA GPUs" (licence l. 161). Its
  distributable list is "the NVAPIs, .lib and .h files, Linux wrapper, and
  samples" (licence l. 163), which does not name the DLL. Whether the DLL falls
  under "NVAPIs" is not stated.
- The Reflex Integration Guide's documented Vulkan path is still the DLL
  (Reflex l. 207-215, 316-332) and never mentions the extension. Whether NVIDIA's
  verification accepts a native integration is #253 OPEN-6, carried here as OPEN-2.

### E4. The current spec decouples the latency `presentID` from `VkPresentIdKHR`; the older text did not

Current text, three times: the `presentID` in `VkSetLatencyMarkerInfoNV`,
`VkLatencyTimingsFrameReportNV` and `VkLatencySubmissionPresentIdNV` "is the
application-defined identifier of the application-rendered frame … It is not
related to presentIds passed via `VkPresentIdKHR::pPresentIds`" (wsi l. 9272-9282,
9404-9415, 9457-9465). "A single application-rendered frame may correspond to
multiple `vkQueuePresentKHR` calls" (wsi l. 9273-9275).

Text at `v1.4.340`: the marker `presentID` "is used to associate the timestamp
with a `vkQueuePresentKHR` command using `VkPresentIdKHR::pPresentIds`"
(wsi@340 l. 8931-8936), and a submission "must include" `VkLatencySubmissionPresentIdNV`
(wsi@340 l. 9093-9097). That changed in the 1.4.361 update (Vulkan-Docs commit
`20a9e589`). Revision 3 states that the new text describes the behaviour of
older implementations too (E2). We have not measured that on the revision-2
driver, so it is OPEN-4.

KHR present ids have their own rule: "A non-zero presentId must be greater than
any non-zero presentId passed previously … for the same swapchain"
(wsi l. 8317-8318; VUID-VkPresentIdKHR-presentIds-04999, l. 8332-8335). Each
`VkSwapchainKHR` starts at zero (l. 8305-8306). A non-NULL `pPresentIds` needs
the `presentId` feature (VUID-VkPresentInfoKHR-pNext-06235, l. 7682-7685).
**Two presents of one rendered frame therefore cannot share one KHR present id,
but they can share one latency `presentID`.**

### E5. The spec defines exactly how frame generation maps onto markers

- "This extension relies on grouping work into a series of application-rendered
  frames … application-rendered frames do not correspond one-to-one with
  `vkQueuePresentKHR` calls. Technologies such as frame generation can present
  multiple images for a single application-rendered frame" (wsi l. 9124-9132).
- "Intermediate layers, such as frame-generation implementations, may defer or
  replace `vkQueuePresentKHR` calls. Such layers should leave the original
  `PRESENT_START`/`PRESENT_END` markers in place and emit
  `OUT_OF_BAND_PRESENT_START`/`OUT_OF_BAND_PRESENT_END` markers surrounding each
  of their `vkQueuePresentKHR` calls" (wsi l. 9335-9343).
- Present attribution, first rule: "If an `OUT_OF_BAND_PRESENT_START` marker has
  been observed without an intervening `OUT_OF_BAND_PRESENT_END`, the frame's
  presentID is the presentID from that marker". The NOTE says this rule "allows
  frame generation implementations to map multiple `vkQueuePresentKHR` calls to
  the same presentID" (wsi l. 9578-9597).
- Submission attribution with no out-of-band queue and no explicit attribution:
  pending submissions are "delimited by `vkQueuePresentKHR` calls, and the group
  is attributed to that present's application-rendered frame" (wsi l. 9599-9630).
  Marking *any* queue out of band switches the delimiter device-wide to
  `PRESENT_END` markers (l. 9623-9626).
- Sleep: "Applications should call this function before input sampling, and
  exactly once between presents" (wsi l. 9203-9206). Read against the
  application-rendered-frame NOTE above, together with the Reflex guide's "This
  sleep call must only be made once per frame" (Reflex l. 517) and Streamline's
  one `slReflexSleep` per app frame with DLSS-G presenting twice, this means once
  per *rendered* frame. That reading is an inference (OPEN-3 measures it).
- `vkSetLatencySleepModeNV` and `vkSetLatencyMarkerNV` are exceptions to
  external synchronization: the swapchain "may be simultaneously used by other
  threads in calls to functions other than `vkDestroySwapchainKHR`"
  (wsi l. 9151-9156, 9253-9258). `signalSemaphore` "must be a timeline
  semaphore" (VUID-VkLatencySleepInfoNV-signalSemaphore-09361, l. 9227-9229).
- `vkGetLatencyTimingsNV` is the two-call array form since revision 2: a NULL
  `pTimings` returns the maximum count, otherwise `timingCount` is in/out, oldest
  entry first (wsi l. 9380-9386).

Reflex guide rules that shape the API: markers "always sent regardless of
Reflex Low Latency mode state" (Reflex l. 1025); "Do not stop calling other
Reflex functions when Reflex Low Latency mode is Off" (l. 394-395); Boost
requires the mode flag true (l. 428-429); the frame ID "must be unique within
the last 1000 frames" (l. 637); PRESENT markers come from the thread that
presents (l. 1323-1324).

**Sleep while Off: every NVIDIA source says keep calling it.** This was added
after the OPEN-7 measurement (below).
- **The Streamline plugin.** Its sleep path is gated only on
  `lowLatencyAvailable`, never on the mode (`reflexEntry.cpp:360-375`). Mode
  `Off` only sets `ctx.enabled = false` (`:474`) and forwards the mode
  (`:487-489`).
- **Streamline's own `VK_NV_low_latency2` backend**
  (`source/platforms/sl.chi/vknvll2.cpp` at v2.14.16):
  - `setSleepMode` maps Off to `lowLatencyMode = false`,
    `minimumIntervalUs = frameLimitUs` (`:210-225`). `frameLimitUs` defaults to
    0 (`include/sl_reflex.h:50`).
  - `sleep()` calls `vkLatencySleepNV` and waits on the semaphore whenever the
    swapchain was created latency-enabled. It skips **only** when there is no
    swapchain or it was not created with `latencyModeEnable` (`:294-340`). The
    wait is capped at 500 ms (`vulkan.h:54`).
  - Streamline injects `latencyModeEnable = VK_TRUE` into every swapchain once
    LL2 is enabled, whatever the mode (`reflexEntry.cpp:925-967`).
- **Streamline's QA list makes calling sleep while Off a verification item.**
  "`slReflexSleep` is called regardless of Reflex Low Latency mode state"
  (`ProgrammingGuideReflex.md:247`). With Reflex Off, the Verification HUD must
  show "`App_Called_Sleep = 1` (it should remain `1`)" (`:286-296`, `:374`).
- **The Reflex SDK's Vulkan header.** "It is recommended to call this function
  even when low latency mode is disabled and minimum interval is 0. Other
  features, such as the Maximum Frame Rate setting, could be enabled in the
  NVIDIA control panel and benefit from this" (`NvLowLatencyVk.h:262-264`). So
  a driver-side cap applied *in the sleep* is by design.
- **The one independent LL2 integration checked, DXVK** (`doitsujin/dxvk`
  `master` @ `829e377d`, `src/dxvk/dxvk_presenter.cpp`):
  - `latencySleep()` calls `vkLatencySleepNV` whenever the swapchain is
    latency-enabled, with no mode check (`:491-520`).
  - It avoids creating a latency swapchain only while the application has
    never set a non-default mode *and* sent no marker (`:443-450`, `:473-479`).
  - With low latency On it raises `minimumIntervalUs` to the display's frame
    interval itself (`:159-163`).

### E6. PCL Stats: the wire protocol

`pclstats.h` defines the provider, its events and the ping thread:

- Provider `"PCLStatsTraceLoggingProvider"`, GUID
  `{0d216f06-82a6-4d49-bc4f-8f38ae56efab}` (pclstats.h:41-45).
- Events: `PCLStatsInit` with no fields (`:129`); `PCLStatsEvent` with `Marker`
  as UInt32 and `FrameID` as UInt64 (`:136`); `PCLStatsInput` with one UInt32
  field named `IdThread`, `VirtualKey` or `MsgId` (`:65`, `:80`, `:86`);
  `PCLStatsFlags` with `Flags` UInt32, written on `EVENT_CONTROL_CODE_CAPTURE_STATE`
  (`:108-109`); and `PCLStatsShutdown` (`:150`). `TraceLoggingWrite` with no
  level argument means level 5 (verbose) and keyword 0.
- The ping thread wakes every 100-300 ms at random, and acts only while a
  session has enabled the provider (`:55-62`). With a thread id set, it posts a
  thread message (`:63-67`). Otherwise, if the foreground window belongs to this
  process, it posts `WM_KEYDOWN`/`WM_KEYUP` for F13-F15 when a virtual key is set
  (`:76-83`), or else the registered window message `"PC_Latency_Stats_Ping"`
  (`:84-88`, `:119`).
- Marker numbering (`:16-32`): 0-5 as Vulkan, INPUT_SAMPLE 6 deprecated,
  TRIGGER_FLASH 7, **PC_LATENCY_PING 8**, OUT_OF_BAND_RENDERSUBMIT_START 9, _END
  10, **OUT_OF_BAND_PRESENT_START 11, _END 12**. From 8 upward this is off by one
  against `VkLatencyMarkerNV` (E1). A shared enum would stamp every OOB marker
  wrong.
- Ping handling: "On seeing the ping message, the application must call
  `PCLSTATS_MARKER(PCLSTATS_PC_LATENCY_PING, frameID)`, either right away, or set
  a flag for the next simulation", using frameID + 1 if simulation has not
  started yet (Reflex l. 819-831). "PC_LATENCY_PING should happen close to or
  before SIMULATION_START with the same frame ID" (l. 1301-1305).

What the consumer actually reads, from PresentMon: it enables the GUID at
`TRACE_LEVEL_VERBOSE` with no filtering (`PresentMonTraceSession.cpp:988-991`)
and decodes TraceLogging self-describing metadata
(`PresentMonTraceConsumer.cpp:3400`). It matches the events by name:
`PCLStatsEvent`, `PCLStatsInput`, `PCLStatsInit` and `PCLStatsShutdown`
(`:3412-3574`), and reads the `Marker` and `FrameID` fields (`:3418-3419`). It
does **not** read `PCLStatsEventV2`. Once it sees any `OutOfBandPresentStart`
(11), it attaches PCL data to presents by the *first* OOB-present-start time per
frame id instead of `PresentStart` (`:3541-3565`, `:2357-2371`). For PC
latency, it attaches that frame to the OS present whose start is the *closest
after* the selected time, among frames not yet assigned
(`ExtractAppTimingData`, `:3920-3958`). A frame id of 0 is treated as "none"
(`:2575`).

**The NVIDIA driver writes PCL out-of-band present markers itself.** Measured
2026-11-06 with an elevated live ETW session on the PCL GUID: HelloCube, one
present per frame, Reflex On, driver 616.92, 20 s; recorded in
`docs/reflex-notes.md` §7.5.
- The wrapper's main thread wrote markers 0-5 and 8 (1200 each).
- A **second thread of the same process** wrote `PCLStatsEvent` 11 and 12
  (OOB_PRESENT_START/END) **1198 times each**, carrying the application's
  frame ids. HelloCube sends no OOB marker.
- Per frame, the driver's 11/12 pair arrives **46-65 ms after** the
  application's PRESENT_END: in frames 100, 101 and 700, the application's
  PRESENT_START → PRESENT_END took about 60 µs, and the driver's 11 followed
  46.4, 46.4 and 65.1 ms later. In all 1198 frames the driver pair falls after
  the application's 5, never inside 4..5. That is the driver's deferred present
  thread bracketing the real OS present, the "intermediate layer" behaviour
  of wsi l. 9335-9343.
- A second `PCLStatsInit` appeared 150 ms **before** HelloCube's own
  `PclStats.Initialize`, which runs after device and swapchain creation
  (`samples/HelloCube/Program.cs:208-227` vs `:407`). A second
  `PCLStatsShutdown` appeared 56 ms **after** the sample's `PclStats.Shutdown`
  (`:661`), when the swapchain and device are disposed. So the driver registers
  its own instance of the provider in-process, on the thread that created the
  swapchain.
- All 96 `PCLStatsInput` pings came from one thread, the wrapper's ping thread;
  the hook counted 96. The driver runs no ping thread of its own.

Streamline does not suppress application PCL markers when LL2 is active.
`sl.reflex` sends every application marker to the driver and then to PCL
(`reflexEntry.cpp:382-428`), and `sl.pcl` routes through `sl.reflex` whenever
it is loaded (`pclImpl.h:92-135`). But a Streamline application never sends
OOB markers itself. Streamline's own OOB present markers go to the driver
through `setAsyncFrameMarker` → `setReflexMarker` (`sl.chi/vulkan.cpp:3756-3777`),
and neither `vulkan.cpp` nor `vknvll2.cpp` makes any PCL call. So on Vulkan, Streamline gets PCL 11/12 only from
the driver, which is what the capture shows.

Where the facts are also published under MIT: the GUID and marker values in
PresentMon's `Nvidia_PCL.h:10-23`; the full marker enum, the thread-id and
virtual-key options and `statsWindowMessage` in Streamline's `include/sl_pcl.h:35-84`;
and the event and field names in PresentMon's consumer. Only the provider *name*
string, the ping *message* string and the `PCLStatsInput`/`PCLStatsFlags` field
names appear solely in `pclstats.h`, which carries NVIDIA's proprietary notice
(`pclstats.h:1-6`). Streamline's copy of `pclstats.h` carries the same notice.
It is not MIT despite living in an MIT repo.

### E7. Direct ETW from C# is a known, small, AOT-clean shape

`TraceLoggingDynamic.cs` does it with `EventRegister`,
`EventSetInformation(handle, 2 /*EventProviderSetTraits*/, traits)` (tld.cs:189-205),
and `EventWriteTransfer` with data descriptors typed 2 (provider metadata), 1
(event metadata) and 0 (payload) (tld.cs:451-469), over `EVENT_DESCRIPTOR`
channel 11 (tld.cs:2355-2369). The repo already uses
`[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]` callbacks
(`src/Ahjo.Vulkan/Lifecycle/Instance.cs:593`) and blittable `[DllImport]`s.

### E8. `EventSource` is disqualified three ways

1. Native AOT turns it off: `<EventSourceSupport Condition="$(EventSourceSupport) == ''">false</EventSourceSupport>`
   (`Microsoft.NETCore.Native.targets:38`). The switch feeds
   `EventSource.IsSupported` (`EventSource.cs:285-289`), and both initializing
   constructors skip initialization when it is false (`EventSource.cs:758`, `:1622`).
   An AOT-published Ahjo game would emit no PCL markers without any error,
   unless the *application* turned the switch on process-wide.
2. The TraceLogging-shaped API needed for named fields, `Write<T>`, is
   `[RequiresUnreferencedCode]` (`TraceLoggingEventSource.cs:140`, `:180`,
   `:222`). Invariant 2 forbids that.
3. The GUID: an `EventSource` named `PCLStatsTraceLoggingProvider` hashes to
   `83d5d0db-7916-50a4-751e-9f3412434fc2` (computed with the EventSource
   name-hash algorithm). Consumers enable `0d216f06-…`, so the GUID needs
   `[EventSource(Guid = …)]`. That is possible, but it does not rescue 1 or 2.

### E9. Consumer audit

`findReferences` (C# LSP) on the two `Present` overloads and `Recreate`:

- `Present(Queue, uint)` has **6** code call sites: `samples/HelloDlaa/Program.cs:705`,
  `samples/HelloTriangle/Program.cs:225`, `samples/HelloVmaWindowed/Program.cs:426`,
  `samples/HelloCube/Program.cs:510`, `tests/Ahjo.Vulkan.Tests/WindowedValidationTests.cs:128`
  and `tests/Ahjo.Vulkan.Tests/SwapchainTests.cs:322`. The other hits are
  `cref`s.
- `Present(Queue, uint, in BinarySemaphore)` has **0** external callers. The
  only caller is the forward at `Swapchain.cs:656`.
- `Recreate` has 4 sample call sites (`HelloCube:733`, `HelloTriangle:300`,
  `HelloDlaa:826`, `HelloVmaWindowed:536`) plus tests.
- `new SwapchainDescription` appears 8× in samples and 30× in tests (grep).

All of these keep compiling under this design. The new field defaults to
`false`, and the new overloads take an extra `ulong`. The only consumer of the
new API is #253 PR C plus the Ahjo engine (pekkah/ahjo#2783).

### E10. Hot paths and CI reality

`LatencySleep`, `SetLatencyMarker` (six or more calls per frame, ten per FG
frame), `Present` with an id and `PclStats.Marker` run every frame, so they are
invariant 3 hot paths and need `[MemoryDiagnoser]` rows. The Windows CI lane has
no answering ICD (#152, `.github/CLAUDE.md:19`), so every `VK_NV_low_latency2`
test is hardware-gated. PCL has no Vulkan dependency, so it is the half CI can
run. The benchmark precedent for hardware-only per-frame paths is
`DlssFrameGenerationEvaluateBenchmarks`, whose Setup throws rather than skipping
(`tests/Ahjo.Vulkan.Benchmarks/DlssFrameGenerationEvaluateBenchmarks.cs:25-27`).

---

## Decision

### D1. Native `VK_NV_low_latency2` through `DeviceFunctionTable`. Not `NvLowLatencyVk.dll`.

The extension is what NVIDIA's own spec text (E3) and its own shipping
middleware prefer. It is the only path whose marker semantics define frame
generation's multi-present attribution (E5). It goes through the same
`vkGetDeviceProcAddr` table every other device extension uses (P1). It ships
nothing: no NVIDIA binary, no SDK header, no licence supplement in the
dependency graph (D8).

Four pointers resolve in a new gated block: `vkSetLatencySleepModeNV`,
`vkLatencySleepNV`, `vkSetLatencyMarkerNV` and `vkGetLatencyTimingsNV`. A device
that enabled the extension but cannot resolve one of them throws at
construction (`ResolveExtensionRequired`).

#### Why not the alternatives

- **`NvLowLatencyVk.dll`.** Khronos calls the path behind it legacy and
  compatibility-layer-only (E3). It is a confidential 2020 binary whose
  redistributability is not clearly granted (licence l. 163). It has no
  frame-generation attribution rule. It would add a ninth native artifact and a
  `NativeLibrary` load path for a consumer that only runs on drivers new enough
  for LL2 anyway (Turing+, E3).
- **Both, with a runtime fallback (Streamline's shape).** Streamline needs the
  fallback for pre-LL2 drivers it must still support. Ahjo's FG target is RTX
  40+ on a 2026 driver (#253 E12). The fallback doubles the tested surface for a
  configuration no consumer runs.
- **`VK_NV_low_latency` legacy commands directly.** "Applications should not
  call these methods directly" (E3).
- **`VK_AMD_anti_lag` alongside.** It is a different vendor's model (one
  `vkAntiLagUpdateAMD` call, wsi l. 8996+) and is not required for FG. It is a
  future sibling under the spec's "Lag Control" section, filed separately if the
  engine wants it.

### D2. Device enablement: two public names and a recipe, no new feature push

- `VulkanExtensions.NvLowLatency2` (`"VK_NV_low_latency2"u8`) and
  `VulkanExtensions.KhrPresentId` (`"VK_KHR_present_id"u8`), with the enable
  recipe in their remarks: both names go in `DeviceDescription.Extensions`
  (present_id is LL2's creation dependency, E1). `timelineSemaphore` is already
  on by default. Push `VkPhysicalDevicePresentIdFeaturesKHR.presentId = 1` only
  if the application will pass non-zero present ids (D4).
- `Device.IsNvLowLatency2Enabled`, true when the gated block resolved. Also
  `Device.IsPresentIdEnabled`, true when `VK_KHR_present_id` was in the enabled
  list. Both are decided once in the constructor, the same way as
  `MemoryBudgetExtensionEnabled` (`Device.cs:89-92`).
- `VK_KHR_present_id`, not `VK_KHR_present_id2`. Either satisfies the
  dependency. `present_id2` additionally needs `VK_KHR_get_surface_capabilities2`
  and a per-surface `presentId2Supported` query (VUID-VkPresentInfoKHR-presentId2Supported-10822,
  wsi l. 7708-7710). That is more ceremony for no gain on the target driver
  (E2).

### D3. The low-latency API lives on `Swapchain`, in a new partial file `Swapchain.Latency.cs`

Every LL2 call takes `(device, swapchain)`, and `Swapchain` is the object that
swaps its handle on `Recreate`. So `Swapchain` is the only place that can keep
the opt-in and the sleep mode alive across a resize (P2). The surface:

```csharp
// SwapchainDescription
public bool LowLatency;                       // chains VkSwapchainLatencyCreateInfoNV { latencyModeEnable = VK_TRUE }

// Swapchain (Swapchain.Latency.cs)
public bool             IsLowLatencyEnabled { get; }   // fixed at construction
public LatencySleepMode LatencySleepMode    { get; }   // last value set; default Off
public void      SetLatencySleepMode(in LatencySleepMode mode);
public WaitState LatencySleep(TimeSpan timeout);
public void      SetLatencyMarker(LatencyMarker marker, ulong frameId);
public int       GetLatencyTimings(scoped Span<LatencyTimings> destination);
public int       LatencyTimingCapacity { get; }

public readonly record struct LatencySleepMode(bool LowLatency, bool Boost, uint MinimumIntervalUs)
{ public static LatencySleepMode Off { get; } public static LatencySleepMode On { get; } public static LatencySleepMode OnPlusBoost { get; } }
```

The rules:

- **The opt-in is a construction-time property, sticky across `Recreate`.**
  `LowLatency = true` on a device without the extension throws
  `InvalidOperationException` naming `VK_NV_low_latency2`. `Recreate` with a
  `desc.LowLatency` different from the constructed value throws
  `ArgumentException`, the same rule as the Surface check (`Swapchain.cs:323-326`).
  `CreateOrRecreate` chains the create-info on every create. After every
  successful create it re-applies the stored sleep mode. The spec does not say
  whether mode survives `oldSwapchain`, so the re-apply is defensive and
  harmless if redundant. It also resets the per-handle KHR present-id high-water
  mark (D4).
- **The sleep semaphore is wrapper-owned.** It is one timeline semaphore per
  `Swapchain` from the existing internal pool (`Swapchain.cs:234`,
  `SemaphorePool.AcquireTimeline`), plus a `ulong` counter. `LatencySleep`
  increments the counter, calls `vkLatencySleepNV` with `{sem, value}`, then
  `TimelineSemaphore.WaitFor(value, timeout)`. That is exactly the guide's
  recipe (Reflex l. 530-557) with no caller-visible semaphore. The semaphore is
  not tied to the `VkSwapchainKHR`, so it survives `Recreate` and is freed by
  the pool in `Dispose`.
- **The per-frame calls degrade, the setup call does not.** On a swapchain
  without the opt-in, `LatencySleep` returns `WaitState.Signaled` at once,
  `SetLatencyMarker` skips the Vulkan call but still emits the PCL marker (D7),
  and `GetLatencyTimings` returns 0. That lets one frame loop run on every
  vendor, which is what "always call, even when off" requires (E5) and what
  Streamline does (`reflexEntry.cpp:362`). **On an opted-in swapchain
  `LatencySleep` always calls `vkLatencySleepNV`, including in mode Off with no
  interval.** This matches Streamline's LL2 backend, DXVK, the Reflex SDK
  header, and the `App_Called_Sleep = 1`-while-Off QA item (E5, "Sleep while
  Off"). Any pacing the driver applies inside that sleep while Off is the
  driver's, for example an NVIDIA Control Panel frame cap or VSync override. A
  Streamline title on the same driver gets the same. `SetLatencySleepMode` on a
  non-opted-in swapchain throws `InvalidOperationException`. It is the UI/setup
  call, and the UI must hide Reflex on other vendors (Reflex l. 1031).
  `SetLatencySleepMode` on a minimized swapchain with no handle stores the mode
  and applies it at the next create.
- **`frameId` must be non-zero.** It is checked with
  `ArgumentOutOfRangeException.ThrowIfZero`. PresentMon treats 0 as "no frame"
  (E6), and a zero latency `presentID` has clearing semantics in the submission
  struct (wsi l. 9500-9502).
- **`LatencyMarker` is the Vulkan numbering, minus three values.** It has
  SimulationStart 0 … PresentEnd 5, TriggerFlash 7, OutOfBandPresentStart 10 and
  OutOfBandPresentEnd 11. InputSample is omitted because it is deprecated
  (Reflex l. 795). The two OOB render-submit values are omitted because they only
  mean something on an out-of-band queue, which this design does not expose
  (below).
- **`LatencyTimings`** is a `readonly struct` laid out exactly as
  `VkLatencyTimingsFrameReportNV`, with read-only `ulong` properties. So
  `GetLatencyTimings` casts the caller's span to the native type, sets
  `sType`/`pNext`, and makes one call with no copy and no allocation.
  `LatencyTimingCapacity` is the NULL-`pTimings` query (E5).
- **Threading.** Markers and the sleep-mode call may come from a thread other
  than the one that presents (E5). The wrapper reads `_handle` once per call.
  They must not overlap `Recreate` or `Dispose`, which is the spec's own
  `vkDestroySwapchainKHR` exception. PRESENT markers go on the presenting
  thread (Reflex l. 1323).

#### Why not the alternatives

- **A separate `LowLatencyController` object.** It would still need to be told
  about every `Recreate` to re-apply the mode, which is the coupling P2 is about,
  and it would split one swapchain's state across two objects.
- **Caller-owned sleep semaphore.** It exposes a value counter the caller must
  keep monotonic and correct for nothing; the guide's recipe is fixed.
- **Throwing from per-frame calls when not opted in.** That forces every
  consumer to branch on vendor in the frame loop, and a forgotten branch on AMD
  crashes the frame instead of just not sleeping.
- **Skipping `vkLatencySleepNV` when the mode is Off and the interval is 0.**
  This was considered after OPEN-7. Neither Streamline nor DXVK does it (E5),
  NVIDIA's header recommends against it, and it would fail the
  `App_Called_Sleep` check NVIDIA's QA runs with Reflex Off. It is only
  reconsidered if the OPEN-7 control shows the sleep itself costs throughput
  that the identical loop without it does not, and then by a human.
- **`vkQueueNotifyOutOfBandNV` and `VkLatencySubmissionPresentIdNV` now.**
  Explicit submission attribution is ignored on the target driver (E2), and
  marking any queue out of band changes the frame delimiter for the whole device
  (E5). Neither has a consumer. Both are additive later.
- **`VkLatencySurfaceCapabilitiesNV`.** Querying it needs
  `VK_KHR_get_surface_capabilities2`, which the wrapper does not enable, and
  nothing consumes the answer.

### D4. Present ids: two additive overloads on the existing result path

```csharp
public AcquireResult Present(Queue queue, uint imageIndex, ulong presentId);
public AcquireResult Present(Queue queue, uint imageIndex, in BinarySemaphore waitSemaphore, ulong presentId);
```

Both route through one private `PresentCore(queue, imageIndex, waitRaw, presentId)`.
The existing two overloads call it with `presentId: 0` and **no** `pNext`, so
their native call is unchanged. A non-zero id chains
`VkPresentIdKHR { swapchainCount = 1, pPresentIds = &id }` from stack locals.
Results map through the same `MapPresentationResult` (#120/#222).

Validation, all cheap branches:

- an id is passed but `Device.IsPresentIdEnabled` is false → `InvalidOperationException`;
- `presentId != 0 && presentId <= _lastPresentId` → `ArgumentOutOfRangeException`,
  so VUID-04999 surfaces as an exception rather than a validation-layer
  message;
- `_lastPresentId` resets to 0 on every create, because a new `VkSwapchainKHR`
  starts at zero (E4). A caller that keeps one monotonic counter across resizes
  is always valid.

Present id `0` is legal and means "no id" (wsi l. 8312-8316).

**One overload hazard, accepted.** `Present(q, i, default)` becomes ambiguous
(CS0121) between `ulong` and `in BinarySemaphore`. The semaphore overload has
zero external callers (E9), so nothing breaks. The overload docs tell callers
to write `0UL` or `default(BinarySemaphore)`.

**KHR present ids are not Reflex frame ids.** They are per `vkQueuePresentKHR`
and strictly increasing per swapchain. The latency `presentID` is per rendered
frame. Nothing in the LL2 API takes a KHR present id (E4). The present-id path
exists for the issue's exit criterion and for later `VK_KHR_present_wait`
feedback pacing (#253 F3). `vkWaitForPresentKHR` itself is out of scope here,
and is filed with a pacer when one is designed.

#### Why not the alternatives

- **An options struct (`in PresentOptions`).** One `ulong` does not justify a
  type. Present timing (#253 F3) would add its own overload with its own
  struct, on the same `PresentCore`.
- **Optional parameter on the existing overloads.** That is a binary break and
  changes those overloads, which the issue rules out.
- **Auto-generating ids inside `Swapchain`.** The caller cannot then name the
  id it would wait on later, and that is the only reason to have one.

### D5. Frame generation: one frame id per rendered frame, OOB-present markers around each present

This is the answer #253 plan step 17b asks for. Every rendered frame N gets one
`ulong frameId` (N ≥ 1). The same value goes to every Vulkan latency marker,
every PCL marker and `DlssFrameGenerationInputs.BackbufferFrameId`
(`DlssFrameGenerationInputs.cs:205`, already a `ulong`). Applied to #253 D5's
loop:

```
swap.LatencySleep(timeout)                          once per RENDERED frame (E5)
[PclStats: pending ping is emitted as PC_LATENCY_PING(N) first]
SetLatencyMarker(SimulationStart, N)
  input, simulation
SetLatencyMarker(SimulationEnd, N)
SetLatencyMarker(RenderSubmitStart, N)
  render frame N, FG evaluate, blit OutputInterpolated → i0, Submit2(rec1)
SetLatencyMarker(PresentStart, N)                   the app's logical present (E5 "leave the original in place")
  SetLatencyMarker(OutOfBandPresentStart, N)
  swap.Present(queue, i0 [, khrId++])               generated frame
  SetLatencyMarker(OutOfBandPresentEnd, N)
  acquire i1, blit OutputReal → i1, fc.Submit(rec2)
  SetLatencyMarker(OutOfBandPresentStart, N)
  swap.Present(queue, i1 [, khrId++])               real frame
  SetLatencyMarker(OutOfBandPresentEnd, N)
SetLatencyMarker(PresentEnd, N)
SetLatencyMarker(RenderSubmitEnd, N)                after PRESENT_END is the guide's typical order (Reflex l. 1288-1292)
```

Why this is correct, by the spec's own rules (E5):

- Both presents are attributed to frame N by present rule 1. Both OOB pairs
  carry N.
- With one queue and no out-of-band queue, `rec1` is delimited by present `i0`
  and `rec2` by present `i1`, so both submissions are attributed to N.
- PRESENT_START/END stay in place, and the OOB pairs surround "each of their
  `vkQueuePresentKHR` calls", which is the spec's instruction for a
  frame-generation layer. Under #253 D5 the application *is* that layer.
- On the PCL side, the OOB markers are **not** forwarded on an opted-in
  swapchain (D7). The driver writes PCL 11/12 itself around its own deferred
  OS presents, with the frame id it attributed (E6). Under the attribution
  rules that is N for both presents, but this is unmeasured with two presents
  (OPEN-8). An application-side 11 would be stamped at `vkQueuePresentKHR`
  time, tens of milliseconds before the OS present. Because PresentMon keeps
  the *first* 11 per frame id and matches frames to OS presents by
  closest-before time, that early stamp would make PresentMon take the
  application's timestamp instead of the driver's and could attach the wrong
  frame to a present.

With FG off (`--mode off`), the loop is the same with one present and no OOB
pair. Optional KHR present ids, if the caller uses them, are `2N`/`2N+1` or any
running counter. Never use N itself twice (E4).

**Verified by measurement, not by this spec:** that the revision-2 driver
honours OOB attribution for two presents, that once-per-rendered-frame sleep is
what NVIDIA verifies, and that the Reflex Test Utility passes with FG on.
PR C step 19i measures these (OPEN-3).

#### Why not the alternatives

- **PRESENT_START/END around each present, no OOB markers.** The second
  present's PRESENT_START would open a new latency frame under present rule 2,
  or present rule 3 would make it a frame "with no presentID". The driver would
  then see two application-rendered frames per rendered frame and pace for
  twice the rate.
- **PRESENT_START/END around only the real present.** Attribution would still
  work, through the OOB rule, but `presentStartTimeUs` would then exclude the
  generated present, and PCL's present region would not cover the first frame
  displayed.
- **A wrapper-owned "present FG pair" helper.** #253 D5 already rejected a
  `PresentWithGeneratedFrame` API, and this design keeps that decision. The
  marker sequence is documented in the consumer contract (plan step 12) and
  implemented in the sample.

### D6. PCL Stats: a static `PclStats` over direct `advapi32` ETW, Windows-only, no-op elsewhere

```csharp
public static class PclStats
{
    public static bool IsInitialized     { get; }
    public static bool IsProviderEnabled { get; }   // an ETW session is listening
    public static uint PingMessageId     { get; }   // RegisterWindowMessageW("PC_Latency_Stats_Ping"); 0 if not initialized or not Windows
    public static void Initialize(in PclStatsOptions options);
    public static void Shutdown();
    public static void Marker(PclMarker marker, ulong frameId);
    public static bool IsPingMessage(uint message);  // message != 0 && message == PingMessageId
    public static void NotifyPingReceived();         // sets the pending-ping flag
}
public struct PclStatsOptions { public uint PingThreadId; public PclPingVirtualKey PingVirtualKey; }
public enum PclPingVirtualKey : ushort { None = 0, F13 = 0x7C, F14 = 0x7D, F15 = 0x7E }
public enum PclMarker : uint { SimulationStart = 0, SimulationEnd = 1, RenderSubmitStart = 2, RenderSubmitEnd = 3,
    PresentStart = 4, PresentEnd = 5, TriggerFlash = 7, PcLatencyPing = 8, OutOfBandPresentStart = 11, OutOfBandPresentEnd = 12 }
```

- **Wire format.** It matches E6 byte-for-byte in meaning: the provider GUID
  and name; `PCLStatsInit` and `PCLStatsShutdown` with no fields;
  `PCLStatsEvent {Marker: UInt32, FrameID: UInt64}`;
  `PCLStatsInput {IdThread|VirtualKey|MsgId: UInt32}`; and
  `PCLStatsFlags {Flags: UInt32 = 0}` on capture-state. Events use level 5,
  keyword 0, opcode 0 and channel 11. Flags are always 0, because the wrapper
  always emits present markers (`PCLSTATS_NO_PRESENT_MARKERS` exists for engines
  that cannot, pclstats.h:34-37). V2 and V3 marker events are not emitted, since
  PresentMon reads neither (E6).
- **Mechanics, the TraceLoggingDynamic shape (E7).** `EventRegister` takes an
  `[UnmanagedCallersOnly]` enable callback that tracks enable and disable and
  answers capture-state. `EventSetInformation(EventProviderSetTraits)` gets the
  provider-traits blob. `EventWriteTransfer` gets five stack
  `EVENT_DATA_DESCRIPTOR`s: provider traits (type 2), event metadata (type 1),
  and the payload. The metadata blobs are compile-time `static ReadOnlySpan<byte>`
  RVA constants, so per-marker cost is one volatile read and, when a session is
  listening, one `EventWriteTransfer`, with zero allocations. All
  `[DllImport]`s are blittable.
- **The ping thread** mirrors pclstats.h:53-97. It is one background managed
  `Thread` created in `Initialize`, which is setup-time allocation. It waits
  `100 + Random.Shared.Next(200)` ms on a quit event, and posts only while
  `IsProviderEnabled`. With `PingThreadId` set it calls `PostThreadMessageW`;
  otherwise, when this process owns the foreground window, it posts
  `WM_KEYDOWN`/`WM_KEYUP` for the virtual key if one is set, or else the
  registered message. It writes the matching `PCLStatsInput` event each time.
- **Pump integration is the application's job, made cheap.** The window pump
  checks `PclStats.IsPingMessage(msg)` and calls `NotifyPingReceived()`. The
  next `Marker(SimulationStart, N)` first emits `PcLatencyPing(N)` and clears the
  flag. That is the guide's "set a flag for the next simulation" with the
  correct frame id by construction (E6). For SDL3, the hook is
  `SDL_SetWindowsMessageHook`, which `ppy.SDL3-CS` 2026.1002.1 exposes (OPEN-5
  confirms it sees the registered message). The virtual-key mode exists because
  pclstats.h has it, but the contract steers away from it: "Keybinding menus work
  properly (no F13)" is a QA item (Reflex l. 1029).
- **Platform.** `Initialize`, `Marker` and `Shutdown` are no-ops when
  `!OperatingSystem.IsWindows()`, because ETW is Windows-only. `Marker` before
  `Initialize` is a no-op, so a frame loop can call it unconditionally.
  `Initialize` twice without `Shutdown` throws `InvalidOperationException`.
- **Placement in `Ahjo.Vulkan`**, in `src/Ahjo.Vulkan/Latency/`.
  `Swapchain.SetLatencyMarker` forwards to it (D7), and `Ahjo.Vulkan` cannot
  reference a package above it. PCL has no Vulkan dependency, so it works on the
  AMD iGPU and with no GPU at all.

#### Why not the alternatives

- **`EventSource`.** Silently disabled under PublishAot, `Write<T>` is
  `[RequiresUnreferencedCode]`, and the GUID needs an attribute override (E8).
- **A native shim compiling `pclstats.h`.** It would put NVIDIA-proprietary
  source into a shipped binary under a supplement that licenses the SDK "only
  for use in systems with NVIDIA GPUs" (licence l. 161), which contradicts the
  all-vendor requirement. It would also add a native artifact per RID for about
  120 lines of logic.
- **A `Microsoft.Diagnostics.Tracing.TraceLoggingDynamic` package dependency.**
  Its `EventBuilder` is a general builder that keeps per-event buffers. Five
  fixed events need none of that, and a runtime dependency for the wrapper is a
  bigger cost than about 150 lines of fixed blobs.
- **A ninth package (`Ahjo.Vulkan.Latency`).** `Swapchain` needs to call it, so
  it would have to sit below `Ahjo.Vulkan`, which means a new root package for
  one static class.

### D7. One call per marker site: `Swapchain.SetLatencyMarker` forwards to PCL through an explicit table

"PCLSTATS_MARKER … should also be called anywhere that … SetLatencyMarker is
called" (Reflex l. 182-184). So `SetLatencyMarker(marker, frameId)` makes the
Vulkan call when the swapchain is opted in, and then calls
`PclStats.Marker(LatencyMarkerMap.ToPcl(marker), frameId)`. The latter is a
no-op if PCL was never initialized.

**One exception, from the E6 capture.** On an **opted-in** swapchain,
`OutOfBandPresentStart`/`End` go to the driver only and are **not** forwarded
to PCL. The driver already writes PCL 11/12 around the real OS present, and an
application copy would be stamped tens of milliseconds early (D5). On a
non-opted-in swapchain they are still forwarded, because no driver writes them
there. The rule is the pure function
`LatencyMarkerMap.ForwardsToPcl(LatencyMarker marker, bool lowLatencySwapchain)`.
The internal map is a `switch`:

| `LatencyMarker` (Vk value) | `PclMarker` (PCL value) |
|---|---|
| SimulationStart 0 | 0 |
| SimulationEnd 1 | 1 |
| RenderSubmitStart 2 | 2 |
| RenderSubmitEnd 3 | 3 |
| PresentStart 4 | 4 |
| PresentEnd 5 | 5 |
| TriggerFlash 7 | 7 |
| OutOfBandPresentStart **10** | **11** |
| OutOfBandPresentEnd **11** | **12** |

`PcLatencyPing` (PCL 8) has no Vulkan counterpart. It is emitted only by
`PclStats` (D6), as Streamline does (`reflexEntry.cpp:382`). The driver gets
`TriggerFlash` unconditionally. Streamline's driver-controlled-flash gate is
"since 511.23" (`reflexEntry.cpp:277`), and the target is 616.92.

A consumer with no swapchain, such as a headless tool, can call
`PclStats.Marker` directly.

**Why not two independent calls at every site.** Ten marker sites per FG frame
times two calls is twenty chances to stamp the wrong enum. The off-by-one in E6
is precisely the bug a shared call removes.

### D8. Licensing and shipping

- **Low latency.** Nothing from the Reflex SDK is used. The extension is
  defined in Khronos's Apache-2.0/MIT headers and implemented by the driver.
  There is no NVIDIA binary to ship.
- **PCL.** Nothing from the Reflex SDK is copied. The C# provider is written
  from the facts in E6, every one of which except three strings (provider name,
  ping message name, `PCLStatsInput`/`Flags` field names) is also published
  under MIT by NVIDIA (`sl_pcl.h`) or Intel (`Nvidia_PCL.h`, PresentMon's
  consumer). No `pclstats.h` text and no NVIDIA binary enters the repo. Source
  comments cite the facts, not the header.
- Whether re-expressing those protocol facts, including the three strings found
  only in the proprietary header, needs anything from NVIDIA is a legal call.
  So is whether the Reflex supplement's marketing clause (licence l. 167) binds
  an integration that never uses the SDK. Both are **OPEN-1**, for a human, like
  #253 OPEN-4. The consumer contract states the facts and settles neither.

### D9. Testing reality and the hardware run

- **CI (Windows, no ICD).** CI covers the pure tests: the marker map,
  enum-value drift against `VkLatencyMarkerNV` and the `sl_pcl.h` literals, the
  sleep-mode presets, the PCL metadata blobs against an independent builder,
  PCL init/marker/shutdown, and zero-allocation assertions. It also runs an ETW
  round-trip test that opens a real-time session on the PCL GUID and decodes the
  events. That test is gated `[gate:platform]` when not elevated.
- **RTX 4070 Ti.** It covers opt-in, mode, sleep, markers, timings, `Recreate`
  keeping the mode, present-id validation, and a validation-layer run of the D5
  marker sequence. Tests on a device without LL2 are `[gate:feature]`.
- **Hardware exit run in this PR (non-FG).** `samples/HelloCube` gains
  `--reflex off|on|boost`, `--present fifo|immediate`, `--present-id`, PCL ping
  handling and left-click TriggerFlash. It records the driver version, LL2
  revision, PresentMon PC latency for off/on/boost and the Reflex Test Utility
  result with Reflex On and On + Boost. It also answers OPEN-4 by checking
  whether timings populate with and without `--present-id`, and runs on the AMD
  iGPU to cover "PCL is not 0.0 on other IHV hardware" (Reflex l. 1030). FG-on
  verification stays in #253 PR C 19i.
- **Benchmarks.** `PclStatsBenchmarks` runs on any Windows host.
  `LowLatencyBenchmarks` is hardware-only and throws in Setup. All rows must
  read `Allocated: -`.
- **AOT.** `samples/AotSmoke` calls `PclStats.Initialize`, `Marker` and
  `Shutdown`, so ILC compiles the ETW path rather than trimming it away.

### D10. Delivery: one PR from `main`

PCL and LL2 meet in `SetLatencyMarker` (D7). The exit run needs both at once,
since the Reflex Test Utility reads PCL. Two PRs would serialize anyway. One PR,
branch `issue-261-reflex-low-latency` from `main`, `Closes #261`.

---

## What #253 PR C gets (plan steps 17b and 19i)

| 17b / 19i placeholder | This design |
|---|---|
| Enablement | `VulkanExtensions.NvLowLatency2` + `VulkanExtensions.KhrPresentId` in `DeviceDescription.Extensions` when `PhysicalDevice.SupportsExtension(VulkanExtensions.NvLowLatency2)`; no feature push unless present ids are passed |
| Swapchain opt-in | `new SwapchainDescription { …, LowLatency = device.IsNvLowLatency2Enabled }`, same value on every `Recreate` |
| Mode | `swap.SetLatencySleepMode(LatencySleepMode.On / OnPlusBoost / Off)`; FG forces On or OnPlusBoost (#253 F1) |
| Per-frame order and two presents | D5 sequence, verbatim. OOB present markers reach the driver only; PCL 11/12 come from the driver (D7, OPEN-8) |
| PCL | `PclStats.Initialize(new PclStatsOptions())` at startup, SDL message hook → `NotifyPingReceived`, `PclStats.Shutdown()` at exit |
| Frame ids | one `ulong` per rendered frame, **starting at 1** (0 throws), shared with `BackbufferFrameId` |
| Latency source (19i) | `swap.GetLatencyTimings(span)` + PresentMon PC latency (PCL) |

---

## OPEN

- **OPEN-1 (human / legal).** Does re-expressing the PCL protocol in C#
  (D8) need anything from NVIDIA? This includes the three strings found only in
  the proprietary `pclstats.h`, and the Reflex supplement's NVIDIA-GPU-only scope
  (licence l. 161) set against the all-vendor guideline (UI l. 142-143). Does
  the supplement's attribution clause (l. 167) apply to an SDK-free
  integration? The implementation does not wait on this. The contract quotes the
  evidence and does not settle it.
- **OPEN-2 (= #253 OPEN-6, measured).** Does NVIDIA's Reflex Test Utility
  accept a native LL2 + C# PCL integration? It is measured here with Reflex On
  and On + Boost (plan step 14), and with FG on in #253 PR C 19i. **If the
  utility reports warnings attributable to the integration path rather than to
  marker placement, stop and report.** Then a human decides whether to contact
  NVIDIA or revisit D1.
- **OPEN-3 (measured in #253 PR C 19i).** On the revision-2 driver, does the D5
  sequence attribute both presents to frame N? Is once-per-rendered-frame sleep
  correct under FG? Does PresentMon's PCL attach to the generated present? If
  timings show doubled frames or PCL is outside the guide's 3.0-5.7× band
  (Reflex l. 1237), PR C stops and reports. PR C must not change this API.
- **OPEN-4 (measured in this PR, plan step 14).** Do `vkGetLatencyTimingsNV`
  reports populate when no `VkPresentIdKHR` is chained? The v1.4.340 text tied
  marker ids to KHR present ids (E4). If reports populate **only** with present
  ids chained *and equal to the marker frame id*, D5's mapping cannot hold for
  two presents. **Stop and report** rather than adapting the API.
- **OPEN-7 (partly measured, the control is still owed; plan step 14 item 2).**

  *Measured 2026-11-06, RTX 4070 Ti, driver 616.92, LL2 revision 2, 60 Hz,
  windowed, IMMEDIATE:*
  - with `lowLatencyMode = false` and `minimumIntervalUs = 0`, every
    `vkLatencySleepNV` waits about one refresh (median 16.665 ms), the same as
    On, including in a loop with no presents;
  - `minimumIntervalUs = 50000` gives 50 ms in both modes;
  - it never times out.

  *Decision, from the E5 "Sleep while Off" evidence:* D3 is **unchanged**.
  Sleep keeps being called while Off, as Streamline, DXVK and NVIDIA's QA
  require.

  *Still open:* whether the sleep *causes* a cap the loop would not otherwise
  have. Windowed IMMEDIATE on Windows may already be compositor-paced, and a
  loop with no presents gives the driver nothing to pace against. The control
  is the identical HelloCube loop with the sleep skipped (`--skip-sleep`,
  diagnostic only), in windowed and borderless-fullscreen. Record the NVIDIA
  Control Panel settings for the process (Max Frame Rate, Vertical sync, Low
  Latency Mode, Vulkan/OpenGL present method): the Reflex header says
  control-panel caps are enforced in this sleep.
  - **If the control runs at the same rate as the sleep run, this item is
    closed** as "driver/compositor pacing, not caused by the sleep".
  - **If the control is clearly faster with Reflex Off** (more than 4%, the
    QA FPS-impact threshold, Reflex l. 1022), **stop and report** with
    both numbers. Diverging from Streamline's behaviour, and from the
    `App_Called_Sleep` QA item, is a human call, not an implementation choice.
- **OPEN-8 (measured in #253 PR C 19i).** With two presents per rendered frame,
  does the driver write **two** PCL 11/12 pairs per rendered frame, both with
  frame id N? The single-present behaviour is measured (E6); this is the
  extrapolation D7 relies on. Measure it with an elevated ETW session on the
  PCL GUID during a `--mode fg` run, using the E6 method.
  - **Stop and report** if:
    - the driver writes no pair, one pair, or pairs with ids other than N;
    - any OOB-present marker 11/12 appears on the application's own thread;
    - PresentMon's PC latency falls outside 3.0-5.7× the average frame time.
  - PR C must not change `SetLatencyMarker`'s forwarding rule.
- **OPEN-5 (verify at implementation).** Does `SDL_SetWindowsMessageHook`'s
  callback see the registered `PC_Latency_Stats_Ping` message posted to the SDL
  window, and with what return-value semantics? If it does not, stop and ask.
  The fallbacks, the thread-id mode and the virtual-key mode, each have a QA
  cost (Reflex l. 1029), so choosing between them is not the implementer's call.
- **Uncertainty, not blocking.** The Reflex Test Utility expects fullscreen
  exclusive with VSync off (Reflex l. 1051-1052). The wrapper does not wrap
  `VK_EXT_full_screen_exclusive`, so the exit run is windowed borderless or
  windowed. The guide allows "some setups such as windowed mode" to read high
  (Reflex l. 1247-1254), so record the mode next to every number.
