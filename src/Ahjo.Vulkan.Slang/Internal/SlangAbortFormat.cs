using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Ahjo.Vulkan.Slang.Internal;

/// <summary>
/// The format grammar and payload layout behind
/// <see cref="SlangAbortMessage"/>: parse the specifiers, then read each
/// argument at its scalar-layout offset.
/// </summary>
/// <remarks>
/// <para>Total by construction: every bound is checked before slicing, UTF-8
/// is decoded with replacement, and there is no <c>try</c>/<c>catch</c> — a
/// catch would hide a bounds bug from the never-throw property tests that are
/// the actual guard.</para>
/// <para>Width and precision saturate at <see cref="MaxWidthOrPrecision"/>, so
/// a format such as <c>%999999999d</c> cannot turn the crash path into an
/// out-of-memory throw when it is rendered.</para>
/// </remarks>
internal static class SlangAbortFormat
{
    /// <summary>The largest width or precision honoured. Larger values are
    /// clamped; no real abort message needs more.</summary>
    internal const int MaxWidthOrPrecision = 512;

    /// <summary>
    /// Parses every conversion specifier in <paramref name="format"/>, in
    /// order, into <paramref name="specifiers"/>. <c>%%</c> is consumed (both
    /// characters) and emits nothing.
    /// </summary>
    /// <returns><see langword="false"/> with a short <paramref name="reason"/>
    /// on the first unsupported or malformed specifier.</returns>
    internal static bool TryParse(
        string format,
        List<SlangAbortSpecifier> specifiers,
        [NotNullWhen(false)] out string? reason)
    {
        reason = null;
        int i = 0;

        while (i < format.Length)
        {
            if (format[i] != '%')
            {
                i++;

                continue;
            }

            int start = i;
            int j = i + 1;

            if (j >= format.Length)
            {
                reason = "incomplete specifier at end of format";

                return false;
            }

            if (format[j] == '%')
            {
                i = j + 1;

                continue;
            }

            // Flags.
            SlangAbortFlags flags = SlangAbortFlags.None;

            while (j < format.Length)
            {
                SlangAbortFlags flag = format[j] switch
                {
                    '-' => SlangAbortFlags.Left,
                    '+' => SlangAbortFlags.Plus,
                    ' ' => SlangAbortFlags.Space,
                    '0' => SlangAbortFlags.Zero,
                    '#' => SlangAbortFlags.Alt,
                    _ => SlangAbortFlags.None,
                };

                if (flag == SlangAbortFlags.None)
                {
                    break;
                }

                flags |= flag;
                j++;
            }

            // Width.
            if (j < format.Length && format[j] == '*')
            {
                reason = $"'*' width or precision at format index {start}";

                return false;
            }

            int width = ReadNumber(format, ref j);

            // Precision.
            int precision = -1;

            if (j < format.Length && format[j] == '.')
            {
                j++;

                if (j < format.Length && format[j] == '*')
                {
                    reason = $"'*' width or precision at format index {start}";

                    return false;
                }

                precision = Math.Max(0, ReadNumber(format, ref j));
            }

            // Length modifier and vector prefix, in either order.
            int components = 1;
            string length = string.Empty;

            if (!TryReadVector(format, ref j, ref components, start, out reason)
                || !TryReadLength(format, ref j, ref length, start, out reason))
            {
                return false;
            }

            if (components == 1 && !TryReadVector(format, ref j, ref components, start, out reason))
            {
                return false;
            }

            if (j >= format.Length)
            {
                reason = "incomplete specifier at end of format";

                return false;
            }

            char conversion = format[j];

            if (!TryMapKind(conversion, length, start, out SlangAbortArgumentKind kind, out reason))
            {
                return false;
            }

            specifiers.Add(new SlangAbortSpecifier(
                start, j - start + 1, conversion, kind, components, flags, width, precision));
            i = j + 1;
        }

        return true;
    }

    /// <summary>
    /// Finds the format in <paramref name="payload"/>, parses it, and reads
    /// every argument at its scalar-layout offset.
    /// </summary>
    /// <param name="payload">One abort payload.</param>
    /// <param name="format">The decoded format when a NUL was found (on success
    /// and on an argument failure); null when there is no NUL.</param>
    /// <param name="specifiers">Receives the parsed specifiers.</param>
    /// <param name="arguments">Receives one argument per scalar component.</param>
    /// <param name="reason">Why decoding failed.</param>
    internal static bool TryComputeLayout(
        ReadOnlySpan<byte> payload,
        [NotNullWhen(true)] out string? format,
        List<SlangAbortSpecifier> specifiers,
        List<SlangAbortArgument> arguments,
        [NotNullWhen(false)] out string? reason)
    {
        format = null;
        int nul = payload.IndexOf((byte)0);

        if (nul < 0)
        {
            reason = "no NUL terminator";

            return false;
        }

        // Encoding.UTF8 replaces invalid sequences with U+FFFD; it never throws.
        format = Encoding.UTF8.GetString(payload[..nul]);

        if (!TryParse(format, specifiers, out reason))
        {
            return false;
        }

        if (specifiers.Count == 0)
        {
            return true;
        }

        // The format is padded to a multiple of 4 (one uint word per 4 bytes).
        long argStart = AlignUp(nul + 1L, 4);

        if (argStart > payload.Length)
        {
            reason = "format string ends past the payload";

            return false;
        }

        long cursor = argStart;
        int argument = 0;

        foreach (SlangAbortSpecifier specifier in specifiers)
        {
            int size = SizeOf(specifier.Kind);

            for (int c = 0; c < specifier.Components; c++)
            {
                argument++;
                long offset = AlignUp(cursor, size);

                if (offset + size > payload.Length)
                {
                    reason = $"argument {argument} needs {size} bytes at offset {offset}, payload has {payload.Length}";

                    return false;
                }

                ReadOnlySpan<byte> bytes = payload.Slice((int)offset, size);
                ulong bits = size switch
                {
                    1 => bytes[0],
                    2 => BinaryPrimitives.ReadUInt16LittleEndian(bytes),
                    4 => BinaryPrimitives.ReadUInt32LittleEndian(bytes),
                    _ => BinaryPrimitives.ReadUInt64LittleEndian(bytes),
                };

                arguments.Add(new SlangAbortArgument(specifier.Kind, (int)offset, bits));
                cursor = offset + size;
            }
        }

        reason = null;

        return true;
    }

    /// <summary>The byte size of one scalar of <paramref name="kind"/>.</summary>
    internal static int SizeOf(SlangAbortArgumentKind kind) => kind switch
    {
        SlangAbortArgumentKind.Int8 or SlangAbortArgumentKind.UInt8 => 1,
        SlangAbortArgumentKind.Int16 or SlangAbortArgumentKind.UInt16 or SlangAbortArgumentKind.Float16 => 2,
        SlangAbortArgumentKind.Int32 or SlangAbortArgumentKind.UInt32 or SlangAbortArgumentKind.Float32 => 4,
        _ => 8,
    };

    private static long AlignUp(long value, int alignment) => (value + alignment - 1) / alignment * alignment;

    /// <summary>Reads decimal digits, saturating at
    /// <see cref="MaxWidthOrPrecision"/>; -1 when there are none.</summary>
    private static int ReadNumber(string format, ref int j)
    {
        int value = -1;

        while (j < format.Length && format[j] >= '0' && format[j] <= '9')
        {
            int digit = format[j] - '0';
            value = value < 0 ? digit : Math.Min(MaxWidthOrPrecision, (value * 10) + digit);
            j++;
        }

        return value;
    }

    private static bool TryReadVector(
        string format, ref int j, ref int components, int start, [NotNullWhen(false)] out string? reason)
    {
        reason = null;

        if (j >= format.Length || format[j] != 'v')
        {
            return true;
        }

        j++;

        if (j >= format.Length || format[j] < '2' || format[j] > '4')
        {
            reason = $"invalid vector size at format index {start}";

            return false;
        }

        components = format[j] - '0';
        j++;

        return true;
    }

    private static bool TryReadLength(
        string format, ref int j, ref string length, int start, [NotNullWhen(false)] out string? reason)
    {
        reason = null;

        if (j >= format.Length)
        {
            return true;
        }

        switch (format[j])
        {
            case 'h':
                j++;
                length = "h";

                if (j < format.Length && format[j] == 'h')
                {
                    j++;
                    length = "hh";
                }

                return true;

            case 'l':
                j++;
                length = "l";

                if (j < format.Length && format[j] == 'l')
                {
                    j++;
                    length = "ll";
                }

                return true;

            case 'L':
            case 'z':
            case 'j':
            case 't':
                reason = $"unsupported length modifier '{format[j]}' at format index {start}";

                return false;

            default:
                return true;
        }
    }

    private static bool TryMapKind(
        char conversion,
        string length,
        int start,
        out SlangAbortArgumentKind kind,
        [NotNullWhen(false)] out string? reason)
    {
        reason = null;
        kind = default;

        switch (conversion)
        {
            case 'd':
            case 'i':
                kind = length switch
                {
                    "hh" => SlangAbortArgumentKind.Int8,
                    "h" => SlangAbortArgumentKind.Int16,
                    "l" or "ll" => SlangAbortArgumentKind.Int64,
                    _ => SlangAbortArgumentKind.Int32,
                };

                return true;

            case 'u':
            case 'x':
            case 'X':
                kind = length switch
                {
                    "hh" => SlangAbortArgumentKind.UInt8,
                    "h" => SlangAbortArgumentKind.UInt16,
                    "l" or "ll" => SlangAbortArgumentKind.UInt64,
                    _ => SlangAbortArgumentKind.UInt32,
                };

                return true;

            case 'f':
            case 'F':
            case 'e':
            case 'E':
            case 'g':
            case 'G':
                if (length == "hh")
                {
                    reason = $"unsupported conversion '{conversion}' with length '{length}' at format index {start}";

                    return false;
                }

                kind = length switch
                {
                    "h" => SlangAbortArgumentKind.Float16,
                    "l" or "ll" => SlangAbortArgumentKind.Float64,
                    _ => SlangAbortArgumentKind.Float32,
                };

                return true;

            default:
                reason = $"unsupported conversion '{conversion}' at format index {start}";

                return false;
        }
    }
}
