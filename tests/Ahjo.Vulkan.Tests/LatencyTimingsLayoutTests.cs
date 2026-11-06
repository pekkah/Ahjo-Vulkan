using System.Runtime.InteropServices;
using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>
/// Issue #261: <see cref="LatencyTimings"/> must be layout-identical to
/// <c>VkLatencyTimingsFrameReportNV</c> — <see cref="Swapchain.GetLatencyTimings"/>
/// hands the caller's span to the driver by cast. Driverless.
/// </summary>
public sealed unsafe class LatencyTimingsLayoutTests
{
    [Fact]
    public void Size_MatchesNative()
    {
        Assert.Equal(sizeof(VkLatencyTimingsFrameReportNV), sizeof(LatencyTimings));
    }

    [Fact]
    public void Properties_MapToNativeFields()
    {
        Span<VkLatencyTimingsFrameReportNV> native = stackalloc VkLatencyTimingsFrameReportNV[1];
        native[0] = new VkLatencyTimingsFrameReportNV
        {
            presentID                = 1,
            inputSampleTimeUs        = 2,
            simStartTimeUs           = 3,
            simEndTimeUs             = 4,
            renderSubmitStartTimeUs  = 5,
            renderSubmitEndTimeUs    = 6,
            presentStartTimeUs       = 7,
            presentEndTimeUs         = 8,
            driverStartTimeUs        = 9,
            driverEndTimeUs          = 10,
            osRenderQueueStartTimeUs = 11,
            osRenderQueueEndTimeUs   = 12,
            gpuRenderStartTimeUs     = 13,
            gpuRenderEndTimeUs       = 14,
        };

        ref readonly LatencyTimings t = ref MemoryMarshal.Cast<VkLatencyTimingsFrameReportNV, LatencyTimings>(native)[0];
        Assert.Equal(1ul,  t.FrameId);
        Assert.Equal(2ul,  t.InputSampleTimeUs);
        Assert.Equal(3ul,  t.SimulationStartTimeUs);
        Assert.Equal(4ul,  t.SimulationEndTimeUs);
        Assert.Equal(5ul,  t.RenderSubmitStartTimeUs);
        Assert.Equal(6ul,  t.RenderSubmitEndTimeUs);
        Assert.Equal(7ul,  t.PresentStartTimeUs);
        Assert.Equal(8ul,  t.PresentEndTimeUs);
        Assert.Equal(9ul,  t.DriverStartTimeUs);
        Assert.Equal(10ul, t.DriverEndTimeUs);
        Assert.Equal(11ul, t.OsRenderQueueStartTimeUs);
        Assert.Equal(12ul, t.OsRenderQueueEndTimeUs);
        Assert.Equal(13ul, t.GpuRenderStartTimeUs);
        Assert.Equal(14ul, t.GpuRenderEndTimeUs);
    }
}
