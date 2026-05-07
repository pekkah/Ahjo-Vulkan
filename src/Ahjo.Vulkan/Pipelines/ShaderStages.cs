namespace Ahjo.Vulkan;

/// <summary>
/// Strongly-typed shadow of <c>VkShaderStageFlagBits</c>. Bit values match
/// the underlying enum so a cast to <c>VkShaderStageFlags</c> (a plain
/// <c>uint</c> in the bindings) is a no-op.
/// </summary>
[Flags]
public enum ShaderStages : uint
{
    None                = 0,
    Vertex              = 0x00000001,
    TessellationControl = 0x00000002,
    TessellationEval    = 0x00000004,
    Geometry            = 0x00000008,
    Fragment            = 0x00000010,
    Compute             = 0x00000020,
    Task                = 0x00000040,
    Mesh                = 0x00000080,
    /// <summary>
    /// Pre-mesh classic graphics pipeline:
    /// vert | tessC | tessE | geom | frag. Mirrors Vulkan's
    /// <c>VK_SHADER_STAGE_ALL_GRAPHICS</c> exactly — the spec defines it
    /// as 0x1F and explicitly excludes <see cref="Task"/> / <see cref="Mesh"/>
    /// (a mesh pipeline replaces the vert/tess/geom front end). Use
    /// <c>Vertex | … | Fragment | Task | Mesh</c> explicitly when targeting
    /// mesh shaders alongside fragment.
    /// </summary>
    AllGraphics         = 0x0000001F,
    All                 = 0x7FFFFFFF,
}
