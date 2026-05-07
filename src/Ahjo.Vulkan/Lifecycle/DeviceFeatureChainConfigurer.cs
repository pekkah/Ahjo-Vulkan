using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Hook handed to <see cref="DeviceDescription.ConfigureFeatures"/>;
/// invoked once during <see cref="PhysicalDevice.CreateDevice"/> after the
/// wrapper has pushed its 1.2/1.3/1.4 default feature structs onto the
/// chain. Push additional <c>IChainable&lt;VkDeviceCreateInfo&gt;</c>
/// structs here (e.g. <c>VkPhysicalDeviceMeshShaderFeaturesEXT</c>).
/// </summary>
/// <remarks>
/// Do not push <c>VkPhysicalDeviceVulkan12Features</c>,
/// <c>VkPhysicalDeviceVulkan13Features</c>, or
/// <c>VkPhysicalDeviceVulkan14Features</c> from this hook — the wrapper
/// already owns those slots and the Vulkan spec disallows two structs of
/// the same sType in one chain. <see cref="PhysicalDevice.CreateDevice"/>
/// throws <see cref="ArgumentException"/> when this rule is violated.
/// To toggle bits the wrapper doesn't enable by default, push the
/// per-feature extension struct (most 1.x bits also have an EXT/KHR
/// equivalent) or open an issue requesting wrapper-side coverage.
/// </remarks>
public unsafe delegate void DeviceFeatureChainConfigurer(ref ChainBuilder<VkDeviceCreateInfo> chain);
