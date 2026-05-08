using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Per-device cache of extension entry points the wrapper itself uses.
/// All entry points are resolved through <c>vkGetDeviceProcAddr</c> at
/// <see cref="Device"/> construction; absent extensions yield null
/// pointers and the corresponding wrapper helpers degrade to no-ops
/// (e.g. <see cref="ObjectName.Set{T}(Device, T, System.ReadOnlySpan{byte})"/>
/// when <c>VK_EXT_debug_utils</c> is not enabled on the instance).
/// </summary>
internal readonly unsafe struct DeviceFunctionTable
{
    private readonly VkDevice_T* _device;

    /// <summary>
    /// <c>vkSetDebugUtilsObjectNameEXT</c>. Null when
    /// <c>VK_EXT_debug_utils</c> is not enabled.
    /// </summary>
    public readonly delegate* unmanaged[Stdcall]<
        VkDevice_T*,
        VkDebugUtilsObjectNameInfoEXT*,
        VkResult> SetDebugUtilsObjectName;

    /// <summary>
    /// <c>vkCmdBeginDebugUtilsLabelEXT</c>. Null when
    /// <c>VK_EXT_debug_utils</c> is not enabled.
    /// </summary>
    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*,
        VkDebugUtilsLabelEXT*,
        void> CmdBeginDebugUtilsLabel;

    /// <summary>
    /// <c>vkCmdEndDebugUtilsLabelEXT</c>. Null when
    /// <c>VK_EXT_debug_utils</c> is not enabled.
    /// </summary>
    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*,
        void> CmdEndDebugUtilsLabel;

    /// <summary>
    /// <c>vkCmdInsertDebugUtilsLabelEXT</c>. Null when
    /// <c>VK_EXT_debug_utils</c> is not enabled.
    /// </summary>
    public readonly delegate* unmanaged[Stdcall]<
        VkCommandBuffer_T*,
        VkDebugUtilsLabelEXT*,
        void> CmdInsertDebugUtilsLabel;

    public DeviceFunctionTable(VkDevice_T* device)
    {
        _device = device;
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
}
