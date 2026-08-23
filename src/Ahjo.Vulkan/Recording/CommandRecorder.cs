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
/// DrawIndirectCount / DrawIndexedIndirect / DrawIndexedIndirectCount,
/// Dispatch / DispatchIndirect, pipeline barriers and split barriers
/// (SetEvent / WaitEvent / ResetEvent), and — behind
/// <c>VK_KHR_acceleration_structure</c> — BuildAccelerationStructures,
/// WriteAccelerationStructuresProperties and
/// CopyAccelerationStructure.</para>
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
    /// Per-device cached <c>vkCmd*</c> entry points. Dispatching through
    /// these skips the loader's per-call trampoline (issue #121). Returned
    /// by <c>ref readonly</c> so a recording call reads the function field
    /// directly off the owning <see cref="Device"/> with no struct copy.
    /// </summary>
    private ref readonly DeviceFunctionTable Fns => ref _pool.Device.Functions;

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
        Fns.EndCommandBuffer(Handle).ThrowIfFailed();
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
                Fns.EndCommandBuffer(Handle).ThrowIfFailed();
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
    public void BeginLabel(scoped ReadOnlySpan<byte> name, in Color color = default)
    {
        var fn = Fns.CmdBeginDebugUtilsLabel;
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
        var fn = Fns.CmdEndDebugUtilsLabel;
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
    public void InsertLabel(scoped ReadOnlySpan<byte> name, in Color color = default)
    {
        var fn = Fns.CmdInsertDebugUtilsLabel;
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
    /// cleanly. No-op when <c>VK_EXT_debug_utils</c> is not loaded, and
    /// also a no-op when <paramref name="name"/> is empty — in that case
    /// neither begin nor dispose touches the marker stack, so begin/end
    /// stay balanced for any enclosing scope.
    /// </summary>
    public DisposableLabel LabelScope(scoped ReadOnlySpan<byte> name, in Color color = default)
    {
        BeginLabel(name, in color);
        // Hand Dispose a non-null End pointer ONLY when BeginLabel actually
        // pushed a label. BeginLabel no-ops both when VK_EXT_debug_utils isn't
        // loaded (CmdBeginDebugUtilsLabel == null) AND when name is empty — in
        // either case Dispose must not pop, or it emits an unbalanced
        // vkCmdEndDebugUtilsLabelEXT (VUID-vkCmdEndDebugUtilsLabelEXT-commandBuffer-01912),
        // corrupting the marker stack of any enclosing scope.
        bool pushed = Fns.CmdBeginDebugUtilsLabel != null && !name.IsEmpty;
        return new DisposableLabel(Handle, pushed ? Fns.CmdEndDebugUtilsLabel : null);
    }

    // ---- Dynamic state ----

    public void SetViewport(in VkViewport viewport)
    {
        fixed (VkViewport* p = &viewport)
            Fns.CmdSetViewport(Handle, 0, 1, p);
    }

    public void SetScissor(in VkRect2D scissor)
    {
        fixed (VkRect2D* p = &scissor)
            Fns.CmdSetScissor(Handle, 0, 1, p);
    }

    // ---- Bind family ----

    public void BindPipeline(in ComputePipeline pipeline)
        => Fns.CmdBindPipeline(Handle, VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_COMPUTE, pipeline.Handle);

    public void BindPipeline(in GraphicsPipeline pipeline)
        => Fns.CmdBindPipeline(Handle, VkPipelineBindPoint.VK_PIPELINE_BIND_POINT_GRAPHICS, pipeline.Handle);

    public void BindDescriptorSets(
        VkPipelineBindPoint    bindPoint,
        in PipelineLayout      layout,
        uint                   firstSet,
        scoped ReadOnlySpan<DescriptorSet> sets,
        scoped ReadOnlySpan<uint> dynamicOffsets = default)
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
            Fns.CmdBindDescriptorSets(
                Handle, bindPoint, layout.Handle,
                firstSet, (uint)sets.Length, (VkDescriptorSet_T**)pSets,
                (uint)dynamicOffsets.Length, dynamicOffsets.IsEmpty ? null : pOffsets);
        }
    }

    private static void AssertSetsMatchLayout(in PipelineLayout layout, uint firstSet, scoped ReadOnlySpan<DescriptorSet> sets)
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
            Fns.CmdPushConstants(Handle, layout.Handle, (uint)stages, offset, (uint)Unsafe.SizeOf<T>(), p);
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
        scoped ReadOnlySpan<Buffer> buffers,
        scoped ReadOnlySpan<ulong>  offsets = default)
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
            Fns.CmdBindVertexBuffers(
                Handle, firstBinding, (uint)buffers.Length,
                (VkBuffer_T**)pBuffers, pOffsets);
    }

    public void BindIndexBuffer(in Buffer buffer, ulong offset, VkIndexType indexType)
        => Fns.CmdBindIndexBuffer(Handle, buffer.Handle, offset, indexType);

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
        var fn = Fns.CmdPushDescriptorSetWithTemplate;
        if (fn == null) ThrowPushDescriptorUnsupported();
        fixed (T* p = &data)
            fn(Handle, template.Handle, layout.Handle, template.Set, p);
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
        scoped ReadOnlySpan<DescriptorWrite> writes)
    {
        if (writes.IsEmpty) return;

        var cmdPushDescriptorSet = Fns.CmdPushDescriptorSet;
        if (cmdPushDescriptorSet == null) ThrowPushDescriptorUnsupported();

        const int StackThreshold = 8;
        int count = writes.Length;
        if (count <= StackThreshold)
        {
            Span<VkWriteDescriptorSet> raws = stackalloc VkWriteDescriptorSet[count];
            // Carved alongside raws by the same rule: an acceleration-structure
            // write needs a VkWriteDescriptorSetAccelerationStructureKHR chained
            // into pNext, and that node must outlive the native call.
            Span<VkWriteDescriptorSetAccelerationStructureKHR> chains =
                stackalloc VkWriteDescriptorSetAccelerationStructureKHR[count];
            FlushPush(cmdPushDescriptorSet, Handle, bindPoint, layout.Handle, set, writes, raws, chains);
            return;
        }

        VkWriteDescriptorSet[] rented =
            System.Buffers.ArrayPool<VkWriteDescriptorSet>.Shared.Rent(count);
        try
        {
            VkWriteDescriptorSetAccelerationStructureKHR[] rentedChains =
                System.Buffers.ArrayPool<VkWriteDescriptorSetAccelerationStructureKHR>.Shared.Rent(count);
            try
            {
                FlushPush(
                    cmdPushDescriptorSet, Handle, bindPoint, layout.Handle, set, writes,
                    rented.AsSpan(0, count), rentedChains.AsSpan(0, count));
            }
            finally
            {
                System.Buffers.ArrayPool<VkWriteDescriptorSetAccelerationStructureKHR>.Shared
                    .Return(rentedChains);
            }
        }
        finally
        {
            System.Buffers.ArrayPool<VkWriteDescriptorSet>.Shared.Return(rented);
        }
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void ThrowPushDescriptorUnsupported() =>
        throw new InvalidOperationException(
            "Push descriptors are not available on this device. The command was promoted to core in " +
            "Vulkan 1.4; on a 1.3 device enable VK_KHR_push_descriptor via DeviceDescription.Extensions, " +
            "and build the target set layout with DescriptorSetLayoutDescription.PushDescriptor.");

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void ThrowMeshShaderUnsupported() =>
        throw new InvalidOperationException(
            "Mesh-shader draw commands are not available on this device. " +
            MeshShaderSupport.EnableInstructions);

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void ThrowAccelerationStructureUnsupported(string what) =>
        throw new InvalidOperationException(
            what + " is not available on this device. " +
            AccelerationStructureSupport.EnableInstructions);

    private static void FlushPush(
        delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkPipelineBindPoint, VkPipelineLayout_T*, uint, uint, VkWriteDescriptorSet*, void> cmdPushDescriptorSet,
        VkCommandBuffer_T*                                 cb,
        VkPipelineBindPoint                                bindPoint,
        VkPipelineLayout_T*                                layout,
        uint                                               set,
        scoped ReadOnlySpan<DescriptorWrite>                      writes,
        scoped Span<VkWriteDescriptorSet>                         raws,
        scoped Span<VkWriteDescriptorSetAccelerationStructureKHR> chains)
    {
        // writes and chains are both pinned across BuildWrites AND the native
        // call: the produced entries point into both.
        fixed (DescriptorWrite* _ = writes)
        fixed (VkWriteDescriptorSetAccelerationStructureKHR* __ = chains)
        {
            // dstSet is ignored by vkCmdPushDescriptorSet; pass null.
            DescriptorWriteBuilder.BuildWrites(writes, setHandle: null, raws, chains);
            fixed (VkWriteDescriptorSet* pRaws = raws)
                cmdPushDescriptorSet(cb, bindPoint, layout, set, (uint)writes.Length, pRaws);
        }
    }

    // ---- Draw / Dispatch ----

    public void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0)
        => Fns.CmdDraw(Handle, vertexCount, instanceCount, firstVertex, firstInstance);

    public void DrawIndexed(
        uint indexCount,
        uint instanceCount = 1,
        uint firstIndex    = 0,
        int  vertexOffset  = 0,
        uint firstInstance = 0)
        => Fns.CmdDrawIndexed(Handle, indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);

    /// <summary>
    /// <c>vkCmdDrawIndirect</c> — reads <paramref name="drawCount"/>
    /// <c>VkDrawIndirectCommand</c> structs from
    /// <paramref name="buffer"/> at <paramref name="offset"/>,
    /// <paramref name="stride"/> bytes apart. The buffer must have been
    /// created with <see cref="BufferUsage.IndirectBuffer"/>.
    /// </summary>
    public void DrawIndirect(in Buffer buffer, ulong offset, uint drawCount, uint stride)
        => Fns.CmdDrawIndirect(Handle, buffer.Handle, offset, drawCount, stride);

    /// <summary>
    /// <c>vkCmdDrawIndirectCount</c> — like <see cref="DrawIndirect"/>, but
    /// the draw count is read from <paramref name="countBuffer"/> at
    /// <paramref name="countBufferOffset"/> (a single <c>uint32</c>) rather
    /// than passed as an immediate. Up to <paramref name="maxDrawCount"/>
    /// <c>VkDrawIndirectCommand</c> structs are read from
    /// <paramref name="buffer"/> at <paramref name="offset"/>,
    /// <paramref name="stride"/> bytes apart; the effective count is
    /// <c>min(maxDrawCount, *countBuffer)</c>. Both buffers must have been
    /// created with <see cref="BufferUsage.IndirectBuffer"/>, and the device
    /// must have the <c>drawIndirectCount</c> feature enabled (Vulkan 1.2
    /// core; flip it via
    /// <c>VkPhysicalDeviceVulkan12Features.drawIndirectCount</c> in
    /// <see cref="DeviceDescription.ConfigureFeatures"/>).
    /// </summary>
    public void DrawIndirectCount(
        in Buffer buffer,
        ulong     offset,
        in Buffer countBuffer,
        ulong     countBufferOffset,
        uint      maxDrawCount,
        uint      stride)
        => Fns.CmdDrawIndirectCount(
            Handle, buffer.Handle, offset,
            countBuffer.Handle, countBufferOffset, maxDrawCount, stride);

    /// <summary>
    /// <c>vkCmdDrawIndexedIndirect</c> — reads
    /// <paramref name="drawCount"/> <c>VkDrawIndexedIndirectCommand</c>
    /// structs from <paramref name="buffer"/>. Caller is responsible for
    /// having bound an index buffer via
    /// <see cref="BindIndexBuffer"/> beforehand.
    /// </summary>
    public void DrawIndexedIndirect(in Buffer buffer, ulong offset, uint drawCount, uint stride)
        => Fns.CmdDrawIndexedIndirect(Handle, buffer.Handle, offset, drawCount, stride);

    /// <summary>
    /// <c>vkCmdDrawIndexedIndirectCount</c> — like
    /// <see cref="DrawIndexedIndirect"/>, but the draw count is read from
    /// <paramref name="countBuffer"/> at <paramref name="countBufferOffset"/>
    /// (a single <c>uint32</c>) rather than passed as an immediate. Up to
    /// <paramref name="maxDrawCount"/> <c>VkDrawIndexedIndirectCommand</c>
    /// structs are read from <paramref name="buffer"/> at
    /// <paramref name="offset"/>, <paramref name="stride"/> bytes apart; the
    /// effective count is <c>min(maxDrawCount, *countBuffer)</c>. Both buffers
    /// must have been created with <see cref="BufferUsage.IndirectBuffer"/>,
    /// and the device must have the <c>drawIndirectCount</c> feature enabled
    /// (Vulkan 1.2 core; flip it via
    /// <c>VkPhysicalDeviceVulkan12Features.drawIndirectCount</c> in
    /// <see cref="DeviceDescription.ConfigureFeatures"/>). Caller is
    /// responsible for having bound an index buffer via
    /// <see cref="BindIndexBuffer"/> beforehand.
    /// </summary>
    public void DrawIndexedIndirectCount(
        in Buffer buffer,
        ulong     offset,
        in Buffer countBuffer,
        ulong     countBufferOffset,
        uint      maxDrawCount,
        uint      stride)
        => Fns.CmdDrawIndexedIndirectCount(
            Handle, buffer.Handle, offset,
            countBuffer.Handle, countBufferOffset, maxDrawCount, stride);

    /// <summary>
    /// <c>vkCmdDrawMeshTasksEXT</c> — launches a grid of mesh (or, when the
    /// bound pipeline has a task stage, task) workgroups. The counts are
    /// <b>workgroups</b>, not vertices, which is why the Y/Z defaults mirror
    /// <see cref="Dispatch"/> rather than <see cref="Draw"/>.
    /// </summary>
    /// <remarks>
    /// <para>Requires <c>VK_EXT_mesh_shader</c> and the <c>meshShader</c>
    /// feature on the device, and a pipeline built with
    /// <see cref="GraphicsPipelineBuilder.WithMeshStages"/>; the command must
    /// be recorded inside a <see cref="BeginRendering"/> /
    /// <see cref="EndRendering"/> scope. Throws
    /// <see cref="InvalidOperationException"/> when the extension was not
    /// enabled on the device.</para>
    /// <para><b>Bounds the wrapper does not check.</b> When the bound
    /// pipeline has a task stage, each <c>groupCount*</c> must be
    /// ≤ <c>VkPhysicalDeviceMeshShaderPropertiesEXT::maxTaskWorkGroupCount[i]</c>
    /// and their product ≤ <c>maxTaskWorkGroupTotalCount</c>
    /// (<c>VUID-vkCmdDrawMeshTasksEXT-TaskEXT-07322</c>/<c>-07323</c>/<c>-07324</c>/<c>-07325</c>);
    /// without a task stage the same bounds apply against
    /// <c>maxMeshWorkGroupCount[i]</c> / <c>maxMeshWorkGroupTotalCount</c>
    /// (<c>-07326</c>/<c>-07327</c>/<c>-07328</c>/<c>-07329</c>). Read those
    /// limits with <see cref="PhysicalDevice.TryGetMeshShaderLimits"/>: use
    /// <see cref="MeshShaderLimits.MaxTaskWorkGroupCountX"/> and its Y/Z/total
    /// siblings when the bound pipeline has a task stage, and
    /// <see cref="MeshShaderLimits.MaxMeshWorkGroupCountX"/> and its siblings
    /// when it does not.</para>
    /// </remarks>
    public void DrawMeshTasks(uint groupCountX, uint groupCountY = 1, uint groupCountZ = 1)
    {
        var fn = Fns.CmdDrawMeshTasks;
        if (fn == null) ThrowMeshShaderUnsupported();
        fn(Handle, groupCountX, groupCountY, groupCountZ);
    }

    /// <summary>
    /// <c>vkCmdDrawMeshTasksIndirectEXT</c> — reads <paramref name="drawCount"/>
    /// <c>VkDrawMeshTasksIndirectCommandEXT</c> structs (three <c>uint32</c>s,
    /// 12 bytes) from <paramref name="buffer"/> at <paramref name="offset"/>,
    /// <paramref name="stride"/> bytes apart.
    /// </summary>
    /// <remarks>
    /// <para>Requires <c>VK_EXT_mesh_shader</c> and the <c>meshShader</c>
    /// feature, a pipeline built with
    /// <see cref="GraphicsPipelineBuilder.WithMeshStages"/>, and a
    /// <see cref="BeginRendering"/> / <see cref="EndRendering"/> scope. Throws
    /// <see cref="InvalidOperationException"/> when the extension was not
    /// enabled on the device.</para>
    /// <para><b>Rules the wrapper does not check.</b>
    /// <paramref name="buffer"/> must have been created with
    /// <see cref="BufferUsage.IndirectBuffer"/>
    /// (<c>VUID-vkCmdDrawMeshTasksIndirectEXT-buffer-02709</c>);
    /// <paramref name="offset"/> must be a multiple of 4 (<c>-offset-02710</c>);
    /// a <paramref name="drawCount"/> greater than 1 requires the
    /// <c>multiDrawIndirect</c> feature (<c>-drawCount-02718</c>) and a
    /// <paramref name="stride"/> that is a multiple of 4 and at least 12
    /// (<c>-drawCount-07088</c>).</para>
    /// <para><b>Bounds the wrapper does not check.</b> The same task/mesh
    /// workgroup-count split as <see cref="DrawMeshTasks"/> applies to the
    /// <c>groupCount*</c> fields <i>inside</i> the indirect buffer, checked
    /// against the command struct rather than the call:
    /// <c>VUID-VkDrawMeshTasksIndirectCommandEXT-TaskEXT-07322</c>…<c>-07325</c>
    /// when the bound pipeline has a task stage,
    /// <c>-07326</c>…<c>-07329</c> when it does not. The wrapper cannot see
    /// those values — they are device memory — so read the limits with
    /// <see cref="PhysicalDevice.TryGetMeshShaderLimits"/> and bound whatever
    /// writes the buffer (compute shader or host fill).</para>
    /// </remarks>
    public void DrawMeshTasksIndirect(in Buffer buffer, ulong offset, uint drawCount, uint stride)
    {
        var fn = Fns.CmdDrawMeshTasksIndirect;
        if (fn == null) ThrowMeshShaderUnsupported();
        fn(Handle, buffer.Handle, offset, drawCount, stride);
    }

    /// <summary>
    /// <c>vkCmdDrawMeshTasksIndirectCountEXT</c> — like
    /// <see cref="DrawMeshTasksIndirect"/>, but the draw count is read from
    /// <paramref name="countBuffer"/> at <paramref name="countBufferOffset"/>
    /// (a single <c>uint32</c>) rather than passed as an immediate; the
    /// effective count is <c>min(maxDrawCount, *countBuffer)</c>.
    /// </summary>
    /// <remarks>
    /// <para>Requires <c>VK_EXT_mesh_shader</c> and the <c>meshShader</c>
    /// feature, a pipeline built with
    /// <see cref="GraphicsPipelineBuilder.WithMeshStages"/>, and a
    /// <see cref="BeginRendering"/> / <see cref="EndRendering"/> scope. Throws
    /// <see cref="InvalidOperationException"/> when the extension was not
    /// enabled on the device.</para>
    /// <para><b>Rules the wrapper does not check.</b> Both buffers must have
    /// been created with <see cref="BufferUsage.IndirectBuffer"/>, and the
    /// device must have the <c>drawIndirectCount</c> feature enabled (Vulkan
    /// 1.2 core; flip it via
    /// <c>VkPhysicalDeviceVulkan12Features.drawIndirectCount</c> in
    /// <see cref="DeviceDescription.ConfigureFeatures"/>) —
    /// <c>VUID-vkCmdDrawMeshTasksIndirectCountEXT-None-04445</c>.
    /// <paramref name="countBufferOffset"/> must be a multiple of 4
    /// (<c>-countBufferOffset-02716</c>), and <paramref name="stride"/> a
    /// multiple of 4 and at least 12 — the size of
    /// <c>VkDrawMeshTasksIndirectCommandEXT</c> (<c>-stride-07096</c>).</para>
    /// <para><b>Bounds the wrapper does not check.</b> As for
    /// <see cref="DrawMeshTasksIndirect"/>, the <c>groupCount*</c> fields
    /// <i>inside</i> the indirect buffer carry the same task/mesh split as
    /// <see cref="DrawMeshTasks"/>:
    /// <c>VUID-VkDrawMeshTasksIndirectCommandEXT-TaskEXT-07322</c>…<c>-07325</c>
    /// with a task stage, <c>-07326</c>…<c>-07329</c> without one. Read the
    /// limits with <see cref="PhysicalDevice.TryGetMeshShaderLimits"/> and
    /// bound the producer that writes the buffer — for the
    /// compute-writes-the-count shape this overload exists for, that is the
    /// compute shader, not this call site.</para>
    /// </remarks>
    public void DrawMeshTasksIndirectCount(
        in Buffer buffer,
        ulong     offset,
        in Buffer countBuffer,
        ulong     countBufferOffset,
        uint      maxDrawCount,
        uint      stride)
    {
        var fn = Fns.CmdDrawMeshTasksIndirectCount;
        if (fn == null) ThrowMeshShaderUnsupported();
        fn(Handle, buffer.Handle, offset, countBuffer.Handle, countBufferOffset, maxDrawCount, stride);
    }

    public void Dispatch(uint groupCountX, uint groupCountY = 1, uint groupCountZ = 1)
        => Fns.CmdDispatch(Handle, groupCountX, groupCountY, groupCountZ);

    /// <summary>
    /// <c>vkCmdDispatchIndirect</c> — reads one
    /// <c>VkDispatchIndirectCommand</c> from <paramref name="buffer"/> at
    /// <paramref name="offset"/>.
    /// </summary>
    public void DispatchIndirect(in Buffer buffer, ulong offset)
        => Fns.CmdDispatchIndirect(Handle, buffer.Handle, offset);

    // ---- Pipeline barriers + split barriers (sync2) ----

    /// <summary>
    /// Which dependency command <see cref="RecordDependency"/> ends in.
    /// </summary>
    private enum DependencyOp
    {
        Barrier,
        SetEvent,
        WaitEvent,
    }

    /// <summary>
    /// Issues one <c>vkCmdPipelineBarrier2</c> for an arbitrary mix of
    /// memory / buffer / image barriers. Vulkan rewards batching — the
    /// API enforces a single underlying call regardless of how many
    /// barriers the caller supplies. Pass <c>default</c> for any kind
    /// you don't need.
    /// </summary>
    public void PipelineBarrier(
        scoped ReadOnlySpan<MemoryBarrier> memory,
        scoped ReadOnlySpan<BufferBarrier> buffer,
        scoped ReadOnlySpan<ImageBarrier>  image)
    {
        // The empty-mix early return belongs here, not in RecordDependency:
        // skipping the driver call is right for a barrier that orders
        // nothing, but dropping a vkCmdSetEvent2 would discard a signal the
        // paired vkCmdWaitEvents2 blocks on forever.
        if (memory.IsEmpty && buffer.IsEmpty && image.IsEmpty) return;
        RecordDependency(DependencyOp.Barrier, null, memory, buffer, image);
    }

    /// <summary>Image-only convenience overload — the dominant case.</summary>
    public void PipelineBarrier(scoped ReadOnlySpan<ImageBarrier> image)
        => PipelineBarrier(default, default, image);

    /// <summary>Single image-barrier convenience overload.</summary>
    public void PipelineBarrier(in ImageBarrier image)
        => PipelineBarrier(default, default,
            MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in image), 1));

    /// <summary>
    /// Signals <paramref name="evt"/> via <c>vkCmdSetEvent2</c> — the
    /// producer half of a split barrier. The event is signaled once the
    /// union of the barriers' <c>SrcStage</c> masks has completed; the
    /// dependency's second half (the destination scopes and any layout
    /// transitions) is applied at the matching
    /// <see cref="WaitEvent"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>The dependency must match the wait's exactly.</b> The
    /// <c>VkDependencyInfo</c> recorded here has to be equal to the one
    /// passed to <see cref="WaitEvent"/>
    /// (<c>VUID-vkCmdWaitEvents2-pEvents-10788</c>). Hold one barrier list
    /// — a field or a local array — and pass it to both calls; the wrapper
    /// does not enforce this, but routing both through one marshalling
    /// implementation guarantees equal inputs produce byte-identical
    /// structs.</para>
    /// <para>Must be recorded <b>outside</b> a
    /// <see cref="BeginRendering"/>/<see cref="EndRendering"/> scope
    /// (<c>VUID-vkCmdSetEvent2-renderpass</c>), and no barrier may carry
    /// <see cref="Stage.Host"/>
    /// (<c>VUID-vkCmdSetEvent2-srcStageMask-09391</c>,
    /// <c>-dstStageMask-09392</c>).</para>
    /// <para><b>Illegal on a transfer-only queue family.</b> Unlike
    /// <see cref="PipelineBarrier"/>, all three split-barrier commands
    /// require a <see cref="CommandBufferPool"/> whose queue family supports
    /// graphics, compute, or video — <c>VK_QUEUE_TRANSFER_BIT</c> alone is
    /// not enough (<c>VUID-vkCmdSetEvent2-commandBuffer-cmdpool</c>,
    /// and the <c>-cmdpool</c> VUIDs on <c>vkCmdWaitEvents2</c> /
    /// <c>vkCmdResetEvent2</c>). A hazard split across a dedicated transfer
    /// queue has to use <see cref="PipelineBarrier"/> plus a semaphore
    /// instead.</para>
    /// <para>Unlike <see cref="PipelineBarrier"/> this never early-returns
    /// on an all-empty mix: dropping the call would silently discard the
    /// signal and hang the paired wait. Under
    /// <see cref="AhjoValidation.Enabled"/> an empty mix (or a null event)
    /// fails instead.</para>
    /// </remarks>
    public void SetEvent(
        in Event evt,
        scoped ReadOnlySpan<MemoryBarrier> memory,
        scoped ReadOnlySpan<BufferBarrier> buffer,
        scoped ReadOnlySpan<ImageBarrier>  image)
    {
        AssertSplitBarrierUsable("SetEvent", in evt, memory, buffer, image);
        RecordDependency(DependencyOp.SetEvent, evt.Handle, memory, buffer, image);
    }

    /// <summary>
    /// Waits on <paramref name="evt"/> via <c>vkCmdWaitEvents2</c> with
    /// <c>eventCount = 1</c> — the consumer half of a split barrier.
    /// </summary>
    /// <remarks>
    /// <para>The event must have been signaled by a corresponding
    /// <see cref="SetEvent"/> <b>earlier in submission order</b>
    /// (<c>VUID-vkCmdWaitEvents2-pEvents-03841</c>). A wait with no
    /// preceding set hangs the queue, and the wrapper cannot detect that at
    /// record time — the pairing spans command buffers and submissions.</para>
    /// <para>The dependency passed here must be exactly equal to the one
    /// recorded at <see cref="SetEvent"/>
    /// (<c>VUID-vkCmdWaitEvents2-pEvents-10788</c>) — pass the same barrier
    /// list to both calls.</para>
    /// <para>Unlike <see cref="SetEvent"/> and <see cref="ResetEvent"/>,
    /// this <em>may</em> be recorded inside a render pass instance, provided
    /// no barrier's <c>SrcStage</c> includes <see cref="Stage.Host"/>
    /// (<c>VUID-vkCmdWaitEvents2-dependencyFlags-03844</c> constrains only the
    /// source mask, and only inside a render pass instance — a
    /// <c>DstStage</c> of <see cref="Stage.Host"/> stays legal).</para>
    /// <para>The multi-event form of <c>vkCmdWaitEvents2</c> is not wrapped:
    /// it needs one <c>VkDependencyInfo</c> per event and no caller batches
    /// hazards today.</para>
    /// <para>Requires a non-transfer-only queue family — see
    /// <see cref="SetEvent"/>.</para>
    /// </remarks>
    public void WaitEvent(
        in Event evt,
        scoped ReadOnlySpan<MemoryBarrier> memory,
        scoped ReadOnlySpan<BufferBarrier> buffer,
        scoped ReadOnlySpan<ImageBarrier>  image)
    {
        AssertSplitBarrierUsable("WaitEvent", in evt, memory, buffer, image);
        RecordDependency(DependencyOp.WaitEvent, evt.Handle, memory, buffer, image);
    }

    /// <summary>
    /// Returns <paramref name="evt"/> to the unsignaled state via
    /// <c>vkCmdResetEvent2</c> so it can be reused on a later frame.
    /// </summary>
    /// <remarks>
    /// <para><b>Record the reset in a submission ordered after the wait
    /// completed</b> — the frame-N+1 command buffer for a frame-N event, or
    /// after an intervening <see cref="PipelineBarrier"/>. The spec requires
    /// an execution dependency between the reset and any wait on the same
    /// event (<c>VUID-vkCmdResetEvent2-event-03831</c>, <c>-03832</c>);
    /// resetting in the same command buffer as the wait is a validation
    /// error.</para>
    /// <para><paramref name="stageMask"/> must not include
    /// <see cref="Stage.Host"/> (<c>VUID-vkCmdResetEvent2-stageMask-03830</c>),
    /// and the command must be recorded outside a render pass instance
    /// (<c>VUID-vkCmdResetEvent2-renderpass</c>).</para>
    /// <para>Requires a non-transfer-only queue family — see
    /// <see cref="SetEvent"/>.</para>
    /// </remarks>
    public void ResetEvent(in Event evt, Stage stageMask)
    {
        // VUID-vkCmdResetEvent2-event-parameter has no VK_NULL_HANDLE
        // exemption, so a null handle is rejected here for the same reason
        // AssertSplitBarrierUsable rejects it on SetEvent/WaitEvent. The
        // empty-mix half of that helper doesn't apply — there is no
        // dependency to be empty.
        if (AhjoValidation.IsEnabled && evt.IsNull)
            AhjoValidation.Fail("CommandRecorder",
                "ResetEvent: event is a null handle. Create one with Device.CreateEvent().");

        Fns.CmdResetEvent2(Handle, evt.Handle, (ulong)stageMask);
    }

    /// <summary>
    /// Marshals a barrier mix into one <c>VkDependencyInfo</c> and dispatches
    /// it as a pipeline barrier, an event signal, or an event wait. Sharing
    /// one implementation is what makes "the Set and the Wait produce
    /// byte-identical <c>VkDependencyInfo</c>s from equal inputs"
    /// (<c>VUID-vkCmdWaitEvents2-pEvents-10788</c>) a structural property
    /// rather than a review obligation.
    /// </summary>
    /// <remarks>
    /// Performs no empty-mix check — see <see cref="PipelineBarrier"/>, which
    /// keeps that early return at its public entry point.
    /// </remarks>
    private void RecordDependency(
        DependencyOp                op,
        VkEvent_T*                  @event,
        scoped ReadOnlySpan<MemoryBarrier> memory,
        scoped ReadOnlySpan<BufferBarrier> buffer,
        scoped ReadOnlySpan<ImageBarrier>  image)
    {
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
                switch (op)
                {
                    case DependencyOp.Barrier:  Fns.CmdPipelineBarrier2(Handle, &dep); break;
                    case DependencyOp.SetEvent: Fns.CmdSetEvent2(Handle, @event, &dep); break;
                    default:
                    {
                        VkEvent_T* e = @event;
                        Fns.CmdWaitEvents2(Handle, 1, &e, &dep);
                        break;
                    }
                }
            }
        }
        finally
        {
            if (mRent is not null) System.Buffers.ArrayPool<VkMemoryBarrier2>.Shared.Return(mRent);
            if (bRent is not null) System.Buffers.ArrayPool<VkBufferMemoryBarrier2>.Shared.Return(bRent);
            if (iRent is not null) System.Buffers.ArrayPool<VkImageMemoryBarrier2>.Shared.Return(iRent);
        }
    }

    private static void AssertSplitBarrierUsable(
        string caller,
        in Event evt,
        scoped ReadOnlySpan<MemoryBarrier> memory,
        scoped ReadOnlySpan<BufferBarrier> buffer,
        scoped ReadOnlySpan<ImageBarrier>  image)
    {
        if (!AhjoValidation.IsEnabled) return;

        if (evt.IsNull)
            AhjoValidation.Fail("CommandRecorder",
                $"{caller}: event is a null handle. Create one with Device.CreateEvent().");

        if (memory.IsEmpty && buffer.IsEmpty && image.IsEmpty)
            AhjoValidation.Fail("CommandRecorder",
                $"{caller}: the dependency is empty. A split barrier with no barriers has an empty " +
                "synchronization scope and orders nothing — the paired wait would block on a signal that " +
                "means nothing. Pass at least one barrier (e.g. " +
                "MemoryBarrier.Between(srcStage, Access.None, dstStage, Access.None)).");
    }

    // ---- Timestamp queries ----

    /// <summary>
    /// Resets <paramref name="queryCount"/> queries of
    /// <paramref name="pool"/> starting at <paramref name="firstQuery"/> to
    /// the unavailable state via <c>vkCmdResetQueryPool</c>.
    /// </summary>
    /// <remarks>
    /// <para><b>Reset before use.</b> Every query must be reset by a
    /// <b>submitted</b> reset before its first
    /// <see cref="WriteTimestamp"/> and between reuses
    /// (<c>VUID-vkCmdWriteTimestamp2-None-03864</c>). The idiomatic
    /// per-frame shape is one <see cref="ResetQueryPool"/> over the frame's
    /// query range at the top of the frame's command buffer.</para>
    /// <para>Must be recorded <b>outside</b> a
    /// <see cref="BeginRendering"/>/<see cref="EndRendering"/> scope
    /// (<c>VUID-vkCmdResetQueryPool-renderpass</c>), and requires a
    /// graphics/compute-capable queue family — a transfer-only pool cannot
    /// record it (<c>VUID-vkCmdResetQueryPool-commandBuffer-cmdpool</c>),
    /// unlike <see cref="WriteTimestamp"/>.</para>
    /// </remarks>
    public void ResetQueryPool(in QueryPool pool, uint firstQuery, uint queryCount)
    {
        if (AhjoValidation.IsEnabled)
        {
            if (pool.IsNull)
                AhjoValidation.Fail("CommandRecorder",
                    "ResetQueryPool: query pool is a null handle. Create one with Device.CreateQueryPool(count).");
            // Widened to ulong before adding: uint arithmetic would wrap
            // (e.g. firstQuery = 0xFFFF_FFFE, queryCount = 4 → 2) and let an
            // out-of-range reset slip past the guard.
            if (pool.QueryCount != 0 && (ulong)firstQuery + queryCount > pool.QueryCount)
                AhjoValidation.Fail("CommandRecorder",
                    $"ResetQueryPool: range [{firstQuery}, {(ulong)firstQuery + queryCount}) exceeds the pool's "
                    + $"queryCount ({pool.QueryCount}).");
        }
        Fns.CmdResetQueryPool(Handle, pool.Handle, firstQuery, queryCount);
    }

    /// <summary>
    /// Writes a timestamp into query <paramref name="query"/> of
    /// <paramref name="pool"/> via <c>vkCmdWriteTimestamp2</c>: the value
    /// latches when all previously submitted commands have completed
    /// <paramref name="stage"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Bracket idiom.</b> Begin the measured span with
    /// <see cref="Stage.TopOfPipe"/> and end it with
    /// <see cref="Stage.BottomOfPipe"/> (or use
    /// <see cref="Stage.AllCommands"/>).</para>
    /// <para><paramref name="stage"/> must include <b>exactly one</b>
    /// pipeline stage (<c>VUID-vkCmdWriteTimestamp2-stage-03859</c>) —
    /// <see cref="Stage.None"/> and multi-bit masks are invalid; meta-flags
    /// like <see cref="Stage.AllCommands"/> are single bits and legal.</para>
    /// <para>The query must have been reset by a <b>submitted</b>
    /// <see cref="ResetQueryPool"/> since its last use
    /// (<c>VUID-vkCmdWriteTimestamp2-None-03864</c>).</para>
    /// <para>The queue family must report non-zero
    /// <see cref="QueueFamilyInfo.TimestampValidBits"/>
    /// (<c>VUID-vkCmdWriteTimestamp2-timestampValidBits-03863</c>) — check
    /// it in the device picker. Unlike <see cref="ResetQueryPool"/>, this
    /// is legal inside a rendering scope and on transfer-only queue
    /// families (<c>VUID-vkCmdWriteTimestamp2-commandBuffer-cmdpool</c>
    /// includes transfer).</para>
    /// </remarks>
    public void WriteTimestamp(in QueryPool pool, Stage stage, uint query)
    {
        if (AhjoValidation.IsEnabled)
        {
            if (pool.IsNull)
                AhjoValidation.Fail("CommandRecorder",
                    "WriteTimestamp: query pool is a null handle. Create one with Device.CreateQueryPool(count).");
            if (System.Numerics.BitOperations.PopCount((ulong)stage) != 1)
                AhjoValidation.Fail("CommandRecorder",
                    "WriteTimestamp: stage must be exactly one Stage bit "
                    + "(VUID-vkCmdWriteTimestamp2-stage-03859); Stage.None and multi-bit masks are invalid.");
            if (pool.QueryCount != 0 && query >= pool.QueryCount)
                AhjoValidation.Fail("CommandRecorder",
                    $"WriteTimestamp: query {query} is out of range for the pool's queryCount ({pool.QueryCount}).");
            // The reciprocal of the type check in
            // WriteAccelerationStructuresProperties. Before #202 the wrapper
            // could only mint timestamp pools, so this could not be got wrong;
            // Device.CreateQueryPool(QueryType, uint) makes a compacted-size
            // pool reachable here. Unknown is a borrowed pool, whose type the
            // wrapper never learned — not enforceable, so it is let through,
            // matching how QueryCount == 0 is treated above.
            if (pool.Type != QueryType.Unknown && pool.Type != QueryType.Timestamp)
                AhjoValidation.Fail("CommandRecorder",
                    $"WriteTimestamp: the pool's type is {pool.Type}, but vkCmdWriteTimestamp2 requires a "
                    + "QueryType.Timestamp pool "
                    + "(VUID-vkCmdWriteTimestamp2-queryPool-03861). Mint one with "
                    + "Device.CreateQueryPool(count).");
        }
        Fns.CmdWriteTimestamp2(Handle, (ulong)stage, pool.Handle, query);
    }

    // ---- Acceleration structures (VK_KHR_acceleration_structure) ----

    // The batch and geometry counts below which BuildAccelerationStructures
    // stackallocs its three native scratch spans instead of renting. The
    // per-frame shape this path exists for is a single TLAS rebuild — one
    // build, one Instances geometry — so 8 and 16 clear it by three orders of
    // magnitude, while load-time BLAS batches (which are not per-frame and can
    // afford a pooled rental) fall through to ArrayPool. Worst case on the
    // stack path is roughly 2.2 KB. Reasoned, not measured: if a consumer
    // turns up that batches ~64 builds per frame, move these with a
    // measurement behind them.
    private const int BuildStackThreshold    = 8;
    private const int GeometryStackThreshold = 16;

    /// <summary>
    /// <c>vkCmdBuildAccelerationStructuresKHR</c> — records a batch of
    /// acceleration-structure builds. Each entry of <paramref name="builds"/>
    /// names its destination, its mode and flags, its caller-owned scratch
    /// address, and a <c>(FirstGeometry, GeometryCount)</c> slice of the other
    /// two spans.
    /// </summary>
    /// <param name="builds">The batch. An empty span is a no-op.</param>
    /// <param name="geometries">
    /// The flat geometry span the builds slice into.
    /// </param>
    /// <param name="ranges">
    /// One <see cref="AccelerationStructureBuildRange"/> per geometry, indexed
    /// <b>identically</b> to <paramref name="geometries"/> — the two spans must
    /// be the same length. Cast in place to
    /// <c>VkAccelerationStructureBuildRangeInfoKHR</c>, never copied.
    /// </param>
    /// <remarks>
    /// <para><b>The CSR contract.</b> One
    /// <see cref="AccelerationStructureBuild.FirstGeometry"/> /
    /// <see cref="AccelerationStructureBuild.GeometryCount"/> pair slices both
    /// <paramref name="geometries"/> and <paramref name="ranges"/>, because
    /// Vulkan pairs exactly one range with each geometry. See
    /// <see cref="AccelerationStructureBuild"/> for the worked
    /// example.</para>
    /// <para><b>Scratch rules, none of which the wrapper can check.</b> Size
    /// each build's scratch from
    /// <see cref="AccelerationStructureBuildSizes.BuildScratchSize"/> (or
    /// <see cref="AccelerationStructureBuildSizes.UpdateScratchSize"/> for
    /// <see cref="AccelerationStructureBuildMode.Update"/>); align the
    /// <b>address</b> to
    /// <see cref="AccelerationStructureLimits.MinScratchOffsetAlignment"/>
    /// (<c>VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-03710</c>); create
    /// the buffer with <see cref="BufferUsage.StorageBuffer"/> |
    /// <see cref="BufferUsage.ShaderDeviceAddress"/>; and give <b>every build in
    /// this one call a non-overlapping scratch range</b>
    /// (<c>-scratchData-03704</c>), because builds within one call may execute
    /// concurrently.</para>
    /// <para><b>Scope and queue.</b> Must be recorded <b>outside</b> a
    /// <see cref="BeginRendering"/> / <see cref="EndRendering"/> scope
    /// (<c>VUID-vkCmdBuildAccelerationStructuresKHR-renderpass</c>), from a
    /// pool whose queue family supports <c>VK_QUEUE_COMPUTE_BIT</c>
    /// (<c>-commandBuffer-cmdpool</c>).</para>
    /// <para><b>The barrier a consumer needs.</b> A build is not visible to
    /// anything that reads it until you barrier
    /// <see cref="Stage.AccelerationStructureBuild"/> /
    /// <see cref="Access.AccelerationStructureWrite"/> → the consuming stage /
    /// <see cref="Access.AccelerationStructureRead"/>. For a ray-query
    /// traversal the consuming stage is the shader stage that runs the query
    /// (<see cref="Stage.ComputeShader"/> /
    /// <see cref="Stage.FragmentShader"/>), never an RT-pipeline stage; for a
    /// TLAS build over freshly built BLASes, or for a compacted-size query, it
    /// is <see cref="Stage.AccelerationStructureBuild"/> again.</para>
    /// <para><b>Lifetime.</b> The destination structures <em>and their
    /// buffers</em>, any update sources and theirs, every scratch range, and
    /// every buffer behind an address in <paramref name="geometries"/> must
    /// stay alive, resident and unmodified until the build completes on the
    /// GPU.</para>
    /// <para>Allocates zero per call when
    /// <paramref name="builds"/> has <c>≤ 8</c> entries and
    /// <paramref name="geometries"/> has <c>≤ 16</c>; larger batches rent from
    /// <see cref="ArrayPool{T}"/>.</para>
    /// <para>All three parameters are <c>scoped</c>: nothing here outlives the
    /// call, and saying so is what lets a caller pass a <c>stackalloc</c> to a
    /// <c>ref struct</c> receiver — the per-frame TLAS shape this method exists
    /// for. Without it the compiler must assume the recorder could capture the
    /// spans (CS9080) and only heap arrays would compile.</para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// <c>VK_KHR_acceleration_structure</c> was not enabled on this device.
    /// </exception>
    public void BuildAccelerationStructures(
        scoped ReadOnlySpan<AccelerationStructureBuild>      builds,
        scoped ReadOnlySpan<AccelerationStructureGeometry>   geometries,
        scoped ReadOnlySpan<AccelerationStructureBuildRange> ranges)
    {
        // Unconditional, deliberately not behind AhjoValidation: IsEnabled is
        // false in Release, which is exactly the build where dispatching
        // through a null pointer is an access violation (the DrawMeshTasks
        // precedent).
        var fn = Fns.CmdBuildAccelerationStructures;
        if (fn == null) ThrowAccelerationStructureUnsupported("BuildAccelerationStructures");

        // An empty batch is a no-op — the CopyBuffer empty-span precedent, and
        // vkCmdBuildAccelerationStructuresKHR requires infoCount > 0 anyway.
        if (builds.IsEmpty) return;

        // Unconditional, for the same reason the null-pointer check above is:
        // these two are MEMORY SAFETY, not valid usage. The translator turns
        // FirstGeometry/GeometryCount straight into a pointer offset and a
        // count over the native geometry buffer, so an out-of-range slice
        // makes the driver read past the end of a stackalloc (or a pooled
        // array) and interpret whatever is there as sType and device
        // addresses. Nothing can diagnose that: the pointers are structurally
        // valid, so the validation layer sees a well-formed call. That is
        // what makes this unlike the ResetQueryPool range guard, where an
        // out-of-range value reaches the driver as a value and the layer
        // catches it — and AhjoValidation.IsEnabled is false in Release,
        // exactly the build where this would corrupt.
        ValidateBuildSlices(builds, geometries, ranges);

        // The remaining guards are valid-usage checks the layer also catches,
        // so they stay gated.
        if (AhjoValidation.IsEnabled) AssertBuildsValid(builds, geometries, ranges);

        int buildCount = builds.Length;
        int geoCount   = geometries.Length;

        if (buildCount <= BuildStackThreshold && geoCount <= GeometryStackThreshold)
        {
            Span<VkAccelerationStructureBuildGeometryInfoKHR> infos =
                stackalloc VkAccelerationStructureBuildGeometryInfoKHR[buildCount];
            Span<VkAccelerationStructureGeometryKHR> natives =
                stackalloc VkAccelerationStructureGeometryKHR[geoCount];
            Span<nint> ppRanges = stackalloc nint[buildCount];
            RecordBuilds(fn, Handle, builds, geometries, ranges, infos, natives, ppRanges);
            return;
        }

        var infoPool   = System.Buffers.ArrayPool<VkAccelerationStructureBuildGeometryInfoKHR>.Shared;
        var geoPool    = System.Buffers.ArrayPool<VkAccelerationStructureGeometryKHR>.Shared;
        var rangePool  = System.Buffers.ArrayPool<nint>.Shared;

        VkAccelerationStructureBuildGeometryInfoKHR[] rentedInfos = infoPool.Rent(buildCount);
        try
        {
            VkAccelerationStructureGeometryKHR[] rentedGeos = geoPool.Rent(geoCount);
            try
            {
                nint[] rentedRanges = rangePool.Rent(buildCount);
                try
                {
                    RecordBuilds(
                        fn, Handle, builds, geometries, ranges,
                        rentedInfos.AsSpan(0, buildCount),
                        rentedGeos.AsSpan(0, geoCount),
                        rentedRanges.AsSpan(0, buildCount));
                }
                finally { rangePool.Return(rentedRanges); }
            }
            finally { geoPool.Return(rentedGeos); }
        }
        finally { infoPool.Return(rentedInfos); }
    }

    // The post-carve half of BuildAccelerationStructures, factored out so the
    // stackalloc and ArrayPool paths share one body (the FlushPush split).
    // Every span reaching the translator is pinned here and stays pinned
    // across the native call — the native structs point into all of them.
    private static void RecordBuilds(
        delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, uint, VkAccelerationStructureBuildGeometryInfoKHR*, VkAccelerationStructureBuildRangeInfoKHR**, void> fn,
        VkCommandBuffer_T*                                       cb,
        scoped ReadOnlySpan<AccelerationStructureBuild>          builds,
        scoped ReadOnlySpan<AccelerationStructureGeometry>       geometries,
        scoped ReadOnlySpan<AccelerationStructureBuildRange>     ranges,
        scoped Span<VkAccelerationStructureBuildGeometryInfoKHR> infos,
        scoped Span<VkAccelerationStructureGeometryKHR>          natives,
        scoped Span<nint>                                        ppRanges)
    {
        fixed (AccelerationStructureBuildRange* pRangesManaged = ranges)
        fixed (VkAccelerationStructureGeometryKHR* pNatives = natives)
        fixed (VkAccelerationStructureBuildGeometryInfoKHR* pInfos = infos)
        fixed (nint* pppRanges = ppRanges)
        {
            // AccelerationStructureBuildRange is an exact layout mirror of
            // VkAccelerationStructureBuildRangeInfoKHR (four uints, pinned by
            // a test), so this is a pointer cast, not a copy.
            var pRanges = (VkAccelerationStructureBuildRangeInfoKHR*)pRangesManaged;
            var ppr     = (VkAccelerationStructureBuildRangeInfoKHR**)pppRanges;

            AccelerationStructureBuildTranslator.BuildGeometryInfos(
                builds, geometries, pRanges, pNatives, pInfos, ppr);

            fn(cb, (uint)builds.Length, pInfos, ppr);
        }
    }

    /// <summary>
    /// The subset of <see cref="BuildAccelerationStructures"/>'s checks that
    /// must run in <b>every</b> build configuration, because the translator
    /// consumes these values as raw pointer arithmetic over caller-sized
    /// buffers rather than passing them to the driver as values. A violation
    /// here is out-of-bounds memory, not a validation error, so it throws
    /// rather than routing through <see cref="AhjoValidation"/>.
    /// </summary>
    private static void ValidateBuildSlices(
        scoped ReadOnlySpan<AccelerationStructureBuild>      builds,
        scoped ReadOnlySpan<AccelerationStructureGeometry>   geometries,
        scoped ReadOnlySpan<AccelerationStructureBuildRange> ranges)
    {
        // ppBuildRangeInfos[b] is sliced from the ranges span at the same
        // offset and count as pGeometries is from the geometry span, so a
        // shorter ranges span is read out of bounds by the driver.
        if (ranges.Length != geometries.Length)
            throw new ArgumentException(
                $"BuildAccelerationStructures: ranges has {ranges.Length} entries but geometries has "
                + $"{geometries.Length}. Vulkan pairs exactly one build range with each geometry, so the "
                + "two spans must be the same length and are indexed identically.", nameof(ranges));

        for (int b = 0; b < builds.Length; b++)
        {
            ref readonly AccelerationStructureBuild build = ref builds[b];

            if (build.GeometryCount == 0)
                throw new ArgumentException(
                    $"BuildAccelerationStructures: builds[{b}].GeometryCount is 0; a build must carry at "
                    + "least one geometry.", nameof(builds));

            // Widened to ulong before adding: uint arithmetic would wrap
            // (e.g. FirstGeometry = 0xFFFF_FFFE, GeometryCount = 4 -> 2) and
            // let an out-of-range slice past the guard.
            if ((ulong)build.FirstGeometry + build.GeometryCount > (ulong)geometries.Length)
                throw new ArgumentOutOfRangeException(nameof(builds),
                    $"BuildAccelerationStructures: builds[{b}] slices geometries["
                    + $"{build.FirstGeometry}, {(ulong)build.FirstGeometry + build.GeometryCount}) "
                    + $"but only {geometries.Length} geometries were passed. The slice is used as a raw "
                    + "pointer offset into the native geometry buffer, so an out-of-range value would "
                    + "have the driver read uninitialized memory.");
        }
    }

    private static void AssertBuildsValid(
        scoped ReadOnlySpan<AccelerationStructureBuild>      builds,
        scoped ReadOnlySpan<AccelerationStructureGeometry>   geometries,
        scoped ReadOnlySpan<AccelerationStructureBuildRange> ranges)
    {
        // Span lengths and every build's slice are already proven in range by
        // the unconditional ValidateBuildSlices, so the indexing below is safe.
        _ = ranges;

        for (int b = 0; b < builds.Length; b++)
        {
            ref readonly AccelerationStructureBuild build = ref builds[b];

            if (build.Destination.IsNull)
                AhjoValidation.Fail("CommandRecorder",
                    $"BuildAccelerationStructures: builds[{b}].Destination is a null handle. Create one "
                    + "with Device.CreateAccelerationStructure.");

            if (build.Mode == AccelerationStructureBuildMode.Update)
            {
                if (build.Source.IsNull)
                    AhjoValidation.Fail("CommandRecorder",
                        $"BuildAccelerationStructures: builds[{b}].Mode is Update but Source is a null "
                        + "handle; an update must name the structure it refits "
                        + "(VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-04630). The source must also "
                        + "have been built with AccelerationStructureBuildFlags.AllowUpdate "
                        + "(-pInfos-03667), which the wrapper cannot see.");
            }
            else if (!build.Source.IsNull)
            {
                AhjoValidation.Fail("CommandRecorder",
                    $"BuildAccelerationStructures: builds[{b}].Mode is Build but Source is non-null; a "
                    + "from-scratch build must leave Source at default. Set Mode = Update to refit.");
            }

            // The type/kind pairing. This is the guard that catches the
            // AccelerationStructureType.TopLevel == 0 footgun.
            if (build.Type == AccelerationStructureType.TopLevel)
            {
                if (build.GeometryCount != 1)
                    AhjoValidation.Fail("CommandRecorder",
                        $"BuildAccelerationStructures: builds[{b}].Type is TopLevel but GeometryCount is "
                        + $"{build.GeometryCount}; a top-level build must carry exactly one geometry "
                        + "(VUID-VkAccelerationStructureBuildGeometryInfoKHR-type-03790). Note TopLevel is "
                        + "the DEFAULT value of AccelerationStructureType — a bottom-level build must set "
                        + "Type = AccelerationStructureType.BottomLevel explicitly.");
                else if (geometries[(int)build.FirstGeometry].Kind != GeometryKind.Instances)
                    AhjoValidation.Fail("CommandRecorder",
                        $"BuildAccelerationStructures: builds[{b}].Type is TopLevel but its geometry is "
                        + $"{geometries[(int)build.FirstGeometry].Kind}, not Instances; a top-level build's "
                        + "geometry must be Instances "
                        + "(VUID-VkAccelerationStructureBuildGeometryInfoKHR-type-03789). Note TopLevel is "
                        + "the DEFAULT value of AccelerationStructureType — a bottom-level build must set "
                        + "Type = AccelerationStructureType.BottomLevel explicitly.");
            }
            else if (build.Type == AccelerationStructureType.BottomLevel)
            {
                for (uint g = 0; g < build.GeometryCount; g++)
                {
                    int gi = (int)(build.FirstGeometry + g);
                    if (geometries[gi].Kind == GeometryKind.Instances)
                        AhjoValidation.Fail("CommandRecorder",
                            $"BuildAccelerationStructures: builds[{b}].Type is BottomLevel but "
                            + $"geometries[{gi}] is Instances; instance geometry belongs to a top-level "
                            + "build only "
                            + "(VUID-VkAccelerationStructureBuildGeometryInfoKHR-type-03791).");
                }
            }

            // Scratch aliasing, exact matches only. Builds batched into one
            // call may execute concurrently, so their scratch ranges must not
            // overlap (VUID-vkCmdBuildAccelerationStructuresKHR-scratchData-03704).
            // General overlap is undecidable here — ScratchAddress is a bare
            // device address with no length attached, and the wrapper never
            // sees the sizes — but two builds pointing at the SAME address is
            // decidable, is the natural first mistake with a batched API (reuse
            // one scratch buffer for the whole batch), and is a violation
            // whenever either build actually consumes scratch. O(n squared),
            // but only under AhjoValidation and only over the batch.
            if (build.ScratchAddress != 0)
            {
                for (int other = 0; other < b; other++)
                    if (builds[other].ScratchAddress == build.ScratchAddress)
                        AhjoValidation.Fail("CommandRecorder",
                            $"BuildAccelerationStructures: builds[{b}] and builds[{other}] share the "
                            + $"scratch address 0x{build.ScratchAddress:X}. Builds in one call may run "
                            + "concurrently, so each needs its own non-overlapping scratch range "
                            + "(VUID-vkCmdBuildAccelerationStructuresKHR-scratchData-03704): suballocate "
                            + "one scratch buffer per build, each sized from "
                            + "AccelerationStructureBuildSizes.BuildScratchSize (or UpdateScratchSize) and "
                            + "aligned to AccelerationStructureLimits.MinScratchOffsetAlignment. (The "
                            + "wrapper flags only exact matches; it cannot see the ranges' sizes, so a "
                            + "partial overlap still reaches the driver. If both builds genuinely need "
                            + "zero scratch this check is over-strict — pass 0 for those.)");
            }
        }
    }

    /// <summary>
    /// <c>vkCmdWriteAccelerationStructuresPropertiesKHR</c> — writes one query
    /// per entry of <paramref name="structures"/> into <paramref name="pool"/>,
    /// starting at <paramref name="firstQuery"/>. With a
    /// <see cref="QueryType.AccelerationStructureCompactedSize"/> pool each
    /// result is the structure's compacted size in <b>bytes</b>.
    /// </summary>
    /// <param name="structures">The structures to measure. Empty is a
    /// no-op.</param>
    /// <param name="pool">
    /// The destination pool. Its <see cref="QueryPool.Type"/> supplies the
    /// command's <c>queryType</c>, which is why this method takes no such
    /// parameter and therefore cannot mismatch the pool
    /// (<c>VUID-vkCmdWriteAccelerationStructuresPropertiesKHR-queryPool-02493</c>).
    /// A borrowed pool is rejected: the wrapper never learned its type and has
    /// nothing valid to pass.
    /// </param>
    /// <param name="firstQuery">First query index to write.</param>
    /// <remarks>
    /// <para><b>Reset first, and submit the reset.</b> The queries must be
    /// <em>unavailable</em> when this executes
    /// (<c>-queryPool-02494</c>), which is what a <b>submitted</b>
    /// <see cref="ResetQueryPool"/> makes them.</para>
    /// <para><b>Every structure must have been built with
    /// <see cref="AccelerationStructureBuildFlags.AllowCompaction"/></b>
    /// for a compacted-size query (<c>-accelerationStructures-03431</c>), and
    /// must have finished building before this command executes
    /// (<c>-pAccelerationStructures-04964</c>) — so a barrier is required
    /// between the build and this command.</para>
    /// <para><b>The compaction flow, end to end.</b></para>
    /// <list type="number">
    ///   <item><description>Build the BLAS with
    ///     <see cref="AccelerationStructureBuildFlags.AllowCompaction"/>.</description></item>
    ///   <item><description>Barrier
    ///     <see cref="Stage.AccelerationStructureBuild"/> /
    ///     <see cref="Access.AccelerationStructureWrite"/> →
    ///     <see cref="Stage.AccelerationStructureBuild"/> /
    ///     <see cref="Access.AccelerationStructureRead"/>.</description></item>
    ///   <item><description><see cref="ResetQueryPool"/> over the range, then
    ///     this command.</description></item>
    ///   <item><description>Submit; wait on the fence.</description></item>
    ///   <item><description><see cref="QueryPool.GetResults(uint, Span{ulong})"/>
    ///     — each value is the compacted size in bytes.</description></item>
    ///   <item><description>Allocate a buffer of that size,
    ///     <see cref="Device.CreateAccelerationStructure"/> over it, then
    ///     <see cref="CopyAccelerationStructure"/> with
    ///     <see cref="AccelerationStructureCopyMode.Compact"/>; submit and
    ///     wait.</description></item>
    ///   <item><description><b>Only now</b> dispose the original structure and
    ///     free its buffer — and remember the compacted copy has a
    ///     <em>different</em> device address, so any TLAS over it must be
    ///     rebuilt.</description></item>
    /// </list>
    /// <para>Must be recorded outside a rendering scope, from a
    /// compute-capable pool. Allocates zero per call for <c>≤ 8</c>
    /// structures.</para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// <c>VK_KHR_acceleration_structure</c> was not enabled on this device, or
    /// <paramref name="pool"/> is null or borrowed.
    /// </exception>
    public void WriteAccelerationStructuresProperties(
        scoped ReadOnlySpan<AccelerationStructure> structures, in QueryPool pool, uint firstQuery)
    {
        var fn = Fns.CmdWriteAccelerationStructuresProperties;
        if (fn == null) ThrowAccelerationStructureUnsupported("WriteAccelerationStructuresProperties");

        // accelerationStructureCount must be > 0
        // (VUID-...-accelerationStructureCount-arraylength), so an empty span
        // returns rather than dispatching a zero-count call.
        if (structures.IsEmpty) return;

        // Unconditional, not AhjoValidation-gated: without a type there is no
        // queryType to pass, so this is a "cannot proceed", not a "you are
        // probably wrong" (the QueryPool.ThrowIfBorrowed voice).
        if (pool.IsNull)
            throw new InvalidOperationException(
                "WriteAccelerationStructuresProperties: query pool is a null handle. Create one with "
                + "Device.CreateQueryPool(QueryType.AccelerationStructureCompactedSize, count).");
        if (pool.Type == QueryType.Unknown)
            throw new InvalidOperationException(
                "WriteAccelerationStructuresProperties requires a pool whose type the wrapper knows; a "
                + "FromRaw-constructed (borrowed) pool reports QueryType.Unknown, and the wrapper has no "
                + "valid queryType to pass to vkCmdWriteAccelerationStructuresPropertiesKHR "
                + "(VUID-vkCmdWriteAccelerationStructuresPropertiesKHR-queryPool-02493). Create the pool "
                + "with Device.CreateQueryPool(QueryType.AccelerationStructureCompactedSize, count).");
        // Unconditional for the same reason as the Unknown case above, which
        // is a failure of identical character: the pool's type IS the
        // queryType this command passes, so a mismatched pool sends the driver
        // a queryType that cannot match the pool it names
        // (VUID-vkCmdWriteAccelerationStructuresPropertiesKHR-queryPool-02493,
        // -queryType-06742). Gating it would let a Timestamp pool through in
        // Release, where AhjoValidation.IsEnabled is false.
        if (pool.Type != QueryType.AccelerationStructureCompactedSize)
            throw new InvalidOperationException(
                $"WriteAccelerationStructuresProperties: the pool's type is {pool.Type}, but this command "
                + "needs QueryType.AccelerationStructureCompactedSize — the pool's own type is what is "
                + "passed as the command's queryType, and the two must match "
                + "(VUID-vkCmdWriteAccelerationStructuresPropertiesKHR-queryPool-02493 / -queryType-06742). "
                + "Create the pool with "
                + "Device.CreateQueryPool(QueryType.AccelerationStructureCompactedSize, count).");

        if (AhjoValidation.IsEnabled)
        {
            // Widened to ulong before adding, as elsewhere in this file.
            if (pool.QueryCount != 0 && (ulong)firstQuery + (uint)structures.Length > pool.QueryCount)
                AhjoValidation.Fail("CommandRecorder",
                    $"WriteAccelerationStructuresProperties: range [{firstQuery}, "
                    + $"{(ulong)firstQuery + (uint)structures.Length}) exceeds the pool's QueryCount "
                    + $"({pool.QueryCount}) "
                    + "(VUID-vkCmdWriteAccelerationStructuresPropertiesKHR-query-04880).");

            for (int i = 0; i < structures.Length; i++)
                if (structures[i].IsNull)
                    AhjoValidation.Fail("CommandRecorder",
                        $"WriteAccelerationStructuresProperties: structures[{i}] is a null handle.");
        }

        int count = structures.Length;
        if (count <= BuildStackThreshold)
        {
            Span<nint> handles = stackalloc nint[count];
            FlushWriteProperties(fn, Handle, structures, handles, pool, firstQuery);
            return;
        }

        nint[] rented = System.Buffers.ArrayPool<nint>.Shared.Rent(count);
        try
        {
            FlushWriteProperties(fn, Handle, structures, rented.AsSpan(0, count), pool, firstQuery);
        }
        finally
        {
            System.Buffers.ArrayPool<nint>.Shared.Return(rented);
        }
    }

    private static void FlushWriteProperties(
        delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, uint, VkAccelerationStructureKHR_T**, VkQueryType, VkQueryPool_T*, uint, void> fn,
        VkCommandBuffer_T*                         cb,
        scoped ReadOnlySpan<AccelerationStructure> structures,
        scoped Span<nint>                          handles,
        in QueryPool                               pool,
        uint                                       firstQuery)
    {
        for (int i = 0; i < structures.Length; i++)
            handles[i] = (nint)structures[i].Handle;

        fixed (nint* pHandles = handles)
            fn(cb, (uint)structures.Length, (VkAccelerationStructureKHR_T**)pHandles,
               (VkQueryType)pool.Type, pool.Handle, firstQuery);
    }

    /// <summary>
    /// <c>vkCmdCopyAccelerationStructureKHR</c> — copies
    /// <paramref name="source"/> into <paramref name="destination"/>, either
    /// verbatim (<see cref="AccelerationStructureCopyMode.Clone"/>) or
    /// compacted (<see cref="AccelerationStructureCopyMode.Compact"/>).
    /// </summary>
    /// <remarks>
    /// <para><b>For <see cref="AccelerationStructureCopyMode.Compact"/></b> the
    /// source must have been built with
    /// <see cref="AccelerationStructureBuildFlags.AllowCompaction"/>
    /// (<c>VUID-VkCopyAccelerationStructureInfoKHR-src-03411</c>) and
    /// <paramref name="destination"/> must have been created over a range of
    /// exactly the size read back from
    /// <see cref="WriteAccelerationStructuresProperties"/>. Neither is
    /// checkable by the wrapper.</para>
    /// <para><b>Ordering.</b> The source must have finished building
    /// (<c>-src-04963</c>), so a barrier is required before this command; and
    /// the source <em>and its buffer</em> must stay alive until the copy has
    /// completed on the GPU — only then may either be disposed. The
    /// destination's memory must not overlap the source's
    /// (<c>-dst-07791</c>).</para>
    /// <para><b>The compacted copy has a different device address.</b> It lives
    /// in a different buffer, so
    /// <see cref="AccelerationStructure.GetDeviceAddress"/> returns a new
    /// value and every TLAS whose instance entries referenced the original must
    /// be <em>fully rebuilt</em> against the new address — there is no
    /// diagnostic for getting this wrong.</para>
    /// <para><b>Barrier with <see cref="Stage.AccelerationStructureBuild"/>,
    /// not <see cref="Stage.AccelerationStructureCopy"/>.</b> The copy stage
    /// bit belongs to <c>VK_KHR_ray_tracing_maintenance1</c>, which the enable
    /// recipe for this surface does not turn on, so using it is a validation
    /// error (<c>VUID-VkMemoryBarrier2-srcStageMask-10752</c>) on an otherwise
    /// correctly configured device. This command executes in
    /// <see cref="Stage.AccelerationStructureBuild"/> as well, so that bit
    /// synchronizes it correctly with no extra extension.</para>
    /// <para>Must be recorded outside a rendering scope, from a
    /// compute-capable pool.</para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// <c>VK_KHR_acceleration_structure</c> was not enabled on this device.
    /// </exception>
    public void CopyAccelerationStructure(
        in AccelerationStructure source,
        in AccelerationStructure destination,
        AccelerationStructureCopyMode mode)
    {
        var fn = Fns.CmdCopyAccelerationStructure;
        if (fn == null) ThrowAccelerationStructureUnsupported("CopyAccelerationStructure");

        if (AhjoValidation.IsEnabled)
        {
            if (source.IsNull)
                AhjoValidation.Fail("CommandRecorder",
                    "CopyAccelerationStructure: source is a null handle.");
            if (destination.IsNull)
                AhjoValidation.Fail("CommandRecorder",
                    "CopyAccelerationStructure: destination is a null handle.");
        }

        var info = new VkCopyAccelerationStructureInfoKHR
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_COPY_ACCELERATION_STRUCTURE_INFO_KHR,
            src   = source.Handle,
            dst   = destination.Handle,
            mode  = (VkCopyAccelerationStructureModeKHR)mode,
        };
        fn(Handle, &info);
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
                Fns.CmdCopyBuffer2(Handle, &info);
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
        // VkBufferCopy2::size must be > 0 — a zero-size source is typically a
        // Buffer.FromRaw handle whose size the wrapper never recorded. Reject
        // it here: it would pass the dst.Size < src.Size guard below (0 < 0 is
        // false) and then ask the driver to copy ~0ul bytes.
        if (src.Size == 0)
            throw new ArgumentException(
                "CopyBuffer whole-buffer overload requires src.Size > 0. A zero-size source is "
                + "typically a Buffer.FromRaw handle whose size is unknown to the wrapper — use the "
                + "multi-region overload with an explicit BufferCopyRegion size.", nameof(src));
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
                Fns.CmdCopyBufferToImage2(Handle, &info);
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
                Fns.CmdCopyImageToBuffer2(Handle, &info);
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
                Fns.CmdCopyImage2(Handle, &info);
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
    /// <para>The precondition is purely about layout: mip 0 may be
    /// produced by <em>any</em> transfer command — a copy
    /// (<c>vkCmdCopyBufferToImage</c>), a clear
    /// (<c>vkCmdClearColorImage</c>), or a blit — as long as it ends in
    /// <c>VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL</c>. Because that producing
    /// command is unknown here, the helper synchronizes mip 0's first
    /// layout transition against all transfer stages
    /// (<see cref="Stage.AllTransfer"/>), not just the Copy stage.</para>
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
            // Mip 0's producer is caller-controlled: the documented
            // precondition only requires the TRANSFER_DST_OPTIMAL layout,
            // not a specific producing command. The caller may have filled
            // mip 0 with a copy (Copy stage), a clear (Clear stage), or a
            // blit (Blit stage). Source scope must therefore cover all
            // transfer stages — Stage.AllTransfer is
            // VK_PIPELINE_STAGE_2_ALL_TRANSFER_BIT, which the spec defines to
            // cover every transfer stage (copy/blit/resolve/clear); it is a
            // single distinct bit, NOT the bitwise-OR of the individual
            // Stage.Copy/Blit/Resolve/Clear values. Widening a source scope is
            // always safe (waits for more, never less); Access.TransferWrite
            // already covers copy/blit/clear/resolve writes.
            ImageBarrier soleBarrier = new()
            {
                Image               = (nint)image.Handle,
                SrcStage            = Stage.AllTransfer,
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
            // the previous write to mip (i-1):
            //   • i == 1: the caller's mip-0 write. Its producing command is
            //     unknown — copy, clear, and blit are all valid ways to
            //     satisfy the documented TRANSFER_DST precondition — so the
            //     source scope must be Stage.AllTransfer
            //     (VK_PIPELINE_STAGE_2_ALL_TRANSFER_BIT, spec-defined to cover
            //     every transfer stage; a single bit, not Copy|Blit|Resolve|Clear).
            //   • i >= 2: the previous iteration's vkCmdBlitImage2 (Blit
            //     stage), which stays Stage.Blit.
            // Sync2 treats Copy/Blit/Clear/Resolve as distinct stages;
            // narrowing i == 1 to a single producer would leave the caller's
            // write unordered against this layout transition (issue #101).
            ImageBarrier srcSwap = new()
            {
                Image               = (nint)image.Handle,
                SrcStage            = i == 1 ? Stage.AllTransfer : Stage.Blit,
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
        scoped ReadOnlySpan<ImageBlitRegion> regions,
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
                Fns.CmdBlitImage2(Handle, &info);
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
        => Fns.CmdFillBuffer(Handle, dst.Handle, offset, size, data);

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
        scoped ReadOnlySpan<VkImageSubresourceRange> ranges)
    {
        if (ranges.IsEmpty) return;
        fixed (VkClearColorValue*       pColor = &color)
        fixed (VkImageSubresourceRange* pRange = ranges)
            Fns.CmdClearColorImage(Handle, image.Handle, layout, pColor, (uint)ranges.Length, pRange);
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
        scoped ReadOnlySpan<VkImageSubresourceRange> ranges)
    {
        if (ranges.IsEmpty) return;
        fixed (VkClearDepthStencilValue* pDs    = &depthStencil)
        fixed (VkImageSubresourceRange*  pRange = ranges)
            Fns.CmdClearDepthStencilImage(Handle, image.Handle, layout, pDs, (uint)ranges.Length, pRange);
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
                Fns.CmdBeginRendering(Handle, &native);
            }
        }
        finally
        {
            if (rented is not null) System.Buffers.ArrayPool<VkRenderingAttachmentInfo>.Shared.Return(rented);
        }
    }

    public void EndRendering() => Fns.CmdEndRendering(Handle);

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
