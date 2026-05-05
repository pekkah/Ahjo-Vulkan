using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;

namespace Ahjo.Vulkan;

/// <summary>
/// Convenience loader for SPIR-V blobs from disk. Rents a <c>byte[]</c>
/// from <see cref="ArrayPool{Byte}.Shared"/> sized to the file length so
/// repeated loads don't churn the LOH; <see cref="Dispose"/> returns the
/// buffer.
/// </summary>
/// <remarks>
/// <para>The pooled buffer is the canonical fixture for the
/// <see cref="ReadOnlySpan{UInt32}"/> view returned by <see cref="Words"/>.
/// SPIR-V is 32-bit-aligned by spec; the file size is asserted to be a
/// multiple of 4 at load time.</para>
/// <para><b>Pinning.</b> <see cref="ArrayPool{Byte}"/> hands back GC-heap
/// arrays — the <see cref="ReadOnlySpan{UInt32}"/> here is a stack span
/// over moveable memory and is safe only inside the <c>using</c> scope of
/// the <see cref="SpirvBlob"/>. Callers passing it to
/// <see cref="Device.CreateShaderModule(ReadOnlySpan{uint})"/> are fine
/// because that method <c>fixed</c>'s the pointer for the duration of
/// the native call.</para>
/// </remarks>
public sealed class SpirvBlob : IDisposable
{
    private byte[]? _buffer;
    private int _length;

    private SpirvBlob(byte[] buffer, int length)
    {
        _buffer = buffer;
        _length = length;
    }

    /// <summary>SPIR-V words, valid until <see cref="Dispose"/>.</summary>
    public ReadOnlySpan<uint> Words
    {
        get
        {
            if (_buffer is null) throw new ObjectDisposedException(nameof(SpirvBlob));
            return MemoryMarshal.Cast<byte, uint>(_buffer.AsSpan(0, _length));
        }
    }

    /// <summary>Raw bytes, valid until <see cref="Dispose"/>.</summary>
    public ReadOnlySpan<byte> Bytes
    {
        get
        {
            if (_buffer is null) throw new ObjectDisposedException(nameof(SpirvBlob));
            return _buffer.AsSpan(0, _length);
        }
    }

    /// <summary>
    /// Reads the SPIR-V file at <paramref name="path"/> into a pooled
    /// buffer. Throws if the file size isn't a multiple of 4.
    /// </summary>
    public static SpirvBlob Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        long longLen = fs.Length;
        if (longLen <= 0)
            throw new ArgumentException($"SPIR-V file is empty: {path}", nameof(path));
        if (longLen > int.MaxValue)
            throw new ArgumentException($"SPIR-V file too large: {path}", nameof(path));
        if ((longLen & 3) != 0)
            throw new ArgumentException(
                $"SPIR-V file size must be a multiple of 4 (got {longLen}): {path}", nameof(path));

        int len = (int)longLen;
        byte[] buf = ArrayPool<byte>.Shared.Rent(len);
        try
        {
            int read = 0;
            while (read < len)
            {
                int n = fs.Read(buf, read, len - read);
                if (n == 0)
                    throw new EndOfStreamException($"Truncated SPIR-V file: {path}");
                read += n;
            }
            return new SpirvBlob(buf, len);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(buf);
            throw;
        }
    }

    public void Dispose()
    {
        byte[]? buf = _buffer;
        if (buf is null) return;
        _buffer = null;
        _length = 0;
        ArrayPool<byte>.Shared.Return(buf);
    }
}
