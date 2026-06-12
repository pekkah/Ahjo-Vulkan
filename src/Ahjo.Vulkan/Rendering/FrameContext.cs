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
    /// Tell the ring that the most recent
    /// <see cref="Swapchain.AcquireNextImage"/> on this slot's
    /// <see cref="ImageAcquired"/> signaled it. Call after
    /// <see cref="AcquireResult.Success"/> or
    /// <see cref="AcquireResult.Suboptimal"/>; do <i>not</i> call after
    /// <see cref="AcquireResult.OutOfDate"/> (the spec says the
    /// semaphore is left untouched on that path). The flag is cleared
    /// automatically when the swapchain-aware
    /// <see cref="Submit(Queue, ref CommandRecorder, Stage, Stage)"/>
    /// queues a wait on the semaphore, or when
    /// <see cref="FrameRing.RecycleStaleAcquireSemaphores"/> rotates
    /// the slot's handle after a <see cref="Swapchain.Recreate"/>.
    /// </summary>
    public void MarkImageAcquireSignaled() => Slot.MarkAcquireSignaled();

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
        queue.Submit2(ref recorder, in Slot.InFlightHandle);
        // Marked only after Submit2 returns (#112): a throwing submit must
        // not arm the in-flight fence wait for GPU work that was never
        // queued — the next BeginFrame would block forever on a fence
        // nothing will signal.
        Slot.MarkSubmitted();
    }

    /// <summary>
    /// Swapchain-aware submit. Waits on this slot's
    /// <see cref="ImageAcquired"/> at <paramref name="imageAcquireWaitStage"/>
    /// (typically <see cref="Stage.ColorAttachmentOutput"/> for a
    /// dynamic-rendering color pass), signals the swapchain's
    /// <i>per-image</i> <c>RenderingDone</c> semaphore for
    /// <paramref name="imageIndex"/> at
    /// <paramref name="renderingDoneSignalStage"/> (typically
    /// <see cref="Stage.AllGraphics"/>), and signals the slot's fence
    /// at completion. Pair with
    /// <see cref="Swapchain.Present(Queue, uint)"/> on the same
    /// <paramref name="swapchain"/> + <paramref name="imageIndex"/> to
    /// keep signal/wait identity consistent.
    /// </summary>
    /// <remarks>
    /// <para><b>Why the signal target lives on the swapchain.</b> A
    /// per-frame-in-flight signal target trips
    /// VUID-vkQueueSubmit2-semaphore-03868 when acquire order doesn't
    /// match slot rotation: frame N+1's submit can re-signal slot K's
    /// semaphore while a prior present of a different image still
    /// holds it. The swapchain orders "next acquire of image i" after
    /// "prior present of image i", so a per-image signal target is
    /// provably safe to re-signal on the next acquire of the same
    /// image. See issue #89 for the full repro and spec citation.</para>
    /// <para><b>imageIndex provenance.</b> Pass the value returned by
    /// <see cref="Swapchain.AcquireNextImage"/> on the same
    /// <paramref name="swapchain"/>, in the same frame. The wrapper
    /// indexes <see cref="Swapchain.GetRenderingDoneFor"/> with it
    /// directly — out-of-range or stale indices would either trip the
    /// validator or worse.</para>
    /// </remarks>
    public void Submit(
        Queue                queue,
        ref CommandRecorder  recorder,
        Swapchain            swapchain,
        uint                 imageIndex,
        Stage                imageAcquireWaitStage    = Stage.ColorAttachmentOutput,
        Stage                renderingDoneSignalStage = Stage.AllGraphics)
    {
        ArgumentNullException.ThrowIfNull(swapchain);
        Submit(queue, ref recorder, swapchain.GetRenderingDoneFor(imageIndex),
               imageAcquireWaitStage, renderingDoneSignalStage);
    }

    /// <summary>
    /// Lower-level swapchain-aware submit: waits on this slot's
    /// <see cref="ImageAcquired"/> at <paramref name="imageAcquireWaitStage"/>
    /// and signals an explicit <paramref name="signalSemaphore"/> at
    /// <paramref name="signalSemaphoreStage"/>. The
    /// <see cref="Submit(Queue, ref CommandRecorder, Swapchain, uint, Stage, Stage)"/>
    /// overload routes through this one with
    /// <see cref="Swapchain.GetRenderingDoneFor"/>; reach for this
    /// overload when you're driving present semaphores yourself
    /// (multi-swapchain bridging, exotic test harnesses).
    /// </summary>
    /// <remarks>
    /// <para>The signal target must be unsignaled when the queue
    /// reaches it (VUID-vkQueueSubmit2-semaphore-03868). Per-acquired-image
    /// semaphores satisfy that automatically; per-frame-in-flight
    /// semaphores do not — see issue #89. If you're not certain which
    /// shape you have, use the
    /// <see cref="Submit(Queue, ref CommandRecorder, Swapchain, uint, Stage, Stage)"/>
    /// overload.</para>
    /// </remarks>
    public void Submit(
        Queue                queue,
        ref CommandRecorder  recorder,
        in BinarySemaphore   signalSemaphore,
        Stage                imageAcquireWaitStage = Stage.ColorAttachmentOutput,
        Stage                signalSemaphoreStage  = Stage.AllGraphics)
    {
        ArgumentNullException.ThrowIfNull(queue);

        var wait   = new SemaphoreSubmit(ImageAcquired,   imageAcquireWaitStage);
        var signal = new SemaphoreSubmit(signalSemaphore, signalSemaphoreStage);
        queue.Submit2(
            ref recorder, in Slot.InFlightHandle,
            System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref wait,   1),
            System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref signal, 1));

        // Bookkeeping only after Submit2 returns (#112): if the submit
        // throws (vkEndCommandBuffer / vkQueueSubmit2 failure), no GPU
        // wait was queued — the acquire signal must stay flagged pending
        // so RecycleStaleAcquireSemaphores rotates the stuck semaphore,
        // and the in-flight fence must stay un-armed so the next
        // BeginFrame doesn't wait on a submit that never happened. On the
        // success path the queued wait consumes the host-side acquire
        // signal as the GPU reaches it, so from FrameRing's bookkeeping
        // POV the signal is no longer pending the moment Submit2 returns.
        Slot.MarkSubmitted();
        Slot.MarkAcquireWaitConsumed();
    }

    public void Dispose()
    {
        // Slot lifecycle is owned by FrameRing — nothing to release here.
        // The end-of-frame contract is "Submit(...) was called", which is
        // what arms InFlight for next rotation. No-op Dispose lets the
        // sketch's `using var frame = frames.BeginFrame()` stay idiomatic.
    }
}
