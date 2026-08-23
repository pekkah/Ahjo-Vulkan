using System.Runtime.CompilerServices;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Covers the <c>VK_KHR_acceleration_structure</c> wrapper surface (#202): the
/// <see cref="AccelerationStructure"/> handle, creation and build-size queries
/// on <see cref="Device"/>, the three <see cref="CommandRecorder"/> commands,
/// the compacted-size <see cref="QueryPool"/> path, the TLAS
/// <see cref="DescriptorWrite"/>, and the gated entry-point loading in
/// <c>Internal/DeviceFunctionTable</c>.
/// </summary>
/// <remarks>
/// <para>Three tiers, deliberately weighted away from the one CI cannot run.
/// <b>Layout mirrors and factory plumbing</b> need no driver at all.
/// <b>Argument guards and the extension-not-enabled surface</b> are
/// <c>[gate:driver]</c>: every guard runs before any native call, and
/// <see cref="Buffer.FromRaw"/> supplies a non-null handle with no driver
/// involvement, so they run on any host with an ICD — ray-tracing-capable or
/// not.</para>
/// <para>Only the <b>RT tier</b> is <c>[gate:feature]</c>. The hosted
/// <c>windows-latest</c> runner has no GPU and no ray-tracing-capable ICD, so a
/// CI run reporting every one of those as skipped is the <em>expected</em>
/// outcome, not a gap to fix. They run on the maintainer's host.</para>
/// </remarks>
public sealed unsafe class AccelerationStructureTests
{
    // ---- Tier 1: driver-free. ----

    /// <summary>
    /// <see cref="AccelerationStructureBuildRange"/> is cast in place to
    /// <c>VkAccelerationStructureBuildRangeInfoKHR</c> by
    /// <see cref="CommandRecorder.BuildAccelerationStructures"/> — never
    /// copied — so a reordered or resized field would silently scramble every
    /// build with no compile error and no validation error. This pins it.
    /// </summary>
    [Fact]
    public void BuildRange_MirrorsNativeLayout()
    {
        Assert.Equal(
            Unsafe.SizeOf<VkAccelerationStructureBuildRangeInfoKHR>(),
            Unsafe.SizeOf<AccelerationStructureBuildRange>());

        var managed = new AccelerationStructureBuildRange
        {
            PrimitiveCount  = 0x1111_1111,
            PrimitiveOffset = 0x2222_2222,
            FirstVertex     = 0x3333_3333,
            TransformOffset = 0x4444_4444,
        };

        // The cast the recorder performs, done here so a field reorder shows
        // up as mismatched values rather than as wrong geometry on a GPU.
        var native = *(VkAccelerationStructureBuildRangeInfoKHR*)&managed;

        Assert.Equal(0x1111_1111u, native.primitiveCount);
        Assert.Equal(0x2222_2222u, native.primitiveOffset);
        Assert.Equal(0x3333_3333u, native.firstVertex);
        Assert.Equal(0x4444_4444u, native.transformOffset);
    }

    [Fact]
    public void BuildRange_Of_SetsPrimitiveCountAndDefaultsRest()
    {
        AccelerationStructureBuildRange r = AccelerationStructureBuildRange.Of(42);
        Assert.Equal(42u, r.PrimitiveCount);
        Assert.Equal(0u, r.PrimitiveOffset);
        Assert.Equal(0u, r.FirstVertex);
        Assert.Equal(0u, r.TransformOffset);
    }

    [Fact]
    public void Geometry_Triangles_PopulatesTriangleMembers()
    {
        AccelerationStructureGeometry g = AccelerationStructureGeometry.Triangles(
            vertexAddress: 0x1000,
            vertexFormat: VkFormat.VK_FORMAT_R32G32B32_SFLOAT,
            vertexStride: 12,
            maxVertex: 2,
            indexAddress: 0x2000,
            indexType: VkIndexType.VK_INDEX_TYPE_UINT32,
            transformAddress: 0x3000);

        Assert.Equal(GeometryKind.Triangles, g.Kind);
        Assert.Equal(GeometryFlags.Opaque, g.Flags);
        Assert.Equal(0x1000ul, g.Address);
        Assert.Equal(12ul, g.Stride);
        Assert.Equal(0x2000ul, g.IndexAddress);
        Assert.Equal(0x3000ul, g.TransformAddress);
        Assert.Equal(2u, g.MaxVertex);
        Assert.Equal(VkFormat.VK_FORMAT_R32G32B32_SFLOAT, g.VertexFormat);
        Assert.Equal(VkIndexType.VK_INDEX_TYPE_UINT32, g.IndexType);
        Assert.False(g.ArrayOfPointers);

        g.WriteNative(out VkAccelerationStructureGeometryKHR n);
        Assert.Equal(VkGeometryTypeKHR.VK_GEOMETRY_TYPE_TRIANGLES_KHR, n.geometryType);
        Assert.Equal((uint)GeometryFlags.Opaque, n.flags);
        Assert.Equal(0x1000ul, n.geometry.triangles.vertexData.deviceAddress);
        Assert.Equal(12ul, n.geometry.triangles.vertexStride);
        Assert.Equal(2u, n.geometry.triangles.maxVertex);
        Assert.Equal(0x2000ul, n.geometry.triangles.indexData.deviceAddress);
        Assert.Equal(0x3000ul, n.geometry.triangles.transformData.deviceAddress);
    }

    [Fact]
    public void Geometry_Aabbs_PopulatesAabbMembersAndLeavesTriangleMembersUnused()
    {
        AccelerationStructureGeometry g = AccelerationStructureGeometry.Aabbs(
            address: 0x4000, stride: 24);

        Assert.Equal(GeometryKind.Aabbs, g.Kind);
        Assert.Equal(GeometryFlags.Opaque, g.Flags);
        Assert.Equal(0x4000ul, g.Address);
        Assert.Equal(24ul, g.Stride);
        // Per-kind unused members.
        Assert.Equal(0ul, g.IndexAddress);
        Assert.Equal(0ul, g.TransformAddress);
        Assert.Equal(0u, g.MaxVertex);
        Assert.Equal(VkFormat.VK_FORMAT_UNDEFINED, g.VertexFormat);
        Assert.Equal(VkIndexType.VK_INDEX_TYPE_NONE_KHR, g.IndexType);
        Assert.False(g.ArrayOfPointers);

        g.WriteNative(out VkAccelerationStructureGeometryKHR n);
        Assert.Equal(VkGeometryTypeKHR.VK_GEOMETRY_TYPE_AABBS_KHR, n.geometryType);
        Assert.Equal(0x4000ul, n.geometry.aabbs.data.deviceAddress);
        Assert.Equal(24ul, n.geometry.aabbs.stride);
    }

    [Fact]
    public void Geometry_Instances_PopulatesInstanceMembersAndLeavesRestUnused()
    {
        AccelerationStructureGeometry g = AccelerationStructureGeometry.Instances(
            address: 0x5000, arrayOfPointers: true);

        Assert.Equal(GeometryKind.Instances, g.Kind);
        // The instance factory defaults to None, not Opaque: per-instance
        // opacity belongs on the instance entry's own flags word.
        Assert.Equal(GeometryFlags.None, g.Flags);
        Assert.Equal(0x5000ul, g.Address);
        Assert.True(g.ArrayOfPointers);
        // Per-kind unused members.
        Assert.Equal(0ul, g.Stride);
        Assert.Equal(0ul, g.IndexAddress);
        Assert.Equal(0ul, g.TransformAddress);
        Assert.Equal(0u, g.MaxVertex);
        Assert.Equal(VkIndexType.VK_INDEX_TYPE_NONE_KHR, g.IndexType);

        g.WriteNative(out VkAccelerationStructureGeometryKHR n);
        Assert.Equal(VkGeometryTypeKHR.VK_GEOMETRY_TYPE_INSTANCES_KHR, n.geometryType);
        Assert.Equal(1u, n.geometry.instances.arrayOfPointers);
        Assert.Equal(0x5000ul, n.geometry.instances.data.deviceAddress);
    }

    [Fact]
    public void AccelerationStructure_BorrowedHandle_ReportsUnownedAndUnknownSize()
    {
        AccelerationStructure borrowed = AccelerationStructure.FromRaw(unchecked((nint)0xDEADBEEF));
        Assert.False(borrowed.OwnsHandle);
        Assert.False(borrowed.IsNull);
        // 0 means *unknown*, never *empty* — a zero-sized structure cannot be
        // created.
        Assert.Equal(0ul, borrowed.Size);
        // Must not dispatch vkDestroyAccelerationStructureKHR through a null
        // device: this would access-violate the loader rather than fail.
        borrowed.Dispose();

        AccelerationStructure empty = default;
        Assert.True(empty.IsNull);
        Assert.False(empty.OwnsHandle);
        Assert.Equal(0ul, empty.Size);
        empty.Dispose();
    }

    [Fact]
    public void AccelerationStructure_ObjectType_IsAccelerationStructure()
        => Assert.Equal(
            VkObjectType.VK_OBJECT_TYPE_ACCELERATION_STRUCTURE_KHR,
            AccelerationStructure.ObjectType);

    [Fact]
    public void DescriptorWrite_AccelerationStructureFactory_PopulatesAsKind()
    {
        AccelerationStructure structure = AccelerationStructure.FromRaw(0x1234);
        DescriptorWrite w = DescriptorWrite.AccelerationStructure(
            binding: 2, arrayElement: 5, in structure);

        Assert.Equal(2u, w._binding);
        Assert.Equal(5u, w._arrayElement);
        Assert.Equal(DescriptorWrite.Kind.AccelerationStructure, w._kind);
        // No type parameter — this is the only descriptor type the write can
        // have.
        Assert.Equal(VkDescriptorType.VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR, w._type);
        Assert.Equal((nint)0x1234, (nint)w._accelerationStructure);
    }

    /// <summary>
    /// The native form of an acceleration-structure write, asserted at the
    /// <see cref="DescriptorWriteBuilder"/> level rather than through a real
    /// <see cref="DescriptorSetExtensions.Update"/>: binding one for real needs
    /// the extension, which no CI host has, and the interesting part is the
    /// <c>pNext</c> chaining rather than the driver's reaction to it.
    /// </summary>
    [Fact]
    public void BuildWrites_AccelerationStructure_ChainsPNextAndNullsInfoPointers()
    {
        AccelerationStructure structure = AccelerationStructure.FromRaw(0xABCD);
        Span<DescriptorWrite> writes =
        [
            DescriptorWrite.AccelerationStructure(binding: 0, arrayElement: 0, in structure),
        ];
        Span<VkWriteDescriptorSet> raws = stackalloc VkWriteDescriptorSet[1];
        Span<VkWriteDescriptorSetAccelerationStructureKHR> chains =
            stackalloc VkWriteDescriptorSetAccelerationStructureKHR[1];

        fixed (DescriptorWrite* pWrites = writes)
        fixed (VkWriteDescriptorSetAccelerationStructureKHR* pChains = chains)
        {
            DescriptorWriteBuilder.BuildWrites(writes, setHandle: null, raws, chains);

            Assert.Equal(
                VkDescriptorType.VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR,
                raws[0].descriptorType);
            Assert.Equal(1u, raws[0].descriptorCount);
            // The whole point: an AS write carries neither info pointer, and
            // instead chains its handle through pNext.
            Assert.True(raws[0].pBufferInfo == null);
            Assert.True(raws[0].pImageInfo == null);
            Assert.True(raws[0].pNext != null);
            Assert.True(raws[0].pNext == pChains);

            Assert.Equal(
                VkStructureType.VK_STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET_ACCELERATION_STRUCTURE_KHR,
                pChains[0].sType);
            Assert.Equal(1u, pChains[0].accelerationStructureCount);
            // pAccelerationStructures points at the handle stored inline on the
            // write — which is why the caller must pin the writes span.
            Assert.True(pChains[0].pAccelerationStructures != null);
            Assert.Equal((nint)0xABCD, (nint)(*pChains[0].pAccelerationStructures));
            Assert.True((byte*)pChains[0].pAccelerationStructures >= (byte*)pWrites);
        }
    }

    /// <summary>
    /// A buffer or image write must keep its old shape: no <c>pNext</c>, the
    /// matching info pointer set. The chains span exists but nothing points at
    /// it.
    /// </summary>
    [Fact]
    public void BuildWrites_BufferWrite_LeavesPNextNull()
    {
        var info = new BufferDescriptorWrite((VkBuffer_T*)0x1234, offset: 0, range: 64);
        Span<DescriptorWrite> writes =
        [
            DescriptorWrite.Buffer(0, 0, VkDescriptorType.VK_DESCRIPTOR_TYPE_STORAGE_BUFFER, in info),
        ];
        Span<VkWriteDescriptorSet> raws = stackalloc VkWriteDescriptorSet[1];
        Span<VkWriteDescriptorSetAccelerationStructureKHR> chains =
            stackalloc VkWriteDescriptorSetAccelerationStructureKHR[1];

        fixed (DescriptorWrite* _ = writes)
        fixed (VkWriteDescriptorSetAccelerationStructureKHR* __ = chains)
        {
            DescriptorWriteBuilder.BuildWrites(writes, setHandle: null, raws, chains);
            Assert.True(raws[0].pNext == null);
            Assert.True(raws[0].pBufferInfo != null);
            Assert.True(raws[0].pImageInfo == null);
        }
    }

    // ---- Tier 2: [gate:driver] — any host with an ICD, RT-capable or not. ----

    /// <summary>
    /// The whole extension-not-enabled surface on a device created without
    /// <c>VK_KHR_acceleration_structure</c>: every public entry point throws an
    /// <see cref="InvalidOperationException"/> naming the extension rather than
    /// dispatching through a null pointer.
    /// </summary>
    [Fact]
    public void ExtensionNotEnabled_EveryEntryPoint_ThrowsNamingTheExtension()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);

        // A real buffer, so CreateAccelerationStructure's argument guards all
        // pass and it reaches the null-pointer check.
        using var backing = device.Allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = 4096,
                Usage = BufferUsage.AccelerationStructureStorage | BufferUsage.ShaderDeviceAddress,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        AssertNotAvailable(Assert.Throws<InvalidOperationException>(() =>
            device.CreateAccelerationStructure(
                AccelerationStructureType.BottomLevel, in backing, offset: 0, size: 1024)));

        AssertNotAvailable(Assert.Throws<InvalidOperationException>(() =>
        {
            Span<AccelerationStructureGeometry> geos =
            [
                AccelerationStructureGeometry.Triangles(
                    0x1000, VkFormat.VK_FORMAT_R32G32B32_SFLOAT, 12, 2),
            ];
            Span<uint> counts = [1];
            device.GetAccelerationStructureBuildSizes(
                AccelerationStructureType.BottomLevel,
                AccelerationStructureBuildFlags.PreferFastTrace, geos, counts);
        }));

        AssertNotAvailable(Assert.Throws<InvalidOperationException>(() =>
            device.CreateQueryPool(QueryType.AccelerationStructureCompactedSize, 1)));

        AccelerationStructure borrowed = AccelerationStructure.FromRaw(0x1234);
        AssertNotAvailable(Assert.Throws<InvalidOperationException>(() =>
            borrowed.GetDeviceAddress(device)));

        // The recorder is a ref struct, so its methods cannot be called from a
        // lambda; each throw is caught inline instead.
        using var cmdPool = new CommandBufferPool(device, family);
        using (var rec = cmdPool.Begin())
        {
            InvalidOperationException? buildEx = null;
            try
            {
                Span<AccelerationStructureBuild>      builds = stackalloc AccelerationStructureBuild[1];
                Span<AccelerationStructureGeometry>   geos   = stackalloc AccelerationStructureGeometry[1];
                Span<AccelerationStructureBuildRange> ranges = stackalloc AccelerationStructureBuildRange[1];
                rec.BuildAccelerationStructures(builds, geos, ranges);
            }
            catch (InvalidOperationException e) { buildEx = e; }
            AssertNotAvailable(Assert.IsType<InvalidOperationException>(buildEx));

            InvalidOperationException? writeEx = null;
            try
            {
                Span<AccelerationStructure> structures = stackalloc AccelerationStructure[1];
                rec.WriteAccelerationStructuresProperties(structures, default, 0);
            }
            catch (InvalidOperationException e) { writeEx = e; }
            AssertNotAvailable(Assert.IsType<InvalidOperationException>(writeEx));

            InvalidOperationException? copyEx = null;
            try
            {
                rec.CopyAccelerationStructure(
                    default, default, AccelerationStructureCopyMode.Compact);
            }
            catch (InvalidOperationException e) { copyEx = e; }
            AssertNotAvailable(Assert.IsType<InvalidOperationException>(copyEx));
        }

        static void AssertNotAvailable(InvalidOperationException ex)
        {
            Assert.Contains("VK_KHR_acceleration_structure", ex.Message, StringComparison.Ordinal);
            Assert.Contains("not available on this device", ex.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Argument guards on <see cref="Device.CreateAccelerationStructure"/> that
    /// need no usage/size knowledge — a borrowed
    /// <see cref="Buffer.FromRaw"/> handle reports size 0 and
    /// <see cref="BufferUsage.None"/>, which those two guards read as
    /// <em>unknown</em> and skip.
    /// </summary>
    /// <remarks>
    /// These also pin the <b>guard ordering</b>: the device-independent misuse
    /// must be reported before the null-pointer extension check, so the caller
    /// gets the more actionable message on a device that happens to lack the
    /// extension. The #201 precedent.
    /// </remarks>
    [Fact]
    public void CreateAccelerationStructure_ArgumentGuards_RunBeforeTheExtensionCheck()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);

        Buffer nullBuffer = default;
        var nullEx = Assert.Throws<ArgumentException>(() =>
            device.CreateAccelerationStructure(
                AccelerationStructureType.BottomLevel, in nullBuffer, 0, 1024));
        Assert.Contains("non-null backing buffer", nullEx.Message, StringComparison.Ordinal);

        Buffer borrowed = Buffer.FromRaw(0x1000);
        Assert.Equal(0ul, borrowed.Size);
        Assert.Equal(BufferUsage.None, borrowed.Usage);

        var sizeEx = Assert.Throws<ArgumentOutOfRangeException>(() =>
            device.CreateAccelerationStructure(
                AccelerationStructureType.BottomLevel, in borrowed, 0, 0));
        Assert.Contains("at least one byte", sizeEx.Message, StringComparison.Ordinal);

        var offsetEx = Assert.Throws<ArgumentException>(() =>
            device.CreateAccelerationStructure(
                AccelerationStructureType.BottomLevel, in borrowed, offset: 128, size: 1024));
        Assert.Contains("multiple of 256", offsetEx.Message, StringComparison.Ordinal);
        Assert.Contains("offset-03734", offsetEx.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The two guards that need a real buffer's cached
    /// <see cref="Buffer.Size"/> / <see cref="Buffer.Usage"/>.
    /// </summary>
    [Fact]
    public void CreateAccelerationStructure_SizeAndUsageGuards_UseTheBuffersCachedMetadata()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);

        using var storage = device.Allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = 4096,
                Usage = BufferUsage.AccelerationStructureStorage | BufferUsage.ShaderDeviceAddress,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        var pastEnd = Assert.Throws<ArgumentOutOfRangeException>(() =>
            device.CreateAccelerationStructure(
                AccelerationStructureType.BottomLevel, in storage, offset: 4096 - 256, size: 512));
        Assert.Contains("offset-03616", pastEnd.Message, StringComparison.Ordinal);

        using var wrongUsage = device.Allocator.CreateBuffer(
            new BufferDescription { Size = 4096, Usage = BufferUsage.StorageBuffer },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        var usageEx = Assert.Throws<ArgumentException>(() =>
            device.CreateAccelerationStructure(
                AccelerationStructureType.BottomLevel, in wrongUsage, 0, 1024));
        Assert.Contains("AccelerationStructureStorage", usageEx.Message, StringComparison.Ordinal);
        Assert.Contains("buffer-03614", usageEx.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetAccelerationStructureBuildSizes_MismatchedPrimitiveCounts_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using Device? device = TryCreateAccelerationStructureDevice(instance, out _);
        TestGate.RequireDeviceFeature(device is not null, RtSkipReason);

        var ex = Assert.Throws<ArgumentException>(() =>
        {
            Span<AccelerationStructureGeometry> geos =
            [
                AccelerationStructureGeometry.Triangles(
                    0x1000, VkFormat.VK_FORMAT_R32G32B32_SFLOAT, 12, 2),
                AccelerationStructureGeometry.Aabbs(0x2000, 24),
            ];
            Span<uint> counts = [1];
            device!.GetAccelerationStructureBuildSizes(
                AccelerationStructureType.BottomLevel,
                AccelerationStructureBuildFlags.None, geos, counts);
        });
        Assert.Contains("one primitive count per geometry", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateQueryPool_UnknownType_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);

        var ex = Assert.Throws<ArgumentException>(() =>
            device.CreateQueryPool(QueryType.Unknown, 1));
        Assert.Contains("borrowed-handle sentinel", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateQueryPool_TimestampOverload_ReportsTimestampType()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out _);

        using QueryPool pool = device.CreateQueryPool(4);
        Assert.Equal(QueryType.Timestamp, pool.Type);
        Assert.Equal(4u, pool.QueryCount);

        // Unknown on a borrowed pool means *unknown*, never *timestamp*.
        Assert.Equal(QueryType.Unknown, QueryPool.FromRaw(0x1234).Type);
        Assert.Equal(QueryType.Unknown, default(QueryPool).Type);
    }

    /// <summary>
    /// A borrowed pool has no type the wrapper knows, so
    /// <see cref="CommandRecorder.WriteAccelerationStructuresProperties"/>
    /// refuses it outright rather than guessing a <c>queryType</c>.
    /// </summary>
    [Fact]
    public void WriteAccelerationStructuresProperties_BorrowedPool_Throws()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using Device? device = TryCreateAccelerationStructureDevice(instance, out uint family);
        TestGate.RequireDeviceFeature(device is not null, RtSkipReason);

        using var cmdPool = new CommandBufferPool(device!, family);
        InvalidOperationException? ex = null;
        using (var rec = cmdPool.Begin())
        {
            Span<AccelerationStructure> structures = stackalloc AccelerationStructure[1];
            structures[0] = AccelerationStructure.FromRaw(0x1234);
            QueryPool borrowed = QueryPool.FromRaw(0x5678);
            try
            {
                rec.WriteAccelerationStructuresProperties(structures, in borrowed, 0);
            }
            catch (InvalidOperationException e) { ex = e; }
        }

        var ioe = Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("QueryType.Unknown", ioe.Message, StringComparison.Ordinal);
        Assert.Contains("queryPool-02493", ioe.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryGetAccelerationStructureLimits_WithoutTheExtension_ReturnsFalseAndDefault()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
                if (info.QueueFamilies[i].SupportsGraphics)
                    return true;
            return false;
        });

        // Whichever way this GPU answers, the contract holds: false must leave
        // `limits` at default and must NOT throw, and true must report a
        // non-zero, power-of-two scratch alignment.
        if (gpu.TryGetAccelerationStructureLimits(out AccelerationStructureLimits limits))
        {
            Assert.NotEqual(0u, limits.MinScratchOffsetAlignment);
            Assert.Equal(0u, limits.MinScratchOffsetAlignment & (limits.MinScratchOffsetAlignment - 1));
            Assert.NotEqual(0ul, limits.MaxInstanceCount);
        }
        else
        {
            Assert.Equal(default, limits);
        }
    }

    // ---- Tier 3: [gate:feature] — RT-capable host only. ----

    /// <summary>
    /// The BLAS round trip: size a one-triangle build, allocate backing and
    /// scratch, create the structure, build it, submit, and read back a
    /// non-zero device address.
    /// </summary>
    [Fact]
    public void Blas_BuildAndGetDeviceAddress_RoundTrips()
    {
        TestGate.RequireDriver();
        TestGate.RequireValidationLayer();

        var errors = new List<DebugMessage>();
        using var instance = CreateValidatedInstance(errors);
        using Device? device = TryCreateAccelerationStructureDevice(instance, out uint family);
        TestGate.RequireDeviceFeature(device is not null, RtSkipReason);

        using var fixture = new BlasFixture(device!, family);

        Assert.NotEqual(0ul, fixture.Sizes.AccelerationStructureSize);
        Assert.NotEqual(0ul, fixture.Sizes.BuildScratchSize);

        fixture.RecordBuildAndSubmit(AccelerationStructureBuildFlags.PreferFastTrace);

        ulong address = fixture.Blas.GetDeviceAddress(device!);
        Assert.NotEqual(0ul, address);
        AssertNoValidationErrors(errors);
    }

    /// <summary>
    /// A TLAS over one instance referencing the BLAS by device address. The
    /// oracle is the validation layer: a clean submit means the instance
    /// buffer, the <c>Instances</c> geometry and the barrier were all accepted.
    /// </summary>
    [Fact]
    public void Tlas_OverOneInstance_BuildsWithoutValidationErrors()
    {
        TestGate.RequireDriver();
        TestGate.RequireValidationLayer();

        // The layer is the oracle here: a TLAS build has almost nothing to
        // assert on afterwards (a non-zero device address proves the handle
        // exists, not that the instance data, the Instances geometry and the
        // barrier were accepted). Capturing errors is what makes this a test.
        var errors = new List<DebugMessage>();
        using var instance = CreateValidatedInstance(errors);
        using Device? device = TryCreateAccelerationStructureDevice(instance, out uint family);
        TestGate.RequireDeviceFeature(device is not null, RtSkipReason);

        using var fixture = new BlasFixture(device!, family);
        fixture.RecordBuildAndSubmit(AccelerationStructureBuildFlags.PreferFastTrace);
        ulong blasAddress = fixture.Blas.GetDeviceAddress(device!);
        Assert.NotEqual(0ul, blasAddress);

        // One VkAccelerationStructureInstanceKHR, written with the generated
        // struct — the wrapper deliberately does not mirror its bitfields.
        using var instanceBuffer = device!.Allocator.CreateBuffer(
            new BufferDescription
            {
                Size = (ulong)sizeof(VkAccelerationStructureInstanceKHR),
                Usage = BufferUsage.AccelerationStructureBuildInputReadOnly
                      | BufferUsage.ShaderDeviceAddress | BufferUsage.TransferDst,
            },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
            });

        Span<VkAccelerationStructureInstanceKHR> instances =
            instanceBuffer.AsSpan<VkAccelerationStructureInstanceKHR>();
        instances[0] = default;
        // Identity 3x4 transform, row-major.
        instances[0].transform.matrix[0] = 1f;
        instances[0].transform.matrix[5] = 1f;
        instances[0].transform.matrix[10] = 1f;
        instances[0].mask = 0xFF;
        instances[0].accelerationStructureReference = blasAddress;
        instanceBuffer.Flush();

        AccelerationStructureGeometry instanceGeo =
            AccelerationStructureGeometry.Instances(instanceBuffer.GetDeviceAddress(device));

        Span<AccelerationStructureGeometry> geos = [instanceGeo];
        Span<uint> maxCounts = [1];
        AccelerationStructureBuildSizes tlasSizes = device.GetAccelerationStructureBuildSizes(
            AccelerationStructureType.TopLevel,
            AccelerationStructureBuildFlags.PreferFastBuild, geos, maxCounts);
        Assert.NotEqual(0ul, tlasSizes.AccelerationStructureSize);

        using var tlasBacking = CreateAsBuffer(device, tlasSizes.AccelerationStructureSize);
        using var tlasScratch = CreateScratchBuffer(device, tlasSizes.BuildScratchSize);
        using AccelerationStructure tlas = device.CreateAccelerationStructure(
            AccelerationStructureType.TopLevel, in tlasBacking, 0, tlasSizes.AccelerationStructureSize);

        using var cmdPool = new CommandBufferPool(device, family);
        var rec = cmdPool.Begin();
        try
        {
            Span<AccelerationStructureBuild> builds = stackalloc AccelerationStructureBuild[1];
            builds[0] = new AccelerationStructureBuild
            {
                Type           = AccelerationStructureType.TopLevel,
                Flags          = AccelerationStructureBuildFlags.PreferFastBuild,
                Destination    = tlas,
                ScratchAddress = tlasScratch.GetDeviceAddress(device),
                FirstGeometry  = 0,
                GeometryCount  = 1,
            };
            Span<AccelerationStructureBuildRange> ranges = stackalloc AccelerationStructureBuildRange[1];
            ranges[0] = AccelerationStructureBuildRange.Of(1);
            rec.BuildAccelerationStructures(builds, geos, ranges);
            SubmitAndWait(device, family, ref rec);
        }
        finally { rec.Dispose(); }

        Assert.NotEqual(0ul, tlas.GetDeviceAddress(device));
        AssertNoValidationErrors(errors);
    }

    /// <summary>
    /// The full compaction round trip, which is the executable form of the
    /// "compaction changes the device address" rule.
    /// </summary>
    [Fact]
    public void Compaction_QuerySizeCopyAndAddressDiffers()
    {
        TestGate.RequireDriver();
        TestGate.RequireValidationLayer();

        // The layer also checks what the asserts cannot: that the queries were
        // reset by a submitted reset before the write executed
        // (VUID-vkCmdWriteAccelerationStructuresPropertiesKHR-queryPool-02494)
        // and that the source was built with AllowCompaction
        // (-accelerationStructures-03431 / VUID-VkCopyAccelerationStructureInfoKHR-src-03411).
        var errors = new List<DebugMessage>();
        using var instance = CreateValidatedInstance(errors);
        using Device? device = TryCreateAccelerationStructureDevice(instance, out uint family);
        TestGate.RequireDeviceFeature(device is not null, RtSkipReason);

        using var fixture = new BlasFixture(device!, family);
        fixture.RecordBuildAndSubmit(
            AccelerationStructureBuildFlags.PreferFastTrace
            | AccelerationStructureBuildFlags.AllowCompaction);

        ulong originalAddress = fixture.Blas.GetDeviceAddress(device!);

        using QueryPool pool = device!.CreateQueryPool(
            QueryType.AccelerationStructureCompactedSize, 1);
        Assert.Equal(QueryType.AccelerationStructureCompactedSize, pool.Type);

        using var cmdPool = new CommandBufferPool(device, family);
        var queryRec = cmdPool.Begin();
        try
        {
            // The build is already complete (submitted and waited above), but
            // the reset must be submitted before the write executes.
            queryRec.ResetQueryPool(in pool, 0, 1);
            Span<AccelerationStructure> structures = stackalloc AccelerationStructure[1];
            structures[0] = fixture.Blas;
            queryRec.WriteAccelerationStructuresProperties(structures, in pool, 0);
            SubmitAndWait(device, family, ref queryRec);
        }
        finally { queryRec.Dispose(); }

        Span<ulong> sizes = stackalloc ulong[1];
        pool.GetResults(0, sizes);
        Assert.NotEqual(0ul, sizes[0]);
        Assert.True(sizes[0] <= fixture.Sizes.AccelerationStructureSize,
            $"compacted size {sizes[0]} should not exceed the original "
            + $"{fixture.Sizes.AccelerationStructureSize}");

        using var compactedBacking = CreateAsBuffer(device, sizes[0]);
        using AccelerationStructure compacted = device.CreateAccelerationStructure(
            AccelerationStructureType.BottomLevel, in compactedBacking, 0, sizes[0]);

        var copyRec = cmdPool.Begin();
        try
        {
            copyRec.CopyAccelerationStructure(
                fixture.Blas, compacted, AccelerationStructureCopyMode.Compact);
            SubmitAndWait(device, family, ref copyRec);
        }
        finally { copyRec.Dispose(); }

        ulong compactedAddress = compacted.GetDeviceAddress(device);
        Assert.NotEqual(0ul, compactedAddress);
        // H6 made executable: the compacted copy lives in a different buffer,
        // so every TLAS over the original must be rebuilt against this value.
        Assert.NotEqual(originalAddress, compactedAddress);
        AssertNoValidationErrors(errors);
    }

    /// <summary>
    /// <see cref="CommandRecorder.BuildAccelerationStructures"/> is a per-frame
    /// path (a dynamic TLAS is rebuilt every frame), so the one-build /
    /// one-geometry shape must allocate nothing. The
    /// <c>MeshPipeline_Build_IsZeroAllocation</c> shape.
    /// </summary>
    [Fact]
    public void BuildAccelerationStructures_OneBuildOneGeometry_IsZeroAllocation()
    {
        TestGate.RequireDriver();

        using var instance = Instance.Create(default);
        using Device? device = TryCreateAccelerationStructureDevice(instance, out uint family);
        TestGate.RequireDeviceFeature(device is not null, RtSkipReason);

        using var fixture = new BlasFixture(device!, family);
        using var cmdPool = new CommandBufferPool(device!, family);

        ulong scratchAddress = fixture.Scratch.GetDeviceAddress(device!);
        AccelerationStructure blas = fixture.Blas;
        AccelerationStructureGeometry geometry = fixture.Geometry;

        bool priorValidation = AhjoValidation.Enabled;
        AhjoValidation.Enabled = false;
        try
        {
            using var rec = cmdPool.Begin();

            // The spans are carved once and re-passed: this measures the
            // recording call, not span construction. A local function is not an
            // option — `rec` is a ref local and cannot be captured (CS8175) —
            // so the loops are written out.
            Span<AccelerationStructureBuild> builds = stackalloc AccelerationStructureBuild[1];
            builds[0] = new AccelerationStructureBuild
            {
                Type           = AccelerationStructureType.BottomLevel,
                Flags          = AccelerationStructureBuildFlags.PreferFastTrace,
                Destination    = blas,
                ScratchAddress = scratchAddress,
                FirstGeometry  = 0,
                GeometryCount  = 1,
            };
            Span<AccelerationStructureGeometry> geos = stackalloc AccelerationStructureGeometry[1];
            geos[0] = geometry;
            Span<AccelerationStructureBuildRange> ranges = stackalloc AccelerationStructureBuildRange[1];
            ranges[0] = AccelerationStructureBuildRange.Of(1);

            // Warm: JIT + tier-up on every path the measured loop touches.
            for (int i = 0; i < 32; i++)
                rec.BuildAccelerationStructures(builds, geos, ranges);

            // Two measured passes: a tier-1 -> tier-2 promotion can still fire
            // on the first measurement-sized loop and charge a one-shot
            // allocation to this thread. Only the second is asserted on.
            long before1 = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++)
                rec.BuildAccelerationStructures(builds, geos, ranges);
            _ = GC.GetAllocatedBytesForCurrentThread() - before1;

            long before2 = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++)
                rec.BuildAccelerationStructures(builds, geos, ranges);
            long after2 = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(0, after2 - before2);
        }
        finally
        {
            AhjoValidation.Enabled = priorValidation;
        }
    }

    // ---- Fixtures and helpers ----

    private const string RtSkipReason =
        "Device does not expose VK_KHR_acceleration_structure + VK_KHR_ray_query with the "
        + "accelerationStructure / rayQuery / bufferDeviceAddress features.";

    /// <summary>
    /// A built-once, one-triangle BLAS plus everything it needs: the vertex
    /// buffer, the sized backing and scratch buffers, and the geometry
    /// description. Shared by every Tier-3 test so the setup is written once.
    /// </summary>
    private sealed class BlasFixture : IDisposable
    {
        private readonly Device _device;
        private readonly uint   _family;
        private readonly Buffer _vertices;
        private readonly Buffer _backing;

        public Buffer                          Scratch  { get; }
        public AccelerationStructure           Blas     { get; }
        public AccelerationStructureGeometry   Geometry { get; }
        public AccelerationStructureBuildSizes Sizes    { get; }

        public BlasFixture(Device device, uint family)
        {
            _device = device;
            _family = family;

            _vertices = device.Allocator.CreateBuffer(
                new BufferDescription
                {
                    Size  = 3 * 3 * sizeof(float),
                    Usage = BufferUsage.AccelerationStructureBuildInputReadOnly
                          | BufferUsage.ShaderDeviceAddress | BufferUsage.TransferDst,
                },
                new AllocationDescription
                {
                    Usage = MemoryUsage.AutoPreferHost,
                    Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
                });

            Span<float> v = _vertices.AsSpan<float>();
            v[0] = 0f;  v[1] = 0f;  v[2] = 0f;
            v[3] = 1f;  v[4] = 0f;  v[5] = 0f;
            v[6] = 0f;  v[7] = 1f;  v[8] = 0f;
            _vertices.Flush();

            Geometry = AccelerationStructureGeometry.Triangles(
                vertexAddress: _vertices.GetDeviceAddress(device),
                vertexFormat: VkFormat.VK_FORMAT_R32G32B32_SFLOAT,
                vertexStride: 3 * sizeof(float),
                maxVertex: 2);

            Span<AccelerationStructureGeometry> geos = [Geometry];
            Span<uint> maxCounts = [1];
            Sizes = device.GetAccelerationStructureBuildSizes(
                AccelerationStructureType.BottomLevel,
                AccelerationStructureBuildFlags.PreferFastTrace
                | AccelerationStructureBuildFlags.AllowCompaction,
                geos, maxCounts);

            _backing = CreateAsBuffer(device, Sizes.AccelerationStructureSize);
            Scratch  = CreateScratchBuffer(device, Sizes.BuildScratchSize);

            // VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03710: the
            // scratch DEVICE ADDRESS must be a multiple of
            // MinScratchOffsetAlignment. This fixture hands the build the
            // buffer's base address unchanged, which only satisfies the rule
            // if the allocator's base happens to be aligned — assert it rather
            // than assume it, so a host where it does not hold fails here with
            // an actionable message instead of somewhere inside the driver.
            // This is also the only place the wrapper's own
            // TryGetAccelerationStructureLimits projection is exercised
            // against a device that actually advertises the extension.
            Assert.True(device.PhysicalDevice.TryGetAccelerationStructureLimits(
                    out AccelerationStructureLimits limits),
                "A device created with VK_KHR_acceleration_structure must advertise "
                + "VkPhysicalDeviceAccelerationStructurePropertiesKHR.");
            Assert.NotEqual(0u, limits.MinScratchOffsetAlignment);

            ulong scratchAddress = Scratch.GetDeviceAddress(device);
            Assert.Equal(0ul, scratchAddress % limits.MinScratchOffsetAlignment);

            Blas = device.CreateAccelerationStructure(
                AccelerationStructureType.BottomLevel, in _backing, 0,
                Sizes.AccelerationStructureSize);
        }

        public void RecordBuildAndSubmit(AccelerationStructureBuildFlags flags)
        {
            using var cmdPool = new CommandBufferPool(_device, _family);

            ReadOnlySpan<MemoryBarrier> barriers =
            [
                new MemoryBarrier
                {
                    SrcStage  = Stage.AccelerationStructureBuild,
                    SrcAccess = Access.AccelerationStructureWrite,
                    DstStage  = Stage.AccelerationStructureBuild,
                    DstAccess = Access.AccelerationStructureRead,
                },
            ];

            var rec = cmdPool.Begin();
            try
            {
                Span<AccelerationStructureBuild> builds = stackalloc AccelerationStructureBuild[1];
                builds[0] = new AccelerationStructureBuild
                {
                    Type           = AccelerationStructureType.BottomLevel,
                    Flags          = flags,
                    Destination    = Blas,
                    ScratchAddress = Scratch.GetDeviceAddress(_device),
                    FirstGeometry  = 0,
                    GeometryCount  = 1,
                };
                Span<AccelerationStructureGeometry> geos = stackalloc AccelerationStructureGeometry[1];
                geos[0] = Geometry;
                Span<AccelerationStructureBuildRange> ranges = stackalloc AccelerationStructureBuildRange[1];
                ranges[0] = AccelerationStructureBuildRange.Of(1);

                rec.BuildAccelerationStructures(builds, geos, ranges);

                // Make the build visible to the compacted-size query and to any
                // later TLAS build over this structure.
                rec.PipelineBarrier(barriers, default, default);

                SubmitAndWait(_device, _family, ref rec);
            }
            finally { rec.Dispose(); }
        }

        public void Dispose()
        {
            Blas.Dispose();
            Scratch.Dispose();
            _backing.Dispose();
            _vertices.Dispose();
        }
    }

    private static Instance CreateValidatedInstance(List<DebugMessage> errors)
        => Instance.Create(new InstanceDescription
        {
            ApiVersion       = VulkanVersion.V1_4,
            EnableValidation = true,
            DebugCallback    = m =>
            {
                if ((m.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) != 0)
                    lock (errors) errors.Add(m);
            },
        });

    private static void AssertNoValidationErrors(List<DebugMessage> errors)
    {
        lock (errors)
            Assert.True(errors.Count == 0,
                "Validation errors recorded: " + string.Join("; ", errors.ConvertAll(e => e.Message)));
    }

    /// <summary>
    /// Ends, submits and waits — the FencePool idiom the rest of the suite
    /// uses. <see cref="CommandRecorder"/> is a <c>ref struct</c>, so it can
    /// only travel by <c>ref</c>.
    /// </summary>
    private static void SubmitAndWait(Device device, uint family, ref CommandRecorder rec)
    {
        using var fencePool = new FencePool(device);
        Fence fence = fencePool.Acquire();
        try
        {
            device.GetQueue(family, 0).Submit2(ref rec, in fence);
            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(10)));
        }
        finally { fencePool.Release(fence); }
    }

    private static Buffer CreateAsBuffer(Device device, ulong size)
        => device.Allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = size,
                Usage = BufferUsage.AccelerationStructureStorage | BufferUsage.ShaderDeviceAddress,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

    private static Buffer CreateScratchBuffer(Device device, ulong size)
        => device.Allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = size == 0 ? 256 : size,
                Usage = BufferUsage.StorageBuffer | BufferUsage.ShaderDeviceAddress,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

    private static Device CreateGraphicsDevice(Instance instance, out uint family)
    {
        uint f = uint.MaxValue;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    f = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });
        family = f;
        return gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });
    }

    /// <summary>
    /// Creates a device with all three ray-query extensions plus the three
    /// features, or returns <see langword="null"/> when no GPU on this host can
    /// supply them: the clean skip signal for the RT tier. The picker screens on
    /// <see cref="PhysicalDeviceInfo.SupportsExtension"/> so a host whose first
    /// graphics-capable GPU is not RT-capable still finds the one that is.
    /// </summary>
    private static Device? TryCreateAccelerationStructureDevice(Instance instance, out uint family)
    {
        uint f = uint.MaxValue;
        PhysicalDevice gpu;
        try
        {
            gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
            {
                if (!info.SupportsExtension(DeviceExtensionNames.AccelerationStructure)) return false;
                if (!info.SupportsExtension(DeviceExtensionNames.RayQuery)) return false;
                if (!info.SupportsExtension(DeviceExtensionNames.DeferredHostOperations)) return false;
                for (int i = 0; i < info.QueueFamilies.Length; i++)
                {
                    // Builds need compute; graphics implies compute in
                    // practice, but ask for compute explicitly since that is
                    // the VU (-commandBuffer-cmdpool).
                    if (info.QueueFamilies[i].SupportsGraphics && info.QueueFamilies[i].SupportsCompute)
                    {
                        f = info.QueueFamilies[i].Index;
                        return true;
                    }
                }
                return false;
            });
        }
        catch (VulkanException ex) when (ex.Result == VkResult.VK_ERROR_INITIALIZATION_FAILED)
        {
            family = 0;
            return null;
        }

        family = f;
        Utf8Name[] extensions =
        [
            VulkanExtensions.KhrAccelerationStructure,
            VulkanExtensions.KhrDeferredHostOperations,
            VulkanExtensions.KhrRayQuery,
        ];
        try
        {
            return gpu.CreateDevice(new DeviceDescription
            {
                Queues     = [new QueueRequest(f, count: 1, priority: 1.0f)],
                Extensions = extensions,
                ConfigureFeatures = static (
                    ref ChainBuilder<VkDeviceCreateInfo> chain,
                    ref VkPhysicalDeviceFeatures2 _,
                    ref VkPhysicalDeviceVulkan12Features v12,
                    ref VkPhysicalDeviceVulkan13Features __,
                    ref VkPhysicalDeviceVulkan14Features ___) =>
                {
                    // Every build input, the scratch and the TLAS instance
                    // references are device addresses.
                    v12.bufferDeviceAddress = 1;
                    ref var asFeatures =
                        ref chain.Push<VkPhysicalDeviceAccelerationStructureFeaturesKHR>();
                    asFeatures.accelerationStructure = 1;
                    ref var rq = ref chain.Push<VkPhysicalDeviceRayQueryFeaturesKHR>();
                    rq.rayQuery = 1;
                },
            });
        }
        catch (VulkanException ex) when (
            ex.Result == VkResult.VK_ERROR_EXTENSION_NOT_PRESENT ||
            ex.Result == VkResult.VK_ERROR_FEATURE_NOT_PRESENT)
        {
            return null;
        }
    }
}
