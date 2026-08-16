namespace Ahjo.Vulkan;

/// <summary>
/// One query's slot from the availability-reporting readback overload
/// <see cref="QueryPool.TryGetResults(uint, Span{QueryResult})"/>: the
/// 64-bit value followed by the 64-bit availability integer.
/// </summary>
/// <remarks>
/// <para><b>Field order is load-bearing.</b> <see cref="Value"/> first,
/// <see cref="Availability"/> second — 16 bytes total — is exactly the
/// layout <c>vkGetQueryPoolResults</c> writes with
/// <c>VK_QUERY_RESULT_64_BIT | VK_QUERY_RESULT_WITH_AVAILABILITY_BIT</c> and
/// <c>stride = 16</c>: the spec places the availability integer as the last
/// element of each query's slot. The driver writes through a pointer fixed
/// over the caller's span; two <c>ulong</c> fields cannot be reordered or
/// padded, so no explicit <c>[StructLayout]</c> is needed.</para>
/// <para><see cref="Value"/> is meaningful only when
/// <see cref="IsAvailable"/> is <see langword="true"/> — for an unavailable
/// query the driver skips the value slot and its contents are
/// undefined.</para>
/// </remarks>
public readonly struct QueryResult
{
    /// <summary>
    /// The query's 64-bit result (raw timestamp ticks for a timestamp
    /// query). Undefined when <see cref="IsAvailable"/> is
    /// <see langword="false"/>.
    /// </summary>
    public readonly ulong Value;

    /// <summary>
    /// The raw availability integer the driver wrote: non-zero means the
    /// query result is available. Prefer <see cref="IsAvailable"/>.
    /// </summary>
    public readonly ulong Availability;

    /// <summary>
    /// <see langword="true"/> when the query's result was available at
    /// readback time, i.e. <see cref="Value"/> is meaningful.
    /// </summary>
    public bool IsAvailable => Availability != 0;
}
