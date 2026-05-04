using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Wrapper handle for a <c>VkPhysicalDevice</c>. Owned by a
/// <see cref="Instance"/>; has no <c>vkDestroyPhysicalDevice</c> call, so no
/// <see cref="System.IDisposable"/>, no finalizer, copy-by-value.
/// </summary>
public readonly unsafe struct PhysicalDevice : IVulkanHandle<PhysicalDevice>
{
    internal readonly VkPhysicalDevice_T* Handle;

    public PhysicalDevice(VkPhysicalDevice_T* handle) => Handle = handle;

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_PHYSICAL_DEVICE;

    public static PhysicalDevice FromRaw(nint handle) => new((VkPhysicalDevice_T*)handle);

    public ulong RawHandle => (ulong)(nint)Handle;

    public bool IsNull => Handle == null;
}
