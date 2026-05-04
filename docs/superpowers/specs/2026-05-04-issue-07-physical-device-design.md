# Issue #7 — Physical-device selection + capabilities API

Status: design (pending plan)
Date: 2026-05-04
Issue: https://github.com/pekkah/ahjo-vulkan/issues/7
Depends on: #6 (Instance creation, just landed). Transitive: #3 (`IVulkanHandle<TSelf>`), #4 (`ChainBuilder`), #5 (`VkResult` policy).

## 1. Goal

A one-method `instance.PickPhysicalDevice(picker)` that walks the host's `VkPhysicalDevice` list, hands each candidate's properties / features / memory / queue-family / extension snapshot to a caller-supplied delegate as a zero-alloc view, and returns the first match. Capability reads use the Vulkan 1.1+ `*2` getters (`vkGetPhysicalDeviceProperties2`, `…Features2`, `…MemoryProperties2`, `…QueueFamilyProperties2`, `vkEnumerateDeviceExtensionProperties`) which are all statically dispatched on a 1.4 instance — no `vkGetInstanceProcAddr` resolution required.

## 2. Public surface

### `PhysicalDevice`

```csharp
public readonly unsafe struct PhysicalDevice : IVulkanHandle<PhysicalDevice>
{
    internal readonly VkPhysicalDevice_T* Handle;

    public PhysicalDevice(VkPhysicalDevice_T* handle) => Handle = handle;

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_PHYSICAL_DEVICE;
    public static PhysicalDevice FromRaw(nint handle) => new((VkPhysicalDevice_T*)handle);
    public ulong RawHandle => (ulong)(nint)Handle;
    public bool  IsNull    => Handle == null;
}
```

`readonly struct` — matches the project's struct-handle convention. Physical devices have no destroy call (owned by the instance), so no `Dispose`, no finalizer, copy-by-value.

### `KhronosExtensionNames`

```csharp
public static class KhronosExtensionNames
{
    /// <summary><c>VK_KHR_swapchain</c> — required for any device that will present.</summary>
    public static ReadOnlySpan<byte> KhrSwapchain => "VK_KHR_swapchain"u8;
}
```

Public, lives in `Lifecycle/`. Populated only with `KhrSwapchain` for #7 (the one entry the issue's sketch references); future issues add what they need. Mirrors the internal `InstanceExtensionNames` pattern.

### `QueueFamilyInfo`

```csharp
public readonly struct QueueFamilyInfo
{
    public readonly uint         Index;       // family index, 0..N-1
    public readonly VkQueueFlagBits Flags;
    public readonly uint         QueueCount;
    public readonly uint         TimestampValidBits;
    public readonly VkExtent3D   MinImageTransferGranularity;

    public bool SupportsGraphics       => (Flags & VkQueueFlagBits.VK_QUEUE_GRAPHICS_BIT)        != 0;
    public bool SupportsCompute        => (Flags & VkQueueFlagBits.VK_QUEUE_COMPUTE_BIT)         != 0;
    public bool SupportsTransfer       => (Flags & VkQueueFlagBits.VK_QUEUE_TRANSFER_BIT)        != 0;
    public bool SupportsSparseBinding  => (Flags & VkQueueFlagBits.VK_QUEUE_SPARSE_BINDING_BIT)  != 0;
}
```

Plain `struct` (one cache line of value data; bit-test getters). **No `SupportsPresent`** — present is a per-surface property in Vulkan and the surface API is out of scope for this issue. Surface-tied present support lands with the surface/swapchain issue.

### `PhysicalDeviceInfo`

```csharp
public readonly ref struct PhysicalDeviceInfo
{
    public readonly PhysicalDevice                Device;

    public readonly ref readonly VkPhysicalDeviceProperties        Properties;
    public readonly ref readonly VkPhysicalDeviceFeatures          Features;     // base 1.0 features
    public readonly ref readonly VkPhysicalDeviceVulkan11Features  Features11;
    public readonly ref readonly VkPhysicalDeviceVulkan12Features  Features12;
    public readonly ref readonly VkPhysicalDeviceVulkan13Features  Features13;
    public readonly ref readonly VkPhysicalDeviceVulkan14Features  Features14;
    public readonly ref readonly VkPhysicalDeviceMemoryProperties  Memory;

    public readonly ReadOnlySpan<QueueFamilyInfo>       QueueFamilies;
    public readonly ReadOnlySpan<VkExtensionProperties> Extensions;     // raw native struct; name is a 256-byte fixed buffer
    public readonly ReadOnlySpan<byte>                  Name;           // slice of Properties.deviceName, no trailing NUL

    public VkPhysicalDeviceType Type => Properties.deviceType;

    public bool SupportsExtension(ReadOnlySpan<byte> utf8Name);
}
```

`ref struct` because of the `ReadOnlySpan<>` and `ref readonly` fields. Cannot escape `PickPhysicalDevice`. The spans point into stack scratch and an `ArrayPool` rental owned by the call — see §3.

The base `VkPhysicalDeviceFeatures` is exposed directly (the `.features` member of `VkPhysicalDeviceFeatures2`) so callers don't have to reach through `.features.` for 1.0 fields. Promoted features live where Vulkan put them: `info.Features12.bufferDeviceAddress`, `info.Features13.dynamicRendering`, etc. — they were never collapsed into `Features14`, so chaining all four is what "no lost features on 1.4" actually requires.

`Extensions` stays as the raw native struct. The dominant access pattern is `SupportsExtension(span)`; wrapping the entry into a friendlier `ExtensionInfo` would force an extra copy step for no functional gain.

`SupportsExtension` does a linear NUL-terminated UTF-8 compare against each `extensionName` fixed buffer. Allocation-free; small `extCount` makes the linearity irrelevant.

### `PhysicalDevicePicker`

```csharp
public delegate bool PhysicalDevicePicker(in PhysicalDeviceInfo info);
```

Hand-declared delegate (cannot be `Func<,>` because the parameter is `in` of a `ref struct`). Stateless by design — a user `static` lambda is cached by the compiler and allocates nothing on dispatch. State-passing pickers (rank-then-pick) would need a generic `PhysicalDevicePicker<TState>` overload; that's a non-breaking future addition not motivated by #7's acceptance.

### `Instance.PickPhysicalDevice`

```csharp
public sealed unsafe class Instance : IDisposable
{
    // … existing #6 members …

    /// <summary>
    /// Walks the host's physical devices and returns the first one for which
    /// <paramref name="picker"/> returns <see langword="true"/>. The
    /// <see cref="PhysicalDeviceInfo"/> handed to the picker is a view over
    /// scratch owned by this call; do not stash references that escape it.
    /// </summary>
    /// <exception cref="VulkanException">No devices reported, or no device matched.</exception>
    /// <remarks>Assumes the instance was created with <c>apiVersion >= 1.1</c>
    /// (the default in <see cref="InstanceDescription"/> is 1.4). On a
    /// pre-1.1 instance the chained 1.x feature structs would silently read
    /// back as zero.</remarks>
    public PhysicalDevice PickPhysicalDevice(PhysicalDevicePicker picker);
}
```

Throws on both "driver reported zero physical devices" and "no candidate satisfied the picker", with a `VulkanException` whose message distinguishes the two. Both are fatal at app-init time, so an exception is the right shape.

## 3. Internal flow

Single method body in `Instance.cs`. One scratch arena per call, reused across candidates.

```csharp
public PhysicalDevice PickPhysicalDevice(PhysicalDevicePicker picker)
{
    // 1. Enumerate device handles.
    uint count = 0;
    Vk.vkEnumeratePhysicalDevices(Handle, &count, null).ThrowIfFailed();
    if (count == 0)
        throw new VulkanException(VkResult.VK_ERROR_INITIALIZATION_FAILED,
            "No Vulkan physical devices reported by the driver.");

    Span<nint> deviceHandles = count <= 16
        ? stackalloc nint[(int)count]
        : new nint[count];                                  // cold path; never seen on real hw
    fixed (nint* p = deviceHandles)
        Vk.vkEnumeratePhysicalDevices(Handle, &count, (VkPhysicalDevice_T**)p).ThrowIfFailed();

    // 2. Reusable per-device scratch.
    Span<byte> propsChain    = stackalloc byte[1024];
    Span<byte> featuresChain = stackalloc byte[1024];
    Span<VkQueueFamilyProperties2> queueScratch = stackalloc VkQueueFamilyProperties2[16];
    Span<QueueFamilyInfo>          queueViews   = stackalloc QueueFamilyInfo[16];
    VkPhysicalDeviceMemoryProperties2 memory = default;
    memory.sType = VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_MEMORY_PROPERTIES_2;

    var extPool = ArrayPool<VkExtensionProperties>.Shared;
    VkExtensionProperties[] extBuf = [];
    try
    {
        for (int i = 0; i < (int)count; i++)
        {
            var d = (VkPhysicalDevice_T*)deviceHandles[i];

            // 3a. Properties chain (root only — 1.x Properties chain is out of scope §6).
            propsChain.Clear();
            var pchain = ChainBuilder.For<VkPhysicalDeviceProperties2>(propsChain);
            pchain.Root();                                  // sType written by ChainBuilder
            Vk.vkGetPhysicalDeviceProperties2(d, pchain.Head);

            // 3b. Features chain — 1.0 base + 1.1/1.2/1.3/1.4 promoted.
            featuresChain.Clear();
            var fchain = ChainBuilder.For<VkPhysicalDeviceFeatures2>(featuresChain);
            fchain.Root();
            ref var f11 = ref fchain.Push<VkPhysicalDeviceVulkan11Features>();
            ref var f12 = ref fchain.Push<VkPhysicalDeviceVulkan12Features>();
            ref var f13 = ref fchain.Push<VkPhysicalDeviceVulkan13Features>();
            ref var f14 = ref fchain.Push<VkPhysicalDeviceVulkan14Features>();
            Vk.vkGetPhysicalDeviceFeatures2(d, fchain.Head);

            // 3c. Memory.
            fixed (VkPhysicalDeviceMemoryProperties2* mp = &memory)
                Vk.vkGetPhysicalDeviceMemoryProperties2(d, mp);

            // 3d. Queue families.
            uint qCount = 0;
            Vk.vkGetPhysicalDeviceQueueFamilyProperties2(d, &qCount, null);
            if (qCount > queueScratch.Length) ThrowQueueOverflow(qCount);
            for (int q = 0; q < (int)qCount; q++)
                queueScratch[q].sType = VkStructureType.VK_STRUCTURE_TYPE_QUEUE_FAMILY_PROPERTIES_2;
            fixed (VkQueueFamilyProperties2* qp = queueScratch)
                Vk.vkGetPhysicalDeviceQueueFamilyProperties2(d, &qCount, qp);
            for (int q = 0; q < (int)qCount; q++)
            {
                ref var src = ref queueScratch[q].queueFamilyProperties;
                queueViews[q] = new QueueFamilyInfo(
                    index: (uint)q,
                    flags: src.queueFlags,
                    queueCount: src.queueCount,
                    timestampValidBits: src.timestampValidBits,
                    minImageTransferGranularity: src.minImageTransferGranularity);
            }

            // 3e. Device extensions — pool-rent, grow once across iterations.
            uint extCount = 0;
            Vk.vkEnumerateDeviceExtensionProperties(d, null, &extCount, null).ThrowIfFailed();
            if (extBuf.Length < extCount)
            {
                if (extBuf.Length != 0) extPool.Return(extBuf);
                extBuf = extPool.Rent((int)extCount);
            }
            fixed (VkExtensionProperties* ep = extBuf)
                Vk.vkEnumerateDeviceExtensionProperties(d, null, &extCount, ep).ThrowIfFailed();

            // 3f. Build the picker view and dispatch.
            ref var props2  = ref *pchain.Head;
            ref var feats2  = ref *fchain.Head;
            var info = new PhysicalDeviceInfo(
                device:        new PhysicalDevice(d),
                properties:    in props2.properties,
                features:      in feats2.features,
                features11:    in f11,
                features12:    in f12,
                features13:    in f13,
                features14:    in f14,
                memory:        in memory.memoryProperties,
                queueFamilies: queueViews[..(int)qCount],
                extensions:    extBuf.AsSpan(0, (int)extCount),
                name:          NameSlice(in props2.properties));

            if (picker(in info))
                return new PhysicalDevice(d);
        }
    }
    finally
    {
        if (extBuf.Length != 0) extPool.Return(extBuf);
    }

    throw new VulkanException(VkResult.VK_ERROR_INITIALIZATION_FAILED,
        "No physical device matched the picker.");
}
```

### Notes on the flow

- **`Span<nint>` for device handles.** `Span<VkPhysicalDevice_T*>` won't compile (pointers in spans isn't allowed). The cast at the call site is the standard idiom.
- **Queue-family ceiling at 16.** Today's drivers report 1–6; the spec doesn't bound it, so we throw loudly with the actual count if a driver ever exceeds the ceiling. Trivial to lift.
- **`extBuf` rent reuse.** One rental, grown if a later candidate has more extensions than the buffer accommodates. The pool may return a buffer longer than asked, which is fine — `extCount` bounds the span we hand to the picker.
- **`ChainBuilder` is a `ref struct`**, no allocation; the backing buffers are wiped via `propsChain.Clear()` between iterations so leftover bytes from a prior candidate can't masquerade as a `pNext` link.
- **`Push<T>` enforces `IChainable<VkPhysicalDeviceFeatures2>`** at compile time. The four 1.x feature structs ship with that interface generated against `VkPhysicalDeviceFeatures2`.
- **`NameSlice`** scans the 256-byte `deviceName` fixed buffer for the first NUL and returns a `ReadOnlySpan<byte>` over `[0, nul)`. Zero allocation; the slice points into the same `propsChain` buffer.

## 4. File layout

| Path | Kind | Purpose |
|---|---|---|
| `src/Ahjo.Vulkan/Lifecycle/PhysicalDevice.cs` | new | `readonly struct PhysicalDevice : IVulkanHandle<PhysicalDevice>`. |
| `src/Ahjo.Vulkan/Lifecycle/PhysicalDeviceInfo.cs` | new | `readonly ref struct PhysicalDeviceInfo` + `SupportsExtension` impl. |
| `src/Ahjo.Vulkan/Lifecycle/QueueFamilyInfo.cs` | new | `readonly struct QueueFamilyInfo` with the four `Supports*` getters. |
| `src/Ahjo.Vulkan/Lifecycle/PhysicalDevicePicker.cs` | new | The `delegate bool PhysicalDevicePicker(in PhysicalDeviceInfo info)` declaration with xmldoc. |
| `src/Ahjo.Vulkan/Lifecycle/KhronosExtensionNames.cs` | new | Public UTF-8 byte literals; ships only `KhrSwapchain` this issue. |
| `src/Ahjo.Vulkan/Lifecycle/Instance.cs` | edit | Add `PickPhysicalDevice(PhysicalDevicePicker picker)` method body from §3. Existing members untouched. |
| `tests/Ahjo.Vulkan.Tests/PhysicalDeviceTests.cs` | new | Driver-dependent integration tests (see §5). |
| `tests/Ahjo.Vulkan.Tests/QueueFamilyInfoTests.cs` | new | Pure unit tests on the `Supports*` getters. |
| `tests/Ahjo.Vulkan.Benchmarks/PhysicalDevicePickerBenchmark.cs` | new | BenchmarkDotNet `[MemoryDiagnoser]` — proves the zero-alloc acceptance criterion. |

No regenerated/native files touched; every native type the issue needs already exists in `Ahjo.Vulkan.Native`.

## 5. Tests

xUnit v3, mirroring the established `VulkanDriverProbe.HasDriver`-gated pattern from `InstanceCreateTests`.

### `QueueFamilyInfoTests` — no driver

- `Flags_GraphicsBitSet_SupportsGraphicsTrue` — construct with `VK_QUEUE_GRAPHICS_BIT`, assert the four `Supports*` bools.
- `Flags_AllZero_AllSupportsFalse`.
- `Flags_GraphicsAndCompute_BothBitsRead` — guards against accidental equality-vs-bitwise-AND in the getter.

### `PhysicalDeviceTests` — driver required

- **`Pick_AcceptAny_ReturnsFirstDevice`** — picker returns `true` immediately. Asserts `gpu.IsNull == false`. Smoke for the iteration plumbing.
- **`Pick_PrefersDiscrete_OrFallsBack`** — first call's picker prefers `VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU`. If no discrete is present (CI / llvmpipe) the picker returns false and the call is expected to throw; the test then issues a second `PickPhysicalDevice` with an accept-all picker and verifies that succeeds. Covers both the type-read and the no-match-throw paths in one method.
- **`Pick_NoMatch_Throws`** — picker that returns `false` for every device. Asserts `VulkanException` with a message containing "No physical device matched".
- **`Pick_PicksDeviceWithGraphicsQueue`** — picker requires at least one queue family with `SupportsGraphics`. Returned device is then re-inspected by a second picker that captures the matching family index into a stack-friendly state holder (e.g. an out-of-band `int*` passed via `fixed` since the picker is stateless). Verifies `QueueFamilies` materialization end-to-end.
- **`Pick_NameSpan_RoundTripsToString`** — picker returns true on the first device. Inside the picker, the test copies `info.Name` into a managed `byte[]` (the only legal escape — copying values out, not the span itself). After the call, the test calls `Vk.vkGetPhysicalDeviceProperties` directly on the returned device and asserts the names match. Verifies the trailing-NUL strip.
- **`Pick_DriverVersion_NonZero`** — picker checks `info.Properties.driverVersion != 0`. Sanity that the props chain populated.
- **`Pick_ExtensionsContainsCommon`** — `info.SupportsExtension("VK_KHR_maintenance1"u8)` is `true` (commonly present on every shipping device); `SupportsExtension("VK_FAKE_does_not_exist"u8)` is `false`. Covers the linear scan and NUL-terminated comparison.
- **`Pick_QueueFamiliesNeverEmpty`** — Vulkan spec guarantees ≥1 queue family per device.
- **`Pick_Vulkan13Features_Readable`** — picker reads `info.Features13.dynamicRendering`. The value may be 0 on llvmpipe; the test only asserts the call completed without driver crash. Confirms the chain wiring all the way to `Features13`.

### `PhysicalDevicePickerBenchmark`

```csharp
[MemoryDiagnoser]
public class PhysicalDevicePickerBenchmark
{
    private Instance _instance = null!;

    [GlobalSetup]   public void Setup()   => _instance = Instance.Create(default);
    [GlobalCleanup] public void Cleanup() => _instance.Dispose();

    [Benchmark]
    public PhysicalDevice Pick() =>
        _instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => true);
}
```

`[MemoryDiagnoser]` reports `Allocated`. Acceptance criterion is `Allocated == 0 B` after warmup (the `ArrayPool` rental gets parked in the pool on the first iteration; subsequent iterations rent and return without allocating).

### Acceptance mapping

| Issue acceptance criterion | Test / artifact |
|---|---|
| Enumerate physical devices, pick one with a graphics queue family, report name + driver version | `Pick_PicksDeviceWithGraphicsQueue` + `Pick_NameSpan_RoundTripsToString` + `Pick_DriverVersion_NonZero` |
| Picker round-trip allocates 0 bytes | `PhysicalDevicePickerBenchmark` (`Allocated == 0 B`) |

## 6. Allocation budget

Per `PickPhysicalDevice` call:

**Stack:** ~4 KB total.
- Device-handle list: `count * sizeof(nint)` (≤ 128 B for ≤ 16 candidates; cold-path heap fallback above 16).
- Two chain buffers: 2 × 1024 B = 2 KB.
- Queue-family scratch: ~768 B; queue-family views: ~512 B.
- One `VkPhysicalDeviceMemoryProperties2`: ~528 B.
- Misc locals + chain headers: < 256 B.

**Heap, steady state:** zero. `ArrayPool<VkExtensionProperties>.Shared` returns a buffer that's already on the pool from a previous call; `[MemoryDiagnoser]` reports 0 B once the pool is warm.

**Heap, first cold call:** one pool fill (≤ a few KB); masked by `[GlobalSetup]` + warmup iterations in the benchmark.

**Return value:** `PhysicalDevice` is a `readonly struct` — copy-by-value, no allocation. Throw paths allocate the `VulkanException` only on the failure branch, not on success.

## 7. Out of scope

- **Surface support / `qf.SupportsPresent`.** Surface API isn't this issue; queue-family present support is per-surface in core Vulkan (Win32-specific surface-less query exists but is platform-locked). Lands with the surface/swapchain issue.
- **`VkPhysicalDeviceVulkan1xProperties` chain.** The issue's sketch only mentions the features chain. Adding `Vulkan11/12/13/14Properties` doubles the props-buffer footprint and isn't justified by anything #7's acceptance asks for. Easy non-breaking addition later if a picker wants to filter on `driverID` / `driverName`.
- **Generated `VK_KHR_*_EXTENSION_NAME` constants from `vk.xml`.** ClangSharp doesn't materialize `#define`-style string constants. Auto-sourcing them needs a generator change in `Ahjo.Vulkan.Native`; that's its own workstream. We hand-write `KhronosExtensionNames.KhrSwapchain` for now and grow the file per consumer.
- **Persistent `PhysicalDeviceInfo` snapshot.** No `device.GetSnapshot(...)` API. Callers that want post-pick inspection use the native `vk*` APIs directly. Re-introducing a snapshot type later is non-breaking.
- **`Instance.PhysicalDevices` enumerator** (yielding handles only). #7 asks for picking, not enumeration; manual iteration use cases aren't motivated yet.
- **CPU-side device ranking helper.** "Best device" scoring is the user's problem; the picker delegate is the extension point.

## 8. Risks

- **Driver reports > 16 queue families.** No shipping driver does; the spec doesn't bound it. Mitigation: `ThrowQueueOverflow(qCount)` includes the actual count so the failure mode is loud. Trivial to lift the ceiling.
- **Pre-1.1 instance.** `vkGetPhysicalDeviceFeatures2` requires `apiVersion ≥ 1.1`. The default `InstanceDescription.ApiVersion` from #6 is 1.4. A caller dropping below 1.1 would get zeros from the chained 1.x feature structs (driver silently ignores unknown chained sTypes). Documented in `PickPhysicalDevice` xmldoc.
- **Pool buffer escape.** `ReadOnlySpan<T>` is a `ref struct` — the C# compiler prevents the picker from stashing the span anywhere it could outlive the call. Standard ref-safety; no extra mitigation needed.
- **CI without driver.** Same mitigation as #6 — `Skip.IfNot(VulkanDriverProbe.HasDriver, …)`. SwiftShader / mock-ICD CI is its own workflow change, tracked separately.
