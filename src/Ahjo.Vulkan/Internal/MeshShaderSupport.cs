namespace Ahjo.Vulkan;

/// <summary>
/// Shared diagnostic text for the <c>VK_EXT_mesh_shader</c> surface (#201).
/// Two places refuse a mesh workload — <c>GraphicsPipelineBuilder.Build</c>
/// (a mesh stage on a device where the extension was never enabled) and the
/// <c>CommandRecorder.DrawMeshTasks*</c> family (a null entry point) — and
/// both must tell the caller the same thing about how to turn mesh shading
/// on, so the instructions live here once rather than drifting apart.
/// </summary>
internal static class MeshShaderSupport
{
    /// <summary>
    /// The "how to enable it" tail shared by every mesh-shader
    /// not-available message. A <see langword="const"/> so the concatenations
    /// at the call sites fold at compile time.
    /// </summary>
    public const string EnableInstructions =
        "Enable VK_EXT_mesh_shader via DeviceDescription.Extensions " +
        "(VulkanExtensions.ExtMeshShader) and turn on the meshShader (and, for a task stage, " +
        "taskShader) feature by pushing VkPhysicalDeviceMeshShaderFeaturesEXT from " +
        "DeviceDescription.ConfigureFeatures, then re-create the Device.";

    /// <summary>
    /// Appended wherever the wrapper's own check is
    /// <b>extension-only</b>. <c>DeviceFunctionTable</c> resolves the mesh
    /// entry points from the extension list the wrapper passed to
    /// <c>vkCreateDevice</c>, so a non-null <c>CmdDrawMeshTasks</c> proves
    /// the <i>extension</i> was enabled and nothing more: Vulkan offers no
    /// query for the enabled feature chain after device creation, so a
    /// device that enabled the extension but not the <c>meshShader</c> /
    /// <c>taskShader</c> feature still gets past the wrapper and is caught
    /// by the driver and the validation layer.
    /// </summary>
    public const string PartialGuardNote =
        "Note: this check only proves the extension was enabled. The wrapper cannot see the enabled " +
        "feature chain after vkCreateDevice, so a device that enabled VK_EXT_mesh_shader without the " +
        "meshShader (or, for a task stage, taskShader) feature still reaches the driver, which rejects " +
        "it (VUID-VkPipelineShaderStageCreateInfo-stage-02091 / -02092).";
}
