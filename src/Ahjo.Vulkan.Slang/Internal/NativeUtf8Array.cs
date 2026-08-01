using System.Runtime.InteropServices;
using System.Text;

namespace Ahjo.Vulkan.Slang.Internal;

/// <summary>
/// A <c>const char* const*</c> array of null-terminated UTF-8 strings in
/// unmanaged memory, alive until <see cref="Dispose"/>.
/// </summary>
/// <remarks>
/// <para>Exists for exactly one caller: <c>SessionDesc.searchPaths</c>. Every
/// other <c>const char*</c> this package passes is either a <c>"…"u8</c>
/// literal or a single runtime string that
/// <see cref="SlangUtf8.ScopedUtf8"/> covers under a <c>fixed</c> block —
/// but an <em>array</em> of pointers cannot be produced by nesting
/// <c>fixed</c> statements when the count is only known at run time.</para>
/// <para>Unmanaged rather than pooled-and-pinned, and kept alive for the whole
/// session rather than just the <c>createSession</c> call: Slang copies search
/// paths into its own strings at session-create time, but "copies" is an
/// implementation detail of a binary we consume prebuilt, and outliving it
/// costs one small allocation at setup time.</para>
/// </remarks>
internal sealed unsafe class NativeUtf8Array : IDisposable
{
    private nint _block;

    private NativeUtf8Array(nint block, int count)
    {
        _block = block;
        Count = count;
    }

    /// <summary>Number of strings; <c>0</c> for an empty array.</summary>
    public int Count { get; }

    /// <summary>The pointer array, or <see langword="null"/> when <see cref="Count"/> is 0.</summary>
    public sbyte** Pointers => (sbyte**)_block;

    /// <summary>
    /// Copies <paramref name="values"/> into one unmanaged block. Returns
    /// <see langword="null"/> for a null or empty input, so the caller can
    /// pass <c>null</c>/<c>0</c> straight through to Slang.
    /// </summary>
    public static NativeUtf8Array? Create(string[]? values)
    {
        if (values is null || values.Length == 0)
        {
            return null;
        }

        int count = values.Length;
        nuint pointerBytes = (nuint)count * (nuint)sizeof(nint);
        nuint textBytes = 0;

        for (int i = 0; i < count; i++)
        {
            string value = values[i]
                ?? throw new ArgumentException("Search paths must not contain null entries.", nameof(values));

            textBytes += (nuint)Encoding.UTF8.GetByteCount(value) + 1;
        }

        nint block = (nint)NativeMemory.Alloc(pointerBytes + textBytes);

        try
        {
            sbyte** pointers = (sbyte**)block;
            byte* text = (byte*)block + pointerBytes;

            for (int i = 0; i < count; i++)
            {
                string value = values[i];
                int byteCount = Encoding.UTF8.GetByteCount(value);

                // Written into memory this type owns and terminates itself —
                // the form invariant #1 forbids is the byte[]-returning
                // overload handed to native code unpinned and unterminated.
                Encoding.UTF8.GetBytes(value, new Span<byte>(text, byteCount));
                text[byteCount] = 0;

                pointers[i] = (sbyte*)text;
                text += byteCount + 1;
            }
        }
        catch
        {
            NativeMemory.Free((void*)block);
            throw;
        }

        return new NativeUtf8Array(block, count);
    }

    public void Dispose()
    {
        nint block = _block;

        _block = 0;

        if (block != 0)
        {
            NativeMemory.Free((void*)block);
        }
    }
}
