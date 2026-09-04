using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;

namespace Ahjo.Vulkan.Ngx;

/// <summary>
/// The Vulkan extension names NGX requires, copied into storage this wrapper
/// owns and exposed as <see cref="Utf8Name"/>s that drop straight into
/// <see cref="InstanceDescription.Extensions"/> /
/// <see cref="DeviceDescription.Extensions"/>.
/// </summary>
/// <remarks>
/// <para><b>Why the copy.</b> NGX documents <c>OutExtensionProperties</c> as
/// "an output pointer that will be populated with an array of
/// <c>VkExtensionProperties</c>" and says nothing about who owns that array or
/// how long it lives (<c>nvsdk_ngx_vk.h:639-649</c>, spec E8).
/// <see cref="Utf8Name.FromLiteral"/>'s contract requires storage that outlives
/// every use and cannot be moved by the GC — pointing a <see cref="Utf8Name"/>
/// into undocumented SDK storage satisfies neither provably. So each name is
/// copied into one <see cref="NativeMemory"/> block with an explicit
/// terminator, and this type owns that block.</para>
/// <para><b>Lifetime.</b> The names only have to outlive the
/// <c>vkCreateInstance</c> / <c>vkCreateDevice</c> that consumes them — Vulkan
/// copies them. Dispose it after. Using <see cref="Names"/> after
/// <see cref="Dispose"/> is undefined.</para>
/// </remarks>
public sealed unsafe class NgxExtensionSet : IDisposable
{
    private void*     _block;
    private Utf8Name[] _names;

    private NgxExtensionSet(void* block, Utf8Name[] names)
    {
        _block = block;
        _names = names;
    }

    /// <summary>
    /// Copies each entry's <c>extensionName</c>, up to its first NUL, into one
    /// owned block.
    /// </summary>
    /// <remarks>
    /// <c>internal</c> on purpose: this is the seam the test suite drives with
    /// a fabricated array, so the copy-and-terminate contract is provable on a
    /// host with no NGX, no NVIDIA driver and no Vulkan loader at all
    /// (spec D13).
    /// </remarks>
    internal static NgxExtensionSet FromProperties(ReadOnlySpan<VkExtensionProperties> properties)
    {
        const int NameCapacity = 256;   // VkExtensionProperties.extensionName is char[256]

        if (properties.Length == 0)
            return new NgxExtensionSet(null, []);

        // Two passes: measure, then copy. One allocation for the bytes, one for
        // the Utf8Name array, and no reallocation in between.
        int total = 0;
        for (int i = 0; i < properties.Length; i++)
            total += NameLength(properties[i], NameCapacity) + 1;   // + the terminator we write

        byte* block  = (byte*)NativeMemory.Alloc((nuint)total);
        var   names  = new Utf8Name[properties.Length];
        int   cursor = 0;

        for (int i = 0; i < properties.Length; i++)
        {
            int length = NameLength(properties[i], NameCapacity);
            names[i] = new Utf8Name((sbyte*)(block + cursor));

            ref readonly VkExtensionProperties props = ref properties[i];
            fixed (VkExtensionProperties* p = &props)
            {
                var source = new ReadOnlySpan<byte>(&p->extensionName, length);
                source.CopyTo(new Span<byte>(block + cursor, length));
            }

            cursor += length;
            // Explicit, always. This is the class of bug PR #217 fixed on the
            // shim side: a source span that stops at Length carries no
            // terminator of its own.
            block[cursor++] = 0;
        }

        return new NgxExtensionSet(block, names);

        static int NameLength(in VkExtensionProperties properties, int capacity)
        {
            fixed (VkExtensionProperties* p = &properties)
            {
                var span = new ReadOnlySpan<byte>(&p->extensionName, capacity);
                int nul  = span.IndexOf((byte)0);
                // An unterminated char[256] is malformed input, not a reason to
                // read past the field: clamp to the field's own capacity.
                return nul >= 0 ? nul : capacity;
            }
        }
    }

    /// <summary>
    /// The names, in the order NGX returned them. Empty after
    /// <see cref="Dispose"/>.
    /// </summary>
    public ReadOnlySpan<Utf8Name> Names => _names;

    /// <summary>How many names the set holds.</summary>
    public int Count => _names.Length;

    /// <summary>Frees the owned block. Idempotent; a second call is a no-op.</summary>
    public void Dispose()
    {
        if (_block is not null)
        {
            NativeMemory.Free(_block);
            _block = null;
        }
        _names = [];
    }
}
