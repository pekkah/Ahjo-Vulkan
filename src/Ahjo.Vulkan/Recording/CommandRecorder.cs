using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Scope object handed back from <see cref="CommandBufferPool.Begin"/>.
/// <c>ref struct</c> so the recorder cannot escape its frame, cannot be
/// captured into a closure, cannot live across an async boundary —
/// matching Vulkan's external-synchronization contract on
/// <c>VkCommandBuffer</c>.
/// </summary>
/// <remarks>
/// <para><b>Lifecycle.</b> <see cref="End"/> finalizes recording
/// (<c>vkEndCommandBuffer</c>) and is idempotent — useful when handing
/// the recorder to <see cref="Queue.Submit2"/>, which calls End for you.
/// <see cref="Dispose"/> ends recording if not already ended and returns
/// the buffer to the pool. The two flags are independent: a recorder
/// can be ended-and-submitted but not yet retired (the pool tracks it
/// as outstanding until Dispose).</para>
/// <para><b>Recording surface.</b> Issue 16 (#17) ships the modern
/// minimum: BeginRendering / EndRendering, dynamic viewport + scissor,
/// BindPipeline (compute and graphics), BindDescriptorSets, typed
/// PushConstants, Draw, Dispatch. The narrower draw / bind family
/// (DrawIndexed, DrawIndirect, DrawIndexedIndirect, BindVertexBuffers,
/// BindIndexBuffer, DispatchIndirect) is filed as a follow-up.</para>
/// </remarks>
public unsafe ref struct CommandRecorder : IDisposable
{
    private readonly CommandBufferPool _pool;
    internal readonly VkCommandBuffer_T* Handle;
    private bool _ended;
    private bool _retired;

    internal CommandRecorder(CommandBufferPool pool, VkCommandBuffer_T* handle)
    {
        _pool   = pool;
        Handle  = handle;
        _ended  = false;
        _retired = false;
    }

    public bool IsNull => Handle == null;

    /// <summary>
    /// Calls <c>vkEndCommandBuffer</c>. Idempotent. Does not retire the
    /// command buffer — the recorder is still owned by the caller and
    /// must be <see cref="Dispose"/>'d eventually.
    /// </summary>
    public void End()
    {
        if (Handle == null || _ended) return;
        Vk.vkEndCommandBuffer(Handle).ThrowIfFailed();
        _ended = true;
    }

    /// <summary>
    /// Ends recording (if not already ended) and returns the buffer to
    /// the pool. Safe to call from a <c>using</c>.
    /// </summary>
    public void Dispose()
    {
        if (Handle == null || _retired) return;
        if (!_ended)
        {
            Vk.vkEndCommandBuffer(Handle).ThrowIfFailed();
            _ended = true;
        }
        _pool.Retire(Handle);
        _retired = true;
    }

    // ---- Dynamic state ----

    public void SetViewport(in VkViewport viewport)
    {
        fixed (VkViewport* p = &viewport)
            Vk.vkCmdSetViewport(Handle, 0, 1, p);
    }

    public void SetScissor(in VkRect2D scissor)
    {
        fixed (VkRect2D* p = &scissor)
            Vk.vkCmdSetScissor(Handle, 0, 1, p);
    }

    // ---- Bind family ----

    public void BindPipeline(in ComputePipeline pipeline)
        => Vk.vkCmdBindPipeline(Handle, VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_COMPUTE, pipeline.Handle);

    public void BindPipeline(in GraphicsPipeline pipeline)
        => Vk.vkCmdBindPipeline(Handle, VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_GRAPHICS, pipeline.Handle);

    public void BindDescriptorSets(
        VkPipelineBindPoint    bindPoint,
        in PipelineLayout      layout,
        uint                   firstSet,
        ReadOnlySpan<DescriptorSet> sets,
        ReadOnlySpan<uint>     dynamicOffsets = default)
    {
        if (sets.IsEmpty) return;
        Span<nint> raw = stackalloc nint[sets.Length];
        for (int i = 0; i < sets.Length; i++) raw[i] = (nint)sets[i].Handle;

        fixed (nint* pSets    = raw)
        fixed (uint* pOffsets = dynamicOffsets)
        {
            Vk.vkCmdBindDescriptorSets(
                Handle, bindPoint, layout.Handle,
                firstSet, (uint)sets.Length, (VkDescriptorSet_T**)pSets,
                (uint)dynamicOffsets.Length, dynamicOffsets.IsEmpty ? null : pOffsets);
        }
    }

    /// <summary>
    /// Pushes <paramref name="data"/> into the layout's push-constant
    /// range. Caller's responsibility to ensure
    /// <c>sizeof(T) + offset</c> fits a range declared on
    /// <paramref name="layout"/> for <paramref name="stages"/>.
    /// </summary>
    public void PushConstants<T>(in PipelineLayout layout, ShaderStages stages, in T data, uint offset = 0)
        where T : unmanaged
    {
        fixed (T* p = &data)
            Vk.vkCmdPushConstants(Handle, layout.Handle, (uint)stages, offset, (uint)Unsafe.SizeOf<T>(), p);
    }

    // ---- Draw / Dispatch ----

    public void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0)
        => Vk.vkCmdDraw(Handle, vertexCount, instanceCount, firstVertex, firstInstance);

    public void Dispatch(uint groupCountX, uint groupCountY = 1, uint groupCountZ = 1)
        => Vk.vkCmdDispatch(Handle, groupCountX, groupCountY, groupCountZ);

    // ---- Pipeline barriers (sync2) ----

    /// <summary>
    /// Issues one <c>vkCmdPipelineBarrier2</c> for an arbitrary mix of
    /// memory / buffer / image barriers. Vulkan rewards batching — the
    /// API enforces a single underlying call regardless of how many
    /// barriers the caller supplies. Pass <c>default</c> for any kind
    /// you don't need.
    /// </summary>
    public void PipelineBarrier(
        ReadOnlySpan<MemoryBarrier> memory,
        ReadOnlySpan<BufferBarrier> buffer,
        ReadOnlySpan<ImageBarrier>  image)
    {
        if (memory.IsEmpty && buffer.IsEmpty && image.IsEmpty) return;

        Span<VkMemoryBarrier2>       nm = stackalloc VkMemoryBarrier2[Math.Max(memory.Length, 1)];
        Span<VkBufferMemoryBarrier2> nb = stackalloc VkBufferMemoryBarrier2[Math.Max(buffer.Length, 1)];
        Span<VkImageMemoryBarrier2>  ni = stackalloc VkImageMemoryBarrier2[Math.Max(image.Length, 1)];
        for (int i = 0; i < memory.Length; i++) nm[i] = memory[i].ToNative();
        for (int i = 0; i < buffer.Length; i++) nb[i] = buffer[i].ToNative();
        for (int i = 0; i < image.Length;  i++) ni[i] = image[i].ToNative();

        fixed (VkMemoryBarrier2*       pm = nm)
        fixed (VkBufferMemoryBarrier2* pb = nb)
        fixed (VkImageMemoryBarrier2*  pi = ni)
        {
            var dep = new VkDependencyInfo
            {
                sType                    = VkStructureType.VK_STRUCTURE_TYPE_DEPENDENCY_INFO,
                memoryBarrierCount       = (uint)memory.Length,
                pMemoryBarriers          = memory.Length > 0 ? pm : null,
                bufferMemoryBarrierCount = (uint)buffer.Length,
                pBufferMemoryBarriers    = buffer.Length > 0 ? pb : null,
                imageMemoryBarrierCount  = (uint)image.Length,
                pImageMemoryBarriers     = image.Length  > 0 ? pi : null,
            };
            Vk.vkCmdPipelineBarrier2(Handle, &dep);
        }
    }

    /// <summary>Image-only convenience overload — the dominant case.</summary>
    public void PipelineBarrier(ReadOnlySpan<ImageBarrier> image)
        => PipelineBarrier(default, default, image);

    /// <summary>Single image-barrier convenience overload.</summary>
    public void PipelineBarrier(in ImageBarrier image)
    {
        ImageBarrier copy = image;
        PipelineBarrier(default, default, MemoryMarshal.CreateReadOnlySpan(ref copy, 1));
    }

    // ---- Copy / blit / clear / fill (copy_commands2 path) ----

    /// <summary>
    /// One <c>vkCmdCopyBuffer2</c>. <paramref name="regions"/> may be
    /// any length (caller's stackalloc, array, ArrayPool rental).
    /// Empty span is a no-op.
    /// </summary>
    public void CopyBuffer(in Buffer src, in Buffer dst, params ReadOnlySpan<BufferCopyRegion> regions)
    {
        if (regions.IsEmpty) return;
        Span<VkBufferCopy2> n = stackalloc VkBufferCopy2[regions.Length];
        for (int i = 0; i < regions.Length; i++) n[i] = regions[i].ToNative();
        fixed (VkBufferCopy2* p = n)
        {
            var info = new VkCopyBufferInfo2
            {
                sType       = VkStructureType.VK_STRUCTURE_TYPE_COPY_BUFFER_INFO_2,
                srcBuffer   = src.Handle,
                dstBuffer   = dst.Handle,
                regionCount = (uint)regions.Length,
                pRegions    = p,
            };
            Vk.vkCmdCopyBuffer2(Handle, &info);
        }
    }

    /// <summary>Whole-buffer copy from <paramref name="src"/> offset 0 → <paramref name="dst"/> offset 0.</summary>
    public void CopyBuffer(in Buffer src, in Buffer dst)
    {
        BufferCopyRegion r = BufferCopyRegion.Of(size: src.Size);
        CopyBuffer(in src, in dst, r);
    }

    public void CopyBufferToImage(
        in Buffer                       src,
        in Image                        dst,
        VkImageLayout                   dstLayout,
        params ReadOnlySpan<BufferImageCopy> regions)
    {
        if (regions.IsEmpty) return;
        Span<VkBufferImageCopy2> n = stackalloc VkBufferImageCopy2[regions.Length];
        for (int i = 0; i < regions.Length; i++) n[i] = regions[i].ToNative();
        fixed (VkBufferImageCopy2* p = n)
        {
            var info = new VkCopyBufferToImageInfo2
            {
                sType          = VkStructureType.VK_STRUCTURE_TYPE_COPY_BUFFER_TO_IMAGE_INFO_2,
                srcBuffer      = src.Handle,
                dstImage       = dst.Handle,
                dstImageLayout = dstLayout,
                regionCount    = (uint)regions.Length,
                pRegions       = p,
            };
            Vk.vkCmdCopyBufferToImage2(Handle, &info);
        }
    }

    public void CopyImageToBuffer(
        in Image                        src,
        VkImageLayout                   srcLayout,
        in Buffer                       dst,
        params ReadOnlySpan<BufferImageCopy> regions)
    {
        if (regions.IsEmpty) return;
        Span<VkBufferImageCopy2> n = stackalloc VkBufferImageCopy2[regions.Length];
        for (int i = 0; i < regions.Length; i++) n[i] = regions[i].ToNative();
        fixed (VkBufferImageCopy2* p = n)
        {
            var info = new VkCopyImageToBufferInfo2
            {
                sType          = VkStructureType.VK_STRUCTURE_TYPE_COPY_IMAGE_TO_BUFFER_INFO_2,
                srcImage       = src.Handle,
                srcImageLayout = srcLayout,
                dstBuffer      = dst.Handle,
                regionCount    = (uint)regions.Length,
                pRegions       = p,
            };
            Vk.vkCmdCopyImageToBuffer2(Handle, &info);
        }
    }

    public void CopyImage(
        in Image                       src, VkImageLayout srcLayout,
        in Image                       dst, VkImageLayout dstLayout,
        params ReadOnlySpan<ImageCopyRegion> regions)
    {
        if (regions.IsEmpty) return;
        Span<VkImageCopy2> n = stackalloc VkImageCopy2[regions.Length];
        for (int i = 0; i < regions.Length; i++) n[i] = regions[i].ToNative();
        fixed (VkImageCopy2* p = n)
        {
            var info = new VkCopyImageInfo2
            {
                sType          = VkStructureType.VK_STRUCTURE_TYPE_COPY_IMAGE_INFO_2,
                srcImage       = src.Handle,
                srcImageLayout = srcLayout,
                dstImage       = dst.Handle,
                dstImageLayout = dstLayout,
                regionCount    = (uint)regions.Length,
                pRegions       = p,
            };
            Vk.vkCmdCopyImage2(Handle, &info);
        }
    }

    /// <summary>
    /// One <c>vkCmdBlitImage2</c>. <paramref name="filter"/> defaults to
    /// linear — the right call for downscale / upscale of color targets.
    /// Use nearest for integer formats or single-texel reads.
    /// </summary>
    public void BlitImage(
        in Image                       src, VkImageLayout srcLayout,
        in Image                       dst, VkImageLayout dstLayout,
        ReadOnlySpan<ImageBlitRegion>  regions,
        VkFilter                       filter = VkFilter.VK_FILTER_LINEAR)
    {
        if (regions.IsEmpty) return;
        Span<VkImageBlit2> n = stackalloc VkImageBlit2[regions.Length];
        for (int i = 0; i < regions.Length; i++) n[i] = regions[i].ToNative();
        fixed (VkImageBlit2* p = n)
        {
            var info = new VkBlitImageInfo2
            {
                sType          = VkStructureType.VK_STRUCTURE_TYPE_BLIT_IMAGE_INFO_2,
                srcImage       = src.Handle,
                srcImageLayout = srcLayout,
                dstImage       = dst.Handle,
                dstImageLayout = dstLayout,
                regionCount    = (uint)regions.Length,
                pRegions       = p,
                filter         = filter,
            };
            Vk.vkCmdBlitImage2(Handle, &info);
        }
    }

    /// <summary>
    /// 32-bit pattern fill via <c>vkCmdFillBuffer</c>. <paramref name="size"/>
    /// of <c>~0ul</c> means <c>VK_WHOLE_SIZE</c> (the rest of the buffer
    /// from <paramref name="offset"/>) — Vulkan rounds down to a 4-byte
    /// boundary internally.
    /// </summary>
    public void FillBuffer(in Buffer dst, uint data, ulong offset = 0, ulong size = ~0ul)
        => Vk.vkCmdFillBuffer(Handle, dst.Handle, offset, size, data);

    /// <summary>
    /// <c>vkCmdClearColorImage</c> across one or more subresource ranges.
    /// Image must have been transitioned to <c>TRANSFER_DST_OPTIMAL</c>
    /// (or <c>GENERAL</c>); the wrapper does not enforce that — that's
    /// the caller's barrier responsibility.
    /// </summary>
    public void ClearColorImage(
        in Image                              image,
        VkImageLayout                         layout,
        in VkClearColorValue                  color,
        ReadOnlySpan<VkImageSubresourceRange> ranges)
    {
        if (ranges.IsEmpty) return;
        fixed (VkClearColorValue*       pColor = &color)
        fixed (VkImageSubresourceRange* pRange = ranges)
            Vk.vkCmdClearColorImage(Handle, image.Handle, layout, pColor, (uint)ranges.Length, pRange);
    }

    /// <summary>Whole-image color clear (mip 0+, layer 0+, color aspect).</summary>
    public void ClearColorImage(in Image image, VkImageLayout layout, in VkClearColorValue color)
    {
        var range = new VkImageSubresourceRange
        {
            aspectMask     = (uint)VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT,
            baseMipLevel   = 0, levelCount = image.MipLevels   == 0 ? 1u : image.MipLevels,
            baseArrayLayer = 0, layerCount = image.ArrayLayers == 0 ? 1u : image.ArrayLayers,
        };
        ClearColorImage(in image, layout, in color, MemoryMarshal.CreateReadOnlySpan(ref range, 1));
    }

    public void ClearDepthStencilImage(
        in Image                              image,
        VkImageLayout                         layout,
        in VkClearDepthStencilValue           depthStencil,
        ReadOnlySpan<VkImageSubresourceRange> ranges)
    {
        if (ranges.IsEmpty) return;
        fixed (VkClearDepthStencilValue* pDs    = &depthStencil)
        fixed (VkImageSubresourceRange*  pRange = ranges)
            Vk.vkCmdClearDepthStencilImage(Handle, image.Handle, layout, pDs, (uint)ranges.Length, pRange);
    }

    /// <summary>
    /// Whole-image depth (and optionally stencil) clear. Pass
    /// <c>VK_IMAGE_ASPECT_DEPTH_BIT | VK_IMAGE_ASPECT_STENCIL_BIT</c>
    /// for combined formats.
    /// </summary>
    public void ClearDepthStencilImage(
        in Image                    image,
        VkImageLayout               layout,
        in VkClearDepthStencilValue depthStencil,
        VkImageAspectFlagBits       aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_DEPTH_BIT)
    {
        var range = new VkImageSubresourceRange
        {
            aspectMask     = (uint)aspect,
            baseMipLevel   = 0, levelCount = image.MipLevels   == 0 ? 1u : image.MipLevels,
            baseArrayLayer = 0, layerCount = image.ArrayLayers == 0 ? 1u : image.ArrayLayers,
        };
        ClearDepthStencilImage(in image, layout, in depthStencil, MemoryMarshal.CreateReadOnlySpan(ref range, 1));
    }

    // ---- Dynamic rendering ----

    public void BeginRendering(in RenderingInfo info)
    {
        Span<VkRenderingAttachmentInfo> color = stackalloc VkRenderingAttachmentInfo[
            Math.Max(info.ColorAttachments.Length, 1)];
        for (int i = 0; i < info.ColorAttachments.Length; i++)
            color[i] = info.ColorAttachments[i].ToNative();

        VkRenderingAttachmentInfo depth = info.DepthAttachment is { } d ? d.ToNative() : default;
        VkRenderingAttachmentInfo stencil = info.StencilAttachment is { } s ? s.ToNative() : default;

        fixed (VkRenderingAttachmentInfo* pColor = color)
        {
            VkRenderingAttachmentInfo* pDepth   = info.DepthAttachment.HasValue   ? &depth   : null;
            VkRenderingAttachmentInfo* pStencil = info.StencilAttachment.HasValue ? &stencil : null;
            var native = new VkRenderingInfo
            {
                sType                = VkStructureType.VK_STRUCTURE_TYPE_RENDERING_INFO,
                renderArea           = info.RenderArea,
                layerCount           = info.LayerCount == 0 ? 1u : info.LayerCount,
                viewMask             = info.ViewMask,
                colorAttachmentCount = (uint)info.ColorAttachments.Length,
                pColorAttachments    = info.ColorAttachments.Length > 0 ? pColor : null,
                pDepthAttachment     = pDepth,
                pStencilAttachment   = pStencil,
            };
            Vk.vkCmdBeginRendering(Handle, &native);
        }
    }

    public void EndRendering() => Vk.vkCmdEndRendering(Handle);
}
