using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// A compile-time guard, not a test. What it proves is one property: <b>a stack
/// span reaches every recording entry point</b>. If it does not, the caller is
/// pushed into a heap array on a path that is supposed to allocate nothing.
/// Every entry point below is called with a stack span; whatever breaks that
/// breaks the build here rather than silently re-imposing the workaround on
/// consumers.
/// </summary>
/// <remarks>
/// Two independent things hold the property up, and the probe guards both:
/// <list type="number">
/// <item><description><b><c>readonly</c> on the member</b> (issue 209). A
/// non-<c>readonly</c> member of a mutable <c>ref struct</c> passes <c>this</c>
/// as a writable <c>ref</c> to a <c>ref struct</c>, which forces caller-wide
/// safe-context on every ref-struct argument in the same invocation — including
/// one passed by <c>in</c>, where <c>scoped</c> is no help. The
/// <see cref="ProbeRendering"/> case fails with CS8350 the moment
/// <c>readonly</c> is dropped from <c>BeginRendering</c>.</description></item>
/// <item><description><b><c>scoped</c> on by-value span parameters</b> (issue
/// 205). Dropping it from any span parameter below fails with CS9080/CS8350.
/// It stays because it is accurate — the recorder captures nothing — and it is
/// what keeps the by-value span path working if a member ever has to become
/// mutating.</description></item>
/// </list>
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

    /// <summary>
    /// Guards the <c>readonly</c> half specifically: <see cref="RenderingInfo"/>
    /// is a <c>ref struct</c> passed by <c>in</c>, carrying a stack-backed span
    /// of attachments. This compiles only because
    /// <see cref="CommandRecorder.BeginRendering"/> is <c>readonly</c>.
    /// </summary>
    internal static void ProbeRendering(ref CommandRecorder rec)
    {
        Span<ColorAttachment> color = stackalloc ColorAttachment[1];
        var info = new RenderingInfo { LayerCount = 1, ColorAttachments = color };
        rec.BeginRendering(in info);
        rec.EndRendering();
    }

    internal static void ProbeEvents(ref CommandRecorder rec, in Event evt)
    {
        Span<MemoryBarrier> mem = stackalloc MemoryBarrier[1];
        rec.SetEvent(in evt, mem, default, default);
        rec.WaitEvent(in evt, mem, default, default);
    }
}
