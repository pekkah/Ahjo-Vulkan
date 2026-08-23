using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// One geometry of an acceleration-structure build — a flat, tagged
/// description of triangle, AABB or instance data, all of it addressed by
/// <c>VkDeviceAddress</c>. Built through the three static factories and passed
/// to <see cref="CommandRecorder.BuildAccelerationStructures"/> or
/// <see cref="Device.GetAccelerationStructureBuildSizes"/> in a span.
/// </summary>
/// <remarks>
/// <para><b>A flat struct with overloaded members, not a union.</b> Which
/// members mean what is decided by <see cref="Kind"/>:</para>
/// <list type="table">
///   <listheader>
///     <term><see cref="Kind"/></term><description>member meanings</description>
///   </listheader>
///   <item>
///     <term><see cref="GeometryKind.Triangles"/></term>
///     <description><see cref="Address"/> is <c>vertexData</c>;
///       <see cref="Stride"/> is <c>vertexStride</c>;
///       <see cref="VertexFormat"/>, <see cref="MaxVertex"/>,
///       <see cref="IndexType"/>, <see cref="IndexAddress"/> and
///       <see cref="TransformAddress"/> all apply.
///       <see cref="ArrayOfPointers"/> is unused.</description>
///   </item>
///   <item>
///     <term><see cref="GeometryKind.Aabbs"/></term>
///     <description><see cref="Address"/> is <c>data</c>, pointing at an array
///       of <c>VkAabbPositionsKHR</c> (six <c>float</c>s, 24 bytes);
///       <see cref="Stride"/> is the AABB stride and <b>must be a multiple of
///       8</b>. Everything else is unused.</description>
///   </item>
///   <item>
///     <term><see cref="GeometryKind.Instances"/></term>
///     <description><see cref="Address"/> is the instance-array address and
///       <see cref="ArrayOfPointers"/> selects how it is read.
///       <see cref="Stride"/>, <see cref="MaxVertex"/>,
///       <see cref="IndexType"/>, <see cref="IndexAddress"/> and
///       <see cref="TransformAddress"/> are unused.</description>
///   </item>
/// </list>
/// <para><b>Build inputs are invisible to the wrapper.</b> Every member here
/// that names a buffer is a bare <c>ulong</c> device address the caller got
/// from <see cref="Buffer.GetDeviceAddress"/>. The wrapper cannot check them,
/// cannot keep them alive, and cannot tell you that you passed an address from
/// a buffer you already disposed. They must be alive, resident and unmodified
/// from submission until the build completes on the GPU, and every source
/// buffer needs
/// <see cref="BufferUsage.AccelerationStructureBuildInputReadOnly"/> |
/// <see cref="BufferUsage.ShaderDeviceAddress"/>.</para>
/// <para>Deliberately not a <c>record struct</c>: nothing compares geometries,
/// and record equality on a struct of primitives adds a synthesized
/// <c>Equals</c>/<c>GetHashCode</c> pair for no caller.</para>
/// </remarks>
public readonly struct AccelerationStructureGeometry
{
    /// <summary>Which shape this geometry describes, and therefore how the
    /// other members are read. See the type's table.</summary>
    public GeometryKind Kind { get; }

    /// <summary>Per-geometry hints. <see cref="GeometryFlags.Opaque"/> by
    /// default on the triangle and AABB factories,
    /// <see cref="GeometryFlags.None"/> on the instance factory.</summary>
    public GeometryFlags Flags { get; }

    /// <summary>The geometry's primary data address:
    /// <c>vertexData</c> for triangles, <c>data</c> for AABBs, the instance
    /// array for instances. See the type's table.</summary>
    public ulong Address { get; }

    /// <summary>Byte stride between elements at <see cref="Address"/>:
    /// <c>vertexStride</c> for triangles, the AABB stride (a multiple of 8) for
    /// AABBs. Unused for instances.</summary>
    public ulong Stride { get; }

    /// <summary>Index-data address for a triangle geometry; 0 (with
    /// <see cref="IndexType"/> = <c>VK_INDEX_TYPE_NONE_KHR</c>) for a
    /// non-indexed build. Unused for AABBs and instances.</summary>
    public ulong IndexAddress { get; }

    /// <summary>Address of an optional <c>VkTransformMatrixKHR</c> (a 3×4
    /// row-major matrix) applied to a triangle geometry; 0 for none. Must be
    /// 16-byte aligned when non-zero. Unused for AABBs and instances.</summary>
    /// <remarks>
    /// An <see cref="AccelerationStructureBuildMode.Update"/> cannot introduce
    /// or remove a transform: if this was 0 in the source's last build it must
    /// be 0 now, and vice versa
    /// (<c>VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03766</c> /
    /// <c>-03767</c>).
    /// </remarks>
    public ulong TransformAddress { get; }

    /// <summary>Highest vertex index this triangle geometry will reference —
    /// the vertex-buffer bound the driver sizes against, <em>not</em> the vertex
    /// count. Unused for AABBs and instances.</summary>
    public uint MaxVertex { get; }

    /// <summary>Vertex position format of a triangle geometry, typically
    /// <c>VK_FORMAT_R32G32B32_SFLOAT</c>. Unused for AABBs and
    /// instances.</summary>
    /// <remarks>Raw <c>VkFormat</c> in the public signature by the same policy
    /// as <see cref="GraphicsPipelineBuilder.WithDynamicRendering"/>:
    /// re-shadowing 200 formats is not on the table.</remarks>
    public VkFormat VertexFormat { get; }

    /// <summary>Index type of a triangle geometry, or
    /// <c>VK_INDEX_TYPE_NONE_KHR</c> for a non-indexed build. Unused for AABBs
    /// and instances.</summary>
    public VkIndexType IndexType { get; }

    /// <summary>
    /// For an instance geometry: <see langword="false"/> when
    /// <see cref="Address"/> points at a packed array of
    /// <c>VkAccelerationStructureInstanceKHR</c>, <see langword="true"/> when it
    /// points at an array of <c>VkDeviceAddress</c> values each pointing at one
    /// such structure. Unused for triangles and AABBs.
    /// </summary>
    public bool ArrayOfPointers { get; }

    private AccelerationStructureGeometry(
        GeometryKind  kind,
        GeometryFlags flags,
        ulong         address,
        ulong         stride,
        ulong         indexAddress,
        ulong         transformAddress,
        uint          maxVertex,
        VkFormat      vertexFormat,
        VkIndexType   indexType,
        bool          arrayOfPointers)
    {
        Kind             = kind;
        Flags            = flags;
        Address          = address;
        Stride           = stride;
        IndexAddress     = indexAddress;
        TransformAddress = transformAddress;
        MaxVertex        = maxVertex;
        VertexFormat     = vertexFormat;
        IndexType        = indexType;
        ArrayOfPointers  = arrayOfPointers;
    }

    /// <summary>
    /// Triangle geometry for a <see cref="AccelerationStructureType.BottomLevel"/>
    /// build — indexed when <paramref name="indexAddress"/> and
    /// <paramref name="indexType"/> are supplied, a plain triangle list
    /// otherwise.
    /// </summary>
    /// <param name="vertexAddress">Device address of the vertex positions.</param>
    /// <param name="vertexFormat">Position format, typically
    /// <c>VK_FORMAT_R32G32B32_SFLOAT</c>.</param>
    /// <param name="vertexStride">Bytes between consecutive vertices — the
    /// whole vertex's size when positions are interleaved with other
    /// attributes.</param>
    /// <param name="maxVertex">Highest vertex index referenced, not the vertex
    /// count.</param>
    /// <param name="indexAddress">Device address of the index data, or 0 for a
    /// non-indexed build.</param>
    /// <param name="indexType"><c>VK_INDEX_TYPE_UINT16</c> /
    /// <c>VK_INDEX_TYPE_UINT32</c>, or <c>VK_INDEX_TYPE_NONE_KHR</c> (the
    /// default) for a non-indexed build.</param>
    /// <param name="transformAddress">Device address of an optional
    /// 16-byte-aligned <c>VkTransformMatrixKHR</c>, or 0 for none.</param>
    /// <param name="flags">Per-geometry hints;
    /// <see cref="GeometryFlags.Opaque"/> by default.</param>
    /// <remarks>
    /// The triangle count and the byte offsets into these buffers are <b>not</b>
    /// here — they live on the paired
    /// <see cref="AccelerationStructureBuildRange"/>, because Vulkan lets one
    /// geometry description be reused across builds that consume different
    /// ranges of it.
    /// </remarks>
    public static AccelerationStructureGeometry Triangles(
        ulong         vertexAddress,
        VkFormat      vertexFormat,
        ulong         vertexStride,
        uint          maxVertex,
        ulong         indexAddress     = 0,
        VkIndexType   indexType        = VkIndexType.VK_INDEX_TYPE_NONE_KHR,
        ulong         transformAddress = 0,
        GeometryFlags flags            = GeometryFlags.Opaque)
        => new(GeometryKind.Triangles, flags, vertexAddress, vertexStride,
               indexAddress, transformAddress, maxVertex, vertexFormat,
               indexType, arrayOfPointers: false);

    /// <summary>
    /// Axis-aligned-bounding-box geometry for a procedural
    /// <see cref="AccelerationStructureType.BottomLevel"/> build.
    /// </summary>
    /// <param name="address">Device address of an array of
    /// <c>VkAabbPositionsKHR</c> (six <c>float</c>s: min XYZ then max
    /// XYZ).</param>
    /// <param name="stride">Bytes between consecutive AABBs. <b>Must be a
    /// multiple of 8</b>; 24 for a packed array.</param>
    /// <param name="flags">Per-geometry hints;
    /// <see cref="GeometryFlags.Opaque"/> by default.</param>
    /// <remarks>
    /// The wrapper deliberately does not mirror <c>VkAabbPositionsKHR</c> —
    /// callers write it using the generated struct from
    /// <c>Ahjo.Vulkan.Native</c>, the same policy the properties-chain query and
    /// <see cref="VkFormat"/> already follow.
    /// </remarks>
    public static AccelerationStructureGeometry Aabbs(
        ulong         address,
        ulong         stride,
        GeometryFlags flags = GeometryFlags.Opaque)
        => new(GeometryKind.Aabbs, flags, address, stride,
               indexAddress: 0, transformAddress: 0, maxVertex: 0,
               vertexFormat: VkFormat.VK_FORMAT_UNDEFINED,
               indexType: VkIndexType.VK_INDEX_TYPE_NONE_KHR,
               arrayOfPointers: false);

    /// <summary>
    /// Instance geometry — the <b>only</b> geometry a
    /// <see cref="AccelerationStructureType.TopLevel"/> build may carry, and it
    /// may carry exactly one
    /// (<c>VUID-VkAccelerationStructureBuildGeometryInfoKHR-type-03789</c> /
    /// <c>-03790</c>).
    /// </summary>
    /// <param name="address">
    /// Device address of the instance array, which must be <b>16-byte
    /// aligned</b>. Each element is a <c>VkAccelerationStructureInstanceKHR</c>
    /// from <c>Ahjo.Vulkan.Native</c> — 64 bytes carrying the 3×4 transform, a
    /// packed instance-custom-index/mask word, a packed SBT-offset/flags word,
    /// and <c>accelerationStructureReference</c>. When
    /// <paramref name="arrayOfPointers"/> is <see langword="true"/> it is
    /// instead an array of <c>VkDeviceAddress</c> values, each pointing at one
    /// such structure.
    /// </param>
    /// <param name="arrayOfPointers">See <paramref name="address"/>.</param>
    /// <param name="flags">Per-geometry hints;
    /// <see cref="GeometryFlags.None"/> by default — the per-instance opacity
    /// decision belongs on the instance entry's own flags word, not
    /// here.</param>
    /// <remarks>
    /// <para>The wrapper deliberately does not mirror
    /// <c>VkAccelerationStructureInstanceKHR</c>: it is 64 bytes of bitfields
    /// with generated accessors, and hand-copying a bitfield layout is precisely
    /// the drift class this repo's shadow-enum tests exist to prevent. Write it
    /// with the generated struct.</para>
    /// <para><b>The dangling-BLAS hazard.</b> Each element's
    /// <c>accelerationStructureReference</c> is
    /// <see cref="AccelerationStructure.GetDeviceAddress"/> of a bottom-level
    /// structure — and once written it is <b>just a number</b>. No layer, no
    /// driver and no tool can tell that the BLAS behind it was destroyed;
    /// traversal reads freed memory with no diagnostic. Every BLAS must outlive
    /// every TLAS built over it, and a TLAS must be fully rebuilt
    /// (<see cref="AccelerationStructureBuildMode.Build"/>, not
    /// <see cref="AccelerationStructureBuildMode.Update"/>) after any referenced
    /// BLAS is destroyed, recreated, or <b>compacted</b> — compaction moves the
    /// BLAS to a new buffer and therefore changes its device address.</para>
    /// </remarks>
    public static AccelerationStructureGeometry Instances(
        ulong         address,
        bool          arrayOfPointers = false,
        GeometryFlags flags           = GeometryFlags.None)
        => new(GeometryKind.Instances, flags, address, stride: 0,
               indexAddress: 0, transformAddress: 0, maxVertex: 0,
               vertexFormat: VkFormat.VK_FORMAT_UNDEFINED,
               indexType: VkIndexType.VK_INDEX_TYPE_NONE_KHR,
               arrayOfPointers: arrayOfPointers);

    /// <summary>
    /// Fills <paramref name="dst"/> with the native form of this geometry. The
    /// only place <c>VkAccelerationStructureGeometryDataKHR</c> — an explicit
    /// union — is touched, so the "write exactly one arm and zero the rest"
    /// rule lives in one method. Assigning a fresh
    /// <c>VkAccelerationStructureGeometryKHR</c> zeroes the whole union first.
    /// </summary>
    internal void WriteNative(out VkAccelerationStructureGeometryKHR dst)
    {
        dst = new VkAccelerationStructureGeometryKHR
        {
            sType        = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_KHR,
            geometryType = (VkGeometryTypeKHR)Kind,
            flags        = (uint)Flags,
        };

        switch (Kind)
        {
            case GeometryKind.Triangles:
                dst.geometry.triangles = new VkAccelerationStructureGeometryTrianglesDataKHR
                {
                    sType        = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_TRIANGLES_DATA_KHR,
                    vertexFormat = VertexFormat,
                    vertexData   = new VkDeviceOrHostAddressConstKHR { deviceAddress = Address },
                    vertexStride = Stride,
                    maxVertex    = MaxVertex,
                    indexType    = IndexType,
                    indexData    = new VkDeviceOrHostAddressConstKHR { deviceAddress = IndexAddress },
                    transformData = new VkDeviceOrHostAddressConstKHR { deviceAddress = TransformAddress },
                };
                break;

            case GeometryKind.Aabbs:
                dst.geometry.aabbs = new VkAccelerationStructureGeometryAabbsDataKHR
                {
                    sType  = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_AABBS_DATA_KHR,
                    data   = new VkDeviceOrHostAddressConstKHR { deviceAddress = Address },
                    stride = Stride,
                };
                break;

            default:
                dst.geometry.instances = new VkAccelerationStructureGeometryInstancesDataKHR
                {
                    sType           = VkStructureType.VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_INSTANCES_DATA_KHR,
                    arrayOfPointers = ArrayOfPointers ? 1u : 0u,
                    data            = new VkDeviceOrHostAddressConstKHR { deviceAddress = Address },
                };
                break;
        }
    }
}
