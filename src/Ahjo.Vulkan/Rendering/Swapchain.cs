using System.Runtime.CompilerServices;
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
    /// <see cref="ImageBarrier.Image"/> in its object-initializer form.
    /// Use <see cref="GetImage"/> instead when you need an
    /// <see cref="Image"/> — for <c>ImageBarrier.Transition</c>, for the
    /// <c>WholeImage</c> region helpers, or for anything that reads the
    /// extent, format or usage.
    /// </summary>
    /// <remarks>
    /// The guards run before the index, matching <see cref="Present(Queue, uint)"/>
    /// (#224): in the states that reject a present the image array is empty
    /// (<see cref="Recreate"/>'s failure path clears it; a construction-time
    /// <see cref="SwapchainState.Minimized"/> never populates it), so indexing
    /// first would throw <see cref="IndexOutOfRangeException"/> — and on a disposed
    /// swapchain silently index a cleared array — instead of the exceptions this
    /// boundary documents. The trailing bounds check mirrors the sibling
    /// <see cref="GetImage"/>, so the two accessors now agree on the index
    /// contract. The three checks are predictable, allocation-free branches on the
    /// per-frame path; <see cref="SwapchainState.NeedsRecreate"/> stays presentable,
    /// so a frame loop mid-resize is unaffected.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The swapchain has been disposed.</exception>
    /// <exception cref="InvalidOperationException">
    /// The swapchain is not presentable — see <see cref="SwapchainState"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is not less than <see cref="ImageCount"/>.
    /// </exception>
    public nint GetImageHandle(uint index)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfNotPresentable();
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, (uint)_images.Length);
        return (nint)_images[index];
    }

    /// <summary>
    /// Swapchain image <paramref name="index"/> as a <b>borrowed</b>
    /// <see cref="Image"/> carrying the four facts the swapchain actually
    /// knows: <see cref="Format"/>, <see cref="Extent"/> (as
    /// <c>Width</c>/<c>Height</c>) and <see cref="ImageUsage"/>, plus
    /// <c>Depth</c>/<c>MipLevels</c>/<c>ArrayLayers</c> of <c>1</c>
    /// (a swapchain image is 2-D and single-mip, and
    /// <c>imageArrayLayers</c> is <c>1</c> at creation).
    /// </summary>
    /// <remarks>
    /// <para><b>The returned image is borrowed.</b> It is built with no VMA
    /// allocation and no owning <see cref="Allocator"/>, so
    /// <see cref="Image.OwnsHandle"/> and <see cref="Image.OwnsMemory"/> are
    /// both <see langword="false"/> and <see cref="Image.Dispose"/> is a
    /// <b>no-op</b>: it returns before <c>HandleRegistry.TrackDispose</c> and
    /// before <c>vmaDestroyImage</c>, so a swapchain-owned <c>VkImage</c> can
    /// never reach VMA. <c>using var image = swap.GetImage(i);</c> is
    /// therefore harmless — and pointless. Don't write it.</para>
    /// <para><b>It is never registered with <c>HandleRegistry</c>.</b>
    /// <c>TrackCreate</c> returns on its first branch for a non-owning
    /// handle, so calling this once per frame costs two predictable branches,
    /// churns nothing, and cannot produce a false double-dispose report.</para>
    /// <para><b>Lifetime.</b> Valid only while this swapchain is alive and
    /// un-recreated. <see cref="Recreate"/> replaces the images and may change
    /// the extent and the format, so never cache the returned value across a
    /// recreate — the same contract <see cref="ImageViews"/> carries.</para>
    /// <para><b>Which one to use.</b> <see cref="GetImageHandle"/> returns the
    /// raw <c>nint</c> that <see cref="ImageBarrier.Image"/> takes in its
    /// object-initializer form. <c>GetImage</c> is what you want for
    /// <c>ImageBarrier.Transition</c>, for <c>ImageBlitRegion.WholeImage</c> /
    /// <c>BufferImageCopy.WholeImage</c>, and for anything that reads the
    /// extent, format or usage: <see cref="Image.FromRaw"/> deliberately
    /// reports <c>0×0</c>, <c>VK_FORMAT_UNDEFINED</c> and
    /// <c>ImageUsage.None</c> for a bare handle (issue #119 — unknown, not
    /// wrong), and a <c>WholeImage</c> region built over one of those is a
    /// degenerate box that copies nothing. This method exists to supply what
    /// the swapchain genuinely knows.</para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is not less than <see cref="ImageCount"/>.
    /// </exception>
    public Image GetImage(uint index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, (uint)_images.Length);
        // Constructed on demand rather than cached: eleven field assignments
        // that allocate nothing, against a second array Recreate would have to
        // keep in sync. allocation/owner stay null so the borrow contract of
        // the remarks above holds by construction, not by a flag.
        return new Image(
            _images[index],
            allocation:       null,
            owner:            default,
            format:           _format.format,
            width:            _extent.width,
            height:           _extent.height,
            depth:            1,
            mipLevels:        1,
            arrayLayers:      1,
            usage:            _imageUsage,
            persistentMapped: null);
    }

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
    /// <para>The presentability guard runs before the index (#224), mirroring
    /// <see cref="Present(Queue, uint)"/>: in the states that reject a present the
    /// per-image semaphore array is empty, so indexing first would throw
    /// <see cref="IndexOutOfRangeException"/> instead of the
    /// <see cref="InvalidOperationException"/> this boundary documents.
    /// <see cref="SwapchainState.NeedsRecreate"/> stays presentable.</para>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The swapchain has been disposed.</exception>
    /// <exception cref="InvalidOperationException">
    /// The swapchain is not presentable — see <see cref="SwapchainState"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="imageIndex"/> is not less than <see cref="ImageCount"/>.
    /// </exception>
    public BinarySemaphore GetRenderingDoneFor(uint imageIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfNotPresentable();
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(imageIndex, (uint)_renderingDone.Length);
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
    /// window is restored). A failure rethrows after setting the state its
    /// <see cref="VkResult"/> implies: <see cref="SwapchainState.SurfaceLost"/>
    /// for <c>VK_ERROR_SURFACE_LOST_KHR</c> and
    /// <see cref="SwapchainState.DeviceLost"/> for <c>VK_ERROR_DEVICE_LOST</c>
    /// — both documented return codes of the surface queries and the create
    /// this makes, and both terminal — otherwise
    /// <see cref="SwapchainState.RecreateFailed"/>, from which calling
    /// <c>Recreate</c> again is the documented recovery. A failure that lands
    /// before anything is destroyed — the capability query or the drain —
    /// leaves the state untouched outside those two terminal codes, and the
    /// swapchain, its views and its semaphores all stay current.
    /// Entering <c>Recreate</c> on a swapchain already in
    /// <see cref="SwapchainState.SurfaceLost"/>, or with
    /// <see cref="Device.IsLost"/> set, throws <see cref="VulkanException"/>
    /// without changing the state — both are terminal (#222).
    /// </returns>
    /// <exception cref="VulkanException">
    /// The device is lost (<c>VK_ERROR_DEVICE_LOST</c>), the swapchain is
    /// already in <see cref="SwapchainState.SurfaceLost"/>
    /// (<c>VK_ERROR_SURFACE_LOST_KHR</c>), or the create itself failed.
    /// </exception>
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
            _state = SwapchainState.DeviceLost;
            ResultExtensions.ThrowDeviceLost();
        }

        // A lost surface is terminal for this Swapchain (#222). Recreate
        // accepts only the Surface this object was constructed with (see the
        // ArgumentException above), so the only surface it would retry over is
        // the dead one. Fail fast with the driver's own verdict instead of
        // letting vkGetPhysicalDeviceSurfaceCapabilitiesKHR decide per-driver
        // whether this spins or throws. Recovery is a new Surface + a new
        // Swapchain; Dispose stays legal. Checked AFTER device loss, which is
        // the wider failure. _state is left as SurfaceLost — the call
        // changes nothing.
        if (_state == SwapchainState.SurfaceLost)
            ResultExtensions.ThrowSurfaceLost();

        // Minimize check BEFORE the drain (#110): a minimized window must
        // not pay a vkDeviceWaitIdle per retry, and nothing may be
        // destroyed — the existing swapchain/views/semaphores stay intact
        // for the next attempt. CreateOrRecreate re-checks post-drain in
        // case the window minimizes while the drain blocks.
        VkSurfaceCapabilitiesKHR caps = default;
        try
        {
            Vk.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(
                _device.PhysicalDevice.Handle, _surface.Handle, &caps).ThrowIfFailed();
        }
        catch (Exception e)
        {
            // This query is the most likely place a real surface loss is first
            // observed (#222): driver restart → OUT_OF_DATE from present →
            // caller calls Recreate → caps query returns
            // VK_ERROR_SURFACE_LOST_KHR. Classify on the driver's verdict so it
            // lands in the terminal state and the fast-fail above can fire on
            // the next call. Nothing has been destroyed at this point, so a
            // non-terminal failure leaves the state exactly as it was — the
            // swapchain, its views and its semaphores are all still current.
            _state = ClassifyFailure(e, fallback: _state);
            throw;
        }
        if (IsZeroExtent(ComputeExtent(in caps, desc.Width, desc.Height)))
        {
            _state = SwapchainState.Minimized;
            return _state;
        }

        bool fullIdle = syncBeforeDestroy is null;
        try
        {
            if (fullIdle)
                Vk.vkDeviceWaitIdle(_device.Handle).ThrowIfFailed();
            else
                syncBeforeDestroy!();
        }
        catch (Exception e)
        {
            // The drain is the one place a device loss can be observed that
            // neither fast-fail above nor the create below would catch:
            // vkDeviceWaitIdle documents VK_ERROR_DEVICE_LOST, and the
            // caller's callback — FrameRing.WaitForInFlightFences and its
            // equivalents — throws the same code out of the fence wait.
            // Without this the state would stay Ready/NeedsRecreate after a
            // real loss and the next AcquireNextImage would issue
            // vkAcquireNextImageKHR against a dead device. Nothing has been
            // destroyed here either, so the fallback is the caps query's:
            // leave the state alone unless the driver named a terminal cause.
            // Neither call can return VK_ERROR_SURFACE_LOST_KHR, but the
            // classifier is shared rather than special-cased — a callback is
            // caller code and may surface anything.
            _state = ClassifyFailure(e, fallback: _state);
            throw;
        }
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
        catch (Exception e)
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
            // RecreateFailed is the *fallback*, not the verdict: every surface
            // query CreateOrRecreate makes — caps, formats, present modes,
            // surface support — and vkCreateSwapchainKHR itself all document
            // VK_ERROR_SURFACE_LOST_KHR as a return code, and filing those
            // under the retryable RecreateFailed would make the terminal
            // states unreachable from here (#222).
            _state = ClassifyFailure(e, fallback: SwapchainState.RecreateFailed);
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
    /// The lifecycle state a throw out of a surface query or a swapchain
    /// create implies (#222). Classifies on the driver's
    /// <see cref="VkResult"/>, never on "something threw":
    /// <c>VK_ERROR_SURFACE_LOST_KHR</c> and <c>VK_ERROR_DEVICE_LOST</c> are
    /// terminal wherever they are observed, and both are documented return
    /// codes of every call <see cref="Recreate"/> makes —
    /// <c>vkGetPhysicalDeviceSurface{Capabilities,Formats,PresentModes,Support}KHR</c>
    /// and <c>vkCreateSwapchainKHR</c>. Anything else (and any non-Vulkan
    /// exception) gets <paramref name="fallback"/>, which is what a
    /// non-terminal failure means at that particular call site: pass
    /// <see cref="SwapchainState.RecreateFailed"/> once the old swapchain has
    /// been retired, and the current state to leave it alone before that.
    /// </summary>
    /// <remarks>
    /// <c>internal</c> rather than <c>private</c> so the mapping is directly
    /// testable: a real <c>VK_ERROR_SURFACE_LOST_KHR</c> out of a caps query
    /// cannot be provoked from CI, and asserting the classifier is a more
    /// honest substitute than a test that only proves the fallback.
    /// </remarks>
    internal static SwapchainState ClassifyFailure(Exception e, SwapchainState fallback)
        => (e as VulkanException)?.Result switch
        {
            VkResult.VK_ERROR_SURFACE_LOST_KHR => SwapchainState.SurfaceLost,
            VkResult.VK_ERROR_DEVICE_LOST      => SwapchainState.DeviceLost,
            _                                  => fallback,
        };

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
    /// <remarks>
    /// All six <see cref="AcquireResult"/> members are reachable from here.
    /// The handling each one obliges the caller to — the state the swapchain is
    /// left in, and whether a bare <c>continue</c> is safe — is tabulated in the
    /// remarks on <see cref="AcquireResult"/> and is not repeated per call site
    /// (#220).
    /// </remarks>
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
    /// <c>SurfaceLost</c> → <see cref="SwapchainState.SurfaceLost"/> (a
    /// same-surface <see cref="Recreate"/> cannot succeed, and after #222 it
    /// says so by throwing),
    /// <c>DEVICE_LOST</c> → mark the device +
    /// <see cref="SwapchainState.DeviceLost"/>, then throw.
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
                _state = SwapchainState.SurfaceLost;
                return AcquireResult.SurfaceLost;
            case VkResult.VK_TIMEOUT when fromAcquire:
                return AcquireResult.Timeout;
            case VkResult.VK_NOT_READY when fromAcquire:
                return AcquireResult.NotReady;
            case VkResult.VK_ERROR_DEVICE_LOST:
                _device.MarkLost();
                _state = SwapchainState.DeviceLost;
                throw new VulkanException(r,
                    $"{fn}: VK_ERROR_DEVICE_LOST. The VkDevice is no longer usable; tear down and recreate the device + every dependent resource.");
            default:
                throw new VulkanException(r, fn);
        }
    }

    // Guards the "loop forever re-acquiring a dead swapchain" failure mode at
    // the API boundary. Written as "not (Ready or NeedsRecreate)" rather than a
    // positive list of bad states (#222): a state added later is
    // non-presentable until someone proves otherwise. NeedsRecreate stays
    // advisory — acquire/present remain legal and keep reporting OutOfDate.
    // Each rejected state gets its own message: their recoveries differ, and
    // none of them can assume a live handle (whether one is held depends on
    // where the state was entered, not on which state it is).
    private void ThrowIfNotPresentable()
    {
        if (_state is not (SwapchainState.Ready or SwapchainState.NeedsRecreate))
            ThrowNotPresentable(_state);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowNotPresentable(SwapchainState state)
        => throw new InvalidOperationException(state switch
        {
            SwapchainState.Minimized =>
                "Swapchain is in the Minimized state (zero-extent surface). Skip rendering, poll the window size, and call Recreate when it is restored.",
            SwapchainState.RecreateFailed =>
                "Swapchain is in the RecreateFailed state: a Recreate threw after the old swapchain was retired, so no swapchain handle is held. Call Recreate again to attempt a from-scratch create, or Dispose.",
            SwapchainState.SurfaceLost =>
                "Swapchain is in the SurfaceLost state: VK_ERROR_SURFACE_LOST_KHR was reported for the VkSurfaceKHR this swapchain was built over. This is terminal — Recreate over the same surface cannot succeed. Dispose this swapchain, rebuild the Surface, and construct a new Swapchain.",
            SwapchainState.DeviceLost =>
                "Swapchain is in the DeviceLost state: the VkDevice was lost. This is terminal — dispose every dependent resource, dispose the Device, and rebuild from a fresh PhysicalDevice.",
            // Unreachable from the guard above, which rejects only the four
            // states named here. Reachable if a member is ever added to
            // SwapchainState without updating this switch.
            _ => $"Swapchain is not presentable (state: {state}).",
        });

    /// <summary>
    /// Presents <paramref name="imageIndex"/> on <paramref name="queue"/>,
    /// waiting on this image's per-image <c>RenderingDone</c> semaphore
    /// (the one returned by <see cref="GetRenderingDoneFor"/>). The
    /// matching submit must have signaled the same semaphore — see the
    /// swapchain-aware
    /// <see cref="FrameContext.Submit(Queue, ref CommandRecorder, Swapchain, uint, Stage, Stage)"/>.
    /// </summary>
    /// <remarks>
    /// Forwards to
    /// <see cref="Present(Queue, uint, in BinarySemaphore)"/>; the result set a
    /// present can report is documented there.
    /// </remarks>
    public AcquireResult Present(Queue queue, uint imageIndex)
    {
        // The guards run here, before the index — in the states that reject a
        // present the per-image semaphore array is empty (Recreate's failure
        // path clears it; a construction-time Minimized never allocates it), so
        // indexing first would throw IndexOutOfRangeException instead of the
        // InvalidOperationException this API documents. The forwarded call
        // repeats all three checks: three predictable, allocation-free
        // branches against a wrong exception type on the real failure shape.
        ArgumentNullException.ThrowIfNull(queue);
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfNotPresentable();
        return Present(queue, imageIndex, in _renderingDone[imageIndex]);
    }

    /// <summary>
    /// Presents <paramref name="imageIndex"/> on <paramref name="queue"/>
    /// after <paramref name="waitSemaphore"/> fires (the
    /// rendering-done semaphore from the matching submit). Returns the
    /// same <see cref="AcquireResult"/> shape as
    /// <see cref="AcquireNextImage"/> so the caller's "did the surface
    /// change?" branch is symmetric with the acquire path.
    /// </summary>
    /// <remarks>
    /// <para>Most callers should use the no-semaphore
    /// <see cref="Present(Queue, uint)"/> overload, which pulls the
    /// per-image semaphore from the swapchain. This explicit overload
    /// stays for the rare case where a caller wants to drive a custom
    /// signal/wait pair (multi-swapchain bridging, headless capture
    /// hooks, etc.).</para>
    /// <para>The shape is symmetric with <see cref="AcquireNextImage"/> but the
    /// result set is not: a present can report only
    /// <see cref="AcquireResult.Success"/>,
    /// <see cref="AcquireResult.Suboptimal"/>,
    /// <see cref="AcquireResult.OutOfDate"/> and
    /// <see cref="AcquireResult.SurfaceLost"/> — four of the six members.
    /// <c>VK_TIMEOUT</c> and <c>VK_NOT_READY</c> belong to
    /// <c>vkAcquireNextImageKHR</c>'s result set only, so a present returning
    /// either is treated as a broken ICD and throws
    /// <see cref="VulkanException"/> rather than mapping to a benign retry.
    /// There is deliberately no benign catch-all on this side (#220).</para>
    /// <para><c>VK_ERROR_DEVICE_LOST</c> is outside
    /// <see cref="AcquireResult"/> entirely, on this path and on the acquire
    /// path alike: the device is marked lost, the swapchain moves to
    /// <see cref="SwapchainState.DeviceLost"/>, and a
    /// <see cref="VulkanException"/> is thrown.</para>
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

    // Test seam (InternalsVisibleTo): drive the non-presentable guard paths
    // without a real window-manager event, a failing driver call, or a lost
    // surface/device.
    internal void OverrideStateForTesting(SwapchainState state) => _state = state;

    // Test seam (InternalsVisibleTo): reproduce the *shape* Recreate's failure
    // path leaves behind, not just its state word — handle destroyed and
    // nulled, per-image semaphores discarded, image/view arrays emptied
    // (#112/#222). OverrideStateForTesting alone flips `_state` and leaves a
    // live handle behind, so a test written on it exercises an ordinary
    // recreate with a real oldSwapchain rather than the from-scratch create
    // that RecreateFailed actually documents. Waits for idle first: the
    // handle being destroyed may still have presents in flight, which is
    // exactly what the fullIdle branch of RetireOrDestroy proves.
    internal void ForceRecreateFailedForTesting()
    {
        Vk.vkDeviceWaitIdle(_device.Handle).ThrowIfFailed();
        DestroyViews();
        DiscardRenderingDoneSemaphores();
        FlushRetired();
        if (_handle != null)
        {
            Vk.vkDestroySwapchainKHR(_device.Handle, _handle, null);
            _handle = null;
        }
        _state = SwapchainState.RecreateFailed;
    }

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
