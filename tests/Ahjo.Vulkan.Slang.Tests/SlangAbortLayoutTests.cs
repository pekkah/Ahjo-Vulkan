using System.Buffers.Binary;
using System.Text;

using Ahjo.Vulkan.Slang.Internal;

using Xunit;

namespace Ahjo.Vulkan.Slang.Tests;

/// <summary>
/// Pins <see cref="SlangAbortMessage"/>'s payload layout to the SPIR-V the
/// pinned Slang actually emits (spec Part C.5): each fixture compiles a real
/// <c>abort(…)</c> through the wrapper and compares the decoder's computed
/// layout with the <c>OpAbortKHR</c> message type's member offsets.
/// </summary>
/// <remarks>
/// <para>Driverless: compiling needs no GPU. The decoder agreeing with itself
/// would prove nothing, which is why the expected offsets come from
/// <see cref="SpirvDecorations.ReadAbortMessageLayout"/>, never from the
/// decoder.</para>
/// <para>What this cannot prove: that a driver writes the payload exactly as
/// the message type says. The Vulkan spec promises that ("no modifications"),
/// but no abort message has been read from a real driver (spec, Test
/// strategy).</para>
/// <para><b>On a Slang bump</b>, a failure here means the abort payload layout
/// changed: re-probe per spec Part C, do not edit the expected offsets.</para>
/// </remarks>
public sealed class SlangAbortLayoutTests(ITestOutputHelper output)
{
    /// <summary>
    /// The spec Part C evidence-table rows, each format spelled to match its
    /// arguments' widths. The table's mismatched spellings (<c>"h %f"</c> with
    /// a <c>half</c>) document that the payload carries no type information;
    /// they are not decoder fixtures.
    /// </summary>
    public static TheoryData<string, string> Fixtures => new()
    {
        { """abort("bad value: %u at %u", v, id.x)""", "bad value: %u at %u" },
        { """abort("signed %d", s)""", "signed %d" },
        { """abort("float %f", f)""", "float %f" },
        { """abort("u=%u d=%d f=%f x=%x", v, s, f, v)""", "u=%u d=%d f=%f x=%x" },
        { """abort("big %llu", uint64_t(v))""", "big %llu" },
        { """abort("ab %d %lld", s, int64_t(s))""", "ab %d %lld" },
        { """abort("d %lf", double(f))""", "d %lf" },
        { """abort("h %hf", half(f))""", "h %hf" },
        { """abort("s %hd", int16_t(s))""", "s %hd" },
        { """abort("c %hhu", uint8_t(v))""", "c %hhu" },
        { """abort("%hhu %hf %u", uint8_t(v), half(f), v)""", "%hhu %hf %u" },
        { """abort("%hd %hd %lf", int16_t(s), int16_t(s), double(f))""", "%hd %hd %lf" },
        { """abort("%hhu %llu", uint8_t(v), uint64_t(v))""", "%hhu %llu" },
        { """abort("%hhu %v3f", uint8_t(v), float3(f, f, f))""", "%hhu %v3f" },
        { """abort("v2=%u %u", uint2(v, v + 1))""", "v2=%u %u" },
        { """abort("%v2u", uint2(v, v))""", "%v2u" },
        { """abort("b %u", v == 3)""", "b %u" },
        { """abort("%08x %.3f %%", v, f)""", "%08x %.3f %%" },
        { """abort("Störung ✓ %u", v)""", "Störung ✓ %u" },
        { """abort("%llv2u", vector<uint64_t, 2>(uint64_t(v), uint64_t(v)))""", "%llv2u" },
        {
            """abort("a fairly long format string that exceeds sixty four bytes for sure %u", v)""",
            "a fairly long format string that exceeds sixty four bytes for sure %u"
        },
    };

    /// <summary>
    /// The rows that also check <see cref="SlangAbortMessage.Text"/>: each is
    /// filled with the values of the matching <c>SlangAbortMessageTests</c>
    /// case (1, 4 and 6) and must render the same text.
    /// </summary>
    private static readonly Dictionary<string, (ulong[] Bits, string Text)> TextChecks = new()
    {
        ["""abort("bad value: %u at %u", v, id.x)"""] = ([42, 0], "bad value: 42 at 0"),
        ["""abort("signed %d", s)"""] = ([unchecked((uint)-5)], "signed -5"),
        ["""abort("%08x %.3f %%", v, f)"""] =
            ([0xBEEF, (uint)BitConverter.SingleToInt32Bits(3.14159f)], "0000beef 3.142 %"),
    };

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Layout_MatchesEmittedSpirv(string abortCall, string format)
    {
        uint[] spirv = Compile(abortCall);
        (uint[] formatWords, var members) = SpirvDecorations.ReadAbortMessageLayout(spirv);

        // 1. String packing: UTF-8 + NUL, zero-padded to a multiple of 4.
        byte[] expectedFormat = PaddedFormat(format);
        byte[] actualFormat = WordsToBytes(formatWords);
        Check(expectedFormat.AsSpan().SequenceEqual(actualFormat), abortCall,
            $"member 0 holds [{Convert.ToHexString(actualFormat)}], expected [{Convert.ToHexString(expectedFormat)}].");

        // The SPIR-V's own layout, one entry per scalar component.
        var expected = new List<(SlangAbortArgumentKind Kind, int Offset, int Size)>();
        foreach ((uint offset, int width, bool isFloat, bool signed, int components) in members)
        {
            int size = width / 8;
            SlangAbortArgumentKind kind = KindOf(width, isFloat, signed, abortCall);

            for (int c = 0; c < components; c++)
            {
                expected.Add((kind, (int)offset + (c * size), size));
            }
        }

        output.WriteLine($"{abortCall}: format {formatWords.Length} words; args " +
            string.Join(", ", expected.ConvertAll(e => $"{e.Kind}@{e.Offset}")));

        // 2. The decoder's layout for that format equals the SPIR-V's.
        int lastEnd = actualFormat.Length;
        foreach ((uint offset, int width, _, _, int components) in members)
        {
            lastEnd = Math.Max(lastEnd, (int)offset + (width / 8 * components));
        }

        var payload = new byte[(lastEnd + 7) / 8 * 8];
        actualFormat.CopyTo(payload, 0);

        var specifiers = new List<SlangAbortSpecifier>();
        var arguments = new List<SlangAbortArgument>();
        bool laidOut = SlangAbortFormat.TryComputeLayout(payload, out _, specifiers, arguments, out string? reason);
        Check(laidOut, abortCall, $"the decoder refused the format: {reason}.");
        Check(arguments.Count == expected.Count, abortCall,
            $"the decoder reads {arguments.Count} arguments, the SPIR-V has {expected.Count}.");

        for (int k = 0; k < expected.Count; k++)
        {
            Check(arguments[k].Kind == expected[k].Kind && arguments[k].Offset == expected[k].Offset, abortCall,
                $"argument {k + 1} is {arguments[k].Kind}@{arguments[k].Offset} in the decoder, " +
                $"{expected[k].Kind}@{expected[k].Offset} in the SPIR-V.");
        }

        // 3. End to end: fill the SPIR-V-derived slots with k + 1, encoded in
        // the member's own type, and decode.
        var values = new ulong[expected.Count];
        for (int k = 0; k < expected.Count; k++)
        {
            values[k] = Encode(expected[k].Kind, k + 1);
            Write(payload, expected[k].Offset, expected[k].Size, values[k]);
        }

        Check(SlangAbortMessage.TryDecode(payload, out SlangAbortMessage? decoded), abortCall,
            "the filled payload did not decode: " + SlangAbortMessage.Describe(payload));

        for (int k = 0; k < expected.Count; k++)
        {
            Check(decoded!.Arguments[k].Bits == values[k], abortCall,
                $"argument {k + 1} decoded as 0x{decoded.Arguments[k].Bits:X}, wrote 0x{values[k]:X}.");
        }

        if (TextChecks.TryGetValue(abortCall, out (ulong[] Bits, string Text) check))
        {
            for (int k = 0; k < expected.Count; k++)
            {
                Write(payload, expected[k].Offset, expected[k].Size, check.Bits[k]);
            }

            string text = SlangAbortMessage.Describe(payload);
            Check(text == check.Text, abortCall, $"rendered \"{text}\", expected \"{check.Text}\".");
        }
    }

    [Fact]
    public void NoArgumentFormats_PackTheStringOnly()
    {
        foreach ((string abortCall, string format) in new[]
        {
            ("""abort("no args here")""", "no args here"),
            ("""abort("")""", string.Empty),
        })
        {
            (uint[] formatWords, var members) = SpirvDecorations.ReadAbortMessageLayout(Compile(abortCall));

            byte[] expectedFormat = PaddedFormat(format);
            byte[] actualFormat = WordsToBytes(formatWords);
            Check(expectedFormat.AsSpan().SequenceEqual(actualFormat), abortCall,
                $"member 0 holds [{Convert.ToHexString(actualFormat)}], expected [{Convert.ToHexString(expectedFormat)}].");
            Check(members.Count == 0, abortCall, $"the message type has {members.Count} argument members, expected none.");
        }
    }

    /// <summary>
    /// Slang refuses string arguments (spec C-E5), which is why the decoder
    /// does not support <c>%s</c>.
    /// </summary>
    [Fact]
    public void StringArgument_IsRejected()
    {
        SlangCompilationException ex = Assert.Throws<SlangCompilationException>(
            () => Compile("""abort("s %s", "hello")"""));

        Assert.Contains("E55211", ex.Message + ex.Diagnostics, StringComparison.Ordinal);
    }

    // ---- Helpers ----

    /// <summary>
    /// Compiles one abort call through the wrapper with the <c>spvAbort</c>
    /// capability declared, asserts a warning-free compile (spec C-E6), and
    /// returns the entry point's SPIR-V.
    /// </summary>
    private static uint[] Compile(string abortCall)
    {
        string source =
            "RWStructuredBuffer<uint> buf; RWStructuredBuffer<float> fbuf; " +
            "[shader(\"compute\")][numthreads(1,1,1)] " +
            "void main(uint3 id : SV_DispatchThreadID) { " +
            "uint v = buf[0]; int s = int(buf[1]); float f = fbuf[0]; " +
            "if (v == 42) " + abortCall + "; " +
            "buf[2] = v + 1; }";

        using SlangCompiler compiler = SlangCompiler.Create();
        using SlangSession session = compiler.CreateSession(new SlangSessionDescription
        {
            Capabilities = [Utf8Name.FromLiteral("spvAbort"u8)],
        });
        using SlangProgram program = session.Compile(new SlangCompileRequest
        {
            ModuleName = "abort",
            Source = source,
        });

        Check(string.IsNullOrEmpty(program.Warnings), abortCall,
            "the compile is expected to be warning-free with spvAbort declared, got: " + program.Warnings);

        return program.Spirv(0).ToArray();
    }

    private static void Check(bool condition, string abortCall, string detail)
    {
        Assert.True(condition,
            $"Slang {SlangPinnedVersion.Tag} changed the abort payload layout ({abortCall}): {detail} " +
            "Re-probe per spec Part C before bumping SlangVersion.");
    }

    private static byte[] PaddedFormat(string format)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(format);
        var padded = new byte[(utf8.Length + 1 + 3) / 4 * 4];
        utf8.CopyTo(padded, 0);

        return padded;
    }

    private static byte[] WordsToBytes(uint[] words)
    {
        var bytes = new byte[words.Length * 4];
        for (int i = 0; i < words.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), words[i]);
        }

        return bytes;
    }

    private static SlangAbortArgumentKind KindOf(int width, bool isFloat, bool signed, string abortCall)
    {
        SlangAbortArgumentKind? kind = (width, isFloat, signed) switch
        {
            (8, false, true) => SlangAbortArgumentKind.Int8,
            (8, false, false) => SlangAbortArgumentKind.UInt8,
            (16, false, true) => SlangAbortArgumentKind.Int16,
            (16, false, false) => SlangAbortArgumentKind.UInt16,
            (32, false, true) => SlangAbortArgumentKind.Int32,
            (32, false, false) => SlangAbortArgumentKind.UInt32,
            (64, false, true) => SlangAbortArgumentKind.Int64,
            (64, false, false) => SlangAbortArgumentKind.UInt64,
            (16, true, _) => SlangAbortArgumentKind.Float16,
            (32, true, _) => SlangAbortArgumentKind.Float32,
            (64, true, _) => SlangAbortArgumentKind.Float64,
            _ => null,
        };

        Check(kind is not null, abortCall, $"a member has an unmapped scalar shape (width {width}, float {isFloat}).");

        return kind!.Value;
    }

    /// <summary><paramref name="value"/> encoded as the bits of
    /// <paramref name="kind"/>.</summary>
    private static ulong Encode(SlangAbortArgumentKind kind, int value) => kind switch
    {
        SlangAbortArgumentKind.Float16 => BitConverter.HalfToUInt16Bits((Half)value),
        SlangAbortArgumentKind.Float32 => (uint)BitConverter.SingleToInt32Bits(value),
        SlangAbortArgumentKind.Float64 => (ulong)BitConverter.DoubleToInt64Bits(value),
        _ => (ulong)value,
    };

    private static void Write(byte[] payload, int offset, int size, ulong bits)
    {
        Span<byte> slot = payload.AsSpan(offset, size);

        switch (size)
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
}
