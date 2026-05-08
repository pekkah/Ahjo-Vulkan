using System.Diagnostics;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Wraps one or more <c>VkDescriptorPool</c>s with per-layout free-lists
/// so descriptor-set <see cref="Acquire"/> / <see cref="Release"/> stays
/// O(1) and allocation-free after warmup. One pool per
/// <c>(thread × use case)</c> per Vulkan's external-sync rules: a single
/// <see cref="DescriptorSetPool"/> is not safe to <see cref="Acquire"/>
/// from multiple threads concurrently.
/// </summary>
/// <remarks>
/// <para><b>Lifetime contract.</b> The pool itself is policy-free — the
/// caller decides whether the <c>VkDescriptorSet</c>s it hands out live
/// for one frame, one scene, or the whole app. The wrapper drives the
/// pool both ways:</para>
/// <list type="bullet">
///   <item><description><b>Per-frame slot</b> (the
///     <see cref="FrameRing"/> path): the ring constructs one pool per
///     in-flight slot and calls <see cref="Reset"/> on every
///     <see cref="FrameRing.BeginFrame"/>. Sets returned by
///     <see cref="FrameContext.DescriptorSets"/> are valid for exactly
///     one frame; retaining a handle across the next BeginFrame is a
///     use-after-free.</description></item>
///   <item><description><b>Long-lived</b> (texture arrays, bindless
///     resource tables, material descriptors): construct the pool
///     directly, never call <see cref="Reset"/>, and either keep the
///     sets for the whole app or return individual sets through
///     <see cref="Release"/>. Per-frame uniform/storage descriptors
///     should usually flow through
///     <c>CommandRecorder.PushDescriptors</c> instead — that path
///     never allocates a <c>VkDescriptorSet</c>.</description></item>
/// </list>
/// <para><b>Reuse semantics.</b> <see cref="Release"/> does not call
/// <c>vkFreeDescriptorSets</c> — the underlying <c>VkDescriptorSet</c>
/// stays alive and re-enters the layout-keyed free-list, ready for the
/// next <see cref="Acquire"/> with the same layout (the caller binds new
/// resources via <c>vkUpdateDescriptorSets</c> / push-descriptors on
/// re-use). <see cref="Reset"/> calls <c>vkResetDescriptorPool</c> on
/// every chained sub-pool and invalidates every set the pool ever
/// handed out — use it to wipe the per-frame table in one cheap call.</para>
/// <para><b>Auto-grow.</b> When <c>vkAllocateDescriptorSets</c> returns
/// <c>VK_ERROR_OUT_OF_POOL_MEMORY</c> or
/// <c>VK_ERROR_FRAGMENTED_POOL</c>, the pool allocates a fresh
/// <c>VkDescriptorPool</c> with the same <c>maxSets</c> + pool-size
/// template the caller passed at construction and retries the alloc.
/// All sub-pools live until <see cref="Dispose"/>;
/// <see cref="Reset"/> resets every one of them. This matches the
/// engine's <c>DescriptorPoolManager</c> behaviour and lets long-lived
/// bindless tables, hot-reload spikes, and asset surges grow past the
/// original budget without having to re-architect around the pool.
/// Pass <c>growOnExhaustion: false</c> when a budget cap is the right
/// failure mode (debug runs that want to tune <c>maxSets</c>; tests).</para>
/// </remarks>
public sealed unsafe class DescriptorSetPool : IDisposable
{
    private readonly Device                        _device;
    private readonly List<nint>                    _pools = new();
    private readonly uint                          _maxSetsPerPool;
    private readonly VkDescriptorPoolSize[]        _poolSizes;
    private readonly bool                          _growOnExhaustion;
    // Per-layout free-list. Different layouts allocate to different binding
    // shapes, so a set built for layout A can't be returned to a caller
    // asking for layout B.
    private readonly Dictionary<nint, Stack<nint>> _idle       = new();
    private readonly List<nint>                    _allHandles = new();
    private bool                                   _disposed;

    /// <summary>Total <c>VkDescriptorSet</c>s ever allocated across every chained sub-pool.</summary>
    public int AllocatedCount => _allHandles.Count;

    /// <summary>Number of chained sub-pools (always ≥ 1 after construction).</summary>
    public int PoolCount => _pools.Count;

    /// <summary>
    /// Creates a pool sized for <paramref name="maxSets"/> total live sets
    /// across the descriptor-type budget given by <paramref name="poolSizes"/>.
    /// When <paramref name="growOnExhaustion"/> is <see langword="true"/>
    /// (default), the pool transparently allocates additional sub-pools
    /// with the same template on <c>OUT_OF_POOL_MEMORY</c> /
    /// <c>FRAGMENTED_POOL</c>.
    /// </summary>
    public DescriptorSetPool(
        Device                              device,
        uint                                maxSets,
        ReadOnlySpan<VkDescriptorPoolSize>  poolSizes,
        bool                                growOnExhaustion = true)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (poolSizes.IsEmpty)
            throw new ArgumentException("poolSizes must contain at least one entry.", nameof(poolSizes));
        _device           = device;
        _maxSetsPerPool   = maxSets;
        _poolSizes        = poolSizes.ToArray();
        _growOnExhaustion = growOnExhaustion;

        _pools.Add(CreatePool());
    }

    /// <summary>
    /// Hands out a descriptor set built against <paramref name="layout"/>.
    /// Pops <paramref name="layout"/>'s idle stack when available;
    /// otherwise <c>vkAllocateDescriptorSets</c> grows the current
    /// sub-pool by one. On exhaustion (<c>OUT_OF_POOL_MEMORY</c> /
    /// <c>FRAGMENTED_POOL</c>) and when growth is enabled, allocates a
    /// fresh sub-pool with the original template and retries.
    /// </summary>
    public DescriptorSet Acquire(VkDescriptorSetLayout_T* layout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (layout == null) throw new ArgumentNullException(nameof(layout));

        if (_idle.TryGetValue((nint)layout, out Stack<nint>? stack) && stack.Count > 0)
            return new DescriptorSet((VkDescriptorSet_T*)stack.Pop(), layout);

        VkDescriptorSet_T* raw = AllocateFromCurrentPool(layout, out VkResult result);
        if (raw != null)
        {
            _allHandles.Add((nint)raw);
            return new DescriptorSet(raw, layout);
        }

        // Exhaustion is the only retry-able failure: a fresh sub-pool
        // built from the same template guarantees the requested binding
        // shape fits, and a brand-new pool can't already be fragmented.
        if (_growOnExhaustion && IsExhaustion(result))
        {
            _pools.Add(CreatePool());
            raw = AllocateFromCurrentPool(layout, out result);
            if (raw != null)
            {
                _allHandles.Add((nint)raw);
                return new DescriptorSet(raw, layout);
            }
        }

        // Neither retry path produced a set — surface the original failure.
        result.ThrowIfFailed();
        return default; // unreachable: ThrowIfFailed always throws on non-success.
    }

    /// <summary>
    /// Returns <paramref name="set"/> to the layout-keyed free-list. The
    /// underlying <c>VkDescriptorSet</c> stays alive — no
    /// <c>vkFreeDescriptorSets</c> call here. <paramref name="layout"/>
    /// must match the layout the set was allocated against; a debug
    /// build asserts this against the layout the set carries internally,
    /// and the routing is by that carried layout regardless, so a
    /// mismatched layout doesn't silently corrupt a different layout's
    /// free-list.
    /// </summary>
    public void Release(VkDescriptorSetLayout_T* layout, DescriptorSet set)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (set.IsNull) return;
        if (layout == null) throw new ArgumentNullException(nameof(layout));
        Debug.Assert(set.Layout == layout,
            "DescriptorSetPool.Release: layout argument doesn't match the layout the set was acquired with.");
        Debug.Assert(set.Layout != null,
            "DescriptorSetPool.Release: set has no layout — was it constructed via FromRaw rather than Acquire?");

        // Route by the carried layout, not the caller-supplied one. In
        // release builds where the assert is compiled out a buggy caller
        // would otherwise push the set onto the wrong layout's stack and
        // a later Acquire(otherLayout) would hand back a set with the
        // wrong binding shape.
        nint key = set.Layout != null ? (nint)set.Layout : (nint)layout;
        if (!_idle.TryGetValue(key, out Stack<nint>? stack))
            _idle[key] = stack = new Stack<nint>();
        stack.Push((nint)set.Handle);
    }

    /// <summary>
    /// Recycles every set the pool has handed out across all chained
    /// sub-pools. All <see cref="DescriptorSet"/> handles previously
    /// acquired become invalid; this is the cheap "rebuild the per-frame
    /// descriptor table" path. Sub-pool count is preserved — Reset does
    /// not free the chained sub-pools, since the next frame will fill
    /// them again to the same shape.
    /// </summary>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        for (int i = 0; i < _pools.Count; i++)
            Vk.vkResetDescriptorPool(_device.Handle, (VkDescriptorPool_T*)_pools[i], flags: 0).ThrowIfFailed();
        _idle.Clear();
        _allHandles.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        for (int i = 0; i < _pools.Count; i++)
        {
            if (_pools[i] != 0)
                Vk.vkDestroyDescriptorPool(_device.Handle, (VkDescriptorPool_T*)_pools[i], null);
        }
        _pools.Clear();
        _idle.Clear();
        _allHandles.Clear();
    }

    private nint CreatePool()
    {
        fixed (VkDescriptorPoolSize* pSizes = _poolSizes)
        {
            var ci = new VkDescriptorPoolCreateInfo
            {
                sType         = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO,
                flags         = 0,
                maxSets       = _maxSetsPerPool,
                poolSizeCount = (uint)_poolSizes.Length,
                pPoolSizes    = pSizes,
            };
            VkDescriptorPool_T* raw = null;
            Vk.vkCreateDescriptorPool(_device.Handle, &ci, null, &raw).ThrowIfFailed();
            return (nint)raw;
        }
    }

    private VkDescriptorSet_T* AllocateFromCurrentPool(VkDescriptorSetLayout_T* layout, out VkResult result)
    {
        var current = (VkDescriptorPool_T*)_pools[^1];
        var ai = new VkDescriptorSetAllocateInfo
        {
            sType              = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO,
            descriptorPool     = current,
            descriptorSetCount = 1,
            pSetLayouts        = &layout,
        };
        VkDescriptorSet_T* raw = null;
        result = Vk.vkAllocateDescriptorSets(_device.Handle, &ai, &raw);
        return result == VkResult.VK_SUCCESS ? raw : null;
    }

    private static bool IsExhaustion(VkResult result) =>
        result == VkResult.VK_ERROR_OUT_OF_POOL_MEMORY ||
        result == VkResult.VK_ERROR_FRAGMENTED_POOL;
}
