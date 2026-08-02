using Ahjo.Vulkan.Slang.Native;

namespace Ahjo.Vulkan.Slang;

/// <summary>
/// What is inside one buffer: its byte size and its members, at the offsets
/// Slang baked into the SPIR-V it emitted for this program's target.
/// </summary>
/// <remarks>
/// <para><b>1. Pre-order, flattened, dotted paths, struct nodes retained.</b>
/// <see cref="Members"/> is a depth-first pre-order flattening of the buffer's
/// element struct in declaration order. A struct-typed member appears as its
/// own entry — <c>Kind == SLANG_TYPE_KIND_STRUCT</c>, with its own offset, size
/// and alignment — immediately followed by its fields, whose
/// <see cref="SlangBufferMember.Name"/> is the parent's path plus <c>"."</c>
/// plus the field name. Filter <c>Kind != SLANG_TYPE_KIND_STRUCT</c> for the
/// leaves.</para>
/// <para><b>2. The list is losslessly a tree.</b> A member's parent is
/// <c>Members[ParentIndex]</c>, or the buffer root when
/// <c>ParentIndex &lt; 0</c>; its children are the entries whose
/// <c>ParentIndex</c> is its own index, and pre-order makes them contiguous and
/// in declaration order. So a caller that wants a tree builds one in O(n) with
/// no string parsing:</para>
/// <code>
/// var children = new List&lt;int&gt;[layout.Members.Length];
/// for (int i = 0; i &lt; layout.Members.Length; i++) children[i] = [];
/// for (int i = 0; i &lt; layout.Members.Length; i++)
///     if (layout.Members[i].ParentIndex &gt;= 0) children[layout.Members[i].ParentIndex].Add(i);
/// </code>
/// <para>That is why there is one representation here rather than a flat list
/// and a parallel nested type: a second one would carry no information this one
/// lacks, and two representations can only diverge.</para>
/// <para><b>3. Arrays are leaves.</b> An array member reports
/// <see cref="SlangBufferMember.ElementCount"/> and
/// <see cref="SlangBufferMember.ElementStride"/>; element <c>i</c> is at
/// <c>Offset + i * ElementStride</c>. Per-element paths are not generated, and
/// for an array of structs the element's own members are not described — use
/// <c>SlangReflection.ToJson()</c> until the follow-up lands. Expanding N
/// elements would multiply the list by N for offsets a caller can compute, and
/// recursing into the element struct would introduce members whose offset is
/// element-relative while every other member's is buffer-relative.</para>
/// <para><b>4. Resources are not members.</b> A field whose <c>UNIFORM</c> size
/// is <c>0</c> — a <c>Texture2D</c>, a <c>SamplerState</c>, a struct of those —
/// occupies no bytes in the buffer and is omitted. Listing it at offset 0 with
/// size 0 would read as writable and is not.</para>
/// </remarks>
public sealed class SlangBufferLayout
{
    private readonly SlangBufferMember[] _members;

    internal SlangBufferLayout(string name, uint size, SlangBufferMember[] members)
    {
        Name = name;
        Size = size;
        _members = members;
    }

    /// <summary>
    /// The declaring parameter's name — the <c>ConstantBuffer</c>'s, the
    /// <c>ParameterBlock</c>'s or the push-constant block's — or
    /// <see cref="string.Empty"/> when Slang reports none.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The byte size, under <c>SLANG_PARAMETER_CATEGORY_UNIFORM</c>, of
    /// <b>what this layout describes</b>: the whole buffer for a constant
    /// buffer, a <c>ParameterBlock</c>'s uniform buffer or a push-constant
    /// block — but <b>one element</b> for a structured buffer, whose total size
    /// is a runtime property of the allocation and not a shader fact.
    /// </summary>
    /// <remarks>
    /// Measured: <c>RWStructuredBuffer&lt;float4&gt;</c> reports <c>16</c> and a
    /// <c>StructuredBuffer&lt;Payload&gt;</c> of two <c>float4</c>s reports
    /// <c>32</c>. <c>0</c> when Slang reports a sentinel because the buffer ends
    /// in a runtime-sized array; the members are exact either way.
    /// </remarks>
    public uint Size { get; }

    /// <summary>The members, pre-order and in declaration order.</summary>
    public ReadOnlySpan<SlangBufferMember> Members => _members;

    /// <summary>
    /// Finds a member by its dotted path — <c>"Params.UvScale"</c>. Ordinal
    /// comparison; a leaf's short name does not match.
    /// </summary>
    /// <remarks>
    /// A linear scan, like <c>SlangReflection.TryGetSet</c>: this is setup-time
    /// and member counts are single to low double digits.
    /// </remarks>
    public bool TryGetMember(string path, out SlangBufferMember member)
    {
        ArgumentNullException.ThrowIfNull(path);

        for (int i = 0; i < _members.Length; i++)
        {
            if (string.Equals(_members[i].Name, path, StringComparison.Ordinal))
            {
                member = _members[i];

                return true;
            }
        }

        member = default;

        return false;
    }
}
