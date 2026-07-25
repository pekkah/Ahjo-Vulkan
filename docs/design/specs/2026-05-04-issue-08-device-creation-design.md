# Issue #8 — Device creation API + queue access — Design

Status: design (pending plan)
Date: 2026-05-04
Issue: https://github.com/pekkah/ahjo-vulkan/issues/8
Depends on: #6 (Instance), #7 (PhysicalDevice + ChainBuilder), #4 (`ChainBuilder<TRoot>`), #5 (`VkResult.ThrowIfFailed()`).

## 1. Goal

`physicalDevice.CreateDevice(in DeviceDescription)` returns a fully-initialised `Device` — `VkDevice` plus its loader-resolved per-device function table plus a cached `Queue` for every requested `(family, index)`. Defaults are picked so a typical 1.4 graphics caller can write `physicalDevice.CreateDevice(new DeviceDescription { Queues = [new QueueRequest(gfxFamily, count: 1, priority: 1.0f)] })` and get a usable device — the wrapper turns on the 1.4 promotional features the rest of the wrapper depends on (synchronization2, dynamicRendering, timelineSemaphore, bufferDeviceAddress, pushDescriptor) automatically.

Allocator and surface support are explicitly **not** in this issue (#9 owns Allocator; surfaces land with the swapchain issue).

This issue also folds in a **PhysicalDevice shape refactor** (a small clean-up of the surface that landed in #7). `PhysicalDevice` becomes a `sealed unsafe class` and drops `IVulkanHandle<PhysicalDevice>`. Reasoning: a pattern is emerging across the wrapper —
- **Owners** (created once, lifetime-managed): `sealed class`. Examples: `Instance`, `Device`, `Queue`, and now `PhysicalDevice`.
- **Resource handles** (created/destroyed per-resource, generic dispatch wanted): `readonly struct` + `IVulkanHandle<TSelf>`. Examples (forthcoming): `Buffer`, `Image`, `BufferView`, semaphores, fences.

`IVulkanHandle<TSelf>` exists to enable generic debug-naming and pool-key dispatch — useful for resources you create thousands of, useless for the 1–3 physical devices on a host. The interface is dropped from `PhysicalDevice` along with the `FromRaw` static abstract; `RawHandle`, `IsNull`, `ObjectType` become plain instance members. Reference equality (the new identity contract) is preserved by caching `PhysicalDevice` instances on the owning `Instance` — see §2 (`Instance.GetPhysicalDevice`) and §3 (`PickPhysicalDevice` cache lookup). The cache keeps the #7 zero-allocation acceptance benchmark green.

## 2. Public surface

### `PhysicalDevice` — `sealed unsafe class` (refactored from `readonly struct`)

```csharp
public sealed unsafe class PhysicalDevice
{
    internal readonly VkPhysicalDevice_T* Handle;
    internal readonly Instance            Instance;          // owner back-ref

    internal PhysicalDevice(Instance instance, VkPhysicalDevice_T* handle);

    public ulong RawHandle => (ulong)(nint)Handle;
    public bool  IsNull    => Handle == null;
    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_PHYSICAL_DEVICE;

    public Device CreateDevice(in DeviceDescription description);   // §3
}
```

- No `IVulkanHandle<TSelf>`, no `FromRaw`. Construction is `internal` — instances are produced exclusively by `Instance.PickPhysicalDevice` / `Instance.GetPhysicalDevice` (see below) so the caching invariant holds.
- `Instance` back-reference replaces the previous "no owner" model — `CreateDevice` and any future operations that need the instance handle (e.g. surface-creation extension functions) reach for it directly.
- Reference equality. Two `PickPhysicalDevice` calls on the same instance that hit the same underlying `VkPhysicalDevice_T*` return the same managed instance.

### `Instance` additions (PhysicalDevice cache)

```csharp
public sealed unsafe class Instance : IDisposable
{
    // … existing members …

    private PhysicalDevice[]? _physicalDeviceCache;   // populated lazily on first PickPhysicalDevice call

    /// <summary>
    /// Returns the wrapped <see cref="PhysicalDevice"/> for a raw native
    /// handle, materialising and caching one if it has not been seen before.
    /// Internal: callers go through <see cref="PickPhysicalDevice"/>; the
    /// helper exists so the picker plumbing returns the same managed
    /// instance across calls (reference-equality identity).
    /// </summary>
    internal PhysicalDevice GetOrCreatePhysicalDevice(VkPhysicalDevice_T* handle);
}
```

The cache is a small `PhysicalDevice[]` (typical N is 1–3, hard-spec ceiling is whatever the host has). Linear scan on lookup — free at this scale, and trivially convertible to a dictionary if the wrapper ever supports a host with dozens of GPUs. The array is null until `PickPhysicalDevice` runs once; on first call we populate it from `vkEnumeratePhysicalDevices`.

### `Device` — `sealed unsafe class : IDisposable`

Mirrors `Instance` exactly: created once, holds a per-device function-table cache, finalizer backstop, deterministic `Dispose` calls `vkDeviceWaitIdle` then `vkDestroyDevice`. Reasons for the class shape are the same as `Instance`'s (issue #6 spec §2.1):

```csharp
public sealed unsafe class Device : IDisposable
{
    internal readonly VkDevice_T*         Handle;
    internal readonly DeviceFunctionTable Functions;       // per-device entry-point cache
    public   readonly PhysicalDevice      PhysicalDevice;  // owner back-ref (now a class — see §2)
    private  readonly Queue[]             _queues;         // pre-baked at Create time, see §3
    private  bool                         _disposed;

    public ulong RawHandle => (ulong)(nint)Handle;
    public bool  IsNull    => Handle == null;
    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_DEVICE;

    public Queue GetQueue(uint familyIndex, uint queueIndex);
    public void  WaitIdle();   // wraps vkDeviceWaitIdle
    public void  Dispose();
}
```

`PhysicalDevice` is held as a public reference so `Device.Allocator` (issue #9) can fetch the physical device handle without a back-reference parameter, and so debug-time queue-validation can re-query queue families if it needs to. (Public not internal — it's load-bearing for callers who want to inspect the GPU after creation.)

### `Queue` — `sealed unsafe class`

Deviation from the issue body (which proposes `readonly struct`). The brainstorm answer was: "struct doesn't provide any value here." Concrete reasoning:

- A queue lives exactly as long as its device (one-shot heap allocation per requested queue, typically 1–4 per app — measurement noise next to the device itself).
- The class shape lets `Submit` / `WaitIdle` / debug-name calls hang naturally on the type without static-helper indirection later.
- A `sealed class` cannot implement `IVulkanHandle<TSelf>` (the interface constrains `TSelf : unmanaged`), so the handle members live as instance properties — same shape as `Instance`.

```csharp
public sealed unsafe class Queue
{
    internal readonly VkQueue_T* Handle;
    public   readonly uint       FamilyIndex;
    public   readonly uint       QueueIndex;     // index within the family
    public   readonly Device     Device;          // owner back-ref

    public ulong RawHandle => (ulong)(nint)Handle;
    public bool  IsNull    => Handle == null;
    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_QUEUE;

    internal Queue(Device device, VkQueue_T* handle, uint familyIndex, uint queueIndex);
}
```

`internal` ctor — `Queue` instances are produced exclusively by `Device.Create` and stored in `Device._queues`. Outside-the-wrapper construction is meaningless (`vkGetDeviceQueue` requires a device).

### `DeviceDescription` — `ref struct`

```csharp
public ref struct DeviceDescription
{
    /// <summary>Queues to create with the device. Must contain at least one entry.</summary>
    public ReadOnlySpan<QueueRequest> Queues;

    /// <summary>UTF-8 device-extension names to enable. Empty by default.</summary>
    public ReadOnlySpan<Utf8Name> Extensions;

    /// <summary>
    /// Optional callback invoked exactly once during <see cref="PhysicalDevice.CreateDevice"/>
    /// after the wrapper has pushed its 1.4 default feature struct onto the chain.
    /// Push additional <see cref="IChainable{VkDeviceCreateInfo}"/> structs here
    /// (e.g. <c>VkPhysicalDeviceMeshShaderFeaturesEXT</c>). Default = null = no extra structs.
    /// </summary>
    public DeviceFeatureChainConfigurer? ConfigureFeatures;
}

public unsafe delegate void DeviceFeatureChainConfigurer(ref ChainBuilder<VkDeviceCreateInfo> chain);
```

`ref struct` because of the spans. Field defaults (`= default`) are legal: an empty `Queues` span fails validation (§4), `Extensions` empty is fine, `ConfigureFeatures` null is fine.

### `QueueRequest` — `readonly record struct`

```csharp
public readonly record struct QueueRequest
{
    public uint  FamilyIndex { get; }
    public uint  Count       { get; }
    public float Priority    { get; }     // single priority broadcast to Count queues

    public QueueRequest(uint familyIndex, uint count, float priority)
    {
        if (count == 0)
            throw new ArgumentException("QueueRequest.Count must be > 0.", nameof(count));
        if (priority < 0f || priority > 1f || float.IsNaN(priority))
            throw new ArgumentException(
                "QueueRequest.Priority must be in [0, 1] (Vulkan spec VUID-VkDeviceQueueCreateInfo-pQueuePriorities-00383).",
                nameof(priority));

        FamilyIndex = familyIndex;
        Count       = count;
        Priority    = priority;
    }
}
```

Validating ctor satisfies Q6 ("API guides to right usage"): an out-of-range priority is rejected at construction, well before `vkCreateDevice` would have triggered driver UB. Cost is two compares + one IsNaN — negligible compared to allocator and driver call costs.

The single-priority shape is the 80% case. Per-queue distinct priorities (rare — the same queue family with two queues each at a different priority) are deferred to a future `QueueRequest(uint, ReadOnlySpan<float>)` overload; non-breaking addition.

### `KhronosExtensionNames`

Issue #7 already populated this with `KhrSwapchain`. No additions for #8 — extension names a user enables come from outside the wrapper. The `Extensions` span on `DeviceDescription` accepts arbitrary `Utf8Name`s.

### `PhysicalDevice.CreateDevice`

`CreateDevice` and its `ValidateQueues` helper live directly in `PhysicalDevice.cs` — already shown in the surface block above. Implementation lives there too (no partial split, consistent with `Instance.cs`).

```csharp
/// <summary>
/// Creates a Vulkan device with the wrapper's 1.4 default feature set
/// (synchronization2, dynamicRendering, timelineSemaphore, bufferDeviceAddress,
/// pushDescriptor) plus any additional features the caller pushes via
/// <see cref="DeviceDescription.ConfigureFeatures"/>. Validates queue requests
/// against this physical device's queue families before the native call.
/// </summary>
/// <exception cref="ArgumentException">Queue request references a non-existent
/// family, or requests more queues than the family supports.</exception>
/// <exception cref="VulkanException">vkCreateDevice failed.</exception>
public Device CreateDevice(in DeviceDescription description);
```

## 3. Internal flow

### `Instance.PickPhysicalDevice` — cache integration (#7 edit)

The existing #7 picker constructs `new PhysicalDevice(d)` twice per candidate (once for the picker view, once for the return value). With `PhysicalDevice` now a class, those two `new`s allocate. Replace with a cache lookup:

```csharp
// inside the per-candidate loop, replacing `new PhysicalDevice(d)`:
PhysicalDevice gpu = GetOrCreatePhysicalDevice(d);

var info = new PhysicalDeviceInfo(
    device:        gpu,        // class reference, not by-value
    properties:    in props2.properties,
    /* … rest unchanged … */);

if (picker(in info))
    return gpu;
```

`GetOrCreatePhysicalDevice` populates `_physicalDeviceCache` lazily — first call enumerates all physical devices and creates one `PhysicalDevice` instance per handle. Subsequent calls do an array scan. The `[MemoryDiagnoser]` benchmark from #7 stays at `Allocated == 0 B` after warmup because the cache is populated during `[GlobalSetup]`'s first call.

`PhysicalDeviceInfo.Device` field becomes a `PhysicalDevice` reference (was a struct field). The `ref struct` shape is preserved — reference fields are legal in `ref struct`.

### `PhysicalDevice.CreateDevice`

One method body, layout mirrors `Instance.PickPhysicalDevice` — stack-only chain buffer, validation up front, single-shot native call, immediate `Queue` materialisation.

```csharp
public unsafe Device CreateDevice(in DeviceDescription desc)
{
    // 1. Validate request shape against this physical device.
    ValidateQueues(desc.Queues);   // throws on bad family / over-subscription

    // 2. Build the queue create-info array on the stack.
    //    Per-queue priority pointers must remain live until vkCreateDevice returns —
    //    we lay them out in a stackalloc'd float array indexed by request.
    int totalQueues = 0;
    for (int i = 0; i < desc.Queues.Length; i++) totalQueues += (int)desc.Queues[i].Count;

    Span<float>                    priorities = stackalloc float[totalQueues];
    Span<VkDeviceQueueCreateInfo>  qcis       = stackalloc VkDeviceQueueCreateInfo[desc.Queues.Length];
    int prioCursor = 0;
    for (int i = 0; i < desc.Queues.Length; i++)
    {
        ref readonly QueueRequest req = ref desc.Queues[i];
        for (int p = 0; p < (int)req.Count; p++)
            priorities[prioCursor + p] = req.Priority;

        qcis[i] = new VkDeviceQueueCreateInfo
        {
            sType            = VkStructureType.VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO,
            queueFamilyIndex = req.FamilyIndex,
            queueCount       = req.Count,
            pQueuePriorities = (float*)Unsafe.AsPointer(ref priorities[prioCursor]),
        };
        prioCursor += (int)req.Count;
    }

    // 3. Build the extension-name pointer array on the stack.
    Span<nint> extPtrs = stackalloc nint[desc.Extensions.Length];
    for (int i = 0; i < desc.Extensions.Length; i++) extPtrs[i] = (nint)desc.Extensions[i].Ptr;

    // 4. Build the feature chain. ChainBuilder writes to a stackalloc'd buffer.
    Span<byte> chainBuf = stackalloc byte[2048];          // headroom for default + caller features
    var chain = ChainBuilder.For<VkDeviceCreateInfo>(chainBuf);
    ref VkDeviceCreateInfo dci = ref chain.Root();

    // 4a. Wrapper defaults — VkPhysicalDeviceVulkan1{1,2,3,4}Features structs zeroed by
    //     ChainBuilder, then specific fields flipped on. Caller's ConfigureFeatures
    //     callback may toggle additional fields on these same structs.
    ref var f12 = ref chain.Push<VkPhysicalDeviceVulkan12Features>();
    f12.bufferDeviceAddress = 1;
    ref var f13 = ref chain.Push<VkPhysicalDeviceVulkan13Features>();
    f13.synchronization2  = 1;
    f13.dynamicRendering  = 1;
    // timelineSemaphore is a Vulkan12 feature; bufferDeviceAddress is Vulkan12; pushDescriptor is Vulkan14.
    f12.timelineSemaphore = 1;
    ref var f14 = ref chain.Push<VkPhysicalDeviceVulkan14Features>();
    f14.pushDescriptor = 1;

    // 4b. Caller hook — last so user toggles override defaults if they collide.
    desc.ConfigureFeatures?.Invoke(ref chain);

    // 5. Fill the rest of VkDeviceCreateInfo.
    dci.queueCreateInfoCount    = (uint)desc.Queues.Length;
    dci.pQueueCreateInfos       = (VkDeviceQueueCreateInfo*)Unsafe.AsPointer(ref qcis[0]);
    dci.enabledExtensionCount   = (uint)desc.Extensions.Length;
    dci.ppEnabledExtensionNames = desc.Extensions.Length > 0
        ? (sbyte**)Unsafe.AsPointer(ref MemoryMarshal.GetReference(extPtrs))
        : null;
    // pEnabledFeatures intentionally null — wrapper drives features through the chain only.

    // 6. Create the device.
    VkDevice_T* raw = null;
    Vk.vkCreateDevice(Handle, chain.Head, null, &raw).ThrowIfFailed();

    // 7. Materialise queues. Doing this here (rather than lazy in GetQueue) folds two
    //    benefits: (a) GetQueue becomes a constant-time array lookup, no allocation;
    //    (b) caller declared the queues, so the exhaustive list is known upfront.
    Queue[] queues = new Queue[totalQueues];
    var device = new Device(raw, physicalDevice: this, queues);  // ctor wires Functions, stores `this` as PhysicalDevice ref
    int qSlot = 0;
    for (int i = 0; i < desc.Queues.Length; i++)
    {
        var req = desc.Queues[i];
        for (uint q = 0; q < req.Count; q++)
        {
            VkQueue_T* qh = null;
            Vk.vkGetDeviceQueue(raw, req.FamilyIndex, q, &qh);
            queues[qSlot++] = new Queue(device, qh, req.FamilyIndex, q);
        }
    }
    return device;
}
```

### `Device.GetQueue`

Linear scan over `_queues` looking for `(familyIndex, queueIndex)`. `_queues` length is bounded by the caller's declared queues — typical N is 1–4. Throwing on miss is the API-guidance bit (Q6): a caller can't accidentally `GetQueue(family: 7, queueIndex: 0)` if no queue at that slot was requested.

```csharp
public Queue GetQueue(uint familyIndex, uint queueIndex)
{
    foreach (var q in _queues)
        if (q.FamilyIndex == familyIndex && q.QueueIndex == queueIndex)
            return q;

    throw new ArgumentException(
        $"No queue requested at (family: {familyIndex}, index: {queueIndex}). " +
        $"Add a corresponding QueueRequest to DeviceDescription.Queues.");
}
```

### `DeviceFunctionTable`

Per-device cache of extension entry points the wrapper itself uses. **For #8 it's empty** (no extension-resolved calls used by `Device` proper this issue) — the type exists as a placeholder so #15 (sync primitives) and #17 (synchronization2 already-promoted-but-some-loaders-still-route-through-extension) can add fields without restructuring. The cache pattern matches `InstanceFunctionTable` from #6.

```csharp
internal readonly unsafe struct DeviceFunctionTable
{
    private readonly VkDevice_T* _device;

    public DeviceFunctionTable(VkDevice_T* device) { _device = device; }

    public delegate* unmanaged[Stdcall]<void> Resolve(Utf8Name name) =>
        Vk.vkGetDeviceProcAddr(_device, name.Ptr);
}
```

## 4. Validation

`ValidateQueues` runs always-on (Q6). Three checks, in this order:

1. **At least one queue request.** `desc.Queues.Length > 0` — `vkCreateDevice` requires `queueCreateInfoCount >= 1`. The wrapper rejects a zero-length list with a clear message rather than letting the driver complain.
2. **Family index is in range.** Re-queries `vkGetPhysicalDeviceQueueFamilyProperties2` for the count (cheap) and rejects any `req.FamilyIndex >= count`. (We don't cache this on `PhysicalDevice` — issue #7 explicitly didn't materialise a snapshot.)
3. **Queue count fits the family.** For each request, asserts `req.Count <= families[req.FamilyIndex].queueCount`.

`QueueRequest` ctor already covered: `Count > 0` and `Priority ∈ [0, 1]`. So Validate doesn't repeat those.

```csharp
private void ValidateQueues(ReadOnlySpan<QueueRequest> queues)
{
    if (queues.IsEmpty)
        throw new ArgumentException("DeviceDescription.Queues must contain at least one entry.");

    uint familyCount = 0;
    Vk.vkGetPhysicalDeviceQueueFamilyProperties2(Handle, &familyCount, null);
    Span<VkQueueFamilyProperties2> qfp = stackalloc VkQueueFamilyProperties2[16];
    if (familyCount > qfp.Length) /* lift the same way Instance.PickPhysicalDevice does */;
    for (int i = 0; i < (int)familyCount; i++)
        qfp[i].sType = VkStructureType.VK_STRUCTURE_TYPE_QUEUE_FAMILY_PROPERTIES_2;
    fixed (VkQueueFamilyProperties2* qp = qfp)
        Vk.vkGetPhysicalDeviceQueueFamilyProperties2(Handle, &familyCount, qp);

    for (int i = 0; i < queues.Length; i++)
    {
        ref readonly var req = ref queues[i];
        if (req.FamilyIndex >= familyCount)
            throw new ArgumentException(
                $"QueueRequest.FamilyIndex {req.FamilyIndex} is out of range (device has {familyCount} families).");
        uint avail = qfp[(int)req.FamilyIndex].queueFamilyProperties.queueCount;
        if (req.Count > avail)
            throw new ArgumentException(
                $"QueueRequest at family {req.FamilyIndex} requests {req.Count} queues but family supports {avail}.");
    }
}
```

## 5. File layout

| Path | Kind | Purpose |
|---|---|---|
| `src/Ahjo.Vulkan/Lifecycle/Device.cs` | new | `sealed unsafe class Device : IDisposable`. Holds `Handle`, `Functions`, `PhysicalDevice`, `_queues[]`. |
| `src/Ahjo.Vulkan/Lifecycle/Queue.cs` | new | `sealed unsafe class Queue` — handle + family/index metadata. |
| `src/Ahjo.Vulkan/Lifecycle/DeviceDescription.cs` | new | `ref struct` with `Queues`, `Extensions`, `ConfigureFeatures`. |
| `src/Ahjo.Vulkan/Lifecycle/QueueRequest.cs` | new | `readonly record struct` with validating ctor. |
| `src/Ahjo.Vulkan/Lifecycle/DeviceFeatureChainConfigurer.cs` | new | Delegate declaration. |
| `src/Ahjo.Vulkan/Lifecycle/PhysicalDevice.cs` | rewrite | `sealed unsafe class PhysicalDevice`. Drops `IVulkanHandle<>` and `FromRaw`. Adds `Instance` back-ref + `CreateDevice` + `ValidateQueues`. |
| `src/Ahjo.Vulkan/Lifecycle/Instance.cs` | edit | Add `_physicalDeviceCache` field + `GetOrCreatePhysicalDevice` internal helper. Modify `PickPhysicalDevice` to use the cache. |
| `src/Ahjo.Vulkan/Lifecycle/PhysicalDeviceInfo.cs` | edit | `Device` field: `PhysicalDevice` (now a class reference). xmldoc note. |
| `src/Ahjo.Vulkan/Internal/DeviceFunctionTable.cs` | new | Per-device `vkGetDeviceProcAddr` cache. Empty for #8. |
| `tests/Ahjo.Vulkan.Tests/QueueRequestTests.cs` | new | Pure unit tests on the validating ctor. |
| `tests/Ahjo.Vulkan.Tests/DeviceTests.cs` | new | Driver-gated integration tests. |
| `tests/Ahjo.Vulkan.Tests/HandleConventionsTests.cs` | edit | Delete the two `PhysicalDevice` round-trip facts (it no longer implements `IVulkanHandle<>`). Add direct `ObjectType` assertions for `PhysicalDevice`, `Device`, and `Queue` to document the owner-class shape. |
| `tests/Ahjo.Vulkan.Tests/PhysicalDeviceTests.cs` | edit | Audit assertions for value-vs-reference equality. Add a fact: two `PickPhysicalDevice` calls return the same `PhysicalDevice` instance (reference equality from cache). |
| `tests/Ahjo.Vulkan.Tests/PhysicalDeviceInfoTests.cs` | edit | Constructor calls now pass a `PhysicalDevice` reference (or `null!`) instead of `default`. |
| `tests/Ahjo.Vulkan.Benchmarks/DeviceCreateBenchmark.cs` | new (optional) | Allocation diagnostics for `CreateDevice` — bounded heap (Queue[] + N×Queue) is acceptable. Skip from this issue if zero-alloc isn't an issue acceptance criterion. |

## 6. Tests

Mirroring #7's TDD shape with `VulkanDriverProbe.HasDriver` gating where a real device is required.

### `PhysicalDevice` refactor — no driver where possible

- `HandleConventionsTests`: drop `PhysicalDevice_DefaultHandleIsNull` and `PhysicalDevice_FromRaw_RoundTrips`. Add `PhysicalDevice_ObjectType_IsPhysicalDevice`, `Device_ObjectType_IsDevice`, `Queue_ObjectType_IsQueue` (compile-time checks of the owner-class convention).
- `PhysicalDeviceTests` (driver): `Pick_TwoCalls_ReturnSameInstance` — reference-equality check; backs the cache invariant.

### `QueueRequestTests` — no driver

- `Ctor_ValidInputs_RoundTrips`
- `Ctor_ZeroCount_Throws`
- `Ctor_NegativePriority_Throws`
- `Ctor_PriorityOver1_Throws`
- `Ctor_NaNPriority_Throws`

### `DeviceTests` — driver required

- `CreateDevice_DefaultDescription_OneGraphicsQueue` — picks the first device with a graphics queue family; creates a device with a single 1-queue request; asserts queue is non-null; disposes.
- `CreateDevice_GetQueueByFamilyIndex_ReturnsCachedInstance` — the same family/index handed to `GetQueue` twice returns the same `Queue` reference (cached).
- `CreateDevice_GetQueue_UnknownSlot_Throws` — request `(family: gfx, index: 0)`, then `GetQueue(family: gfx, index: 5)` — `ArgumentException` "No queue requested at …".
- `CreateDevice_QueueOversubscribed_ThrowsBeforeNativeCall` — request `count: 999` for a family that supports 1; `ArgumentException`. No `vkCreateDevice` call (assert by re-using the device after a separate accepting create — harder to assert directly; lean on validation reaching first).
- `CreateDevice_BogusFamilyIndex_Throws` — request `family: 99`; `ArgumentException`.
- `CreateDevice_EmptyQueues_Throws` — `desc.Queues = default`; `ArgumentException`.
- `CreateDevice_DefaultsEnableSync2AndDynamicRendering` — create the device, then immediately call `vkCmdBeginRendering` (extension-promoted, present iff dynamicRendering on) on a one-shot command buffer to confirm. **Or**, simpler and just as reliable: query `Features13.dynamicRendering` on the same physical device — if 1, the test is meaningful (we passed it through the chain); if the GPU reports 0 the test only asserts that creation succeeded with our defaults attempted (drivers ignore unsupported feature toggles silently). Actually the cleanest assertion is: the native call returned `VK_SUCCESS`. Drivers reject mismatched feature requests with `VK_ERROR_FEATURE_NOT_PRESENT`, so a successful create on a 1.4 driver is itself proof the chain was well-formed.
- `CreateDevice_ConfigureFeaturesCallback_Invoked` — pass a closure that flips a captured bool; assert true after create.
- `CreateDevice_ExtensionList_PassedThrough` — request `KhrSwapchain` (universally supported); device is created without throwing. (We don't have a way to read back enabled extensions from a device handle without an extension; this test exists to prove the pointer plumbing works end-to-end via lack-of-throw.)
- `Dispose_CallsWaitIdleAndDestroy` — create, dispose, no exception; create+dispose twice in a row to confirm idempotence.

### Optional: `DeviceCreateBenchmark`

`[MemoryDiagnoser]`. Reports `Allocated`. **Acceptance is not zero** — `Device` allocates one class instance plus `Queue[]` plus N `Queue` instances per call. The bench is documentation of that fact, so future allocation regressions (e.g. closure capture in a hot path) show up as a delta.

## 7. Allocation budget

**Stack per call:**
- `priorities`: `totalQueues * 4 B` — bounded by spec to `<= 65535` per family but realistic ≤ 4. ~16 B.
- `qcis`: `desc.Queues.Length * sizeof(VkDeviceQueueCreateInfo)` — ~40 B per request, ≤ 4 requests typical = ~160 B.
- `extPtrs`: `desc.Extensions.Length * 8 B`.
- `chainBuf`: 2048 B.
- `qfp` in validation: `16 * sizeof(VkQueueFamilyProperties2)` = ~12 KB. Largest single stack consumer; only present during validation, popped before `vkCreateDevice`.

**Heap per `CreateDevice` call:**
- One `Device` instance.
- One `Queue[]` of length `totalQueues`.
- `totalQueues` `Queue` instances.

All long-lived (lifetime = device lifetime). No churn.

**Heap from the PhysicalDevice refactor:**
- First `Instance.PickPhysicalDevice` call: one `PhysicalDevice[]` cache + one `PhysicalDevice` per host GPU (typically 1–3). Lifetime = instance lifetime.
- Subsequent picker calls: zero allocations (cache hit). The #7 `[MemoryDiagnoser]` benchmark runs first call inside `[GlobalSetup]` so the measured iterations report `0 B`.

**Zero stack alloc once dispose.** `Dispose` calls `vkDeviceWaitIdle` then `vkDestroyDevice` and clears the queue array reference. No allocation, no throw on the success path.

## 8. Out of scope

- **`Device.Allocator` property.** Lands with #9.
- **Surface / present-queue picking.** Lands with the surface/swapchain issue.
- **Per-queue distinct priorities** (`QueueRequest(uint, ReadOnlySpan<float>)` overload). Add when a caller actually needs it.
- **Extension function pointers in `DeviceFunctionTable`.** Empty for #8; populated incrementally by later issues.
- **`Device.WaitIdle()` thread-safety annotations / queue-submit safety.** `vkDeviceWaitIdle` is externally synchronised; xmldoc notes will be added as `Submit` lands (issue #15+).
- **Multi-device / device-group creation.** `VkDeviceGroupDeviceCreateInfo` chain support is non-breaking — caller can push it via `ConfigureFeatures` today; first-class support waits on a real use case.
- **`Queue.Submit` etc.** Queue-side methods land with sync primitives (#15) and command buffers (#13/#16).

## 9. Risks

- **`pQueuePriorities` lifetime.** The pointer must be live during the `vkCreateDevice` call. The wrapper holds it in `stackalloc`'d span on the same stack frame that calls `vkCreateDevice` — guaranteed live. Anyone refactoring this code path must keep the lifetime invariant explicit.
- **Pointer pinning for `qcis` / `extPtrs`.** Same as above: stackalloc'd in the call frame; pointer obtained via `Unsafe.AsPointer(ref MemoryMarshal.GetReference(span))` is valid for the call. C# compiler doesn't error if this is moved into a helper that returns; an in-method comment + the spec call out the invariant.
- **Driver doesn't support a default feature.** A 1.4 driver must support every 1.4 promoted feature (it's the spec definition of "1.4 device"). For 1.3 drivers (which the wrapper doesn't currently support — `Instance` defaults to 1.4), the chain would still be sent but the driver might `VK_ERROR_FEATURE_NOT_PRESENT`. Mitigation: out-of-scope. Documented in xmldoc on `CreateDevice`.
- **`Queue` heap allocation surprises a struct-handle reader.** Documented inline at the type. The ten-line xmldoc references the brainstorm record so readers don't second-guess.
- **PhysicalDevice cache + instance lifetime.** `_physicalDeviceCache` lives until `Instance.Dispose`; the `PhysicalDevice` references it holds become invalid (their `Handle` field is dangling) once the instance is destroyed. Same lifetime contract as today (a struct holding a `VkPhysicalDevice_T*` whose backing was destroyed has the same problem). Mitigated by the convention: the user holds a `using var instance` and any `PhysicalDevice` they captured is implicitly scoped under it.
- **PhysicalDevice equality semantics changed.** Was: equal-by-handle struct. Now: reference equality (cache makes this match the previous semantic for the same `VkPhysicalDevice_T*`). Anyone outside the wrapper that compared `PhysicalDevice` values via `==` continues to work; anyone that destructured the struct's `Handle` field directly is now hitting an internal field — but `Handle` was internal already, so no public surface broke.
