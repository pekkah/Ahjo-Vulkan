namespace Ahjo.Vulkan;

/// <summary>
/// Per-frame view onto a <see cref="FrameRing"/> slot. The ring hands one
/// of these out from <see cref="FrameRing.BeginFrame"/>; the same instance
/// rotates back into use every <c>FramesInFlight</c> frames. All
/// resources exposed here belong to the slot — <see cref="Dispose"/> is
/// a no-op (next <see cref="FrameRing.BeginFrame"/> will cycle this slot
/// again, waiting on <see cref="InFlight"/> first).
/// </summary>
/// <remarks>
/// Owner-class shape so the frame loop can hold a reference across
/// helper methods without ref-struct restrictions. Cheap to recycle —
/// the ring constructs <c>FramesInFlight</c> instances at start and
/// reuses them.
/// </remarks>
public sealed class FrameContext : IDisposable
{
    internal readonly FrameRing.Slot Slot;

    /// <summary>Zero-based index of this slot inside the ring.</summary>
    public uint SlotIndex { get; }

    /// <summary>Monotonically increasing frame counter — survives slot rotation.</summary>
    public ulong FrameNumber { get; internal set; }

    internal FrameContext(FrameRing.Slot slot, uint slotIndex)
    {
        Slot      = slot;
        SlotIndex = slotIndex;
    }

    /// <summary>Per-slot command-buffer pool. Reset at <see cref="FrameRing.BeginFrame"/>.</summary>
    public CommandBufferPool CommandBuffers => Slot.CommandBuffers;

    /// <summary>Per-slot staging uploader. Bumped to head 0 at <see cref="FrameRing.BeginFrame"/>.</summary>
    public StagingUploader Staging => Slot.Staging;

    /// <summary>
    /// Per-slot descriptor-set pool, or <c>null</c> when the ring was
    /// constructed without descriptor-pool sizes. <see cref="FrameRing.BeginFrame"/>
    /// calls <see cref="DescriptorSetPool.Reset"/> on rotation, so any
    /// <see cref="DescriptorSet"/> acquired here is valid for exactly one
    /// frame — re-acquire after every <see cref="FrameRing.BeginFrame"/>.
    /// </summary>
    public DescriptorSetPool? DescriptorSets => Slot.DescriptorSets;

    /// <summary>
    /// Binary semaphore the swapchain will signal on image acquire. Reserved
    /// for the swapchain integration in #24 — currently created and held by
    /// the slot but not yet wired into <see cref="Submit"/>.
    /// </summary>
    public BinarySemaphore ImageAcquired => Slot.ImageAcquired;

    /// <summary>
    /// Binary semaphore signaled at submit-completion. Reserved for the
    /// swapchain present integration in #24.
    /// </summary>
    public BinarySemaphore RenderingDone => Slot.RenderingDone;

    /// <summary>
    /// Per-slot fence signaled at submit-completion. The next time this
    /// slot rotates back through <see cref="FrameRing.BeginFrame"/>, the
    /// ring waits on this fence before reusing the slot's pools — that's
    /// the throttle that pins the CPU to "at most FramesInFlight ahead of
    /// the GPU".
    /// </summary>
    public Fence InFlight => Slot.InFlight;

    /// <summary>
    /// Submits <paramref name="recorder"/> on <paramref name="queue"/> and
    /// signals <see cref="InFlight"/>. Headless variant — does not wire
    /// the swapchain semaphores. Use the
    /// <see cref="Submit(Queue, ref CommandRecorder, Stage, Stage)"/>
    /// overload during a real present loop.
    /// </summary>
    public void Submit(Queue queue, ref CommandRecorder recorder)
    {
        ArgumentNullException.ThrowIfNull(queue);
        Slot.MarkSubmitted();
        queue.Submit2(ref recorder, in Slot.InFlightHandle);
    }

    /// <summary>
    /// Swapchain-aware submit. Waits on
    /// <see cref="ImageAcquired"/> at <paramref name="imageAcquireWaitStage"/>
    /// (typically <see cref="Stage.ColorAttachmentOutput"/> for a
    /// dynamic-rendering color pass), signals
    /// <see cref="RenderingDone"/> at
    /// <paramref name="renderingDoneSignalStage"/> (typically
    /// <see cref="Stage.AllGraphics"/>), and signals the slot's fence
    /// at completion.
    /// </summary>
    public void Submit(
        Queue                queue,
        ref CommandRecorder  recorder,
        Stage                imageAcquireWaitStage    = Stage.ColorAttachmentOutput,
        Stage                renderingDoneSignalStage = Stage.AllGraphics)
    {
        ArgumentNullException.ThrowIfNull(queue);
        Slot.MarkSubmitted();

        var wait   = new SemaphoreSubmit(ImageAcquired, imageAcquireWaitStage);
        var signal = new SemaphoreSubmit(RenderingDone, renderingDoneSignalStage);
        queue.Submit2(
            ref recorder, in Slot.InFlightHandle,
            System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref wait,   1),
            System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref signal, 1));
    }

    public void Dispose()
    {
        // Slot lifecycle is owned by FrameRing — nothing to release here.
        // The end-of-frame contract is "Submit(...) was called", which is
        // what arms InFlight for next rotation. No-op Dispose lets the
        // sketch's `using var frame = frames.BeginFrame()` stay idiomatic.
    }
}
