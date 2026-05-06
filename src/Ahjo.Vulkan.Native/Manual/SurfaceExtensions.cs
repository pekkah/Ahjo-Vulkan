using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Native;

/// <summary>
/// Hand-authored bindings for the platform-specific surface extensions.
/// The clang-sharp generator skips these because they pull in
/// <c>windows.h</c> / <c>X11.h</c> / <c>wayland-client.h</c> for the
/// platform handle types — far heavier than what they're worth here.
/// We declare the platform handles as <c>nint</c> (or raw pointers)
/// directly, side-stepping the include chain.
/// </summary>
/// <remarks>
/// Currently Win32 only — Linux variants land alongside the wrapper-side
/// platform factories when there's a non-Windows test host to validate
/// them on.
/// </remarks>
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
