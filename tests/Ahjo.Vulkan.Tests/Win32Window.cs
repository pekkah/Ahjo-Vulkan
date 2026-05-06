using System.Runtime.InteropServices;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Hidden Win32 window for surface tests. Avoids pulling in SDL3 / GLFW
/// just to get an HWND — we register a process-private window class and
/// create one window with <c>WS_OVERLAPPED</c> at <c>800×600</c> off the
/// visible area. Drained-message-pump is enough for swapchain tests
/// since none of them actually present pixels.
/// </summary>
internal sealed class Win32Window : IDisposable
{
    public nint HInstance { get; }
    public nint Hwnd      { get; private set; }
    public uint Width     { get; private set; }
    public uint Height    { get; private set; }

    private readonly string _className;
    private static readonly Native.WndProc _staticWndProc = StaticWndProc;

    public Win32Window(uint width, uint height, string className)
    {
        Width      = width;
        Height     = height;
        _className = className;
        HInstance  = Native.GetModuleHandleW(null);

        var wc = new Native.WNDCLASS
        {
            lpfnWndProc   = Marshal.GetFunctionPointerForDelegate(_staticWndProc),
            hInstance     = HInstance,
            lpszClassName = className,
        };
        if (Native.RegisterClassW(ref wc) == 0)
        {
            int err = Marshal.GetLastWin32Error();
            // ERROR_CLASS_ALREADY_EXISTS (1410) is fine — we own the
            // process and registered the same class on a previous run.
            if (err != 1410)
                throw new InvalidOperationException($"RegisterClassW failed: 0x{err:X}");
        }

        Hwnd = Native.CreateWindowExW(
            dwExStyle:  0,
            lpClassName: className,
            lpWindowName: "Ahjo.Vulkan Tests",
            dwStyle:    Native.WS_OVERLAPPED,
            x: -2000, y: -2000,
            nWidth: (int)width, nHeight: (int)height,
            hWndParent: 0, hMenu: 0, hInstance: HInstance, lpParam: 0);

        if (Hwnd == 0)
            throw new InvalidOperationException(
                $"CreateWindowExW failed: 0x{Marshal.GetLastWin32Error():X}");
    }

    public void Resize(uint width, uint height)
    {
        Width  = width;
        Height = height;
        Native.SetWindowPos(Hwnd, 0, -2000, -2000, (int)width, (int)height,
            Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
    }

    public void Dispose()
    {
        if (Hwnd != 0)
        {
            Native.DestroyWindow(Hwnd);
            Hwnd = 0;
        }
        Native.UnregisterClassW(_className, HInstance);
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);

    private static nint StaticWndProc(nint hwnd, uint msg, nint wParam, nint lParam)
        => Native.DefWindowProcW(hwnd, msg, wParam, lParam);

    private static class Native
    {
        public const uint WS_OVERLAPPED = 0x00000000;
        public const uint SWP_NOZORDER  = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WNDCLASS
        {
            public uint   style;
            public nint   lpfnWndProc;
            public int    cbClsExtra;
            public int    cbWndExtra;
            public nint   hInstance;
            public nint   hIcon;
            public nint   hCursor;
            public nint   hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)] public string  lpszClassName;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern nint GetModuleHandleW(string? lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern ushort RegisterClassW(ref WNDCLASS lpWndClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool UnregisterClassW(string lpClassName, nint hInstance);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern nint CreateWindowExW(
            uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
            int x, int y, int nWidth, int nHeight,
            nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyWindow(nint hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern nint DefWindowProcW(nint hWnd, uint Msg, nint wParam, nint lParam);
    }
}
