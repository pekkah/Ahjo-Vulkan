namespace Ahjo.Vulkan.Slang;

/// <summary>
/// Thrown when Slang refuses to produce something: a module that will not
/// parse or check, an entry point that does not exist at the requested stage,
/// a composite that will not link, or a target that will not codegen.
/// </summary>
/// <remarks>
/// <para><see cref="Diagnostics"/> carries the compiler's own text, verbatim
/// and unabridged — Slang's diagnostics already name the file, the line, the
/// column and the offending token, and reformatting them would only lose
/// information:</para>
/// <code>
/// error[E30015]: undefined identifier
///  --&gt; bad.slang:1:21
///   |
/// 1 | float4 f() { return notAThing; }
///   |                     ^^^^^^^^^ undefined identifier 'notAThing'.
/// </code>
/// <para><see cref="Exception.Message"/> is the first line of that text, so a
/// log line stays a line.</para>
/// <para>There is no code path in this package that swallows a failure and
/// returns an empty SPIR-V blob instead. That is the acceptance criterion
/// issue #166 states, and it is enforced by tests rather than by convention.</para>
/// </remarks>
public sealed class SlangCompilationException : Exception
{
    /// <summary>Creates an exception carrying the compiler's full diagnostics text.</summary>
    /// <param name="operation">
    /// What was attempted, e.g. <c>"loadModuleFromSourceString"</c>. Used when
    /// Slang produced no diagnostics at all, which happens for a bare failing
    /// result code.
    /// </param>
    /// <param name="diagnostics">The compiler's text, possibly empty.</param>
    public SlangCompilationException(string operation, string diagnostics)
        : base(BuildMessage(operation, diagnostics))
        => Diagnostics = diagnostics ?? string.Empty;

    /// <summary>Creates an exception with an explicit message and diagnostics text.</summary>
    public SlangCompilationException(string message, string diagnostics, Exception? innerException)
        : base(message, innerException)
        => Diagnostics = diagnostics ?? string.Empty;

    /// <summary>
    /// The compiler's diagnostics blob, verbatim. Empty when Slang failed
    /// without saying anything.
    /// </summary>
    public string Diagnostics { get; }

    private static string BuildMessage(string operation, string diagnostics)
    {
        if (string.IsNullOrWhiteSpace(diagnostics))
        {
            return $"Slang compilation failed: {operation} reported no diagnostics.";
        }

        ReadOnlySpan<char> text = diagnostics.AsSpan().TrimStart();
        int end = text.IndexOfAny('\r', '\n');
        ReadOnlySpan<char> firstLine = end < 0 ? text : text[..end];

        return $"Slang compilation failed: {firstLine.TrimEnd().ToString()}";
    }
}
