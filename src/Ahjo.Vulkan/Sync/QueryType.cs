namespace Ahjo.Vulkan;

/// <summary>
/// Strongly-typed shadow of the <c>VkQueryType</c> values
/// <see cref="Device.CreateQueryPool(QueryType, uint)"/> can mint, carried on
/// <see cref="QueryPool.Type"/> so a recorded query command never has to be
/// told a type the pool already knows.
/// </summary>
/// <remarks>
/// <para><b>Why <see cref="Unknown"/> = 0 is safe rather than a fudge.</b>
/// <c>VkQueryType</c> 0 is <c>VK_QUERY_TYPE_OCCLUSION</c> — a type this
/// wrapper does not create and has no surface for. That leaves 0 free to carry
/// the "borrowed handle, type not known" meaning, exactly as
/// <see cref="QueryPool.QueryCount"/>'s 0 already carries "size not known".
/// The other members keep native values, so the cast to <c>VkQueryType</c> is
/// free and a drift test pins both.</para>
/// <para>Adding members is <b>additive</b>: a future occlusion or
/// pipeline-statistics surface takes the native value and needs no renumbering
/// here. Should occlusion queries ever be wrapped, <see cref="Unknown"/> would
/// need renaming — that is the one cost of this choice, and it is a compile
/// error at every call site rather than a silent change.</para>
/// </remarks>
public enum QueryType
{
    /// <summary>
    /// Not a creatable type: the sentinel a borrowed pool
    /// (<see cref="QueryPool.FromRaw"/> or <c>default</c>) reports, meaning the
    /// wrapper never learned the pool's type. Passing it to
    /// <see cref="Device.CreateQueryPool(QueryType, uint)"/> throws.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// <c>VK_QUERY_TYPE_TIMESTAMP</c> — each result is a raw tick count written
    /// by <see cref="CommandRecorder.WriteTimestamp"/>. What
    /// <see cref="Device.CreateQueryPool(uint)"/> mints.
    /// </summary>
    Timestamp = 2,

    /// <summary>
    /// <c>VK_QUERY_TYPE_ACCELERATION_STRUCTURE_COMPACTED_SIZE_KHR</c> — each
    /// result is a size in <b>bytes</b>, written by
    /// <see cref="CommandRecorder.WriteAccelerationStructuresProperties"/> for
    /// an acceleration structure built with
    /// <see cref="AccelerationStructureBuildFlags.AllowCompaction"/>. Requires
    /// <c>VK_KHR_acceleration_structure</c>.
    /// </summary>
    AccelerationStructureCompactedSize = 1000150000,
}
