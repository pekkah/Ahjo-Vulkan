namespace Ahjo.Vulkan;

/// <summary>
/// Bounded ring of every KHR fault entry the wrapper has drained for one
/// device (#244). Not thread-safe: <see cref="Device"/> touches it only under
/// its fault-read lock.
/// </summary>
/// <remarks>
/// <para>Entries are shared by reference with the
/// <see cref="Device.TryPollDeviceFaults"/> return value that drained them and
/// with every later <see cref="DeviceFaultReport"/>; nothing is copied. A
/// consumer treats their arrays as read-only.</para>
/// <para>The backing array is allocated on the first non-empty
/// <see cref="Append"/>, at <see cref="Capacity"/>, so a device that never
/// sees a fault never allocates it. When an append overflows the ring, the
/// oldest entries are overwritten and <see cref="Dropped"/> is set for
/// good.</para>
/// </remarks>
internal sealed class DeviceFaultLog
{
    /// <summary>The ring's size: the same number as
    /// <see cref="DeviceFaultReader.MaxKhrEntries"/>, so one full crash drain
    /// always fits.</summary>
    internal const int Capacity = DeviceFaultReader.MaxKhrEntries;

    private DeviceFaultEntry[]? _ring;
    private int                 _start;

    /// <summary>Sticky: set once any entry was overwritten.</summary>
    public bool Dropped { get; private set; }

    /// <summary>The number of entries held, at most <see cref="Capacity"/>.</summary>
    public int Count { get; private set; }

    /// <summary>Appends <paramref name="entries"/> in order. An empty span is
    /// a no-op and allocates nothing.</summary>
    public void Append(ReadOnlySpan<DeviceFaultEntry> entries)
    {
        if (entries.IsEmpty) return;

        // Only the last Capacity elements of an oversized append can survive.
        if (entries.Length > Capacity)
        {
            entries = entries[^Capacity..];
            Dropped = true;
        }

        _ring ??= new DeviceFaultEntry[Capacity];
        foreach (DeviceFaultEntry entry in entries)
        {
            if (Count == Capacity)
            {
                // Full: overwrite the oldest slot and advance the start.
                _ring[_start] = entry;
                _start        = (_start + 1) % Capacity;
                Dropped       = true;
            }
            else
            {
                _ring[(_start + Count) % Capacity] = entry;
                Count++;
            }
        }
    }

    /// <summary>The entries held, oldest first, as a new array; the shared
    /// empty array when <see cref="Count"/> is 0.</summary>
    public DeviceFaultEntry[] Snapshot()
    {
        if (Count == 0 || _ring is null) return [];

        var copy = new DeviceFaultEntry[Count];
        for (int i = 0; i < Count; i++)
            copy[i] = _ring[(_start + i) % Capacity];
        return copy;
    }
}
