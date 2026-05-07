using System.Runtime.CompilerServices;
using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Locks down <see cref="ChainBuilder{TRoot}"/>: round-trip a 3-node chain
/// through the canonical pNext walker, confirm sType + payload survive,
/// and verify the success path is zero-allocation. The structural validity
/// of the chain (<c>VkPhysicalDeviceVulkan13Features</c> can extend
/// <c>VkPhysicalDeviceFeatures2</c>) is enforced at compile time by the
/// <see cref="IChainable{TRoot}"/> generic constraint — these tests don't
/// need to exercise it; the build itself does.
/// </summary>
public sealed unsafe class ChainBuilderTests
{
    [Fact]
    public void RoundTrip_ThreeNodes_PNextWalkVisitsInOrder()
    {
        Span<byte> scratch = stackalloc byte[1024];
        var chain = ChainBuilder.For<VkPhysicalDeviceFeatures2>(scratch);

        ref var head = ref chain.Root();
        head.features.geometryShader = 1;

        ref var v13 = ref chain.Push<VkPhysicalDeviceVulkan13Features>();
        v13.synchronization2 = 1;
        v13.dynamicRendering = 1;

        ref var v12 = ref chain.Push<VkPhysicalDeviceVulkan12Features>();
        v12.timelineSemaphore = 1;

        var headPtr = chain.Head;
        Assert.True(headPtr != null);
        Assert.Equal(VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2, headPtr->sType);
        Assert.Equal(1u, headPtr->features.geometryShader);

        var cursor = (VkBaseOutStructure*)headPtr;
        Span<VkStructureType> visited = stackalloc VkStructureType[4];
        var count = 0;
        while (cursor != null && count < visited.Length)
        {
            visited[count++] = cursor->sType;
            cursor = cursor->pNext;
        }

        Assert.Equal(3, count);
        Assert.Equal(VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2, visited[0]);
        Assert.Equal(VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_3_FEATURES, visited[1]);
        Assert.Equal(VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_2_FEATURES, visited[2]);

        var node2 = (VkPhysicalDeviceVulkan13Features*)((VkBaseOutStructure*)headPtr)->pNext;
        Assert.Equal(1u, node2->synchronization2);
        Assert.Equal(1u, node2->dynamicRendering);

        var node3 = (VkPhysicalDeviceVulkan12Features*)((VkBaseOutStructure*)node2)->pNext;
        Assert.Equal(1u, node3->timelineSemaphore);
    }

    [Fact]
    public void Head_BeforeRoot_ReturnsNull()
    {
        Span<byte> scratch = stackalloc byte[64];
        var chain = ChainBuilder.For<VkPhysicalDeviceFeatures2>(scratch);
        Assert.True(chain.Head == null);
    }

    [Fact]
    public void Push_BeforeRoot_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            Span<byte> scratch = stackalloc byte[1024];
            var chain = ChainBuilder.For<VkPhysicalDeviceFeatures2>(scratch);
            chain.Push<VkPhysicalDeviceVulkan13Features>();
        });
    }

    [Fact]
    public void Root_CalledTwice_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            Span<byte> scratch = stackalloc byte[512];
            var chain = ChainBuilder.For<VkPhysicalDeviceFeatures2>(scratch);
            chain.Root();
            chain.Root();
        });
    }

    [Fact]
    public void Reserve_OverflowsBuffer_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            // 32 bytes is far smaller than VkPhysicalDeviceFeatures2.
            Span<byte> tiny = stackalloc byte[32];
            var chain = ChainBuilder.For<VkPhysicalDeviceFeatures2>(tiny);
            chain.Root();
        });
    }

    [Fact]
    public void EachNode_AlignedToPointerSize()
    {
        Span<byte> scratch = stackalloc byte[1024];
        var chain = ChainBuilder.For<VkPhysicalDeviceFeatures2>(scratch);

        ref var head = ref chain.Root();
        ref var node2 = ref chain.Push<VkPhysicalDeviceVulkan13Features>();
        ref var node3 = ref chain.Push<VkPhysicalDeviceVulkan12Features>();

        var basePtr = (nint)Unsafe.AsPointer(ref head);
        var node2Ptr = (nint)Unsafe.AsPointer(ref node2);
        var node3Ptr = (nint)Unsafe.AsPointer(ref node3);

        Assert.Equal(0, (node2Ptr - basePtr) % sizeof(nint));
        Assert.Equal(0, (node3Ptr - basePtr) % sizeof(nint));
    }

    [Fact]
    public void RoundTrip_IsZeroAllocation()
    {
        for (var i = 0; i < 1_000; i++)
        {
            BuildSampleChain();
        }

        // Two measured passes. Tier-1→tier-2 promotion + dynamic-PGO
        // recompiles can fire on the first measurement-sized loop on
        // slower CI hardware, charging a small one-shot allocation to
        // the calling thread that doesn't reflect per-call cost. By
        // the second pass the JIT is fully settled, so any residual
        // delta is genuinely per-iteration allocation. The first pass
        // delta is intentionally not asserted on.
        var before1 = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100_000; i++)
        {
            BuildSampleChain();
        }
        _ = GC.GetAllocatedBytesForCurrentThread() - before1;

        var before2 = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100_000; i++)
        {
            BuildSampleChain();
        }
        var after2 = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after2 - before2);
    }

    [Fact]
    public void STypeAndRootSType_AreStaticConstants()
    {
        // Static abstract members on partial structs — confirms the
        // generated chain partials wired the right constants.
        Assert.Equal(
            VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2,
            VkPhysicalDeviceFeatures2.RootSType);
        Assert.Equal(
            VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_3_FEATURES,
            VkPhysicalDeviceVulkan13Features.SType);
    }

    [SkipLocalsInit]
    private static int BuildSampleChain()
    {
        Span<byte> scratch = stackalloc byte[1024];
        var chain = ChainBuilder.For<VkPhysicalDeviceFeatures2>(scratch);
        chain.Root();
        chain.Push<VkPhysicalDeviceVulkan13Features>();
        chain.Push<VkPhysicalDeviceVulkan12Features>();
        return chain.Length;
    }
}
