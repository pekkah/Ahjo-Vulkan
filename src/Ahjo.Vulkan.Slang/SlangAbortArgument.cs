namespace Ahjo.Vulkan.Slang;

/// <summary>
/// One scalar argument of a decoded Slang abort message. A vector specifier
/// (<c>%v3f</c>) yields one argument per component.
/// </summary>
/// <param name="Kind">The scalar type the format specifier gave it.</param>
/// <param name="Offset">Its byte offset in the payload.</param>
/// <param name="Bits">Its little-endian bytes, zero-extended into a
/// <see cref="ulong"/>. Raw, so a value whose specifier disagreed with the
/// shader's argument type can still be re-interpreted.</param>
/// <remarks>None of the accessors throws.</remarks>
public readonly record struct SlangAbortArgument(SlangAbortArgumentKind Kind, int Offset, ulong Bits)
{
    // 2^63, exactly representable as a double.
    private const double TwoPow63 = 9223372036854775808.0;

    /// <summary>
    /// The value as a signed 64-bit integer: sign-extended for the <c>Int*</c>
    /// kinds, reinterpreted unchecked for the <c>UInt*</c> kinds, and
    /// truncated toward zero for the float kinds (saturating; NaN is 0).
    /// </summary>
    public long AsInt64()
    {
        switch (Kind)
        {
            case SlangAbortArgumentKind.Int8:
                return unchecked((sbyte)Bits);

            case SlangAbortArgumentKind.Int16:
                return unchecked((short)Bits);

            case SlangAbortArgumentKind.Int32:
                return unchecked((int)Bits);

            case SlangAbortArgumentKind.Float16:
            case SlangAbortArgumentKind.Float32:
            case SlangAbortArgumentKind.Float64:
            {
                double d = AsDouble();

                if (double.IsNaN(d))
                {
                    return 0;
                }

                if (d >= TwoPow63)
                {
                    return long.MaxValue;
                }

                if (d <= -TwoPow63)
                {
                    return long.MinValue;
                }

                return (long)d;
            }

            default:
                return unchecked((long)Bits);
        }
    }

    /// <summary>
    /// The value as an unsigned 64-bit integer: <see cref="Bits"/> for the
    /// integer kinds, and <see cref="AsInt64"/> reinterpreted unchecked for the
    /// float kinds.
    /// </summary>
    public ulong AsUInt64() => Kind switch
    {
        SlangAbortArgumentKind.Float16 or SlangAbortArgumentKind.Float32 or SlangAbortArgumentKind.Float64 =>
            unchecked((ulong)AsInt64()),
        _ => Bits,
    };

    /// <summary>
    /// The value as a double: the float kinds widened from their IEEE bits,
    /// the integer kinds converted from their value.
    /// </summary>
    public double AsDouble() => Kind switch
    {
        SlangAbortArgumentKind.Float16 => (double)BitConverter.UInt16BitsToHalf(unchecked((ushort)Bits)),
        SlangAbortArgumentKind.Float32 => BitConverter.Int32BitsToSingle(unchecked((int)Bits)),
        SlangAbortArgumentKind.Float64 => BitConverter.Int64BitsToDouble(unchecked((long)Bits)),
        SlangAbortArgumentKind.Int8 or SlangAbortArgumentKind.Int16
            or SlangAbortArgumentKind.Int32 or SlangAbortArgumentKind.Int64 => AsInt64(),
        _ => Bits,
    };
}
