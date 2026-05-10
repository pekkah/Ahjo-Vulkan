using SDL;
using static SDL.SDL3;

// Both Ahjo.Vulkan.Native and SDL declare opaque Vulkan handle stubs
// (VkInstance_T, VkSurfaceKHR_T) — alias the SDL ones so the bridging
// casts below are unambiguous without dropping the rest of the SDL
// namespace.
using SdlVkInstance   = SDL.VkInstance_T;
using SdlVkSurfaceKHR = SDL.VkSurfaceKHR_T;

// Lives in the parent namespace so the test suite (Ahjo.Vulkan.Tests)
// and the windowed samples (Ahjo.Vulkan.Samples.*) both pick it up via
// nesting without an explicit using.
namespace Ahjo.Vulkan;

/// <summary>
/// Cross-platform SDL3-backed window shim. Replaces the per-project
/// Win32 helper with a single file shared by the test suite (Wayland +
/// Metal E2E surface coverage) and the windowed samples
/// (HelloTriangle, HelloCube). Created hidden + Vulkan-capable by
/// default; samples opt into a visible resizable window via the
/// constructor flags.
/// </summary>
/// <remarks>
/// <para><b>Lifecycle.</b> SDL3's video subsystem is reference-counted
/// here so multiple <see cref="SdlWindow"/>s in the same process share
/// one <c>SDL_Init(SDL_INIT_VIDEO)</c>; the last <see cref="Dispose"/>
/// calls <c>SDL_Quit</c>. Windows take exclusive ownership of their
/// <c>SDL_Window*</c> and (on macOS Metal) the matching
/// <c>SDL_MetalView</c>.</para>
/// <para><b>Native handles.</b> For Vulkan the canonical path is
/// <see cref="CreateVulkanSurface"/>, which calls
/// <c>SDL_Vulkan_CreateSurface</c> and hands the raw <c>VkSurfaceKHR</c>
/// to <see cref="Surface.WrapExternal"/>. The platform-specific
/// accessors (<see cref="WaylandDisplay"/>, <see cref="WaylandSurface"/>,
/// <see cref="MetalLayer"/>) exist so <c>SurfaceTests</c> can drive
/// <c>Surface.CreateWayland</c> / <c>Surface.CreateMetal</c> directly
/// against real handles instead of stubs.</para>
/// </remarks>
internal sealed unsafe class SdlWindow : IDisposable
{
    public uint Width      { get; private set; }
    public uint Height     { get; private set; }
    public bool ShouldClose { get; private set; }

    private SDL_Window* _window;
    private nint        _metalView; // SDL_MetalView, populated lazily on macOS.
    private bool        _resized;
    private bool        _wireframeRequested;

    public nint Handle => (nint)_window;

    public SdlWindow(string title, uint width, uint height, bool hidden = true, bool resizable = false)
    {
        EnsureVideoSubsystem();
        try
        {
            Width  = width;
            Height = height;

            SDL_WindowFlags flags = SDL_WindowFlags.SDL_WINDOW_VULKAN;
            if (hidden)    flags |= SDL_WindowFlags.SDL_WINDOW_HIDDEN;
            if (resizable) flags |= SDL_WindowFlags.SDL_WINDOW_RESIZABLE;

            _window = SDL_CreateWindow(title, (int)width, (int)height, flags);
            if (_window == null)
                throw new InvalidOperationException($"SDL_CreateWindow failed: {SDL_GetError()}");
        }
        catch
        {
            ReleaseVideoSubsystem();
            throw;
        }
    }

    /// <summary>
    /// Creates a <c>VkSurfaceKHR</c> for this window via SDL's
    /// platform-abstracted helper and wraps it for owned destruction
    /// through <see cref="Surface.WrapExternal"/>. Surface destruction
    /// goes through <c>vkDestroySurfaceKHR</c> (the wrapper's
    /// <see cref="Surface.Dispose"/>); SDL is not asked to destroy it.
    /// </summary>
    public Surface CreateVulkanSurface(Instance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        SdlVkSurfaceKHR* raw = null;
        if (!SDL_Vulkan_CreateSurface(_window, (SdlVkInstance*)(nint)instance.RawHandle, allocator: null, &raw))
            throw new InvalidOperationException($"SDL_Vulkan_CreateSurface failed: {SDL_GetError()}");

        return Surface.WrapExternal(instance, (nint)raw);
    }

    /// <summary>
    /// Wayland <c>wl_display *</c> for this window. Returns 0 when the
    /// window is not running under Wayland (e.g. SDL chose X11).
    /// </summary>
    public nint WaylandDisplay => GetPointerProperty("SDL.window.wayland.display"u8);

    /// <summary>
    /// Wayland <c>wl_surface *</c> for this window. Returns 0 when the
    /// window is not running under Wayland.
    /// </summary>
    public nint WaylandSurface => GetPointerProperty("SDL.window.wayland.surface"u8);

    /// <summary>
    /// Returns the <c>CAMetalLayer *</c> for this window, lazily
    /// creating an SDL Metal view on first access. macOS / iOS only.
    /// The view (and the layer it owns) is destroyed in
    /// <see cref="Dispose"/>.
    /// </summary>
    public nint MetalLayer
    {
        get
        {
            if (_metalView == 0)
            {
                _metalView = SDL_Metal_CreateView(_window);
                if (_metalView == 0)
                    throw new InvalidOperationException($"SDL_Metal_CreateView failed: {SDL_GetError()}");
            }
            return SDL_Metal_GetLayer(_metalView);
        }
    }

    public void PumpEvents()
    {
        SDL_Event evt;
        while (SDL_PollEvent(&evt))
        {
            switch (evt.Type)
            {
                case SDL_EventType.SDL_EVENT_QUIT:
                case SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED:
                    ShouldClose = true;
                    break;
                case SDL_EventType.SDL_EVENT_KEY_DOWN:
                    if (evt.key.key == SDL_Keycode.SDLK_ESCAPE) ShouldClose         = true;
                    else if (evt.key.key == SDL_Keycode.SDLK_W) _wireframeRequested = true;
                    break;
                case SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED:
                    int w = 0, h = 0;
                    SDL_GetWindowSizeInPixels(_window, &w, &h);
                    if (w > 0 && h > 0 && ((uint)w != Width || (uint)h != Height))
                    {
                        Width    = (uint)w;
                        Height   = (uint)h;
                        _resized = true;
                    }
                    break;
            }
        }
    }

    public bool ConsumeResize()
    {
        bool r = _resized;
        _resized = false;
        return r;
    }

    public bool ConsumeWireframeToggle()
    {
        bool r = _wireframeRequested;
        _wireframeRequested = false;
        return r;
    }

    public void Dispose()
    {
        if (_metalView != 0)
        {
            SDL_Metal_DestroyView(_metalView);
            _metalView = 0;
        }
        if (_window != null)
        {
            SDL_DestroyWindow(_window);
            _window = null;
            ReleaseVideoSubsystem();
        }
    }

    /// <summary>
    /// Vulkan instance extensions that <c>SDL_Vulkan_CreateSurface</c>
    /// requires for the video driver SDL ends up choosing at runtime —
    /// typically <c>VK_KHR_surface</c> plus the platform-specific
    /// surface extension (<c>VK_KHR_win32_surface</c>, <c>VK_KHR_wayland_surface</c>,
    /// <c>VK_KHR_xlib_surface</c>, <c>VK_EXT_metal_surface</c>). Hand the
    /// returned array straight to <c>InstanceDescription.Extensions</c>.
    /// The extension name pointers belong to the loaded SDL3 native
    /// library and remain valid for the lifetime of the process.
    /// </summary>
    public static Utf8Name[] GetRequiredVulkanInstanceExtensions()
    {
        EnsureVideoSubsystem();
        try
        {
            uint count = 0;
            byte** ptrs = SDL_Vulkan_GetInstanceExtensions(&count);
            if (ptrs == null)
                throw new InvalidOperationException(
                    $"SDL_Vulkan_GetInstanceExtensions failed: {SDL_GetError()}");

            var result = new Utf8Name[count];
            for (uint i = 0; i < count; i++)
                result[i] = new Utf8Name((sbyte*)ptrs[i]);
            return result;
        }
        finally
        {
            ReleaseVideoSubsystem();
        }
    }

    /// <summary>
    /// Whether SDL3 can initialise its video subsystem on this host.
    /// Probed once per process — failures (no SDL3 native, no
    /// $WAYLAND_DISPLAY/$DISPLAY, etc.) cache as "unavailable" so a
    /// headless CI runner skips the Wayland / Metal round-trip tests
    /// instead of hard-failing.
    /// </summary>
    public static bool IsAvailable => s_isAvailable.Value;

    // Refcount mutation and the matching SDL_InitSubSystem /
    // SDL_QuitSubSystem call must happen as a single critical
    // section: a 1->0->SDL_QuitSubSystem racing with another thread's
    // 0->1->SDL_InitSubSystem would tear the second thread's window
    // out from under it. The lock is declared above the Lazy<bool>
    // probe below so static-init order leaves it non-null when the
    // probe's factory runs.
    private static readonly object s_videoLock = new();
    private static int s_videoRefCount;

    // The probe must not tear down the subsystem out from under a
    // window that is already alive on another thread. Take the same
    // lock <see cref="EnsureVideoSubsystem"/> and <see cref="ReleaseVideoSubsystem"/>
    // use, and short-circuit when the refcount is non-zero (an
    // existing window already proves availability). When we *are* the
    // first to touch SDL, use the refcount-aware Init/Quit-subsystem
    // pair, never the bare SDL_Quit which forcibly tears down all
    // subsystems regardless of the refcount.
    private static readonly Lazy<bool> s_isAvailable = new(() =>
    {
        lock (s_videoLock)
        {
            if (s_videoRefCount > 0) return true;
            try
            {
                if (!SDL_InitSubSystem(SDL_InitFlags.SDL_INIT_VIDEO))
                    return false;
                SDL_QuitSubSystem(SDL_InitFlags.SDL_INIT_VIDEO);
                return true;
            }
            catch
            {
                return false;
            }
        }
    });

    private nint GetPointerProperty(ReadOnlySpan<byte> name)
    {
        SDL_PropertiesID props = SDL_GetWindowProperties(_window);
        if ((uint)props == 0) return 0;
        fixed (byte* namePtr = name)
        {
            return SDL_GetPointerProperty(props, namePtr, default_value: 0);
        }
    }

    private static void EnsureVideoSubsystem()
    {
        lock (s_videoLock)
        {
            if (!SDL_InitSubSystem(SDL_InitFlags.SDL_INIT_VIDEO))
                throw new InvalidOperationException($"SDL_InitSubSystem(SDL_INIT_VIDEO) failed: {SDL_GetError()}");
            s_videoRefCount++;
        }
    }

    private static void ReleaseVideoSubsystem()
    {
        lock (s_videoLock)
        {
            s_videoRefCount--;
            SDL_QuitSubSystem(SDL_InitFlags.SDL_INIT_VIDEO);
        }
    }
}
