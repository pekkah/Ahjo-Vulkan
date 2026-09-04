using System.Text;
using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Ngx.Tests;

/// <summary>
/// <see cref="NgxExtensionSet"/> copies NGX's extension names into storage it
/// owns, because NGX's own array has no documented lifetime and
/// <see cref="Utf8Name"/> requires stable, non-movable storage (spec E8).
/// </summary>
/// <remarks>
/// <para>Drives the internal <c>FromProperties</c> seam with a fabricated
/// <c>VkExtensionProperties[]</c>, so the copy-and-terminate contract is
/// provable with no NGX, no NVIDIA driver and no Vulkan loader — which is
/// exactly what CI has.</para>
/// <para>The terminator assertion is the direct regression test for the class
/// of bug PR #217 fixed on the shim side: a source span that stops at
/// <c>Length</c> carries no terminator of its own, and a <c>const char*</c>
/// without one runs off the end of the allocation.</para>
/// </remarks>
public sealed unsafe class NgxExtensionSetTests
{
    [Fact]
    public void TwoNames_RoundTripByteForByte()
    {
        using NgxExtensionSet set = NgxExtensionSet.FromProperties(
            Fabricate("VK_KHR_get_physical_device_properties2", "VK_NVX_binary_import"));

        Assert.Equal(2, set.Count);
        Assert.Equal(2, set.Names.Length);
        Assert.Equal("VK_KHR_get_physical_device_properties2", ReadBack(set.Names[0]));
        Assert.Equal("VK_NVX_binary_import", ReadBack(set.Names[1]));
    }

    [Fact]
    public void EveryNameIsNulTerminatedExactlyAtItsLength()
    {
        string[] names = ["VK_KHR_swapchain", "VK_EXT_memory_budget"];
        using NgxExtensionSet set = NgxExtensionSet.FromProperties(Fabricate(names));

        for (int i = 0; i < names.Length; i++)
        {
            sbyte* p = set.Names[i].Ptr;
            Assert.False(p == null);

            int expected = Encoding.UTF8.GetByteCount(names[i]);
            for (int b = 0; b < expected; b++)
                Assert.NotEqual(0, p[b]);

            // The terminator sits exactly one past the last byte — not
            // somewhere later by luck, and not missing.
            Assert.Equal(0, p[expected]);
        }
    }

    [Fact]
    public void MaximumLengthName_Survives()
    {
        // VkExtensionProperties.extensionName is char[256], so 255 characters
        // plus a terminator is the longest name that can arrive terminated.
        string longest = new('a', 255);
        using NgxExtensionSet set = NgxExtensionSet.FromProperties(Fabricate(longest));

        Assert.Equal(1, set.Count);
        Assert.Equal(longest, ReadBack(set.Names[0]));
    }

    [Fact]
    public void EmptyInput_ProducesAnEmptySet()
    {
        using NgxExtensionSet set = NgxExtensionSet.FromProperties(default);

        Assert.Equal(0, set.Count);
        Assert.True(set.Names.IsEmpty);
    }

    [Fact]
    public void DisposeTwice_DoesNotThrow()
    {
        NgxExtensionSet set = NgxExtensionSet.FromProperties(Fabricate("VK_KHR_swapchain"));
        set.Dispose();
        set.Dispose();

        Assert.Equal(0, set.Count);
    }

    /// <summary>
    /// Builds the array NGX would have written: each name UTF-8 encoded into a
    /// zero-filled <c>char[256]</c>, so the copy path sees a genuinely
    /// terminated field.
    /// </summary>
    private static VkExtensionProperties[] Fabricate(params string[] names)
    {
        var properties = new VkExtensionProperties[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            Span<sbyte> field = properties[i].extensionName;
            Span<byte>  bytes = System.Runtime.InteropServices.MemoryMarshal.Cast<sbyte, byte>(field);
            bytes.Clear();
            int written = Encoding.UTF8.GetBytes(names[i], bytes);
            Assert.True(written < bytes.Length, "fixture name does not fit in char[256]");
            properties[i].specVersion = 1;
        }
        return properties;
    }

    private static string ReadBack(Utf8Name name)
    {
        int length = 0;
        while (name.Ptr[length] != 0) length++;
        return Encoding.UTF8.GetString((byte*)name.Ptr, length);
    }
}
