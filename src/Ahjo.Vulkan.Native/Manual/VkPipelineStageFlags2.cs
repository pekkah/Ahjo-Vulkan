namespace Ahjo.Vulkan.Native;

/// <summary>
/// Typed access to <c>VkPipelineStageFlags2</c> bit values. The Vulkan
/// spec defines the sync2 stage bits via <c>#define ((VkFlags64)0x…)</c>
/// rather than as a C enum (C enums aren't reliably 64-bit pre-C23), so
/// ClangSharp emits them as <c>public const ulong VK_PIPELINE_STAGE_2_…</c>
/// on <c>Vk</c>. Grouping them under this name matches the shape of
/// Vortice's <c>VkPipelineStageFlags2</c> enum and keeps call sites
/// readable:
/// <code>
/// srcStageMask = VkPipelineStageFlags2.ComputeShader | VkPipelineStageFlags2.FragmentShader,
/// </code>
/// No cast is needed because the values are <c>ulong</c> and the struct
/// fields (<c>VkImageMemoryBarrier2.srcStageMask</c> etc.) are also
/// <c>ulong</c>. Each member sources from the generated <c>Vk.VK_*_BIT</c>
/// constant so the values stay in lock-step across regen.
///
/// Scope: core Vulkan 1.3 baseline only. <c>_KHR</c> aliases and vendor
/// extensions (NV / EXT / HUAWEI / ARM / etc.) remain accessible via
/// <c>Vk.VK_PIPELINE_STAGE_2_*</c>.
/// </summary>
public static class VkPipelineStageFlags2
{
    public const ulong None                          = Vk.VK_PIPELINE_STAGE_2_NONE;
    public const ulong TopOfPipe                     = Vk.VK_PIPELINE_STAGE_2_TOP_OF_PIPE_BIT;
    public const ulong DrawIndirect                  = Vk.VK_PIPELINE_STAGE_2_DRAW_INDIRECT_BIT;
    public const ulong VertexInput                   = Vk.VK_PIPELINE_STAGE_2_VERTEX_INPUT_BIT;
    public const ulong VertexShader                  = Vk.VK_PIPELINE_STAGE_2_VERTEX_SHADER_BIT;
    public const ulong TessellationControlShader     = Vk.VK_PIPELINE_STAGE_2_TESSELLATION_CONTROL_SHADER_BIT;
    public const ulong TessellationEvaluationShader  = Vk.VK_PIPELINE_STAGE_2_TESSELLATION_EVALUATION_SHADER_BIT;
    public const ulong GeometryShader                = Vk.VK_PIPELINE_STAGE_2_GEOMETRY_SHADER_BIT;
    public const ulong FragmentShader                = Vk.VK_PIPELINE_STAGE_2_FRAGMENT_SHADER_BIT;
    public const ulong EarlyFragmentTests            = Vk.VK_PIPELINE_STAGE_2_EARLY_FRAGMENT_TESTS_BIT;
    public const ulong LateFragmentTests             = Vk.VK_PIPELINE_STAGE_2_LATE_FRAGMENT_TESTS_BIT;
    public const ulong ColorAttachmentOutput         = Vk.VK_PIPELINE_STAGE_2_COLOR_ATTACHMENT_OUTPUT_BIT;
    public const ulong ComputeShader                 = Vk.VK_PIPELINE_STAGE_2_COMPUTE_SHADER_BIT;
    public const ulong AllTransfer                   = Vk.VK_PIPELINE_STAGE_2_ALL_TRANSFER_BIT;
    public const ulong Transfer                      = Vk.VK_PIPELINE_STAGE_2_TRANSFER_BIT;
    public const ulong BottomOfPipe                  = Vk.VK_PIPELINE_STAGE_2_BOTTOM_OF_PIPE_BIT;
    public const ulong Host                          = Vk.VK_PIPELINE_STAGE_2_HOST_BIT;
    public const ulong AllGraphics                   = Vk.VK_PIPELINE_STAGE_2_ALL_GRAPHICS_BIT;
    public const ulong AllCommands                   = Vk.VK_PIPELINE_STAGE_2_ALL_COMMANDS_BIT;
    public const ulong Copy                          = Vk.VK_PIPELINE_STAGE_2_COPY_BIT;
    public const ulong Resolve                       = Vk.VK_PIPELINE_STAGE_2_RESOLVE_BIT;
    public const ulong Blit                          = Vk.VK_PIPELINE_STAGE_2_BLIT_BIT;
    public const ulong Clear                         = Vk.VK_PIPELINE_STAGE_2_CLEAR_BIT;
    public const ulong IndexInput                    = Vk.VK_PIPELINE_STAGE_2_INDEX_INPUT_BIT;
    public const ulong VertexAttributeInput          = Vk.VK_PIPELINE_STAGE_2_VERTEX_ATTRIBUTE_INPUT_BIT;
    public const ulong PreRasterizationShaders       = Vk.VK_PIPELINE_STAGE_2_PRE_RASTERIZATION_SHADERS_BIT;
}
