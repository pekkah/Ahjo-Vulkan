namespace Ahjo.Vulkan.Slang;

/// <summary>
/// How hard Slang optimizes the emitted SPIR-V.
/// </summary>
/// <remarks>
/// The numeric values match <c>SlangOptimizationLevel</c> in the native
/// bindings (<c>SLANG_OPTIMIZATION_LEVEL_NONE</c> = 0 …
/// <c>SLANG_OPTIMIZATION_LEVEL_MAXIMAL</c> = 3), which is what lets
/// <see cref="SlangCompiler.CreateSession"/> pass the level straight through
/// as a <c>CompilerOptionName.Optimization</c> integer without a mapping
/// table. Keep them in sync if the native enum ever changes.
/// </remarks>
public enum SlangOptimizationLevel
{
    /// <summary>No optimization.</summary>
    None = 0,

    /// <summary>Slang's default level. What you get when the description leaves this unset.</summary>
    Default = 1,

    /// <summary>More aggressive than <see cref="Default"/>.</summary>
    High = 2,

    /// <summary>Everything Slang has, at the cost of compile time.</summary>
    Maximal = 3,
}
