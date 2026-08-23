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
/// <item><description><b>Instance-extension entry points reached through
/// <c>vkGetDeviceProcAddr</c></b> — the four <c>VK_EXT_debug_utils</c>
/// pointers. <c>VK_EXT_debug_utils</c> is enabled on the <em>instance</em>,
/// never appears in <see cref="DeviceDescription.Extensions"/>, and so is
/// resolved unconditionally; absent ⇒ null ⇒ the corresponding wrapper
/// helper degrades to a no-op (e.g.
/// <see cref="ObjectName.Set{T}(Device, T, System.ReadOnlySpan{byte})"/>
/// when the extension is not enabled on the
/// instance).</description></item>
/// <item><description><b>Device-extension entry points</b> — resolved
/// <b>only</b> when the extension appears in the enabled list passed to
/// <c>vkCreateDevice</c>. Two failure modes, both loud: not enabled ⇒ null
/// pointer ⇒ the calling wrapper method throws a message naming the
/// extension; enabled but unresolvable ⇒ throw here, at
/// <see cref="Device"/> construction. This group is <b>not</b> limited to
/// <c>vkCmd*</c>: the loader does not export extension symbols through
/// <c>vulkan-1.dll</c> (see <c>Internal/InstanceFunctionTable.cs</c>), so
/// create/destroy/query entry points of a device extension belong here
/// too, unlike core cold-path calls which stay on the static
/// <c>[DllImport]</c>s. <c>VK_EXT_mesh_shader</c> was the first member
/// (issue #201) and <c>VK_KHR_acceleration_structure</c> is the second
/// (issue #202), added as a second <c>if</c> block of the same shape.
/// The acceleration-structure block is the first to carry
/// <b>device-level</b> entry points — <c>vkCreateAccelerationStructureKHR</c>,
/// <c>vkDestroyAccelerationStructureKHR</c>,
/// <c>vkGetAccelerationStructureBuildSizesKHR</c> and
/// <c>vkGetAccelerationStructureDeviceAddressKHR</c> are create/destroy/query
/// rather than <c>vkCmd*</c>, which is exactly the case this bullet
/// anticipated. <c>VK_KHR_ray_query</c> and
/// <c>VK_KHR_deferred_host_operations</c> ride along at
/// <c>vkCreateDevice</c> but gate nothing here: the first defines no entry
/// points and the wrapper calls none of the second extension commands.</description></item>
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

    // ---- Mesh shading (VK_EXT_mesh_shader) ----

    /// <summary><c>vkCmdDrawMeshTasksEXT</c>. Null when VK_EXT_mesh_shader
    /// was not enabled on this device.</summary>
    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, uint, uint, uint, void> CmdDrawMeshTasks;

    /// <summary><c>vkCmdDrawMeshTasksIndirectEXT</c>. Null when
    /// VK_EXT_mesh_shader was not enabled on this device.</summary>
    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkBuffer_T*, ulong, uint, uint, void> CmdDrawMeshTasksIndirect;

    /// <summary><c>vkCmdDrawMeshTasksIndirectCountEXT</c>. Null when
    /// VK_EXT_mesh_shader was not enabled on this device.</summary>
    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkBuffer_T*, ulong, VkBuffer_T*, ulong, uint, uint, void> CmdDrawMeshTasksIndirectCount;

    // ---- Acceleration structures (VK_KHR_acceleration_structure) ----

    /// <summary><c>vkCreateAccelerationStructureKHR</c>. Null when
    /// VK_KHR_acceleration_structure was not enabled on this
    /// device.</summary>
    public readonly delegate* unmanaged[Stdcall]<
        VkDevice_T*, VkAccelerationStructureCreateInfoKHR*, VkAllocationCallbacks*,
        VkAccelerationStructureKHR_T**, VkResult> CreateAccelerationStructure;

    /// <summary><c>vkDestroyAccelerationStructureKHR</c>. Null when
    /// VK_KHR_acceleration_structure was not enabled on this
    /// device.</summary>
    public readonly delegate* unmanaged[Stdcall]<
        VkDevice_T*, VkAccelerationStructureKHR_T*, VkAllocationCallbacks*, void>
        DestroyAccelerationStructure;

    /// <summary><c>vkGetAccelerationStructureBuildSizesKHR</c>. Null when
    /// VK_KHR_acceleration_structure was not enabled on this
    /// device.</summary>
    public readonly delegate* unmanaged[Stdcall]<
        VkDevice_T*, VkAccelerationStructureBuildTypeKHR,
        VkAccelerationStructureBuildGeometryInfoKHR*, uint*,
        VkAccelerationStructureBuildSizesInfoKHR*, void> GetAccelerationStructureBuildSizes;

    /// <summary><c>vkGetAccelerationStructureDeviceAddressKHR</c>. Null when
    /// VK_KHR_acceleration_structure was not enabled on this
    /// device.</summary>
    public readonly delegate* unmanaged[Stdcall]<
        VkDevice_T*, VkAccelerationStructureDeviceAddressInfoKHR*, ulong>
        GetAccelerationStructureDeviceAddress;

    /// <summary><c>vkCmdBuildAccelerationStructuresKHR</c>. Null when
    /// VK_KHR_acceleration_structure was not enabled on this
    /// device.</summary>
    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, uint, VkAccelerationStructureBuildGeometryInfoKHR*,
        VkAccelerationStructureBuildRangeInfoKHR**, void> CmdBuildAccelerationStructures;

    /// <summary><c>vkCmdWriteAccelerationStructuresPropertiesKHR</c>. Null when
    /// VK_KHR_acceleration_structure was not enabled on this
    /// device.</summary>
    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, uint, VkAccelerationStructureKHR_T**, VkQueryType,
        VkQueryPool_T*, uint, void> CmdWriteAccelerationStructuresProperties;

    /// <summary><c>vkCmdCopyAccelerationStructureKHR</c>. Null when
    /// VK_KHR_acceleration_structure was not enabled on this
    /// device.</summary>
    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkCopyAccelerationStructureInfoKHR*, void>
        CmdCopyAccelerationStructure;

    // ---- Pipeline barriers (sync2) ----

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkDependencyInfo*, void> CmdPipelineBarrier2;

    // ---- Split barriers (sync2 events) ----

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkEvent_T*, VkDependencyInfo*, void> CmdSetEvent2;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, uint, VkEvent_T**, VkDependencyInfo*, void> CmdWaitEvents2;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkEvent_T*, ulong, void> CmdResetEvent2;

    // ---- Timestamp queries ----

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, VkQueryPool_T*, uint, uint, void> CmdResetQueryPool;

    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*, ulong, VkQueryPool_T*, uint, void> CmdWriteTimestamp2;

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

    public DeviceFunctionTable(VkDevice_T* device, ReadOnlySpan<Utf8Name> enabledExtensions)
    {
        _device = device;

        // Device-extension pointers stay null unless the gated block at the
        // end of this constructor resolves them; `readonly` fields must be
        // definitely assigned on every path.
        CmdDrawMeshTasks              = null;
        CmdDrawMeshTasksIndirect      = null;
        CmdDrawMeshTasksIndirectCount = null;

        CreateAccelerationStructure              = null;
        DestroyAccelerationStructure             = null;
        GetAccelerationStructureBuildSizes       = null;
        GetAccelerationStructureDeviceAddress    = null;
        CmdBuildAccelerationStructures           = null;
        CmdWriteAccelerationStructuresProperties = null;
        CmdCopyAccelerationStructure             = null;

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

        // Split barriers. All three are core since Vulkan 1.3 and the
        // wrapper's device floor is 1.3, so no KHR-suffixed fallback is
        // needed — ResolveRequired is correct.
        CmdSetEvent2 =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkEvent_T*, VkDependencyInfo*, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdSetEvent2"u8));
        CmdWaitEvents2 =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, uint, VkEvent_T**, VkDependencyInfo*, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdWaitEvents2"u8));
        CmdResetEvent2 =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkEvent_T*, ulong, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdResetEvent2"u8));

        // Timestamp queries. vkCmdResetQueryPool is core since Vulkan 1.0
        // and vkCmdWriteTimestamp2 since 1.3 — the wrapper's device floor —
        // so no KHR-suffixed fallback is needed; ResolveRequired is correct.
        CmdResetQueryPool =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkQueryPool_T*, uint, uint, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdResetQueryPool"u8));
        CmdWriteTimestamp2 =
            (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, ulong, VkQueryPool_T*, uint, void>)
            ResolveRequired(Utf8Name.FromLiteral("vkCmdWriteTimestamp2"u8));

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

        // Device-extension entry points. Gated on the list the wrapper itself
        // passed to vkCreateDevice — vkCreateDevice has already succeeded, so
        // membership in that list *is* "enabled", and Vulkan offers no query to
        // ask the device after the fact.
        if (IsExtensionEnabled(enabledExtensions, DeviceExtensionNames.MeshShader))
        {
            CmdDrawMeshTasks =
                (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, uint, uint, uint, void>)
                ResolveExtensionRequired(
                    Utf8Name.FromLiteral(DeviceExtensionNames.CmdDrawMeshTasks),
                    DeviceExtensionNames.MeshShader);
            CmdDrawMeshTasksIndirect =
                (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkBuffer_T*, ulong, uint, uint, void>)
                ResolveExtensionRequired(
                    Utf8Name.FromLiteral(DeviceExtensionNames.CmdDrawMeshTasksIndirect),
                    DeviceExtensionNames.MeshShader);
            CmdDrawMeshTasksIndirectCount =
                (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkBuffer_T*, ulong, VkBuffer_T*, ulong, uint, uint, void>)
                ResolveExtensionRequired(
                    Utf8Name.FromLiteral(DeviceExtensionNames.CmdDrawMeshTasksIndirectCount),
                    DeviceExtensionNames.MeshShader);
        }

        // VK_KHR_acceleration_structure is the only one of the three
        // ray-query extensions that gates anything: VK_KHR_ray_query defines
        // no entry points, and the wrapper calls none of
        // the VK_KHR_deferred_host_operations commands. Four of the seven
        // pointers below are device-level (create/destroy/query), not vkCmd*.
        if (IsExtensionEnabled(enabledExtensions, DeviceExtensionNames.AccelerationStructure))
        {
            CreateAccelerationStructure =
                (delegate* unmanaged[Stdcall]<VkDevice_T*, VkAccelerationStructureCreateInfoKHR*, VkAllocationCallbacks*, VkAccelerationStructureKHR_T**, VkResult>)
                ResolveExtensionRequired(
                    Utf8Name.FromLiteral(DeviceExtensionNames.CreateAccelerationStructure),
                    DeviceExtensionNames.AccelerationStructure);
            DestroyAccelerationStructure =
                (delegate* unmanaged[Stdcall]<VkDevice_T*, VkAccelerationStructureKHR_T*, VkAllocationCallbacks*, void>)
                ResolveExtensionRequired(
                    Utf8Name.FromLiteral(DeviceExtensionNames.DestroyAccelerationStructure),
                    DeviceExtensionNames.AccelerationStructure);
            GetAccelerationStructureBuildSizes =
                (delegate* unmanaged[Stdcall]<VkDevice_T*, VkAccelerationStructureBuildTypeKHR, VkAccelerationStructureBuildGeometryInfoKHR*, uint*, VkAccelerationStructureBuildSizesInfoKHR*, void>)
                ResolveExtensionRequired(
                    Utf8Name.FromLiteral(DeviceExtensionNames.GetAccelerationStructureBuildSizes),
                    DeviceExtensionNames.AccelerationStructure);
            GetAccelerationStructureDeviceAddress =
                (delegate* unmanaged[Stdcall]<VkDevice_T*, VkAccelerationStructureDeviceAddressInfoKHR*, ulong>)
                ResolveExtensionRequired(
                    Utf8Name.FromLiteral(DeviceExtensionNames.GetAccelerationStructureDeviceAddress),
                    DeviceExtensionNames.AccelerationStructure);
            CmdBuildAccelerationStructures =
                (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, uint, VkAccelerationStructureBuildGeometryInfoKHR*, VkAccelerationStructureBuildRangeInfoKHR**, void>)
                ResolveExtensionRequired(
                    Utf8Name.FromLiteral(DeviceExtensionNames.CmdBuildAccelerationStructures),
                    DeviceExtensionNames.AccelerationStructure);
            CmdWriteAccelerationStructuresProperties =
                (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, uint, VkAccelerationStructureKHR_T**, VkQueryType, VkQueryPool_T*, uint, void>)
                ResolveExtensionRequired(
                    Utf8Name.FromLiteral(DeviceExtensionNames.CmdWriteAccelerationStructuresProperties),
                    DeviceExtensionNames.AccelerationStructure);
            CmdCopyAccelerationStructure =
                (delegate* unmanaged[Stdcall]<VkCommandBuffer_T*, VkCopyAccelerationStructureInfoKHR*, void>)
                ResolveExtensionRequired(
                    Utf8Name.FromLiteral(DeviceExtensionNames.CmdCopyAccelerationStructure),
                    DeviceExtensionNames.AccelerationStructure);
        }
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

    /// <summary>
    /// True when <paramref name="utf8Name"/> is in the device-extension list
    /// the caller passed to <c>vkCreateDevice</c>. Setup-time, allocation-free;
    /// the span-over-NUL-terminated-pointer idiom is the one
    /// <c>Instance.IsExtensionSupported(Utf8Name)</c> uses.
    /// </summary>
    private static bool IsExtensionEnabled(ReadOnlySpan<Utf8Name> enabled, ReadOnlySpan<byte> utf8Name)
    {
        for (int i = 0; i < enabled.Length; i++)
        {
            if (enabled[i].IsNull) continue;
            if (System.Runtime.InteropServices.MemoryMarshal
                    .CreateReadOnlySpanFromNullTerminated((byte*)enabled[i].Ptr)
                    .SequenceEqual(utf8Name))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Resolves an entry point belonging to a device extension the caller
    /// enabled. A null result means the driver advertised the extension at
    /// <c>vkCreateDevice</c> but does not expose the command — a broken
    /// loader/driver configuration, reported here rather than as an access
    /// violation on a later frame.
    /// </summary>
    private delegate* unmanaged[Stdcall]<void> ResolveExtensionRequired(
        Utf8Name entryPoint, ReadOnlySpan<byte> extension)
    {
        var p = Resolve(entryPoint);
        if (p == null) ThrowExtensionEntryPointMissing(entryPoint, extension);
        return p;
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void ThrowExtensionEntryPointMissing(Utf8Name entryPoint, ReadOnlySpan<byte> extension) =>
        throw new InvalidOperationException(
            "vkGetDeviceProcAddr returned null for " +
            $"'{System.Runtime.InteropServices.Marshal.PtrToStringUTF8((nint)entryPoint.Ptr)}', which belongs to " +
            $"device extension '{System.Text.Encoding.UTF8.GetString(extension)}' — enabled at device creation " +
            "via DeviceDescription.Extensions. The driver advertises the extension but does not expose the " +
            "command; this indicates a loader or driver configuration the wrapper does not support.");
}
