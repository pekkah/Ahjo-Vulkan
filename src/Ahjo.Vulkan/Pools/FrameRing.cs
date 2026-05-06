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
            _slots[i]    = new Slot(device, queueFamily, stagingChunkSize, descriptorPoolSizes, descriptorMaxSets);
            _contexts[i] = new FrameContext(_slots[i], i);
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
        _nextSlot    = (slotIdx + 1) % (uint)_slots.Length;

        Slot slot = _slots[slotIdx];
        slot.WaitAndReset();

        FrameContext fc = _contexts[slotIdx];
        fc.FrameNumber = ++_frameCounter;
        return fc;
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
        public  readonly BinarySemaphore   ImageAcquired;
        public  readonly BinarySemaphore   RenderingDone;
        public  readonly Fence             InFlightHandle;
        public  readonly DescriptorSetPool? DescriptorSets;
        private          bool              _everSubmitted;

        public Slot(
            Device device,
            uint   queueFamily,
            ulong  stagingChunkSize,
            ReadOnlySpan<VkDescriptorPoolSize> descriptorPoolSizes,
            uint   descriptorMaxSets)
        {
            _device         = device;
            CommandBuffers  = new CommandBufferPool(device, queueFamily);
            Staging         = new StagingUploader(device.Allocator, stagingChunkSize);
            SemaphorePool   = new SemaphorePool(device);
            FencePool       = new FencePool(device);
            ImageAcquired   = SemaphorePool.AcquireBinary();
            RenderingDone   = SemaphorePool.AcquireBinary();
            InFlightHandle  = FencePool.Acquire(initiallySignaled: true);
            DescriptorSets  = descriptorPoolSizes.IsEmpty
                ? null
                : new DescriptorSetPool(device, descriptorMaxSets, descriptorPoolSizes);
        }

        public ref readonly Fence InFlight => ref InFlightHandle;

        public void MarkSubmitted() => _everSubmitted = true;

        /// <summary>
        /// Block on the in-flight fence (skip on a slot that's never been
        /// submitted to — though our fence is initially signaled either
        /// way, that early-out keeps the wait stack out of profilers
        /// during the first round through the ring), then reset the
        /// fence and downstream per-frame pools.
        /// </summary>
        public void WaitAndReset()
        {
            if (_everSubmitted)
            {
                if (InFlightHandle.Wait(Timeout.InfiniteTimeSpan) != WaitState.Signaled)
                    throw new VulkanException(VkResult.VK_TIMEOUT,
                        "FrameRing slot fence never signaled.");
            }
            InFlightHandle.Reset();
            CommandBuffers.ResetForFrame();
            Staging.Reset();
            DescriptorSets?.Reset();
        }

        public void Dispose()
        {
            // Block on outstanding GPU work before tearing down the pools
            // so a teardown immediately after a Submit doesn't trip
            // VK_ERROR_DEVICE_LOST or similar on the validation layer.
            if (_everSubmitted)
                InFlightHandle.Wait(Timeout.InfiniteTimeSpan);

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
