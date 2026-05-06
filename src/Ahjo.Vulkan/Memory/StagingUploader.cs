using System.Runtime.InteropServices;

namespace Ahjo.Vulkan;

/// <summary>
/// Per-frame bump-allocator over persistent-mapped host buffers. One
/// upload = one <see cref="MemoryMarshal.AsBytes{T}(ReadOnlySpan{T})"/>
/// + <see cref="System.ReadOnlySpan{T}.CopyTo(System.Span{T})"/>. No
/// VMA traffic in steady state — chunks are allocated on demand and
/// kept across <see cref="Reset"/>.
/// </summary>
/// <remarks>
/// <para><b>Lifecycle.</b> Owned by a frame-scoped object (see
/// <c>FrameContext</c>, #16). The owner calls <see cref="Reset"/> at
/// frame begin to rewind every chunk's head back to 0. The chunks
/// themselves stick around — first frame after construction grows the
/// pool, subsequent frames hit the same chunks with no allocator
/// calls.</para>
/// <para><b>Oversize uploads.</b> If a single upload exceeds
/// <see cref="ChunkSize"/>, a one-off chunk is allocated to fit it and
/// added to the pool. The chunk stays in the pool for future frames —
/// pathological, but never fails.</para>
/// </remarks>
public sealed unsafe class StagingUploader : IDisposable
{
    /// <summary>4 MiB. Picked to comfortably hold a typical frame's
    /// worth of mesh / uniform updates without needing a second chunk.</summary>
    public const ulong DefaultChunkSize = 4UL * 1024 * 1024;

    /// <summary>Conservative, covers texel-block-size + 4-byte
    /// <c>bufferOffset</c> requirement for <c>vkCmdCopyBufferToImage</c>
    /// and aligns spans for SIMD-friendly downstream reads.</summary>
    public const ulong DefaultAlignment = 16;

    private readonly Allocator _allocator;
    private readonly ulong     _chunkSize;
    private readonly ulong     _alignment;
    private readonly List<Chunk> _chunks = new();
    private int                _activeChunkIndex;

    private struct Chunk
    {
        public Buffer Buf;
        public ulong  Head;
    }

    public StagingUploader(
        Allocator allocator,
        ulong     chunkSize = DefaultChunkSize,
        ulong     alignment = DefaultAlignment)
    {
        if (allocator.IsNull) throw new ArgumentException("Allocator handle is null.", nameof(allocator));
        if (chunkSize == 0)   throw new ArgumentOutOfRangeException(nameof(chunkSize));
        if (alignment == 0 || (alignment & (alignment - 1)) != 0)
            throw new ArgumentException("Alignment must be a power of two.", nameof(alignment));
        _allocator = allocator;
        _chunkSize = chunkSize;
        _alignment = alignment;
    }

    public ulong ChunkSize => _chunkSize;
    public ulong Alignment => _alignment;
    public int   ChunkCount => _chunks.Count;

    /// <summary>
    /// Sum of bytes consumed across every chunk since the last
    /// <see cref="Reset"/>. Useful for sizing telemetry; not part of the
    /// hot path.
    /// </summary>
    public ulong UsedBytes
    {
        get
        {
            ulong total = 0;
            foreach (var c in _chunks) total += c.Head;
            return total;
        }
    }

    /// <summary>
    /// Bumps a chunk's head, copies <paramref name="data"/> in, and hands
    /// back a <see cref="StagedUpload"/> pointing at the new range. The
    /// returned <see cref="StagedUpload.Source"/> stays valid until the
    /// next <see cref="Dispose"/> — the bytes themselves stay valid until
    /// the next <see cref="Reset"/>, so the caller must record + submit
    /// the consuming copy before the frame ends.
    /// </summary>
    public StagedUpload Upload<T>(ReadOnlySpan<T> data) where T : unmanaged
    {
        ulong sizeBytes = (ulong)data.Length * (ulong)sizeof(T);
        if (sizeBytes == 0) return default;

        var chunkSpan = CollectionsMarshal.AsSpan(_chunks);
        while (_activeChunkIndex < chunkSpan.Length)
        {
            ref Chunk c       = ref chunkSpan[_activeChunkIndex];
            ulong     aligned = AlignUp(c.Head, _alignment);
            if (aligned + sizeBytes <= c.Buf.Size)
            {
                Span<byte> dst = c.Buf.AsSpan<byte>().Slice(checked((int)aligned), checked((int)sizeBytes));
                MemoryMarshal.AsBytes(data).CopyTo(dst);
                c.Head = aligned + sizeBytes;
                return new StagedUpload(c.Buf, aligned, sizeBytes);
            }
            _activeChunkIndex++;
        }

        // No existing chunk has room. Grow.
        ulong  needed = Math.Max(_chunkSize, AlignUp(sizeBytes, _alignment));
        Buffer newBuf = AllocateChunkBuffer(needed);
        _chunks.Add(new Chunk { Buf = newBuf, Head = sizeBytes });
        Span<byte> dst2 = newBuf.AsSpan<byte>().Slice(0, checked((int)sizeBytes));
        MemoryMarshal.AsBytes(data).CopyTo(dst2);
        _activeChunkIndex = _chunks.Count - 1;
        return new StagedUpload(newBuf, 0, sizeBytes);
    }

    /// <summary>
    /// Rewinds every chunk's head to zero. Chunk buffers themselves are
    /// retained — a steady-state frame after warmup hits this path
    /// followed by a sequence of <see cref="Upload{T}"/> calls and stays
    /// allocation-free.
    /// </summary>
    public void Reset()
    {
        var chunkSpan = CollectionsMarshal.AsSpan(_chunks);
        for (int i = 0; i < chunkSpan.Length; i++) chunkSpan[i].Head = 0;
        _activeChunkIndex = 0;
    }

    public void Dispose()
    {
        var chunkSpan = CollectionsMarshal.AsSpan(_chunks);
        for (int i = 0; i < chunkSpan.Length; i++) chunkSpan[i].Buf.Dispose();
        _chunks.Clear();
    }

    private Buffer AllocateChunkBuffer(ulong size)
        => _allocator.CreateBuffer(
            new BufferDescription
            {
                Size  = size,
                Usage = BufferUsage.TransferSrc,
            },
            new AllocationDescription
            {
                Usage = MemoryUsage.AutoPreferHost,
                Flags = AllocationFlags.HostAccessSequentialWrite | AllocationFlags.Mapped,
            });

    private static ulong AlignUp(ulong value, ulong alignment)
        => (value + (alignment - 1)) & ~(alignment - 1);
}
