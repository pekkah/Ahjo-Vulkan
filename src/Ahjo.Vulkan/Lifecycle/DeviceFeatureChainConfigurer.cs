using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Hook handed to <see cref="DeviceDescription.ConfigureFeatures"/>;
/// invoked once during <see cref="PhysicalDevice.CreateDevice"/> after the
/// wrapper has pushed its default feature structs onto the chain. Push
/// additional <c>IChainable&lt;VkDeviceCreateInfo&gt;</c> structs here
/// (e.g. <c>VkPhysicalDeviceMeshShaderFeaturesEXT</c>) or flip extra bits
/// on the pre-pushed structs via the <paramref name="features2"/> /
/// <paramref name="features12"/> / <paramref name="features13"/> /
/// <paramref name="features14"/> ref parameters.
/// </summary>
/// <param name="chain">Chain builder positioned past the wrapper's pre-pushed structs.</param>
/// <param name="features2">
/// <c>VkPhysicalDeviceFeatures2</c> already in the chain. The wrapper
/// pre-enables a "3D-game baseline" — <c>samplerAnisotropy</c>,
/// <c>depthClamp</c>, <c>fillModeNonSolid</c>, <c>independentBlend</c>,
/// <c>imageCubeArray</c> — but only the subset the physical device
/// actually advertises; flip additional 1.0-era core bits here (e.g.
/// <c>shaderInt64</c>, <c>multiDrawIndirect</c>,
/// <c>fragmentStoresAndAtomics</c>) without re-pushing the struct.
/// </param>
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
/// <c>VkPhysicalDeviceVulkan14Features</c> already in the chain when
/// the device advertises Vulkan 1.4 — flip 1.4-promoted bits (e.g.
/// <c>maintenance5</c>) directly. On a sub-1.4 device the wrapper omits
/// the struct from the chain (sType 55 is unrecognized by 1.3 drivers
/// and crashes some ICDs), so mutations to this ref are silently
/// dropped. Callers needing 1.4-promoted features on a 1.3 device must
/// request the equivalent extension (e.g. <c>VK_KHR_push_descriptor</c>)
/// via <see cref="DeviceDescription.Extensions"/>.
/// </param>
/// <remarks>
/// <para>The Vulkan spec forbids two pNext nodes with the same sType in
/// one chain, which used to mean a caller wanting an extra 1.3 bit had
/// to either avoid the wrapper's pre-pushed defaults or accept a
/// duplicate-sType throw. With the ref parameters above, callers
/// mutate the wrapper's structs in place and never push their own copy.
/// <see cref="PhysicalDevice.CreateDevice"/> retains the
/// duplicate-sType validator as a defensive guard for callers that
/// nevertheless try to push a second of these structs via
/// <paramref name="chain"/>.</para>
/// <para>The bits the wrapper sets by default
/// (<c>bufferDeviceAddress</c>, <c>timelineSemaphore</c>,
/// <c>separateDepthStencilLayouts</c>, <c>synchronization2</c>,
/// <c>dynamicRendering</c>, <c>pushDescriptor</c>, plus the queried
/// 1.0-era game baseline above) are visible to the configurer through
/// the ref parameters — callers can read them and choose not to clear
/// them. Bits the wrapper would have enabled but the device doesn't
/// support are left at zero, so device creation does not fail on the
/// optional 1.0 flags.</para>
/// </remarks>
public unsafe delegate void DeviceFeatureChainConfigurer(
    ref ChainBuilder<VkDeviceCreateInfo> chain,
    ref VkPhysicalDeviceFeatures2        features2,
    ref VkPhysicalDeviceVulkan12Features features12,
    ref VkPhysicalDeviceVulkan13Features features13,
    ref VkPhysicalDeviceVulkan14Features features14);
