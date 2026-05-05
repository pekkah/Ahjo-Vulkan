using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Pools binary and timeline semaphores in separate free-lists so the
/// engine's per-frame churn (image-acquired / rendering-done binaries +
/// the cross-frame timeline) stays allocation-free after warmup.
/// </summary>
/// <remarks>
/// Single-threaded by design — same threading contract as
/// <see cref="FencePool"/>. Binary and timeline are intentionally not
/// fungible: the wrapper's distinct types
/// (<see cref="BinarySemaphore"/> vs. <see cref="TimelineSemaphore"/>)
/// make accidental cross-pool reuse a compile-time error rather than a
/// validation-runtime crash.
/// </remarks>
public sealed unsafe class SemaphorePool : IDisposable
{
    private readonly Device      _device;
    private readonly Stack<nint> _freeBinary   = new();
    private readonly Stack<nint> _freeTimeline = new();
    private readonly List<nint>  _allHandles   = new();
    private bool _disposed;

    public int IdleBinaryCount   => _freeBinary.Count;
    public int IdleTimelineCount => _freeTimeline.Count;
    public int AllocatedCount    => _allHandles.Count;

    public SemaphorePool(Device device)
    {
        ArgumentNullException.ThrowIfNull(device);
        _device = device;
    }

    public BinarySemaphore AcquireBinary()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_freeBinary.Count > 0)
            return new BinarySemaphore((VkSemaphore_T*)_freeBinary.Pop());

        var ci = new VkSemaphoreCreateInfo
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO,
        };
        VkSemaphore_T* raw = null;
        Vk.vkCreateSemaphore(_device.Handle, &ci, null, &raw).ThrowIfFailed();
        _allHandles.Add((nint)raw);
        return new BinarySemaphore(raw);
    }

    public TimelineSemaphore AcquireTimeline(ulong initialValue = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_freeTimeline.Count > 0)
            return new TimelineSemaphore((VkSemaphore_T*)_freeTimeline.Pop(), _device.Handle);

        var typeInfo = new VkSemaphoreTypeCreateInfo
        {
            sType         = VkStructureType.VK_STRUCTURE_TYPE_SEMAPHORE_TYPE_CREATE_INFO,
            semaphoreType = VkSemaphoreType.VK_SEMAPHORE_TYPE_TIMELINE,
            initialValue  = initialValue,
        };
        var ci = new VkSemaphoreCreateInfo
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO,
            pNext = &typeInfo,
        };
        VkSemaphore_T* raw = null;
        Vk.vkCreateSemaphore(_device.Handle, &ci, null, &raw).ThrowIfFailed();
        _allHandles.Add((nint)raw);
        return new TimelineSemaphore(raw, _device.Handle);
    }

    public void Release(BinarySemaphore sem)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (sem.IsNull) return;
        _freeBinary.Push((nint)sem.Handle);
    }

    public void Release(TimelineSemaphore sem)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (sem.IsNull) return;
        _freeTimeline.Push((nint)sem.Handle);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (nint h in _allHandles)
            Vk.vkDestroySemaphore(_device.Handle, (VkSemaphore_T*)h, null);
        _allHandles.Clear();
        _freeBinary.Clear();
        _freeTimeline.Clear();
    }
}
