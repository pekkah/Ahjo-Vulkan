namespace Ahjo.Vulkan;

/// <summary>
/// Inputs to <see cref="GraphicsPipelineBuilder.WithVertexInput"/>.
/// <c>ref struct</c> so the spans don't escape — they're only consumed
/// during <see cref="GraphicsPipelineBuilder.Build"/>.
/// </summary>
public ref struct VertexInputDescription
{
    public ReadOnlySpan<VertexBindingDescription>   Bindings;
    public ReadOnlySpan<VertexAttributeDescription> Attributes;
}
