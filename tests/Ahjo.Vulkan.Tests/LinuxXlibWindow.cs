using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Hidden Xlib window for surface tests. Avoids pulling in SDL3 / GLFW
/// just to get a <c>Display*</c> + <c>Window</c> — we open the default
/// X display, create one <c>InputOutput</c> child of the root window,
/// and never map it. The test only needs the handles for
/// <c>vkCreateXlibSurfaceKHR</c>; no rendering happens here.
/// </summary>
/// <remarks>
/// Construction throws when libX11 isn't loadable or the display can't
/// be opened (no <c>DISPLAY</c> set, no X server running). Callers gate
/// the test on <see cref="IsAvailable"/> so a Linux runner without an X
/// server skips cleanly rather than failing.
/// </remarks>
internal sealed class LinuxXlibWindow : IDisposable
{
    public nint Display { get; private set; }
    public nint Window  { get; private set; }

    public LinuxXlibWindow(uint width, uint height)
    {
        Display = Native.XOpenDisplay(null);
        if (Display == 0)
            throw new InvalidOperationException("XOpenDisplay returned NULL — no X server reachable.");

        int    screen = Native.XDefaultScreen(Display);
        nint   root   = Native.XRootWindow(Display, screen);
        CULong black  = Native.XBlackPixel(Display, screen);

        Window = Native.XCreateSimpleWindow(
            Display, root,
            x: 0, y: 0,
            width: width, height: height,
            border_width: 0,
            border: black,
            background: black);

        if (Window == 0)
        {
            Native.XCloseDisplay(Display);
            Display = 0;
            throw new InvalidOperationException("XCreateSimpleWindow failed.");
        }
    }

    /// <summary>
    /// Whether libX11 is loadable and an X server is reachable.
    /// Probes once per process; cached. Any failure is treated as
    /// "unavailable" — the cached negative answer must not poison the
    /// rest of the suite.
    /// </summary>
    public static bool IsAvailable => _isAvailable.Value;

    private static readonly Lazy<bool> _isAvailable = new(() =>
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return false;
        try
        {
            nint d = Native.XOpenDisplay(null);
            if (d == 0) return false;
            Native.XCloseDisplay(d);
            return true;
        }
        catch
        {
            return false;
        }
    });

    public void Dispose()
    {
        if (Window != 0 && Display != 0)
        {
            Native.XDestroyWindow(Display, Window);
            Window = 0;
        }
        if (Display != 0)
        {
            Native.XCloseDisplay(Display);
            Display = 0;
        }
    }

    private static class Native
    {
        private const string LibX11 = "libX11.so.6";

        [DllImport(LibX11)]
        public static extern nint XOpenDisplay([MarshalAs(UnmanagedType.LPStr)] string? display_name);

        [DllImport(LibX11)]
        public static extern int XCloseDisplay(nint display);

        [DllImport(LibX11)]
        public static extern int XDefaultScreen(nint display);

        [DllImport(LibX11)]
        public static extern nint XRootWindow(nint display, int screen_number);

        [DllImport(LibX11)]
        public static extern CULong XBlackPixel(nint display, int screen_number);

        [DllImport(LibX11)]
        public static extern nint XCreateSimpleWindow(
            nint display, nint parent,
            int x, int y,
            uint width, uint height,
            uint border_width,
            CULong border, CULong background);

        [DllImport(LibX11)]
        public static extern int XDestroyWindow(nint display, nint window);
    }
}
