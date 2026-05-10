using Xunit;

namespace Ahjo.Vulkan.Tests;

public sealed class VulkanVersionTests
{
    [Fact]
    public void Make_PacksMajorMinorPatch()
    {
        var v = VulkanVersion.Make(1, 4, 7);
        Assert.Equal(1u, v.Major);
        Assert.Equal(4u, v.Minor);
        Assert.Equal(7u, v.Patch);
    }

    [Fact]
    public void V1_4_HasExpectedPackedValue()
    {
        // VK_MAKE_API_VERSION(0,1,4,0) = (1<<22) | (4<<12) = 0x00404000.
        Assert.Equal(0x00404000u, (uint)VulkanVersion.V1_4);
    }

    [Fact]
    public void ImplicitOperatorUint_ReturnsPacked()
    {
        VulkanVersion v = VulkanVersion.Make(1, 2, 3);
        uint packed = v;
        Assert.Equal(v.Packed, packed);
    }

    [Fact]
    public void Variant_RoundTrips()
    {
        // Khronos-default Make values have variant=0.
        Assert.Equal(0u, VulkanVersion.Make(1, 4, 0).Variant);

        // A raw-packed value with the top 3 bits set must surface variant=7
        // and leave Major/Minor/Patch unaffected (the variant field doesn't
        // overlap them).
        var raw = new VulkanVersion((7u << 29) | (1u << 22) | (4u << 12) | 7u);
        Assert.Equal(7u, raw.Variant);
        Assert.Equal(1u, raw.Major);
        Assert.Equal(4u, raw.Minor);
        Assert.Equal(7u, raw.Patch);
    }
}
