namespace Ahjo.Vulkan;

/// <summary>
/// Strongly-typed shadow of <c>VkCopyAccelerationStructureModeKHR</c>, as
/// accepted by <see cref="CommandRecorder.CopyAccelerationStructure"/>.
/// </summary>
/// <remarks>
/// Only the two modes <c>vkCmdCopyAccelerationStructureKHR</c> accepts are
/// listed — <c>VUID-VkCopyAccelerationStructureInfoKHR-mode-03410</c> requires
/// <c>mode</c> to be one of exactly these two. The native enum's
/// <c>SERIALIZE</c> / <c>DESERIALIZE</c> members belong to
/// <c>vkCmdCopyAccelerationStructureToMemoryKHR</c> /
/// <c>vkCmdCopyMemoryToAccelerationStructureKHR</c>, which this surface
/// deliberately does not wrap: acceleration-structure serialization is a
/// disk-cache feature with its own versioning-compatibility handshake
/// (<c>vkGetDeviceAccelerationStructureCompatibilityKHR</c>) and belongs in its
/// own cut. Adding the members later is additive — they keep native values.
/// </remarks>
public enum AccelerationStructureCopyMode
{
    /// <summary>
    /// <c>VK_COPY_ACCELERATION_STRUCTURE_MODE_CLONE_KHR</c> — a byte-for-byte
    /// copy. The destination must have been created over a range at least as
    /// large as the source's.
    /// </summary>
    Clone = 0,

    /// <summary>
    /// <c>VK_COPY_ACCELERATION_STRUCTURE_MODE_COMPACT_KHR</c> — copy into a
    /// smaller destination sized by the compacted-size query. The source must
    /// have been built with
    /// <see cref="AccelerationStructureBuildFlags.AllowCompaction"/>
    /// (<c>VUID-VkCopyAccelerationStructureInfoKHR-src-03411</c>), and the
    /// destination must have been created over a range of exactly the size
    /// read back after
    /// <see cref="CommandRecorder.WriteAccelerationStructuresProperties"/>.
    /// </summary>
    Compact = 1,
}
