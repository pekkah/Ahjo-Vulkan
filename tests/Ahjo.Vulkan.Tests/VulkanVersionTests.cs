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
}
