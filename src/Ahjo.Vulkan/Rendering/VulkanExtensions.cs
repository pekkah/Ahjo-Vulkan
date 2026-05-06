namespace Ahjo.Vulkan;

/// <summary>
/// Convenience accessors for the extension and layer name strings the
/// wrapper actively wraps. Always returned as
/// <see cref="Utf8Name"/> so callers can drop them straight into
/// <see cref="InstanceDescription.Extensions"/> /
/// <see cref="DeviceDescription.Extensions"/>. The underlying UTF-8
/// literals live in the assembly's read-only data segment — process
/// lifetime, no allocation.
/// </summary>
public static class VulkanExtensions
{
    /// <summary>VK_KHR_surface — instance-level. Required for any
    /// platform-specific surface creation.</summary>
    public static Utf8Name KhrSurface => Utf8Name.FromLiteral("VK_KHR_surface"u8);

    /// <summary>VK_KHR_win32_surface — instance-level. Pair with
    /// <see cref="KhrSurface"/> when creating a surface from an HWND
    /// via <see cref="Surface.CreateWin32"/>.</summary>
    public static Utf8Name KhrWin32Surface => Utf8Name.FromLiteral("VK_KHR_win32_surface"u8);

    /// <summary>VK_KHR_swapchain — device-level. Required for
    /// <see cref="Swapchain"/> creation and present.</summary>
    public static Utf8Name KhrSwapchain => Utf8Name.FromLiteral("VK_KHR_swapchain"u8);
}
