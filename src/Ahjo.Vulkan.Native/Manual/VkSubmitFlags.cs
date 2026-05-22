namespace Ahjo.Vulkan.Native;

/// <summary>
/// Typed access to <c>VkSubmitFlags</c> bit values. Unlike the sync2
/// stage/access masks this one is 32-bit — <c>VkSubmitFlagBits</c> is a
/// proper C enum that ClangSharp emits as a real C# enum, so the typed
/// names already exist. This static class re-exposes them under the
/// typedef name to match Vortice's shape:
/// <code>
/// submitInfo.flags = VkSubmitFlags.Protected,
/// </code>
///
/// Members are <c>const uint</c> (not the enum type) so they assign to the
/// <c>uint</c> field <c>VkSubmitInfo2.flags</c> without a cast.
/// </summary>
public static class VkSubmitFlags
{
    public const uint Protected = (uint)VkSubmitFlagBits.VK_SUBMIT_PROTECTED_BIT;
}
