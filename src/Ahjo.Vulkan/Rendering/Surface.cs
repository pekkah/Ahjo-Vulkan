using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Wraps a <c>VkSurfaceKHR</c>. <c>readonly struct</c> handle — a pair
/// of pointers (the surface + the instance that owns destroy
/// permission). Construction goes through one of the platform-specific
/// factories (<see cref="CreateWin32"/> on Windows; Linux variants land
/// alongside the matching native bindings).
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
    public static Surface FromRaw(nint handle) => new((VkSurfaceKHR_T*)handle, null);
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

    public void Dispose()
    {
        if (Handle == null || InstanceHandle == null) return;
        Vk.vkDestroySurfaceKHR(InstanceHandle, Handle, null);
    }
}
