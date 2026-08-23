namespace Ahjo.Vulkan;

/// <summary>
/// Shared diagnostic text for the <c>VK_KHR_acceleration_structure</c>
/// surface (#202). Seven places refuse an acceleration-structure operation on
/// a device that never enabled the extension —
/// <see cref="Device.CreateAccelerationStructure"/>,
/// <see cref="Device.GetAccelerationStructureBuildSizes"/>,
/// <see cref="Device.CreateQueryPool(QueryType, uint)"/> for a compacted-size
/// pool, <see cref="AccelerationStructure.GetDeviceAddress"/>, and the
/// <see cref="CommandRecorder.BuildAccelerationStructures"/> /
/// <see cref="CommandRecorder.WriteAccelerationStructuresProperties"/> /
/// <see cref="CommandRecorder.CopyAccelerationStructure"/> trio — and all
/// seven must tell the caller the same thing about how to turn ray query on,
/// so the instructions live here once rather than drifting apart. The
/// <see cref="MeshShaderSupport"/> shape.
/// </summary>
internal static class AccelerationStructureSupport
{
    /// <summary>
    /// The "how to enable it" tail shared by every acceleration-structure
    /// not-available message. A <see langword="const"/> so the concatenations
    /// at the call sites fold at compile time.
    /// </summary>
    public const string EnableInstructions =
        "Enable VK_KHR_acceleration_structure, VK_KHR_deferred_host_operations and " +
        "VK_KHR_ray_query via DeviceDescription.Extensions " +
        "(VulkanExtensions.KhrAccelerationStructure / .KhrDeferredHostOperations / .KhrRayQuery) " +
        "and turn on the accelerationStructure, rayQuery and bufferDeviceAddress features by pushing " +
        "VkPhysicalDeviceAccelerationStructureFeaturesKHR, VkPhysicalDeviceRayQueryFeaturesKHR and " +
        "VkPhysicalDeviceVulkan12Features from DeviceDescription.ConfigureFeatures, then re-create the Device.";

    /// <summary>
    /// Appended wherever the wrapper's own check is <b>extension-only</b>.
    /// <see cref="DeviceFunctionTable"/> resolves the acceleration-structure
    /// entry points from the extension list the wrapper passed to
    /// <c>vkCreateDevice</c>, so a non-null pointer proves the
    /// <i>extension</i> was enabled and nothing more: Vulkan offers no query
    /// for the enabled feature chain after device creation, so a device that
    /// enabled <c>VK_KHR_acceleration_structure</c> but not the
    /// <c>accelerationStructure</c> feature still gets past the wrapper and is
    /// caught by the driver and the validation layer.
    /// </summary>
    public const string PartialGuardNote =
        "Note: this check only proves the extension was enabled. The wrapper cannot see the enabled " +
        "feature chain after vkCreateDevice, so a device that enabled VK_KHR_acceleration_structure " +
        "without the accelerationStructure feature still reaches the driver, which rejects it " +
        "(VUID-vkCmdBuildAccelerationStructuresKHR-accelerationStructure-08923 / " +
        "VUID-vkCmdCopyAccelerationStructureKHR-accelerationStructure-08925).";
}
