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
