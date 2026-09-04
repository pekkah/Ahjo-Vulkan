using Ahjo.Vulkan.Ngx.Native;

namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// Turns an <see cref="NVSDK_NGX_Result"/> into a verdict, a sentence, or an
/// exception.
/// </summary>
internal static unsafe class NgxResult
{
    private const int DescriptionBufferBytes = 128;

    /// <summary>
    /// NGX's success value is <c>0x1</c>, not zero — a <c>result == 0</c> test
    /// would call every success a failure and every failure a success. This
    /// method exists so that mistake has one place to not happen.
    /// </summary>
    internal static bool Succeeded(NVSDK_NGX_Result result)
        => result == NVSDK_NGX_Result.NVSDK_NGX_Result_Success;

    /// <summary>
    /// NGX's own text for <paramref name="result"/>, via the shim's
    /// caller-buffer API (#216 D2) so the common case formats out of a
    /// <c>stackalloc</c>.
    /// </summary>
    internal static string Describe(NVSDK_NGX_Result result)
    {
        Span<byte> buffer = stackalloc byte[DescriptionBufferBytes];
        uint required;
        fixed (byte* p = buffer)
            required = NgxApi.ahjo_ngx_result_to_utf8(result, (sbyte*)p, DescriptionBufferBytes);

        if (required == 0)
            return "(no description)";

        // The shim returns the byte count it needed. Longer than the stack
        // buffer is not expected for any current result, but the contract
        // allows it, so honour it once rather than truncating silently.
        if (required > DescriptionBufferBytes)
        {
            byte[] larger = new byte[required];
            fixed (byte* p = larger)
                NgxApi.ahjo_ngx_result_to_utf8(result, (sbyte*)p, required);
            return NgxUtf8.ToString(larger, larger.Length);
        }

        return NgxUtf8.ToString(buffer, DescriptionBufferBytes);
    }

    /// <summary>
    /// Throws <see cref="NgxException"/> when <paramref name="result"/> is not
    /// <c>Success</c>. The message names the operation, the result symbolically
    /// and numerically, and NGX's own description of it.
    /// </summary>
    internal static void ThrowIfFailed(NVSDK_NGX_Result result, string operation)
    {
        if (Succeeded(result)) return;
        throw new NgxException(result, Format(result, operation));
    }

    /// <summary>
    /// The standard message shape, shared with the callers that build a more
    /// specific exception type.
    /// </summary>
    internal static string Format(NVSDK_NGX_Result result, string operation)
        => $"{operation} failed: {result} (0x{(uint)result:X8}) — {Describe(result)}";
}
