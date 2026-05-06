using System.Buffers.Binary;
using System.IO.Compression;

namespace Ahjo.Vulkan.Utilities;

/// <summary>
/// Minimal RGBA8 PNG writer. No third-party dependency — uses
/// <see cref="ZLibStream"/> from <c>System.IO.Compression</c> for the
/// <c>IDAT</c> deflate payload and an inline CRC-32 (PNG / IEEE 802.3
/// polynomial 0xEDB88320) for the per-chunk checksum. Suitable for
/// screenshot dumps; not a general-purpose encoder (no palette / no
/// interlace / no tRNS handling).
/// </summary>
public static class PngWriter
{
    /// <summary>
    /// Writes <paramref name="rgba"/> to <paramref name="path"/> as a
    /// PNG of size <paramref name="width"/> × <paramref name="height"/>.
    /// <paramref name="rgba"/> must be tightly packed RGBA8 (4 bytes per
    /// pixel, no row padding).
    /// </summary>
    public static void Write(string path, ReadOnlySpan<byte> rgba, int width, int height)
    {
        ValidateInputs(rgba, width, height);
        using var fs = File.Create(path);
        Write(fs, rgba, width, height);
    }

    /// <summary>Stream-targeting overload for in-memory or pipe scenarios.</summary>
    public static void Write(Stream destination, ReadOnlySpan<byte> rgba, int width, int height)
    {
        ValidateInputs(rgba, width, height);

        // 1. Magic.
        destination.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        // 2. IHDR — 13 bytes payload.
        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr[0..4], width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr[4..8], height);
        ihdr[8]  = 8;     // bit depth
        ihdr[9]  = 6;     // color type: truecolor + alpha
        ihdr[10] = 0;     // compression: deflate
        ihdr[11] = 0;     // filter: none
        ihdr[12] = 0;     // interlace: none
        WriteChunk(destination, "IHDR"u8, ihdr);

        // 3. IDAT — filtered scanlines, deflated. Filter byte 0 (None)
        //    per scanline. Pack into a single buffer up-front so the
        //    DeflateStream sees one continuous stream.
        int filteredLength = (width * 4 + 1) * height;
        byte[] filtered = new byte[filteredLength];
        int rowStride = width * 4;
        for (int y = 0; y < height; y++)
        {
            int dst = y * (rowStride + 1);
            filtered[dst] = 0; // filter type "None"
            rgba.Slice(y * rowStride, rowStride).CopyTo(filtered.AsSpan(dst + 1));
        }

        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Fastest, leaveOpen: true))
            z.Write(filtered, 0, filtered.Length);
        WriteChunk(destination, "IDAT"u8, ms.GetBuffer().AsSpan(0, (int)ms.Length));

        // 4. IEND — empty payload.
        WriteChunk(destination, "IEND"u8, ReadOnlySpan<byte>.Empty);
    }

    private static void ValidateInputs(ReadOnlySpan<byte> rgba, int width, int height)
    {
        if (width  <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        int expected = checked(width * height * 4);
        if (rgba.Length != expected)
            throw new ArgumentException(
                $"rgba.Length ({rgba.Length}) does not match width*height*4 ({expected}).",
                nameof(rgba));
    }

    private static void WriteChunk(Stream s, ReadOnlySpan<byte> typeUtf8, ReadOnlySpan<byte> payload)
    {
        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(header[0..4], (uint)payload.Length);
        typeUtf8.CopyTo(header[4..8]);
        s.Write(header);
        s.Write(payload);

        // CRC covers chunk type + payload.
        uint crc = 0xFFFFFFFFu;
        crc = Crc32.Append(crc, typeUtf8);
        crc = Crc32.Append(crc, payload);
        crc ^= 0xFFFFFFFFu;
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        s.Write(crcBytes);
    }

    /// <summary>
    /// CRC-32 over the IEEE 802.3 polynomial reflected
    /// (<c>0xEDB88320</c>) — what PNG specifies for chunk CRCs. Inlined
    /// to keep <see cref="Ahjo.Vulkan.Utilities"/> dependency-free.
    /// </summary>
    private static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        public static uint Append(uint crc, ReadOnlySpan<byte> data)
        {
            for (int i = 0; i < data.Length; i++)
                crc = Table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            return crc;
        }

        private static uint[] BuildTable()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[n] = c;
            }
            return t;
        }
    }
}
