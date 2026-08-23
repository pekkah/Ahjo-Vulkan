namespace Ahjo.Vulkan;

/// <summary>
/// Strongly-typed shadow of <c>VkGeometryTypeKHR</c> — which of the three
/// shapes an <see cref="AccelerationStructureGeometry"/> describes, and
/// therefore how its address and stride members are read.
/// </summary>
/// <remarks>
/// A sibling enum rather than one nested in
/// <see cref="AccelerationStructureGeometry"/> so that the enum members do not
/// collide with the identically named static factories
/// (<see cref="AccelerationStructureGeometry.Triangles"/> and friends).
/// </remarks>
public enum GeometryKind
{
    /// <summary><c>VK_GEOMETRY_TYPE_TRIANGLES_KHR</c> — indexed or non-indexed
    /// triangle soup. Bottom level only.</summary>
    Triangles = 0,

    /// <summary><c>VK_GEOMETRY_TYPE_AABBS_KHR</c> — axis-aligned bounding boxes
    /// for procedural geometry. Bottom level only.</summary>
    Aabbs = 1,

    /// <summary><c>VK_GEOMETRY_TYPE_INSTANCES_KHR</c> — references to
    /// bottom-level structures. Top level only, and a top-level build must have
    /// exactly one geometry of this kind
    /// (<c>VUID-VkAccelerationStructureBuildGeometryInfoKHR-type-03789</c> /
    /// <c>-03790</c>).</summary>
    Instances = 2,
}
