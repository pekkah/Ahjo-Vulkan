namespace Ahjo.Vulkan.Slang;

/// <summary>
/// What a <see cref="SlangSession"/> compiles for: one SPIR-V target, its
/// profile, its optimization level, and where <c>import</c> looks for files.
/// </summary>
/// <remarks>
/// <para><c>default(SlangSessionDescription)</c> is the configuration this
/// package exists to produce: SPIR-V 1.5, Slang's default optimization level,
/// direct SPIR-V emission on, no search paths. Every member below states what
/// "unset" means, because a description struct whose zero value is invalid is
/// a trap (issue #119's valid-by-default rule).</para>
/// </remarks>
public readonly record struct SlangSessionDescription
{
    // Stored inverted so that `default` means "on". EmitSpirvDirectly is
    // Slang's own default (kDefaultTargetFlags ==
    // SLANG_TARGET_FLAG_GENERATE_SPIRV_DIRECTLY, spec E7) and it is the only
    // path this package was measured on; a bool field that defaults to false
    // would make `default(SlangSessionDescription)` mean "route SPIR-V through
    // glslang", which is both the slower path and the one that needs a native
    // library this package deliberately does not ship (spec E6 / OPEN-1).
    private readonly bool _viaGlslang;

    /// <summary>
    /// The SPIR-V profile name handed to <c>IGlobalSession::findProfile</c> —
    /// e.g. <c>Utf8Name.FromLiteral("spirv_1_5"u8)</c>. Default (null) means
    /// <c>"spirv_1_5"u8</c>.
    /// </summary>
    /// <remarks>
    /// A <see cref="Utf8Name"/> rather than a <see cref="string"/>: profile
    /// names are compile-time constants, so this is invariant #1 in its
    /// literal form — a <c>"…"u8</c> span in the assembly's read-only data
    /// segment, with no encoding step and nothing for the GC to move.
    /// <see cref="Utf8Name"/> rather than <c>ReadOnlySpan&lt;byte&gt;</c>
    /// because a <c>ref struct</c> cannot be a field of a description struct
    /// — which is the reason <see cref="Utf8Name"/> exists
    /// (<c>src/Ahjo.Vulkan/Lifecycle/Utf8Name.cs:10-12</c>).
    /// </remarks>
    public Utf8Name SpirvProfile { get; init; }

    /// <summary>
    /// Optimization level for the target. Defaults to
    /// <see cref="SlangOptimizationLevel.Default"/>.
    /// </summary>
    public SlangOptimizationLevel Optimization { get; init; }

    /// <summary>
    /// Emit SPIR-V directly from Slang's own backend rather than routing
    /// through GLSL. <b>Defaults to <see langword="true"/></b> — this is
    /// Slang's own default and the only path
    /// <c>Ahjo.Vulkan.Slang.Native</c>'s shipped binary subset covers.
    /// Setting it <see langword="false"/> asks for a downstream compiler this
    /// package does not ship, and the compile will fail saying so.
    /// </summary>
    public bool EmitSpirvDirectly
    {
        get => !_viaGlslang;
        init => _viaGlslang = !value;
    }

    /// <summary>
    /// Directories <c>import</c> and <c>#include</c> search, in order. Null or
    /// empty (the default) means no file-system search — which is the correct
    /// setting for a program built entirely from in-memory sources.
    /// </summary>
    public string[]? SearchPaths { get; init; }
}
