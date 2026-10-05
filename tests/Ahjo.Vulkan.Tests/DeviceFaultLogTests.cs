using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Driverless coverage of <c>Internal/DeviceFaultLog</c> (#244): the bounded
/// ring every drained KHR fault entry is kept in — order, overflow, the sticky
/// <c>Dropped</c> flag, snapshot copies and lazy storage.
/// </summary>
/// <remarks>No <c>TestGate</c>: nothing here reaches a driver.</remarks>
public sealed class DeviceFaultLogTests
{
    private static DeviceFaultEntry Entry(ulong groupId) => new() { GroupId = groupId };

    private static DeviceFaultEntry[] Entries(ulong first, int count)
    {
        var entries = new DeviceFaultEntry[count];
        for (int i = 0; i < count; i++)
            entries[i] = Entry(first + (ulong)i);
        return entries;
    }

    private static ulong[] GroupIds(DeviceFaultEntry[] entries)
    {
        var ids = new ulong[entries.Length];
        for (int i = 0; i < entries.Length; i++)
            ids[i] = entries[i].GroupId;
        return ids;
    }

    [Fact]
    public void Empty_SnapshotIsEmpty_NotDropped_NoStorage()
    {
        var log = new DeviceFaultLog();

        // Warm: the first call JITs Append.
        log.Append([]);
        long before = GC.GetAllocatedBytesForCurrentThread();
        log.Append(ReadOnlySpan<DeviceFaultEntry>.Empty);
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
        Assert.Equal(0, log.Count);
        Assert.False(log.Dropped);
        DeviceFaultEntry[] snapshot = log.Snapshot();
        Assert.Empty(snapshot);
        Assert.Same(Array.Empty<DeviceFaultEntry>(), snapshot);
    }

    [Fact]
    public void Append_PreservesOrder_AcrossCalls()
    {
        var log = new DeviceFaultLog();

        log.Append(Entries(1, 2));
        log.Append(Entries(3, 1));
        log.Append(Entries(4, 3));

        Assert.Equal(6, log.Count);
        Assert.Equal([1UL, 2, 3, 4, 5, 6], GroupIds(log.Snapshot()));
        Assert.False(log.Dropped);
    }

    [Fact]
    public void Append_EmptySpan_IsNoOp()
    {
        var log = new DeviceFaultLog();
        log.Append(Entries(1, 2));

        log.Append([]);

        Assert.Equal(2, log.Count);
        Assert.Equal([1UL, 2], GroupIds(log.Snapshot()));
        Assert.False(log.Dropped);
    }

    [Fact]
    public void Overflow_DropsOldest_SetsDropped()
    {
        var log = new DeviceFaultLog();
        int total = DeviceFaultLog.Capacity + 3;

        // Several calls of uneven size so the wrap point falls mid-append.
        log.Append(Entries(0, 500));
        log.Append(Entries(500, 500));
        log.Append(Entries(1000, total - 1000));

        Assert.True(log.Dropped);
        Assert.Equal(DeviceFaultLog.Capacity, log.Count);
        ulong[] ids = GroupIds(log.Snapshot());
        Assert.Equal(DeviceFaultLog.Capacity, ids.Length);
        for (int i = 0; i < ids.Length; i++)
            Assert.Equal((ulong)(i + 3), ids[i]);
    }

    [Fact]
    public void SingleAppendLargerThanCapacity_KeepsTail_SetsDropped()
    {
        var log = new DeviceFaultLog();
        int total = DeviceFaultLog.Capacity + 10;

        log.Append(Entries(0, total));

        Assert.True(log.Dropped);
        Assert.Equal(DeviceFaultLog.Capacity, log.Count);
        ulong[] ids = GroupIds(log.Snapshot());
        for (int i = 0; i < ids.Length; i++)
            Assert.Equal((ulong)(i + 10), ids[i]);
    }

    [Fact]
    public void Snapshot_IsACopy()
    {
        var log = new DeviceFaultLog();
        log.Append(Entries(1, 3));

        DeviceFaultEntry[] first = log.Snapshot();
        first[0] = Entry(99);
        DeviceFaultEntry[] second = log.Snapshot();

        Assert.NotSame(first, second);
        Assert.Equal([1UL, 2, 3], GroupIds(second));
    }

    [Fact]
    public void Dropped_IsSticky()
    {
        var log = new DeviceFaultLog();
        log.Append(Entries(0, DeviceFaultLog.Capacity + 1));
        Assert.True(log.Dropped);

        // Further appends, including empty ones, never clear it.
        log.Append([]);
        log.Append(Entries(5000, 1));
        _ = log.Snapshot();

        Assert.True(log.Dropped);
    }
}
