using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Wrapper handle for a <c>VkPhysicalDevice</c>. Owned by an
/// <see cref="Instance"/> and produced exclusively by
/// <see cref="Instance.PickPhysicalDevice"/>; <see cref="Instance"/> caches
/// one managed instance per native handle, so reference equality matches
/// "same GPU."
/// </summary>
/// <remarks>
/// Owner-class shape (sealed class) rather than struct + <c>IVulkanHandle&lt;&gt;</c>:
/// physical devices are created 1–3 times per process and are never debug-named
/// or pooled, so the generic-dispatch infrastructure that
/// <c>IVulkanHandle&lt;TSelf&gt;</c> exists for is inert here. Resource handles
/// (Buffer, Image, …) keep the struct + interface convention.
/// </remarks>
public sealed unsafe class PhysicalDevice
{
    internal readonly VkPhysicalDevice_T* Handle;
    internal readonly Instance            Instance;

    internal PhysicalDevice(Instance instance, VkPhysicalDevice_T* handle)
    {
        Instance = instance;
        Handle   = handle;
    }

    public ulong RawHandle => (ulong)(nint)Handle;
    public bool  IsNull    => Handle == null;
    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_PHYSICAL_DEVICE;
}
