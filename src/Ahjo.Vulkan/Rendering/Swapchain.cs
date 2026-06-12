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
    // Old swapchain handles + their per-image RenderingDone semaphores
    // retired by a Recreate whose drain was the caller's fence callback
    // (issue #111): per-frame fences prove submit completion, not present
    // completion, so destroying these immediately would violate
    // VUID-vkDestroySemaphore-semaphore-01137 /
    // VUID-vkDestroySwapchainKHR-swapchain-01282. They are destroyed at
    // the points where device-wide completion is proven: a later Recreate
    // that used the vkDeviceWaitIdle default, or Dispose (whose contract
    // is "call after the device is idle"). Growth is bounded by
    // recreates-per-session — user-interactive rate, not per-frame.
    private readonly List<(nint Swapchain, BinarySemaphore[] RenderingDone)> _retired = [];
    private VkSurfaceFormatKHR    _format;
    private VkExtent2D            _extent;
    private VkPresentModeKHR      _presentMode;
    private ImageUsage            _imageUsage;
    private SwapchainState        _state;
    private bool                  _disposed;

    /// <summary>
    /// Current lifecycle state — see <see cref="SwapchainState"/> for the
    /// transition table. <see cref="Recreate"/> returns the post-call
    /// state, so frame loops rarely need to read this directly.
    /// </summary>
    public SwapchainState State => _state;

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
        // Zero extent at construction (app launched minimized, #110) is a
        // legal starting state: no VkSwapchainKHR exists yet; the first
        // non-minimized Recreate performs the initial create.
        _state = CreateOrRecreate(in desc, oldSwapchain: null);
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
    /// Because per-frame fences prove submit completion but not <i>present</i>
    /// completion, the old swapchain handle and its per-image semaphores
    /// are not destroyed immediately on this path — they are parked and
    /// destroyed at the next proven-idle point (a later default-drain
    /// <c>Recreate</c>, or <see cref="Dispose"/>); see issue #111.
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
    /// <returns>
    /// The post-call <see cref="SwapchainState"/>:
    /// <see cref="SwapchainState.Ready"/> on success,
    /// <see cref="SwapchainState.Minimized"/> when the surface reports a
    /// zero extent (nothing is drained or destroyed — retry when the
    /// window is restored). A create failure sets
    /// <see cref="SwapchainState.Poisoned"/> and rethrows.
    /// </returns>
    public SwapchainState Recreate(in SwapchainDescription desc, SwapchainSyncCallback? syncBeforeDestroy = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (desc.Surface.Handle != _surface.Handle)
            throw new ArgumentException(
                "Recreate must use the same Surface this Swapchain was constructed with.",
                nameof(desc));
        // Fail fast after device loss instead of attempting a drain +
        // create against a dead device (#120). The cached exception keeps
        // the failure path allocation-free.
        if (_device.IsLost)
        {
            _state = SwapchainState.Poisoned;
            ResultExtensions.ThrowDeviceLost();
        }

        // Minimize check BEFORE the drain (#110): a minimized window must
        // not pay a vkDeviceWaitIdle per retry, and nothing may be
        // destroyed — the existing swapchain/views/semaphores stay intact
        // for the next attempt. CreateOrRecreate re-checks post-drain in
        // case the window minimizes while the drain blocks.
        VkSurfaceCapabilitiesKHR caps = default;
        Vk.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(
            _device.PhysicalDevice.Handle, _surface.Handle, &caps).ThrowIfFailed();
        if (IsZeroExtent(ComputeExtent(in caps, desc.Width, desc.Height)))
        {
            _state = SwapchainState.Minimized;
            return _state;
        }

        bool fullIdle = syncBeforeDestroy is null;
        if (fullIdle)
            Vk.vkDeviceWaitIdle(_device.Handle).ThrowIfFailed();
        else
            syncBeforeDestroy!();
        // A full wait-idle proves present completion device-wide — the
        // point at which previously retired handles/semaphores (#111) are
        // safe to destroy.
        if (fullIdle) FlushRetired();

        VkSwapchainKHR_T* old      = _handle;
        BinarySemaphore[] oldSems  = _renderingDone;
        DestroyViews();
        try
        {
            _state = CreateOrRecreate(in desc, oldSwapchain: old);
        }
        catch
        {
            // CreateOrRecreate assigns _handle before LoadImagesAndViews;
            // a throw from the image/view step would otherwise orphan the
            // freshly created swapchain. Destroying it immediately is
            // legal — none of its images were acquired or presented. (Its
            // fresh RenderingDone semaphores stay tracked by
            // _semaphorePool and are recovered at Dispose.)
            VkSwapchainKHR_T* created = _handle;
            if (created != null && created != old)
                Vk.vkDestroySwapchainKHR(_device.Handle, created, null);
            // Passing oldSwapchain retires it even when creation fails
            // (#112): the object must not keep referencing the retired
            // handle, and a retry must not pass it as oldSwapchain again.
            _handle        = null;
            _renderingDone = [];
            RetireOrDestroy(old, oldSems, fullIdle);
            _state = SwapchainState.Poisoned;
            throw;
        }

        if (_state == SwapchainState.Minimized)
        {
            // Window minimized during the drain; CreateOrRecreate returned
            // before creating or touching anything — the old handle and
            // _renderingDone (== oldSems) remain current. Views were
            // destroyed above; the next successful Recreate rebuilds them
            // and the ThrowIfNotPresentable guard covers the gap.
            return _state;
        }

        RetireOrDestroy(old, oldSems, fullIdle);
        return _state;
    }

    /// <summary>
    /// Destroy the retired swapchain + its per-image semaphores when
    /// device-wide completion is proven (<paramref name="provenIdle"/>);
    /// otherwise park them on the retire list (#111 — a fence drain does
    /// not cover the presentation engine's semaphore waits or pending
    /// presents from the old swapchain).
    /// </summary>
    private void RetireOrDestroy(VkSwapchainKHR_T* old, BinarySemaphore[] sems, bool provenIdle)
    {
        if (old == null && sems.Length == 0) return;
        if (provenIdle)
        {
            for (int i = 0; i < sems.Length; i++) _semaphorePool.Discard(sems[i]);
            if (old != null) Vk.vkDestroySwapchainKHR(_device.Handle, old, null);
        }
        else
        {
            _retired.Add(((nint)old, sems));
        }
    }

    private void FlushRetired()
    {
        for (int i = 0; i < _retired.Count; i++)
        {
            (nint old, BinarySemaphore[] sems) = _retired[i];
            for (int s = 0; s < sems.Length; s++) _semaphorePool.Discard(sems[s]);
            if (old != 0) Vk.vkDestroySwapchainKHR(_device.Handle, (VkSwapchainKHR_T*)old, null);
        }
        _retired.Clear();
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
        ThrowIfNotPresentable();
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
        return MapPresentationResult(r, "vkAcquireNextImageKHR", fromAcquire: true);
    }

    /// <summary>
    /// Shared result mapping + state transitions for the acquire/present
    /// pair (#120): <c>OutOfDate</c> → <see cref="SwapchainState.NeedsRecreate"/>,
    /// <c>SurfaceLost</c> → <see cref="SwapchainState.Poisoned"/> (a
    /// same-surface <see cref="Recreate"/> cannot succeed),
    /// <c>DEVICE_LOST</c> → mark the device + poison, then throw.
    /// <c>Suboptimal</c> deliberately leaves the state untouched — the
    /// image is presentable per spec; recreating stays the caller's choice.
    /// <c>TIMEOUT</c>/<c>NOT_READY</c> are in <c>vkAcquireNextImageKHR</c>'s
    /// result set only — a present returning them is a broken ICD and
    /// throws instead of mapping to a benign retry.
    /// </summary>
    private AcquireResult MapPresentationResult(VkResult r, string fn, bool fromAcquire)
    {
        switch (r)
        {
            case VkResult.VK_SUCCESS:
                return AcquireResult.Success;
            case VkResult.VK_SUBOPTIMAL_KHR:
                return AcquireResult.Suboptimal;
            case VkResult.VK_ERROR_OUT_OF_DATE_KHR:
                _state = SwapchainState.NeedsRecreate;
                return AcquireResult.OutOfDate;
            case VkResult.VK_ERROR_SURFACE_LOST_KHR:
                _state = SwapchainState.Poisoned;
                return AcquireResult.SurfaceLost;
            case VkResult.VK_TIMEOUT when fromAcquire:
                return AcquireResult.Timeout;
            case VkResult.VK_NOT_READY when fromAcquire:
                return AcquireResult.NotReady;
            case VkResult.VK_ERROR_DEVICE_LOST:
                _device.MarkLost();
                _state = SwapchainState.Poisoned;
                throw new VulkanException(r,
                    $"{fn}: VK_ERROR_DEVICE_LOST. The VkDevice is no longer usable; tear down and recreate the device + every dependent resource.");
            default:
                throw new VulkanException(r, fn);
        }
    }

    // Guards the "loop forever re-acquiring a dead swapchain" failure mode
    // at the API boundary: in Minimized there is no usable swapchain for
    // this frame; in Poisoned no handle is held at all. NeedsRecreate stays
    // advisory — acquire/present remain legal and keep reporting OutOfDate.
    private void ThrowIfNotPresentable()
    {
        if (_state is SwapchainState.Minimized or SwapchainState.Poisoned)
        {
            throw new InvalidOperationException(
                _state == SwapchainState.Minimized
                    ? "Swapchain is in the Minimized state (zero-extent surface). Skip rendering, poll the window size, and call Recreate when it is restored."
                    : "Swapchain is in the Poisoned state (a Recreate failed, or the surface/device was lost). Call Recreate to attempt a from-scratch create, or Dispose.");
        }
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
        ThrowIfNotPresentable();

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
        return MapPresentationResult(r, "vkQueuePresentKHR", fromAcquire: false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        DestroyViews();
        DiscardRenderingDoneSemaphores();
        // Retired handles/semaphores parked by fence-callback Recreates
        // (#111) are destroyed here: Dispose's documented contract is
        // "call after the device is idle", which proves present completion.
        FlushRetired();
        if (_handle != null)
        {
            Vk.vkDestroySwapchainKHR(_device.Handle, _handle, null);
            _handle = null;
        }
        _semaphorePool.Dispose();
    }

    // Test seam (InternalsVisibleTo): drive the Minimized/Poisoned guard
    // paths without a real window-manager event or a failing driver call.
    internal void OverrideStateForTesting(SwapchainState state) => _state = state;

    private SwapchainState CreateOrRecreate(in SwapchainDescription desc, VkSwapchainKHR_T* oldSwapchain)
    {
        VkPhysicalDevice_T* gpu = _device.PhysicalDevice.Handle;

        // ---- Caps ----
        VkSurfaceCapabilitiesKHR caps = default;
        Vk.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(gpu, _surface.Handle, &caps).ThrowIfFailed();

        // ---- Extent ----
        // Checked before anything is created: a zero extent (minimized
        // window, #110) would violate VUID-VkSwapchainCreateInfoKHR-
        // imageExtent-01689. Recreate already checked pre-drain; this
        // re-check covers a minimize that lands while the drain blocks.
        VkExtent2D extent = ComputeExtent(in caps, desc.Width, desc.Height);
        if (IsZeroExtent(extent))
            return SwapchainState.Minimized;
        _extent = extent;

        // ---- Format ----
        _format = NegotiateFormat(gpu, in desc);

        // ---- Present mode ----
        _presentMode = NegotiatePresentMode(gpu, in desc);

        // ---- Image count ----
        uint count = ComputeImageCount(in caps, desc.PreferredImageCount);

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
                compositeAlpha        = PickCompositeAlpha(caps.supportedCompositeAlpha),
                presentMode           = _presentMode,
                clipped               = 1,
                oldSwapchain          = oldSwapchain,
            };
            VkSwapchainKHR_T* raw = null;
            Vk.vkCreateSwapchainKHR(_device.Handle, &ci, null, &raw).ThrowIfFailed();
            _handle = raw;
        }

        LoadImagesAndViews();
        return SwapchainState.Ready;
    }

    /// <summary>
    /// Surface extent for the next create. When the surface pins
    /// <c>currentExtent</c> (anything but the <c>0xFFFFFFFF</c>
    /// "window-manager decides" sentinel) that wins verbatim — including
    /// the <c>(0, 0)</c> a minimized Windows window reports. In the
    /// sentinel branch the caller's size is clamped to the caps range,
    /// which can also legitimately produce zero when
    /// <c>maxImageExtent</c> is <c>(0, 0)</c>. Callers must treat a zero
    /// result as <see cref="SwapchainState.Minimized"/> (#110) — it is
    /// never a valid <c>imageExtent</c>.
    /// </summary>
    internal static VkExtent2D ComputeExtent(in VkSurfaceCapabilitiesKHR caps, uint descWidth, uint descHeight)
    {
        return caps.currentExtent.width != ~0u
            ? caps.currentExtent
            : new VkExtent2D
            {
                width  = Math.Clamp(descWidth  == 0 ? 1u : descWidth,
                                    caps.minImageExtent.width,  caps.maxImageExtent.width),
                height = Math.Clamp(descHeight == 0 ? 1u : descHeight,
                                    caps.minImageExtent.height, caps.maxImageExtent.height),
            };
    }

    internal static bool IsZeroExtent(VkExtent2D extent)
        => extent.width == 0 || extent.height == 0;

    /// <summary>
    /// Image count clamped to the caps range. <c>maxImageCount == 0</c>
    /// means "no limit" and maps to <see cref="uint.MaxValue"/> — the old
    /// requested-as-max mapping made <c>Math.Clamp</c> throw whenever the
    /// caller preferred fewer images than <c>minImageCount</c> on an
    /// unlimited surface (#104); the documented behavior is to clamp up.
    /// </summary>
    internal static uint ComputeImageCount(in VkSurfaceCapabilitiesKHR caps, uint preferredImageCount)
    {
        uint requested = preferredImageCount == 0 ? caps.minImageCount + 1u : preferredImageCount;
        uint maxClamp  = caps.maxImageCount == 0 ? uint.MaxValue : caps.maxImageCount;
        return Math.Clamp(requested, caps.minImageCount, maxClamp);
    }

    /// <summary>
    /// Prefer <c>OPAQUE</c> when the surface supports it; otherwise fall
    /// back to the lowest set bit of <c>supportedCompositeAlpha</c> (the
    /// spec guarantees at least one). The old hard-coded <c>OPAQUE</c>
    /// violated VUID-VkSwapchainCreateInfoKHR-compositeAlpha-01280 on
    /// compositors that only expose <c>PRE_MULTIPLIED</c>/<c>INHERIT</c>
    /// (#110 — latent on Windows, real on Wayland/Android).
    /// </summary>
    internal static VkCompositeAlphaFlagBitsKHR PickCompositeAlpha(uint supportedCompositeAlpha)
    {
        const uint opaque = (uint)VkCompositeAlphaFlagBitsKHR.VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR;
        if ((supportedCompositeAlpha & opaque) != 0 || supportedCompositeAlpha == 0)
            return VkCompositeAlphaFlagBitsKHR.VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR;
        return (VkCompositeAlphaFlagBitsKHR)(supportedCompositeAlpha & ~(supportedCompositeAlpha - 1));
    }

    private void LoadImagesAndViews()
    {
        uint imageCount = 0;
        Vk.vkGetSwapchainImagesKHR(_device.Handle, _handle, &imageCount, null).ThrowIfErrored();

        _images = new VkImage_T*[imageCount];
        _views  = new ImageView[imageCount];
        // Allocate fresh per-image RenderingDone semaphores for the new
        // swapchain. The prior swapchain's array is NOT discarded here:
        // Recreate captured it before calling in and decides between
        // immediate destroy (full wait-idle drain) and the retire list
        // (fence-callback drain, #111 — the presentation engine may still
        // hold one of the old semaphores).
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
        // FIFO is required by the spec — if that's the request (the
        // valid-by-default value, see SwapchainDescription / issue #105), ship
        // it without an extra round-trip query. Note: we deliberately do NOT
        // treat the zero enum value as "unset" here. The description's field
        // initializer already makes FIFO the default, so a present mode of 0
        // means the caller explicitly asked for VK_PRESENT_MODE_IMMEDIATE_KHR
        // and we honour it below.
        if (desc.PreferredPresentMode == VkPresentModeKHR.VK_PRESENT_MODE_FIFO_KHR)
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
