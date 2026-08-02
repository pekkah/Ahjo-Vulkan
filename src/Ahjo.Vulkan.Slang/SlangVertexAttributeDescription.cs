using Ahjo.Vulkan.Slang.Native;

namespace Ahjo.Vulkan.Slang;

/// <summary>
/// One vertex attribute in a Slang program.
/// </summary>
public readonly record struct SlangVertexAttributeDescription
{
    public uint Location { get; init; }
    public string Name { get; init; }
    public SlangTypeKind Kind { get; init; }
    public SlangScalarType ScalarType { get; init; }
    public uint ComponentCount { get; init; }
    public uint RowCount { get; init; }
    public uint ColumnCount { get; init; }
    public long SizeInLocations { get; init; }
}
