namespace Ahjo.Vulkan.Slang.Internal;

/// <summary>
/// One argument-consuming conversion specifier parsed out of a Slang abort
/// format. <c>%%</c> produces none.
/// </summary>
/// <param name="Start">Index of the <c>%</c> in the format string.</param>
/// <param name="Length">Length of the whole specifier, <c>%</c> included, in
/// format-string characters.</param>
/// <param name="Conversion">The conversion character (<c>d i u x X f F e E g G</c>).</param>
/// <param name="Kind">The scalar type each component is read as.</param>
/// <param name="Components">1, or 2–4 for a <c>vN</c> vector.</param>
/// <param name="Flags">The C flags present.</param>
/// <param name="Width">Minimum field width, or -1 when absent.</param>
/// <param name="Precision">Precision, or -1 when absent.</param>
internal readonly record struct SlangAbortSpecifier(
    int Start,
    int Length,
    char Conversion,
    SlangAbortArgumentKind Kind,
    int Components,
    SlangAbortFlags Flags,
    int Width,
    int Precision);

/// <summary>The C <c>printf</c> flag characters.</summary>
[Flags]
internal enum SlangAbortFlags
{
    None = 0,

    /// <summary><c>-</c>: left-justify.</summary>
    Left = 1,

    /// <summary><c>+</c>: always print a sign on signed conversions.</summary>
    Plus = 2,

    /// <summary>space: a space where a <c>+</c> would go.</summary>
    Space = 4,

    /// <summary><c>0</c>: pad with zeros after the sign.</summary>
    Zero = 8,

    /// <summary><c>#</c>: alternate form (<c>0x</c> prefix; keep the point and
    /// trailing zeros on floats).</summary>
    Alt = 16,
}
