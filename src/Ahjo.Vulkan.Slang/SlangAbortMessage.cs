using System.Diagnostics.CodeAnalysis;
using System.Text;

using Ahjo.Vulkan.Slang.Internal;

namespace Ahjo.Vulkan.Slang;

/// <summary>
/// Decodes one <c>DeviceFaultReport.ShaderAbortMessages</c> payload written by
/// a Slang <c>abort(format, args…)</c>: the format string, the typed arguments
/// and a printf-style rendering.
/// </summary>
/// <remarks>
/// <para><b>The layout is not a Slang contract.</b> What this decodes is the
/// behaviour of the pinned Slang version: the format's UTF-8 bytes and a NUL,
/// zero-padded to a multiple of 4, then each argument in call order, each
/// aligned to its own scalar size (scalar layout; a vector is aligned to its
/// component size). Slang documents <c>abort</c> only as "emits
/// <c>OpAbortKHR</c>". <c>SlangAbortLayoutTests</c> pins the layout against the
/// SPIR-V the pinned compiler emits, so a Slang bump that changes it fails the
/// suite rather than misdecoding.</para>
/// <para><b>Specifiers.</b> C <c>printf</c> grammar,
/// <c>%[flags][width][.precision][length][vN]conversion</c>, with flags
/// <c>- + space 0 #</c>, decimal width and precision, and a GLSL
/// <c>debugPrintfEXT</c>-style vector prefix <c>v2</c>–<c>v4</c> accepted
/// before or after the length modifier:</para>
/// <list type="table">
/// <listheader><term>Conversion</term><description>none / <c>hh</c> / <c>h</c> / <c>l</c>, <c>ll</c></description></listheader>
/// <item><term><c>d</c>, <c>i</c></term><description><see cref="SlangAbortArgumentKind.Int32"/> / <see cref="SlangAbortArgumentKind.Int8"/> / <see cref="SlangAbortArgumentKind.Int16"/> / <see cref="SlangAbortArgumentKind.Int64"/></description></item>
/// <item><term><c>u</c>, <c>x</c>, <c>X</c></term><description><see cref="SlangAbortArgumentKind.UInt32"/> / <see cref="SlangAbortArgumentKind.UInt8"/> / <see cref="SlangAbortArgumentKind.UInt16"/> / <see cref="SlangAbortArgumentKind.UInt64"/></description></item>
/// <item><term><c>f F e E g G</c></term><description><see cref="SlangAbortArgumentKind.Float32"/> / unsupported / <see cref="SlangAbortArgumentKind.Float16"/> / <see cref="SlangAbortArgumentKind.Float64"/></description></item>
/// <item><term><c>%%</c></term><description>a literal <c>%</c>; consumes no argument</description></item>
/// </list>
/// <para>Two departures from C are deliberate: <c>%hf</c> is a 16-bit half,
/// because C has no half type and the payload needs the width; and
/// <c>%lf</c> is a 64-bit double, because <c>%f</c> here means the shader's
/// 32-bit <c>float</c>. Everything else (<c>%s %c %p %n %a %o</c>, the length
/// modifiers <c>L z j t</c>, a <c>*</c> width or precision) fails decoding;
/// Slang refuses string arguments, so <c>%s</c> could never match one.</para>
/// <para><b>Argument widths come from the format</b>, as in C. Slang does not
/// check the format against the arguments, so <see cref="Arguments"/> keep the
/// raw bits and payload offset of each value. Bytes left over after the last
/// argument are ignored, as C ignores extra arguments.</para>
/// <para><b>Never throws.</b> A malformed payload makes
/// <see cref="TryDecode"/> return <see langword="false"/>, and
/// <see cref="Describe"/> always returns readable text: it is the
/// crash-handler entry point, and runs on the same path as the device-fault
/// read that produced the bytes.</para>
/// <para><b>Producing aborts.</b> The device needs <c>VK_KHR_shader_abort</c>
/// with the <c>shaderAbort</c> feature (see
/// <c>VulkanExtensions.KhrShaderAbort</c>), and the session needs
/// <c>Capabilities = [Utf8Name.FromLiteral("spvAbort"u8)]</c> for a
/// warning-free compile under the default profile.</para>
/// </remarks>
public sealed class SlangAbortMessage
{
    private const int DescribeHexBytes = 64;

    private SlangAbortMessage(string format, SlangAbortArgument[] arguments, string text)
    {
        Format = format;
        Arguments = arguments;
        Text = text;
    }

    /// <summary>The UTF-8 format string, up to its NUL. Invalid UTF-8 is
    /// replaced with U+FFFD.</summary>
    public string Format { get; }

    /// <summary>One element per scalar, in payload order; a vector specifier
    /// contributes one per component. Never null.</summary>
    public SlangAbortArgument[] Arguments { get; }

    /// <summary>The printf-style rendering of <see cref="Format"/> with
    /// <see cref="Arguments"/>, using the invariant culture. C-like, not
    /// byte-identical to any particular libc.</summary>
    public string Text { get; }

    /// <inheritdoc cref="Text"/>
    public override string ToString() => Text;

    /// <summary>
    /// Decodes one Slang abort payload.
    /// </summary>
    /// <param name="payload">One element of
    /// <c>DeviceFaultReport.ShaderAbortMessages</c>.</param>
    /// <param name="message">The decoded message, when the method returns
    /// <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when the payload is not a decodable
    /// Slang abort message: no NUL, an unsupported specifier, or too few
    /// argument bytes.</returns>
    public static bool TryDecode(ReadOnlySpan<byte> payload, [NotNullWhen(true)] out SlangAbortMessage? message)
    {
        message = null;
        var specifiers = new List<SlangAbortSpecifier>();
        var arguments = new List<SlangAbortArgument>();

        if (!SlangAbortFormat.TryComputeLayout(payload, out string? format, specifiers, arguments, out _))
        {
            return false;
        }

        string text = SlangAbortRenderer.Render(format, specifiers, arguments);
        message = new SlangAbortMessage(format, arguments.ToArray(), text);

        return true;
    }

    /// <summary>
    /// Renders one payload as text, whatever it contains: the decoded
    /// <see cref="Text"/>, or the format plus why its arguments did not decode,
    /// or a hex dump of the first 64 bytes when the payload is not a Slang abort
    /// message at all. Never throws and never returns null.
    /// </summary>
    /// <example><code>
    /// foreach (byte[] p in report.ShaderAbortMessages)
    ///     log(SlangAbortMessage.Describe(p));
    /// </code></example>
    public static string Describe(ReadOnlySpan<byte> payload)
    {
        var specifiers = new List<SlangAbortSpecifier>();
        var arguments = new List<SlangAbortArgument>();

        if (SlangAbortFormat.TryComputeLayout(payload, out string? format, specifiers, arguments, out string? reason))
        {
            return SlangAbortRenderer.Render(format, specifiers, arguments);
        }

        if (format is not null)
        {
            return $"{format} <abort arguments not decoded: {reason}; {payload.Length} payload bytes>";
        }

        int n = payload.Length;

        if (n == 0)
        {
            return "<shader abort payload, 0 bytes, not a Slang abort message>";
        }

        var hex = new StringBuilder();
        int shown = Math.Min(n, DescribeHexBytes);

        for (int i = 0; i < shown; i++)
        {
            if (i > 0)
            {
                hex.Append(' ');
            }

            hex.Append(HexDigit(payload[i] >> 4)).Append(HexDigit(payload[i] & 0xF));
        }

        if (n > DescribeHexBytes)
        {
            hex.Append(" …");
        }

        return $"<shader abort payload, {n} bytes, not a Slang abort message: {hex}>";
    }

    private static char HexDigit(int nibble) => (char)(nibble < 10 ? '0' + nibble : 'a' + (nibble - 10));
}
