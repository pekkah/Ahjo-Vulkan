using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// One vertex attribute (a location + binding + format + offset). Maps
/// onto <c>VkVertexInputAttributeDescription</c>.
/// </summary>
public readonly record struct VertexAttributeDescription
{
    public uint     Location { get; init; }
    public uint     Binding  { get; init; }
    public VkFormat Format   { get; init; }
    public uint     Offset   { get; init; }
}
