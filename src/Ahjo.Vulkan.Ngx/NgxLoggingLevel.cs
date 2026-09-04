namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// How much NGX logs. Shadow of <c>NVSDK_NGX_Logging_Level</c>.
/// </summary>
/// <remarks>
/// Anything above <see cref="Off"/> installs a callback that forwards NGX's own
/// messages to <see cref="AhjoDiagnostics.Sink"/>, tagged <c>"NGX"</c>. At
/// <see cref="Off"/> — the default — no callback is installed at all, so NGX
/// never calls back into managed code.
/// <para>The native enum's terminating <c>_NUM</c> count member is omitted; the
/// member-count drift test asserts that.</para>
/// </remarks>
public enum NgxLoggingLevel : uint
{
    /// <summary>No logging callback is installed. Default.</summary>
    Off = 0,

    /// <summary>NGX's normal log output.</summary>
    On = 1,

    /// <summary>Everything NGX will say. Noisy; for diagnosing a failure.</summary>
    Verbose = 2,
}
