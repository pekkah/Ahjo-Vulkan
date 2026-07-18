using System.Buffers;
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
/// <remarks>
/// <para><b>Thread safety.</b> An <see cref="Instance"/> is not thread-safe
/// for disposal — do not call <see cref="Dispose"/> concurrently from
/// multiple threads. Vulkan calls made through the wrapped handle are
/// thread-safe per the Vulkan spec (the underlying <c>VkInstance</c> is
/// externally synchronizable).</para>
/// </remarks>
public sealed unsafe class Instance : IDisposable
{
    internal readonly VkInstance_T*               Handle;
    internal          VkDebugUtilsMessengerEXT_T* Messenger;     // mutated by Dispose
    internal readonly InstanceFunctionTable       Functions;
    private  GCHandle                    _callbackKeepAlive;
    private  bool                        _disposed;
    // Single-writer field — populated lazily by GetOrCreatePhysicalDevice on
    // the first call that misses the cache. Concurrent picker calls would
    // each rebuild and overwrite the cache, breaking the "same handle =>
    // same PhysicalDevice instance" reference-equality contract documented
    // on PickPhysicalDevice. The wrapper does not lock — PickPhysicalDevice
    // is a startup-time call and the contract is "one picker call at a
    // time per Instance, or external sync."
    private  PhysicalDevice[]?           _physicalDeviceCache;

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

    /// <summary>
    /// Raw <c>VkInstance</c> handle as a pointer-sized integer, for
    /// interop with windowing libraries (SDL3, GLFW) that expect a
    /// <c>VkInstance</c> argument when creating surfaces. Mirrors
    /// <see cref="Device.RawHandle"/> / <see cref="PhysicalDevice.RawHandle"/>.
    /// </summary>
    public ulong RawHandle => (ulong)(nint)Handle;

    public static Instance Create(scoped in InstanceDescription desc)
    {
        uint apiVersion = desc.ApiVersion.Packed != 0 ? desc.ApiVersion.Packed : VulkanVersion.V1_4.Packed;

        // Confirm the auto-injected validation layer + debug-utils extension
        // are actually available before vkCreateInstance, so the failure mode
        // when the SDK isn't installed is a wrapper-level message naming the
        // missing piece instead of a bare VK_ERROR_LAYER_NOT_PRESENT /
        // VK_ERROR_EXTENSION_NOT_PRESENT from the loader. Costs two
        // enumerations on the validation-on path; nothing on validation-off.
        if (desc.EnableValidation)
        {
            EnsureInstanceLayerPresent(InstanceExtensionNames.KhronosValidationLayer);
            EnsureInstanceExtensionPresent(InstanceExtensionNames.DebugUtilsExtension);
        }

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

            // Anything between vkCreateInstance and the final return that
            // throws — function-table resolve, the validation-extension-
            // missing throw, messenger create failure, managed OOM at
            // `new Instance` — needs to roll the live VkInstance back;
            // otherwise the handle outlives its managed owner and the
            // GPU/driver stays tied to a dead wrapper.
            VkDebugUtilsMessengerEXT_T* messenger = null;
            InstanceFunctionTable       functions = default;
            try
            {
                functions = new InstanceFunctionTable(raw);

                if (desc.EnableValidation)
                {
                    // VK_EXT_debug_utils is auto-added when EnableValidation
                    // is true (see CopyAndMaybeAppend above), so a null
                    // entry-point here means the loader has the extension
                    // declared but the function pointer didn't resolve —
                    // typically a validation-layer-less SDK install.
                    // Silently dropping the messenger would let
                    // validation = true succeed without ever running a
                    // callback after instance creation; surface the gap
                    // loud so the caller fixes the install.
                    if (functions.CreateDebugUtilsMessenger == null)
                        throw new VulkanException(VkResult.VK_ERROR_EXTENSION_NOT_PRESENT,
                            "EnableValidation = true but vkCreateDebugUtilsMessengerEXT could not be resolved. " +
                            "Install the Vulkan SDK validation layers, or set EnableValidation = false.");

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

                    // Stage into a local — on failure the Vulkan spec
                    // leaves pMessenger's value undefined, so committing
                    // to `messenger` only on success keeps the catch
                    // path from invoking destroy on garbage.
                    VkDebugUtilsMessengerEXT_T* msg = null;
                    functions.CreateDebugUtilsMessenger(raw, &mci, null, &msg).ThrowIfFailed();
                    messenger = msg;
                }

                return new Instance(raw, messenger, functions, keepAlive);
            }
            catch
            {
                if (messenger != null && functions.DestroyDebugUtilsMessenger != null)
                    functions.DestroyDebugUtilsMessenger(raw, messenger, null);
                Vk.vkDestroyInstance(raw, null);
                throw;
            }
        }
        catch
        {
            if (keepAlive.IsAllocated) keepAlive.Free();
            throw;
        }
    }

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
        Vk.vkEnumeratePhysicalDevices(Handle, &count, null).ThrowIfErrored();
        if (count == 0)
            throw new VulkanException(VkResult.VK_ERROR_INITIALIZATION_FAILED,
                "No Vulkan physical devices reported by the driver.");

        Span<nint> deviceHandles = count <= 16
            ? stackalloc nint[(int)count]
            : new nint[count];
        fixed (nint* p = deviceHandles)
            Vk.vkEnumeratePhysicalDevices(Handle, &count, (VkPhysicalDevice_T**)p).ThrowIfErrored();

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
                uint deviceApiVersion = pchain.Head->properties.apiVersion;

                // 2b. Features chain — base + 1.1/1.2/1.3 always, plus 1.4
                // only when the device advertises Vulkan 1.4. Same gate as
                // the device-create path in PhysicalDevice.CreateDevice:
                // SwiftShader (and other 1.3-only ICDs) log
                // "UNSUPPORTED: curExtension->sType: 55" — the
                // VkPhysicalDeviceVulkan14Features sType — when the struct
                // sits in the read-back chain, and the cumulative state
                // damage manifests as later SIGSEGVs in unrelated entry
                // points. Mirroring the create-path gate keeps both probe
                // and create chains internally consistent for the same GPU.
                featuresChain.Clear();
                var fchain = ChainBuilder.For<VkPhysicalDeviceFeatures2>(featuresChain);
                fchain.Root();
                ref var f11 = ref fchain.Push<VkPhysicalDeviceVulkan11Features>();
                ref var f12 = ref fchain.Push<VkPhysicalDeviceVulkan12Features>();
                ref var f13 = ref fchain.Push<VkPhysicalDeviceVulkan13Features>();
                VkPhysicalDeviceVulkan14Features f14Local = default;
                ref VkPhysicalDeviceVulkan14Features f14 = ref f14Local;
                if (deviceApiVersion >= VulkanVersion.V1_4.Packed)
                    f14 = ref fchain.Push<VkPhysicalDeviceVulkan14Features>();
                Vk.vkGetPhysicalDeviceFeatures2(d, fchain.Head);

                // 2c. Memory.
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
                Vk.vkEnumerateDeviceExtensionProperties(d, null, &extCount, null).ThrowIfErrored();
                if (extBuf.Length < extCount)
                {
                    if (extBuf.Length != 0) extPool.Return(extBuf);
                    extBuf = extCount == 0 ? [] : extPool.Rent((int)extCount);
                }
                if (extCount > 0)
                {
                    fixed (VkExtensionProperties* ep = extBuf)
                        Vk.vkEnumerateDeviceExtensionProperties(d, null, &extCount, ep).ThrowIfErrored();
                }

                // 2f. Build the picker view and dispatch.
                ref var props2 = ref *pchain.Head;
                ref var feats2 = ref *fchain.Head;
                var gpu = GetOrCreatePhysicalDevice(d);
                var info = new PhysicalDeviceInfo(
                    device:        gpu,
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
                    return gpu;
            }
        }
        finally
        {
            if (extBuf.Length != 0) extPool.Return(extBuf);
        }

        throw new VulkanException(VkResult.VK_ERROR_INITIALIZATION_FAILED,
            "No physical device matched the picker.");
    }

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
        return PopulateCacheAndFind(handle);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private PhysicalDevice PopulateCacheAndFind(VkPhysicalDevice_T* handle)
    {
        uint count = 0;
        Vk.vkEnumeratePhysicalDevices(Handle, &count, null).ThrowIfErrored();

        var fresh = new PhysicalDevice[count];
        if (count > 0)
        {
            Span<nint> raw = count <= 16
                ? stackalloc nint[(int)count]
                : new nint[count];
            fixed (nint* p = raw)
                Vk.vkEnumeratePhysicalDevices(Handle, &count, (VkPhysicalDevice_T**)p).ThrowIfErrored();

            for (int i = 0; i < (int)count; i++)
                fresh[i] = new PhysicalDevice(this, (VkPhysicalDevice_T*)raw[i]);
        }
        _physicalDeviceCache = fresh;

        for (int i = 0; i < fresh.Length; i++)
            if (fresh[i].Handle == handle)
                return fresh[i];

        // Unreachable on a well-behaved driver: caller observed `handle` from
        // vkEnumeratePhysicalDevices, and the spec doesn't allow the set to
        // shrink mid-frame.
        throw new VulkanException(VkResult.VK_ERROR_INITIALIZATION_FAILED,
            "PhysicalDevice handle was not in the freshly-enumerated set.");
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

    /// <summary>
    /// Compares a NUL-terminated UTF-8 string at <paramref name="p"/>
    /// against the (non-NUL-terminated) <paramref name="target"/> bytes.
    /// Returns true iff the strings are equal *and* the source is exactly
    /// <c>target.Length</c> bytes long (so a longer source whose prefix
    /// matches returns false).
    /// </summary>
    /// <remarks>
    /// Reads <paramref name="p"/>[<paramref name="target"/>.Length] to
    /// confirm the NUL terminator. Safe only when the caller can guarantee
    /// <paramref name="p"/> is a real C-style NUL-terminated string — the
    /// Vulkan loader / VK_LAYER_/VK_EXT_ constants in this assembly always
    /// are (UTF-8 string literals carry an implicit trailing NUL byte
    /// past <c>span.Length</c>; <see cref="Utf8Name"/>'s contract enforces
    /// that callers pass only such literals). Do not feed this function a
    /// pointer into a byte buffer the caller authored without first
    /// asserting NUL termination.
    /// </remarks>
    private static bool PointerStringEquals(sbyte* p, ReadOnlySpan<byte> target)
    {
        if (p == null) return false;
        for (int i = 0; i < target.Length; i++)
        {
            if (p[i] == 0 || (byte)p[i] != target[i]) return false;
        }
        return p[target.Length] == 0;
    }

    private static void EnsureInstanceLayerPresent(ReadOnlySpan<byte> layerName)
    {
        uint count = 0;
        Vk.vkEnumerateInstanceLayerProperties(&count, null).ThrowIfErrored();
        if (count == 0)
            throw new VulkanException(VkResult.VK_ERROR_LAYER_NOT_PRESENT,
                $"EnableValidation = true but the loader reports no instance layers — install the Vulkan SDK validation layers, or set EnableValidation = false (looking for '{System.Text.Encoding.UTF8.GetString(layerName)}').");

        var pool = ArrayPool<VkLayerProperties>.Shared;
        var buf  = pool.Rent((int)count);
        try
        {
            fixed (VkLayerProperties* p = buf)
                Vk.vkEnumerateInstanceLayerProperties(&count, p).ThrowIfErrored();
            for (int i = 0; i < (int)count; i++)
            {
                ref readonly var first = ref buf[i].layerName.e0;
                if (PointerStringEquals(
                        (sbyte*)Unsafe.AsPointer(ref Unsafe.AsRef(in first)), layerName))
                    return;
            }
        }
        finally { pool.Return(buf); }

        throw new VulkanException(VkResult.VK_ERROR_LAYER_NOT_PRESENT,
            $"EnableValidation = true but instance layer '{System.Text.Encoding.UTF8.GetString(layerName)}' is not installed on this host. Install the Vulkan SDK validation layers, or set EnableValidation = false.");
    }

    /// <summary>
    /// True when the loader/ICDs advertise the given instance extension —
    /// callable before any instance exists
    /// (<c>vkEnumerateInstanceExtensionProperties</c> is instance-less).
    /// Use it to decide OPTIONAL extensions (e.g.
    /// <see cref="VulkanExtensions.ExtHeadlessSurface"/>) up front: probing
    /// by attempting <see cref="Create"/> with the extension in the list
    /// makes the loader report an error through any active debug messenger
    /// before the <see cref="VulkanException"/> surfaces, which pollutes
    /// validation-as-oracle captures. Returns <see langword="false"/> for a
    /// null name and on hosts with no <c>vulkan-1</c> loader at all (no
    /// loader ⇒ no extensions — the capability answer, not an error).
    /// </summary>
    public static bool IsExtensionSupported(Utf8Name extension)
        => !extension.IsNull
           && IsExtensionSupported(
               MemoryMarshal.CreateReadOnlySpanFromNullTerminated((byte*)extension.Ptr));

    /// <inheritdoc cref="IsExtensionSupported(Utf8Name)"/>
    public static bool IsExtensionSupported(ReadOnlySpan<byte> utf8ExtensionName)
    {
        if (utf8ExtensionName.IsEmpty) return false;

        uint count = 0;
        try
        {
            Vk.vkEnumerateInstanceExtensionProperties(null, &count, null).ThrowIfErrored();
        }
        catch (DllNotFoundException)
        {
            return false;
        }

        if (count == 0) return false;

        var pool = ArrayPool<VkExtensionProperties>.Shared;
        var buf  = pool.Rent((int)count);
        try
        {
            fixed (VkExtensionProperties* p = buf)
                Vk.vkEnumerateInstanceExtensionProperties(null, &count, p).ThrowIfErrored();
            for (int i = 0; i < (int)count; i++)
            {
                ref readonly var first = ref buf[i].extensionName.e0;
                if (PointerStringEquals(
                        (sbyte*)Unsafe.AsPointer(ref Unsafe.AsRef(in first)), utf8ExtensionName))
                    return true;
            }
        }
        finally { pool.Return(buf); }

        return false;
    }

    private static void EnsureInstanceExtensionPresent(ReadOnlySpan<byte> extensionName)
    {
        uint count = 0;
        Vk.vkEnumerateInstanceExtensionProperties(null, &count, null).ThrowIfErrored();
        if (count == 0)
            throw new VulkanException(VkResult.VK_ERROR_EXTENSION_NOT_PRESENT,
                $"EnableValidation = true but the loader reports no instance extensions — install the Vulkan SDK, or set EnableValidation = false (looking for '{System.Text.Encoding.UTF8.GetString(extensionName)}').");

        var pool = ArrayPool<VkExtensionProperties>.Shared;
        var buf  = pool.Rent((int)count);
        try
        {
            fixed (VkExtensionProperties* p = buf)
                Vk.vkEnumerateInstanceExtensionProperties(null, &count, p).ThrowIfErrored();
            for (int i = 0; i < (int)count; i++)
            {
                ref readonly var first = ref buf[i].extensionName.e0;
                if (PointerStringEquals(
                        (sbyte*)Unsafe.AsPointer(ref Unsafe.AsRef(in first)), extensionName))
                    return;
            }
        }
        finally { pool.Return(buf); }

        throw new VulkanException(VkResult.VK_ERROR_EXTENSION_NOT_PRESENT,
            $"EnableValidation = true but instance extension '{System.Text.Encoding.UTF8.GetString(extensionName)}' is not advertised by the loader. Install the Vulkan SDK, or set EnableValidation = false.");
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
            DiagnosticSeverity mapped =
                (severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT)   != 0 ? DiagnosticSeverity.Error :
                (severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT) != 0 ? DiagnosticSeverity.Warning :
                                                                                                                           DiagnosticSeverity.Info;
            AhjoDiagnostics.Write(mapped, "Vulkan", $"[Vulkan {severity}] {msg}");
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
        try
        {
            if (Messenger != null && Functions.DestroyDebugUtilsMessenger != null)
            {
                Functions.DestroyDebugUtilsMessenger(Handle, Messenger, null);
                Messenger = null;
            }
            if (Handle != null) Vk.vkDestroyInstance(Handle, null);
            if (_callbackKeepAlive.IsAllocated) _callbackKeepAlive.Free();
        }
        finally
        {
            // Set the flag and suppress the finalizer in finally so a throw
            // out of destroy can't leave the handle alive AND have the
            // finalizer re-enter Dispose to destroy it a second time
            // (vkDestroyInstance on an already-destroyed handle is UB). The
            // tradeoff is that a destroy failure leaks the handle for the
            // rest of the process — preferable to UB.
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    ~Instance()
    {
        Debug.Fail("Instance was not disposed.");
        Dispose();
    }
}
