using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// A <c>VkImageView</c> bound to the device that created it. Lightweight —
/// two pointers; <see cref="Dispose"/> calls <c>vkDestroyImageView</c>.
/// </summary>
/// <remarks>
/// Holds a raw <c>VkDevice_T*</c> rather than a <see cref="Device"/>
/// reference so the struct stays <c>unmanaged</c> and satisfies the
/// <see cref="IVulkanHandle{TSelf}"/> constraint. The pointer is valid as
/// long as the parent <see cref="Device"/> hasn't been disposed; viewers
/// must outlive their image and not outlive their device.
/// <c>default(ImageView)</c> is a legal null handle: <see cref="IsNull"/>
/// returns <see langword="true"/> and <see cref="Dispose"/> is a no-op.
/// </remarks>
public readonly unsafe struct ImageView : IVulkanHandle<ImageView>, IDisposable
{
    public readonly VkImageView_T* Handle;
    internal readonly VkDevice_T*  DeviceHandle;

    internal ImageView(VkImageView_T* handle, VkDevice_T* device)
    {
        Handle       = handle;
        DeviceHandle = device;
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_IMAGE_VIEW;

    public static ImageView FromRaw(nint handle) => new((VkImageView_T*)handle, null);

    public ulong RawHandle => (ulong)Handle;

    public bool IsNull => Handle == null;

    public void Dispose()
    {
        if (Handle == null) return;
        Vk.vkDestroyImageView(DeviceHandle, Handle, null);
    }
}
