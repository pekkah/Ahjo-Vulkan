namespace Ahjo.Vulkan;

/// <summary>
/// The result of <see cref="Device.TryGetDeviceFault(out DeviceFaultReport)"/>:
/// why a lost device was lost, as far as the driver can say.
/// </summary>
/// <remarks>
/// <para>Publicly constructible, so a consumer's formatter tests can hand-build
/// one.</para>
/// <para><b>Entries.</b> An EXT read always yields exactly one entry. A KHR
/// read yields zero or more, in order of occurrence.</para>
/// <para><b>No formatter.</b> No <c>ToString</c> override is provided; the
/// consumer owns presentation.</para>
/// </remarks>
public sealed class DeviceFaultReport
{
    /// <summary>The path the read took.</summary>
    public DeviceFaultApi Api { get; init; }

    /// <summary>The faults, one per entry. Never null.</summary>
    public DeviceFaultEntry[] Entries { get; init; } = [];

    /// <summary>
    /// The vendor's opaque binary crash dump. Never null; empty unless the
    /// driver produced one, which normally needs the
    /// <c>deviceFaultVendorBinary</c> feature.
    /// </summary>
    /// <remarks>Begins with the 56-byte
    /// <c>VkDeviceFaultVendorBinaryHeaderVersionOneKHR</c>; parsing the rest is
    /// for vendor tools.</remarks>
    public byte[] VendorBinary { get; init; } = [];

    /// <summary>
    /// Payloads of <c>OpAbortKHR</c> instructions, in driver order; the first
    /// is the first abort executed. Never null; empty on the EXT path, when
    /// <c>VK_KHR_shader_abort</c> was not enabled, or when no shader aborted.
    /// </summary>
    /// <remarks>
    /// <para>Each payload is the shader's message type laid out verbatim.
    /// Vulkan performs no formatting, so the shading language defines the
    /// layout. The wrapper parses only the spec-defined (size, payload)
    /// framing around the payloads.</para>
    /// <para>Slang v2026.19's <c>abort(format, args…)</c> was observed to emit
    /// a NUL-terminated UTF-8 format string padded to a 4-byte boundary,
    /// followed by each scalar argument in scalar layout. That is an
    /// observation, not a contract. <c>Ahjo.Vulkan.Slang.SlangAbortMessage</c>
    /// decodes such payloads.</para>
    /// </remarks>
    public byte[][] ShaderAbortMessages { get; init; } = [];

    /// <summary>
    /// <see langword="true"/> when the driver had more than was returned: an
    /// EXT or KHR debug-info <c>VK_INCOMPLETE</c>, a KHR drain cap reached
    /// while more entries were queued, a KHR drain round that failed after
    /// some entries had already been drained, a fault log that dropped its
    /// oldest entries, or a shader-abort message buffer that was capped,
    /// truncated, or malformed.
    /// </summary>
    public bool IsIncomplete { get; init; }
}
