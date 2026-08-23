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
}
