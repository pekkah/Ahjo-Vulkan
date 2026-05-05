namespace Ahjo.Vulkan;

/// <summary>
/// Inputs to <see cref="Allocator.CreateBuffer"/>. Maps onto
/// <c>VkBufferCreateInfo</c> minus the boilerplate (<c>sType</c>,
/// <c>pNext</c>, <c>queueFamilyIndexCount</c>/<c>pQueueFamilyIndices</c> for
/// the exclusive sharing case the wrapper assumes).
/// </summary>
/// <remarks>
/// <c>sharingMode</c> is hard-coded to <c>VK_SHARING_MODE_EXCLUSIVE</c> for
/// now. Concurrent sharing across multiple queue families is rare, costs
/// more than the implementation typically saves, and can be added behind
/// a dedicated overload when a real consumer needs it.
/// </remarks>
public readonly record struct BufferDescription
{
    /// <summary>Buffer size in bytes. Must be &gt; 0.</summary>
    public ulong Size { get; init; }

    /// <summary>Bitwise-OR of buffer usage bits.</summary>
    public BufferUsage Usage { get; init; }
}
