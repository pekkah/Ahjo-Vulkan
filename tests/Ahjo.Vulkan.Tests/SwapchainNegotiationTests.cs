using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Driver-free unit tests over the pure negotiation helpers extracted
/// from <c>Swapchain.CreateOrRecreate</c> for issue #120 — covering the
/// #104 image-count clamp throw, the #110 zero-extent (minimized) and
/// compositeAlpha-fallback matrices.
/// </summary>
public sealed class SwapchainNegotiationTests
{
    private static VkSurfaceCapabilitiesKHR Caps(
        uint currentW, uint currentH,
        uint minImages = 2, uint maxImages = 0,
        uint minW = 1, uint minH = 1,
        uint maxW = 4096, uint maxH = 4096,
        uint supportedCompositeAlpha = (uint)VkCompositeAlphaFlagBitsKHR.VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR)
        => new()
        {
            currentExtent           = new VkExtent2D { width = currentW, height = currentH },
            minImageCount           = minImages,
            maxImageCount           = maxImages,
            minImageExtent          = new VkExtent2D { width = minW, height = minH },
            maxImageExtent          = new VkExtent2D { width = maxW, height = maxH },
            supportedCompositeAlpha = supportedCompositeAlpha,
        };

    // ---- ComputeImageCount (#104) ----

    /// <summary>
    /// The #104 repro: PreferredImageCount below minImageCount on an
    /// unlimited surface (maxImageCount == 0) made Math.Clamp throw
    /// ArgumentException; the documented behavior is to clamp up to the
    /// surface minimum.
    /// </summary>
    [Fact]
    public void ComputeImageCount_PreferredBelowMin_UnlimitedMax_ClampsUp()
    {
        var caps = Caps(800, 600, minImages: 2, maxImages: 0);
        Assert.Equal(2u, Swapchain.ComputeImageCount(in caps, preferredImageCount: 1));
    }

    [Fact]
    public void ComputeImageCount_Unset_DefaultsToMinPlusOne()
    {
        var caps = Caps(800, 600, minImages: 2, maxImages: 0);
        Assert.Equal(3u, Swapchain.ComputeImageCount(in caps, preferredImageCount: 0));
    }

    [Fact]
    public void ComputeImageCount_BoundedMax_Clamps()
    {
        var caps = Caps(800, 600, minImages: 2, maxImages: 3);
        Assert.Equal(3u, Swapchain.ComputeImageCount(in caps, preferredImageCount: 8));
    }

    [Fact]
    public void ComputeImageCount_PreferredInRange_Honored()
    {
        var caps = Caps(800, 600, minImages: 2, maxImages: 8);
        Assert.Equal(4u, Swapchain.ComputeImageCount(in caps, preferredImageCount: 4));
    }

    // ---- ComputeExtent / IsZeroExtent (#110 §1) ----

    [Fact]
    public void ComputeExtent_PinnedCurrentExtent_WinsOverDescription()
    {
        var caps = Caps(800, 600);
        var extent = Swapchain.ComputeExtent(in caps, descWidth: 1234, descHeight: 999);
        Assert.Equal(800u, extent.width);
        Assert.Equal(600u, extent.height);
    }

    /// <summary>
    /// A minimized Windows window reports currentExtent == (0, 0) — not
    /// the 0xFFFFFFFF sentinel — and the old code used it verbatim as
    /// imageExtent, violating VUID-VkSwapchainCreateInfoKHR-imageExtent-01689.
    /// </summary>
    [Fact]
    public void ComputeExtent_MinimizedWindow_ZeroCurrentExtent_IsZero()
    {
        var caps = Caps(0, 0);
        Assert.True(Swapchain.IsZeroExtent(Swapchain.ComputeExtent(in caps, 800, 600)));
    }

    [Fact]
    public void ComputeExtent_Sentinel_ClampsDescriptionIntoCapsRange()
    {
        var caps = Caps(~0u, ~0u, minW: 100, minH: 100, maxW: 1000, maxH: 1000);
        var extent = Swapchain.ComputeExtent(in caps, descWidth: 5000, descHeight: 50);
        Assert.Equal(1000u, extent.width);
        Assert.Equal(100u,  extent.height);
    }

    /// <summary>
    /// In the sentinel branch maxImageExtent can also be (0, 0) on a
    /// minimized window; the clamp then legitimately produces zero, which
    /// callers must treat as Minimized rather than create with.
    /// </summary>
    [Fact]
    public void ComputeExtent_Sentinel_ZeroMaxImageExtent_IsZero()
    {
        var caps = Caps(~0u, ~0u, minW: 0, minH: 0, maxW: 0, maxH: 0);
        Assert.True(Swapchain.IsZeroExtent(Swapchain.ComputeExtent(in caps, 800, 600)));
    }

    [Fact]
    public void ComputeExtent_Sentinel_ZeroDescription_ClampsToAtLeastOne()
    {
        var caps = Caps(~0u, ~0u, minW: 1, minH: 1, maxW: 4096, maxH: 4096);
        var extent = Swapchain.ComputeExtent(in caps, descWidth: 0, descHeight: 0);
        Assert.False(Swapchain.IsZeroExtent(extent));
    }

    // ---- PickCompositeAlpha (#110 §2) ----

    [Fact]
    public void PickCompositeAlpha_OpaqueSupported_PrefersOpaque()
    {
        uint supported = (uint)(VkCompositeAlphaFlagBitsKHR.VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR
                              | VkCompositeAlphaFlagBitsKHR.VK_COMPOSITE_ALPHA_INHERIT_BIT_KHR);
        Assert.Equal(VkCompositeAlphaFlagBitsKHR.VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR,
            Swapchain.PickCompositeAlpha(supported));
    }

    /// <summary>
    /// The Wayland/Android case from #110: only PRE_MULTIPLIED/INHERIT
    /// advertised — the old hard-coded OPAQUE violated
    /// VUID-VkSwapchainCreateInfoKHR-compositeAlpha-01280. The fallback
    /// is the lowest set bit.
    /// </summary>
    [Fact]
    public void PickCompositeAlpha_OpaqueAbsent_FallsBackToLowestSetBit()
    {
        uint supported = (uint)(VkCompositeAlphaFlagBitsKHR.VK_COMPOSITE_ALPHA_PRE_MULTIPLIED_BIT_KHR
                              | VkCompositeAlphaFlagBitsKHR.VK_COMPOSITE_ALPHA_INHERIT_BIT_KHR);
        Assert.Equal(VkCompositeAlphaFlagBitsKHR.VK_COMPOSITE_ALPHA_PRE_MULTIPLIED_BIT_KHR,
            Swapchain.PickCompositeAlpha(supported));
    }

    [Fact]
    public void PickCompositeAlpha_InheritOnly_ReturnsInherit()
    {
        uint supported = (uint)VkCompositeAlphaFlagBitsKHR.VK_COMPOSITE_ALPHA_INHERIT_BIT_KHR;
        Assert.Equal(VkCompositeAlphaFlagBitsKHR.VK_COMPOSITE_ALPHA_INHERIT_BIT_KHR,
            Swapchain.PickCompositeAlpha(supported));
    }
}
