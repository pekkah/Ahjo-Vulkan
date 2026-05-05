using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// One vertex input binding (a stride + step rate) for the graphics
/// pipeline. Maps onto <c>VkVertexInputBindingDescription</c>.
/// </summary>
public readonly record struct VertexBindingDescription
{
    public uint              Slot      { get; init; }
    public uint              Stride    { get; init; }
    public VkVertexInputRate InputRate { get; init; }
}
