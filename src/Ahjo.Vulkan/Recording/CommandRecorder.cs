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
/// <para><b>Recording surface.</b> BeginRendering / EndRendering,
/// dynamic viewport + scissor, BindPipeline (compute and graphics),
/// BindDescriptorSets, typed PushConstants, BindVertexBuffers /
/// BindIndexBuffer, Draw / DrawIndexed / DrawIndirect /
/// DrawIndexedIndirect, Dispatch / DispatchIndirect.</para>
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
    /// Raw <c>VkCommandBuffer</c> as a platform-sized integer. Lets callers
    /// hand the recorded buffer to a different thread for submission, since
    /// the recorder itself is a <c>ref struct</c> and can't cross threads.
    /// The pool's external-sync rules still apply — the recorder must
    /// remain undisposed (i.e. the buffer stays in the pool's outstanding
    /// set) for the duration of any cross-thread submit, otherwise
    /// <see cref="CommandBufferPool.ResetForFrame"/> on the recording
    /// thread can race ahead.
    /// </summary>
    public nint RawHandle => (nint)Handle;

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
    /// <remarks>
    /// vkEndCommandBuffer can fail (out-of-memory, validation reject); if
    /// it does, the buffer must still be retired or the pool's tracking
    /// drifts — _outstanding stays elevated, the cb is in neither _idle
    /// nor _spent, and the next <see cref="CommandBufferPool.ResetForFrame"/>
    /// trips its outstanding assert. The pool can't recover a cb it has
    /// no record of, so a future <see cref="CommandBufferPool.Begin"/>
    /// would also never hand it back out. The try/finally pushes the
    /// retire onto the failure path, the original exception still
    /// propagates.
    /// </remarks>
    public void Dispose()
    {
        if (Handle == null || _retired) return;
        try
        {
            if (!_ended)
            {
                Vk.vkEndCommandBuffer(Handle).ThrowIfFailed();
                _ended = true;
            }
        }
        finally
        {
            _pool.Retire(Handle);
            _retired = true;
        }
    }

    // ---- Debug markers (VK_EXT_debug_utils) ----

    /// <summary>
    /// Pushes a debug label onto the command buffer's marker stack via
    /// <c>vkCmdBeginDebugUtilsLabelEXT</c>. RenderDoc / Nsight render the
    /// labeled region as a collapsible group with the supplied
    /// <paramref name="color"/> swatch. No-op when
    /// <c>VK_EXT_debug_utils</c> is not loaded on the device's instance.
    /// Pair with <see cref="EndLabel"/>; prefer <see cref="LabelScope"/>
    /// for clean nesting via <c>using</c>.
    /// </summary>
    public void BeginLabel(ReadOnlySpan<byte> name, in Color color = default)
    {
        var fn = _pool.Device.Functions.CmdBeginDebugUtilsLabel;
        if (fn == null || name.IsEmpty) return;

        fixed (byte* pName = name)
        {
            var label = new VkDebugUtilsLabelEXT
            {
                sType      = VkStructureType.VK_STRUCTURE_TYPE_DEBUG_UTILS_LABEL_EXT,
                pLabelName = (sbyte*)pName,
            };
            label.color[0] = color.R;
            label.color[1] = color.G;
            label.color[2] = color.B;
            label.color[3] = color.A;
            fn(Handle, &label);
        }
    }

    /// <summary>
    /// Pops the most-recently-pushed debug label via
    /// <c>vkCmdEndDebugUtilsLabelEXT</c>. No-op when
    /// <c>VK_EXT_debug_utils</c> is not loaded.
    /// </summary>
    public void EndLabel()
    {
        var fn = _pool.Device.Functions.CmdEndDebugUtilsLabel;
        if (fn == null) return;
        fn(Handle);
    }

    /// <summary>
    /// Inserts a single-shot debug marker via
    /// <c>vkCmdInsertDebugUtilsLabelEXT</c>. Unlike
    /// <see cref="BeginLabel"/> / <see cref="EndLabel"/>, this does not
    /// open a scope — captures show it as a flag on the timeline at the
    /// recorded position.
    /// </summary>
    public void InsertLabel(ReadOnlySpan<byte> name, in Color color = default)
    {
        var fn = _pool.Device.Functions.CmdInsertDebugUtilsLabel;
        if (fn == null || name.IsEmpty) return;

        fixed (byte* pName = name)
        {
            var label = new VkDebugUtilsLabelEXT
            {
                sType      = VkStructureType.VK_STRUCTURE_TYPE_DEBUG_UTILS_LABEL_EXT,
                pLabelName = (sbyte*)pName,
            };
            label.color[0] = color.R;
            label.color[1] = color.G;
            label.color[2] = color.B;
            label.color[3] = color.A;
            fn(Handle, &label);
        }
    }

    /// <summary>
    /// Opens a debug-label scope and returns a <see cref="DisposableLabel"/>
    /// that calls <see cref="EndLabel"/> when disposed — typically via
    /// <c>using var scope = rec.LabelScope("PassName"u8);</c>. Nests
    /// cleanly. No-op when <c>VK_EXT_debug_utils</c> is not loaded.
    /// </summary>
    public DisposableLabel LabelScope(ReadOnlySpan<byte> name, in Color color = default)
    {
        BeginLabel(name, in color);
        // Capture the End fn pointer at scope-open time. If the extension
        // wasn't loaded, _end stays null and Dispose is a no-op — the
        // matching BeginLabel call was also a no-op so the marker stack
        // stays balanced.
        return new DisposableLabel(Handle, _pool.Device.Functions.CmdEndDebugUtilsLabel);
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
        AssertSetsMatchLayout(in layout, firstSet, sets);
        // Vulkan's maxBoundDescriptorSets is at least 4 by spec and 32 on
        // typical desktop drivers. The stack-vs-heap threshold below
        // covers every real GPU; falls back to the heap on the
        // pathological "I'm passing 1024 sets" path so the wrapper can't
        // overflow the recording thread's stack.
        Span<nint> raw = sets.Length <= 32
            ? stackalloc nint[sets.Length]
            : new nint[sets.Length];
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

    private static void AssertSetsMatchLayout(in PipelineLayout layout, uint firstSet, ReadOnlySpan<DescriptorSet> sets)
    {
        if (!AhjoValidation.IsEnabled) return;

        nint[]? declared = layout.Metadata?.SetLayouts;
        // PipelineLayout.FromRaw carries no metadata, and a layout built
        // without set layouts declares none — there's nothing to validate
        // against. The bind still fires; the driver / validation layer is
        // the backstop.
        if (declared is null || declared.Length == 0) return;

        for (int i = 0; i < sets.Length; i++)
        {
            // DescriptorSet.FromRaw produces sets without a Layout —
            // those can't be validated and should not have been routed
            // to BindDescriptorSets in the first place. Skip rather
            // than fail to keep FromRaw debug-name flows usable.
            if (sets[i].Layout == null) continue;

            uint slot = firstSet + (uint)i;
            if (slot >= declared.Length)
                AhjoValidation.Fail("CommandRecorder",
                    $"BindDescriptorSets: slot {slot} (firstSet={firstSet} + i={i}) is out of range; PipelineLayout declares {declared.Length} set(s).");

            if (declared[slot] != (nint)sets[i].Layout)
                AhjoValidation.Fail("CommandRecorder",
                    $"BindDescriptorSets: set[{i}] was allocated against a different VkDescriptorSetLayout than slot {slot} declares on PipelineLayout. " +
                    "The pipeline layout's set layout and the bound set's source layout must match.");
        }
    }

    /// <summary>
    /// Pushes <paramref name="data"/> into the layout's push-constant
    /// range. When <see cref="AhjoValidation.Enabled"/> the call validates
    /// that the <c>[offset, offset + sizeof(T))</c> window fits a range
    /// declared on <paramref name="layout"/> whose stage mask covers
    /// <paramref name="stages"/>; otherwise it relies on the driver /
    /// validation layer.
    /// </summary>
    public void PushConstants<T>(in PipelineLayout layout, ShaderStages stages, in T data, uint offset = 0)
        where T : unmanaged
    {
        // The push-constant size ceiling lives on the layout (its declared
        // ranges) and on the device (maxPushConstantsSize, ≥128 by spec
        // but typically 256 on desktop). AssertPushRangeFits validates the
        // [offset, offset+sizeof) window against the declared range; the
        // device limit is enforced once at PipelineLayout creation time
        // so per-call asserts don't have to re-fetch it.
        AssertPushRangeFits(in layout, stages, offset, (uint)Unsafe.SizeOf<T>());

        fixed (T* p = &data)
            Vk.vkCmdPushConstants(Handle, layout.Handle, (uint)stages, offset, (uint)Unsafe.SizeOf<T>(), p);
    }

    private static void AssertPushRangeFits(in PipelineLayout layout, ShaderStages stages, uint offset, uint size)
    {
        if (!AhjoValidation.IsEnabled) return;

        PushConstantRange[]? ranges = layout.Metadata?.PushRanges;
        // PipelineLayout.FromRaw carries no metadata, and a layout built
        // without push ranges declares none — there's nothing to validate
        // against. The call still fires; the driver / validation layer is
        // the backstop.
        if (ranges is null || ranges.Length == 0) return;

        for (int i = 0; i < ranges.Length; i++)
        {
            var r = ranges[i];
            // Find a single declared range that fully contains the
            // call's [offset, offset+size) window AND whose stage mask
            // is a superset of the requested stages. Vulkan also
            // permits ranges to be split across multiple declarations
            // (each byte's stage union must equal stageFlags), but
            // single-range coverage is the dominant case and matches
            // the "one push-constant block per layout" idiom the
            // wrapper documents on PushConstantRange.
            if ((stages & r.Stages) == stages
                && offset >= r.Offset
                && (ulong)offset + size <= (ulong)r.Offset + r.Size)
            {
                return;
            }
        }

        AhjoValidation.Fail("CommandRecorder",
            $"PushConstants: no declared range on PipelineLayout fits stages={stages}, offset={offset}, size={size}. " +
            "Declared ranges must include the requested window AND cover the requested stages — " +
            "see PipelineLayoutDescription.PushConstantRanges.");
    }

    /// <summary>
    /// Records <c>vkCmdBindVertexBuffers</c> for a tightly-packed range of
    /// vertex bindings starting at <paramref name="firstBinding"/>. Pass
    /// an empty <paramref name="offsets"/> span to bind every buffer at
    /// offset 0; otherwise <paramref name="offsets"/> must match
    /// <paramref name="buffers"/> in length.
    /// </summary>
    public void BindVertexBuffers(
        uint                  firstBinding,
        ReadOnlySpan<Buffer>  buffers,
        ReadOnlySpan<ulong>   offsets = default)
    {
        if (buffers.IsEmpty) return;
        if (!offsets.IsEmpty && offsets.Length != buffers.Length)
            throw new ArgumentException(
                "offsets must have the same length as buffers (or be empty to default to all-zero offsets).",
                nameof(offsets));

        // maxVertexInputBindings is at least 16 by spec and 32 on typical
        // desktop drivers; mirror the BindDescriptorSets threshold so the
        // wrapper can't be coerced into a stack overflow by an oversized
        // caller span.
        Span<nint> rawBuffers = buffers.Length <= 32
            ? stackalloc nint[buffers.Length]
            : new nint[buffers.Length];
        for (int i = 0; i < buffers.Length; i++)
            rawBuffers[i] = (nint)buffers[i].Handle;

        // Single-expression init keeps the stackalloc at method scope so
        // the C# ref-safety analysis accepts it; the heap-fallback path
        // only triggers when offsets is empty AND the caller passed more
        // than 32 buffers, which is far outside any real-world bind set.
        Span<ulong> zero = !offsets.IsEmpty
            ? default
            : (buffers.Length <= 32
                ? stackalloc ulong[buffers.Length]
                : (Span<ulong>)new ulong[buffers.Length]);
        ReadOnlySpan<ulong> useOffsets = offsets.IsEmpty ? zero : offsets;

        fixed (nint*  pBuffers = rawBuffers)
        fixed (ulong* pOffsets = useOffsets)
            Vk.vkCmdBindVertexBuffers(
                Handle, firstBinding, (uint)buffers.Length,
                (VkBuffer_T**)pBuffers, pOffsets);
    }

    public void BindIndexBuffer(in Buffer buffer, ulong offset, VkIndexType indexType)
        => Vk.vkCmdBindIndexBuffer(Handle, buffer.Handle, offset, indexType);

    /// <summary>
    /// Issues <c>vkCmdPushDescriptorSetWithTemplate</c> — records
    /// <paramref name="data"/> as the per-frame descriptor state for the
    /// set index baked into <paramref name="template"/>. No
    /// <c>VkDescriptorSet</c> is allocated; the layout backing the set
    /// must have been created with
    /// <see cref="DescriptorSetLayoutDescription.PushDescriptor"/>.
    /// </summary>
    public void PushDescriptors<T>(
        in DescriptorTemplate<T> template,
        in PipelineLayout        layout,
        in T                     data)
        where T : unmanaged
    {
        fixed (T* p = &data)
            Vk.vkCmdPushDescriptorSetWithTemplate(Handle, template.Handle, layout.Handle, template.Set, p);
    }

    /// <summary>
    /// Issues <c>vkCmdPushDescriptorSet</c> with a span of
    /// <see cref="DescriptorWrite"/> records — the non-templated
    /// counterpart of <see cref="PushDescriptors{T}"/>. Use when the
    /// per-pass binding shape doesn't fit a fixed struct template
    /// (heterogeneous bindings, bindless single-element writes,
    /// per-pass image-view rotation).
    /// </summary>
    /// <remarks>
    /// <para>The layout backing <paramref name="set"/> must have been
    /// created with
    /// <see cref="DescriptorSetLayoutDescription.PushDescriptor"/>.
    /// Allocates zero per call when <paramref name="writes"/> contains
    /// <c>≤ 8</c> entries; longer runs rent from
    /// <see cref="ArrayPool{T}"/>.</para>
    /// </remarks>
    public void PushDescriptorSet(
        VkPipelineBindPoint           bindPoint,
        in PipelineLayout             layout,
        uint                          set,
        ReadOnlySpan<DescriptorWrite> writes)
    {
        if (writes.IsEmpty) return;

        const int StackThreshold = 8;
        int count = writes.Length;
        if (count <= StackThreshold)
        {
            Span<VkWriteDescriptorSet> raws = stackalloc VkWriteDescriptorSet[count];
            FlushPush(Handle, bindPoint, layout.Handle, set, writes, raws);
            return;
        }

        VkWriteDescriptorSet[] rented =
            System.Buffers.ArrayPool<VkWriteDescriptorSet>.Shared.Rent(count);
        try
        {
            FlushPush(Handle, bindPoint, layout.Handle, set, writes, rented.AsSpan(0, count));
        }
        finally
        {
            System.Buffers.ArrayPool<VkWriteDescriptorSet>.Shared.Return(rented);
        }
    }

    private static void FlushPush(
        VkCommandBuffer_T*            cb,
        VkPipelineBindPoint           bindPoint,
        VkPipelineLayout_T*           layout,
        uint                          set,
        ReadOnlySpan<DescriptorWrite> writes,
        Span<VkWriteDescriptorSet>    raws)
    {
        fixed (DescriptorWrite* _ = writes)
        {
            // dstSet is ignored by vkCmdPushDescriptorSet; pass null.
            DescriptorWriteBuilder.BuildWrites(writes, setHandle: null, raws);
            fixed (VkWriteDescriptorSet* pRaws = raws)
                Vk.vkCmdPushDescriptorSet(cb, bindPoint, layout, set, (uint)writes.Length, pRaws);
        }
    }

    // ---- Draw / Dispatch ----

    public void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0)
        => Vk.vkCmdDraw(Handle, vertexCount, instanceCount, firstVertex, firstInstance);

    public void DrawIndexed(
        uint indexCount,
        uint instanceCount = 1,
        uint firstIndex    = 0,
        int  vertexOffset  = 0,
        uint firstInstance = 0)
        => Vk.vkCmdDrawIndexed(Handle, indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);

    /// <summary>
    /// <c>vkCmdDrawIndirect</c> — reads <paramref name="drawCount"/>
    /// <c>VkDrawIndirectCommand</c> structs from
    /// <paramref name="buffer"/> at <paramref name="offset"/>,
    /// <paramref name="stride"/> bytes apart. The buffer must have been
    /// created with <see cref="BufferUsage.IndirectBuffer"/>.
    /// </summary>
    public void DrawIndirect(in Buffer buffer, ulong offset, uint drawCount, uint stride)
        => Vk.vkCmdDrawIndirect(Handle, buffer.Handle, offset, drawCount, stride);

    /// <summary>
    /// <c>vkCmdDrawIndexedIndirect</c> — reads
    /// <paramref name="drawCount"/> <c>VkDrawIndexedIndirectCommand</c>
    /// structs from <paramref name="buffer"/>. Caller is responsible for
    /// having bound an index buffer via
    /// <see cref="BindIndexBuffer"/> beforehand.
    /// </summary>
    public void DrawIndexedIndirect(in Buffer buffer, ulong offset, uint drawCount, uint stride)
        => Vk.vkCmdDrawIndexedIndirect(Handle, buffer.Handle, offset, drawCount, stride);

    public void Dispatch(uint groupCountX, uint groupCountY = 1, uint groupCountZ = 1)
        => Vk.vkCmdDispatch(Handle, groupCountX, groupCountY, groupCountZ);

    /// <summary>
    /// <c>vkCmdDispatchIndirect</c> — reads one
    /// <c>VkDispatchIndirectCommand</c> from <paramref name="buffer"/> at
    /// <paramref name="offset"/>.
    /// </summary>
    public void DispatchIndirect(in Buffer buffer, ulong offset)
        => Vk.vkCmdDispatchIndirect(Handle, buffer.Handle, offset);

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

        const int Threshold = 16;
        Span<VkMemoryBarrier2>       mSlab = stackalloc VkMemoryBarrier2[Threshold];
        Span<VkBufferMemoryBarrier2> bSlab = stackalloc VkBufferMemoryBarrier2[Threshold];
        Span<VkImageMemoryBarrier2>  iSlab = stackalloc VkImageMemoryBarrier2[Threshold];

        Span<VkMemoryBarrier2>       nm = RentForOverflow(memory.Length, Threshold, mSlab, out VkMemoryBarrier2[]?       mRent);
        Span<VkBufferMemoryBarrier2> nb = RentForOverflow(buffer.Length, Threshold, bSlab, out VkBufferMemoryBarrier2[]? bRent);
        Span<VkImageMemoryBarrier2>  ni = RentForOverflow(image.Length,  Threshold, iSlab, out VkImageMemoryBarrier2[]?  iRent);
        try
        {
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
        finally
        {
            if (mRent is not null) System.Buffers.ArrayPool<VkMemoryBarrier2>.Shared.Return(mRent);
            if (bRent is not null) System.Buffers.ArrayPool<VkBufferMemoryBarrier2>.Shared.Return(bRent);
            if (iRent is not null) System.Buffers.ArrayPool<VkImageMemoryBarrier2>.Shared.Return(iRent);
        }
    }

    /// <summary>Image-only convenience overload — the dominant case.</summary>
    public void PipelineBarrier(ReadOnlySpan<ImageBarrier> image)
        => PipelineBarrier(default, default, image);

    /// <summary>Single image-barrier convenience overload.</summary>
    public void PipelineBarrier(in ImageBarrier image)
        => PipelineBarrier(default, default,
            MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in image), 1));

    // ---- Copy / blit / clear / fill (copy_commands2 path) ----

    /// <summary>
    /// One <c>vkCmdCopyBuffer2</c>. <paramref name="regions"/> may be
    /// any length (caller's stackalloc, array, ArrayPool rental).
    /// Empty span is a no-op.
    /// </summary>
    public void CopyBuffer(in Buffer src, in Buffer dst, params ReadOnlySpan<BufferCopyRegion> regions)
    {
        if (regions.IsEmpty) return;

        const int Threshold = 16;
        Span<VkBufferCopy2> slab = stackalloc VkBufferCopy2[Threshold];
        Span<VkBufferCopy2> n = RentForOverflow(regions.Length, Threshold, slab, out VkBufferCopy2[]? rented);
        try
        {
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
        finally
        {
            if (rented is not null) System.Buffers.ArrayPool<VkBufferCopy2>.Shared.Return(rented);
        }
    }

    /// <summary>Whole-buffer copy from <paramref name="src"/> offset 0 → <paramref name="dst"/> offset 0.</summary>
    public void CopyBuffer(in Buffer src, in Buffer dst)
    {
        // Whole-buffer overload writes src.Size bytes into dst at offset 0.
        // Without this guard a smaller dst would either trip Vulkan
        // validation (best case) or silently overrun another allocation
        // backing the same VkDeviceMemory range. The multi-region overload
        // is the right tool for partial copies — point callers at it.
        if (dst.Size < src.Size)
            throw new ArgumentException(
                $"CopyBuffer whole-buffer overload requires dst.Size ({dst.Size}) >= src.Size ({src.Size}). " +
                "Use the multi-region overload with explicit BufferCopyRegion bounds for a partial copy.",
                nameof(dst));
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

        const int Threshold = 16;
        Span<VkBufferImageCopy2> slab = stackalloc VkBufferImageCopy2[Threshold];
        Span<VkBufferImageCopy2> n = RentForOverflow(regions.Length, Threshold, slab, out VkBufferImageCopy2[]? rented);
        try
        {
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
        finally
        {
            if (rented is not null) System.Buffers.ArrayPool<VkBufferImageCopy2>.Shared.Return(rented);
        }
    }

    public void CopyImageToBuffer(
        in Image                        src,
        VkImageLayout                   srcLayout,
        in Buffer                       dst,
        params ReadOnlySpan<BufferImageCopy> regions)
    {
        if (regions.IsEmpty) return;

        const int Threshold = 16;
        Span<VkBufferImageCopy2> slab = stackalloc VkBufferImageCopy2[Threshold];
        Span<VkBufferImageCopy2> n = RentForOverflow(regions.Length, Threshold, slab, out VkBufferImageCopy2[]? rented);
        try
        {
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
        finally
        {
            if (rented is not null) System.Buffers.ArrayPool<VkBufferImageCopy2>.Shared.Return(rented);
        }
    }

    public void CopyImage(
        in Image                       src, VkImageLayout srcLayout,
        in Image                       dst, VkImageLayout dstLayout,
        params ReadOnlySpan<ImageCopyRegion> regions)
    {
        if (regions.IsEmpty) return;

        const int Threshold = 16;
        Span<VkImageCopy2> slab = stackalloc VkImageCopy2[Threshold];
        Span<VkImageCopy2> n = RentForOverflow(regions.Length, Threshold, slab, out VkImageCopy2[]? rented);
        try
        {
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
        finally
        {
            if (rented is not null) System.Buffers.ArrayPool<VkImageCopy2>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Generate a full mip chain for <paramref name="image"/> via
    /// successive <c>vkCmdBlitImage2</c> downsamples from level i-1 to
    /// level i. On entry mip 0 must be in
    /// <c>VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL</c>; on exit every mip is
    /// in <paramref name="finalLayout"/>. The image must have been
    /// created with <see cref="ImageUsage.TransferSrc"/> +
    /// <see cref="ImageUsage.TransferDst"/>.
    /// </summary>
    /// <param name="image">Multi-mip image to fill.</param>
    /// <param name="finalLayout">
    /// Layout every mip lands in. Typical values are
    /// <c>VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL</c> for sampled
    /// textures and <c>VK_IMAGE_LAYOUT_GENERAL</c> for storage images.
    /// </param>
    /// <param name="filter">
    /// Blit filter. Defaults to <see cref="VkFilter.VK_FILTER_LINEAR"/>;
    /// use <see cref="VkFilter.VK_FILTER_NEAREST"/> on integer formats
    /// or formats that don't advertise
    /// <c>VK_FORMAT_FEATURE_SAMPLED_IMAGE_FILTER_LINEAR_BIT</c> /
    /// <c>VK_FORMAT_FEATURE_BLIT_SRC_BIT</c>. Probe via
    /// <see cref="PhysicalDevice.SupportsOptimalTilingFeature"/>.
    /// </param>
    /// <param name="aspect">
    /// Subresource aspect — color (default), depth, or stencil.
    /// </param>
    /// <remarks>
    /// <para>For an image with <c>MipLevels = 1</c> the helper just
    /// transitions mip 0 into <paramref name="finalLayout"/>.</para>
    /// <para>Per-axis mip dimensions use <c>max(1, dim &gt;&gt; i)</c>,
    /// which is the spec's downsample formula and matches the engine's
    /// non-power-of-two behaviour.</para>
    /// <para>All barriers are <c>stackalloc</c>'d; barrier count is
    /// bounded by mip count + 2 (per-iteration src-layout transition
    /// plus the final batched transitions).</para>
    /// </remarks>
    public void GenerateMips(
        in Image              image,
        VkImageLayout         finalLayout,
        VkFilter              filter = VkFilter.VK_FILTER_LINEAR,
        VkImageAspectFlagBits aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_COLOR_BIT)
    {
        uint mipLevels   = image.MipLevels;
        uint arrayLayers = image.ArrayLayers;

        // Single-mip image: only thing left is to put mip 0 into the
        // requested final layout.
        if (mipLevels <= 1)
        {
            ImageBarrier soleBarrier = new()
            {
                Image               = (nint)image.Handle,
                SrcStage            = Stage.Copy,
                SrcAccess           = Access.TransferWrite,
                DstStage            = Stage.AllCommands,
                DstAccess           = Access.MemoryRead | Access.MemoryWrite,
                OldLayout           = VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                NewLayout           = finalLayout,
                SrcQueueFamilyIndex = ImageBarrier.QueueFamilyIgnored,
                DstQueueFamilyIndex = ImageBarrier.QueueFamilyIgnored,
                Aspect              = aspect,
                BaseMipLevel        = 0,
                LevelCount          = 1,
                BaseArrayLayer      = 0,
                LayerCount          = arrayLayers,
            };
            PipelineBarrier(in soleBarrier);
            return;
        }

        // Step 1: mips 1..N-1 start in UNDEFINED and need to move to
        // TRANSFER_DST so the loop's blit destinations are valid. The
        // upcoming write is vkCmdBlitImage2, which executes at the Blit
        // stage in sync2 (not Copy), so DstStage must include Blit —
        // otherwise the layout transition isn't ordered against the blit
        // write and sync2 validation fires WRITE-AFTER-WRITE.
        ImageBarrier dstInit = new()
        {
            Image               = (nint)image.Handle,
            SrcStage            = Stage.None,
            SrcAccess           = Access.None,
            DstStage            = Stage.Blit,
            DstAccess           = Access.TransferWrite,
            OldLayout           = VkImageLayout.VK_IMAGE_LAYOUT_UNDEFINED,
            NewLayout           = VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
            SrcQueueFamilyIndex = ImageBarrier.QueueFamilyIgnored,
            DstQueueFamilyIndex = ImageBarrier.QueueFamilyIgnored,
            Aspect              = aspect,
            BaseMipLevel        = 1,
            LevelCount          = mipLevels - 1,
            BaseArrayLayer      = 0,
            LayerCount          = arrayLayers,
        };
        PipelineBarrier(in dstInit);

        // Step 2: per-mip downsample loop.
        int srcW = (int)image.Width;
        int srcH = (int)image.Height;
        int srcD = (int)image.Depth;

        for (uint i = 1; i < mipLevels; i++)
        {
            int dstW = Math.Max(1, srcW >> 1);
            int dstH = Math.Max(1, srcH >> 1);
            int dstD = Math.Max(1, srcD >> 1);

            // Move mip (i-1) from TRANSFER_DST → TRANSFER_SRC so the
            // upcoming blit can sample it. SrcStage tracks the producer of
            // the previous write to mip (i-1): the caller's
            // vkCmdCopyBufferToImage on i=1 (Copy stage), and the previous
            // iteration's vkCmdBlitImage2 on i>=2 (Blit stage). Sync2 treats
            // Copy and Blit as distinct stages; getting this wrong leaves
            // the prior write unordered against the layout transition.
            ImageBarrier srcSwap = new()
            {
                Image               = (nint)image.Handle,
                SrcStage            = i == 1 ? Stage.Copy : Stage.Blit,
                SrcAccess           = Access.TransferWrite,
                DstStage            = Stage.Blit,
                DstAccess           = Access.TransferRead,
                OldLayout           = VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                NewLayout           = VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                SrcQueueFamilyIndex = ImageBarrier.QueueFamilyIgnored,
                DstQueueFamilyIndex = ImageBarrier.QueueFamilyIgnored,
                Aspect              = aspect,
                BaseMipLevel        = i - 1,
                LevelCount          = 1,
                BaseArrayLayer      = 0,
                LayerCount          = arrayLayers,
            };
            PipelineBarrier(in srcSwap);

            ImageBlitRegion region = new()
            {
                SrcAspect         = aspect,
                SrcMipLevel       = i - 1,
                SrcBaseArrayLayer = 0,
                SrcLayerCount     = arrayLayers,
                SrcOffset0        = default,
                SrcOffset1        = new VkOffset3D { x = srcW, y = srcH, z = srcD },
                DstAspect         = aspect,
                DstMipLevel       = i,
                DstBaseArrayLayer = 0,
                DstLayerCount     = arrayLayers,
                DstOffset0        = default,
                DstOffset1        = new VkOffset3D { x = dstW, y = dstH, z = dstD },
            };
            BlitImage(
                in image, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                in image, VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                MemoryMarshal.CreateReadOnlySpan(ref region, 1),
                filter);

            srcW = dstW;
            srcH = dstH;
            srcD = dstD;
        }

        // Step 3: mips 0..N-2 ended in TRANSFER_SRC; mip N-1 ended in
        // TRANSFER_DST. Move both subranges to the final layout. Issue
        // them as two separate barrier calls — batching into a single
        // vkCmdPipelineBarrier2 would require a stackalloc'd span that
        // ref-safety analysis can't reconcile with the recorder's
        // ref-struct receiver, and the extra barrier on a one-shot
        // mip-gen path is negligible.
        ImageBarrier finalSrcBarrier = new()
        {
            Image               = (nint)image.Handle,
            SrcStage            = Stage.Blit,
            SrcAccess           = Access.TransferRead,
            DstStage            = Stage.AllCommands,
            DstAccess           = Access.MemoryRead | Access.MemoryWrite,
            OldLayout           = VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
            NewLayout           = finalLayout,
            SrcQueueFamilyIndex = ImageBarrier.QueueFamilyIgnored,
            DstQueueFamilyIndex = ImageBarrier.QueueFamilyIgnored,
            Aspect              = aspect,
            BaseMipLevel        = 0,
            LevelCount          = mipLevels - 1,
            BaseArrayLayer      = 0,
            LayerCount          = arrayLayers,
        };
        PipelineBarrier(in finalSrcBarrier);

        // The last mip (mipLevels - 1) was written by vkCmdBlitImage2 in
        // the final loop iteration — SrcStage must be Blit, not Copy, or
        // the transition out of TRANSFER_DST won't be ordered against that
        // write under sync2's separate Copy/Blit stages.
        ImageBarrier finalDstBarrier = new()
        {
            Image               = (nint)image.Handle,
            SrcStage            = Stage.Blit,
            SrcAccess           = Access.TransferWrite,
            DstStage            = Stage.AllCommands,
            DstAccess           = Access.MemoryRead | Access.MemoryWrite,
            OldLayout           = VkImageLayout.VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
            NewLayout           = finalLayout,
            SrcQueueFamilyIndex = ImageBarrier.QueueFamilyIgnored,
            DstQueueFamilyIndex = ImageBarrier.QueueFamilyIgnored,
            Aspect              = aspect,
            BaseMipLevel        = mipLevels - 1,
            LevelCount          = 1,
            BaseArrayLayer      = 0,
            LayerCount          = arrayLayers,
        };
        PipelineBarrier(in finalDstBarrier);
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

        const int Threshold = 16;
        Span<VkImageBlit2> slab = stackalloc VkImageBlit2[Threshold];
        Span<VkImageBlit2> n = RentForOverflow(regions.Length, Threshold, slab, out VkImageBlit2[]? rented);
        try
        {
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
        finally
        {
            if (rented is not null) System.Buffers.ArrayPool<VkImageBlit2>.Shared.Return(rented);
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
            baseMipLevel   = 0, levelCount = image.MipLevels,
            baseArrayLayer = 0, layerCount = image.ArrayLayers,
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
    /// Whole-image depth (and optionally stencil) clear. When
    /// <paramref name="aspect"/> is <c>VK_IMAGE_ASPECT_NONE</c> (the
    /// default), the wrapper infers it from <paramref name="image"/>'s
    /// format: depth-only formats clear depth, stencil-only formats clear
    /// stencil, combined formats (D24_UNORM_S8_UINT etc.) clear both.
    /// Pass an explicit aspect mask to override — e.g. clear only depth on
    /// a combined format.
    /// </summary>
    public void ClearDepthStencilImage(
        in Image                    image,
        VkImageLayout               layout,
        in VkClearDepthStencilValue depthStencil,
        VkImageAspectFlagBits       aspect = VkImageAspectFlagBits.VK_IMAGE_ASPECT_NONE)
    {
        // The previous default of VK_IMAGE_ASPECT_DEPTH_BIT only would
        // silently miss the stencil plane on combined formats and trip
        // VUID-vkCmdClearDepthStencilImage-image-02825. Infer from format
        // so the dominant case (clear everything the format carries)
        // works without the caller doing format gymnastics.
        if (aspect == VkImageAspectFlagBits.VK_IMAGE_ASPECT_NONE)
            aspect = InferDepthStencilAspect(image.Format);

        var range = new VkImageSubresourceRange
        {
            aspectMask     = (uint)aspect,
            baseMipLevel   = 0, levelCount = image.MipLevels,
            baseArrayLayer = 0, layerCount = image.ArrayLayers,
        };
        ClearDepthStencilImage(in image, layout, in depthStencil, MemoryMarshal.CreateReadOnlySpan(ref range, 1));
    }

    private static VkImageAspectFlagBits InferDepthStencilAspect(VkFormat format) => format switch
    {
        VkFormat.VK_FORMAT_S8_UINT => VkImageAspectFlagBits.VK_IMAGE_ASPECT_STENCIL_BIT,
        VkFormat.VK_FORMAT_D16_UNORM_S8_UINT
            or VkFormat.VK_FORMAT_D24_UNORM_S8_UINT
            or VkFormat.VK_FORMAT_D32_SFLOAT_S8_UINT
            => VkImageAspectFlagBits.VK_IMAGE_ASPECT_DEPTH_BIT
             | VkImageAspectFlagBits.VK_IMAGE_ASPECT_STENCIL_BIT,
        // D16, D32_SFLOAT, X8_D24 — depth-only. Also catches non-depth
        // formats; the caller will hit Vulkan validation with a clearer
        // message than aspect=0 would have produced.
        _ => VkImageAspectFlagBits.VK_IMAGE_ASPECT_DEPTH_BIT,
    };

    // ---- Dynamic rendering ----

    public void BeginRendering(in RenderingInfo info)
    {
        // Spec floor for maxColorAttachments is 8; some GPUs allow more.
        // Stack-budget the common case and fall back to ArrayPool to keep
        // a pathological caller from blowing the recording thread's stack.
        const int Threshold = 8;
        int count = info.ColorAttachments.Length;
        Span<VkRenderingAttachmentInfo> slab = stackalloc VkRenderingAttachmentInfo[Threshold];
        Span<VkRenderingAttachmentInfo> color = RentForOverflow(count, Threshold, slab, out VkRenderingAttachmentInfo[]? rented);
        try
        {
            for (int i = 0; i < count; i++)
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
                    colorAttachmentCount = (uint)count,
                    pColorAttachments    = count > 0 ? pColor : null,
                    pDepthAttachment     = pDepth,
                    pStencilAttachment   = pStencil,
                };
                Vk.vkCmdBeginRendering(Handle, &native);
            }
        }
        finally
        {
            if (rented is not null) System.Buffers.ArrayPool<VkRenderingAttachmentInfo>.Shared.Return(rented);
        }
    }

    public void EndRendering() => Vk.vkCmdEndRendering(Handle);

    // ---- Internal: bounded-stackalloc / ArrayPool fallback ----

    /// <summary>
    /// Returns a span of <paramref name="count"/> elements that lives on
    /// the stack when <paramref name="count"/> ≤ <paramref name="stackThreshold"/>
    /// and on an <see cref="System.Buffers.ArrayPool{T}"/> rental otherwise.
    /// Caller must pre-allocate <paramref name="stackSlab"/> (of length
    /// <paramref name="stackThreshold"/>) at the callsite — this method
    /// cannot stackalloc on the caller's frame. The returned span aliases
    /// either the slab slice or the rented array; pass <paramref name="rented"/>
    /// to <c>System.Buffers.ArrayPool&lt;T&gt;.Shared.Return</c> in a finally.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Span<T> RentForOverflow<T>(
        int count,
        int stackThreshold,
        Span<T> stackSlab,
        out T[]? rented)
        where T : unmanaged
    {
        if (count <= stackThreshold)
        {
            rented = null;
            return stackSlab[..count];
        }
        rented = System.Buffers.ArrayPool<T>.Shared.Rent(count);
        return rented.AsSpan(0, count);
    }
}
