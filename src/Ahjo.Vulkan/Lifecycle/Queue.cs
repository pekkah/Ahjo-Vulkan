using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Wrapper handle for a <c>VkQueue</c>. Owned by a <see cref="Device"/>
/// and produced exclusively by <see cref="PhysicalDevice.CreateDevice"/>;
/// the device caches one instance per <c>(family, index)</c> requested in
/// <see cref="DeviceDescription.Queues"/>.
/// </summary>
/// <remarks>
/// Owner-class shape (sealed class) for the same reason as
/// <see cref="PhysicalDevice"/> and <see cref="Device"/>: queues are
/// created 1–4 times per device and never debug-named or pooled.
/// Construction is internal — outside-the-wrapper construction is
/// meaningless because <c>vkGetDeviceQueue</c> requires a device.
/// </remarks>
public sealed unsafe class Queue
{
    internal readonly VkQueue_T* Handle;
    public   readonly uint       FamilyIndex;
    public   readonly uint       QueueIndex;
    public   readonly Device     Device;

    internal Queue(Device device, VkQueue_T* handle, uint familyIndex, uint queueIndex)
    {
        Device      = device;
        Handle      = handle;
        FamilyIndex = familyIndex;
        QueueIndex  = queueIndex;
    }

    public ulong RawHandle => (ulong)(nint)Handle;
    public bool  IsNull    => Handle == null;
    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_QUEUE;

    /// <summary>
    /// Wraps <c>vkQueueWaitIdle</c>. Blocks the calling thread until every
    /// previously-submitted batch on this queue has finished. Cheaper than
    /// <see cref="Device.WaitIdle"/> when only one queue's work needs to
    /// drain; the spec is explicit that <c>vkQueueWaitIdle</c> is
    /// equivalent to a fence on every prior submit, waited to completion.
    /// </summary>
    public void WaitIdle()
    {
        Vk.vkQueueWaitIdle(Handle).ThrowIfFailed();
    }

    /// <summary>
    /// One-shot record/submit/wait helper. Acquires a primary command
    /// buffer from <paramref name="pool"/> (already begun with
    /// <c>ONE_TIME_SUBMIT</c>), runs <paramref name="record"/>, ends and
    /// submits via <see cref="Submit2(ref CommandRecorder, in Fence)"/>
    /// with no waits / signals / fence, then calls <see cref="WaitIdle"/>.
    /// The buffer is returned to the pool's outstanding-set on exit (via
    /// <see cref="CommandRecorder.Dispose"/>) regardless of whether the
    /// recording delegate or the submit threw.
    /// </summary>
    /// <remarks>
    /// <para>This is the canonical pattern for one-off GPU work that the
    /// caller needs to observe synchronously: asset uploads, mip
    /// generation, IBL convolution, BRDF/sheen LUT bakes, particle
    /// state init, environment hot-swap. Per-call overhead after pool
    /// warmup is the begin/end pair plus the queue submit and wait —
    /// no managed allocation.</para>
    /// <para><b>Mid-frame implications.</b> <see cref="WaitIdle"/> drains
    /// every other submit on this queue, including the in-flight frame
    /// when called mid-frame. Engines that hot-swap assets between
    /// frames rely on this — a mid-frame replace works because
    /// ImmediateSubmit waits for any prior in-flight work that might
    /// still reference the resource being replaced. Use a sparing
    /// hand on hot paths; per-draw or per-pass invocation will tank
    /// throughput.</para>
    /// </remarks>
    public void ImmediateSubmit(CommandBufferPool pool, ImmediateRecord record)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(record);

        CommandRecorder recorder = pool.Begin();
        try
        {
            record(ref recorder);
            // Submit2(ref recorder, in Fence) ends the recorder for us.
            // default(Fence) lowers to a null VkFence — fire-and-forget,
            // since WaitIdle below provides the synchronisation.
            Submit2(ref recorder, default);
            WaitIdle();
        }
        finally
        {
            // Retire the buffer no matter what: a record-time throw, a
            // submit failure, or a wait-idle device-lost must still leave
            // the pool's accounting consistent so the next frame's reset
            // doesn't trip its outstanding assert.
            recorder.Dispose();
        }
    }

    /// <summary>
    /// Submits a single command buffer via <c>vkQueueSubmit2</c>. Calls
    /// <see cref="CommandRecorder.End"/> first if the recorder is still
    /// open. Pass <c>default(Fence)</c> for fire-and-forget; otherwise
    /// the fence is signaled when GPU execution completes.
    /// </summary>
    public void Submit2(ref CommandRecorder recorder, in Fence fence)
        => Submit2(ref recorder, in fence, default, default);

    /// <summary>
    /// Submits a pre-recorded, already-ended command buffer by raw handle
    /// (typically <see cref="CommandRecorder.RawHandle"/>) via
    /// <c>vkQueueSubmit2</c>. Use when the recording thread and the
    /// submitting thread differ — <see cref="CommandRecorder"/> is a
    /// <c>ref struct</c> and cannot legally cross threads, but the
    /// underlying <c>VkCommandBuffer</c> pointer can.
    /// </summary>
    /// <remarks>
    /// The caller must have ended recording (e.g. via
    /// <see cref="CommandRecorder.End"/>) before crossing threads — this
    /// overload does not call <c>vkEndCommandBuffer</c>. The recorder
    /// must also remain undisposed on the recording thread for the
    /// duration of the submit so the pool's outstanding-set tracks the
    /// in-flight buffer correctly.
    /// </remarks>
    public void Submit2(nint commandBuffer, in Fence fence)
    {
        var cb = (VkCommandBuffer_T*)commandBuffer;
        var cbInfo = new VkCommandBufferSubmitInfo
        {
            sType         = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_SUBMIT_INFO,
            commandBuffer = cb,
        };
        var submit = new VkSubmitInfo2
        {
            sType                  = VkStructureType.VK_STRUCTURE_TYPE_SUBMIT_INFO_2,
            commandBufferInfoCount = 1,
            pCommandBufferInfos    = &cbInfo,
        };
        Device.Functions.QueueSubmit2(Handle, 1, &submit, fence.Handle).ThrowIfFailed();
    }

    /// <summary>
    /// Submits a single command buffer with optional wait + signal
    /// binary semaphores. Pass empty spans (or use the no-semaphore
    /// overload) for fire-and-forget submits.
    /// </summary>
    public void Submit2(
        ref CommandRecorder           recorder,
        in  Fence                     fence,
        ReadOnlySpan<SemaphoreSubmit> waits,
        ReadOnlySpan<SemaphoreSubmit> signals)
    {
        recorder.End();
        VkCommandBuffer_T* cb = recorder.Handle;
        var cbInfo = new VkCommandBufferSubmitInfo
        {
            sType         = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_SUBMIT_INFO,
            commandBuffer = cb,
        };

        Span<VkSemaphoreSubmitInfo> waitInfos   = stackalloc VkSemaphoreSubmitInfo[Math.Max(waits.Length,   1)];
        Span<VkSemaphoreSubmitInfo> signalInfos = stackalloc VkSemaphoreSubmitInfo[Math.Max(signals.Length, 1)];
        for (int i = 0; i < waits.Length; i++)
            waitInfos[i] = new VkSemaphoreSubmitInfo
            {
                sType     = VkStructureType.VK_STRUCTURE_TYPE_SEMAPHORE_SUBMIT_INFO,
                semaphore = waits[i].Semaphore.Handle,
                stageMask = (ulong)waits[i].Stage,
            };
        for (int i = 0; i < signals.Length; i++)
            signalInfos[i] = new VkSemaphoreSubmitInfo
            {
                sType     = VkStructureType.VK_STRUCTURE_TYPE_SEMAPHORE_SUBMIT_INFO,
                semaphore = signals[i].Semaphore.Handle,
                stageMask = (ulong)signals[i].Stage,
            };

        fixed (VkSemaphoreSubmitInfo* pWait   = waitInfos)
        fixed (VkSemaphoreSubmitInfo* pSignal = signalInfos)
        {
            var submit = new VkSubmitInfo2
            {
                sType                    = VkStructureType.VK_STRUCTURE_TYPE_SUBMIT_INFO_2,
                waitSemaphoreInfoCount   = (uint)waits.Length,
                pWaitSemaphoreInfos      = waits.Length   > 0 ? pWait   : null,
                commandBufferInfoCount   = 1,
                pCommandBufferInfos      = &cbInfo,
                signalSemaphoreInfoCount = (uint)signals.Length,
                pSignalSemaphoreInfos    = signals.Length > 0 ? pSignal : null,
            };
            Device.Functions.QueueSubmit2(Handle, 1, &submit, fence.Handle).ThrowIfFailed();
        }
    }
}
