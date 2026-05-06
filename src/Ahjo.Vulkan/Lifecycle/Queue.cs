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
    /// Submits a single command buffer via <c>vkQueueSubmit2</c>. Calls
    /// <see cref="CommandRecorder.End"/> first if the recorder is still
    /// open. Pass <c>default(Fence)</c> for fire-and-forget; otherwise
    /// the fence is signaled when GPU execution completes.
    /// </summary>
    public void Submit2(ref CommandRecorder recorder, in Fence fence)
        => Submit2(ref recorder, in fence, default, default);

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
            Vk.vkQueueSubmit2(Handle, 1, &submit, fence.Handle).ThrowIfFailed();
        }
    }
}
