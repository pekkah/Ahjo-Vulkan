using System.Runtime.InteropServices;

namespace Ahjo.Vulkan;

/// <summary>
/// The raw ETW TraceLogging half of <see cref="PclStats"/> (issue #261): the
/// blittable <c>advapi32</c>/<c>user32</c> imports, the two descriptor structs
/// and the fixed self-describing metadata blobs of the PCL Stats provider.
/// </summary>
/// <remarks>
/// <para>The mechanics are TraceLogging's documented wire shape, as
/// implemented over <c>EventWriteTransfer</c> by Microsoft's
/// <c>TraceLoggingDynamic.cs</c> (microsoft/tracelogging,
/// <c>etw/cs/traceloggingdynamic</c>): provider traits set once with
/// <c>EventSetInformation(EventProviderSetTraits)</c>, and every write carries
/// a provider-traits descriptor (type 2), an event-metadata descriptor (type 1)
/// and the payload descriptors (type 0) over an <c>EVENT_DESCRIPTOR</c> on
/// channel 11.</para>
/// <para>The protocol facts — GUID, event names, field names and types,
/// level — are the PCL Stats provider's (issue #261 spec E6). Every blob below
/// is pinned byte-for-byte by <c>PclStatsTests.MetadataBlobs_MatchBuilder</c>,
/// which re-encodes each one from (name, fields) with an independent
/// builder.</para>
/// </remarks>
internal static unsafe class PclStatsEtw
{
    // ---- Constants ----

    /// <summary>The PCL Stats provider GUID,
    /// <c>{0d216f06-82a6-4d49-bc4f-8f38ae56efab}</c>; the GUID PresentMon and
    /// the Reflex tools enable.</summary>
    public static readonly Guid ProviderId = new(0x0d216f06, 0x82a6, 0x4d49, 0xbc, 0x4f, 0x8f, 0x38, 0xae, 0x56, 0xef, 0xab);

    /// <summary>The registered window-message name of the ping.</summary>
    public const string PingMessageName = "PC_Latency_Stats_Ping";

    /// <summary><c>EVENT_CONTROL_CODE_DISABLE_PROVIDER</c>.</summary>
    public const uint ControlDisable = 0;
    /// <summary><c>EVENT_CONTROL_CODE_ENABLE_PROVIDER</c>.</summary>
    public const uint ControlEnable = 1;
    /// <summary><c>EVENT_CONTROL_CODE_CAPTURE_STATE</c>.</summary>
    public const uint ControlCaptureState = 2;

    /// <summary><c>EVENT_INFO_CLASS.EventProviderSetTraits</c>.</summary>
    public const int EventProviderSetTraits = 2;

    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP   = 0x0101;

    // ---- Structs ----

    /// <summary><c>EVENT_DESCRIPTOR</c>, 16 bytes.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct EventDescriptor
    {
        public ushort Id;
        public byte   Version;
        public byte   Channel;
        public byte   Level;
        public byte   Opcode;
        public ushort Task;
        public ulong  Keyword;
    }

    /// <summary><c>EVENT_DATA_DESCRIPTOR</c>, 16 bytes. <see cref="Type"/> is
    /// 2 for provider traits, 1 for event metadata, 0 for payload.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct EventDataDescriptor
    {
        public ulong  Ptr;
        public uint   Size;
        public byte   Type;
        public byte   Reserved1;
        public ushort Reserved2;
    }

    /// <summary>Every PCL event: id/version/opcode/task/keyword 0, channel 11
    /// (TraceLogging), level 5 (verbose) — what <c>TraceLoggingWrite</c> with no
    /// level argument emits.</summary>
    public static readonly EventDescriptor Descriptor = new() { Channel = 11, Level = 5 };

    // ---- Metadata blobs ----
    //
    // Each is a "…"u8 literal, which compiles to an RVA static: process
    // lifetime, never moved, so `fixed` on it costs nothing. \u escapes (not
    // \x, whose variable length would swallow following hex letters) encode the
    // non-printable bytes; every one is < 0x80, so UTF-8 encodes it as itself.
    // The one printable size byte, 0x22 (MarkerEvent), is written \".
    //
    // Provider traits: UInt16 LE total size, provider name, NUL.
    // Event metadata:  UInt16 LE total size, tag byte 0x00, event name, NUL,
    //                  then per field: field name, NUL, InType byte
    //                  (UInt32 = 0x08, UInt64 = 0x0A).

    /// <summary>Size 31: <c>"PCLStatsTraceLoggingProvider"</c>.</summary>
    public static ReadOnlySpan<byte> ProviderTraits =>
        "\u001F\u0000PCLStatsTraceLoggingProvider\u0000"u8;

    /// <summary>Size 16: <c>PCLStatsInit</c>, no fields.</summary>
    public static ReadOnlySpan<byte> InitEvent =>
        "\u0010\u0000\u0000PCLStatsInit\u0000"u8;

    /// <summary>Size 34: <c>PCLStatsEvent { Marker: UInt32, FrameID: UInt64 }</c>.</summary>
    public static ReadOnlySpan<byte> MarkerEvent =>
        "\"\u0000\u0000PCLStatsEvent\u0000Marker\u0000\u0008FrameID\u0000\u000A"u8;

    /// <summary>Size 27: <c>PCLStatsInput { IdThread: UInt32 }</c>.</summary>
    public static ReadOnlySpan<byte> InputIdThreadEvent =>
        "\u001B\u0000\u0000PCLStatsInput\u0000IdThread\u0000\u0008"u8;

    /// <summary>Size 29: <c>PCLStatsInput { VirtualKey: UInt32 }</c>.</summary>
    public static ReadOnlySpan<byte> InputVirtualKeyEvent =>
        "\u001D\u0000\u0000PCLStatsInput\u0000VirtualKey\u0000\u0008"u8;

    /// <summary>Size 24: <c>PCLStatsInput { MsgId: UInt32 }</c>.</summary>
    public static ReadOnlySpan<byte> InputMsgIdEvent =>
        "\u0018\u0000\u0000PCLStatsInput\u0000MsgId\u0000\u0008"u8;

    /// <summary>Size 24: <c>PCLStatsFlags { Flags: UInt32 }</c>.</summary>
    public static ReadOnlySpan<byte> FlagsEvent =>
        "\u0018\u0000\u0000PCLStatsFlags\u0000Flags\u0000\u0008"u8;

    /// <summary>Size 20: <c>PCLStatsShutdown</c>, no fields.</summary>
    public static ReadOnlySpan<byte> ShutdownEvent =>
        "\u0014\u0000\u0000PCLStatsShutdown\u0000"u8;

    // ---- Write ----

    /// <summary>
    /// One <c>EventWriteTransfer</c> of <paramref name="eventMetadata"/> with
    /// up to two payload fields. Allocation-free: the descriptors are on the
    /// stack and the blobs are RVA statics. Returns nothing — an ETW write
    /// failure is never the frame's problem.
    /// </summary>
    public static void Write(
        ulong regHandle, ReadOnlySpan<byte> eventMetadata,
        void* payload0, uint size0, void* payload1, uint size1)
    {
        EventDataDescriptor* data = stackalloc EventDataDescriptor[4];
        EventDescriptor d = Descriptor;
        fixed (byte* traits = ProviderTraits)
        fixed (byte* meta = eventMetadata)
        {
            data[0] = new EventDataDescriptor { Ptr = (ulong)traits, Size = (uint)ProviderTraits.Length, Type = 2 };
            data[1] = new EventDataDescriptor { Ptr = (ulong)meta,   Size = (uint)eventMetadata.Length,  Type = 1 };
            uint count = 2;
            if (payload0 != null) data[count++] = new EventDataDescriptor { Ptr = (ulong)payload0, Size = size0 };
            if (payload1 != null) data[count++] = new EventDataDescriptor { Ptr = (ulong)payload1, Size = size1 };
            _ = EventWriteTransfer(regHandle, &d, null, null, count, data);
        }
    }

    // ---- Interop (blittable; no SetLastError) ----

    [DllImport("advapi32", ExactSpelling = true)]
    public static extern uint EventRegister(
        Guid* providerId,
        delegate* unmanaged[Stdcall]<Guid*, uint, byte, ulong, ulong, void*, void*, void> callback,
        void* context,
        ulong* regHandle);

    [DllImport("advapi32", ExactSpelling = true)]
    public static extern uint EventUnregister(ulong regHandle);

    [DllImport("advapi32", ExactSpelling = true)]
    public static extern uint EventSetInformation(ulong regHandle, int informationClass, void* information, uint length);

    [DllImport("advapi32", ExactSpelling = true)]
    public static extern uint EventWriteTransfer(
        ulong regHandle, EventDescriptor* descriptor, Guid* activityId, Guid* relatedActivityId,
        uint userDataCount, EventDataDescriptor* userData);

    [DllImport("user32", ExactSpelling = true)]
    public static extern uint RegisterWindowMessageW(char* name);

    [DllImport("user32", ExactSpelling = true)]
    public static extern nint GetForegroundWindow();

    [DllImport("user32", ExactSpelling = true)]
    public static extern uint GetWindowThreadProcessId(nint hwnd, uint* processId);

    [DllImport("user32", ExactSpelling = true)]
    public static extern int PostMessageW(nint hwnd, uint msg, nuint wParam, nint lParam);

    [DllImport("user32", ExactSpelling = true)]
    public static extern int PostThreadMessageW(uint threadId, uint msg, nuint wParam, nint lParam);
}
