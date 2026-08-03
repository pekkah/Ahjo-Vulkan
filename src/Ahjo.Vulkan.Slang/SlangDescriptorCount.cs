namespace Ahjo.Vulkan.Slang;

/// <summary>
/// What kind of answer Slang gave for a descriptor range's descriptor count.
/// </summary>
public enum SlangDescriptorCountKind
{
    /// <summary>
    /// Slang stated a descriptor count, and
    /// <see cref="SlangDescriptorCount.Value"/> is it.
    /// </summary>
    Fixed,

    /// <summary>
    /// An unsized (bindless) array: <c>SLANG_UNBOUNDED_SIZE</c>
    /// (<c>slang.h:2361</c>, <c>~size_t(0)</c>, which reaches C# as <c>-1</c>
    /// through the <see cref="long"/>-returning binding). The shader declares
    /// no capacity, so neither does reflection — see
    /// <c>SlangVulkanMapping.MapBinding(SlangDescriptorBinding, uint)</c>.
    /// </summary>
    Unbounded,

    /// <summary>
    /// The count depends on unresolved generic parameters or link-time
    /// constants: <c>SLANG_UNKNOWN_SIZE</c> (<c>slang.h:2362</c>,
    /// <c>SLANG_UNBOUNDED_SIZE - 1</c>, which reaches C# as <c>-2</c>).
    /// </summary>
    /// <remarks>
    /// <b>No fixture in this repository produces this</b>; it is mapped from
    /// the documented sentinel value and is not covered by a test. The
    /// <see cref="Unbounded"/> mapping <em>is</em> measured — see
    /// <c>Reflection_UnboundedArray_ReportsBindingInsteadOfThrowing</c>.
    /// </remarks>
    Unknown,
}

/// <summary>
/// A descriptor range's descriptor count, or the reason there is no number to
/// read — <b>an option, not a <see cref="uint"/></b>.
/// </summary>
/// <remarks>
/// <para>An unbounded (bindless) array has no descriptor count Slang can state,
/// and no <see cref="uint"/> is safe to stand in for one: <c>0</c> is
/// normalized to <c>1</c> by <c>Ahjo.Vulkan</c>'s descriptor-set-layout build
/// path (<c>DescriptorBinding</c>, issue #119), and <c>uint.MaxValue</c> crashes
/// the driver inside <c>vkCreateDescriptorSetLayout</c>. So the only ways to get
/// a number out of this type are one that throws
/// (<see cref="Value"/>) and one that forces a branch
/// (<see cref="TryGetValue"/>).</para>
/// <para><c>default(SlangDescriptorCount)</c> is <c>Fixed(0)</c> — no
/// descriptors, which is what <see cref="IsZero"/> names and what
/// <c>SlangVulkanMapping</c> omits from a layout rather than emitting. The type
/// callers actually construct, <see cref="SlangDescriptorBinding"/>, supplies
/// <see cref="Fixed(uint)">Fixed(1)</see> from its parameterless constructor, so
/// the valid-by-default rule (issue #119) still holds there.</para>
/// </remarks>
public readonly record struct SlangDescriptorCount
{
    private readonly uint _value;

    private SlangDescriptorCount(SlangDescriptorCountKind kind, uint value)
    {
        Kind = kind;
        _value = value;
    }

    /// <summary>
    /// The count of an unsized (bindless) array — <c>SLANG_UNBOUNDED_SIZE</c>.
    /// </summary>
    public static SlangDescriptorCount Unbounded { get; } = new(SlangDescriptorCountKind.Unbounded, 0);

    /// <summary>
    /// A count that depends on unresolved generic parameters or link-time
    /// constants — <c>SLANG_UNKNOWN_SIZE</c>.
    /// </summary>
    public static SlangDescriptorCount Unknown { get; } = new(SlangDescriptorCountKind.Unknown, 0);

    /// <summary>Which of the three answers this is.</summary>
    public SlangDescriptorCountKind Kind { get; }

    /// <summary>
    /// The descriptor count. Only readable when <see cref="Kind"/> is
    /// <see cref="SlangDescriptorCountKind.Fixed"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <see cref="Kind"/> is not <see cref="SlangDescriptorCountKind.Fixed"/>.
    /// </exception>
    public uint Value => Kind == SlangDescriptorCountKind.Fixed
        ? _value
        : throw new InvalidOperationException(
            $"This binding's descriptor count is {Kind}: Slang reports no count for it. Use TryGetValue, or "
            + "supply a capacity through SlangVulkanMapping.MapBinding(binding, descriptorCount).");

    /// <summary>
    /// <see langword="true"/> when this is an unsized (bindless) array.
    /// </summary>
    public bool IsUnbounded => Kind == SlangDescriptorCountKind.Unbounded;

    /// <summary>
    /// <see langword="true"/> when Slang stated a count and that count is
    /// <c>0</c> — a zero-length resource array (<c>Texture2D gTex[0]</c>, or
    /// <c>gTex[N]</c> with <c>N = 0</c>).
    /// </summary>
    /// <remarks>
    /// <para>Slang reserves a binding number for such a declaration and emits no
    /// SPIR-V variable for it, and no shader code can index it, so it is not a
    /// Vulkan descriptor binding: <c>SlangVulkanMapping.MapBindings</c> omits it
    /// from the layout and <c>MapBinding</c> refuses it. Measured on
    /// <c>v2026.14.1</c> / win-x64 — issue #183.</para>
    /// <para><c>default(SlangDescriptorCount)</c> satisfies this, which is why
    /// <see cref="SlangDescriptorBinding"/>'s parameterless constructor supplies
    /// <see cref="Fixed(uint)">Fixed(1)</see> instead (issue #119).</para>
    /// </remarks>
    public bool IsZero => Kind == SlangDescriptorCountKind.Fixed && _value == 0;

    /// <summary>A stated descriptor count.</summary>
    public static SlangDescriptorCount Fixed(uint count) => new(SlangDescriptorCountKind.Fixed, count);

    /// <summary>
    /// Reads the descriptor count, or returns <see langword="false"/> when
    /// Slang stated none.
    /// </summary>
    public bool TryGetValue(out uint count)
    {
        count = _value;

        return Kind == SlangDescriptorCountKind.Fixed;
    }
}
