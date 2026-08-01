namespace Ahjo.Vulkan.Slang;

/// <summary>
/// One module in, one linked program out — the convenience path over
/// <see cref="SlangSession.Compile"/>.
/// </summary>
/// <remarks>
/// <para>Exactly one of <see cref="Path"/> and <see cref="Source"/> must be
/// set. <see cref="Path"/> reads a file (resolved against the session's
/// <see cref="SlangSessionDescription.SearchPaths"/>);
/// <see cref="Source"/> compiles an in-memory string with no file system
/// involved.</para>
/// <para>For a program assembled from several modules — the shape a material
/// system needs — the component list and <em>its order</em> are part of the
/// layout contract and cannot be expressed here. That is a separate surface;
/// see <see cref="SlangSession.LoadModuleFromSource(string, string, string)"/>
/// and <see cref="SlangModule"/>.</para>
/// </remarks>
public readonly record struct SlangCompileRequest
{
    /// <summary>
    /// File to compile. Mutually exclusive with <see cref="Source"/>. Also
    /// used as the reported source path when <see cref="Source"/> is set.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Slang/HLSL source text to compile. Mutually exclusive with
    /// <see cref="Path"/>.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Name the module is registered under in the session, so a later
    /// module's <c>import</c> can find it. Defaults to
    /// <see cref="Path"/>'s file name, or <c>"module"</c> when compiling a
    /// bare source string.
    /// </summary>
    public string? ModuleName { get; init; }

    /// <summary>
    /// Entry points to link, in order. <see langword="null"/> (the default)
    /// links every entry point the module defines with a
    /// <c>[shader("…")]</c> attribute, in declaration order.
    /// </summary>
    /// <remarks>
    /// The order given here is the order Slang assigns entry-point indices, so
    /// it is the order <see cref="SlangProgram.EntryPoint"/> and
    /// <see cref="SlangProgram.Spirv"/> report.
    /// </remarks>
    public IReadOnlyList<string>? EntryPoints { get; init; }
}
