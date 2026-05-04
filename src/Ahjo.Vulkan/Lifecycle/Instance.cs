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
            mci.pfnUserCallback = &DefaultCallback;
            mci.pUserData = null;
        }

        VkInstance_T* raw = null;
        Vk.vkCreateInstance(chain.Head, null, &raw).ThrowIfFailed();

        return new Instance(raw, null);
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Messenger != null)
        {
            // persistent messenger destruction wired in Task 12
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
