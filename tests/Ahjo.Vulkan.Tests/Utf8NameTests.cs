using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed unsafe class Utf8NameTests
{
    [Fact]
    public void FromLiteral_NonEmpty_ReturnsNonNullPointer()
    {
        var name = Utf8Name.FromLiteral("VK_KHR_surface"u8);
        Assert.False(name.IsNull);
        Assert.Equal("VK_KHR_surface", Utf8.ToString(name.Ptr));
    }

    [Fact]
    public void Default_IsNull()
    {
        Utf8Name name = default;
        Assert.True(name.IsNull);
    }
}
