using System.Runtime.InteropServices;
using System.Text;

namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// One native, bump-allocated block holding every UTF-8 string a single NGX
/// setup call needs, plus the pointer array for the search-path list.
/// </summary>
/// <remarks>
/// <para><b>Why a native block rather than pinned <c>byte[]</c>s.</b>
/// <c>AhjoNgxInitInfo</c> carries <c>const char*</c> fields, and a managed
/// array's address is only valid inside a <c>fixed</c> scope — with several
/// strings plus an array of pointers to them, that is a nest of <c>fixed</c>
/// statements the encoder can silently outlive.</para>
/// <para><b>Every <see cref="Add"/> writes the terminating NUL explicitly.</b>
/// That is the exact bug PR #217 fixed on the shim side:
/// <c>"…"u8.ToArray()</c> copies only <c>Length</c> bytes, dropping the
/// terminator that a <c>"…"u8</c> literal has but that is not part of its
/// span.</para>
/// <para><b>Lifetime.</b> The block only has to outlive the P/Invoke: the shim
/// copies and retains every string on the init path
/// (<c>native/ngx/src/ahjo_ngx.cpp:707-760</c>, spec E5) and the discovery path
/// is call-scoped by construction. Setup-time only — nothing on the evaluate
/// path uses this.</para>
/// </remarks>
internal unsafe ref struct NgxUtf8Block
{
    private byte*  _bytes;        // start of the string area
    private sbyte** _pointers;    // start of the pointer area (may be null)
    private void*  _allocation;   // what Dispose frees
    private int    _byteCapacity;
    private int    _byteCursor;
    private int    _pointerCapacity;
    private int    _pointerCursor;

    /// <param name="byteCapacity">
    /// Bytes of UTF-8 (terminators included) the block must hold. Size it with
    /// <see cref="Encoding.UTF8"/>'s maximum byte count plus one per string.
    /// </param>
    /// <param name="stringCapacity">
    /// How many entries the <see cref="AddArray"/> pointer array must hold.
    /// Zero when no array is needed.
    /// </param>
    internal NgxUtf8Block(int byteCapacity, int stringCapacity)
    {
        _byteCapacity    = byteCapacity;
        _pointerCapacity = stringCapacity;

        nuint pointerBytes = (nuint)stringCapacity * (nuint)sizeof(sbyte*);
        nuint total        = pointerBytes + (nuint)byteCapacity;
        if (total == 0) total = 1;   // NativeMemory.Alloc(0) is not worth reasoning about

        // Pointer area first so it keeps NativeMemory.Alloc's natural
        // alignment; the byte area needs none.
        _allocation = NativeMemory.Alloc(total);
        _pointers   = stringCapacity > 0 ? (sbyte**)_allocation : null;
        _bytes      = (byte*)_allocation + pointerBytes;
    }

    /// <summary>
    /// Encodes <paramref name="value"/> into the block and returns a pointer to
    /// the NUL-terminated copy. Returns <see langword="null"/> for
    /// <see langword="null"/> — the shape every optional <c>const char*</c>
    /// field on <c>AhjoNgxInitInfo</c> wants.
    /// </summary>
    internal sbyte* Add(string? value)
    {
        if (value is null) return null;

        int written = Encoding.UTF8.GetBytes(
            value.AsSpan(),
            new Span<byte>(_bytes + _byteCursor, _byteCapacity - _byteCursor));

        sbyte* start = (sbyte*)(_bytes + _byteCursor);
        _byteCursor += written;

        // The terminator, explicitly. GetBytes never writes one.
        if (_byteCursor >= _byteCapacity)
            throw new InvalidOperationException("NgxUtf8Block was sized too small — this is a bug in Ahjo.Vulkan.Ngx.");
        _bytes[_byteCursor++] = 0;

        return start;
    }

    /// <summary>
    /// Encodes each entry of <paramref name="values"/> and returns a pointer to
    /// an array of the resulting pointers, as
    /// <c>AhjoNgxInitInfo.FeatureSearchPaths</c> wants. Returns
    /// <see langword="null"/> with <paramref name="count"/> zero for a null or
    /// empty list.
    /// </summary>
    internal sbyte** AddArray(IReadOnlyList<string>? values, out uint count)
    {
        if (values is null || values.Count == 0)
        {
            count = 0;
            return null;
        }

        if (_pointerCursor + values.Count > _pointerCapacity)
            throw new InvalidOperationException("NgxUtf8Block was sized too small — this is a bug in Ahjo.Vulkan.Ngx.");

        sbyte** start = _pointers + _pointerCursor;
        for (int i = 0; i < values.Count; i++)
            _pointers[_pointerCursor++] = Add(values[i]);

        count = (uint)values.Count;
        return start;
    }

    /// <summary>Frees the block. Idempotent.</summary>
    internal void Dispose()
    {
        if (_allocation is null) return;
        NativeMemory.Free(_allocation);
        _allocation = null;
        _bytes      = null;
        _pointers   = null;
    }
}

/// <summary>Decoding helpers for UTF-8 the NGX side hands back.</summary>
internal static unsafe class NgxUtf8
{
    /// <summary>
    /// Decodes a NUL-terminated UTF-8 string; <see langword="null"/> in,
    /// <see langword="null"/> out. Cold path only (the logging thunk, error
    /// text) — it allocates a <see cref="string"/>.
    /// </summary>
    internal static string? ToString(sbyte* utf8)
        => utf8 is null ? null : Marshal.PtrToStringUTF8((nint)utf8);

    /// <summary>
    /// Decodes at most <paramref name="maxLength"/> bytes of a UTF-8 buffer,
    /// stopping at the first NUL. For NGX's fixed-size inline char arrays,
    /// which are not guaranteed terminated when full.
    /// </summary>
    internal static string ToString(ReadOnlySpan<byte> utf8, int maxLength)
    {
        ReadOnlySpan<byte> bounded = utf8.Length > maxLength ? utf8[..maxLength] : utf8;
        int nul = bounded.IndexOf((byte)0);
        return Encoding.UTF8.GetString(nul >= 0 ? bounded[..nul] : bounded);
    }
}
