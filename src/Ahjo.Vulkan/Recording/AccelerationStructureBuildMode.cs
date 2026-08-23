namespace Ahjo.Vulkan;

/// <summary>
/// Strongly-typed shadow of <c>VkBuildAccelerationStructureModeKHR</c>:
/// whether an <see cref="AccelerationStructureBuild"/> builds its destination
/// from scratch or refits it from an existing source.
/// </summary>
public enum AccelerationStructureBuildMode
{
    /// <summary>
    /// <c>VK_BUILD_ACCELERATION_STRUCTURE_MODE_BUILD_KHR</c> — build the
    /// destination from the supplied geometry.
    /// <see cref="AccelerationStructureBuild.Source"/> must be null, and the
    /// scratch range is sized by
    /// <see cref="AccelerationStructureBuildSizes.BuildScratchSize"/>. The
    /// default value.
    /// </summary>
    Build = 0,

    /// <summary>
    /// <c>VK_BUILD_ACCELERATION_STRUCTURE_MODE_UPDATE_KHR</c> — refit the
    /// destination from <see cref="AccelerationStructureBuild.Source"/>, which
    /// must be non-null
    /// (<c>VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-04630</c>) and must
    /// have been built with
    /// <see cref="AccelerationStructureBuildFlags.AllowUpdate"/>
    /// (<c>-pInfos-03667</c>). The scratch range is sized by
    /// <see cref="AccelerationStructureBuildSizes.UpdateScratchSize"/>.
    /// </summary>
    /// <remarks>
    /// An update may move vertices but must not change topology: the geometry
    /// count, the flags, the type, each geometry's kind and every
    /// <see cref="AccelerationStructureBuildRange.PrimitiveCount"/> must match
    /// the source's last build (<c>-pInfos-03758</c> … <c>-03762</c>,
    /// <c>-primitiveCount-03769</c>). Source and destination may be the same
    /// structure — the in-place refit — or must not alias at all
    /// (<c>-pInfos-03668</c>).
    /// </remarks>
    Update = 1,
}
