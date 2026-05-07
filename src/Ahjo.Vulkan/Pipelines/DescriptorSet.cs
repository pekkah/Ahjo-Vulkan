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
/// <c>default(DescriptorSet)</c> is a legal null handle. Distinct from
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

    internal DescriptorSet(VkDescriptorSet_T* handle, VkDescriptorSetLayout_T* layout = null)
    {
        Handle = handle;
        Layout = layout;
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_DESCRIPTOR_SET;
    public static DescriptorSet FromRaw(nint handle) => new((VkDescriptorSet_T*)handle);
    public ulong RawHandle => (ulong)Handle;
    public bool IsNull => Handle == null;
}
