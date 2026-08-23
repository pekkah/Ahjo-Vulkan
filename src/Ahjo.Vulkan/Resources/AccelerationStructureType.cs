namespace Ahjo.Vulkan;

/// <summary>
/// Strongly-typed shadow of <c>VkAccelerationStructureTypeKHR</c>: what an
/// <see cref="AccelerationStructure"/> holds, fixed at
/// <see cref="Device.CreateAccelerationStructure"/> and repeated on every
/// <see cref="AccelerationStructureBuild.Type"/> that targets it.
/// </summary>
/// <remarks>
/// <para><b>The footgun: <see cref="TopLevel"/> is 0.</b> The members keep
/// Vulkan's native numbering, and Vulkan numbers top level first — so
/// <c>default(AccelerationStructureType)</c> is <see cref="TopLevel"/>, and a
/// default-initialized <see cref="AccelerationStructureBuild"/> builds a
/// <b>TLAS</b>. A BLAS build must set
/// <see cref="AccelerationStructureBuild.Type"/> to
/// <see cref="BottomLevel"/> explicitly; forgetting to is the mistake this
/// numbering invites. The geometry-kind guard in
/// <see cref="CommandRecorder.BuildAccelerationStructures"/> exists to catch
/// exactly that omission — a build left at <see cref="TopLevel"/> with
/// triangle geometry fails the guard naming the default value
/// (<c>VUID-VkAccelerationStructureBuildGeometryInfoKHR-type-03789</c>).</para>
/// <para>Renumbering the shadow away from native values to dodge the footgun
/// was considered and rejected: it would break the repo-wide shadow-enum
/// convention and its drift tests to fix one ergonomic wart, and the cast to
/// <c>VkAccelerationStructureTypeKHR</c> would stop being free.</para>
/// </remarks>
public enum AccelerationStructureType
{
    /// <summary>
    /// <c>VK_ACCELERATION_STRUCTURE_TYPE_TOP_LEVEL_KHR</c> — a TLAS: one
    /// <see cref="AccelerationStructureGeometry.Instances"/> geometry whose
    /// elements reference bottom-level structures by device address. This is
    /// the <b>default</b> value of the enum; see the type's remarks.
    /// </summary>
    TopLevel = 0,

    /// <summary>
    /// <c>VK_ACCELERATION_STRUCTURE_TYPE_BOTTOM_LEVEL_KHR</c> — a BLAS over
    /// triangle or AABB geometry. Must be set explicitly; it is not the
    /// default.
    /// </summary>
    BottomLevel = 1,

    /// <summary>
    /// <c>VK_ACCELERATION_STRUCTURE_TYPE_GENERIC_KHR</c> — a structure whose
    /// top/bottom nature is decided at build time rather than creation time.
    /// Intended for API-layering tools; a ray-query consumer wants
    /// <see cref="TopLevel"/> or <see cref="BottomLevel"/>.
    /// </summary>
    Generic = 2,
}
