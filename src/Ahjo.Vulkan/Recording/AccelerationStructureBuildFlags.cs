namespace Ahjo.Vulkan;

/// <summary>
/// Strongly-typed shadow of <c>VkBuildAccelerationStructureFlagBitsKHR</c> —
/// the build-time trade-offs an acceleration structure is built with, carried
/// on <see cref="AccelerationStructureBuild.Flags"/> and on the matching
/// <see cref="Device.GetAccelerationStructureBuildSizes"/> query.
/// </summary>
/// <remarks>
/// <para><b>Flags are part of a structure's identity, not just its build.</b>
/// Two of them are permissions that must be requested up front or the later
/// operation is invalid: <see cref="AllowUpdate"/> is required before any
/// <see cref="AccelerationStructureBuildMode.Update"/> against the structure
/// (<c>VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03667</c>), and
/// <see cref="AllowCompaction"/> is required before both the compacted-size
/// query
/// (<c>VUID-vkCmdWriteAccelerationStructuresPropertiesKHR-accelerationStructures-03431</c>)
/// and the compaction copy
/// (<c>VUID-VkCopyAccelerationStructureInfoKHR-src-03411</c>). Neither can be
/// added after the fact — the structure has to be rebuilt.</para>
/// <para><b>Pass the same flags to the size query.</b>
/// <see cref="Device.GetAccelerationStructureBuildSizes"/> takes these because
/// they change the sizes the driver reports; sizing with one set and building
/// with another gives a scratch or backing range that is too small.</para>
/// <para><see cref="PreferFastTrace"/> and <see cref="PreferFastBuild"/> are
/// not mutually exclusive to Vulkan, but they are in practice: they ask the
/// driver for opposite things, and setting both means the driver picks. Set
/// one. The usual split is <see cref="PreferFastTrace"/> for static BLASes
/// built once at load, <see cref="PreferFastBuild"/> for a TLAS rebuilt every
/// frame.</para>
/// </remarks>
[Flags]
public enum AccelerationStructureBuildFlags : uint
{
    /// <summary>No flags. The driver picks its own trade-off, and neither
    /// update nor compaction is permitted against the result.</summary>
    None = 0,

    /// <summary>
    /// <c>VK_BUILD_ACCELERATION_STRUCTURE_ALLOW_UPDATE_BIT_KHR</c> — permits a
    /// later <see cref="AccelerationStructureBuildMode.Update"/> against this
    /// structure. Required for the per-frame TLAS-refit pattern; without it an
    /// update is a validation error, not a slow path.
    /// </summary>
    AllowUpdate = 0x1,

    /// <summary>
    /// <c>VK_BUILD_ACCELERATION_STRUCTURE_ALLOW_COMPACTION_BIT_KHR</c> —
    /// permits
    /// <see cref="CommandRecorder.WriteAccelerationStructuresProperties"/>
    /// with a <see cref="QueryType.AccelerationStructureCompactedSize"/> pool
    /// and the subsequent <see cref="AccelerationStructureCopyMode.Compact"/>
    /// copy. Costs a little build memory; typically set on load-time BLASes
    /// that are then compacted once.
    /// </summary>
    AllowCompaction = 0x2,

    /// <summary>
    /// <c>VK_BUILD_ACCELERATION_STRUCTURE_PREFER_FAST_TRACE_BIT_KHR</c> —
    /// spend more build time for faster traversal. The usual choice for a
    /// static BLAS. Do not combine with <see cref="PreferFastBuild"/>.
    /// </summary>
    PreferFastTrace = 0x4,

    /// <summary>
    /// <c>VK_BUILD_ACCELERATION_STRUCTURE_PREFER_FAST_BUILD_BIT_KHR</c> —
    /// spend less build time at the cost of traversal speed. The usual choice
    /// for a TLAS rebuilt every frame. Do not combine with
    /// <see cref="PreferFastTrace"/>.
    /// </summary>
    PreferFastBuild = 0x8,

    /// <summary>
    /// <c>VK_BUILD_ACCELERATION_STRUCTURE_LOW_MEMORY_BIT_KHR</c> — minimize
    /// the memory the structure and its scratch consume, at the cost of both
    /// build and traversal time.
    /// </summary>
    LowMemory = 0x10,
}
