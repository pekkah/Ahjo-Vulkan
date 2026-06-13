using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// One source/destination range for <see cref="CommandRecorder.CopyBuffer"/>.
/// Maps onto <c>VkBufferCopy2</c> (1.3 core, copy_commands2). <see cref="Size"/>
/// must be greater than zero: <c>VkBufferCopy2::size</c> has no
/// <c>VK_WHOLE_SIZE</c> sentinel (unlike <c>VkBufferMemoryBarrier2::size</c>),
/// so <see cref="ToNative"/> throws on a zero size rather than emitting a
/// bogus whole-size value. For the "copy everything" case use the whole-buffer
/// <see cref="CommandRecorder.CopyBuffer(in Buffer, in Buffer)"/> overload,
/// which passes the source length explicitly.
/// </summary>
public readonly record struct BufferCopyRegion
{
    public ulong SrcOffset { get; init; }
    public ulong DstOffset { get; init; }
    public ulong Size      { get; init; }

    public static BufferCopyRegion Of(ulong size, ulong srcOffset = 0, ulong dstOffset = 0)
        => new() { SrcOffset = srcOffset, DstOffset = dstOffset, Size = size };

    internal VkBufferCopy2 ToNative()
    {
        if (Size == 0)
            throw new ArgumentException(
                "BufferCopyRegion.Size must be greater than zero. VkBufferCopy2::size has no " +
                "VK_WHOLE_SIZE semantics (VUID-VkBufferCopy2-size-01988); for a whole-buffer copy " +
                "use CommandRecorder.CopyBuffer(src, dst), which passes src.Size explicitly.");
        return new()
        {
            sType     = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_COPY_2,
            srcOffset = SrcOffset,
            dstOffset = DstOffset,
            size      = Size,
        };
    }
}
