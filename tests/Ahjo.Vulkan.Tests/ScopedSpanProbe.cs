using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Issue 205: a compile-time guard, not a test. <see cref="CommandRecorder"/> is
/// a <c>ref struct</c>, so a span parameter that is not <c>scoped</c> cannot
/// receive a <c>stackalloc</c> — the call site fails with CS9080/CS8350 and the
/// caller is pushed into a heap array on a path that is supposed to allocate
/// nothing. Every span entry point below is called with a stack span; dropping
/// <c>scoped</c> from any of them breaks the build here rather than silently
/// re-imposing the workaround on consumers.
/// </summary>
/// <remarks>
/// Nothing in here runs — the handles are default and the calls would be
/// invalid Vulkan. The value is entirely in the compiler accepting it. The
/// <c>params ReadOnlySpan&lt;T&gt;</c> overloads at the bottom carry no
/// <c>scoped</c> modifier because a <c>params</c> span parameter is implicitly
/// scoped; they are probed to keep that assumption honest.
/// </remarks>
internal static unsafe class ScopedSpanProbe
{
    internal static void Probe(ref CommandRecorder rec, in PipelineLayout layout, in Image img, in Buffer buf)
    {
        rec.BeginLabel(stackalloc byte[4]);
        rec.InsertLabel(stackalloc byte[4]);
        using (rec.LabelScope(stackalloc byte[4])) { }

        Span<DescriptorSet> sets = stackalloc DescriptorSet[1];
        Span<uint> offs = stackalloc uint[1];
        rec.BindDescriptorSets(VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_GRAPHICS, in layout, 0, sets, offs);

        Span<Buffer> bufs = stackalloc Buffer[1];
        Span<ulong> boffs = stackalloc ulong[1];
        rec.BindVertexBuffers(0, bufs, boffs);

        Span<DescriptorWrite> writes = stackalloc DescriptorWrite[1];
        rec.PushDescriptorSet(VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_GRAPHICS, in layout, 0, writes);

        Span<MemoryBarrier> mem = stackalloc MemoryBarrier[1];
        Span<BufferBarrier> bb  = stackalloc BufferBarrier[1];
        Span<ImageBarrier>  ib  = stackalloc ImageBarrier[1];
        rec.PipelineBarrier(mem, bb, ib);
        rec.PipelineBarrier(ib);

        Span<ImageBlitRegion> blits = stackalloc ImageBlitRegion[1];
        rec.BlitImage(in img, default, in img, default, blits);

        Span<VkImageSubresourceRange> ranges = stackalloc VkImageSubresourceRange[1];
        VkClearColorValue cc = default;
        rec.ClearColorImage(in img, default, in cc, ranges);
        VkClearDepthStencilValue ds = default;
        rec.ClearDepthStencilImage(in img, default, in ds, ranges);

        // params ReadOnlySpan<T> is implicitly scoped — no modifier needed.
        rec.CopyBuffer(in buf, in buf, stackalloc BufferCopyRegion[1]);
        rec.CopyImage(in img, default, in img, default, stackalloc ImageCopyRegion[1]);
    }

    internal static void ProbeEvents(ref CommandRecorder rec, in Event evt)
    {
        Span<MemoryBarrier> mem = stackalloc MemoryBarrier[1];
        rec.SetEvent(in evt, mem, default, default);
        rec.WaitEvent(in evt, mem, default, default);
    }
}
