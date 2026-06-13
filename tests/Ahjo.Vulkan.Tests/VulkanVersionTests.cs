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
    public void Make_RejectsOutOfRangeFields()
    {
        // Each field has a fixed bit-width; an over-range value would bleed
        // into the next field, so Make throws unconditionally (setup-time).
        Assert.Throws<ArgumentOutOfRangeException>(() => VulkanVersion.Make(major: 200, 0, 0));   // major > 0x7F
        Assert.Throws<ArgumentOutOfRangeException>(() => VulkanVersion.Make(0, minor: 1024, 0));  // minor > 0x3FF
        Assert.Throws<ArgumentOutOfRangeException>(() => VulkanVersion.Make(0, 0, patch: 4096));  // patch > 0xFFF
    }

    [Fact]
    public void Make_AtFieldMaxima_RoundTrips()
    {
        // Exactly-at-max values (the inclusive upper bound the guards allow)
        // must pack and round-trip — the rejection test only covers
        // one-past-max. Also confirms major's top bit doesn't bleed into the
        // variant field.
        var v = VulkanVersion.Make(0x7F, 0x3FF, 0xFFF);
        Assert.Equal(0x7Fu,  v.Major);
        Assert.Equal(0x3FFu, v.Minor);
        Assert.Equal(0xFFFu, v.Patch);
        Assert.Equal(0u,     v.Variant);
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
