namespace Ahjo.Vulkan.Slang;

/// <summary>
/// The scalar type of one <see cref="SlangAbortArgument"/>, taken from its
/// format specifier: a Slang abort payload carries no type information of its
/// own.
/// </summary>
/// <remarks>
/// <c>%hf</c> and <c>%lf</c> depart from C on purpose: the payload needs the
/// width, and C has no 16-bit float. See <see cref="SlangAbortMessage"/>.
/// </remarks>
public enum SlangAbortArgumentKind
{
    /// <summary>1 byte, signed: <c>%hhd</c> / <c>%hhi</c>.</summary>
    Int8,

    /// <summary>1 byte, unsigned: <c>%hhu</c> / <c>%hhx</c> / <c>%hhX</c>.</summary>
    UInt8,

    /// <summary>2 bytes, signed: <c>%hd</c> / <c>%hi</c>.</summary>
    Int16,

    /// <summary>2 bytes, unsigned: <c>%hu</c> / <c>%hx</c> / <c>%hX</c>.</summary>
    UInt16,

    /// <summary>4 bytes, signed: <c>%d</c> / <c>%i</c>.</summary>
    Int32,

    /// <summary>4 bytes, unsigned: <c>%u</c> / <c>%x</c> / <c>%X</c> (a Slang
    /// <c>bool</c> is widened to this).</summary>
    UInt32,

    /// <summary>8 bytes, signed: <c>%ld</c> / <c>%lld</c> / <c>%li</c> /
    /// <c>%lli</c>.</summary>
    Int64,

    /// <summary>8 bytes, unsigned: <c>%lu</c> / <c>%llu</c> / <c>%lx</c> /
    /// <c>%llx</c> (and <c>X</c>).</summary>
    UInt64,

    /// <summary>2 bytes, IEEE half: <c>%hf</c> (and <c>F e E g G</c>). Not
    /// C.</summary>
    Float16,

    /// <summary>4 bytes, IEEE single: <c>%f</c> / <c>%F</c> / <c>%e</c> /
    /// <c>%E</c> / <c>%g</c> / <c>%G</c>.</summary>
    Float32,

    /// <summary>8 bytes, IEEE double: <c>%lf</c> / <c>%llf</c> (and
    /// <c>F e E g G</c>).</summary>
    Float64,
}
