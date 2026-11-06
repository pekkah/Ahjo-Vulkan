# NVIDIA Reflex and PC Latency Stats with Ahjo.Vulkan — the consumer contract

Everything on this page is either in the Vulkan specification
(`KhronosGroup/Vulkan-Docs` `main` @ `fa30d4e3`, `chapters/VK_KHR_surface/wsi.adoc`,
cited "wsi l. N"), in the *NVIDIA Reflex SDK Integration Guide* v1.6 from
`NVIDIA-RTX/REFLEX` @ `c2522cee` ("Reflex l. N", the `pdftotext -layout`
extraction #253 cites), in NVIDIA Streamline `v2.14.16` or Intel PresentMon
`main` @ `362f2d93` (both MIT), or was **measured on hardware** — an RTX 4070 Ti
on driver 616.92 (§7). Measured facts say so, with the driver version.

The design record is issue #261's spec
(`docs/design/specs/2026-11-06-issue-261-reflex-low-latency-design.md`).
`samples/HelloCube` is the worked single-present call site; the
frame-generation call site is #253's `samples/HelloFrameGen`.

---

## 1. What ships

Three things, all in `Ahjo.Vulkan`, none of them an NVIDIA binary:

- **Reflex Low Latency on `Swapchain`**, over the native
  `VK_NV_low_latency2` extension through `vkGetDeviceProcAddr`:
  `SetLatencySleepMode`, `LatencySleep`, `SetLatencyMarker`,
  `GetLatencyTimings` / `LatencyTimingCapacity`, `IsLowLatencyEnabled`,
  `LatencySleepMode`. Opted in per swapchain with
  `SwapchainDescription.LowLatency`, and **kept across `Recreate`**: every
  create re-chains `VkSwapchainLatencyCreateInfoNV` and re-applies the stored
  sleep mode, so a window resize never turns Reflex off.
- **`PclStats`**, the PC Latency Stats ETW TraceLogging provider latency tools
  read (PresentMon, the Reflex Test Utility, FrameView). Written directly over
  `advapi32` — no NVIDIA SDK code, no `EventSource` (AOT, see
  [aot-notes.md](aot-notes.md)). Works on every GPU vendor, and with no GPU.
- **KHR present ids**: `Swapchain.Present(Queue, uint, ulong)` and
  `Present(Queue, uint, in BinarySemaphore, ulong)` chain `VkPresentIdKHR`.
  Additive: the existing overloads make the identical native call they always
  did (no `pNext`).

`NvLowLatencyVk.dll` (the Reflex SDK's Vulkan DLL) is **not** used. Khronos,
in text authored by NVIDIA, calls `VK_NV_low_latency2` the path for new
implementations (`VK_NV_low_latency2.adoc:32-34`), and Streamline uses it
first (`sl.chi/vulkan.cpp:1179-1184`).

---

## 2. Enable recipe

1. Check support: `gpu.SupportsExtension(VulkanExtensions.NvLowLatency2)`.
   Only NVIDIA drivers advertise it; the AMD iGPU on the measurement host has
   `VK_AMD_anti_lag` instead (§7).
2. Put **both** `VulkanExtensions.NvLowLatency2` and
   `VulkanExtensions.KhrPresentId` in `DeviceDescription.Extensions`.
   `VK_KHR_present_id` is LL2's creation dependency (`vk.xml:29889`).
3. `timelineSemaphore` is required and already on by default. LL2 has no
   feature struct (`nofeatures="true"`).
4. Push `VkPhysicalDevicePresentIdFeaturesKHR { presentId = 1 }` from
   `ConfigureFeatures` **only** if you will pass non-zero present ids
   (VUID-VkPresentInfoKHR-pNext-06235). Reflex itself does not need it.
5. Create the swapchain with
   `LowLatency = device.IsNvLowLatency2Enabled`, and pass the same value on
   every `Recreate` (a different value throws `ArgumentException`).
   `LowLatency = true` on a device without the extension throws
   `InvalidOperationException` at construction.
6. `swap.SetLatencySleepMode(LatencySleepMode.On)` once — **On is NVIDIA's
   required default** (§5).

`Device.IsPresentIdEnabled` reflects the extension only; the wrapper cannot
see whether the `presentId` feature was pushed (no post-create feature query),
so the validation layer is the oracle for that.

---

## 3. The frame-loop contract

**One frame id per rendered frame**, a `ulong` starting at **1** (0 throws —
PresentMon treats 0 as "no frame", PresentMonTraceConsumer.cpp:2575). The same
value goes to every latency marker, every PCL marker, and
`DlssFrameGenerationInputs.BackbufferFrameId`. It must be unique within the
last 1000 frames (Reflex l. 637).

**Sleep once per rendered frame**, before input sampling (wsi l. 9203-9206,
Reflex l. 517) — not once per present, even with frame generation. Pass a
bounded timeout (1 s); on `WaitState.Timeout`, carry on with the frame.

**Keep calling everything with Reflex Off and on other vendors.** On a
swapchain that is not opted in, `LatencySleep` returns `Signaled` at once,
`SetLatencyMarker` skips the Vulkan call but still emits the PCL marker, and
`GetLatencyTimings` returns 0 — so one loop runs everywhere. NVIDIA asks for
markers regardless of mode (Reflex l. 1025, l. 394-395), and Streamline's QA
list makes the sleep call while Off a verification item: with Reflex Off the
Verification HUD must show `App_Called_Sleep = 1`
(Streamline `ProgrammingGuideReflex.md:247`, `:286-296`). The Reflex SDK's
Vulkan header says why: control-panel features such as Maximum Frame Rate are
enforced in the sleep (`NvLowLatencyVk.h:262-264`). Streamline and DXVK both
keep sleeping while Off. On the measurement driver, the Off-mode sleep waits
about one refresh (§7, OPEN-7).

**PRESENT markers go on the presenting thread** (Reflex l. 1323-1324). Other
markers and `SetLatencySleepMode` may come from any thread (wsi l. 9151-9156,
9253-9258) but must not overlap `Recreate` or `Dispose`.

### Single present (frame generation off)

```
swap.LatencySleep(timeout)
SetLatencyMarker(SimulationStart, N)
  input, simulation
SetLatencyMarker(SimulationEnd, N)
SetLatencyMarker(RenderSubmitStart, N)
  render frame N, submit
SetLatencyMarker(PresentStart, N)
  swap.Present(queue, i)
SetLatencyMarker(PresentEnd, N)
SetLatencyMarker(RenderSubmitEnd, N)
```

An abandoned frame (acquire `OutOfDate`) still closes with `RenderSubmitEnd`
under its own id; the next iteration takes a fresh id.

### Frame generation (two presents per rendered frame) — spec D5, verbatim

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

Both presents carry frame id N: the spec's present-attribution rule 1 maps an
open `OUT_OF_BAND_PRESENT_START` to its id, which "allows frame generation
implementations to map multiple `vkQueuePresentKHR` calls to the same
presentID" (wsi l. 9578-9597). This sequence runs validation-clean on the
measurement host (`WindowedValidationTests.FrameGenerationMarkerSequence_IsValidationClean`,
30 frames, 0 errors). Whether the revision-2 driver attributes both presents to
N is measured by #253 PR C, not here.

### KHR present ids are not frame ids

A KHR present id is per `vkQueuePresentKHR` and must be strictly greater than
the last non-zero id on the same `VkSwapchainKHR`
(VUID-VkPresentIdKHR-presentIds-04999); the wrapper throws
`ArgumentOutOfRangeException` before the call instead. The high-water mark
resets to 0 on every create, so one monotonic counter across resizes is always
valid. A frame-generation frame needs **two** distinct KHR ids (`2N`/`2N+1`,
or any running counter) while both presents share frame id N. Nothing in LL2
takes a KHR id (wsi l. 9272-9282); latency timings populate without one (§7).
`Present(q, i, default)` is ambiguous (CS0121) — write `0UL` or
`default(BinarySemaphore)`.

---

## 4. PC Latency Stats

```csharp
PclStats.Initialize(new PclStatsOptions());   // once, at startup
window.EnablePclPingHook();                   // SDL3: SDL_SetWindowsMessageHook → NotifyPingReceived
...
PclStats.Shutdown();                          // at exit
```

- `Swapchain.SetLatencyMarker` forwards every marker to `PclStats.Marker`
  through an explicit table, because PCL numbers its markers one higher from 8
  upward (`OutOfBandPresentStart` is 10 in Vulkan, 11 in PCL;
  Streamline `include/sl_pcl.h:62-74`). Call `PclStats.Marker` directly only
  where there is no swapchain.
- **One exception:** on a swapchain created with `LowLatency = true`,
  `OutOfBandPresentStart`/`End` go to the driver only and are **not** forwarded
  to PCL, because the NVIDIA driver writes PCL 11/12 itself around the real OS
  present (measured, §7.5). On a swapchain without the opt-in (every other
  vendor) they are forwarded, since no driver writes them there.
- **The ping.** While a latency tool is listening, a background thread posts
  the registered window message `PC_Latency_Stats_Ping` to the foreground
  window if it belongs to this process, every 100-300 ms. The window pump
  calls `PclStats.NotifyPingReceived()` when `PclStats.IsPingMessage(msg)`;
  the next `SimulationStart` marker then emits `PC_LATENCY_PING` first with
  the same frame id — the guide's "set a flag for the next simulation"
  (Reflex l. 819-831). For SDL3 that is `SDL_SetWindowsMessageHook`, whose
  callback sees the registered message (measured, §7); return `true` so SDL
  keeps processing it. `tests/Ahjo.Vulkan.Tests/SdlWindow.cs` is the worked
  hook.
- **TriggerFlash** on the left mouse button: `SetLatencyMarker(TriggerFlash, N)`.
- **All vendors.** NVIDIA wants PCL markers "always enabled on all vendor GPUs"
  (UI l. 142-143). PCL does not touch Vulkan (Reflex l. 182-184), so it works on
  the AMD iGPU and with no GPU at all.
- **Why not F13.** `PclStatsOptions.PingVirtualKey` can post F13-F15 instead of
  the registered message, as the reference implementation allows. Avoid it:
  "Keybinding menus work properly (no F13)" is a Reflex QA item (Reflex
  l. 1029). `PingThreadId` posts to a thread instead.
- `PclStats` is process-wide and static; `Initialize` twice throws;
  `Marker` before `Initialize` is a no-op; everything is a no-op off Windows.
  If `EventRegister` fails, a warning goes to `AhjoDiagnostics.Sink` and
  markers stay off — telemetry never crashes the game.

---

## 5. NVIDIA's obligations on the application

- **Default On.** Reflex Low Latency must default to On (Reflex l. 1017).
- **Not disableable with frame generation on** (UI l. 149-150, #253 F1).
- **Boost needs LowLatency** (Reflex l. 428-429). The Vulkan spec permits Boost
  alone (wsi l. 9179-9181) and the wrapper passes it through, so use
  `LatencySleepMode.OnPlusBoost`.
- **Markers always sent**, regardless of Reflex mode (Reflex l. 1025).
- **Reflex UI hidden on other vendors** (Reflex l. 1031): hide it when
  `Device.IsNvLowLatency2Enabled` is false. `SetLatencySleepMode` on a
  swapchain that is not opted in throws, by design.
- **The QA checklist** is the Reflex guide's verification section (Reflex
  §4.2, l. 1017-1031 for the items above).

---

## 6. Licensing facts (OPEN-1 — stated, not settled)

- **Low latency.** No Reflex SDK code or binary is used. `VK_NV_low_latency2`
  is defined in Khronos's Apache-2.0/MIT headers and implemented by the driver.
- **PCL.** No `pclstats.h` text and no NVIDIA binary is in the repo. The
  provider is written from protocol facts. The GUID and marker values are
  published under MIT in PresentMon's `Nvidia_PCL.h:10-23` and Streamline's
  `include/sl_pcl.h:35-84`; the event and field names PresentMon matches are
  in its MIT consumer. **Three strings appear only in `pclstats.h`**, which
  carries NVIDIA's proprietary notice (`pclstats.h:1-6`): the provider name
  `PCLStatsTraceLoggingProvider`, the ping message name
  `PC_Latency_Stats_Ping`, and the `PCLStatsInput`/`PCLStatsFlags` field
  names.
- The Reflex SDK licence supplement makes the SDK "licensed for you to develop
  applications only for use in systems with NVIDIA GPUs" (licence l. 161),
  while NVIDIA's guidelines want PCL on all vendors (UI l. 142-143). It also
  has an attribution clause (licence l. 167).
- **Open, for a human:** whether re-expressing those protocol facts (including
  the three strings) needs anything from NVIDIA, and whether the supplement's
  scope and attribution clause bind an integration that never uses the SDK.
  This page does not settle either.

---

## 7. Measured

Host: RTX 4070 Ti, driver **616.92**, `VK_NV_low_latency2` **revision 2**
(`vulkaninfoSDK`, SDK 1.4.363.0), plus an AMD Radeon iGPU (driver 2.0.353) with
no LL2. Display 60 Hz. HelloCube Debug build, validation layer on, 1024×768.
Dates: 2026-11-06.

"Sim → GPU end" is HelloCube's `--latency-report`: the mean of
`GpuRenderEndTimeUs - SimulationStartTimeUs` over the driver's last 64
reports. "OS-queue wait" is `GpuRenderStartTimeUs - OsRenderQueueStartTimeUs`
over the same reports. PresentMon's PC-latency column and the Reflex Test
Utility were **not** available on this host (not installed; see the gaps
below), so there is no PCL mean/p99 here yet.

### 7.1 Reflex modes, windowed 1024×768 (about 60 s each)

| Config | FPS | Sim → GPU end |
|---|---|---|
| `--present immediate --reflex off` | 58 | 1.07–1.40 ms |
| `--present immediate --reflex on` | 59–60 | 1.08–1.21 ms |
| `--present immediate --reflex boost` | 60 | 0.35–0.38 ms |
| `--present fifo --reflex on` | 60 | 0.32–0.36 ms (see 7.4) |

`VK_PRESENT_MODE_IMMEDIATE_KHR` was granted but did not uncap the loop: a run
with no LL2 at all and no sleep call also ran at 58 fps in a hidden window,
so windowed presentation is display-paced on this host.

### 7.2 OPEN-7: the Off-mode sleep — closed

With `lowLatencyMode = 0` and `minimumIntervalUs = 0`, every
`vkLatencySleepNV` + wait takes about one refresh, never times out, and does
so even in a loop with no presents:

| Sleep-only loop (no presents), 60 calls | min | median | max |
|---|---|---|---|
| Off, interval 0 | 16.629 ms | 16.665 ms | 16.683 ms |
| On, interval 0 | 16.640 ms | 16.665 ms | 16.693 ms |
| Off, interval 50 000 µs | 46.8 ms | 49.99 ms | 50.01 ms |
| On, interval 50 000 µs | 49.97 ms | 50.00 ms | 50.02 ms |

The control (plan #261 step 14 item 2): the identical HelloCube loop,
`--present immediate`, 60 s each, with the sleep skipped (`--skip-sleep`,
diagnostic only):

| Mode | `--reflex off` | `--reflex off --skip-sleep` | `--reflex on` |
|---|---|---|---|
| Windowed 1024×768 | 60 fps, sleep mean 16.38 ms | **60 fps**, no sleep | 59 fps, sleep mean 16.49–16.53 ms |
| Borderless 2560×1440 | 60 fps, sleep mean 16.39–16.40 ms | **60 fps**, no sleep | 60 fps, sleep mean 16.33 ms |

The control runs at the same rate as the sleep-while-Off run in both modes
(0% difference, threshold 4%), so the pacing is **not caused by the sleep**:
the sleep absorbs a frame time that presentation imposes anyway. Every sleep
returned `Signaled`; timeouts were 0 in every run. The NVIDIA Control Panel
settings for the process (Max Frame Rate, Vertical sync, Low Latency Mode,
Vulkan/OpenGL present method) could not be read from the command line on this
host and were **not captured** — the fact that `--skip-sleep` is still held
at 60 fps in borderless IMMEDIATE suggests a driver-level cap or VSync
override is active.

### 7.3 OPEN-4: timings without present ids — closed

`GetLatencyTimings` returns populated reports **with no `VkPresentIdKHR`
chained**: 64/64 reports carried a frame id in every HelloCube run above, with
and without `--present-id`, and `SwapchainLowLatencyTests.OptedIn_SleepMarkersTimings`
(120 frames, no present id) asserts that every non-zero `FrameId` is one of
the marker ids sent. So the revision-2 driver follows the current spec text
(the latency `presentID` is the application's frame id, wsi l. 9272-9282), not
the v1.4.340 text that tied it to KHR present ids.

### 7.4 FIFO + Reflex On: a bimodal OS-queue wait (driver/compositor, not the wrapper)

Under `--present fifo --reflex on`, windowed, sim → GPU end sometimes sits at
about 0.3–1.3 ms for a whole run and sometimes climbs to one or two refresh
intervals:

| Run (same build, back to back) | `--present-id` | Sim → GPU end |
|---|---|---|
| matrix | no | 0.32–0.36 ms |
| matrix | yes | 14.8 → 19.8 ms, rising |
| standalone 1 | yes | 0.89–1.49 ms |
| standalone 2 | no | 22.99 → 38–44 ms |
| diagnostic 1 | no | 0.88–1.67 ms |
| diagnostic 2 | yes | 0.89–1.27 ms |
| diagnostic 3 | no | 0.97 ms, then 21–22.7 ms from frame ~600 |
| diagnostic 4 | yes | 0.95 → 10.4 → 14.5 → 18.6 ms, rising |
| PCL session run (7.5) | no | 1.05 → 23.2 → 42.5 → 44.7 ms; OS-queue wait 0.79 → 22.9 → 32.9 ms |

- It is **independent of present ids**: it hit two of four runs each with and
  without `--present-id`. Without an id the wrapper's present is byte-for-byte
  the pre-#261 call (`pNext = null`).
- **All of the extra time is in the OS render queue**, after the submit returned
  and before the GPU started: in diagnostic 3, the per-report breakdown went
  from `osq 0.13..0.73 ms, gpu 0.64..0.73 ms` to `osq 0.15..17.29 ms,
  gpu 17.15..17.29 ms` relative to simulation start, while simulation, submit
  and present markers stayed within 0.3 ms. In diagnostic 4 the queue wait grew
  steadily (2.6 → 6.7 → 10.5 ms), which looks like the frame's phase drifting
  against vblank.
- The CPU side is unchanged throughout: the Reflex sleep stayed at about
  16.3–16.5 ms per frame, at 60 fps, with 0 timeouts.

Read as: in windowed FIFO, on this driver, the composed presentation queue
sometimes keeps a frame waiting behind a flip that Reflex's CPU pacing does
not absorb. Measure latency in IMMEDIATE (or borderless) for comparisons, and
record the presentation mode next to every number.

### 7.5 Frame generation sequence, validation, other vendors, PCL

- **Spec D5 two-present sequence**: 30 frames with two acquires, two submits
  and two presents per frame, OUT_OF_BAND pairs and KHR ids `2N`/`2N+1`, under
  `VK_LAYER_KHRONOS_validation` 1.4.363: **0 errors**.
- **AMD iGPU** (`VK_LOADER_DRIVERS_SELECT=*amd*`): HelloCube prints
  "Reflex: unavailable on this GPU — PCL markers only", runs at 59.8 fps, and
  `GetLatencyTimings` returns 0 reports. The PresentMon PCL value on AMD is
  **not yet measured**.
- **OPEN-5, the SDL hook — closed.** The ppy.SDL3-CS 2026.1002.1 hook is
  `delegate* unmanaged[Cdecl]<nint, MSG*, SDLBool>`, and returning `true` lets
  SDL continue processing. With a real ETW session on the PCL GUID
  (`logman … -p {0d216f06-…} 0 5 -ets`, elevated) and HelloCube in the
  foreground for 20 s, the ping thread posted 96 pings, the SDL hook saw all
  96, and 95 `PC_LATENCY_PING` (8) markers were written ahead of
  `SimulationStart` (the last ping arrived after the final frame).
- **The ETW wire format decodes as TraceLogging**:
  `PclStatsEtwRoundTripTests` (elevated, TraceEvent's self-describing parser)
  passes, and `tracerpt` on the session above names the provider
  `PCLStatsTraceLoggingProvider` and the events `PCLStatsInit`,
  `PCLStatsEvent` (`Marker`, `FrameID`), `PCLStatsInput` and
  `PCLStatsShutdown`, all at level 5.
- **The NVIDIA driver writes PCL events of its own into the same provider,
  in-process.** In that session, alongside the wrapper's markers (0-5 and 8,
  1200 each, on the main thread), a second thread of the same process wrote
  `PCLStatsEvent` **11 and 12** (`OUT_OF_BAND_PRESENT_START/END`) 1198 times
  each, with the application's frame ids (1, 2, 3, …) — although HelloCube
  sends no out-of-band marker — and a second `PCLStatsInit` / `PCLStatsShutdown`
  pair appeared on the main thread. The driver's LL2 implementation evidently
  reports its own present as an out-of-band present. A latency consumer that,
  like PresentMon, switches to "first OOB present start per frame id" once it
  sees any marker 11 (spec E6) will therefore do so on this driver even for a
  single-present application. Measured once, 2026-11-06, driver 616.92.
  Because of this, `Swapchain.SetLatencyMarker` no longer forwards the
  out-of-band present markers to PCL on a low-latency swapchain (§4); how the
  driver's 11/12 behave with two presents per rendered frame is OPEN-8, measured
  in #253 PR C step 19i.

### 7.6 Not measured here

- **PresentMon PC latency** (mean, p99) for 7.1 and on the AMD iGPU —
  **deferred to #253 PR C step 19i.**
- **The Reflex Test Utility** (Reflex On, On + Boost; OPEN-2) — **deferred to
  #253 PR C step 19i.**
- **The NVIDIA Control Panel settings** for 7.2 (Max Frame Rate, Vertical
  sync, Low Latency Mode, Vulkan/OpenGL present method) — **not captured.**

---

## 8. Not provided

- **Out-of-band queues** (`vkQueueNotifyOutOfBandNV`) and the two
  `OUT_OF_BAND_RENDERSUBMIT` markers. Marking any queue out of band switches
  the frame delimiter device-wide to `PRESENT_END` markers (wsi l. 9623-9626).
  Additive later.
- **Explicit submission attribution** (`VkLatencySubmissionPresentIdNV`): only
  respected on revision 3+ (`VK_NV_low_latency2.adoc:44-52`); the measurement
  driver is revision 2, and "most applications do not need to pass" it (wsi
  l. 9517-9519).
- **`VkLatencySurfaceCapabilitiesNV`**: needs `VK_KHR_get_surface_capabilities2`,
  which the wrapper does not enable, and nothing consumes it.
- **`vkWaitForPresentKHR`** (`VK_KHR_present_wait`): present ids exist for it,
  but a pacer that uses it is a separate design (#253 F3).
- **`VK_AMD_anti_lag`**: a different vendor's model, filed separately if the
  engine wants it.
- **`VK_EXT_full_screen_exclusive`**: not wrapped, so every measurement here is
  windowed or borderless; the Reflex guide notes windowed setups can read high
  (Reflex l. 1247-1254).
