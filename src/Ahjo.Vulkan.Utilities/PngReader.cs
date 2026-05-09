using System.Buffers.Binary;
using System.IO.Compression;

namespace Ahjo.Vulkan.Utilities;

/// <summary>
/// Minimal 8-bit PNG decoder targeting <see cref="PngWriter"/>'s output
/// shape and the typical asset-pipeline output (color type 2 / RGB or
/// 6 / RGBA, 8 bits per channel, no interlace, no palette). Returns
/// tightly-packed RGBA8 — RGB inputs are padded with opaque alpha. Uses
/// <see cref="ZLibStream"/> for the IDAT deflate; no third-party
/// dependency. Suitable for samples that need to load a single PNG;
/// not a complete reference decoder (no palette / no interlace / no
/// 16-bit / no tRNS).
/// </summary>
public static class PngReader
{
    /// <summary>
    /// Decodes <paramref name="path"/> into a fresh RGBA8 byte array sized
    /// <c>4 * width * height</c>.
    /// </summary>
    public static byte[] LoadRgba8(string path, out int width, out int height)
    {
        using var fs = File.OpenRead(path);
        return LoadRgba8(fs, out width, out height);
    }

    /// <summary>Stream-targeting overload.</summary>
    public static byte[] LoadRgba8(Stream source, out int width, out int height)
    {
        ArgumentNullException.ThrowIfNull(source);

        Span<byte> magic = stackalloc byte[8];
        ReadExact(source, magic);
        if (!magic.SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            throw new InvalidDataException("Not a PNG (bad magic).");

        int  imgWidth = 0, imgHeight = 0, bitDepth = 0, colorType = 0;
        bool sawIhdr = false, sawIend = false;
        var  idat    = new MemoryStream();

        Span<byte> chunkHeader = stackalloc byte[8];
        Span<byte> ihdr        = stackalloc byte[13];
        Span<byte> crc         = stackalloc byte[4];

        while (!sawIend)
        {
            ReadExact(source, chunkHeader);
            uint length = BinaryPrimitives.ReadUInt32BigEndian(chunkHeader[0..4]);
            ReadOnlySpan<byte> type = chunkHeader[4..8];

            if (type.SequenceEqual("IHDR"u8))
            {
                if (sawIhdr) throw new InvalidDataException("Duplicate IHDR.");
                if (length != 13) throw new InvalidDataException("IHDR length must be 13.");
                ReadExact(source, ihdr);
                imgWidth   = BinaryPrimitives.ReadInt32BigEndian(ihdr[0..4]);
                imgHeight  = BinaryPrimitives.ReadInt32BigEndian(ihdr[4..8]);
                bitDepth   = ihdr[8];
                colorType  = ihdr[9];
                int compression = ihdr[10];
                int filter      = ihdr[11];
                int interlace   = ihdr[12];

                if (imgWidth <= 0 || imgHeight <= 0)
                    throw new InvalidDataException($"Invalid PNG dimensions {imgWidth}×{imgHeight}.");
                if (bitDepth != 8)
                    throw new NotSupportedException($"PngReader only handles 8-bit channels (got {bitDepth}).");
                if (colorType != 2 && colorType != 6)
                    throw new NotSupportedException(
                        $"PngReader only handles color types 2 (RGB) and 6 (RGBA); got {colorType}.");
                if (compression != 0)
                    throw new InvalidDataException($"Unsupported compression {compression} (PNG mandates 0).");
                if (filter != 0)
                    throw new InvalidDataException($"Unsupported filter method {filter} (PNG mandates 0).");
                if (interlace != 0)
                    throw new NotSupportedException("PngReader does not support Adam7 interlace.");

                sawIhdr = true;
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                if (!sawIhdr) throw new InvalidDataException("IDAT seen before IHDR.");
                CopyExact(source, idat, (int)length);
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                if (length != 0) throw new InvalidDataException("IEND must be empty.");
                sawIend = true;
            }
            else
            {
                // Skip ancillary chunks (gAMA, sRGB, pHYs, tEXt, …) — the
                // demo decoder doesn't need them. Reading + discarding
                // keeps the stream cursor aligned for the next chunk.
                Skip(source, (int)length);
            }
            // Discard the per-chunk CRC. Robust decoders verify; this one
            // trusts the source (caller's local file or zip extraction)
            // and skips the bookkeeping.
            ReadExact(source, crc);
        }

        if (idat.Length == 0)
            throw new InvalidDataException("PNG had no IDAT data.");

        int channels  = colorType == 6 ? 4 : 3;
        int rowStride = imgWidth * channels;
        int filtered  = (rowStride + 1) * imgHeight;
        byte[] raw    = new byte[filtered];

        idat.Position = 0;
        using (var z = new ZLibStream(idat, CompressionMode.Decompress, leaveOpen: true))
        {
            int read = 0;
            while (read < filtered)
            {
                int n = z.Read(raw, read, filtered - read);
                if (n == 0) throw new InvalidDataException(
                    $"Decompressed IDAT short: got {read}, expected {filtered}.");
                read += n;
            }
        }

        // Defilter scanlines in place + pack into RGBA8. The PNG filter
        // chain references the previous (already-defiltered) scanline,
        // which is why we walk top-to-bottom and reuse `raw` as both
        // input and scratch.
        int outBpp = 4;
        byte[] rgba = new byte[imgWidth * imgHeight * outBpp];
        Span<byte> prev = stackalloc byte[rowStride];
        prev.Clear();
        Span<byte> curr = new byte[rowStride];

        int srcRow = 0;
        for (int y = 0; y < imgHeight; y++)
        {
            byte filterByte = raw[srcRow];
            ReadOnlySpan<byte> rowData = raw.AsSpan(srcRow + 1, rowStride);
            rowData.CopyTo(curr);

            ApplyFilter(filterByte, curr, prev, channels);

            int dstBase = y * imgWidth * outBpp;
            for (int x = 0; x < imgWidth; x++)
            {
                int s = x * channels;
                int d = dstBase + x * outBpp;
                rgba[d + 0] = curr[s + 0];
                rgba[d + 1] = curr[s + 1];
                rgba[d + 2] = curr[s + 2];
                rgba[d + 3] = channels == 4 ? curr[s + 3] : (byte)255;
            }

            curr.CopyTo(prev);
            srcRow += rowStride + 1;
        }

        width  = imgWidth;
        height = imgHeight;
        return rgba;
    }

    private static void ApplyFilter(byte filterByte, Span<byte> curr, ReadOnlySpan<byte> prev, int bpp)
    {
        switch (filterByte)
        {
            case 0: // None
                break;
            case 1: // Sub: curr[i] += curr[i - bpp]
                for (int i = bpp; i < curr.Length; i++)
                    curr[i] = (byte)(curr[i] + curr[i - bpp]);
                break;
            case 2: // Up: curr[i] += prev[i]
                for (int i = 0; i < curr.Length; i++)
                    curr[i] = (byte)(curr[i] + prev[i]);
                break;
            case 3: // Average: curr[i] += floor((left + above) / 2)
                for (int i = 0; i < curr.Length; i++)
                {
                    byte left  = i >= bpp ? curr[i - bpp] : (byte)0;
                    byte above = prev[i];
                    curr[i] = (byte)(curr[i] + (left + above) / 2);
                }
                break;
            case 4: // Paeth
                for (int i = 0; i < curr.Length; i++)
                {
                    byte left      = i >= bpp ? curr[i - bpp] : (byte)0;
                    byte above     = prev[i];
                    byte upperLeft = i >= bpp ? prev[i - bpp] : (byte)0;
                    curr[i] = (byte)(curr[i] + Paeth(left, above, upperLeft));
                }
                break;
            default:
                throw new InvalidDataException($"Unknown PNG filter byte {filterByte}.");
        }
    }

    private static byte Paeth(byte a, byte b, byte c)
    {
        // a = left, b = above, c = upper-left.
        int p  = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc) return a;
        if (pb <= pc)             return b;
        return c;
    }

    private static void ReadExact(Stream s, Span<byte> buffer)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int n = s.Read(buffer[read..]);
            if (n == 0) throw new EndOfStreamException("Unexpected end of PNG stream.");
            read += n;
        }
    }

    private static void CopyExact(Stream src, Stream dst, int length)
    {
        const int ChunkSize = 4096;
        byte[] buffer = new byte[Math.Min(ChunkSize, length)];
        int remaining = length;
        while (remaining > 0)
        {
            int want = Math.Min(buffer.Length, remaining);
            int n = src.Read(buffer, 0, want);
            if (n == 0) throw new EndOfStreamException("Unexpected end of PNG stream.");
            dst.Write(buffer, 0, n);
            remaining -= n;
        }
    }

    private static void Skip(Stream s, int length)
    {
        if (s.CanSeek) { s.Seek(length, SeekOrigin.Current); return; }
        Span<byte> trash = stackalloc byte[1024];
        int remaining = length;
        while (remaining > 0)
        {
            int n = s.Read(trash[..Math.Min(trash.Length, remaining)]);
            if (n == 0) throw new EndOfStreamException("Unexpected end of PNG stream.");
            remaining -= n;
        }
    }
}
