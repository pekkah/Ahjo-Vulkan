namespace Ahjo.Vulkan;

/// <summary>
/// The three sizes <c>vkGetAccelerationStructureBuildSizesKHR</c> reports for
/// a prospective build, as returned by
/// <see cref="Device.GetAccelerationStructureBuildSizes"/>: how much backing
/// storage the acceleration structure needs, and how much scratch each of the
/// two build modes needs.
/// </summary>
/// <remarks>
/// <para>This lives in <c>Memory/</c> because it <em>is</em> a
/// memory-requirements record, next to <see cref="MemoryRequirements"/> — the
/// answer to "how much do I have to allocate before I can record this build?".
/// It is setup-time output; nothing here is read per frame.</para>
/// <para>The query ignores <c>srcAccelerationStructure</c>,
/// <c>dstAccelerationStructure</c> and <c>scratchData</c>, which is why
/// <see cref="Device.GetAccelerationStructureBuildSizes"/> has no destination
/// parameter and why both scratch sizes come back from a single call.</para>
/// </remarks>
public readonly record struct AccelerationStructureBuildSizes
{
    /// <summary>
    /// Bytes to reserve in the backing <see cref="Buffer"/> for the
    /// acceleration structure itself — the <c>size</c> argument of
    /// <see cref="Device.CreateAccelerationStructure"/>.
    /// </summary>
    /// <remarks>
    /// The buffer must have been created with
    /// <see cref="BufferUsage.AccelerationStructureStorage"/>
    /// (<c>VUID-VkAccelerationStructureCreateInfoKHR-buffer-03614</c>), and the
    /// offset within it must be a multiple of <b>256</b>
    /// (<c>-offset-03734</c>). Suballocating many structures into one large
    /// buffer at 256-byte-aligned offsets is the intended pattern.
    /// </remarks>
    public ulong AccelerationStructureSize { get; init; }

    /// <summary>
    /// Bytes of scratch a build with
    /// <see cref="AccelerationStructureBuildMode.Build"/> needs, addressed
    /// through <see cref="AccelerationStructureBuild.ScratchAddress"/>.
    /// </summary>
    /// <remarks>
    /// The scratch buffer must be created with
    /// <see cref="BufferUsage.StorageBuffer"/> |
    /// <see cref="BufferUsage.ShaderDeviceAddress"/>, and the address handed to
    /// a build must be a multiple of
    /// <see cref="AccelerationStructureLimits.MinScratchOffsetAlignment"/>
    /// (<c>VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03710</c>) — read
    /// that with
    /// <see cref="PhysicalDevice.TryGetAccelerationStructureLimits"/>. Every
    /// build batched into one
    /// <see cref="CommandRecorder.BuildAccelerationStructures"/> call needs its
    /// own non-overlapping scratch range (<c>-scratchData-03704</c>), because
    /// builds within one call may execute concurrently.
    /// </remarks>
    public ulong BuildScratchSize { get; init; }

    /// <summary>
    /// Bytes of scratch a build with
    /// <see cref="AccelerationStructureBuildMode.Update"/> needs — usually much
    /// smaller than <see cref="BuildScratchSize"/>. A scratch buffer reused for
    /// both modes must be sized by the larger of the two.
    /// </summary>
    /// <remarks>
    /// Same buffer usage, same
    /// <see cref="AccelerationStructureLimits.MinScratchOffsetAlignment"/> rule
    /// and same non-overlap rule as <see cref="BuildScratchSize"/>. Only
    /// meaningful when the structure was built with
    /// <see cref="AccelerationStructureBuildFlags.AllowUpdate"/>.
    /// </remarks>
    public ulong UpdateScratchSize { get; init; }
}
