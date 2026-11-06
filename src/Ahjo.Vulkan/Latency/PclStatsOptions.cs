namespace Ahjo.Vulkan;

/// <summary>
/// How <see cref="PclStats"/>'s ping thread delivers the PC Latency ping to the
/// application (issue #261). <c>default</c> selects the registered-message
/// mode, which is the recommended one.
/// </summary>
/// <remarks>
/// <para><b>Three modes, in precedence order</b> (the PCL Stats reference
/// behaviour, <c>pclstats.h:63-88</c>):</para>
/// <list type="number">
/// <item><description><see cref="PingThreadId"/> non-zero: the ping is posted
/// with <c>PostThreadMessageW</c> to that thread, as the registered
/// message.</description></item>
/// <item><description>Otherwise, <see cref="PingVirtualKey"/> not
/// <see cref="PclPingVirtualKey.None"/>: when this process owns the foreground
/// window, a <c>WM_KEYDOWN</c>/<c>WM_KEYUP</c> pair for that key is posted to
/// it. This collides with the Reflex QA item "Keybinding menus work properly
/// (no F13)" (Reflex guide l. 1029), so avoid it.</description></item>
/// <item><description>Otherwise (the default): when this process owns the
/// foreground window, the registered window message
/// <see cref="PclStats.PingMessageId"/> is posted to it. The window pump
/// recognises it with <see cref="PclStats.IsPingMessage"/>.</description></item>
/// </list>
/// </remarks>
public struct PclStatsOptions
{
    /// <summary>Win32 thread id to post the ping to; 0 (default) selects one of
    /// the window modes.</summary>
    public uint PingThreadId;

    /// <summary>Virtual key to post as the ping when
    /// <see cref="PingThreadId"/> is 0; <see cref="PclPingVirtualKey.None"/>
    /// (default) selects the registered message.</summary>
    public PclPingVirtualKey PingVirtualKey;
}
