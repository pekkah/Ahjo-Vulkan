using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Typed wrapper over <c>VkSpecializationInfo</c> driven by the field
/// layout of <typeparamref name="T"/>. Each public field of <typeparamref name="T"/>
/// (in declaration order) becomes one <c>VkSpecializationMapEntry</c>;
/// the entry's <c>constantID</c> is the field index, the <c>offset</c>
/// is the field's <c>Marshal.OffsetOf</c>, and the <c>size</c> is the
/// field type's <c>Marshal.SizeOf</c>.
/// </summary>
/// <remarks>
/// <para>The auto-derived map assumes the GLSL/HLSL spec constants use
/// <c>layout(constant_id = 0, 1, 2, …)</c> in declaration order. Order
/// the <typeparamref name="T"/> fields to match. Use
/// <c>[StructLayout(LayoutKind.Sequential)]</c> on <typeparamref name="T"/>
/// so the offsets line up with what the SPIR-V validator expects (the
/// default for <c>struct</c> in C# is <c>Sequential</c>; spell it out
/// when in doubt).</para>
/// <para><b>Lifetime.</b> The wrapper holds a raw pointer to the
/// caller's <typeparamref name="T"/> value, so the value must remain
/// alive (and pinned, if it lives in a managed object) until the
/// pipeline build that consumes the wrapper completes. The dominant
/// usage — <c>SpecializationInfo.For&lt;T&gt;(in localValue)</c> chained
/// directly into <c>WithSpecialization(...).Build()</c> on the same
/// stack frame — satisfies this trivially. The compiler does not
/// enforce the contract because the pointer-backed design sidesteps
/// C#'s ref-safety analyzer; abide by the contract and the build is
/// safe.</para>
/// <para><b>Allocation.</b> The <c>VkSpecializationMapEntry[]</c> is
/// computed once per <typeparamref name="T"/> via reflection and cached
/// in a static field. Subsequent <see cref="SpecializationInfo.For{T}"/>
/// calls hand out the same array — steady-state pipeline build is
/// allocation-free on the wrapper side.</para>
/// </remarks>
public readonly unsafe struct SpecializationInfo<T> where T : unmanaged
{
    internal readonly void*                      DataPtr;
    internal readonly VkSpecializationMapEntry[] Entries;

    internal SpecializationInfo(void* dataPtr, VkSpecializationMapEntry[] entries)
    {
        DataPtr = dataPtr;
        Entries = entries;
    }

    internal int DataSize => sizeof(T);

    /// <summary>True when no spec constants are configured.</summary>
    public bool IsEmpty => Entries is null || Entries.Length == 0;
}

/// <summary>Factory entry-point for <see cref="SpecializationInfo{T}"/>.</summary>
public static class SpecializationInfo
{
    /// <summary>
    /// Builds a <see cref="SpecializationInfo{T}"/> over <paramref name="values"/>.
    /// The map entries are derived from <typeparamref name="T"/>'s public field
    /// layout and cached per type — see the type-level remarks.
    /// </summary>
    /// <remarks>
    /// The wrapper stores a raw pointer to <paramref name="values"/>.
    /// <paramref name="values"/> must outlive the pipeline build that
    /// consumes the wrapper; the chained
    /// <c>device.BuildComputePipeline().WithSpecialization(SpecializationInfo.For(in v)).Build()</c>
    /// idiom satisfies this on a single stack frame.
    /// </remarks>
    public static unsafe SpecializationInfo<T> For<T>(in T values) where T : unmanaged
        => new(
            Unsafe.AsPointer(ref Unsafe.AsRef(in values)),
            SpecializationLayout<T>.Entries);
}

/// <summary>
/// Per-<typeparamref name="T"/> map-entry cache. The entries array is built
/// once on first access and reused for every <see cref="SpecializationInfo.For{T}"/>
/// call thereafter.
/// </summary>
internal static class SpecializationLayout<T> where T : unmanaged
{
    public static readonly VkSpecializationMapEntry[] Entries = Build();

    private static VkSpecializationMapEntry[] Build()
    {
        FieldInfo[] fields = typeof(T).GetFields(
            BindingFlags.Public | BindingFlags.Instance);
        // GetFields() does not guarantee declaration order; the metadata
        // token does. For Sequential layout (the dominant case, and the
        // one the wrapper documents) the token order matches the declared
        // field order, which is what the spec-constant IDs key off.
        Array.Sort(fields, static (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));

        var entries = new VkSpecializationMapEntry[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo f = fields[i];
            entries[i] = new VkSpecializationMapEntry
            {
                constantID = (uint)i,
                offset     = (uint)Marshal.OffsetOf<T>(f.Name),
                size       = (nuint)Marshal.SizeOf(f.FieldType),
            };
        }
        return entries;
    }
}
