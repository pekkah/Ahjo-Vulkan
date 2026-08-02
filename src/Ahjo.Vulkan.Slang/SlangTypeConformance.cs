namespace Ahjo.Vulkan.Slang;

/// <summary>
/// Declares that <paramref name="ConcreteType"/> implements
/// <paramref name="InterfaceType"/>, so a program dispatching through the
/// interface can generate code for it.
/// </summary>
/// <remarks>
/// <para>The semantics — when the names are resolved, and what happens without
/// a conformance — are documented on
/// <see cref="SlangProgramBuilder.AddTypeConformance"/>, which is where this
/// ends up.</para>
/// <para>A named type rather than a <c>(string, string)</c> tuple because it has
/// somewhere to grow: Slang's <c>conformanceIdOverride</c> (passed as <c>-1</c>
/// today, so Slang assigns the dispatch ID) is a member this type will want once
/// someone needs deterministic dispatch IDs, and a <c>ValueTuple</c> cannot
/// acquire one. The override is deliberately not exposed now — this package has
/// never exercised it.</para>
/// </remarks>
/// <param name="ConcreteType">The implementing type's name, e.g. <c>"Glossy"</c>.</param>
/// <param name="InterfaceType">The interface's name, e.g. <c>"ISurface"</c>.</param>
public readonly record struct SlangTypeConformance(string ConcreteType, string InterfaceType);
