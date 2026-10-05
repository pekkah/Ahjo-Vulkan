using System.Globalization;
using System.Text;

namespace Ahjo.Vulkan.Slang.Internal;

/// <summary>
/// The C-like printf rendering behind <see cref="SlangAbortMessage.Text"/>.
/// </summary>
/// <remarks>
/// <para>Built with a <see cref="StringBuilder"/> and invariant-culture
/// formatting of primitives — no <c>string.Format</c> with a runtime format
/// string, nothing trim-unsafe. C-like, not byte-identical to any particular
/// libc: the fixed vectors in <c>SlangAbortMessageTests</c> are the
/// definition.</para>
/// <para>Integers honour <c>- + space 0 #</c>, width and precision (minimum
/// digits). Floats follow C: <c>f</c> with default precision 6, <c>e</c> with a
/// signed exponent of at least two digits, <c>g</c> choosing <c>f</c> or
/// <c>e</c> by C's rule and stripping trailing zeros unless <c>#</c>, and NaN
/// and infinity as <c>nan</c> / <c>inf</c> (uppercase for <c>F E G</c>). A
/// vector renders its components joined with <c>", "</c>.</para>
/// </remarks>
internal static class SlangAbortRenderer
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>
    /// Renders <paramref name="format"/> with <paramref name="arguments"/>.
    /// The inputs are the output of a successful
    /// <see cref="SlangAbortFormat.TryComputeLayout"/>: one argument per
    /// specifier component.
    /// </summary>
    internal static string Render(
        string format,
        List<SlangAbortSpecifier> specifiers,
        List<SlangAbortArgument> arguments)
    {
        var text = new StringBuilder(format.Length + 16);
        int position = 0;
        int argument = 0;

        foreach (SlangAbortSpecifier specifier in specifiers)
        {
            AppendLiteral(text, format, position, specifier.Start);

            for (int c = 0; c < specifier.Components && argument < arguments.Count; c++)
            {
                if (c > 0)
                {
                    text.Append(", ");
                }

                AppendValue(text, specifier, arguments[argument++]);
            }

            position = specifier.Start + specifier.Length;
        }

        AppendLiteral(text, format, position, format.Length);

        return text.ToString();
    }

    /// <summary>Copies a literal run, turning each <c>%%</c> into <c>%</c>.</summary>
    private static void AppendLiteral(StringBuilder text, string format, int from, int to)
    {
        for (int i = from; i < to; i++)
        {
            text.Append(format[i]);

            if (format[i] == '%' && i + 1 < to && format[i + 1] == '%')
            {
                i++;
            }
        }
    }

    private static void AppendValue(StringBuilder text, in SlangAbortSpecifier specifier, SlangAbortArgument argument)
    {
        switch (argument.Kind)
        {
            case SlangAbortArgumentKind.Float16:
            case SlangAbortArgumentKind.Float32:
            case SlangAbortArgumentKind.Float64:
                AppendFloat(text, specifier, argument.AsDouble());

                break;

            default:
                AppendInteger(text, specifier, argument);

                break;
        }
    }

    private static void AppendInteger(StringBuilder text, in SlangAbortSpecifier specifier, SlangAbortArgument argument)
    {
        SlangAbortFlags flags = specifier.Flags;
        string sign = string.Empty;
        ulong magnitude;

        if (specifier.Conversion is 'd' or 'i')
        {
            long value = argument.AsInt64();
            bool negative = value < 0;
            // -(value + 1) + 1 stays in range for long.MinValue.
            magnitude = negative ? (ulong)(-(value + 1)) + 1 : (ulong)value;
            sign = negative ? "-"
                : (flags & SlangAbortFlags.Plus) != 0 ? "+"
                : (flags & SlangAbortFlags.Space) != 0 ? " "
                : string.Empty;
        }
        else
        {
            // u / x / X always map to an unsigned kind: Bits is the value.
            magnitude = argument.Bits;
        }

        string digits = specifier.Conversion switch
        {
            'x' => magnitude.ToString("x", Invariant),
            'X' => magnitude.ToString("X", Invariant),
            _ => magnitude.ToString(Invariant),
        };

        if (specifier.Precision >= 0)
        {
            // C: an explicit precision of 0 prints no digits for a zero value.
            digits = specifier.Precision == 0 && magnitude == 0
                ? string.Empty
                : digits.PadLeft(specifier.Precision, '0');
        }

        string prefix = (flags & SlangAbortFlags.Alt) != 0 && magnitude != 0 && specifier.Conversion is 'x' or 'X'
            ? (specifier.Conversion == 'x' ? "0x" : "0X")
            : string.Empty;

        // The 0 flag is ignored when a precision is given, as in C.
        bool zeroPad = (flags & SlangAbortFlags.Zero) != 0 && specifier.Precision < 0;
        AppendPadded(text, specifier, sign + prefix, digits, zeroPad);
    }

    private static void AppendFloat(StringBuilder text, in SlangAbortSpecifier specifier, double value)
    {
        SlangAbortFlags flags = specifier.Flags;
        bool upper = specifier.Conversion is 'F' or 'E' or 'G';

        if (double.IsNaN(value))
        {
            AppendPadded(text, specifier, string.Empty, upper ? "NAN" : "nan", zeroPad: false);

            return;
        }

        string sign = double.IsNegative(value) ? "-"
            : (flags & SlangAbortFlags.Plus) != 0 ? "+"
            : (flags & SlangAbortFlags.Space) != 0 ? " "
            : string.Empty;

        if (double.IsInfinity(value))
        {
            AppendPadded(text, specifier, sign, upper ? "INF" : "inf", zeroPad: false);

            return;
        }

        double magnitude = Math.Abs(value);
        int precision = specifier.Precision < 0 ? 6 : specifier.Precision;
        bool alt = (flags & SlangAbortFlags.Alt) != 0;

        string body = specifier.Conversion switch
        {
            'e' or 'E' => FormatExponential(magnitude, precision, alt, upper),
            'g' or 'G' => FormatGeneral(magnitude, precision, alt, upper),
            _ => FormatFixed(magnitude, precision, alt),
        };

        AppendPadded(text, specifier, sign, body, (flags & SlangAbortFlags.Zero) != 0);
    }

    private static string FormatFixed(double magnitude, int precision, bool alt)
    {
        string body = magnitude.ToString("F" + precision.ToString(Invariant), Invariant);

        return alt && precision == 0 ? body + "." : body;
    }

    private static string FormatExponential(double magnitude, int precision, bool alt, bool upper)
    {
        SplitExponential(magnitude, precision, out string mantissa, out int exponent);

        if (alt && precision == 0)
        {
            mantissa += ".";
        }

        return mantissa + ExponentSuffix(exponent, upper);
    }

    private static string FormatGeneral(double magnitude, int precision, bool alt, bool upper)
    {
        int p = precision == 0 ? 1 : precision;

        // X is the exponent after rounding to P significant digits.
        int x = 0;

        if (magnitude != 0)
        {
            SplitExponential(magnitude, p - 1, out _, out x);
        }

        if (p > x && x >= -4)
        {
            string body = magnitude.ToString("F" + (p - 1 - x).ToString(Invariant), Invariant);

            if (alt)
            {
                return body.Contains('.', StringComparison.Ordinal) ? body : body + ".";
            }

            return StripTrailingZeros(body);
        }

        SplitExponential(magnitude, p - 1, out string mantissa, out int exponent);

        if (alt)
        {
            if (!mantissa.Contains('.', StringComparison.Ordinal))
            {
                mantissa += ".";
            }
        }
        else
        {
            mantissa = StripTrailingZeros(mantissa);
        }

        return mantissa + ExponentSuffix(exponent, upper);
    }

    /// <summary>
    /// Formats <paramref name="magnitude"/> as .NET's <c>E</c> format
    /// (<c>1.500000E+001</c>) and splits it into the mantissa and the decimal
    /// exponent, so the exponent can be re-emitted the C way.
    /// </summary>
    private static void SplitExponential(double magnitude, int precision, out string mantissa, out int exponent)
    {
        string s = magnitude.ToString("E" + precision.ToString(Invariant), Invariant);
        int e = s.IndexOf('E', StringComparison.Ordinal);

        if (e < 0)
        {
            mantissa = s;
            exponent = 0;

            return;
        }

        mantissa = s[..e];
        exponent = 0;
        bool negative = false;

        for (int i = e + 1; i < s.Length; i++)
        {
            char c = s[i];

            if (c == '-')
            {
                negative = true;
            }
            else if (c >= '0' && c <= '9')
            {
                exponent = (exponent * 10) + (c - '0');
            }
        }

        if (negative)
        {
            exponent = -exponent;
        }
    }

    /// <summary>C's exponent: a sign and at least two digits.</summary>
    private static string ExponentSuffix(int exponent, bool upper)
    {
        string digits = Math.Abs(exponent).ToString(Invariant).PadLeft(2, '0');

        return (upper ? "E" : "e") + (exponent < 0 ? "-" : "+") + digits;
    }

    private static string StripTrailingZeros(string body)
    {
        if (!body.Contains('.', StringComparison.Ordinal))
        {
            return body;
        }

        return body.TrimEnd('0').TrimEnd('.');
    }

    /// <summary>
    /// Pads <paramref name="lead"/> + <paramref name="body"/> to the
    /// specifier's width: spaces on the right for <c>-</c>, zeros between the
    /// lead (sign, <c>0x</c>) and the body for <paramref name="zeroPad"/>,
    /// spaces on the left otherwise.
    /// </summary>
    private static void AppendPadded(
        StringBuilder text, in SlangAbortSpecifier specifier, string lead, string body, bool zeroPad)
    {
        int pad = specifier.Width - lead.Length - body.Length;

        if (pad <= 0)
        {
            text.Append(lead).Append(body);
        }
        else if ((specifier.Flags & SlangAbortFlags.Left) != 0)
        {
            text.Append(lead).Append(body).Append(' ', pad);
        }
        else if (zeroPad)
        {
            text.Append(lead).Append('0', pad).Append(body);
        }
        else
        {
            text.Append(' ', pad).Append(lead).Append(body);
        }
    }
}
