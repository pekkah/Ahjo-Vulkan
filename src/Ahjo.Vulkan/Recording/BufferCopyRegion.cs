using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// One source/destination range for <see cref="CommandRecorder.CopyBuffer"/>.
/// Maps onto <c>VkBufferCopy2</c> (1.3 core, copy_commands2). When
/// <see cref="Size"/> is zero, <see cref="ToNative"/> reads it as
/// <c>VK_WHOLE_SIZE</c> — covers the dominant single-region "copy
/// everything" case without forcing the caller to look up the source
/// length.
/// </summary>
public readonly record struct BufferCopyRegion
{
    public ulong SrcOffset { get; init; }
    public ulong DstOffset { get; init; }
    public ulong Size      { get; init; }

    public static BufferCopyRegion Of(ulong size, ulong srcOffset = 0, ulong dstOffset = 0)
        => new() { SrcOffset = srcOffset, DstOffset = dstOffset, Size = size };

    internal VkBufferCopy2 ToNative() => new()
    {
        sType     = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_COPY_2,
        srcOffset = SrcOffset,
        dstOffset = DstOffset,
        size      = Size == 0 ? ~0ul : Size,
    };
}
