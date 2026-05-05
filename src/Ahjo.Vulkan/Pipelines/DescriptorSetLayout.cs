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

    public void Dispose()
    {
        if (Handle == null) return;
        Vk.vkDestroyDescriptorSetLayout(DeviceHandle, Handle, null);
    }
}
