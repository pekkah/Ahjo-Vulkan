namespace Ahjo.Vulkan;

/// <summary>
/// Convenience accessors for the extension and layer name strings the
/// wrapper actively wraps. Always returned as
/// <see cref="Utf8Name"/> so callers can drop them straight into
/// <see cref="InstanceDescription.Extensions"/> /
/// <see cref="DeviceDescription.Extensions"/>. The underlying UTF-8
/// literals live in the assembly's read-only data segment — process
/// lifetime, no allocation.
/// </summary>
public static class VulkanExtensions
{
    /// <summary>VK_KHR_surface — instance-level. Required for any
    /// platform-specific surface creation.</summary>
    public static Utf8Name KhrSurface => Utf8Name.FromLiteral("VK_KHR_surface"u8);

    /// <summary>VK_KHR_win32_surface — instance-level. Pair with
    /// <see cref="KhrSurface"/> when creating a surface from an HWND
    /// via <see cref="Surface.CreateWin32"/>.</summary>
    public static Utf8Name KhrWin32Surface => Utf8Name.FromLiteral("VK_KHR_win32_surface"u8);

    /// <summary>VK_KHR_xlib_surface — instance-level. Pair with
    /// <see cref="KhrSurface"/> when creating a surface from an Xlib
    /// <c>Display*</c> + <c>Window</c> via
    /// <see cref="Surface.CreateXlib"/>.</summary>
    public static Utf8Name KhrXlibSurface => Utf8Name.FromLiteral("VK_KHR_xlib_surface"u8);

    /// <summary>VK_KHR_wayland_surface — instance-level. Pair with
    /// <see cref="KhrSurface"/> when creating a surface from a Wayland
    /// <c>wl_display*</c> + <c>wl_surface*</c> via
    /// <see cref="Surface.CreateWayland"/>.</summary>
    public static Utf8Name KhrWaylandSurface => Utf8Name.FromLiteral("VK_KHR_wayland_surface"u8);

    /// <summary>VK_EXT_metal_surface — instance-level (MoltenVK on
    /// macOS). Pair with <see cref="KhrSurface"/> when creating a
    /// surface from a Cocoa <c>CAMetalLayer</c> via
    /// <see cref="Surface.CreateMetal"/>.</summary>
    public static Utf8Name ExtMetalSurface => Utf8Name.FromLiteral("VK_EXT_metal_surface"u8);

    /// <summary>VK_EXT_headless_surface — instance-level. Pair with
    /// <see cref="KhrSurface"/> to create a window-system-independent
    /// surface via <see cref="Surface.CreateHeadless"/>. Implemented by
    /// Mesa (lavapipe), so it lets the WSI stack — caps queries, formats,
    /// swapchain create, acquire/present — run on hosted CI runners with
    /// no display server attached.</summary>
    public static Utf8Name ExtHeadlessSurface => Utf8Name.FromLiteral("VK_EXT_headless_surface"u8);

    /// <summary>VK_KHR_swapchain — device-level. Required for
    /// <see cref="Swapchain"/> creation and present.</summary>
    public static Utf8Name KhrSwapchain => Utf8Name.FromLiteral("VK_KHR_swapchain"u8);

    /// <summary>VK_EXT_memory_budget — device-level. Pair with
    /// <see cref="AllocatorDescription.EnableMemoryBudget"/> so
    /// <see cref="Allocator.GetHeapBudgets"/> reports the driver's real heap
    /// usage rather than VMA's own bookkeeping. Needed to see allocations VMA
    /// never made — notably DLSS's internal history and scratch surfaces
    /// (issue #214).</summary>
    public static Utf8Name ExtMemoryBudget => Utf8Name.FromLiteral("VK_EXT_memory_budget"u8);

    /// <summary>VK_EXT_mesh_shader — device-level. Enables
    /// <see cref="GraphicsPipelineBuilder.WithMeshStages"/> /
    /// <see cref="GraphicsPipelineBuilder.WithTaskStage"/> and the
    /// <see cref="CommandRecorder.DrawMeshTasks"/> family. Pair it with the
    /// <c>meshShader</c> (and, for a task stage, <c>taskShader</c>) feature via
    /// <see cref="DeviceDescription.ConfigureFeatures"/> pushing
    /// <c>VkPhysicalDeviceMeshShaderFeaturesEXT</c> — the extension alone is not
    /// enough.</summary>
    public static Utf8Name ExtMeshShader => Utf8Name.FromLiteral(DeviceExtensionNames.MeshShader);

    /// <summary>VK_KHR_external_memory_win32 — device-level. Enable to
    /// export a <c>VkDeviceMemory</c> as a Win32 <c>HANDLE</c> for cross-API
    /// GPU interop (e.g. an <see cref="ExportableImage"/> imported by a
    /// compositor). Pairs with <see cref="ExternalHandleType.OpaqueWin32"/>.
    /// Windows only.</summary>
    public static Utf8Name KhrExternalMemoryWin32 => Utf8Name.FromLiteral("VK_KHR_external_memory_win32"u8);

    /// <summary>VK_KHR_external_memory_fd — device-level. Enable to export a
    /// <c>VkDeviceMemory</c> as a POSIX file descriptor. Pairs with
    /// <see cref="ExternalHandleType.OpaqueFd"/>. Linux only.</summary>
    public static Utf8Name KhrExternalMemoryFd => Utf8Name.FromLiteral("VK_KHR_external_memory_fd"u8);

    /// <summary>VK_KHR_external_semaphore_win32 — device-level. Enable to
    /// export a <c>VkSemaphore</c> as a Win32 <c>HANDLE</c> for the
    /// cross-API sync handshake. Pairs with an exportable
    /// <see cref="ExportableSemaphore"/>. Windows only.</summary>
    public static Utf8Name KhrExternalSemaphoreWin32 => Utf8Name.FromLiteral("VK_KHR_external_semaphore_win32"u8);

    /// <summary>VK_KHR_external_semaphore_fd — device-level. Enable to
    /// export a <c>VkSemaphore</c> as a POSIX file descriptor. Pairs with an
    /// exportable <see cref="ExportableSemaphore"/>. Linux only.</summary>
    public static Utf8Name KhrExternalSemaphoreFd => Utf8Name.FromLiteral("VK_KHR_external_semaphore_fd"u8);

    /// <summary>VK_KHR_acceleration_structure — device-level. Enables
    /// <see cref="Device.CreateAccelerationStructure"/>,
    /// <see cref="Device.GetAccelerationStructureBuildSizes"/>,
    /// <see cref="AccelerationStructure.GetDeviceAddress"/> and the
    /// <see cref="CommandRecorder.BuildAccelerationStructures"/> /
    /// <see cref="CommandRecorder.WriteAccelerationStructuresProperties"/> /
    /// <see cref="CommandRecorder.CopyAccelerationStructure"/> commands. This is
    /// the <b>only</b> one of the three ray-query extensions the wrapper gates
    /// entry-point resolution on.</summary>
    /// <remarks>
    /// <para><b>The full enable recipe, once.</b> Ray query needs all three
    /// extensions in <see cref="DeviceDescription.Extensions"/> —
    /// <see cref="KhrAccelerationStructure"/>,
    /// <see cref="KhrDeferredHostOperations"/> and <see cref="KhrRayQuery"/> —
    /// <b>and</b> three features pushed from
    /// <see cref="DeviceDescription.ConfigureFeatures"/>:
    /// <c>VkPhysicalDeviceAccelerationStructureFeaturesKHR.accelerationStructure</c>,
    /// <c>VkPhysicalDeviceRayQueryFeaturesKHR.rayQuery</c>, and Vulkan 1.2's
    /// <c>VkPhysicalDeviceVulkan12Features.bufferDeviceAddress</c> (every
    /// build input, the scratch and the TLAS instance references are device
    /// addresses). The extensions alone are not enough, and the wrapper cannot
    /// check the features — Vulkan exposes no post-<c>vkCreateDevice</c>
    /// feature query.</para>
    /// </remarks>
    public static Utf8Name KhrAccelerationStructure =>
        Utf8Name.FromLiteral(DeviceExtensionNames.AccelerationStructure);

    /// <summary>VK_KHR_ray_query — device-level. Adds the <c>OpRayQuery*</c>
    /// SPIR-V instructions a shader uses to traverse a TLAS bound through a
    /// <c>VK_DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR</c> binding (see
    /// <see cref="DescriptorWrite.AccelerationStructure"/>).</summary>
    /// <remarks>Gates <b>nothing</b> in the wrapper: ray query defines no
    /// Vulkan entry points at all, only shader capability. Pass it to
    /// <c>vkCreateDevice</c> alongside <see cref="KhrAccelerationStructure"/>
    /// and enable <c>VkPhysicalDeviceRayQueryFeaturesKHR.rayQuery</c> — the
    /// full recipe is on <see cref="KhrAccelerationStructure"/>.</remarks>
    public static Utf8Name KhrRayQuery => Utf8Name.FromLiteral(DeviceExtensionNames.RayQuery);

    /// <summary>VK_KHR_deferred_host_operations — device-level. Required by
    /// <see cref="KhrAccelerationStructure"/> as a device-creation dependency:
    /// <c>vkCreateDevice</c> fails without it.</summary>
    /// <remarks>Gates <b>nothing</b> in the wrapper, and the wrapper calls no
    /// deferred command — it records the command-buffer forms of build and
    /// copy, which take no <c>VkDeferredOperationKHR</c>. It is listed here
    /// only so the caller can satisfy the dependency. The full recipe is on
    /// <see cref="KhrAccelerationStructure"/>.</remarks>
    public static Utf8Name KhrDeferredHostOperations =>
        Utf8Name.FromLiteral(DeviceExtensionNames.DeferredHostOperations);

    /// <summary>VK_EXT_device_fault — device-level. Enables
    /// <see cref="Device.TryGetDeviceFault(out DeviceFaultReport)"/> after
    /// device loss (used when <see cref="KhrDeviceFault"/> is not also
    /// enabled).</summary>
    /// <remarks>
    /// <para><b>The enable recipe.</b> Add this name to
    /// <see cref="DeviceDescription.Extensions"/>, then push
    /// <c>VkPhysicalDeviceFaultFeaturesEXT</c> from
    /// <see cref="DeviceDescription.ConfigureFeatures"/> with
    /// <c>deviceFault = 1</c> — and <c>deviceFaultVendorBinary = 1</c> for a
    /// vendor crash dump in <see cref="DeviceFaultReport.VendorBinary"/>. The
    /// extension alone is not enough. <c>deviceFault</c> is guaranteed
    /// wherever the extension is advertised; <c>deviceFaultVendorBinary</c>
    /// is optional, so read it first with
    /// <see cref="PhysicalDevice.TryGetFeatures{T}(Utf8Name, out T)"/>
    /// (<c>TryGetFeatures&lt;VkPhysicalDeviceFaultFeaturesEXT&gt;(ExtDeviceFault, out …)</c>)
    /// and set it only when reported. What the wrapper cannot see is the
    /// <b>enabled</b> state after <c>vkCreateDevice</c> — Vulkan exposes no
    /// post-create feature query — which is why
    /// <see cref="Device.DeviceFaultApi"/> reflects the extension
    /// only.</para>
    /// </remarks>
    public static Utf8Name ExtDeviceFault => Utf8Name.FromLiteral(DeviceExtensionNames.ExtDeviceFault);

    /// <summary>VK_KHR_device_fault — device-level. Promotion of
    /// <see cref="ExtDeviceFault"/>; when enabled,
    /// <see cref="Device.TryGetDeviceFault(out DeviceFaultReport)"/> reads
    /// through it in preference to EXT.</summary>
    /// <remarks>
    /// <para><b>The enable recipe.</b> Add this name to
    /// <see cref="DeviceDescription.Extensions"/>, then push
    /// <b><c>VkPhysicalDeviceFaultFeaturesKHR</c></b> — a different
    /// <c>sType</c> from the EXT feature struct — from
    /// <see cref="DeviceDescription.ConfigureFeatures"/> with
    /// <c>deviceFault = 1</c>, and <c>deviceFaultVendorBinary = 1</c> for a
    /// vendor crash dump. As with EXT, the extension alone is not enough.
    /// <c>deviceFault</c> is guaranteed wherever the extension is advertised;
    /// <c>deviceFaultVendorBinary</c> is optional, so read it first with
    /// <see cref="PhysicalDevice.TryGetFeatures{T}(Utf8Name, out T)"/>
    /// (<c>TryGetFeatures&lt;VkPhysicalDeviceFaultFeaturesKHR&gt;(KhrDeviceFault, out …)</c>)
    /// and set it only when reported. Query the KHR struct, not the EXT one —
    /// they are separate structs, and a bit is legal to enable only in the
    /// struct that reported it. What the wrapper cannot see is the
    /// <b>enabled</b> state after <c>vkCreateDevice</c>, which is why
    /// <see cref="Device.DeviceFaultApi"/> reflects the extension
    /// only.</para>
    /// <para><b>Validation layer floor: 1.4.363.</b> That layer validates both
    /// KHR commands and the KHR feature struct. Older layers (1.4.341 is one)
    /// do not know the extension, and report the KHR feature struct as
    /// <c>VUID-VkDeviceCreateInfo-pNext-pNext</c> ("unknown
    /// VkStructureType"). The wrapper does not suppress that message.</para>
    /// <para><b>Healthy-device polling.</b>
    /// <see cref="Device.TryPollDeviceFaults"/> reads masked faults (faults
    /// the driver recovered from without losing the device) when
    /// <c>deviceFaultReportMasked</c> was enabled. Query support first with
    /// <c>gpu.TryGetFeatures&lt;VkPhysicalDeviceFaultFeaturesKHR&gt;(KhrDeviceFault, out var f)</c>
    /// and copy <c>f.deviceFaultReportMasked</c> into the pushed struct.
    /// <c>deviceFaultDeviceLostOnMasked</c> remains the caller's to
    /// set.</para>
    /// </remarks>
    public static Utf8Name KhrDeviceFault => Utf8Name.FromLiteral(DeviceExtensionNames.KhrDeviceFault);

    /// <summary>VK_KHR_shader_abort — device-level. With
    /// <see cref="KhrDeviceFault"/>,
    /// <see cref="Device.TryGetDeviceFault(out DeviceFaultReport)"/> returns
    /// the messages of shaders that executed <c>OpAbortKHR</c> in
    /// <see cref="DeviceFaultReport.ShaderAbortMessages"/>.</summary>
    /// <remarks>
    /// <para><b>The enable recipe.</b></para>
    /// <list type="number">
    /// <item><description>Add <see cref="KhrDeviceFault"/>, this name and
    /// <see cref="KhrShaderConstantData"/> to
    /// <see cref="DeviceDescription.Extensions"/>. The last is a dependency:
    /// the validation layer reports
    /// <c>VUID-vkCreateDevice-ppEnabledExtensionNames-01387</c> without
    /// it.</description></item>
    /// <item><description>From <see cref="DeviceDescription.ConfigureFeatures"/>,
    /// push <c>VkPhysicalDeviceFaultFeaturesKHR</c> with
    /// <c>deviceFault = 1</c> and <c>VkPhysicalDeviceShaderAbortFeaturesKHR</c>
    /// with <c>shaderAbort = 1</c>, after checking
    /// <c>gpu.TryGetFeatures&lt;VkPhysicalDeviceShaderAbortFeaturesKHR&gt;(KhrShaderAbort, out …)</c>
    /// (<see cref="PhysicalDevice.TryGetFeatures{T}(Utf8Name, out T)"/>).</description></item>
    /// <item><description>Slang's <c>abort(format, args…)</c> emits
    /// <c>OpAbortKHR</c> (v2026.19); <c>Ahjo.Vulkan.Slang.SlangAbortMessage</c>
    /// decodes its payloads.</description></item>
    /// <item><description>The wrapper cannot see the enabled feature, only the
    /// extension: it chains the abort-message struct on the post-loss
    /// debug-info read whenever this extension was enabled.</description></item>
    /// </list>
    /// </remarks>
    public static Utf8Name KhrShaderAbort => Utf8Name.FromLiteral(DeviceExtensionNames.KhrShaderAbort);

    /// <summary>VK_KHR_shader_constant_data — device-level. Required by
    /// <see cref="KhrShaderAbort"/> as a device-creation dependency.</summary>
    /// <remarks>Gates <b>nothing</b> in the wrapper. It is listed here only so
    /// the caller can satisfy the dependency. The
    /// <c>shaderConstantData</c> feature is needed only by shaders that declare
    /// <c>ConstantDataKHR</c>; Slang's <c>abort</c> does not. The full recipe
    /// is on <see cref="KhrShaderAbort"/>.</remarks>
    public static Utf8Name KhrShaderConstantData =>
        Utf8Name.FromLiteral(DeviceExtensionNames.KhrShaderConstantData);
}
