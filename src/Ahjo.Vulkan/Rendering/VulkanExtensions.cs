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

    /// <summary>VK_KHR_xlib_surface — instance-level. Pair with
    /// <see cref="KhrSurface"/> when creating a surface from an Xlib
    /// <c>Display*</c> + <c>Window</c> via
    /// <see cref="Surface.CreateXlib"/>.</summary>
    public static Utf8Name KhrXlibSurface => Utf8Name.FromLiteral("VK_KHR_xlib_surface"u8);

    /// <summary>VK_KHR_wayland_surface — instance-level. Pair with
    /// <see cref="KhrSurface"/> when creating a surface from a Wayland
    /// <c>wl_display*</c> + <c>wl_surface*</c> via
    /// <see cref="Surface.CreateWayland"/>.</summary>
    public static Utf8Name KhrWaylandSurface => Utf8Name.FromLiteral("VK_KHR_wayland_surface"u8);

    /// <summary>VK_EXT_metal_surface — instance-level (MoltenVK on
    /// macOS). Pair with <see cref="KhrSurface"/> when creating a
    /// surface from a Cocoa <c>CAMetalLayer</c> via
    /// <see cref="Surface.CreateMetal"/>.</summary>
    public static Utf8Name ExtMetalSurface => Utf8Name.FromLiteral("VK_EXT_metal_surface"u8);

    /// <summary>VK_EXT_headless_surface — instance-level. Pair with
    /// <see cref="KhrSurface"/> to create a window-system-independent
    /// surface via <see cref="Surface.CreateHeadless"/>. Implemented by
    /// Mesa (lavapipe), so it lets the WSI stack — caps queries, formats,
    /// swapchain create, acquire/present — run on hosted CI runners with
    /// no display server attached.</summary>
    public static Utf8Name ExtHeadlessSurface => Utf8Name.FromLiteral("VK_EXT_headless_surface"u8);

    /// <summary>VK_KHR_swapchain — device-level. Required for
    /// <see cref="Swapchain"/> creation and present.</summary>
    public static Utf8Name KhrSwapchain => Utf8Name.FromLiteral("VK_KHR_swapchain"u8);
}
