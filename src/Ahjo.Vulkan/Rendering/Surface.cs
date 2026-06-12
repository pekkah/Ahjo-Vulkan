using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Wraps a <c>VkSurfaceKHR</c>. <c>readonly struct</c> handle — a pair
/// of pointers (the surface + the instance that owns destroy
/// permission). Construction goes through one of the platform-specific
/// factories: <see cref="CreateWin32"/> on Windows,
/// <see cref="CreateXlib"/> / <see cref="CreateWayland"/> on Linux,
/// <see cref="CreateMetal"/> on macOS via MoltenVK.
/// </summary>
/// <remarks>
/// <para><c>default(Surface)</c> is a legal null handle; <see cref="IsNull"/>
/// reports <see langword="true"/> and <see cref="Dispose"/> is a no-op.
/// Surfaces are externally synchronized — do not call into the same
/// <see cref="Surface"/> from multiple threads.</para>
/// <para><b>Lifecycle.</b> <see cref="Dispose"/> calls
/// <c>vkDestroySurfaceKHR</c>. The Vulkan spec requires that no
/// swapchain referencing the surface is alive at the time of destroy —
/// dispose the <see cref="Swapchain"/> first.</para>
/// </remarks>
public readonly unsafe struct Surface : IVulkanHandle<Surface>, IDisposable
{
    public readonly VkSurfaceKHR_T* Handle;
    internal readonly VkInstance_T* InstanceHandle;

    internal Surface(VkSurfaceKHR_T* handle, VkInstance_T* instance)
    {
        Handle         = handle;
        InstanceHandle = instance;
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_SURFACE_KHR;

    /// <summary>
    /// Borrowing constructor from a raw <c>VkSurfaceKHR</c> handle. The
    /// resulting <see cref="Surface"/> has <see cref="InstanceHandle"/> =
    /// <see langword="null"/> and a no-op <see cref="Dispose"/> — use when
    /// the original creator still owns the surface and the wrapper is
    /// only inspecting it. For ownership transfer (the SDL/GLFW handoff
    /// case) use <see cref="WrapExternal"/> instead.
    /// </summary>
    public static Surface FromRaw(nint handle) => new((VkSurfaceKHR_T*)handle, null);

    /// <summary>
    /// Owning constructor over an externally-created <c>VkSurfaceKHR</c>.
    /// Used when SDL3 / GLFW / another library creates the surface (and
    /// returns a raw <c>uint64</c> handle) and the engine wants the
    /// wrapper to take responsibility for destroy. <see cref="Dispose"/>
    /// calls <c>vkDestroySurfaceKHR</c>.
    /// </summary>
    /// <param name="instance">
    /// The <see cref="Instance"/> the surface was created against.
    /// Required for <c>vkDestroySurfaceKHR</c>.
    /// </param>
    /// <param name="handle">Raw <c>VkSurfaceKHR</c> handle (a pointer-sized integer).</param>
    /// <remarks>
    /// The caller transfers ownership: do not call
    /// <c>vkDestroySurfaceKHR</c> manually after wrapping. Disposing two
    /// <see cref="Surface"/> values that wrap the same raw handle is
    /// undefined behaviour, just as it would be for any other Vulkan
    /// destroy entry-point.
    /// </remarks>
    public static Surface WrapExternal(Instance instance, nint handle)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (handle == 0)
            throw new ArgumentException("Surface handle is null.", nameof(handle));
        return new Surface((VkSurfaceKHR_T*)handle, instance.Handle);
    }

    public ulong RawHandle => (ulong)Handle;
    public bool  IsNull    => Handle == null;

    /// <summary>
    /// Wraps a Win32 <c>HWND</c> as a Vulkan surface. Caller must have
    /// enabled <see cref="VulkanExtensions.KhrSurface"/> and
    /// <see cref="VulkanExtensions.KhrWin32Surface"/> on the
    /// <paramref name="instance"/>.
    /// </summary>
    /// <param name="hinstance">
    /// Module handle (<c>HINSTANCE</c>) for the window class. Pass the
    /// process module from <c>GetModuleHandle(null)</c>.
    /// </param>
    /// <param name="hwnd">Window handle to draw into.</param>
    public static Surface CreateWin32(Instance instance, nint hinstance, nint hwnd)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (hwnd      == 0) throw new ArgumentException("HWND is null.", nameof(hwnd));
        if (hinstance == 0) throw new ArgumentException("HINSTANCE is null.", nameof(hinstance));

        var ci = new VkWin32SurfaceCreateInfoKHR
        {
            sType     = VkStructureType.VK_STRUCTURE_TYPE_WIN32_SURFACE_CREATE_INFO_KHR,
            hinstance = hinstance,
            hwnd      = hwnd,
        };
        VkSurfaceKHR_T* raw = null;
        Vk.vkCreateWin32SurfaceKHR(instance.Handle, &ci, null, &raw).ThrowIfFailed();
        return new Surface(raw, instance.Handle);
    }

    /// <summary>
    /// Wraps an Xlib <c>Display*</c> + <c>Window</c> as a Vulkan surface.
    /// Caller must have enabled <see cref="VulkanExtensions.KhrSurface"/>
    /// and <see cref="VulkanExtensions.KhrXlibSurface"/> on the
    /// <paramref name="instance"/>.
    /// </summary>
    /// <param name="display"><c>Display *</c> from <c>XOpenDisplay</c>; caller retains ownership.</param>
    /// <param name="window">X11 <c>Window</c> (an <c>XID</c>); <c>None</c> (zero) is rejected.</param>
    public static Surface CreateXlib(Instance instance, nint display, nint window)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (display == 0) throw new ArgumentException("Xlib Display* is null.", nameof(display));
        if (window  == 0) throw new ArgumentException("Xlib Window is None.", nameof(window));

        var ci = new VkXlibSurfaceCreateInfoKHR
        {
            sType  = VkStructureType.VK_STRUCTURE_TYPE_XLIB_SURFACE_CREATE_INFO_KHR,
            dpy    = display,
            window = window,
        };
        VkSurfaceKHR_T* raw = null;
        Vk.vkCreateXlibSurfaceKHR(instance.Handle, &ci, null, &raw).ThrowIfFailed();
        return new Surface(raw, instance.Handle);
    }

    /// <summary>
    /// Wraps a Wayland <c>wl_display*</c> + <c>wl_surface*</c> as a
    /// Vulkan surface. Caller must have enabled
    /// <see cref="VulkanExtensions.KhrSurface"/> and
    /// <see cref="VulkanExtensions.KhrWaylandSurface"/> on the
    /// <paramref name="instance"/>.
    /// </summary>
    /// <param name="display"><c>wl_display *</c> from <c>wl_display_connect</c>; caller retains ownership.</param>
    /// <param name="surface"><c>wl_surface *</c> from <c>wl_compositor_create_surface</c>.</param>
    public static Surface CreateWayland(Instance instance, nint display, nint surface)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (display == 0) throw new ArgumentException("wl_display* is null.", nameof(display));
        if (surface == 0) throw new ArgumentException("wl_surface* is null.", nameof(surface));

        var ci = new VkWaylandSurfaceCreateInfoKHR
        {
            sType   = VkStructureType.VK_STRUCTURE_TYPE_WAYLAND_SURFACE_CREATE_INFO_KHR,
            display = display,
            surface = surface,
        };
        VkSurfaceKHR_T* raw = null;
        Vk.vkCreateWaylandSurfaceKHR(instance.Handle, &ci, null, &raw).ThrowIfFailed();
        return new Surface(raw, instance.Handle);
    }

    /// <summary>
    /// Wraps a Cocoa <c>CAMetalLayer</c> as a Vulkan surface (MoltenVK
    /// on macOS / iOS). Caller must have enabled
    /// <see cref="VulkanExtensions.KhrSurface"/> and
    /// <see cref="VulkanExtensions.ExtMetalSurface"/> on the
    /// <paramref name="instance"/>.
    /// </summary>
    /// <param name="metalLayer"><c>CAMetalLayer*</c> (Objective-C object id); MoltenVK retains a reference for the surface lifetime.</param>
    public static Surface CreateMetal(Instance instance, nint metalLayer)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (metalLayer == 0)
            throw new ArgumentException("CAMetalLayer is null.", nameof(metalLayer));

        var ci = new VkMetalSurfaceCreateInfoEXT
        {
            sType  = VkStructureType.VK_STRUCTURE_TYPE_METAL_SURFACE_CREATE_INFO_EXT,
            pLayer = metalLayer,
        };
        VkSurfaceKHR_T* raw = null;
        Vk.vkCreateMetalSurfaceEXT(instance.Handle, &ci, null, &raw).ThrowIfFailed();
        return new Surface(raw, instance.Handle);
    }

    /// <inheritdoc/>
    public bool OwnsHandle => InstanceHandle != null;

    public void Dispose()
    {
        if (Handle == null || !OwnsHandle) return;
        Vk.vkDestroySurfaceKHR(InstanceHandle, Handle, null);
    }
}
