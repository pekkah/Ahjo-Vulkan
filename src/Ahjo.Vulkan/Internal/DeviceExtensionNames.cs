namespace Ahjo.Vulkan;

/// <summary>
/// UTF-8 string literals for the <b>device</b> extension and device-level
/// Vulkan function symbol names this assembly hard-codes. Centralized so a
/// typo can only be made in one place.
/// </summary>
/// <remarks>
/// The instance-level counterpart is
/// <see cref="InstanceExtensionNames"/>; the two are <b>not</b>
/// interchangeable. A device extension is the one named in
/// <see cref="DeviceDescription.Extensions"/> and passed to
/// <c>vkCreateDevice</c>, which is what
/// <see cref="DeviceFunctionTable"/> gates its resolution on. An instance
/// extension (<c>VK_EXT_debug_utils</c>) never appears in that list even
/// when its device-level entry points are reached through
/// <c>vkGetDeviceProcAddr</c>, so gating those on the device list would
/// disable them everywhere.
/// </remarks>
internal static class DeviceExtensionNames
{
    public static ReadOnlySpan<byte> MeshShader => "VK_EXT_mesh_shader"u8;

    public static ReadOnlySpan<byte> CmdDrawMeshTasks              => "vkCmdDrawMeshTasksEXT"u8;
    public static ReadOnlySpan<byte> CmdDrawMeshTasksIndirect      => "vkCmdDrawMeshTasksIndirectEXT"u8;
    public static ReadOnlySpan<byte> CmdDrawMeshTasksIndirectCount => "vkCmdDrawMeshTasksIndirectCountEXT"u8;
}
