namespace Ahjo.Vulkan;

/// <summary>
/// Result handed back by <see cref="StagingUploader.Upload{T}"/>. Carries
/// the staging <see cref="Buffer"/> the bytes landed in, the <see cref="Offset"/>
/// at which they start, and the <see cref="Size"/> in bytes. Pass directly
/// into <see cref="CommandRecorder.CopyBuffer(in Buffer, in Buffer, ReadOnlySpan{BufferCopyRegion})"/>
/// or <see cref="CommandRecorder.CopyBufferToImage"/> to consume.
/// </summary>
public readonly record struct StagedUpload(Buffer Source, ulong Offset, ulong Size)
{
    public bool IsEmpty => Size == 0;

    /// <summary>
    /// Builds a <see cref="BufferCopyRegion"/> that pulls these bytes
    /// into <paramref name="dstOffset"/> on the destination buffer.
    /// </summary>
    public BufferCopyRegion ToCopyRegion(ulong dstOffset = 0)
        => BufferCopyRegion.Of(Size, srcOffset: Offset, dstOffset: dstOffset);
}
