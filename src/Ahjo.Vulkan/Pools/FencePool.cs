using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Pools <see cref="Fence"/> handles so per-frame
/// <see cref="Acquire(bool)"/>/<see cref="Release"/> stays allocation-free
/// after warmup. The pool routes by fence state: <see cref="Release"/>
/// queries <c>vkGetFenceStatus</c> once and pushes the handle onto the
/// signaled or unsignaled free-list, so <see cref="Acquire(bool)"/>
/// always honors <c>initiallySignaled</c> — popping from the matching
/// list, or growing the pool with the right create flag when the list is
/// empty. A still-pending fence (caller didn't wait before releasing) is
/// a usage bug; it would surface as an asymmetric stack push since
/// <c>vkGetFenceStatus</c> distinguishes only signaled / not-signaled,
/// not signaled / unsignaled / pending.
/// </summary>
/// <remarks>
/// Single-threaded by design: a pool is one thread's view of an internal
/// resource (the same as <see cref="CommandBufferPool"/>). Per-thread
/// pools share the underlying <see cref="Device"/> safely because
/// <c>vkCreateFence</c>/<c>vkDestroyFence</c> are externally synchronized
/// only on the device.
/// </remarks>
public sealed unsafe class FencePool : IDisposable
{
    private readonly Device       _device;
    private readonly Stack<nint>  _freeSignaled   = new();
    private readonly Stack<nint>  _freeUnsignaled = new();
    private readonly List<nint>   _allHandles     = new();
    private bool _disposed;

    public int IdleCount           => _freeSignaled.Count + _freeUnsignaled.Count;
    public int IdleSignaledCount   => _freeSignaled.Count;
    public int IdleUnsignaledCount => _freeUnsignaled.Count;
    public int AllocatedCount      => _allHandles.Count;

    public FencePool(Device device)
    {
        ArgumentNullException.ThrowIfNull(device);
        _device = device;
    }

    /// <summary>
    /// Hands out a fence in the requested state. Pops the matching
    /// free-list when non-empty; otherwise <c>vkCreateFence</c> grows the
    /// pool by one with the correct <c>VK_FENCE_CREATE_SIGNALED_BIT</c>
    /// setting. The returned fence is guaranteed to be in the requested
    /// state regardless of pool history.
    /// </summary>
    public Fence Acquire(bool initiallySignaled = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Stack<nint> preferred = initiallySignaled ? _freeSignaled : _freeUnsignaled;
        if (preferred.Count > 0)
            return new Fence((VkFence_T*)preferred.Pop(), _device.Handle);

        // Pre-grow _allHandles so the Add below can't OOM after
        // vkCreateFence and orphan the just-created VkFence.
        _allHandles.EnsureCapacity(_allHandles.Count + 1);

        var ci = new VkFenceCreateInfo
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_FENCE_CREATE_INFO,
            flags = initiallySignaled ? (uint)VkFenceCreateFlagBits.VK_FENCE_CREATE_SIGNALED_BIT : 0u,
        };
        VkFence_T* raw = null;
        Vk.vkCreateFence(_device.Handle, &ci, null, &raw).ThrowIfFailed();
        _allHandles.Add((nint)raw);
        return new Fence(raw, _device.Handle);
    }

    /// <summary>
    /// Returns a fence to the appropriate free-list, querying
    /// <c>vkGetFenceStatus</c> to decide. Caller must not release a
    /// fence with pending GPU work — wait or reset first. Double-release
    /// is undefined.
    /// </summary>
    public void Release(Fence fence)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (fence.IsNull) return;
        Stack<nint> bucket = fence.IsSignaled ? _freeSignaled : _freeUnsignaled;
        bucket.Push((nint)fence.Handle);
    }

    /// <summary>
    /// Returns a fence to a free-list without querying
    /// <c>vkGetFenceStatus</c>, routing by the caller-supplied
    /// <paramref name="knownSignaled"/> state. Use this on a teardown path
    /// after device loss, where the status query would itself throw
    /// <c>VK_ERROR_DEVICE_LOST</c> (see issue #107): the bucket choice is
    /// immaterial since <see cref="Dispose"/> destroys every handle next,
    /// but the parameterless <see cref="Release(Fence)"/> would let the
    /// exception escape <c>Dispose</c> and strand the remaining handles.
    /// </summary>
    /// <remarks>
    /// Precondition: outside an imminent <see cref="Dispose"/>,
    /// <paramref name="knownSignaled"/> MUST match the fence's actual state.
    /// A wrong value files the fence on the wrong free-list and breaks
    /// <see cref="Acquire(bool)"/>'s guarantee to hand back a fence in the
    /// requested state. This overload exists only for the device-lost
    /// teardown path; per-frame recycling must use the status-querying
    /// <see cref="Release(Fence)"/>.
    /// </remarks>
    public void Release(Fence fence, bool knownSignaled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (fence.IsNull) return;
        Stack<nint> bucket = knownSignaled ? _freeSignaled : _freeUnsignaled;
        bucket.Push((nint)fence.Handle);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (nint h in _allHandles)
            Vk.vkDestroyFence(_device.Handle, (VkFence_T*)h, null);
        _allHandles.Clear();
        _freeSignaled.Clear();
        _freeUnsignaled.Clear();
    }
}
