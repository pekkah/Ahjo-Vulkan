namespace Ahjo.Vulkan;

/// <summary>
/// The <c>VkPhysicalDeviceLimits</c> fields that decide how memory may be sub-allocated —
/// the subset a caller packing resources into one allocation actually has to obey. Read
/// with <see cref="PhysicalDevice.GetMemoryLimits"/>.
/// </summary>
/// <remarks>
/// A narrow accessor rather than the whole limits struct on purpose: the full
/// <c>VkPhysicalDeviceLimits</c> is reachable only through <c>PhysicalDeviceInfo</c>, which
/// is a <c>ref struct</c> that cannot escape the device-picker callback. These five are the
/// ones a memory allocator needs after the device exists.
/// </remarks>
public readonly record struct DeviceMemoryLimits
{
    /// <summary>
    /// The page size within which a linear resource (a buffer, or a
    /// <c>VK_IMAGE_TILING_LINEAR</c> image) and an optimal-tiled image bound to the same
    /// memory ALIAS each other, whatever their byte ranges say. A packer must keep
    /// resources of different tiling classes in different pages of this size, or the two
    /// implicitly share memory with nothing to synchronize them. 1 means no constraint.
    /// </summary>
    public ulong BufferImageGranularity { get; init; }

    /// <summary>Minimum alignment for a uniform-buffer descriptor's offset.</summary>
    public ulong MinUniformBufferOffsetAlignment { get; init; }

    /// <summary>Minimum alignment for a storage-buffer descriptor's offset.</summary>
    public ulong MinStorageBufferOffsetAlignment { get; init; }

    /// <summary>
    /// Alignment host writes must be flushed at on non-coherent memory — the granularity
    /// <c>vkFlushMappedMemoryRanges</c> rounds to.
    /// </summary>
    public ulong NonCoherentAtomSize { get; init; }

    /// <summary>
    /// How many <c>VkDeviceMemory</c> objects may exist at once. The reason to sub-allocate
    /// at all: it is commonly 4096, and one allocation per resource exhausts it long before
    /// memory runs out.
    /// </summary>
    public uint MaxMemoryAllocationCount { get; init; }
}
