# Issue #8 — Device creation API + queue access — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `physicalDevice.CreateDevice(in DeviceDescription)` returning a `Device` (sealed class) with a pre-baked `Queue[]` and the wrapper's 1.4 default features auto-enabled. Bundle a small refactor: `PhysicalDevice` becomes a `sealed class` (drops `IVulkanHandle<>`) and is cached on its owning `Instance` so picker round-trips remain zero-allocation.

**Architecture:** `Device` mirrors `Instance` (sealed class, finalizer backstop, deterministic `Dispose` calls `vkDeviceWaitIdle` + `vkDestroyDevice`). `Queue` is a sealed class held in a `Device._queues` array, materialised once at create time. `QueueRequest` is a validating `readonly record struct`. Feature defaults are pushed onto a stack-allocated `ChainBuilder<VkDeviceCreateInfo>`; callers can append more via `desc.ConfigureFeatures`. `PhysicalDevice` cache uses a small array on `Instance` keyed by `VkPhysicalDevice_T*`.

**Tech stack:** .NET 10, C# 11+ (`unsafe`, `ref` fields in `ref struct`, `Unsafe.AsPointer`), xUnit v3, BenchmarkDotNet `[MemoryDiagnoser]` (benchmark optional). Existing primitives: `IVulkanHandle<TSelf>` (#3), `ChainBuilder<TRoot>` (#4), `VkResult.ThrowIfFailed()` (#5), `Instance` (#6), `PhysicalDeviceInfo` + picker (#7), `VulkanDriverProbe`.

**Spec:** `docs/superpowers/specs/2026-05-04-issue-08-device-creation-design.md`. Read it before starting.

---

## File map

| Path | Kind | Purpose |
|---|---|---|
| `src/Ahjo.Vulkan/Lifecycle/PhysicalDevice.cs` | rewrite | `sealed unsafe class`. Drops `IVulkanHandle<>` + `FromRaw`. Adds `Instance` back-ref + `CreateDevice` + `ValidateQueues`. |
| `src/Ahjo.Vulkan/Lifecycle/Instance.cs` | edit | Add `_physicalDeviceCache` + `GetOrCreatePhysicalDevice`. Modify `PickPhysicalDevice` to use the cache. |
| `src/Ahjo.Vulkan/Lifecycle/PhysicalDeviceInfo.cs` | edit | `Device` field is now a `PhysicalDevice` reference. |
| `src/Ahjo.Vulkan/Lifecycle/QueueRequest.cs` | new | Validating `readonly record struct`. |
| `src/Ahjo.Vulkan/Lifecycle/DeviceFeatureChainConfigurer.cs` | new | Delegate declaration. |
| `src/Ahjo.Vulkan/Lifecycle/DeviceDescription.cs` | new | `ref struct` with `Queues`, `Extensions`, `ConfigureFeatures`. |
| `src/Ahjo.Vulkan/Lifecycle/Queue.cs` | new | `sealed unsafe class` — handle + family/index metadata. |
| `src/Ahjo.Vulkan/Lifecycle/Device.cs` | new | `sealed unsafe class : IDisposable`. |
| `src/Ahjo.Vulkan/Internal/DeviceFunctionTable.cs` | new | Per-device `vkGetDeviceProcAddr` cache. Empty for #8. |
| `tests/Ahjo.Vulkan.Tests/HandleConventionsTests.cs` | edit | Drop the two `PhysicalDevice` round-trip facts. Add `ObjectType` smoke for `PhysicalDevice`/`Device`/`Queue`. |
| `tests/Ahjo.Vulkan.Tests/PhysicalDeviceInfoTests.cs` | edit | Constructor calls now pass a `PhysicalDevice` reference (or `null!`). |
| `tests/Ahjo.Vulkan.Tests/PhysicalDeviceTests.cs` | edit | Audit assertions; add `Pick_TwoCalls_ReturnSameInstance`. |
| `tests/Ahjo.Vulkan.Tests/QueueRequestTests.cs` | new | Pure unit tests for the validating ctor. |
| `tests/Ahjo.Vulkan.Tests/DeviceTests.cs` | new | Driver-gated integration tests for `PhysicalDevice.CreateDevice` + `Device`/`Queue`. |

Tasks are ordered as **refactor → leaf types → keystone method → integration tests**. The refactor lands first because every later task assumes `PhysicalDevice` is already a class.

---

## Task 1: Refactor `PhysicalDevice` → sealed class (handle file only)

The minimum-blast-radius first move. Replaces the struct definition with a class definition and updates the test file. Build is broken at the end of this step (Instance, PhysicalDeviceInfo, picker tests still expect a struct) — Tasks 2–4 close the loop.

**Files:**
- Rewrite: `src/Ahjo.Vulkan/Lifecycle/PhysicalDevice.cs`
- Edit: `tests/Ahjo.Vulkan.Tests/HandleConventionsTests.cs`

- [ ] **Step 1: Rewrite `PhysicalDevice.cs`.**

```csharp
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Wrapper handle for a <c>VkPhysicalDevice</c>. Owned by a
/// <see cref="Instance"/> and produced exclusively by
/// <see cref="Instance.PickPhysicalDevice"/>; <see cref="Instance"/> caches
/// one managed instance per native handle so reference equality matches
/// "same GPU."
/// </summary>
/// <remarks>
/// Owner-class shape (sealed class) rather than struct + <c>IVulkanHandle&lt;&gt;</c>:
/// physical devices are created 1–3 times per process and never debug-named
/// or pooled, so the generic-dispatch infrastructure that
/// <c>IVulkanHandle&lt;TSelf&gt;</c> exists for is inert here. Resource
/// handles (Buffer, Image, …) keep the struct + interface convention.
/// </remarks>
public sealed unsafe class PhysicalDevice
{
    internal readonly VkPhysicalDevice_T* Handle;
    internal readonly Instance            Instance;

    internal PhysicalDevice(Instance instance, VkPhysicalDevice_T* handle)
    {
        Instance = instance;
        Handle   = handle;
    }

    public ulong RawHandle => (ulong)(nint)Handle;
    public bool  IsNull    => Handle == null;
    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_PHYSICAL_DEVICE;
}
```

> `CreateDevice` is added in Task 8 once its dependencies (`Device`, `Queue`, `QueueRequest`, `DeviceDescription`) exist.

- [ ] **Step 2: Update `HandleConventionsTests.cs`.**

Remove the two `[Fact]` methods added by issue #7:
- `PhysicalDevice_DefaultHandleIsNull`
- `PhysicalDevice_FromRaw_RoundTrips`

Add three replacement facts (no helpers needed):

```csharp
    [Fact]
    public void PhysicalDevice_ObjectType_IsPhysicalDevice()
    {
        Assert.Equal(VkObjectType.VK_OBJECT_TYPE_PHYSICAL_DEVICE, PhysicalDevice.ObjectType);
    }

    [Fact]
    public void Device_ObjectType_IsDevice()
    {
        Assert.Equal(VkObjectType.VK_OBJECT_TYPE_DEVICE, Device.ObjectType);
    }

    [Fact]
    public void Queue_ObjectType_IsQueue()
    {
        Assert.Equal(VkObjectType.VK_OBJECT_TYPE_QUEUE, Queue.ObjectType);
    }
```

> The `Device` and `Queue` references won't compile yet — Tasks 6 and 7 add those types. Acceptable because the whole point of Task 1 is to land the breaking change; the test file builds again at the end of Task 8.

- [ ] **Step 3: Run the build — expect failures.** Confirm the failures are the predictable ones (struct vs class, missing `Device`/`Queue`, `PhysicalDeviceInfo` constructor signature, etc.) — nothing unexpected.

```
dotnet build src/Ahjo.Vulkan/Ahjo.Vulkan.csproj
```

- [ ] **Step 4: Do not commit yet.** Tasks 2–4 close the loop; one commit covers the whole shape change.

---

## Task 2: Update `PhysicalDeviceInfo` for the new `PhysicalDevice` shape

**Files:**
- Edit: `src/Ahjo.Vulkan/Lifecycle/PhysicalDeviceInfo.cs`

- [ ] **Step 1: Field type stays `PhysicalDevice`** — the source no longer has to change *type-wise* (the field was `public readonly PhysicalDevice Device` before and stays `public readonly PhysicalDevice Device`). What changes is the *meaning*: it's now a reference. Update the xmldoc:

Replace the `Device` field's xmldoc (or add it if absent) with:

```csharp
    /// <summary>
    /// The physical device this view describes. <see cref="PhysicalDevice"/>
    /// is a class; the wrapper caches one instance per native handle on
    /// <see cref="Instance"/>, so this reference is the same instance other
    /// callers see for the same GPU.
    /// </summary>
    public readonly PhysicalDevice Device;
```

- [ ] **Step 2: Constructor parameter** — the existing constructor takes `PhysicalDevice device` by value. No source change needed; the call sites adjust automatically because the type is the same name.

- [ ] **Step 3: Run the build — still failing on `Instance.PickPhysicalDevice` (cache not added yet) and on `Device`/`Queue` references.** Expected; Tasks 3 and 4 close the gaps.

---

## Task 3: Add the PhysicalDevice cache to `Instance`

**Files:**
- Edit: `src/Ahjo.Vulkan/Lifecycle/Instance.cs`

- [ ] **Step 1: Add the cache field + helper method.**

Inside the `Instance` class, after the `_callbackKeepAlive` / `_disposed` fields:

```csharp
    private PhysicalDevice[]? _physicalDeviceCache;

    /// <summary>
    /// Returns the wrapped <see cref="PhysicalDevice"/> for a raw native
    /// handle, materialising and caching one if the handle has not been
    /// seen before. Called by <see cref="PickPhysicalDevice"/> so identity
    /// (reference equality) matches "same GPU."
    /// </summary>
    internal PhysicalDevice GetOrCreatePhysicalDevice(VkPhysicalDevice_T* handle)
    {
        var cache = _physicalDeviceCache;
        if (cache != null)
        {
            for (int i = 0; i < cache.Length; i++)
                if (cache[i].Handle == handle)
                    return cache[i];
        }

        // Cold path: enumerate every physical device the instance reports
        // and populate the cache. We always materialise the full set so a
        // later picker call does not allocate even if it sees a different
        // candidate first.
        return PopulateCacheAndFind(handle);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private PhysicalDevice PopulateCacheAndFind(VkPhysicalDevice_T* handle)
    {
        uint count = 0;
        Vk.vkEnumeratePhysicalDevices(Handle, &count, null).ThrowIfFailed();

        var fresh = new PhysicalDevice[count];
        if (count > 0)
        {
            Span<nint> raw = count <= 16
                ? stackalloc nint[(int)count]
                : new nint[count];
            fixed (nint* p = raw)
                Vk.vkEnumeratePhysicalDevices(Handle, &count, (VkPhysicalDevice_T**)p).ThrowIfFailed();

            for (int i = 0; i < (int)count; i++)
                fresh[i] = new PhysicalDevice(this, (VkPhysicalDevice_T*)raw[i]);
        }
        _physicalDeviceCache = fresh;

        for (int i = 0; i < fresh.Length; i++)
            if (fresh[i].Handle == handle)
                return fresh[i];

        // Should be unreachable — caller saw `handle` from `vkEnumeratePhysicalDevices`,
        // and the spec doesn't allow the set to shrink mid-frame.
        throw new VulkanException(VkResult.VK_ERROR_INITIALIZATION_FAILED,
            "PhysicalDevice handle was not in the freshly-enumerated set.");
    }
```

- [ ] **Step 2: Modify `PickPhysicalDevice`.** Replace the two `new PhysicalDevice(d)` allocations with cache lookups.

Find:
```csharp
                var info = new PhysicalDeviceInfo(
                    device:        new PhysicalDevice(d),
                    properties:    in props2.properties,
```

Replace with:
```csharp
                var gpu = GetOrCreatePhysicalDevice(d);
                var info = new PhysicalDeviceInfo(
                    device:        gpu,
                    properties:    in props2.properties,
```

Find:
```csharp
                if (picker(in info))
                    return new PhysicalDevice(d);
```

Replace with:
```csharp
                if (picker(in info))
                    return gpu;
```

- [ ] **Step 3: Build expects to fail only on `Device`/`Queue`-related call sites** (HandleConventionsTests, the soon-to-arrive Device tests). The `Ahjo.Vulkan` project itself should now build.

```
dotnet build src/Ahjo.Vulkan/Ahjo.Vulkan.csproj
```

Expected: clean build of `Ahjo.Vulkan`.

---

## Task 4: Update `PhysicalDeviceInfoTests` ctor calls + commit the refactor

**Files:**
- Edit: `tests/Ahjo.Vulkan.Tests/PhysicalDeviceInfoTests.cs`
- Edit: `tests/Ahjo.Vulkan.Tests/PhysicalDeviceTests.cs`

- [ ] **Step 1: `PhysicalDeviceInfoTests` — `device:` parameter.**

The four existing facts pass `device: default`. With `PhysicalDevice` as a class, `default` is `null`. Vk wrapper code never reads `info.Device` inside `SupportsExtension`, so passing `null!` is safe (the test only exercises the extension-name scan).

Find every occurrence of `device: default,` (in both `BuildAndQuery` and the inlined empty-extensions test) and replace with `device: null!,`.

- [ ] **Step 2: `PhysicalDeviceTests` — add the cache identity test.**

Append to the class:

```csharp
    [Fact]
    public void Pick_TwoCalls_ReturnSameInstance()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        PhysicalDevice a = instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => true);
        PhysicalDevice b = instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => true);

        Assert.Same(a, b);
    }
```

- [ ] **Step 3: Audit `Pick_NameSpan_RoundTripsToString`.** It calls `Vk.vkGetPhysicalDeviceProperties(gpu.Handle, &props)` — `gpu.Handle` is now an internal field on a class. The test lives in the same assembly, so the access still compiles. Confirm by reading the test file.

- [ ] **Step 4: Build the whole solution.** It must compile *except* for the three `Device`/`Queue` references in `HandleConventionsTests` added by Task 1.

```
dotnet build
```

Expected: only the three "type does not exist" errors for `Device.ObjectType` / `Queue.ObjectType`. Acceptable temporarily.

> **Note:** we cannot run tests yet — the test project doesn't build. We rely on the compile-success of `Ahjo.Vulkan` itself plus the visual review of the changes to keep moving. Tasks 6 and 7 add the missing types; the full build comes back at the end of Task 7.

- [ ] **Step 5: Commit the refactor as one unit.** Tasks 1–4 are inseparable.

```
git add src/Ahjo.Vulkan/Lifecycle/PhysicalDevice.cs \
        src/Ahjo.Vulkan/Lifecycle/PhysicalDeviceInfo.cs \
        src/Ahjo.Vulkan/Lifecycle/Instance.cs \
        tests/Ahjo.Vulkan.Tests/HandleConventionsTests.cs \
        tests/Ahjo.Vulkan.Tests/PhysicalDeviceInfoTests.cs \
        tests/Ahjo.Vulkan.Tests/PhysicalDeviceTests.cs
git commit -m "Refactor PhysicalDevice to sealed class with Instance cache (issue 08 prep)"
```

---

## Task 5: `QueueRequest` validating struct

**Files:**
- Create: `src/Ahjo.Vulkan/Lifecycle/QueueRequest.cs`
- Test: `tests/Ahjo.Vulkan.Tests/QueueRequestTests.cs`

- [ ] **Step 1: Write the failing tests.**

```csharp
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed class QueueRequestTests
{
    [Fact]
    public void Ctor_ValidInputs_RoundTrips()
    {
        var r = new QueueRequest(familyIndex: 2, count: 3, priority: 0.5f);
        Assert.Equal(2u,   r.FamilyIndex);
        Assert.Equal(3u,   r.Count);
        Assert.Equal(0.5f, r.Priority);
    }

    [Fact]
    public void Ctor_ZeroCount_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new QueueRequest(0, 0, 0.5f));
        Assert.Equal("count", ex.ParamName);
    }

    [Fact]
    public void Ctor_NegativePriority_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new QueueRequest(0, 1, -0.0001f));
        Assert.Equal("priority", ex.ParamName);
    }

    [Fact]
    public void Ctor_PriorityOver1_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new QueueRequest(0, 1, 1.0001f));
        Assert.Equal("priority", ex.ParamName);
    }

    [Fact]
    public void Ctor_NaNPriority_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new QueueRequest(0, 1, float.NaN));
        Assert.Equal("priority", ex.ParamName);
    }
}
```

- [ ] **Step 2: Run — expect compile failure.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~QueueRequestTests"
```

- [ ] **Step 3: Implement.**

```csharp
namespace Ahjo.Vulkan;

/// <summary>
/// Single-priority queue creation request handed to
/// <see cref="DeviceDescription.Queues"/>. The constructor validates
/// <paramref name="count"/> &gt; 0 and <paramref name="priority"/> in
/// [0, 1] so misuse fails at construction rather than as a Vulkan
/// validation-layer warning at <c>vkCreateDevice</c>.
/// </summary>
/// <remarks>
/// The single-priority shape covers the 80% case (one priority broadcast
/// to every queue in the family). Per-queue distinct priorities (rare —
/// e.g. one realtime and one background queue in the same family) are a
/// non-breaking future addition: a second constructor accepting
/// <c>ReadOnlySpan&lt;float&gt;</c>.
/// </remarks>
public readonly record struct QueueRequest
{
    public uint  FamilyIndex { get; }
    public uint  Count       { get; }
    public float Priority    { get; }

    public QueueRequest(uint familyIndex, uint count, float priority)
    {
        if (count == 0)
            throw new ArgumentException("QueueRequest.Count must be > 0.", nameof(count));
        if (priority < 0f || priority > 1f || float.IsNaN(priority))
            throw new ArgumentException(
                "QueueRequest.Priority must be in [0, 1] (Vulkan VUID-VkDeviceQueueCreateInfo-pQueuePriorities-00383).",
                nameof(priority));

        FamilyIndex = familyIndex;
        Count       = count;
        Priority    = priority;
    }
}
```

- [ ] **Step 4: Run — expect 5 passing.**

- [ ] **Step 5: Commit.**

```
git add src/Ahjo.Vulkan/Lifecycle/QueueRequest.cs tests/Ahjo.Vulkan.Tests/QueueRequestTests.cs
git commit -m "Add QueueRequest validating struct (issue 08)"
```

---

## Task 6: `Queue` sealed class + `DeviceFunctionTable` + `DeviceFeatureChainConfigurer` + `DeviceDescription`

Bundled because they're all small, mutually-referential leaf types with no test surface of their own.

**Files:**
- Create: `src/Ahjo.Vulkan/Lifecycle/DeviceFeatureChainConfigurer.cs`
- Create: `src/Ahjo.Vulkan/Lifecycle/DeviceDescription.cs`
- Create: `src/Ahjo.Vulkan/Lifecycle/Queue.cs`
- Create: `src/Ahjo.Vulkan/Internal/DeviceFunctionTable.cs`

- [ ] **Step 1: Write `DeviceFeatureChainConfigurer.cs`.**

```csharp
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Hook handed to <see cref="DeviceDescription.ConfigureFeatures"/>;
/// invoked once during <c>CreateDevice</c> after the wrapper has pushed
/// the 1.4 default feature structs onto the chain. Push additional
/// <c>IChainable&lt;VkDeviceCreateInfo&gt;</c> structs here.
/// </summary>
public unsafe delegate void DeviceFeatureChainConfigurer(ref ChainBuilder<VkDeviceCreateInfo> chain);
```

- [ ] **Step 2: Write `DeviceDescription.cs`.**

```csharp
namespace Ahjo.Vulkan;

/// <summary>
/// Inputs to <see cref="PhysicalDevice.CreateDevice"/>. <c>ref struct</c>
/// because of the spans. Field defaults are legal: <see cref="Queues"/>
/// must be non-empty (validated in <c>CreateDevice</c>); the rest may be
/// empty / null.
/// </summary>
public ref struct DeviceDescription
{
    public ReadOnlySpan<QueueRequest> Queues;
    public ReadOnlySpan<Utf8Name>     Extensions;
    public DeviceFeatureChainConfigurer? ConfigureFeatures;
}
```

- [ ] **Step 3: Write `Queue.cs`.**

```csharp
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Wrapper handle for a <c>VkQueue</c>. Owned by a <see cref="Device"/>
/// and produced exclusively by <see cref="PhysicalDevice.CreateDevice"/>;
/// the device caches one instance per <c>(family, index)</c> requested
/// in <see cref="DeviceDescription.Queues"/>.
/// </summary>
/// <remarks>
/// Owner-class shape (sealed class) for the same reason as
/// <see cref="PhysicalDevice"/> and <see cref="Device"/>: queues are
/// created 1–4 times per device and never debug-named or pooled.
/// Construction is internal; outside-the-wrapper construction is
/// meaningless because <c>vkGetDeviceQueue</c> requires a device.
/// </remarks>
public sealed unsafe class Queue
{
    internal readonly VkQueue_T* Handle;
    public   readonly uint       FamilyIndex;
    public   readonly uint       QueueIndex;
    public   readonly Device     Device;

    internal Queue(Device device, VkQueue_T* handle, uint familyIndex, uint queueIndex)
    {
        Device      = device;
        Handle      = handle;
        FamilyIndex = familyIndex;
        QueueIndex  = queueIndex;
    }

    public ulong RawHandle => (ulong)(nint)Handle;
    public bool  IsNull    => Handle == null;
    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_QUEUE;
}
```

- [ ] **Step 4: Write `DeviceFunctionTable.cs`.**

```csharp
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Per-device cache of extension entry points the wrapper itself uses.
/// Empty for #8; populated incrementally by later issues
/// (e.g. timeline-semaphore helpers, debug-utils naming).
/// </summary>
internal readonly unsafe struct DeviceFunctionTable
{
    private readonly VkDevice_T* _device;

    public DeviceFunctionTable(VkDevice_T* device) { _device = device; }

    public delegate* unmanaged[Stdcall]<void> Resolve(Utf8Name name) =>
        Vk.vkGetDeviceProcAddr(_device, name.Ptr);
}
```

- [ ] **Step 5: Build — expect failure only on `Device` (not yet defined).**

```
dotnet build src/Ahjo.Vulkan/Ahjo.Vulkan.csproj
```

- [ ] **Step 6: Do not commit yet.** Tasks 6 + 7 commit together.

---

## Task 7: `Device` sealed class

**Files:**
- Create: `src/Ahjo.Vulkan/Lifecycle/Device.cs`

- [ ] **Step 1: Write `Device.cs`.**

```csharp
using System.Diagnostics;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Owner of a <c>VkDevice</c>. <c>sealed class</c> for the same reasons
/// as <see cref="Instance"/>: created once per app, never copied, deterministic
/// <see cref="Dispose"/> calls <c>vkDeviceWaitIdle</c> + <c>vkDestroyDevice</c>,
/// finalizer backstops a missed dispose (a leaked device leaves the GPU
/// busy for the rest of the process).
/// </summary>
public sealed unsafe class Device : IDisposable
{
    internal readonly VkDevice_T*         Handle;
    internal readonly DeviceFunctionTable Functions;
    public   readonly PhysicalDevice      PhysicalDevice;
    private  readonly Queue[]             _queues;
    private  bool                         _disposed;

    internal Device(VkDevice_T* handle, PhysicalDevice physicalDevice, Queue[] queues)
    {
        Handle         = handle;
        Functions      = new DeviceFunctionTable(handle);
        PhysicalDevice = physicalDevice;
        _queues        = queues;
    }

    public ulong RawHandle => (ulong)(nint)Handle;
    public bool  IsNull    => Handle == null;
    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_DEVICE;

    /// <summary>
    /// Returns the cached <see cref="Queue"/> for the requested
    /// <c>(familyIndex, queueIndex)</c>. The pair must match a
    /// <see cref="QueueRequest"/> that was passed to
    /// <see cref="DeviceDescription.Queues"/>; otherwise an
    /// <see cref="ArgumentException"/> guides the caller to declare it.
    /// </summary>
    public Queue GetQueue(uint familyIndex, uint queueIndex)
    {
        var queues = _queues;
        for (int i = 0; i < queues.Length; i++)
        {
            if (queues[i].FamilyIndex == familyIndex && queues[i].QueueIndex == queueIndex)
                return queues[i];
        }

        throw new ArgumentException(
            $"No queue requested at (family: {familyIndex}, index: {queueIndex}). " +
            "Add a corresponding QueueRequest to DeviceDescription.Queues.");
    }

    /// <summary>Wraps <c>vkDeviceWaitIdle</c>.</summary>
    public void WaitIdle()
    {
        Vk.vkDeviceWaitIdle(Handle).ThrowIfFailed();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Handle != null)
        {
            Vk.vkDeviceWaitIdle(Handle);  // best-effort; Dispose mustn't throw on the success path
            Vk.vkDestroyDevice(Handle, null);
        }
        GC.SuppressFinalize(this);
    }

    ~Device()
    {
        Debug.Fail("Device was not disposed.");
        Dispose();
    }
}
```

- [ ] **Step 2: Build the whole solution.** Should now succeed end-to-end (the previously-broken `HandleConventionsTests` references to `Device`/`Queue` resolve).

```
dotnet build
```

Expected: clean build.

- [ ] **Step 3: Run the existing test suite to confirm zero regressions from the refactor.**

```
dotnet test
```

Expected: all green except the new `Pick_TwoCalls_ReturnSameInstance` (which should also pass on a host with a driver) and any driver-gated tests that legitimately skip on driverless hosts.

- [ ] **Step 4: Commit Tasks 6 + 7 together.** This brings the build back to green.

```
git add src/Ahjo.Vulkan/Lifecycle/DeviceFeatureChainConfigurer.cs \
        src/Ahjo.Vulkan/Lifecycle/DeviceDescription.cs \
        src/Ahjo.Vulkan/Lifecycle/Queue.cs \
        src/Ahjo.Vulkan/Lifecycle/Device.cs \
        src/Ahjo.Vulkan/Internal/DeviceFunctionTable.cs
git commit -m "Add Device + Queue + DeviceDescription scaffolding (issue 08)"
```

---

## Task 8: `PhysicalDevice.CreateDevice` + smoke test

The keystone method. Implements the full body from spec §3 and confirms it round-trips with a single graphics queue.

**Files:**
- Edit: `src/Ahjo.Vulkan/Lifecycle/PhysicalDevice.cs`
- Create: `tests/Ahjo.Vulkan.Tests/DeviceTests.cs`

- [ ] **Step 1: Write the failing smoke test.**

```csharp
using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed unsafe class DeviceTests
{
    [Fact]
    public void CreateDevice_DefaultDescription_OneGraphicsQueue()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        uint gfxFamily = uint.MaxValue;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    gfxFamily = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });

        Assert.NotEqual(uint.MaxValue, gfxFamily);

        var desc = new DeviceDescription
        {
            Queues = [new QueueRequest(gfxFamily, count: 1, priority: 1.0f)],
        };

        using var device = gpu.CreateDevice(in desc);

        Queue gfx = device.GetQueue(gfxFamily, queueIndex: 0);
        Assert.False(gfx.IsNull);
        Assert.Same(device, gfx.Device);
    }
}
```

- [ ] **Step 2: Run — expect compile failure on `gpu.CreateDevice`.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~DeviceTests.CreateDevice_DefaultDescription_OneGraphicsQueue"
```

- [ ] **Step 3: Implement `CreateDevice` + `ValidateQueues`.**

Open `src/Ahjo.Vulkan/Lifecycle/PhysicalDevice.cs`. Add the necessary usings at the top:

```csharp
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;
```

Append two methods to the class. Match the existing brace style in the file.

```csharp
    /// <summary>
    /// Creates a Vulkan device with the wrapper's 1.4 default feature set
    /// (synchronization2, dynamicRendering, timelineSemaphore,
    /// bufferDeviceAddress, pushDescriptor) plus any additional features
    /// the caller pushes via
    /// <see cref="DeviceDescription.ConfigureFeatures"/>. Validates queue
    /// requests against this physical device's queue families before the
    /// native call.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <see cref="DeviceDescription.Queues"/> is empty, or a queue request
    /// references a non-existent family / requests more queues than the
    /// family supports.
    /// </exception>
    /// <exception cref="VulkanException">
    /// <c>vkCreateDevice</c> failed (driver mismatch, OOM, feature not
    /// present on the device, etc.).
    /// </exception>
    /// <remarks>
    /// Assumes a Vulkan 1.4 device. The default feature set lights up
    /// every 1.4 promotional feature; on a 1.3 device the driver may
    /// reject the chain with <c>VK_ERROR_FEATURE_NOT_PRESENT</c>.
    /// </remarks>
    public Device CreateDevice(in DeviceDescription desc)
    {
        ValidateQueues(desc.Queues);

        int totalQueues = 0;
        for (int i = 0; i < desc.Queues.Length; i++)
            totalQueues += (int)desc.Queues[i].Count;

        Span<float>                   priorities = stackalloc float[totalQueues];
        Span<VkDeviceQueueCreateInfo> qcis       = stackalloc VkDeviceQueueCreateInfo[desc.Queues.Length];
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

        Span<nint> extPtrs = stackalloc nint[desc.Extensions.Length];
        for (int i = 0; i < desc.Extensions.Length; i++)
            extPtrs[i] = (nint)desc.Extensions[i].Ptr;

        Span<byte> chainBuf = stackalloc byte[2048];
        var chain = ChainBuilder.For<VkDeviceCreateInfo>(chainBuf);
        ref VkDeviceCreateInfo dci = ref chain.Root();

        ref var f12 = ref chain.Push<VkPhysicalDeviceVulkan12Features>();
        f12.bufferDeviceAddress = 1;
        f12.timelineSemaphore   = 1;
        ref var f13 = ref chain.Push<VkPhysicalDeviceVulkan13Features>();
        f13.synchronization2 = 1;
        f13.dynamicRendering = 1;
        ref var f14 = ref chain.Push<VkPhysicalDeviceVulkan14Features>();
        f14.pushDescriptor = 1;

        desc.ConfigureFeatures?.Invoke(ref chain);

        dci.queueCreateInfoCount    = (uint)desc.Queues.Length;
        dci.pQueueCreateInfos       = (VkDeviceQueueCreateInfo*)Unsafe.AsPointer(ref qcis[0]);
        dci.enabledExtensionCount   = (uint)desc.Extensions.Length;
        dci.ppEnabledExtensionNames = desc.Extensions.Length > 0
            ? (sbyte**)Unsafe.AsPointer(ref MemoryMarshal.GetReference(extPtrs))
            : null;
        // pEnabledFeatures intentionally null — features driven through the chain only.

        VkDevice_T* raw = null;
        Vk.vkCreateDevice(Handle, chain.Head, null, &raw).ThrowIfFailed();

        Queue[] queues = new Queue[totalQueues];
        var device = new Device(raw, physicalDevice: this, queues);
        int qSlot = 0;
        for (int i = 0; i < desc.Queues.Length; i++)
        {
            QueueRequest req = desc.Queues[i];
            for (uint q = 0; q < req.Count; q++)
            {
                VkQueue_T* qh = null;
                Vk.vkGetDeviceQueue(raw, req.FamilyIndex, q, &qh);
                queues[qSlot++] = new Queue(device, qh, req.FamilyIndex, q);
            }
        }
        return device;
    }

    private void ValidateQueues(ReadOnlySpan<QueueRequest> queues)
    {
        if (queues.IsEmpty)
            throw new ArgumentException("DeviceDescription.Queues must contain at least one entry.");

        uint familyCount = 0;
        Vk.vkGetPhysicalDeviceQueueFamilyProperties2(Handle, &familyCount, null);

        Span<VkQueueFamilyProperties2> qfp = stackalloc VkQueueFamilyProperties2[16];
        if (familyCount > qfp.Length)
            throw new VulkanException(VkResult.VK_ERROR_INITIALIZATION_FAILED,
                $"Physical device reports {familyCount} queue families; wrapper ceiling is 16.");
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

- [ ] **Step 4: Run the smoke test — expect 1 passing (or skipped without a driver).**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~DeviceTests.CreateDevice_DefaultDescription_OneGraphicsQueue"
```

Then run the full suite — no regressions:

```
dotnet test
```

- [ ] **Step 5: Commit.**

```
git add src/Ahjo.Vulkan/Lifecycle/PhysicalDevice.cs tests/Ahjo.Vulkan.Tests/DeviceTests.cs
git commit -m "Add PhysicalDevice.CreateDevice + smoke test (issue 08)"
```

---

## Task 9: Validation regression tests

**Files:**
- Edit: `tests/Ahjo.Vulkan.Tests/DeviceTests.cs`

- [ ] **Step 1: Append the validation tests.**

```csharp
    [Fact]
    public void CreateDevice_EmptyQueues_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        var gpu = instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => true);

        var desc = default(DeviceDescription);   // Queues = ReadOnlySpan<QueueRequest>.Empty

        Assert.Throws<ArgumentException>(() => gpu.CreateDevice(in desc));
    }

    [Fact]
    public void CreateDevice_BogusFamilyIndex_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        var gpu = instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => true);

        var desc = new DeviceDescription
        {
            Queues = [new QueueRequest(familyIndex: 99, count: 1, priority: 0.5f)],
        };

        var ex = Assert.Throws<ArgumentException>(() => gpu.CreateDevice(in desc));
        Assert.Contains("FamilyIndex 99", ex.Message);
    }

    [Fact]
    public void CreateDevice_QueueOversubscribed_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        uint gfxFamily = uint.MaxValue;
        uint gfxAvail  = 0;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    gfxFamily = info.QueueFamilies[i].Index;
                    gfxAvail  = info.QueueFamilies[i].QueueCount;
                    return true;
                }
            }
            return false;
        });

        var desc = new DeviceDescription
        {
            Queues = [new QueueRequest(gfxFamily, count: gfxAvail + 1, priority: 0.5f)],
        };

        var ex = Assert.Throws<ArgumentException>(() => gpu.CreateDevice(in desc));
        Assert.Contains("requests", ex.Message);
    }
```

- [ ] **Step 2: Run — expect 3 passing (or skipped without a driver).**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~DeviceTests"
```

- [ ] **Step 3: Commit.**

```
git add tests/Ahjo.Vulkan.Tests/DeviceTests.cs
git commit -m "Test: CreateDevice queue-request validation (issue 08)"
```

---

## Task 10: `GetQueue` cache-hit + miss tests

**Files:**
- Edit: `tests/Ahjo.Vulkan.Tests/DeviceTests.cs`

- [ ] **Step 1: Append.**

```csharp
    [Fact]
    public void CreateDevice_GetQueueByFamilyIndex_ReturnsCachedInstance()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        uint gfxFamily = uint.MaxValue;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    gfxFamily = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });

        using var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(gfxFamily, count: 1, priority: 1.0f)],
        });

        Queue a = device.GetQueue(gfxFamily, queueIndex: 0);
        Queue b = device.GetQueue(gfxFamily, queueIndex: 0);

        Assert.Same(a, b);
    }

    [Fact]
    public void CreateDevice_GetQueue_UnknownSlot_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        uint gfxFamily = uint.MaxValue;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    gfxFamily = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });

        using var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(gfxFamily, count: 1, priority: 1.0f)],
        });

        var ex = Assert.Throws<ArgumentException>(() => device.GetQueue(gfxFamily, queueIndex: 5));
        Assert.Contains("No queue requested", ex.Message);
    }
```

- [ ] **Step 2: Run — expect 2 passing.**

- [ ] **Step 3: Commit.**

```
git add tests/Ahjo.Vulkan.Tests/DeviceTests.cs
git commit -m "Test: Device.GetQueue cache + miss (issue 08)"
```

---

## Task 11: Feature-callback + extension passthrough + dispose

**Files:**
- Edit: `tests/Ahjo.Vulkan.Tests/DeviceTests.cs`

- [ ] **Step 1: Append.**

```csharp
    [Fact]
    public void CreateDevice_ConfigureFeaturesCallback_Invoked()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        uint gfxFamily = PickGraphicsFamily(instance, out var gpu);

        bool invoked = false;
        var desc = new DeviceDescription
        {
            Queues = [new QueueRequest(gfxFamily, count: 1, priority: 1.0f)],
            ConfigureFeatures = (ref ChainBuilder<VkDeviceCreateInfo> _) => invoked = true,
        };

        using var device = gpu.CreateDevice(in desc);
        Assert.True(invoked);
    }

    [Fact]
    public void CreateDevice_ExtensionList_PassedThrough()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        uint gfxFamily = PickGraphicsFamily(instance, out var gpu);

        // Pass an extension we know the device advertises (proven by issue #7's extension scan).
        // The wrapper plumbs the pointer through; success on vkCreateDevice means the chain
        // and pointer table were well-formed.
        Span<Utf8Name> exts = stackalloc Utf8Name[1];
        exts[0] = Utf8Name.FromLiteral(KhronosExtensionNames.KhrSwapchain);

        var desc = new DeviceDescription
        {
            Queues     = [new QueueRequest(gfxFamily, count: 1, priority: 1.0f)],
            Extensions = exts,
        };

        using var device = gpu.CreateDevice(in desc);
        Assert.False(device.IsNull);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        uint gfxFamily = PickGraphicsFamily(instance, out var gpu);

        var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(gfxFamily, count: 1, priority: 1.0f)],
        });

        device.Dispose();
        device.Dispose();   // must not throw
    }

    private static uint PickGraphicsFamily(Instance instance, out PhysicalDevice gpu)
    {
        uint family = uint.MaxValue;
        gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    family = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });
        return family;
    }
```

> The `PickGraphicsFamily` helper deduplicates the picker boilerplate from the previous tests; consider migrating earlier tests to it during a follow-up cleanup pass (out of scope for this issue's TDD).

- [ ] **Step 2: Run — expect 3 passing (or skipped).**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~DeviceTests"
```

- [ ] **Step 3: Run the whole suite — confirm no regressions anywhere.**

```
dotnet test
```

Expected: every test green or skipped (driver-gated on driverless hosts).

- [ ] **Step 4: Commit.**

```
git add tests/Ahjo.Vulkan.Tests/DeviceTests.cs
git commit -m "Test: CreateDevice feature callback + extensions + dispose (issue 08)"
```

---

## Final acceptance check

Issue #8 acceptance criteria from the GitHub issue body:

- [ ] **Criterion 1: Test creates a device with default 1.4 features + a graphics queue and confirms queue is non-null.**

`DeviceTests.CreateDevice_DefaultDescription_OneGraphicsQueue` — passes on a host with a driver.

- [ ] **Criterion 2: Disposal calls `vkDeviceWaitIdle` then `vkDestroyDevice`.**

`DeviceTests.Dispose_IsIdempotent` exercises the dispose path. The order (`vkDeviceWaitIdle` before `vkDestroyDevice`) is enforced by the body of `Device.Dispose`; the test asserts it doesn't throw and is idempotent. Visual code review of `Device.cs` covers the ordering invariant.

- [ ] **Final solution-wide test pass.**

```
dotnet test
```

All green except driver-gated skips.

- [ ] **Issue close-out.** Reference the commit range in a closing comment on GitHub issue #8.
