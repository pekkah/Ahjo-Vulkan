using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan.Samples.HelloRayQuery;

/// <summary>
/// The acceleration-structure half of the sample: a two-triangle BLAS, one
/// instance referencing it, and a TLAS over that instance — everything sized,
/// allocated and created, with the recording left to
/// <see cref="RecordBuilds"/> so both builds land in the caller's single
/// command buffer.
/// </summary>
/// <remarks>
/// This is the sequence a consumer actually writes and that no test in the
/// suite runs end to end: size → allocate backing + scratch → create → build
/// BLAS → barrier → read the BLAS device address → write the instance → build
/// TLAS. The instance's <c>accelerationStructureReference</c> is a raw device
/// address that nothing can validate, which is exactly why the BLAS has to be
/// built and its address read before the instance can be written.
/// </remarks>
internal sealed unsafe class RayQueryScene : IDisposable
{
    private const uint TriangleCount = 2;
    private const uint VertexStride  = 3 * sizeof(float);

    private readonly Device _device;

    private readonly Buffer _vertices;
    private readonly Buffer _blasBacking;
    private readonly Buffer _blasScratch;
    private readonly Buffer _instances;
    private readonly Buffer _tlasBacking;
    private readonly Buffer _tlasScratch;

    private readonly AccelerationStructure _blas;
    private readonly AccelerationStructureGeometry _triangles;
    private readonly AccelerationStructureGeometry _instanceGeometry;

    public AccelerationStructure Tlas { get; }

    public ulong BlasAddress { get; }
    public ulong TlasAddress { get; }

    public RayQueryScene(Device device, uint family)
    {
        _ = family;
        _device = device;

        // ---- BLAS: two triangles ----
        _vertices = CreateHostBuffer(
            (ulong)Program.VertexBytes,
            BufferUsage.AccelerationStructureBuildInputReadOnly | BufferUsage.ShaderDeviceAddress);
        Program.WriteVertices(_vertices.AsSpan<float>());
        _vertices.Flush();

        _triangles = AccelerationStructureGeometry.Triangles(
            vertexAddress: _vertices.GetDeviceAddress(device),
            vertexFormat:  VkFormat.VK_FORMAT_R32G32B32_SFLOAT,
            vertexStride:  VertexStride,
            maxVertex:     TriangleCount * 3 - 1);

        Span<AccelerationStructureGeometry> blasGeos = [_triangles];
        Span<uint> blasCounts = [TriangleCount];
        AccelerationStructureBuildSizes blasSizes = device.GetAccelerationStructureBuildSizes(
            AccelerationStructureType.BottomLevel,
            AccelerationStructureBuildFlags.PreferFastTrace, blasGeos, blasCounts);

        _blasBacking = CreateAsBuffer(blasSizes.AccelerationStructureSize);
        _blasScratch = CreateScratchBuffer(blasSizes.BuildScratchSize);
        RequireScratchAlignment(_blasScratch, "BLAS");

        _blas = device.CreateAccelerationStructure(
            AccelerationStructureType.BottomLevel, in _blasBacking, 0,
            blasSizes.AccelerationStructureSize);

        // The BLAS handle exists as soon as it is created, and so does its
        // device address — the build fills it in, but the address is stable
        // from creation, which is what lets the instance below be written
        // before anything is submitted.
        BlasAddress = _blas.GetDeviceAddress(device);

        // ---- One instance referencing that BLAS ----
        _instances = CreateHostBuffer(
            (ulong)sizeof(VkAccelerationStructureInstanceKHR),
            BufferUsage.AccelerationStructureBuildInputReadOnly | BufferUsage.ShaderDeviceAddress);

        Span<VkAccelerationStructureInstanceKHR> instances =
            _instances.AsSpan<VkAccelerationStructureInstanceKHR>();
        instances[0] = default;
        // Identity 3x4, row-major. The wrapper deliberately does not mirror
        // VkAccelerationStructureInstanceKHR — its packed bitfields are written
        // through the generated struct.
        instances[0].transform.matrix[0]  = 1f;
        instances[0].transform.matrix[5]  = 1f;
        instances[0].transform.matrix[10] = 1f;
        instances[0].mask = 0xFF;
        instances[0].accelerationStructureReference = BlasAddress;
        _instances.Flush();

        _instanceGeometry = AccelerationStructureGeometry.Instances(
            _instances.GetDeviceAddress(device));

        Span<AccelerationStructureGeometry> tlasGeos = [_instanceGeometry];
        Span<uint> tlasCounts = [1];
        AccelerationStructureBuildSizes tlasSizes = device.GetAccelerationStructureBuildSizes(
            AccelerationStructureType.TopLevel,
            AccelerationStructureBuildFlags.PreferFastTrace, tlasGeos, tlasCounts);

        _tlasBacking = CreateAsBuffer(tlasSizes.AccelerationStructureSize);
        _tlasScratch = CreateScratchBuffer(tlasSizes.BuildScratchSize);
        RequireScratchAlignment(_tlasScratch, "TLAS");

        Tlas = device.CreateAccelerationStructure(
            AccelerationStructureType.TopLevel, in _tlasBacking, 0,
            tlasSizes.AccelerationStructureSize);
        TlasAddress = Tlas.GetDeviceAddress(device);
    }

    /// <summary>
    /// Records both builds with the build→build barrier between them. The
    /// caller adds the build→shader-read barrier afterwards, because that one
    /// belongs to the consumer, not to the scene.
    /// </summary>
    public void RecordBuilds(ref CommandRecorder rec)
    {
        Span<AccelerationStructureBuild> builds = stackalloc AccelerationStructureBuild[1];
        Span<AccelerationStructureGeometry> geos = stackalloc AccelerationStructureGeometry[1];
        Span<AccelerationStructureBuildRange> ranges = stackalloc AccelerationStructureBuildRange[1];

        builds[0] = new AccelerationStructureBuild
        {
            Type           = AccelerationStructureType.BottomLevel,
            Flags          = AccelerationStructureBuildFlags.PreferFastTrace,
            Destination    = _blas,
            ScratchAddress = _blasScratch.GetDeviceAddress(_device),
            FirstGeometry  = 0,
            GeometryCount  = 1,
        };
        geos[0]   = _triangles;
        ranges[0] = AccelerationStructureBuildRange.Of(TriangleCount);
        rec.BuildAccelerationStructures(builds, geos, ranges);

        // The TLAS build reads the BLAS this command buffer just wrote.
        rec.PipelineBarrier(
            [
                new MemoryBarrier
                {
                    SrcStage  = Stage.AccelerationStructureBuild,
                    SrcAccess = Access.AccelerationStructureWrite,
                    DstStage  = Stage.AccelerationStructureBuild,
                    DstAccess = Access.AccelerationStructureRead,
                },
            ],
            default, default);

        builds[0] = new AccelerationStructureBuild
        {
            Type           = AccelerationStructureType.TopLevel,
            Flags          = AccelerationStructureBuildFlags.PreferFastTrace,
            Destination    = Tlas,
            ScratchAddress = _tlasScratch.GetDeviceAddress(_device),
            FirstGeometry  = 0,
            GeometryCount  = 1,
        };
        geos[0]   = _instanceGeometry;
        ranges[0] = AccelerationStructureBuildRange.Of(1);
        rec.BuildAccelerationStructures(builds, geos, ranges);
    }

    private Buffer CreateHostBuffer(ulong size, BufferUsage usage)
        => _device.Allocator.CreateBuffer(
            new BufferDescription { Size = size, Usage = usage },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
            });

    private Buffer CreateAsBuffer(ulong size)
        => _device.Allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = size,
                Usage = BufferUsage.AccelerationStructureStorage | BufferUsage.ShaderDeviceAddress,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

    private Buffer CreateScratchBuffer(ulong size)
        => _device.Allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = size == 0 ? 256 : size,
                Usage = BufferUsage.StorageBuffer | BufferUsage.ShaderDeviceAddress,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

    /// <summary>
    /// VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03710: the scratch
    /// device address must be a multiple of <c>MinScratchOffsetAlignment</c>.
    /// This sample hands the build each buffer's base address unchanged, which
    /// only satisfies the rule if the allocator's base happens to be aligned —
    /// so check it here, where the message can say what went wrong, rather than
    /// letting it surface from inside the driver.
    /// </summary>
    private void RequireScratchAlignment(in Buffer scratch, string which)
    {
        if (!_device.PhysicalDevice.TryGetAccelerationStructureLimits(
                out AccelerationStructureLimits limits))
        {
            throw new InvalidOperationException(
                "A device created with VK_KHR_acceleration_structure must advertise "
                + "VkPhysicalDeviceAccelerationStructurePropertiesKHR.");
        }

        ulong address = scratch.GetDeviceAddress(_device);
        if (address % limits.MinScratchOffsetAlignment != 0)
        {
            throw new InvalidOperationException(
                $"{which} scratch buffer base address 0x{address:X} is not a multiple of "
                + $"minAccelerationStructureScratchOffsetAlignment ({limits.MinScratchOffsetAlignment}). "
                + "Over-allocate the scratch buffer and align the offset by hand.");
        }
    }

    public void Dispose()
    {
        Tlas.Dispose();
        _blas.Dispose();
        _tlasScratch.Dispose();
        _tlasBacking.Dispose();
        _instances.Dispose();
        _blasScratch.Dispose();
        _blasBacking.Dispose();
        _vertices.Dispose();
    }
}
