namespace Ahjo.Vulkan.Slang;

/// <summary>
/// A linked program's entry point: the name Slang emitted it under and the
/// pipeline stage it runs at.
/// </summary>
/// <param name="Name">Entry-point name, as reported by reflection.</param>
/// <param name="Stage">
/// The stage, mapped from Slang's <c>SlangStage</c>. Feeds
/// <c>ShaderStages</c>-shaped wrapper APIs directly.
/// </param>
public readonly record struct SlangEntryPointInfo(string Name, ShaderStages Stage);
