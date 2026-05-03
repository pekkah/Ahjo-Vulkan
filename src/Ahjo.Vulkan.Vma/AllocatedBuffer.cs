using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan.Vma;

/// <summary>
/// A <c>VkBuffer</c> handle backed by a VMA <see cref="Allocation"/>.
/// Returned from <c>Allocator.CreateBuffer</c>. Both fields are required
/// to free the resource.
/// </summary>
/// <remarks>
/// Vulkan's <c>VkBuffer</c> is a typedef for <c>VkBuffer_T*</c>; ClangSharp
/// elides the typedef and uses the pointer form everywhere. Storing the
/// raw pointer matches the binding shape and stays copy-by-value friendly.
/// </remarks>
public readonly unsafe struct AllocatedBuffer(VkBuffer_T* buffer, Allocation allocation)
{
    public readonly VkBuffer_T* Buffer = buffer;
    public readonly Allocation Allocation = allocation;

    public bool IsNull => Buffer == null;
}
