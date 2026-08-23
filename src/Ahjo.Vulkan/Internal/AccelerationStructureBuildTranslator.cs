using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Packs the wrapper's CSR span triple —
/// <see cref="AccelerationStructureBuild"/>,
/// <see cref="AccelerationStructureGeometry"/>,
/// <see cref="AccelerationStructureBuildRange"/> — into the native
/// <c>VkAccelerationStructureBuildGeometryInfoKHR</c> /
/// <c>VkAccelerationStructureGeometryKHR</c> / <c>ppBuildRangeInfos</c> shape
/// <c>vkCmdBuildAccelerationStructuresKHR</c> reads. Shared by
/// <see cref="CommandRecorder.BuildAccelerationStructures"/> and
/// <see cref="Device.GetAccelerationStructureBuildSizes"/> — the
/// <see cref="DescriptorWriteBuilder"/> relationship.
/// </summary>
/// <remarks>
/// <para><b>Every buffer is a caller-pinned pointer, deliberately.</b> The
/// native structs this fills point <em>into</em> the caller's storage:
/// <c>pInfos[b].pGeometries</c> points into <c>pNativeGeometries</c>, and
/// <c>ppRanges[b]</c> points into the caller's own ranges span (no copy — the
/// exact-layout mirror on <see cref="AccelerationStructureBuildRange"/> lets
/// the recorder cast that span in place). The scratch spans are
/// <c>stackalloc</c> on the small path but pooled <b>arrays</b> on the large
/// one, and a pooled array is movable, so taking their addresses inside this
/// class would let a pointer escape its own <c>fixed</c> scope. Passing them
/// in already pinned makes that impossible to get wrong, and makes the
/// pinning obligation visible in the signature — the same contract
/// <see cref="DescriptorWriteBuilder.BuildWrites"/> states for its
/// <c>writes</c> span. <b>The caller MUST keep all of them pinned and
/// addressable for the duration of the native call.</b></para>
/// <para>Allocation-free by construction: every output buffer is carved by the
/// caller and this class only writes into them.</para>
/// </remarks>
internal static unsafe class AccelerationStructureBuildTranslator
{
    /// <summary>
    /// Translates <paramref name="geometries"/> into
    /// <paramref name="pNativeGeometries"/> once over the whole span, then
    /// fills one <c>VkAccelerationStructureBuildGeometryInfoKHR</c> and one
    /// range pointer per build.
    /// </summary>
    /// <param name="builds">The batch, in the order it will be submitted.</param>
    /// <param name="geometries">
    /// The flat geometry span every build slices with
    /// <see cref="AccelerationStructureBuild.FirstGeometry"/> /
    /// <see cref="AccelerationStructureBuild.GeometryCount"/>. Translated
    /// <b>once, up front, over the whole span</b> rather than per build, so
    /// overlapping slices cost nothing.
    /// </param>
    /// <param name="pRanges">
    /// The caller's ranges span, already pinned and cast in place to
    /// <c>VkAccelerationStructureBuildRangeInfoKHR*</c>.
    /// </param>
    /// <param name="pNativeGeometries">Pinned scratch, at least
    /// <c>geometries.Length</c> elements.</param>
    /// <param name="pInfos">Pinned scratch, at least <c>builds.Length</c>
    /// elements.</param>
    /// <param name="ppRanges">Pinned scratch, at least <c>builds.Length</c>
    /// pointers.</param>
    internal static void BuildGeometryInfos(
        ReadOnlySpan<AccelerationStructureBuild>     builds,
        ReadOnlySpan<AccelerationStructureGeometry>  geometries,
        VkAccelerationStructureBuildRangeInfoKHR*    pRanges,
        VkAccelerationStructureGeometryKHR*          pNativeGeometries,
        VkAccelerationStructureBuildGeometryInfoKHR* pInfos,
        VkAccelerationStructureBuildRangeInfoKHR**   ppRanges)
    {
        for (int i = 0; i < geometries.Length; i++)
            geometries[i].WriteNative(out pNativeGeometries[i]);

        for (int b = 0; b < builds.Length; b++)
        {
            ref readonly AccelerationStructureBuild build = ref builds[b];

            pInfos[b] = new VkAccelerationStructureBuildGeometryInfoKHR
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_BUILD_GEOMETRY_INFO_KHR,
                type  = (VkAccelerationStructureTypeKHR)build.Type,
                flags = (uint)build.Flags,
                mode  = (VkBuildAccelerationStructureModeKHR)build.Mode,
                srcAccelerationStructure = build.Source.Handle,
                dstAccelerationStructure = build.Destination.Handle,
                geometryCount            = build.GeometryCount,
                pGeometries              = pNativeGeometries + build.FirstGeometry,
                // Exactly one of pGeometries / ppGeometries may be non-null
                // (VUID-VkAccelerationStructureBuildGeometryInfoKHR-pGeometries-03788).
                ppGeometries = null,
                scratchData  = new VkDeviceOrHostAddressKHR { deviceAddress = build.ScratchAddress },
            };

            // ppBuildRangeInfos[b] is an array of geometryCount range structs —
            // the same slice, pointing into the caller's pinned ranges span.
            ppRanges[b] = pRanges + build.FirstGeometry;
        }
    }

    /// <summary>
    /// The smaller entry point
    /// <see cref="Device.GetAccelerationStructureBuildSizes"/> needs: one
    /// <c>VkAccelerationStructureBuildGeometryInfoKHR</c> over the whole
    /// geometry span, with <c>srcAccelerationStructure</c>,
    /// <c>dstAccelerationStructure</c> and <c>scratchData</c> left zero because
    /// <c>vkGetAccelerationStructureBuildSizesKHR</c> ignores all three.
    /// </summary>
    internal static void BuildSizeQueryInfo(
        AccelerationStructureType                       type,
        AccelerationStructureBuildFlags                 flags,
        ReadOnlySpan<AccelerationStructureGeometry>     geometries,
        VkAccelerationStructureGeometryKHR*             pNativeGeometries,
        out VkAccelerationStructureBuildGeometryInfoKHR info)
    {
        for (int i = 0; i < geometries.Length; i++)
            geometries[i].WriteNative(out pNativeGeometries[i]);

        info = new VkAccelerationStructureBuildGeometryInfoKHR
        {
            sType         = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_BUILD_GEOMETRY_INFO_KHR,
            type          = (VkAccelerationStructureTypeKHR)type,
            flags         = (uint)flags,
            mode          = VkBuildAccelerationStructureModeKHR.VK_BUILD_ACCELERATION_STRUCTURE_MODE_BUILD_KHR,
            geometryCount = (uint)geometries.Length,
            pGeometries   = pNativeGeometries,
            ppGeometries  = null,
        };
    }
}
