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
