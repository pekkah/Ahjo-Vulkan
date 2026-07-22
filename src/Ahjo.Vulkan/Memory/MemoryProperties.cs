namespace Ahjo.Vulkan;

/// <summary>
/// Strongly-typed shadow of <c>VkMemoryPropertyFlagBits</c> — what a memory type offers.
/// Bit values match the underlying enum; the cast to <c>VkMemoryPropertyFlags</c> (a plain
/// <c>uint</c> in the bindings) is a no-op.
/// </summary>
/// <remarks>
/// Used to state memory requirements a <see cref="MemoryUsage"/> hint cannot express.
/// <see cref="Allocator.AllocateMemory"/> needs it: VMA's <c>Auto*</c> hints derive the
/// memory type from the RESOURCE's usage flags, and a block allocated before any resource
/// exists has none to derive from — so the caller states the properties itself.
/// </remarks>
[Flags]
public enum MemoryProperties : uint
{
    /// <summary>No requirement.</summary>
    None = 0,

    /// <summary>Most efficient for device access. What GPU-resident resources want.</summary>
    DeviceLocal = 0x00000001,

    /// <summary>Mappable by the host with <c>vkMapMemory</c>.</summary>
    HostVisible = 0x00000002,

    /// <summary>Host and device see each other's writes without explicit flush/invalidate.</summary>
    HostCoherent = 0x00000004,

    /// <summary>Host-cached: faster host reads, and non-coherent unless also
    /// <see cref="HostCoherent"/>.</summary>
    HostCached = 0x00000008,

    /// <summary>Backing memory may be allocated on demand — transient attachments only.</summary>
    LazilyAllocated = 0x00000010,

    /// <summary>Protected-content memory.</summary>
    Protected = 0x00000020,
}
