namespace Ahjo.Vulkan.Native;

/// <summary>
/// Typed access to <c>VkAccessFlags2</c> bit values. Companion to
/// <see cref="VkPipelineStageFlags2"/> — see that type for the rationale.
/// Members alias the corresponding <c>Vk.VK_ACCESS_2_*</c> constants so
/// barrier call sites read like:
/// <code>
/// srcAccessMask = VkAccessFlags2.ShaderWrite | VkAccessFlags2.ShaderRead,
/// </code>
///
/// Scope: core Vulkan 1.3 baseline only. <c>_KHR</c> aliases and vendor
/// extensions remain accessible via <c>Vk.VK_ACCESS_2_*</c>.
/// </summary>
public static class VkAccessFlags2
{
    public const ulong None                          = Vk.VK_ACCESS_2_NONE;
    public const ulong IndirectCommandRead           = Vk.VK_ACCESS_2_INDIRECT_COMMAND_READ_BIT;
    public const ulong IndexRead                     = Vk.VK_ACCESS_2_INDEX_READ_BIT;
    public const ulong VertexAttributeRead           = Vk.VK_ACCESS_2_VERTEX_ATTRIBUTE_READ_BIT;
    public const ulong UniformRead                   = Vk.VK_ACCESS_2_UNIFORM_READ_BIT;
    public const ulong InputAttachmentRead           = Vk.VK_ACCESS_2_INPUT_ATTACHMENT_READ_BIT;
    public const ulong ShaderRead                    = Vk.VK_ACCESS_2_SHADER_READ_BIT;
    public const ulong ShaderWrite                   = Vk.VK_ACCESS_2_SHADER_WRITE_BIT;
    public const ulong ColorAttachmentRead           = Vk.VK_ACCESS_2_COLOR_ATTACHMENT_READ_BIT;
    public const ulong ColorAttachmentWrite          = Vk.VK_ACCESS_2_COLOR_ATTACHMENT_WRITE_BIT;
    public const ulong DepthStencilAttachmentRead    = Vk.VK_ACCESS_2_DEPTH_STENCIL_ATTACHMENT_READ_BIT;
    public const ulong DepthStencilAttachmentWrite   = Vk.VK_ACCESS_2_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT;
    public const ulong TransferRead                  = Vk.VK_ACCESS_2_TRANSFER_READ_BIT;
    public const ulong TransferWrite                 = Vk.VK_ACCESS_2_TRANSFER_WRITE_BIT;
    public const ulong HostRead                      = Vk.VK_ACCESS_2_HOST_READ_BIT;
    public const ulong HostWrite                     = Vk.VK_ACCESS_2_HOST_WRITE_BIT;
    public const ulong MemoryRead                    = Vk.VK_ACCESS_2_MEMORY_READ_BIT;
    public const ulong MemoryWrite                   = Vk.VK_ACCESS_2_MEMORY_WRITE_BIT;
    public const ulong ShaderSampledRead             = Vk.VK_ACCESS_2_SHADER_SAMPLED_READ_BIT;
    public const ulong ShaderStorageRead             = Vk.VK_ACCESS_2_SHADER_STORAGE_READ_BIT;
    public const ulong ShaderStorageWrite            = Vk.VK_ACCESS_2_SHADER_STORAGE_WRITE_BIT;
}
