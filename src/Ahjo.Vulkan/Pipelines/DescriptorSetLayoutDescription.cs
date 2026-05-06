namespace Ahjo.Vulkan;

/// <summary>
/// Inputs to <see cref="Device.CreateDescriptorSetLayout"/>.
/// <c>ref struct</c> so the spans don't escape; the layout is built
/// synchronously inside <see cref="Device.CreateDescriptorSetLayout"/>
/// and the spans don't need to outlive the call.
/// </summary>
public ref struct DescriptorSetLayoutDescription
{
    /// <summary>Bindings in slot order (the wrapper sorts internally).</summary>
    public ReadOnlySpan<DescriptorBinding> Bindings;

    /// <summary>
    /// Set true to create a layout that supports
    /// <see cref="DescriptorBindingFlags.UpdateAfterBind"/>; required when
    /// any binding uses that flag.
    /// </summary>
    public bool UpdateAfterBindPool;

    /// <summary>
    /// Set true to enable the push-descriptor path
    /// (<c>VK_DESCRIPTOR_SET_LAYOUT_CREATE_PUSH_DESCRIPTOR_BIT</c>). Layouts
    /// flagged this way cannot be used to allocate <c>VkDescriptorSet</c>s
    /// from a pool; descriptors are recorded inline via
    /// <see cref="CommandRecorder.PushDescriptors{T}"/> instead.
    /// </summary>
    public bool PushDescriptor;
}
