namespace Ahjo.Vulkan;

/// <summary>
/// Inputs to <see cref="Device.CreatePipelineLayout"/>. <c>ref struct</c>
/// to keep the spans on the stack — the layout build is synchronous so
/// the caller's lifetime is sufficient.
/// </summary>
public ref struct PipelineLayoutDescription
{
    public ReadOnlySpan<DescriptorSetLayout> SetLayouts;
    public ReadOnlySpan<PushConstantRange>   PushConstantRanges;
}
