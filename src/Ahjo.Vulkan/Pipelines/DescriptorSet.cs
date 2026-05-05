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

    internal DescriptorSet(VkDescriptorSet_T* handle) { Handle = handle; }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_DESCRIPTOR_SET;
    public static DescriptorSet FromRaw(nint handle) => new((VkDescriptorSet_T*)handle);
    public ulong RawHandle => (ulong)Handle;
    public bool IsNull => Handle == null;
}
