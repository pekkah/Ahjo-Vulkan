using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// A timestamp-typed <c>VkQueryPool</c>: bracket GPU work with
/// <see cref="CommandRecorder.WriteTimestamp"/> pairs, reset the frame's
/// query range with <see cref="CommandRecorder.ResetQueryPool"/>, and read
/// the raw ticks back with <see cref="TryGetResults(uint, Span{ulong})"/>.
/// </summary>
/// <remarks>
/// <para><b>Ownership.</b> Caller-owned, like its <c>Sync/</c> neighbour
/// <see cref="Event"/>: minted by <see cref="Device.CreateQueryPool"/> and
/// destroys the <c>VkQueryPool</c> on <see cref="Dispose"/>. There is no
/// managed pool-of-pools — query availability resolves asynchronously frames
/// later, so a pool could answer nothing the caller's own
/// <see cref="TryGetResults(uint, Span{ulong})"/> doesn't.</para>
/// <para><b>Lifetime.</b> Do not dispose while a submission that references
/// the pool is still pending
/// (<c>VUID-vkDestroyQueryPool-queryPool-00793</c>). <c>default(QueryPool)</c>
/// is a legal null handle (<see cref="IsNull"/> is <see langword="true"/>,
/// <see cref="Dispose"/> is a no-op); double-dispose is undefined behavior —
/// the standard handle contract, see <see cref="IVulkanHandle{TSelf}"/>.</para>
/// <para><b><see cref="QueryCount"/> on a borrowed handle means
/// "unknown".</b> <see cref="FromRaw"/> and <c>default</c> carry a
/// <see cref="QueryCount"/> of 0 because the wrapper never learns a borrowed
/// pool's size — read 0 as <em>unknown</em>, never as <em>an empty pool</em>
/// (an empty pool cannot be created,
/// <c>VUID-VkQueryPoolCreateInfo-queryCount-02763</c>).</para>
/// <para><b>Reset before use.</b> Queries start <em>uninitialized</em> at
/// pool creation: every query must be reset by a <b>submitted</b>
/// <see cref="CommandRecorder.ResetQueryPool"/> before its first
/// <see cref="CommandRecorder.WriteTimestamp"/> and before any readback —
/// reading a never-reset query is a validation error
/// (<c>VUID-vkGetQueryPoolResults-None-09401</c>), not a
/// <see langword="false"/>.</para>
/// <para><b>Ticks → nanoseconds.</b> The readback returns raw ticks: mask
/// each value to the writing queue family's
/// <see cref="QueueFamilyInfo.TimestampValidBits"/>, then multiply the
/// masked delta by <see cref="Device.TimestampPeriod"/>.</para>
/// </remarks>
public readonly unsafe struct QueryPool : IVulkanHandle<QueryPool>, IDisposable
{
    public readonly VkQueryPool_T* Handle;
    internal readonly VkDevice_T* DeviceHandle;
    private readonly uint _queryCount;

    internal QueryPool(VkQueryPool_T* handle, VkDevice_T* device, uint queryCount)
    {
        Handle       = handle;
        DeviceHandle = device;
        _queryCount  = queryCount;
        HandleRegistry.TrackCreate(this);
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_QUERY_POOL;
    public static QueryPool FromRaw(nint handle) => new((VkQueryPool_T*)handle, null, 0);
    public ulong RawHandle => (ulong)Handle;
    public bool IsNull => Handle == null;

    /// <inheritdoc/>
    public bool OwnsHandle => DeviceHandle != null;

    /// <summary>
    /// Number of queries in the pool, as declared at
    /// <see cref="Device.CreateQueryPool"/>. 0 for a borrowed
    /// (<see cref="FromRaw"/> / <c>default</c>) handle, where it means
    /// <em>unknown</em> rather than <em>empty</em> — an empty pool cannot be
    /// created (<c>VUID-VkQueryPoolCreateInfo-queryCount-02763</c>).
    /// </summary>
    public uint QueryCount => _queryCount;

    public void Dispose()
    {
        if (Handle == null) return;
        // FromRaw produces a borrowed handle with no DeviceHandle — the
        // caller owns the lifetime; calling vkDestroyQueryPool with a null
        // device handle would crash on every loader.
        if (!OwnsHandle) return;
        HandleRegistry.TrackDispose(this);
        Vk.vkDestroyQueryPool(DeviceHandle, Handle, null);
    }

    /// <summary>
    /// Non-blocking readback of <paramref name="results"/>.Length queries
    /// starting at <paramref name="firstQuery"/>, 64-bit values
    /// (<c>VK_QUERY_RESULT_64_BIT</c>). Returns <see langword="true"/> when
    /// every query in the range was available; <see langword="false"/>
    /// (<c>VK_NOT_READY</c>) when at least one was not.
    /// </summary>
    /// <remarks>
    /// <para><b>Never blocks.</b> On the <see langword="false"/> path the
    /// value slots of unavailable queries are <em>not written</em> — their
    /// contents are undefined. Use the
    /// <see cref="TryGetResults(uint, Span{QueryResult})"/> overload to see
    /// which entries are live.</para>
    /// <para>Every query in the range must have been reset by a
    /// <b>submitted</b> <see cref="CommandRecorder.ResetQueryPool"/> since
    /// pool creation (<c>VUID-vkGetQueryPoolResults-None-09401</c>).</para>
    /// <para>Values are raw ticks: mask to the writing queue family's
    /// <see cref="QueueFamilyInfo.TimestampValidBits"/> and multiply the
    /// masked delta by <see cref="Device.TimestampPeriod"/> for
    /// nanoseconds.</para>
    /// <para>An empty span returns <see langword="true"/> without calling
    /// the driver (<c>vkGetQueryPoolResults</c> requires
    /// <c>dataSize &gt; 0</c>,
    /// <c>VUID-vkGetQueryPoolResults-dataSize-arraylength</c>).</para>
    /// </remarks>
    public bool TryGetResults(uint firstQuery, Span<ulong> results)
    {
        if (results.IsEmpty) return true;
        AssertRangeInBounds("TryGetResults", firstQuery, (uint)results.Length);
        ThrowIfBorrowed();
        VkResult result;
        fixed (ulong* p = results)
        {
            result = Vk.vkGetQueryPoolResults(
                DeviceHandle, Handle, firstQuery, (uint)results.Length,
                (nuint)results.Length * 8, p, stride: 8,
                (uint)VkQueryResultFlagBits.VK_QUERY_RESULT_64_BIT);
        }
        result.ThrowIfErrored();
        return result != VkResult.VK_NOT_READY;
    }

    /// <summary>
    /// Non-blocking readback with per-query availability
    /// (<c>VK_QUERY_RESULT_64_BIT | VK_QUERY_RESULT_WITH_AVAILABILITY_BIT</c>).
    /// Returns <see langword="true"/> when every query in the range was
    /// available; <see langword="false"/> when at least one was not.
    /// </summary>
    /// <remarks>
    /// <para><b>Never blocks.</b> Unlike the <c>Span&lt;ulong&gt;</c>
    /// overload, the driver writes <see cref="QueryResult.Availability"/>
    /// for <em>every</em> query in the range even on the
    /// <see langword="false"/> path, so the caller can see which entries are
    /// live. <see cref="QueryResult.Value"/> stays undefined for unavailable
    /// queries.</para>
    /// <para>Every query in the range must have been reset by a
    /// <b>submitted</b> <see cref="CommandRecorder.ResetQueryPool"/> since
    /// pool creation (<c>VUID-vkGetQueryPoolResults-None-09401</c>).</para>
    /// <para>An empty span returns <see langword="true"/> without calling
    /// the driver
    /// (<c>VUID-vkGetQueryPoolResults-dataSize-arraylength</c>).</para>
    /// </remarks>
    public bool TryGetResults(uint firstQuery, Span<QueryResult> results)
    {
        if (results.IsEmpty) return true;
        AssertRangeInBounds("TryGetResults", firstQuery, (uint)results.Length);
        ThrowIfBorrowed();
        VkResult result;
        fixed (QueryResult* p = results)
        {
            result = Vk.vkGetQueryPoolResults(
                DeviceHandle, Handle, firstQuery, (uint)results.Length,
                (nuint)results.Length * 16, p, stride: 16,
                (uint)VkQueryResultFlagBits.VK_QUERY_RESULT_64_BIT
                | (uint)VkQueryResultFlagBits.VK_QUERY_RESULT_WITH_AVAILABILITY_BIT);
        }
        result.ThrowIfErrored();
        return result != VkResult.VK_NOT_READY;
    }

    /// <summary>
    /// Blocking readback
    /// (<c>VK_QUERY_RESULT_64_BIT | VK_QUERY_RESULT_WAIT_BIT</c>): waits
    /// until every query in the range is available and writes all values.
    /// </summary>
    /// <remarks>
    /// <para><b>Can wait forever.</b> A query that was reset but whose
    /// <see cref="CommandRecorder.WriteTimestamp"/> never got submitted will
    /// never become available, and the wrapper cannot see submission state —
    /// this is the debug/teardown tier, never the per-frame path. Use
    /// <see cref="TryGetResults(uint, Span{ulong})"/> per frame.</para>
    /// <para>Every query in the range must have been reset by a
    /// <b>submitted</b> <see cref="CommandRecorder.ResetQueryPool"/> since
    /// pool creation (<c>VUID-vkGetQueryPoolResults-None-09401</c>).</para>
    /// <para>An empty span returns without calling the driver
    /// (<c>VUID-vkGetQueryPoolResults-dataSize-arraylength</c>).</para>
    /// </remarks>
    public void GetResults(uint firstQuery, Span<ulong> results)
    {
        if (results.IsEmpty) return;
        AssertRangeInBounds("GetResults", firstQuery, (uint)results.Length);
        ThrowIfBorrowed();
        VkResult result;
        fixed (ulong* p = results)
        {
            result = Vk.vkGetQueryPoolResults(
                DeviceHandle, Handle, firstQuery, (uint)results.Length,
                (nuint)results.Length * 8, p, stride: 8,
                (uint)VkQueryResultFlagBits.VK_QUERY_RESULT_64_BIT
                | (uint)VkQueryResultFlagBits.VK_QUERY_RESULT_WAIT_BIT);
        }
        result.ThrowIfErrored();
    }

    // FromRaw produces a borrowed pool with no DeviceHandle; dispatching
    // through it would dereference the loader's null dispatch table and
    // access-violate the process. Fail loudly instead (the Fence pattern).
    private void ThrowIfBorrowed()
    {
        if (DeviceHandle == null)
            throw new InvalidOperationException(
                "QueryPool requires an owning device for result readback; " +
                "a FromRaw-constructed (borrowed) pool has none.");
    }

    // Validation-gated bounds check against the declared pool size
    // (VUID-vkGetQueryPoolResults-firstQuery-09436 / -09437). A borrowed
    // pool (_queryCount == 0) skips it — unknown is not enforceable.
    private void AssertRangeInBounds(string caller, uint firstQuery, uint count)
    {
        if (!AhjoValidation.IsEnabled) return;
        // Widened to ulong before adding: uint arithmetic would wrap
        // (e.g. firstQuery = 0xFFFF_FFFE, count = 4 → 2) and let an
        // out-of-range readback slip past the guard.
        if (_queryCount != 0 && (ulong)firstQuery + count > _queryCount)
            AhjoValidation.Fail("QueryPool",
                $"{caller}: range [{firstQuery}, {(ulong)firstQuery + count}) exceeds the pool's "
                + $"QueryCount ({_queryCount}) "
                + "(VUID-vkGetQueryPoolResults-firstQuery-09436/-09437).");
    }
}
