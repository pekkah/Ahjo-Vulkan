using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// <c>Ahjo.Vulkan</c>'s validation protocol, re-expressed against its public
/// surface.
/// </summary>
/// <remarks>
/// <para>This is <c>AhjoValidation.Fail</c>
/// (<c>src/Ahjo.Vulkan/Diagnostics/AhjoValidation.cs:94-99</c>) written out in
/// three lines. Both it and <c>AhjoDiagnostics.Write</c> are <c>internal</c> to
/// <c>Ahjo.Vulkan</c>, and <c>Ahjo.Vulkan.Ngx</c> is a separately published,
/// independently versioned package — <b>do not</b> add an
/// <c>InternalsVisibleTo</c> to <c>Ahjo.Vulkan</c> to avoid this duplication
/// (spec D10/E4). Everything it needs is already public:
/// <see cref="AhjoValidation.Enabled"/>,
/// <see cref="AhjoValidationException"/> and
/// <see cref="AhjoDiagnostics.Sink"/>.</para>
/// <para>Behaviour is identical, deliberately: a failing check reports through
/// the host's installed sink and throws the same exception type the wrapper
/// throws, so a consumer's diagnostics wiring does not need a second case.</para>
/// </remarks>
internal static class NgxValidation
{
    /// <summary>
    /// Fast-path gate. Inlined so a disabled build pays a volatile read and a
    /// branch, matching the wrapper's cost model.
    /// </summary>
    internal static bool IsEnabled
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => AhjoValidation.Enabled;
    }

    /// <summary>
    /// Reports <paramref name="message"/> through
    /// <see cref="AhjoDiagnostics.Sink"/> and throws
    /// <see cref="AhjoValidationException"/>. Call only from the failure branch
    /// of a check guarded by <see cref="IsEnabled"/>, so the message string is
    /// never built on a passing hot path.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [DoesNotReturn]
    internal static void Fail(string source, string message)
    {
        AhjoDiagnostics.Sink(DiagnosticSeverity.Error, source, message);
        throw new AhjoValidationException(message);
    }
}
