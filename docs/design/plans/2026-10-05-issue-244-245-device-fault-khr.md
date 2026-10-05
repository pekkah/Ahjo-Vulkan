Paired with [../specs/2026-10-05-issue-244-245-device-fault-khr-design.md](../specs/2026-10-05-issue-244-245-device-fault-khr-design.md). Read it first; this plan only says *how*.

# Implementation plan: issues #244 + #245, KHR device-fault polling, shader-abort messages and the Slang abort decoder

Branch: `issue-244-245-device-fault-khr` (from `main`). One PR, `Closes #244`, `Closes #245`.

**Maintainer decisions (2026-10-05):**

- the design is approved;
- the real device-loss test is dropped (there is no Step B8);
- the fault-lock test hook is allowed (A7 case 7 is firm);
- the Slang abort-payload decoder is in this PR (Part C).

This is managed-surface work only. Nothing under `src/*/Generated/`, `native/` or
`tools/*.rsp` changes, and there is no regen (spec S1). Every native type is
already generated:

- `VkDeviceFaultShaderAbortMessageInfoKHR`
- `VkPhysicalDeviceShaderAbortFeaturesKHR`
- `VkPhysicalDeviceShaderConstantDataFeaturesKHR`
- the `VK_STRUCTURE_TYPE_DEVICE_FAULT_SHADER_ABORT_MESSAGE_INFO_KHR` sType (`VkStructureType.cs:587`)
- everything #242 already uses

**Order.**

1. Part A (#244, Steps A1–A8) comes first and ends at a green checkpoint.
2. Part B (#245, Steps B1–B7) builds on that tree and ends at its own checkpoint.
3. Part C (the Slang decoder, Steps C1–C6) is last, with its own checkpoint.
4. The shared tail (Steps T1–T3) covers docs, verification and the PR.

Do not interleave the parts. Each one should build and pass on its own, so a
reviewer can follow any of them.

Unchanged from #242 and still binding:

- no `vk…(` call spellings in `DeviceFaultReader`;
- no `ThrowIfFailed` / `ThrowIfErrored`; branch on `(int)r < 0`;
- an explicit sType and `pNext` on every native struct;
- every driver-reported size is clamped and written back;
- the read never throws for a driver result.

---

## Part A: #244, healthy-device polling

### Step A1: `src/Ahjo.Vulkan/Internal/DeviceFaultLog.cs` (new)

```csharp
/// Bounded ring of every KHR fault entry the wrapper has drained for one device
/// (spec A.3). Not thread-safe: Device touches it only under _faultReadLock.
internal sealed class DeviceFaultLog
{
    internal const int Capacity = DeviceFaultReader.MaxKhrEntries; // 1024

    public bool Dropped { get; }        // sticky: set once any entry was overwritten
    public int  Count   { get; }

    public void Append(ReadOnlySpan<DeviceFaultEntry> entries);
    public DeviceFaultEntry[] Snapshot(); // oldest first; [] (shared empty) when Count == 0
}
```

- **Lazy storage.** The backing `DeviceFaultEntry[]` is allocated on the first
  `Append` with a non-empty span, at `Capacity`. Appending an empty span
  allocates nothing.
- **Ring.** Writes go to `(_start + Count) % Capacity`. When full, the write
  overwrites the oldest slot, advances `_start` and sets `Dropped = true`. If a
  single `Append` is longer than `Capacity`, only its last `Capacity` elements
  survive, and `Dropped` is set.
- **`Snapshot()`.** Copies the entries in order into a new array. That one
  allocation is fine, because it only happens on the crash read.
- The `<remarks>` say entries are shared by reference with the poll's return
  value and the reports. That is spec A.3; do not copy them.

### Step A2: `src/Ahjo.Vulkan/Internal/DeviceFaultReader.cs`, lazy list in `ReadKhrEntries`

- Replace `var collected = new List<DeviceFaultEntry>();` (`:270`) with
  `List<DeviceFaultEntry>? collected = null;`.
- Create the list only after a count call returns a non-zero `count`. That is
  just before the first `collected.Add`, or before computing `requested`; the
  current `MaxKhrEntries - collected.Count` becomes `- (collected?.Count ?? 0)`.
- Every `entries = collected.ToArray()` becomes
  `entries = collected is null ? [] : collected.ToArray();`. The `[]` must
  resolve to `Array.Empty<DeviceFaultEntry>()`; a collection expression
  targeting an array type does.
- Behavior is otherwise identical. The existing 11 KHR-entry tests must pass
  unchanged.
- Add one sentence to the method `<summary>`: "Allocates nothing when the first
  count call reports no entries; a per-frame poll depends on this (#244)."

### Step A3: `src/Ahjo.Vulkan/Internal/DeviceFaultReader.cs`, `TryRead` takes the log

New signature:

```csharp
internal static bool TryRead(
    in DeviceFaultEntryPoints eps,
    VkDevice_T*               device,
    ulong                     timeoutNs,
    DeviceFaultLog            log,
    [NotNullWhen(true)] out DeviceFaultReport? report);
```

- **The EXT arm** ignores `log` and is unchanged.
- **The KHR arm:**
  1. Run `ReadKhrEntries` as now.
  2. **Always** call `log.Append(entries)`, even after a negative result. Drained
     entries are logged whatever happened.
  3. On a negative result:
     - if `log.Count == 0`, write the existing "no device-fault report is
       available" warning and return `false`. The message text is unchanged.
     - otherwise, write the existing partial-drain warning and set
       `entriesIncomplete = true`. Return `true` later.
     - Keep the existing `{entries.Length}` in the partial-drain warning: it
       counts what *this* read drained.
     - A negative result where this read drained 0 entries but the log is
       non-empty needs a new message:
       `$"Device.TryGetDeviceFault: vkGetDeviceFaultReportsKHR returned {r}; returning {log.Count} previously drained entries."`
  4. Make the debug-info call as now (Part B changes it later).
  5. Build the report with `Entries = log.Snapshot()` and
     `IsIncomplete = entriesIncomplete || binaryIncomplete || log.Dropped`.
- Update the class `<remarks>`. A KHR read now reports the device's fault log:
  entries drained by earlier polls, then this read's. Repeat calls are
  idempotent.

### Step A4: `src/Ahjo.Vulkan/Lifecycle/Device.cs`

**A4a. Field.** Add `private readonly DeviceFaultLog _faultLog = new();` next
to `_faultReadLock` (`:49`). Extend that field's comment: "also guards
`_faultLog` and is taken by `Dispose`".

**A4b. Shared timeout helper.** Extract the validation now inline at
`:266-276` into

```csharp
private static ulong ToFiniteFaultTimeout(TimeSpan timeout, string paramName)
```

It throws `ArgumentOutOfRangeException` for a negative span, which includes
`InfiniteTimeSpan`, and for a span that saturates to `UINT64_MAX`. It returns
the nanoseconds. Keep the message text exactly as it is, apart from replacing
"after device loss" with "on the device-fault path". `TryGetDeviceFault` calls it
with `nameof(timeout)`. The existing `DeviceFaultTests` timeout cases
(`DeviceFaultTests.cs:96-133`) assert only the exception type, not the
message (verified), so they keep passing.

**A4c. `TryGetDeviceFault(TimeSpan, out …)`.**

- Inside the `lock`, add `ObjectDisposedException.ThrowIf(_disposed, this);`
  before the read. Use the `bool ok = …; return ok;` form if CS8762 objects.
- Pass `_faultLog` into `TryRead`.
- Update the `<remarks>`:
  - **Repeat calls.** "EXT and KHR both return the same report again; KHR's
    `Entries` is the device's fault log (entries drained by earlier
    `TryPollDeviceFaults` calls, then the entries this read drained), capped at
    1024; nothing new is drained after the first post-loss read."
  - **Thread safety.** Append: "a read waits for an in-flight
    `TryPollDeviceFaults`, at most that poll's timeout."
  - Replace the closing "Healthy-device KHR polling … is not wrapped." with
    "Healthy-device KHR polling: see `TryPollDeviceFaults`."

**A4d. New method.** Insert it after `TryGetDeviceFault(TimeSpan, …)`:

```csharp
public bool TryPollDeviceFaults(TimeSpan timeout, out DeviceFaultEntry[] entries)
{
    ObjectDisposedException.ThrowIf(_disposed, this);
    ulong timeoutNs = ToFiniteFaultTimeout(timeout, nameof(timeout));
    entries = [];
    var fn = Functions.GetDeviceFaultReports;
    if (fn == null) return false;   // no VK_KHR_device_fault; EXT is lost-only
    if (IsLost) return false;       // crash read owns the queue after loss; #120
    // Monitor.TryEnter(object, TimeSpan) throws above int.MaxValue ms (~24.8 days),
    // which ToFiniteFaultTimeout accepts: clamp the lock wait only.
    TimeSpan lockWait = timeout.TotalMilliseconds > int.MaxValue
        ? TimeSpan.FromMilliseconds(int.MaxValue) : timeout;
    bool lockTaken = false;
    try
    {
        Monitor.TryEnter(_faultReadLock, lockWait, ref lockTaken);
        if (!lockTaken) return false;
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsLost) return false;
        VkResult r = DeviceFaultReader.ReadKhrEntries(fn, Handle, timeoutNs, out entries, out _);
        _faultLog.Append(entries);
        if ((int)r < 0) { /* sink warning, see below */ }
        return entries.Length > 0;
    }
    finally { if (lockTaken) Monitor.Exit(_faultReadLock); }
}
```

That is the shape, not the final body. On a negative `r`, write exactly one
warning (`DiagnosticSeverity.Warning`, source `"Device"`):

- with no entries:
  `$"Device.TryPollDeviceFaults: vkGetDeviceFaultReportsKHR returned {r}; no fault entries were drained."`
- with entries:
  `$"Device.TryPollDeviceFaults: vkGetDeviceFaultReportsKHR returned {r} after {entries.Length} entries were drained; returning them."`

The `out _` ignores the cap flag on purpose: the remainder stays queued for the
next poll (spec A.2 step 7).

XML docs:

- `<summary>`: "Drains fault reports from a **healthy** device through
  `vkGetDeviceFaultReportsKHR`: faults the driver recovered from without losing
  the device (masked faults)."
- `<param name="timeout">`: it bounds both the wait for another in-flight
  fault read and the driver's wait for a first report. Every later driver call
  passes 0. It must be non-negative and finite. `TimeSpan.Zero` is the
  non-blocking per-frame form. The lock wait is additionally capped at
  `int.MaxValue` ms (about 24.8 days), `Monitor`'s limit.
- `<param name="entries">`: never null; empty when the method returns `false`.
- `<returns>`: `true` when at least one entry was drained.
- `<exception>`: `ObjectDisposedException` and `ArgumentOutOfRangeException`,
  as for `TryGetDeviceFault`.
- `<remarks>`:
  - **Feature.** Masked reports need `deviceFaultReportMasked`. Enable it via
    `ConfigureFeatures` after `PhysicalDevice.TryGetFeatures<VkPhysicalDeviceFaultFeaturesKHR>`
    reports it. Without it a poll only ever times out. The wrapper does not
    check the feature.
  - **Requires** `VK_KHR_device_fault`. It returns `false` on EXT-only devices.
  - **After loss** it returns `false` without a driver call; use
    `TryGetDeviceFault`. A background loop that sees `false` with `IsLost`
    set should exit, because the queue now belongs to `TryGetDeviceFault`. It
    mentions the multi-device `IsLost` caveat in one sentence.
  - **One queue.** Every drained entry is also kept in the device's fault log,
    so a later `TryGetDeviceFault` reports it too, including a device-lost entry
    a background poller drained before the loss was observed.
  - **Shared instances.** Entries are shared with later reports; treat their
    arrays as read-only.
  - **Threading.** Fault reads are serialized. A contended poll returns `false`
    rather than wait past `timeout`. `TryGetDeviceFault` and `Dispose` wait for
    an in-flight poll. Keep background-poller timeouts at or below
    `DefaultDeviceFaultTimeout`, and join the poller before `Dispose`.
  - **Allocation.** A poll that finds nothing allocates nothing.

**A4e. `Dispose`.** Keep `if (_disposed) return;` first. Then wrap the whole
existing `try { … } finally { … }` in `lock (_faultReadLock) { if (_disposed) return; … }`.
Add a comment: "Waits for an in-flight fault poll or read (bounded by its finite
timeout) so vkDestroyDevice never runs under a thread inside
vkGetDeviceFaultReportsKHR. Safe from the finalizer: an unreachable Device has
no poller." Update the class `<remarks>` "Thread safety" paragraph
(`Device.cs:19-22`) with one sentence on the same point.

### Step A5: `src/Ahjo.Vulkan/Rendering/VulkanExtensions.cs` and `Internal/DeviceFunctionTable.cs`

- `KhrDeviceFault` `<remarks>`: replace the last paragraph (`:194-200`) with
  the polling recipe.
  - `Device.TryPollDeviceFaults` reads masked faults on a healthy device when
    `deviceFaultReportMasked` was enabled.
  - Query support first, with
    `gpu.TryGetFeatures<VkPhysicalDeviceFaultFeaturesKHR>(KhrDeviceFault, out var f)`,
    and copy `f.deviceFaultReportMasked` into the pushed struct.
  - `deviceFaultDeviceLostOnMasked` remains the caller's.
- `DeviceFunctionTable` charter comment (`:56-60`): replace "The wrapper calls
  all three only after `Device.IsLost`." with "EXT info and KHR debug-info are
  called only after `Device.IsLost`; KHR reports is also called on a healthy
  device by `Device.TryPollDeviceFaults` (#244)." Make the same change to the
  inline comment above the KHR block (`:574-576`).
- `DeviceExtensionNames` `<remarks>` (`:31-35`): "called by the wrapper only
  after loss" becomes "called by the wrapper after loss and, through
  `Device.TryPollDeviceFaults`, on a healthy device".

### Step A6: Tests, driverless

**`tests/Ahjo.Vulkan.Tests/DeviceFaultLogTests.cs`** (new; no `TestGate`; 7 cases):

1. `Empty_SnapshotIsEmpty_NotDropped_NoStorage`. Asserts `Snapshot()` is empty
   and `Dropped` is false. Assert storage was not allocated through an internal
   `bool HasStorage` test seam, **or** via
   `GC.GetAllocatedBytesForCurrentThread` across an empty `Append`. Prefer the
   allocation measurement, and warm up with one empty `Append` before
   measuring, because the first call JITs. Add no seam unless it is needed.
2. `Append_PreservesOrder_AcrossCalls`.
3. `Append_EmptySpan_IsNoOp`.
4. `Overflow_DropsOldest_SetsDropped`. Append `Capacity + 3` entries over
   several calls; the snapshot holds the last `Capacity`, oldest first.
5. `SingleAppendLargerThanCapacity_KeepsTail_SetsDropped`.
6. `Snapshot_IsACopy`. Mutating the returned array does not change the next
   snapshot.
7. `Dropped_IsSticky`.

**`tests/Ahjo.Vulkan.Tests/DeviceFaultReaderTests.cs`** (extend):

- Mechanical: the seven existing `TryRead(…)` call sites (`:973`, `:989`,
  `:1009`, `:1030`, `:1056`, `:1093`, `:1123`) pass `new DeviceFaultLog()`.
- New `TryRead` + log cases, with the sink captured by `InstallDeviceSink`:
  - `TryRead_Khr_LogEntriesPrecedeFreshEntries`. Pre-append 2 entries (groupIds
    1 and 2) to the log; the fake queue holds 1 (groupId 3, `DeviceLost`). The
    report's `Entries` groupIds are `[1, 2, 3]`.
  - `TryRead_Khr_RepeatCall_ReturnsSameEntries`. Two `TryRead`s with one log:
    the second drains nothing, and `Entries` is equal by sequence and by
    reference to the first.
  - `TryRead_Khr_DrainErrorWithNonEmptyLog_ReturnsTrue_Incomplete`. The first
    count call returns `VK_ERROR_UNKNOWN` and the log holds 1 entry. The result
    is `true`, 1 entry, `IsIncomplete`, and exactly the new "previously drained"
    warning.
  - `TryRead_Khr_DrainErrorWithEmptyLog_ReturnsFalse`. This is the existing
    behavior, asserted with an explicit empty log.
  - `TryRead_Khr_LogDropped_SetsIncomplete`.
  - `TryRead_Ext_IgnoresLog`. EXT with a non-empty log returns exactly one
    entry, the EXT one.
- New `ReadKhrEntries` case: `Khr_NoFault_ReturnsSharedEmptyArray`. On
  `VK_TIMEOUT` from the first count, `entries` is `Array.Empty<DeviceFaultEntry>()`
  by reference.

### Step A7: Tests, device-level (`tests/Ahjo.Vulkan.Tests/DeviceFaultTests.cs`, extend)

Add to the class `<remarks>` a sentence noting that the polling cases (#244) call
only the reports command, which is legal on a healthy device.

**`[gate:driver]`** (any ICD; reuse `CreateGraphicsDevice` with no fault
extension):

1. `Poll_NoKhrExtension_ReturnsFalse_EmptyArray`. Also check it after
   `MarkLost()`. That is safe because there is no pointer to call.
2. `Poll_Disposed_Throws`.
3. `Poll_NegativeInfiniteOrSaturatingTimeout_Throws`. Check
   `TimeSpan.FromMilliseconds(-1)`, `Timeout.InfiniteTimeSpan` and
   `TimeSpan.MaxValue`.

**`[gate:feature]`** (KHR, through `TryCreateFaultDevice(…, khr: true, …)`):

4. `Poll_HealthyKhrDevice_RealDriver_ReturnsFalse`. Use
   `TryPollDeviceFaults(TimeSpan.Zero, out var e)`. The result is `false`, `e`
   is empty and non-null, and `IsLost` is false. Output-log the result.
5. `Poll_AfterMarkLost_ReturnsFalse_WithoutDriverCall`. After `MarkLost()`, a
   poll returns `false`. This is safe: the poll's own gate runs before any
   call, and the reports command would be legal anyway. **Do not** call
   `TryGetDeviceFault` afterwards; that is the forbidden test.
6. `Poll_NoFault_IsZeroAllocation`. Warm up with one poll, then take
   `long before = GC.GetAllocatedBytesForCurrentThread();`, run 128 ×
   `TryPollDeviceFaults(TimeSpan.Zero, out _)`, and assert a delta of 0. The
   precedent is `AccelerationStructureTests.cs:875`.
7. `Poll_ContendedLock_TimeoutZero_ReturnsFalseImmediately`. Expose the lock
   through an internal seam, `internal object FaultReadLockForTests => _faultReadLock;`
   on `Device`. **The contention must come from another thread**: `Monitor`
   is reentrant, so a lock held by the test thread itself would let the poll
   through.
   - A holder thread runs `lock (device.FaultReadLockForTests) { acquired.Set(); release.Wait(); }`,
     using two `ManualResetEventSlim`s.
   - The test waits on `acquired`, then asserts that
     `TryPollDeviceFaults(TimeSpan.Zero, out var e)` returns `false` with
     `e` empty.
   - The test then sets `release` and joins the holder.
   This is deterministic: no blocking poll and no timing bound. The seam is
   approved by the maintainer (2026-10-05). Give it a one-line doc comment:
   "Test seam (InternalsVisibleTo): lets DeviceFaultTests hold the fault lock
   from another thread." The precedents are the `// Test seam` members at
   `Pipelines/PipelineCache.cs:163` and `Rendering/Swapchain.cs:733`.

**`[gate:validation]` + `[gate:feature]`**, at the `MinKhrLayer` floor:

8. `Poll_RealDriver_UnderValidation_Clean`. Case 4 under
   `CreateValidatedInstance`, followed by `AssertNoValidationErrors`.

### Step A8: Benchmark, then **checkpoint A**

- New file `tests/Ahjo.Vulkan.Benchmarks/DeviceFaultPollBenchmarks.cs`:

  ```csharp
  [MemoryDiagnoser]
  public unsafe class DeviceFaultPollBenchmarks
  {
      [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
      private static VkResult FakeReportsNoFault(VkDevice_T* d, ulong t, uint* pCount, VkDeviceFaultInfoKHR* p)
      { *pCount = 0; return VkResult.VK_TIMEOUT; }

      [Benchmark(OperationsPerInvoke = 1024)]
      public int ReadKhrEntries_NoFault_1024() { /* 1024 × ReadKhrEntries(&FakeReportsNoFault, (VkDevice_T*)0x1, 0, out var e, out _); sum e.Length */ }
  }
  ```

  This is driverless, the `ResultPolicyBenchmarks` shape.
- `docs/benchmarks.md`:
  - Add a baseline row `DeviceFaultPoll.ReadKhrEntries_NoFault_1024` with
    `Allocated` `-`.
  - The note says: #244 canary. The no-fault per-frame poll is one count call
    answering `VK_TIMEOUT` and must allocate nothing. It is driverless; the
    `Device` layer is covered on KHR hardware by
    `DeviceFaultTests.Poll_NoFault_IsZeroAllocation` (and the contended-lock
    test), whose exact-zero assertion is the stronger check (spec A.6).
  - Add `*DeviceFaultPoll*` to the driverless filter line (`:23`).
- **Checkpoint A.**
  - `dotnet build Ahjo.Vulkan.slnx` is clean.
  - `dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~DeviceFault"`
    is green.
  - `dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter "*DeviceFaultPoll*"`
    shows `Allocated` `-`. Record the Mean.

---

## Part B: #245, shader-abort messages

### Step B1: Names

- `src/Ahjo.Vulkan/Internal/DeviceExtensionNames.cs`, after `GetDeviceFaultDebugInfo`:

  ```csharp
  public static ReadOnlySpan<byte> KhrShaderAbort        => "VK_KHR_shader_abort"u8;
  public static ReadOnlySpan<byte> KhrShaderConstantData => "VK_KHR_shader_constant_data"u8;
  ```

  `<remarks>`: neither gates an entry point. `KhrShaderAbort` gates the chaining
  of `VkDeviceFaultShaderAbortMessageInfoKHR` on the KHR debug-info read, and
  `KhrShaderConstantData` is its device-creation dependency.
- `src/Ahjo.Vulkan/Rendering/VulkanExtensions.cs`, after `KhrDeviceFault`:
  - `public static Utf8Name KhrShaderAbort => Utf8Name.FromLiteral(DeviceExtensionNames.KhrShaderAbort);`
    - Summary: "VK_KHR_shader_abort — device-level. With `KhrDeviceFault`,
      `Device.TryGetDeviceFault` returns the messages of shaders that executed
      `OpAbortKHR` in `DeviceFaultReport.ShaderAbortMessages`."
    - Remarks, the recipe (spec B.1):
      1. List `KhrDeviceFault`, `KhrShaderAbort` and `KhrShaderConstantData`.
         The last is a dependency; the layer reports
         `VUID-vkCreateDevice-ppEnabledExtensionNames-01387` without it.
      2. Push `VkPhysicalDeviceFaultFeaturesKHR` (`deviceFault = 1`) and
         `VkPhysicalDeviceShaderAbortFeaturesKHR` (`shaderAbort = 1`), after
         checking `TryGetFeatures<VkPhysicalDeviceShaderAbortFeaturesKHR>(KhrShaderAbort, out …)`.
      3. Slang's `abort(format, args…)` emits `OpAbortKHR` (v2026.19).
      4. The wrapper cannot see the enabled feature, only the extension.
  - `public static Utf8Name KhrShaderConstantData => Utf8Name.FromLiteral(DeviceExtensionNames.KhrShaderConstantData);`
    - Summary: "VK_KHR_shader_constant_data — device-level. Required by
      `KhrShaderAbort` as a device-creation dependency."
    - Remarks: gates nothing (the `KhrDeferredHostOperations` wording).
      `shaderConstantData` is needed only by shaders that declare
      `ConstantDataKHR`; Slang's `abort` does not.

### Step B2: `src/Ahjo.Vulkan/Lifecycle/Device.cs`, capture

- Add `internal readonly bool ShaderAbortExtensionEnabled;` next to
  `MemoryBudgetExtensionEnabled` (`:39`), with a one-line comment in that
  field's voice.
- In the constructor, after `:75-76`:
  `ShaderAbortExtensionEnabled = PhysicalDevice.ContainsExtension(enabledExtensions, DeviceExtensionNames.KhrShaderAbort);`
- `FaultEntryPoints` (`:287-288`) passes a fourth argument,
  `shaderAbortMessages: ShaderAbortExtensionEnabled`.

### Step B3: `src/Ahjo.Vulkan/Internal/DeviceFaultReader.cs`

**B3a. `DeviceFaultEntryPoints`.**

- Add `public readonly bool ShaderAbortMessages;`.
- Add the constructor parameter `bool shaderAbortMessages = false`, assigned as
  `ShaderAbortMessages = shaderAbortMessages && khrDebugInfo != null;`.
- `Api` is unchanged.

**B3b. Constants.**

```csharp
internal const uint MaxShaderAbortMessageBytes = 16u * 1024 * 1024; // spec B.2 step 3
internal const int  MaxShaderAbortMessages     = 1024;
```

Each gets a `<summary>` in the voice of `MaxVendorBinaryBytes`. The first notes
the 64 KiB per-message floor of `maxShaderAbortMessageSize`.

**B3c. `ReadKhrDebugInfo`, new signature:**

```csharp
internal static VkResult ReadKhrDebugInfo(
    delegate* unmanaged[Stdcall]<VkDevice_T*, VkDeviceFaultDebugInfoKHR*, VkResult> fn,
    VkDevice_T* device,
    bool        readShaderAbortMessages,
    out byte[]   binary,
    out byte[][] shaderAbortMessages,
    out bool     incomplete);
```

The body follows spec B.2 exactly:

1. Set `binary = []`, `shaderAbortMessages = []` and `incomplete = false`.
2. Declare a stack local `VkDeviceFaultShaderAbortMessageInfoKHR abort` with
   explicit `sType = VK_STRUCTURE_TYPE_DEVICE_FAULT_SHADER_ABORT_MESSAGE_INFO_KHR`,
   `pNext = null`, `messageDataSize = 0` and `pMessageData = null`. Set
   `info.pNext = readShaderAbortMessages ? &abort : null`.
3. **Query.** A negative result returns it. If both
   `info.vendorBinarySize == 0` and (when chained) `abort.messageDataSize == 0`,
   return `VK_SUCCESS` with no fill.
4. **Clamp** both sizes and write them back. Set
   `capped = vendor > MaxVendorBinaryBytes || message > MaxShaderAbortMessageBytes`.
5. **Allocate.** `byte[] vendorBuf = new byte[vendorSize]` and
   `ulong[] messageBuf = new ulong[(messageSize + 7) / 8]`. Add a comment that
   `ulong[]` is for 8-byte alignment of the (size, payload) pairs.
6. **Fill.** Use `fixed` over both arrays. An empty array yields null, so the
   matching size stays 0. Set `info.pVendorBinaryData`, `abort.pMessageData` and
   `abort.messageDataSize = messageSize`, then make one call `fn(device, &info)`.
   A negative result returns it with everything empty.
7. **Trim the binary** exactly as now.
8. **Trim the messages.**
   `int written = (int)Math.Min(abort.messageDataSize, messageSize);`
   `ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(messageBuf.AsSpan())[..written];`
   `shaderAbortMessages = ParseShaderAbortMessages(bytes, out bool truncated);`
9. Set `incomplete = r == VK_INCOMPLETE || capped || truncated`.

When `readShaderAbortMessages` is false, the behavior must be byte-identical to
today. That is: `pNext` is null, the size-0 shortcut is on the vendor size
alone, and there is one fill call. The existing 5 debug-info tests pin this.

**B3d. The parser:**

```csharp
internal static byte[][] ParseShaderAbortMessages(ReadOnlySpan<byte> data, out bool truncated)
```

- Start at `offset = 0`. While `data.Length - offset >= 8`:
  1. Read `ulong size = MemoryMarshal.Read<ulong>(data[offset..])`. This is
     native endianness, which the spec's "64-bit integer" means; every
     supported RID is little-endian.
  2. Advance `offset += 8`.
  3. If `size > (ulong)(data.Length - offset)`, set `truncated = true` and break.
  4. If the count has reached `MaxShaderAbortMessages`, set `truncated = true`
     and break.
  5. Append `data.Slice(offset, (int)size).ToArray()`.
  6. Set `offset = AlignUp8(offset + (int)size)`. If that exceeds
     `data.Length`, break.
- Fewer than 8 trailing bytes are padding and are ignored.
- Return `[]` (the shared empty array) when no message was parsed.
- Allocate the list lazily.
- Add a comment citing `debugging.adoc` "Shader Abort Messages", and that the
  proposal's sample reader advances 4 bytes and is wrong (spec B3).

**B3e. `TryRead`, KHR arm.**

- Call `ReadKhrDebugInfo(eps.KhrDebugInfo, device, eps.ShaderAbortMessages, out byte[] binary, out byte[][] abortMessages, out bool binaryIncomplete)`.
- Set `ShaderAbortMessages = abortMessages` on the report.
- Change the debug-info failure warning to:
  `$"Device.TryGetDeviceFault: vkGetDeviceFaultDebugInfoKHR returned {d}; the report carries no vendor binary and no shader abort messages."`
- Update the class `<remarks>` with one sentence on the chained abort struct.

### Step B4: `src/Ahjo.Vulkan/Lifecycle/DeviceFaultReport.cs`

- Add `public byte[][] ShaderAbortMessages { get; init; } = [];`.
- `<summary>`: "Payloads of `OpAbortKHR` instructions, in driver order; the
  first is the first abort executed. Never null; empty on the EXT path, when
  `VK_KHR_shader_abort` was not enabled, or when no shader aborted."
- `<remarks>`:
  - Each payload is the shader's message type laid out verbatim. Vulkan
    performs no formatting, so the shading language defines the layout.
  - Slang v2026.19's `abort(format, args…)` was observed to emit a
    NUL-terminated UTF-8 format string padded to a 4-byte boundary, followed by
    each scalar argument in scalar layout. This is an observation, not a
    contract.
  - The wrapper parses the spec-defined (size, payload) framing only.
- Extend `IsIncomplete`'s summary with "a shader-abort message buffer that was
  capped, truncated, or malformed".

### Step B5: Tests, driverless (`tests/Ahjo.Vulkan.Tests/DeviceFaultReaderTests.cs`, extend)

**Fake extension.** Extend `FakeDebugInfo` without changing its existing
behavior:

- New scenario statics:
  - `s_abortData` (`byte[]`, the full message buffer to report);
  - `s_abortReportedSizeOnFill` (`ulong?`). `null` leaves `messageDataSize`
    untouched on the fill, which models B4.
- New recorders:
  - `s_abortChained` per call (whether `info->pNext` is non-null);
  - the chained struct's sType and pNext;
  - `messageDataSize` as passed on the query and on the fill;
  - `s_abortPtrAligned8` on the fill (`((nint)p & 7) == 0`).
- Query: write `messageDataSize = (ulong)s_abortData.Length` when chained and
  `pMessageData == null`.
- Fill: copy `min(passed, data)` bytes. A conforming fake returns
  `VK_INCOMPLETE` when passed less.
- Never write more than passed (#242 fake rule).
- Add a helper `static byte[] Frame(params byte[][] payloads)` that builds the
  (u64 size, payload, pad to 8) framing.

**Mechanical.** The 5 existing `ReadKhrDebugInfo` call sites (`:884`, `:905`,
`:921`, `:935`, `:950`) pass `readShaderAbortMessages: false, out byte[] binary, out _, …`.
`TryRead_KhrDebugInfoError_KeepsEntries_EmptyBinary_Warning` (`:1138`) asserts
the new message text.

**Parser cases** (`ParseShaderAbortMessages`, pure, 8):

1. `Parse_Empty_ReturnsSharedEmpty`.
2. `Parse_OneMessage`: the payload `"bad value: %u\0"` padded to 16, plus a
   4-byte arg (the measured Slang shape).
3. `Parse_TwoMessages_SecondAtNext8AlignedOffset`: the first payload is 13 bytes.
4. `Parse_SizeOverrunsBuffer_KeepsPrefix_Truncated`.
5. `Parse_TrailingFewerThan8Bytes_Ignored_NotTruncated`.
6. `Parse_ZeroLengthPayload_Kept`.
7. `Parse_CountCap_StopsAndTruncates`. Use `MaxShaderAbortMessages + 1`
   zero-length messages, which is 8 bytes each.
8. `Parse_FourByteAdvanceBug_NotReproduced`. A buffer that the proposal's
   4-byte-advance reader would misparse parses correctly.

**`ReadKhrDebugInfo` + abort cases** (10):

9. `KhrDebug_AbortOff_NoChain`. `pNext` is null on every call, the behavior is
   unchanged, and `shaderAbortMessages` is empty.
10. `KhrDebug_AbortOn_ChainsStruct_STypeAndNullPNext_QueryPassesZeroAndNull`.
11. `KhrDebug_AbortOn_MessageOnly_StillFills_VendorNull`. The vendor size is 0
    and there are 2 framed messages. The fill call is made, the vendor pointer
    is null, and both messages parse.
12. `KhrDebug_AbortOn_BinaryAndMessages_OneFill`. There are exactly 2 calls.
13. `KhrDebug_AbortOn_BothZero_NoFill`.
14. `KhrDebug_AbortOn_MessagePointerIs8Aligned`.
15. `KhrDebug_AbortOn_SizeAboveCap_PassedAsCap_Incomplete`. The reported size
    is `ulong.MaxValue`. The fill is passed `MaxShaderAbortMessageBytes`, and
    the result is incomplete. This allocates the 16 MiB cap once.
16. `KhrDebug_AbortOn_DriverLeavesSizeUntouched_TrimsToAllocated`. Set
    `s_abortReportedSizeOnFill = null`.
17. `KhrDebug_AbortOn_MalformedFraming_KeepsPrefix_Incomplete`.
18. `KhrDebug_AbortOn_Error_EmptyBinaryAndMessages`.

**`TryRead` composition** (2):

19. `TryRead_Khr_AbortEnabled_SurfacesMessages`. The entry points are
    `new(null, &FakeReports, &FakeDebugInfo, shaderAbortMessages: true)`.
20. `TryRead_Khr_EntryPointsFlagFalse_WhenDebugInfoNull`. A unit test of the
    B3a guard: `new(null, &FakeReports, null, true).ShaderAbortMessages` is
    `false`.

### Step B6: Tests, device-level (`tests/Ahjo.Vulkan.Tests/DeviceFaultTests.cs`, extend)

- Add the helper `TryCreateShaderAbortDevice(Instance, out uint family)`,
  modelled on `TryCreateFaultDevice` (`:477-545`).
  - The picker requires `SupportsExtension` for `KhrDeviceFault`,
    `KhrShaderAbort` and `KhrShaderConstantData`.
  - The extensions are those three.
  - The configurer pushes `VkPhysicalDeviceFaultFeaturesKHR { deviceFault = 1 }`
    and `VkPhysicalDeviceShaderAbortFeaturesKHR { shaderAbort = 1 }`.
  - `EXTENSION_NOT_PRESENT` / `FEATURE_NOT_PRESENT` return `null`.
  - Skip reason constant: `"No GPU exposes VK_KHR_shader_abort with the shaderAbort feature."`.
- Cases:
  1. `ShaderAbort_Enabled_CapturedOnDevice` (`[gate:feature]`). Assert
     `device.ShaderAbortExtensionEnabled` and
     `device.FaultEntryPoints.ShaderAbortMessages`. Change the private
     `FaultEntryPoints` property (`Device.cs:287`) to `internal`; do not add a
     new seam. Also
     assert that a plain KHR device (`TryCreateFaultDevice(khr: true)`) reports
     `false` for both.
  2. `ShaderAbort_CreatesCleanly_UnderValidation` (`[gate:validation]` at the
     `MinKhrLayer` floor, plus `[gate:feature]`). Create, dispose and
     `AssertNoValidationErrors`.
- **Forbidden, restated in the class remarks:** no test calls
  `ReadKhrDebugInfo` with abort chaining on a healthy device. The rule is the
  same VUID-12383 as before.

### Step B7: **Checkpoint B**

- `dotnet build Ahjo.Vulkan.slnx` is clean.
- `dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~DeviceFault"`
  is green.
- `ResultPolicyGuardTests` stays green.

---

## Part C: Slang abort-payload decoder (`Ahjo.Vulkan.Slang`)

This part follows spec Part C. It touches only `src/Ahjo.Vulkan.Slang/` and
`tests/Ahjo.Vulkan.Slang.Tests/`. It does not depend on any Part A or Part B
type: the decoder takes a `ReadOnlySpan<byte>`. The rules in
`src/Ahjo.Vulkan.Slang/CLAUDE.md` apply:

- no benchmark and no `docs/benchmarks.md` row;
- no edit to `src/Ahjo.Vulkan/`;
- allocation is fine.

The never-throw posture comes from #242 (spec C.3). It does **not** relax the
package's "compiler diagnostics are exceptions" rule, because nothing here calls
Slang.

### Step C1: Public types (one file per type, `src/Ahjo.Vulkan.Slang/`)

- **`SlangAbortArgumentKind.cs`.**
  `public enum SlangAbortArgumentKind { Int8, UInt8, Int16, UInt16, Int32, UInt32, Int64, UInt64, Float16, Float32, Float64 }`.
  The doc gives each member's byte size and its `printf` spelling, as in the
  spec C.2 table.
- **`SlangAbortArgument.cs`.**
  `public readonly record struct SlangAbortArgument(SlangAbortArgumentKind Kind, int Offset, ulong Bits)`.
  - `Bits` holds the little-endian value zero-extended into a `ulong`.
  - `Offset` is the byte offset in the payload.
  - Members:
    - `public long AsInt64()`: sign-extends the `Int*` kinds, reinterprets the
      `UInt*` kinds unchecked, and truncates the float kinds toward zero via
      `AsDouble`, saturating.
    - `public ulong AsUInt64()`: returns `Bits` for the integer kinds and
      `unchecked((ulong)AsInt64())` for floats.
    - `public double AsDouble()`: `Float16` through
      `(double)BitConverter.UInt16BitsToHalf((ushort)Bits)`, `Float32` through
      `BitConverter.Int32BitsToSingle`, `Float64` through
      `BitConverter.Int64BitsToDouble`; the integer kinds convert their value.
  - None of the three throws.
- **`SlangAbortMessage.cs`.** `public sealed class SlangAbortMessage` exactly as
  in spec C.1:
  - `Format`, `Arguments` and `Text` are get-only;
  - the constructor is private;
  - `ToString()` returns `Text`;
  - `TryDecode(ReadOnlySpan<byte> payload, [NotNullWhen(true)] out SlangAbortMessage? message)`;
  - `Describe(ReadOnlySpan<byte> payload)`.
- XML docs on `SlangAbortMessage`:
  - **Summary.** Decodes one `DeviceFaultReport.ShaderAbortMessages` payload
    written by a Slang `abort(format, args…)`.
  - **Remarks:**
    - The layout is behavior of the pinned Slang version, not a contract (spec C-E7). It is
      pinned by `SlangAbortLayoutTests`.
    - The specifier table, with the two deliberate departures from C: `%hf` is
      half and `%lf` is double.
    - Argument widths come from the format, as in C. Slang does not check the
      format against the arguments (C-E4), so `Arguments` keep raw bits.
    - Never throws. `Describe` is the crash-handler entry point.
    - Shaders need `VK_KHR_shader_abort` + `shaderAbort` on the device, and
      `Capabilities = [Utf8Name.FromLiteral("spvAbort"u8)]` on the session for
      a warning-free compile (C-E6).

### Step C2: Internals (`src/Ahjo.Vulkan.Slang/Internal/`)

- **`SlangAbortSpecifier.cs`.**
  `internal readonly record struct SlangAbortSpecifier(int Start, int Length, char Conversion, SlangAbortArgumentKind Kind, int Components, SlangAbortFlags Flags, int Width, int Precision)`.
  - `Width` and `Precision` are `-1` when absent.
  - `Start` and `Length` index into the format `string`.
  - `[Flags] internal enum SlangAbortFlags { None = 0, Left = 1, Plus = 2, Space = 4, Zero = 8, Alt = 16 }`
    goes in the same file.
- **`SlangAbortFormat.cs`.** `internal static class SlangAbortFormat`, with:
  - `internal static bool TryParse(string format, List<SlangAbortSpecifier> specifiers, [NotNullWhen(false)] out string? reason)`.
    It implements the spec C.2 step 2 grammar and table. The parser
    **consumes** `%%` (both characters) and emits no specifier, so `%%d` is a
    literal `%` followed by `d`. The renderer substitutes `%`. On failure it returns one of
    these **exact** reason strings, where `{i}` is the index of the `%` in
    `format`:
    - `$"unsupported conversion '{c}' at format index {i}"`
    - `$"unsupported conversion '{c}' with length '{len}' at format index {i}"`
      (for example `hhf`)
    - `$"unsupported length modifier '{m}' at format index {i}"` (`L z j t`)
    - `$"'*' width or precision at format index {i}"`
    - `$"invalid vector size at format index {i}"` (`v` not followed by 2, 3 or 4)
    - `"incomplete specifier at end of format"`
  - `internal static bool TryComputeLayout(ReadOnlySpan<byte> payload, out string? format, List<SlangAbortSpecifier> specifiers, List<SlangAbortArgument> arguments, [NotNullWhen(false)] out string? reason)`.
    This is the C.2 steps 1 and 3 driver, and the C.5 tests call it with
    payload-free inputs too (see C4). On failure, `format` is the decoded format
    when a NUL was found, and `null` otherwise. `Describe` branches on that
    null. Its additional reasons are:
    - `"no NUL terminator"`
    - `"format string ends past the payload"`, only when the format has at
      least one argument specifier
    - `$"argument {n} needs {bytes} bytes at offset {offset}, payload has {length}"`,
      with `n` 1-based, counting vector components individually.

    Every bound is checked **before** slicing. Reading uses
    `BinaryPrimitives.ReadUInt16LittleEndian` / `ReadUInt32LittleEndian` /
    `ReadUInt64LittleEndian`, or a single byte. There is no `try`/`catch`.
- **`SlangAbortRenderer.cs`.** `internal static class SlangAbortRenderer`, with
  `internal static string Render(string format, List<SlangAbortSpecifier> specifiers, List<SlangAbortArgument> arguments)`.
  - It uses a `StringBuilder` and copies literal runs. `%%` becomes `%`.
  - Each specifier is formatted through `TryFormat` and `CultureInfo.InvariantCulture`,
    following the rules in spec C.2 step 4.
  - A vector renders its components joined by `", "`, each with the same flags,
    width and precision.
  - For `g` and `G`, implement C's rule:
    - P = precision, defaulting to 6 and treated as 1 when 0;
    - X = the decimal exponent;
    - if P > X ≥ −4, use `f` with precision P−1−X, otherwise `e` with
      precision P−1;
    - strip trailing zeros, and a then-trailing `.`, unless `#`.
  - For `e` and `E`, the exponent has at least two digits and a sign.
  - NaN and ±Inf render as `nan`, `inf` and `-inf`, uppercase for `F E G`.
    Width and the `-` flag apply; `0` does not.
- **`SlangAbortMessage.TryDecode`.** It calls `TryComputeLayout`; on success it
  renders and constructs.
- **`SlangAbortMessage.Describe`:**
  - on success, `Text`;
  - when a format was found:
    `$"{format} <abort arguments not decoded: {reason}; {payload.Length} payload bytes>"`;
  - when there is no NUL:
    `$"<shader abort payload, {n} bytes, not a Slang abort message{(n > 0 ? ": " + hex : "")}>"`,
    where `hex` is the first 64 bytes as lowercase pairs joined by single
    spaces, with `" …"` appended when `n > 64`.

### Step C3: Decoder tests (`tests/Ahjo.Vulkan.Slang.Tests/SlangAbortMessageTests.cs`, new; driverless; no `TestGate`)

Add a test helper `AbortPayloads.Build(string format, params (SlangAbortArgumentKind Kind, ulong Bits, int Offset)[] args)`
in the same file. It writes the UTF-8 format, a NUL and zero padding to 4, then
each argument's little-endian bytes **at the explicit offset given**. The
offsets are written out by hand in each test, from the spec C-E table, so this
helper never re-derives the layout the decoder computes.

**Successful decodes.** Each asserts `TryDecode == true`, the exact `Text`, and
the `Arguments` kinds and offsets.

1. `"bad value: %u at %u"`, `(UInt32, 42, 20)`, `(UInt32, 0, 24)` → `"bad value: 42 at 0"`.
2. `"no args here"` → `"no args here"`, with no arguments.
3. `""` → `""`.
4. `"signed %d"`, `(Int32, -5 as bits, 12)` → `"signed -5"`.
5. `"float %f"`, `(Float32, bits(1.5f), 12)` → `"float 1.500000"`.
6. `"%08x %.3f %%"`, `(UInt32, 0xBEEF, 16)`, `(Float32, bits(3.14159f), 20)` → `"0000beef 3.142 %"`.
7. `"big %llu"`, `(UInt64, ulong.MaxValue, 16)` → `"big 18446744073709551615"`.
8. `"ab %d %lld"`, `(Int32, -1, 12)`, `(Int64, long.MinValue, 16)` → `"ab -1 -9223372036854775808"`.
9. `"%hhu %hf %u"`, `(UInt8, 200, 12)`, `(Float16, bits((Half)0.5), 14)`, `(UInt32, 7, 16)` → `"200 0.500000 7"`.
10. `"%hd %hd %lf"`, `(Int16, -2, 12)`, `(Int16, 3, 14)`, `(Float64, bits(2.25), 16)` → `"-2 3 2.250000"`.
11. `"%hhu %v3f"`, `(UInt8, 1, 12)`, then three `Float32` at 16 / 20 / 24 → `"1 1.000000, 2.000000, 3.000000"`.
12. `"%v2u"`, `(UInt32, 4, 8)`, `(UInt32, 5, 12)` → `"4, 5"`.
12b. `"%llv2u"`, `(UInt64, 1, 8)`, `(UInt64, 2, 16)` → `"1, 2"`. The layout was
     measured: `v2ulong` at 8.
12c. `"%v2llu"`, same payload → `"1, 2"`. Both orders of the length modifier
     and `vN` are accepted (spec C.2 step 2).
13. `"%#x %#X %+d % d %-4d|"`, five `UInt32`/`Int32` args at 24, 28, 32, 36, 40
    (the format is 21 bytes, so 24 bytes with NUL and padding)
    → `"0xff 0XFF +5  5 7   |"`.
14. `"%e %E %g %G"`, `Float32` values 15, 1e-5, 0.0001 and 1e10 at offsets
    12 / 16 / 20 / 24 (the format is 11 bytes, so 12 with NUL and padding)
    → `"1.500000e+01 1.000000E-05 0.0001 1E+10"`.
15. `"%f %F"`, NaN at offset 8 and +∞ at offset 12 → `"nan INF"`.
16. `"Störung ✓ %u"`, `(UInt32, 1, 16)` → `"Störung ✓ 1"`.
17. `ExtraTrailingBytes_Ignored`: `"%u"`, `(UInt32, 7, 4)`, plus 4 extra bytes →
    `"7"`. This is the too-many-arguments shape (C-E4).
18. `InvalidUtf8InFormat_ReplacedNotThrown`: format bytes `0xFF 'a' NUL` →
    `TryDecode` is `true` and `Format` is `"�a"`.
19. `AsAccessors`: `AsInt64` / `AsUInt64` / `AsDouble` for one argument of each
    kind, including `Int8` −1 sign extension and `Float16`.

**Failures.** Each asserts `TryDecode == false`, `message == null`, and the
exact `Describe` string.

20. No NUL, bytes `01 02 03` → `"<shader abort payload, 3 bytes, not a Slang abort message: 01 02 03>"`.
21. Empty payload → `"<shader abort payload, 0 bytes, not a Slang abort message>"`.
22. A 70-byte non-NUL payload → the hex shows exactly 64 pairs, then `" …"`.
23. `"s %s"` → `"s %s <abort arguments not decoded: unsupported conversion 's' at format index 2; 8 payload bytes>"`.
24. Too few arguments, `"%u %u"` with one `UInt32` at 8 (12-byte payload) →
    the reason is `"argument 2 needs 4 bytes at offset 12, payload has 12"`.
25. `"%"` at the end → `"incomplete specifier at end of format"`.
26. `"%*d"` → the `'*'` reason.
27. `"%v5f"` → the vector-size reason.
28. `"%Lf"` → `"unsupported length modifier 'L' …"`.
29. `"%hhf"` → `"unsupported conversion 'f' with length 'hh' …"`.
30. `"abcde %u"` + NUL in a 9-byte payload. The padded string would end at 12,
    so the result is `"format string ends past the payload"`.
30b. `NoSpecifiers_ShortPadding_StillDecodes`: `"abcde"` + NUL in a 6-byte
     payload (the padding is missing) → `TryDecode` is `true` and `Text` is
     `"abcde"`. The missing padding fails decoding only when there are
     arguments to read.

**Property** (the never-throw guard):

31. `NeverThrows_RandomPayloads`. Use `new Random(244245)`, 10,000 payloads of
    random length 0–96 with random bytes, and also random formats built from
    `%`, conversion letters, digits, `h l v . * #` and padding. Assert neither
    `TryDecode` nor `Describe` throws, and that `Describe` is non-null and
    non-empty.
32. `NeverThrows_EveryTruncation`. Every prefix of each of the cases 1–16
    payloads: no throw, and `Describe` is non-empty.

### Step C4: Layout pin (`tests/Ahjo.Vulkan.Slang.Tests/SpirvDecorations.cs` + `SlangAbortLayoutTests.cs`)

**`SpirvDecorations.ReadAbortMessageLayout`** (new). Opcodes: `OpTypeInt` 21,
`OpTypeFloat` 22, `OpTypeVector` 23, `OpTypeArray` 28, `OpTypeStruct` 30,
`OpConstant` 43, `OpConstantComposite` 44, `OpMemberDecorate` 72 (already
present), `OpAbortKHR` **5121**.

```csharp
public static (uint[] FormatWords, List<(uint Offset, int Width, bool Float, bool Signed, int Components)> Args)
    ReadAbortMessageLayout(ReadOnlySpan<uint> words)
```

- Find the single `OpAbortKHR` and take its operand 0, the message type id.
- Resolve that `OpTypeStruct`. Member 0 must be an `OpTypeArray` of a 32-bit
  `OpTypeInt`.
- `FormatWords` are the `OpConstant` values of the `OpConstantComposite` whose
  result type is that array type.
- For members 1 and up: the `OpMemberDecorate … Offset`, and the scalar or
  vector shape resolved through `OpTypeInt` (width, signedness), `OpTypeFloat`
  (width) and `OpTypeVector` (component, count).
- Throw `InvalidOperationException` with a message naming what is missing. This
  is test code, where throwing is correct.
- Extend the class `<remarks>` "picks out three opcodes" sentence to cover
  these.

**`SlangAbortLayoutTests.cs`** (new; driverless; `ITestOutputHelper`).

- A private `Compile(string abortCall)` builds the source:
  `RWStructuredBuffer<uint> buf; RWStructuredBuffer<float> fbuf; [shader("compute")][numthreads(1,1,1)] void main(uint3 id : SV_DispatchThreadID) { uint v = buf[0]; int s = int(buf[1]); float f = fbuf[0]; if (v == 42) <abortCall>; buf[2] = v + 1; }`.
  - It compiles through `SlangCompiler.Create()` → `CreateSession(new SlangSessionDescription { Capabilities = [Utf8Name.FromLiteral("spvAbort"u8)] })`
    → `Compile(new SlangCompileRequest { ModuleName = "abort", Source = … })`.
    This is the `SlangBufferLayoutTests.Compiled` shape (`SlangBufferLayoutTests.cs:405-430`).
  - It asserts the program reports **no warnings** (C-E6).
  - It returns `Program.Spirv(0)`.
- Put the fixture rows in a `TheoryData<string abortCall, string format>`.
  These are the spec C-E table rows, **with each format spelled to match its
  arguments' widths**. The table's mismatched spellings (`"h %f"` with `half`
  and the like) document C-E3; they are not decoder fixtures. Changing a
  specifier does not change the layout when the string pads to the same word
  count, and every substitution below was checked to do so.

  | # | `abortCall` | format |
  |---|---|---|
  | 1 | `abort("bad value: %u at %u", v, id.x)` | `bad value: %u at %u` |
  | 2 | `abort("signed %d", s)` | `signed %d` |
  | 3 | `abort("float %f", f)` | `float %f` |
  | 4 | `abort("u=%u d=%d f=%f x=%x", v, s, f, v)` | `u=%u d=%d f=%f x=%x` |
  | 5 | `abort("big %llu", uint64_t(v))` | `big %llu` |
  | 6 | `abort("ab %d %lld", s, int64_t(s))` | `ab %d %lld` |
  | 7 | `abort("d %lf", double(f))` | `d %lf` |
  | 8 | `abort("h %hf", half(f))` | `h %hf` |
  | 9 | `abort("s %hd", int16_t(s))` | `s %hd` |
  | 10 | `abort("c %hhu", uint8_t(v))` | `c %hhu` |
  | 11 | `abort("%hhu %hf %u", uint8_t(v), half(f), v)` | `%hhu %hf %u` |
  | 12 | `abort("%hd %hd %lf", int16_t(s), int16_t(s), double(f))` | `%hd %hd %lf` |
  | 13 | `abort("%hhu %llu", uint8_t(v), uint64_t(v))` | `%hhu %llu` |
  | 14 | `abort("%hhu %v3f", uint8_t(v), float3(f, f, f))` | `%hhu %v3f` |
  | 15 | `abort("v2=%u %u", uint2(v, v + 1))` | `v2=%u %u` |
  | 16 | `abort("%v2u", uint2(v, v))` | `%v2u` |
  | 17 | `abort("b %u", v == 3)` | `b %u` (Slang widens `bool` to `uint`) |
  | 18 | `abort("%08x %.3f %%", v, f)` | `%08x %.3f %%` |
  | 19 | `abort("Störung ✓ %u", v)` | `Störung ✓ %u` |
  | 21 | `abort("%llv2u", vector<uint64_t, 2>(uint64_t(v), uint64_t(v)))` | `%llv2u` |
  | 20 | `abort("a fairly long format string that exceeds sixty four bytes for sure %u", v)` | the same string |

  `"no args here"` and `""` go in a separate `[Fact]`, which asserts
  `FormatWords` and an empty argument list.
- For each row, assert:
  1. `FormatWords` as little-endian bytes equal `UTF8(format) + 0 + zero pad to 4`.
  2. `SlangAbortFormat.TryComputeLayout` on a payload built from those bytes
     plus a zero tail sized to the SPIR-V's last member end. That avoids a
     false overrun, and is `AlignUp(lastOffset + lastWidth × components, 8)`.
     The resulting argument list, with SPIR-V vector members expanded to
     components at `offset + i × width`, equals the SPIR-V members' offsets and
     kinds. Map the kinds as (width, float, signed) → `SlangAbortArgumentKind`.
  3. **End to end.** Fill that payload's argument slots at the **SPIR-V**
     offsets with deterministic values: component *k* gets `k + 1`, encoded
     in the SPIR-V member's own type. `TryDecode` it and assert that every
     `Arguments[k].Bits` equals the encoded value. For rows 1, 4 and 18, also
     assert `Text` against the matching Step C3 rendering, filled with the C3
     values.
- Every assertion message starts with
  `$"Slang {SlangPinnedVersion.Tag} changed the abort payload layout ({abortCall}): "`
  and ends with `"Re-probe per spec Part C before bumping SlangVersion."`.
- Two more facts:
  - ~~`ZeroArgumentAbort_DoesNotCompileForSpirv`~~ — **dropped during
    implementation.** `abort()` does not throw: it compiles cleanly through the
    wrapper and then `Spirv(0)` kills the process (0xC0000005), upstream
    shader-slang/slang#13166. The corrected facts are in spec C-E5.
  - `StringArgument_IsRejected`. `abort("s %s", "hello")` throws with text
    containing `E55211`, which is why `%s` is unsupported.

### Step C5: Slang package docs

- `src/Ahjo.Vulkan.Slang/README.md`: add a short section, "Decoding shader-abort
  messages". It covers enabling (the `VulkanExtensions.KhrShaderAbort` recipe and
  the `spvAbort` capability), and a crash-handler snippet:
  `foreach (byte[] p in report.ShaderAbortMessages) log(SlangAbortMessage.Describe(p));`.
  It also notes that the layout is pinned by tests and is not a Slang contract.
- `src/Ahjo.Vulkan.Slang/CLAUDE.md`: add one bullet under *Boundaries*.
  `SlangAbortMessage` is the package's one never-throw API: it decodes GPU crash
  data and never calls Slang, so it follows the #242 crash-path posture rather
  than the diagnostics-as-exceptions rule. `SlangAbortLayoutTests` pins its
  layout to the pinned compiler; on a Slang bump, a failure there means
  re-probing spec Part C, not editing the expected offsets.

### Step C6: **Checkpoint C**

- `dotnet build Ahjo.Vulkan.slnx` is clean.
- `dotnet test tests/Ahjo.Vulkan.Slang.Tests` is green with the new classes, and
  no new skips: everything is driverless.
- `dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~DeviceFault"`
  is still green.

---

## Shared tail

### Step T1: Docs

- `src/Ahjo.Vulkan/README.md:109-113`: add `KhrShaderAbort` and
  `KhrShaderConstantData` to the `VulkanExtensions` list. Add one sentence after
  the list: "`Device.TryPollDeviceFaults` reads masked faults on a healthy
  device (`VK_KHR_device_fault` + `deviceFaultReportMasked`)."
- `docs/benchmarks.md`: the Step A8 row and filter. There is no row for Part C,
  by the Slang package rule.
- `src/Ahjo.Vulkan.Slang/README.md` and `CLAUDE.md`: Step C5.
- `docs/aot-notes.md`: no change.

### Step T2: Verify

- `dotnet build Ahjo.Vulkan.slnx` is clean.
- `dotnet test` (all suites) is green. That includes `Ahjo.Vulkan.Slang.Tests`,
  where the Part C layout pins run on every host, CI included, because they are
  driverless.
- `AHJO_VULKAN_TIER=validation dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~DeviceFault|FullyQualifiedName~ResultPolicyGuard|FullyQualifiedName~ShadowEnumDrift"`
  on the RTX 4070 Ti. Quote:
  - the contract test's `declared=… observed=…` line;
  - the layer version, which must be at least 1.4.363;
  - the pass/skip counts of `DeviceFaultTests`;
  - the outputs of A7 case 4 and B6 case 1.
- If the AMD iGPU is reachable, the shader-abort and KHR cases must skip
  `[gate:feature]` there.
- AOT smoke publish
  (`dotnet publish samples/AotSmoke/AotSmoke.csproj -c Release -r win-x64 -p:IlcUseEnvironmentalTools=true`)
  shows no new trim warning.
- Reviewers:
  - **`vulkan-validation-reviewer`:**
    - the chained-struct gating (extension, not feature);
    - sType/pNext on the abort struct;
    - the pointer/size pairing for both debug-info pairs;
    - `Dispose` versus an in-flight poll;
    - no debug-info call on a healthy device.
  - **`bench-coverage-checker`:**
    - the no-fault poll is the only per-frame-callable path;
    - it is covered by `DeviceFaultPoll.ReadKhrEntries_NoFault_1024` (driverless)
      and `Poll_NoFault_IsZeroAllocation` (device);
    - the crash read still allocates by design;
    - Part C is in `Ahjo.Vulkan.Slang`, which takes no benchmarks by rule.

### Step T3: PR

- Title: `Device: KHR fault polling + VK_KHR_shader_abort messages`. Body:
  `Closes #244`, `Closes #245`. The body also lists the new Slang decoder as a
  third, related change.
- **Name the behavior changes for Ahjo:**
  - On KHR, a repeat `TryGetDeviceFault` now returns the same `Entries` (the
    fault log) instead of none. Reports can include masked entries polled
    earlier; these have no `DeviceLost` flag.
  - `Dispose` now waits for an in-flight fault poll.
  - There is a new `DeviceFaultReport.ShaderAbortMessages`. It holds raw
    payloads, not strings, and the PR says why.
  - There is a new `Ahjo.Vulkan.Slang.SlangAbortMessage.Describe(payload)` that
    turns a Slang payload into text, plus `TryDecode` for structured access.
    Name the supported specifiers and the `%hf` / `%lf` departures from C.
- Quote the research record from the spec: S2 (vulkaninfo), S3 (layer), B2
  and the C-E table (Slang). Quote the Step T2 runs.
- **State the coverage limit.** The real device-loss test was dropped by the
  maintainer, so the post-loss read, abort chaining and the masked-poll drain
  are tested against fakes only, and the decoder against Slang's SPIR-V rather
  than driver-written bytes (spec *Test strategy*).
- Releasing is a separate step after merge.

## Open items

None. OPEN-1 (the device-loss test) was dropped, OPEN-2 (the decoder) became
Part C, and OPEN-A (the lock seam) is approved. If something in Part C's
grammar or rendering turns out to be ambiguous during implementation, the
spec C.2 table and the Step C3 vectors decide. If those do not, stop and ask.
