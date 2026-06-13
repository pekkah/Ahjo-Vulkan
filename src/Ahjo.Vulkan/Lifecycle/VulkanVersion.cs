namespace Ahjo.Vulkan;

/// <summary>
/// Strongly-typed wrapper around the packed <c>uint</c> the Vulkan headers
/// produce via the <c>VK_MAKE_API_VERSION</c> macro. The binding generator
/// does not materialize the macro, so this is the canonical replacement.
/// <see cref="Packed"/> is the value <c>VkApplicationInfo.apiVersion</c> wants.
/// </summary>
public readonly record struct VulkanVersion(uint Packed)
{
    public static VulkanVersion V1_0 { get; } = Make(1, 0, 0);
    public static VulkanVersion V1_1 { get; } = Make(1, 1, 0);
    public static VulkanVersion V1_2 { get; } = Make(1, 2, 0);
    public static VulkanVersion V1_3 { get; } = Make(1, 3, 0);
    public static VulkanVersion V1_4 { get; } = Make(1, 4, 0);

    /// <summary>
    /// Packs <paramref name="major"/> / <paramref name="minor"/> /
    /// <paramref name="patch"/> into the layout
    /// <c>VK_MAKE_API_VERSION(0, major, minor, patch)</c> uses. The
    /// <c>variant</c> field (top 3 bits) is fixed at 0 — Khronos Vulkan. A
    /// non-Khronos variant would need a different overload, which we don't
    /// have a caller for yet.
    /// </summary>
    public static VulkanVersion Make(uint major, uint minor, uint patch)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(major, 0x7Fu, nameof(major));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minor, 0x3FFu, nameof(minor));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(patch, 0xFFFu, nameof(patch));
        return new((major << 22) | (minor << 12) | patch);
    }

    public uint Variant => (Packed >> 29) & 0x7u;
    public uint Major   => (Packed >> 22) & 0x7Fu;
    public uint Minor   => (Packed >> 12) & 0x3FFu;
    public uint Patch   =>  Packed        & 0xFFFu;

    public static implicit operator uint(VulkanVersion v) => v.Packed;
}
