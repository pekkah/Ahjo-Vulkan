using Xunit;

namespace Ahjo.Vulkan.Tests;

/// <summary>Issue #261: the three <see cref="LatencySleepMode"/> presets. Driverless.</summary>
public sealed class LatencySleepModeTests
{
    [Fact]
    public void Off_IsAllZero_AndDefault()
    {
        LatencySleepMode off = LatencySleepMode.Off;
        Assert.False(off.LowLatency);
        Assert.False(off.Boost);
        Assert.Equal(0u, off.MinimumIntervalUs);
        Assert.Equal(default(LatencySleepMode), off);
    }

    [Fact]
    public void On_IsLowLatencyOnly()
    {
        LatencySleepMode on = LatencySleepMode.On;
        Assert.True(on.LowLatency);
        Assert.False(on.Boost);
        Assert.Equal(0u, on.MinimumIntervalUs);
    }

    [Fact]
    public void OnPlusBoost_IsLowLatencyAndBoost()
    {
        LatencySleepMode b = LatencySleepMode.OnPlusBoost;
        Assert.True(b.LowLatency);
        Assert.True(b.Boost);
        Assert.Equal(0u, b.MinimumIntervalUs);
        Assert.NotEqual(LatencySleepMode.On, b);
    }
}
