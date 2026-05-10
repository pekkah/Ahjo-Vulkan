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
            fixed (VkLayerProperties* entry = &props[i])
            {
                if (Match((sbyte*)entry, target)) return true;
            }
        }
        return false;
    });

    // Distinguishes a software ICD (Mesa lavapipe / SwiftShader) from a
    // hardware driver. Tests that submit GPU work and depend on real
    // image/buffer copy semantics use this to skip on lavapipe, where
    // certain command-buffer paths SIGSEGV inside the driver. Hardware
    // drivers report DISCRETE_GPU / INTEGRATED_GPU / VIRTUAL_GPU; CPU
    // is the only deviceType the spec mandates for a software ICD.
    private static readonly Lazy<bool> _isSoftwareDriver = new(() =>
    {
        if (!_hasDriver.Value) return false;

        VkInstance_T* instance = null;
        var ai = new VkApplicationInfo
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_APPLICATION_INFO,
            apiVersion = (1u << 22) | (3u << 12),
        };
        var ci = new VkInstanceCreateInfo
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
            pApplicationInfo = &ai,
        };
        if (Vk.vkCreateInstance(&ci, null, &instance) != VkResult.VK_SUCCESS) return false;
        try
        {
            uint gpuCount = 0;
            if (Vk.vkEnumeratePhysicalDevices(instance, &gpuCount, null) != VkResult.VK_SUCCESS || gpuCount == 0)
                return false;

            VkPhysicalDevice_T* gpu = null;
            uint one = 1;
            if (Vk.vkEnumeratePhysicalDevices(instance, &one, &gpu) is not (VkResult.VK_SUCCESS or VkResult.VK_INCOMPLETE))
                return false;

            VkPhysicalDeviceProperties props;
            Vk.vkGetPhysicalDeviceProperties(gpu, &props);
            return props.deviceType == VkPhysicalDeviceType.VK_PHYSICAL_DEVICE_TYPE_CPU;
        }
        finally
        {
            Vk.vkDestroyInstance(instance, null);
        }
    });

    public static bool HasDriver => _hasDriver.Value;
    public static bool HasValidationLayer => _hasValidationLayer.Value;
    public static bool IsSoftwareDriver => _isSoftwareDriver.Value;

    // Per-extension cache. Used by surface-extension tests so they can
    // skip cleanly when the ICD doesn't expose the platform extension
    // they target — e.g. SwiftShader on Linux ships VK_KHR_wayland_surface
    // but not VK_KHR_xlib_surface, so the Xlib argument-validation tests
    // would otherwise fail with VK_ERROR_EXTENSION_NOT_PRESENT at
    // Instance.Create time rather than testing what they mean to test.
    private static readonly Dictionary<string, bool> _extensionCache = new();

    public static bool HasInstanceExtension(ReadOnlySpan<byte> extension)
    {
        if (!_hasDriver.Value) return false;

        var key = System.Text.Encoding.UTF8.GetString(extension);
        lock (_extensionCache)
        {
            if (_extensionCache.TryGetValue(key, out var cached)) return cached;
            bool present = ProbeInstanceExtension(extension);
            _extensionCache[key] = present;
            return present;
        }
    }

    private static bool ProbeInstanceExtension(ReadOnlySpan<byte> target)
    {
        uint count = 0;
        if (Vk.vkEnumerateInstanceExtensionProperties(null, &count, null) != VkResult.VK_SUCCESS || count == 0)
            return false;

        var props = new VkExtensionProperties[count];
        fixed (VkExtensionProperties* p = props)
        {
            if (Vk.vkEnumerateInstanceExtensionProperties(null, &count, p) != VkResult.VK_SUCCESS)
                return false;
        }

        for (int i = 0; i < count; i++)
        {
            fixed (VkExtensionProperties* entry = &props[i])
            {
                if (Match((sbyte*)entry, target)) return true;
            }
        }
        return false;
    }

    private static bool Match(sbyte* name, ReadOnlySpan<byte> target)
    {
        for (int i = 0; i < target.Length; i++)
        {
            if (name[i] == 0 || (byte)name[i] != target[i]) return false;
        }
        return name[target.Length] == 0;
    }
}
