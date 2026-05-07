using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Frames-in-flight ring. Owns N <see cref="FrameContext"/> instances
/// (one per slot) plus the per-slot resources that the engine needs
/// fresh every frame: a <see cref="CommandBufferPool"/>, a
/// <see cref="StagingUploader"/>, two binary semaphores reserved for
/// swapchain handoff (#24), the in-flight <see cref="Fence"/> that
/// throttles the CPU when the GPU falls behind, and — when the ring is
/// configured with descriptor-pool sizes — a per-slot
/// <see cref="DescriptorSetPool"/> reset alongside the command pool.
/// </summary>
/// <remarks>
/// <para><b>Lifecycle.</b> Build once at engine startup with the queue
/// family the frame loop submits on. <see cref="BeginFrame"/> rotates
/// to the next slot, blocks on that slot's fence (initially signaled —
/// no first-frame deadlock), resets its command pool and staging head,
/// and hands back a <see cref="FrameContext"/>. The caller records,
/// calls <see cref="FrameContext.Submit"/>, and lets the
/// <c>using var frame = ring.BeginFrame()</c> scope close.</para>
/// <para><b>Allocation.</b> Slot resources are constructed in
/// <see cref="FrameRing(Device,uint,uint,ulong,ReadOnlySpan{VkDescriptorPoolSize},uint)"/>;
/// the steady-state per-frame path is index advance + fence wait +
/// pool resets and allocates 0 B.</para>
/// </remarks>
public sealed unsafe class FrameRing : IDisposable
{
    private readonly Device       _device;
    private readonly Slot[]       _slots;
    private readonly FrameContext[] _contexts;
    private uint                  _nextSlot;
    private ulong                 _frameCounter;
    private bool                  _disposed;

    /// <summary>
    /// Creates the ring. <paramref name="framesInFlight"/> typically 2 or
    /// 3 — higher values increase latency without raising throughput.
    /// Pass <paramref name="descriptorPoolSizes"/> + a non-zero
    /// <paramref name="descriptorMaxSets"/> to give every slot its own
    /// <see cref="DescriptorSetPool"/>; the pool is reset alongside the
    /// command pool at the start of each rotation, so descriptor sets
    /// allocated through <see cref="FrameContext.DescriptorSets"/> are
    /// valid for exactly one frame.
    /// </summary>
    public FrameRing(
        Device device,
        uint   framesInFlight,
        uint   queueFamily,
        ulong  stagingChunkSize     = StagingUploader.DefaultChunkSize,
        ReadOnlySpan<VkDescriptorPoolSize> descriptorPoolSizes = default,
        uint   descriptorMaxSets    = 0)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (framesInFlight == 0) throw new ArgumentOutOfRangeException(nameof(framesInFlight));
        if (!descriptorPoolSizes.IsEmpty && descriptorMaxSets == 0)
            throw new ArgumentOutOfRangeException(nameof(descriptorMaxSets),
                "descriptorMaxSets must be > 0 when descriptorPoolSizes is non-empty.");
        if (descriptorPoolSizes.IsEmpty && descriptorMaxSets != 0)
            throw new ArgumentException(
                "descriptorMaxSets is set but descriptorPoolSizes is empty — pass both or neither.",
                nameof(descriptorPoolSizes));

        _device   = device;
        _slots    = new Slot[framesInFlight];
        _contexts = new FrameContext[framesInFlight];

        for (uint i = 0; i < framesInFlight; i++)
        {
            try
            {
                _slots[i]    = new Slot(device, queueFamily, stagingChunkSize, descriptorPoolSizes, descriptorMaxSets);
                _contexts[i] = new FrameContext(_slots[i], i);
            }
            catch
            {
                // Roll back any earlier successfully-built slots so a later
                // failure doesn't leak the wrappers they own.
                for (uint j = 0; j < i; j++) _slots[j]?.Dispose();
                throw;
            }
        }
    }

    public uint  FramesInFlight => (uint)_slots.Length;
    public ulong FrameNumber    => _frameCounter;

    /// <summary>
    /// Advances to the next slot. Blocks on that slot's in-flight fence
    /// (signaled at construction so the first <see cref="BeginFrame"/>
    /// returns immediately), resets the slot's pools, and returns a
    /// <see cref="FrameContext"/> the caller drives for one frame.
    /// </summary>
    public FrameContext BeginFrame()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        uint slotIdx = _nextSlot;
        Slot slot    = _slots[slotIdx];
        // Wait+reset *before* advancing the index so a throw out of
        // WaitAndReset doesn't skip past the broken slot — the next call
        // will retry the same slot rather than silently rotating past
        // GPU work the caller still needs to recover from.
        slot.WaitAndReset();
        _nextSlot = (slotIdx + 1) % (uint)_slots.Length;

        FrameContext fc = _contexts[slotIdx];
        fc.FrameNumber = ++_frameCounter;
        return fc;
    }

    /// <summary>
    /// After a <see cref="Swapchain.Recreate"/>, replace any per-slot
    /// <see cref="FrameContext.ImageAcquired"/> binary semaphore that
    /// <c>vkAcquireNextImageKHR</c> host-signaled but no swapchain-aware
    /// submit consumed (typical
    /// <see cref="AcquireResult.Suboptimal"/> → bail-out path). Vulkan
    /// has no host-reset for binary semaphores, so the recovery is
    /// destroy + create — handled here via
    /// <see cref="SemaphorePool.Discard(BinarySemaphore)"/> +
    /// <see cref="SemaphorePool.AcquireBinary"/>. Slots with no pending
    /// host-signal are left untouched. Caller marks signals via
    /// <see cref="FrameContext.MarkImageAcquireSignaled"/>; the
    /// swapchain-aware <see cref="FrameContext.Submit(Queue, ref CommandRecorder, Stage, Stage)"/>
    /// clears the flag automatically.
    /// </summary>
    public void RecycleStaleAcquireSemaphores()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].AcquireSignalPending)
                _slots[i].RotateImageAcquired();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        for (int i = 0; i < _slots.Length; i++) _slots[i].Dispose();
    }

    /// <summary>
    /// Per-slot bag of resources. Internal; callers reach it through
    /// <see cref="FrameContext"/>.
    /// </summary>
    internal sealed class Slot : IDisposable
    {
        private readonly Device           _device;
        public  readonly CommandBufferPool CommandBuffers;
        public  readonly StagingUploader   Staging;
        public  readonly SemaphorePool     SemaphorePool;
        public  readonly FencePool         FencePool;
        // Mutable rather than readonly: RecycleStaleAcquireSemaphores
        // swaps the stuck handle out under FrameRing's control after a
        // post-OutOfDate Swapchain.Recreate where AcquireNextImage
        // signaled but no Submit consumed the signal. RenderingDone is
        // also signaled-then-waited but the wait is queued by Present
        // before vkDeviceWaitIdle drains, so it is never stuck.
        public           BinarySemaphore   ImageAcquired;
        public  readonly BinarySemaphore   RenderingDone;
        public  readonly Fence             InFlightHandle;
        public  readonly DescriptorSetPool? DescriptorSets;
        // True when the most recent AcquireNextImage host-signaled
        // ImageAcquired and no swapchain-aware Submit has queued a
        // wait that consumes it. Used by RecycleStaleAcquireSemaphores
        // to find slots whose ImageAcquired is stuck signaled after a
        // Swapchain.Recreate (Vulkan offers no host-reset for binary
        // semaphores, so the recovery is destroy + AcquireBinary
        // fresh — see Swapchain.Recreate's remarks).
        private          bool              _acquireSignalPending;
        // True when the slot has been submitted-to-the-queue since its
        // last WaitAndReset — i.e. the in-flight fence has pending GPU
        // work that will signal it. False on a fresh slot, after
        // WaitAndReset (fence was just reset to unsignaled), and after
        // Dispose's terminal wait. The previous "ever submitted" flag
        // was a different question — it stayed sticky-true after Reset,
        // which made Dispose try to wait on a freshly-reset fence and
        // deadlock when the user called BeginFrame without a matching
        // Submit before tearing the ring down.
        private          bool              _pendingSubmit;

        public Slot(
            Device device,
            uint   queueFamily,
            ulong  stagingChunkSize,
            ReadOnlySpan<VkDescriptorPoolSize> descriptorPoolSizes,
            uint   descriptorMaxSets)
        {
            _device = device;

            // Build into locals so a throw partway through can dispose the
            // wrappers we already created. `readonly` field assignment is
            // deferred to the end of the happy path.
            CommandBufferPool? cmdPool   = null;
            StagingUploader?   staging   = null;
            SemaphorePool?     semPool   = null;
            FencePool?         fencePool = null;
            BinarySemaphore    imgAcq    = default;
            BinarySemaphore    rendDone  = default;
            Fence              inFlight  = default;
            DescriptorSetPool? descSets  = null;
            bool committed = false;
            try
            {
                cmdPool   = new CommandBufferPool(device, queueFamily);
                staging   = new StagingUploader(device.Allocator, stagingChunkSize);
                semPool   = new SemaphorePool(device);
                fencePool = new FencePool(device);
                imgAcq    = semPool.AcquireBinary();
                rendDone  = semPool.AcquireBinary();
                inFlight  = fencePool.Acquire(initiallySignaled: true);
                descSets  = descriptorPoolSizes.IsEmpty
                    ? null
                    : new DescriptorSetPool(device, descriptorMaxSets, descriptorPoolSizes);

                CommandBuffers = cmdPool;
                Staging        = staging;
                SemaphorePool  = semPool;
                FencePool      = fencePool;
                ImageAcquired  = imgAcq;
                RenderingDone  = rendDone;
                InFlightHandle = inFlight;
                DescriptorSets = descSets;
                committed = true;
            }
            finally
            {
                if (!committed)
                {
                    // Reverse-order cleanup mirroring Dispose(): pool-borrowed
                    // handles return to their pools first, owned wrappers
                    // get disposed last.
                    descSets?.Dispose();
                    if (fencePool is not null && !inFlight.IsNull)
                        fencePool.Release(inFlight);
                    if (semPool is not null)
                    {
                        if (!rendDone.IsNull) semPool.Release(rendDone);
                        if (!imgAcq.IsNull)   semPool.Release(imgAcq);
                    }
                    staging?.Dispose();
                    cmdPool?.Dispose();
                    semPool?.Dispose();
                    fencePool?.Dispose();
                }
            }
        }

        public ref readonly Fence InFlight => ref InFlightHandle;

        public void MarkSubmitted()           => _pendingSubmit       = true;
        public void MarkAcquireSignaled()     => _acquireSignalPending = true;
        public void MarkAcquireWaitConsumed() => _acquireSignalPending = false;
        public bool AcquireSignalPending      => _acquireSignalPending;

        /// <summary>
        /// Replace this slot's <see cref="ImageAcquired"/> with a fresh
        /// binary semaphore from <see cref="SemaphorePool"/>. The old
        /// one is destroyed via <see cref="SemaphorePool.Discard(BinarySemaphore)"/>;
        /// the pending-acquire flag is cleared.
        /// </summary>
        public void RotateImageAcquired()
        {
            BinarySemaphore stale = ImageAcquired;
            // Acquire-before-discard so a throw out of AcquireBinary
            // leaves the slot still owning a valid (if stuck) handle
            // rather than a destroyed one.
            BinarySemaphore fresh = SemaphorePool.AcquireBinary();
            SemaphorePool.Discard(stale);
            ImageAcquired = fresh;
            _acquireSignalPending = false;
        }

        /// <summary>
        /// Block on the in-flight fence when there is a pending submit
        /// for it, then reset the fence and downstream per-frame pools.
        /// On a fresh slot the fence was created signaled and there is
        /// no pending submit, so the wait is skipped — keeping the wait
        /// stack out of profilers during the first round through the
        /// ring without resorting to a sticky "ever submitted" flag.
        /// </summary>
        public void WaitAndReset()
        {
            if (_pendingSubmit)
            {
                if (InFlightHandle.Wait(Timeout.InfiniteTimeSpan) != WaitState.Signaled)
                    throw new VulkanException(VkResult.VK_TIMEOUT,
                        "FrameRing slot fence never signaled.");
            }
            InFlightHandle.Reset();
            _pendingSubmit = false;
            CommandBuffers.ResetForFrame();
            Staging.Reset();
            DescriptorSets?.Reset();
        }

        public void Dispose()
        {
            // Block on outstanding GPU work before tearing down the pools
            // so a teardown immediately after a Submit doesn't trip
            // VK_ERROR_DEVICE_LOST or similar on the validation layer.
            // Critically, only wait when a submit is actually pending —
            // a slot whose fence was reset by BeginFrame but never
            // re-submitted has an unsignaled fence with no GPU work
            // behind it, and waiting on it would hang Dispose forever.
            if (_pendingSubmit)
                InFlightHandle.Wait(Timeout.InfiniteTimeSpan);
            _pendingSubmit = false;

            FencePool.Release(InFlightHandle);
            SemaphorePool.Release(ImageAcquired);
            SemaphorePool.Release(RenderingDone);
            DescriptorSets?.Dispose();
            Staging.Dispose();
            CommandBuffers.Dispose();
            SemaphorePool.Dispose();
            FencePool.Dispose();
        }
    }
}
