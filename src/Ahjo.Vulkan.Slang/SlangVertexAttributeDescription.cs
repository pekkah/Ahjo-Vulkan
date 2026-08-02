using Ahjo.Vulkan.Slang.Native;

namespace Ahjo.Vulkan.Slang;

/// <summary>
/// One vertex attribute in a Slang program.
/// </summary>
public readonly record struct SlangVertexAttributeDescription
{
    /// <summary>The vertex input location, as decorated in the emitted SPIR-V.</summary>
    public uint Location { get; init; }

    /// <summary>
    /// The field's name in the shader — <c>position</c>, <c>uv</c>.
    /// </summary>
    /// <remarks>
    /// This is not what an application's mesh format keys by. A vertex-buffer
    /// binder matches on <see cref="SemanticName"/> plus
    /// <see cref="SemanticIndex"/>; the field name is a detail of how the
    /// shader author spelled the struct.
    /// </remarks>
    public string Name { get; init; }

    /// <summary>
    /// The HLSL semantic, upper-cased and with its trailing digits stripped —
    /// <c>POSITION</c>, <c>TEXCOORD</c>, <c>TANGENT</c> — or
    /// <see cref="string.Empty"/> when the input declares none.
    /// </summary>
    /// <remarks>
    /// <b>This is what a vertex-buffer binder matches on.</b> Slang splits
    /// <c>TEXCOORD0</c> into <see cref="SemanticName"/> <c>TEXCOORD</c> and
    /// <see cref="SemanticIndex"/> <c>0</c>, so a binder comparing against the
    /// literal string <c>"TEXCOORD0"</c> never matches — compare the two parts.
    /// </remarks>
    public string SemanticName { get; init; }

    /// <summary>
    /// The semantic's trailing index: <c>0</c> for <c>TEXCOORD0</c>, <c>1</c>
    /// for <c>TEXCOORD1</c>, <c>0</c> for a semantic with no digits.
    /// </summary>
    public uint SemanticIndex { get; init; }

    /// <summary>The declared type's kind.</summary>
    public SlangTypeKind Kind { get; init; }

    /// <summary>The scalar type, for scalar and vector inputs.</summary>
    public SlangScalarType ScalarType { get; init; }

    /// <summary>Components in a vector input; <c>1</c> for a scalar.</summary>
    public uint ComponentCount { get; init; }

    /// <summary>Rows, for a matrix input.</summary>
    public uint RowCount { get; init; }

    /// <summary>Columns, for a matrix input.</summary>
    public uint ColumnCount { get; init; }

    /// <summary>How many consecutive locations the input occupies.</summary>
    public long SizeInLocations { get; init; }
}
