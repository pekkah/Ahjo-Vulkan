namespace Ahjo.Vulkan.Native;

/// <summary>
/// Hand-authored Vulkan sentinel constants. ClangSharp's
/// <c>generate-unmanaged-constants</c> mode picks up <c>#define</c>s that
/// expand to clean integer literals but skips macros with operator/cast
/// expansions like <c>#define VK_WHOLE_SIZE (~0ULL)</c>, which is why these
/// four don't show up in <c>Generated/Vk.cs</c>. The values are spec-stable
/// since Vulkan 1.0.
/// </summary>
public static unsafe partial class Vk
{
    /// <summary>
    /// Sentinel for queue-family index fields on barriers and resource
    /// create-infos that don't transfer ownership across queue families.
    /// Spec-stable since Vulkan 1.0.
    /// </summary>
    public const uint VK_QUEUE_FAMILY_IGNORED = ~0U;

    /// <summary>
    /// Sentinel for size/range fields meaning "from the offset to the end
    /// of the buffer or memory allocation" — used by
    /// <c>VkDescriptorBufferInfo.range</c>, <c>vkCmdCopyBuffer</c> region
    /// sizes, and <c>vkFlushMappedMemoryRanges</c> / <c>vkInvalidate…</c>.
    /// Spec-stable since Vulkan 1.0.
    /// </summary>
    public const ulong VK_WHOLE_SIZE = ~0UL;

    /// <summary>
    /// Sentinel for <c>VkImageSubresourceRange.levelCount</c> meaning "all
    /// mip levels from <c>baseMipLevel</c> to the last level of the image".
    /// Spec-stable since Vulkan 1.0.
    /// </summary>
    public const uint VK_REMAINING_MIP_LEVELS = ~0U;

    /// <summary>
    /// Sentinel for <c>VkImageSubresourceRange.layerCount</c> meaning "all
    /// array layers from <c>baseArrayLayer</c> to the last layer of the
    /// image". Spec-stable since Vulkan 1.0.
    /// </summary>
    public const uint VK_REMAINING_ARRAY_LAYERS = ~0U;
}
