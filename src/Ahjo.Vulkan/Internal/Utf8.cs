using System.Runtime.InteropServices;
using System.Text;

namespace Ahjo.Vulkan;

/// <summary>
/// Helpers for the UTF-8 boundary between Vulkan (which speaks <c>const char*</c>)
/// and managed code. Used by the debug-utils trampoline and the device-fault
/// decode; not on a hot path.
/// </summary>
internal static unsafe class Utf8
{
    public static string? ToString(sbyte* utf8) =>
        utf8 == null ? null : Marshal.PtrToStringUTF8((nint)utf8);

    /// <summary>
    /// Decodes a fixed-size <c>char[N]</c> field a driver is not forced to
    /// NUL-terminate: up to the first NUL, or the whole span when there is
    /// none. Invalid sequences become U+FFFD; nothing throws. An empty result
    /// is <see cref="string.Empty"/>.
    /// </summary>
    public static string FromBounded(ReadOnlySpan<sbyte> buffer)
    {
        int nul = buffer.IndexOf((sbyte)0);
        ReadOnlySpan<sbyte> text = nul < 0 ? buffer : buffer[..nul];
        return text.IsEmpty ? string.Empty : Encoding.UTF8.GetString(MemoryMarshal.AsBytes(text));
    }
}
