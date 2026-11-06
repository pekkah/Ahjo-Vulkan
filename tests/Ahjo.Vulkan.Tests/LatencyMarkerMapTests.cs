using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Issue #261 spec D7: the Vulkan and PCL marker numberings differ from 8
/// upward, so the forward map is an explicit table and both enums are pinned
/// to their wire values. Driverless.
/// </summary>
public sealed class LatencyMarkerMapTests
{
    [Theory]
    [InlineData(LatencyMarker.SimulationStart,       PclMarker.SimulationStart)]
    [InlineData(LatencyMarker.SimulationEnd,         PclMarker.SimulationEnd)]
    [InlineData(LatencyMarker.RenderSubmitStart,     PclMarker.RenderSubmitStart)]
    [InlineData(LatencyMarker.RenderSubmitEnd,       PclMarker.RenderSubmitEnd)]
    [InlineData(LatencyMarker.PresentStart,          PclMarker.PresentStart)]
    [InlineData(LatencyMarker.PresentEnd,            PclMarker.PresentEnd)]
    [InlineData(LatencyMarker.TriggerFlash,          PclMarker.TriggerFlash)]
    [InlineData(LatencyMarker.OutOfBandPresentStart, PclMarker.OutOfBandPresentStart)]
    [InlineData(LatencyMarker.OutOfBandPresentEnd,   PclMarker.OutOfBandPresentEnd)]
    public void ToPcl_MatchesSpecTable(LatencyMarker vk, PclMarker pcl)
    {
        Assert.Equal(pcl, LatencyMarkerMap.ToPcl(vk));
    }

    [Theory]
    [InlineData(LatencyMarker.SimulationStart,       false, true)]
    [InlineData(LatencyMarker.SimulationStart,       true,  true)]
    [InlineData(LatencyMarker.SimulationEnd,         false, true)]
    [InlineData(LatencyMarker.SimulationEnd,         true,  true)]
    [InlineData(LatencyMarker.RenderSubmitStart,     false, true)]
    [InlineData(LatencyMarker.RenderSubmitStart,     true,  true)]
    [InlineData(LatencyMarker.RenderSubmitEnd,       false, true)]
    [InlineData(LatencyMarker.RenderSubmitEnd,       true,  true)]
    [InlineData(LatencyMarker.PresentStart,          false, true)]
    [InlineData(LatencyMarker.PresentStart,          true,  true)]
    [InlineData(LatencyMarker.PresentEnd,            false, true)]
    [InlineData(LatencyMarker.PresentEnd,            true,  true)]
    [InlineData(LatencyMarker.TriggerFlash,          false, true)]
    [InlineData(LatencyMarker.TriggerFlash,          true,  true)]
    [InlineData(LatencyMarker.OutOfBandPresentStart, false, true)]
    [InlineData(LatencyMarker.OutOfBandPresentStart, true,  false)]
    [InlineData(LatencyMarker.OutOfBandPresentEnd,   false, true)]
    [InlineData(LatencyMarker.OutOfBandPresentEnd,   true,  false)]
    public void ForwardsToPcl_SuppressesOnlyOobPresentOnLowLatency(LatencyMarker marker, bool lowLatency, bool expected)
    {
        // Spec E6/D7: on an opted-in swapchain the driver writes PCL 11/12.
        Assert.Equal(expected, LatencyMarkerMap.ForwardsToPcl(marker, lowLatency));
    }

    [Fact]
    public void ToPcl_OutOfBandIsOffByOne()
    {
        Assert.Equal(10u, (uint)LatencyMarker.OutOfBandPresentStart);
        Assert.Equal(11u, (uint)LatencyMarkerMap.ToPcl(LatencyMarker.OutOfBandPresentStart));
        Assert.Equal(11u, (uint)LatencyMarker.OutOfBandPresentEnd);
        Assert.Equal(12u, (uint)LatencyMarkerMap.ToPcl(LatencyMarker.OutOfBandPresentEnd));
    }

    [Theory]
    [InlineData(LatencyMarker.SimulationStart,       VkLatencyMarkerNV.VK_LATENCY_MARKER_SIMULATION_START_NV)]
    [InlineData(LatencyMarker.SimulationEnd,         VkLatencyMarkerNV.VK_LATENCY_MARKER_SIMULATION_END_NV)]
    [InlineData(LatencyMarker.RenderSubmitStart,     VkLatencyMarkerNV.VK_LATENCY_MARKER_RENDERSUBMIT_START_NV)]
    [InlineData(LatencyMarker.RenderSubmitEnd,       VkLatencyMarkerNV.VK_LATENCY_MARKER_RENDERSUBMIT_END_NV)]
    [InlineData(LatencyMarker.PresentStart,          VkLatencyMarkerNV.VK_LATENCY_MARKER_PRESENT_START_NV)]
    [InlineData(LatencyMarker.PresentEnd,            VkLatencyMarkerNV.VK_LATENCY_MARKER_PRESENT_END_NV)]
    [InlineData(LatencyMarker.TriggerFlash,          VkLatencyMarkerNV.VK_LATENCY_MARKER_TRIGGER_FLASH_NV)]
    [InlineData(LatencyMarker.OutOfBandPresentStart, VkLatencyMarkerNV.VK_LATENCY_MARKER_OUT_OF_BAND_PRESENT_START_NV)]
    [InlineData(LatencyMarker.OutOfBandPresentEnd,   VkLatencyMarkerNV.VK_LATENCY_MARKER_OUT_OF_BAND_PRESENT_END_NV)]
    public void ToVk_EqualsVkLatencyMarkerNV(LatencyMarker marker, VkLatencyMarkerNV expected)
    {
        Assert.Equal((uint)expected, (uint)marker);
        Assert.Equal(expected, LatencyMarkerMap.ToVk(marker));
    }

    [Fact]
    public void PclMarker_ValuesPinnedToSlPcl()
    {
        // NVIDIA Streamline include/sl_pcl.h:62-74 (MIT), tag v2.14.16.
        Assert.Equal(0u,  (uint)PclMarker.SimulationStart);
        Assert.Equal(1u,  (uint)PclMarker.SimulationEnd);
        Assert.Equal(2u,  (uint)PclMarker.RenderSubmitStart);
        Assert.Equal(3u,  (uint)PclMarker.RenderSubmitEnd);
        Assert.Equal(4u,  (uint)PclMarker.PresentStart);
        Assert.Equal(5u,  (uint)PclMarker.PresentEnd);
        Assert.Equal(7u,  (uint)PclMarker.TriggerFlash);
        Assert.Equal(8u,  (uint)PclMarker.PcLatencyPing);
        Assert.Equal(11u, (uint)PclMarker.OutOfBandPresentStart);
        Assert.Equal(12u, (uint)PclMarker.OutOfBandPresentEnd);
    }

    [Fact]
    public void Enum_IsExhaustive()
    {
        // A value added to LatencyMarker without a ToPcl arm (and an
        // InlineData row above) fails here.
        Assert.Equal(9, Enum.GetValues<LatencyMarker>().Length);
        Assert.Equal(10, Enum.GetValues<PclMarker>().Length);
    }

    [Fact]
    public void ToPcl_UndefinedValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LatencyMarkerMap.ToPcl((LatencyMarker)6));
    }
}
