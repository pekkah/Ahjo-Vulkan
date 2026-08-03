using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// A <c>VkDescriptorSet</c> handed out by <see cref="DescriptorSetPool"/>.
/// Lifetime is owned by the pool — return through
/// <see cref="DescriptorSetPool.Release"/> when the set is no longer in
/// use, or call <see cref="DescriptorSetPool.Reset"/> to recycle every
/// allocated set in one batch.
/// </summary>
/// <remarks>
/// <c>default(DescriptorSet)</c> is a legal null handle. The struct is two
/// pointers (the set and the layout it was allocated against) plus the
/// variable-descriptor count it was allocated with. Distinct from
/// the dynamic update-template / push-descriptor path (#17 + follow-ups),
/// which never allocates a <c>VkDescriptorSet</c> at all — those flow
/// through <c>CommandRecorder.PushDescriptors</c> when that lands.
/// </remarks>
public readonly unsafe struct DescriptorSet : IVulkanHandle<DescriptorSet>
{
    public readonly VkDescriptorSet_T* Handle;
    // The layout the pool used to allocate this set. Carried so
    // DescriptorSetPool.Release can assert it matches the layout the
    // caller is claiming, instead of trusting the caller and silently
    // routing to the wrong layout-keyed free-list. Null for
    // FromRaw-constructed instances (debug-name attachment etc.) — those
    // shouldn't be passed to Release in the first place.
    internal readonly VkDescriptorSetLayout_T* Layout;
    // The variable-descriptor count this set was allocated with — the value
    // that went into VkDescriptorSetVariableDescriptorCountAllocateInfo::
    // pDescriptorCounts, or 0 when no chain was emitted. Carried for the same
    // reason as Layout: DescriptorSetPool.Release has to route the set back to
    // the free-list bucket it came from, and a set physically holds the count
    // it was allocated with (the driver checks every write against it —
    // VUID-VkWriteDescriptorSet-dstArrayElement-00321). 0 for FromRaw-
    // constructed instances, which is indistinguishable from a genuine zero;
    // that ambiguity is why this stays internal.
    internal readonly uint VariableDescriptorCount;

    internal DescriptorSet(
        VkDescriptorSet_T*       handle,
        VkDescriptorSetLayout_T* layout                  = null,
        uint                     variableDescriptorCount = 0)
    {
        Handle                  = handle;
        Layout                  = layout;
        VariableDescriptorCount = variableDescriptorCount;
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_DESCRIPTOR_SET;
    public static DescriptorSet FromRaw(nint handle) => new((VkDescriptorSet_T*)handle);
    public ulong RawHandle => (ulong)Handle;
    public bool IsNull => Handle == null;

    /// <summary>
    /// Always <see langword="false"/>: <see cref="DescriptorSetPool"/> owns
    /// the <c>VkDescriptorSet</c>'s lifetime. The <see cref="Layout"/>
    /// pointer is release-routing metadata, not ownership.
    /// </summary>
    public bool OwnsHandle => false;
}
