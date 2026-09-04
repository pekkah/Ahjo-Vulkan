using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;
using Ahjo.Vulkan.Testing;
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
        TestGate.RequirePlatform(IsWindows, "Surface tests are Win32-only for now.");
        TestGate.RequireDriver();

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
        TestGate.RequirePlatform(IsWindows, "Surface tests are Win32-only for now.");
        TestGate.RequireDriver();

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
        TestGate.RequirePlatform(IsWindows, "Surface tests are Win32-only for now.");
        TestGate.RequireDriver();

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
        TestGate.RequirePlatform(IsWindows, "Surface tests are Win32-only for now.");
        TestGate.RequireDriver();

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
        TestGate.RequirePlatform(IsWindows, "Surface tests are Win32-only for now.");
        TestGate.RequireDriver();

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
        TestGate.RequirePlatform(IsWindows, "Surface tests are Win32-only for now.");
        TestGate.RequireDriver();

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
    /// Regression for issue #105 (fixed via #119's valid-by-default
    /// descriptions): <c>VK_PRESENT_MODE_IMMEDIATE_KHR</c> is the zero enum
    /// value, so the old "<c>PreferredPresentMode == default</c> means unset →
    /// ship FIFO" logic made IMMEDIATE unrequestable. Now FIFO is the field
    /// initializer default and IMMEDIATE survives as an explicit request. This
    /// test asks for IMMEDIATE and asserts the swapchain honours it when the
    /// surface supports it (skips when it doesn't — SwiftShader/headless ICDs
    /// may expose FIFO only).
    /// </summary>
    [Fact]
    public void Request_ImmediatePresentMode_IsHonouredWhenSupported()
    {
        TestGate.RequirePlatform(IsWindows, "Surface tests are Win32-only for now.");
        TestGate.RequireDriver();

        using var window = new Win32Window(640, 480, $"AhjoVk_{Guid.NewGuid():N}");
        Utf8Name[] instanceExts = [VulkanExtensions.KhrSurface, VulkanExtensions.KhrWin32Surface];
        using var instance = Instance.Create(new InstanceDescription { Extensions = instanceExts });
        using var surface  = Surface.CreateWin32(instance, window.HInstance, window.Hwnd);
        using var device   = CreatePresentDevice(instance, in surface, out _);

        // Does the surface support IMMEDIATE at all?
        uint count = 0;
        Vk.vkGetPhysicalDeviceSurfacePresentModesKHR(device.PhysicalDevice.Handle, surface.Handle, &count, null).ThrowIfFailed();
        var modes = new VkPresentModeKHR[count];
        fixed (VkPresentModeKHR* p = modes)
            Vk.vkGetPhysicalDeviceSurfacePresentModesKHR(device.PhysicalDevice.Handle, surface.Handle, &count, p).ThrowIfFailed();

        bool supportsImmediate = Array.IndexOf(modes, VkPresentModeKHR.VK_PRESENT_MODE_IMMEDIATE_KHR) >= 0;
        TestGate.RequireDeviceFeature(supportsImmediate, "Surface does not expose VK_PRESENT_MODE_IMMEDIATE_KHR.");

        var desc = new SwapchainDescription
        {
            Surface              = surface,
            Width                = window.Width,
            Height               = window.Height,
            PreferredPresentMode = VkPresentModeKHR.VK_PRESENT_MODE_IMMEDIATE_KHR,
        };
        using var swap = new Swapchain(device, in desc);

        Assert.Equal(VkPresentModeKHR.VK_PRESENT_MODE_IMMEDIATE_KHR, swap.PresentMode);
    }

    // ---- Issue #120: SwapchainState machine ----

    [Fact]
    public void NewSwapchain_StartsReady()
    {
        TestGate.RequirePlatform(IsWindows, "Surface tests are Win32-only for now.");
        TestGate.RequireDriver();

        using var window = new Win32Window(640, 480, $"AhjoVk_{Guid.NewGuid():N}");
        Utf8Name[] instanceExts = [VulkanExtensions.KhrSurface, VulkanExtensions.KhrWin32Surface];
        using var instance = Instance.Create(new InstanceDescription { Extensions = instanceExts });
        using var surface  = Surface.CreateWin32(instance, window.HInstance, window.Hwnd);
        using var device   = CreatePresentDevice(instance, in surface, out _);

        var desc = new SwapchainDescription { Surface = surface, Width = window.Width, Height = window.Height };
        using var swap = new Swapchain(device, in desc);

        Assert.Equal(SwapchainState.Ready, swap.State);
    }

    [Fact]
    public void Recreate_ReturnsReady_OnSuccess()
    {
        TestGate.RequirePlatform(IsWindows, "Surface tests are Win32-only for now.");
        TestGate.RequireDriver();

        using var window = new Win32Window(640, 480, $"AhjoVk_{Guid.NewGuid():N}");
        Utf8Name[] instanceExts = [VulkanExtensions.KhrSurface, VulkanExtensions.KhrWin32Surface];
        using var instance = Instance.Create(new InstanceDescription { Extensions = instanceExts });
        using var surface  = Surface.CreateWin32(instance, window.HInstance, window.Hwnd);
        using var device   = CreatePresentDevice(instance, in surface, out _);

        var desc = new SwapchainDescription { Surface = surface, Width = window.Width, Height = window.Height };
        using var swap = new Swapchain(device, in desc);

        Assert.Equal(SwapchainState.Ready, swap.Recreate(in desc));
    }

    /// <summary>
    /// Acquire/present on a Minimized or Poisoned swapchain throw instead
    /// of looping forever against a dead handle (#110/#112). The states
    /// are driven through the internal test seam — provoking a real
    /// window-manager minimize or a failing vkCreateSwapchainKHR from CI
    /// is not portable.
    /// </summary>
    [Fact]
    public void AcquireAndPresent_InMinimizedOrPoisoned_Throw()
    {
        TestGate.RequirePlatform(IsWindows, "Surface tests are Win32-only for now.");
        TestGate.RequireDriver();

        using var window = new Win32Window(640, 480, $"AhjoVk_{Guid.NewGuid():N}");
        Utf8Name[] instanceExts = [VulkanExtensions.KhrSurface, VulkanExtensions.KhrWin32Surface];
        using var instance = Instance.Create(new InstanceDescription { Extensions = instanceExts });
        using var surface  = Surface.CreateWin32(instance, window.HInstance, window.Hwnd);
        using var device   = CreatePresentDevice(instance, in surface, out uint family);

        var desc = new SwapchainDescription { Surface = surface, Width = window.Width, Height = window.Height };
        using var swap = new Swapchain(device, in desc);
        using var semaphores = new SemaphorePool(device);
        var acquireSem = semaphores.AcquireBinary();
        var queue = device.GetQueue(family, 0);

        foreach (var state in new[] { SwapchainState.Minimized, SwapchainState.Poisoned })
        {
            swap.OverrideStateForTesting(state);
            var acquireEx = Assert.Throws<InvalidOperationException>(
                () => swap.AcquireNextImage(in acquireSem, TimeSpan.Zero, out _));
            Assert.Contains(state.ToString(), acquireEx.Message);
            Assert.Throws<InvalidOperationException>(() => swap.Present(queue, 0));
        }

        // Restore so Dispose runs against coherent state.
        swap.OverrideStateForTesting(SwapchainState.Ready);
        semaphores.Release(acquireSem);
    }

    /// <summary>
    /// Recreate after device loss fails fast with the cached DeviceLost
    /// exception and poisons the swapchain — no drain or create is
    /// attempted against the dead device (#120).
    /// </summary>
    [Fact]
    public void Recreate_AfterDeviceLoss_ThrowsAndPoisons()
    {
        TestGate.RequirePlatform(IsWindows, "Surface tests are Win32-only for now.");
        TestGate.RequireDriver();

        using var window = new Win32Window(640, 480, $"AhjoVk_{Guid.NewGuid():N}");
        Utf8Name[] instanceExts = [VulkanExtensions.KhrSurface, VulkanExtensions.KhrWin32Surface];
        using var instance = Instance.Create(new InstanceDescription { Extensions = instanceExts });
        using var surface  = Surface.CreateWin32(instance, window.HInstance, window.Hwnd);
        using var device   = CreatePresentDevice(instance, in surface, out _);

        var desc = new SwapchainDescription { Surface = surface, Width = window.Width, Height = window.Height };
        using var swap = new Swapchain(device, in desc);

        device.MarkLost();
        // SwapchainDescription is a ref struct — no lambda capture; same
        // wrapper-method shape as Recreate_WithWrongSurface above.
        TryRecreate(swap, surface, window.Width, window.Height, out VulkanException? ex);
        Assert.NotNull(ex);
        Assert.Equal(VkResult.VK_ERROR_DEVICE_LOST, ex.Result);
        Assert.Equal(SwapchainState.Poisoned, swap.State);

        static void TryRecreate(Swapchain s, Surface surface, uint width, uint height, out VulkanException? thrown)
        {
            try
            {
                var retry = new SwapchainDescription { Surface = surface, Width = width, Height = height };
                s.Recreate(in retry);
                thrown = null;
            }
            catch (VulkanException e) { thrown = e; }
        }
    }

    // ---- Swapchain.GetImage (issue #219 D11) ----------------------------
    //
    // What these five cases can prove here: that the borrowed Image reports
    // the swapchain's own facts, that it is never tracked and never destroys,
    // that the WholeImage region helpers now cover the swapchain (the E4
    // defect), that the values track Recreate, and that the bounds check
    // names its parameter.
    //
    // What they deliberately cannot prove: that a blit *into* a swapchain
    // image executes correctly and validation-layer-clean end to end. That
    // needs a present loop on real hardware and is what samples/HelloDlaa's
    // hardware run supplies — CI has no NVIDIA GPU (#32).

    [Fact]
    public void GetImage_Reports_The_Swapchains_Own_Facts()
    {
        TestGate.RequirePlatform(IsWindows, "Surface tests are Win32-only for now.");
        TestGate.RequireDriver();

        using var window = new Win32Window(640, 480, $"AhjoVk_{Guid.NewGuid():N}");
        Utf8Name[] instanceExts = [VulkanExtensions.KhrSurface, VulkanExtensions.KhrWin32Surface];
        using var instance = Instance.Create(new InstanceDescription { Extensions = instanceExts });
        using var surface  = Surface.CreateWin32(instance, window.HInstance, window.Hwnd);
        using var device   = CreatePresentDevice(instance, in surface, out _);

        var desc = new SwapchainDescription { Surface = surface, Width = window.Width, Height = window.Height };
        using var swap = new Swapchain(device, in desc);

        for (uint i = 0; i < swap.ImageCount; i++)
        {
            Image image = swap.GetImage(i);
            Assert.Equal((ulong)swap.GetImageHandle(i), image.RawHandle);
            Assert.False(image.IsNull);
            Assert.Equal(swap.Format,        image.Format);
            Assert.Equal(swap.Extent.width,  image.Width);
            Assert.Equal(swap.Extent.height, image.Height);
            Assert.Equal(swap.ImageUsage,    image.Usage);
            Assert.Equal(1u, image.Depth);
            Assert.Equal(1u, image.MipLevels);
            Assert.Equal(1u, image.ArrayLayers);
        }
    }

    /// <summary>
    /// The direct regression test for the <c>HandleRegistry</c> question
    /// (#219 E7): a borrowed handle is never tracked, so a second
    /// <c>Dispose</c> is not a double-dispose and cannot throw — and the
    /// swapchain's image survives both calls, because <c>Dispose</c> returns
    /// before <c>vmaDestroyImage</c>.
    /// </summary>
    [Fact]
    public void GetImage_Returns_A_Borrowed_Handle_That_Dispose_Does_Not_Destroy()
    {
        TestGate.RequirePlatform(IsWindows, "Surface tests are Win32-only for now.");
        TestGate.RequireDriver();

        using var window = new Win32Window(640, 480, $"AhjoVk_{Guid.NewGuid():N}");
        Utf8Name[] instanceExts = [VulkanExtensions.KhrSurface, VulkanExtensions.KhrWin32Surface];
        using var instance = Instance.Create(new InstanceDescription { Extensions = instanceExts });
        using var surface  = Surface.CreateWin32(instance, window.HInstance, window.Hwnd);
        using var device   = CreatePresentDevice(instance, in surface, out _);

        var desc = new SwapchainDescription { Surface = surface, Width = window.Width, Height = window.Height };
        using var swap = new Swapchain(device, in desc);

        bool wasEnabled = AhjoValidation.Enabled;
        try
        {
            AhjoValidation.Enabled = true;

            Image image = swap.GetImage(0);
            Assert.False(image.OwnsHandle);
            Assert.False(image.OwnsMemory);

            ulong before = image.RawHandle;
            image.Dispose();
            image.Dispose();

            Assert.Equal(before, (ulong)swap.GetImageHandle(0));
            Assert.Equal(before, swap.GetImage(0).RawHandle);
        }
        finally { AhjoValidation.Enabled = wasEnabled; }
    }

    /// <summary>
    /// Regression for #219 E4: <c>ImageBlitRegion.WholeImage</c> reads
    /// <c>Width</c>/<c>Height</c>/<c>Depth</c> off the destination, so a
    /// <c>FromRaw</c> swapchain handle produced a degenerate destination box
    /// and the blit silently did nothing. Asserts both halves — the fix and
    /// the defect it fixes.
    /// </summary>
    [Fact]
    public void GetImage_Makes_WholeImage_Regions_Cover_The_Swapchain()
    {
        TestGate.RequirePlatform(IsWindows, "Surface tests are Win32-only for now.");
        TestGate.RequireDriver();

        using var window = new Win32Window(640, 480, $"AhjoVk_{Guid.NewGuid():N}");
        Utf8Name[] instanceExts = [VulkanExtensions.KhrSurface, VulkanExtensions.KhrWin32Surface];
        using var instance = Instance.Create(new InstanceDescription { Extensions = instanceExts });
        using var surface  = Surface.CreateWin32(instance, window.HInstance, window.Hwnd);
        using var device   = CreatePresentDevice(instance, in surface, out _);

        var desc = new SwapchainDescription
        {
            Surface    = surface,
            Width      = window.Width,
            Height     = window.Height,
            ImageUsage = ImageUsage.ColorAttachment | ImageUsage.TransferDst,
        };
        using var swap = new Swapchain(device, in desc);

        using var source = device.Allocator.CreateImage(
            new ImageDescription
            {
                ImageType     = VkImageType.VK_IMAGE_TYPE_2D,
                Format        = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
                Width         = 64, Height = 64, Depth = 1,
                MipLevels     = 1,  ArrayLayers = 1,
                Samples       = VkSampleCountFlagBits.VK_SAMPLE_COUNT_1_BIT,
                Tiling        = VkImageTiling.VK_IMAGE_TILING_OPTIMAL,
                Usage         = ImageUsage.TransferSrc,
                InitialLayout = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            },
            new AllocationDescription { Usage = MemoryUsage.AutoPreferDevice });

        Image described = swap.GetImage(0);
        ImageBlitRegion good = ImageBlitRegion.WholeImage(in source, in described);
        Assert.Equal((int)swap.Extent.width,  good.DstOffset1.x);
        Assert.Equal((int)swap.Extent.height, good.DstOffset1.y);
        Assert.Equal(1, good.DstOffset1.z);

        Image bare = Image.FromRaw(swap.GetImageHandle(0));
        ImageBlitRegion degenerate = ImageBlitRegion.WholeImage(in source, in bare);
        Assert.Equal(0, degenerate.DstOffset1.x);
        Assert.Equal(0, degenerate.DstOffset1.y);
    }

    [Fact]
    public void GetImage_Tracks_Recreate()
    {
        TestGate.RequirePlatform(IsWindows, "Surface tests are Win32-only for now.");
        TestGate.RequireDriver();

        using var window = new Win32Window(640, 480, $"AhjoVk_{Guid.NewGuid():N}");
        Utf8Name[] instanceExts = [VulkanExtensions.KhrSurface, VulkanExtensions.KhrWin32Surface];
        using var instance = Instance.Create(new InstanceDescription { Extensions = instanceExts });
        using var surface  = Surface.CreateWin32(instance, window.HInstance, window.Hwnd);
        using var device   = CreatePresentDevice(instance, in surface, out _);

        var initialDesc = new SwapchainDescription { Surface = surface, Width = window.Width, Height = window.Height };
        using var swap = new Swapchain(device, in initialDesc);

        VkExtent2D before = swap.Extent;
        Assert.Equal(before.width,  swap.GetImage(0).Width);
        Assert.Equal(before.height, swap.GetImage(0).Height);

        window.Resize(1024, 768);
        var resizedDesc = new SwapchainDescription { Surface = surface, Width = window.Width, Height = window.Height };
        swap.Recreate(in resizedDesc);

        VkExtent2D after = swap.Extent;
        Assert.True(after.width != before.width || after.height != before.height,
            $"Resize had no effect on swapchain extent (was {before.width}x{before.height}, still is).");

        Image image = swap.GetImage(0);
        Assert.Equal(after.width,  image.Width);
        Assert.Equal(after.height, image.Height);
        Assert.Equal((ulong)swap.GetImageHandle(0), image.RawHandle);
    }

    [Fact]
    public void GetImage_Rejects_An_Out_Of_Range_Index()
    {
        TestGate.RequirePlatform(IsWindows, "Surface tests are Win32-only for now.");
        TestGate.RequireDriver();

        using var window = new Win32Window(640, 480, $"AhjoVk_{Guid.NewGuid():N}");
        Utf8Name[] instanceExts = [VulkanExtensions.KhrSurface, VulkanExtensions.KhrWin32Surface];
        using var instance = Instance.Create(new InstanceDescription { Extensions = instanceExts });
        using var surface  = Surface.CreateWin32(instance, window.HInstance, window.Hwnd);
        using var device   = CreatePresentDevice(instance, in surface, out _);

        var desc = new SwapchainDescription { Surface = surface, Width = window.Width, Height = window.Height };
        using var swap = new Swapchain(device, in desc);

        uint outOfRange = swap.ImageCount;
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => swap.GetImage(outOfRange));
        // The point of the explicit guard over letting the array throw:
        // IndexOutOfRangeException from `_images` would name nothing the caller
        // can see. Assert the parameter name, since that is the whole benefit.
        Assert.Equal("index", ex.ParamName);
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
