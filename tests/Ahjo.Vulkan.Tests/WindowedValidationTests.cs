using System.Threading;
using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Standing regression for issue #89: a windowed FrameRing+Swapchain
/// loop must not trip any validation error. Runs ~10 frames against a
/// hidden SDL3 window using the swapchain-aware
/// <see cref="FrameContext.Submit(Queue, ref CommandRecorder, Swapchain, uint, Stage, Stage)"/>
/// + <see cref="Swapchain.Present(Queue, uint)"/> overload pair, with
/// the validation layer enabled, and asserts zero error-severity
/// messages. The original bug surfaced as
/// <c>VUID-vkQueueSubmit2-semaphore-03868</c> after a few frames when
/// <c>RenderingDone</c> was per-slot rather than per-acquired-image; if
/// a future change re-introduces a per-slot present semaphore (or any
/// other windowed-loop spec violation), this test catches it.
/// </summary>
public sealed unsafe class WindowedValidationTests
{
    [Fact]
    public void TenFrames_FrameRingPlusSwapchain_NoValidationErrors()
    {
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");
        Assert.SkipWhen(VulkanDriverProbe.IsSoftwareDriver,
            "Software ICD (Mesa lavapipe): vkQueueSubmit2 SIGSEGVs during command-buffer execution.");
        Assert.SkipUnless(SdlWindow.IsAvailable, "SDL3 video subsystem unavailable.");

        // Counters live on the fixture rather than the callback so the
        // closure stays captureless. Validation messages may dispatch
        // from a non-main thread, hence Interlocked + Volatile.Read.
        int errorCount   = 0;
        int warningCount = 0;
        Action<DebugMessage> sink = msg =>
        {
            if ((msg.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_ERROR_BIT_EXT) != 0)
                Interlocked.Increment(ref errorCount);
            else if ((msg.Severity & VkDebugUtilsMessageSeverityFlagBitsEXT.VK_DEBUG_UTILS_MESSAGE_SEVERITY_WARNING_BIT_EXT) != 0)
                Interlocked.Increment(ref warningCount);
        };

        using var window = new SdlWindow(
            $"AhjoVk_VR_{Guid.NewGuid():N}",
            width: 320, height: 240, hidden: true);

        Utf8Name[] instanceExts = SdlWindow.GetRequiredVulkanInstanceExtensions();
        InstanceDescription desc = default;
        desc.Extensions       = instanceExts;
        desc.EnableValidation = true;
        desc.DebugCallback    = sink;
        using var instance = Instance.Create(desc);

        using var surface = window.CreateVulkanSurface(instance);
        using var device  = CreatePresentDevice(instance, in surface, out uint family);

        var swapDesc = new SwapchainDescription
        {
            Surface = surface,
            Width   = window.Width,
            Height  = window.Height,
        };
        using var swap  = new Swapchain(device, in swapDesc);
        var       queue = device.GetQueue(family, 0);
        using var ring  = new FrameRing(device, framesInFlight: 2, queueFamily: family);

        // Ten frames is enough to exercise slot rotation past
        // FramesInFlight several times (the per-slot RenderingDone bug
        // started tripping by frame 3-4). No real rendering — a
        // CLEAR-only render pass + the layout-transition barriers are
        // sufficient to walk every code path the validator cares about
        // for the present semaphore reuse VUID.
        const uint Frames = 10;
        uint rendered = 0;
        while (rendered < Frames)
        {
            using var fc = ring.BeginFrame();

            var acq = swap.AcquireNextImage(fc.ImageAcquired, TimeSpan.FromSeconds(1), out uint imageIndex);
            if (acq is AcquireResult.Success or AcquireResult.Suboptimal)
                fc.MarkImageAcquireSignaled();
            if (acq is AcquireResult.OutOfDate)
            {
                device.WaitIdle();
                swap.Recreate(in swapDesc);
                ring.RecycleStaleAcquireSemaphores();
                continue;
            }
            if (acq != AcquireResult.Success && acq != AcquireResult.Suboptimal)
                Assert.Fail($"Unexpected AcquireNextImage result: {acq}");

            var rec = fc.CommandBuffers.Begin();
            try
            {
                RecordSwapBarrier(ref rec, swap, imageIndex,
                    from: VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                    to:   VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    srcStage: Stage.TopOfPipe,             srcAccess: Access.None,
                    dstStage: Stage.ColorAttachmentOutput, dstAccess: Access.ColorAttachmentWrite);

                ColorAttachment[] color = [new ColorAttachment
                {
                    View       = swap.ImageViews[(int)imageIndex],
                    Layout     = VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    LoadOp     = VkAttachmentLoadOp.VK_ATTACHMENT_LOAD_OP_CLEAR,
                    StoreOp    = VkAttachmentStoreOp.VK_ATTACHMENT_STORE_OP_STORE,
                    ClearColor = ClearColor(0.05f, 0.07f, 0.10f, 1.0f),
                }];
                rec.BeginRendering(new RenderingInfo
                {
                    RenderArea       = new VkRect2D { extent = swap.Extent },
                    LayerCount       = 1,
                    ColorAttachments = color,
                });
                rec.EndRendering();

                RecordSwapBarrier(ref rec, swap, imageIndex,
                    from: VkImageLayout.VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                    to:   VkImageLayout.VK_IMAGE_LAYOUT_PRESENT_SRC_KHR,
                    srcStage: Stage.ColorAttachmentOutput, srcAccess: Access.ColorAttachmentWrite,
                    dstStage: Stage.BottomOfPipe,          dstAccess: Access.None);

                fc.Submit(queue, ref rec, swap, imageIndex);
            }
            finally { rec.Dispose(); }

            var pres = swap.Present(queue, imageIndex);
            if (pres is AcquireResult.OutOfDate or AcquireResult.Suboptimal)
            {
                device.WaitIdle();
                swap.Recreate(in swapDesc);
                ring.RecycleStaleAcquireSemaphores();
            }
            rendered++;
        }

        device.WaitIdle();

        int errs  = Volatile.Read(ref errorCount);
        // Warnings are noise (loader/manifest naming for unrelated
        // installed overlays — GalaxyOverlayVkLayer etc.); only error
        // severity is a real spec violation.
        Assert.True(errs == 0,
            $"Validation reported {errs} error(s) over {Frames} frames — see issue #89 for the canonical regression.");
    }

    private static void RecordSwapBarrier(
        ref CommandRecorder rec,
        Swapchain           swap,
        uint                imageIndex,
        VkImageLayout       from,
        VkImageLayout       to,
        Stage               srcStage, Access srcAccess,
        Stage               dstStage, Access dstAccess)
    {
        var barrier = new ImageBarrier
        {
            Image          = swap.GetImageHandle(imageIndex),
            SrcStage       = srcStage, SrcAccess = srcAccess,
            DstStage       = dstStage, DstAccess = dstAccess,
            OldLayout      = from,     NewLayout = to,
            Aspect         = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
            BaseMipLevel   = 0, LevelCount = 1,
            BaseArrayLayer = 0, LayerCount = 1,
        };
        rec.PipelineBarrier(barrier);
    }

    private static VkClearColorValue ClearColor(float r, float g, float b, float a)
    {
        var c = new VkClearColorValue();
        c.float32[0] = r;
        c.float32[1] = g;
        c.float32[2] = b;
        c.float32[3] = a;
        return c;
    }

    private static Device CreatePresentDevice(Instance instance, in Surface surface, out uint family)
    {
        Surface local  = surface;
        uint    chosen = uint.MaxValue;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (!info.QueueFamilies[i].SupportsGraphics) continue;
                if (info.Device.SupportsPresent(info.QueueFamilies[i].Index, in local))
                {
                    chosen = info.QueueFamilies[i].Index;
                    return true;
                }
            }
            return false;
        });
        family = chosen;

        Utf8Name[] deviceExts = [VulkanExtensions.KhrSwapchain];
        return gpu.CreateDevice(new DeviceDescription
        {
            Queues     = [new QueueRequest(family, count: 1, priority: 1.0f)],
            Extensions = deviceExts,
        });
    }
}
