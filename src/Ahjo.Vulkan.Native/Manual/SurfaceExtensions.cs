using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Native;

/// <summary>
/// Hand-authored bindings for the platform-specific surface extensions.
/// The clang-sharp generator skips these because they pull in
/// <c>windows.h</c> / <c>X11.h</c> / <c>wayland-client.h</c> /
/// <c>QuartzCore</c> for the platform handle types — far heavier than
/// what they're worth here. We declare the platform handles as
/// <c>nint</c> (or raw pointers) directly, side-stepping the include
/// chain.
/// </summary>
public static unsafe partial class Vk
{
    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateWin32SurfaceKHR(
        [NativeTypeName("VkInstance")] VkInstance_T*                            instance,
        [NativeTypeName("const VkWin32SurfaceCreateInfoKHR *")] VkWin32SurfaceCreateInfoKHR* pCreateInfo,
        [NativeTypeName("const VkAllocationCallbacks *")]       VkAllocationCallbacks*       pAllocator,
        [NativeTypeName("VkSurfaceKHR *")]                      VkSurfaceKHR_T**             pSurface);

    /// <summary>
    /// Probe whether <paramref name="physicalDevice"/>'s queue family
    /// <paramref name="queueFamilyIndex"/> can present to the Win32
    /// desktop window manager. Companion to
    /// <see cref="vkGetPhysicalDeviceSurfaceSupportKHR"/> — one is for
    /// "can this queue talk to <i>any</i> surface", the other answers
    /// "can this queue present to the desktop at all".
    /// </summary>
    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("VkBool32")]
    public static extern uint vkGetPhysicalDeviceWin32PresentationSupportKHR(
        [NativeTypeName("VkPhysicalDevice")] VkPhysicalDevice_T* physicalDevice,
        [NativeTypeName("uint32_t")]         uint                queueFamilyIndex);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateXlibSurfaceKHR(
        [NativeTypeName("VkInstance")]                          VkInstance_T*               instance,
        [NativeTypeName("const VkXlibSurfaceCreateInfoKHR *")]  VkXlibSurfaceCreateInfoKHR* pCreateInfo,
        [NativeTypeName("const VkAllocationCallbacks *")]       VkAllocationCallbacks*      pAllocator,
        [NativeTypeName("VkSurfaceKHR *")]                      VkSurfaceKHR_T**            pSurface);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateWaylandSurfaceKHR(
        [NativeTypeName("VkInstance")]                            VkInstance_T*                  instance,
        [NativeTypeName("const VkWaylandSurfaceCreateInfoKHR *")] VkWaylandSurfaceCreateInfoKHR* pCreateInfo,
        [NativeTypeName("const VkAllocationCallbacks *")]         VkAllocationCallbacks*         pAllocator,
        [NativeTypeName("VkSurfaceKHR *")]                        VkSurfaceKHR_T**               pSurface);

    [DllImport("vulkan-1", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern VkResult vkCreateMetalSurfaceEXT(
        [NativeTypeName("VkInstance")]                          VkInstance_T*                instance,
        [NativeTypeName("const VkMetalSurfaceCreateInfoEXT *")] VkMetalSurfaceCreateInfoEXT* pCreateInfo,
        [NativeTypeName("const VkAllocationCallbacks *")]       VkAllocationCallbacks*       pAllocator,
        [NativeTypeName("VkSurfaceKHR *")]                      VkSurfaceKHR_T**             pSurface);
}

/// <summary>
/// Hand-authored mirror of <c>VkWin32SurfaceCreateInfoKHR</c>. Platform
/// handle fields are <c>nint</c> — <c>HINSTANCE</c> and <c>HWND</c> are
/// pointer-sized opaque types, and storing them as <c>nint</c> keeps the
/// struct trivially blittable without dragging in <c>windows.h</c>.
/// </summary>
public unsafe partial struct VkWin32SurfaceCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkWin32SurfaceCreateFlagsKHR")]
    public uint flags;

    /// <summary>Win32 <c>HINSTANCE</c> for the module owning the window class.</summary>
    [NativeTypeName("HINSTANCE")]
    public nint hinstance;

    /// <summary>Win32 <c>HWND</c> for the target window.</summary>
    [NativeTypeName("HWND")]
    public nint hwnd;
}

/// <summary>
/// Hand-authored mirror of <c>VkXlibSurfaceCreateInfoKHR</c>. <c>Display*</c>
/// is a pointer; X11's <c>Window</c> is an <c>XID</c> typedef'd to
/// <c>unsigned long</c> — pointer-sized on every supported 64-bit
/// target, so <c>nint</c> matches the C ABI without pulling in
/// <c>X11.h</c>.
/// </summary>
public unsafe partial struct VkXlibSurfaceCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkXlibSurfaceCreateFlagsKHR")]
    public uint flags;

    /// <summary>Xlib <c>Display *</c> from <c>XOpenDisplay</c>.</summary>
    [NativeTypeName("Display *")]
    public nint dpy;

    /// <summary>Xlib <c>Window</c> (an <c>XID</c>) for the target window.</summary>
    [NativeTypeName("Window")]
    public nint window;
}

/// <summary>
/// Hand-authored mirror of <c>VkWaylandSurfaceCreateInfoKHR</c>. Both
/// platform fields are pointers (<c>wl_display *</c> /
/// <c>wl_surface *</c>) — <c>nint</c> represents them without dragging
/// in <c>wayland-client.h</c>.
/// </summary>
public unsafe partial struct VkWaylandSurfaceCreateInfoKHR
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkWaylandSurfaceCreateFlagsKHR")]
    public uint flags;

    /// <summary>Wayland <c>wl_display *</c> from <c>wl_display_connect</c>.</summary>
    [NativeTypeName("struct wl_display *")]
    public nint display;

    /// <summary>Wayland <c>wl_surface *</c> from the compositor proxy.</summary>
    [NativeTypeName("struct wl_surface *")]
    public nint surface;
}

/// <summary>
/// Hand-authored mirror of <c>VkMetalSurfaceCreateInfoEXT</c>.
/// <c>pLayer</c> points at a Cocoa <c>CAMetalLayer</c> (an Objective-C
/// object id) — <c>nint</c> stores it without pulling in QuartzCore.
/// Wired through MoltenVK on macOS.
/// </summary>
public unsafe partial struct VkMetalSurfaceCreateInfoEXT
{
    public VkStructureType sType;

    [NativeTypeName("const void *")]
    public void* pNext;

    [NativeTypeName("VkMetalSurfaceCreateFlagsEXT")]
    public uint flags;

    /// <summary>Cocoa <c>CAMetalLayer *</c> the surface renders into.</summary>
    [NativeTypeName("const CAMetalLayer *")]
    public nint pLayer;
}
