namespace Ahjo.Vulkan.Vma;

/// <summary>
/// One VMA-managed memory allocation. Returned alongside a <c>VkBuffer</c>
/// or <c>VkImage</c> from the allocator and required to free the resource.
/// </summary>
/// <remarks>
/// Pure handle — VMA owns the underlying state. <c>default(Allocation)</c>
/// is null. Pass to <c>Allocator.Destroy*</c> to release.
/// </remarks>
public readonly struct Allocation
{
    internal readonly nint Handle;

    internal Allocation(nint handle)
    {
        Handle = handle;
    }

    public bool IsNull => Handle == 0;
}
