using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Ngx.Tests;

/// <summary>
/// The <see cref="AhjoValidation.Enabled"/>-gated checks
/// <see cref="DlssFeature.Evaluate"/> runs before it touches NGX.
/// </summary>
/// <remarks>
/// <para>Drives the static <c>ValidateInputs</c> seam over
/// <c>NgxImage.FromRaw</c> handles, so every branch is provable with no NGX, no
/// NVIDIA driver and no Vulkan device (spec D13). A real
/// <see cref="DlssFeature"/> needs all three.</para>
/// <para><see cref="AhjoValidation.Enabled"/> is a process-wide flag, so each
/// test restores it in a <c>finally</c>: leaving it on would silently change
/// the cost model of every later test in the run, and leaving it off would make
/// these tests pass vacuously.</para>
/// </remarks>
public sealed class DlssValidationTests
{
    private const uint MinRenderWidth  = 1280;
    private const uint MinRenderHeight = 720;
    private const uint MaxRenderWidth  = 2560;
    private const uint MaxRenderHeight = 1440;

    /// <summary>A bound slot with a plausible extent and the usage its slot wants.</summary>
    private static NgxImage Slot(uint width, uint height, ImageUsage usage, nint handle,
                                 VkFormat format = VkFormat.VK_FORMAT_R16G16B16A16_SFLOAT)
        => NgxImage.FromRaw(image: handle, view: handle, width, height, usage, format);

    private static NgxImage Input(nint handle) => Slot(2560, 1440, ImageUsage.Sampled, handle);
    private static NgxImage Output(nint handle) => Slot(3840, 2160, ImageUsage.Storage, handle);

    private static DlssEvaluateInputs Complete() => new()
    {
        Color         = Input(1),
        Depth         = Input(2),
        MotionVectors = Input(3),
        Output        = Output(4),
        RenderWidth   = 2560,
        RenderHeight  = 1440,
    };

    private static void Validate(in DlssEvaluateInputs inputs)
        => DlssFeature.ValidateInputs(in inputs, MinRenderWidth, MinRenderHeight, MaxRenderWidth, MaxRenderHeight);

    /// <summary>
    /// Runs <paramref name="body"/> with validation forced on, and asserts it
    /// throws with a message containing <paramref name="expectedFragment"/>.
    /// </summary>
    private static void AssertFails(string expectedFragment, DlssEvaluateInputs inputs)
    {
        bool previous = AhjoValidation.Enabled;
        AhjoValidation.Enabled = true;
        try
        {
            var ex = Assert.Throws<AhjoValidationException>(() => Validate(in inputs));
            Assert.Contains(expectedFragment, ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            AhjoValidation.Enabled = previous;
        }
    }

    private static void AssertPasses(DlssEvaluateInputs inputs)
    {
        bool previous = AhjoValidation.Enabled;
        AhjoValidation.Enabled = true;
        try
        {
            Validate(in inputs);
        }
        finally
        {
            AhjoValidation.Enabled = previous;
        }
    }

    [Fact]
    public void CompleteInputs_Pass() => AssertPasses(Complete());

    [Theory]
    [InlineData("Color")]
    [InlineData("Depth")]
    [InlineData("MotionVectors")]
    [InlineData("Output")]
    public void MissingRequiredSlot_Fails_AndNamesTheSlot(string slot)
    {
        DlssEvaluateInputs inputs = Complete();
        inputs = slot switch
        {
            "Color"         => inputs with { Color = default },
            "Depth"         => inputs with { Depth = default },
            "MotionVectors" => inputs with { MotionVectors = default },
            _               => inputs with { Output = default },
        };

        AssertFails($"DlssEvaluateInputs.{slot} is not set", inputs);
    }

    [Fact]
    public void OptionalSlotsMayBeAbsent()
    {
        DlssEvaluateInputs inputs = Complete();
        Assert.True(inputs.ExposureTexture.IsNull);
        Assert.True(inputs.BiasCurrentColorMask.IsNull);
        AssertPasses(inputs);
    }

    [Fact]
    public void OutputBelowThirtyTwoSquare_Fails()
    {
        DlssEvaluateInputs inputs = Complete() with
        {
            Output = Slot(31, 31, ImageUsage.Storage, 4),
        };

        AssertFails("at least 32x32", inputs);
    }

    [Fact]
    public void RenderWidthOutsideDynamicRange_Fails_AndQuotesAllThreeNumbers()
    {
        DlssEvaluateInputs inputs = Complete() with { RenderWidth = MaxRenderWidth + 1 };

        AssertFails($"is {MaxRenderWidth + 1}", inputs);
        AssertFails($"[{MinRenderWidth}, {MaxRenderWidth}]", inputs);
    }

    [Fact]
    public void RenderHeightBelowDynamicRange_Fails()
    {
        DlssEvaluateInputs inputs = Complete() with { RenderHeight = MinRenderHeight - 1 };

        AssertFails($"[{MinRenderHeight}, {MaxRenderHeight}]", inputs);
    }

    [Fact]
    public void OutputWithoutStorageUsage_Fails_AndNamesTheMissingBit()
    {
        DlssEvaluateInputs inputs = Complete() with
        {
            Output = Slot(3840, 2160, ImageUsage.ColorAttachment, 4),
        };

        AssertFails("ImageUsage.Storage", inputs);
        AssertFails("DlssEvaluateInputs.Output", inputs);
    }

    [Fact]
    public void InputWithoutSampledUsage_Fails_AndNamesTheSlot()
    {
        DlssEvaluateInputs inputs = Complete() with
        {
            Depth = Slot(2560, 1440, ImageUsage.DepthStencilAttachment, 2),
        };

        AssertFails("DlssEvaluateInputs.Depth", inputs);
        AssertFails("ImageUsage.Sampled", inputs);
    }

    /// <summary>
    /// <see cref="ImageUsage.None"/> is <c>Image.FromRaw</c>'s "unknown" state
    /// (spec E3), not "no usage bits". Treating it as a missing bit would
    /// reject every swapchain-owned image — the exact case this carve-out
    /// exists for.
    /// </summary>
    [Fact]
    public void UnknownUsage_IsSkippedRatherThanFailed()
    {
        DlssEvaluateInputs inputs = Complete() with
        {
            Color  = Slot(2560, 1440, ImageUsage.None, 1),
            Output = Slot(3840, 2160, ImageUsage.None, 4),
        };

        AssertPasses(inputs);
    }

    /// <summary>
    /// Extent is <b>not</b> covered by the same carve-out, and the difference is
    /// the point: NGX reads <c>Width</c>/<c>Height</c> straight out of
    /// <c>NVSDK_NGX_ImageViewInfo_VK</c>, so an <c>Image.FromRaw</c> handle's
    /// 0×0 is a silent wrong answer rather than a harmless unknown. It fails.
    /// </summary>
    [Fact]
    public void UnknownExtent_Fails_AndNamesTheCause()
    {
        DlssEvaluateInputs inputs = Complete() with
        {
            Output = Slot(0, 0, ImageUsage.Storage, 4),
        };

        AssertFails("0x0 extent", inputs);
        AssertFails("Image.FromRaw", inputs);
    }

    /// <summary>
    /// Same reasoning for format: NGX validates it against the formats DLSS
    /// supports, so <c>VK_FORMAT_UNDEFINED</c> cannot be forwarded.
    /// </summary>
    [Fact]
    public void UndefinedFormat_Fails()
    {
        DlssEvaluateInputs inputs = Complete() with
        {
            Depth = Slot(2560, 1440, ImageUsage.Sampled, 2, VkFormat.VK_FORMAT_UNDEFINED),
        };

        AssertFails("VK_FORMAT_UNDEFINED", inputs);
        AssertFails("DlssEvaluateInputs.Depth", inputs);
    }
}
