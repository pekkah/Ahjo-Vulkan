using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ahjo.Vulkan;

/// <summary>
/// NVIDIA PC Latency (PCL) Stats: the ETW TraceLogging provider that latency
/// tools (PresentMon, the Reflex Test Utility, FrameView) read to measure PC
/// latency on <b>every</b> GPU vendor (issue #261).
/// </summary>
/// <remarks>
/// <para><b>Use.</b> <see cref="Initialize"/> once at startup,
/// <see cref="Shutdown"/> at exit. Send markers through
/// <see cref="Swapchain.SetLatencyMarker"/>, which forwards here; call
/// <see cref="Marker"/> directly only where there is no swapchain. In the
/// window pump, call <see cref="NotifyPingReceived"/> when
/// <see cref="IsPingMessage"/> is true; the next
/// <see cref="PclMarker.SimulationStart"/> then emits
/// <see cref="PclMarker.PcLatencyPing"/> first, with the same frame id.</para>
/// <para><b>One process-wide provider.</b> PCL Stats is a single provider per
/// process (the reference implementation keeps it in globals), so this class is
/// static.</para>
/// <para><b>Platform.</b> Windows-only: on other platforms every member is a
/// no-op and <see cref="IsInitialized"/> stays false. <see cref="Marker"/>
/// before <see cref="Initialize"/> is also a no-op, so a frame loop can call it
/// unconditionally.</para>
/// <para><b>Threading.</b> <see cref="Marker"/>, <see cref="IsPingMessage"/>
/// and <see cref="NotifyPingReceived"/> are thread-safe and allocation-free.
/// <see cref="Initialize"/>/<see cref="Shutdown"/> are serialized by a lock and
/// must not race the frame loop's markers.</para>
/// <para><b>Why not <c>EventSource</c>.</b> Native AOT sets
/// <c>EventSourceSupport=false</c>, which silently disables every
/// <c>EventSource</c>; the TraceLogging-shaped <c>Write&lt;T&gt;</c> is
/// <c>[RequiresUnreferencedCode]</c>; and the GUID would need an attribute
/// override. So this is raw <c>advapi32</c> TraceLogging
/// (<c>EventRegister</c>/<c>EventSetInformation</c>/<c>EventWriteTransfer</c>)
/// with an <c>[UnmanagedCallersOnly]</c> enable callback (spec E8).</para>
/// <para><b>Provenance.</b> The provider GUID and marker numbering are
/// published under MIT in NVIDIA Streamline's <c>include/sl_pcl.h</c> and
/// Intel PresentMon's <c>Nvidia_PCL.h</c>; the event and field names PresentMon
/// matches are in its MIT consumer. The provider name, ping message name and
/// <c>PCLStatsInput</c>/<c>PCLStatsFlags</c> field names are the protocol's
/// strings. No NVIDIA source or binary is used (spec E6/D8); the licensing
/// facts are in <c>docs/reflex-notes.md</c>.</para>
/// </remarks>
public static unsafe class PclStats
{
    private static readonly Lock s_lock = new();

    private static ulong             s_handle;
    private static volatile bool     s_enabled;
    private static int               s_pendingPing;
    private static uint              s_pingMessageId;
    private static PclStatsOptions   s_options;
    private static Thread?           s_pingThread;
    private static ManualResetEvent? s_quit;
    private static volatile bool     s_initialized;
    private static uint              s_processId;
    private static int               s_pingMarkersWritten;

    /// <summary><see langword="true"/> between a successful
    /// <see cref="Initialize"/> and <see cref="Shutdown"/>. Always false off
    /// Windows, and false when <c>EventRegister</c> failed.</summary>
    public static bool IsInitialized => s_initialized;

    /// <summary><see langword="true"/> while an ETW session has the provider
    /// enabled — something is listening.</summary>
    public static bool IsProviderEnabled => s_enabled;

    /// <summary>The registered window message of the ping
    /// (<c>RegisterWindowMessageW("PC_Latency_Stats_Ping")</c>); 0 when not
    /// initialized or not on Windows.</summary>
    public static uint PingMessageId => s_pingMessageId;

    /// <summary>
    /// Registers the provider, writes <c>PCLStatsInit</c> and starts the ping
    /// thread. A no-op off Windows.
    /// </summary>
    /// <remarks>
    /// Telemetry must not crash a game: if <c>EventRegister</c> fails, a
    /// warning goes to <see cref="AhjoDiagnostics.Sink"/>,
    /// <see cref="IsInitialized"/> stays false and markers stay no-ops.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Already initialized; call
    /// <see cref="Shutdown"/> first.</exception>
    public static void Initialize(in PclStatsOptions options)
    {
        if (!OperatingSystem.IsWindows()) return;
        lock (s_lock)
        {
            if (s_initialized)
                throw new InvalidOperationException("PclStats is already initialized; call Shutdown first.");

            uint msg;
            fixed (char* name = PclStatsEtw.PingMessageName)
                msg = PclStatsEtw.RegisterWindowMessageW(name);

            Guid id = PclStatsEtw.ProviderId;
            ulong handle = 0;
            uint status = PclStatsEtw.EventRegister(&id, &EnableCallback, null, &handle);
            if (status != 0)
            {
                AhjoDiagnostics.Write(DiagnosticSeverity.Warning, "PclStats",
                    $"EventRegister failed (0x{status:X8}); PCL Stats markers are disabled.");
                return;
            }

            s_handle        = handle;
            s_pingMessageId = msg;
            s_options       = options;
            s_processId     = (uint)Environment.ProcessId;
            Volatile.Write(ref s_pendingPing, 0);

            fixed (byte* traits = PclStatsEtw.ProviderTraits)
                _ = PclStatsEtw.EventSetInformation(
                    handle, PclStatsEtw.EventProviderSetTraits, traits, (uint)PclStatsEtw.ProviderTraits.Length);

            PclStatsEtw.Write(handle, PclStatsEtw.InitEvent, null, 0, null, 0);

            s_quit = new ManualResetEvent(false);
            s_pingThread = new Thread(PingThreadBody)
            {
                IsBackground = true,
                Name         = "Ahjo PCL Stats ping",
            };
            s_initialized = true;
            s_pingThread.Start();
        }
    }

    /// <summary>
    /// Stops the ping thread, writes <c>PCLStatsShutdown</c> and unregisters
    /// the provider. A no-op when not initialized.
    /// </summary>
    public static void Shutdown()
    {
        lock (s_lock)
        {
            if (!s_initialized) return;
            s_initialized = false;

            s_quit!.Set();
            s_pingThread!.Join(TimeSpan.FromSeconds(1));

            ulong handle = s_handle;
            PclStatsEtw.Write(handle, PclStatsEtw.ShutdownEvent, null, 0, null, 0);
            s_handle = 0;
            _ = PclStatsEtw.EventUnregister(handle);

            s_quit.Dispose();
            s_quit          = null;
            s_pingThread    = null;
            s_enabled       = false;
            s_pingMessageId = 0;
            s_options       = default;
            Volatile.Write(ref s_pendingPing, 0);
        }
    }

    /// <summary>
    /// Writes a <c>PCLStatsEvent { Marker, FrameID }</c>. On
    /// <see cref="PclMarker.SimulationStart"/> a pending ping (see
    /// <see cref="NotifyPingReceived"/>) is first written as
    /// <see cref="PclMarker.PcLatencyPing"/> with the same frame id. Thread-safe
    /// and allocation-free; a no-op before <see cref="Initialize"/>, off
    /// Windows, and — apart from consuming a pending ping — while no session is
    /// listening.
    /// </summary>
    /// <param name="marker">The marker.</param>
    /// <param name="frameId">The rendered frame's id, ≥ 1. PresentMon treats 0
    /// as "no frame".</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="frameId"/>
    /// is 0.</exception>
    public static void Marker(PclMarker marker, ulong frameId)
    {
        ArgumentOutOfRangeException.ThrowIfZero(frameId);
        if (!s_initialized) return;

        // The ping flag is consumed even when nobody is listening, so it cannot
        // go stale and fire on a much later frame.
        if (marker == PclMarker.SimulationStart && Interlocked.Exchange(ref s_pendingPing, 0) == 1 && s_enabled)
        {
            WriteMarker(PclMarker.PcLatencyPing, frameId);
            Interlocked.Increment(ref s_pingMarkersWritten);
        }

        if (!s_enabled) return;
        WriteMarker(marker, frameId);
    }

    /// <summary><see langword="true"/> when <paramref name="message"/> is the
    /// ping's registered window message. Call from the window pump.</summary>
    public static bool IsPingMessage(uint message) => message != 0 && message == s_pingMessageId;

    /// <summary>Records that the ping arrived; the next
    /// <see cref="PclMarker.SimulationStart"/> marker emits
    /// <see cref="PclMarker.PcLatencyPing"/> first.</summary>
    public static void NotifyPingReceived() => Volatile.Write(ref s_pendingPing, 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteMarker(PclMarker marker, ulong frameId)
    {
        uint  m = (uint)marker;
        ulong f = frameId;
        PclStatsEtw.Write(s_handle, PclStatsEtw.MarkerEvent, &m, sizeof(uint), &f, sizeof(ulong));
    }

    // ETW enable callback. May fire synchronously inside EventRegister, before
    // s_handle is stored — hence the handle check on capture-state. Must never
    // throw: an exception across UnmanagedCallersOnly terminates the process.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void EnableCallback(
        Guid* sourceId, uint controlCode, byte level, ulong matchAny, ulong matchAll, void* filter, void* context)
    {
        try
        {
            switch (controlCode)
            {
                case PclStatsEtw.ControlEnable:
                    s_enabled = true;
                    break;
                case PclStatsEtw.ControlDisable:
                    s_enabled = false;
                    break;
                case PclStatsEtw.ControlCaptureState:
                    ulong h = s_handle;
                    if (h != 0)
                    {
                        // Always 0: the wrapper always emits present markers
                        // (no PCLSTATS_NO_PRESENT_MARKERS).
                        uint flags = 0;
                        PclStatsEtw.Write(h, PclStatsEtw.FlagsEvent, &flags, sizeof(uint), null, 0);
                    }
                    break;
            }
        }
        catch
        {
            // Never throw across the unmanaged-to-managed boundary.
        }
    }

    // pclstats.h:53-97 semantics (spec D6): wake every 100-300 ms, act only
    // while a session listens, post the ping by the configured mode and record
    // a PCLStatsInput event. Allocation-free inside the loop.
    private static void PingThreadBody()
    {
        ManualResetEvent quit    = s_quit!;
        PclStatsOptions  options = s_options;
        ulong            handle  = s_handle;
        uint             msg     = s_pingMessageId;
        uint             pid     = s_processId;

        while (!quit.WaitOne(100 + Random.Shared.Next(200)))
        {
            if (!s_enabled) continue;

            if (options.PingThreadId != 0)
            {
                _ = PclStatsEtw.PostThreadMessageW(options.PingThreadId, msg, 0, 0);
                uint v = options.PingThreadId;
                PclStatsEtw.Write(handle, PclStatsEtw.InputIdThreadEvent, &v, sizeof(uint), null, 0);
                continue;
            }

            nint hwnd = PclStatsEtw.GetForegroundWindow();
            if (hwnd == 0) continue;
            uint owner = 0;
            _ = PclStatsEtw.GetWindowThreadProcessId(hwnd, &owner);
            if (owner != pid) continue;

            if (options.PingVirtualKey != PclPingVirtualKey.None)
            {
                nuint vk = (nuint)(ushort)options.PingVirtualKey;
                _ = PclStatsEtw.PostMessageW(hwnd, PclStatsEtw.WM_KEYDOWN, vk, 1);
                _ = PclStatsEtw.PostMessageW(hwnd, PclStatsEtw.WM_KEYUP, vk, unchecked((nint)0xC0000001u));
                uint v = (ushort)options.PingVirtualKey;
                PclStatsEtw.Write(handle, PclStatsEtw.InputVirtualKeyEvent, &v, sizeof(uint), null, 0);
            }
            else
            {
                _ = PclStatsEtw.PostMessageW(hwnd, msg, 0, 0);
                uint v = msg;
                PclStatsEtw.Write(handle, PclStatsEtw.InputMsgIdEvent, &v, sizeof(uint), null, 0);
            }
        }
    }

    // ---- Test / benchmark seam (InternalsVisibleTo) ----

    /// <summary>Forces <see cref="IsProviderEnabled"/> without an ETW session,
    /// so tests and benchmarks can drive the write path.</summary>
    internal static void SetProviderEnabledForTesting(bool enabled) => s_enabled = enabled;

    /// <summary>How many <see cref="PclMarker.PcLatencyPing"/> markers
    /// <see cref="Marker"/> has written this process.</summary>
    internal static int PingMarkersWrittenForTesting => Volatile.Read(ref s_pingMarkersWritten);
}
