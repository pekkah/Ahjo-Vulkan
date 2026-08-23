namespace Ahjo.Vulkan;

/// <summary>
/// How much of one geometry a build actually consumes: the primitive count
/// plus the byte/element offsets into that geometry's vertex, index, AABB and
/// transform data. One of these per geometry, passed to
/// <see cref="CommandRecorder.BuildAccelerationStructures"/> in a span indexed
/// identically to the geometry span.
/// </summary>
/// <remarks>
/// <para><b>Field order is load-bearing.</b>
/// <see cref="PrimitiveCount"/>, <see cref="PrimitiveOffset"/>,
/// <see cref="FirstVertex"/>, <see cref="TransformOffset"/> — four
/// <c>uint</c>s, 16 bytes — is exactly the layout of
/// <c>VkAccelerationStructureBuildRangeInfoKHR</c>. The recorder does not copy
/// this struct: it pins the caller's span and casts the pointer in place, the
/// same trick <see cref="BufferDescriptorWrite"/> plays on
/// <c>VkDescriptorBufferInfo</c> and <see cref="QueryResult"/> plays on the
/// query readback slot. <b>Reordering these four fields would silently
/// scramble every build</b> — no compile error, no validation error, just
/// wrong geometry — so a test pins the size and all four offsets
/// (<c>AccelerationStructureTests.BuildRange_MirrorsNativeLayout</c>). Four
/// <c>uint</c> fields cannot be reordered or padded by the runtime, so no
/// explicit <c>[StructLayout]</c> is needed.</para>
/// <para><b>What the offsets mean depends on the geometry's kind</b>, because
/// Vulkan overloads them:</para>
/// <list type="bullet">
///   <item><description><b>Triangles</b> — <see cref="PrimitiveCount"/> is the
///     triangle count. <see cref="PrimitiveOffset"/> is a byte offset into the
///     index data (or, with
///     <c>VK_INDEX_TYPE_NONE_KHR</c>, into the vertex data) and must be a
///     multiple of the index size (4 for a non-indexed build).
///     <see cref="FirstVertex"/> is added to every index.
///     <see cref="TransformOffset"/> is a byte offset into the transform data
///     and must be a multiple of 16.</description></item>
///   <item><description><b>AABBs</b> — <see cref="PrimitiveCount"/> is the AABB
///     count and <see cref="PrimitiveOffset"/> is a byte offset into the AABB
///     data, a multiple of 8. <see cref="FirstVertex"/> and
///     <see cref="TransformOffset"/> are ignored.</description></item>
///   <item><description><b>Instances</b> — <see cref="PrimitiveCount"/> is the
///     instance count and <see cref="PrimitiveOffset"/> is a byte offset into
///     the instance array, a multiple of 16. <see cref="FirstVertex"/> and
///     <see cref="TransformOffset"/> are ignored.</description></item>
/// </list>
/// <para>For an <see cref="AccelerationStructureBuildMode.Update"/> build every
/// <see cref="PrimitiveCount"/> must match the source's last build
/// (<c>VUID-vkCmdBuildAccelerationStructuresKHR-primitiveCount-03769</c>).</para>
/// </remarks>
public readonly record struct AccelerationStructureBuildRange
{
    /// <summary>
    /// Triangles, AABBs or instances this build reads from the paired
    /// geometry. Must not exceed the <c>maxPrimitiveCounts</c> entry the same
    /// geometry was sized with in
    /// <see cref="Device.GetAccelerationStructureBuildSizes"/>.
    /// </summary>
    public uint PrimitiveCount { get; init; }

    /// <summary>
    /// Byte offset into the geometry's primitive data — index data for indexed
    /// triangles, vertex data for non-indexed triangles, the AABB array, or the
    /// instance array. See the type's remarks for the per-kind alignment rule.
    /// </summary>
    public uint PrimitiveOffset { get; init; }

    /// <summary>
    /// Value added to every vertex index of a triangle geometry. Ignored for
    /// AABB and instance geometries.
    /// </summary>
    public uint FirstVertex { get; init; }

    /// <summary>
    /// Byte offset into a triangle geometry's transform data, a multiple of 16.
    /// Ignored when the geometry has no transform address, and for AABB and
    /// instance geometries.
    /// </summary>
    public uint TransformOffset { get; init; }

    /// <summary>
    /// Terse constructor for the common case — the per-frame TLAS rebuild is
    /// <c>Of(instanceCount)</c> and a whole-buffer BLAS build is
    /// <c>Of(triangleCount)</c>.
    /// </summary>
    public static AccelerationStructureBuildRange Of(
        uint primitiveCount,
        uint primitiveOffset = 0,
        uint firstVertex     = 0,
        uint transformOffset = 0)
        => new()
        {
            PrimitiveCount  = primitiveCount,
            PrimitiveOffset = primitiveOffset,
            FirstVertex     = firstVertex,
            TransformOffset = transformOffset,
        };
}
