namespace Ahjo.Vulkan;

/// <summary>
/// Inputs to <see cref="PhysicalDevice.CreateDevice"/>. <c>ref struct</c>
/// because of the spans. Field defaults are legal: <see cref="Queues"/>
/// must be non-empty (validated in <c>CreateDevice</c>); the rest may be
/// empty / null.
/// </summary>
public ref struct DeviceDescription
{
    /// <summary>Queues to create with the device. Must contain at least one entry.</summary>
    public ReadOnlySpan<QueueRequest> Queues;

    /// <summary>UTF-8 device-extension names to enable. Empty by default.</summary>
    public ReadOnlySpan<Utf8Name> Extensions;

    /// <summary>
    /// Optional callback invoked exactly once during
    /// <see cref="PhysicalDevice.CreateDevice"/> after the wrapper has
    /// pushed its 1.4 default feature structs onto the chain. Push
    /// additional <c>IChainable&lt;VkDeviceCreateInfo&gt;</c> structs
    /// here. <see langword="null"/> = no extra structs.
    /// </summary>
    public DeviceFeatureChainConfigurer? ConfigureFeatures;
}
