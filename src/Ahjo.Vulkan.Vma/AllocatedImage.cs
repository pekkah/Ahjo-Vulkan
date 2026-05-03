using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan.Vma;

/// <summary>
/// A <c>VkImage</c> handle backed by a VMA <see cref="Allocation"/>.
/// Returned from <c>Allocator.CreateImage</c>. Both fields are required
/// to free the resource.
/// </summary>
/// <remarks>
/// See <see cref="AllocatedBuffer"/> for the rationale on storing the
/// raw <c>VkImage_T*</c> pointer.
/// </remarks>
public readonly unsafe struct AllocatedImage(VkImage_T* image, Allocation allocation)
{
    public readonly VkImage_T* Image = image;
    public readonly Allocation Allocation = allocation;

    public bool IsNull => Image == null;
}
