using System.Collections.Generic;
using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Backs the acceptance criterion of issue #46: a real two-queue
/// release/acquire pair using <see cref="ImageBarrier.Release"/> /
/// <see cref="ImageBarrier.Acquire"/> and the buffer counterparts on a
/// graphics + dedicated-transfer queue pair, validated by the validation
/// layer (no errors).
/// </summary>
/// <remarks>
/// Skips on hardware that exposes no separate transfer-only family —
/// integrated GPUs and most software drivers fall into this bucket.
/// </remarks>
public sealed unsafe class QueueOwnershipTransferTests
{
    [Fact]
    public void BufferOwnership_GraphicsToTransfer_PassesValidation()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver,          "No Vulkan driver on host.");
        Assert.SkipUnless(VulkanDriverProbe.HasValidationLayer, "Validation layer not installed.");

        var errors = new List<DebugMessage>();
        using var instance = Instance.Create(new InstanceDescription
        {
            ApiVersion       = VulkanVersion.V1_4,
            EnableValidation = true,
            DebugCallback    = m =>
            {
                if ((m.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) != 0)
                    lock (errors) errors.Add(m);
            },
        });

        if (!TryPickGraphicsAndTransferDevice(instance, out var gpu, out uint graphicsFamily, out uint transferFamily))
            Assert.Skip("No physical device with separate graphics + dedicated-transfer queue families.");

        using var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues =
            [
                new QueueRequest(graphicsFamily, count: 1, priority: 1.0f),
                new QueueRequest(transferFamily, count: 1, priority: 1.0f),
            ],
        });

        var graphics = device.GetQueue(graphicsFamily, 0);
        var transfer = device.GetQueue(transferFamily, 0);

        // Buffer is created with default (EXCLUSIVE) sharing — without a
        // matching release/acquire pair the contents become undefined the
        // moment the second queue touches it. That's the whole point of
        // the new factories.
        using var buffer = device.Allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = 1024,
                Usage = BufferUsage.TransferSrc | BufferUsage.TransferDst,
            },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferDevice,
            });

        using var graphicsPool = new CommandBufferPool(device, graphicsFamily);
        using var transferPool = new CommandBufferPool(device, transferFamily);
        using var fencePool    = new FencePool(device);
        using var semaphores   = new SemaphorePool(device);

        var releaseDone = semaphores.AcquireBinary();
        var graphicsFence = fencePool.Acquire();
        var transferFence = fencePool.Acquire();
        try
        {
            // ---- Graphics queue: produce + release ----
            var producer = graphicsPool.Begin();
            try
            {
                producer.FillBuffer(in buffer, data: 0xDEADBEEFu);

                BufferBarrier[] releases =
                [
                    BufferBarrier.Release(in buffer,
                        fromQueueFamily: graphicsFamily, toQueueFamily: transferFamily,
                        Stage.Copy, Access.TransferWrite),
                ];
                producer.PipelineBarrier(default, releases, default);

                SemaphoreSubmit[] signals = [new SemaphoreSubmit(releaseDone, Stage.Copy)];
                Fence noFence = default;
                graphics.Submit2(ref producer, in noFence, default, signals);
            }
            finally { producer.Dispose(); }

            // The graphics submit returned without waiting; queue the transfer
            // submit in front of the semaphore so the GPU orders them.

            // ---- Transfer queue: acquire + consume ----
            var consumer = transferPool.Begin();
            try
            {
                BufferBarrier[] acquires =
                [
                    BufferBarrier.Acquire(in buffer,
                        fromQueueFamily: graphicsFamily, toQueueFamily: transferFamily,
                        Stage.Copy, Access.TransferRead),
                ];
                consumer.PipelineBarrier(default, acquires, default);

                // No follow-up read needed for the validation contract — the
                // acquire half is the thing under test. Recording it inside a
                // valid command buffer that gets submitted is enough.

                SemaphoreSubmit[] waits = [new SemaphoreSubmit(releaseDone, Stage.Copy)];
                transfer.Submit2(ref consumer, in transferFence, waits, default);
            }
            finally { consumer.Dispose(); }

            Assert.Equal(WaitState.Signaled, transferFence.Wait(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            fencePool.Release(graphicsFence);
            fencePool.Release(transferFence);
            semaphores.Release(releaseDone);
        }

        lock (errors)
            Assert.True(errors.Count == 0,
                "Validation errors recorded: " + string.Join("; ", errors.ConvertAll(e => e.Message)));
    }

    [Fact]
    public void ImageOwnership_GraphicsToTransfer_PassesValidation()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver,          "No Vulkan driver on host.");
        Assert.SkipUnless(VulkanDriverProbe.HasValidationLayer, "Validation layer not installed.");

        var errors = new List<DebugMessage>();
        using var instance = Instance.Create(new InstanceDescription
        {
            ApiVersion       = VulkanVersion.V1_4,
            EnableValidation = true,
            DebugCallback    = m =>
            {
                if ((m.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) != 0)
                    lock (errors) errors.Add(m);
            },
        });

        if (!TryPickGraphicsAndTransferDevice(instance, out var gpu, out uint graphicsFamily, out uint transferFamily))
            Assert.Skip("No physical device with separate graphics + dedicated-transfer queue families.");

        using var device = gpu.CreateDevice(new DeviceDescription
        {
            Queues =
            [
                new QueueRequest(graphicsFamily, count: 1, priority: 1.0f),
                new QueueRequest(transferFamily, count: 1, priority: 1.0f),
            ],
        });

        var graphics = device.GetQueue(graphicsFamily, 0);
        var transfer = device.GetQueue(transferFamily, 0);

        using var image = device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType   = VkImageType.VK_IMAGE_TYPE_2D,
                Format      = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width       = 64, Height = 64, Depth = 1,
                MipLevels   = 1, ArrayLayers = 1,
                Samples     = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling      = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage       = ImageUsage.TransferSrc | ImageUsage.TransferDst,
            },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferDevice,
            });

        using var graphicsPool = new CommandBufferPool(device, graphicsFamily);
        using var transferPool = new CommandBufferPool(device, transferFamily);
        using var fencePool    = new FencePool(device);
        using var semaphores   = new SemaphorePool(device);

        var releaseDone = semaphores.AcquireBinary();
        var graphicsFence = fencePool.Acquire();
        var transferFence = fencePool.Acquire();
        try
        {
            // ---- Graphics queue: transition into TRANSFER_DST + release ----
            var producer = graphicsPool.Begin();
            try
            {
                var preTransition = ImageBarrier.Transition(in image,
                    from: VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                    to:   VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                    Stage.TopOfPipe, Access.None,
                    Stage.Copy,      Access.TransferWrite);
                producer.PipelineBarrier(in preTransition);

                var clearColor = new VkClearColorValue();
                clearColor.float32[0] = 1.0f;
                producer.ClearColorImage(in image,
                    layout: VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                    color: clearColor);

                var release = ImageBarrier.Release(in image,
                    from: VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                    to:   VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                    fromQueueFamily: graphicsFamily, toQueueFamily: transferFamily,
                    Stage.Copy, Access.TransferWrite);
                producer.PipelineBarrier(in release);

                SemaphoreSubmit[] signals = [new SemaphoreSubmit(releaseDone, Stage.Copy)];
                graphics.Submit2(ref producer, in graphicsFence, default, signals);
            }
            finally { producer.Dispose(); }

            // ---- Transfer queue: acquire ----
            var consumer = transferPool.Begin();
            try
            {
                var acquire = ImageBarrier.Acquire(in image,
                    from: VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                    to:   VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                    fromQueueFamily: graphicsFamily, toQueueFamily: transferFamily,
                    Stage.Copy, Access.TransferRead);
                consumer.PipelineBarrier(in acquire);

                SemaphoreSubmit[] waits = [new SemaphoreSubmit(releaseDone, Stage.Copy)];
                transfer.Submit2(ref consumer, in transferFence, waits, default);
            }
            finally { consumer.Dispose(); }

            Assert.Equal(WaitState.Signaled, graphicsFence.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(WaitState.Signaled, transferFence.Wait(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            fencePool.Release(graphicsFence);
            fencePool.Release(transferFence);
            semaphores.Release(releaseDone);
        }

        lock (errors)
            Assert.True(errors.Count == 0,
                "Validation errors recorded: " + string.Join("; ", errors.ConvertAll(e => e.Message)));
    }

    /// <summary>
    /// Picks a physical device that exposes a graphics-capable family and a
    /// distinct family that supports transfer (the "dedicated transfer" or
    /// "DMA" family). Returns <c>false</c> if no such pairing exists on any
    /// device — common on integrated GPUs and software drivers.
    /// </summary>
    private static bool TryPickGraphicsAndTransferDevice(
        Instance instance,
        out PhysicalDevice gpu,
        out uint graphicsFamily,
        out uint transferFamily)
    {
        uint gFam = uint.MaxValue, tFam = uint.MaxValue;
        var picked = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            uint g = uint.MaxValue, t = uint.MaxValue;
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                ref readonly var f = ref info.QueueFamilies[i];
                if (g == uint.MaxValue && f.SupportsGraphics) g = f.Index;
            }
            // Prefer a transfer-only family (no graphics, no compute) — the
            // canonical "DMA" engine on discrete GPUs. Falls back to any
            // transfer-supporting family that isn't the graphics one.
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                ref readonly var f = ref info.QueueFamilies[i];
                if (!f.SupportsTransfer) continue;
                if (f.Index == g) continue;
                if (!f.SupportsGraphics && !f.SupportsCompute)
                {
                    t = f.Index;
                    break;
                }
            }
            if (t == uint.MaxValue)
            {
                for (int i = 0; i < info.QueueFamilies.Length; i++)
                {
                    ref readonly var f = ref info.QueueFamilies[i];
                    if (f.SupportsTransfer && f.Index != g) { t = f.Index; break; }
                }
            }

            if (g == uint.MaxValue || t == uint.MaxValue) return false;
            gFam = g; tFam = t;
            return true;
        });

        gpu            = picked;
        graphicsFamily = gFam;
        transferFamily = tFam;
        return picked is not null && gFam != uint.MaxValue && tFam != uint.MaxValue;
    }
}
