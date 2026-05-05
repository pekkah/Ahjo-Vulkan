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
    AllGraphics         = 0x0000001F,
    All                 = 0x7FFFFFFF,
}
