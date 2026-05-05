using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Hook handed to <see cref="DeviceDescription.ConfigureFeatures"/>;
/// invoked once during <see cref="PhysicalDevice.CreateDevice"/> after the
/// wrapper has pushed its 1.4 default feature structs onto the chain.
/// Push additional <c>IChainable&lt;VkDeviceCreateInfo&gt;</c> structs
/// here (e.g. <c>VkPhysicalDeviceMeshShaderFeaturesEXT</c>).
/// </summary>
public unsafe delegate void DeviceFeatureChainConfigurer(ref ChainBuilder<VkDeviceCreateInfo> chain);
