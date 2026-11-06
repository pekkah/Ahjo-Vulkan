using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Ngx.Tests;

/// <summary>
/// The always-on checks <see cref="NgxContext.CreateFrameGeneration"/> runs on
/// its description before touching NGX, driven through the device-free
/// <c>ValidateFrameGenerationDescription</c> seam.
/// </summary>
public sealed class DlssFrameGenerationDescriptionTests
{
    private static DlssFrameGenerationDescription Valid() => new()
    {
        Width            = 1920,
        Height           = 1080,
        BackbufferFormat = VkFormat.VK_FORMAT_R8G8B8A8_UNORM,
        RenderWidth      = 1920,
        RenderHeight     = 1080,
    };

    private static ArgumentException AssertThrows(DlssFrameGenerationDescription description)
        => Assert.Throws<ArgumentException>(() => NgxContext.ValidateFrameGenerationDescription(in description));

    [Fact]
    public void Unorm_Passes() => NgxContext.ValidateFrameGenerationDescription(Valid());

    [Fact]
    public void ZeroWidth_Throws()
    {
        ArgumentException ex = AssertThrows(Valid() with { Width = 0 });
        Assert.Contains("0x1080", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ZeroRenderHeight_Throws()
    {
        ArgumentException ex = AssertThrows(Valid() with { RenderHeight = 0 });
        Assert.Contains("render extent", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UndefinedFormat_Throws()
    {
        ArgumentException ex = AssertThrows(Valid() with { BackbufferFormat = VkFormat.VK_FORMAT_UNDEFINED });
        Assert.Contains("VK_FORMAT_UNDEFINED", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SrgbFormat_Throws_AndPointsAtUnorm()
    {
        ArgumentException ex = AssertThrows(Valid() with { BackbufferFormat = VkFormat.VK_FORMAT_B8G8R8A8_SRGB });
        Assert.Contains("UNORM", ex.Message, StringComparison.Ordinal);
        Assert.Contains("docs/ngx-notes.md", ex.Message, StringComparison.Ordinal);
    }
}
