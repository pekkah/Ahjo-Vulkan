using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Wrapper handle for a <c>VkDescriptorSetLayout</c>. <c>readonly struct</c>
/// + <see cref="IDisposable"/>; layouts are typically built once and held
/// for the program's lifetime, but supporting <see cref="Dispose"/> keeps
/// the API regular.
/// </summary>
/// <remarks>
/// <c>default(DescriptorSetLayout)</c> is a legal null handle and
/// <see cref="Dispose"/> is a no-op on it. Holds the owning
/// <c>VkDevice_T*</c> so disposal doesn't require the caller to thread the
/// device through.
/// </remarks>
public readonly unsafe struct DescriptorSetLayout : IVulkanHandle<DescriptorSetLayout>, IDisposable
{
    public readonly VkDescriptorSetLayout_T* Handle;
    internal readonly VkDevice_T* DeviceHandle;

    internal DescriptorSetLayout(VkDescriptorSetLayout_T* handle, VkDevice_T* device)
    {
        Handle       = handle;
        DeviceHandle = device;
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_DESCRIPTOR_SET_LAYOUT;
    public static DescriptorSetLayout FromRaw(nint handle) => new((VkDescriptorSetLayout_T*)handle, null);
    public ulong RawHandle => (ulong)Handle;
    public bool IsNull => Handle == null;

    /// <inheritdoc/>
    public bool OwnsHandle => DeviceHandle != null;

    /// <summary>
    /// Builds a <see cref="DescriptorTemplate{T}"/> for the long-lived
    /// <c>vkUpdateDescriptorSetWithTemplate</c> path. The template's update
    /// entries are derived from <typeparamref name="T"/>'s field offsets
    /// matched against <paramref name="bindings"/> in declaration order;
    /// <paramref name="bindings"/> should be the same span used to build
    /// this layout.
    /// </summary>
    public DescriptorTemplate<T> CreateUpdateTemplate<T>(ReadOnlySpan<DescriptorBinding> bindings)
        where T : unmanaged
    {
        // FromRaw'd layouts carry no DeviceHandle; the template create call
        // would dispatch through a null device. Fail loudly instead.
        if (DeviceHandle == null)
            throw new InvalidOperationException(
                "DescriptorSetLayout.CreateUpdateTemplate requires an owning device; " +
                "a FromRaw-constructed (borrowed) layout has none.");
        return DescriptorTemplateBuilder.CreateForSet<T>(DeviceHandle, Handle, bindings);
    }

    public void Dispose()
    {
        if (Handle == null) return;
        // FromRaw produces a borrowed handle with no DeviceHandle — the
        // caller owns the lifetime; calling vkDestroyDescriptorSetLayout
        // with a null device handle would crash on every loader.
        if (!OwnsHandle) return;
        Vk.vkDestroyDescriptorSetLayout(DeviceHandle, Handle, null);
    }
}
