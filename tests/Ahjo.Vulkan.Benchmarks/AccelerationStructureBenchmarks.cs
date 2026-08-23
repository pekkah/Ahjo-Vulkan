using Ahjo.Vulkan.Native;
using BenchmarkDotNet.Attributes;

namespace Ahjo.Vulkan.Benchmarks;

/// <summary>
/// Issue 202: <see cref="CommandRecorder.BuildAccelerationStructures"/> is a
/// genuine per-frame path — a dynamic TLAS is rebuilt (or refitted) every frame
/// for any scene with moving objects — so it carries the same <b>0 B per
/// call</b> obligation as the rest of <c>Recording/</c>. Unlike the
/// <c>DrawMeshTasks*</c> forwards it is not a thin pass-through: it validates,
/// carves three native scratch spans, and runs a translator that fills
/// <c>VkAccelerationStructureBuildGeometryInfoKHR</c> /
/// <c>VkAccelerationStructureGeometryKHR</c> and a <c>ppBuildRangeInfos</c>
/// pointer array. The CSR encoding plus the
/// <c>stackalloc</c>-under-threshold rule exists precisely so none of that
/// allocates, and this benchmark is what keeps it that way.
/// </summary>
/// <remarks>
/// <para>Deliberately a separate class from <see cref="CommandRecorderBenchmarks"/>,
/// the <see cref="MeshShaderBenchmarks"/> precedent: this <see cref="Setup"/>
/// requires <c>VK_KHR_acceleration_structure</c> + <c>VK_KHR_ray_query</c> +
/// <c>VK_KHR_deferred_host_operations</c> and the <c>accelerationStructure</c>,
/// <c>rayQuery</c> and <c>bufferDeviceAddress</c> features, and a host without
/// ray tracing must not take the issue-29 canary
/// (<c>CommandRecorder.RenderingPass100Cmds</c>) down with it.</para>
/// <para>The measured shape is the per-frame one: <b>one</b> build,
/// <b>one</b> <c>Instances</c> geometry, which is well inside both stack
/// thresholds (8 builds / 16 geometries) and therefore takes the
/// <c>stackalloc</c> path. The setup builds a real BLAS so the TLAS instance
/// entry carries a real device address — <c>vkCmdBuildAccelerationStructures</c>
/// against a garbage reference is a VU violation even when the command buffer
/// is never submitted.</para>
/// </remarks>
[MemoryDiagnoser]
public unsafe class AccelerationStructureBenchmarks
{
    private const int BuildsPerInvoke = 1024;

    // Sixteen builds crosses CommandRecorder.BuildStackThreshold (8), so
    // BuildBlasBatch_16x1_1024 is the only benchmark that reaches the
    // ArrayPool leg. Note the threshold test is `&&`, so exceeding EITHER
    // threshold sends all three scratch buffers to the pool.
    private const int BatchBuilds = 16;

    private Instance          _instance = null!;
    private Device            _device   = null!;
    private CommandBufferPool _cmdPool  = null!;

    private Buffer _vertices;
    private Buffer _blasBacking;
    private Buffer _blasScratch;
    private Buffer _instances;
    private Buffer _tlasBacking;
    private Buffer _tlasScratch;

    private AccelerationStructure _blas;
    private AccelerationStructure _tlas;

    private ulong _tlasScratchAddress;
    private AccelerationStructureGeometry _instanceGeometry;

    // Hoisted so BuildBlasBatch_16x1_1024 measures only the recording call.
    // The triangle geometry is also what gives the Triangles arm of
    // AccelerationStructureGeometry.WriteNative — eight field writes, the
    // widest of the three — its only measured coverage; BuildTlas_1024 only
    // ever reaches the Instances arm.
    private AccelerationStructureGeometry _triangleGeometry;

    // Sixteen distinct BLASes suballocated into one backing buffer at
    // 256-byte-aligned offsets, with sixteen non-overlapping scratch ranges.
    // Distinct on purpose: reusing one destination or one scratch address
    // across a batch violates
    // VUID-vkCmdBuildAccelerationStructuresKHR-scratchData-03704 (and the
    // wrapper's own exact-match scratch guard), so a shortcut here would
    // measure a shape no correct caller can record.
    private Buffer                    _batchBacking;
    private Buffer                    _batchScratch;
    private AccelerationStructure[]   _batchStructures = null!;
    private AccelerationStructureBuild[]      _batchBuilds  = null!;
    private AccelerationStructureGeometry[]   _batchGeos    = null!;
    private AccelerationStructureBuildRange[] _batchRanges  = null!;

    [GlobalSetup]
    public void Setup()
    {
        _instance = Instance.Create(default);

        uint family = uint.MaxValue;
        var gpu = _instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            if (!info.SupportsExtension(DeviceExtensionNames.AccelerationStructure)) return false;
            if (!info.SupportsExtension(DeviceExtensionNames.RayQuery)) return false;
            if (!info.SupportsExtension(DeviceExtensionNames.DeferredHostOperations)) return false;
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                // Builds require a compute-capable pool
                // (VUID-vkCmdBuildAccelerationStructuresKHR-commandBuffer-cmdpool).
                if (info.QueueFamilies[i].SupportsGraphics && info.QueueFamilies[i].SupportsCompute)
                {
                    family = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });

        Utf8Name[] extensions =
        [
            VulkanExtensions.KhrAccelerationStructure,
            VulkanExtensions.KhrDeferredHostOperations,
            VulkanExtensions.KhrRayQuery,
        ];
        _device = gpu.CreateDevice(new DeviceDescription
        {
            Queues            = [new QueueRequest(family, count: 1, priority: 1.0f)],
            Extensions        = extensions,
            ConfigureFeatures = static (
                ref ChainBuilder<VkDeviceCreateInfo> chain,
                ref VkPhysicalDeviceFeatures2 _,
                ref VkPhysicalDeviceVulkan12Features f12,
                ref VkPhysicalDeviceVulkan13Features __,
                ref VkPhysicalDeviceVulkan14Features ___) =>
            {
                // Every build input, the scratch and the TLAS instance
                // references are device addresses.
                f12.bufferDeviceAddress = 1;
                ref var asFeatures = ref chain.Push<VkPhysicalDeviceAccelerationStructureFeaturesKHR>();
                asFeatures.accelerationStructure = 1;
                ref var rq = ref chain.Push<VkPhysicalDeviceRayQueryFeaturesKHR>();
                rq.rayQuery = 1;
            },
        });

        _cmdPool = new CommandBufferPool(_device, family);

        // ---- One triangle, one BLAS, built once. ----
        _vertices = _device.Allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = 3 * 3 * sizeof(float),
                Usage = BufferUsage.AccelerationStructureBuildInputReadOnly
                      | BufferUsage.ShaderDeviceAddress,
            },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
            });

        Span<float> v = _vertices.AsSpan<float>();
        v[0] = 0f; v[1] = 0f; v[2] = 0f;
        v[3] = 1f; v[4] = 0f; v[5] = 0f;
        v[6] = 0f; v[7] = 1f; v[8] = 0f;
        _vertices.Flush();

        _triangleGeometry = AccelerationStructureGeometry.Triangles(
            vertexAddress: _vertices.GetDeviceAddress(_device),
            vertexFormat: VkFormat.VK_FORMAT_R32G32B32_SFLOAT,
            vertexStride: 3 * sizeof(float),
            maxVertex: 2);
        AccelerationStructureGeometry triangles = _triangleGeometry;

        AccelerationStructureBuildSizes blasSizes;
        {
            Span<AccelerationStructureGeometry> geos = stackalloc AccelerationStructureGeometry[1];
            geos[0] = triangles;
            Span<uint> counts = stackalloc uint[1];
            counts[0] = 1;
            blasSizes = _device.GetAccelerationStructureBuildSizes(
                AccelerationStructureType.BottomLevel,
                AccelerationStructureBuildFlags.PreferFastTrace, geos, counts);
        }

        _blasBacking = CreateAsBuffer(blasSizes.AccelerationStructureSize);
        _blasScratch = CreateScratchBuffer(blasSizes.BuildScratchSize);
        _blas = _device.CreateAccelerationStructure(
            AccelerationStructureType.BottomLevel, in _blasBacking, 0,
            blasSizes.AccelerationStructureSize);

        ReadOnlySpan<MemoryBarrier> buildBarrier =
        [
            new MemoryBarrier
            {
                SrcStage  = Stage.AccelerationStructureBuild,
                SrcAccess = Access.AccelerationStructureWrite,
                DstStage  = Stage.AccelerationStructureBuild,
                DstAccess = Access.AccelerationStructureRead,
            },
        ];

        // Build the BLAS for real and wait, so the instance entry below holds a
        // live device address.
        using (var fencePool = new FencePool(_device))
        {
            Fence fence = fencePool.Acquire();
            var rec = _cmdPool.Begin();
            try
            {
                Span<AccelerationStructureBuild> builds = stackalloc AccelerationStructureBuild[1];
                builds[0] = new AccelerationStructureBuild
                {
                    Type           = AccelerationStructureType.BottomLevel,
                    Flags          = AccelerationStructureBuildFlags.PreferFastTrace,
                    Destination    = _blas,
                    ScratchAddress = _blasScratch.GetDeviceAddress(_device),
                    FirstGeometry  = 0,
                    GeometryCount  = 1,
                };
                Span<AccelerationStructureGeometry> geos = stackalloc AccelerationStructureGeometry[1];
                geos[0] = triangles;
                Span<AccelerationStructureBuildRange> ranges = stackalloc AccelerationStructureBuildRange[1];
                ranges[0] = AccelerationStructureBuildRange.Of(1);

                rec.BuildAccelerationStructures(builds, geos, ranges);
                rec.PipelineBarrier(buildBarrier, default, default);
                _device.GetQueue(family, 0).Submit2(ref rec, in fence);
                fence.Wait(TimeSpan.FromSeconds(10));
            }
            finally
            {
                rec.Dispose();
                fencePool.Release(fence);
            }
            _cmdPool.ResetForFrame();
        }

        // ---- One instance referencing that BLAS, and the TLAS over it. ----
        _instances = _device.Allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = (ulong)sizeof(VkAccelerationStructureInstanceKHR),
                Usage = BufferUsage.AccelerationStructureBuildInputReadOnly
                      | BufferUsage.ShaderDeviceAddress,
            },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
            });

        Span<VkAccelerationStructureInstanceKHR> inst =
            _instances.AsSpan<VkAccelerationStructureInstanceKHR>();
        inst[0] = default;
        inst[0].transform.matrix[0]  = 1f;
        inst[0].transform.matrix[5]  = 1f;
        inst[0].transform.matrix[10] = 1f;
        inst[0].mask = 0xFF;
        inst[0].accelerationStructureReference = _blas.GetDeviceAddress(_device);
        _instances.Flush();

        _instanceGeometry = AccelerationStructureGeometry.Instances(
            _instances.GetDeviceAddress(_device));

        AccelerationStructureBuildSizes tlasSizes;
        {
            Span<AccelerationStructureGeometry> geos = stackalloc AccelerationStructureGeometry[1];
            geos[0] = _instanceGeometry;
            Span<uint> counts = stackalloc uint[1];
            counts[0] = 1;
            tlasSizes = _device.GetAccelerationStructureBuildSizes(
                AccelerationStructureType.TopLevel,
                AccelerationStructureBuildFlags.PreferFastBuild, geos, counts);
        }

        _tlasBacking = CreateAsBuffer(tlasSizes.AccelerationStructureSize);
        _tlasScratch = CreateScratchBuffer(tlasSizes.BuildScratchSize);
        _tlas = _device.CreateAccelerationStructure(
            AccelerationStructureType.TopLevel, in _tlasBacking, 0,
            tlasSizes.AccelerationStructureSize);
        _tlasScratchAddress = _tlasScratch.GetDeviceAddress(_device);

        // ---- Sixteen BLASes for the batched, above-threshold row. ----
        if (!gpu.TryGetAccelerationStructureLimits(out AccelerationStructureLimits limits))
            throw new InvalidOperationException(
                "A physical device advertising VK_KHR_acceleration_structure must also advertise "
                + "VkPhysicalDeviceAccelerationStructurePropertiesKHR.");

        ulong asStride      = AlignUp(blasSizes.AccelerationStructureSize, 256);
        ulong scratchStride = AlignUp(
            blasSizes.BuildScratchSize == 0 ? 1 : blasSizes.BuildScratchSize,
            limits.MinScratchOffsetAlignment);

        _batchBacking = CreateAsBuffer(asStride * BatchBuilds);
        _batchScratch = CreateScratchBuffer(scratchStride * BatchBuilds);

        ulong scratchBase = _batchScratch.GetDeviceAddress(_device);
        if (scratchBase % limits.MinScratchOffsetAlignment != 0)
            throw new InvalidOperationException(
                $"Scratch buffer base address 0x{scratchBase:X} is not a multiple of "
                + $"minAccelerationStructureScratchOffsetAlignment ({limits.MinScratchOffsetAlignment}); "
                + "the benchmark would record a VUID-violating build "
                + "(VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03710).");

        _batchStructures = new AccelerationStructure[BatchBuilds];
        _batchBuilds     = new AccelerationStructureBuild[BatchBuilds];
        _batchGeos       = new AccelerationStructureGeometry[BatchBuilds];
        _batchRanges     = new AccelerationStructureBuildRange[BatchBuilds];

        for (int i = 0; i < BatchBuilds; i++)
        {
            _batchStructures[i] = _device.CreateAccelerationStructure(
                AccelerationStructureType.BottomLevel, in _batchBacking,
                asStride * (ulong)i, blasSizes.AccelerationStructureSize);

            _batchGeos[i]   = _triangleGeometry;
            _batchRanges[i] = AccelerationStructureBuildRange.Of(1);
            _batchBuilds[i] = new AccelerationStructureBuild
            {
                Type           = AccelerationStructureType.BottomLevel,
                Flags          = AccelerationStructureBuildFlags.PreferFastTrace,
                Destination    = _batchStructures[i],
                ScratchAddress = scratchBase + scratchStride * (ulong)i,
                FirstGeometry  = (uint)i,
                GeometryCount  = 1,
            };
        }

        // Warm: fault in the pool's first command buffer and JIT every
        // recording path so the measured runs hit steady-state reuse.
        BuildTlas_1024();
        BuildBlasBatch_16x1_1024();
    }

    private static ulong AlignUp(ulong value, ulong alignment)
        => (value + alignment - 1) / alignment * alignment;

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_batchStructures is not null)
            for (int i = 0; i < _batchStructures.Length; i++)
                _batchStructures[i].Dispose();
        _batchScratch.Dispose();
        _batchBacking.Dispose();
        _tlas.Dispose();
        _blas.Dispose();
        _tlasScratch.Dispose();
        _tlasBacking.Dispose();
        _instances.Dispose();
        _blasScratch.Dispose();
        _blasBacking.Dispose();
        _vertices.Dispose();
        _cmdPool?.Dispose();
        _device?.Dispose();
        _instance?.Dispose();
    }

    /// <summary>
    /// The per-frame TLAS rebuild, recorded 1024 times into one command buffer
    /// and never submitted. One build, one <c>Instances</c> geometry — inside
    /// both stack thresholds, so this measures the <c>stackalloc</c> path plus
    /// the translator. Expect <c>-</c> in <b>Allocated</b>.
    /// </summary>
    [Benchmark(OperationsPerInvoke = BuildsPerInvoke)]
    public void BuildTlas_1024()
    {
        // Dispose the recorder BEFORE ResetForFrame: Retire fires on Dispose,
        // not End, so the buffer must reach _spent before the reset drains
        // _spent -> _idle, or it never recycles and the pool ping-pongs two
        // buffers (#188/#199, docs/benchmarks.md).
        using (var rec = _cmdPool.Begin())
        {
            Span<AccelerationStructureBuild> builds = stackalloc AccelerationStructureBuild[1];
            builds[0] = new AccelerationStructureBuild
            {
                Type           = AccelerationStructureType.TopLevel,
                Flags          = AccelerationStructureBuildFlags.PreferFastBuild,
                Destination    = _tlas,
                ScratchAddress = _tlasScratchAddress,
                FirstGeometry  = 0,
                GeometryCount  = 1,
            };
            Span<AccelerationStructureGeometry> geos = stackalloc AccelerationStructureGeometry[1];
            geos[0] = _instanceGeometry;
            Span<AccelerationStructureBuildRange> ranges = stackalloc AccelerationStructureBuildRange[1];
            ranges[0] = AccelerationStructureBuildRange.Of(1);

            for (int i = 0; i < BuildsPerInvoke; i++)
                rec.BuildAccelerationStructures(builds, geos, ranges);
        }

        _cmdPool.ResetForFrame();
    }

    /// <summary>
    /// Sixteen BLAS builds, one triangle geometry each, recorded 1024 times
    /// into one command buffer and never submitted. Two gaps closed at once:
    /// </summary>
    /// <remarks>
    /// <para><b>The rental leg.</b> 16 builds is above
    /// <c>BuildStackThreshold = 8</c>, so all three native scratch buffers
    /// come from <see cref="System.Buffers.ArrayPool{T}"/> in nested
    /// <c>try/finally</c> rather than <c>stackalloc</c>. This is the only
    /// benchmark that reaches that path, and three rent/return pairs per call
    /// must still amortize to <c>-</c> in <b>Allocated</b>.</para>
    /// <para><b>The <c>Triangles</c> union arm.</b>
    /// <see cref="BuildTlas_1024"/> only ever drives
    /// <c>AccelerationStructureGeometry.WriteNative</c>'s <c>Instances</c> arm
    /// (three field writes). The <c>Triangles</c> arm is the widest at eight,
    /// and before this row it ran only in <c>[GlobalSetup]</c>, where BDN
    /// measures nothing. The <c>Aabbs</c> arm stays unmeasured — narrowest of
    /// the three, no per-frame consumer; its correctness is covered instead by
    /// the Tier-3 <c>AccelerationStructureTests.Blas_OverAabbs_*</c> builds
    /// (issue 206).</para>
    /// <para>The batch is a per-frame shape, not a load-time one: BLAS refits
    /// for skinned and deformable geometry are rebuilt every frame, and nine
    /// animated meshes already cross the threshold.</para>
    /// </remarks>
    [Benchmark(OperationsPerInvoke = BuildsPerInvoke)]
    public void BuildBlasBatch_16x1_1024()
    {
        using (var rec = _cmdPool.Begin())
        {
            for (int i = 0; i < BuildsPerInvoke; i++)
                rec.BuildAccelerationStructures(_batchBuilds, _batchGeos, _batchRanges);
        }

        _cmdPool.ResetForFrame();
    }

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
}
