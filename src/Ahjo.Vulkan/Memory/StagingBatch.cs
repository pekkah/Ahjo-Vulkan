using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Bulk staging helper for the asset-load case: collects N pending
/// host-to-device buffer uploads and flushes them in a single
/// <c>vkQueueSubmit2</c> + <c>vkQueueWaitIdle</c>. Designed for scene-init
/// workloads where the per-frame <see cref="StagingUploader"/> ring is the
/// wrong shape — a hundred mesh / texture uploads at startup amortize
/// the wait-idle cost across one wait, not one per upload.
/// </summary>
/// <remarks>
/// <para><b>How it differs from <see cref="StagingUploader"/>.</b>
/// <see cref="StagingUploader"/> targets the per-frame ring: bytes are
/// owned by the in-flight frame, <see cref="StagingUploader.Reset"/>
/// rewinds heads when the frame retires, the consuming copies are
/// recorded into the frame's existing command buffer.
/// <see cref="StagingBatch"/> targets the asset-load path: enqueue
/// all uploads, flush once, block until they're done, optionally
/// <see cref="Reset"/> to reuse the batch for the next pile of assets.</para>
/// <para><b>Lifecycle.</b> Construct against an
/// <see cref="Allocator"/>. Call <see cref="EnqueueUpload{T}"/> N times
/// (each accepts a destination <see cref="Buffer"/> + offset). Call
/// <see cref="Flush"/> with a <see cref="Queue"/> and a
/// <see cref="CommandBufferPool"/> the caller owns; the helper records
/// the copies, submits, waits idle on the queue, then resets internal
/// state. <see cref="Dispose"/> frees the chunked staging memory.</para>
/// <para><b>Pairs with #61.</b> The flush is the same shape as
/// <c>Queue.ImmediateSubmit</c> would record — once #61 lands, the
/// internal record/submit/wait dance can call into it. The duplication
/// here is one Begin / End / Submit2 / vkQueueWaitIdle pair to keep
/// this branch standalone.</para>
/// <para><b>Thread safety.</b> Single-threaded by design — concurrent
/// <see cref="EnqueueUpload{T}"/> calls would race on the chunk list and
/// the upload-record list. Use one <see cref="StagingBatch"/> per
/// asset-loading thread.</para>
/// </remarks>
public sealed unsafe class StagingBatch : IDisposable
{
    /// <summary>4 MiB. Same default as <see cref="StagingUploader"/>.</summary>
    public const ulong DefaultChunkSize = 4UL * 1024 * 1024;

    /// <summary>16-byte alignment — matches <see cref="StagingUploader.DefaultAlignment"/>.</summary>
    public const ulong DefaultAlignment = 16;

    private readonly Allocator        _allocator;
    private readonly ulong            _chunkSize;
    private readonly ulong            _alignment;
    private readonly List<Buffer>     _chunks  = new();
    private readonly List<ulong>      _heads   = new();
    private readonly List<PendingCopy> _pending = new();
    private int                       _activeChunk;
    private bool                      _disposed;

    private struct PendingCopy
    {
        public VkBuffer_T* SrcBuffer;
        public ulong       SrcOffset;
        public VkBuffer_T* DstBuffer;
        public ulong       DstOffset;
        public ulong       Size;
    }

    public StagingBatch(
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

    public ulong ChunkSize  => _chunkSize;
    public ulong Alignment  => _alignment;
    public int   ChunkCount => _chunks.Count;
    public int   PendingCount => _pending.Count;

    /// <summary>
    /// Stages <paramref name="data"/> into a host-visible chunk and
    /// records a pending copy from the chunk into <paramref name="destination"/>
    /// at <paramref name="destinationOffset"/>. The copy doesn't touch
    /// the GPU until <see cref="Flush"/>.
    /// </summary>
    public void EnqueueUpload<T>(
        ReadOnlySpan<T> data,
        in Buffer       destination,
        ulong           destinationOffset = 0)
        where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (destination.IsNull) throw new ArgumentException("Destination buffer is null.", nameof(destination));

        ulong sizeBytes = (ulong)data.Length * (ulong)sizeof(T);
        if (sizeBytes == 0) return;

        Buffer src = ReserveStagingRange(sizeBytes, out ulong srcOffset);
        Span<byte> dst = src.AsSpan<byte>().Slice(checked((int)srcOffset), checked((int)sizeBytes));
        MemoryMarshal.AsBytes(data).CopyTo(dst);

        _pending.Add(new PendingCopy
        {
            SrcBuffer = src.Handle,
            SrcOffset = srcOffset,
            DstBuffer = destination.Handle,
            DstOffset = destinationOffset,
            Size      = sizeBytes,
        });
    }

    /// <summary>
    /// Records every pending copy into one command buffer from
    /// <paramref name="pool"/>, submits to <paramref name="queue"/>,
    /// and blocks (<c>vkQueueWaitIdle</c>) until the GPU finishes. On
    /// return all destination buffers contain the staged data and the
    /// batch's pending list is cleared; staging chunks are recycled
    /// (heads reset to zero) for the next round of uploads.
    /// </summary>
    public void Flush(Queue queue, CommandBufferPool pool)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(pool);
        if (_pending.Count == 0) return;

        CommandRecorder rec = pool.Begin();
        try
        {
            foreach (var copy in CollectionsMarshal.AsSpan(_pending))
            {
                var region = new VkBufferCopy2
                {
                    sType     = VkStructureType.VK_STRUCTURE_TYPE_BUFFER_COPY_2,
                    srcOffset = copy.SrcOffset,
                    dstOffset = copy.DstOffset,
                    size      = copy.Size,
                };
                var info = new VkCopyBufferInfo2
                {
                    sType       = VkStructureType.VK_STRUCTURE_TYPE_COPY_BUFFER_INFO_2,
                    srcBuffer   = copy.SrcBuffer,
                    dstBuffer   = copy.DstBuffer,
                    regionCount = 1,
                    pRegions    = &region,
                };
                Vk.vkCmdCopyBuffer2((VkCommandBuffer_T*)rec.RawHandle, &info);
            }

            // Submit2 ends the recorder for us; default fence = no fence.
            queue.Submit2(ref rec, fence: default);
            // Wait for the entire submit; mirrors the engine's bulk-flush
            // pattern in StagingBatch.cs and matches Queue.ImmediateSubmit
            // (issue 61).
            Vk.vkQueueWaitIdle(queue.Handle).ThrowIfFailed();
        }
        finally
        {
            rec.Dispose();
            ResetState();
        }
    }

    /// <summary>
    /// Rewinds every chunk's head back to zero and clears the pending
    /// list without flushing. Use when a partial batch must be abandoned
    /// (load failure, cancellation). Chunks are kept across resets.
    /// </summary>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ResetState();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var chunkSpan = CollectionsMarshal.AsSpan(_chunks);
        for (int i = 0; i < chunkSpan.Length; i++) chunkSpan[i].Dispose();
        _chunks.Clear();
        _heads.Clear();
        _pending.Clear();
    }

    private Buffer ReserveStagingRange(ulong sizeBytes, out ulong offset)
    {
        var heads  = CollectionsMarshal.AsSpan(_heads);
        var chunks = CollectionsMarshal.AsSpan(_chunks);
        while (_activeChunk < heads.Length)
        {
            ulong head    = heads[_activeChunk];
            ulong aligned = AlignUp(head, _alignment);
            if (aligned + sizeBytes <= chunks[_activeChunk].Size)
            {
                heads[_activeChunk] = aligned + sizeBytes;
                offset = aligned;
                return chunks[_activeChunk];
            }
            _activeChunk++;
        }

        // Need a fresh chunk. Mirror StagingUploader's growth shape:
        // size to the larger of the default chunk size or the upload's
        // own (aligned) length so a single oversize upload never fails.
        ulong needed = Math.Max(_chunkSize, AlignUp(sizeBytes, _alignment));
        // Pre-grow both lists before the native alloc. Without this,
        // _chunks.Add can succeed before _heads.Add throws on capacity
        // expansion — the catch can free `fresh` but `_chunks` still
        // holds it, and the eventual Dispose double-vmaDestroyBuffers.
        int nextCount = _chunks.Count + 1;
        _chunks.EnsureCapacity(nextCount);
        _heads.EnsureCapacity(nextCount);
        Buffer fresh = AllocateChunkBuffer(needed);
        try
        {
            _chunks.Add(fresh);
            _heads.Add(sizeBytes);
        }
        catch
        {
            // EnsureCapacity above means Add is allocation-free; this
            // catch is defense-in-depth for an async thread-abort
            // landing between the two Adds. Pull `fresh` back out of
            // _chunks if it landed there so Dispose can't double-free.
            if (_chunks.Count == nextCount) _chunks.RemoveAt(_chunks.Count - 1);
            fresh.Dispose();
            throw;
        }
        _activeChunk = _chunks.Count - 1;
        offset = 0;
        return fresh;
    }

    private void ResetState()
    {
        var heads = CollectionsMarshal.AsSpan(_heads);
        for (int i = 0; i < heads.Length; i++) heads[i] = 0;
        _activeChunk = 0;
        _pending.Clear();
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
