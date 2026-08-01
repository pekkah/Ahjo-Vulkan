namespace Ahjo.Vulkan.Slang;

/// <summary>
/// How <see cref="SlangReflection"/> fills <c>DescriptorBinding.Stages</c>.
/// </summary>
/// <remarks>
/// <para>Precision costs a code generation, so it is opt-in. Slang's
/// <em>reflection</em> API cannot answer "which stages use this binding" at
/// all: <c>spReflectionVariableLayout_getStage</c> returns
/// <c>SLANG_STAGE_NONE</c> for every global descriptor parameter, in every
/// fixture measured, composed and single-module alike, and
/// <c>spReflection_ToJson</c> lists the whole global scope under every entry
/// point rather than a narrowed subset. The question is only answerable from
/// the <em>compiled artifact</em> side, through
/// <c>IComponentType::getEntryPointMetadata</c> — which carries the same
/// preconditions as <c>getEntryPointCode</c>, i.e. it compiles.</para>
/// </remarks>
public enum SlangStageAttribution
{
    /// <summary>
    /// Every binding gets the union of the program's entry-point stages.
    /// </summary>
    /// <remarks>
    /// Always valid, sometimes broader than necessary, compiles nothing and
    /// cannot throw. A superset of stages in
    /// <c>VkDescriptorSetLayoutBinding.stageFlags</c> is legal Vulkan; it only
    /// costs a little descriptor visibility.
    /// </remarks>
    ProgramStageUnion,

    /// <summary>
    /// Each binding gets the union of only the stages that actually read it.
    /// </summary>
    /// <remarks>
    /// <para>Costs one <c>getEntryPointMetadata</c> per entry point, which is a
    /// code generation — so building a reflection in this mode can throw
    /// <see cref="SlangCompilationException"/> where
    /// <see cref="ProgramStageUnion"/> never does.</para>
    /// <para>Usage is reported <em>post-optimization</em>. A binding no entry
    /// point touches would come out as <c>ShaderStages.None</c>, which is not a
    /// usable <c>stageFlags</c> value, so such a binding falls back to the
    /// program union.</para>
    /// <para>Push-constant ranges are unaffected: they stay the program union
    /// in both modes, because <c>isParameterLocationUsed</c> reports push
    /// constants as unused even when the stage provably reads them.</para>
    /// </remarks>
    PerEntryPointUsage,
}
