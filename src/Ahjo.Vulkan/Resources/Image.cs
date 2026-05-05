using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Wrapper handle for a <c>VkImage</c> paired with its VMA backing
/// <see cref="Allocation"/>. Both halves are required to free the resource;
/// pairing them in one struct keeps the lifetime contract local.
/// </summary>
public readonly unsafe struct Image : IVulkanHandle<Image>
{
    public readonly VkImage_T* Raw;
    public readonly Allocation Allocation;

    internal Image(VkImage_T* raw, Allocation allocation)
    {
        Raw        = raw;
        Allocation = allocation;
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_IMAGE;

    public static Image FromRaw(nint handle) => new((VkImage_T*)handle, default);

    public ulong RawHandle => (ulong)Raw;

    public bool IsNull => Raw == null;
}
