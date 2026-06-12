using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Wraps a <c>VkSwapchainKHR</c> plus its per-image
/// <c>VkImage</c> / <c>VkImageView</c> arrays. Class shape (rather than
/// the wrapper-wide <c>readonly struct</c> handle convention) because
/// resize swaps the underlying handles in-place and the per-image
/// view arrays need explicit lifecycle.
/// </summary>
/// <remarks>
/// <para><b>Lifecycle.</b> Construct against a <see cref="Surface"/> +
/// the <see cref="Device"/> that owns the swapchain device-extension.
/// Call <see cref="AcquireNextImage"/> per frame to drive the present
/// loop, <see cref="Recreate"/> on
/// <see cref="AcquireResult.OutOfDate"/> / window resize, and
/// <see cref="Dispose"/> when tearing down (after the device is idle).</para>
/// <para><b>Sharing mode.</b> Auto-detected per swapchain create. The
/// wrapper resolves the device's first graphics-capable queue family
/// and the first family that can present to the swapchain's
/// <see cref="Surface"/>. When they differ — most desktop hardware
/// unifies them, but split-family configurations exist (Mesa lavapipe,
/// some Apple Silicon setups, simulator stacks) — the swapchain is
/// created with <c>VK_SHARING_MODE_CONCURRENT</c> listing both family
/// indices in <c>pQueueFamilyIndices</c>, and present can flow
/// without queue-ownership transfers. Unified-family hardware keeps
/// the spec-default <c>VK_SHARING_MODE_EXCLUSIVE</c>.</para>
/// </remarks>
public sealed unsafe class Swapchain : IDisposable
{
    private readonly Device       _device;
    private readonly Surface      _surface;
    private readonly SemaphorePool _semaphorePool;
    private VkSwapchainKHR_T*     _handle;
    private VkImage_T*[]          _images        = [];
    private ImageView[]           _views         = [];
    // Per-acquired-image RenderingDone semaphores, indexed by the
    // imageIndex returned from vkAcquireNextImageKHR. Sized to the
    // swapchain's ImageCount; allocated in CreateOrRecreate, rotated
    // every Recreate, released in Dispose. Spec rationale: a binary
    // signal semaphore must be unsignalled when the signal executes
    // (VUID-vkQueueSubmit2-semaphore-03868); per-image keying uses
    // the swapchain's own "next acquire of image i waits on the prior
    // present of image i" ordering to guarantee that, where per-slot
    // keying does not.
    private BinarySemaphore[]     _renderingDone = [];
    private VkSurfaceFormatKHR    _format;
    private VkExtent2D            _extent;
    private VkPresentModeKHR      _presentMode;
    private ImageUsage            _imageUsage;
    private bool                  _disposed;

    public VkSwapchainKHR_T*     Handle      => _handle;
    public VkSurfaceFormatKHR    SurfaceFormat => _format;
    public VkFormat              Format      => _format.format;
    public VkColorSpaceKHR       ColorSpace  => _format.colorSpace;
    public VkExtent2D            Extent      => _extent;
    public VkPresentModeKHR      PresentMode => _presentMode;
    public ImageUsage            ImageUsage  => _imageUsage;
    public uint                  ImageCount  => (uint)_images.Length;
    public ReadOnlySpan<ImageView> ImageViews  => _views;

    /// <summary>
    /// Raw <c>VkImage_T*</c> for image <paramref name="index"/>, returned
    /// as <c>nint</c> so it can drop straight into
    /// <see cref="ImageBarrier.Image"/>. The wrapper does not vend a
    /// full <see cref="Image"/> for swapchain-owned images — they are
    /// not VMA-allocated and have no <see cref="Allocator"/> backing.
    /// </summary>
    public nint GetImageHandle(uint index) => (nint)_images[index];

    /// <summary>
    /// The per-image <c>RenderingDone</c> binary semaphore for
    /// <paramref name="imageIndex"/>. Pass it as the signal in the submit
    /// that produced this image's color contents and as the wait in the
    /// matching <see cref="Present(Queue, uint, in BinarySemaphore)"/>
    /// (the no-semaphore <see cref="Present(Queue, uint)"/> overload pulls
    /// it implicitly).
    /// </summary>
    /// <remarks>
    /// <para>Per-image rather than per-frame-in-flight: the spec requires
    /// the signal target to be unsignalled when the signal executes
    /// (VUID-vkQueueSubmit2-semaphore-03868). With a per-slot semaphore,
    /// frame N+1's submit can re-signal slot K's semaphore while a prior
    /// present of a different image is still holding it. Per-image works
    /// because the swapchain itself orders "next acquire of image i"
    /// after "prior present of image i", so the prior wait must have
    /// been consumed by the time we re-signal for image i.</para>
    /// <para>The handle is stable across acquire/present cycles but is
    /// invalidated by <see cref="Recreate"/> — never cache it across
    /// the recreate boundary.</para>
    /// </remarks>
    public BinarySemaphore GetRenderingDoneFor(uint imageIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _renderingDone[imageIndex];
    }

    public Swapchain(Device device, in SwapchainDescription desc)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (desc.Surface.IsNull) throw new ArgumentException("Surface is null.", nameof(desc));

        _device        = device;
        _surface       = desc.Surface;
        // Owned by the Swapchain so its lifetime tracks the per-image
        // semaphore array's. Internal — not exposed to callers.
        _semaphorePool = new SemaphorePool(device);
        CreateOrRecreate(in desc, oldSwapchain: null);
    }

    /// <summary>
    /// Tear down the existing swapchain and rebuild against the same
    /// surface. Pass a fresh <see cref="SwapchainDescription"/> with the
    /// new <see cref="SwapchainDescription.Width"/> /
    /// <see cref="SwapchainDescription.Height"/> on resize. The
    /// <see cref="Surface"/> field MUST equal the surface this swapchain
    /// was originally built against.
    /// </summary>
    /// <param name="desc">New swapchain parameters.</param>
    /// <param name="syncBeforeDestroy">
    /// Optional caller-supplied hook that drains GPU work referencing the
    /// old swapchain. <see langword="null"/> (default) preserves the
    /// historical behavior of calling <c>vkDeviceWaitIdle</c>, which is
    /// correct in isolation but defeats the point of having frames in
    /// flight. Frame-loop callers should pass
    /// <see cref="FrameRing.WaitForInFlightFences"/> (or any equivalent
    /// per-frame-fence drain) so the wait covers exactly the submits
    /// that touch the swapchain. The wrapper has no way to verify the
    /// callback is sufficient — a callback that misses a pending submit
    /// will surface as a driver-side device-lost on the next frame.
    /// </param>
    /// <remarks>
    /// <para><b>Why an optional callback rather than a hard
    /// <see cref="FrameRing"/> dependency.</b> The swapchain is usable
    /// without a frame ring (single-frame tools, screenshot harnesses,
    /// some test paths) — those callers want the conservative
    /// <c>vkDeviceWaitIdle</c> default. Engines built on
    /// <see cref="FrameRing"/> wire the callback once at startup and
    /// stop blocking the entire device on every resize.</para>
    /// <para><b>Binary-semaphore rotation (VUID-vkAcquireNextImageKHR-semaphore-01779).</b>
    /// The drain here (whether <c>vkDeviceWaitIdle</c> or the per-frame
    /// fences) flushes pending queue work, but it does <i>not</i> clear
    /// a host-side acquire signal that was never consumed by a submit —
    /// the canonical case is <see cref="AcquireNextImage"/> returning
    /// <see cref="AcquireResult.Suboptimal"/> (or completing successfully
    /// before the caller realized a resize was pending) followed by the
    /// caller bailing out to <see cref="Recreate"/> without submitting.
    /// Vulkan offers no host-reset for binary semaphores, so the only
    /// way to get back to a clean state is to destroy the stuck
    /// semaphore and create a fresh one. Use
    /// <see cref="SemaphorePool.Discard(BinarySemaphore)"/> for the
    /// destroy step and <see cref="SemaphorePool.AcquireBinary"/> for
    /// the replacement; per-slot acquire semaphores live on
    /// <see cref="FrameContext.ImageAcquired"/>, so the rotation
    /// typically happens once per frames-in-flight after Recreate. A
    /// binary semaphore that <i>was</i> consumed by a submit is left
    /// unsignaled by the drain and does not need rotation.</para>
    /// </remarks>
    public void Recreate(in SwapchainDescription desc, SwapchainSyncCallback? syncBeforeDestroy = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (desc.Surface.Handle != _surface.Handle)
            throw new ArgumentException(
                "Recreate must use the same Surface this Swapchain was constructed with.",
                nameof(desc));

        if (syncBeforeDestroy is null)
            Vk.vkDeviceWaitIdle(_device.Handle).ThrowIfFailed();
        else
            syncBeforeDestroy();

        VkSwapchainKHR_T* old = _handle;
        DestroyViews();
        CreateOrRecreate(in desc, oldSwapchain: old);
        if (old != null) Vk.vkDestroySwapchainKHR(_device.Handle, old, null);
    }

    /// <summary>
    /// Block (up to <paramref name="timeout"/>) for the next swapchain
    /// image. The acquired image will be ready for rendering once
    /// <paramref name="signaled"/> fires on the GPU. The CPU side
    /// receives an <see cref="AcquireResult"/> describing whether the
    /// caller can use the image (<see cref="AcquireResult.Success"/> /
    /// <see cref="AcquireResult.Suboptimal"/>) or must
    /// <see cref="Recreate"/> first
    /// (<see cref="AcquireResult.OutOfDate"/>).
    /// </summary>
    public AcquireResult AcquireNextImage(
        in BinarySemaphore  signaled,
        TimeSpan            timeout,
        out uint            imageIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // Write into a stack local rather than &imageIndex: the out param
        // can target a GC-heap location (a class field, an array element)
        // and vkAcquireNextImageKHR blocks for up to `timeout` while the
        // driver holds the pointer. An unpinned heap pointer captured via
        // Unsafe.AsPointer would dangle if a compacting GC moved the
        // object mid-wait — silent corruption. The local is on the stack
        // (never moved) and we copy out only after the call returns.
        uint idx = 0;
        VkResult r = Vk.vkAcquireNextImageKHR(
            _device.Handle, _handle, timeout.ToVulkanTimeout(),
            signaled.Handle, fence: null,
            &idx);
        imageIndex = idx;

        return r switch
        {
            VkResult.VK_SUCCESS                  => AcquireResult.Success,
            VkResult.VK_SUBOPTIMAL_KHR           => AcquireResult.Suboptimal,
            VkResult.VK_ERROR_OUT_OF_DATE_KHR    => AcquireResult.OutOfDate,
            VkResult.VK_ERROR_SURFACE_LOST_KHR   => AcquireResult.SurfaceLost,
            VkResult.VK_TIMEOUT                  => AcquireResult.Timeout,
            VkResult.VK_NOT_READY                => AcquireResult.NotReady,
            VkResult.VK_ERROR_DEVICE_LOST        => throw new VulkanException(r,
                "vkAcquireNextImageKHR: VK_ERROR_DEVICE_LOST. The VkDevice is no longer usable; tear down and recreate the device + every dependent resource."),
            _                                    => throw new VulkanException(r, "vkAcquireNextImageKHR"),
        };
    }

    /// <summary>
    /// Presents <paramref name="imageIndex"/> on <paramref name="queue"/>,
    /// waiting on this image's per-image <c>RenderingDone</c> semaphore
    /// (the one returned by <see cref="GetRenderingDoneFor"/>). The
    /// matching submit must have signaled the same semaphore — see the
    /// swapchain-aware
    /// <see cref="FrameContext.Submit(Queue, ref CommandRecorder, Swapchain, uint, Stage, Stage)"/>.
    /// </summary>
    public AcquireResult Present(Queue queue, uint imageIndex)
        => Present(queue, imageIndex, in _renderingDone[imageIndex]);

    /// <summary>
    /// Presents <paramref name="imageIndex"/> on <paramref name="queue"/>
    /// after <paramref name="waitSemaphore"/> fires (the
    /// rendering-done semaphore from the matching submit). Returns the
    /// same <see cref="AcquireResult"/> shape as
    /// <see cref="AcquireNextImage"/> so the caller's "did the surface
    /// change?" branch is symmetric with the acquire path.
    /// </summary>
    /// <remarks>
    /// Most callers should use the no-semaphore
    /// <see cref="Present(Queue, uint)"/> overload, which pulls the
    /// per-image semaphore from the swapchain. This explicit overload
    /// stays for the rare case where a caller wants to drive a custom
    /// signal/wait pair (multi-swapchain bridging, headless capture
    /// hooks, etc.).
    /// </remarks>
    public AcquireResult Present(Queue queue, uint imageIndex, in BinarySemaphore waitSemaphore)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ObjectDisposedException.ThrowIf(_disposed, this);

        VkSemaphore_T*    waitRaw = waitSemaphore.Handle;
        VkSwapchainKHR_T* swap    = _handle;
        uint              idx     = imageIndex;

        var info = new VkPresentInfoKHR
        {
            sType              = VkStructureType.VK_STRUCTURE_TYPE_PRESENT_INFO_KHR,
            waitSemaphoreCount = waitRaw != null ? 1u : 0u,
            pWaitSemaphores    = waitRaw != null ? &waitRaw : null,
            swapchainCount     = 1,
            pSwapchains        = &swap,
            pImageIndices      = &idx,
        };
        VkResult r = Vk.vkQueuePresentKHR(queue.Handle, &info);
        return r switch
        {
            VkResult.VK_SUCCESS                => AcquireResult.Success,
            VkResult.VK_SUBOPTIMAL_KHR         => AcquireResult.Suboptimal,
            VkResult.VK_ERROR_OUT_OF_DATE_KHR  => AcquireResult.OutOfDate,
            VkResult.VK_ERROR_SURFACE_LOST_KHR => AcquireResult.SurfaceLost,
            VkResult.VK_ERROR_DEVICE_LOST      => throw new VulkanException(r,
                "vkQueuePresentKHR: VK_ERROR_DEVICE_LOST. The VkDevice is no longer usable; tear down and recreate the device + every dependent resource."),
            _                                  => throw new VulkanException(r, "vkQueuePresentKHR"),
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        DestroyViews();
        DiscardRenderingDoneSemaphores();
        if (_handle != null)
        {
            Vk.vkDestroySwapchainKHR(_device.Handle, _handle, null);
            _handle = null;
        }
        _semaphorePool.Dispose();
    }

    private void CreateOrRecreate(in SwapchainDescription desc, VkSwapchainKHR_T* oldSwapchain)
    {
        VkPhysicalDevice_T* gpu = _device.PhysicalDevice.Handle;

        // ---- Caps ----
        VkSurfaceCapabilitiesKHR caps = default;
        Vk.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(gpu, _surface.Handle, &caps).ThrowIfFailed();

        // ---- Format ----
        _format = NegotiateFormat(gpu, in desc);

        // ---- Present mode ----
        _presentMode = NegotiatePresentMode(gpu, in desc);

        // ---- Extent ----
        _extent = caps.currentExtent.width != ~0u
            ? caps.currentExtent
            : new VkExtent2D
            {
                width  = Math.Clamp(desc.Width  == 0 ? 1u : desc.Width,
                                    caps.minImageExtent.width,  caps.maxImageExtent.width),
                height = Math.Clamp(desc.Height == 0 ? 1u : desc.Height,
                                    caps.minImageExtent.height, caps.maxImageExtent.height),
            };

        // ---- Image count ----
        uint requested = desc.PreferredImageCount == 0 ? caps.minImageCount + 1u : desc.PreferredImageCount;
        uint maxClamp  = caps.maxImageCount == 0 ? requested : caps.maxImageCount; // 0 means "no limit"
        uint count     = Math.Clamp(requested, caps.minImageCount, maxClamp);

        // ---- Usage ----
        _imageUsage = desc.ImageUsage == 0 ? ImageUsage.ColorAttachment : desc.ImageUsage;

        // Resolve graphics + present families and decide sharing mode.
        // On unified-family hardware (the dominant desktop case) gfx ==
        // present, the family-index list collapses to one entry, and we
        // ship Exclusive. On split-family hardware Concurrent listing
        // both indices skips the queue-ownership transfer barriers the
        // wrapper does not emit.
        Span<uint> shareFamilies = stackalloc uint[2];
        VkSharingMode sharingMode = ResolveSharingMode(gpu, shareFamilies, out uint familyCount);

        VkSwapchainCreateInfoKHR ci;
        fixed (uint* pFamilies = shareFamilies)
        {
            ci = new VkSwapchainCreateInfoKHR
            {
                sType                 = VkStructureType.VK_STRUCTURE_TYPE_SWAPCHAIN_CREATE_INFO_KHR,
                surface               = _surface.Handle,
                minImageCount         = count,
                imageFormat           = _format.format,
                imageColorSpace       = _format.colorSpace,
                imageExtent           = _extent,
                imageArrayLayers      = 1,
                imageUsage            = (uint)_imageUsage,
                imageSharingMode      = sharingMode,
                queueFamilyIndexCount = sharingMode == VkSharingMode.VK_SHARING_MODE_CONCURRENT ? familyCount : 0u,
                pQueueFamilyIndices   = sharingMode == VkSharingMode.VK_SHARING_MODE_CONCURRENT ? pFamilies : null,
                preTransform          = caps.currentTransform,
                compositeAlpha        = VkCompositeAlphaFlagBitsKHR.VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR,
                presentMode           = _presentMode,
                clipped               = 1,
                oldSwapchain          = oldSwapchain,
            };
            VkSwapchainKHR_T* raw = null;
            Vk.vkCreateSwapchainKHR(_device.Handle, &ci, null, &raw).ThrowIfFailed();
            _handle = raw;
        }

        LoadImagesAndViews();
    }

    private void LoadImagesAndViews()
    {
        uint imageCount = 0;
        Vk.vkGetSwapchainImagesKHR(_device.Handle, _handle, &imageCount, null).ThrowIfErrored();

        _images = new VkImage_T*[imageCount];
        _views  = new ImageView[imageCount];
        // Reallocate per-image RenderingDone semaphores. On the first
        // call (initial create) _renderingDone is empty; on Recreate it
        // holds the prior swapchain's per-image semaphores, which are
        // either consumed-and-unsignaled (safe to release back to the
        // pool but easier to discard uniformly) or stuck signaled if
        // the matching present returned OutOfDate (must be discarded —
        // there is no host-reset for binary semaphores). Discarding
        // every semaphore covers both cases without per-element state
        // tracking.
        DiscardRenderingDoneSemaphores();
        AllocateRenderingDoneSemaphores((int)imageCount);

        fixed (VkImage_T** p = _images)
            Vk.vkGetSwapchainImagesKHR(_device.Handle, _handle, &imageCount, p).ThrowIfErrored();

        for (uint i = 0; i < imageCount; i++)
        {
            var ci = new VkImageViewCreateInfo
            {
                sType    = VkStructureType.VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO,
                image    = _images[i],
                viewType = VkImageViewType.VK_IMAGE_VIEW_TYPE_2D,
                format   = _format.format,
                subresourceRange = new VkImageSubresourceRange
                {
                    aspectMask     = (uint)VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
                    baseMipLevel   = 0, levelCount = 1,
                    baseArrayLayer = 0, layerCount = 1,
                },
            };
            VkImageView_T* viewRaw = null;
            Vk.vkCreateImageView(_device.Handle, &ci, null, &viewRaw).ThrowIfFailed();
            _views[i] = new ImageView(viewRaw, _device.Handle);
        }
    }

    private void DestroyViews()
    {
        for (int i = 0; i < _views.Length; i++) _views[i].Dispose();
        _views  = [];
        _images = [];
    }

    /// <summary>
    /// Destroy + replace every per-image <c>RenderingDone</c> semaphore
    /// the wrapper owns. Called from <see cref="Recreate"/> (after the
    /// drain) and from <see cref="Dispose"/>. Discards rather than
    /// releases so a semaphore stuck signaled (submit landed but
    /// matching present returned <c>OutOfDate</c>) doesn't poison the
    /// pool's reuse path. The pool's <see cref="SemaphorePool.Discard"/>
    /// destroys the underlying <c>VkSemaphore</c>; binary semaphores
    /// can't be host-reset.
    /// </summary>
    private void DiscardRenderingDoneSemaphores()
    {
        for (int i = 0; i < _renderingDone.Length; i++)
            _semaphorePool.Discard(_renderingDone[i]);
        _renderingDone = [];
    }

    private void AllocateRenderingDoneSemaphores(int count)
    {
        var fresh = new BinarySemaphore[count];
        for (int i = 0; i < count; i++) fresh[i] = _semaphorePool.AcquireBinary();
        _renderingDone = fresh;
    }

    /// <summary>
    /// Walks the physical device's queue families, picks the first
    /// graphics-capable family and the first family that can present to
    /// <see cref="_surface"/>, and decides between
    /// <c>VK_SHARING_MODE_EXCLUSIVE</c> (same family) and
    /// <c>VK_SHARING_MODE_CONCURRENT</c> (different families). On
    /// Concurrent, fills <paramref name="shareFamilies"/> with the two
    /// indices and returns the count via <paramref name="familyCount"/>.
    /// </summary>
    private VkSharingMode ResolveSharingMode(
        VkPhysicalDevice_T* gpu,
        Span<uint>          shareFamilies,
        out uint            familyCount)
    {
        uint count = 0;
        Vk.vkGetPhysicalDeviceQueueFamilyProperties(gpu, &count, null);
        if (count == 0)
        {
            familyCount = 0;
            return VkSharingMode.VK_SHARING_MODE_EXCLUSIVE;
        }

        Span<VkQueueFamilyProperties> qfp = count <= 16
            ? stackalloc VkQueueFamilyProperties[(int)count]
            : new VkQueueFamilyProperties[count];
        fixed (VkQueueFamilyProperties* p = qfp)
            Vk.vkGetPhysicalDeviceQueueFamilyProperties(gpu, &count, p);

        uint graphicsFamily = uint.MaxValue;
        uint presentFamily  = uint.MaxValue;
        for (uint i = 0; i < count; i++)
        {
            if (graphicsFamily == uint.MaxValue &&
                ((VkQueueFlagBits)qfp[(int)i].queueFlags & VkQueueFlagBits.VK_QUEUE_GRAPHICS_BIT) != 0)
            {
                graphicsFamily = i;
            }

            if (presentFamily == uint.MaxValue)
            {
                uint supports = 0;
                Vk.vkGetPhysicalDeviceSurfaceSupportKHR(gpu, i, _surface.Handle, &supports).ThrowIfFailed();
                if (supports != 0) presentFamily = i;
            }

            if (graphicsFamily != uint.MaxValue && presentFamily != uint.MaxValue) break;
        }

        // Either family un-resolvable (no graphics queue, or no
        // present-capable family on this surface) means the swapchain
        // can't function as a presentation source — fall back to
        // Exclusive and let the driver/validation layer flag the
        // missing capability with a clearer message than a sharing-
        // mode mismatch would produce.
        if (graphicsFamily == uint.MaxValue || presentFamily == uint.MaxValue ||
            graphicsFamily == presentFamily)
        {
            familyCount = 0;
            return VkSharingMode.VK_SHARING_MODE_EXCLUSIVE;
        }

        shareFamilies[0] = graphicsFamily;
        shareFamilies[1] = presentFamily;
        familyCount = 2;
        return VkSharingMode.VK_SHARING_MODE_CONCURRENT;
    }

    private VkSurfaceFormatKHR NegotiateFormat(VkPhysicalDevice_T* gpu, in SwapchainDescription desc)
    {
        uint count = 0;
        Vk.vkGetPhysicalDeviceSurfaceFormatsKHR(gpu, _surface.Handle, &count, null).ThrowIfErrored();
        if (count == 0)
            throw new VulkanException(VkResult.VK_ERROR_INITIALIZATION_FAILED,
                "Surface reports zero supported formats.");

        Span<VkSurfaceFormatKHR> formats = count <= 16
            ? stackalloc VkSurfaceFormatKHR[(int)count]
            : new VkSurfaceFormatKHR[count];
        fixed (VkSurfaceFormatKHR* p = formats)
            Vk.vkGetPhysicalDeviceSurfaceFormatsKHR(gpu, _surface.Handle, &count, p).ThrowIfErrored();

        // Walk the caller's priority list once; first match wins. Spec
        // doesn't guarantee any specific format/colorSpace pair other
        // than (count > 0), so callers that care about a specific
        // encoding (e.g. sRGB) should pass several variants.
        for (int p = 0; p < desc.PreferredFormats.Length; p++)
        {
            var want = desc.PreferredFormats[p];
            if (want.format == VkFormat.VK_FORMAT_UNDEFINED) continue;
            for (int i = 0; i < formats.Length; i++)
            {
                if (formats[i].format     == want.format &&
                    formats[i].colorSpace == want.colorSpace)
                    return formats[i];
            }
        }
        return formats[0];
    }

    private VkPresentModeKHR NegotiatePresentMode(VkPhysicalDevice_T* gpu, in SwapchainDescription desc)
    {
        // FIFO is required by the spec — if the caller didn't request
        // anything else, ship FIFO without an extra round-trip query.
        if (desc.PreferredPresentMode == default ||
            desc.PreferredPresentMode == VkPresentModeKHR.VK_PRESENT_MODE_FIFO_KHR)
            return VkPresentModeKHR.VK_PRESENT_MODE_FIFO_KHR;

        uint count = 0;
        Vk.vkGetPhysicalDeviceSurfacePresentModesKHR(gpu, _surface.Handle, &count, null).ThrowIfErrored();
        Span<VkPresentModeKHR> modes = count <= 8
            ? stackalloc VkPresentModeKHR[(int)count]
            : new VkPresentModeKHR[count];
        fixed (VkPresentModeKHR* p = modes)
            Vk.vkGetPhysicalDeviceSurfacePresentModesKHR(gpu, _surface.Handle, &count, p).ThrowIfErrored();

        for (int i = 0; i < modes.Length; i++)
            if (modes[i] == desc.PreferredPresentMode) return desc.PreferredPresentMode;

        return VkPresentModeKHR.VK_PRESENT_MODE_FIFO_KHR;
    }
}
