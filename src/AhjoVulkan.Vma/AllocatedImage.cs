using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan.Vma;

/// <summary>
/// A <c>VkImage</c> backed by a VMA <see cref="Allocation"/>. Returned
/// from <c>Allocator.CreateImage</c>. Both fields are required to free.
/// </summary>
public readonly record struct AllocatedImage(VkImage Image, Allocation Allocation);
