using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed unsafe class Utf8Tests
{
    [Fact]
    public void ToString_NullPointer_ReturnsNull()
    {
        Assert.Null(Utf8.ToString((sbyte*)null));
    }

    [Fact]
    public void ToString_RoundTripsAsciiLiteral()
    {
        ReadOnlySpan<byte> literal = "VK_KHR_surface"u8;
        sbyte* p = (sbyte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(literal));
        Assert.Equal("VK_KHR_surface", Utf8.ToString(p));
    }

    // ---- FromBounded: the bounded char[N] decode used by the device-fault read ----

    private static ReadOnlySpan<sbyte> AsSBytes(byte[] bytes) =>
        MemoryMarshal.Cast<byte, sbyte>(bytes);

    [Fact]
    public void FromBounded_StopsAtFirstNul()
    {
        byte[] bytes = "abc\0def"u8.ToArray();
        Assert.Equal("abc", Utf8.FromBounded(AsSBytes(bytes)));
    }

    [Fact]
    public void FromBounded_NoNul_UsesWholeBuffer()
    {
        byte[] bytes = new byte[256];
        Array.Fill(bytes, (byte)'A');
        string decoded = Utf8.FromBounded(AsSBytes(bytes));
        Assert.Equal(256, decoded.Length);
        Assert.Equal(new string('A', 256), decoded);
    }

    [Fact]
    public void FromBounded_MultiByte()
    {
        byte[] bytes = [.. "Störung ✓"u8, 0, (byte)'x'];
        Assert.Equal("Störung ✓", Utf8.FromBounded(AsSBytes(bytes)));
    }

    [Fact]
    public void FromBounded_InvalidUtf8_DoesNotThrow()
    {
        byte[] bytes = [(byte)'a', 0xC3, 0x28, 0xFF, (byte)'b', 0];
        string decoded = Utf8.FromBounded(AsSBytes(bytes));
        Assert.Contains('�', decoded);
    }

    [Fact]
    public void FromBounded_Empty()
    {
        byte[] bytes = [0, (byte)'a', (byte)'b'];
        Assert.Same(string.Empty, Utf8.FromBounded(AsSBytes(bytes)));
    }
}
