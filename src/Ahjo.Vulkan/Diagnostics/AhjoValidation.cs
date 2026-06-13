using System.Runtime.CompilerServices;
using System.Threading;

namespace Ahjo.Vulkan;

/// <summary>
/// Thrown when an <see cref="AhjoValidation"/> check fails — a wrapper-level
/// misuse the driver / validation layer might not catch (or would catch only
/// later, with a worse message): a descriptor set bound against the wrong
/// layout, a push-constant window outside the layout's declared ranges, a
/// pool handed a foreign handle, a double-dispose. Distinct from
/// <see cref="VulkanException"/> (a non-success <c>VkResult</c> from the
/// driver) so callers can tell wrapper-contract violations from driver errors.
/// </summary>
public sealed class AhjoValidationException : InvalidOperationException
{
    public AhjoValidationException(string message) : base(message)
    {
    }
}

/// <summary>
/// Master switch for the wrapper's internal, debug-oriented validation:
/// <list type="bullet">
/// <item><see cref="CommandRecorder"/>'s PipelineLayout compatibility checks
/// (descriptor-set layout match, push-constant range coverage),</item>
/// <item>pool ownership scans (<see cref="SemaphorePool"/>,
/// <see cref="DescriptorSetPool"/>, <see cref="CommandBufferPool"/>),</item>
/// <item>the debug double-dispose registry for owning handles.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>These checks were previously scattered behind <c>Debug.Assert</c> /
/// <c>[Conditional("DEBUG")]</c>, which compiled out entirely in Release.
/// Routing them through one runtime flag makes the cost model explicit and
/// — crucially — lets a user flip validation on in a <em>Release</em> build to
/// chase a bug that only reproduces there. A failing check throws
/// <see cref="AhjoValidationException"/> (and reports through
/// <see cref="AhjoDiagnostics"/>) rather than tripping a debugger-only assert,
/// so it surfaces identically in Debug and Release.</para>
/// <para><see cref="Enabled"/> defaults to <see langword="true"/> in
/// <c>DEBUG</c> builds and <see langword="false"/> in Release. When disabled
/// every check is a single predictable branch on a <see langword="bool"/> —
/// no allocation, no work — preserving the zero-per-frame-allocation contract
/// on the hot paths. When enabled, failing checks may allocate their message
/// strings; passing checks on the hot path stay allocation-free because the
/// message is only built on the failure branch.</para>
/// <para>Toggle it as early as possible — ideally before creating the handles
/// you want covered. The double-dispose registry only tracks handles created
/// while <see cref="Enabled"/> is <see langword="true"/>, so flipping it on
/// mid-run leaves earlier handles untracked (a benign gap, never a false
/// positive).</para>
/// </remarks>
public static class AhjoValidation
{
    private static int s_enabled =
#if DEBUG
        1;
#else
        0;
#endif

    /// <summary>
    /// When <see langword="true"/>, the wrapper runs its internal correctness
    /// checks and throws <see cref="AhjoValidationException"/> on violation.
    /// Defaults to <see langword="true"/> in DEBUG, <see langword="false"/> in
    /// Release. Safe to set from any thread.
    /// </summary>
    public static bool Enabled
    {
        get => Volatile.Read(ref s_enabled) != 0;
        set => Volatile.Write(ref s_enabled, value ? 1 : 0);
    }

    /// <summary>
    /// Fast-path gate for call sites that must avoid building a message (or
    /// running a linear scan) unless validation is on. Inlined so a disabled
    /// build pays just a volatile read and a branch.
    /// </summary>
    internal static bool IsEnabled
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref s_enabled) != 0;
    }

    /// <summary>
    /// Reports <paramref name="message"/> through <see cref="AhjoDiagnostics"/>
    /// and throws <see cref="AhjoValidationException"/>. Call only from the
    /// failure branch of a check guarded by <see cref="IsEnabled"/> so the
    /// message string is never built on a passing hot path.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    internal static void Fail(string source, string message)
    {
        AhjoDiagnostics.Write(DiagnosticSeverity.Error, source, message);
        throw new AhjoValidationException(message);
    }
}
