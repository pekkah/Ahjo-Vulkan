namespace Ahjo.Vulkan;

/// <summary>
/// Severity attached to a wrapper diagnostic routed through
/// <see cref="AhjoDiagnostics.Sink"/>. Maps loosely onto host logger
/// levels; the wrapper currently emits <see cref="Warning"/> for its own
/// teardown/setup notices and mirrors the debug-utils severity for
/// validation-layer messages.
/// </summary>
public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>
/// Receives every diagnostic the wrapper would otherwise write to stderr.
/// <paramref name="source"/> is a short stable identifier of the emitting
/// subsystem (<c>"Device"</c>, <c>"Allocator"</c>, <c>"FrameRing"</c>,
/// <c>"PipelineCache"</c>, <c>"Vulkan"</c> for debug-utils messages);
/// <paramref name="message"/> is the full human-readable text.
/// </summary>
public delegate void DiagnosticSink(DiagnosticSeverity severity, string source, string message);

/// <summary>
/// Process-wide diagnostics hook. The wrapper never writes to
/// <see cref="Console"/> directly — every diagnostic (dispose-time
/// warnings, VMA leak reports, pipeline-cache header mismatches, the
/// default debug-utils callback) flows through <see cref="Sink"/>, which
/// defaults to writing the message to <see cref="Console.Error"/>. An
/// engine host replaces it once at startup to route into its own logger.
/// </summary>
/// <remarks>
/// <para><b>Why static, not per-<see cref="Device"/>.</b> The default
/// debug-utils callback fires from <see cref="Instance"/> before any
/// device exists, and there is one logger per host process in practice.
/// The <c>source</c> argument keeps finer-grained routing possible on the
/// host side without API change.</para>
/// <para><b>Threading.</b> The sink field is <c>volatile</c>: replacement
/// is an atomic reference store, readers never observe a torn value, and
/// a sink installed before work starts is visible to all threads. The
/// sink itself may be invoked from any thread that disposes wrapper
/// objects (and from the loader's debug-utils callback thread) — hosts
/// must install a thread-safe delegate.</para>
/// <para><b>Cost.</b> Every wrapper call site is a cold path (dispose,
/// setup, validation-message); nothing in the per-frame
/// recording/sync/pool surface emits diagnostics.</para>
/// </remarks>
public static class AhjoDiagnostics
{
    private static volatile DiagnosticSink s_sink = WriteToStdError;

    /// <summary>
    /// The active sink. Never <see langword="null"/> — assigning
    /// <see langword="null"/> throws. Restore the default by assigning
    /// <see cref="WriteToStdError"/>.
    /// </summary>
    public static DiagnosticSink Sink
    {
        get => s_sink;
        set => s_sink = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// The default sink: writes <c>message</c> verbatim to
    /// <see cref="Console.Error"/> (severity and source are carried for
    /// hosts that filter; the default preserves the wrapper's historical
    /// stderr output byte-for-byte).
    /// </summary>
    public static void WriteToStdError(DiagnosticSeverity severity, string source, string message)
        => Console.Error.WriteLine(message);

    internal static void Write(DiagnosticSeverity severity, string source, string message)
        => s_sink(severity, source, message);
}
