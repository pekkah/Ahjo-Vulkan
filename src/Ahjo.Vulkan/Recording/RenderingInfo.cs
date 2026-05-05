using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Inputs to <see cref="CommandRecorder.BeginRendering"/>. <c>ref struct</c>
/// because of the spans; the recorder consumes them synchronously inside
/// <c>vkCmdBeginRendering</c> so the caller's lifetime is sufficient.
/// </summary>
public ref struct RenderingInfo
{
    public VkRect2D                       RenderArea;
    public uint                           LayerCount;
    public uint                           ViewMask;
    public ReadOnlySpan<ColorAttachment>  ColorAttachments;
    public DepthAttachment?               DepthAttachment;
    public DepthAttachment?               StencilAttachment;
}
