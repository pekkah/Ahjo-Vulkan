namespace Ahjo.Vulkan;

/// <summary>
/// One acceleration-structure build in a batch: the destination, the mode and
/// flags it is built with, the caller-owned scratch address, and a
/// <c>(FirstGeometry, GeometryCount)</c> slice naming which geometries of the
/// batch belong to it.
/// </summary>
/// <remarks>
/// <para><b>The CSR contract.</b>
/// <see cref="CommandRecorder.BuildAccelerationStructures"/> takes three flat
/// spans — builds, geometries, ranges — in a compressed-sparse-row shape:
/// <see cref="FirstGeometry"/> and <see cref="GeometryCount"/> slice the
/// <c>geometries</c> span <b>and</b> the <c>ranges</c> span, and the two are
/// indexed <em>identically</em> because Vulkan pairs exactly one
/// <see cref="AccelerationStructureBuildRange"/> with each
/// <see cref="AccelerationStructureGeometry"/>. That is why one slice indexes
/// both and why <c>ranges.Length</c> must equal <c>geometries.Length</c>.
/// Spans of spans would be the obvious alternative and do not compile: a struct
/// holding a <c>ReadOnlySpan&lt;T&gt;</c> is a <c>ref struct</c>, and
/// <c>ReadOnlySpan&lt;T&gt;</c> cannot be instantiated over one. The CSR shape
/// batches N builds with no span-of-spans, no allocation and no wrapper-owned
/// scratch object; the caller owns all three spans and each can be a
/// <c>stackalloc</c>, a pooled array or a long-lived field.</para>
/// <para><b>Scratch is caller-owned and passed as an address, not a
/// handle.</b> The recorder never allocates, sizes, suballocates or recycles
/// scratch — how scratch is reused across builds and frames is a frame-graph
/// decision the wrapper cannot see. See
/// <see cref="ScratchAddress"/> for the four obligations that come with
/// that.</para>
/// <para><b><see cref="Type"/> defaults to
/// <see cref="AccelerationStructureType.TopLevel"/></b>, because Vulkan numbers
/// top level 0. A BLAS build must set it explicitly; see
/// <see cref="AccelerationStructureType"/>.</para>
/// <para><b>Usage — the per-frame TLAS rebuild.</b></para>
/// <code>
/// Span&lt;AccelerationStructureBuild&gt;      builds = stackalloc AccelerationStructureBuild[1];
/// Span&lt;AccelerationStructureGeometry&gt;   geos   = stackalloc AccelerationStructureGeometry[1];
/// Span&lt;AccelerationStructureBuildRange&gt; ranges = stackalloc AccelerationStructureBuildRange[1];
///
/// geos[0]   = AccelerationStructureGeometry.Instances(instanceBufferAddress);
/// ranges[0] = AccelerationStructureBuildRange.Of(instanceCount);
/// builds[0] = new AccelerationStructureBuild
/// {
///     Type = AccelerationStructureType.TopLevel,
///     Flags = AccelerationStructureBuildFlags.PreferFastBuild,
///     Destination = tlas,
///     ScratchAddress = scratch.GetDeviceAddress(device),
///     FirstGeometry = 0,
///     GeometryCount = 1,
/// };
/// recorder.BuildAccelerationStructures(builds, geos, ranges);
/// </code>
/// </remarks>
public readonly struct AccelerationStructureBuild
{
    /// <summary>
    /// Whether this build produces a TLAS or a BLAS. Must match the
    /// <see cref="Destination"/>'s creation type, and for an
    /// <see cref="AccelerationStructureBuildMode.Update"/> must match the
    /// <see cref="Source"/>'s last build
    /// (<c>VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03760</c>).
    /// <b>Defaults to <see cref="AccelerationStructureType.TopLevel"/></b> — a
    /// BLAS build must set it.
    /// </summary>
    public AccelerationStructureType Type { get; init; }

    /// <summary>
    /// Build-time trade-offs and permissions. Must be the same set passed to
    /// <see cref="Device.GetAccelerationStructureBuildSizes"/> when the scratch
    /// and backing ranges were sized, and for an
    /// <see cref="AccelerationStructureBuildMode.Update"/> must match the
    /// <see cref="Source"/>'s last build (<c>-pInfos-03759</c>).
    /// </summary>
    public AccelerationStructureBuildFlags Flags { get; init; }

    /// <summary>
    /// Build from scratch or refit from <see cref="Source"/>. Defaults to
    /// <see cref="AccelerationStructureBuildMode.Build"/>.
    /// </summary>
    public AccelerationStructureBuildMode Mode { get; init; }

    /// <summary>
    /// The structure being refitted. Required <b>iff</b> <see cref="Mode"/> is
    /// <see cref="AccelerationStructureBuildMode.Update"/>
    /// (<c>VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-04630</c>), and must
    /// have been built with
    /// <see cref="AccelerationStructureBuildFlags.AllowUpdate"/>
    /// (<c>-pInfos-03667</c>). Must be left null for
    /// <see cref="AccelerationStructureBuildMode.Build"/>. May be the same
    /// structure as <see cref="Destination"/> (the in-place refit) or must not
    /// alias it at all (<c>-pInfos-03668</c>).
    /// </summary>
    public AccelerationStructure Source { get; init; }

    /// <summary>
    /// The structure this build writes. Required, and its backing range must be
    /// at least
    /// <see cref="AccelerationStructureBuildSizes.AccelerationStructureSize"/>
    /// bytes.
    /// </summary>
    public AccelerationStructure Destination { get; init; }

    /// <summary>
    /// Device address of this build's scratch memory.
    /// </summary>
    /// <remarks>
    /// Four caller obligations, none of which the wrapper can check:
    /// <list type="number">
    ///   <item><description>Size it from
    ///     <see cref="AccelerationStructureBuildSizes.BuildScratchSize"/>, or
    ///     <see cref="AccelerationStructureBuildSizes.UpdateScratchSize"/> for
    ///     <see cref="AccelerationStructureBuildMode.Update"/>.</description></item>
    ///   <item><description>Align the <b>address</b> to
    ///     <see cref="AccelerationStructureLimits.MinScratchOffsetAlignment"/>
    ///     (<c>VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03710</c>) — an
    ///     aligned suballocation offset is not enough unless the buffer's base
    ///     address is at least as aligned.</description></item>
    ///   <item><description>Create the buffer with
    ///     <see cref="BufferUsage.StorageBuffer"/> |
    ///     <see cref="BufferUsage.ShaderDeviceAddress"/>.</description></item>
    ///   <item><description>Give <b>every build in one
    ///     <see cref="CommandRecorder.BuildAccelerationStructures"/> call a
    ///     non-overlapping scratch range</b> (<c>-scratchData-03704</c>) —
    ///     builds within one call may execute concurrently — and keep the range
    ///     alive and untouched by anything else until the build completes on
    ///     the GPU.</description></item>
    /// </list>
    /// </remarks>
    public ulong ScratchAddress { get; init; }

    /// <summary>
    /// Index into the <c>geometries</c> and <c>ranges</c> spans where this
    /// build's geometries start. See the type's CSR contract.
    /// </summary>
    public uint FirstGeometry { get; init; }

    /// <summary>
    /// How many geometries (and, one-to-one, ranges) this build consumes
    /// starting at <see cref="FirstGeometry"/>. Must be greater than 0, and
    /// exactly 1 when <see cref="Type"/> is
    /// <see cref="AccelerationStructureType.TopLevel"/>
    /// (<c>VUID-VkAccelerationStructureBuildGeometryInfoKHR-type-03790</c>).
    /// For an <see cref="AccelerationStructureBuildMode.Update"/> it must match
    /// the <see cref="Source"/>'s last build
    /// (<c>VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03758</c>).
    /// </summary>
    public uint GeometryCount { get; init; }
}
