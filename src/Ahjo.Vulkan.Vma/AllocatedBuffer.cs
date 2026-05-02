using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan.Vma;

/// <summary>
/// A <c>VkBuffer</c> backed by a VMA <see cref="Allocation"/>. Returned
/// from <c>Allocator.CreateBuffer</c>. Both fields are required to free.
/// </summary>
public readonly record struct AllocatedBuffer(VkBuffer Buffer, Allocation Allocation);
