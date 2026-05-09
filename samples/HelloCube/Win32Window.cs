using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ahjo.Vulkan.Samples.HelloCube;

/// <summary>
/// Minimal visible Win32 window for the cube sample. Direct user32 /
/// kernel32 P/Invokes — no SDL3 / GLFW dependency. Mirrors the
/// HelloTriangle sample's window; surface creation needs only the
/// <c>HWND</c> + <c>HINSTANCE</c> nints.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class Win32Window : IDisposable
{
    public nint HInstance { get; }
    public nint Hwnd      { get; private set; }
    public uint Width     { get; private set; }
    public uint Height    { get; private set; }
    public bool ShouldClose { get; private set; }
    public bool Resized     { get; private set; }
    public bool WireframeRequested { get; private set; }

    private readonly string _className;
    private readonly Native.WndProc _wndProc;

    public Win32Window(string title, uint width, uint height)
    {
        Width      = width;
        Height     = height;
        _className = $"AhjoVulkanHelloCube_{Guid.NewGuid():N}";
        _wndProc   = WndProc;
        HInstance  = Native.GetModuleHandleW(null);

        var wc = new Native.WNDCLASSW
        {
            style         = Native.CS_HREDRAW | Native.CS_VREDRAW,
            lpfnWndProc   = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance     = HInstance,
            hCursor       = Native.LoadCursorW(0, Native.IDC_ARROW),
            hbrBackground = 0,
            lpszClassName = _className,
        };
        if (Native.RegisterClassW(ref wc) == 0)
            throw new InvalidOperationException(
                $"RegisterClassW failed: 0x{Marshal.GetLastWin32Error():X}");

        var r = new Native.RECT { left = 0, top = 0, right = (int)width, bottom = (int)height };
        Native.AdjustWindowRect(ref r, Native.WS_OVERLAPPEDWINDOW, false);

        Hwnd = Native.CreateWindowExW(
            dwExStyle:   0,
            lpClassName: _className,
            lpWindowName: title,
            dwStyle:     Native.WS_OVERLAPPEDWINDOW,
            x: Native.CW_USEDEFAULT, y: Native.CW_USEDEFAULT,
            nWidth:  r.right  - r.left,
            nHeight: r.bottom - r.top,
            hWndParent: 0, hMenu: 0, hInstance: HInstance, lpParam: 0);

        if (Hwnd == 0)
            throw new InvalidOperationException(
                $"CreateWindowExW failed: 0x{Marshal.GetLastWin32Error():X}");

        Native.ShowWindow(Hwnd, Native.SW_SHOW);
    }

    public void PumpEvents()
    {
        while (Native.PeekMessageW(out var msg, 0, 0, 0, Native.PM_REMOVE))
        {
            if (msg.message == Native.WM_QUIT) { ShouldClose = true; continue; }
            Native.TranslateMessage(ref msg);
            Native.DispatchMessageW(ref msg);
        }
    }

    public bool ConsumeResize()
    {
        bool r = Resized;
        Resized = false;
        return r;
    }

    public bool ConsumeWireframeToggle()
    {
        bool r = WireframeRequested;
        WireframeRequested = false;
        return r;
    }

    private nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case Native.WM_CLOSE:
                ShouldClose = true;
                return 0;
            case Native.WM_DESTROY:
                Native.PostQuitMessage(0);
                return 0;
            case Native.WM_KEYDOWN:
                if (wParam == Native.VK_ESCAPE) { ShouldClose = true; return 0; }
                if (wParam == Native.VK_W)      { WireframeRequested = true; return 0; }
                break;
            case Native.WM_SIZE:
                uint w = (uint)(lParam.ToInt64() & 0xFFFF);
                uint h = (uint)((lParam.ToInt64() >> 16) & 0xFFFF);
                if (w != 0 && h != 0 && (w != Width || h != Height))
                {
                    Width = w; Height = h; Resized = true;
                }
                return 0;
        }
        return Native.DefWindowProcW(hwnd, msg, wParam, lParam);
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

    private static class Native
    {
        public const uint CS_HREDRAW            = 0x0002;
        public const uint CS_VREDRAW            = 0x0001;
        public const uint WS_OVERLAPPEDWINDOW   = 0x00CF0000;
        public const int  CW_USEDEFAULT         = unchecked((int)0x80000000);
        public const int  SW_SHOW               = 5;
        public const uint WM_CLOSE              = 0x0010;
        public const uint WM_DESTROY            = 0x0002;
        public const uint WM_QUIT               = 0x0012;
        public const uint WM_KEYDOWN            = 0x0100;
        public const uint WM_SIZE               = 0x0005;
        public const uint PM_REMOVE             = 0x0001;
        public const nint VK_ESCAPE             = 0x1B;
        public const nint VK_W                  = 0x57;
        public const nint IDC_ARROW             = 32512;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int x, y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public nint hwnd;
            public uint message;
            public nint wParam;
            public nint lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WNDCLASSW
        {
            public uint style;
            public nint lpfnWndProc;
            public int  cbClsExtra;
            public int  cbWndExtra;
            public nint hInstance;
            public nint hIcon;
            public nint hCursor;
            public nint hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)] public string  lpszClassName;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern nint GetModuleHandleW(string? lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern ushort RegisterClassW(ref WNDCLASSW lpWndClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool UnregisterClassW(string lpClassName, nint hInstance);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern nint CreateWindowExW(
            uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
            int x, int y, int nWidth, int nHeight,
            nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyWindow(nint hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(nint hWnd, int nCmdShow);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern nint DefWindowProcW(nint hWnd, uint Msg, nint wParam, nint lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool PeekMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern nint DispatchMessageW(ref MSG lpMsg);

        [DllImport("user32.dll")]
        public static extern void PostQuitMessage(int nExitCode);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool AdjustWindowRect(ref RECT lpRect, uint dwStyle, bool bMenu);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern nint LoadCursorW(nint hInstance, nint lpCursorName);
    }
}
