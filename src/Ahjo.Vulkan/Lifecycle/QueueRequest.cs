namespace Ahjo.Vulkan;

/// <summary>
/// Single-priority queue creation request handed to
/// <see cref="DeviceDescription.Queues"/>. The constructor validates
/// <paramref name="count"/> &gt; 0 and <paramref name="priority"/> in
/// <c>[0, 1]</c> so misuse fails at construction rather than as a Vulkan
/// validation-layer warning at <c>vkCreateDevice</c>.
/// </summary>
/// <remarks>
/// The single-priority shape covers the 80% case (one priority broadcast
/// to every queue in the family). Per-queue distinct priorities — rare,
/// e.g. one realtime and one background queue in the same family — are a
/// non-breaking future addition: a second constructor that accepts
/// <c>ReadOnlySpan&lt;float&gt;</c>.
/// </remarks>
public readonly record struct QueueRequest
{
    public uint  FamilyIndex { get; }
    public uint  Count       { get; }
    public float Priority    { get; }

    public QueueRequest(uint familyIndex, uint count, float priority)
    {
        if (count == 0)
            throw new ArgumentException("QueueRequest.Count must be > 0.", nameof(count));
        if (priority < 0f || priority > 1f || float.IsNaN(priority))
            throw new ArgumentException(
                "QueueRequest.Priority must be in [0, 1] (Vulkan VUID-VkDeviceQueueCreateInfo-pQueuePriorities-00383).",
                nameof(priority));

        FamilyIndex = familyIndex;
        Count       = count;
        Priority    = priority;
    }
}
