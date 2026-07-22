using System.IO;
using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Explicit allocation + aliasing binds: querying what a resource would need without
/// creating one, allocating a <see cref="MemoryBlock"/> with nothing bound to it, and
/// creating several resources into that block at chosen offsets.
///
/// The load-bearing assertion throughout is OWNERSHIP: a resource created into a block owns
/// its own <c>Vk*</c> object and none of the memory. The oracle for that is the allocator's
/// own leak report — if an aliasing resource wrongly freed the block, the later
/// <c>vmaFreeMemory</c> would be a double free; if nothing freed it,
/// <c>Allocator.Dispose</c> writes "live allocation" to stderr. Both failure directions are
/// visible, which "it didn't crash" alone is not.
/// </summary>
public sealed class MemoryAliasingTests
{
    [Fact]
    public void CombineWith_TakesMaxSizeMaxAlignmentAndSharedTypes()
    {
        // Pure arithmetic — no driver needed, and the one piece of this API a caller can
        // get wrong without Vulkan telling them.
        var a = new MemoryRequirements { Size = 1024, Alignment = 256, MemoryTypeBits = 0b1110 };
        var b = new MemoryRequirements { Size = 512, Alignment = 4096, MemoryTypeBits = 0b0111 };

        var combined = a.CombineWith(b);

        Assert.Equal(1024ul, combined.Size);
        Assert.Equal(4096ul, combined.Alignment);
        Assert.Equal(0b0110u, combined.MemoryTypeBits);

        // Commutative, so folding a set needs no ordering rule.
        Assert.Equal(combined, b.CombineWith(a));

        // Resources that share no memory type combine to zero — the caller's signal that
        // one block cannot host both, checkable before allocating.
        var disjoint = new MemoryRequirements { Size = 8, Alignment = 1, MemoryTypeBits = 0b0011 }
            .CombineWith(new MemoryRequirements { Size = 8, Alignment = 1, MemoryTypeBits = 0b1100 });
        Assert.Equal(0u, disjoint.MemoryTypeBits);
    }

    [Fact]
    public void GetMemoryRequirements_AnswersWithoutLeavingAResourceBehind()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        uint gfxFamily = PickGraphicsFamily(instance, out var gpu);
        using var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(gfxFamily, count: 1, priority: 1.0f)],
        });

        var image = device.GetImageMemoryRequirements(new ImageDescription
        {
            Format = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
            Width = 256,
            Height = 256,
            Usage = ImageUsage.TransferDst | ImageUsage.Sampled,
        });

        // An optimal-tiled 256x256 RGBA image needs at least its texel bytes; a tiled
        // layout usually needs more, which is exactly why the caller must ask instead of
        // computing width * height * bpp.
        Assert.True(image.Size >= 256ul * 256 * 4, $"image size {image.Size} is below the linear minimum");
        Assert.True(IsPowerOfTwo(image.Alignment), $"image alignment {image.Alignment} is not a power of two");
        Assert.NotEqual(0u, image.MemoryTypeBits);

        var buffer = device.GetBufferMemoryRequirements(new BufferDescription
        {
            Size = 64 * 1024,
            Usage = BufferUsage.TransferSrc | BufferUsage.TransferDst,
        });

        Assert.True(buffer.Size >= 64 * 1024, $"buffer size {buffer.Size} is below the requested size");
        Assert.True(IsPowerOfTwo(buffer.Alignment), $"buffer alignment {buffer.Alignment} is not a power of two");
        Assert.NotEqual(0u, buffer.MemoryTypeBits);

        // The queries create a probe resource and destroy it again. Nothing may survive
        // them: the allocator's leak report is silent, and the device teardown below would
        // report an object leak if a probe had escaped.
        var allocator = Allocator.Create(device);
        Assert.Equal(string.Empty, DisposeAndCaptureStdErr(allocator));
    }

    [Fact]
    public void GetMemoryLimits_ReportsAUsableGranularity()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        _ = PickGraphicsFamily(instance, out var gpu);

        var limits = gpu.GetMemoryLimits();

        // Vulkan requires bufferImageGranularity to be a power of two, and 1 (no
        // constraint) is a legal answer a packer must handle without special-casing.
        Assert.True(IsPowerOfTwo(limits.BufferImageGranularity),
            $"bufferImageGranularity {limits.BufferImageGranularity} is not a power of two");
        Assert.True(IsPowerOfTwo(limits.NonCoherentAtomSize));
        Assert.True(limits.MaxMemoryAllocationCount > 0);
    }

    [Fact]
    public void AliasingResources_ShareOneBlock_AndFreeNoMemoryWhenDisposed()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        int errorCount = 0;
        Action<DebugMessage> sink = msg =>
        {
            if ((msg.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) != 0)
                Interlocked.Increment(ref errorCount);
        };

        using var instance = Instance.Create(new InstanceDescription
        {
            EnableValidation = VulkanDriverProbe.HasValidationLayer,
            DebugCallback = VulkanDriverProbe.HasValidationLayer ? sink : null,
        });
        uint gfxFamily = PickGraphicsFamily(instance, out var gpu);
        using var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(gfxFamily, count: 1, priority: 1.0f)],
        });

        var first = new ImageDescription
        {
            Format = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
            Width = 256,
            Height = 256,
            Usage = ImageUsage.TransferDst | ImageUsage.Sampled,
        };
        var second = new ImageDescription
        {
            Format = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
            Width = 128,
            Height = 128,
            Usage = ImageUsage.TransferDst | ImageUsage.Sampled,
        };

        var firstNeeds = device.GetImageMemoryRequirements(first);
        var secondNeeds = device.GetImageMemoryRequirements(second);
        var shared = firstNeeds.CombineWith(secondNeeds);

        // Lay them out end to end rather than aliased, so the block genuinely has to hold
        // both at once: an offset bind that silently ignored the offset would put the
        // second image on top of the first and validation would say so.
        ulong secondOffset = AlignUp(firstNeeds.Size, secondNeeds.Alignment);
        var blockNeeds = shared with { Size = secondOffset + secondNeeds.Size };

        var allocator = Allocator.Create(device);
        var block = allocator.AllocateMemory(blockNeeds, new AllocationDescription
        {
            Usage = MemoryUsage.Unknown,
            RequiredFlags = MemoryProperties.DeviceLocal,
            Flags = AllocationFlags.CanAlias,
        });

        Assert.False(block.IsNull);
        Assert.True(block.Size >= blockNeeds.Size);

        var imageA = allocator.CreateAliasingImage(block, 0, first);
        var imageB = allocator.CreateAliasingImage(block, secondOffset, second);

        Assert.False(imageA.IsNull);
        Assert.False(imageB.IsNull);
        // Both own their VkImage and neither owns memory — that pair is the whole contract.
        Assert.True(imageA.OwnsHandle);
        Assert.True(imageB.OwnsHandle);
        Assert.False(imageA.OwnsMemory);
        Assert.False(imageB.OwnsMemory);

        imageA.Dispose();
        imageB.Dispose();

        // The block outlived both, which is what "the resources free nothing" means. Prove
        // it by binding a third image into the same bytes and using it, rather than by
        // asserting the absence of a crash.
        var imageC = allocator.CreateAliasingImage(block, 0, first);
        Assert.False(imageC.IsNull);
        using (var view = imageC.CreateView(device, new ImageViewDescription
        {
            Format = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
            Aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
        }))
        {
            Assert.False(view.IsNull);
        }

        imageC.Dispose();
        block.Dispose();

        // Nothing left: a block that some image had already freed would have double-freed
        // above, and a block nobody freed would be reported here.
        Assert.Equal(string.Empty, DisposeAndCaptureStdErr(allocator));

        if (VulkanDriverProbe.HasValidationLayer)
        {
            Assert.Equal(0, Volatile.Read(ref errorCount));
        }
    }

    [Fact]
    public void AliasingBuffer_OwnsNoMemory_AndHostSyncIsANoOp()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        uint gfxFamily = PickGraphicsFamily(instance, out var gpu);
        using var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(gfxFamily, count: 1, priority: 1.0f)],
        });

        var desc = new BufferDescription { Size = 4096, Usage = BufferUsage.TransferSrc | BufferUsage.TransferDst };
        var needs = device.GetBufferMemoryRequirements(desc);

        var allocator = Allocator.Create(device);
        var block = allocator.AllocateMemory(needs, new AllocationDescription
        {
            Usage = MemoryUsage.Unknown,
            RequiredFlags = MemoryProperties.DeviceLocal,
            Flags = AllocationFlags.CanAlias,
        });

        var aliased = allocator.CreateAliasingBuffer(block, 0, desc);
        Assert.False(aliased.OwnsMemory);
        Assert.False(aliased.IsHostVisible);
        Assert.Equal(4096ul, aliased.Size);

        // Flush/Invalidate address the ALLOCATION, which this view does not own. They must
        // do nothing rather than hand VMA a null allocation.
        aliased.Flush();
        aliased.Invalidate();

        // The block's own resource still owns its memory — the contrast that makes
        // OwnsMemory meaningful rather than always-false.
        var owned = allocator.CreateBuffer(desc, new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });
        Assert.True(owned.OwnsMemory);
        owned.Dispose();

        aliased.Dispose();
        block.Dispose();
        Assert.Equal(string.Empty, DisposeAndCaptureStdErr(allocator));
    }

    [Fact]
    public void AllocateMemory_RejectsAutoUsage_WithAnActionableMessage()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        uint gfxFamily = PickGraphicsFamily(instance, out var gpu);
        using var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(gfxFamily, count: 1, priority: 1.0f)],
        });

        using var allocator = Allocator.Create(device);
        var needs = device.GetBufferMemoryRequirements(new BufferDescription
        {
            Size = 4096,
            Usage = BufferUsage.TransferDst,
        });

        // VMA's own answer to this is an assert plus VK_ERROR_FEATURE_NOT_PRESENT, which
        // reads as "your device lacks a feature" and sends the caller hunting the wrong
        // thing. The wrapper owes them the actual instruction.
        var thrown = Assert.Throws<ArgumentException>(() => allocator.AllocateMemory(
            needs,
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice, Flags = AllocationFlags.CanAlias }));

        Assert.Equal("allocation", thrown.ParamName);
        Assert.Contains("MemoryUsage.Unknown", thrown.Message);
        Assert.Contains("RequiredFlags", thrown.Message);
    }

    private static string DisposeAndCaptureStdErr(Allocator allocator)
    {
        var originalErr = Console.Error;
        var captured = new StringWriter();
        Console.SetError(captured);
        try
        {
            allocator.Dispose();
        }
        finally
        {
            Console.SetError(originalErr);
        }

        return captured.ToString();
    }

    private static bool IsPowerOfTwo(ulong value) => value != 0 && (value & (value - 1)) == 0;

    private static ulong AlignUp(ulong value, ulong alignment) => (value + alignment - 1) & ~(alignment - 1);

    private static uint PickGraphicsFamily(Instance instance, out PhysicalDevice gpu)
    {
        uint family = uint.MaxValue;
        gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if ((info.QueueFamilies[i].Flags & VkQueueFlagBits.VK_QUEUE_GRAPHICS_BIT) != 0)
                {
                    family = (uint)i;
                    return true;
                }
            }

            return false;
        });

        return family;
    }
}
