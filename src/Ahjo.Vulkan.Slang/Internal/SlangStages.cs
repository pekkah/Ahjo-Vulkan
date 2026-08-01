using Ahjo.Vulkan.Slang.Native;

namespace Ahjo.Vulkan.Slang.Internal;

/// <summary>
/// The <c>SlangStage</c> ↔ <see cref="ShaderStages"/> mapping, in one place and
/// total in both directions.
/// </summary>
/// <remarks>
/// Neither direction has a fallback. A stage that is not mapped throws
/// <see cref="NotSupportedException"/> naming it, because both silent
/// alternatives are worse: <c>SLANG_STAGE_NONE</c> handed to
/// <c>findAndCheckEntryPoint</c> makes Slang look for an entry point at "no
/// stage" and report a confusing miss, and <see cref="ShaderStages.None"/> in
/// a <c>VkPipelineShaderStageCreateInfo</c> is a validation error the caller
/// cannot trace back to here.
/// </remarks>
internal static class SlangStages
{
    /// <summary>Maps a Slang stage to the wrapper's flag.</summary>
    public static ShaderStages ToShaderStages(SlangStage stage) => stage switch
    {
        SlangStage.SLANG_STAGE_VERTEX => ShaderStages.Vertex,
        SlangStage.SLANG_STAGE_HULL => ShaderStages.TessellationControl,
        SlangStage.SLANG_STAGE_DOMAIN => ShaderStages.TessellationEval,
        SlangStage.SLANG_STAGE_GEOMETRY => ShaderStages.Geometry,
        SlangStage.SLANG_STAGE_FRAGMENT => ShaderStages.Fragment,
        SlangStage.SLANG_STAGE_COMPUTE => ShaderStages.Compute,
        SlangStage.SLANG_STAGE_AMPLIFICATION => ShaderStages.Task,
        SlangStage.SLANG_STAGE_MESH => ShaderStages.Mesh,

        // Ray-tracing, callable and work-graph stages have no named member in
        // ShaderStages, which shadows VkShaderStageFlagBits' graphics/compute
        // subset only. Adding them is a change to src/Ahjo.Vulkan/ and is out
        // of scope here (spec D5's "reflection adapts to the existing types").
        _ => throw new NotSupportedException(
            $"Slang stage {stage} has no ShaderStages equivalent; this package covers the graphics and compute stages."),
    };

    /// <summary>Maps a wrapper flag to the Slang stage to ask for.</summary>
    /// <remarks>
    /// Takes exactly one flag. A combination such as
    /// <c>Vertex | Fragment</c> is not a stage an entry point can be found at,
    /// so it throws rather than picking the lowest bit.
    /// </remarks>
    public static SlangStage ToSlangStage(ShaderStages stage) => stage switch
    {
        ShaderStages.Vertex => SlangStage.SLANG_STAGE_VERTEX,
        ShaderStages.TessellationControl => SlangStage.SLANG_STAGE_HULL,
        ShaderStages.TessellationEval => SlangStage.SLANG_STAGE_DOMAIN,
        ShaderStages.Geometry => SlangStage.SLANG_STAGE_GEOMETRY,
        ShaderStages.Fragment => SlangStage.SLANG_STAGE_FRAGMENT,
        ShaderStages.Compute => SlangStage.SLANG_STAGE_COMPUTE,
        ShaderStages.Task => SlangStage.SLANG_STAGE_AMPLIFICATION,
        ShaderStages.Mesh => SlangStage.SLANG_STAGE_MESH,

        _ => throw new NotSupportedException(
            $"ShaderStages value '{stage}' is not a single Slang entry-point stage."),
    };
}
