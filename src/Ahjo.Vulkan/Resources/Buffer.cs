using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Wrapper handle for a <c>VkBuffer</c> paired with its VMA backing
/// <see cref="Allocation"/>. Both halves are required to free the resource;
/// pairing them in one struct keeps the lifetime contract local.
/// </summary>
/// <remarks>
/// Placeholder: the API surface (creation, mapping, descriptor wiring) lands
/// alongside <see cref="Allocator"/>. This type exists now to anchor the
/// <see cref="IVulkanHandle{TSelf}"/> contract end to end.
/// </remarks>
public readonly unsafe struct Buffer : IVulkanHandle<Buffer>
{
    public readonly VkBuffer_T* Raw;
    public readonly Allocation Allocation;

    internal Buffer(VkBuffer_T* raw, Allocation allocation)
    {
        Raw = raw;
        Allocation = allocation;
    }

    public static VkObjectType ObjectType => VkObjectType.VK_OBJECT_TYPE_BUFFER;

    public static Buffer FromRaw(nint handle) => new((VkBuffer_T*)handle, default);

    public ulong RawHandle => (ulong)Raw;

    public bool IsNull => Raw == null;
}
