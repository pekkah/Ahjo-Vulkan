using System.Runtime.InteropServices;

namespace Ahjo.Vulkan;

/// <summary>
/// Helpers for the UTF-8 boundary between Vulkan (which speaks <c>const char*</c>)
/// and managed code. Used by the debug-utils callback trampoline; not on a hot path.
/// </summary>
internal static unsafe class Utf8
{
    public static string? ToString(sbyte* utf8) =>
        utf8 == null ? null : Marshal.PtrToStringUTF8((nint)utf8);
}
