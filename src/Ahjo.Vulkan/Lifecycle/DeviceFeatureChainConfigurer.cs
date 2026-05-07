using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Hook handed to <see cref="DeviceDescription.ConfigureFeatures"/>;
/// invoked once during <see cref="PhysicalDevice.CreateDevice"/> after the
/// wrapper has pushed its 1.2/1.3/1.4 default feature structs onto the
/// chain. Push additional <c>IChainable&lt;VkDeviceCreateInfo&gt;</c>
/// structs here (e.g. <c>VkPhysicalDeviceMeshShaderFeaturesEXT</c>) or
/// flip extra bits on the pre-pushed Vulkan-version feature structs via
/// the <paramref name="features12"/> / <paramref name="features13"/> /
/// <paramref name="features14"/> ref parameters.
/// </summary>
/// <param name="chain">Chain builder positioned past the wrapper's pre-pushed structs.</param>
/// <param name="features12">
/// <c>VkPhysicalDeviceVulkan12Features</c> already in the chain — flip
/// additional bits here (e.g. <c>descriptorIndexing</c>) without
/// re-pushing the struct.
/// </param>
/// <param name="features13">
/// <c>VkPhysicalDeviceVulkan13Features</c> already in the chain — same
/// pattern, e.g. enable <c>maintenance4</c> or
/// <c>shaderDemoteToHelperInvocation</c>.
/// </param>
/// <param name="features14">
/// <c>VkPhysicalDeviceVulkan14Features</c> already in the chain — flip
/// 1.4-promoted bits (e.g. <c>maintenance5</c>) directly.
/// </param>
/// <remarks>
/// <para>The Vulkan spec forbids two pNext nodes with the same sType in
/// one chain, which used to mean a caller wanting an extra 1.3 bit had
/// to either avoid the wrapper's pre-pushed defaults or accept a
/// duplicate-sType throw. With the ref parameters above, callers
/// mutate the wrapper's structs in place and never push their own copy.
/// <see cref="PhysicalDevice.CreateDevice"/> retains the
/// duplicate-sType validator as a defensive guard for callers that
/// nevertheless try to push a second
/// <c>VkPhysicalDeviceVulkan1{2,3,4}Features</c> via
/// <paramref name="chain"/>.</para>
/// <para>The bits the wrapper sets unconditionally
/// (<c>bufferDeviceAddress</c>, <c>timelineSemaphore</c>,
/// <c>synchronization2</c>, <c>dynamicRendering</c>,
/// <c>pushDescriptor</c>) are visible to the configurer through the
/// ref parameters — callers can read them and choose not to clear them.
/// </para>
/// </remarks>
public unsafe delegate void DeviceFeatureChainConfigurer(
    ref ChainBuilder<VkDeviceCreateInfo> chain,
    ref VkPhysicalDeviceVulkan12Features features12,
    ref VkPhysicalDeviceVulkan13Features features13,
    ref VkPhysicalDeviceVulkan14Features features14);
