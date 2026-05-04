using System.Runtime.CompilerServices;
using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Locks down <see cref="ChainBuilder"/>: round-trip a 3-node chain through
/// the canonical pNext walker, confirm sType + payload survive, and verify
/// the success path is zero-allocation.
/// </summary>
public sealed unsafe class ChainBuilderTests
{
    [Fact]
    public void RoundTrip_ThreeNodes_PNextWalkVisitsInOrder()
    {
        Span<byte> scratch = stackalloc byte[1024];
        var chain = ChainBuilder.From(scratch);

        ref var head = ref chain.Root<VkPhysicalDeviceFeatures2>(
            VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2);
        head.features.geometryShader = 1;

        ref var v13 = ref chain.Push<VkPhysicalDeviceVulkan13Features>(
            VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_3_FEATURES);
        v13.synchronization2 = 1;
        v13.dynamicRendering = 1;

        ref var v12 = ref chain.Push<VkPhysicalDeviceVulkan12Features>(
            VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_2_FEATURES);
        v12.timelineSemaphore = 1;

        var headPtr = chain.IntoNative<VkPhysicalDeviceFeatures2>();
        Assert.True(headPtr != null);
        Assert.Equal(VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2, headPtr->sType);
        Assert.Equal(1u, headPtr->features.geometryShader);

        // Walk the chain through the canonical VkBaseOutStructure cursor.
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

        // Confirm the payloads we wrote are still there at the linked-to addresses.
        var node2 = (VkPhysicalDeviceVulkan13Features*)((VkBaseOutStructure*)headPtr)->pNext;
        Assert.Equal(1u, node2->synchronization2);
        Assert.Equal(1u, node2->dynamicRendering);

        var node3 = (VkPhysicalDeviceVulkan12Features*)((VkBaseOutStructure*)node2)->pNext;
        Assert.Equal(1u, node3->timelineSemaphore);
    }

    [Fact]
    public void IntoNative_BeforeRoot_ReturnsNull()
    {
        Span<byte> scratch = stackalloc byte[64];
        var chain = ChainBuilder.From(scratch);
        Assert.True(chain.IntoNative<VkPhysicalDeviceFeatures2>() == null);
        Assert.True(chain.IntoNative() == null);
    }

    [Fact]
    public void Push_BeforeRoot_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            Span<byte> scratch = stackalloc byte[128];
            var chain = ChainBuilder.From(scratch);
            chain.Push<VkPhysicalDeviceVulkan13Features>(
                VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_3_FEATURES);
        });
    }

    [Fact]
    public void Root_CalledTwice_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            Span<byte> scratch = stackalloc byte[256];
            var chain = ChainBuilder.From(scratch);
            chain.Root<VkPhysicalDeviceFeatures2>(VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2);
            chain.Root<VkPhysicalDeviceFeatures2>(VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2);
        });
    }

    [Fact]
    public void Reserve_OverflowsBuffer_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            // 32 bytes is far smaller than VkPhysicalDeviceFeatures2 (which
            // contains a fully-populated VkPhysicalDeviceFeatures payload).
            Span<byte> tiny = stackalloc byte[32];
            var chain = ChainBuilder.From(tiny);
            chain.Root<VkPhysicalDeviceFeatures2>(
                VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2);
        });
    }

    [Fact]
    public void EachNode_AlignedToPointerSize()
    {
        Span<byte> scratch = stackalloc byte[1024];
        var chain = ChainBuilder.From(scratch);

        ref var head = ref chain.Root<VkPhysicalDeviceFeatures2>(
            VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2);
        ref var node2 = ref chain.Push<VkPhysicalDeviceVulkan13Features>(
            VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_3_FEATURES);
        ref var node3 = ref chain.Push<VkPhysicalDeviceVulkan12Features>(
            VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_2_FEATURES);

        var basePtr = (nint)Unsafe.AsPointer(ref head);
        var node2Ptr = (nint)Unsafe.AsPointer(ref node2);
        var node3Ptr = (nint)Unsafe.AsPointer(ref node3);

        Assert.Equal(0, (node2Ptr - basePtr) % sizeof(nint));
        Assert.Equal(0, (node3Ptr - basePtr) % sizeof(nint));
    }

    [Fact]
    public void RoundTrip_IsZeroAllocation()
    {
        // Warm up
        for (var i = 0; i < 1_000; i++)
        {
            BuildSampleChain();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100_000; i++)
        {
            BuildSampleChain();
        }
        var after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    [SkipLocalsInit]
    private static int BuildSampleChain()
    {
        Span<byte> scratch = stackalloc byte[1024];
        var chain = ChainBuilder.From(scratch);
        chain.Root<VkPhysicalDeviceFeatures2>(
            VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2);
        chain.Push<VkPhysicalDeviceVulkan13Features>(
            VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_3_FEATURES);
        chain.Push<VkPhysicalDeviceVulkan12Features>(
            VkStructureType.VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_VULKAN_1_2_FEATURES);
        return chain.Length;
    }
}
