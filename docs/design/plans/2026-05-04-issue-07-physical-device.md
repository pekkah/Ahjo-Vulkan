# Issue #7 — PhysicalDevice selection + capabilities API — Implementation Plan

> **For agentic workers:** Execute this plan with the repo's `implementer` agent (`.claude/agents/implementer.md`) task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `instance.PickPhysicalDevice(picker)` for `Ahjo.Vulkan` — declarative physical-device selection with a zero-alloc `PhysicalDeviceInfo` view (properties, all four 1.x feature structs, memory, queue families, device extensions, name).

**Architecture:** `PhysicalDevice` is a `readonly struct` handle (`IVulkanHandle<PhysicalDevice>`); `PhysicalDeviceInfo` is a `readonly ref struct` over stack/pool scratch built once per candidate inside `PickPhysicalDevice`. The picker is a hand-declared `delegate bool PhysicalDevicePicker(in PhysicalDeviceInfo info)`. Capability reads use the Vulkan 1.1+ `*2` getters (statically dispatched on the 1.4 instance — no `vkGetInstanceProcAddr`). Surface-tied present support is deferred to the surface issue.

**Tech Stack:** .NET 10, C# 11+ (unsafe, `ref` fields in `ref struct`), xUnit v3, BenchmarkDotNet `[MemoryDiagnoser]`. Existing primitives: `IVulkanHandle<TSelf>` (#3), `ChainBuilder<TRoot>` (#4), `VkResult.ThrowIfFailed()` (#5), `Instance` (#6), `VulkanDriverProbe`.

**Spec:** `docs/design/specs/2026-05-04-issue-07-physical-device-design.md`. Read it before starting.

---

## File map

| Path | Kind | Purpose |
|---|---|---|
| `src/Ahjo.Vulkan/Lifecycle/QueueFamilyInfo.cs` | new | Bit-test getters over `VkQueueFlagBits`. |
| `src/Ahjo.Vulkan/Lifecycle/PhysicalDevice.cs` | new | `readonly struct` handle; implements `IVulkanHandle<PhysicalDevice>`. |
| `src/Ahjo.Vulkan/Lifecycle/KhronosExtensionNames.cs` | new | Public UTF-8 byte literals; ships only `KhrSwapchain` this issue. |
| `src/Ahjo.Vulkan/Lifecycle/PhysicalDevicePicker.cs` | new | The `delegate bool PhysicalDevicePicker(in PhysicalDeviceInfo info)` declaration. |
| `src/Ahjo.Vulkan/Lifecycle/PhysicalDeviceInfo.cs` | new | `readonly ref struct` with the picker's view + `SupportsExtension`. |
| `src/Ahjo.Vulkan/Lifecycle/Instance.cs` | edit | Add `PickPhysicalDevice` method. Existing members untouched. |
| `tests/Ahjo.Vulkan.Tests/QueueFamilyInfoTests.cs` | new | Pure unit tests for the bit-test getters. |
| `tests/Ahjo.Vulkan.Tests/PhysicalDeviceInfoTests.cs` | new | Pure unit tests for `SupportsExtension`. |
| `tests/Ahjo.Vulkan.Tests/PhysicalDeviceTests.cs` | new | Driver-gated integration tests for `PickPhysicalDevice`. |
| `tests/Ahjo.Vulkan.Tests/HandleConventionsTests.cs` | edit | Add `PhysicalDevice` to the existing `IVulkanHandle` round-trip suite. |
| `tests/Ahjo.Vulkan.Benchmarks/PhysicalDevicePickerBenchmark.cs` | new | `[MemoryDiagnoser]` proof of zero-alloc picker round-trip. |

Tasks build bottom-up — pure value types first, then the keystone method, then driver-gated regression tests, then the benchmark.

---

## Task 1: `QueueFamilyInfo`

**Files:**
- Create: `src/Ahjo.Vulkan/Lifecycle/QueueFamilyInfo.cs`
- Test: `tests/Ahjo.Vulkan.Tests/QueueFamilyInfoTests.cs`

- [ ] **Step 1: Write the failing tests.**

Write `tests/Ahjo.Vulkan.Tests/QueueFamilyInfoTests.cs`:

```csharp
using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed class QueueFamilyInfoTests
{
    [Fact]
    public void Flags_GraphicsBitSet_SupportsGraphicsTrue()
    {
        var info = new QueueFamilyInfo(
            index: 0,
            flags: VkQueueFlagBits.VK_QUEUE_GRAPHICS_BIT,
            queueCount: 1,
            timestampValidBits: 0,
            minImageTransferGranularity: default);

        Assert.True (info.SupportsGraphics);
        Assert.False(info.SupportsCompute);
        Assert.False(info.SupportsTransfer);
        Assert.False(info.SupportsSparseBinding);
    }

    [Fact]
    public void Flags_AllZero_AllSupportsFalse()
    {
        var info = new QueueFamilyInfo(0, 0u, 0, 0, default);

        Assert.False(info.SupportsGraphics);
        Assert.False(info.SupportsCompute);
        Assert.False(info.SupportsTransfer);
        Assert.False(info.SupportsSparseBinding);
    }

    [Fact]
    public void Flags_GraphicsAndCompute_BothBitsRead()
    {
        var both = VkQueueFlagBits.VK_QUEUE_GRAPHICS_BIT | VkQueueFlagBits.VK_QUEUE_COMPUTE_BIT;
        var info = new QueueFamilyInfo(0, both, 1, 0, default);

        Assert.True (info.SupportsGraphics);
        Assert.True (info.SupportsCompute);
        Assert.False(info.SupportsTransfer);
    }
}
```

- [ ] **Step 2: Run the test — expect compile failure.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~QueueFamilyInfoTests"
```

Expected: build error — `QueueFamilyInfo` is not defined.

- [ ] **Step 3: Implement `QueueFamilyInfo`.**

Write `src/Ahjo.Vulkan/Lifecycle/QueueFamilyInfo.cs`:

```csharp
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Snapshot of one <c>VkQueueFamilyProperties</c> entry. Plain <c>struct</c>
/// — pure value data the picker reads via bit-test getters. The
/// <see cref="Index"/> is the family's position in
/// <c>vkGetPhysicalDeviceQueueFamilyProperties2</c>'s output array.
/// </summary>
/// <remarks>
/// No <c>SupportsPresent</c>: present is per-surface in core Vulkan and the
/// surface API is out of scope for issue #7. The surface/swapchain issue
/// composes a present check on top of <see cref="Index"/>.
/// </remarks>
public readonly struct QueueFamilyInfo
{
    public readonly uint         Index;
    public readonly VkQueueFlagBits Flags;
    public readonly uint         QueueCount;
    public readonly uint         TimestampValidBits;
    public readonly VkExtent3D   MinImageTransferGranularity;

    public QueueFamilyInfo(
        uint         index,
        VkQueueFlagBits flags,
        uint         queueCount,
        uint         timestampValidBits,
        VkExtent3D   minImageTransferGranularity)
    {
        Index                       = index;
        Flags                       = flags;
        QueueCount                  = queueCount;
        TimestampValidBits          = timestampValidBits;
        MinImageTransferGranularity = minImageTransferGranularity;
    }

    public bool SupportsGraphics      => (Flags & VkQueueFlagBits.VK_QUEUE_GRAPHICS_BIT)       != 0;
    public bool SupportsCompute       => (Flags & VkQueueFlagBits.VK_QUEUE_COMPUTE_BIT)        != 0;
    public bool SupportsTransfer      => (Flags & VkQueueFlagBits.VK_QUEUE_TRANSFER_BIT)       != 0;
    public bool SupportsSparseBinding => (Flags & VkQueueFlagBits.VK_QUEUE_SPARSE_BINDING_BIT) != 0;
}
```

- [ ] **Step 4: Run the tests — expect 3 passing.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~QueueFamilyInfoTests"
```

- [ ] **Step 5: Commit.**

```
git add src/Ahjo.Vulkan/Lifecycle/QueueFamilyInfo.cs tests/Ahjo.Vulkan.Tests/QueueFamilyInfoTests.cs
git commit -m "Add QueueFamilyInfo bit-test struct (issue 07)"
```

---

## Task 2: `PhysicalDevice` handle struct

**Files:**
- Create: `src/Ahjo.Vulkan/Lifecycle/PhysicalDevice.cs`
- Modify: `tests/Ahjo.Vulkan.Tests/HandleConventionsTests.cs`

- [ ] **Step 1: Add a failing test for `PhysicalDevice` to the existing handle suite.**

Open `tests/Ahjo.Vulkan.Tests/HandleConventionsTests.cs` and append two new `[Fact]` methods at the end of the class (just before the two private helpers `MakeFromRaw` / `ObjectTypeOf`):

```csharp
    [Fact]
    public void PhysicalDevice_DefaultHandleIsNull()
    {
        PhysicalDevice empty = default;
        Assert.True(empty.IsNull);
        Assert.Equal(0UL, empty.RawHandle);
    }

    [Fact]
    public void PhysicalDevice_FromRaw_RoundTrips()
    {
        nint raw = 0x9A_BCDE_F012;
        PhysicalDevice gpu = MakeFromRaw<PhysicalDevice>(raw);

        Assert.False(gpu.IsNull);
        Assert.Equal((ulong)raw, gpu.RawHandle);
        Assert.Equal(VkObjectType.VK_OBJECT_TYPE_PHYSICAL_DEVICE, ObjectTypeOf<PhysicalDevice>());
    }
```

- [ ] **Step 2: Run the test — expect compile failure.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~HandleConventionsTests"
```

Expected: build error — `PhysicalDevice` is not defined.

- [ ] **Step 3: Implement `PhysicalDevice`.**

Write `src/Ahjo.Vulkan/Lifecycle/PhysicalDevice.cs`:

```csharp
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Wrapper handle for a <c>VkPhysicalDevice</c>. Owned by a
/// <see cref="Instance"/>; has no <c>vkDestroyPhysicalDevice</c> call, so no
/// <see cref="System.IDisposable"/>, no finalizer, copy-by-value.
/// </summary>
public readonly unsafe struct PhysicalDevice : IVulkanHandle<PhysicalDevice>
{
    internal readonly VkPhysicalDevice_T* Handle;

    public PhysicalDevice(VkPhysicalDevice_T* handle) => Handle = handle;

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_PHYSICAL_DEVICE;

    public static PhysicalDevice FromRaw(nint handle) => new((VkPhysicalDevice_T*)handle);

    public ulong RawHandle => (ulong)(nint)Handle;

    public bool IsNull => Handle == null;
}
```

- [ ] **Step 4: Run the tests — expect all `HandleConventionsTests` passing (existing 3 + new 2 = 5).**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~HandleConventionsTests"
```

- [ ] **Step 5: Commit.**

```
git add src/Ahjo.Vulkan/Lifecycle/PhysicalDevice.cs tests/Ahjo.Vulkan.Tests/HandleConventionsTests.cs
git commit -m "Add PhysicalDevice handle struct (issue 07)"
```

---

## Task 3: `KhronosExtensionNames` + `PhysicalDevicePicker` delegate

These two are one-line public surface declarations. No dedicated tests — they're exercised end-to-end in Task 5+.

**Files:**
- Create: `src/Ahjo.Vulkan/Lifecycle/KhronosExtensionNames.cs`
- Create: `src/Ahjo.Vulkan/Lifecycle/PhysicalDevicePicker.cs`

- [ ] **Step 1: Write `KhronosExtensionNames`.**

```csharp
namespace Ahjo.Vulkan;

/// <summary>
/// Public UTF-8 byte literals for the Khronos device-extension names this
/// wrapper hard-codes. Centralised so a typo can only be made in one place;
/// callers compose pickers with these constants. Populated lazily — entries
/// are added as the wrapper grows extension-aware features.
/// </summary>
/// <remarks>
/// The C# spec guarantees <c>"…"u8</c> literals live in the assembly's
/// read-only data segment for the lifetime of the process and are
/// followed by an out-of-bounds NUL byte, so the address of the span's
/// first element is safe to pass to a Vulkan API expecting
/// <c>const char*</c>.
/// </remarks>
public static class KhronosExtensionNames
{
    /// <summary><c>VK_KHR_swapchain</c> — required for any device that will present.</summary>
    public static ReadOnlySpan<byte> KhrSwapchain => "VK_KHR_swapchain"u8;
}
```

- [ ] **Step 2: Write `PhysicalDevicePicker`.**

```csharp
namespace Ahjo.Vulkan;

/// <summary>
/// User-supplied predicate that decides whether a candidate physical
/// device satisfies the caller's requirements. Hand-declared (cannot be
/// <see cref="System.Func{T1,T2}"/>) because the parameter is <c>in</c> of
/// a <c>ref struct</c>.
/// </summary>
/// <param name="info">View over the candidate's properties, features,
/// memory, queue families, and device extensions. Backed by stack and
/// pooled scratch owned by <see cref="Instance.PickPhysicalDevice"/>; do
/// not stash any references that escape the picker call.</param>
/// <returns><see langword="true"/> to select this candidate;
/// <see langword="false"/> to keep searching.</returns>
public delegate bool PhysicalDevicePicker(in PhysicalDeviceInfo info);
```

> Note: this file references `PhysicalDeviceInfo`, defined in Task 4. The compile will fail at the end of this task and pass once Task 4 lands. That's acceptable for a paired task; the alternative is one combined task.

- [ ] **Step 3: Verify both files compile in isolation.** They reference `PhysicalDeviceInfo` (Task 4) so a full build will fail; that's expected. Run a dry compile of just the wrapper project to confirm there are no other errors:

```
dotnet build src/Ahjo.Vulkan/Ahjo.Vulkan.csproj
```

Expected: a single error about `PhysicalDeviceInfo` not being found, originating from `PhysicalDevicePicker.cs`. No errors from `KhronosExtensionNames.cs`.

- [ ] **Step 4: Do not commit yet.** Leave the partial state on disk; Task 4 will close it.

---

## Task 4: `PhysicalDeviceInfo` + `SupportsExtension`

The keystone view type. `SupportsExtension` is the only behaviour worth unit-testing in isolation; everything else is a field exposure.

**Files:**
- Create: `src/Ahjo.Vulkan/Lifecycle/PhysicalDeviceInfo.cs`
- Test: `tests/Ahjo.Vulkan.Tests/PhysicalDeviceInfoTests.cs`

- [ ] **Step 1: Write the failing test.**

Write `tests/Ahjo.Vulkan.Tests/PhysicalDeviceInfoTests.cs`:

```csharp
using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed unsafe class PhysicalDeviceInfoTests
{
    [Fact]
    public void SupportsExtension_KnownName_ReturnsTrue()
    {
        Assert.True(BuildAndQuery("VK_TEST_known"u8, "VK_TEST_known"u8));
    }

    [Fact]
    public void SupportsExtension_UnknownName_ReturnsFalse()
    {
        Assert.False(BuildAndQuery("VK_TEST_known"u8, "VK_TEST_other"u8));
    }

    [Fact]
    public void SupportsExtension_PrefixOnly_ReturnsFalse()
    {
        // Querying "VK_TEST" against a buffer holding "VK_TEST_known"
        // must reject because the buffer's NUL is at position 13, not 7.
        Assert.False(BuildAndQuery("VK_TEST_known"u8, "VK_TEST"u8));
    }

    [Fact]
    public void SupportsExtension_EmptyExtensionList_ReturnsFalse()
    {
        var props = default(VkPhysicalDeviceProperties);
        var feats = default(VkPhysicalDeviceFeatures);
        var f11   = default(VkPhysicalDeviceVulkan11Features);
        var f12   = default(VkPhysicalDeviceVulkan12Features);
        var f13   = default(VkPhysicalDeviceVulkan13Features);
        var f14   = default(VkPhysicalDeviceVulkan14Features);
        var mem   = default(VkPhysicalDeviceMemoryProperties);
        Span<VkExtensionProperties> exts  = [];
        Span<QueueFamilyInfo>       qfs   = [];

        var info = new PhysicalDeviceInfo(
            device: default, properties: in props, features: in feats,
            features11: in f11, features12: in f12, features13: in f13, features14: in f14,
            memory: in mem, queueFamilies: qfs, extensions: exts, name: default);

        Assert.False(info.SupportsExtension("VK_KHR_swapchain"u8));
    }

    private static bool BuildAndQuery(ReadOnlySpan<byte> bufferContent, ReadOnlySpan<byte> query)
    {
        var ext = default(VkExtensionProperties);
        for (int i = 0; i < bufferContent.Length; i++)
            ext.extensionName[i] = (sbyte)bufferContent[i];
        // 256-byte buffer was zero-initialised → NUL terminator already in place at bufferContent.Length.

        Span<VkExtensionProperties> exts = [ext];
        Span<QueueFamilyInfo>       qfs  = [];

        var props = default(VkPhysicalDeviceProperties);
        var feats = default(VkPhysicalDeviceFeatures);
        var f11   = default(VkPhysicalDeviceVulkan11Features);
        var f12   = default(VkPhysicalDeviceVulkan12Features);
        var f13   = default(VkPhysicalDeviceVulkan13Features);
        var f14   = default(VkPhysicalDeviceVulkan14Features);
        var mem   = default(VkPhysicalDeviceMemoryProperties);

        var info = new PhysicalDeviceInfo(
            device: default, properties: in props, features: in feats,
            features11: in f11, features12: in f12, features13: in f13, features14: in f14,
            memory: in mem, queueFamilies: qfs, extensions: exts, name: default);

        return info.SupportsExtension(query);
    }
}
```

- [ ] **Step 2: Run the tests — expect compile failure.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~PhysicalDeviceInfoTests"
```

Expected: build error — `PhysicalDeviceInfo` is not defined.

- [ ] **Step 3: Implement `PhysicalDeviceInfo`.**

Write `src/Ahjo.Vulkan/Lifecycle/PhysicalDeviceInfo.cs`:

```csharp
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// View handed to a <see cref="PhysicalDevicePicker"/>. Holds
/// <c>ref readonly</c> references and <see cref="ReadOnlySpan{T}"/> views
/// into stack / pooled scratch owned by
/// <see cref="Instance.PickPhysicalDevice"/>. Cannot escape the picker call.
/// </summary>
public readonly ref struct PhysicalDeviceInfo
{
    public readonly PhysicalDevice                                 Device;
    public readonly ref readonly VkPhysicalDeviceProperties        Properties;
    public readonly ref readonly VkPhysicalDeviceFeatures          Features;
    public readonly ref readonly VkPhysicalDeviceVulkan11Features  Features11;
    public readonly ref readonly VkPhysicalDeviceVulkan12Features  Features12;
    public readonly ref readonly VkPhysicalDeviceVulkan13Features  Features13;
    public readonly ref readonly VkPhysicalDeviceVulkan14Features  Features14;
    public readonly ref readonly VkPhysicalDeviceMemoryProperties  Memory;

    public readonly ReadOnlySpan<QueueFamilyInfo>       QueueFamilies;
    public readonly ReadOnlySpan<VkExtensionProperties> Extensions;
    public readonly ReadOnlySpan<byte>                  Name;

    public PhysicalDeviceInfo(
        PhysicalDevice                                Device,
        in VkPhysicalDeviceProperties                 Properties,
        in VkPhysicalDeviceFeatures                   Features,
        in VkPhysicalDeviceVulkan11Features           Features11,
        in VkPhysicalDeviceVulkan12Features           Features12,
        in VkPhysicalDeviceVulkan13Features           Features13,
        in VkPhysicalDeviceVulkan14Features           Features14,
        in VkPhysicalDeviceMemoryProperties           Memory,
        ReadOnlySpan<QueueFamilyInfo>                 QueueFamilies,
        ReadOnlySpan<VkExtensionProperties>           Extensions,
        ReadOnlySpan<byte>                            Name)
    {
        this.Device        = Device;
        this.Properties    = ref Properties;
        this.Features      = ref Features;
        this.Features11    = ref Features11;
        this.Features12    = ref Features12;
        this.Features13    = ref Features13;
        this.Features14    = ref Features14;
        this.Memory        = ref Memory;
        this.QueueFamilies = QueueFamilies;
        this.Extensions    = Extensions;
        this.Name          = Name;
    }

    public VkPhysicalDeviceType Type => Properties.deviceType;

    /// <summary>
    /// Linear-scan check for a NUL-terminated UTF-8 name in
    /// <see cref="Extensions"/>. Allocation-free.
    /// </summary>
    public unsafe bool SupportsExtension(ReadOnlySpan<byte> utf8Name)
    {
        if (Extensions.IsEmpty) return false;

        fixed (VkExtensionProperties* exts = Extensions)
        {
            for (int i = 0; i < Extensions.Length; i++)
            {
                if (NameEquals((sbyte*)&exts[i].extensionName.e0, utf8Name)) return true;
            }
        }
        return false;
    }

    private static unsafe bool NameEquals(sbyte* name, ReadOnlySpan<byte> target)
    {
        for (int i = 0; i < target.Length; i++)
        {
            if (name[i] == 0 || (byte)name[i] != target[i]) return false;
        }
        return name[target.Length] == 0;
    }
}
```

> Constructor parameter names are camelCase (`device`, `properties`, `features11` …) — the field names stay PascalCase. C# is fine with assigning across the case difference inside the body; the spec §3 named-arg call sites use the camelCase form, so this is the canonical spelling and matches the rest of the codebase (`QueueFamilyInfo`'s ctor follows the same convention).

- [ ] **Step 4: Run the tests — expect 4 passing in `PhysicalDeviceInfoTests`.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~PhysicalDeviceInfoTests"
```

- [ ] **Step 5: Commit Tasks 3 + 4 together** (the partial Task 3 state is now closed by `PhysicalDeviceInfo`).

```
git add src/Ahjo.Vulkan/Lifecycle/KhronosExtensionNames.cs \
        src/Ahjo.Vulkan/Lifecycle/PhysicalDevicePicker.cs \
        src/Ahjo.Vulkan/Lifecycle/PhysicalDeviceInfo.cs \
        tests/Ahjo.Vulkan.Tests/PhysicalDeviceInfoTests.cs
git commit -m "Add PhysicalDeviceInfo + picker delegate + extension-name constants (issue 07)"
```

---

## Task 5: `Instance.PickPhysicalDevice` + smoke test

The keystone method. Implements the full body from spec §3 and verifies it round-trips with an accept-all picker.

**Files:**
- Modify: `src/Ahjo.Vulkan/Lifecycle/Instance.cs` (add one method, no other changes)
- Create: `tests/Ahjo.Vulkan.Tests/PhysicalDeviceTests.cs`

- [ ] **Step 1: Write the failing smoke test.**

Write `tests/Ahjo.Vulkan.Tests/PhysicalDeviceTests.cs`:

```csharp
using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed unsafe class PhysicalDeviceTests
{
    [Fact]
    public void Pick_AcceptAny_ReturnsFirstDevice()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        PhysicalDevice gpu = instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => true);

        Assert.False(gpu.IsNull);
    }
}
```

- [ ] **Step 2: Run the test — expect compile failure.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~PhysicalDeviceTests"
```

Expected: build error — `Instance.PickPhysicalDevice` is not defined.

- [ ] **Step 3: Implement `PickPhysicalDevice` on `Instance`.**

Open `src/Ahjo.Vulkan/Lifecycle/Instance.cs`. Add `using System.Buffers;` to the top. Insert the following method into the `Instance` class — between the `Create` method and the `AllSeverities` constant. Do not modify any existing member.

```csharp
    /// <summary>
    /// Walks the host's physical devices and returns the first one for
    /// which <paramref name="picker"/> returns <see langword="true"/>. The
    /// <see cref="PhysicalDeviceInfo"/> handed to the picker is a view
    /// over scratch owned by this call; do not stash references that
    /// escape it.
    /// </summary>
    /// <exception cref="VulkanException">No physical devices reported, or
    /// no candidate satisfied the picker.</exception>
    /// <remarks>Assumes the instance was created with
    /// <c>apiVersion &gt;= 1.1</c> (the default in
    /// <see cref="InstanceDescription"/> is 1.4). On a pre-1.1 instance
    /// the chained 1.x feature structs would silently read back as
    /// zero.</remarks>
    public PhysicalDevice PickPhysicalDevice(PhysicalDevicePicker picker)
    {
        ArgumentNullException.ThrowIfNull(picker);

        // 1. Enumerate device handles.
        uint count = 0;
        Vk.vkEnumeratePhysicalDevices(Handle, &count, null).ThrowIfFailed();
        if (count == 0)
            throw new VulkanException(VkResult.VK_ERROR_INITIALIZATION_FAILED,
                "No Vulkan physical devices reported by the driver.");

        Span<nint> deviceHandles = count <= 16
            ? stackalloc nint[(int)count]
            : new nint[count];
        fixed (nint* p = deviceHandles)
            Vk.vkEnumeratePhysicalDevices(Handle, &count, (VkPhysicalDevice_T**)p).ThrowIfFailed();

        // 2. Reusable per-device scratch.
        Span<byte>                         propsChain    = stackalloc byte[1024];
        Span<byte>                         featuresChain = stackalloc byte[1024];
        Span<VkQueueFamilyProperties2>     queueScratch  = stackalloc VkQueueFamilyProperties2[16];
        Span<QueueFamilyInfo>              queueViews    = stackalloc QueueFamilyInfo[16];
        VkPhysicalDeviceMemoryProperties2  memory        = default;
        memory.sType = VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_MEMORY_PROPERTIES_2;

        var extPool = ArrayPool<VkExtensionProperties>.Shared;
        VkExtensionProperties[] extBuf = [];

        try
        {
            for (int i = 0; i < (int)count; i++)
            {
                var d = (VkPhysicalDevice_T*)deviceHandles[i];

                // 2a. Properties chain (root only).
                propsChain.Clear();
                var pchain = ChainBuilder.For<VkPhysicalDeviceProperties2>(propsChain);
                pchain.Root();
                Vk.vkGetPhysicalDeviceProperties2(d, pchain.Head);

                // 2b. Features chain — base + 1.1/1.2/1.3/1.4.
                featuresChain.Clear();
                var fchain = ChainBuilder.For<VkPhysicalDeviceFeatures2>(featuresChain);
                fchain.Root();
                ref var f11 = ref fchain.Push<VkPhysicalDeviceVulkan11Features>();
                ref var f12 = ref fchain.Push<VkPhysicalDeviceVulkan12Features>();
                ref var f13 = ref fchain.Push<VkPhysicalDeviceVulkan13Features>();
                ref var f14 = ref fchain.Push<VkPhysicalDeviceVulkan14Features>();
                Vk.vkGetPhysicalDeviceFeatures2(d, fchain.Head);

                // 2c. Memory. `memory` is a stack local — `fixed` is neither
                // legal nor needed; `&memory` is already a stable pointer.
                Vk.vkGetPhysicalDeviceMemoryProperties2(d, &memory);

                // 2d. Queue families.
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
                        flags: (VkQueueFlagBits)src.queueFlags,
                        queueCount: src.queueCount,
                        timestampValidBits: src.timestampValidBits,
                        minImageTransferGranularity: src.minImageTransferGranularity);
                }

                // 2e. Device extensions — pool-rent, grow once across iterations.
                uint extCount = 0;
                Vk.vkEnumerateDeviceExtensionProperties(d, null, &extCount, null).ThrowIfFailed();
                if (extBuf.Length < extCount)
                {
                    if (extBuf.Length != 0) extPool.Return(extBuf);
                    extBuf = extCount == 0 ? [] : extPool.Rent((int)extCount);
                }
                if (extCount > 0)
                {
                    fixed (VkExtensionProperties* ep = extBuf)
                        Vk.vkEnumerateDeviceExtensionProperties(d, null, &extCount, ep).ThrowIfFailed();
                }

                // 2f. Build the picker view and dispatch.
                ref var props2 = ref *pchain.Head;
                ref var feats2 = ref *fchain.Head;
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

    private static ReadOnlySpan<byte> NameSlice(in VkPhysicalDeviceProperties props)
    {
        // Treat the 256-byte fixed deviceName buffer as a span and slice it
        // at the first NUL. ref readonly + Unsafe is the friction-free path
        // to a span over the inline array.
        ref readonly var first = ref props.deviceName.e0;
        ReadOnlySpan<sbyte> raw = MemoryMarshal.CreateReadOnlySpan(in first, 256);
        ReadOnlySpan<byte>  asBytes = MemoryMarshal.Cast<sbyte, byte>(raw);
        int nul = asBytes.IndexOf((byte)0);
        return nul < 0 ? asBytes : asBytes[..nul];
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void ThrowQueueOverflow(uint count) =>
        throw new VulkanException(VkResult.VK_ERROR_INITIALIZATION_FAILED,
            $"Physical device reports {count} queue families; wrapper ceiling is 16. " +
            "File an issue if you see this on real hardware.");
```

- [ ] **Step 4: Run the smoke test — expect 1 passing (or skipped if no driver).**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~PhysicalDeviceTests.Pick_AcceptAny_ReturnsFirstDevice"
```

Expected: 1 passed (or 1 skipped if no Vulkan driver). If passed, also verify the existing test suite still passes:

```
dotnet test tests/Ahjo.Vulkan.Tests
```

Expected: all green.

- [ ] **Step 5: Commit.**

```
git add src/Ahjo.Vulkan/Lifecycle/Instance.cs tests/Ahjo.Vulkan.Tests/PhysicalDeviceTests.cs
git commit -m "Add Instance.PickPhysicalDevice + smoke test (issue 07)"
```

---

## Task 6: `Pick_NoMatch_Throws`

Regression test: picker returning false everywhere yields the "no device matched" `VulkanException`.

**Files:**
- Modify: `tests/Ahjo.Vulkan.Tests/PhysicalDeviceTests.cs`

- [ ] **Step 1: Append the test to `PhysicalDeviceTests`.**

```csharp
    [Fact]
    public void Pick_NoMatch_Throws()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        var ex = Assert.Throws<VulkanException>(() =>
            instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => false));

        Assert.Contains("No physical device matched", ex.Message);
    }
```

- [ ] **Step 2: Run the test — expect 1 passing.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~PhysicalDeviceTests.Pick_NoMatch_Throws"
```

- [ ] **Step 3: Commit.**

```
git add tests/Ahjo.Vulkan.Tests/PhysicalDeviceTests.cs
git commit -m "Test: PickPhysicalDevice throws on no-match (issue 07)"
```

---

## Task 7: `Pick_DriverVersion_NonZero` + `Pick_NameSpan_RoundTripsToString`

Verifies the properties chain is wired correctly and the device-name slice strips the trailing NUL.

**Files:**
- Modify: `tests/Ahjo.Vulkan.Tests/PhysicalDeviceTests.cs`

- [ ] **Step 1: Append the tests to `PhysicalDeviceTests`.**

```csharp
    [Fact]
    public void Pick_DriverVersion_NonZero()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        uint observedDriverVersion = 0;
        var picker = (PhysicalDevicePicker)((in PhysicalDeviceInfo info) =>
        {
            observedDriverVersion = info.Properties.driverVersion;
            return true;
        });

        instance.PickPhysicalDevice(picker);

        Assert.NotEqual(0u, observedDriverVersion);
    }

    [Fact]
    public void Pick_NameSpan_RoundTripsToString()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        byte[] capturedName = [];
        var picker = (PhysicalDevicePicker)((in PhysicalDeviceInfo info) =>
        {
            capturedName = info.Name.ToArray();   // copy out — span itself can't escape
            return true;
        });

        PhysicalDevice gpu = instance.PickPhysicalDevice(picker);

        // Spec invariant: deviceName is a non-empty UTF-8 string and Name
        // strips the trailing NUL. Cross-check by re-querying the raw API
        // and computing the same slice.
        var props = default(VkPhysicalDeviceProperties);
        Vk.vkGetPhysicalDeviceProperties(gpu.Handle, &props);
        int nulOffset = 0;
        while (nulOffset < 256 && props.deviceName[nulOffset] != 0) nulOffset++;

        Assert.True(nulOffset > 0, "VkPhysicalDeviceProperties.deviceName was empty.");
        Assert.Equal(nulOffset, capturedName.Length);
        for (int i = 0; i < nulOffset; i++)
            Assert.Equal((byte)props.deviceName[i], capturedName[i]);
    }
```

> **Why a non-`static` lambda for the picker:** the test needs to capture state from inside the picker (`observedDriverVersion`, `capturedName`). Tests are not on a hot path — capturing closures here is fine. Production callers that care about zero-alloc dispatch use `static` lambdas.

- [ ] **Step 2: Run the tests — expect 2 passing.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~PhysicalDeviceTests.Pick_DriverVersion_NonZero|FullyQualifiedName~PhysicalDeviceTests.Pick_NameSpan_RoundTripsToString"
```

- [ ] **Step 3: Commit.**

```
git add tests/Ahjo.Vulkan.Tests/PhysicalDeviceTests.cs
git commit -m "Test: PickPhysicalDevice properties + name slice (issue 07)"
```

---

## Task 8: `Pick_QueueFamiliesNeverEmpty` + `Pick_PicksDeviceWithGraphicsQueue`

Verifies the queue-family materialisation end-to-end.

**Files:**
- Modify: `tests/Ahjo.Vulkan.Tests/PhysicalDeviceTests.cs`

- [ ] **Step 1: Append the tests to `PhysicalDeviceTests`.**

```csharp
    [Fact]
    public void Pick_QueueFamiliesNeverEmpty()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        int observedFamilyCount = -1;
        var picker = (PhysicalDevicePicker)((in PhysicalDeviceInfo info) =>
        {
            observedFamilyCount = info.QueueFamilies.Length;
            return true;
        });

        instance.PickPhysicalDevice(picker);

        Assert.True(observedFamilyCount >= 1,
            $"Vulkan spec guarantees ≥1 queue family per device; saw {observedFamilyCount}.");
    }

    [Fact]
    public void Pick_PicksDeviceWithGraphicsQueue()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        bool observedGraphicsFamily = false;
        var picker = (PhysicalDevicePicker)((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    observedGraphicsFamily = true;
                    return true;
                }
            }
            return false;
        });

        PhysicalDevice gpu = instance.PickPhysicalDevice(picker);

        Assert.False(gpu.IsNull);
        Assert.True(observedGraphicsFamily);
    }
```

- [ ] **Step 2: Run the tests — expect 2 passing.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~PhysicalDeviceTests.Pick_QueueFamiliesNeverEmpty|FullyQualifiedName~PhysicalDeviceTests.Pick_PicksDeviceWithGraphicsQueue"
```

- [ ] **Step 3: Commit.**

```
git add tests/Ahjo.Vulkan.Tests/PhysicalDeviceTests.cs
git commit -m "Test: PickPhysicalDevice queue families (issue 07)"
```

---

## Task 9: `Pick_ExtensionsContainsCommon`

Verifies the extension-list materialisation and `SupportsExtension` linear scan against a real driver.

**Files:**
- Modify: `tests/Ahjo.Vulkan.Tests/PhysicalDeviceTests.cs`

- [ ] **Step 1: Append the test.**

```csharp
    [Fact]
    public void Pick_ExtensionsContainsCommon()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        bool observedMaintenance1 = false;
        bool observedFakeExtension = false;
        var picker = (PhysicalDevicePicker)((in PhysicalDeviceInfo info) =>
        {
            // VK_KHR_maintenance1 promoted to core in 1.1 but still
            // reported as a device extension on every shipping driver.
            observedMaintenance1  = info.SupportsExtension("VK_KHR_maintenance1"u8);
            observedFakeExtension = info.SupportsExtension("VK_FAKE_does_not_exist"u8);
            return true;
        });

        instance.PickPhysicalDevice(picker);

        Assert.True (observedMaintenance1,
            "Every shipping Vulkan driver advertises VK_KHR_maintenance1.");
        Assert.False(observedFakeExtension);
    }
```

- [ ] **Step 2: Run the test — expect 1 passing.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~PhysicalDeviceTests.Pick_ExtensionsContainsCommon"
```

> If this test fails on a particularly minimal driver (some software rasterisers omit `VK_KHR_maintenance1`), substitute another universally-present extension name like `"VK_KHR_storage_buffer_storage_class"u8` or `"VK_KHR_dedicated_allocation"u8`. Update the failure message accordingly.

- [ ] **Step 3: Commit.**

```
git add tests/Ahjo.Vulkan.Tests/PhysicalDeviceTests.cs
git commit -m "Test: PickPhysicalDevice extension scan (issue 07)"
```

---

## Task 10: `Pick_Vulkan13Features_Readable`

Confirms the features chain extends all the way to `Vulkan13Features` (and by implication, `11/12/14` too — they sit between `Features2` and `13` in the chain).

**Files:**
- Modify: `tests/Ahjo.Vulkan.Tests/PhysicalDeviceTests.cs`

- [ ] **Step 1: Append the test.**

```csharp
    [Fact]
    public void Pick_Vulkan13Features_Readable()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        bool pickerInvoked = false;
        uint dynamicRendering = 0;
        var picker = (PhysicalDevicePicker)((in PhysicalDeviceInfo info) =>
        {
            pickerInvoked   = true;
            dynamicRendering = info.Features13.dynamicRendering;
            // No assertion on the value — software rasterisers may report
            // 0; the test only proves the chain was wired.
            return true;
        });

        instance.PickPhysicalDevice(picker);

        Assert.True(pickerInvoked);
        Assert.True(dynamicRendering == 0u || dynamicRendering == 1u,
            "VkBool32 must be 0 or 1.");
    }
```

- [ ] **Step 2: Run the test — expect 1 passing.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~PhysicalDeviceTests.Pick_Vulkan13Features_Readable"
```

- [ ] **Step 3: Commit.**

```
git add tests/Ahjo.Vulkan.Tests/PhysicalDeviceTests.cs
git commit -m "Test: PickPhysicalDevice features chain reaches 1.3 (issue 07)"
```

---

## Task 11: `Pick_PrefersDiscrete_OrFallsBack`

Integration smoke that exercises the `info.Type` path and the no-match-throw + retry pattern.

**Files:**
- Modify: `tests/Ahjo.Vulkan.Tests/PhysicalDeviceTests.cs`

- [ ] **Step 1: Append the test.**

```csharp
    [Fact]
    public void Pick_PrefersDiscrete_OrFallsBack()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        // First pass: prefer discrete.
        PhysicalDevice gpu;
        try
        {
            gpu = instance.PickPhysicalDevice(static (in PhysicalDeviceInfo info) =>
                info.Type == VkPhysicalDeviceType.VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU);
        }
        catch (VulkanException)
        {
            // No discrete GPU on this host (CI / llvmpipe). Fall back to any.
            gpu = instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => true);
        }

        Assert.False(gpu.IsNull);
    }
```

- [ ] **Step 2: Run the test — expect 1 passing.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~PhysicalDeviceTests.Pick_PrefersDiscrete_OrFallsBack"
```

- [ ] **Step 3: Run the full test suite to confirm no regression.**

```
dotnet test
```

Expected: all green; only driver-gated tests are skipped on driverless hosts.

- [ ] **Step 4: Commit.**

```
git add tests/Ahjo.Vulkan.Tests/PhysicalDeviceTests.cs
git commit -m "Test: PickPhysicalDevice prefer-discrete fallback (issue 07)"
```

---

## Task 12: `PhysicalDevicePickerBenchmark`

The `[MemoryDiagnoser]` proof of acceptance criterion 2 ("picker round-trip allocates 0 bytes").

**Files:**
- Create: `tests/Ahjo.Vulkan.Benchmarks/PhysicalDevicePickerBenchmark.cs`

- [ ] **Step 1: Write the benchmark.**

```csharp
using Ahjo.Vulkan.Native;
using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Backs acceptance criterion 2 of issue #7: PickPhysicalDevice round-trip
/// reports zero managed allocations after the ArrayPool is warm. The
/// picker delegate is <c>static</c> so the compiler caches a singleton
/// instance; allocation pressure shows up only if PickPhysicalDevice
/// itself allocates.
/// </summary>
[MemoryDiagnoser]
public class PhysicalDevicePickerBenchmark
{
    private Instance _instance = null!;

    [GlobalSetup]
    public void Setup()
    {
        _instance = Instance.Create(default);

        // Warm the ArrayPool by running one picker round-trip outside
        // the measured iterations — the very first call may inflate the
        // pool. Subsequent calls hit a parked buffer and report 0 B.
        _instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => true);
    }

    [GlobalCleanup]
    public void Cleanup() => _instance.Dispose();

    [Benchmark]
    public PhysicalDevice Pick() =>
        _instance.PickPhysicalDevice(static (in PhysicalDeviceInfo _) => true);
}
```

- [ ] **Step 2: Build the benchmark project to confirm it compiles.**

```
dotnet build tests/Ahjo.Vulkan.Benchmarks/Ahjo.Vulkan.Benchmarks.csproj -c Release
```

Expected: build succeeds, no warnings new to this file.

- [ ] **Step 3: Run the benchmark and capture the `Allocated` column.**

```
dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter *PhysicalDevicePickerBenchmark*
```

Expected: BenchmarkDotNet's summary table shows `Allocated = 0 B` (or `-`) for the `Pick` benchmark. If the column reports a non-zero number, investigate (look for `new …[]`, boxing, closure capture, missing `static` on the picker lambda).

> If no Vulkan driver is on the host, the benchmark will throw during `[GlobalSetup]`; that's fine — the acceptance criterion is verified on developer machines and CI hosts that ship a driver. Document the driver requirement in the README if not already there (out of scope for this issue).

- [ ] **Step 4: Commit.**

```
git add tests/Ahjo.Vulkan.Benchmarks/PhysicalDevicePickerBenchmark.cs
git commit -m "Bench: zero-alloc proof for PickPhysicalDevice (issue 07)"
```

---

## Final acceptance check

Run the issue's acceptance criteria one last time:

- [ ] **Criterion 1: Test enumerates physical devices, picks one with a graphics queue family, reports its name + driver version.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~PhysicalDeviceTests"
```

Confirm `Pick_PicksDeviceWithGraphicsQueue`, `Pick_NameSpan_RoundTripsToString`, and `Pick_DriverVersion_NonZero` all pass on a host with a driver.

- [ ] **Criterion 2: Allocation benchmark — picker round-trip allocates 0 bytes.**

```
dotnet run --project tests/Ahjo.Vulkan.Benchmarks -c Release -- --filter *PhysicalDevicePickerBenchmark*
```

Confirm `Allocated == 0 B` in the summary table.

- [ ] **Issue close-out.** Add the issue number to the commits as expected by the project conventions and close GitHub issue #7 with a reference to the commit range.
