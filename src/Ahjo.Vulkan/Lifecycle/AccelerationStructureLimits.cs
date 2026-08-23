namespace Ahjo.Vulkan;

/// <summary>
/// The <c>VkPhysicalDeviceAccelerationStructurePropertiesKHR</c> fields an
/// acceleration-structure consumer actually has to obey — the scratch-address
/// alignment every build must satisfy plus the four capacity ceilings a GPU
/// picker checks. Read with
/// <see cref="PhysicalDevice.TryGetAccelerationStructureLimits"/>.
/// </summary>
/// <remarks>
/// <para><b><see cref="MinScratchOffsetAlignment"/> is the one that bites.</b>
/// It is a device constant, not a per-build result, which is why it lives here
/// rather than on <see cref="AccelerationStructureBuildSizes"/>: folding it
/// into the size query would make a per-frame TLAS rebuild pay a second
/// chained native query for a value that never changes. Read it once at setup
/// and align every
/// <see cref="AccelerationStructureBuild.ScratchAddress"/> to it
/// (<c>VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03710</c>).</para>
/// <para><b>Narrow on purpose</b>, the same policy as
/// <see cref="MeshShaderLimits"/> and <see cref="DeviceMemoryLimits"/>. Left
/// out: <c>maxPerStageDescriptorUpdateAfterBindAccelerationStructures</c> and
/// <c>maxDescriptorSetUpdateAfterBindAccelerationStructures</c>. Those bound a
/// descriptor-indexing update-after-bind pool, which is a different subsystem's
/// budget and not something an acceleration-structure caller reasons about
/// while sizing builds; a caller who needs them reads the raw struct in one
/// line through
/// <c>PhysicalDevice.TryGetProperties&lt;VkPhysicalDeviceAccelerationStructurePropertiesKHR&gt;</c>.
/// Widening the projection later is additive.</para>
/// <para>This type lives in <c>Lifecycle/</c> rather than <c>Recording/</c>: it
/// is a device-capability record produced by <see cref="PhysicalDevice"/> at
/// setup time, and <c>Recording/</c> is the zero-per-frame-allocation directory
/// where a setup-time record would misfile.</para>
/// </remarks>
public readonly record struct AccelerationStructureLimits
{
    /// <summary>
    /// <c>minAccelerationStructureScratchOffsetAlignment</c> — every
    /// <see cref="AccelerationStructureBuild.ScratchAddress"/> passed to
    /// <see cref="CommandRecorder.BuildAccelerationStructures"/> must be a
    /// multiple of this
    /// (<c>VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03710</c>).
    /// </summary>
    /// <remarks>
    /// It applies to the <b>device address</b>, not to an offset within the
    /// scratch buffer, so aligning a suballocation offset is not enough unless
    /// the buffer's own base address is at least as aligned. The sizes to
    /// reserve at that address are
    /// <see cref="AccelerationStructureBuildSizes.BuildScratchSize"/> /
    /// <see cref="AccelerationStructureBuildSizes.UpdateScratchSize"/>.
    /// </remarks>
    public uint MinScratchOffsetAlignment { get; init; }

    /// <summary>
    /// <c>maxGeometryCount</c> — the most geometries one bottom-level build may
    /// carry
    /// (<c>VUID-VkAccelerationStructureBuildGeometryInfoKHR-type-03793</c>).
    /// </summary>
    public ulong MaxGeometryCount { get; init; }

    /// <summary>
    /// <c>maxInstanceCount</c> — the most instances one top-level build may
    /// carry. The ceiling a GPU picker checks a scene's object count against.
    /// </summary>
    public ulong MaxInstanceCount { get; init; }

    /// <summary>
    /// <c>maxPrimitiveCount</c> — the most triangles (or AABBs) summed across
    /// all geometries of one bottom-level build
    /// (<c>VUID-VkAccelerationStructureBuildGeometryInfoKHR-type-03794</c> /
    /// <c>-03795</c>).
    /// </summary>
    public ulong MaxPrimitiveCount { get; init; }

    /// <summary>
    /// <c>maxPerStageDescriptorAccelerationStructures</c> — the most
    /// <c>VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR</c> descriptors one
    /// shader stage may access, i.e. how many TLASes a ray-query shader can
    /// bind at once (see <see cref="DescriptorWrite.AccelerationStructure"/>).
    /// </summary>
    public uint MaxPerStageDescriptorAccelerationStructures { get; init; }

    /// <summary>
    /// <c>maxDescriptorSetAccelerationStructures</c> — the same ceiling summed
    /// across all stages of one pipeline layout.
    /// </summary>
    public uint MaxDescriptorSetAccelerationStructures { get; init; }
}
