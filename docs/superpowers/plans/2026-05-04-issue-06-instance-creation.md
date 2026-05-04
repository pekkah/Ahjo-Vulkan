# Issue #6 — Instance creation API — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `Instance.Create(in InstanceDescription)` for `Ahjo.Vulkan` — Vulkan 1.4 baseline, opt-in Khronos validation with a debug-utils messenger, and full `IDisposable` lifetime.

**Architecture:** `Instance` is a `sealed class` (created once per process, owns unmanaged Vulkan resources, finalizer backstops missed `Dispose`). Input is a `ref struct InstanceDescription` with UTF-8 string lists held as `ReadOnlySpan<Utf8Name>` (a pointer-wrapper that lets a `params`-style collection compile around `ref struct`-banned `ReadOnlySpan<byte>`). Validation, when enabled, auto-adds `VK_LAYER_KHRONOS_validation` and `VK_EXT_debug_utils`, chains a `VkDebugUtilsMessengerCreateInfoEXT` into `VkInstanceCreateInfo.pNext` (so the callback fires *during* `vkCreateInstance`), and additionally creates a persistent post-create messenger via `vkGetInstanceProcAddr`. Callbacks come in three flavors: managed `Action<DebugMessage>`, raw `delegate* unmanaged[Stdcall]`, or a default `Console.Error` + `Debugger.Break()` sink.

**Tech Stack:** .NET 10, C# (unsafe), xUnit v3. Existing primitives from prior issues: `IVulkanHandle<TSelf>` (issue #3), `ChainBuilder<TRoot>` (issue #4), `VkResult.ThrowIfFailed()` + `VulkanException` (issue #5). Native bindings in `Ahjo.Vulkan.Native`.

**Spec:** `docs/superpowers/specs/2026-05-04-issue-06-instance-creation-design.md`. Read it before starting.

---

## File map

| Path | Kind | Purpose |
|---|---|---|
| `src/Ahjo.Vulkan/Lifecycle/VulkanVersion.cs` | new | Strongly-typed `record struct` over the `VK_MAKE_API_VERSION` packed `uint`. |
| `src/Ahjo.Vulkan/Lifecycle/Utf8Name.cs` | new | Pointer wrapper for UTF-8 string literals; lets `ReadOnlySpan<Utf8Name>` compile. |
| `src/Ahjo.Vulkan/Lifecycle/DebugMessage.cs` | new | Public `record struct` delivered to managed callbacks. |
| `src/Ahjo.Vulkan/Lifecycle/InstanceDescription.cs` | new | Input `ref struct` for `Instance.Create`. |
| `src/Ahjo.Vulkan/Lifecycle/Instance.cs` | new | `sealed class Instance : IDisposable`. The keystone. |
| `src/Ahjo.Vulkan/Internal/Utf8.cs` | new | `ToString(sbyte*)` helper. |
| `src/Ahjo.Vulkan/Internal/InstanceExtensionNames.cs` | new | UTF-8 byte literals for the layer + extension names + symbol names we hard-code. |
| `src/Ahjo.Vulkan/Internal/InstanceFunctionTable.cs` | new | Per-instance cache of resolved extension entry points. |
| `tests/Ahjo.Vulkan.Tests/VulkanVersionTests.cs` | new | Pure unit tests. |
| `tests/Ahjo.Vulkan.Tests/Utf8Tests.cs` | new | Pure unit tests on the `Utf8.ToString` helper. |
| `tests/Ahjo.Vulkan.Tests/Utf8NameTests.cs` | new | Pure unit tests on `Utf8Name.FromLiteral`. |
| `tests/Ahjo.Vulkan.Tests/VulkanDriverProbe.cs` | new | Test helper: detects whether the host has a Vulkan ICD and a validation layer. |
| `tests/Ahjo.Vulkan.Tests/InstanceCreateTests.cs` | new | Driver-dependent integration tests for `Instance.Create`. |
| `tests/Ahjo.Vulkan.Tests/InstanceFunctionTableTests.cs` | new | Resolve known + unknown function names through a live `Instance`. |

The order of tasks below builds bottom-up: pure units first, then the keystone class with the cheapest acceptance path, then the validation/callback variants, then cleanup tests.

---

## Task 1: `VulkanVersion`

Pure value type; no driver needed. TDD start.

**Files:**
- Create: `src/Ahjo.Vulkan/Lifecycle/VulkanVersion.cs`
- Test: `tests/Ahjo.Vulkan.Tests/VulkanVersionTests.cs`

- [ ] **Step 1: Write the failing test.**

Write `tests/Ahjo.Vulkan.Tests/VulkanVersionTests.cs`:

```csharp
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed class VulkanVersionTests
{
    [Fact]
    public void Make_PacksMajorMinorPatch()
    {
        var v = VulkanVersion.Make(1, 4, 7);
        Assert.Equal(1u, v.Major);
        Assert.Equal(4u, v.Minor);
        Assert.Equal(7u, v.Patch);
    }

    [Fact]
    public void V1_4_HasExpectedPackedValue()
    {
        // VK_MAKE_API_VERSION(0,1,4,0) = (1<<22) | (4<<12) = 0x00404000.
        Assert.Equal(0x00404000u, (uint)VulkanVersion.V1_4);
    }

    [Fact]
    public void ImplicitOperatorUint_ReturnsPacked()
    {
        VulkanVersion v = VulkanVersion.Make(1, 2, 3);
        uint packed = v;
        Assert.Equal(v.Packed, packed);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~VulkanVersionTests"
```

Expected: build error — `VulkanVersion` is not defined.

- [ ] **Step 3: Implement `VulkanVersion`.**

Write `src/Ahjo.Vulkan/Lifecycle/VulkanVersion.cs`:

```csharp
namespace Ahjo.Vulkan;

/// <summary>
/// Strongly-typed wrapper around the packed <c>uint</c> the Vulkan headers
/// produce via the <c>VK_MAKE_API_VERSION</c> macro. The binding generator
/// does not materialize the macro, so this is the canonical replacement.
/// <see cref="Packed"/> is the value <c>VkApplicationInfo.apiVersion</c> wants.
/// </summary>
public readonly record struct VulkanVersion(uint Packed)
{
    public static VulkanVersion V1_0 { get; } = Make(1, 0, 0);
    public static VulkanVersion V1_1 { get; } = Make(1, 1, 0);
    public static VulkanVersion V1_2 { get; } = Make(1, 2, 0);
    public static VulkanVersion V1_3 { get; } = Make(1, 3, 0);
    public static VulkanVersion V1_4 { get; } = Make(1, 4, 0);

    public static VulkanVersion Make(uint major, uint minor, uint patch)
        => new((major << 22) | (minor << 12) | patch);

    public uint Major => (Packed >> 22) & 0x7Fu;
    public uint Minor => (Packed >> 12) & 0x3FFu;
    public uint Patch =>  Packed         & 0xFFFu;

    public static implicit operator uint(VulkanVersion v) => v.Packed;
}
```

- [ ] **Step 4: Run the tests to verify they pass.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~VulkanVersionTests"
```

Expected: 3 passed.

- [ ] **Step 5: Commit.**

```
git add src/Ahjo.Vulkan/Lifecycle/VulkanVersion.cs tests/Ahjo.Vulkan.Tests/VulkanVersionTests.cs
git commit -m "Add VulkanVersion typed wrapper (issue 06)"
```

---

## Task 2: `Utf8` helper

Trivial UTF-8 pointer → string converter used by the managed callback trampoline and the default callback.

**Files:**
- Create: `src/Ahjo.Vulkan/Internal/Utf8.cs`
- Test: `tests/Ahjo.Vulkan.Tests/Utf8Tests.cs`

- [ ] **Step 1: Write the failing test.**

Write `tests/Ahjo.Vulkan.Tests/Utf8Tests.cs`:

```csharp
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed unsafe class Utf8Tests
{
    [Fact]
    public void ToString_NullPointer_ReturnsNull()
    {
        Assert.Null(Utf8.ToString((sbyte*)null));
    }

    [Fact]
    public void ToString_RoundTripsAsciiLiteral()
    {
        ReadOnlySpan<byte> literal = "VK_KHR_surface"u8;
        sbyte* p = (sbyte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(literal));
        Assert.Equal("VK_KHR_surface", Utf8.ToString(p));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~Utf8Tests"
```

Expected: build error — `Utf8` is not defined.

- [ ] **Step 3: Implement `Utf8`.**

Write `src/Ahjo.Vulkan/Internal/Utf8.cs`:

```csharp
using System.Runtime.InteropServices;

namespace Ahjo.Vulkan;

/// <summary>
/// Helpers for the UTF-8 boundary between Vulkan (which speaks <c>const char*</c>)
/// and managed code. Used by the debug-utils callback trampoline; not on a hot path.
/// </summary>
internal static unsafe class Utf8
{
    public static string? ToString(sbyte* utf8) =>
        utf8 == null ? null : Marshal.PtrToStringUTF8((nint)utf8);
}
```

- [ ] **Step 4: Run the tests to verify they pass.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~Utf8Tests"
```

Expected: 2 passed.

- [ ] **Step 5: Commit.**

```
git add src/Ahjo.Vulkan/Internal/Utf8.cs tests/Ahjo.Vulkan.Tests/Utf8Tests.cs
git commit -m "Add Utf8 helper for null-terminated pointer to string (issue 06)"
```

---

## Task 3: `Utf8Name`

Pointer wrapper that makes `ReadOnlySpan<Utf8Name>` compile (`ReadOnlySpan<byte>` is a `ref struct` and cannot be a span element type). `FromLiteral` is the only constructor entry — explicit name carries the lifetime contract that no implicit conversion can enforce.

**Files:**
- Create: `src/Ahjo.Vulkan/Lifecycle/Utf8Name.cs`
- Test: `tests/Ahjo.Vulkan.Tests/Utf8NameTests.cs`

- [ ] **Step 1: Write the failing test.**

Write `tests/Ahjo.Vulkan.Tests/Utf8NameTests.cs`:

```csharp
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed unsafe class Utf8NameTests
{
    [Fact]
    public void FromLiteral_NonEmpty_ReturnsNonNullPointer()
    {
        var name = Utf8Name.FromLiteral("VK_KHR_surface"u8);
        Assert.False(name.IsNull);
        Assert.Equal("VK_KHR_surface", Utf8.ToString(name.Ptr));
    }

    [Fact]
    public void Default_IsNull()
    {
        Utf8Name name = default;
        Assert.True(name.IsNull);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~Utf8NameTests"
```

Expected: build error — `Utf8Name` is not defined.

- [ ] **Step 3: Implement `Utf8Name`.**

Write `src/Ahjo.Vulkan/Lifecycle/Utf8Name.cs`:

```csharp
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ahjo.Vulkan;

/// <summary>
/// Pointer wrapper over a static, null-terminated UTF-8 string (typically a
/// <c>"…"u8</c> literal in the assembly's read-only data segment, lifetime =
/// process). Exists because <c>ReadOnlySpan&lt;byte&gt;</c> is a <c>ref
/// struct</c> and cannot live inside <c>ReadOnlySpan&lt;T&gt;</c> or arrays —
/// the wrapper is the safe collection element.
/// </summary>
public readonly unsafe struct Utf8Name
{
    public readonly sbyte* Ptr;

    public Utf8Name(sbyte* ptr) => Ptr = ptr;

    /// <summary>
    /// Creates a Utf8Name over a UTF-8 string LITERAL (<c>"…"u8</c>). Per the
    /// C# specification, <c>"…"u8</c> literals live in the assembly's
    /// read-only data segment for the lifetime of the process and are
    /// followed by a trailing null byte (the byte is past
    /// <c>span.Length</c> — <c>(sbyte*)&amp;span[0]</c> is safe to pass to a
    /// Vulkan API that wants <c>const char*</c>).
    ///
    /// Callers MUST NOT pass a span over a <c>byte[]</c> or <c>stackalloc</c>
    /// buffer. The GC can move a managed array; a stack buffer is gone the
    /// moment the frame returns. The resulting pointer would dangle. There
    /// is no implicit conversion from <c>ReadOnlySpan&lt;byte&gt;</c>
    /// precisely because the compiler cannot enforce this contract at the
    /// call site; <c>FromLiteral</c> is the safety announcement.
    /// </summary>
    public static Utf8Name FromLiteral(ReadOnlySpan<byte> utf8Literal)
    {
        Debug.Assert(utf8Literal.Length > 0, "Utf8Name requires a non-empty UTF-8 literal.");
        return new Utf8Name(
            (sbyte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(utf8Literal)));
    }

    public bool IsNull => Ptr == null;
}
```

- [ ] **Step 4: Run the tests to verify they pass.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~Utf8NameTests"
```

Expected: 2 passed.

- [ ] **Step 5: Commit.**

```
git add src/Ahjo.Vulkan/Lifecycle/Utf8Name.cs tests/Ahjo.Vulkan.Tests/Utf8NameTests.cs
git commit -m "Add Utf8Name pointer wrapper for params lists (issue 06)"
```

---

## Task 4: `DebugMessage`

Public `record struct` delivered to managed callbacks. Pure data; no test of its own (its behavior is exercised in the InstanceCreate tests later).

**Files:**
- Create: `src/Ahjo.Vulkan/Lifecycle/DebugMessage.cs`

- [ ] **Step 1: Write the type.**

Write `src/Ahjo.Vulkan/Lifecycle/DebugMessage.cs`:

```csharp
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Friendly form delivered to <see cref="InstanceDescription.DebugCallback"/>.
/// Marshalled once per validation message inside the unmanaged trampoline
/// (allocates two <c>string</c> instances). Validation messages are not a
/// hot path; the cost is irrelevant.
/// </summary>
public readonly record struct DebugMessage(
    VkDebugUtilsMessageSeverityFlagBitsEXT Severity,
    VkDebugUtilsMessageTypeFlagBitsEXT     Type,
    string                                 Message,
    string?                                MessageIdName,
    int                                    MessageIdNumber);
```

- [ ] **Step 2: Build to verify it compiles.**

```
dotnet build src/Ahjo.Vulkan
```

Expected: build succeeds.

- [ ] **Step 3: Commit.**

```
git add src/Ahjo.Vulkan/Lifecycle/DebugMessage.cs
git commit -m "Add DebugMessage record-struct for managed callbacks (issue 06)"
```

---

## Task 5: `InstanceExtensionNames`

Single source of truth for the UTF-8 byte literals we hard-code (the validation layer name, the debug-utils extension name, and the two function-name strings we resolve via `vkGetInstanceProcAddr`). Pure data; no test of its own.

**Files:**
- Create: `src/Ahjo.Vulkan/Internal/InstanceExtensionNames.cs`

- [ ] **Step 1: Write the type.**

Write `src/Ahjo.Vulkan/Internal/InstanceExtensionNames.cs`:

```csharp
namespace Ahjo.Vulkan;

/// <summary>
/// UTF-8 string literals for the layer, extension, and Vulkan function
/// symbol names this assembly hard-codes. Centralized so a typo can only
/// be made in one place.
/// </summary>
internal static class InstanceExtensionNames
{
    public static ReadOnlySpan<byte> KhronosValidationLayer => "VK_LAYER_KHRONOS_validation"u8;
    public static ReadOnlySpan<byte> DebugUtilsExtension    => "VK_EXT_debug_utils"u8;

    public static ReadOnlySpan<byte> CreateDebugUtilsMessenger  => "vkCreateDebugUtilsMessengerEXT"u8;
    public static ReadOnlySpan<byte> DestroyDebugUtilsMessenger => "vkDestroyDebugUtilsMessengerEXT"u8;
}
```

- [ ] **Step 2: Build to verify it compiles.**

```
dotnet build src/Ahjo.Vulkan
```

Expected: build succeeds.

- [ ] **Step 3: Commit.**

```
git add src/Ahjo.Vulkan/Internal/InstanceExtensionNames.cs
git commit -m "Add InstanceExtensionNames UTF-8 literal table (issue 06)"
```

---

## Task 6: `InstanceDescription`

Input `ref struct`. Plain data fields; no behavior of its own. Tests live with `Instance.Create`.

**Files:**
- Create: `src/Ahjo.Vulkan/Lifecycle/InstanceDescription.cs`

- [ ] **Step 1: Write the type.**

Write `src/Ahjo.Vulkan/Lifecycle/InstanceDescription.cs`:

```csharp
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Inputs to <see cref="Instance.Create"/>. <c>ref struct</c> because
/// <see cref="ReadOnlySpan{T}"/> of <see cref="Utf8Name"/> cannot live
/// inside a non-<c>ref</c> type. Fields default to zero/null so callers
/// only set what they care about.
/// </summary>
public ref struct InstanceDescription
{
    public Utf8Name      ApplicationName;     // optional; default null
    public Utf8Name      EngineName;          // optional; default null
    public uint          ApplicationVersion;  // optional; default 0
    public uint          EngineVersion;       // optional; default 0
    public VulkanVersion ApiVersion;          // defaults to V1_4 inside Create when Packed == 0

    public bool          EnableValidation;

    public ReadOnlySpan<Utf8Name> Extensions;
    public ReadOnlySpan<Utf8Name> Layers;

    public Action<DebugMessage>? DebugCallback;

    public unsafe delegate* unmanaged[Stdcall]<
        VkDebugUtilsMessageSeverityFlagBitsEXT,
        uint,
        VkDebugUtilsMessengerCallbackDataEXT*,
        void*,
        uint> DebugCallbackRaw;
}
```

- [ ] **Step 2: Build to verify it compiles.**

```
dotnet build src/Ahjo.Vulkan
```

Expected: build succeeds.

- [ ] **Step 3: Commit.**

```
git add src/Ahjo.Vulkan/Lifecycle/InstanceDescription.cs
git commit -m "Add InstanceDescription ref struct (issue 06)"
```

---

## Task 7: `VulkanDriverProbe` test helper

Driver-dependent tests need to detect (a) an ICD that can answer `vkCreateInstance` and (b) the Khronos validation layer being installed. Probes once per test session and caches the result so we don't allocate dozens of throwaway instances.

**Files:**
- Create: `tests/Ahjo.Vulkan.Tests/VulkanDriverProbe.cs`

- [ ] **Step 1: Write the helper.**

Write `tests/Ahjo.Vulkan.Tests/VulkanDriverProbe.cs`:

```csharp
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Detects whether the host has a Vulkan ICD and the Khronos validation
/// layer. Tests that need a driver guard with <c>Skip.IfNot(...)</c> so a
/// CI runner without a driver doesn't fail the whole suite.
/// </summary>
internal static unsafe class VulkanDriverProbe
{
    private static readonly Lazy<bool> _hasDriver = new(() =>
    {
        VkInstance_T* instance = null;
        var ai = new VkApplicationInfo
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_APPLICATION_INFO,
            apiVersion = (1u << 22) | (0u << 12),
        };
        var ci = new VkInstanceCreateInfo
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
            pApplicationInfo = &ai,
        };
        var r = Vk.vkCreateInstance(&ci, null, &instance);
        if (r == VkResult.VK_SUCCESS)
        {
            Vk.vkDestroyInstance(instance, null);
            return true;
        }
        return false;
    });

    private static readonly Lazy<bool> _hasValidationLayer = new(() =>
    {
        if (!_hasDriver.Value) return false;

        uint count = 0;
        if (Vk.vkEnumerateInstanceLayerProperties(&count, null) != VkResult.VK_SUCCESS || count == 0)
            return false;

        var props = new VkLayerProperties[count];
        fixed (VkLayerProperties* p = props)
        {
            if (Vk.vkEnumerateInstanceLayerProperties(&count, p) != VkResult.VK_SUCCESS)
                return false;
        }

        ReadOnlySpan<byte> target = "VK_LAYER_KHRONOS_validation"u8;
        for (int i = 0; i < count; i++)
        {
            fixed (sbyte* name = props[i].layerName)
            {
                if (Match(name, target)) return true;
            }
        }
        return false;
    });

    public static bool HasDriver => _hasDriver.Value;
    public static bool HasValidationLayer => _hasValidationLayer.Value;

    private static bool Match(sbyte* name, ReadOnlySpan<byte> target)
    {
        for (int i = 0; i < target.Length; i++)
        {
            if (name[i] == 0 || (byte)name[i] != target[i]) return false;
        }
        return name[target.Length] == 0;
    }
}
```

- [ ] **Step 2: Build to verify it compiles.**

```
dotnet build tests/Ahjo.Vulkan.Tests
```

Expected: build succeeds.

- [ ] **Step 3: Commit.**

```
git add tests/Ahjo.Vulkan.Tests/VulkanDriverProbe.cs
git commit -m "Add VulkanDriverProbe test helper (issue 06)"
```

---

## Task 8: `Instance` minimal — create + destroy without validation

This task gets the keystone class building and proves AC1 (instance creates against the host driver) and the disposal-half of AC2. Validation, callbacks, and the persistent messenger come in subsequent tasks.

**Files:**
- Create: `src/Ahjo.Vulkan/Lifecycle/Instance.cs`
- Create: `tests/Ahjo.Vulkan.Tests/InstanceCreateTests.cs`

- [ ] **Step 1: Write the failing test.**

Write `tests/Ahjo.Vulkan.Tests/InstanceCreateTests.cs`:

```csharp
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed unsafe class InstanceCreateTests
{
    [Fact]
    public void Create_MinimalDescription_Succeeds()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(new InstanceDescription
        {
            ApiVersion = VulkanVersion.V1_4,
        });

        Assert.True(instance.Handle != null);
    }

    [Fact]
    public void Create_DefaultsApiVersionWhenZero()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);

        Assert.True(instance.Handle != null);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~InstanceCreateTests"
```

Expected: build error — `Instance` is not defined.

- [ ] **Step 3: Implement `Instance` minimal version.**

Write `src/Ahjo.Vulkan/Lifecycle/Instance.cs`:

```csharp
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Owner of a <c>VkInstance</c>. <c>sealed class</c> rather than the wider
/// struct-handle convention because <see cref="Instance"/> is created once
/// per process, never copied, never on a hot path, and benefits from a
/// finalizer that backstops a missed <c>Dispose</c>. See the design spec at
/// <c>docs/superpowers/specs/2026-05-04-issue-06-instance-creation-design.md</c>
/// for the rationale.
/// </summary>
public sealed unsafe class Instance : IDisposable
{
    internal VkInstance_T*               Handle;
    internal VkDebugUtilsMessengerEXT_T* Messenger;
    private  bool                        _disposed;

    private Instance(VkInstance_T* handle, VkDebugUtilsMessengerEXT_T* messenger)
    {
        Handle = handle;
        Messenger = messenger;
    }

    public static Instance Create(scoped in InstanceDescription desc)
    {
        uint apiVersion = desc.ApiVersion.Packed != 0 ? desc.ApiVersion.Packed : VulkanVersion.V1_4.Packed;

        var appInfo = new VkApplicationInfo
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_APPLICATION_INFO,
            pApplicationName = desc.ApplicationName.Ptr,
            applicationVersion = desc.ApplicationVersion,
            pEngineName = desc.EngineName.Ptr,
            engineVersion = desc.EngineVersion,
            apiVersion = apiVersion,
        };

        Span<nint> layerPtrs = stackalloc nint[Math.Max(1, desc.Layers.Length)];
        for (int i = 0; i < desc.Layers.Length; i++) layerPtrs[i] = (nint)desc.Layers[i].Ptr;

        Span<nint> extPtrs = stackalloc nint[Math.Max(1, desc.Extensions.Length)];
        for (int i = 0; i < desc.Extensions.Length; i++) extPtrs[i] = (nint)desc.Extensions[i].Ptr;

        var ci = new VkInstanceCreateInfo
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
            pApplicationInfo = &appInfo,
            enabledLayerCount = (uint)desc.Layers.Length,
            ppEnabledLayerNames = desc.Layers.Length > 0
                ? (sbyte**)Unsafe.AsPointer(ref MemoryMarshal.GetReference(layerPtrs))
                : null,
            enabledExtensionCount = (uint)desc.Extensions.Length,
            ppEnabledExtensionNames = desc.Extensions.Length > 0
                ? (sbyte**)Unsafe.AsPointer(ref MemoryMarshal.GetReference(extPtrs))
                : null,
        };

        VkInstance_T* raw = null;
        Vk.vkCreateInstance(&ci, null, &raw).ThrowIfFailed();

        return new Instance(raw, null);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Messenger != null)
        {
            // wired in Task 12
        }
        if (Handle != null) Vk.vkDestroyInstance(Handle, null);
        GC.SuppressFinalize(this);
    }

    ~Instance()
    {
        Debug.Fail("Instance was not disposed.");
        Dispose();
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~InstanceCreateTests"
```

Expected: 2 passed (or 2 skipped if no driver).

- [ ] **Step 5: Commit.**

```
git add src/Ahjo.Vulkan/Lifecycle/Instance.cs tests/Ahjo.Vulkan.Tests/InstanceCreateTests.cs
git commit -m "Add Instance.Create minimal path + dispose (issue 06)"
```

---

## Task 9: Validation auto-add + chained `pNext` messenger + default callback

Adds: validation layer/extension auto-add (with dedupe), chains `VkDebugUtilsMessengerCreateInfoEXT` into `VkInstanceCreateInfo.pNext`, wires the default `Console.Error` + `Debugger.Break` callback. Hits AC3.

**Files:**
- Modify: `src/Ahjo.Vulkan/Lifecycle/Instance.cs`
- Modify: `tests/Ahjo.Vulkan.Tests/InstanceCreateTests.cs`

- [ ] **Step 1: Add the failing test.**

Append to `tests/Ahjo.Vulkan.Tests/InstanceCreateTests.cs` inside the class:

```csharp
    [Fact]
    public void Create_WithValidation_DefaultCallback_FiresOnUnknownExtension()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver,           "No Vulkan driver on host.");
        Assert.SkipUnless(VulkanDriverProbe.HasValidationLayer,  "Validation layer not installed.");

        var stderr = new StringWriter();
        var oldErr = Console.Error;
        Console.SetError(stderr);
        try
        {
            ReadOnlySpan<Utf8Name> bogus = stackalloc Utf8Name[]
            {
                Utf8Name.FromLiteral("VK_FAKE_extension_does_not_exist"u8),
            };

            Assert.Throws<VulkanException>(() => Instance.Create(new InstanceDescription
            {
                ApiVersion = VulkanVersion.V1_4,
                EnableValidation = true,
                Extensions = bogus,
            }));
        }
        finally
        {
            Console.SetError(oldErr);
        }

        Assert.NotEmpty(stderr.ToString());
    }
```

- [ ] **Step 2: Run the test to verify it fails.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~Create_WithValidation_DefaultCallback"
```

Expected: fails because `EnableValidation` is currently a no-op — no `VulkanException`, no stderr output, or a different failure mode.

- [ ] **Step 3: Add helper extension-list/layer-list resolver and validation wiring to `Instance`.**

Replace the body of `Instance.cs` with:

```csharp
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

public sealed unsafe class Instance : IDisposable
{
    internal VkInstance_T*               Handle;
    internal VkDebugUtilsMessengerEXT_T* Messenger;
    private  bool                        _disposed;

    private Instance(VkInstance_T* handle, VkDebugUtilsMessengerEXT_T* messenger)
    {
        Handle = handle;
        Messenger = messenger;
    }

    public static Instance Create(scoped in InstanceDescription desc)
    {
        uint apiVersion = desc.ApiVersion.Packed != 0 ? desc.ApiVersion.Packed : VulkanVersion.V1_4.Packed;

        // Layers + extensions, with dedupe-aware auto-add when validation is on.
        // Lists are tiny — linear scans are fine.
        Span<nint> layerPtrs = stackalloc nint[desc.Layers.Length + 1];
        int layerCount = CopyAndMaybeAppend(desc.Layers, layerPtrs,
            desc.EnableValidation ? InstanceExtensionNames.KhronosValidationLayer : default);

        Span<nint> extPtrs = stackalloc nint[desc.Extensions.Length + 1];
        int extCount = CopyAndMaybeAppend(desc.Extensions, extPtrs,
            desc.EnableValidation ? InstanceExtensionNames.DebugUtilsExtension : default);

        var appInfo = new VkApplicationInfo
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_APPLICATION_INFO,
            pApplicationName = desc.ApplicationName.Ptr,
            applicationVersion = desc.ApplicationVersion,
            pEngineName = desc.EngineName.Ptr,
            engineVersion = desc.EngineVersion,
            apiVersion = apiVersion,
        };

        // Build the create info — chain the messenger into pNext when validation is on
        // so callbacks fire during vkCreateInstance itself.
        Span<byte> chainBuf = stackalloc byte[256];
        var chain = ChainBuilder.For<VkInstanceCreateInfo>(chainBuf);
        ref VkInstanceCreateInfo ci = ref chain.Root();
        ci.pApplicationInfo = &appInfo;
        ci.enabledLayerCount = (uint)layerCount;
        ci.ppEnabledLayerNames = layerCount > 0
            ? (sbyte**)Unsafe.AsPointer(ref MemoryMarshal.GetReference(layerPtrs))
            : null;
        ci.enabledExtensionCount = (uint)extCount;
        ci.ppEnabledExtensionNames = extCount > 0
            ? (sbyte**)Unsafe.AsPointer(ref MemoryMarshal.GetReference(extPtrs))
            : null;

        if (desc.EnableValidation)
        {
            ref VkDebugUtilsMessengerCreateInfoEXT mci = ref chain.Push<VkDebugUtilsMessengerCreateInfoEXT>();
            mci.messageSeverity =
                (uint)VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_VERBOSE_BIT_EXT |
                (uint)VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_INFO_BIT_EXT |
                (uint)VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT |
                (uint)VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT;
            mci.messageType =
                (uint)VkDebugUtilsMessageTypeFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_GENERAL_BIT_EXT |
                (uint)VkDebugUtilsMessageTypeFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_VALIDATION_BIT_EXT |
                (uint)VkDebugUtilsMessageTypeFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_PERFORMANCE_BIT_EXT;
            mci.pfnUserCallback = &DefaultCallback;
            mci.pUserData = null;
        }

        VkInstance_T* raw = null;
        Vk.vkCreateInstance(chain.Head, null, &raw).ThrowIfFailed();

        return new Instance(raw, null);
    }

    private static int CopyAndMaybeAppend(
        ReadOnlySpan<Utf8Name> input,
        Span<nint> dest,
        ReadOnlySpan<byte> autoAddIfNonEmpty)
    {
        int n = 0;
        for (int i = 0; i < input.Length; i++) dest[n++] = (nint)input[i].Ptr;
        if (autoAddIfNonEmpty.IsEmpty) return n;

        // dedupe: skip if the auto-add string is already present
        for (int i = 0; i < input.Length; i++)
        {
            if (PointerStringEquals((sbyte*)input[i].Ptr, autoAddIfNonEmpty)) return n;
        }
        dest[n++] = (nint)Unsafe.AsPointer(ref MemoryMarshal.GetReference(autoAddIfNonEmpty));
        return n;
    }

    private static bool PointerStringEquals(sbyte* p, ReadOnlySpan<byte> target)
    {
        if (p == null) return false;
        for (int i = 0; i < target.Length; i++)
        {
            if (p[i] == 0 || (byte)p[i] != target[i]) return false;
        }
        return p[target.Length] == 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    private static uint DefaultCallback(
        VkDebugUtilsMessageSeverityFlagBitsEXT severity,
        uint                                   type,
        VkDebugUtilsMessengerCallbackDataEXT*  data,
        void*                                  userData)
    {
        var msg = data != null ? Utf8.ToString(data->pMessage) : null;
        Console.Error.WriteLine($"[Vulkan {severity}] {msg}");
        if ((severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) != 0
            && Debugger.IsAttached)
        {
            Debugger.Break();
        }
        return 0; // VK_FALSE — VK_TRUE would abort the calling Vulkan command
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Messenger != null)
        {
            // wired in Task 12
        }
        if (Handle != null) Vk.vkDestroyInstance(Handle, null);
        GC.SuppressFinalize(this);
    }

    ~Instance()
    {
        Debug.Fail("Instance was not disposed.");
        Dispose();
    }
}
```

Note: `[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]` — the `CallConvStdcall` type is in `System.Runtime.CompilerServices`; the `using` is already at the top.

- [ ] **Step 4: Run all tests to verify they pass.**

```
dotnet test tests/Ahjo.Vulkan.Tests
```

Expected: all `InstanceCreateTests` pass (or skip if no driver / no validation layer); prior tasks' tests still pass.

- [ ] **Step 5: Commit.**

```
git add src/Ahjo.Vulkan/Lifecycle/Instance.cs tests/Ahjo.Vulkan.Tests/InstanceCreateTests.cs
git commit -m "Wire validation layer + chained pNext messenger + default callback (issue 06)"
```

---

## Task 10: Managed `Action<DebugMessage>` callback path

Adds the trampoline that lets callers register a managed callback. The `GCHandle` for the delegate is allocated **before** `vkCreateInstance` so the chained `pNext` messenger can fire during the call itself.

**Files:**
- Modify: `src/Ahjo.Vulkan/Lifecycle/Instance.cs`
- Modify: `tests/Ahjo.Vulkan.Tests/InstanceCreateTests.cs`

- [ ] **Step 1: Add the failing test.**

Append to `InstanceCreateTests.cs` inside the class:

```csharp
    [Fact]
    public void Create_WithValidation_ManagedCallback_RoundTripsMessage()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver,           "No Vulkan driver on host.");
        Assert.SkipUnless(VulkanDriverProbe.HasValidationLayer,  "Validation layer not installed.");

        var captured = new List<DebugMessage>();

        ReadOnlySpan<Utf8Name> bogus = stackalloc Utf8Name[]
        {
            Utf8Name.FromLiteral("VK_FAKE_extension_does_not_exist"u8),
        };

        Assert.Throws<VulkanException>(() => Instance.Create(new InstanceDescription
        {
            ApiVersion = VulkanVersion.V1_4,
            EnableValidation = true,
            Extensions = bogus,
            DebugCallback = m => { lock (captured) captured.Add(m); },
        }));

        Assert.NotEmpty(captured);
        Assert.Contains(captured, m =>
            (m.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) != 0);
    }
```

- [ ] **Step 2: Run the test to verify it fails.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~ManagedCallback_RoundTripsMessage"
```

Expected: fails — managed callback path is not yet wired (the default callback fires instead, captured stays empty).

- [ ] **Step 3: Replace the entire `Instance.cs` with the managed-callback-aware version.**

Write `src/Ahjo.Vulkan/Lifecycle/Instance.cs` (full file replacement):

```csharp
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

public sealed unsafe class Instance : IDisposable
{
    internal VkInstance_T*               Handle;
    internal VkDebugUtilsMessengerEXT_T* Messenger;
    private  GCHandle                    _callbackKeepAlive;
    private  bool                        _disposed;

    private Instance(
        VkInstance_T* handle,
        VkDebugUtilsMessengerEXT_T* messenger,
        GCHandle callbackKeepAlive)
    {
        Handle = handle;
        Messenger = messenger;
        _callbackKeepAlive = callbackKeepAlive;
    }

    public static Instance Create(scoped in InstanceDescription desc)
    {
        uint apiVersion = desc.ApiVersion.Packed != 0 ? desc.ApiVersion.Packed : VulkanVersion.V1_4.Packed;

        // Layers + extensions, with dedupe-aware auto-add when validation is on.
        Span<nint> layerPtrs = stackalloc nint[desc.Layers.Length + 1];
        int layerCount = CopyAndMaybeAppend(desc.Layers, layerPtrs,
            desc.EnableValidation ? InstanceExtensionNames.KhronosValidationLayer : default);

        Span<nint> extPtrs = stackalloc nint[desc.Extensions.Length + 1];
        int extCount = CopyAndMaybeAppend(desc.Extensions, extPtrs,
            desc.EnableValidation ? InstanceExtensionNames.DebugUtilsExtension : default);

        var appInfo = new VkApplicationInfo
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_APPLICATION_INFO,
            pApplicationName = desc.ApplicationName.Ptr,
            applicationVersion = desc.ApplicationVersion,
            pEngineName = desc.EngineName.Ptr,
            engineVersion = desc.EngineVersion,
            apiVersion = apiVersion,
        };

        // Allocate GCHandle BEFORE vkCreateInstance: the chained pNext messenger
        // can fire from inside the call (e.g. on extension rejection), so
        // pUserData must already point at a live handle.
        GCHandle keepAlive = default;
        if (desc.EnableValidation && desc.DebugCallback is not null && desc.DebugCallbackRaw == null)
        {
            keepAlive = GCHandle.Alloc(desc.DebugCallback);
        }

        try
        {
            Span<byte> chainBuf = stackalloc byte[256];
            var chain = ChainBuilder.For<VkInstanceCreateInfo>(chainBuf);
            ref VkInstanceCreateInfo ci = ref chain.Root();
            ci.pApplicationInfo = &appInfo;
            ci.enabledLayerCount = (uint)layerCount;
            ci.ppEnabledLayerNames = layerCount > 0
                ? (sbyte**)Unsafe.AsPointer(ref MemoryMarshal.GetReference(layerPtrs))
                : null;
            ci.enabledExtensionCount = (uint)extCount;
            ci.ppEnabledExtensionNames = extCount > 0
                ? (sbyte**)Unsafe.AsPointer(ref MemoryMarshal.GetReference(extPtrs))
                : null;

            if (desc.EnableValidation)
            {
                ref VkDebugUtilsMessengerCreateInfoEXT mci = ref chain.Push<VkDebugUtilsMessengerCreateInfoEXT>();
                mci.messageSeverity = AllSeverities;
                mci.messageType = AllTypes;

                if (desc.DebugCallbackRaw != null)
                {
                    mci.pfnUserCallback = desc.DebugCallbackRaw;
                    mci.pUserData = null;
                }
                else if (desc.DebugCallback is not null)
                {
                    mci.pfnUserCallback = &ManagedCallbackThunk;
                    mci.pUserData = (void*)GCHandle.ToIntPtr(keepAlive);
                }
                else
                {
                    mci.pfnUserCallback = &DefaultCallback;
                    mci.pUserData = null;
                }
            }

            VkInstance_T* raw = null;
            Vk.vkCreateInstance(chain.Head, null, &raw).ThrowIfFailed();

            return new Instance(raw, null, keepAlive);
        }
        catch
        {
            if (keepAlive.IsAllocated) keepAlive.Free();
            throw;
        }
    }

    private const uint AllSeverities =
        (uint)VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_VERBOSE_BIT_EXT |
        (uint)VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_INFO_BIT_EXT |
        (uint)VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT |
        (uint)VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT;

    private const uint AllTypes =
        (uint)VkDebugUtilsMessageTypeFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_GENERAL_BIT_EXT |
        (uint)VkDebugUtilsMessageTypeFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_VALIDATION_BIT_EXT |
        (uint)VkDebugUtilsMessageTypeFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_PERFORMANCE_BIT_EXT;

    private static int CopyAndMaybeAppend(
        ReadOnlySpan<Utf8Name> input,
        Span<nint> dest,
        ReadOnlySpan<byte> autoAddIfNonEmpty)
    {
        int n = 0;
        for (int i = 0; i < input.Length; i++) dest[n++] = (nint)input[i].Ptr;
        if (autoAddIfNonEmpty.IsEmpty) return n;

        for (int i = 0; i < input.Length; i++)
        {
            if (PointerStringEquals((sbyte*)input[i].Ptr, autoAddIfNonEmpty)) return n;
        }
        dest[n++] = (nint)Unsafe.AsPointer(ref MemoryMarshal.GetReference(autoAddIfNonEmpty));
        return n;
    }

    private static bool PointerStringEquals(sbyte* p, ReadOnlySpan<byte> target)
    {
        if (p == null) return false;
        for (int i = 0; i < target.Length; i++)
        {
            if (p[i] == 0 || (byte)p[i] != target[i]) return false;
        }
        return p[target.Length] == 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint DefaultCallback(
        VkDebugUtilsMessageSeverityFlagBitsEXT severity,
        uint                                   type,
        VkDebugUtilsMessengerCallbackDataEXT*  data,
        void*                                  userData)
    {
        var msg = data != null ? Utf8.ToString(data->pMessage) : null;
        Console.Error.WriteLine($"[Vulkan {severity}] {msg}");
        if ((severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) != 0
            && Debugger.IsAttached)
        {
            Debugger.Break();
        }
        return 0; // VK_FALSE — VK_TRUE would abort the calling Vulkan command
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint ManagedCallbackThunk(
        VkDebugUtilsMessageSeverityFlagBitsEXT severity,
        uint                                   type,
        VkDebugUtilsMessengerCallbackDataEXT*  data,
        void*                                  userData)
    {
        if (userData == null || data == null) return 0;
        var handle = GCHandle.FromIntPtr((nint)userData);
        if (handle.Target is not Action<DebugMessage> cb) return 0;

        var msg = new DebugMessage(
            severity,
            (VkDebugUtilsMessageTypeFlagBitsEXT)type,
            Utf8.ToString(data->pMessage) ?? string.Empty,
            Utf8.ToString(data->pMessageIdName),
            data->messageIdNumber);

        try { cb(msg); } catch { /* swallow: never throw across native boundary */ }
        return 0; // VK_FALSE
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Messenger != null)
        {
            // persistent messenger destruction wired in Task 12
        }
        if (Handle != null) Vk.vkDestroyInstance(Handle, null);
        if (_callbackKeepAlive.IsAllocated) _callbackKeepAlive.Free();
        GC.SuppressFinalize(this);
    }

    ~Instance()
    {
        Debug.Fail("Instance was not disposed.");
        Dispose();
    }
}
```

- [ ] **Step 4: Run all tests to verify they pass.**

```
dotnet test tests/Ahjo.Vulkan.Tests
```

Expected: all tests pass (skipped if no driver / validation layer).

- [ ] **Step 5: Commit.**

```
git add src/Ahjo.Vulkan/Lifecycle/Instance.cs tests/Ahjo.Vulkan.Tests/InstanceCreateTests.cs
git commit -m "Wire managed Action<DebugMessage> callback path (issue 06)"
```

---

## Task 11: Raw `delegate*` callback path

The raw path is already wired structurally in Task 10's selection logic. This task just adds an acceptance test that exercises it.

**Files:**
- Modify: `tests/Ahjo.Vulkan.Tests/InstanceCreateTests.cs`

- [ ] **Step 1: Add the failing test.**

Append to `InstanceCreateTests.cs`:

```csharp
    private static int s_rawCallbackHits;

    [System.Runtime.InteropServices.UnmanagedCallersOnly(
        CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    private static uint RawCountingCallback(
        VkDebugUtilsMessageSeverityFlagBitsEXT severity,
        uint                                   type,
        VkDebugUtilsMessengerCallbackDataEXT*  data,
        void*                                  userData)
    {
        System.Threading.Interlocked.Increment(ref s_rawCallbackHits);
        return 0;
    }

    [Fact]
    public void Create_WithValidation_RawCallback_IsInvoked()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver,           "No Vulkan driver on host.");
        Assert.SkipUnless(VulkanDriverProbe.HasValidationLayer,  "Validation layer not installed.");

        s_rawCallbackHits = 0;

        ReadOnlySpan<Utf8Name> bogus = stackalloc Utf8Name[]
        {
            Utf8Name.FromLiteral("VK_FAKE_extension_does_not_exist"u8),
        };

        Assert.Throws<VulkanException>(() => Instance.Create(new InstanceDescription
        {
            ApiVersion = VulkanVersion.V1_4,
            EnableValidation = true,
            Extensions = bogus,
            DebugCallbackRaw = &RawCountingCallback,
        }));

        Assert.True(s_rawCallbackHits > 0, $"Expected raw callback to fire; hits = {s_rawCallbackHits}");
    }
```

- [ ] **Step 2: Run the test to verify it passes.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~RawCallback_IsInvoked"
```

Expected: passes (the wiring from Task 10 already routes a non-null `DebugCallbackRaw` into `pfnUserCallback`). If it doesn't, the routing in Task 10 step 3c is wrong — fix there before continuing.

- [ ] **Step 3: Commit.**

```
git add tests/Ahjo.Vulkan.Tests/InstanceCreateTests.cs
git commit -m "Add raw delegate* callback acceptance test (issue 06)"
```

---

## Task 12: `InstanceFunctionTable` + persistent post-create messenger

Until this task the messenger only catches messages emitted *during* `vkCreateInstance` via the chained `pNext`. This task creates the persistent `VkDebugUtilsMessengerEXT` so messages also fire post-create. It introduces `InstanceFunctionTable` to hold the resolved extension entry points, since they must come from `vkGetInstanceProcAddr` (the loader does not export them through `vulkan-1.dll`).

**Files:**
- Create: `src/Ahjo.Vulkan/Internal/InstanceFunctionTable.cs`
- Create: `tests/Ahjo.Vulkan.Tests/InstanceFunctionTableTests.cs`
- Modify: `src/Ahjo.Vulkan/Lifecycle/Instance.cs`
- Modify: `tests/Ahjo.Vulkan.Tests/InstanceCreateTests.cs`

- [ ] **Step 1: Add the failing tests for the persistent messenger and the function-table.**

Append to `InstanceCreateTests.cs`:

```csharp
    [Fact]
    public void PersistentMessenger_FiresOnPostCreateValidationViolation()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver,           "No Vulkan driver on host.");
        Assert.SkipUnless(VulkanDriverProbe.HasValidationLayer,  "Validation layer not installed.");

        var captured = new List<DebugMessage>();
        using var instance = Instance.Create(new InstanceDescription
        {
            ApiVersion = VulkanVersion.V1_4,
            EnableValidation = true,
            DebugCallback = m => { lock (captured) captured.Add(m); },
        });

        captured.Clear();

        // Intentional VUID violation: vkEnumeratePhysicalDevices requires a
        // non-null pPhysicalDeviceCount per VUID-vkEnumeratePhysicalDevices-
        // pPhysicalDeviceCount-parameter. The persistent messenger must fire.
        Native.Vk.vkEnumeratePhysicalDevices(instance.Handle, null, null);

        Assert.NotEmpty(captured);
    }
```

Write `tests/Ahjo.Vulkan.Tests/InstanceFunctionTableTests.cs`:

```csharp
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed unsafe class InstanceFunctionTableTests
{
    [Fact]
    public void Resolve_KnownExtension_ReturnsNonNull()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver,           "No Vulkan driver on host.");
        Assert.SkipUnless(VulkanDriverProbe.HasValidationLayer,  "Validation layer not installed.");

        using var instance = Instance.Create(new InstanceDescription
        {
            ApiVersion = VulkanVersion.V1_4,
            EnableValidation = true,
        });

        Assert.True(instance.Functions.CreateDebugUtilsMessenger != null);
        Assert.True(instance.Functions.DestroyDebugUtilsMessenger != null);
    }

    [Fact]
    public void Resolve_UnknownName_ReturnsNull()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(new InstanceDescription
        {
            ApiVersion = VulkanVersion.V1_4,
        });

        ReadOnlySpan<byte> nope = "vkDoesNotExist"u8;
        Assert.True(instance.Functions.Resolve(Utf8Name.FromLiteral(nope)) == null);
    }
}
```

- [ ] **Step 2: Run to verify they fail.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~PersistentMessenger|FullyQualifiedName~InstanceFunctionTableTests"
```

Expected: build error — `Functions` field, `InstanceFunctionTable` type don't exist yet.

- [ ] **Step 3: Implement `InstanceFunctionTable`.**

Write `src/Ahjo.Vulkan/Internal/InstanceFunctionTable.cs`:

```csharp
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Per-instance cache of extension entry points resolved through
/// <c>vkGetInstanceProcAddr</c>. The loader does not export extension
/// functions through <c>vulkan-1.dll</c>; the only legal way to call them
/// is via the function pointer the loader hands back at runtime.
/// </summary>
internal unsafe struct InstanceFunctionTable
{
    public delegate* unmanaged[Stdcall]<
        VkInstance_T*,
        VkDebugUtilsMessengerCreateInfoEXT*,
        VkAllocationCallbacks*,
        VkDebugUtilsMessengerEXT_T**,
        VkResult> CreateDebugUtilsMessenger;

    public delegate* unmanaged[Stdcall]<
        VkInstance_T*,
        VkDebugUtilsMessengerEXT_T*,
        VkAllocationCallbacks*,
        void> DestroyDebugUtilsMessenger;

    private VkInstance_T* _instance;

    public InstanceFunctionTable(VkInstance_T* instance)
    {
        _instance = instance;
        CreateDebugUtilsMessenger =
            (delegate* unmanaged[Stdcall]<VkInstance_T*, VkDebugUtilsMessengerCreateInfoEXT*, VkAllocationCallbacks*, VkDebugUtilsMessengerEXT_T**, VkResult>)
            Resolve(Utf8Name.FromLiteral(InstanceExtensionNames.CreateDebugUtilsMessenger));
        DestroyDebugUtilsMessenger =
            (delegate* unmanaged[Stdcall]<VkInstance_T*, VkDebugUtilsMessengerEXT_T*, VkAllocationCallbacks*, void>)
            Resolve(Utf8Name.FromLiteral(InstanceExtensionNames.DestroyDebugUtilsMessenger));
    }

    public delegate* unmanaged[Stdcall]<void> Resolve(Utf8Name name) =>
        Vk.vkGetInstanceProcAddr(_instance, name.Ptr);
}
```

- [ ] **Step 4: Replace the entire `Instance.cs` with the persistent-messenger-aware version.**

Write `src/Ahjo.Vulkan/Lifecycle/Instance.cs` (full file replacement; this is the final form for the issue):

```csharp
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

public sealed unsafe class Instance : IDisposable
{
    internal VkInstance_T*               Handle;
    internal VkDebugUtilsMessengerEXT_T* Messenger;
    internal InstanceFunctionTable       Functions;
    private  GCHandle                    _callbackKeepAlive;
    private  bool                        _disposed;

    private Instance(
        VkInstance_T* handle,
        VkDebugUtilsMessengerEXT_T* messenger,
        InstanceFunctionTable functions,
        GCHandle callbackKeepAlive)
    {
        Handle = handle;
        Messenger = messenger;
        Functions = functions;
        _callbackKeepAlive = callbackKeepAlive;
    }

    public static Instance Create(scoped in InstanceDescription desc)
    {
        uint apiVersion = desc.ApiVersion.Packed != 0 ? desc.ApiVersion.Packed : VulkanVersion.V1_4.Packed;

        Span<nint> layerPtrs = stackalloc nint[desc.Layers.Length + 1];
        int layerCount = CopyAndMaybeAppend(desc.Layers, layerPtrs,
            desc.EnableValidation ? InstanceExtensionNames.KhronosValidationLayer : default);

        Span<nint> extPtrs = stackalloc nint[desc.Extensions.Length + 1];
        int extCount = CopyAndMaybeAppend(desc.Extensions, extPtrs,
            desc.EnableValidation ? InstanceExtensionNames.DebugUtilsExtension : default);

        var appInfo = new VkApplicationInfo
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_APPLICATION_INFO,
            pApplicationName = desc.ApplicationName.Ptr,
            applicationVersion = desc.ApplicationVersion,
            pEngineName = desc.EngineName.Ptr,
            engineVersion = desc.EngineVersion,
            apiVersion = apiVersion,
        };

        // GCHandle BEFORE vkCreateInstance — chained pNext messenger may fire during the call.
        GCHandle keepAlive = default;
        if (desc.EnableValidation && desc.DebugCallback is not null && desc.DebugCallbackRaw == null)
        {
            keepAlive = GCHandle.Alloc(desc.DebugCallback);
        }

        try
        {
            Span<byte> chainBuf = stackalloc byte[256];
            var chain = ChainBuilder.For<VkInstanceCreateInfo>(chainBuf);
            ref VkInstanceCreateInfo ci = ref chain.Root();
            ci.pApplicationInfo = &appInfo;
            ci.enabledLayerCount = (uint)layerCount;
            ci.ppEnabledLayerNames = layerCount > 0
                ? (sbyte**)Unsafe.AsPointer(ref MemoryMarshal.GetReference(layerPtrs))
                : null;
            ci.enabledExtensionCount = (uint)extCount;
            ci.ppEnabledExtensionNames = extCount > 0
                ? (sbyte**)Unsafe.AsPointer(ref MemoryMarshal.GetReference(extPtrs))
                : null;

            if (desc.EnableValidation)
            {
                ref VkDebugUtilsMessengerCreateInfoEXT mci = ref chain.Push<VkDebugUtilsMessengerCreateInfoEXT>();
                mci.messageSeverity = AllSeverities;
                mci.messageType = AllTypes;

                if (desc.DebugCallbackRaw != null)
                {
                    mci.pfnUserCallback = desc.DebugCallbackRaw;
                    mci.pUserData = null;
                }
                else if (desc.DebugCallback is not null)
                {
                    mci.pfnUserCallback = &ManagedCallbackThunk;
                    mci.pUserData = (void*)GCHandle.ToIntPtr(keepAlive);
                }
                else
                {
                    mci.pfnUserCallback = &DefaultCallback;
                    mci.pUserData = null;
                }
            }

            VkInstance_T* raw = null;
            Vk.vkCreateInstance(chain.Head, null, &raw).ThrowIfFailed();

            var functions = new InstanceFunctionTable(raw);

            VkDebugUtilsMessengerEXT_T* messenger = null;
            if (desc.EnableValidation && functions.CreateDebugUtilsMessenger != null)
            {
                var mci = new VkDebugUtilsMessengerCreateInfoEXT
                {
                    sType = VkStructureType.VK_STRUCTURE_TYPE_DEBUG_UTILS_MESSENGER_CREATE_INFO_EXT,
                    messageSeverity = AllSeverities,
                    messageType = AllTypes,
                };

                if (desc.DebugCallbackRaw != null)
                {
                    mci.pfnUserCallback = desc.DebugCallbackRaw;
                }
                else if (desc.DebugCallback is not null)
                {
                    mci.pfnUserCallback = &ManagedCallbackThunk;
                    mci.pUserData = (void*)GCHandle.ToIntPtr(keepAlive);
                }
                else
                {
                    mci.pfnUserCallback = &DefaultCallback;
                }

                var r = functions.CreateDebugUtilsMessenger(raw, &mci, null, &messenger);
                if (r != VkResult.VK_SUCCESS)
                {
                    Vk.vkDestroyInstance(raw, null);
                    r.ThrowIfFailed();
                }
            }

            return new Instance(raw, messenger, functions, keepAlive);
        }
        catch
        {
            if (keepAlive.IsAllocated) keepAlive.Free();
            throw;
        }
    }

    private const uint AllSeverities =
        (uint)VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_VERBOSE_BIT_EXT |
        (uint)VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_INFO_BIT_EXT |
        (uint)VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT |
        (uint)VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT;

    private const uint AllTypes =
        (uint)VkDebugUtilsMessageTypeFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_GENERAL_BIT_EXT |
        (uint)VkDebugUtilsMessageTypeFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_VALIDATION_BIT_EXT |
        (uint)VkDebugUtilsMessageTypeFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_TYPE_PERFORMANCE_BIT_EXT;

    private static int CopyAndMaybeAppend(
        ReadOnlySpan<Utf8Name> input,
        Span<nint> dest,
        ReadOnlySpan<byte> autoAddIfNonEmpty)
    {
        int n = 0;
        for (int i = 0; i < input.Length; i++) dest[n++] = (nint)input[i].Ptr;
        if (autoAddIfNonEmpty.IsEmpty) return n;

        for (int i = 0; i < input.Length; i++)
        {
            if (PointerStringEquals((sbyte*)input[i].Ptr, autoAddIfNonEmpty)) return n;
        }
        dest[n++] = (nint)Unsafe.AsPointer(ref MemoryMarshal.GetReference(autoAddIfNonEmpty));
        return n;
    }

    private static bool PointerStringEquals(sbyte* p, ReadOnlySpan<byte> target)
    {
        if (p == null) return false;
        for (int i = 0; i < target.Length; i++)
        {
            if (p[i] == 0 || (byte)p[i] != target[i]) return false;
        }
        return p[target.Length] == 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint DefaultCallback(
        VkDebugUtilsMessageSeverityFlagBitsEXT severity,
        uint                                   type,
        VkDebugUtilsMessengerCallbackDataEXT*  data,
        void*                                  userData)
    {
        try
        {
            var msg = data != null ? Utf8.ToString(data->pMessage) : null;
            Console.Error.WriteLine($"[Vulkan {severity}] {msg}");
            if ((severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) != 0
                && Debugger.IsAttached)
            {
                Debugger.Break();
            }
        }
        catch
        {
            // Never throw across the unmanaged-to-managed boundary — Vulkan loader UB.
        }
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint ManagedCallbackThunk(
        VkDebugUtilsMessageSeverityFlagBitsEXT severity,
        uint                                   type,
        VkDebugUtilsMessengerCallbackDataEXT*  data,
        void*                                  userData)
    {
        if (userData == null || data == null) return 0;
        var handle = GCHandle.FromIntPtr((nint)userData);
        if (handle.Target is not Action<DebugMessage> cb) return 0;

        try
        {
            var msg = new DebugMessage(
                severity,
                (VkDebugUtilsMessageTypeFlagBitsEXT)type,
                Utf8.ToString(data->pMessage) ?? string.Empty,
                Utf8.ToString(data->pMessageIdName),
                data->messageIdNumber);

            cb(msg);
        }
        catch
        {
            // Swallow: never throw across the unmanaged-to-managed boundary.
        }
        return 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Messenger != null && Functions.DestroyDebugUtilsMessenger != null)
        {
            Functions.DestroyDebugUtilsMessenger(Handle, Messenger, null);
            Messenger = null;
        }
        if (Handle != null) Vk.vkDestroyInstance(Handle, null);
        if (_callbackKeepAlive.IsAllocated) _callbackKeepAlive.Free();
        GC.SuppressFinalize(this);
    }

    ~Instance()
    {
        Debug.Fail("Instance was not disposed.");
        Dispose();
    }
}
```

- [ ] **Step 5: Run all tests.**

```
dotnet test tests/Ahjo.Vulkan.Tests
```

Expected: all tests pass (or skip).

- [ ] **Step 6: Commit.**

```
git add src/Ahjo.Vulkan/Internal/InstanceFunctionTable.cs src/Ahjo.Vulkan/Lifecycle/Instance.cs tests/Ahjo.Vulkan.Tests/InstanceCreateTests.cs tests/Ahjo.Vulkan.Tests/InstanceFunctionTableTests.cs
git commit -m "Add InstanceFunctionTable + persistent post-create messenger (issue 06)"
```

---

## Task 13: Idempotent dispose + post-validation re-create

Two AC2-flavored tests proving disposal is well-behaved.

**Files:**
- Modify: `tests/Ahjo.Vulkan.Tests/InstanceCreateTests.cs`

- [ ] **Step 1: Add tests.**

Append to `InstanceCreateTests.cs`:

```csharp
    [Fact]
    public void Dispose_TwiceIsIdempotent()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        var instance = Instance.Create(new InstanceDescription { ApiVersion = VulkanVersion.V1_4 });
        instance.Dispose();
        instance.Dispose(); // must not throw
    }

    [Fact]
    public void Dispose_AfterValidationCreate_DestroysMessengerAndInstance()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver,           "No Vulkan driver on host.");
        Assert.SkipUnless(VulkanDriverProbe.HasValidationLayer,  "Validation layer not installed.");

        var first = Instance.Create(new InstanceDescription
        {
            ApiVersion = VulkanVersion.V1_4,
            EnableValidation = true,
        });
        first.Dispose();

        // If the prior dispose left the messenger or instance dangling we'd
        // see a driver-level error or layer error on a fresh create.
        using var second = Instance.Create(new InstanceDescription
        {
            ApiVersion = VulkanVersion.V1_4,
            EnableValidation = true,
        });

        Assert.True(second.Handle != null);
    }
```

- [ ] **Step 2: Run tests.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~Dispose"
```

Expected: 2 passed (or skipped).

- [ ] **Step 3: Commit.**

```
git add tests/Ahjo.Vulkan.Tests/InstanceCreateTests.cs
git commit -m "Add dispose idempotence + post-dispose re-create tests (issue 06)"
```

---

## Task 14: Failure-cleanup test

Verifies that a `vkCreateInstance` failure with a managed callback configured frees the GCHandle correctly (and the construction try/catch in Task 10 step 3c handles it). Proxy: a subsequent successful create must work.

**Files:**
- Modify: `tests/Ahjo.Vulkan.Tests/InstanceCreateTests.cs`

- [ ] **Step 1: Add the test.**

Append to `InstanceCreateTests.cs`:

```csharp
    [Fact]
    public void Create_FailureWithManagedCallback_FreesGCHandleAndAllowsSubsequentCreate()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver,           "No Vulkan driver on host.");
        Assert.SkipUnless(VulkanDriverProbe.HasValidationLayer,  "Validation layer not installed.");

        ReadOnlySpan<Utf8Name> bogus = stackalloc Utf8Name[]
        {
            Utf8Name.FromLiteral("VK_FAKE_extension_does_not_exist"u8),
        };

        Assert.Throws<VulkanException>(() => Instance.Create(new InstanceDescription
        {
            ApiVersion = VulkanVersion.V1_4,
            EnableValidation = true,
            Extensions = bogus,
            DebugCallback = _ => { },
        }));

        // Subsequent successful create must work — proves the failed-create
        // cleanup path freed its GCHandle and didn't leak driver state.
        using var ok = Instance.Create(new InstanceDescription
        {
            ApiVersion = VulkanVersion.V1_4,
            EnableValidation = true,
            DebugCallback = _ => { },
        });

        Assert.True(ok.Handle != null);
    }
```

- [ ] **Step 2: Run the test.**

```
dotnet test tests/Ahjo.Vulkan.Tests --filter "FullyQualifiedName~Create_FailureWithManagedCallback"
```

Expected: passes (or skipped).

- [ ] **Step 3: Final full test pass.**

```
dotnet test
```

Expected: every test in the solution passes (or skips appropriately). Watch for regressions in `HandleConventionsTests`, `ResultPolicyTests`, `VulkanLoaderSmokeTests`, `VmaSmokeTests`.

- [ ] **Step 4: Commit.**

```
git add tests/Ahjo.Vulkan.Tests/InstanceCreateTests.cs
git commit -m "Add failure-cleanup test for managed-callback path (issue 06)"
```

---

## Task 15: Close out the issue

- [ ] **Step 1: Confirm acceptance criteria.**

Manually re-check:
- AC1 — `Create_MinimalDescription_Succeeds` passes against the host driver.
- AC2 — `Dispose_TwiceIsIdempotent` + `Dispose_AfterValidationCreate_DestroysMessengerAndInstance` cover disposal.
- AC3 — `Create_WithValidation_DefaultCallback_FiresOnUnknownExtension`, `Create_WithValidation_ManagedCallback_RoundTripsMessage`, `Create_WithValidation_RawCallback_IsInvoked`, and `PersistentMessenger_FiresOnPostCreateValidationViolation` all cover validation callbacks firing.

- [ ] **Step 2: Push and let the user open the PR.**

```
git push
```

Do NOT open the PR yourself — defer that to the user.
