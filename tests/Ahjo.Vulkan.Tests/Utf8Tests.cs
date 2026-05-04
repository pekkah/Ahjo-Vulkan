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
}
