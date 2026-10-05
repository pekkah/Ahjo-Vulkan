using System.Buffers.Binary;
using System.Text;

using Xunit;

namespace Ahjo.Vulkan.Slang.Tests;

/// <summary>
/// Synthesizes Slang abort payloads for the decoder tests.
/// </summary>
/// <remarks>
/// Every argument is written at the offset the test gives — spelled out by hand
/// in each test from the spec Part C evidence table — so this helper never
/// re-derives the layout the decoder computes.
/// </remarks>
internal static class AbortPayloads
{
    /// <summary>
    /// The UTF-8 format, a NUL and zero padding to a multiple of 4, then each
    /// argument's little-endian bytes at its explicit offset.
    /// </summary>
    public static byte[] Build(string format, params (SlangAbortArgumentKind Kind, ulong Bits, int Offset)[] args)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(format);
        int length = (utf8.Length + 1 + 3) / 4 * 4;

        foreach ((SlangAbortArgumentKind kind, _, int offset) in args)
        {
            length = Math.Max(length, offset + SizeOf(kind));
        }

        var payload = new byte[length];
        utf8.CopyTo(payload, 0);

        foreach ((SlangAbortArgumentKind kind, ulong bits, int offset) in args)
        {
            Span<byte> slot = payload.AsSpan(offset, SizeOf(kind));

            switch (slot.Length)
            {
                case 1:
                    slot[0] = (byte)bits;

                    break;

                case 2:
                    BinaryPrimitives.WriteUInt16LittleEndian(slot, (ushort)bits);

                    break;

                case 4:
                    BinaryPrimitives.WriteUInt32LittleEndian(slot, (uint)bits);

                    break;

                default:
                    BinaryPrimitives.WriteUInt64LittleEndian(slot, bits);

                    break;
            }
        }

        return payload;
    }

    public static int SizeOf(SlangAbortArgumentKind kind) => kind switch
    {
        SlangAbortArgumentKind.Int8 or SlangAbortArgumentKind.UInt8 => 1,
        SlangAbortArgumentKind.Int16 or SlangAbortArgumentKind.UInt16 or SlangAbortArgumentKind.Float16 => 2,
        SlangAbortArgumentKind.Int32 or SlangAbortArgumentKind.UInt32 or SlangAbortArgumentKind.Float32 => 4,
        _ => 8,
    };

    public static ulong Bits(float value) => (uint)BitConverter.SingleToInt32Bits(value);

    public static ulong Bits(double value) => (ulong)BitConverter.DoubleToInt64Bits(value);

    public static ulong Bits(Half value) => BitConverter.HalfToUInt16Bits(value);

    public static ulong Bits(int value) => (uint)value;

    public static ulong Bits(long value) => (ulong)value;
}

/// <summary>
/// Driverless coverage of <see cref="SlangAbortMessage"/> (#245, spec Part C):
/// every supported conversion and length, flags, width and precision, vectors,
/// every failure reason, and the never-throw guarantee.
/// </summary>
/// <remarks>No <c>TestGate</c>: nothing here compiles a shader or touches a
/// driver. The layout itself is pinned against real compiler output by
/// <see cref="SlangAbortLayoutTests"/>.</remarks>
public sealed class SlangAbortMessageTests
{
    private const SlangAbortArgumentKind I8 = SlangAbortArgumentKind.Int8;
    private const SlangAbortArgumentKind U8 = SlangAbortArgumentKind.UInt8;
    private const SlangAbortArgumentKind I16 = SlangAbortArgumentKind.Int16;
    private const SlangAbortArgumentKind I32 = SlangAbortArgumentKind.Int32;
    private const SlangAbortArgumentKind U32 = SlangAbortArgumentKind.UInt32;
    private const SlangAbortArgumentKind I64 = SlangAbortArgumentKind.Int64;
    private const SlangAbortArgumentKind U64 = SlangAbortArgumentKind.UInt64;
    private const SlangAbortArgumentKind F16 = SlangAbortArgumentKind.Float16;
    private const SlangAbortArgumentKind F32 = SlangAbortArgumentKind.Float32;
    private const SlangAbortArgumentKind F64 = SlangAbortArgumentKind.Float64;

    // ---- The successful-decode payloads (cases 1–16), shared with the
    // ---- every-truncation property test.

    private static byte[] Case01() => AbortPayloads.Build("bad value: %u at %u", (U32, 42, 20), (U32, 0, 24));

    private static byte[] Case02() => AbortPayloads.Build("no args here");

    private static byte[] Case03() => AbortPayloads.Build("");

    private static byte[] Case04() => AbortPayloads.Build("signed %d", (I32, AbortPayloads.Bits(-5), 12));

    private static byte[] Case05() => AbortPayloads.Build("float %f", (F32, AbortPayloads.Bits(1.5f), 12));

    private static byte[] Case06() =>
        AbortPayloads.Build("%08x %.3f %%", (U32, 0xBEEF, 16), (F32, AbortPayloads.Bits(3.14159f), 20));

    private static byte[] Case07() => AbortPayloads.Build("big %llu", (U64, ulong.MaxValue, 16));

    private static byte[] Case08() =>
        AbortPayloads.Build("ab %d %lld", (I32, AbortPayloads.Bits(-1), 12), (I64, AbortPayloads.Bits(long.MinValue), 16));

    private static byte[] Case09() =>
        AbortPayloads.Build("%hhu %hf %u", (U8, 200, 12), (F16, AbortPayloads.Bits((Half)0.5), 14), (U32, 7, 16));

    private static byte[] Case10() =>
        AbortPayloads.Build("%hd %hd %lf",
            (I16, unchecked((ushort)(short)-2), 12), (I16, 3, 14), (F64, AbortPayloads.Bits(2.25), 16));

    private static byte[] Case11() =>
        AbortPayloads.Build("%hhu %v3f",
            (U8, 1, 12),
            (F32, AbortPayloads.Bits(1f), 16), (F32, AbortPayloads.Bits(2f), 20), (F32, AbortPayloads.Bits(3f), 24));

    private static byte[] Case12() => AbortPayloads.Build("%v2u", (U32, 4, 8), (U32, 5, 12));

    private static byte[] Case12b() => AbortPayloads.Build("%llv2u", (U64, 1, 8), (U64, 2, 16));

    private static byte[] Case12c() => AbortPayloads.Build("%v2llu", (U64, 1, 8), (U64, 2, 16));

    private static byte[] Case13() =>
        AbortPayloads.Build("%#x %#X %+d % d %-4d|",
            (U32, 0xFF, 24), (U32, 0xFF, 28), (I32, 5, 32), (I32, 5, 36), (I32, 7, 40));

    private static byte[] Case14() =>
        AbortPayloads.Build("%e %E %g %G",
            (F32, AbortPayloads.Bits(15f), 12),
            (F32, AbortPayloads.Bits(1e-5f), 16),
            (F32, AbortPayloads.Bits(0.0001f), 20),
            (F32, AbortPayloads.Bits(1e10f), 24));

    private static byte[] Case15() =>
        AbortPayloads.Build("%f %F", (F32, AbortPayloads.Bits(float.NaN), 8), (F32, AbortPayloads.Bits(float.PositiveInfinity), 12));

    private static byte[] Case16() => AbortPayloads.Build("Störung ✓ %u", (U32, 1, 16));

    private static IEnumerable<byte[]> SuccessPayloads()
    {
        yield return Case01();
        yield return Case02();
        yield return Case03();
        yield return Case04();
        yield return Case05();
        yield return Case06();
        yield return Case07();
        yield return Case08();
        yield return Case09();
        yield return Case10();
        yield return Case11();
        yield return Case12();
        yield return Case12b();
        yield return Case12c();
        yield return Case13();
        yield return Case14();
        yield return Case15();
        yield return Case16();
    }

    private static SlangAbortMessage Decode(byte[] payload)
    {
        Assert.True(SlangAbortMessage.TryDecode(payload, out SlangAbortMessage? message),
            "Expected a decode, got: " + SlangAbortMessage.Describe(payload));
        Assert.Equal(message.Text, SlangAbortMessage.Describe(payload));
        Assert.Equal(message.Text, message.ToString());

        return message;
    }

    private static void AssertArguments(SlangAbortMessage message, params (SlangAbortArgumentKind Kind, int Offset)[] expected)
    {
        Assert.Equal(expected.Length, message.Arguments.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Kind, message.Arguments[i].Kind);
            Assert.Equal(expected[i].Offset, message.Arguments[i].Offset);
        }
    }

    // ---- Successful decodes ----

    [Fact]
    public void Decode_TwoUInts()
    {
        SlangAbortMessage m = Decode(Case01());

        Assert.Equal("bad value: %u at %u", m.Format);
        Assert.Equal("bad value: 42 at 0", m.Text);
        AssertArguments(m, (U32, 20), (U32, 24));
        Assert.Equal(42UL, m.Arguments[0].Bits);
    }

    [Fact]
    public void Decode_NoArguments()
    {
        SlangAbortMessage m = Decode(Case02());

        Assert.Equal("no args here", m.Text);
        Assert.Empty(m.Arguments);
    }

    [Fact]
    public void Decode_EmptyFormat()
    {
        SlangAbortMessage m = Decode(Case03());

        Assert.Equal(string.Empty, m.Format);
        Assert.Equal(string.Empty, m.Text);
        Assert.Empty(m.Arguments);
    }

    [Fact]
    public void Decode_SignedInt()
    {
        SlangAbortMessage m = Decode(Case04());

        Assert.Equal("signed -5", m.Text);
        AssertArguments(m, (I32, 12));
    }

    [Fact]
    public void Decode_Float()
    {
        SlangAbortMessage m = Decode(Case05());

        Assert.Equal("float 1.500000", m.Text);
        AssertArguments(m, (F32, 12));
    }

    [Fact]
    public void Decode_ZeroPadHex_Precision_PercentLiteral()
    {
        SlangAbortMessage m = Decode(Case06());

        Assert.Equal("0000beef 3.142 %", m.Text);
        AssertArguments(m, (U32, 16), (F32, 20));
    }

    [Fact]
    public void Decode_UInt64_AlignedTo8()
    {
        SlangAbortMessage m = Decode(Case07());

        Assert.Equal("big 18446744073709551615", m.Text);
        AssertArguments(m, (U64, 16));
    }

    [Fact]
    public void Decode_Int32ThenInt64()
    {
        SlangAbortMessage m = Decode(Case08());

        Assert.Equal("ab -1 -9223372036854775808", m.Text);
        AssertArguments(m, (I32, 12), (I64, 16));
    }

    [Fact]
    public void Decode_UInt8_Half_UInt32()
    {
        SlangAbortMessage m = Decode(Case09());

        Assert.Equal("200 0.500000 7", m.Text);
        AssertArguments(m, (U8, 12), (F16, 14), (U32, 16));
    }

    [Fact]
    public void Decode_Int16_Int16_Double()
    {
        SlangAbortMessage m = Decode(Case10());

        Assert.Equal("-2 3 2.250000", m.Text);
        AssertArguments(m, (I16, 12), (I16, 14), (F64, 16));
    }

    [Fact]
    public void Decode_UInt8_ThenFloat3()
    {
        SlangAbortMessage m = Decode(Case11());

        Assert.Equal("1 1.000000, 2.000000, 3.000000", m.Text);
        AssertArguments(m, (U8, 12), (F32, 16), (F32, 20), (F32, 24));
    }

    [Fact]
    public void Decode_UInt2()
    {
        SlangAbortMessage m = Decode(Case12());

        Assert.Equal("4, 5", m.Text);
        AssertArguments(m, (U32, 8), (U32, 12));
    }

    [Fact]
    public void Decode_LengthBeforeVector()
    {
        SlangAbortMessage m = Decode(Case12b());

        Assert.Equal("1, 2", m.Text);
        AssertArguments(m, (U64, 8), (U64, 16));
    }

    [Fact]
    public void Decode_VectorBeforeLength()
    {
        SlangAbortMessage m = Decode(Case12c());

        Assert.Equal("1, 2", m.Text);
        AssertArguments(m, (U64, 8), (U64, 16));
    }

    [Fact]
    public void Decode_Flags()
    {
        SlangAbortMessage m = Decode(Case13());

        Assert.Equal("0xff 0XFF +5  5 7   |", m.Text);
        AssertArguments(m, (U32, 24), (U32, 28), (I32, 32), (I32, 36), (I32, 40));
    }

    [Fact]
    public void Decode_ExponentAndGeneral()
    {
        SlangAbortMessage m = Decode(Case14());

        Assert.Equal("1.500000e+01 1.000000E-05 0.0001 1E+10", m.Text);
        AssertArguments(m, (F32, 12), (F32, 16), (F32, 20), (F32, 24));
    }

    [Fact]
    public void Decode_NanAndInfinity()
    {
        SlangAbortMessage m = Decode(Case15());

        Assert.Equal("nan INF", m.Text);
        AssertArguments(m, (F32, 8), (F32, 12));
    }

    [Fact]
    public void Decode_MultiByteUtf8Format()
    {
        SlangAbortMessage m = Decode(Case16());

        Assert.Equal("Störung ✓ 1", m.Text);
        AssertArguments(m, (U32, 16));
    }

    [Fact]
    public void ExtraTrailingBytes_Ignored()
    {
        byte[] exact = AbortPayloads.Build("%u", (U32, 7, 4));
        var payload = new byte[exact.Length + 4];
        exact.CopyTo(payload, 0);
        payload[^1] = 0xEE;

        SlangAbortMessage m = Decode(payload);

        Assert.Equal("7", m.Text);
        AssertArguments(m, (U32, 4));
    }

    [Fact]
    public void InvalidUtf8InFormat_ReplacedNotThrown()
    {
        byte[] payload = [0xFF, (byte)'a', 0];

        Assert.True(SlangAbortMessage.TryDecode(payload, out SlangAbortMessage? m));
        Assert.Equal("�a", m.Format);
        Assert.Equal("�a", m.Text);
    }

    [Fact]
    public void AsAccessors()
    {
        var int8 = new SlangAbortArgument(I8, 0, 0xFF);
        Assert.Equal(-1L, int8.AsInt64());
        Assert.Equal(0xFFUL, int8.AsUInt64());
        Assert.Equal(-1.0, int8.AsDouble());

        var uint8 = new SlangAbortArgument(U8, 0, 0xFF);
        Assert.Equal(255L, uint8.AsInt64());
        Assert.Equal(255UL, uint8.AsUInt64());
        Assert.Equal(255.0, uint8.AsDouble());

        var int16 = new SlangAbortArgument(I16, 0, 0xFFFE);
        Assert.Equal(-2L, int16.AsInt64());
        Assert.Equal(-2.0, int16.AsDouble());

        var uint16 = new SlangAbortArgument(SlangAbortArgumentKind.UInt16, 0, 0xFFFE);
        Assert.Equal(65534L, uint16.AsInt64());

        var int32 = new SlangAbortArgument(I32, 0, 0xFFFF_FFFB);
        Assert.Equal(-5L, int32.AsInt64());
        Assert.Equal(0xFFFF_FFFBUL, int32.AsUInt64());

        var uint32 = new SlangAbortArgument(U32, 0, 0xFFFF_FFFF);
        Assert.Equal(4294967295L, uint32.AsInt64());

        var int64 = new SlangAbortArgument(I64, 0, AbortPayloads.Bits(long.MinValue));
        Assert.Equal(long.MinValue, int64.AsInt64());
        Assert.Equal((double)long.MinValue, int64.AsDouble());

        var uint64 = new SlangAbortArgument(U64, 0, ulong.MaxValue);
        Assert.Equal(-1L, uint64.AsInt64());
        Assert.Equal(ulong.MaxValue, uint64.AsUInt64());
        Assert.Equal((double)ulong.MaxValue, uint64.AsDouble());

        var half = new SlangAbortArgument(F16, 0, AbortPayloads.Bits((Half)(-2.5)));
        Assert.Equal(-2.5, half.AsDouble());
        Assert.Equal(-2L, half.AsInt64());
        Assert.Equal(unchecked((ulong)-2L), half.AsUInt64());

        var single = new SlangAbortArgument(F32, 0, AbortPayloads.Bits(1e30f));
        Assert.Equal((double)1e30f, single.AsDouble());
        Assert.Equal(long.MaxValue, single.AsInt64());

        var nan = new SlangAbortArgument(F64, 0, AbortPayloads.Bits(double.NaN));
        Assert.True(double.IsNaN(nan.AsDouble()));
        Assert.Equal(0L, nan.AsInt64());

        var negativeHuge = new SlangAbortArgument(F64, 0, AbortPayloads.Bits(-1e300));
        Assert.Equal(long.MinValue, negativeHuge.AsInt64());
    }

    // ---- Failures ----

    private static void AssertFails(byte[] payload, string expectedDescribe)
    {
        Assert.False(SlangAbortMessage.TryDecode(payload, out SlangAbortMessage? message));
        Assert.Null(message);
        Assert.Equal(expectedDescribe, SlangAbortMessage.Describe(payload));
    }

    [Fact]
    public void Fail_NoNul_HexDump()
    {
        AssertFails([0x01, 0x02, 0x03],
            "<shader abort payload, 3 bytes, not a Slang abort message: 01 02 03>");
    }

    [Fact]
    public void Fail_EmptyPayload()
    {
        AssertFails([], "<shader abort payload, 0 bytes, not a Slang abort message>");
    }

    [Fact]
    public void Fail_LongNoNul_HexTruncatedAt64()
    {
        var payload = new byte[70];
        for (int i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)(i + 1);
        }

        var hex = new StringBuilder();
        for (int i = 0; i < 64; i++)
        {
            if (i > 0)
            {
                hex.Append(' ');
            }

            hex.Append((i + 1).ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }

        AssertFails(payload, $"<shader abort payload, 70 bytes, not a Slang abort message: {hex} …>");
    }

    [Fact]
    public void Fail_StringConversion()
    {
        AssertFails(AbortPayloads.Build("s %s"),
            "s %s <abort arguments not decoded: unsupported conversion 's' at format index 2; 8 payload bytes>");
    }

    [Fact]
    public void Fail_TooFewArguments()
    {
        AssertFails(AbortPayloads.Build("%u %u", (U32, 9, 8)),
            "%u %u <abort arguments not decoded: argument 2 needs 4 bytes at offset 12, payload has 12; 12 payload bytes>");
    }

    [Fact]
    public void Fail_LonePercentAtEnd()
    {
        AssertFails(AbortPayloads.Build("%"),
            "% <abort arguments not decoded: incomplete specifier at end of format; 4 payload bytes>");
    }

    [Fact]
    public void Fail_StarWidth()
    {
        AssertFails(AbortPayloads.Build("%*d"),
            "%*d <abort arguments not decoded: '*' width or precision at format index 0; 4 payload bytes>");
    }

    [Fact]
    public void Fail_VectorSize()
    {
        AssertFails(AbortPayloads.Build("%v5f"),
            "%v5f <abort arguments not decoded: invalid vector size at format index 0; 8 payload bytes>");
    }

    [Fact]
    public void Fail_LengthModifierL()
    {
        AssertFails(AbortPayloads.Build("%Lf"),
            "%Lf <abort arguments not decoded: unsupported length modifier 'L' at format index 0; 4 payload bytes>");
    }

    [Fact]
    public void Fail_HalfHalfFloat()
    {
        AssertFails(AbortPayloads.Build("%hhf"),
            "%hhf <abort arguments not decoded: unsupported conversion 'f' with length 'hh' at format index 0; 8 payload bytes>");
    }

    [Fact]
    public void Fail_FormatEndsPastPayload()
    {
        // "abcde %u" + NUL is 9 bytes; padded, the arguments would start at 12.
        byte[] payload = [.. Encoding.UTF8.GetBytes("abcde %u"), 0];
        Assert.Equal(9, payload.Length);

        AssertFails(payload,
            "abcde %u <abort arguments not decoded: format string ends past the payload; 9 payload bytes>");
    }

    [Fact]
    public void NoSpecifiers_ShortPadding_StillDecodes()
    {
        byte[] payload = [.. Encoding.UTF8.GetBytes("abcde"), 0];
        Assert.Equal(6, payload.Length);

        Assert.True(SlangAbortMessage.TryDecode(payload, out SlangAbortMessage? m));
        Assert.Equal("abcde", m.Text);
    }

    // ---- Property: never throws ----

    [Fact]
    public void NeverThrows_RandomPayloads()
    {
        var random = new Random(244245);
        const string Alphabet = "%%%%diuxXfFeEgGscpnaoLzjt0123456789hhllvv.*#-+ abc";

        for (int i = 0; i < 10_000; i++)
        {
            // Random bytes.
            var bytes = new byte[random.Next(0, 97)];
            random.NextBytes(bytes);
            AssertTotal(bytes);

            // A random format of specifier-ish characters, a NUL, then random
            // argument bytes.
            var format = new StringBuilder();
            int formatLength = random.Next(0, 24);
            for (int c = 0; c < formatLength; c++)
            {
                format.Append(Alphabet[random.Next(Alphabet.Length)]);
            }

            byte[] utf8 = Encoding.UTF8.GetBytes(format.ToString());
            var payload = new byte[utf8.Length + 1 + random.Next(0, 72)];
            utf8.CopyTo(payload, 0);
            random.NextBytes(payload.AsSpan(utf8.Length + 1));
            AssertTotal(payload);
        }
    }

    [Fact]
    public void NeverThrows_EveryTruncation()
    {
        foreach (byte[] payload in SuccessPayloads())
        {
            for (int length = 0; length <= payload.Length; length++)
            {
                AssertTotal(payload.AsSpan(0, length).ToArray());
            }
        }
    }

    [Fact]
    public void NeverThrows_HugeWidthAndPrecision()
    {
        byte[] payload = AbortPayloads.Build("%999999999999d %.999999999999f %-99999999999999x",
            (I32, 1, 52), (F32, AbortPayloads.Bits(1f), 56), (U32, 2, 60));

        AssertTotal(payload);
        Assert.True(SlangAbortMessage.TryDecode(payload, out SlangAbortMessage? m));
        Assert.True(m.Text.Length < 4096, $"Rendered {m.Text.Length} characters.");
    }

    /// <summary>
    /// Neither entry point throws, and <see cref="SlangAbortMessage.Describe"/>
    /// returns non-null text that is non-empty whenever decoding failed. A
    /// successful decode of an empty format (<c>abort("")</c>, case 3) renders
    /// as the empty <see cref="SlangAbortMessage.Text"/>, by definition.
    /// </summary>
    private static void AssertTotal(byte[] payload)
    {
        try
        {
            bool decoded = SlangAbortMessage.TryDecode(payload, out SlangAbortMessage? message);
            string described = SlangAbortMessage.Describe(payload);
            Assert.NotNull(described);

            if (decoded)
            {
                Assert.Equal(message!.Text, described);
            }
            else
            {
                Assert.NotEqual(string.Empty, described);
            }
        }
        catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
        {
            Assert.Fail($"Threw {ex.GetType().Name} for payload [{Convert.ToHexString(payload)}]: {ex.Message}");
        }
    }
}
