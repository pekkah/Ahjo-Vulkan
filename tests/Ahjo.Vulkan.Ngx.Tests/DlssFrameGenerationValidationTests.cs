using System.Numerics;
using Ahjo.Vulkan.Native;
using Xunit;

namespace Ahjo.Vulkan.Ngx.Tests;

/// <summary>
/// The <see cref="AhjoValidation.Enabled"/>-gated checks
/// <see cref="DlssFrameGenerationFeature.Evaluate"/> runs before it touches
/// NGX, plus the inputs' valid-by-default construction.
/// </summary>
/// <remarks>
/// <para>Drives the static <c>ValidateInputs</c> seam over
/// <c>NgxImage.FromRaw</c> handles, so every branch is provable with no NGX, no
/// NVIDIA driver and no Vulkan device — the <see cref="DlssValidationTests"/>
/// pattern.</para>
/// <para><see cref="AhjoValidation.Enabled"/> is a process-wide flag, so each
/// test restores it in a <c>finally</c>.</para>
/// </remarks>
public sealed class DlssFrameGenerationValidationTests
{
    private const uint Width  = 1920;
    private const uint Height = 1080;
    private const uint MaxGeneratedFrames = 1;

    private const VkFormat ColorFormat = VkFormat.VK_FORMAT_R8G8B8A8_UNORM;

    private static NgxImage Slot(uint width, uint height, ImageUsage usage, nint handle, VkFormat format = ColorFormat)
        => NgxImage.FromRaw(image: handle, view: handle, width, height, usage, format);

    private static NgxImage Input(nint handle) => Slot(Width, Height, ImageUsage.Sampled, handle);
    private static NgxImage Output(nint handle) => Slot(Width, Height, ImageUsage.Storage, handle);

    /// <summary>Every slot bound, every value valid.</summary>
    private static DlssFrameGenerationInputs Complete() => new()
    {
        Backbuffer         = Input(1),
        Depth              = Slot(Width, Height, ImageUsage.Sampled, 2, VkFormat.VK_FORMAT_D32_SFLOAT),
        MotionVectors      = Slot(Width, Height, ImageUsage.Sampled, 3, VkFormat.VK_FORMAT_R16G16_SFLOAT),
        Hudless            = Input(4),
        UiColorAndAlpha    = Slot(Width, Height, ImageUsage.Sampled, 5, VkFormat.VK_FORMAT_R16G16B16A16_SFLOAT),
        OutputInterpolated = Output(6),
        OutputReal         = Output(7),
    };

    private static void Validate(
        in DlssFrameGenerationInputs inputs,
        uint maxGeneratedFrames = MaxGeneratedFrames,
        uint renderWidth = Width,
        uint renderHeight = Height,
        bool dynamicResolution = false)
        => DlssFrameGenerationFeature.ValidateInputs(
            in inputs, Width, Height, ColorFormat, renderWidth, renderHeight, dynamicResolution, maxGeneratedFrames);

    private static void AssertFails(string expectedFragment, DlssFrameGenerationInputs inputs, uint maxGeneratedFrames = MaxGeneratedFrames)
    {
        bool previous = AhjoValidation.Enabled;
        AhjoValidation.Enabled = true;
        try
        {
            var ex = Assert.Throws<AhjoValidationException>(() => Validate(in inputs, maxGeneratedFrames));
            Assert.Contains(expectedFragment, ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            AhjoValidation.Enabled = previous;
        }
    }

    private static void AssertPasses(DlssFrameGenerationInputs inputs, uint renderWidth = Width, uint renderHeight = Height, bool dynamicResolution = false)
    {
        bool previous = AhjoValidation.Enabled;
        AhjoValidation.Enabled = true;
        try
        {
            Validate(in inputs, MaxGeneratedFrames, renderWidth, renderHeight, dynamicResolution);
        }
        finally
        {
            AhjoValidation.Enabled = previous;
        }
    }

    /// <summary>
    /// Runs the validation with a capturing sink and returns every warning
    /// reported; asserts nothing failed.
    /// </summary>
    private static List<string> CaptureWarnings(DlssFrameGenerationInputs inputs)
    {
        var warnings = new List<string>();
        bool previous = AhjoValidation.Enabled;
        DiagnosticSink previousSink = AhjoDiagnostics.Sink;
        AhjoValidation.Enabled = true;
        AhjoDiagnostics.Sink = (severity, _, message) =>
        {
            if (severity == DiagnosticSeverity.Warning) warnings.Add(message);
        };
        try
        {
            Validate(in inputs);
        }
        finally
        {
            AhjoDiagnostics.Sink = previousSink;
            AhjoValidation.Enabled = previous;
        }
        return warnings;
    }

    [Fact]
    public void CompleteInputs_Pass() => AssertPasses(Complete());

    [Fact]
    public void RequiredSlotsOnly_Pass()
        => AssertPasses(Complete() with { Hudless = default, UiColorAndAlpha = default, OutputReal = default });

    [Theory]
    [InlineData("Backbuffer")]
    [InlineData("Depth")]
    [InlineData("MotionVectors")]
    [InlineData("OutputInterpolated")]
    public void MissingRequiredSlot_Fails_AndNamesTheSlot(string slot)
    {
        DlssFrameGenerationInputs inputs = Complete();
        inputs = slot switch
        {
            "Backbuffer"    => inputs with { Backbuffer = default },
            "Depth"         => inputs with { Depth = default },
            "MotionVectors" => inputs with { MotionVectors = default },
            _               => inputs with { OutputInterpolated = default },
        };

        AssertFails($"DlssFrameGenerationInputs.{slot} is not set", inputs);
    }

    [Fact]
    public void ZeroExtentHudless_Fails_AndNamesTheSlot()
    {
        DlssFrameGenerationInputs inputs = Complete() with { Hudless = Slot(0, 0, ImageUsage.Sampled, 4) };

        AssertFails("DlssFrameGenerationInputs.Hudless", inputs);
        AssertFails("0x0 extent", inputs);
    }

    [Fact]
    public void UndefinedFormatOutputReal_Fails_AndNamesTheSlot()
    {
        DlssFrameGenerationInputs inputs = Complete() with
        {
            OutputReal = Slot(Width, Height, ImageUsage.Storage, 7, VkFormat.VK_FORMAT_UNDEFINED),
        };

        AssertFails("DlssFrameGenerationInputs.OutputReal", inputs);
        AssertFails("VK_FORMAT_UNDEFINED", inputs);
    }

    [Fact]
    public void BackbufferExtentDifferentFromFeature_Fails()
    {
        DlssFrameGenerationInputs inputs = Complete() with
        {
            Backbuffer         = Slot(1280, 720, ImageUsage.Sampled, 1),
            OutputInterpolated = Slot(1280, 720, ImageUsage.Storage, 6),
            OutputReal         = Slot(1280, 720, ImageUsage.Storage, 7),
        };

        AssertFails("DlssFrameGenerationInputs.Backbuffer", inputs);
        AssertFails("recreate the feature on resize", inputs);
    }

    [Fact]
    public void BackbufferFormatDifferentFromFeature_Fails()
    {
        const VkFormat Other = VkFormat.VK_FORMAT_B8G8R8A8_UNORM;
        DlssFrameGenerationInputs inputs = Complete() with
        {
            Backbuffer         = Slot(Width, Height, ImageUsage.Sampled, 1, Other),
            OutputInterpolated = Slot(Width, Height, ImageUsage.Storage, 6, Other),
            OutputReal         = Slot(Width, Height, ImageUsage.Storage, 7, Other),
        };

        AssertFails("DlssFrameGenerationInputs.Backbuffer is VK_FORMAT_B8G8R8A8_UNORM", inputs);
        AssertFails("recreate the feature", inputs);
    }

    [Theory]
    [InlineData("Depth")]
    [InlineData("MotionVectors")]
    public void RenderExtentMismatch_WithoutDynamicResolution_Fails(string slot)
    {
        DlssFrameGenerationInputs inputs = Complete();
        inputs = slot == "Depth"
            ? inputs with { Depth = Slot(960, 540, ImageUsage.Sampled, 2, VkFormat.VK_FORMAT_D32_SFLOAT) }
            : inputs with { MotionVectors = Slot(960, 540, ImageUsage.Sampled, 3, VkFormat.VK_FORMAT_R16G16_SFLOAT) };

        AssertFails($"DlssFrameGenerationInputs.{slot} is 960x540", inputs);
        AssertFails("DLSSG.InternalWidth/Height", inputs);
    }

    [Fact]
    public void RenderExtentBelowBackbuffer_MatchingFeature_Passes()
    {
        // The SR-then-FG shape: depth and motion vectors at the render extent,
        // backbuffer (and Hudless/UI, which are not render-extent checked) at
        // the output extent.
        DlssFrameGenerationInputs inputs = Complete() with
        {
            Depth         = Slot(960, 540, ImageUsage.Sampled, 2, VkFormat.VK_FORMAT_D32_SFLOAT),
            MotionVectors = Slot(960, 540, ImageUsage.Sampled, 3, VkFormat.VK_FORMAT_R16G16_SFLOAT),
        };

        AssertPasses(inputs, renderWidth: 960, renderHeight: 540);
    }

    [Fact]
    public void RenderExtentMismatch_WithDynamicResolution_Passes()
    {
        DlssFrameGenerationInputs inputs = Complete() with
        {
            Depth = Slot(960, 540, ImageUsage.Sampled, 2, VkFormat.VK_FORMAT_D32_SFLOAT),
        };

        AssertPasses(inputs, dynamicResolution: true);
    }

    [Fact]
    public void OutputRealBound_BackbufferWithoutTransferSrc_WarnsButPasses()
    {
        List<string> warnings = CaptureWarnings(Complete());

        string warning = Assert.Single(warnings);
        Assert.Contains("VUID-vkCmdCopyImage-aspect-06662", warning, StringComparison.Ordinal);
        Assert.Contains("ImageUsage.TransferSrc", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void BackbufferWithTransferSrc_DoesNotWarn()
    {
        DlssFrameGenerationInputs inputs = Complete() with
        {
            Backbuffer = Slot(Width, Height, ImageUsage.Sampled | ImageUsage.TransferSrc, 1),
        };

        Assert.Empty(CaptureWarnings(inputs));
    }

    [Fact]
    public void OutputRealUnbound_DoesNotWarnAboutTransferSrc()
        => Assert.Empty(CaptureWarnings(Complete() with { OutputReal = default }));

    [Fact]
    public void UnknownBackbufferUsage_DoesNotWarnAboutTransferSrc()
        => Assert.Empty(CaptureWarnings(Complete() with { Backbuffer = Slot(Width, Height, ImageUsage.None, 1) }));

    [Fact]
    public void OutputInterpolatedFormatDifferentFromBackbuffer_Fails()
    {
        DlssFrameGenerationInputs inputs = Complete() with
        {
            OutputInterpolated = Slot(Width, Height, ImageUsage.Storage, 6, VkFormat.VK_FORMAT_R16G16B16A16_SFLOAT),
        };

        AssertFails("DlssFrameGenerationInputs.OutputInterpolated", inputs);
        AssertFails("same size and format", inputs);
    }

    [Fact]
    public void OutputRealExtentDifferentFromBackbuffer_Fails()
    {
        DlssFrameGenerationInputs inputs = Complete() with { OutputReal = Slot(1280, 720, ImageUsage.Storage, 7) };

        AssertFails("DlssFrameGenerationInputs.OutputReal", inputs);
        AssertFails("same size and format", inputs);
    }

    [Fact]
    public void OutputWithoutStorageUsage_Fails_AndNamesTheMissingBit()
    {
        DlssFrameGenerationInputs inputs = Complete() with
        {
            OutputInterpolated = Slot(Width, Height, ImageUsage.ColorAttachment, 6),
        };

        AssertFails("DlssFrameGenerationInputs.OutputInterpolated", inputs);
        AssertFails("ImageUsage.Storage", inputs);
    }

    [Fact]
    public void OutputRealWithoutStorageUsage_Fails_AndHintsTransferDst()
    {
        DlssFrameGenerationInputs inputs = Complete() with
        {
            OutputReal = Slot(Width, Height, ImageUsage.ColorAttachment, 7),
        };

        AssertFails("DlssFrameGenerationInputs.OutputReal", inputs);
        AssertFails("ImageUsage.TransferDst as well", inputs);
    }

    [Fact]
    public void InputWithoutSampledUsage_Fails_AndNamesTheSlot()
    {
        DlssFrameGenerationInputs inputs = Complete() with
        {
            Depth = Slot(Width, Height, ImageUsage.DepthStencilAttachment, 2, VkFormat.VK_FORMAT_D32_SFLOAT),
        };

        AssertFails("DlssFrameGenerationInputs.Depth", inputs);
        AssertFails("ImageUsage.Sampled", inputs);
    }

    /// <summary>
    /// <see cref="ImageUsage.None"/> is <c>Image.FromRaw</c>'s "unknown" state,
    /// not "no usage bits", so it is skipped rather than failed — on inputs and
    /// outputs alike.
    /// </summary>
    [Fact]
    public void UnknownUsage_IsSkippedRatherThanFailed()
    {
        DlssFrameGenerationInputs inputs = Complete() with
        {
            Backbuffer         = Slot(Width, Height, ImageUsage.None, 1),
            OutputInterpolated = Slot(Width, Height, ImageUsage.None, 6),
        };

        AssertPasses(inputs);
    }

    [Fact]
    public void MultiFrameIndexZero_Fails()
    {
        DlssFrameGenerationInputs inputs = Complete() with { MultiFrameIndex = 0 };

        AssertFails("DlssFrameGenerationInputs.MultiFrameIndex is 0", inputs);
    }

    [Fact]
    public void MultiFrameIndexAboveCount_Fails()
    {
        DlssFrameGenerationInputs inputs = Complete() with { MultiFrameCount = 1, MultiFrameIndex = 2 };

        AssertFails("DlssFrameGenerationInputs.MultiFrameIndex is 2", inputs);
        AssertFails("MultiFrameCount = 1", inputs);
    }

    [Fact]
    public void MultiFrameCountAboveDeviceMax_Fails_AndSaysRtx40IsTwoXOnly()
    {
        DlssFrameGenerationInputs inputs = Complete() with { MultiFrameCount = 2 };

        AssertFails("DlssFrameGenerationInputs.MultiFrameCount is 2", inputs, maxGeneratedFrames: 1);
        AssertFails("RTX 40-series GPUs support 2x", inputs, maxGeneratedFrames: 1);
    }

    [Fact]
    public void Defaults_AreValidByConstruction()
    {
        var inputs = new DlssFrameGenerationInputs();

        Assert.Equal(1f, inputs.MotionVectorScaleX);
        Assert.Equal(1f, inputs.MotionVectorScaleY);
        Assert.Equal(1u, inputs.MultiFrameCount);
        Assert.Equal(1u, inputs.MultiFrameIndex);
        Assert.True(inputs.CameraMotionIncluded);
        Assert.Equal(Matrix4x4.Identity, inputs.Camera.ClipToLensClip);
        Assert.Equal(Matrix4x4.Identity, new DlssFrameGenerationCamera().ClipToLensClip);
    }
}
