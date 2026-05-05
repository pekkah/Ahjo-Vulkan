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
