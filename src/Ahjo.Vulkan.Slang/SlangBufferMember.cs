using Ahjo.Vulkan.Slang.Native;

namespace Ahjo.Vulkan.Slang;

/// <summary>
/// One member of a <see cref="SlangBufferLayout"/> — where it sits in the
/// buffer, how big it is, and what type the shader declared it as.
/// </summary>
/// <remarks>
/// The type vocabulary here is deliberately the same as
/// <see cref="SlangVertexAttributeDescription"/>'s — <see cref="Kind"/>,
/// <see cref="ScalarType"/>, <see cref="ComponentCount"/>,
/// <see cref="RowCount"/>, <see cref="ColumnCount"/> — so the package has one
/// way of describing a type rather than two.
/// </remarks>
public readonly record struct SlangBufferMember
{
    /// <summary>
    /// The dotted path from the buffer root: <c>Params.UvScale</c> for the
    /// <c>UvScale</c> field of the <c>Params</c> member.
    /// </summary>
    /// <remarks>
    /// The path is built for readability and for
    /// <see cref="SlangBufferLayout.TryGetMember"/>. It is <b>not</b> the
    /// structural link — <see cref="ParentIndex"/> is, precisely so nothing
    /// depends on <c>.</c> being absent from an identifier.
    /// </remarks>
    public string Name { get; init; }

    /// <summary>
    /// Index into the owning <see cref="SlangBufferLayout.Members"/> of the
    /// enclosing struct member, or <c>-1</c> when this member sits at the
    /// buffer's root.
    /// </summary>
    /// <remarks>
    /// This plus the pre-order ordering is what makes the flat list losslessly
    /// a tree — see <see cref="SlangBufferLayout"/>'s remarks.
    /// </remarks>
    public int ParentIndex { get; init; }

    /// <summary>
    /// Byte offset from the start of the buffer, under
    /// <c>SLANG_PARAMETER_CATEGORY_UNIFORM</c> — the layout Slang baked into
    /// the SPIR-V it emitted for this program's target.
    /// </summary>
    public uint Offset { get; init; }

    /// <summary>
    /// Byte size, excluding trailing padding. <c>0</c> when
    /// <see cref="IsUnsized"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Size"/> and <see cref="Stride"/> differ for anything padded:
    /// a <c>float3</c> in a uniform buffer is 12 bytes of data in a 16-byte
    /// footprint. Copy <see cref="Size"/> bytes; advance by
    /// <see cref="Stride"/>.
    /// </remarks>
    public uint Size { get; init; }

    /// <summary>
    /// Byte size including trailing padding — the member's footprint.
    /// </summary>
    public uint Stride { get; init; }

    /// <summary>
    /// Required byte alignment. This is what a caller checks its own C# struct
    /// against before blitting one over the other.
    /// </summary>
    public uint Alignment { get; init; }

    /// <summary>
    /// <see langword="true"/> when Slang reported a size or element-count
    /// sentinel (<c>slang.h:2361-2362</c>) for this member — a trailing
    /// runtime-sized array. <see cref="Size"/> and <see cref="ElementCount"/>
    /// are then <c>0</c>.
    /// </summary>
    public bool IsUnsized { get; init; }

    /// <summary>
    /// The declared type's name, or <see cref="string.Empty"/> when unnamed.
    /// </summary>
    /// <remarks>
    /// Measured on <c>v2026.14.1</c>: a user struct reports its own name
    /// (<c>MaterialParams</c>) and a scalar reports <c>float</c> / <c>uint</c>,
    /// but a vector reports the <em>generic</em> name <c>vector</c> and a
    /// matrix reports <c>matrix</c> — not <c>float4</c>. Use <see cref="Kind"/>,
    /// <see cref="ScalarType"/> and <see cref="ComponentCount"/> when the exact
    /// type matters; this member is for display.
    /// </remarks>
    public string TypeName { get; init; }

    /// <summary>The declared type's kind. A struct member has its own entry <em>and</em> its fields'.</summary>
    public SlangTypeKind Kind { get; init; }

    /// <summary>The scalar type, for scalar, vector and matrix members.</summary>
    public SlangScalarType ScalarType { get; init; }

    /// <summary>Components in a vector member; <c>1</c> for a scalar; <c>0</c> otherwise.</summary>
    public uint ComponentCount { get; init; }

    /// <summary>Rows, for a matrix member.</summary>
    public uint RowCount { get; init; }

    /// <summary>Columns, for a matrix member.</summary>
    public uint ColumnCount { get; init; }

    /// <summary>
    /// Row- or column-major, for a matrix member.
    /// </summary>
    /// <remarks>
    /// <para>The difference between a correct transform and a transposed one,
    /// with no other symptom. Measured on <c>v2026.14.1</c> with a default
    /// session: Slang reports <c>SLANG_MATRIX_LAYOUT_ROW_MAJOR</c>.</para>
    /// <para><b>Do not be alarmed that the emitted SPIR-V decorates the same
    /// member <c>ColMajor</c>.</b> The two conventions are inverted and are not
    /// in conflict. Slang's <c>ROW_MAJOR</c> means row <c>j</c> lives at
    /// <see cref="Offset"/> <c>+ j *</c> <see cref="MatrixStride"/>; Slang emits
    /// the SPIR-V matrix type <em>transposed</em>, so a SPIR-V "column" is a
    /// Slang row, and <c>ColMajor</c> on that transposed type states the very
    /// same byte layout. They agree about bytes and disagree about words. The
    /// byte-level statement — <c>Size == RowCount * MatrixStride</c> — is what a
    /// caller should build against, and
    /// <c>BufferLayout_Matrix_ReportsLayoutMode</c> asserts it.</para>
    /// </remarks>
    public SlangMatrixLayoutMode MatrixLayout { get; init; }

    /// <summary>
    /// Bytes between consecutive rows (or columns, per
    /// <see cref="MatrixLayout"/>) of a matrix member.
    /// </summary>
    /// <remarks>
    /// Slang exposes no dedicated getter for this. Measured on
    /// <c>v2026.14.1</c> / win-x64, <c>GetElementTypeLayout</c> on a
    /// <c>float4x4</c>'s type layout yields the row vector's layout, whose
    /// <c>UNIFORM</c> stride is <c>16</c> — that is the derivation used here.
    /// <c>GetElementStride</c> on the matrix itself returns <c>0</c>. It is
    /// deliberately <b>not</b> derived from <c>Size / RowCount</c>: a guess
    /// about a matrix stride has no symptom until the transform comes out
    /// transposed. <c>0</c> when Slang gives nothing to derive it from.
    /// </remarks>
    public uint MatrixStride { get; init; }

    /// <summary>
    /// Elements in an array member; <c>0</c> when <see cref="IsUnsized"/> or
    /// when the member is not an array.
    /// </summary>
    /// <remarks>
    /// <b>An array member is a leaf.</b> Element <c>i</c> starts at
    /// <see cref="Offset"/> <c>+ i *</c> <see cref="ElementStride"/>; per-element
    /// paths like <c>Lights[0].Color</c> are not generated, and the element
    /// struct's own members are not described. Use
    /// <c>SlangReflection.ToJson()</c> until the follow-up lands.
    /// </remarks>
    public uint ElementCount { get; init; }

    /// <summary>Bytes between consecutive elements of an array member.</summary>
    public uint ElementStride { get; init; }
}
