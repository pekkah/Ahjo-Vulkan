# Issue #6 — Instance creation API

Status: design (pending plan)
Date: 2026-05-04
Issue: https://github.com/pekkah/ahjo-vulkan/issues/6
Depends on: #3 (handle conventions), #4 (`ChainBuilder`), #5 (`VkResult` policy). Transitive: #2 (VMA coupling, closed).

## 1. Goal

A one-line `Instance.Create(...)` that gives callers a working `VkInstance` against the host driver, with optional Khronos validation and a debug-utils messenger that surfaces driver/validation messages during *and* after instance creation. Vulkan 1.4 is the default API version. Allocations on the create path are bounded and small; per-frame use is irrelevant since instance creation is a once-per-process event.

## 2. Public surface

### `VulkanVersion`

```csharp
public readonly record struct VulkanVersion(uint Packed)
{
    public static VulkanVersion V1_0 { get; } = Make(1, 0, 0);
    public static VulkanVersion V1_1 { get; } = Make(1, 1, 0);
    public static VulkanVersion V1_2 { get; } = Make(1, 2, 0);
    public static VulkanVersion V1_3 { get; } = Make(1, 3, 0);
    public static VulkanVersion V1_4 { get; } = Make(1, 4, 0);

    public static VulkanVersion Make(uint major, uint minor, uint patch)
        => new((major << 22) | (minor << 12) | patch);

    public uint Major => (Packed >> 22) & 0x7F;
    public uint Minor => (Packed >> 12) & 0x3FF;
    public uint Patch =>  Packed         & 0xFFF;
}
```

The Vulkan headers express version values via the `VK_MAKE_API_VERSION` macro, which the binding generator does not materialize. `VulkanVersion` is the strongly-typed replacement; `Packed` is what `VkApplicationInfo.apiVersion` wants.

### `Utf8Name`

A pointer wrapper that lets callers pass UTF-8 string literals as a `params` collection. `ReadOnlySpan<byte>` is a `ref struct`, so `ReadOnlySpan<ReadOnlySpan<byte>>` and `ReadOnlySpan<byte>[]` are both rejected by the compiler. Wrapping the pointer is the workaround.

```csharp
public readonly unsafe struct Utf8Name
{
    public readonly sbyte* Ptr;
    public Utf8Name(sbyte* ptr) => Ptr = ptr;

    public static Utf8Name From(ReadOnlySpan<byte> utf8Literal)
    {
        Debug.Assert(utf8Literal.Length > 0, "Utf8Name requires a non-empty UTF-8 literal.");
        return new Utf8Name(
            (sbyte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(utf8Literal)));
    }

    public static implicit operator Utf8Name(ReadOnlySpan<byte> s) => From(s);
    public bool IsNull => Ptr == null;
}
```

Call sites read `"VK_KHR_surface"u8` and the implicit conversion produces a `Utf8Name`. Per the C# spec, `"…"u8` literals are emitted into the assembly's read-only data segment with a trailing null byte; the resulting `ReadOnlySpan<byte>` covers the UTF-8 bytes of the content (Length excludes the trailing null), but `(sbyte*)&span[0]` points at storage that *is* null-terminated, so passing it to a Vulkan API that wants `const char*` is safe and the lifetime is the process. We cannot assert null-termination from the span alone (the null is past `Length`); the `Length > 0` assert is the cheapest guard against a `default(ReadOnlySpan<byte>)` slipping through.

### `DebugMessage`

```csharp
public readonly record struct DebugMessage(
    VkDebugUtilsMessageSeverityFlagBitsEXT Severity,
    VkDebugUtilsMessageTypeFlagBitsEXT     Type,
    string                                 Message,
    string?                                MessageIdName,
    int                                    MessageIdNumber);
```

The friendly form delivered to the managed `Action<DebugMessage>` callback. Marshalled once per validation message in the unmanaged trampoline; never on a hot path.

### `InstanceDescription`

```csharp
public ref struct InstanceDescription
{
    public Utf8Name      ApplicationName;     // optional; default is null pointer
    public Utf8Name      EngineName;          // optional
    public uint          ApplicationVersion;  // optional; default 0
    public uint          EngineVersion;       // optional; default 0
    public VulkanVersion ApiVersion;          // defaults to V1_4 if Packed == 0

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

`ref struct` because `ReadOnlySpan<Utf8Name>` cannot live inside a non-`ref` type. Defaults are intentionally zero/null so callers only set what they care about.

### `Instance`

```csharp
public sealed unsafe class Instance : IDisposable
{
    internal VkInstance_T*               Handle;
    internal VkDebugUtilsMessengerEXT_T* Messenger;          // null when validation off
    internal InstanceFunctionTable       Functions;          // resolved extension entry points

    public static Instance Create(scoped in InstanceDescription desc);

    public void Dispose();
    ~Instance();
}
```

`class`, not `readonly struct`. Justification:

- `Instance` is created once and lives the lifetime of the app. The hot-path / per-frame / copy-by-value reasons that motivate the struct-handle convention (see `IVulkanHandle<TSelf>` doc) do not apply.
- A finalizer can backstop a missed `Dispose` — leaking a `VkInstance` is a real driver-level resource leak, not just a managed-heap blip.
- Holding the persistent messenger pointer, the `GCHandle` for the managed callback (when used), and the `InstanceFunctionTable` is structurally cleaner as fields on a class than as side-tables keyed on a struct's pointer.
- Reference equality is the natural identity for a singleton-per-process resource. `Device`, `Surface`, etc. will hold a reference back to it.
- `IVulkanHandle<TSelf>` requires `TSelf : unmanaged`. `Instance` does not need to be an `IVulkanHandle` — that interface exists for debug-naming and pool keys (issue #25), neither of which apply to instance.

The struct-handle convention stands for everything that legitimately benefits from it (`Buffer`, `Image`, `Queue`, `CommandBuffer`, etc.).

## 3. Behavior

### 3.1 Argument resolution

- **Layers.** If `EnableValidation` is true, `VK_LAYER_KHRONOS_validation` is appended to the layer list unless already present (linear `strcmp` scan; lists are tiny).
- **Extensions.** If `EnableValidation` is true, `VK_EXT_debug_utils` is appended to the extension list unless already present (same scan).
- **Application info.** Names default to null pointers (Vulkan accepts `pApplicationName == NULL`); versions default to 0; `apiVersion` falls back to `VulkanVersion.V1_4.Packed` if `desc.ApiVersion.Packed` is 0.

### 3.2 Validation messenger wiring

When `EnableValidation` is true:

1. Build a `VkDebugUtilsMessengerCreateInfoEXT` with severity `VERBOSE | INFO | WARNING | ERROR` and type `GENERAL | VALIDATION | PERFORMANCE` (full coverage — debug builds want everything).
2. **Chain** that struct into `VkInstanceCreateInfo.pNext` via `ChainBuilder.For<VkInstanceCreateInfo>(stackalloc byte[256])`. This catches messages emitted *during* `vkCreateInstance` and `vkDestroyInstance`, including the "unknown extension" path acceptance test exercises.
3. **Also create a persistent messenger** after `vkCreateInstance` succeeds: resolve `vkCreateDebugUtilsMessengerEXT` via `vkGetInstanceProcAddr` (the function is an extension entry, not exported by the loader directly), call it with the same `VkDebugUtilsMessengerCreateInfoEXT`, store the resulting `VkDebugUtilsMessengerEXT_T*` on the instance.

### 3.3 Callback selection

In priority order:

1. `desc.DebugCallbackRaw` if non-null — used as `pfnUserCallback` directly. `pUserData = null`.
2. `desc.DebugCallback` if non-null — `pfnUserCallback = &ManagedCallbackThunk`, `pUserData = (void*)GCHandle.ToIntPtr(GCHandle.Alloc(cb))`. The `GCHandle` is freed in `Dispose` and in the finalizer.
3. Otherwise (no caller-supplied callback) — `pfnUserCallback = &DefaultCallback` which writes to `Console.Error` and `Debugger.Break()`s on `ERROR` severity if `Debugger.IsAttached`.

### 3.4 Disposal order

```
free GCHandle (if allocated)
vkDestroyDebugUtilsMessengerEXT(Handle, Messenger, null)   // if Messenger != null
vkDestroyInstance(Handle, null)                            // if Handle != null
```

Reverse of construction; idempotent via a `_disposed` guard. The finalizer asserts in `DEBUG` and runs the same path (best-effort) in `RELEASE` so a missed `Dispose` doesn't strand the driver.

### 3.5 Failure handling

- `vkCreateInstance` returns non-success → `ThrowIfFailed` raises `VulkanException` (per issue #4 policy). Nothing to clean up; nothing was created.
- `vkCreateInstance` succeeds, persistent messenger creation fails → call `vkDestroyInstance` and rethrow. Wrapped in try/finally.
- `GCHandle.Alloc` fails → cannot happen in practice (only fails on OOM, in which case nothing else is going to work either); not specially handled.

## 4. File layout

| Path | Kind | Purpose |
|---|---|---|
| `src/Ahjo.Vulkan/Lifecycle/Instance.cs` | new | The `Instance` class. |
| `src/Ahjo.Vulkan/Lifecycle/InstanceDescription.cs` | new | The input `ref struct`. |
| `src/Ahjo.Vulkan/Lifecycle/VulkanVersion.cs` | new | Strongly-typed version `record struct`. |
| `src/Ahjo.Vulkan/Lifecycle/Utf8Name.cs` | new | UTF-8 pointer wrapper. |
| `src/Ahjo.Vulkan/Lifecycle/DebugMessage.cs` | new | Public callback record. |
| `src/Ahjo.Vulkan/Internal/InstanceFunctionTable.cs` | new | Caches extension entry points (`vkCreateDebugUtilsMessengerEXT`, `vkDestroyDebugUtilsMessengerEXT`, plus a `Resolve<T>(Utf8Name)` helper for future issues). |
| `src/Ahjo.Vulkan/Internal/Utf8.cs` | new | `ToString(sbyte*)`, `ToString(ReadOnlySpan<byte>)` helpers. |
| `src/Ahjo.Vulkan/Internal/InstanceExtensionNames.cs` | new | UTF-8 literals for `VK_EXT_debug_utils`, `VK_LAYER_KHRONOS_validation`, and the symbol names resolved through `vkGetInstanceProcAddr`. |
| `tests/Ahjo.Vulkan.Tests/InstanceCreateTests.cs` | new | Integration tests (see §5). |
| `tests/Ahjo.Vulkan.Tests/InstanceFunctionTableTests.cs` | new | Resolve known/unknown function names through a live instance. |
| `tests/Ahjo.Vulkan.Tests/Utf8Tests.cs` | new | `Utf8.ToString` round-trip + null. |
| `tests/Ahjo.Vulkan.Tests/VulkanVersionTests.cs` | new | Pure unit tests on `Make` / accessors. |

## 5. Tests

Existing test convention: xUnit v3, one class per type, descriptive method names. Driver-dependent tests probe `vkEnumerateInstanceVersion` first and skip if no driver is present (matches `VulkanLoaderSmokeTests`).

### `VulkanVersionTests` (no driver)

- `Make_PacksMajorMinorPatch` — `VulkanVersion.Make(1, 4, 7).Packed` round-trips through `Major`/`Minor`/`Patch`.
- `V1_4_HasExpectedPackedValue` — sanity check against the known constant.

### `Utf8Tests` (no driver)

- `ToString_NullPointer_ReturnsNull`.
- `ToString_RoundTripsUtf8` — feeds a UTF-8 byte literal, expects the matching .NET string.

### `InstanceCreateTests` (driver required)

- `Create_MinimalDescription_Succeeds` — empty description, `ApiVersion = V1_4`. `instance.Handle != null`. Disposes cleanly.
- `Create_DefaultsApiVersionWhenZero` — passes `default(InstanceDescription)`. Asserts that creation succeeds (i.e. the default fell back to `V1_4`).
- `Create_WithValidation_DefaultCallback_FiresOnUnknownExtension` — passes `Extensions = ["VK_FAKE_extension_does_not_exist"u8]`. `Create` is expected to throw a `VulkanException` with `VK_ERROR_EXTENSION_NOT_PRESENT`. Validation callback should have fired at least once *before* the throw — the chained `pNext` messenger catches the rejection in the loader. Captures `Console.Error` to verify.
- `Create_WithValidation_ManagedCallback_RoundTripsMessage` — same trigger, but with `DebugCallback = msg => list.Add(msg)`. Asserts `list.Count >= 1` and that `Severity` includes `ERROR_BIT_EXT`.
- `Create_WithValidation_RawCallback_IsInvoked` — same trigger, `DebugCallbackRaw = &CountingThunk` (a `[UnmanagedCallersOnly]` static that increments a static counter). Asserts the counter went up.
- `Dispose_TwiceIsIdempotent` — Dispose; Dispose. No throw, no double-free.
- `Dispose_AfterValidationCreate_DestroysMessengerAndInstance` — proxy: create with validation, dispose, create *again* with validation. If the prior dispose left the messenger or instance dangling we'd see a driver-level error or a layer error on the second create.

### `InstanceFunctionTableTests` (driver required)

- `Resolve_KnownExtension_ReturnsNonNull` — through a live `Instance` with validation enabled, resolve `vkCreateDebugUtilsMessengerEXT` and assert non-null.
- `Resolve_UnknownName_ReturnsNull` — resolve `"vkDoesNotExist"u8` and assert null.

## 6. Acceptance mapping

| Issue acceptance | Test |
|---|---|
| `Instance.Create` succeeds against the host driver | `Create_MinimalDescription_Succeeds` |
| Disposal calls `vkDestroyInstance` | `Dispose_AfterValidationCreate_DestroysMessengerAndInstance` (proxy: a second create works) + `Dispose_TwiceIsIdempotent` |
| Validation-enabled path produces ≥1 callback firing for an intentional misuse | `Create_WithValidation_DefaultCallback_FiresOnUnknownExtension`, `Create_WithValidation_ManagedCallback_RoundTripsMessage`, `Create_WithValidation_RawCallback_IsInvoked` |

## 7. Allocation budget

Steady-state per `Instance.Create`:

- Stack: `VkApplicationInfo` (~32 B), `VkInstanceCreateInfo` chain buffer (256 B), two `Utf8Name` scratch spans (≤ ~80 B each), one `VkDebugUtilsMessengerCreateInfoEXT` (~48 B, lives inside the chain buffer).
- Heap: one `Instance` object; one `GCHandle.Alloc` *only when* a managed `DebugCallback` is supplied. Default and raw paths allocate nothing on the heap beyond the `Instance` itself.

Per validation message (only when validation enabled and managed callback is in use):

- One `DebugMessage` record-struct (stack — it's a struct).
- Up to two `string` instances (`Message`, `MessageIdName`) via `Marshal.PtrToStringUTF8`.

Validation messages are not a hot path; this allocation rate is irrelevant.

## 8. Out of scope

- `PhysicalDevice` enumeration — issue #7's whole scope.
- Multi-callback fanout — YAGNI; callers can wrap two callbacks themselves.
- A cross-cutting "function table per scope" abstraction — `InstanceFunctionTable` is intentionally instance-scoped; `Device`-level entry points get their own table in issue #8.

## 9. Risks

- **No driver in CI.** Mitigated by skip-if-no-driver helper, matching the existing pattern.
- **Loader name resolution.** `vkGetInstanceProcAddr` for extension functions is well-defined per-spec; the only failure mode is "extension not enabled," which we control.
- **`GCHandle` leak on construction failure.** Allocate the `GCHandle` only after `vkCreateInstance` succeeds, then place it in a try/finally that frees it if subsequent steps throw.
