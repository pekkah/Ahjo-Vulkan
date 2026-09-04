using Xunit;

namespace Ahjo.Vulkan.Ngx.Tests;

/// <summary>
/// The availability semantics of <see cref="DlssOptimalSettings"/>, over the
/// internal projection seam so they are provable with no device.
/// </summary>
/// <remarks>
/// A 0×0 render extent is NGX saying "this mode is not offered on this GPU",
/// not a size. Reporting it as a size would invite a caller who ignores
/// <see cref="DlssOptimalSettings.IsAvailable"/> to allocate a zero-extent
/// render target (spec D8).
/// </remarks>
public sealed class DlssOptimalSettingsTests
{
    [Fact]
    public void ZeroRenderExtent_ReportsUnavailable_WithEveryDimensionZero()
    {
        DlssOptimalSettings settings = NgxContext.ProjectOptimalSettings(
            renderWidth: 0, renderHeight: 0,
            minWidth: 1280, minHeight: 720,
            maxWidth: 2560, maxHeight: 1440);

        Assert.False(settings.IsAvailable);
        Assert.Equal(0u, settings.RenderWidth);
        Assert.Equal(0u, settings.RenderHeight);
        // The min/max NGX happened to leave in the map do NOT leak through.
        Assert.Equal(0u, settings.MinRenderWidth);
        Assert.Equal(0u, settings.MinRenderHeight);
        Assert.Equal(0u, settings.MaxRenderWidth);
        Assert.Equal(0u, settings.MaxRenderHeight);
    }

    [Theory]
    [InlineData(0u, 1440u)]
    [InlineData(2560u, 0u)]
    public void EitherDimensionZero_ReportsUnavailable(uint width, uint height)
    {
        DlssOptimalSettings settings = NgxContext.ProjectOptimalSettings(
            width, height, minWidth: 1, minHeight: 1, maxWidth: 4, maxHeight: 4);

        Assert.False(settings.IsAvailable);
    }

    [Fact]
    public void NonZeroRenderExtent_ReportsAvailable_WithAllSixValues()
    {
        DlssOptimalSettings settings = NgxContext.ProjectOptimalSettings(
            renderWidth: 2560, renderHeight: 1440,
            minWidth: 1280, minHeight: 720,
            maxWidth: 3840, maxHeight: 2160);

        Assert.True(settings.IsAvailable);
        Assert.Equal(2560u, settings.RenderWidth);
        Assert.Equal(1440u, settings.RenderHeight);
        Assert.Equal(1280u, settings.MinRenderWidth);
        Assert.Equal(720u, settings.MinRenderHeight);
        Assert.Equal(3840u, settings.MaxRenderWidth);
        Assert.Equal(2160u, settings.MaxRenderHeight);
    }
}
