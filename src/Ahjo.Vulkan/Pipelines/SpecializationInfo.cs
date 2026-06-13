using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan;

/// <summary>
/// Typed wrapper over <c>VkSpecializationInfo</c> driven by the field
/// layout of <typeparamref name="T"/>. Each public field of <typeparamref name="T"/>
/// (in declaration order) becomes one <c>VkSpecializationMapEntry</c>;
/// the entry's <c>constantID</c> is the field index, the <c>offset</c>
/// is the field's position in the natural-alignment managed layout (the
/// same layout the wrapper hands Vulkan via <c>pData</c>), and the
/// <c>size</c> is the field's exact primitive size — never the padded
/// gap to the next field.
/// </summary>
/// <remarks>
/// <para>The auto-derived map assumes the GLSL/HLSL spec constants use
/// <c>layout(constant_id = 0, 1, 2, …)</c> in declaration order. Order
/// the <typeparamref name="T"/> fields to match. Use
/// <c>[StructLayout(LayoutKind.Sequential)]</c> on <typeparamref name="T"/>
/// with default packing (the default for <c>struct</c> in C#; spell it
/// out when in doubt) so the modeled offsets match the real managed
/// layout that <c>pData</c> points at.</para>
/// <para><b>Supported field types.</b> Each field must be a blittable
/// primitive whose managed and Vulkan layouts coincide:
/// <c>byte</c>, <c>sbyte</c>, <c>short</c>, <c>ushort</c>, <c>int</c>,
/// <c>uint</c>, <c>long</c>, <c>ulong</c>, <c>float</c>, <c>double</c>.
/// <c>bool</c> is rejected — it is 1 byte in the managed layout but
/// Vulkan boolean spec constants are <c>VkBool32</c> (4 bytes); use
/// <c>uint</c>/<c>VkBool32</c> (1 = true, 0 = false) instead. <c>char</c>,
/// <c>nint</c>/<c>nuint</c>, <c>decimal</c>, enums, nested structs, and
/// fixed buffers are likewise rejected. An unsupported field type, or a
/// layout that cannot be modeled by natural-alignment sequential packing
/// (custom <c>Pack</c>, <c>LayoutKind.Explicit</c>/<c>Auto</c>), throws a
/// <c>NotSupportedException</c> on first use of that <typeparamref name="T"/>.</para>
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
    public static unsafe SpecializationInfo<T> For<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>(in T values)
        where T : unmanaged
        => new(
            Unsafe.AsPointer(ref Unsafe.AsRef(in values)),
            SpecializationLayout<T>.Entries);
}

/// <summary>
/// Per-<typeparamref name="T"/> map-entry cache. The entries array is built
/// once on first access and reused for every <see cref="SpecializationInfo.For{T}"/>
/// call thereafter.
/// </summary>
internal static class SpecializationLayout<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>
    where T : unmanaged
{
    public static readonly VkSpecializationMapEntry[] Entries = Build();

    private static VkSpecializationMapEntry[] Build()
    {
        FieldInfo[] fields = typeof(T).GetFields(
            BindingFlags.Public | BindingFlags.Instance);
        if (fields.Length == 0)
            return Array.Empty<VkSpecializationMapEntry>();

        // The offset model below assumes the CLR's default sequential layout
        // (natural-alignment packing). Reject Explicit/Auto outright: an
        // Explicit struct can reorder fields while preserving the total size,
        // which the size sanity-check below would NOT catch — it would emit
        // offsets pointing at the wrong bytes of pData. IsLayoutSequential is
        // a TypeAttributes flag read (AOT-safe; no reflection over attributes).
        if (!typeof(T).IsLayoutSequential)
            throw new NotSupportedException(
                $"SpecializationInfo<{typeof(T).Name}>: T must use the default " +
                "[StructLayout(LayoutKind.Sequential)] layout. Explicit/Auto layouts are not " +
                "supported — spec-constant field offsets are derived from natural-alignment " +
                "sequential packing over the managed layout.");

        // GetFields() does not guarantee declaration order; the metadata
        // token does. For Sequential layout (the dominant case, and the
        // one the wrapper documents) the token order matches the declared
        // field order, which is what the spec-constant IDs key off.
        Array.Sort(fields, static (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));

        // Model the MANAGED layout that pData actually points at:
        // natural-alignment sequential packing over each field's exact
        // primitive size. Sizes come from a typeof switch — no Marshal
        // (Marshal.OffsetOf/SizeOf is [RequiresDynamicCode] and indexes
        // the *marshalled* layout, which diverges from pData for bool/char)
        // and no MakeGenericMethod, so this stays Native-AOT clean.
        var entries = new VkSpecializationMapEntry[fields.Length];
        uint running  = 0;
        uint maxAlign = 1;
        for (int i = 0; i < fields.Length; i++)
        {
            uint size   = FieldSize(fields[i]);
            uint align  = size; // natural alignment == size for these primitives
            uint offset = AlignUp(running, align);
            running     = offset + size;
            maxAlign    = Math.Max(maxAlign, align);

            entries[i] = new VkSpecializationMapEntry
            {
                constantID = (uint)i,
                offset     = offset,
                size       = (nuint)size,
            };
        }

        // Sanity-check the model against the real managed size. AlignUp by
        // maxAlign accounts for trailing padding (e.g. { double; int } is
        // 16 bytes while running stops at 12). A mismatch means the layout
        // could not be modeled — custom Pack, LayoutKind.Explicit/Auto, or
        // an unsupported field.
        if (AlignUp(running, maxAlign) != (uint)Unsafe.SizeOf<T>())
        {
            throw new NotSupportedException(
                $"SpecializationInfo<{typeof(T).Name}>: the computed natural-alignment " +
                $"layout ({AlignUp(running, maxAlign)} bytes) does not match the actual " +
                $"managed size ({(uint)Unsafe.SizeOf<T>()} bytes). The struct's layout " +
                "could not be modeled — it likely uses a custom Pack, " +
                "LayoutKind.Explicit/Auto, or an unsupported field. T must be " +
                "[StructLayout(LayoutKind.Sequential)] (the default) with default packing " +
                "and only the supported primitive fields (byte, sbyte, short, ushort, int, " +
                "uint, long, ulong, float, double).");
        }

        return entries;
    }

    private static uint FieldSize(FieldInfo f)
    {
        Type t = f.FieldType;
        if (t == typeof(byte) || t == typeof(sbyte)) return 1;
        if (t == typeof(short) || t == typeof(ushort)) return 2;
        if (t == typeof(int) || t == typeof(uint) || t == typeof(float)) return 4;
        if (t == typeof(long) || t == typeof(ulong) || t == typeof(double)) return 8;

        if (t == typeof(bool))
        {
            throw new NotSupportedException(
                $"SpecializationInfo<{typeof(T).Name}>: field '{f.Name}' is a System.Boolean, " +
                "which is 1 byte in the managed layout but Vulkan boolean spec constants are " +
                "VkBool32 (4 bytes). Use uint/VkBool32 (1 = true, 0 = false) instead.");
        }

        throw new NotSupportedException(
            $"SpecializationInfo<{typeof(T).Name}>: field '{f.Name}' has unsupported type " +
            $"'{f.FieldType}'. Spec-constant fields must be a blittable primitive (byte, sbyte, " +
            "short, ushort, int, uint, long, ulong, float, double) whose managed and Vulkan " +
            "layouts coincide.");
    }

    private static uint AlignUp(uint value, uint align) => (value + (align - 1)) & ~(align - 1);
}
