using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Wrapper-suite-specific device queries: optional Vulkan 1.2 feature bits and
/// instance-extension presence. Tests gate on these through
/// <see cref="TestGate.RequireDeviceFeature"/> / <see cref="TestGate.RequirePlatform"/>.
/// </summary>
/// <remarks>
/// The driver / software-ICD / validation-layer probes moved to
/// <see cref="VulkanEnvironment"/> (issue #158) so all three Vulkan-touching
/// suites share one ladder and one declared-tier contract. The three properties
/// below forward there; nothing re-probes.
/// </remarks>
internal static unsafe class VulkanDriverProbe
{
    /// <inheritdoc cref="VulkanEnvironment.HasDriver"/>
    public static bool HasDriver => VulkanEnvironment.HasDriver;

    /// <inheritdoc cref="VulkanEnvironment.HasValidationLayer"/>
    public static bool HasValidationLayer => VulkanEnvironment.HasValidationLayer;

    /// <inheritdoc cref="VulkanEnvironment.IsSoftwareDriver"/>
    public static bool IsSoftwareDriver => VulkanEnvironment.IsSoftwareDriver;

    // Snapshot of VkPhysicalDeviceVulkan12Features for the first physical
    // device the loader enumerates — the same one
    // CreateGraphicsDevice in the tests will pick. Cached because every
    // bindless / descriptor-indexing test asks the same question. The
    // physical-device choice has to match what the tests do, so this stays
    // a "first device wins" probe instead of asking each test to drive its
    // own picker.
    private static readonly Lazy<VkPhysicalDeviceVulkan12Features> _features12 = new(() =>
    {
        if (!VulkanEnvironment.HasDriver) return default;

        VkInstance_T* instance = null;
        var ai = new VkApplicationInfo
        {
            sType      = VkStructureType.VK_STRUCTURE_TYPE_APPLICATION_INFO,
            apiVersion = (1u << 22) | (3u << 12),
        };
        var ci = new VkInstanceCreateInfo
        {
            sType            = VkStructureType.VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
            pApplicationInfo = &ai,
        };
        if (Vk.vkCreateInstance(&ci, null, &instance) != VkResult.VK_SUCCESS) return default;
        try
        {
            VkPhysicalDevice_T* gpu = null;
            uint one = 1;
            if (Vk.vkEnumeratePhysicalDevices(instance, &one, &gpu) is not (VkResult.VK_SUCCESS or VkResult.VK_INCOMPLETE) || gpu == null)
                return default;

            var f12 = new VkPhysicalDeviceVulkan12Features
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_2_FEATURES,
            };
            var f2 = new VkPhysicalDeviceFeatures2
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2,
                pNext = &f12,
            };
            Vk.vkGetPhysicalDeviceFeatures2(gpu, &f2);
            return f12;
        }
        finally
        {
            Vk.vkDestroyInstance(instance, null);
        }
    });

    /// <summary>
    /// <see langword="true"/> when the first enumerated GPU advertises
    /// the bits needed to allocate a partially-bound, update-after-bind
    /// storage-buffer array: <c>descriptorBindingPartiallyBound</c> and
    /// <c>descriptorBindingStorageBufferUpdateAfterBind</c>. Bindless
    /// storage-buffer tests gate on this — SwiftShader's Linux build
    /// reports neither.
    /// </summary>
    public static bool SupportsBindlessStorageBuffer
    {
        get
        {
            var f12 = _features12.Value;
            return f12.descriptorBindingPartiallyBound != 0
                && f12.descriptorBindingStorageBufferUpdateAfterBind != 0;
        }
    }

    /// <summary>
    /// <see langword="true"/> when the first enumerated GPU advertises
    /// the bits needed to declare a partially-bound,
    /// variable-descriptor-count, update-after-bind sampled-image array:
    /// <c>descriptorBindingPartiallyBound</c>,
    /// <c>descriptorBindingVariableDescriptorCount</c>, and
    /// <c>descriptorBindingSampledImageUpdateAfterBind</c>. Bindless
    /// texture-table tests gate on this.
    /// </summary>
    public static bool SupportsBindlessSampledImage
    {
        get
        {
            var f12 = _features12.Value;
            return f12.descriptorBindingPartiallyBound != 0
                && f12.descriptorBindingVariableDescriptorCount != 0
                && f12.descriptorBindingSampledImageUpdateAfterBind != 0;
        }
    }

    /// <summary>
    /// <see langword="true"/> when the first enumerated GPU advertises the bits
    /// needed to allocate a variable-descriptor-count storage-buffer heap:
    /// <c>descriptorBindingPartiallyBound</c>,
    /// <c>descriptorBindingVariableDescriptorCount</c> and
    /// <c>descriptorBindingStorageBufferUpdateAfterBind</c>. Issue #182's
    /// allocation tests gate on this.
    /// </summary>
    public static bool SupportsBindlessVariableCountStorageBuffer
    {
        get
        {
            var f12 = _features12.Value;
            return f12.descriptorBindingPartiallyBound != 0
                && f12.descriptorBindingVariableDescriptorCount != 0
                && f12.descriptorBindingStorageBufferUpdateAfterBind != 0;
        }
    }

    // Per-extension cache. Used by surface-extension tests so they can
    // skip cleanly when the ICD doesn't expose the platform extension
    // they target — e.g. SwiftShader on Linux ships VK_KHR_wayland_surface
    // but not VK_KHR_xlib_surface, so the Xlib argument-validation tests
    // would otherwise fail with VK_ERROR_EXTENSION_NOT_PRESENT at
    // Instance.Create time rather than testing what they mean to test.
    private static readonly Dictionary<string, bool> _extensionCache = new();

    public static bool HasInstanceExtension(ReadOnlySpan<byte> extension)
    {
        if (!VulkanEnvironment.HasDriver) return false;

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
                if (VulkanEnvironment.Match((sbyte*)entry, target)) return true;
            }
        }
        return false;
    }
}
