using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

using Ahjo.Vulkan.Slang.Native;

namespace Ahjo.Vulkan.Slang.Internal;

/// <summary>
/// The UTF-8 boundary between this wrapper and Slang's <c>const char*</c>
/// surface, plus the reader for Slang's <c>ISlangBlob</c> diagnostics.
/// </summary>
/// <remarks>
/// <para><b>Why this exists next to invariant #1.</b> The wrapper's rule is
/// <c>Utf8Name.FromLiteral("…"u8)</c> for Vulkan <c>const char*</c>, and that
/// rule is unchanged for everything Slang takes that <em>is</em> a constant:
/// the SPIR-V profile name (<c>"spirv_1_5"u8</c>), the default module name,
/// the synthetic source path. Those go through <c>"…"u8</c> literals and never
/// touch this file.</para>
/// <para>Slang additionally takes <em>runtime-variable</em> strings — a file
/// path the caller typed, a module name a material system generated, an
/// entry-point name read out of an asset. There is no literal to point at. The
/// prohibition being honoured for those is the one invariant #1 actually
/// protects against: <em>never hand native code a GC-movable, unterminated
/// pointer.</em> <see cref="ScopedUtf8"/> encodes into a caller-provided
/// <c>stackalloc</c> scratch buffer (or a pooled rental when the string does
/// not fit), writes an explicit <c>0</c> terminator, and exposes only a
/// <see cref="ReadOnlySpan{T}"/> — so the pointer can be produced only inside
/// a <c>fixed</c> block, which is exactly the scope the native call has to sit
/// in.</para>
/// <para>The existing precedent for the same shape is
/// <c>GraphicsPipelineBuilder.CopyName</c>
/// (<c>src/Ahjo.Vulkan/Pipelines/GraphicsPipelineBuilder.cs:213-220</c>),
/// which copies a caller-supplied <c>ReadOnlySpan&lt;byte&gt;</c> into an
/// inline fixed-size buffer and null-terminates by clearing first.</para>
/// <para>Nothing here is on a per-frame path. Compilation is setup-time and
/// invariant #3 does not apply (spec §Problem); the pooling below is about not
/// stack-overflowing on a long path, not about allocation counts.</para>
/// </remarks>
internal static unsafe class SlangUtf8
{
    /// <summary>
    /// Scratch size every call site stack-allocates before constructing a
    /// <see cref="ScopedUtf8"/>. Comfortably covers module names, entry-point
    /// names and profile names; longer file paths fall back to the pool.
    /// </summary>
    public const int StackScratchBytes = 512;

    /// <summary>
    /// Decodes a null-terminated native string. Mirrors
    /// <c>src/Ahjo.Vulkan/Internal/Utf8.cs:11-12</c>.
    /// </summary>
    public static string? ToString(sbyte* utf8)
        => utf8 == null ? null : Marshal.PtrToStringUTF8((nint)utf8);

    /// <summary>
    /// Reads a Slang diagnostics/text blob into a managed string. Returns
    /// <see cref="string.Empty"/> for a null or zero-length blob; does
    /// <b>not</b> release the blob — the caller owns it.
    /// </summary>
    /// <remarks>
    /// Decoding native bytes into a managed string is the opposite direction
    /// from invariant #1, which governs the pointers we hand Slang. A blob is
    /// neither null-terminated by contract nor owned by us, so the
    /// length-carrying overload is the correct one.
    /// </remarks>
    public static string ReadBlob(ISlangBlob* blob)
    {
        if (blob == null)
        {
            return string.Empty;
        }

        int size = (int)blob->getBufferSize();

        return size <= 0 ? string.Empty : Encoding.UTF8.GetString((byte*)blob->getBufferPointer(), size);
    }

    /// <summary>
    /// Reads and releases an <c>outDiagnostics</c> blob, clearing the caller's
    /// pointer. Always call this — before inspecting the result code, before
    /// inspecting a returned pointer, and on the success path too.
    /// </summary>
    /// <remarks>
    /// Every Slang call in this package that takes an
    /// <c>ISlangBlob** outDiagnostics</c> passes one and funnels it through
    /// here, so there is no call site where a compiler message can be produced
    /// and dropped. A non-empty blob on a <em>successful</em> call is a
    /// warning set and reaches <see cref="SlangProgram.Warnings"/>.
    /// </remarks>
    public static string TakeDiagnostics(ISlangBlob** diagnostics)
    {
        ISlangBlob* blob = *diagnostics;

        *diagnostics = null;

        if (blob == null)
        {
            return string.Empty;
        }

        string text = ReadBlob(blob);

        blob->release();

        return text;
    }

    /// <summary>
    /// A null-terminated UTF-8 view of a runtime-variable string, valid until
    /// <see cref="Dispose"/>.
    /// </summary>
    /// <remarks>
    /// <para>A <c>ref struct</c> on purpose: the bytes live either in the
    /// caller's stack frame or in a pooled array that <see cref="Dispose"/>
    /// returns, so the value must not outlive the statement that made it.</para>
    /// <para>There is deliberately no <c>sbyte* Ptr</c> property. A pointer to
    /// GC-heap memory is only meaningful inside a <c>fixed</c> block, and
    /// exposing one as a property would let a caller take it outside that
    /// block — the exact failure invariant #1 exists to prevent. Call sites do:
    /// <code>
    /// Span&lt;byte&gt; scratch = stackalloc byte[SlangUtf8.StackScratchBytes];
    /// using var name = new SlangUtf8.ScopedUtf8(scratch, moduleName);
    /// fixed (byte* p = name.Bytes) { /* the native call */ }
    /// </code>
    /// </para>
    /// </remarks>
    public ref struct ScopedUtf8 : IDisposable
    {
        private byte[]? _rented;
        private Span<byte> _bytes;

        /// <summary>Copies UTF-8 <paramref name="utf8"/> and appends a terminator.</summary>
        public ScopedUtf8(Span<byte> scratch, ReadOnlySpan<byte> utf8)
        {
            Span<byte> destination = Reserve(scratch, utf8.Length + 1);

            utf8.CopyTo(destination);
            destination[utf8.Length] = 0;
            _bytes = destination;
        }

        /// <summary>Encodes <paramref name="value"/> as UTF-8 and appends a terminator.</summary>
        public ScopedUtf8(Span<byte> scratch, string value)
        {
            ArgumentNullException.ThrowIfNull(value);

            // Encoding.UTF8.GetBytes(ReadOnlySpan<char>, Span<byte>) writes
            // into memory we own and terminate ourselves. The forbidden form
            // is the byte[]-returning overload whose result gets handed to
            // native code unpinned and unterminated; this is not that.
            int byteCount = Encoding.UTF8.GetByteCount(value);
            Span<byte> destination = Reserve(scratch, byteCount + 1);

            Encoding.UTF8.GetBytes(value, destination);
            destination[byteCount] = 0;
            _bytes = destination;
        }

        /// <summary>
        /// The encoded bytes <b>including</b> the trailing <c>0</c>. Never
        /// empty, so <c>fixed</c> over it always yields a non-null pointer.
        /// </summary>
        public readonly ReadOnlySpan<byte> Bytes => _bytes;

        public void Dispose()
        {
            byte[]? rented = _rented;

            _rented = null;
            _bytes = default;

            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        private Span<byte> Reserve(Span<byte> scratch, int length)
        {
            if (length <= scratch.Length)
            {
                return scratch[..length];
            }

            _rented = ArrayPool<byte>.Shared.Rent(length);

            return _rented.AsSpan(0, length);
        }
    }
}
