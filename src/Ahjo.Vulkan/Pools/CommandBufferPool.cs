using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Owns one <c>VkCommandPool</c> and tracks command buffers across the
/// per-frame begin → end → reset cycle. One pool per
/// <c>(queue family × thread)</c> per Vulkan's external-synchronization
/// rules: a single <see cref="CommandBufferPool"/> is not safe to
/// <see cref="Begin"/> from multiple threads concurrently.
/// </summary>
/// <remarks>
/// <para><b>Lifecycle.</b> The pool is deliberately created without
/// <c>RESET_COMMAND_BUFFER_BIT</c> — per-buffer reset costs more than a
/// whole-pool reset and offers no real flexibility for a frame-paced
/// engine. That means a buffer in the executable state cannot be
/// re-<see cref="Begin"/>'d until <see cref="ResetForFrame"/> moves the
/// whole pool back to "initial."</para>
/// <para>The pool maintains two lists: <c>_idle</c> (buffers in the
/// initial state, eligible to hand out) and <c>_spent</c> (buffers that
/// are no longer in the initial state — used this frame, or left in an
/// indeterminate state by a failed <c>vkBeginCommandBuffer</c> — and so
/// must wait for the next whole-pool reset before reuse).
/// <see cref="Begin"/> pops from <c>_idle</c> (or grows the pool by one)
/// and routes a begin-failed buffer to <c>_spent</c>;
/// <see cref="Retire"/> pushes to <c>_spent</c>;
/// <see cref="ResetForFrame"/> calls <c>vkResetCommandPool</c> and bulk-
/// moves <c>_spent</c> into <c>_idle</c>.</para>
/// <para>The lists hold raw <c>nint</c> handles so per-frame churn stays
/// allocation-free and never boxes a wrapper struct. <see cref="CommandRecorder"/>
/// is a stack-only <c>ref struct</c> built per <see cref="Begin"/> call
/// without a heap allocation.</para>
/// </remarks>
public sealed unsafe class CommandBufferPool : IDisposable
{
    private readonly Device           _device;
    private readonly VkCommandPool_T* _pool;
    private readonly Stack<nint>      _idle  = new();
    private readonly Stack<nint>      _spent = new();
    private          int              _outstanding;
    private          int              _allocated;
    private          bool             _disposed;

    /// <summary>The queue family this pool was created against.</summary>
    public uint QueueFamilyIndex { get; }

    /// <summary>
    /// Owning device. Exposed internally so <see cref="CommandRecorder"/>
    /// can reach the per-device extension function table for debug
    /// markers etc. without re-resolving entry points per call.
    /// </summary>
    internal Device Device => _device;

    /// <summary>Total <c>VkCommandBuffer</c>s ever allocated through this pool.</summary>
    public int AllocatedCount => _allocated;

    /// <summary>Number of recorders currently between <see cref="Begin"/> and <c>Dispose</c>.</summary>
    public int OutstandingCount => _outstanding;

    /// <summary>
    /// Creates a pool against <paramref name="device"/>'s queue family
    /// <paramref name="queueFamilyIndex"/>. The
    /// <c>VK_COMMAND_POOL_CREATE_TRANSIENT_BIT</c> hint signals to the
    /// driver that the pool churns per-frame.
    /// </summary>
    public CommandBufferPool(Device device, uint queueFamilyIndex)
    {
        ArgumentNullException.ThrowIfNull(device);
        _device          = device;
        QueueFamilyIndex = queueFamilyIndex;

        var ci = new VkCommandPoolCreateInfo
        {
            sType            = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO,
            flags            = (uint)VkCommandPoolCreateFlagBits.VK_COMMAND_POOL_CREATE_TRANSIENT_BIT,
            queueFamilyIndex = queueFamilyIndex,
        };
        VkCommandPool_T* raw = null;
        Vk.vkCreateCommandPool(device.Handle, &ci, null, &raw).ThrowIfFailed();
        _pool = raw;
    }

    /// <summary>
    /// Acquires a primary command buffer in the initial state (popping the
    /// free list, or growing by one if empty), calls
    /// <c>vkBeginCommandBuffer</c> with <c>ONE_TIME_SUBMIT</c>, and returns
    /// a <see cref="CommandRecorder"/>.
    /// </summary>
    public CommandRecorder Begin()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Pre-grow _spent BEFORE acquiring a buffer so the catch handler
        // below can return a begin-failed buffer without an OOM on capacity
        // growth while already handling a failure — and so that if this
        // growth itself OOMs, no command buffer has been acquired yet to
        // orphan. By the time the begin runs, _spent has room for the push.
        _spent.EnsureCapacity(_spent.Count + 1);

        VkCommandBuffer_T* cb;
        if (_idle.Count > 0)
        {
            cb = (VkCommandBuffer_T*)_idle.Pop();
        }
        else
        {
            var ai = new VkCommandBufferAllocateInfo
            {
                sType              = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO,
                commandPool        = _pool,
                level              = VkCommandBufferLevel.VK_COMMAND_BUFFER_LEVEL_PRIMARY,
                commandBufferCount = 1,
            };
            VkCommandBuffer_T* raw = null;
            Vk.vkAllocateCommandBuffers(_device.Handle, &ai, &raw).ThrowIfFailed();
            cb = raw;
            _allocated++;
        }

        var bi = new VkCommandBufferBeginInfo
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO,
            flags = (uint)VkCommandBufferUsageFlagBits.VK_COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT,
        };
        try
        {
            _device.Functions.BeginCommandBuffer(cb, &bi).ThrowIfFailed();
        }
        catch
        {
            // A failed vkBeginCommandBuffer (OOM, validation-layer reject)
            // does NOT guarantee the buffer returns to the initial state,
            // and this pool deliberately omits RESET_COMMAND_BUFFER_BIT.
            // Routing the cb back to _idle would let a later Begin pop it
            // and call vkBeginCommandBuffer on a non-initial buffer —
            // VUID-vkBeginCommandBuffer-commandBuffer-00049/00050. Route it
            // to _spent instead: the cb only re-enters circulation after
            // ResetForFrame's vkResetCommandPool returns the whole pool to
            // the initial state. Still no orphaned handle, and _allocated
            // stays accurate. (The pre-grow at the top of Begin guarantees
            // this push can't OOM.)
            _spent.Push((nint)cb);
            throw;
        }

        _outstanding++;
        return new CommandRecorder(this, cb);
    }

    /// <summary>
    /// Marks a recorder's command buffer as spent for the current frame.
    /// Called by <see cref="CommandRecorder.Dispose"/>; user code shouldn't
    /// invoke directly.
    /// </summary>
    internal void Retire(VkCommandBuffer_T* cb)
    {
        _spent.Push((nint)cb);
        _outstanding--;
    }

    /// <summary>
    /// Resets the entire pool — every spent buffer returns to the initial
    /// state and re-enters the free list. Must be called once per frame
    /// (typically from <c>FrameContext.BeginFrame</c>) and only after the
    /// GPU has finished consuming the prior frame's submissions, or
    /// validation will fire.
    /// </summary>
    public void ResetForFrame()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (AhjoValidation.IsEnabled && _outstanding != 0)
            AhjoValidation.Fail("CommandBufferPool",
                $"ResetForFrame called with {_outstanding} outstanding recorder(s) — dispose them first.");

        Vk.vkResetCommandPool(_device.Handle, _pool, flags: 0).ThrowIfFailed();
        while (_spent.Count > 0)
            _idle.Push(_spent.Pop());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // vkDestroyCommandPool implicitly frees every command buffer the
        // pool ever allocated, so we don't iterate _idle/_spent ourselves.
        if (_pool != null)
            Vk.vkDestroyCommandPool(_device.Handle, _pool, null);
        _idle.Clear();
        _spent.Clear();
    }
}
