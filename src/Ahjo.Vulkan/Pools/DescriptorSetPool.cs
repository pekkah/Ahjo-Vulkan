using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Wraps a <c>VkDescriptorPool</c> with per-layout free-lists so per-frame
/// descriptor-set allocation stays O(1) and allocation-free after warmup.
/// One pool per <c>(thread × use case)</c> per Vulkan's external-sync
/// rules: a single <see cref="DescriptorSetPool"/> is not safe to
/// <see cref="Acquire"/> from multiple threads concurrently.
/// </summary>
/// <remarks>
/// <para><b>Reuse semantics.</b> <see cref="Release"/> does not call
/// <c>vkFreeDescriptorSets</c> — the underlying <c>VkDescriptorSet</c>
/// stays alive and re-enters the layout-keyed free-list, ready for the
/// next <see cref="Acquire"/> with the same layout (the caller binds new
/// resources via <c>vkUpdateDescriptorSets</c> / push-descriptors on
/// re-use). <see cref="Reset"/> calls <c>vkResetDescriptorPool</c> and
/// invalidates every set the pool ever handed out — use it to wipe the
/// per-frame table in one cheap call.</para>
/// <para>The pool is for long-lived descriptor sets (texture arrays,
/// bindless resource tables). Per-frame uniform/storage descriptors
/// flow through <c>CommandRecorder.PushDescriptors</c> when that lands
/// (#17 follow-up) and never allocate a <c>VkDescriptorSet</c> at all.</para>
/// </remarks>
public sealed unsafe class DescriptorSetPool : IDisposable
{
    private readonly Device              _device;
    private readonly VkDescriptorPool_T* _pool;
    // Per-layout free-list. Different layouts allocate to different binding
    // shapes, so a set built for layout A can't be returned to a caller
    // asking for layout B.
    private readonly Dictionary<nint, Stack<nint>> _idle       = new();
    private readonly List<nint>                    _allHandles = new();
    private bool _disposed;

    /// <summary>Total <c>VkDescriptorSet</c>s ever allocated through this pool.</summary>
    public int AllocatedCount => _allHandles.Count;

    /// <summary>
    /// Creates a pool sized for <paramref name="maxSets"/> total live sets
    /// across the descriptor-type budget given by <paramref name="poolSizes"/>.
    /// </summary>
    public DescriptorSetPool(Device device, uint maxSets, ReadOnlySpan<VkDescriptorPoolSize> poolSizes)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (poolSizes.IsEmpty)
            throw new ArgumentException("poolSizes must contain at least one entry.", nameof(poolSizes));
        _device = device;

        fixed (VkDescriptorPoolSize* pSizes = poolSizes)
        {
            var ci = new VkDescriptorPoolCreateInfo
            {
                sType         = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO,
                flags         = 0,
                maxSets       = maxSets,
                poolSizeCount = (uint)poolSizes.Length,
                pPoolSizes    = pSizes,
            };
            VkDescriptorPool_T* raw = null;
            Vk.vkCreateDescriptorPool(device.Handle, &ci, null, &raw).ThrowIfFailed();
            _pool = raw;
        }
    }

    /// <summary>
    /// Hands out a descriptor set built against <paramref name="layout"/>.
    /// Pops <paramref name="layout"/>'s idle stack when available;
    /// otherwise <c>vkAllocateDescriptorSets</c> grows the pool by one
    /// (within the original budget).
    /// </summary>
    public DescriptorSet Acquire(VkDescriptorSetLayout_T* layout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (layout == null) throw new ArgumentNullException(nameof(layout));

        if (_idle.TryGetValue((nint)layout, out Stack<nint>? stack) && stack.Count > 0)
            return new DescriptorSet((VkDescriptorSet_T*)stack.Pop());

        var ai = new VkDescriptorSetAllocateInfo
        {
            sType              = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO,
            descriptorPool     = _pool,
            descriptorSetCount = 1,
            pSetLayouts        = &layout,
        };
        VkDescriptorSet_T* raw = null;
        Vk.vkAllocateDescriptorSets(_device.Handle, &ai, &raw).ThrowIfFailed();
        _allHandles.Add((nint)raw);
        return new DescriptorSet(raw);
    }

    /// <summary>
    /// Returns <paramref name="set"/> to the layout-keyed free-list. The
    /// caller passes the same <paramref name="layout"/> it used to
    /// <see cref="Acquire"/>; mixing layouts is undefined behavior. The
    /// underlying <c>VkDescriptorSet</c> stays alive — no
    /// <c>vkFreeDescriptorSets</c> call here.
    /// </summary>
    public void Release(VkDescriptorSetLayout_T* layout, DescriptorSet set)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (set.IsNull) return;
        if (layout == null) throw new ArgumentNullException(nameof(layout));

        if (!_idle.TryGetValue((nint)layout, out Stack<nint>? stack))
            _idle[(nint)layout] = stack = new Stack<nint>();
        stack.Push((nint)set.Handle);
    }

    /// <summary>
    /// Recycles every set the pool has handed out. All
    /// <see cref="DescriptorSet"/> handles previously acquired become
    /// invalid; this is the cheap "rebuild the per-frame descriptor table"
    /// path.
    /// </summary>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Vk.vkResetDescriptorPool(_device.Handle, _pool, flags: 0).ThrowIfFailed();
        _idle.Clear();
        _allHandles.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_pool != null)
            Vk.vkDestroyDescriptorPool(_device.Handle, _pool, null);
        _idle.Clear();
        _allHandles.Clear();
    }
}
