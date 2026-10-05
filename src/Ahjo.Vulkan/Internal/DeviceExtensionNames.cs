namespace Ahjo.Vulkan;

/// <summary>
/// UTF-8 string literals for the <b>device</b> extension and device-level
/// Vulkan function symbol names this assembly hard-codes. Centralized so a
/// typo can only be made in one place.
/// </summary>
/// <remarks>
/// <para>The instance-level counterpart is
/// <see cref="InstanceExtensionNames"/>; the two are <b>not</b>
/// interchangeable. A device extension is the one named in
/// <see cref="DeviceDescription.Extensions"/> and passed to
/// <c>vkCreateDevice</c>, which is what
/// <see cref="DeviceFunctionTable"/> gates its resolution on. An instance
/// extension (<c>VK_EXT_debug_utils</c>) never appears in that list even
/// when its device-level entry points are reached through
/// <c>vkGetDeviceProcAddr</c>, so gating those on the device list would
/// disable them everywhere.</para>
/// <para><b>Not every name here gates something.</b>
/// <see cref="AccelerationStructure"/> gates the seven acceleration-structure
/// entry points below. <see cref="RayQuery"/> and
/// <see cref="DeferredHostOperations"/> gate <b>nothing</b>: ray query is a
/// pure SPIR-V capability and defines no entry points at all, and the wrapper
/// calls no deferred-host-operation command (it records the command-buffer
/// forms of build and copy, which take no <c>VkDeferredOperationKHR</c>).
/// They exist so <see cref="VulkanExtensions"/> can hand callers the names
/// <c>vkCreateDevice</c> requires — <c>VK_KHR_acceleration_structure</c>
/// itself depends on <c>VK_KHR_deferred_host_operations</c>, so device
/// creation fails without it whether or not the wrapper ever calls a
/// deferred command.</para>
/// <para><see cref="ExtDeviceFault"/> gates <c>vkGetDeviceFaultInfoEXT</c>,
/// which is legal only on a lost device. <see cref="KhrDeviceFault"/> gates
/// <c>vkGetDeviceFaultReportsKHR</c>, which is legal at any time and called by
/// the wrapper after loss and, through <see cref="Device.TryPollDeviceFaults"/>,
/// on a healthy device, and <c>vkGetDeviceFaultDebugInfoKHR</c>, which is
/// legal only on a lost device.</para>
/// <para><see cref="KhrShaderAbort"/> and <see cref="KhrShaderConstantData"/>
/// gate no entry point. <see cref="KhrShaderAbort"/> gates the chaining of
/// <c>VkDeviceFaultShaderAbortMessageInfoKHR</c> on the KHR debug-info read
/// (#245); <see cref="KhrShaderConstantData"/> is its device-creation
/// dependency.</para>
/// </remarks>
internal static class DeviceExtensionNames
{
    public static ReadOnlySpan<byte> MeshShader => "VK_EXT_mesh_shader"u8;

    public static ReadOnlySpan<byte> CmdDrawMeshTasks              => "vkCmdDrawMeshTasksEXT"u8;
    public static ReadOnlySpan<byte> CmdDrawMeshTasksIndirect      => "vkCmdDrawMeshTasksIndirectEXT"u8;
    public static ReadOnlySpan<byte> CmdDrawMeshTasksIndirectCount => "vkCmdDrawMeshTasksIndirectCountEXT"u8;

    public static ReadOnlySpan<byte> AccelerationStructure  => "VK_KHR_acceleration_structure"u8;
    public static ReadOnlySpan<byte> RayQuery               => "VK_KHR_ray_query"u8;
    public static ReadOnlySpan<byte> DeferredHostOperations => "VK_KHR_deferred_host_operations"u8;

    public static ReadOnlySpan<byte> CreateAccelerationStructure              => "vkCreateAccelerationStructureKHR"u8;
    public static ReadOnlySpan<byte> DestroyAccelerationStructure             => "vkDestroyAccelerationStructureKHR"u8;
    public static ReadOnlySpan<byte> GetAccelerationStructureBuildSizes       => "vkGetAccelerationStructureBuildSizesKHR"u8;
    public static ReadOnlySpan<byte> GetAccelerationStructureDeviceAddress    => "vkGetAccelerationStructureDeviceAddressKHR"u8;
    public static ReadOnlySpan<byte> CmdBuildAccelerationStructures           => "vkCmdBuildAccelerationStructuresKHR"u8;
    public static ReadOnlySpan<byte> CmdWriteAccelerationStructuresProperties => "vkCmdWriteAccelerationStructuresPropertiesKHR"u8;
    public static ReadOnlySpan<byte> CmdCopyAccelerationStructure             => "vkCmdCopyAccelerationStructureKHR"u8;

    public static ReadOnlySpan<byte> ExtDeviceFault          => "VK_EXT_device_fault"u8;
    public static ReadOnlySpan<byte> GetDeviceFaultInfo      => "vkGetDeviceFaultInfoEXT"u8;

    public static ReadOnlySpan<byte> KhrDeviceFault          => "VK_KHR_device_fault"u8;
    public static ReadOnlySpan<byte> GetDeviceFaultReports   => "vkGetDeviceFaultReportsKHR"u8;
    public static ReadOnlySpan<byte> GetDeviceFaultDebugInfo => "vkGetDeviceFaultDebugInfoKHR"u8;

    public static ReadOnlySpan<byte> KhrShaderAbort        => "VK_KHR_shader_abort"u8;
    public static ReadOnlySpan<byte> KhrShaderConstantData => "VK_KHR_shader_constant_data"u8;
}
