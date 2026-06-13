using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Smoke tests for <c>VK_EXT_debug_utils</c> integration: object naming
/// and command-buffer label scopes. Visual verification that labels
/// surface in RenderDoc / Nsight is manual and not part of automated
/// CI — these tests just pin the contract that the wrapper paths run
/// without throwing in both the loaded and not-loaded cases.
/// </summary>
public sealed class DebugMarkerTests
{
    [Fact]
    public void DebugMarkers_WithoutExtension_AreNoOps()
    {
        // Default Instance.Create has EnableValidation = false → no
        // VK_EXT_debug_utils on the instance → DeviceFunctionTable's
        // four debug-utils fn pointers resolve to null →
        // ObjectName.Set, BeginLabel/EndLabel/InsertLabel, and
        // LabelScope must all silently no-op rather than NRE.
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var instance = Instance.Create(default);
        using var device   = CreateGraphicsDevice(instance, out uint family);
        using var pool     = new CommandBufferPool(device, family);

        using var buffer = device.Allocator.CreateBuffer(
            new BufferDescription { Size = 64, Usage = BufferUsage.TransferDst },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        ObjectName.Set(device, buffer, "TestBuffer"u8);

        using var rec = pool.Begin();
        rec.BeginLabel("Outer"u8, new Color(1, 0, 0));
        rec.InsertLabel("Marker"u8);
        using (rec.LabelScope("Nested"u8, new Color(0, 1, 0)))
        {
            rec.InsertLabel("InsideScope"u8);
        }
        rec.EndLabel();
    }

    [Fact]
    public void DebugMarkers_WithValidation_NameAndLabel_NoValidationErrors()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipUnless(VulkanDriverProbe.HasValidationLayer,
            "Khronos validation layer not installed — needed to confirm marker calls don't trip validation.");
        // Same lavapipe gate as the rest of the queue-submitting suite.
        Assert.SkipWhen(VulkanDriverProbe.IsSoftwareDriver,
            "Software driver — vkQueueSubmit2 SIGSEGV on lavapipe; gated.");

        // Capture any validation messages so we can assert no error-
        // severity messages fire as a result of the marker calls.
        // The callback runs on whatever thread Vulkan dispatches from;
        // we don't read the list until after WaitIdle, by which point
        // the layer has fully drained.
        var errors = new List<string>();
        var lockObj = new object();

        var instanceDesc = new InstanceDescription
        {
            EnableValidation = true,
            DebugCallback = msg =>
            {
                if ((msg.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) != 0)
                {
                    lock (lockObj) errors.Add(msg.Message ?? "<no message>");
                }
            },
        };

        using var instance = Instance.Create(in instanceDesc);
        using var device   = CreateGraphicsDevice(instance, out uint family);
        using var pool     = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);

        using var buffer = device.Allocator.CreateBuffer(
            new BufferDescription { Size = 64, Usage = BufferUsage.TransferDst },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        using var image = device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType   = VkImageType.VK_IMAGE_TYPE_2D,
                Format      = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width       = 16, Height = 16, Depth = 1,
                MipLevels   = 1, ArrayLayers = 1,
                Samples     = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling      = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage       = ImageUsage.TransferDst,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        ObjectName.Set(device, buffer, "TestBuffer"u8);
        ObjectName.Set(device, image,  "TestImage"u8);

        var fence = fencePool.Acquire();
        try
        {
            var rec = pool.Begin();
            try
            {
                using (rec.LabelScope("MainPass"u8, new Color(0.2f, 0.4f, 0.8f)))
                {
                    rec.InsertLabel("BeforeBarrier"u8);
                    // No-op pipeline — empty scope still exercises the
                    // begin/end pair. Capture verification belongs in
                    // RenderDoc, not here.
                }

                var queue = device.GetQueue(family, 0);
                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(10)));
        }
        finally { fencePool.Release(fence); }

        device.WaitIdle();

        lock (lockObj)
        {
            Assert.Empty(errors);
        }
    }

    [Fact]
    public void LabelScope_EmptyName_WithValidation_NoUnbalancedEndLabel()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipUnless(VulkanDriverProbe.HasValidationLayer,
            "Khronos validation layer not installed — needed to confirm the empty-name scope stays balanced.");
        // Same lavapipe gate as the rest of the queue-submitting suite.
        Assert.SkipWhen(VulkanDriverProbe.IsSoftwareDriver,
            "Software driver — vkQueueSubmit2 SIGSEGV on lavapipe; gated.");

        // Capture any validation messages so we can assert no error-
        // severity messages fire as a result of the marker calls.
        // The callback runs on whatever thread Vulkan dispatches from;
        // we don't read the list until after WaitIdle, by which point
        // the layer has fully drained.
        var errors = new List<string>();
        var lockObj = new object();

        var instanceDesc = new InstanceDescription
        {
            EnableValidation = true,
            DebugCallback = msg =>
            {
                if ((msg.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) != 0)
                {
                    lock (lockObj) errors.Add(msg.Message ?? "<no message>");
                }
            },
        };

        using var instance = Instance.Create(in instanceDesc);
        using var device   = CreateGraphicsDevice(instance, out uint family);
        using var pool     = new CommandBufferPool(device, family);
        using var fencePool = new FencePool(device);

        var fence = fencePool.Acquire();
        try
        {
            var rec = pool.Begin();
            try
            {
                using (rec.LabelScope("Outer"u8, new Color(0.2f, 0.4f, 0.8f)))
                {
                    // Empty inner scope: BeginLabel no-ops (name empty), so its Dispose
                    // must NOT emit vkCmdEndDebugUtilsLabelEXT. Before the fix it did,
                    // popping "Outer" early and leaving the outer EndLabel unbalanced
                    // (VUID-vkCmdEndDebugUtilsLabelEXT-commandBuffer-01912).
                    using (rec.LabelScope(default))
                    {
                    }
                    rec.InsertLabel("StillInsideOuter"u8);
                }

                var queue = device.GetQueue(family, 0);
                queue.Submit2(ref rec, in fence);
            }
            finally { rec.Dispose(); }

            Assert.Equal(WaitState.Signaled, fence.Wait(TimeSpan.FromSeconds(10)));
        }
        finally { fencePool.Release(fence); }

        device.WaitIdle();

        lock (lockObj)
        {
            Assert.Empty(errors);
        }
    }

    private static Device CreateGraphicsDevice(Instance instance, out uint family)
    {
        uint chosen = uint.MaxValue;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (info.QueueFamilies[i].SupportsGraphics)
                {
                    chosen = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });
        family = chosen;
        return gpu.CreateDevice(new DeviceDescription
        {
            Queues = [new QueueRequest(family, count: 1, priority: 1.0f)],
        });
    }
}
