# Device fault, KHR follow-ups — healthy-device polling (#244), shader-abort messages (#245) and a Slang abort-payload decoder

- Issues: #244 — "Device fault: KHR healthy-device (masked) fault polling"; #245 — "Device fault: chain VK_KHR_shader_abort messages on the KHR debug-info read"
- Plan: [`../plans/2026-10-05-issue-244-245-device-fault-khr.md`](../plans/2026-10-05-issue-244-245-device-fault-khr.md)
- Builds on: [#242 device fault](2026-10-02-issue-242-device-fault-design.md) (the reader, `TryGetDeviceFault`, `_faultReadLock`), [#246 feature query](2026-10-04-issue-246-physical-device-features-design.md) (`PhysicalDevice.TryGetFeatures<T>`), [#120 device loss](2026-06-12-issue-120-device-loss-design.md) (`IsLost`, the post-loss no-blocking policy)

**One PR, three parts.** The maintainer asked for both issues in one PR. They
are independent designs that touch the same two files (`Internal/DeviceFaultReader.cs`,
`Lifecycle/Device.cs`), so this document keeps them in separate parts. Part A is
#244 and Part B is #245. Part C is a decoder for Slang abort payloads in
`Ahjo.Vulkan.Slang`; it consumes Part B's output, and the maintainer added it to
this PR on 2026-10-05. Each part has its own Problem, Evidence, Decision and
alternatives. The shared evidence comes first, and the *Interaction* section
says what the parts touch in common. Each part can be reviewed on its own.

**Maintainer decisions (2026-10-05).** The design was approved and the three
open items were resolved:

- the real device-loss test is **not** taken (see *Test strategy*);
- the Slang decoder is **included** (Part C);
- the internal test hook that exposes the fault lock is **allowed**.

## Shared evidence

### S1. No regeneration is needed

Every native symbol either part needs is already generated from the pinned
Vulkan-Headers `vulkan-sdk-1.4.363.0` (`Directory.Build.props:18`):

| Symbol | Where |
|---|---|
| `VK_KHR_shader_abort`, rev 1, `depends="VK_KHR_device_fault+VK_KHR_shader_constant_data"` | `native/include/vulkan/vk.xml:25574-25586` |
| `VkPhysicalDeviceShaderAbortFeaturesKHR` (`shaderAbort`) | `Generated/VkPhysicalDeviceShaderAbortFeaturesKHR.cs`; `IChainable<VkPhysicalDeviceFeatures2>, IChainable<VkDeviceCreateInfo>` (`Generated/Chains/VkPhysicalDeviceShaderAbortFeaturesKHR.Chain.g.cs:6`) |
| `VkPhysicalDeviceShaderAbortPropertiesKHR` (`maxShaderAbortMessageSize`) | `Generated/VkPhysicalDeviceShaderAbortPropertiesKHR.cs` |
| `VkDeviceFaultShaderAbortMessageInfoKHR` (`ulong messageDataSize`, `void* pMessageData`) | `Generated/VkDeviceFaultShaderAbortMessageInfoKHR.cs`; `IChainable<VkDeviceFaultDebugInfoKHR>` (`Chains/…Chain.g.cs:6`) |
| sTypes `…SHADER_ABORT_FEATURES_KHR = 1000233000`, `…DEVICE_FAULT_SHADER_ABORT_MESSAGE_INFO_KHR = 1000233001`, `…SHADER_ABORT_PROPERTIES_KHR = 1000233002` | `Generated/VkStructureType.cs:586-588` |
| `VK_KHR_shader_constant_data` and `VkPhysicalDeviceShaderConstantDataFeaturesKHR` | `vk.xml:25510-25516`, `Generated/VkPhysicalDeviceShaderConstantDataFeaturesKHR.cs` |
| `vkGetDeviceFaultReportsKHR`, `VkPhysicalDeviceFaultFeaturesKHR.deviceFaultReportMasked` | already used by #242 (`DeviceFunctionTable.cs:577-589`) |

`VK_KHR_shader_abort` defines **no commands** (`vk.xml:25575-25585`), so it
needs no `DeviceFunctionTable` block. No `tools/*.rsp` change and no `/regen-bindings`.

### S2. Hardware (vulkaninfo, SDK 1.4.363 loader, 2026-10-05)

| | RTX 4070 Ti (NVIDIA 616.92, api 1.4.351) | AMD Radeon iGPU (26.8.1, api 1.4.315) |
|---|---|---|
| `VK_EXT_device_fault` | rev 2; `deviceFault`, `deviceFaultVendorBinary` true | rev 2; `deviceFault` true, `deviceFaultVendorBinary` false |
| `VK_KHR_device_fault` | rev 1; `deviceFault` true, `deviceFaultVendorBinary` true, **`deviceFaultReportMasked` false**, `deviceFaultDeviceLostOnMasked` false; **`maxDeviceFaultCount = 1`** | absent |
| `VK_KHR_shader_abort` | **rev 1, `shaderAbort = true`, `maxShaderAbortMessageSize = 65536`** | absent |
| `VK_KHR_shader_constant_data` | rev 1, `shaderConstantData = true` | absent |

So a masked fault cannot be produced on any local GPU (#244), but a shader abort
can (#245). It is NVIDIA only.

### S3. The validation layer (SDK 1.4.363.0, the only registered `VK_LAYER_KHRONOS_validation`)

Source: `KhronosGroup/Vulkan-ValidationLayers@vulkan-sdk-1.4.363.0`.

- `vkGetDeviceFaultReportsKHR`: extension enablement, per-element sType/pNext,
  and `-pFaultCounts-arraylength`. There is no lost-state check
  (`layers/vulkan/generated/stateless_validation_helper.cpp:20959-20978`, as #242 §4 found).
- `vkGetDeviceFaultDebugInfoKHR`: `VkDeviceFaultShaderAbortMessageInfoKHR` is
  the **only** allowed pNext (`stateless_validation_helper.cpp:20991-20998`).
  `CoreChecks` enforces VUID-12383, which requires a lost device (#242 §4).
- `VkPhysicalDeviceShaderAbortFeaturesKHR` is a known `VkDeviceCreateInfo` pNext
  struct (`stateless_validation_helper.cpp:982-990` member check, `:9496` allowed list).
- SPIR-V: capability `AbortKHR` requires `VkPhysicalDeviceShaderAbortFeaturesKHR::shaderAbort`,
  and `SPV_KHR_abort` requires `VK_KHR_shader_abort`
  (`layers/vulkan/generated/spirv_validation_helper.cpp:307`, `:463`, `:1353`, `:1484`).
- Extension dependencies are enforced at `vkCreateDevice` as
  `VUID-vkCreateDevice-ppEnabledExtensionNames-01387` (`layers/stateless/sl_instance_device.cpp:108`).
  Enabling `VK_KHR_shader_abort` without `VK_KHR_shader_constant_data` is a
  validation error.
- **Not checked:** whether a chained pNext struct's extension was enabled.
  `Context::ValidatePnextStructExtension` (`layers/stateless/sl_utils.cpp:204-233`)
  covers only two maintenance5 structs. Chaining the abort struct on a device
  without `VK_KHR_shader_abort` passes the layer. The spec's pNext validity rule
  still forbids it, so the wrapper gate in Part B is spec-required and the layer
  is not its oracle.

## Part A — #244: healthy-device (masked) fault polling

### A. Problem

`Device.TryGetDeviceFault` reads KHR fault reports only after loss:
`if (!IsLost) return false;` (`Lifecycle/Device.cs:282`). The docs say
healthy-device polling "is not wrapped" (`Device.cs:260-261`,
`Rendering/VulkanExtensions.cs:194-200`). `vkGetDeviceFaultReportsKHR` itself
is legal at any time (#242 §2: no lost-state VUID). With the
`deviceFaultReportMasked` feature, it reports faults the driver recovered from
without losing the device (`features.adoc:8290-8295`, `Vulkan-Docs@e4e53e4`).
There is no wrapper door to those reports.

#242 §E deferred this because the queue is drain-once: "Each individual fault
report is returned exactly once" (`proposals/VK_KHR_device_fault.adoc:172`). A
poller and the crash read therefore compete for the same entries.

### A. Evidence

**A1. The race that decides the queue-sharing question.** The proposal picks
"Polling With Timeout" so an application can run "a low CPU usage fault catcher
thread" (`VK_KHR_device_fault.adoc:86-91`, `:111-115`). Its example is a
`while(true)` loop around a blocking count call (`:463-495`). In that intended
configuration the poller is *blocked inside the driver* when the device dies.
When the driver posts the `DEVICE_LOST`-flagged entry, the poller's count call
returns and the poller drains the entry. That happens **before** the main thread
sees `VK_ERROR_DEVICE_LOST` from a submit or wait and sets `IsLost`
(`Device.cs:121-133`, `Sync/Fence.cs:87,118`). A design where "the crash read
sees what is left" therefore loses the device-lost entry in exactly the
configuration the API was designed for.

**A2. The driver keeps almost nothing.** "Implementations are expected to
retain fault reports in a fixed size buffer … If the number of faults generated
exceeds this limit, then the oldest records will be overwritten" (`VK_KHR_device_fault.adoc:413-420`).
The 4070 Ti reports `maxDeviceFaultCount = 1` (S2). If masked faults are not
drained, the loss entry overwrites them. If a poller drains them, they are gone
from the driver. Either way, the driver cannot provide "the entries that
preceded the loss" to the crash read. Only an application-side copy can.

**A3. Concurrency is legal; the wrapper serializes anyway.** No parameter of
`vkGetDeviceFaultReportsKHR` carries `externsync` (`vk.xml:18888-18893`). "Can
be invoked in parallel from different threads, in which case each invocation …
will return a unique set of reports" (`VK_KHR_device_fault.adoc:174-177`). All
wrapper reads already go through `lock (_faultReadLock)` (`Device.cs:49`,
`:283-284`).

**A4. `Dispose` does not take the fault lock.** `Device.Dispose`
(`Device.cs:1174-1215`) calls `vkDeviceWaitIdle` and `vkDestroyDevice` with no
lock. Today the only call that can block inside the driver is the post-loss
read. A healthy-device poll on a background thread would add a long-lived
in-driver call that a concurrent `Dispose` destroys the device under. That is
undefined behavior. The class remarks cover only concurrent `Dispose` calls
(`Device.cs:19-22`).

**A5. Post-loss blocking is already policy.** "Post-loss waits return
immediately (#120)" (`Sync/Fence.cs:110-114`). `TryGetDeviceFault` refuses any
timeout that would mean an infinite wait (`Device.cs:266-276`).

**A6. The feature bit gates nothing in the API.** No device-fault command has a
feature VUID (#242 §2). The 1.4.363 `validusage.json` lists only the four
parameter VUIDs for the reports command. Without `deviceFaultReportMasked`, a
healthy-device poll returns `VK_TIMEOUT` cleanly. This was measured on the
4070 Ti under the 1.4.363 layer
(`DeviceFaultTests.Khr_RealDriverReportsCall_UnderValidation_Clean`,
`tests/Ahjo.Vulkan.Tests/DeviceFaultTests.cs:301-326`). #246 added the pre-create
"supported?" query, `PhysicalDevice.TryGetFeatures<T>` (`Lifecycle/PhysicalDevice.cs:640-651`).
#242 rejected reading feature bits back from the create chain because the
wrapper's guards are deliberately extension-only (#242 *Why not*, "Read
`deviceFaultVendorBinary` back").

**A7. Allocation today.** `ReadKhrEntries` allocates a `List<DeviceFaultEntry>`
before its first native call (`Internal/DeviceFaultReader.cs:270`). That is
harmless once after loss. It is a per-call allocation if the reader is called
every frame. A timeout-0 poll on the render thread is a legitimate per-frame use
(the proposal's first example, `:437-460`), and the allocation rule follows the
call frequency, not the directory (`src/Ahjo.Vulkan/CLAUDE.md`).
`List<T>.ToArray()` on an empty list returns a shared empty array, but that is
a BCL detail the benchmark checks rather than assumes.

**A8. Consumers.** None. A search for `TryGetDeviceFault` / `GetDeviceFaultReports`
outside `Generated/` finds only the wrapper (`Device.cs`, `DeviceFaultReader.cs`,
`DeviceFunctionTable.cs`) and its two test classes. Ahjo has not requested
polling (issue text: "park until a consumer needs it"). The shape is grounded in
the spec's intended usage (A1), not in a call site.

### A. Decision

**The wrapper owns one queue.** It adds `Device.TryPollDeviceFaults(TimeSpan timeout, out DeviceFaultEntry[] entries)`.
Every KHR entry the wrapper drains, by poll or by crash read, is appended to a
bounded per-device **fault log**. The post-loss `TryGetDeviceFault` (KHR path)
returns the log rather than only what it drained itself. The poller and the
crash read share the queue by sharing what was drained, so neither can starve
the other. That holds in the A1 race too: if the poller drains the device-lost
entry, the crash read still reports it.

#### A.1 Shape

```csharp
public bool TryPollDeviceFaults(TimeSpan timeout, out DeviceFaultEntry[] entries);
```

- Returns `true` when at least one entry was drained. `entries` is **never
  null**: on `false` it is the shared empty array.
- `DeviceFaultEntry[]` rather than `DeviceFaultReport`. A poll has no
  `VendorBinary`, because vendor binaries "are only generated after
  device-loss" (`VK_KHR_device_fault.adoc:389-390`) and the debug-info call is
  lost-only (VUID-12383). It has no shader-abort messages either (Part B, same
  call). Reusing the report type would carry two fields that are always empty.
- KHR only. If `Functions.GetDeviceFaultReports` is null (no `VK_KHR_device_fault`;
  EXT is lost-only), the method returns `false` without a driver call.

#### A.2 Gates, in order

1. `ObjectDisposedException.ThrowIf(_disposed, this)`.
2. The timeout is validated **exactly as `TryGetDeviceFault` validates it**: a
   negative, infinite or `UINT64_MAX`-saturating span throws
   `ArgumentOutOfRangeException`. Both methods share one private helper. Infinite
   is refused here for a new reason as well as #120's: `Dispose` and the crash
   read wait behind an in-flight poll (A.4), so their wait is bounded only if the
   poll's is.
3. No KHR reports pointer: return `false`.
4. `IsLost`: return `false` without a driver call. After loss the crash read owns
   the queue, and #120 forbids new blocking waits (A5). Under the multi-device
   `IsLost` false positive (#242 §6), a healthy sibling also stops polling. That
   is accepted and documented, the same trade #242 made.
5. `Monitor.TryEnter(_faultReadLock, timeout)`. If the lock is held (another
   poller, or a crash read in progress), return `false`. The caller's timeout
   bounds the lock wait as well as the driver wait, so a timeout-0 per-frame poll
   never stalls behind a background poller. The worst case is about 2× `timeout`.
   The lock wait is clamped to `int.MaxValue` ms (about 24.8 days), because
   `Monitor.TryEnter(object, TimeSpan)` throws above that.
6. Inside the lock, re-check `_disposed` (throw) and `IsLost` (return `false`).
   Then `DeviceFaultReader.ReadKhrEntries(…, timeoutNs, …)`, the #242 drain loop
   unchanged: the caller's timeout on the first count call, 0 afterwards, with
   the caps.
7. Append the drained entries to the log, release the lock, and return.
   - A negative result with nothing drained gives `false` and one sink warning.
   - A negative result after a partial drain returns the drained entries, which
     are logged, plus a warning.
   - Reaching a drain cap leaves the remainder queued for the next poll. No
     incompleteness flag is surfaced, because the next poll gets the rest.
   - Never throws for a driver result (#242 posture).

#### A.3 The fault log

`Internal/DeviceFaultLog.cs` is a ring of `DeviceFaultEntry` references with
`Capacity = 1024`, the same number as `DeviceFaultReader.MaxKhrEntries`, so one
full crash drain always fits.

- Its backing array is allocated on the first append, so a device that never
  sees a fault never allocates it.
- When an append overflows the ring, the oldest entries are overwritten and a
  sticky `Dropped` flag is set.
- It is accessed only under `_faultReadLock`, so it needs no synchronization of
  its own.

**Crash-read change (KHR path only).** `DeviceFaultReader.TryRead` takes the log:

- It drains as before and appends the fresh entries to the log.
- The report's `Entries` is `log.Snapshot()`: oldest first, so polled entries
  come before the fresh ones, in order of occurrence.
- `IsIncomplete |= log.Dropped`.
- It returns `false` only when the drain errored **and** the log is empty.
  Before this change, a failed drain with nothing drained returned `false` even
  if a poller held the device-lost entry.

**Behavior change (name it in the PR).** A repeat KHR `TryGetDeviceFault` now
returns the **same** entries, the logged ones, instead of "no further entries"
(`Device.cs:247-249`). Both paths are now idempotent across repeat calls, as EXT
always was. A crash report may now also contain masked entries polled earlier
in the session. Their `Flags` lack `DeviceLost`, which is how a formatter tells
them apart. This is the "entries that preceded the loss" the issue asks for
(A2).

Entries are shared by reference between a poll's `entries` and later reports.
`DeviceFaultEntry` has only `init` properties, but its arrays are mutable, and
the docs say to treat them as read-only. Copying every entry would allocate on
every fault for no consumer need.

#### A.4 Threading and `Dispose`

- `Dispose` takes `_faultReadLock` around its existing body, with `_disposed`
  re-checked inside. A destroy therefore waits for an in-flight poll or crash
  read. That wait is bounded by the poll's finite timeout (A.2 step 2).
- Both read paths re-check `_disposed` after acquiring the lock (A.2 step 6), so
  a reader that was waiting for the lock never calls into a destroyed device.
- The finalizer path is safe: a `Device` being finalized is unreachable, so no
  thread can be inside a poll on it.
- The crash read keeps the blocking `lock` because it must see everything.
- **Documented latency:** a crash read or `Dispose` can wait up to one
  in-flight poll's timeout. The docs recommend that background pollers use
  timeouts of at most `DefaultDeviceFaultTimeout` (100 ms) and that the poller
  thread be joined before `Dispose`.

#### A.5 Feature gate: extension-only, documented recipe

The wrapper does not read back `deviceFaultReportMasked`:

- The call is legal without it (A6), and the result without it is a clean
  `VK_TIMEOUT` after at most the caller's own timeout.
- A read-back would be the wrapper's first per-feature guard, which #242
  rejected.

The recipe goes on `VulkanExtensions.KhrDeviceFault`, replacing "not wrapped":
query `gpu.TryGetFeatures<VkPhysicalDeviceFaultFeaturesKHR>(VulkanExtensions.KhrDeviceFault, out var f)`
and set `deviceFaultReportMasked` in `ConfigureFeatures` only when `f` reports
it. `deviceFaultDeviceLostOnMasked` is unrelated to polling and stays the
caller's.

#### A.6 Allocation posture

- **The no-fault poll allocates nothing.** That covers argument validation, the
  `Monitor.TryEnter`, one count call answering `VK_TIMEOUT`, and the shared
  empty array.
- `ReadKhrEntries` creates its list lazily, only once the count is non-zero (A7).
- A poll that drains entries allocates them, along with any log growth, as the
  crash read already did. Faults are rare, diagnostic events.
- Proof: a driverless `[MemoryDiagnoser]` benchmark over `ReadKhrEntries` with
  an `[UnmanagedCallersOnly]` fake that answers `VK_TIMEOUT`
  (`ResultPolicyBenchmarks` shape; `InternalsVisibleTo Ahjo.Vulkan.Benchmarks`,
  `Ahjo.Vulkan.csproj:26`).
- A device-level `GC.GetAllocatedBytesForCurrentThread` test covers the
  `Device` layer on KHR hardware. The precedent is
  `MeshShaderTests.MeshPipeline_Build_IsZeroAllocation`.
- There is no device-level benchmark, by choice rather than necessity. One
  would be possible as its own host-gated class, as `MeshShaderBenchmarks` and
  `DescriptorSetPoolVariableCountBenchmarks` are. The device test was chosen
  instead because its exact-zero assertion is the stronger check: it fails
  the suite on any allocated byte, on every test run, while a benchmark row is
  read only when someone runs it. The driverless row above keeps the reader in
  the benchmark canary.

### A. Why not the alternatives

- **Crash read sees only what is left (the poller's entries belong to the
  poller).** This loses the device-lost entry in the intended background-poller
  configuration (A1). That entry is the one thing the crash read exists for.
- **Poller and crash read mutually exclusive by configuration** (a
  `DeviceDescription` switch). It does not address A1: whichever is configured,
  a poller still drains the loss entry. It also adds create-time state for a
  runtime choice.
- **A callback sink.** Someone still has to call the driver. That is either a
  wrapper-owned thread, which the wrapper has nowhere else and the proposal
  rejected callbacks to avoid (`VK_KHR_device_fault.adoc:94-106`), or the
  caller's thread, in which case it is a poll with an extra indirection.
  Invoking user code under `_faultReadLock` also invites reentrancy into
  `TryGetDeviceFault` and deadlock.
- **Return `DeviceFaultReport` from the poll.** `VendorBinary` and the abort
  messages would always be empty (A.1).
- **Allow polling after `IsLost`.** This contradicts #120's no-new-blocking
  policy, and the crash read already drains everything after loss.
- **Accept an infinite or saturating poll timeout.** `Dispose` and the crash
  read would then wait unboundedly behind a background poll (A.4).
- **Plain `lock` in the poll.** A timeout-0 render-thread poll could then stall
  for a background poller's full timeout. `Monitor.TryEnter` with the same
  timeout costs nothing.
- **Unbounded log.** A device that streams masked faults would grow without
  limit for the process lifetime.
- **Log only the device-lost group** (by `groupId`). Grouping is a "should"
  (`VK_KHR_device_fault.adoc:208-212`, `:236-238`), so a driver that does not group would
  make the filter drop context. The spec also says the masked entries that
  preceded the loss are worth having (A2).
- **Read `deviceFaultReportMasked` back from the create chain** (or gate the
  poll on it). No VUID requires it (A6), the ungated result is a clean
  `VK_TIMEOUT`, and it would reverse #242's extension-only stance for a
  convenience.
- **Defensively copy entries into the log.** That doubles allocation on every
  fault to protect against a caller mutating arrays it was told are read-only.
- **No `Dispose` change; document "join your poller first" only.** A missed join
  is undefined behavior in a destroyed device. Taking the lock costs one
  uncontended monitor on a cold path.

## Part B — #245: `VK_KHR_shader_abort` messages on the KHR debug-info read

### B. Problem

`ReadKhrDebugInfo` sends `pNext = null` (`Internal/DeviceFaultReader.cs:342-348`),
as #242 §D step 5 decided: "VK_KHR_shader_abort, which this issue does not wrap".
A shader that executes `OpAbortKHR` triggers device loss and leaves a message
that is retrievable **only** through `VkDeviceFaultShaderAbortMessageInfoKHR`
chained on `vkGetDeviceFaultDebugInfoKHR` (`proposals/VK_KHR_shader_abort.adoc:61-62`).
The wrapper has no extension name for it (`Rendering/VulkanExtensions.cs` lists
none), so it never captures whether the extension was enabled, and
`DeviceFaultReport` (`Lifecycle/DeviceFaultReport.cs:15-40`) has nowhere to put
the message.

### B. Evidence

**B1. The message is not a string.** From `chapters/debugging.adoc:2662-2716`
(`Vulkan-Docs@e4e53e4`):

- `pMessageData` holds "a series of (size,payload) pairs, each aligned to
  8-byte boundaries. The size … is a 64-bit integer". The payload "is laid out
  in the exact manner specified in the OpAbortKHR instruction by the message
  type, with no modifications. If multiple messages are present, the next
  message size will always be at the following 8-byte aligned offset after the
  payload ends."
- "Implementations must report the message reported by the first OpAbortKHR …
  They may report additional messages."
- Note: "no formatting is performed by the Vulkan implementation. Applications
  should consult documentation for the shader language … on how abort messages
  are packed."

The **framing** is defined by the spec. The **payload** is defined by the
shader language.

**B2. What Slang actually emits.** The pinned Slang is v2026.19
(`Directory.Build.props:46`). Its core module declares
`void abort<each T>(NativeString format, expand each T args)`, documented as
"emits `OpAbortKHR` for SPIR-V targets" (string table of
`native/slang/downloaded/win-x64/bin/slang-compiler.dll`).

Compiling `abort("bad value: %u at %u", v, id.x)` with
`slangc -target spirv` produced:

- `OpCapability AbortKHR` and `OpExtension "SPV_KHR_abort"`;
- `%AbortMessage = OpTypeStruct %_arr_uint_int_5 %uint %uint` at member offsets
  **0 / 20 / 24**. The format string is packed little-endian into five `uint`
  words (`0x20646162` = `"bad "` … `0x00752520` = `" %u\0"`), and the two
  arguments are scalar `uint`s;
- `OpAbortKHR %AbortMessage %35`.

There is **no** `ConstantDataKHR` capability: Slang packs the string as `uint`
constants. `spirv-val --target-env vulkan1.3` (SPIRV-Tools v2026.4, SDK
1.4.363) accepts the module.

glslang in the same SDK rejects `#extension GL_EXT_shader_abort`
("extension not supported"). The test project's `glslc` pipeline
(`tests/Ahjo.Vulkan.Tests/Ahjo.Vulkan.Tests.csproj:38-57`) therefore cannot
produce an abort shader. Slang can.

So a Slang abort payload is
`{ NUL-terminated UTF-8 format, padded to 4 bytes; each argument in scalar layout }`.
Decoding the whole payload as UTF-8 would turn the arguments into garbage.

**B3. Spec errata to avoid copying.** The proposal's sample readers
(`VK_KHR_shader_abort.adoc:206-209`, `:263-266`) advance 4 bytes after the
8-byte size, and its byte example (`:221`) shows a big-endian size. The spec chapter
(B1) is authoritative: an 8-byte size in host byte order, then the payload,
with the next size at the next 8-aligned offset.

**B4. Size protocol.** `messageDataSize`: "If `pMessageData` is NULL, this value
is populated by the implementation … with the required size" (`debugging.adoc:2678-2685`).
Unlike `vendorBinarySize` (`:2531-2532`), it is **not** said to be overwritten
with the bytes written on a fill. The two size/data pairs are independent within
one call. `VK_INCOMPLETE` does not say which buffer was short.

**B5. Limits.** `maxShaderAbortMessageSize` is the per-message maximum, "at
least 65536" (`VK_KHR_shader_abort.adoc:103`), and the 4070 Ti reports exactly
65536 (S2). Multiple messages are allowed (B1).

**B6. Gating.** The chained struct must come from an enabled extension (the
spec's pNext validity rule). The layer does not check this (S3), so the wrapper
must. `VK_KHR_shader_abort` depends on `VK_KHR_shader_constant_data`, and the
layer enforces that (S3, VUID-01387). The precedent for capturing an enabled
extension without a function table entry is `MemoryBudgetExtensionEnabled`,
taken from the constructor's span (`Device.cs:39`, `:75-76`).
`DeviceExtensionNames` / `VulkanExtensions` already carry "gates nothing,
dependency only" names (`VulkanExtensions.cs:131-141`, `KhrDeferredHostOperations`).

**B7. Consumers.** None in `src/`, `samples/` or `tests/`; Ahjo has not
requested it. The issue's "without a shader producer, the feature has no
consumer" is answered by B2: the repo's own `Ahjo.Vulkan.Slang` package
produces aborts today.

### B. Decision

**Wrap the extension name, chain the struct whenever the extension was enabled,
parse the spec-defined framing, and surface the raw per-message payloads.** The
wrapper does not decode payloads.

#### B.1 Names and capture

- `DeviceExtensionNames.KhrShaderAbort` (`"VK_KHR_shader_abort"u8`) and
  `DeviceExtensionNames.KhrShaderConstantData` (`"VK_KHR_shader_constant_data"u8`).
- `VulkanExtensions.KhrShaderAbort` and `VulkanExtensions.KhrShaderConstantData`.
  The second gates nothing and exists for the dependency, like
  `KhrDeferredHostOperations`.
- `Device` captures `internal readonly bool ShaderAbortExtensionEnabled` at
  construction, through `PhysicalDevice.ContainsExtension` (the B6 precedent).
- `DeviceFaultEntryPoints` gains `public readonly bool ShaderAbortMessages`,
  set to `ShaderAbortExtensionEnabled && KHR debug-info pointer present`. The new
  constructor parameter defaults to `false`, so every existing fake-backed test
  constructs unchanged.

The recipe, on `VulkanExtensions.KhrShaderAbort`:

1. List `KhrDeviceFault`, `KhrShaderAbort` and `KhrShaderConstantData`.
2. Push `VkPhysicalDeviceFaultFeaturesKHR` (`deviceFault = 1`) and
   `VkPhysicalDeviceShaderAbortFeaturesKHR` (`shaderAbort = 1`), after checking
   `TryGetFeatures<VkPhysicalDeviceShaderAbortFeaturesKHR>(VulkanExtensions.KhrShaderAbort, …)`.
3. `shaderConstantData` is needed only by shaders that declare `ConstantDataKHR`.
   Slang's `abort` does not (B2).

#### B.2 The read (`ReadKhrDebugInfo`, KHR path only)

1. **Query.** Set sType on `VkDeviceFaultDebugInfoKHR`. When `ShaderAbortMessages`
   is set, `pNext` points at a stack `VkDeviceFaultShaderAbortMessageInfoKHR`
   with explicit sType, `pNext = null`, `messageDataSize = 0` and
   `pMessageData = null`. When it is not set, `pNext = null`, exactly as today.
   A negative result returns with everything empty.
2. If **both** sizes are 0, return `VK_SUCCESS` with no fill call. The current
   shortcut (`:351`) considers only the vendor size.
3. **Clamp** each size to its cap, write it back, and record `capped`:
   - the vendor size to `MaxVendorBinaryBytes` (64 MiB, unchanged);
   - the message size to `MaxShaderAbortMessageBytes = 16 MiB`, which is 256
     messages at the 64 KiB per-message floor (B5).
4. **Allocate.** The vendor binary is a `byte[]`. The message buffer is a
   **`ulong[(size + 7) / 8]`**: the element type guarantees an 8-byte-aligned
   pointer, so 8-aligned offsets from the buffer start are 8-aligned addresses,
   which B1 requires. `messageDataSize` is set to the clamped byte size, not the
   rounded-up one. A zero size passes a null pointer.
5. **One fill call** carrying both pairs. A negative result returns with
   everything empty.
6. **Trim.**
   - The binary is trimmed to `min(written vendorBinarySize, allocated)`.
   - The message bytes are trimmed to `min(messageDataSize after the call, clamped size)`.
     The `min` covers a driver that leaves the field untouched on a fill (B4).
7. **Parse** with `ParseShaderAbortMessages(ReadOnlySpan<byte>, out bool truncated)`:
   - Read a native-endian `ulong` size, then the payload. The next pair starts
     at the next multiple of 8 from the buffer start.
   - A size that overruns the remaining bytes stops parsing, keeps the prefix
     already parsed, and sets `truncated`.
   - More than `MaxShaderAbortMessages = 1024` messages stops parsing and sets
     `truncated`. Without this cap, a zero-filled buffer would parse into
     millions of empty messages.
   - Fewer than 8 trailing bytes are padding and are ignored.
   - Zero-length payloads are kept, since the spec does not forbid them.
8. `incomplete = r == VK_INCOMPLETE || capped || truncated`. `VK_INCOMPLETE` is
   ambiguous as to which buffer was short (B4), so both buffers are trimmed and
   the single flag covers either.

Failure isolation is unchanged from #242. A debug-info error keeps the entries,
leaves the binary **and** the messages empty, and writes one warning, whose text
now names both. The read never throws.

#### B.3 Report shape

```csharp
public sealed class DeviceFaultReport
{
    // … existing members …
    public byte[][] ShaderAbortMessages { get; init; } = [];
}
```

Each element is one `OpAbortKHR` payload in driver order. The first is the first
abort executed (B1). The array is empty on the EXT path, when
`VK_KHR_shader_abort` was not enabled, or when no shader aborted. It is
report-level, like `VendorBinary`, because the debug-info call is per device.
The XML remarks state:

- the payload is the shader's message type laid out verbatim (B1);
- what Slang v2026.19 was observed to emit (B2), as an observation and not a
  contract;
- that Slang payloads can be decoded with
  `Ahjo.Vulkan.Slang.SlangAbortMessage` (Part C).

### B. Why not the alternatives

- **`string ShaderAbortMessage`** (the issue's suggestion). The spec says no
  formatting is performed and that the layout belongs to the language (B1). The
  measured Slang payload mixes a format string with binary arguments (B2), so a
  UTF-8 decode would garble every message that has arguments, and a single
  string would also drop every message after the first.
- **Raw `byte[] ShaderAbortMessageData` only.** The (size, payload) framing is
  spec-defined and easy to get wrong: the proposal's own samples get it wrong
  (B3). The wrapper should parse it once.
- **Decode payloads in the core wrapper.** The layout belongs to the shading
  language (B1, B2). Decoding is Part C, in `Ahjo.Vulkan.Slang`. The report
  keeps the raw payloads, so shaders from other compilers still get their bytes.
- **Chain only when `shaderAbort` was enabled** (feature read-back). The pNext
  rule is about the *extension*. A chained struct on a device whose feature is
  off simply reports size 0. It would also reverse #242's extension-only stance.
- **Always chain the struct.** That is a pNext validity violation on devices
  without the extension. The layer would not catch it (S3), which makes it
  worse, not better.
- **A separate `vkGetDeviceFaultDebugInfoKHR` call for the messages.** One call
  can carry both pairs, the spec's own retrieval example uses one call
  (`VK_KHR_shader_abort.adoc:227-275`), and a second call doubles the crash-path
  calls for no new information.
- **Retry without the chain when the chained call fails.** That guards against a
  hypothetical driver bug at the cost of a second protocol path on the crash
  path. Chaining with the extension enabled is valid usage.
- **`byte[]` for the message buffer.** `byte[]` has no alignment contract that
  the language guarantees; `ulong[]` does (B.2 step 4).
- **No count cap.** A garbage or zero-filled buffer would allocate millions of
  empty arrays on a path that must not throw `OutOfMemoryException`.
- **Read `maxShaderAbortMessageSize` to size the buffer.** It is a per-message
  limit, while the buffer holds an unknown number of messages. The size query is
  the documented protocol.
- **Add the names to `KhronosExtensionNames`.** That class is vestigial (#242 §5).

## Part C — Slang abort-payload decoder (in this PR at the maintainer's request)

The architect originally recommended a follow-up issue for this (the earlier
OPEN-2). The maintainer chose to include it, and this part designs it in full.
Part B keeps `DeviceFaultReport.ShaderAbortMessages` as raw payloads, which is
correct for any shading language. Part C adds a decoder for the one language
this repo ships a compiler for.

### C. Problem

A Part B payload is the shader's message type laid out verbatim (B1). For a
Slang `abort(format, args…)` that means a packed format string followed by
binary arguments (B2). A consumer that wants to print "bad value: 42 at 0"
has to know Slang's packing rules. Those rules are not documented anywhere
(B2) and live in neither `Ahjo.Vulkan` nor the Vulkan spec. The repo already
ships a Slang wrapper (`src/Ahjo.Vulkan.Slang/`, which references `Ahjo.Vulkan`;
`Ahjo.Vulkan.Slang.csproj:33`) and pins the compiler version
(`Directory.Build.props:46`). It is the one place that can own those rules and
detect when a Slang bump changes them.

### C. Evidence: what the pinned Slang emits

Every probe below was compiled with the pinned `slangc` (v2026.19,
`native/slang/downloaded/win-x64/bin/slangc.exe`), `-target spirv`, as a
compute shader, and disassembled with SDK 1.4.363 `spirv-dis`. "Offsets" are
the `OpMemberDecorate %AbortMessage n Offset` values. Member 0 is always the
format string.

| `abort(…)` call | `AbortMessage` members | Offsets |
|---|---|---|
| `"no args here"` | `uint[4]` | 0 |
| `""` | `uint[1]` (one zero word) | 0 |
| `"signed %d", s` (int) | `uint[3]`, `int` | 0, 12 |
| `"float %f", f` | `uint[3]`, `float` | 0, 12 |
| `"bad value: %u at %u", v, id.x` | `uint[5]`, `uint`, `uint` | 0, 20, 24 |
| `"u=%u d=%d f=%f x=%x", v, s, f, v` | `uint[5]`, `uint`, `int`, `float`, `uint` | 0, 20, 24, 28, 32 |
| `"big %llu", uint64_t(v)` | `uint[3]`, `ulong` | 0, **16** |
| `"ab %d %lld", s, int64_t(s)` | `uint[3]`, `int`, `long` | 0, 12, 16 |
| `"d %f", double(f)` | `uint[2]`, `double` | 0, 8 |
| `"h %f", half(f)` | `uint[2]`, `half` | 0, 8 |
| `"s %d", int16_t(s)` | `uint[2]`, `short` | 0, 8 |
| `"c %u", uint8_t(v)` | `uint[2]`, `uchar` | 0, 8 |
| `"%hhu %hf %u", uint8_t, half, uint` | `uint[3]`, `uchar`, `half`, `uint` | 0, 12, **14**, 16 |
| `"%hd %hd %lf", int16_t, int16_t, double` | `uint[3]`, `short`, `short`, `double` | 0, 12, 14, 16 |
| `"%hhu %llu", uint8_t, uint64_t` | `uint[3]`, `uchar`, `ulong` | 0, 12, **16** |
| `"%hhu %v3f", uint8_t, float3` | `uint[3]`, `uchar`, `v3float` | 0, 12, **16** |
| `"v2=%u %u", uint2(…)` | `uint[3]`, `v2uint` | 0, 12 |
| `"%v2u", uint2(…)` | `uint[2]`, `v2uint` | 0, 8 |
| `"%llv2u", vector<uint64_t,2>(…)` | `uint[2]`, `v2ulong` | 0, 8 |
| `"b %d", v == 3` (bool) | `uint[2]`, `uint` | 0, 8 |
| `"%08x %.3f %%", v, f` | `uint[4]`, `uint`, `float` | 0, 16, 20 |
| `"Störung ✓ %u", v` | `uint[4]`, `uint` | 0, 16 |
| `"a fairly long … %u"` (69 bytes) | `uint[18]`, `uint` | 0, 72 |

What the table establishes:

- **C-E1. String packing.** The format is its UTF-8 bytes plus one NUL,
  zero-padded to a multiple of 4 and stored as little-endian `uint` words
  (`0x20646162` = `"bad "`). So the word count is `ceil((utf8Length + 1) / 4)`.
  The empty string is one zero word. Multi-byte UTF-8 is stored verbatim:
  `"Störung ✓ %u"` is 15 bytes, which gives 4 words.
- **C-E2. Argument layout is scalar layout.** Each scalar argument is aligned to
  its own size (1, 2, 4 or 8 bytes). A vector is aligned to its component size,
  and its components are contiguous. Arguments are packed in call order straight
  after the string, with no other padding. Evidence: `ulong` lands at 16 after a
  12-byte string, `half` at 14 after a `uchar` at 12, `float3` at 16 after a
  `uchar` at 12, and `uint2` at 12.
- **C-E3. The payload carries no type information.** `%f` with `float`,
  `half` or `double` produces three different layouts, and nothing in the bytes
  says which one it is. `bool` is widened to a 32-bit `uint`. A decoder has to
  take each argument's width and type from the **format specifier**, exactly as
  C `printf` does.
- **C-E4. Slang does not check the format against the arguments.**
  `abort("%u %u", v)` (too few arguments) and `abort("%u", v, v)` (too many)
  both compile. The format is passed through verbatim, which is why `%v2u`
  compiles.
- **C-E5. What Slang refuses.** A string argument is rejected:
  `abort("s %s", "hello")` gives `error[E55211]: unsupported abort argument type`,
  and the stdlib doc string says struct, matrix, array, pointer, resource and
  cooperative arguments get the same error. So `%s` can never have a matching
  argument.

  The zero-argument `abort()` (the HLSL SM 4.0 overload) does **not** fail
  cleanly. Through the wrapper it gets a clean `Compile` with no warnings, and
  then `SlangProgram.Spirv(0)` crashes the process inside `getEntryPointCode`
  (0xC0000005). `slangc -target spirv` prints `error[E29000]: snippet parsing
  failed … unable to parse target intrinsic snippet: abort` and then segfaults.
  This is upstream shader-slang/slang#13166: E29000 is diagnosed, then SPIR-V
  emission hits `SLANG_ASSERT(snippet)`, which is a null dereference in release
  builds. The plan's `ZeroArgumentAbort_DoesNotCompileForSpirv` test was dropped
  for this reason: it would crash the test host instead of failing.
- **C-E6. No `ConstantDataKHR`.** No probe declared `ConstantDataKHR`; the string
  is plain `uint` constants. Under the wrapper's default profile `spirv_1_5`
  (`SlangSessionDescription.cs:27-28`), `abort` warns `E41012 profile implicitly
  upgraded … 'spvAbort'`. Declaring `Capabilities = [spvAbort]`
  (`SlangSessionDescription.cs:69-99`) silences it and produced byte-identical
  SPIR-V in the probe (`cmp`).
- **C-E7. Not a Slang contract, but the extension author's convention.** Slang
  documents `abort` only as "emits `OpAbortKHR`", so the packing above is an
  observation of v2026.19 that Slang itself does not promise. It does, however,
  match the convention the `VK_KHR_shader_abort` proposal sets for GLSL
  (`proposals/VK_KHR_shader_abort.adoc:140-152`, "GLSL changes"): the message
  string and the arguments are "packed into a SPIR-V structure type, laid out
  using scalar packing, with any string arguments converted to an array of
  bytes". That is a reason to expect the layout to be stable, not a guarantee,
  and the tests in C.5 pin it.

The Slang project rules this part has to respect are in `src/Ahjo.Vulkan.Slang/CLAUDE.md`:

- compilation is setup-time;
- no benchmarks for this project;
- compiler diagnostics become exceptions (`SlangCompilationException`);
- nothing here edits `src/Ahjo.Vulkan/`;
- UTF-8 rules for strings handed *to* Slang.

### C. Decision

**A pure managed decoder, `SlangAbortMessage`, in `Ahjo.Vulkan.Slang`.** It
takes one raw payload and returns the format string, the typed arguments and a
printf-style rendering. It never throws for any payload bytes: it returns
`false` from `TryDecode`, and `Describe` falls back to a readable rendering of
the raw bytes. The layout it assumes is pinned by tests that compile real abort
shaders with the pinned Slang through the wrapper and compare against the
emitted SPIR-V.

#### C.1 API

```csharp
namespace Ahjo.Vulkan.Slang;

public sealed class SlangAbortMessage
{
    public string Format { get; }                    // UTF-8 format, up to its NUL
    public SlangAbortArgument[] Arguments { get; }   // one per scalar; vectors expand to N
    public string Text { get; }                      // printf-style rendering of Format + Arguments
    public override string ToString() => Text;

    public static bool TryDecode(ReadOnlySpan<byte> payload,
                                 [NotNullWhen(true)] out SlangAbortMessage? message);

    public static string Describe(ReadOnlySpan<byte> payload);   // never throws, never null
}

public readonly record struct SlangAbortArgument(SlangAbortArgumentKind Kind, int Offset, ulong Bits)
{
    public long   AsInt64();    // sign-extended for the signed kinds
    public ulong  AsUInt64();
    public double AsDouble();   // Float16/32/64 widened; integers converted
}

public enum SlangAbortArgumentKind
{ Int8, UInt8, Int16, UInt16, Int32, UInt32, Int64, UInt64, Float16, Float32, Float64 }
```

- **Input is one payload**, an element of `DeviceFaultReport.ShaderAbortMessages`.
  There is deliberately no overload taking a `DeviceFaultReport`: the decoder
  takes a span so it can be used by anything that holds the bytes.
- **`Arguments`** keep the raw bits and the payload offset, so a consumer can
  re-interpret a value the format got wrong (C-E3).
- **`Describe`** is the one call a crash handler needs:
  `foreach (var p in report.ShaderAbortMessages) log(SlangAbortMessage.Describe(p));`.

#### C.2 What it decodes

1. **The format string.** Find the first NUL in `payload`. If there is none,
   decoding fails. The format is `UTF8.GetString(payload[..nul])`, which replaces
   invalid sequences with U+FFFD and never throws. Arguments start at
   `argStart = AlignUp(nul + 1, 4)` (C-E1). If `argStart > payload.Length`
   and the format has at least one argument specifier, decoding fails. A format
   with no arguments decodes even without its padding.
2. **Conversion specifiers.** These are parsed left to right with C `printf`
   grammar: `%[flags][width][.precision][length][v N]conversion`.
   - Flags: `-`, `+`, space, `0`, `#`.
   - Width and precision: decimal digits only. A `*` fails decoding: there is
     no argument to take it from.
   - The vector prefix `vN`, with N from 2 to 4, comes from GLSL `debugPrintfEXT`.
     Slang passes it through (C-E4), and it consumes N components. It is
     accepted either before or after the length modifier (`%llv2u` and
     `%v2llu` both mean two 64-bit unsigned components). The measured layout is
     `v2ulong` at offset 8.

   | Conversion | No length | `hh` | `h` | `l` / `ll` |
   |---|---|---|---|---|
   | `d`, `i` | Int32 | Int8 | Int16 | Int64 |
   | `u` | UInt32 | UInt8 | UInt16 | UInt64 |
   | `x`, `X` | UInt32 (hex) | UInt8 | UInt16 | UInt64 |
   | `f F e E g G` | Float32 | fails | **Float16** | **Float64** |
   | `%%` | a literal `%`; consumes nothing | | | |

   The two departures from C are deliberate and documented:
   - `%hf` is a 16-bit float, because C has no half type and the payload needs
     the width (C-E3).
   - `%lf` / `%llf` is a 64-bit double, because C's `%lf` means `double` and
     `%f` here means the shader's 32-bit `float`.

   Anything else fails decoding: `%s`, `%c`, `%p`, `%n`, `%a`, `%o`, the length
   modifiers `L z j t`, an unknown conversion, or a lone `%` at the end. `%s` is
   included because Slang refuses string arguments (C-E5).
3. **Layout.** For each specifier, in order:
   - `offset = AlignUp(cursor, componentSize)` (C-E2);
   - read `N × componentSize` bytes, little-endian;
   - advance `cursor`.

   A read past `payload.Length` fails decoding: it means too few arguments
   (C-E4) or a wrong width. **Bytes left over after the last argument are
   ignored.** That covers extra arguments, which C `printf` also ignores, and
   any tail padding the driver includes. Whether a driver pads the payload size
   is unmeasured (C. *Uncertainty*).
4. **Rendering (`Text`).** The format, with each specifier replaced, using
   `CultureInfo.InvariantCulture`:
   - integers in decimal or hex, honoring `-`, `+`, space, `0`, width,
     precision (minimum digits), and `#` (a `0x` / `0X` prefix);
   - floats with C semantics: `f` with default precision 6; `e` with at least
     two exponent digits (`1.500000e+01`); `g` choosing `e` or `f` by C's rule
     and stripping trailing zeros unless `#`; NaN and infinity as
     `nan` / `inf` (uppercase for `F E G`);
   - vector components joined with `", "`, the `debugPrintfEXT` convention.

   The rendering is C-like, not byte-identical to any particular libc. The
   fixed test vectors in plan C3 are the definition.

#### C.3 Failure behavior: never throws on the fault path

- **`TryDecode` is total by construction.** It checks every slice bound before
  slicing, decodes UTF-8 with replacement, and formats through invariant culture.
  A malformed payload returns `false` with `message = null`. There is no
  `try/catch`: a catch would hide a bounds bug from the property test (plan C3)
  that is the actual guard.
- **`Describe` never fails.**
  - If decoding succeeds, it returns `Text`.
  - If a format was found but the arguments did not decode, it returns
    `$"{Format} <abort arguments not decoded: {reason}; {payload.Length} payload bytes>"`.
    `reason` is a short internal string such as
    `"unsupported conversion '%s' at format index 7"` or
    `"argument 2 needs 8 bytes at offset 16, payload has 20"`.
  - If there is no NUL, it returns
    `$"<shader abort payload, {n} bytes, not a Slang abort message: {hex}>"`,
    with the first 64 bytes as lowercase hex pairs separated by spaces and
    `" …"` when truncated.
- **How this squares with `src/Ahjo.Vulkan.Slang/CLAUDE.md`.** That file's
  "diagnostics are exceptions" rule covers **compiler** diagnostics: a failed
  compile must throw `SlangCompilationException` instead of returning empty
  SPIR-V. The decoder never calls into Slang. Its input is forensic data a GPU
  produced while dying, and it runs on the same crash path as #242's reader. A
  second exception thrown over the caller's `DeviceLost` handling is the
  failure #242 ruled out, so the #242 never-throw posture applies. The other
  rules hold:
  - the decoder allocates freely (setup-time posture; it runs once per crash);
  - it adds no benchmark (that file's explicit rule);
  - it does not touch `src/Ahjo.Vulkan/`, and only *reads* the
    `DeviceFaultReport` bytes the consumer passes in;
  - the UTF-8 rule concerns strings handed *to* Slang, while this decodes UTF-8
    coming *out* of a payload.

#### C.4 AOT

AOT safety here is established by inspection of the APIs used:

- no reflection;
- no `string.Format` with runtime format strings (the renderer builds the
  output with a `StringBuilder` and `TryFormat` on primitives);
- no dynamic code;
- `BitConverter.UInt16BitsToHalf`, `BinaryPrimitives` and `Encoding.UTF8` are
  all trim-safe.

`samples/AotSmoke` references `Ahjo.Vulkan.Slang` (`AotSmoke.csproj:37`) but
does not call the decoder. The CI AOT publish therefore does not exercise this
code, and nothing here needs it to.

#### C.5 Pinning the layout so a Slang bump fails loudly

`tests/Ahjo.Vulkan.Slang.Tests/SlangAbortLayoutTests.cs` is driverless, because
compiling needs no GPU.

- Each fixture is a row of the C. *Evidence* table, with the format spelled to
  match its arguments' widths (`"h %hf"` rather than `"h %f"`). The table's
  mismatched spellings are evidence for C-E3, not decoder inputs. It is compiled through the
  wrapper (`SlangSession.Compile`, `Capabilities = [spvAbort]`), with an
  assertion of **no warnings**, which pins C-E6.
- The emitted SPIR-V (`SlangProgram.Spirv(0)`) is then read by a new
  `SpirvDecorations.ReadAbortMessageLayout`. It follows `OpAbortKHR`
  (opcode 5121) to the message type and returns:
  - each member's offset and scalar type (width, signedness, float) and vector
    count;
  - member 0's `OpConstantComposite` words.
- Each fixture asserts:
  1. member 0's words, as little-endian bytes, equal the UTF-8 format plus NUL
     plus zero padding (C-E1);
  2. the decoder's own layout for that format (internal
     `SlangAbortMessage.TryComputeLayout`) equals the SPIR-V member offsets and
     kinds (C-E2, C-E3);
  3. a payload synthesized from the **SPIR-V-derived** layout with known values
     decodes to the expected `Text`.
- The failure messages name `SlangPinnedVersion.Tag` and say "Slang changed the
  abort payload layout; re-probe (spec Part C) before bumping".

The test project already generates `SlangPinnedVersion`
(`Ahjo.Vulkan.Slang.Tests.csproj:41-53`) and has `SpirvDecorations` as its
SPIR-V oracle (`SpirvDecorations.cs:166`, `ReadMemberOffsets`). This follows
the project's rule that reflection agreeing with itself proves nothing. Here
the decoder agreeing with itself would prove nothing either.

What the pin covers and what it does not:

- It proves the decoder matches the **SPIR-V Slang emits**.
- It cannot prove that a driver writes the payload exactly as `OpAbortKHR`'s type
  says. The Vulkan spec promises that ("no modifications", B1), but it is
  unverified on hardware, because the real device-loss test was not taken (see
  *Test strategy*).

### C. Why not the alternatives

- **Decode in `Ahjo.Vulkan` (on `DeviceFaultReport`).** The layout is Slang's,
  not Vulkan's. The core wrapper would have to track a compiler it does not
  reference, and a non-Slang payload would be misread.
- **A Slang-side reflection of the abort message type** (ask Slang for the
  `AbortMessage` layout). The fault path has only the payload bytes, not the
  program that produced them. A crash handler cannot be required to keep the
  `SlangProgram` alive, and per-call-site layouts are not reachable through the
  reflection API this package wraps.
- **Infer argument widths from the payload length.** The payload carries no type
  information (C-E3). `float` and `int`, or `half` + `half` and `float`, are the
  same size.
- **Throw `FormatException` on a malformed payload.** It runs on a crash path
  (C.3).
- **Strict tail check** (fail when bytes are left over). It would reject valid
  messages with extra arguments, which C ignores, and would turn any driver tail
  padding into a decode failure that nobody can test locally.
- **Support `%s`.** No Slang abort can carry a string argument (C-E5).
- **Plain C semantics for `%f` / `%lf` / `%hf`.** C has no 16-bit float, and its
  `%lf` would be `float` here, which silently misreads a shader `double`.
- **Return only the formatted string.** It loses the raw values a consumer needs
  when the format's specifier disagrees with the shader's argument type, and
  Slang does not check that (C-E4).
- **A `try/catch` around the decoder.** It hides bounds bugs from the property
  test, and totality by construction is checkable.
- **A wrapper constant for the `spvAbort` capability.** No caller needs it beyond
  the tests. `Utf8Name.FromLiteral("spvAbort"u8)` is the documented form
  (`SlangSessionDescription.cs:69-99`).

### C. Uncertainty

- **Driver tail padding.** Whether a driver's payload size includes the
  struct's tail padding is unknown. Leftover bytes are ignored (C.2 step 3), so
  either answer decodes.
- **Specifier coverage.** The supported set covers every argument type Slang
  accepts (scalars and vectors of 8/16/32/64-bit integers, 16/32/64-bit floats,
  and bool as `uint`). Rendering is C-like rather than libc-identical.
- **The vector spelling.** `%vN` is GLSL `debugPrintfEXT`'s spelling, and Slang
  passes it through untouched. Whether Slang's GLSL backend (`abortEXT`) treats
  it specially was not probed, because only the SPIR-V target matters here.

## Interaction between the parts

Both parts touch `DeviceFaultReader.TryRead`:

- Part A adds the log parameter and the "false only if the drain failed and the
  log is empty" rule.
- Part B changes `ReadKhrDebugInfo`'s signature and fills `ShaderAbortMessages`.

They edit different statements. The plan does Part A first and leaves the tree
green before Part B starts.

Part C touches only `src/Ahjo.Vulkan.Slang/` and its tests. It depends on Part
B only through the payload format: it reads `byte` spans and never references a
Part B type in its API. The plan does it last, at its own checkpoint. A crash report after a shader abort carries both the
logged entries (A) and the abort messages (B). Polling never touches the abort
messages, because the debug-info call is lost-only.

## Invariants honored

1. **UTF-8.** The two new extension names are `"…"u8` literals in
   `DeviceExtensionNames`, surfaced as `Utf8Name.FromLiteral`. The core wrapper
   decodes no payloads. The Part C decoder reads UTF-8 *out of* a payload
   (driver to managed), the direction invariant #1 does not govern. The tests
   pass the `spvAbort` capability as a `Utf8Name.FromLiteral("…"u8)`.
2. **AOT.** `delegate* unmanaged`, `[UnmanagedCallersOnly]` test fakes,
   `Monitor.TryEnter`, `MemoryMarshal`. No reflection and no dynamic code.
3. **Zero per-frame allocations.** The no-fault poll is allocation-free (A.6),
   with a benchmark row and a device-level assertion. The crash read and the
   Part C decoder are not per-frame paths, and `Ahjo.Vulkan.Slang` takes no
   benchmarks by rule (C.3).
4. **Generated code untouched** (S1).
5. **Warnings-as-errors.** No suppression is needed.

## Test strategy

The rules from #242 stand:

- the reader tests are driverless and fake-backed (`DeviceFaultReaderTests`, a
  serial suite with static fakes);
- device tests are gated `[gate:driver]` / `[gate:feature]` / `[gate:validation]`;
- the forbidden tests stay forbidden: no `ReadKhrDebugInfo`, `ReadExt` or
  `MarkLost()` → `TryGetDeviceFault()` on an extension-enabled healthy device.

**Part A.**

- **Driverless.** The new `DeviceFaultLog` (ring, overflow, `Dropped`, lazy
  allocation, snapshot order), and `TryRead` with a pre-filled log:
  - polled entries come before fresh ones;
  - a failed drain with a non-empty log still produces a report;
  - repeat calls return the same entries;
  - `Dropped` maps to `IsIncomplete`.
- **Device-level** (4070 Ti, `[gate:feature]`):
  - a real healthy-device `TryPollDeviceFaults(TimeSpan.Zero)` returns `false`
    with an empty array;
  - the gates hold (lost, no KHR, disposed, bad timeout);
  - the poll is zero-allocation;
  - a validated twin runs at the 1.4.363 floor.
- A masked fault **cannot** be produced on any local GPU (S2:
  `deviceFaultReportMasked = false`). The drained-entry path of the poll is
  covered by fakes only.

**Part B.**

- **Driverless.** The parser (framing, alignment, overrun, count cap, padding),
  and `ReadKhrDebugInfo` with the abort fake: chaining on and off, sTypes, the
  size query passing 0/null, the fill passing clamped sizes and an 8-aligned
  pointer, message-only and binary-only fills, the caps, errors, and `TryRead`
  composition.
- **Device-level.** Create with KHR fault + shader abort + constant data and
  both features, plain and under validation (`[gate:feature]`, NVIDIA only),
  asserting `ShaderAbortExtensionEnabled` and the entry-point flag.
- The layer is the oracle for the create chain and the dependency rule (S3). It
  is **not** the oracle for the chaining gate; the fake tests are.

**Part C.** Driverless, in `Ahjo.Vulkan.Slang.Tests`:

- layout pins: real abort shaders compiled with the pinned Slang, checked
  against the emitted SPIR-V (C.5);
- decoder unit vectors: every supported conversion and length, flags, width and
  precision, vectors, and every failure reason;
- a property test: random and truncated payloads never throw, and `Describe`
  never returns null.

**Real device loss: rejected for this PR (maintainer's decision, 2026-10-05).**
`OpAbortKHR` would be the first way to produce a *real* device loss on the dev
host. A Slang abort shader dispatched on the 4070 Ti would run the whole #242
post-loss path, plus Parts B and C, against a real driver under the layer's own
loss tracking. It was not taken because it deliberately loses the device on the
display GPU, possibly with a TDR. A real loss also marks every live `Device`
lost through `Device.NotifyDeviceLossObserved` (`Device.cs:121-133`), and a GPU
reset can disturb the ICD for the rest of the process.

**The coverage limit this leaves.** Both new paths are tested only against fake
function pointers and synthesized bytes, never against a real driver:

- the KHR post-loss read, now with the fault log and abort-message chaining;
- the masked-fault poll's drained-entry path.

The Part C decoder is pinned to the SPIR-V the pinned Slang emits, not to bytes
a driver wrote. What a real driver does with `messageDataSize` on a fill, with
multiple messages and with payload tail padding stays unmeasured
(*Uncertainty*). If a consumer later needs that evidence, the shape to use is:
opt-in, its own `[gate:<class>]`, its own `dotnet test` invocation, in
`Ahjo.Vulkan.Slang.Tests`.

**CI.** The hosted Windows runner has neither extension, so the new device
tests skip as `[gate:feature]`. The driverless tests run everywhere. The PR
quotes a local `AHJO_VULKAN_TIER=validation` run on the 4070 Ti.

## Uncertainty, stated

- **The caps are judgment values**: a log `Capacity` of 1024,
  `MaxShaderAbortMessageBytes` of 16 MiB and `MaxShaderAbortMessages` of 1024.
  Each is far above any measured need (`maxDeviceFaultCount = 1`; one 64 KiB
  message), and each is an `internal const`.
- **The masked-fault path has never touched a driver.** No local GPU supports
  `deviceFaultReportMasked` (S2).
- **No abort message has been read from a real driver.** The wrapper protocol is
  fake-tested. Whether NVIDIA writes `messageDataSize` back on a fill (B4), and
  whether it reports more than one message, stays unknown: the real
  device-loss test was not taken (*Test strategy*).
- **The Slang payload layout** (B2, C. *Evidence*) comes from 23 probe compiles
  at v2026.19 and is not a Slang contract. The C.5 tests pin it, so a Slang bump
  that changes it fails the suite rather than misdecoding silently.
- **The 2× timeout bound** of a contended poll (A.2 step 5) is approximate,
  because `Monitor.TryEnter` and the driver's timeout each have their own
  granularity.

## Open items

None. All three earlier OPEN items were resolved by the maintainer on 2026-10-05:

- the real device-loss test was dropped (see *Test strategy* for the coverage
  limit);
- the Slang decoder was included (Part C);
- the fault-lock test hook was allowed (plan A7 case 7).

## Cross-links

- **Resolves** #244 and #245.
- **Must stay consistent with** the Slang wrapper's rules (`src/Ahjo.Vulkan.Slang/CLAUDE.md`) and its design record (`2026-08-01-issue-166-slang-support-design.md`).
- **Builds on** #242 (reader, `_faultReadLock`, never-throw posture, caps), #120
  (`IsLost`, no post-loss blocking), #246 (`TryGetFeatures<T>`, the pre-create
  recipe for `deviceFaultReportMasked` and `shaderAbort`).
- **Must land consistently with** #117 (result policy: `VK_TIMEOUT` / `VK_INCOMPLETE`
  are success codes, branched on by sign), #122 (shadow enums; no new enum here),
  #158 (`[gate:*]` classification), #32 (Windows-only wrapper tests).
- **Resolves, beyond the issues,** the payload-decoding gap #245 left open (Part C).
- **Follow-ups:** none from this design. Releasing (`v*` tag) is a separate step
  after merge.
