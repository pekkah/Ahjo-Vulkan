using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Per-device cache of the entry points the wrapper itself dispatches
/// through. Two kinds of pointer live here:
/// <list type="bullet">
/// <item><description>Hot-path core commands — every <c>vkCmd*</c> the
/// <see cref="CommandRecorder"/> records, plus <c>vkBeginCommandBuffer</c>,
/// <c>vkEndCommandBuffer</c> and <c>vkQueueSubmit2</c>. Resolving these once
/// per device via <c>vkGetDeviceProcAddr</c> and calling through the cached
/// <c>delegate* unmanaged</c> skips the loader's per-call dispatch
/// trampoline and ICD lookup that the static <c>[DllImport]</c> path pays on
/// every draw/bind/barrier (issue #121). The wrapper requires a Vulkan 1.3+
/// device (see <see cref="PhysicalDevice"/>), so all of these except the two
/// push-descriptor commands are guaranteed core and always resolve. The
/// push-descriptor pair was promoted to core only in 1.4, so it is resolved
/// with a <c>KHR</c>-suffixed fallback for 1.3 devices that enable
/// <c>VK_KHR_push_descriptor</c> and stays null — the recorder throws rather
/// than dispatching through it — when neither is present.</description></item>
/// <item><description>Extension entry points — <c>VK_EXT_debug_utils</c>.
/// Absent extensions yield null pointers and the corresponding wrapper
/// helpers degrade to no-ops (e.g.
/// <see cref="ObjectName.Set{T}(Device, T, System.ReadOnlySpan{byte})"/>
/// when <c>VK_EXT_debug_utils</c> is not enabled on the
/// instance).</description></item>
/// </list>
/// All pointers are resolved at <see cref="Device"/> construction. Cold-path
/// and instance-level calls keep using the static <c>[DllImport]</c>s on
/// <see cref="Vk"/>.
/// </summary>
internal readonly unsafe struct DeviceFunctionTable
{
    private readonly VkDevice_T* _device;

    // ---- Command buffer lifecycle ----

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkCommandBufferBeginInfo*, VkResult> BeginCommandBuffer;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkResult> EndCommandBuffer;

    // ---- Dynamic state ----

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, uint, uint, VkViewport*, void> CmdSetViewport;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, uint, uint, VkRect2D*, void> CmdSetScissor;

    // ---- Bind family ----

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkPipelineBindPoint, VkPipeline_T*, void> CmdBindPipeline;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkPipelineBindPoint, VkPipelineLayout_T*, uint, uint,
        VkDescriptorSet_T**, uint, uint*, void> CmdBindDescriptorSets;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkPipelineLayout_T*, uint, uint, uint, void*, void> CmdPushConstants;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, uint, uint, VkBuffer_T**, ulong*, void> CmdBindVertexBuffers;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkBuffer_T*, ulong, VkIndexType, void> CmdBindIndexBuffer;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkDescriptorUpdateTemplate_T*, VkPipelineLayout_T*, uint, void*, void>
        CmdPushDescriptorSetWithTemplate;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkPipelineBindPoint, VkPipelineLayout_T*, uint, uint,
        VkWriteDescriptorSet*, void> CmdPushDescriptorSet;

    // ---- Draw / dispatch ----

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, uint, uint, uint, uint, void> CmdDraw;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, uint, uint, uint, int, uint, void> CmdDrawIndexed;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkBuffer_T*, ulong, uint, uint, void> CmdDrawIndirect;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkBuffer_T*, ulong, VkBuffer_T*, ulong, uint, uint, void> CmdDrawIndirectCount;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkBuffer_T*, ulong, uint, uint, void> CmdDrawIndexedIndirect;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkBuffer_T*, ulong, VkBuffer_T*, ulong, uint, uint, void> CmdDrawIndexedIndirectCount;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, uint, uint, uint, void> CmdDispatch;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkBuffer_T*, ulong, void> CmdDispatchIndirect;

    // ---- Pipeline barriers (sync2) ----

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkDependencyInfo*, void> CmdPipelineBarrier2;

    // ---- Copy / blit / clear / fill (copy_commands2 path) ----

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkCopyBufferInfo2*, void> CmdCopyBuffer2;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkCopyBufferToImageInfo2*, void> CmdCopyBufferToImage2;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkCopyImageToBufferInfo2*, void> CmdCopyImageToBuffer2;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkCopyImageInfo2*, void> CmdCopyImage2;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkBlitImageInfo2*, void> CmdBlitImage2;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkBuffer_T*, ulong, ulong, uint, void> CmdFillBuffer;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkImage_T*, VkImageLayout, VkClearColorValue*, uint,
        VkImageSubresourceRange*, void> CmdClearColorImage;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkImage_T*, VkImageLayout, VkClearDepthStencilValue*, uint,
        VkImageSubresourceRange*, void> CmdClearDepthStencilImage;

    // ---- Dynamic rendering ----

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkRenderingInfo*, void> CmdBeginRendering;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, void> CmdEndRendering;

    // ---- Queue submission ----

    public readonly delegate* unmanaged[Stdcall]<
        VkQueue_T*, uint, VkSubmitInfo2*, VkFence_T*, VkResult> QueueSubmit2;

    // ---- Debug markers (VK_EXT_debug_utils) ----

    /// <summary>
    /// <c>vkSetDebugUtilsObjectNameEXT</c>. Null when
    /// <c>VK_EXT_debug_utils</c> is not enabled.
    /// </summary>
    public readonly delegate* unmanaged[Stdcall]<
        VkDevice_T*, VkDebugUtilsObjectNameInfoEXT*, VkResult> SetDebugUtilsObjectName;

    /// <summary>
    /// <c>vkCmdBeginDebugUtilsLabelEXT</c>. Null when
    /// <c>VK_EXT_debug_utils</c> is not enabled.
    /// </summary>
    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkDebugUtilsLabelEXT*, void> CmdBeginDebugUtilsLabel;

    /// <summary>
    /// <c>vkCmdEndDebugUtilsLabelEXT</c>. Null when
    /// <c>VK_EXT_debug_utils</c> is not enabled.
    /// </summary>
    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, void> CmdEndDebugUtilsLabel;

    /// <summary>
    /// <c>vkCmdInsertDebugUtilsLabelEXT</c>. Null when
    /// <c>VK_EXT_debug_utils</c> is not enabled.
    /// </summary>
    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkDebugUtilsLabelEXT*, void> CmdInsertDebugUtilsLabel;

    public DeviceFunctionTable(VkDevice_T* device)
    {
        _device = device;

        // Core hot-path commands. The wrapper rejects pre-1.3 devices, so
        // every one of these resolves to a valid pointer; the resulting
        // dispatch skips the loader trampoline the static DllImports route
        // through (issue #121).
        BeginCommandBuffer =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkCommandBufferBeginInfo*, VkResult>)
            ResolveRequired(Utf8Name.FromLiteral("vkBeginCommandBuffer"u8));
        EndCommandBuffer =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkResult>)
            ResolveRequired(Utf8Name.FromLiteral("vkEndCommandBuffer"u8));

        CmdSetViewport =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, uint, uint, VkViewport*, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdSetViewport"u8));
        CmdSetScissor =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, uint, uint, VkRect2D*, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdSetScissor"u8));

        CmdBindPipeline =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkPipelineBindPoint, VkPipeline_T*, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdBindPipeline"u8));
        CmdBindDescriptorSets =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkPipelineBindPoint, VkPipelineLayout_T*, uint, uint, VkDescriptorSet_T**, uint, uint*, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdBindDescriptorSets"u8));
        CmdPushConstants =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkPipelineLayout_T*, uint, uint, uint, void*, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdPushConstants"u8));
        CmdBindVertexBuffers =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, uint, uint, VkBuffer_T**, ulong*, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdBindVertexBuffers"u8));
        CmdBindIndexBuffer =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkBuffer_T*, ulong, VkIndexType, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdBindIndexBuffer"u8));
        // Push-descriptor was promoted from VK_KHR_push_descriptor to core
        // only in Vulkan 1.4. On the wrapper's minimum 1.3 device the
        // un-suffixed core name returns null from vkGetDeviceProcAddr, so
        // fall back to the KHR-suffixed name the extension defines. Both
        // null (device is 1.3 and the extension wasn't enabled) leaves the
        // pointer null and CommandRecorder.PushDescriptors* throws a clear
        // error rather than dispatching through null.
        CmdPushDescriptorSetWithTemplate =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkDescriptorUpdateTemplate_T*, VkPipelineLayout_T*, uint, void*, void>)
            ResolveWithFallback(
                Utf8Name.FromLiteral("vkCmdPushDescriptorSetWithTemplate"u8),
                Utf8Name.FromLiteral("vkCmdPushDescriptorSetWithTemplateKHR"u8));
        CmdPushDescriptorSet =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkPipelineBindPoint, VkPipelineLayout_T*, uint, uint, VkWriteDescriptorSet*, void>)
            ResolveWithFallback(
                Utf8Name.FromLiteral("vkCmdPushDescriptorSet"u8),
                Utf8Name.FromLiteral("vkCmdPushDescriptorSetKHR"u8));

        CmdDraw =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, uint, uint, uint, uint, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdDraw"u8));
        CmdDrawIndexed =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, uint, uint, uint, int, uint, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdDrawIndexed"u8));
        CmdDrawIndirect =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkBuffer_T*, ulong, uint, uint, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdDrawIndirect"u8));
        CmdDrawIndirectCount =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkBuffer_T*, ulong, VkBuffer_T*, ulong, uint, uint, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdDrawIndirectCount"u8));
        CmdDrawIndexedIndirect =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkBuffer_T*, ulong, uint, uint, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdDrawIndexedIndirect"u8));
        CmdDrawIndexedIndirectCount =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkBuffer_T*, ulong, VkBuffer_T*, ulong, uint, uint, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdDrawIndexedIndirectCount"u8));
        CmdDispatch =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, uint, uint, uint, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdDispatch"u8));
        CmdDispatchIndirect =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkBuffer_T*, ulong, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdDispatchIndirect"u8));

        CmdPipelineBarrier2 =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkDependencyInfo*, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdPipelineBarrier2"u8));

        CmdCopyBuffer2 =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkCopyBufferInfo2*, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdCopyBuffer2"u8));
        CmdCopyBufferToImage2 =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkCopyBufferToImageInfo2*, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdCopyBufferToImage2"u8));
        CmdCopyImageToBuffer2 =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkCopyImageToBufferInfo2*, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdCopyImageToBuffer2"u8));
        CmdCopyImage2 =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkCopyImageInfo2*, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdCopyImage2"u8));
        CmdBlitImage2 =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkBlitImageInfo2*, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdBlitImage2"u8));
        CmdFillBuffer =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkBuffer_T*, ulong, ulong, uint, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdFillBuffer"u8));
        CmdClearColorImage =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkImage_T*, VkImageLayout, VkClearColorValue*, uint, VkImageSubresourceRange*, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdClearColorImage"u8));
        CmdClearDepthStencilImage =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkImage_T*, VkImageLayout, VkClearDepthStencilValue*, uint, VkImageSubresourceRange*, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdClearDepthStencilImage"u8));

        CmdBeginRendering =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkRenderingInfo*, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdBeginRendering"u8));
        CmdEndRendering =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdEndRendering"u8));

        QueueSubmit2 =
            (delegate* unmanaged[Stdcall]<VkQueue_T*, uint, VkSubmitInfo2*, VkFence_T*, VkResult>)
            ResolveRequired(Utf8Name.FromLiteral("vkQueueSubmit2"u8));

        // Extension entry points — null when VK_EXT_debug_utils is absent.
        SetDebugUtilsObjectName =
            (delegate* unmanaged[Stdcall]<VkDevice_T*, VkDebugUtilsObjectNameInfoEXT*, VkResult>)
            Resolve(Utf8Name.FromLiteral("vkSetDebugUtilsObjectNameEXT"u8));
        CmdBeginDebugUtilsLabel =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkDebugUtilsLabelEXT*, void>)
            Resolve(Utf8Name.FromLiteral("vkCmdBeginDebugUtilsLabelEXT"u8));
        CmdEndDebugUtilsLabel =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, void>)
            Resolve(Utf8Name.FromLiteral("vkCmdEndDebugUtilsLabelEXT"u8));
        CmdInsertDebugUtilsLabel =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkDebugUtilsLabelEXT*, void>)
            Resolve(Utf8Name.FromLiteral("vkCmdInsertDebugUtilsLabelEXT"u8));
    }

    public delegate* unmanaged[Stdcall]<void> Resolve(Utf8Name name) =>
        Vk.vkGetDeviceProcAddr(_device, name.Ptr);

    /// <summary>
    /// Resolves a command that is guaranteed core on the wrapper's minimum
    /// Vulkan 1.3 device and throws if <c>vkGetDeviceProcAddr</c> returns
    /// null. Without this, a null pointer (a loader/driver quirk, or a
    /// hypothetical typo in the resolve name) would surface as an access
    /// violation deep inside an unrelated hot-path call rather than a clear
    /// error naming the missing entry point at <see cref="Device"/>
    /// construction.
    /// </summary>
    private delegate* unmanaged[Stdcall]<void> ResolveRequired(Utf8Name name)
    {
        var p = Resolve(name);
        if (p == null) ThrowEntryPointMissing(name);
        return p;
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void ThrowEntryPointMissing(Utf8Name name) =>
        throw new InvalidOperationException(
            "vkGetDeviceProcAddr returned null for required core entry point " +
            $"'{System.Runtime.InteropServices.Marshal.PtrToStringUTF8((nint)name.Ptr)}'. The wrapper " +
            "requires a Vulkan 1.3+ device that exposes all core commands; this indicates a loader or " +
            "driver configuration it does not support.");

    /// <summary>
    /// Resolves <paramref name="core"/>, falling back to
    /// <paramref name="extension"/> when the core name is null — for
    /// commands that are core in a Vulkan version newer than the wrapper's
    /// minimum but available via an extension on older devices.
    /// </summary>
    private delegate* unmanaged[Stdcall]<void> ResolveWithFallback(Utf8Name core, Utf8Name extension)
    {
        var p = Resolve(core);
        return p != null ? p : Resolve(extension);
    }
}
