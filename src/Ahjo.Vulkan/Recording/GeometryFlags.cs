namespace Ahjo.Vulkan;

/// <summary>
/// Strongly-typed shadow of <c>VkGeometryFlagBitsKHR</c> — per-geometry hints
/// carried on <see cref="AccelerationStructureGeometry.Flags"/>.
/// </summary>
[Flags]
public enum GeometryFlags : uint
{
    /// <summary>No flags. For a ray-query consumer this differs from
    /// <see cref="Opaque"/> only if the shader itself implements an
    /// any-hit-style test against the candidate intersections the query
    /// reports.</summary>
    None = 0,

    /// <summary>
    /// <c>VK_GEOMETRY_OPAQUE_BIT_KHR</c> — the geometry is fully opaque, so
    /// traversal may accept an intersection without any further test. The
    /// default on the triangle and AABB factories.
    /// </summary>
    Opaque = 0x1,

    /// <summary>
    /// <c>VK_GEOMETRY_NO_DUPLICATE_ANY_HIT_INVOCATION_BIT_KHR</c> — promises
    /// the implementation will invoke the any-hit shader at most once per
    /// primitive per ray.
    /// </summary>
    /// <remarks>
    /// <b>Inert for ray query.</b> Ray query has no any-hit shader stage —
    /// the calling shader inspects candidate intersections itself — so this
    /// bit constrains nothing a ray-query consumer runs. It is listed for
    /// completeness, and because a ray-tracing-pipeline follow-up would need
    /// it without a shadow-enum change.
    /// </remarks>
    NoDuplicateAnyHitInvocation = 0x2,
}
