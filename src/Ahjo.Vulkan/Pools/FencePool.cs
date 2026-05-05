using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Pools <see cref="Fence"/> handles so per-frame
/// <see cref="Acquire(bool)"/>/<see cref="Release"/> stays allocation-free
/// after warmup. Released fences are not reset — the caller decides
/// whether the fence needs <see cref="Fence.Reset"/> before re-use, since
/// resetting a still-pending fence is a validation error.
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
    private readonly Stack<nint>  _free = new();
    private readonly List<nint>   _allHandles = new();
    private bool _disposed;

    public int IdleCount      => _free.Count;
    public int AllocatedCount => _allHandles.Count;

    public FencePool(Device device)
    {
        ArgumentNullException.ThrowIfNull(device);
        _device = device;
    }

    /// <summary>
    /// Hands out a fence. Pops the free list if available; otherwise
    /// <c>vkCreateFence</c> grows the pool by one.
    /// </summary>
    /// <param name="initiallySignaled">
    /// Only meaningful when the pool grows. Pooled fences are returned in
    /// whatever state the caller left them; <see cref="Fence.Reset"/>
    /// before re-using if you need it unsignaled.
    /// </param>
    public Fence Acquire(bool initiallySignaled = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_free.Count > 0)
            return new Fence((VkFence_T*)_free.Pop(), _device.Handle);

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

    /// <summary>Returns a fence to the free list. Double-release is undefined.</summary>
    public void Release(Fence fence)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (fence.IsNull) return;
        _free.Push((nint)fence.Handle);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (nint h in _allHandles)
            Vk.vkDestroyFence(_device.Handle, (VkFence_T*)h, null);
        _allHandles.Clear();
        _free.Clear();
    }
}
