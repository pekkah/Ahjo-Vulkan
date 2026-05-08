using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Type-safe factories for <see cref="VkClearColorValue"/>. The native
/// struct is a union over <c>float[4]</c> / <c>int[4]</c> /
/// <c>uint[4]</c>, all overlapping at offset 0 — assigning through the
/// wrong member bit-reinterprets the input rather than converting it,
/// which the spec explicitly calls out as the caller's responsibility
/// to match against the image's numeric type. Picking the helper named
/// after the format's numeric class keeps the union member and the
/// image format in sync.
/// </summary>
/// <remarks>
/// <para><b>Tripwire.</b> Clearing a UINT-format image (e.g. the engine's
/// <c>R16G16B16A16_UINT</c> material G-buffer RT) with the float
/// constructor uploads garbage — the float bits sit in the union, the
/// driver reads the corresponding uint slot, and the resulting clear
/// value bears no relation to the requested integer. Using
/// <see cref="UInt"/> for UINT formats and <see cref="Int"/> for SINT
/// formats avoids the trap.</para>
/// </remarks>
public static class ClearColor
{
    /// <summary>
    /// Builds a <see cref="VkClearColorValue"/> populated through its
    /// <c>float32</c> union member. Use for UNORM, SNORM, FLOAT, and
    /// SRGB formats.
    /// </summary>
    public static VkClearColorValue Float(float r, float g, float b, float a)
    {
        var v = default(VkClearColorValue);
        v.float32[0] = r;
        v.float32[1] = g;
        v.float32[2] = b;
        v.float32[3] = a;
        return v;
    }

    /// <summary>
    /// Builds a <see cref="VkClearColorValue"/> populated through its
    /// <c>uint32</c> union member. Use for UINT formats (e.g.
    /// <c>R16G16B16A16_UINT</c> G-buffers).
    /// </summary>
    public static VkClearColorValue UInt(uint r, uint g, uint b, uint a)
    {
        var v = default(VkClearColorValue);
        v.uint32[0] = r;
        v.uint32[1] = g;
        v.uint32[2] = b;
        v.uint32[3] = a;
        return v;
    }

    /// <summary>
    /// Builds a <see cref="VkClearColorValue"/> populated through its
    /// <c>int32</c> union member. Use for SINT formats.
    /// </summary>
    public static VkClearColorValue Int(int r, int g, int b, int a)
    {
        var v = default(VkClearColorValue);
        v.int32[0] = r;
        v.int32[1] = g;
        v.int32[2] = b;
        v.int32[3] = a;
        return v;
    }
}
