using System.Diagnostics;
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

        // Pre-grow so the Add after vkCreateSemaphore can't OOM and
        // orphan the just-created VkSemaphore.
        _allHandles.EnsureCapacity(_allHandles.Count + 1);

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

        // Pre-grow so the Add after vkCreateSemaphore can't OOM and
        // orphan the just-created VkSemaphore.
        _allHandles.EnsureCapacity(_allHandles.Count + 1);

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
        // Type narrows away most cross-pool misuse at compile time, but
        // a release of a foreign-pool handle would corrupt _freeBinary
        // and Dispose's destruction loop. Linear scan is fine — the
        // pool size is bounded by frames-in-flight (small).
        Debug.Assert(_allHandles.Contains((nint)sem.Handle),
            "SemaphorePool.Release(BinarySemaphore): handle was not produced by this pool.");
        _freeBinary.Push((nint)sem.Handle);
    }

    public void Release(TimelineSemaphore sem)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (sem.IsNull) return;
        Debug.Assert(_allHandles.Contains((nint)sem.Handle),
            "SemaphorePool.Release(TimelineSemaphore): handle was not produced by this pool.");
        _freeTimeline.Push((nint)sem.Handle);
    }

    /// <summary>
    /// Destroys <paramref name="sem"/> instead of returning it to the
    /// free-list — the escape hatch for binary semaphores that are
    /// stuck in a bad state and cannot be safely recycled. The canonical
    /// case is a binary semaphore that <c>vkAcquireNextImageKHR</c>
    /// signaled but a submit never waited on (typical
    /// <c>VK_ERROR_OUT_OF_DATE_KHR</c> path before
    /// <see cref="Swapchain.Recreate"/>): it is permanently signaled
    /// from the host side, and Vulkan offers no host-reset for binary
    /// semaphores. Discard removes the handle from the pool's tracking
    /// and from any free-list, then immediately calls
    /// <c>vkDestroySemaphore</c>; the caller follows up with
    /// <see cref="AcquireBinary"/> to materialize a fresh one.
    /// </summary>
    public void Discard(BinarySemaphore sem)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (sem.IsNull) return;
        DiscardCore((nint)sem.Handle, _freeBinary);
    }

    /// <summary>Counterpart of <see cref="Discard(BinarySemaphore)"/> for timeline semaphores.</summary>
    public void Discard(TimelineSemaphore sem)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (sem.IsNull) return;
        DiscardCore((nint)sem.Handle, _freeTimeline);
    }

    private void DiscardCore(nint handle, Stack<nint> matchingFreeList)
    {
        // Drop from tracking *before* destroy so a throw out of
        // vkDestroySemaphore can't leave us with a dangling _allHandles
        // entry that Dispose would later free a second time.
        if (!_allHandles.Remove(handle))
            throw new ArgumentException("Semaphore was not produced by this pool.", nameof(handle));

        // Remove from the matching free-list if it was Released back in
        // before this Discard call. Stack<T> has no Remove; rebuild
        // without the target.
        PurgeStack(matchingFreeList, handle);

        Vk.vkDestroySemaphore(_device.Handle, (VkSemaphore_T*)handle, null);
    }

    private static void PurgeStack(Stack<nint> stack, nint target)
    {
        if (stack.Count == 0) return;
        nint[] snapshot = stack.ToArray();   // top-of-stack first
        stack.Clear();
        for (int i = snapshot.Length - 1; i >= 0; i--)
        {
            if (snapshot[i] == target) continue;
            stack.Push(snapshot[i]);
        }
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
