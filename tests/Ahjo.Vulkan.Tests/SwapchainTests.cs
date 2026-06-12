using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Win32-only — covers issue 24's surface + swapchain creation and
/// resize round-trip. Linux/macOS tests land alongside the matching
/// platform surface bindings in a follow-up.
/// </summary>
public sealed unsafe class SwapchainTests
{
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    [Fact]
    public void Win32_Surface_Plus_Swapchain_Creates_Multiple_Images()
    {
        Assert.SkipUnless(IsWindows, "Surface tests are Win32-only for now.");
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var window = new Win32Window(800, 600, $"AhjoVk_{Guid.NewGuid():N}");

        Utf8Name[] instanceExts = [VulkanExtensions.KhrSurface, VulkanExtensions.KhrWin32Surface];
        var instDesc = new InstanceDescription { Extensions = instanceExts };
        using var instance = Instance.Create(instDesc);

        using var surface = Surface.CreateWin32(instance, window.HInstance, window.Hwnd);
        Assert.False(surface.IsNull);

        using var device = CreatePresentDevice(instance, in surface, out _);

        var swapDesc = new SwapchainDescription
        {
            Surface = surface,
            Width   = window.Width,
            Height  = window.Height,
        };
        using var swap = new Swapchain(device, in swapDesc);

        // Vulkan reports the *client* extent — for an OVERLAPPED window
        // that's smaller than the requested CreateWindowEx size by the
        // non-client decorations. Asserting non-zero + reasonable lower
        // bound is the right invariant; exact size needs an
        // AdjustWindowRect dance the surface API doesn't care about.
        Assert.True(swap.ImageCount >= 2);
        Assert.True(swap.Extent.width  > 0);
        Assert.True(swap.Extent.height > 0);
        Assert.Equal(swap.ImageCount, (uint)swap.ImageViews.Length);
        for (int i = 0; i < swap.ImageViews.Length; i++)
            Assert.False(swap.ImageViews[i].IsNull);
    }

    [Fact]
    public void Recreate_Swapchain_At_New_Extent()
    {
        Assert.SkipUnless(IsWindows, "Surface tests are Win32-only for now.");
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var window = new Win32Window(640, 480, $"AhjoVk_{Guid.NewGuid():N}");

        Utf8Name[] instanceExts = [VulkanExtensions.KhrSurface, VulkanExtensions.KhrWin32Surface];
        using var instance = Instance.Create(new InstanceDescription { Extensions = instanceExts });
        using var surface  = Surface.CreateWin32(instance, window.HInstance, window.Hwnd);
        using var device   = CreatePresentDevice(instance, in surface, out _);

        var initialDesc = new SwapchainDescription
        {
            Surface = surface, Width = window.Width, Height = window.Height,
        };
        using var swap = new Swapchain(device, in initialDesc);
        VkExtent2D before = swap.Extent;
        Assert.True(before.width  > 0);
        Assert.True(before.height > 0);

        // Pump any pending messages, resize the window so currentExtent
        // moves with it, then ask the swapchain to rebuild.
        window.Resize(1024, 768);
        var resizedDesc = new SwapchainDescription
        {
            Surface = surface, Width = window.Width, Height = window.Height,
        };
        swap.Recreate(in resizedDesc);

        VkExtent2D after = swap.Extent;
        Assert.True(after.width  != before.width || after.height != before.height,
            $"Resize had no effect on swapchain extent (was {before.width}x{before.height}, still is).");
        Assert.True(after.width  > before.width);
        Assert.True(after.height > before.height);
        Assert.True(swap.ImageCount >= 2);
    }

    /// <summary>
    /// <see cref="Swapchain.Recreate"/> defaults to <c>vkDeviceWaitIdle</c>
    /// when no sync callback is provided; pass a callback (typically
    /// <c>FrameRing.WaitForInFlightFences</c>) to skip the device-wide
    /// stall in favor of waiting only on the per-frame fences that
    /// actually reference swapchain images. The callback must run; this
    /// test proves it does.
    /// </summary>
    [Fact]
    public void Recreate_InvokesSyncCallback_InsteadOfWaitIdle()
    {
        Assert.SkipUnless(IsWindows, "Surface tests are Win32-only for now.");
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var window = new Win32Window(640, 480, $"AhjoVk_{Guid.NewGuid():N}");
        Utf8Name[] instanceExts = [VulkanExtensions.KhrSurface, VulkanExtensions.KhrWin32Surface];
        using var instance = Instance.Create(new InstanceDescription { Extensions = instanceExts });
        using var surface  = Surface.CreateWin32(instance, window.HInstance, window.Hwnd);
        using var device   = CreatePresentDevice(instance, in surface, out _);

        var desc = new SwapchainDescription { Surface = surface, Width = window.Width, Height = window.Height };
        using var swap = new Swapchain(device, in desc);

        int callbackInvocations = 0;
        window.Resize(800, 600);
        var resizedDesc = new SwapchainDescription { Surface = surface, Width = window.Width, Height = window.Height };
        swap.Recreate(in resizedDesc, () => callbackInvocations++);

        Assert.Equal(1, callbackInvocations);
        Assert.True(swap.ImageCount >= 2);
    }

    [Fact]
    public void Swapchain_Recreate_With_Different_Surface_Throws()
    {
        Assert.SkipUnless(IsWindows, "Surface tests are Win32-only for now.");
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var window = new Win32Window(640, 480, $"AhjoVk_{Guid.NewGuid():N}");
        Utf8Name[] instanceExts = [VulkanExtensions.KhrSurface, VulkanExtensions.KhrWin32Surface];
        using var instance = Instance.Create(new InstanceDescription { Extensions = instanceExts });
        using var surfaceA = Surface.CreateWin32(instance, window.HInstance, window.Hwnd);
        using var surfaceB = Surface.CreateWin32(instance, window.HInstance, window.Hwnd);
        using var device   = CreatePresentDevice(instance, in surfaceA, out _);

        var desc = new SwapchainDescription { Surface = surfaceA, Width = 640, Height = 480 };
        using var swap = new Swapchain(device, in desc);

        // Recreate is non-generic, so wrap the call site in a static
        // method that takes the ref struct by `in` to satisfy the
        // "no ref struct in lambda" rule.
        TryRecreate(swap, surfaceB, out bool threw);
        Assert.True(threw);

        static void TryRecreate(Swapchain s, Surface other, out bool threw)
        {
            try
            {
                var bogus = new SwapchainDescription { Surface = other, Width = 640, Height = 480 };
                s.Recreate(in bogus);
                threw = false;
            }
            catch (ArgumentException) { threw = true; }
        }
    }

    /// <summary>
    /// Covers <see cref="SwapchainDescription.PreferredFormats"/>: the
    /// negotiator walks the list in priority order and returns the first
    /// surface-supported entry. An unsupported synthetic format at the
    /// head is skipped; an unrelated supported format that follows wins
    /// over <c>formats[0]</c>. Backs the engine's
    /// <c>[BGRA8_SRGB, RGBA8_SRGB]</c> cross-platform sRGB recipe.
    /// </summary>
    [Fact]
    public void NegotiateFormat_Walks_PreferredFormats_In_Priority_Order()
    {
        Assert.SkipUnless(IsWindows, "Surface tests are Win32-only for now.");
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var window = new Win32Window(640, 480, $"AhjoVk_{Guid.NewGuid():N}");
        Utf8Name[] instanceExts = [VulkanExtensions.KhrSurface, VulkanExtensions.KhrWin32Surface];
        using var instance = Instance.Create(new InstanceDescription { Extensions = instanceExts });
        using var surface  = Surface.CreateWin32(instance, window.HInstance, window.Hwnd);
        using var device   = CreatePresentDevice(instance, in surface, out _);

        // Query surface formats so the test stays driver-agnostic.
        uint count = 0;
        Vk.vkGetPhysicalDeviceSurfaceFormatsKHR(device.PhysicalDevice.Handle, surface.Handle, &count, null).ThrowIfFailed();
        Assert.True(count >= 1, "Surface reports zero formats.");
        var supported = new VkSurfaceFormatKHR[count];
        fixed (VkSurfaceFormatKHR* p = supported)
            Vk.vkGetPhysicalDeviceSurfaceFormatsKHR(device.PhysicalDevice.Handle, surface.Handle, &count, p).ThrowIfFailed();

        // Pick a non-first supported format to prove the negotiator
        // actually prefers the caller's list over `formats[0]`. If the
        // surface only exposes one, the priority-order claim is
        // vacuously true and we fall through to the no-op assertion.
        VkSurfaceFormatKHR? secondary = null;
        for (int i = 1; i < supported.Length; i++)
        {
            if (supported[i].format != supported[0].format ||
                supported[i].colorSpace != supported[0].colorSpace)
            {
                secondary = supported[i];
                break;
            }
        }

        // Synthetic format that no surface implements as a swapchain
        // colour attachment — drives the "skip unsupported, take next"
        // path.
        var synthetic = new VkSurfaceFormatKHR
        {
            format     = VkFormat.VK_FORMAT_R8_USCALED,
            colorSpace = VkColorSpaceKHR.VK_COLOR_SPACE_SRGB_NONLINEAR_KHR,
        };

        // (a) Empty list → driver's formats[0].
        TryCreate(device, in surface, window, ReadOnlySpan<VkSurfaceFormatKHR>.Empty, supported[0]);

        // (b) Single supported preference → exactly that format.
        if (secondary is { } s)
        {
            VkSurfaceFormatKHR[] one = [s];
            TryCreate(device, in surface, window, one, s);

            // (c) Unsupported synthetic at head, supported at tail → tail wins.
            VkSurfaceFormatKHR[] two = [synthetic, s];
            TryCreate(device, in surface, window, two, s);
        }
        else
        {
            // (b') Single supported preference matching formats[0] still
            // returns formats[0] — sanity check.
            VkSurfaceFormatKHR[] only = [supported[0]];
            TryCreate(device, in surface, window, only, supported[0]);
        }

        static void TryCreate(Device device, in Surface surface, Win32Window window,
            ReadOnlySpan<VkSurfaceFormatKHR> preferred, VkSurfaceFormatKHR expected)
        {
            var desc = new SwapchainDescription
            {
                Surface           = surface,
                Width             = window.Width,
                Height            = window.Height,
                PreferredFormats  = preferred,
            };
            using var swap = new Swapchain(device, in desc);
            Assert.Equal(expected.format,     swap.Format);
            Assert.Equal(expected.colorSpace, swap.ColorSpace);
        }
    }

    /// <summary>
    /// Regression for issue #103: <see cref="Swapchain.AcquireNextImage"/>
    /// must write its <c>out</c> image index correctly even when the
    /// caller's argument targets a GC-heap location — a class field or an
    /// array element (<c>out _frameState.ImageIndex</c> is natural calling
    /// code). The old implementation captured an unpinned pointer to the
    /// out param via <c>Unsafe.AsPointer</c> and passed it to
    /// <c>vkAcquireNextImageKHR</c>, which can block for up to the timeout
    /// while the driver holds the pointer; a compacting GC mid-wait would
    /// move the object and the driver would write through a stale pointer.
    /// The fix routes through a stack local. This test drives the natural
    /// heap-target calling pattern and asserts a valid index round-trips,
    /// then completes a clean acquire→present cycle so device teardown is
    /// validation-clean.
    /// </summary>
    [Fact]
    public void AcquireNextImage_Writes_ImageIndex_Into_HeapTarget()
    {
        Assert.SkipUnless(IsWindows, "Surface tests are Win32-only for now.");
        Assert.SkipUnless(VulkanDriverProbe.HasDriver, "No Vulkan driver on host.");

        using var window = new Win32Window(640, 480, $"AhjoVk_{Guid.NewGuid():N}");
        Utf8Name[] instanceExts = [VulkanExtensions.KhrSurface, VulkanExtensions.KhrWin32Surface];
        using var instance = Instance.Create(new InstanceDescription { Extensions = instanceExts });
        using var surface  = Surface.CreateWin32(instance, window.HInstance, window.Hwnd);
        using var device   = CreatePresentDevice(instance, in surface, out uint family);

        var desc = new SwapchainDescription { Surface = surface, Width = window.Width, Height = window.Height };
        using var swap = new Swapchain(device, in desc);

        using var ring = new FrameRing(device, framesInFlight: 1, queueFamily: family);
        var queue = device.GetQueue(family, 0);

        // The out target is a GC-heap array element — the exact pattern
        // from issue #103. Pre-seed with a sentinel the driver can never
        // return so a missed write would surface as an out-of-range index.
        uint[] heapTarget = new uint[1];
        heapTarget[0] = uint.MaxValue;

        using var fc = ring.BeginFrame();
        var acq = swap.AcquireNextImage(fc.ImageAcquired, TimeSpan.FromSeconds(1), out heapTarget[0]);
        Assert.True(acq is AcquireResult.Success or AcquireResult.Suboptimal,
            $"AcquireNextImage returned {acq}.");

        uint imageIndex = heapTarget[0];
        Assert.True(imageIndex < swap.ImageCount,
            $"imageIndex {imageIndex} is out of range (ImageCount {swap.ImageCount}).");

        fc.MarkImageAcquireSignaled();

        // Consume the acquire semaphore and present the image so device
        // teardown stays validation-clean (a host-signaled-but-unwaited
        // binary semaphore can't be destroyed). UNDEFINED→PRESENT_SRC is a
        // legal direct transition; no rendering needed.
        var rec = fc.CommandBuffers.Begin();
        try
        {
            var barrier = new ImageBarrier
            {
                Image          = swap.GetImageHandle(imageIndex),
                SrcStage       = Stage.TopOfPipe,    SrcAccess = Access.None,
                DstStage       = Stage.BottomOfPipe, DstAccess = Access.None,
                OldLayout      = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
                NewLayout      = VkImageLayout.VK_IMAGE_LAYOUT_PRESENT_SRC_KHR,
                Aspect         = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
                BaseMipLevel   = 0, LevelCount = 1,
                BaseArrayLayer = 0, LayerCount = 1,
            };
            rec.PipelineBarrier(barrier);
            fc.Submit(queue, ref rec, swap, imageIndex);
        }
        finally { rec.Dispose(); }

        swap.Present(queue, imageIndex);
        device.WaitIdle();
    }

    /// <summary>
    /// Picks the first physical device whose graphics queue family also
    /// supports presenting to <paramref name="surface"/>, then creates a
    /// device with VK_KHR_swapchain enabled.
    /// </summary>
    private static Device CreatePresentDevice(Instance instance, in Surface surface, out uint family)
    {
        VkSurfaceKHR_T* surfaceHandle = surface.Handle;
        uint chosen = uint.MaxValue;
        var gpu = instance.PickPhysicalDevice((in PhysicalDeviceInfo info) =>
        {
            for (int i = 0; i < info.QueueFamilies.Length; i++)
            {
                if (!info.QueueFamilies[i].SupportsGraphics) continue;
                uint supported = 0;
                Vk.vkGetPhysicalDeviceSurfaceSupportKHR(
                    info.Device.Handle, info.QueueFamilies[i].Index,
                    surfaceHandle, &supported).ThrowIfFailed();
                if (supported != 0)
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
