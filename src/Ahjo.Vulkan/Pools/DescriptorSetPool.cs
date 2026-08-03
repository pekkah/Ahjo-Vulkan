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
/// failure mode (debug runs that want to tune <c>maxSets</c>; tests).
/// A variable-descriptor count makes the requested binding shape a
/// function of the <see cref="Acquire(VkDescriptorSetLayout_T*, uint)"/>
/// argument rather than of the layout alone, so the sub-pool template must
/// be sized for the sum, over the live sets, of <i>all</i> descriptors of
/// that type — the variable binding contributing the count passed to
/// <c>Acquire</c> rather than its declared maximum, and fixed bindings of
/// the same type contributing their declared counts. Not
/// <c>maxSets × layoutDeclaredCount</c>. The pre-flight guard in
/// <see cref="Acquire(VkDescriptorSetLayout_T*, uint)"/> rejects only the
/// counts exceeding the largest per-type total in the template — it cannot
/// know which descriptor type the variable binding will draw from, so it is
/// a necessary condition, never a sufficient one.</para>
/// </remarks>
public sealed unsafe class DescriptorSetPool : IDisposable
{
    private readonly Device                        _device;
    private readonly List<nint>                    _pools = new();
    private readonly uint                          _maxSetsPerPool;
    private readonly VkDescriptorPoolSize[]        _poolSizes;
    private readonly bool                          _growOnExhaustion;
    private readonly VkDescriptorPoolCreateFlagBits _poolFlags;
    // Free-list keyed by (layout, variable-descriptor count). Different
    // layouts allocate to different binding shapes, so a set built for
    // layout A can't be returned to a caller asking for layout B — and
    // different variable counts are different binding shapes too: a set
    // allocated with count 4 physically holds four descriptors in its
    // variable binding, so handing it to a caller asking for 64 hands back
    // the wrong shape under one layout.
    private readonly Dictionary<IdleKey, Stack<nint>> _idle       = new();
    private readonly List<nint>                       _allHandles = new();
    // The largest number of descriptors this pool's template holds for any
    // single descriptor type — per-TYPE totals, because vkCreateDescriptorPool
    // sums duplicate same-type entries in poolSizes. Computed once in the
    // constructor so the Acquire budget guard is one comparison, not a scan.
    private readonly uint                             _maxPerTypeDescriptorTotal;
    private bool                                      _disposed;

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
    /// <param name="poolSizes">
    /// Per-descriptor-type budget each sub-pool is created with. Size it for
    /// the total number of descriptors of that type across the live sets, not
    /// per set: with
    /// <see cref="Acquire(VkDescriptorSetLayout_T*, uint)"/> the budget is
    /// consumed at the variable count the caller passes rather than at the
    /// layout's declared maximum, so an entry must cover the sum over the
    /// live sets of both those counts and the declared counts of any fixed
    /// bindings of the same type. Duplicate same-type entries are summed, both
    /// here and by <c>vkCreateDescriptorPool</c>. A variable count larger than
    /// the biggest per-type total makes
    /// <see cref="Acquire(VkDescriptorSetLayout_T*, uint)"/> throw before it
    /// reaches Vulkan.
    /// </param>
    /// <param name="updateAfterBind">
    /// Set <see langword="true"/> to create the pool with
    /// <c>VK_DESCRIPTOR_POOL_CREATE_UPDATE_AFTER_BIND_BIT</c>. Required
    /// (VUID-VkDescriptorSetAllocateInfo-pSetLayouts-03044) whenever the
    /// caller will allocate sets against a layout built with
    /// <see cref="DescriptorSetLayoutDescription.UpdateAfterBindPool"/>;
    /// a flag mismatch is undefined behaviour — hardware drivers tend to
    /// tolerate it, but SwiftShader SIGSEGVs inside vkAllocateDescriptorSets.
    /// </param>
    public DescriptorSetPool(
        Device                              device,
        uint                                maxSets,
        ReadOnlySpan<VkDescriptorPoolSize>  poolSizes,
        bool                                growOnExhaustion = true,
        bool                                updateAfterBind  = false)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentOutOfRangeException.ThrowIfZero(maxSets, nameof(maxSets));
        if (poolSizes.IsEmpty)
            throw new ArgumentException("poolSizes must contain at least one entry.", nameof(poolSizes));
        // Per-type totals, not per-entry maxima: vkCreateDescriptorPool sums
        // duplicate same-type entries ("if multiple VkDescriptorPoolSize
        // structures containing the same descriptor type appear in pPoolSizes
        // then the pool will be created with enough storage for the total
        // number of descriptors of each type"), and only MUTABLE_EXT restricts
        // repeats (VUID-VkDescriptorPoolCreateInfo-pPoolSizes-04787). A
        // template of [{STORAGE_BUFFER, 64}, {STORAGE_BUFFER, 64}] therefore
        // holds 128 of them. O(n²) over a handful of entries, once, at setup.
        uint maxPerTypeTotal = 0;
        for (int i = 0; i < poolSizes.Length; i++)
        {
            ulong total = 0;
            for (int j = 0; j < poolSizes.Length; j++)
            {
                if (poolSizes[j].type == poolSizes[i].type)
                    total += poolSizes[j].descriptorCount;
            }
            // Clamp rather than wrap: an absurd template must not overflow into
            // a small bound and start rejecting legitimate requests.
            uint clamped = total > uint.MaxValue ? uint.MaxValue : (uint)total;
            if (clamped > maxPerTypeTotal) maxPerTypeTotal = clamped;
        }

        _device                     = device;
        _maxSetsPerPool             = maxSets;
        _maxPerTypeDescriptorTotal  = maxPerTypeTotal;
        _poolSizes                  = poolSizes.ToArray();
        _growOnExhaustion           = growOnExhaustion;
        _poolFlags                  = updateAfterBind
            ? VkDescriptorPoolCreateFlagBits.VK_DESCRIPTOR_POOL_CREATE_UPDATE_AFTER_BIND_BIT
            : 0;

        // Pre-grow so the Add after the native call below can't OOM
        // and orphan the freshly-created VkDescriptorPool.
        _pools.EnsureCapacity(1);
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
    /// <remarks>
    /// For a layout whose highest binding carries
    /// <see cref="DescriptorBindingFlags.VariableDescriptorCount"/>, this
    /// overload allocates that binding with an effective count of
    /// <b>zero</b> — no
    /// <c>VkDescriptorSetVariableDescriptorCountAllocateInfo</c> is chained,
    /// and every subsequent write to it fails
    /// (VUID-VkWriteDescriptorSet-dstBinding-00316,
    /// VUID-VkWriteDescriptorSet-dstArrayElement-00321). The wrapper cannot
    /// detect this: Vulkan exposes no way to read a layout's binding flags
    /// back from the handle. Use
    /// <see cref="Acquire(VkDescriptorSetLayout_T*, uint)"/> for such a
    /// layout.
    /// </remarks>
    public DescriptorSet Acquire(VkDescriptorSetLayout_T* layout)
        => Acquire(layout, variableDescriptorCount: 0);

    /// <summary>
    /// Hands out a descriptor set built against <paramref name="layout"/>
    /// whose variable-count binding holds
    /// <paramref name="variableDescriptorCount"/> descriptors, by chaining
    /// <c>VkDescriptorSetVariableDescriptorCountAllocateInfo</c> with
    /// <c>descriptorSetCount = 1</c> onto the allocation
    /// (VUID-VkDescriptorSetVariableDescriptorCountAllocateInfo-descriptorSetCount-03045
    /// requires it to match the allocate-info's own set count). Free-list,
    /// auto-grow and reuse behave exactly as in
    /// <see cref="Acquire(VkDescriptorSetLayout_T*)"/>.
    /// </summary>
    /// <remarks>
    /// <para>The count applies to the one binding carrying
    /// <see cref="DescriptorBindingFlags.VariableDescriptorCount"/>, which
    /// Vulkan requires to be the binding with the highest binding number in
    /// the set
    /// (VUID-VkDescriptorSetLayoutBindingFlagsCreateInfo-pBindingFlags-03004)
    /// — which is why a single <see cref="uint"/> is the whole story.</para>
    /// <para>It must not exceed that binding's declared
    /// <see cref="DescriptorBinding.Count"/>
    /// (VUID-VkDescriptorSetAllocateInfo-pSetLayouts-09380); the wrapper
    /// cannot check that, because the declared count is not readable back
    /// from a <c>VkDescriptorSetLayout</c> handle.</para>
    /// <para>The device must have enabled
    /// <c>descriptorBindingVariableDescriptorCount</c>. <b>The pool cannot
    /// check this</b>, and the violation is not the pool's to report: without
    /// the feature the flag already breaks
    /// VUID-VkDescriptorSetLayoutBindingFlagsCreateInfo-descriptorBindingVariableDescriptorCount-03014
    /// at <c>vkCreateDescriptorSetLayout</c>, upstream of any allocation.
    /// <c>VK_LAYER_KHRONOS_validation</c> is the oracle there — a VUID
    /// violation is undefined behaviour, not a mandated <c>VkResult</c>
    /// failure, and drivers have been measured accepting such a layout
    /// silently.</para>
    /// <para>Passing <c>0</c> emits no chain and is exactly
    /// <see cref="Acquire(VkDescriptorSetLayout_T*)"/>.</para>
    /// <para>Sets acquired with different counts occupy different free-list
    /// buckets, because a recycled set physically holds the count it was
    /// allocated with. A caller who varies the count without bound therefore
    /// grows the pool's bookkeeping every frame — a variable-count set is a
    /// long-lived heap table, not per-frame scratch. Per-frame descriptors
    /// belong on <c>CommandRecorder.PushDescriptors</c>.</para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="variableDescriptorCount"/> exceeds the largest
    /// per-descriptor-type total in the pool's <c>poolSizes</c> template
    /// (duplicate same-type entries are summed, as
    /// <c>vkCreateDescriptorPool</c> sums them), so no sub-pool built from
    /// that template could satisfy it for any descriptor type.
    /// </exception>
    public DescriptorSet Acquire(VkDescriptorSetLayout_T* layout, uint variableDescriptorCount)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (layout == null) throw new ArgumentNullException(nameof(layout));
        // NECESSARY-condition guard, and it must stay one. The pool is handed a
        // raw VkDescriptorSetLayout, and Vulkan exposes no way to read a
        // layout's binding flags — or its variable binding's descriptor type —
        // back from the handle. So the strongest sound test is against the
        // largest per-type total in the template: if the request exceeds even
        // that, then whichever type the variable binding turns out to be, no
        // sub-pool built from this template can hold it, and the auto-grow
        // retry below would keep building sub-pools that also cannot. Do NOT
        // "tighten" this into a per-type check — the type is not knowable here,
        // and a sufficient-condition check would reject legal requests.
        if (variableDescriptorCount > _maxPerTypeDescriptorTotal)
            throw new ArgumentOutOfRangeException(
                nameof(variableDescriptorCount), variableDescriptorCount,
                $"This pool's poolSizes template holds at most " +
                $"{_maxPerTypeDescriptorTotal} descriptors of any single descriptor type, so no " +
                $"sub-pool built from it can satisfy a variable-descriptor count of " +
                $"{variableDescriptorCount}. Size poolSizes for the sum, over the live sets, of " +
                $"all descriptors of that type.");

        var key = new IdleKey((nint)layout, variableDescriptorCount);
        if (_idle.TryGetValue(key, out Stack<nint>? stack) && stack.Count > 0)
            return new DescriptorSet((VkDescriptorSet_T*)stack.Pop(), layout, variableDescriptorCount);

        // Pre-grow _allHandles so each Add after a successful native
        // alloc can't OOM and orphan the just-acquired VkDescriptorSet.
        _allHandles.EnsureCapacity(_allHandles.Count + 1);

        VkDescriptorSet_T* raw = AllocateFromCurrentPool(layout, variableDescriptorCount, out VkResult result);
        if (raw != null)
        {
            _allHandles.Add((nint)raw);
            return new DescriptorSet(raw, layout, variableDescriptorCount);
        }

        // Exhaustion is the only retry-able failure: a fresh sub-pool built
        // from the same template fits the requested binding shape, and a
        // brand-new pool can't already be fragmented. The guard above is
        // what keeps that true once the shape depends on a runtime count —
        // it rejects the counts no template entry could hold, before any
        // sub-pool is built for them.
        if (_growOnExhaustion && IsExhaustion(result))
        {
            _pools.EnsureCapacity(_pools.Count + 1);
            _pools.Add(CreatePool());
            raw = AllocateFromCurrentPool(layout, variableDescriptorCount, out result);
            if (raw != null)
            {
                _allHandles.Add((nint)raw);
                return new DescriptorSet(raw, layout, variableDescriptorCount);
            }
        }

        // Neither retry path produced a set — surface the original failure.
        result.ThrowIfFailed();
        return default; // unreachable: ThrowIfFailed always throws on non-success.
    }

    /// <summary>
    /// Returns <paramref name="set"/> to the free-list bucket it came from
    /// — keyed by layout <i>and</i> by the variable-descriptor count the set
    /// was allocated with, since a recycled set physically holds that count.
    /// The
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
        if (AhjoValidation.IsEnabled)
        {
            // Null-layout check first: a FromRaw set has set.Layout == null,
            // which would also fail the != layout check below (layout is
            // non-null here) — but with the more specific message. Fail is
            // [DoesNotReturn], so order decides which diagnostic the caller sees.
            if (set.Layout == null)
                AhjoValidation.Fail("DescriptorSetPool",
                    "Release: set has no layout — was it constructed via FromRaw rather than Acquire?");
            if (set.Layout != layout)
                AhjoValidation.Fail("DescriptorSetPool",
                    "Release: layout argument doesn't match the layout the set was acquired with.");
        }

        // Route by the carried layout, not the caller-supplied one. In
        // release builds where the assert is compiled out a buggy caller
        // would otherwise push the set onto the wrong layout's stack and
        // a later Acquire(otherLayout) would hand back a set with the
        // wrong binding shape.
        var key = new IdleKey(
            set.Layout != null ? (nint)set.Layout : (nint)layout,
            set.VariableDescriptorCount);
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
    /// them again to the same shape. The per-bucket idle free-lists are
    /// emptied but their <see cref="Stack{T}"/> instances (and backing
    /// arrays) are retained, so a Release-then-Reset frame loop stays
    /// allocation-free (issue 114).
    /// </summary>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        for (int i = 0; i < _pools.Count; i++)
            Vk.vkResetDescriptorPool(_device.Handle, (VkDescriptorPool_T*)_pools[i], flags: 0).ThrowIfFailed();
        // Empty each bucket's idle stack but KEEP the Stack instances (and their
        // backing arrays) and the dictionary entries so a Release-then-Reset frame
        // loop stays allocation-free. vkResetDescriptorPool above invalidates the
        // contained VkDescriptorSet handles, so the stacks must be emptied — but
        // the layout-pointer + count keys stay valid across resets, so the Stack
        // objects are reusable (issue 114). Enumerating _idle here only mutates the
        // value objects (Stack.Clear), never the dictionary structure, so the struct
        // Dictionary.Enumerator stays valid and the loop allocates nothing.
        foreach (KeyValuePair<IdleKey, Stack<nint>> entry in _idle)
            entry.Value.Clear();
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
                flags         = (uint)_poolFlags,
                maxSets       = _maxSetsPerPool,
                poolSizeCount = (uint)_poolSizes.Length,
                pPoolSizes    = pSizes,
            };
            VkDescriptorPool_T* raw = null;
            Vk.vkCreateDescriptorPool(_device.Handle, &ci, null, &raw).ThrowIfFailed();
            return (nint)raw;
        }
    }

    private VkDescriptorSet_T* AllocateFromCurrentPool(
        VkDescriptorSetLayout_T* layout,
        uint                     variableDescriptorCount,
        out VkResult             result)
    {
        var current = (VkDescriptorPool_T*)_pools[^1];
        var ai = new VkDescriptorSetAllocateInfo
        {
            sType              = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO,
            descriptorPool     = current,
            descriptorSetCount = 1,
            pSetLayouts        = &layout,
        };
        // Both locals live in this stack frame, and this is also the frame
        // that makes the vkAllocateDescriptorSets call below — so no pinning
        // is needed and nothing reaches the managed heap.
        uint count = variableDescriptorCount;
        var vc = new VkDescriptorSetVariableDescriptorCountAllocateInfo
        {
            sType              = VkStructureType.VK_STRUCTURE_TYPE_DESCRIPTOR_SET_VARIABLE_DESCRIPTOR_COUNT_ALLOCATE_INFO,
            descriptorSetCount = 1,     // must equal ai.descriptorSetCount — VUID-VkDescriptorSetVariableDescriptorCountAllocateInfo-descriptorSetCount-03045
            pDescriptorCounts  = &count,
        };
        // The != 0 condition is load-bearing, not an optimization: the
        // validation layer's check is "was a chain provided", not "was a
        // non-zero count provided", so chaining a zero suppresses
        // WARNING-CoreValidation-AllocateDescriptorSets-VariableDescriptorCount
        // — the only diagnostic a caller who forgot the count ever gets, and
        // one this wrapper provably cannot replace (binding flags are not
        // readable back from a VkDescriptorSetLayout handle).
        if (variableDescriptorCount != 0) ai.pNext = &vc;

        VkDescriptorSet_T* raw = null;
        result = Vk.vkAllocateDescriptorSets(_device.Handle, &ai, &raw);
        return result == VkResult.VK_SUCCESS ? raw : null;
    }

    private static bool IsExhaustion(VkResult result) =>
        result == VkResult.VK_ERROR_OUT_OF_POOL_MEMORY ||
        result == VkResult.VK_ERROR_FRAGMENTED_POOL;

    /// <summary>
    /// Free-list bucket identity: a recycled set physically holds the
    /// variable-descriptor count it was allocated with, so a set acquired
    /// with count 4 must never be handed to a caller asking for count 64.
    /// A <c>readonly record struct</c> implements <see cref="IEquatable{T}"/>,
    /// so <c>EqualityComparer&lt;IdleKey&gt;.Default</c> devirtualizes and
    /// never boxes — the dictionary lookup stays allocation-free.
    /// </summary>
    private readonly record struct IdleKey(nint Layout, uint VariableDescriptorCount);
}
