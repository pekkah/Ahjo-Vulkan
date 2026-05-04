using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ahjo.Vulkan;

/// <summary>
/// Pointer wrapper over a static, null-terminated UTF-8 string (typically a
/// <c>"…"u8</c> literal in the assembly's read-only data segment, lifetime =
/// process). Exists because <c>ReadOnlySpan&lt;byte&gt;</c> is a <c>ref
/// struct</c> and cannot live inside <c>ReadOnlySpan&lt;T&gt;</c> or arrays —
/// the wrapper is the safe collection element.
/// </summary>
public readonly unsafe struct Utf8Name
{
    public readonly sbyte* Ptr;

    public Utf8Name(sbyte* ptr) => Ptr = ptr;

    /// <summary>
    /// Creates a Utf8Name over a UTF-8 string LITERAL (<c>"…"u8</c>). Per the
    /// C# specification, <c>"…"u8</c> literals live in the assembly's
    /// read-only data segment for the lifetime of the process and are
    /// followed by a trailing null byte (the byte is past
    /// <c>span.Length</c> — <c>(sbyte*)&amp;span[0]</c> is safe to pass to a
    /// Vulkan API that wants <c>const char*</c>).
    ///
    /// Callers MUST NOT pass a span over a <c>byte[]</c> or <c>stackalloc</c>
    /// buffer. The GC can move a managed array; a stack buffer is gone the
    /// moment the frame returns. The resulting pointer would dangle. There
    /// is no implicit conversion from <c>ReadOnlySpan&lt;byte&gt;</c>
    /// precisely because the compiler cannot enforce this contract at the
    /// call site; <c>FromLiteral</c> is the safety announcement.
    /// </summary>
    public static Utf8Name FromLiteral(ReadOnlySpan<byte> utf8Literal)
    {
        Debug.Assert(utf8Literal.Length > 0, "Utf8Name requires a non-empty UTF-8 literal.");
        return new Utf8Name(
            (sbyte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(utf8Literal)));
    }

    public bool IsNull => Ptr == null;
}
