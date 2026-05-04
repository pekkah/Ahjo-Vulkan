namespace Ahjo.Vulkan;

/// <summary>
/// Public UTF-8 byte literals for the Khronos device-extension names this
/// wrapper hard-codes. Centralised so a typo can only be made in one place;
/// callers compose pickers with these constants. Populated lazily — entries
/// are added as the wrapper grows extension-aware features.
/// </summary>
/// <remarks>
/// The C# spec guarantees <c>"…"u8</c> literals live in the assembly's
/// read-only data segment for the lifetime of the process and are
/// followed by an out-of-bounds NUL byte, so the address of the span's
/// first element is safe to pass to a Vulkan API expecting
/// <c>const char*</c>.
/// </remarks>
public static class KhronosExtensionNames
{
    /// <summary><c>VK_KHR_swapchain</c> — required for any device that will present.</summary>
    public static ReadOnlySpan<byte> KhrSwapchain => "VK_KHR_swapchain"u8;
}
